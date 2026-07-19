namespace Weft.Concurrency;

/// <summary>
/// Manages active documents with per-document serialized access (one actor/channel per <c>docId</c>,
/// constitution P-V). Thread-safe and the only supported way to share a document across threads.
/// Registers and reuses actors by identity, evicts them by inactivity and by memory pressure
/// (LRU), and releases native resources deterministically.
/// </summary>
/// <remarks>
/// The <see cref="DocumentBrokerOptions.MaxActiveDocuments"/> limit is "soft": it is reasserted in the periodic
/// sweep, not synchronously in <see cref="OpenAsync"/>, and it never evicts a document with live
/// sessions. It may be exceeded transiently under bursts of openings or when all active documents
/// have open sessions.
/// </remarks>
public sealed class DocumentBroker : IAsyncDisposable
{
    private readonly ICrdtEngine _engine;
    private readonly DocumentBrokerOptions _options;
    private readonly object _gate = new();
    private readonly Dictionary<string, DocumentActor> _actors = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Task<DocumentActor>> _loading = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Task> _evicting = new(StringComparer.Ordinal);
    private readonly CancellationTokenSource _shutdown = new();
    private readonly Task _sweeper;
    private bool _disposed;

    /// <summary>Creates the broker over a CRDT engine and lifecycle options (defaults if omitted).</summary>
    public DocumentBroker(ICrdtEngine engine, DocumentBrokerOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(engine);
        _engine = engine;
        _options = options ?? new DocumentBrokerOptions();
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(_options.MaxActiveDocuments,
            $"{nameof(options)}.{nameof(DocumentBrokerOptions.MaxActiveDocuments)}");
        _sweeper = Task.Run(SweepLoopAsync);
    }

    /// <summary>Number of currently active (registered) documents.</summary>
    public int ActiveDocumentCount
    {
        get { lock (_gate) { return _actors.Count; } }
    }

