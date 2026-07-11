using System.Collections.Concurrent;

namespace Weft.Versioning.Blobs;

/// <summary>Almacén content-addressed en memoria (tests/dev). Thread-safe.</summary>
public sealed class InMemoryBlobStore : IBlobStore
{
    private readonly ConcurrentDictionary<string, byte[]> _blobs = new(StringComparer.Ordinal);

    /// <summary>Número de blobs distintos almacenados (útil para verificar dedup/compactación).</summary>
    public int Count => _blobs.Count;

    /// <inheritdoc/>
    public ValueTask PutAsync(VersionId id, ReadOnlyMemory<byte> blob, CancellationToken ct = default)
    {
        // Idempotente: si ya existe, no se re-copia (dedup por hash).
        _blobs.TryAdd(id.ToString(), blob.ToArray());
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask<byte[]?> GetAsync(VersionId id, CancellationToken ct = default) =>
        ValueTask.FromResult(_blobs.TryGetValue(id.ToString(), out byte[]? blob) ? blob : null);

    /// <inheritdoc/>
    public ValueTask<bool> ExistsAsync(VersionId id, CancellationToken ct = default) =>
        ValueTask.FromResult(_blobs.ContainsKey(id.ToString()));
}
