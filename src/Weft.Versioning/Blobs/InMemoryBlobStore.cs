using System.Collections.Concurrent;

namespace Weft.Versioning.Blobs;

/// <summary>In-memory content-addressed store (tests/dev). Thread-safe.</summary>
public sealed class InMemoryBlobStore : IBlobStore
{
    private readonly ConcurrentDictionary<VersionId, byte[]> _blobs = new();

    /// <summary>Number of distinct blobs stored (useful to verify dedup/compaction).</summary>
    public int Count => _blobs.Count;

    /// <inheritdoc/>
    public ValueTask PutAsync(VersionId id, ReadOnlyMemory<byte> blob, CancellationToken ct = default)
    {
        // Idempotent: if it already exists, it is not re-copied (dedup by hash).
        _blobs.TryAdd(id, blob.ToArray());
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask<byte[]?> GetAsync(VersionId id, CancellationToken ct = default) =>
        ValueTask.FromResult(_blobs.TryGetValue(id, out byte[]? blob) ? blob : null);

    /// <inheritdoc/>
    public ValueTask<bool> ExistsAsync(VersionId id, CancellationToken ct = default) =>
        ValueTask.FromResult(_blobs.ContainsKey(id));
}
