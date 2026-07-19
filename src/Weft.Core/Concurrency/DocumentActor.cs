using System.Threading.Channels;

namespace Weft.Concurrency;

/// <summary>Observable state of a document's actor (constitution P-V).</summary>
internal enum DocumentActorState
{
    /// <summary>Accepts and processes operations.</summary>
    Active,

    /// <summary>Selected for eviction; draining the pending queue before releasing.</summary>
    Idle,

    /// <summary>Terminated normally; document persisted (if there was a hook) and released.</summary>
    Evicted,

    /// <summary>Terminated by unrecoverable fault; document released without persisting.</summary>
    Faulted,
}

/// <summary>
/// Serializes ALL access to a native <see cref="ICrdtDoc"/> through a single reader that drains a
/// channel of operations (actor pattern, constitution P-V). Never executes two operations of the same
/// document at once; releases the document exactly once when finished. <c>internal</c>: used
/// through <see cref="DocumentBroker"/> and <see cref="DocumentSession"/>.
/// </summary>
internal sealed class DocumentActor
{
    private readonly ICrdtDoc _doc;
    private readonly Channel<IWorkItem> _channel;
    private readonly Task _runLoop;
    private readonly Func<string, byte[], CancellationToken, ValueTask>? _onEvicting;
    private readonly object _sessionsLock = new();
    private readonly List<DocumentSession> _sessions = [];

    private volatile DocumentActorState _state = DocumentActorState.Active;
    private volatile Exception? _fault;
    private long _lastActivityTick = Environment.TickCount64;

    internal DocumentActor(string docId, ICrdtDoc doc, Func<string, byte[], CancellationToken, ValueTask>? onEvicting)
    {
        DocId = docId;
        _doc = doc;
        _onEvicting = onEvicting;
        _channel = Channel.CreateUnbounded<IWorkItem>(new UnboundedChannelOptions
        {
            SingleReader = true,   // a single reader: the serialization guarantee (P-V)
            SingleWriter = false,  // several sessions/threads enqueue
        });
        _runLoop = Task.Run(RunAsync);
    }

    internal string DocId { get; }

    internal DocumentActorState State => _state;

    /// <summary>Milliseconds elapsed since the last processed operation (for idle eviction).</summary>
    internal long IdleMilliseconds => Environment.TickCount64 - Interlocked.Read(ref _lastActivityTick);

    internal int SessionCount
    {
        get { lock (_sessionsLock) { return _sessions.Count; } }
    }

    internal void AddSession(DocumentSession session)
    {
        lock (_sessionsLock) { _sessions.Add(session); }
    }

    internal void RemoveSession(DocumentSession session)
    {
        lock (_sessionsLock) { _sessions.Remove(session); }
    }

    /// <summary>
    /// Enqueues an operation on the document and returns its result asynchronously. The operation
    /// executes within the actor's turn (never concurrent with another of the same document).
    /// </summary>
    internal ValueTask<T> EnqueueAsync<T>(Func<ICrdtDoc, T> op, bool mutating, CancellationToken ct)
    {
        var item = new WorkItem<T>(op, mutating, ct);
        if (!_channel.Writer.TryWrite(item))
        {
            item.Fail(ClosedReason());
        }
        return new ValueTask<T>(item.Task);
    }

    /// <summary>
    /// Starts the cooperative eviction: accepts no more operations, drains the pending ones, persists with the
    /// hook (if applicable) and releases the document. The returned <see cref="Task"/> completes when the document
    /// has been released.
    /// </summary>
    internal Task BeginEvictionAsync()
    {
        if (_state is DocumentActorState.Active or DocumentActorState.Idle)
        {
            _state = DocumentActorState.Idle;
            _channel.Writer.TryComplete();
        }
        return _runLoop;
    }

    private Exception ClosedReason() =>
        _fault ?? new ObjectDisposedException(nameof(DocumentSession),
            $"The document '{DocId}' was evicted or the broker was closed.");

