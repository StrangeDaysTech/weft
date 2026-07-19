using System.Diagnostics;
using System.Net.WebSockets;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Weft;
using Weft.Server;
using Weft.Server.Auth;
using Weft.Server.Persistence;
using Weft.Server.Protocol;
using Weft.Yrs;

namespace Weft.LoadTest;

/// <summary>
/// Real relay load (FU-010/CHARTER-14): measures the editor→observer latency of an update through
/// the full relay (TestServer + WebSocket + <see cref="FileSystemDocumentStore"/> with fsync), in
/// BOTH durability modes. It is the evidence backing the <c>PersistThenBroadcast</c> default: without
/// it, the choice of default would be an unmeasured claim. The US2 <see cref="Program"/> drives
/// the broker directly and is blind to the relay's persistence path.
/// </summary>
internal static class RelayLoad
{
    public static async Task<int> RunAsync(int edits)
    {
        Console.WriteLine($"[relay-load] editor→observador vía relay real, {edits} ediciones/modo, FileSystemDocumentStore + fsync");

        LatencyStats persist = await MeasureAsync(DurabilityMode.PersistThenBroadcast, edits);
        LatencyStats broadcast = await MeasureAsync(DurabilityMode.BroadcastThenPersist, edits);

        Console.WriteLine($"[relay-load] PersistThenBroadcast (default): {persist}");
        Console.WriteLine($"[relay-load] BroadcastThenPersist (legacy):  {broadcast}");
        Console.WriteLine(
            $"[relay-load] coste del orden seguro (p50): +{persist.P50 - broadcast.P50:F1}ms, " +
            $"(p99): +{persist.P99 - broadcast.P99:F1}ms");

        // PASS = both modes converged on all edits (no losses or timeouts). The latency
        // number is informational (depends on the runner's disk); what is gated is correctness.
        bool ok = persist.Count == edits && broadcast.Count == edits;
        Console.WriteLine($"[relay-load] convergencia: persist={persist.Count}/{edits} broadcast={broadcast.Count}/{edits} " +
                          $"→ {(ok ? "PASS" : "FAIL")}");
        return ok ? 0 : 1;
    }

    private static async Task<LatencyStats> MeasureAsync(DurabilityMode mode, int edits)
    {
        string dataDir = Path.Combine(Path.GetTempPath(), $"weft-relay-load-{mode}-{Environment.ProcessId}");
        Directory.CreateDirectory(dataDir);
        try
        {
            using IHost host = await BuildHostAsync(dataDir, mode);
            TestServer server = host.GetTestServer();

            await using var editor = await RelayClient.ConnectAsync(server, "doc");
            await using var observer = await RelayClient.ConnectAsync(server, "doc");

            var samples = new List<double>(edits);
            int converged = 0;
            for (int i = 1; i <= edits; i++)
            {
                var sw = Stopwatch.StartNew();
                await editor.EditAsync("x");
                bool seen = await WaitUntilAsync(() => observer.TextLength >= i, TimeSpan.FromSeconds(10));
                sw.Stop();
                if (seen)
                {
                    samples.Add(sw.Elapsed.TotalMilliseconds);
                    converged++;
                }
            }

            await host.StopAsync();
            return LatencyStats.From(samples, converged);
        }
        finally
        {
            try { Directory.Delete(dataDir, recursive: true); } catch { /* best-effort */ }
        }
    }

