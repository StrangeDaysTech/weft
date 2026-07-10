namespace Weft.Versioning.Blobs;

/// <summary>
/// Almacén content-addressed (hash → blob). Las implementaciones deben ser thread-safe.
/// Sin <c>delete</c> en v1: las versiones publicadas son inmutables (FR-012); la retención es
/// dominio del consumidor.
/// </summary>
public interface IBlobStore
{
    /// <summary>Persiste un blob bajo su identidad. Idempotente: put del mismo contenido es no-op
    /// (dedup natural por hash).</summary>
    ValueTask PutAsync(VersionId id, ReadOnlyMemory<byte> blob, CancellationToken ct = default);

    /// <summary>Devuelve el blob, o <c>null</c> si no existe.</summary>
    ValueTask<byte[]?> GetAsync(VersionId id, CancellationToken ct = default);

    /// <summary>Indica si existe un blob para la identidad dada.</summary>
    ValueTask<bool> ExistsAsync(VersionId id, CancellationToken ct = default);
}