    /// <summary>
    /// Opens (or reuses) the document <paramref name="docId"/>. If it is not active, it loads it with
    /// <paramref name="loader"/> (a <c>loader</c> that returns <c>null</c>/empty ⇒ new document).
    /// Returns a <see cref="DocumentSession"/> to operate it asynchronously.
    /// </summary>
    public async ValueTask<DocumentSession> OpenAsync(
        string docId,
        Func<string, CancellationToken, ValueTask<byte[]?>>? loader = null,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(docId);
        ObjectDisposedException.ThrowIf(_disposed, this);

        while (true)
        {
            DocumentActor? existing;
            Task<DocumentActor> loadTask;
            Task? inflightEviction = null;
            lock (_gate)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                if (_evicting.TryGetValue(docId, out Task? eviction))
                {
                    // There is an in-flight eviction of this document: wait for it to persist its state before
                    // loading, or we would load a half-written snapshot (lost updates, SC-006).
                    existing = null;
                    loadTask = null!;
                    inflightEviction = eviction;
                }
                else if (_actors.TryGetValue(docId, out DocumentActor? found)
                    && found.State is DocumentActorState.Active or DocumentActorState.Idle)
                {
                    existing = found;
                    loadTask = System.Threading.Tasks.Task.FromResult(found);
                }
                else
                {
                    // A terminated actor (Faulted/Evicted) that remained registered is discarded to recreate it
                    // clean — never retry over it (avoids the infinite spin over a dead actor).
                    if (found is not null)
                    {
                        _actors.Remove(docId);
                    }
                    existing = null;
                    if (_loading.TryGetValue(docId, out Task<DocumentActor>? pending))
                    {
                        loadTask = pending;
                    }
                    else
                    {
                        // The shared load uses the broker's token, NOT the caller's: a caller's
                        // cancellation must not poison the load for the other waiters of the same docId.
                        loadTask = LoadAndRegisterAsync(docId, loader, _shutdown.Token);
                        _loading[docId] = loadTask;
                    }
                }
            }

            if (inflightEviction is not null)
            {
                try { await inflightEviction.WaitAsync(ct).ConfigureAwait(false); } catch (OperationCanceledException) { throw; } catch { /* the eviction reports separately */ }
                continue; // the state is already persisted; retrying will load the correct snapshot
            }

            // WaitAsync applies THIS caller's ct only to the wait; the shared load stays alive for
            // other waiters even if this one cancels (finding H).
            DocumentActor actor = existing ?? await loadTask.WaitAsync(ct).ConfigureAwait(false);

            // Add the session atomically with respect to the sweep: only if the actor is still registered and
            // did not terminate. If it was evicted in the window, retry (will reopen or reuse).
            lock (_gate)
            {
                if (_actors.TryGetValue(docId, out DocumentActor? still)
                    && ReferenceEquals(still, actor)
                    && actor.State is DocumentActorState.Active or DocumentActorState.Idle)
                {
                    var session = new DocumentSession(actor);
                    actor.AddSession(session);
                    return session;
                }
            }
            // evicted between load and session registration (rare): yield and retry without burning CPU
            await System.Threading.Tasks.Task.Yield();
        }
    }

    private async Task<DocumentActor> LoadAndRegisterAsync(
        string docId,
        Func<string, CancellationToken, ValueTask<byte[]?>>? loader,
        CancellationToken ct)
    {
        // Yield BEFORE working: guarantees it never completes synchronously inside OpenAsync's lock,
        // so OpenAsync already assigned `_loading[docId]` before the `finally` here removes it (this avoids
        // the stale entry that caused the R6 livelock, and lets the load manage its own entry).
        await System.Threading.Tasks.Task.Yield();
        try
        {
            byte[]? initial = loader is not null ? await loader(docId, ct).ConfigureAwait(false) : null;
            ICrdtDoc doc = initial is { Length: > 0 } ? _engine.LoadDoc(initial) : _engine.CreateDoc();
            var actor = new DocumentActor(docId, doc, _options.OnEvicting);

            bool disposedRace;
            lock (_gate)
            {
                disposedRace = _disposed;
                if (!disposedRace)
                {
                    _actors[docId] = actor;
                }
            }
            if (disposedRace)
            {
                // Broker closed during the load: release the actor DETERMINISTICALLY (await, not
                // fire-and-forget) so that DisposeAsync —which waits for the in-flight loads— does not return
                // before this document is released (finding F).
                await actor.BeginEvictionAsync().ConfigureAwait(false);
                throw new ObjectDisposedException(nameof(DocumentBroker));
            }
            return actor;
        }
        finally
        {
            lock (_gate) { _loading.Remove(docId); }
        }
    }

    private async Task SweepLoopAsync()
    {
        try
        {
            using var timer = new PeriodicTimer(_options.ResolveSweepInterval());
            while (await timer.WaitForNextTickAsync(_shutdown.Token).ConfigureAwait(false))
            {
                try
                {
                    await SweepOnceAsync().ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // a failed sweep must not kill the background sweep (it would never evict again).
                    System.Diagnostics.Debug.WriteLine($"[DocumentBroker] barrido falló: {ex}");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // shutdown
        }
    }

    /// <summary>A single eviction pass: inactivity (idle) + memory pressure (LRU). Visible for tests.</summary>
    internal async ValueTask SweepOnceAsync()
    {
        List<Task> evictions = [];
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            var toEvict = new HashSet<DocumentActor>();
            long idleThreshold = (long)_options.IdleEviction.TotalMilliseconds;
            foreach (DocumentActor a in _actors.Values)
            {
                bool terminated = a.State is DocumentActorState.Faulted or DocumentActorState.Evicted;
                bool idle = a.SessionCount == 0 && a.IdleMilliseconds >= idleThreshold;
                if (terminated || idle)
                {
                    toEvict.Add(a);
                }
            }

            int remaining = _actors.Count - toEvict.Count;
            int over = remaining - _options.MaxActiveDocuments;
            if (over > 0)
            {
                // Memory pressure: evict the least recently used ones WITHOUT a session, even if they are
                // "warm". Ordering by descending inactivity protects the recently used/created ones; if
                // one is evicted in the window before its first session, OpenAsync retries.
                // `toEvict` is a HashSet → the exclusion is O(1) per candidate (finding K).
                List<DocumentActor> lru = _actors.Values
                    .Where(a => !toEvict.Contains(a) && a.SessionCount == 0)
                    .OrderByDescending(a => a.IdleMilliseconds)
                    .Take(over)
                    .ToList();
                foreach (DocumentActor a in lru)
                {
                    toEvict.Add(a);
                }
            }

            foreach (DocumentActor a in toEvict)
            {
                _actors.Remove(a.DocId);
                Task eviction = EvictActorAsync(a);
                _evicting[a.DocId] = eviction; // concurrent OpenAsync calls wait for it to persist
                evictions.Add(eviction);
            }
        }

        // Wait for the evictions this sweep started to finish (persistence included). It gives
        // determinism to the tests; in the background sweep it only pauses until the next tick.
        await Task.WhenAll(evictions).ConfigureAwait(false);
    }

    private async Task EvictActorAsync(DocumentActor actor)
    {
        await System.Threading.Tasks.Task.Yield(); // do not complete synchronously: _evicting is assigned before the finally
        try
        {
            await actor.BeginEvictionAsync().ConfigureAwait(false);
        }
        catch
        {
            // one actor's eviction must not bring down the sweep of the others
        }
        finally
        {
            lock (_gate)
            {
                _evicting.Remove(actor.DocId);
            }
        }
    }

    /// <summary>Drains and releases all documents exactly once; stops the sweep.</summary>
    public async ValueTask DisposeAsync()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
        }

        _shutdown.Cancel();
        try
        {
            await _sweeper.ConfigureAwait(false);
        }
        catch
        {
            // the sweeper is already terminating
        }

        // Wait for the in-flight loads and evictions before draining the rest, so that the release
        // is DETERMINISTIC with respect to DisposeAsync's return (finding F). Since `_disposed` is already true,
        // no later load registers in `_actors`: the ones that were in flight either already registered
        // (captured in `all`), or see `_disposed` and release their actor themselves (await, not fire-and-forget).
        Task[] loading;
        Task[] inflight;
        List<DocumentActor> all;
        lock (_gate)
        {
            loading = [.. _loading.Values];
            inflight = [.. _evicting.Values];
            all = [.. _actors.Values];
            _actors.Clear();
        }
        try
        {
            await Task.WhenAll(loading).ConfigureAwait(false);
        }
        catch
        {
            // a load during shutdown throws ObjectDisposedException after releasing its actor; it does not block
        }
        try
        {
            await Task.WhenAll(inflight).ConfigureAwait(false);
        }
        catch
        {
            // each eviction reports its own failure; do not block the shutdown
        }

        foreach (DocumentActor a in all)
        {
            try
            {
                await a.BeginEvictionAsync().ConfigureAwait(false);
            }
            catch
            {
                // release the rest despite an isolated failure
            }
        }

        _shutdown.Dispose();
    }
}
