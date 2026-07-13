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

    private sealed class FixedAuthorizer(WeftAccess access) : IWeftAuthorizer
    {
        public ValueTask<WeftAccess> AuthorizeAsync(HttpContext context, string docId, CancellationToken ct)
            => ValueTask.FromResult(access);
    }

    private static async Task<IHost> BuildHostAsync(WeftAccess access, IDocumentStore store, IBlobStore? blobs = null)
    {
        IHostBuilder builder = new HostBuilder().ConfigureWebHost(web =>
        {
            web.UseTestServer();
            web.ConfigureServices(services =>
            {
                services.AddRouting();
                services.AddWeftServer(o =>
                    o.Broker = new DocumentBrokerOptions { IdleSweepInterval = TimeSpan.FromMilliseconds(50) });
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

    private static async Task<RelayHost> StartRelayAsync(WeftAccess access, IDocumentStore store, IBlobStore? blobs = null)
        => new RelayHost(await BuildHostAsync(access, store, blobs));

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
            TestServer server, string docId, byte[]? seedState = null, CancellationToken ct = default)
        {
            WebSocketClient wsc = server.CreateWebSocketClient();
            WebSocket ws = await wsc.ConnectAsync(new Uri(server.BaseAddress, $"collab/{docId}"), ct);
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
            TimeSpan.FromSeconds(1));

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
        Assert.True(await WaitUntilAsync(() => fresh.Text().Length == 20_000, TimeSpan.FromSeconds(1)),
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
    public async Task ReadOnly_client_that_writes_is_closed_with_policy_violation()
    {
        await using RelayHost relay = await StartRelayAsync(WeftAccess.ReadOnly, new InMemoryDocumentStore());
        TestServer server = relay.Server;
        await using YClient ro = await YClient.ConnectAsync(server, "doc");

        await ro.EditAsync(0, "nope"); // un update desde una conexión ReadOnly

        bool closed = await WaitUntilAsync(
            () => ro.CloseStatus == WebSocketCloseStatus.PolicyViolation, TimeSpan.FromSeconds(1));
        Assert.True(closed, $"closeStatus={ro.CloseStatus}");
    }

    [Fact]
    public async Task Awareness_is_relayed_and_withdrawn_on_disconnect()
    {
        await using RelayHost relay = await StartRelayAsync(WeftAccess.ReadWrite, new InMemoryDocumentStore());
        TestServer server = relay.Server;
        await using YClient observer = await YClient.ConnectAsync(server, "doc");
        YClient presence = await YClient.ConnectAsync(server, "doc");

        const uint clientId = 4242;
        await presence.SendAwarenessAsync(AwarenessUpdate(clientId, 1, "{\"user\":\"A\"}"));

        // El observador ve el estado de awareness del par.
        Assert.True(await WaitUntilAsync(
            () => observer.AwarenessReceived.Any(p => AwarenessHasClient(p, clientId, requireNull: false)),
            TimeSpan.FromSeconds(1)));

        // Al desconectar el par, el observador recibe la RETIRADA (estado "null" para su clientID).
        await presence.DisposeAsync();
        Assert.True(await WaitUntilAsync(
            () => observer.AwarenessReceived.Any(p => AwarenessHasClient(p, clientId, requireNull: true)),
            TimeSpan.FromSeconds(1)));
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
            Assert.True(await WaitUntilAsync(() => c2.Text() == "durable", TimeSpan.FromSeconds(1)));
        } // r1.DisposeAsync consolida el snapshot en el store (WeftServer.DisposeAsync)

        await using RelayHost r2 = await StartRelayAsync(WeftAccess.ReadWrite, store);
        TestServer s2 = r2.Server;
        await using YClient c3 = await YClient.ConnectAsync(s2, "doc");
        Assert.True(await WaitUntilAsync(() => c3.Text() == "durable", TimeSpan.FromSeconds(1)),
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
            () => probe.Text() == "content-addressed parity", TimeSpan.FromSeconds(1)));

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
}
