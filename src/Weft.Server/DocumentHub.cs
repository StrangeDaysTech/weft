using System.Collections.Concurrent;
using Weft.Concurrency;
using Weft.Server.Persistence;
using Weft.Server.Protocol;

namespace Weft.Server;

/// <summary>
/// Meeting point of all connections for a single document. Keeps <b>one</b>
/// <see cref="DocumentSession"/> per document (the session refcount keeps the document resident
/// while there are connections). Applies each update, persists it and broadcasts its delta; the order between
/// persisting and broadcasting is set by <see cref="WeftServerOptions.Durability"/> (FU-010).
/// </summary>
internal sealed class DocumentHub : IAsyncDisposable
{
    private readonly IDocumentStore _store;
    private readonly DurabilityMode _durability;
    private readonly ConcurrentDictionary<WeftConnection, byte> _connections = new();
    private int _disposed;

    public DocumentHub(string docId, DocumentSession session, IDocumentStore store, DurabilityMode durability)
    {
        DocId = docId;
        Session = session;
        _store = store;
        _durability = durability;
        // The broadcast is EXPLICIT (ApplyAndPersistAsync), not via the UpdateApplied event: capturing the
        // delta as the return value of the turn allows ordering append/broadcast according to the mode and is
        // race-free between concurrent connections of the same document. The event is kept for other
        // DocumentSession consumers, but the relay no longer depends on it.
    }

    /// <summary>Document identifier.</summary>
    public string DocId { get; }

    /// <summary>Shared async session of the document (serialized actor turn, P-V).</summary>
    public DocumentSession Session { get; }

    /// <summary>Active connections of the document.</summary>
    public int ConnectionCount => _connections.Count;

    public void Add(WeftConnection connection) => _connections.TryAdd(connection, 0);

    public void Remove(WeftConnection connection) => _connections.TryRemove(connection, out _);

    /// <summary>Requests the close of all the document's connections (their teardown removes them from the hub).</summary>
    public void DisconnectAll()
    {
        foreach (WeftConnection c in _connections.Keys)
        {
            c.RequestClose();
        }
    }

    /// <summary>
    /// Broadcasts a frame to the document's connections, optionally excluding the origin. Each send is isolated:
    /// a failure/backpressure of one connection (which closes it) does not affect the peers.
    /// </summary>
    public void Broadcast(byte[] frame, WeftConnection? exclude)
    {
        foreach (WeftConnection c in _connections.Keys)
        {
            if (!ReferenceEquals(c, exclude))
            {
                c.TryEnqueue(frame);
            }
        }
    }

    /// <summary>
    /// Applies an incoming update to the document (actor turn), persists it and broadcasts its delta. The order
    /// between persisting and broadcasting is set by <see cref="WeftServerOptions.Durability"/> (FU-010).
    /// </summary>
    /// <remarks>
    /// In <see cref="DurabilityMode.PersistThenBroadcast"/>, if the append fails, the update is already in the
    /// live document but was NEVER broadcast: the peers would stay silent and out of date forever
    /// (the next edit broadcasts only its own delta, not the one that was missed). That is why an append
    /// failure closes ALL the document's connections (the caller closes them with 1011): they reconnect, send
    /// SyncStep1, and the server —authoritative, which does hold the update— resends the state. Convergence recovered.
    /// </remarks>
    public async ValueTask ApplyAndPersistAsync(byte[] update, CancellationToken ct)
    {
        byte[] delta = await Session.ApplyAndCaptureDeltaAsync(update, ct).ConfigureAwait(false);

        if (_durability == DurabilityMode.BroadcastThenPersist)
        {
            BroadcastDelta(delta);
            await _store.AppendUpdateAsync(DocId, update, ct).ConfigureAwait(false);
            return;
        }

        // PersistThenBroadcast (default): persist before any peer sees it.
        try
        {
            await _store.AppendUpdateAsync(DocId, update, ct).ConfigureAwait(false);
        }
        catch
        {
            // The update stayed applied but not broadcast: closing the whole document forces the authoritative
            // re-sync. Rethrowing lets the caller close this connection with 1011.
            DisconnectAll();
            throw;
        }

        BroadcastDelta(delta);
    }

    // The delta is broadcast to ALL the document's connections. Reapplying its own delta at the origin is an
    // idempotent CRDT no-op (Yjs clients tolerate it), which avoids tracking the origin inside the actor turn
    // (which would be a race). The cost is an echo to the sender; acceptable for v1.
    private void BroadcastDelta(byte[] delta)
    {
        if (delta.Length == 0)
        {
            return;
        }

        Broadcast(SyncProtocol.EncodeUpdate(delta), exclude: null);
    }

    /// <summary>
    /// Closes the hub: unsubscribes the broadcast, consolidates a snapshot (compaction) and releases the session
    /// (which lets the broker evict the document for inactivity). Idempotent.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        try
        {
            // Consolidation snapshot: the full state within the actor turn replaces the updates
            // accumulated in the store (compaction). Best-effort: a failure must not break the session close.
            byte[] snapshot = await Session.ExportStateAsync().ConfigureAwait(false);
            await _store.SaveSnapshotAsync(DocId, snapshot).ConfigureAwait(false);
        }
        catch
        {
            // The broker's eviction (OnEvicting) also persists; durability does not depend on this alone.
        }
        finally
        {
            await Session.DisposeAsync().ConfigureAwait(false);
        }
    }
}
