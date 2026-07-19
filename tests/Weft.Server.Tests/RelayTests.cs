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
/// End-to-end relay integration tests (T051): a <see cref="TestServer"/> hosts the relay and <b>simulated</b>
/// Yjs clients (real yrs engine on both sides, speaking the y-sync wire via <see cref="SyncProtocol"/>)
/// connect over WebSocket. Covers the US3 Independent Test criteria.
/// </summary>
public sealed class RelayTests
{
    private const string Field = "content";

    // ---------- Harness ----------

    // Grants the `fallback` access, unless the connection requests an explicit one via query (?access=ro|rw|deny) —
    // this allows mixing a ReadWrite writer and a ReadOnly reader over the same document in one test.
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

    /// <summary>Relay host with async-dispose (the concrete <c>IHost</c> is IAsyncDisposable; the static one is not).</summary>
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

    // The timeout is a generous UPPER bound to absorb CI runner contention (e.g. slow macOS),
    // NOT the US3 convergence latency target (SC-005 <1 s); actual convergence is
    // sub-second (verified by the headless check and local runs).
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

    /// <summary>Simulated Yjs client: WebSocket + a real yrs doc, speaking y-sync.</summary>
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
            // Initial sync: we announce our state vector (after seeding the prior state, if any).
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

        /// <summary>Edits locally and broadcasts the delta to the server.</summary>
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

        /// <summary>Sends a raw state/update as an Update message (without applying it locally).</summary>
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

        // A client builds a large document and we capture its state.
        await using YClient writer = await YClient.ConnectAsync(server, "doc");
        await writer.EditAsync(0, new string('x', 20_000));
        byte[] fullState = writer.ExportState();

        // FRESH client (empty SV): the server sends it the full state (≫20 KB).
        await using YClient fresh = await YClient.ConnectAsync(server, "doc");
        Assert.True(await WaitUntilAsync(() => fresh.Text().Length == 20_000, TimeSpan.FromSeconds(5)),
            $"fresh no sincronizó: len={fresh.Text().Length}");
        Assert.True(fresh.BytesReceived > 20_000, $"fresh={fresh.BytesReceived}");

