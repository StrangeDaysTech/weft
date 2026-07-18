using System.Buffers;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Weft;
using Weft.Concurrency;
using Weft.Server.Auth;
using Weft.Server.Persistence;
using Weft.Server.Protocol;
using Weft.Versioning;
using Weft.Versioning.Blobs;
using Weft.Yrs;

namespace Weft.Server.Tests;

/// <summary>
/// Tests de integración del relay end-to-end (T051): un <see cref="TestServer"/> hospeda el relay y clientes
/// Yjs <b>simulados</b> (motor yrs real en ambos lados, hablando el wire y-sync vía <see cref="SyncProtocol"/>)
/// se conectan por WebSocket. Cubre los criterios del Independent Test de US3.
/// </summary>
public sealed class RelayTests
{
    private const string Field = "content";

    // ---------- Harness ----------

    // Concede el acceso `fallback`, salvo que la conexión pida uno explícito por query (?access=ro|rw|deny) —
    // esto permite mezclar un escritor ReadWrite y un lector ReadOnly sobre el mismo documento en un test.
    private sealed class FixedAuthorizer(WeftAccess fallback) : IWeftAuthorizer
    {
        public ValueTask<WeftAccess> AuthorizeAsync(HttpContext context, string docId, CancellationToken ct)
        {
            WeftAccess access = context.Request.Query["access"].ToString() switch
            {
                "ro" => WeftAccess.ReadOnly,
                "rw" => WeftAccess.ReadWrite,
                "deny" => WeftAccess.Deny,
                _ => fallback,
            };
            return ValueTask.FromResult(access);
        }
    }

    private static async Task<IHost> BuildHostAsync(
        WeftAccess access,
        IDocumentStore store,
        IBlobStore? blobs = null,
        DurabilityMode durability = DurabilityMode.PersistThenBroadcast)
    {
        IHostBuilder builder = new HostBuilder().ConfigureWebHost(web =>
        {
            web.UseTestServer();
            web.ConfigureServices(services =>
            {
                services.AddRouting();
                services.AddWeftServer(o =>
                {
                    o.Broker = new DocumentBrokerOptions { IdleSweepInterval = TimeSpan.FromMilliseconds(50) };
                    o.Durability = durability;
                });
                services.AddSingleton<IWeftAuthorizer>(new FixedAuthorizer(access));
                services.AddSingleton<IDocumentStore>(store);
                if (blobs is not null)
                {
                    services.AddSingleton<IBlobStore>(blobs);
                }
            });
            web.Configure(app =>
            {
                app.UseWebSockets();
                app.UseRouting();
                app.UseEndpoints(e => e.MapWeft("/collab"));
            });
        });
        return await builder.StartAsync();
    }

    /// <summary>Host del relay con async-dispose (el <c>IHost</c> concreto es IAsyncDisposable; el estático no).</summary>
    private sealed class RelayHost(IHost host) : IAsyncDisposable
    {
        public TestServer Server { get; } = host.GetTestServer();
        public IServiceProvider Services => host.Services;

        public async ValueTask DisposeAsync()
        {
            if (host is IAsyncDisposable ad)
            {
                await ad.DisposeAsync();
            }
            else
            {
                host.Dispose();
            }
        }
    }

    private static async Task<RelayHost> StartRelayAsync(
        WeftAccess access,
        IDocumentStore store,
        IBlobStore? blobs = null,
        DurabilityMode durability = DurabilityMode.PersistThenBroadcast)
        => new RelayHost(await BuildHostAsync(access, store, blobs, durability));

