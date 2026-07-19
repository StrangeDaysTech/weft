namespace Weft.Versioning.Blobs;

/// <summary>
/// Content-addressed store (hash → blob). Implementations must be thread-safe.
/// No <c>delete</c> in v1: published versions are immutable (FR-012); retention is
/// the consumer's domain.
/// </summary>
public interface IBlobStore
{
    /// <summary>Persists a blob under its identity. Idempotent: putting the same content is a no-op
    /// (natural dedup by hash).</summary>
    ValueTask PutAsync(VersionId id, ReadOnlyMemory<byte> blob, CancellationToken ct = default);

    /// <summary>Returns the blob, or <c>null</c> if it does not exist.</summary>
    ValueTask<byte[]?> GetAsync(VersionId id, CancellationToken ct = default);

    /// <summary>Indicates whether a blob exists for the given identity.</summary>
    ValueTask<bool> ExistsAsync(VersionId id, CancellationToken ct = default);
}