    private async Task RunAsync()
    {
        try
        {
            await foreach (IWorkItem item in _channel.Reader.ReadAllAsync().ConfigureAwait(false))
            {
                if (_fault is not null)
                {
                    item.Fail(_fault); // after faulting, we drain the queue failing each pending item
                    continue;
                }

                try
                {
                    bool wantUpdates = item.IsMutating && AnySessionWantsUpdates();
                    byte[]? stateVector = wantUpdates ? _doc.ExportStateVector() : null;

                    item.Execute(_doc);
                    Interlocked.Exchange(ref _lastActivityTick, Environment.TickCount64);

                    if (stateVector is not null)
                    {
                        byte[] delta = _doc.ExportUpdateSince(stateVector);
                        if (delta.Length > 0)
                        {
                            NotifySessions(delta);
                        }
                    }
                }
                catch (Exception ex)
                {
                    // The operation failed WITHIN the turn: the document state may be invalid.
                    // The item already propagated the exception to its caller; the actor enters Faulted and drains.
                    _fault = ex;
                    _state = DocumentActorState.Faulted;
                    _channel.Writer.TryComplete();
                }
            }
        }
        finally
        {
            await FinalizeAsync().ConfigureAwait(false);
        }
    }

    private async ValueTask FinalizeAsync()
    {
        // Persistence only on graceful eviction (not on fault): drain → OnEvicting → release.
        // `_fault` is the AUTHORITATIVE fault signal: if non-null, the document is in an
        // unknown state and must not be persisted, regardless of `_state` (which another thread may have left
        // in Idle when starting the eviction right before the turn faulted).
        if (_fault is null)
        {
            if (_onEvicting is not null)
            {
                try
                {
                    byte[] state = _doc.ExportState();
                    await _onEvicting(DocId, state, CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    // Persistence is best-effort: if the hook fails, we release the document anyway
                    // (not leaving native memory dangling takes priority over not losing the snapshot). The hook's
                    // failure is the consumer's responsibility; we surface it via traces for observability.
                    System.Diagnostics.Debug.WriteLine(
                        $"[DocumentActor] OnEvicting failed for '{DocId}': {ex.GetType().Name}: {ex.Message}");
                }
            }
            _state = DocumentActorState.Evicted;
        }

        _doc.Dispose(); // exactly once: the loop terminates only once (P-I)
    }

    private bool AnySessionWantsUpdates()
    {
        lock (_sessionsLock)
        {
            foreach (DocumentSession s in _sessions)
            {
                if (s.WantsUpdates)
                {
                    return true;
                }
            }
        }
        return false;
    }

    private void NotifySessions(byte[] delta)
    {
        DocumentSession[] snapshot;
        lock (_sessionsLock)
        {
            if (_sessions.Count == 0)
            {
                return;
            }
            snapshot = _sessions.ToArray();
        }
        var mem = new ReadOnlyMemory<byte>(delta);
        foreach (DocumentSession s in snapshot)
        {
            // An UpdateApplied handler that throws must NOT fault the document for all sessions:
            // the bug is the consumer's (relay/persistence of M2), not the publisher's. It is isolated and traced.
            try
            {
                s.RaiseUpdateApplied(mem);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[DocumentActor] UpdateApplied handler threw for '{DocId}': {ex.GetType().Name}: {ex.Message}");
            }
        }
    }

    // -- Enqueued work items --

    private interface IWorkItem
    {
        bool IsMutating { get; }
        void Execute(ICrdtDoc doc);
        void Fail(Exception ex);
    }

    private sealed class WorkItem<T> : IWorkItem
    {
        private readonly Func<ICrdtDoc, T> _op;
        private readonly TaskCompletionSource<T> _tcs =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly CancellationToken _ct;

        internal WorkItem(Func<ICrdtDoc, T> op, bool mutating, CancellationToken ct)
        {
            _op = op;
            IsMutating = mutating;
            _ct = ct;
        }

        public bool IsMutating { get; }

        internal Task<T> Task => _tcs.Task;

        public void Execute(ICrdtDoc doc)
        {
            if (_ct.IsCancellationRequested)
            {
                _tcs.TrySetCanceled(_ct); // cancellation does not fault the actor
                return;
            }
            try
            {
                _tcs.TrySetResult(_op(doc));
            }
            catch (Exception ex)
            {
                _tcs.TrySetException(ex); // the caller sees the causal exception...
                throw;                    // ...and the actor faults (turn aborted)
            }
        }

        public void Fail(Exception ex) => _tcs.TrySetException(ex);
    }
}