    private static async Task<IHost> BuildHostAsync(string dataDir, DurabilityMode mode)
    {
        IHostBuilder builder = new HostBuilder().ConfigureWebHost(web =>
        {
            web.UseTestServer();
            web.ConfigureServices(services =>
            {
                services.AddRouting();
                services.AddWeftServer(o =>
                {
                    o.Engine = YrsEngine.Instance;
                    o.Durability = mode;
                });
                services.AddSingleton<IWeftAuthorizer>(new AllowAll());
                services.AddSingleton<IDocumentStore>(new FileSystemDocumentStore(dataDir));
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

    private static async Task<bool> WaitUntilAsync(Func<bool> cond, TimeSpan timeout)
    {
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < timeout)
        {
            if (cond())
            {
                return true;
            }

            await Task.Delay(2);
        }

        return cond();
    }

    private sealed class AllowAll : IWeftAuthorizer
    {
        public ValueTask<WeftAccess> AuthorizeAsync(Microsoft.AspNetCore.Http.HttpContext context, string docId, CancellationToken ct)
            => ValueTask.FromResult(WeftAccess.ReadWrite);
    }

    private readonly record struct LatencyStats(double P50, double P99, double Max, int Count)
    {
        public static LatencyStats From(List<double> samples, int count)
        {
            if (samples.Count == 0)
            {
                return new LatencyStats(0, 0, 0, count);
            }

            samples.Sort();
            return new LatencyStats(
                Percentile(samples, 0.50),
                Percentile(samples, 0.99),
                samples[^1],
                count);
        }

        private static double Percentile(List<double> sorted, double p)
        {
            int idx = (int)Math.Ceiling(p * sorted.Count) - 1;
            return sorted[Math.Clamp(idx, 0, sorted.Count - 1)];
        }

        public override string ToString() => $"p50={P50:F1}ms p99={P99:F1}ms max={Max:F1}ms (n={Count})";
    }

    /// <summary>Minimal WebSocket client with a real yrs doc, speaking y-sync against the relay.</summary>
    private sealed class RelayClient : IAsyncDisposable
    {
        private const string Field = "body";
        private readonly WebSocket _ws;
        private readonly ICrdtDoc _doc = YrsEngine.Instance.CreateDoc();
        private readonly object _lock = new();
        private readonly CancellationTokenSource _cts = new();
        private readonly Task _recv;

        private RelayClient(WebSocket ws)
        {
            _ws = ws;
            _recv = Task.Run(ReceiveLoopAsync);
        }

        public int TextLength
        {
            get { lock (_lock) { return _doc.GetText(Field).Length; } }
        }

        public static async Task<RelayClient> ConnectAsync(TestServer server, string docId)
        {
            WebSocketClient wsc = server.CreateWebSocketClient();
            WebSocket ws = await wsc.ConnectAsync(new Uri(server.BaseAddress, $"collab/{docId}"), CancellationToken.None);
            var client = new RelayClient(ws);
            byte[] sv;
            lock (client._lock) { sv = client._doc.ExportStateVector(); }
            await client.SendAsync(SyncProtocol.EncodeSyncStep1(sv));
            return client;
        }

        public async Task EditAsync(string text)
        {
            byte[] delta;
            lock (_lock)
            {
                byte[] before = _doc.ExportStateVector();
                _doc.InsertText(Field, 0, text);
                delta = _doc.ExportUpdateSince(before);
            }

            await SendAsync(SyncProtocol.EncodeUpdate(delta));
        }

        private async Task SendAsync(byte[] frame) =>
            await _ws.SendAsync(frame, WebSocketMessageType.Binary, endOfMessage: true, _cts.Token).ConfigureAwait(false);

        private async Task ReceiveLoopAsync()
        {
            var buf = new byte[16 * 1024];
            try
            {
                while (!_cts.IsCancellationRequested && _ws.State == WebSocketState.Open)
                {
                    WebSocketReceiveResult r = await _ws.ReceiveAsync(buf, _cts.Token).ConfigureAwait(false);
                    if (r.MessageType == WebSocketMessageType.Close)
                    {
                        return;
                    }

                    byte[] frame = buf[..r.Count];
                    SyncMessage m = SyncProtocol.Decode(frame);
                    if (m.Type == MessageType.Sync)
                    {
                        byte[] payload = m.Payload.ToArray();
                        if (m.SyncType == SyncMessageType.Step1)
                        {
                            byte[] delta;
                            lock (_lock) { delta = _doc.ExportUpdateSince(payload); }
                            await SendAsync(SyncProtocol.EncodeSyncStep2(delta));
                        }
                        else
                        {
                            lock (_lock) { _doc.ApplyUpdate(payload); }
                        }
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (WebSocketException) { }
        }

        public async ValueTask DisposeAsync()
        {
            await _cts.CancelAsync();
            try { await _recv; } catch { }
            _ws.Dispose();
            _cts.Dispose();
        }
    }
}
