using System.Collections.Concurrent;

namespace Weft.Server.Persistence;

/// <summary>
/// <see cref="IDocumentStore"/> backed by memory: for tests and development. Does not survive the process.
/// Thread-safe per document via a per-entry lock; the global map uses <see cref="ConcurrentDictionary{TKey,TValue}"/>.
/// </summary>
public sealed class InMemoryDocumentStore : IDocumentStore
{
    private sealed class Entry
    {
        public readonly object Gate = new();
        public byte[]? Snapshot;
        public readonly List<byte[]> Updates = new();
    }

    private readonly ConcurrentDictionary<string, Entry> _docs = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public ValueTask<byte[]?> LoadAsync(string docId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(docId);
        ct.ThrowIfCancellationRequested();

        if (!_docs.TryGetValue(docId, out Entry? entry))
        {
            return new ValueTask<byte[]?>((byte[]?)null);
        }

        lock (entry.Gate)
        {
            byte[]? framed = DocumentStateFraming.Frame(entry.Snapshot, entry.Updates);
            return new ValueTask<byte[]?>(framed);
        }
    }

    /// <inheritdoc />
    public ValueTask AppendUpdateAsync(string docId, ReadOnlyMemory<byte> update, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(docId);
        ct.ThrowIfCancellationRequested();

        Entry entry = _docs.GetOrAdd(docId, static _ => new Entry());
        lock (entry.Gate)
        {
            entry.Updates.Add(update.ToArray());
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask SaveSnapshotAsync(string docId, ReadOnlyMemory<byte> state, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(docId);
        ct.ThrowIfCancellationRequested();

        Entry entry = _docs.GetOrAdd(docId, static _ => new Entry());
        lock (entry.Gate)
        {
            entry.Snapshot = state.ToArray();
            entry.Updates.Clear();
        }

        return ValueTask.CompletedTask;
    }
}
