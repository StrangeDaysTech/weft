using Weft.Server.Protocol;

namespace Weft.Server.Persistence;

/// <summary>
/// Shared framing of a document's persisted state. Since the <see cref="IDocumentStore"/> blobs are
/// <b>opaque</b> (P-IV: the store does not know yrs types and cannot merge updates), the state that
/// <see cref="IDocumentStore.LoadAsync"/> returns is a <b>flat sequence of records</b>: the snapshot (if it
/// exists) followed by each update accumulated since that snapshot, in append order. Each record is framed
/// as a lib0 <c>VarUint8Array</c>.
/// </summary>
/// <remarks>
/// The relay (CHARTER-05) reconstructs the document by applying each record in order to a fresh yrs doc
/// (all are idempotent <c>apply_update</c> operations: reapplying an already-integrated update is a CRDT no-op,
/// which makes recovery tolerant of a snapshot and updates that overlap). This class is the only point
/// that knows the container format; the adapters share it and the contract suite verifies it.
/// </remarks>
public static class DocumentStateFraming
{
    /// <summary>
    /// Frames <paramref name="snapshot"/> (optional) and <paramref name="updates"/> into a sequence of records.
    /// Returns <c>null</c> if nothing is persisted (no snapshot and no updates) — the "nonexistent doc" signal.
    /// </summary>
    public static byte[]? Frame(byte[]? snapshot, IReadOnlyList<byte[]> updates)
    {
        if (snapshot is null && updates.Count == 0)
        {
            return null;
        }

        var w = new Lib0Encoding.Lib0Writer();
        if (snapshot is not null)
        {
            w.WriteVarUint8Array(snapshot);
        }

        foreach (byte[] update in updates)
        {
            w.WriteVarUint8Array(update);
        }

        return w.ToArray();
    }

    /// <summary>
    /// Reads back the records of a state framed by <see cref="Frame"/>. Each element is a copy of
    /// a record; they are applied in order. A truncated container fails with <see cref="MalformedMessageException"/>
    /// (the same structural guard of the codec).
    /// </summary>
    public static IReadOnlyList<byte[]> ReadRecords(ReadOnlySpan<byte> framed)
    {
        var records = new List<byte[]>();
        var r = new Lib0Encoding.Lib0Reader(framed);
        while (!r.AtEnd)
        {
            records.Add(r.ReadVarUint8Array().ToArray());
        }

        return records;
    }
}
