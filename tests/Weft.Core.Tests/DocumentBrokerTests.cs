using System.Text;
using Weft.Concurrency;
using Weft.Yrs;

namespace Weft.Core.Tests;

/// <summary>
/// Concurrency contract of the <see cref="DocumentBroker"/> (T040, US2, constitution P-V): strict
/// per-document serialization, FIFO ordering per session, eviction→persistence→reopen, fault
/// propagation and dispose semantics. The serialization/fault cases use a test engine that
/// instruments concurrency; the lifecycle ones use the real yrs engine.
/// </summary>
/// <remarks>
/// The <c>Category=Concurrency</c> is what the US2 command in <c>quickstart.md</c> selects;
/// without it that filter matched no test and the runbook step passed green while running
/// zero tests (detected in the T063 validation pass, CHARTER-11).
/// </remarks>
[Trait("Category", "Concurrency")]
public sealed class DocumentBrokerTests
{
    // -- Serialization (Acceptance Scenario 1): never two simultaneous operations on the same document --

    [Fact]
    public async Task Operations_on_same_document_never_run_concurrently()
    {
        var engine = new TrackingEngine();
        await using var broker = new DocumentBroker(engine);
        await using DocumentSession session = await broker.OpenAsync("doc");

        Task[] writers = Enumerable.Range(0, 50).Select(_ => Task.Run(async () =>
        {
            for (int k = 0; k < 20; k++)
            {
                await session.InsertTextAsync("body", 0, "x");
            }
        })).ToArray();
        await Task.WhenAll(writers);

        TrackingDoc doc = Assert.Single(engine.Docs);
        Assert.Equal(1, doc.PeakConcurrency);            // the actor serialized all access
        Assert.Equal(50 * 20, (await session.GetTextAsync("body")).Length);
    }

    // -- FIFO per session: enqueued operations are applied in enqueue order --

    [Fact]
    public async Task Operations_from_a_session_apply_in_FIFO_order()
    {
        await using var broker = new DocumentBroker(YrsEngine.Instance);
        await using DocumentSession session = await broker.OpenAsync("doc");

        var pending = new List<Task>();
        for (int i = 0; i < 10; i++)
        {
            // synchronous enqueue in order; inserts digit i at position i
            pending.Add(session.InsertTextAsync("body", i, i.ToString()).AsTask());
        }
        await Task.WhenAll(pending);

        Assert.Equal("0123456789", await session.GetTextAsync("body"));
    }

    // -- Acceptance Scenario 2: idle eviction → OnEvicting → reopen from what was persisted --

    [Fact]
    public async Task Idle_document_is_evicted_persisted_and_can_be_reopened()
    {
        byte[]? persisted = null;
        int evictions = 0;
        var options = new DocumentBrokerOptions
        {
            IdleEviction = TimeSpan.FromMilliseconds(20),
            IdleSweepInterval = TimeSpan.FromHours(1), // automatic sweep disabled: we trigger it manually
            OnEvicting = (id, state, ct) =>
            {
                persisted = state;
                Interlocked.Increment(ref evictions);
                return ValueTask.CompletedTask;
            },
        };
        await using var broker = new DocumentBroker(YrsEngine.Instance, options);

        DocumentSession session = await broker.OpenAsync("doc-1");
        await session.InsertTextAsync("body", 0, "hola");
        await session.DisposeAsync();          // no live sessions → eviction candidate
        await Task.Delay(60);                  // exceed IdleEviction (20ms)
        await broker.SweepOnceAsync();         // force the sweep (don't wait for the timer)

        Assert.Equal(1, evictions);
        Assert.NotNull(persisted);
        Assert.Equal(0, broker.ActiveDocumentCount);

        // Reopen with a loader that returns the persisted state → content restored.
        await using DocumentSession reopened = await broker.OpenAsync(
            "doc-1", (id, ct) => ValueTask.FromResult<byte[]?>(persisted));
        Assert.Equal("hola", await reopened.GetTextAsync("body"));
    }

    // -- LRU: when the maximum is exceeded, the least recently used is evicted (with no live sessions) --