    // El timeout es una cota SUPERIOR generosa para absorber la contención del runner de CI (p. ej. macOS
    // lento), NO el objetivo de latencia de convergencia de US3 (SC-005 <1 s); la convergencia real es
    // sub-segundo (verificada por el check headless y las corridas locales).
    private static async Task<bool> WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < timeout)
        {
            if (condition())
            {
                return true;
            }

            await Task.Delay(15);
        }

        return condition();
    }

    private static byte[] AwarenessUpdate(uint clientId, uint clock, string stateJson)
    {
        var inner = new Lib0Encoding.Lib0Writer();
        inner.WriteVarUint(1);
        inner.WriteVarUint(clientId);
        inner.WriteVarUint(clock);
        inner.WriteVarUint8Array(Encoding.UTF8.GetBytes(stateJson));
        return SyncProtocol.EncodeAwareness(inner.WrittenSpan);
    }

    /// <summary>Cliente Yjs simulado: WebSocket + un doc yrs real, hablando y-sync.</summary>
    private sealed class YClient : IAsyncDisposable
    {
        private readonly WebSocket _ws;
        private readonly ICrdtDoc _doc = YrsEngine.Instance.CreateDoc();
        private readonly object _docLock = new();
        private readonly SemaphoreSlim _sendLock = new(1, 1);
        private readonly CancellationTokenSource _cts = new();
        private readonly Task _recv;

        public long BytesReceived;
        public WebSocketCloseStatus? CloseStatus { get; private set; }
        public List<byte[]> AwarenessReceived { get; } = new();

        private YClient(WebSocket ws)
        {
            _ws = ws;
            _recv = Task.Run(ReceiveLoopAsync);
        }

        public static async Task<YClient> ConnectAsync(
            TestServer server, string docId, byte[]? seedState = null, string? access = null, CancellationToken ct = default)
        {
            WebSocketClient wsc = server.CreateWebSocketClient();
            string query = access is null ? "" : $"?access={access}";
            WebSocket ws = await wsc.ConnectAsync(new Uri(server.BaseAddress, $"collab/{docId}{query}"), ct);
            var client = new YClient(ws);
            // Sync inicial: anunciamos nuestro state vector (tras sembrar el estado previo, si lo hay).
            byte[] sv;
            lock (client._docLock)
            {
                if (seedState is not null)
                {
                    client._doc.ApplyUpdate(seedState);
                }

                sv = client._doc.ExportStateVector();
            }

            await client.SendAsync(SyncProtocol.EncodeSyncStep1(sv), ct);
            return client;
        }

        public string Text()
        {
            lock (_docLock) { return _doc.GetText(Field); }
        }

        public byte[] ExportState()
        {
            lock (_docLock) { return _doc.ExportState(); }
        }

        /// <summary>Edita localmente y difunde el delta al servidor.</summary>
        public async Task EditAsync(int index, string text, CancellationToken ct = default)
        {
            byte[] delta;
            lock (_docLock)
            {
                byte[] before = _doc.ExportStateVector();
                _doc.InsertText(Field, index, text);
                delta = _doc.ExportUpdateSince(before);
            }

            await SendAsync(SyncProtocol.EncodeUpdate(delta), ct);
        }

        /// <summary>Envía un estado/update crudo como mensaje Update (sin aplicarlo localmente).</summary>
        public Task SendUpdateAsync(byte[] update, CancellationToken ct = default)
            => SendAsync(SyncProtocol.EncodeUpdate(update), ct);

        public Task SendAwarenessAsync(byte[] awarenessMessage, CancellationToken ct = default)
            => SendAsync(awarenessMessage, ct);

        private async Task SendAsync(byte[] frame, CancellationToken ct)
        {
            await _sendLock.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                await _ws.SendAsync(frame, WebSocketMessageType.Binary, endOfMessage: true, ct).ConfigureAwait(false);
            }
            finally
            {
                _sendLock.Release();
            }
        }

        private async Task ReceiveLoopAsync()
        {
            try
            {
                while (!_cts.IsCancellationRequested && _ws.State == WebSocketState.Open)
                {
                    byte[]? msg = await ReceiveFullAsync(_cts.Token).ConfigureAwait(false);
                    if (msg is null)
                    {
                        return;
                    }

                    Interlocked.Add(ref BytesReceived, msg.Length);
                    Dispatch(msg);
                }
            }
            catch (OperationCanceledException) { }
            catch (WebSocketException) { }
        }

        private void Dispatch(byte[] frame)
        {
            MessageType type;
            SyncMessageType syncType;
            byte[] payload;
            try
            {
                SyncMessage m = SyncProtocol.Decode(frame);
                type = m.Type;
                syncType = m.SyncType;
                payload = m.Payload.ToArray();
            }
            catch (MalformedMessageException)
            {
                return;
            }

            switch (type)
            {
                case MessageType.Sync when syncType == SyncMessageType.Step1:
                    byte[] delta;
                    lock (_docLock) { delta = _doc.ExportUpdateSince(payload); }
                    _ = SendAsync(SyncProtocol.EncodeSyncStep2(delta), _cts.Token);
                    break;
                case MessageType.Sync: // Step2 / Update
                    lock (_docLock) { _doc.ApplyUpdate(payload); }
                    break;
                case MessageType.Awareness:
                    lock (AwarenessReceived) { AwarenessReceived.Add(payload); }
                    break;
            }
        }

        private async Task<byte[]?> ReceiveFullAsync(CancellationToken ct)
        {
            byte[] rent = ArrayPool<byte>.Shared.Rent(8192);
            var acc = new ArrayBufferWriter<byte>();
            try
            {
                while (true)
                {
                    WebSocketReceiveResult r = await _ws.ReceiveAsync(rent, ct).ConfigureAwait(false);
                    if (r.MessageType == WebSocketMessageType.Close)
                    {
                        CloseStatus = r.CloseStatus;
                        return null;
                    }

                    acc.Write(rent.AsSpan(0, r.Count));
                    if (r.EndOfMessage)
                    {
                        return acc.WrittenSpan.ToArray();
                    }
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(rent);
            }
        }

        public async ValueTask DisposeAsync()
        {
            await _cts.CancelAsync();
            try
            {
                if (_ws.State == WebSocketState.Open)
                {
                    await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None);
                }
            }
            catch { }

            try { await _recv; } catch { }
            _ws.Dispose();
            _cts.Dispose();
        }
    }

    // ---------- Tests ----------

    [Fact]
    public async Task Two_readwrite_clients_converge_after_cross_edits()
    {
        await using RelayHost relay = await StartRelayAsync(WeftAccess.ReadWrite, new InMemoryDocumentStore());
        TestServer server = relay.Server;
        await using YClient a = await YClient.ConnectAsync(server, "doc");
        await using YClient b = await YClient.ConnectAsync(server, "doc");

        await a.EditAsync(0, "Hello ");
        await b.EditAsync(0, "World");

        bool converged = await WaitUntilAsync(
            () => a.Text().Length == "Hello World".Length && a.Text() == b.Text(),
            TimeSpan.FromSeconds(5));

        Assert.True(converged, $"a='{a.Text()}' b='{b.Text()}'");
        Assert.Contains("Hello", a.Text());
        Assert.Contains("World", a.Text());
    }

    [Fact]
    public async Task Reconnecting_client_receives_only_a_small_delta()
    {
        await using RelayHost relay = await StartRelayAsync(WeftAccess.ReadWrite, new InMemoryDocumentStore());
        TestServer server = relay.Server;

        // Un cliente construye un documento grande y capturamos su estado.
        await using YClient writer = await YClient.ConnectAsync(server, "doc");
        await writer.EditAsync(0, new string('x', 20_000));
        byte[] fullState = writer.ExportState();

        // Cliente FRESCO (SV vacío): el servidor le envía el estado completo (≫20 KB).
        await using YClient fresh = await YClient.ConnectAsync(server, "doc");
        Assert.True(await WaitUntilAsync(() => fresh.Text().Length == 20_000, TimeSpan.FromSeconds(5)),
            $"fresh no sincronizó: len={fresh.Text().Length}");
        Assert.True(fresh.BytesReceived > 20_000, $"fresh={fresh.BytesReceived}");

        // Cliente AL DÍA (sembrado con el estado → SV completo): el servidor no tiene nada nuevo que enviarle.
        await using YClient upToDate = await YClient.ConnectAsync(server, "doc", seedState: fullState);
        await Task.Delay(150); // deja llegar el sync inicial
        Assert.True(upToDate.BytesReceived * 4 < fresh.BytesReceived,
            $"upToDate={upToDate.BytesReceived} fresh={fresh.BytesReceived} (delta en reconexión ≪ estado completo)");
    }

    [Fact]
    public async Task Denied_connection_exchanges_no_content()
    {
        await using RelayHost relay = await StartRelayAsync(WeftAccess.Deny, new InMemoryDocumentStore());
        TestServer server = relay.Server;
        // 403 antes del upgrade → la conexión WebSocket se rechaza (0 bytes de contenido).
        await Assert.ThrowsAnyAsync<Exception>(async () =>
        {
            await using YClient _ = await YClient.ConnectAsync(server, "doc");
        });
    }

    [Fact]
    public async Task ReadOnly_client_receives_updates_survives_handshake_but_closes_on_write()
    {
        // Regresión de F1 (auditoría CHARTER-05): el ReadOnly no debe cerrarse por el SyncStep2 del handshake,
        // solo por un Update en vivo. Sin el fix, el lector se cierra durante el handshake y este test falla.
        await using RelayHost relay = await StartRelayAsync(WeftAccess.ReadWrite, new InMemoryDocumentStore());
        TestServer server = relay.Server;

        await using YClient writer = await YClient.ConnectAsync(server, "doc");                 // ReadWrite (default)
        await using YClient reader = await YClient.ConnectAsync(server, "doc", access: "ro");   // ReadOnly

        // El lector sobrevive el handshake (su SyncStep2 se ignora, no lo cierra) y recibe el update del escritor.
        await writer.EditAsync(0, "shared");
        Assert.True(await WaitUntilAsync(() => reader.Text() == "shared", TimeSpan.FromSeconds(5)),
            $"el lector ReadOnly no recibió el update: text='{reader.Text()}' close={reader.CloseStatus}");
        Assert.Null(reader.CloseStatus); // sigue conectado tras recibir updates

        // Pero si el lector intenta escribir (Update en vivo), se cierra con 1008.
        await reader.EditAsync(0, "nope");
        Assert.True(await WaitUntilAsync(
            () => reader.CloseStatus == WebSocketCloseStatus.PolicyViolation, TimeSpan.FromSeconds(5)),
            $"close={reader.CloseStatus}");
    }

    [Fact]
    public async Task Awareness_is_relayed_and_withdrawn_on_disconnect()
    {
        await using RelayHost relay = await StartRelayAsync(WeftAccess.ReadWrite, new InMemoryDocumentStore());
        TestServer server = relay.Server;
        await using YClient observer = await YClient.ConnectAsync(server, "doc");
        YClient presence = await YClient.ConnectAsync(server, "doc");

        // Liveness: una edición converge en observer → ambas conexiones están unidas al hub antes de difundir
        // awareness (evita la carrera "observer aún no está en el hub" al hacer el broadcast).
        await presence.EditAsync(0, "x");
        Assert.True(await WaitUntilAsync(() => observer.Text() == "x", TimeSpan.FromSeconds(5)));

        const uint clientId = 4242;
        await presence.SendAwarenessAsync(AwarenessUpdate(clientId, 1, "{\"user\":\"A\"}"));

        // El observador ve el estado de awareness del par.
        Assert.True(await WaitUntilAsync(
            () => observer.AwarenessReceived.Any(p => AwarenessHasClient(p, clientId, requireNull: false)),
            TimeSpan.FromSeconds(5)));

        // Al desconectar el par, el observador recibe la RETIRADA (estado "null" para su clientID).
        await presence.DisposeAsync();
        Assert.True(await WaitUntilAsync(
            () => observer.AwarenessReceived.Any(p => AwarenessHasClient(p, clientId, requireNull: true)),
            TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public async Task Awareness_with_zero_clock_for_new_client_does_not_crash()
    {
        // Regresión de F2 (auditoría CHARTER-05): un awareness con clock 0 para un clientID nuevo no debe
        // lanzar KeyNotFoundException en TrackClients (que faultearía la conexión). Se prueba por SUPERVIVENCIA
        // (una edición posterior al awareness aún se relaya), evitando la carrera de "observer aún no está en
        // el hub" con una barrera de liveness previa.
        await using RelayHost relay = await StartRelayAsync(WeftAccess.ReadWrite, new InMemoryDocumentStore());
        TestServer server = relay.Server;
        await using YClient observer = await YClient.ConnectAsync(server, "doc");
        await using YClient presence = await YClient.ConnectAsync(server, "doc");

        // Liveness: una edición de presence converge en observer → ambas conexiones vivas y unidas al hub.
        await presence.EditAsync(0, "live");
        Assert.True(await WaitUntilAsync(() => observer.Text() == "live", TimeSpan.FromSeconds(5)),
            $"no se estableció liveness: observer='{observer.Text()}'");

        // Awareness con clock 0 para un clientID nuevo (común en el primer awareness de un cliente Yjs).
        await presence.SendAwarenessAsync(AwarenessUpdate(777, 0, "{\"user\":\"Z\"}"));

        // La conexión de presence SIGUE viva tras el awareness clock-0: una edición posterior aún se relaya
        // (sin el fix, TrackClients habría lanzado y faulteado la conexión → esta edición nunca llegaría).
        await presence.EditAsync(4, " more");
        Assert.True(await WaitUntilAsync(() => observer.Text() == "live more", TimeSpan.FromSeconds(5)),
            $"presence dejó de relayar tras el awareness clock-0 (¿crasheó?): observer='{observer.Text()}' close={presence.CloseStatus}");
        Assert.Null(presence.CloseStatus);
    }

    [Fact]
    public async Task State_survives_a_server_restart()
    {
        var store = new InMemoryDocumentStore(); // el store durable sobrevive al "reinicio" del proceso

        await using (RelayHost r1 = await StartRelayAsync(WeftAccess.ReadWrite, store))
        {
            TestServer s1 = r1.Server;
            await using YClient c1 = await YClient.ConnectAsync(s1, "doc");
            await c1.EditAsync(0, "durable");
            await using YClient c2 = await YClient.ConnectAsync(s1, "doc");
            Assert.True(await WaitUntilAsync(() => c2.Text() == "durable", TimeSpan.FromSeconds(5)));
        } // r1.DisposeAsync consolida el snapshot en el store (WeftServer.DisposeAsync)

        await using RelayHost r2 = await StartRelayAsync(WeftAccess.ReadWrite, store);
        TestServer s2 = r2.Server;
        await using YClient c3 = await YClient.ConnectAsync(s2, "doc");
        Assert.True(await WaitUntilAsync(() => c3.Text() == "durable", TimeSpan.FromSeconds(5)),
            $"recovered='{c3.Text()}'");
    }

    [Fact]
    public async Task Server_publish_matches_local_publish_version_id()
    {
        var blobs = new InMemoryBlobStore();

        // Publicación local del mismo contenido.
        using ICrdtDoc local = YrsEngine.Instance.CreateDoc();
        local.InsertText(Field, 0, "content-addressed parity");
        byte[] localState = local.ExportState();
        var versionStore = new VersionStore(YrsEngine.Instance, blobs);
        VersionId localId = await versionStore.PublishAsync(local);

        // El servidor recibe exactamente el mismo estado y publica.
        await using RelayHost relay = await StartRelayAsync(WeftAccess.ReadWrite, new InMemoryDocumentStore(), blobs);
        TestServer server = relay.Server;
        await using YClient c = await YClient.ConnectAsync(server, "doc");
        await c.SendUpdateAsync(localState);

        // Esperar a que el servidor aplique el estado (un 2.º cliente converge → el actor ya lo procesó).
        await using YClient probe = await YClient.ConnectAsync(server, "doc");
        Assert.True(await WaitUntilAsync(
            () => probe.Text() == "content-addressed parity", TimeSpan.FromSeconds(5)));

        var weftServer = relay.Services.GetRequiredService<IWeftServer>();
        VersionId serverId = await weftServer.PublishAsync("doc");

        Assert.Equal(localId, serverId);
    }

    // awarenessPayload = el payload interno de un mensaje Awareness (lo que el cliente guarda en Dispatch).
    private static bool AwarenessHasClient(byte[] awarenessPayload, uint clientId, bool requireNull)
    {
        try
        {
            var r = new Lib0Encoding.Lib0Reader(awarenessPayload);
            uint count = r.ReadVarUint();
            for (uint i = 0; i < count; i++)
            {
                uint id = r.ReadVarUint();
                _ = r.ReadVarUint(); // clock
                ReadOnlySpan<byte> state = r.ReadVarUint8Array();
                if (id == clientId)
                {
                    bool isNull = state.SequenceEqual("null"u8);
                    return requireNull ? isNull : !isNull;
                }
            }
        }
        catch (MalformedMessageException) { }

        return false;
    }

    // ---------- Durabilidad del relay (FU-010, CHARTER-14) ----------

    /// <summary>
    /// Store de control para los tests de durabilidad. Cada conexión dispara un append en el handshake
    /// (SyncStep2), así que fallar/bloquear por número de llamada es frágil; en su lugar se «arma» el
    /// próximo append cuando el test lo decide (tras conectar los clientes), para que solo la edición de
    /// interés falle o se bloquee.
    /// </summary>
    private sealed class ControllableAppendStore(IDocumentStore inner) : IDocumentStore
    {
        private int _failNext;
        private TaskCompletionSource? _gate;
        private int _appends;

        public int AppendCount => Volatile.Read(ref _appends);

        /// <summary>El PRÓXIMO append lanzará una vez.</summary>
        public void ArmFailure() => Interlocked.Exchange(ref _failNext, 1);

        /// <summary>Los appends a partir de ahora se bloquean hasta <see cref="Release"/>.</summary>
        public void ArmGate() => _gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        public void Release() => _gate?.TrySetResult();

        public ValueTask<byte[]?> LoadAsync(string docId, CancellationToken ct = default)
            => inner.LoadAsync(docId, ct);

        public async ValueTask AppendUpdateAsync(string docId, ReadOnlyMemory<byte> update, CancellationToken ct = default)
        {
            Interlocked.Increment(ref _appends);

            if (Interlocked.Exchange(ref _failNext, 0) == 1)
            {
                throw new IOException("fallo de append inyectado (test)");
            }

            TaskCompletionSource? gate = _gate;
            if (gate is not null)
            {
                await gate.Task.WaitAsync(ct).ConfigureAwait(false);
            }

            await inner.AppendUpdateAsync(docId, update, ct).ConfigureAwait(false);
        }

        public ValueTask SaveSnapshotAsync(string docId, ReadOnlyMemory<byte> state, CancellationToken ct = default)
            => inner.SaveSnapshotAsync(docId, state, ct);
    }

    // Espera a que los appends del handshake de las conexiones se asienten (dejen de crecer) antes de armar.
    private static async Task SettleAsync(ControllableAppendStore store)
    {
        int last = -1;
        while (last != store.AppendCount)
        {
            last = store.AppendCount;
            await Task.Delay(120);
        }
    }

    [Fact]
    public async Task PersistThenBroadcast_un_append_fallido_no_lo_observan_los_pares_y_cierra_la_conexion()
    {
        var store = new ControllableAppendStore(new InMemoryDocumentStore());
        await using RelayHost relay = await StartRelayAsync(WeftAccess.ReadWrite, store);
        TestServer server = relay.Server;
        await using YClient a = await YClient.ConnectAsync(server, "doc");
        await using YClient b = await YClient.ConnectAsync(server, "doc");

        await SettleAsync(store);
        store.ArmFailure(); // el próximo append (la edición de a) falla
        await a.EditAsync(0, "no debe verse");

        // El par b NUNCA recibe el delta (persist-before-broadcast: el broadcast no ocurrió), y la conexión
        // emisora se cierra con 1011 InternalError.
        bool aClosed = await WaitUntilAsync(
            () => a.CloseStatus == WebSocketCloseStatus.InternalServerError, TimeSpan.FromSeconds(5));
        Assert.True(aClosed, $"a.CloseStatus={a.CloseStatus}");
        Assert.NotEqual("no debe verse", b.Text());
    }

    [Fact]
    public async Task PersistThenBroadcast_reconexion_tras_fallo_resincroniza_el_estado_vivo()
    {
        var store = new ControllableAppendStore(new InMemoryDocumentStore());
        await using RelayHost relay = await StartRelayAsync(WeftAccess.ReadWrite, store);
        TestServer server = relay.Server;

        // El primer cliente edita; su append falla → la edición queda en el doc vivo del servidor pero no
        // se difundió, y su conexión se cerró.
        await using (YClient a = await YClient.ConnectAsync(server, "doc"))
        {
            await SettleAsync(store);
            store.ArmFailure();
            await a.EditAsync(0, "vivo");
            await WaitUntilAsync(() => a.CloseStatus is not null, TimeSpan.FromSeconds(5));
        }

        // Un cliente que reconecta recibe el estado vivo del servidor (autoritativo) vía el handshake.
        await using YClient c = await YClient.ConnectAsync(server, "doc");
        bool resynced = await WaitUntilAsync(() => c.Text() == "vivo", TimeSpan.FromSeconds(5));
        Assert.True(resynced, $"c='{c.Text()}'");
    }

    [Fact]
    public async Task BroadcastThenPersist_difunde_antes_de_que_el_append_confirme()
    {
        // El modo heredado difunde SIN esperar al append: con el append bloqueado, el par recibe la edición
        // antes de liberar la persistencia.
        var store = new ControllableAppendStore(new InMemoryDocumentStore());
        await using RelayHost relay = await StartRelayAsync(
            WeftAccess.ReadWrite, store, durability: DurabilityMode.BroadcastThenPersist);
        TestServer server = relay.Server;
        await using YClient a = await YClient.ConnectAsync(server, "doc");
        await using YClient b = await YClient.ConnectAsync(server, "doc");

        await SettleAsync(store);
        store.ArmGate(); // el próximo append (la edición) se bloquea
        await a.EditAsync(0, "rápido");

        bool sawBeforePersist = await WaitUntilAsync(() => b.Text() == "rápido", TimeSpan.FromSeconds(5));
        store.Release();

        Assert.True(sawBeforePersist, $"b='{b.Text()}' — el modo heredado difunde antes de persistir");
    }

    [Fact]
    public async Task PersistThenBroadcast_no_difunde_hasta_que_el_append_confirme()
    {
        // Testigo de orden del default: con el append bloqueado, el par NO ve la edición hasta liberarlo.
        var store = new ControllableAppendStore(new InMemoryDocumentStore());
        await using RelayHost relay = await StartRelayAsync(WeftAccess.ReadWrite, store);
        TestServer server = relay.Server;
        await using YClient a = await YClient.ConnectAsync(server, "doc");
        await using YClient b = await YClient.ConnectAsync(server, "doc");

        await SettleAsync(store);
        store.ArmGate();
        await a.EditAsync(0, "durable");

        // Mientras el append está bloqueado, el par no debe ver nada.
        bool leakedEarly = await WaitUntilAsync(() => b.Text() == "durable", TimeSpan.FromMilliseconds(400));
        Assert.False(leakedEarly, "persist-before-broadcast NO debe difundir antes de que el append confirme");

        // Al liberar el append, converge.
        store.Release();
        bool converged = await WaitUntilAsync(() => b.Text() == "durable", TimeSpan.FromSeconds(5));
        Assert.True(converged, $"b='{b.Text()}'");
    }
}
