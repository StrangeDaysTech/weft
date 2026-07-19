namespace Weft.Concurrency;

/// <summary>
/// Asynchronous facade of a document managed by the <see cref="DocumentBroker"/>. Mirror of
/// <see cref="ICrdtDoc"/> where each call is enqueued to the document's actor and executed serialized
/// (constitution P-V). Several sessions can share the same document; all receive the
/// <see cref="UpdateApplied"/> event. It does not expose the underlying <see cref="ICrdtDoc"/> except,
/// transiently, inside the delegate of <see cref="ExecuteAsync{T}"/>.
/// </summary>
public sealed class DocumentSession : IAsyncDisposable
{
    private readonly DocumentActor _actor;
    private bool _disposed;

    internal DocumentSession(DocumentActor actor)
    {
        _actor = actor;
        DocId = actor.DocId;
    }

    /// <summary>Logical identifier of the document.</summary>
    public string DocId { get; }

    /// <summary>
    /// Fires after each update applied to the document (own or imported by another session), with the
    /// corresponding delta. Intended for relay/persistence (M2). The handler is invoked within the actor's
    /// turn: it must not block waiting for another operation of the same document.
    /// </summary>
    public event Action<DocumentSession, ReadOnlyMemory<byte>>? UpdateApplied;

    /// <summary>Inserts text into the given field (enqueued and serialized).</summary>
    public async ValueTask InsertTextAsync(string field, int index, string text, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(field);
        ArgumentNullException.ThrowIfNull(text);
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ThrowIfDisposed();
        await _actor.EnqueueAsync(doc => { doc.InsertText(field, index, text); return true; }, mutating: true, ct)
            .ConfigureAwait(false);
    }

    /// <summary>Deletes text from the given field (enqueued and serialized).</summary>
    public async ValueTask DeleteTextAsync(string field, int index, int length, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(field);
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        ThrowIfDisposed();
        await _actor.EnqueueAsync(doc => { doc.DeleteText(field, index, length); return true; }, mutating: true, ct)
            .ConfigureAwait(false);
    }

    /// <summary>Reads the full content of the given field.</summary>
    public ValueTask<string> GetTextAsync(string field, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(field);
        ThrowIfDisposed();
        return _actor.EnqueueAsync(doc => doc.GetText(field), mutating: false, ct);
    }

    /// <summary>Exports the full state of the document (basis of content-addressing).</summary>
    public ValueTask<byte[]> ExportStateAsync(CancellationToken ct = default)
    {
        ThrowIfDisposed();
        return _actor.EnqueueAsync(doc => doc.ExportState(), mutating: false, ct);
    }

    /// <summary>Exports the state vector ("what I know") for incremental sync.</summary>
    public ValueTask<byte[]> ExportStateVectorAsync(CancellationToken ct = default)
    {
        ThrowIfDisposed();
        return _actor.EnqueueAsync(doc => doc.ExportStateVector(), mutating: false, ct);
    }

    /// <summary>Exports the delta with the changes the sender of the state vector does not know.</summary>
    public ValueTask<byte[]> ExportUpdateSinceAsync(ReadOnlyMemory<byte> stateVector, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        byte[] sv = stateVector.ToArray(); // defensive copy: the caller's buffer may change before the turn
        return _actor.EnqueueAsync(doc => doc.ExportUpdateSince(sv), mutating: false, ct);
    }

    /// <summary>Merges an update/state from another replica (convergent); fires <see cref="UpdateApplied"/>.</summary>
    public async ValueTask ApplyUpdateAsync(ReadOnlyMemory<byte> update, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        byte[] u = update.ToArray(); // defensive copy (see ExportUpdateSinceAsync)
        await _actor.EnqueueAsync(doc => { doc.ApplyUpdate(u); return true; }, mutating: true, ct)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Applies an update and RETURNS its delta within the same actor turn. Intended for the relay with
    /// persist-before-broadcast (FU-010): the delta is captured as a return value —race-free against
    /// several concurrent connections of the same document— to broadcast it after persisting, instead of
    /// relying on the <see cref="UpdateApplied"/> event (which fires within the turn, before
    /// persisting). The delta is empty if the update brought no new changes (idempotent).
    /// </summary>
    public ValueTask<byte[]> ApplyAndCaptureDeltaAsync(ReadOnlyMemory<byte> update, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        byte[] u = update.ToArray(); // defensive copy (see ExportUpdateSinceAsync)
        return _actor.EnqueueAsync(
            doc =>
            {
                byte[] before = doc.ExportStateVector();
                doc.ApplyUpdate(u);
                return doc.ExportUpdateSince(before);
            },
            mutating: true,
            ct);
    }

    /// <summary>
    /// Executes a delegate as an atomic turn with respect to the other operations of the same document
    /// (logical transaction). The received <see cref="ICrdtDoc"/> must NOT be captured or used outside the
    /// delegate: it is only valid during the execution of the turn.
    /// </summary>
    public ValueTask<T> ExecuteAsync<T>(Func<ICrdtDoc, T> operation, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ThrowIfDisposed();
        return _actor.EnqueueAsync(operation, mutating: true, ct);
    }

    internal bool WantsUpdates => UpdateApplied is not null;

    internal void RaiseUpdateApplied(ReadOnlyMemory<byte> delta) => UpdateApplied?.Invoke(this, delta);

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

    /// <summary>Closes the session: stops receiving events and releases its reference in the actor. It does not
    /// evict the document (its lifecycle is managed by the broker via idle/LRU).</summary>
    public ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return ValueTask.CompletedTask;
        }
        _disposed = true;
        UpdateApplied = null;
        _actor.RemoveSession(this);
        return ValueTask.CompletedTask;
    }
}