    [Fact]
    public async Task Over_capacity_evicts_least_recently_used_without_sessions()
    {
        var options = new DocumentBrokerOptions
        {
            MaxActiveDocuments = 2,
            IdleEviction = TimeSpan.FromHours(1), // isolate: we only want to see LRU eviction
        };
        await using var broker = new DocumentBroker(YrsEngine.Instance, options);

        // Three documents, closing each session (no live sessions → eligible for LRU). The delay
        // separates the idle timestamps, so 'a' is the least recently used.
        foreach (string id in new[] { "a", "b", "c" })
        {
            DocumentSession s = await broker.OpenAsync(id);
            await s.InsertTextAsync("body", 0, id);
            await s.DisposeAsync();
            await Task.Delay(EvictionGrace);
        }

        Assert.Equal(3, broker.ActiveDocumentCount); // the limit is soft until the sweep
        await broker.SweepOnceAsync();
        Assert.Equal(2, broker.ActiveDocumentCount); // 'a' (LRU) evicted

        // Verify the IDENTITY of the evicted one, not just the count: 'a' (LRU, without OnEvicting/loader)
        // was lost → reopening gives an empty document; 'b' and 'c' remain active with their content. An
        // LRU that had evicted the MRU would pass the count assert but fail these.
        await using (DocumentSession ra = await broker.OpenAsync("a"))
        {
            Assert.Equal("", await ra.GetTextAsync("body"));
        }
        await using (DocumentSession rb = await broker.OpenAsync("b"))
        {
            Assert.Equal("b", await rb.GetTextAsync("body"));
        }
        await using (DocumentSession rc = await broker.OpenAsync("c"))
        {
            Assert.Equal("c", await rc.GetTextAsync("body"));
        }
    }

    // -- An UpdateApplied handler that throws must NOT fault the actor (finding G, for M2) --

    [Fact]
    public async Task Throwing_UpdateApplied_handler_does_not_fault_the_actor()
    {
        await using var broker = new DocumentBroker(YrsEngine.Instance);
        await using DocumentSession session = await broker.OpenAsync("doc");
        session.UpdateApplied += (_, _) => throw new InvalidOperationException("handler boom");

        // The insertion fires UpdateApplied → the handler throws, but the actor must stay healthy.
        await session.InsertTextAsync("body", 0, "hola");
        Assert.Equal("hola", await session.GetTextAsync("body"));

        // Subsequent operations keep working (the actor did not fault).
        await session.InsertTextAsync("body", 0, "! ");
        Assert.Equal("! hola", await session.GetTextAsync("body"));
    }

    // -- UpdateApplied fires with an applicable delta, for other sessions of the same doc (finding J) --

    [Fact]
    public async Task UpdateApplied_fires_with_applicable_delta_for_other_sessions()
    {
        await using var broker = new DocumentBroker(YrsEngine.Instance);
        await using DocumentSession writer = await broker.OpenAsync("doc");
        await using DocumentSession observer = await broker.OpenAsync("doc"); // same doc, another session

        var gotDelta = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        observer.UpdateApplied += (_, delta) => gotDelta.TrySetResult(delta.ToArray());

        await writer.InsertTextAsync("body", 0, "hola");

        byte[] applied = await gotDelta.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.NotEmpty(applied);

        // The delta is applicable to a fresh document and reproduces the text (relay surface for M2).
        using ICrdtDoc fresh = YrsEngine.Instance.CreateDoc();
        fresh.ApplyUpdate(applied);
        Assert.Equal("hola", fresh.GetText("body"));
    }

    // -- Actor in unrecoverable fault: propagates the causal exception to pending/future operations --