        // UP-TO-DATE client (seeded with the state → full SV): the server has nothing new to send it.
        await using YClient upToDate = await YClient.ConnectAsync(server, "doc", seedState: fullState);
        await Task.Delay(150); // let the initial sync arrive
        Assert.True(upToDate.BytesReceived * 4 < fresh.BytesReceived,
            $"upToDate={upToDate.BytesReceived} fresh={fresh.BytesReceived} (delta en reconexión ≪ estado completo)");
    }

    [Fact]
    public async Task Denied_connection_exchanges_no_content()
    {
        await using RelayHost relay = await StartRelayAsync(WeftAccess.Deny, new InMemoryDocumentStore());
        TestServer server = relay.Server;
        // 403 before the upgrade → the WebSocket connection is rejected (0 bytes of content).
        await Assert.ThrowsAnyAsync<Exception>(async () =>
        {
            await using YClient _ = await YClient.ConnectAsync(server, "doc");
        });
    }

    [Fact]
    public async Task ReadOnly_client_receives_updates_survives_handshake_but_closes_on_write()
    {
        // F1 regression (CHARTER-05 audit): the ReadOnly client must not close due to the handshake SyncStep2,
        // only due to a live Update. Without the fix, the reader closes during the handshake and this test fails.
        await using RelayHost relay = await StartRelayAsync(WeftAccess.ReadWrite, new InMemoryDocumentStore());
        TestServer server = relay.Server;

        await using YClient writer = await YClient.ConnectAsync(server, "doc");                 // ReadWrite (default)
        await using YClient reader = await YClient.ConnectAsync(server, "doc", access: "ro");   // ReadOnly

        // The reader survives the handshake (its SyncStep2 is ignored, does not close it) and receives the writer's update.
        await writer.EditAsync(0, "shared");
        Assert.True(await WaitUntilAsync(() => reader.Text() == "shared", TimeSpan.FromSeconds(5)),
            $"el lector ReadOnly no recibió el update: text='{reader.Text()}' close={reader.CloseStatus}");
        Assert.Null(reader.CloseStatus); // still connected after receiving updates

        // But if the reader tries to write (live Update), it closes with 1008.
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

        // Liveness: one edit converges on observer → both connections are joined to the hub before broadcasting
        // awareness (avoids the "observer not yet in the hub" race when broadcasting).
        await presence.EditAsync(0, "x");
        Assert.True(await WaitUntilAsync(() => observer.Text() == "x", TimeSpan.FromSeconds(5)));

        const uint clientId = 4242;
        await presence.SendAwarenessAsync(AwarenessUpdate(clientId, 1, "{\"user\":\"A\"}"));

        // The observer sees the peer's awareness state.
        Assert.True(await WaitUntilAsync(
            () => observer.AwarenessReceived.Any(p => AwarenessHasClient(p, clientId, requireNull: false)),
            TimeSpan.FromSeconds(5)));

        // When the peer disconnects, the observer receives the WITHDRAWAL ("null" state for its clientID).
        await presence.DisposeAsync();
        Assert.True(await WaitUntilAsync(
            () => observer.AwarenessReceived.Any(p => AwarenessHasClient(p, clientId, requireNull: true)),
            TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public async Task Awareness_with_zero_clock_for_new_client_does_not_crash()
    {
        // F2 regression (CHARTER-05 audit): an awareness with clock 0 for a new clientID must not
        // throw KeyNotFoundException in TrackClients (which would fault the connection). It is tested by SURVIVAL
        // (an edit after the awareness is still relayed), avoiding the "observer not yet in the
        // hub" race with a prior liveness barrier.
        await using RelayHost relay = await StartRelayAsync(WeftAccess.ReadWrite, new InMemoryDocumentStore());
        TestServer server = relay.Server;
        await using YClient observer = await YClient.ConnectAsync(server, "doc");
        await using YClient presence = await YClient.ConnectAsync(server, "doc");

        // Liveness: an edit from presence converges on observer → both connections alive and joined to the hub.
        await presence.EditAsync(0, "live");
        Assert.True(await WaitUntilAsync(() => observer.Text() == "live", TimeSpan.FromSeconds(5)),
            $"no se estableció liveness: observer='{observer.Text()}'");

        // Awareness with clock 0 for a new clientID (common in a Yjs client's first awareness).
        await presence.SendAwarenessAsync(AwarenessUpdate(777, 0, "{\"user\":\"Z\"}"));

        // The presence connection is STILL alive after the clock-0 awareness: a later edit is still relayed
        // (without the fix, TrackClients would have thrown and faulted the connection → this edit would never arrive).
        await presence.EditAsync(4, " more");
        Assert.True(await WaitUntilAsync(() => observer.Text() == "live more", TimeSpan.FromSeconds(5)),
            $"presence dejó de relayar tras el awareness clock-0 (¿crasheó?): observer='{observer.Text()}' close={presence.CloseStatus}");
        Assert.Null(presence.CloseStatus);
    }

    [Fact]
    public async Task State_survives_a_server_restart()
    {
        var store = new InMemoryDocumentStore(); // the durable store survives the process "restart"

        await using (RelayHost r1 = await StartRelayAsync(WeftAccess.ReadWrite, store))
        {
            TestServer s1 = r1.Server;
            await using YClient c1 = await YClient.ConnectAsync(s1, "doc");
            await c1.EditAsync(0, "durable");
            await using YClient c2 = await YClient.ConnectAsync(s1, "doc");
            Assert.True(await WaitUntilAsync(() => c2.Text() == "durable", TimeSpan.FromSeconds(5)));
        } // r1.DisposeAsync consolidates the snapshot into the store (WeftServer.DisposeAsync)

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

        // Local publish of the same content.
        using ICrdtDoc local = YrsEngine.Instance.CreateDoc();
        local.InsertText(Field, 0, "content-addressed parity");
        byte[] localState = local.ExportState();
        var versionStore = new VersionStore(YrsEngine.Instance, blobs);
        VersionId localId = await versionStore.PublishAsync(local);

        // The server receives exactly the same state and publishes.
        await using RelayHost relay = await StartRelayAsync(WeftAccess.ReadWrite, new InMemoryDocumentStore(), blobs);
        TestServer server = relay.Server;
        await using YClient c = await YClient.ConnectAsync(server, "doc");
        await c.SendUpdateAsync(localState);

        // Wait for the server to apply the state (a 2nd client converges → the actor already processed it).
        await using YClient probe = await YClient.ConnectAsync(server, "doc");
        Assert.True(await WaitUntilAsync(
            () => probe.Text() == "content-addressed parity", TimeSpan.FromSeconds(5)));

        var weftServer = relay.Services.GetRequiredService<IWeftServer>();
        VersionId serverId = await weftServer.PublishAsync("doc");

        Assert.Equal(localId, serverId);
    }

    // awarenessPayload = the inner payload of an Awareness message (what the client stores in Dispatch).
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

    // ---------- Relay durability (FU-010, CHARTER-14) ----------

    /// <summary>
    /// Control store for the durability tests. Each connection triggers an append in the handshake
    /// (SyncStep2), so failing/blocking by call number is fragile; instead the next append is "armed"
    /// when the test decides (after connecting the clients), so that only the edit of
    /// interest fails or blocks.
    /// </summary>
    private sealed class ControllableAppendStore(IDocumentStore inner) : IDocumentStore
    {
        private int _failNext;
        private TaskCompletionSource? _gate;
        private int _appends;

        public int AppendCount => Volatile.Read(ref _appends);

        /// <summary>The NEXT append will throw once.</summary>
        public void ArmFailure() => Interlocked.Exchange(ref _failNext, 1);

        /// <summary>Appends from now on block until <see cref="Release"/>.</summary>
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

    // Waits for the connections' handshake appends to settle (stop growing) before arming.
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
        store.ArmFailure(); // the next append (a's edit) fails
        await a.EditAsync(0, "no debe verse");

        // Peer b NEVER receives the delta (persist-before-broadcast: the broadcast did not happen), and the
        // sending connection closes with 1011 InternalError.
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

        // The first client edits; its append fails → the edit stays in the server's live doc but was not
        // broadcast, and its connection closed.
        await using (YClient a = await YClient.ConnectAsync(server, "doc"))
        {
            await SettleAsync(store);
            store.ArmFailure();
            await a.EditAsync(0, "vivo");
            await WaitUntilAsync(() => a.CloseStatus is not null, TimeSpan.FromSeconds(5));
        }

        // A reconnecting client receives the server's live (authoritative) state via the handshake.
        await using YClient c = await YClient.ConnectAsync(server, "doc");
        bool resynced = await WaitUntilAsync(() => c.Text() == "vivo", TimeSpan.FromSeconds(5));
        Assert.True(resynced, $"c='{c.Text()}'");
    }

    [Fact]
    public async Task BroadcastThenPersist_difunde_antes_de_que_el_append_confirme()
    {
        // The legacy mode broadcasts WITHOUT waiting for the append: with the append blocked, the peer receives the edit
        // before releasing persistence.
        var store = new ControllableAppendStore(new InMemoryDocumentStore());
        await using RelayHost relay = await StartRelayAsync(
            WeftAccess.ReadWrite, store, durability: DurabilityMode.BroadcastThenPersist);
        TestServer server = relay.Server;
        await using YClient a = await YClient.ConnectAsync(server, "doc");
        await using YClient b = await YClient.ConnectAsync(server, "doc");

        await SettleAsync(store);
        store.ArmGate(); // the next append (the edit) blocks
        await a.EditAsync(0, "rápido");

        bool sawBeforePersist = await WaitUntilAsync(() => b.Text() == "rápido", TimeSpan.FromSeconds(5));
        store.Release();

        Assert.True(sawBeforePersist, $"b='{b.Text()}' — el modo heredado difunde antes de persistir");
    }

    [Fact]
    public async Task PersistThenBroadcast_no_difunde_hasta_que_el_append_confirme()
    {
        // Ordering witness for the default: with the append blocked, the peer does NOT see the edit until it is released.
        var store = new ControllableAppendStore(new InMemoryDocumentStore());
        await using RelayHost relay = await StartRelayAsync(WeftAccess.ReadWrite, store);
        TestServer server = relay.Server;
        await using YClient a = await YClient.ConnectAsync(server, "doc");
        await using YClient b = await YClient.ConnectAsync(server, "doc");

        await SettleAsync(store);
        store.ArmGate();
        await a.EditAsync(0, "durable");

        // While the append is blocked, the peer must not see anything.
        bool leakedEarly = await WaitUntilAsync(() => b.Text() == "durable", TimeSpan.FromMilliseconds(400));
        Assert.False(leakedEarly, "persist-before-broadcast NO debe difundir antes de que el append confirme");

        // On releasing the append, it converges.
        store.Release();
        bool converged = await WaitUntilAsync(() => b.Text() == "durable", TimeSpan.FromSeconds(5));
        Assert.True(converged, $"b='{b.Text()}'");
    }
}
