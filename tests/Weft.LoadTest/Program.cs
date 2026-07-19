using System.Collections.Concurrent;
using System.Diagnostics;
using Weft.Concurrency;
using Weft.LoadTest;
using Weft.Yrs;

// Relay mode (FU-010/CHARTER-14): measures the broadcast latency of the real relay in both durability
// modes. Distinct from the US2 load below (which drives the broker directly, blind to the relay).
if (Array.IndexOf(args, "--relay") >= 0)
{
    return await RelayLoad.RunAsync(ArgInt(args, "--edits", 200));
}

// US2/M1 load test (SC-006): hundreds of documents and many concurrent tasks editing at
// random over a sustained period. Verifies (a) final consistency of each document and (b) bounded
// memory — the number of active documents stays under the limit even though the total exceeds the pool
// (idle+LRU eviction with persistence and reopening). Non-zero exit if anything fails (CI gate).

int docs = ArgInt(args, "--docs", 300);
int tasks = ArgInt(args, "--tasks", 8);
int seconds = ArgInt(args, "--seconds", 20);
int maxActive = ArgInt(args, "--max-active", Math.Max(8, docs / 4));

Console.WriteLine($"[load-test] docs={docs} tasks={tasks} seconds={seconds} max-active={maxActive} " +
                  $"gc-server={System.Runtime.GCSettings.IsServerGC}");

// In-memory "persistence": the OnEvicting hook saves the state here; the loader re-reads it on reopen.
var store = new ConcurrentDictionary<string, byte[]>(StringComparer.Ordinal);
long evictions = 0;
long confirmedOps = 0;
long errors = 0;

var options = new DocumentBrokerOptions
{
    MaxActiveDocuments = maxActive,
    // Aggressive idle: forces constant eviction/reopen under load → exercises the
    // eviction-vs-reopen race (persistence + reload) that SC-006 requires without losing updates.
    IdleEviction = TimeSpan.FromMilliseconds(30),
    IdleSweepInterval = TimeSpan.FromMilliseconds(10),
    OnEvicting = (id, state, ct) =>
    {
        store[id] = state;
        Interlocked.Increment(ref evictions);
        return ValueTask.CompletedTask;
    },
};

// Count of confirmed inserts per document: the final text length must equal it.
long[] inserts = new long[docs];

await using var broker = new DocumentBroker(YrsEngine.Instance, options);

Func<string, CancellationToken, ValueTask<byte[]?>> loader =
    (id, ct) => ValueTask.FromResult(store.TryGetValue(id, out byte[]? blob) ? blob : null);

// Sampling of the peak of active documents during the load (evidence of bounded memory).
int peakActive = 0;
using var samplerStop = new CancellationTokenSource();
Task sampler = Task.Run(async () =>
{
    while (!samplerStop.IsCancellationRequested)
    {
        int active = broker.ActiveDocumentCount;
        if (active > peakActive)
        {
            Interlocked.Exchange(ref peakActive, active);
        }
        try { await Task.Delay(20, samplerStop.Token); } catch (OperationCanceledException) { break; }
    }
});

var sw = Stopwatch.StartNew();
long deadlineTicks = sw.ElapsedMilliseconds + (seconds * 1000L);

// Each document grows up to a cap and is then read-only: bounds the SIZE (memory per doc),
// while pooling bounds the NUMBER of active documents. Both → bounded memory (SC-006).
const int perDocCap = 150;

Task[] workers = Enumerable.Range(0, tasks).Select(workerId => Task.Run(async () =>
{
    var rng = new Random(unchecked(0x5EED + workerId));
    while (sw.ElapsedMilliseconds < deadlineTicks)
    {
        int idx = rng.Next(docs);
        string docId = $"doc-{idx}";
        try
        {
            await using DocumentSession session = await broker.OpenAsync(docId, loader);
            if (Volatile.Read(ref inserts[idx]) < perDocCap)
            {
                int burst = rng.Next(1, 4);
                for (int b = 0; b < burst && Volatile.Read(ref inserts[idx]) < perDocCap; b++)
                {
                    await session.InsertTextAsync("body", 0, "x");
                    Interlocked.Increment(ref inserts[idx]);
                    Interlocked.Increment(ref confirmedOps);
                }
            }
            else
            {
                await session.GetTextAsync("body"); // keeps the open/evict churn without growing
            }
        }
        catch (Exception ex)
        {
            // Under correct operation this should not happen (a live session protects its document from
            // eviction). Any failure here is a regression in the concurrency layer.
            Interlocked.Increment(ref errors);
            if (Interlocked.Read(ref errors) <= 5)
            {
                Console.WriteLine($"[load-test] error on '{docId}': {ex.GetType().Name}: {ex.Message}");
            }
        }
    }
})).ToArray();

await Task.WhenAll(workers);
sw.Stop();
samplerStop.Cancel();
await sampler;

Console.WriteLine($"[load-test] load complete in {sw.Elapsed.TotalSeconds:F1}s: " +
                  $"ops={confirmedOps} evictions={evictions} peak-active={peakActive} errors={errors}");

// -- Consistency check: reopen each document and compare its length against the inserts --
int inconsistencias = 0;
for (int idx = 0; idx < docs; idx++)
{
    long expected = Interlocked.Read(ref inserts[idx]);
    await using DocumentSession session = await broker.OpenAsync($"doc-{idx}", loader);
    string text = await session.GetTextAsync("body");
    if (text.Length != expected)
    {
        if (inconsistencias < 10)
        {
            Console.WriteLine($"[load-test] INCONSISTENT doc-{idx}: len={text.Length} expected={expected}");
        }
        inconsistencias++;
    }
}

// -- Memory (informational): managed heap after a forced GC + working set. Memory is bounded two ways:
//    the SIZE per document (insert cap) and the NUMBER of active documents (pool + LRU). The peak
//    of active docs is a SOFT bound (reasserted on each sweep, not in OpenAsync), so it may exceed
//    MaxActiveDocuments transiently; the hard PASS criterion is the absolute working set, below. --
long managed = GC.GetTotalMemory(forceFullCollection: true);
long workingSet = Process.GetCurrentProcess().WorkingSet64;
Console.WriteLine($"[load-test] memory: managed-heap={managed / (1024 * 1024)}MB " +
                  $"working-set={workingSet / (1024 * 1024)}MB");

// Bounded memory (SC-006): with per-doc size and number of active docs both bounded, the working
// set stabilizes. Generous absolute limit; a process growing without bound exceeds it comfortably.
const long workingSetLimitMb = 1536;
long workingSetMb = workingSet / (1024 * 1024);
bool memoryBounded = workingSetMb < workingSetLimitMb;
bool consistent = inconsistencias == 0;
bool noErrors = Interlocked.Read(ref errors) == 0;

Console.WriteLine($"[load-test] consistency={(consistent ? "OK" : $"FAIL ({inconsistencias})")} " +
                  $"memory-bounded={(memoryBounded ? "OK" : $"FAIL (working-set {workingSetMb}MB >= {workingSetLimitMb}MB)")} " +
                  $"no-errors={(noErrors ? "OK" : "FAIL")}");

if (consistent && memoryBounded && noErrors)
{
    Console.WriteLine("[load-test] RESULT: PASS");
    return 0;
}

Console.WriteLine("[load-test] RESULT: FAIL");
return 1;

static int ArgInt(string[] args, string name, int fallback)
{
    int i = Array.IndexOf(args, name);
    if (i >= 0 && i + 1 < args.Length && int.TryParse(args[i + 1], out int value))
    {
        return value;
    }
    return fallback;
}
