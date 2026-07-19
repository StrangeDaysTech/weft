namespace Weft.Server.Persistence;

/// <summary>
/// Durable per-document state (FR-017). The blobs are <b>opaque</b>: the store never interprets yrs bytes
/// (P-IV). Every implementation is <b>thread-safe</b> per document and passes the same shared contract suite
/// (<c>DocumentStoreContractSuite</c>), the interchangeability guarantee required by the US3 acceptance
/// scenario.
/// </summary>
/// <remarks>
/// A document's persisted state is a consolidated snapshot plus the incremental updates accumulated
/// since that snapshot. <see cref="LoadAsync"/> returns them framed by <see cref="DocumentStateFraming"/>
/// (snapshot first, then the updates in append order); the relay applies each record in order to
/// reconstruct the document. <see cref="SaveSnapshotAsync"/> performs <b>compaction</b>: it replaces the snapshot and
/// discards the accumulated updates.
/// </remarks>
public interface IDocumentStore
{
    /// <summary>
    /// Full persisted state of the document (snapshot + accumulated updates, framed by
    /// <see cref="DocumentStateFraming"/>), or <c>null</c> if the document does not exist.
    /// </summary>
    ValueTask<byte[]?> LoadAsync(string docId, CancellationToken ct = default);

    /// <summary>Appends an incremental update to the document's durable queue (durability between snapshots).</summary>
    ValueTask AppendUpdateAsync(string docId, ReadOnlyMemory<byte> update, CancellationToken ct = default);

    /// <summary>
    /// Saves a consolidated snapshot: replaces the state and discards the accumulated updates (compaction).
    /// </summary>
    ValueTask SaveSnapshotAsync(string docId, ReadOnlyMemory<byte> state, CancellationToken ct = default);
}