    [Fact]
    public async Task Faulted_actor_propagates_causal_exception()
    {
        await using var broker = new DocumentBroker(YrsEngine.Instance);
        await using DocumentSession session = await broker.OpenAsync("doc");

        var boom = new InvalidOperationException("boom");
        var gate = new TaskCompletionSource();

        // A turn that blocks the actor until 'gate' and then throws: guarantees that 'pending' is enqueued
        // BEHIND before the fault occurs (deterministic test).
        Task<int> faulting = session.ExecuteAsync<int>(_ => { gate.Task.Wait(); throw boom; }).AsTask();
        Task<string> pending = session.GetTextAsync("body").AsTask();
        gate.SetResult();

        InvalidOperationException fromFaulting =
            await Assert.ThrowsAsync<InvalidOperationException>(() => faulting);
        InvalidOperationException fromPending =
            await Assert.ThrowsAsync<InvalidOperationException>(() => pending);
        Assert.Same(boom, fromFaulting);
        Assert.Same(boom, fromPending);

        // Future operations also fail with the same causal exception.
        InvalidOperationException fromFuture =
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await session.GetTextAsync("body"));
        Assert.Same(boom, fromFuture);
    }

    // -- Dispose semantics: predictable platform error, never a crash (Acceptance Scenario 3) --

    [Fact]
    public async Task Using_a_disposed_session_throws_ObjectDisposedException()
    {
        await using var broker = new DocumentBroker(YrsEngine.Instance);
        DocumentSession session = await broker.OpenAsync("doc");
        await session.DisposeAsync();

        await Assert.ThrowsAsync<ObjectDisposedException>(
            async () => await session.InsertTextAsync("body", 0, "x"));
    }

    [Fact]
    public async Task Operations_after_broker_dispose_fail_predictably()
    {
        var broker = new DocumentBroker(YrsEngine.Instance);
        DocumentSession session = await broker.OpenAsync("doc");
        await broker.DisposeAsync();

        await Assert.ThrowsAsync<ObjectDisposedException>(
            async () => await session.GetTextAsync("body"));
        await Assert.ThrowsAsync<ObjectDisposedException>(
            async () => await broker.OpenAsync("other"));
    }

    private static readonly TimeSpan EvictionGrace = TimeSpan.FromMilliseconds(300);

    // -- Test engine that instruments the concurrency observed per document --

    private sealed class TrackingEngine : ICrdtEngine
    {
        public List<TrackingDoc> Docs { get; } = [];
        public string Name => "tracking";
        public INativeVersioning? NativeVersioning => null;
        public IDeterministicSeeding? DeterministicSeeding => null;

        public ICrdtDoc CreateDoc()
        {
            var doc = new TrackingDoc();
            lock (Docs) { Docs.Add(doc); }
            return doc;
        }

        public ICrdtDoc LoadDoc(ReadOnlySpan<byte> blob)
        {
            var doc = new TrackingDoc(blob);
            lock (Docs) { Docs.Add(doc); }
            return doc;
        }
    }

    private sealed class TrackingDoc : ICrdtDoc
    {
        private readonly StringBuilder _text = new();
        private int _inside;
        private int _peak;

        public TrackingDoc() { }

        public TrackingDoc(ReadOnlySpan<byte> blob) => _text.Append(Encoding.UTF8.GetString(blob));

        public int PeakConcurrency => Volatile.Read(ref _peak);

        public string EngineName => "tracking";

        public void InsertText(string field, int index, string text) =>
            Guarded(() => _text.Insert(index, text));

        public void DeleteText(string field, int index, int length) =>
            Guarded(() => _text.Remove(index, length));

        public string GetText(string field) => Guarded(() => _text.ToString());

        public byte[] ExportState() => Guarded(() => Encoding.UTF8.GetBytes(_text.ToString()));

        public byte[] ExportStateVector() => [];

        public byte[] ExportUpdateSince(ReadOnlySpan<byte> stateVector) => ExportState();

        public void ApplyUpdate(ReadOnlySpan<byte> update)
        {
            byte[] copy = update.ToArray();
            Guarded(() => _text.Append(Encoding.UTF8.GetString(copy)));
        }

        public void Dispose() { }

        private void Guarded(Action body) => Guarded(() => { body(); return true; });

        private T Guarded<T>(Func<T> body)
        {
            int now = Interlocked.Increment(ref _inside);
            InterlockedMax(ref _peak, now);
            try
            {
                Thread.SpinWait(100); // widen the window to expose any overlap
                return body();
            }
            finally
            {
                Interlocked.Decrement(ref _inside);
            }
        }

        private static void InterlockedMax(ref int target, int value)
        {
            int current;
            do
            {
                current = Volatile.Read(ref target);
                if (value <= current)
                {
                    return;
                }
            }
            while (Interlocked.CompareExchange(ref target, value, current) != current);
        }
    }
}
