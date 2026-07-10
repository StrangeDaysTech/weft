namespace Weft.Versioning.Blobs;

/// <summary>
/// Almacén content-addressed sobre el sistema de archivos (v1). Sharding <c>aa/bb/hash</c> para
/// evitar directorios enormes; escritura atómica (temp + rename) para no dejar blobs a medias.
/// Thread-safe: el content-addressing hace que dos escritores del mismo hash escriban lo mismo.
/// </summary>
public sealed class FileSystemBlobStore : IBlobStore
{
    private readonly string _root;

    /// <summary>Crea el almacén enraizado en <paramref name="rootDirectory"/> (lo crea si no existe).</summary>
    public FileSystemBlobStore(string rootDirectory)
    {
        ArgumentException.ThrowIfNullOrEmpty(rootDirectory);
        _root = rootDirectory;
        Directory.CreateDirectory(_root);
    }

    private string PathFor(VersionId id)
    {
        string hex = id.ToString();
        return Path.Combine(_root, hex[..2], hex[2..4], hex);
    }

    /// <inheritdoc/>
    public async ValueTask PutAsync(VersionId id, ReadOnlyMemory<byte> blob, CancellationToken ct = default)
    {
        string path = PathFor(id);
        if (File.Exists(path))
        {
            return; // idempotente (dedup por hash)
        }
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        string tmp = path + ".tmp-" + Path.GetRandomFileName();
        await File.WriteAllBytesAsync(tmp, blob.ToArray(), ct).ConfigureAwait(false);
        try
        {
            // Rename atómico dentro del mismo directorio; overwrite:false → si otro escritor ganó
            // la carrera, ambos escribieron el MISMO contenido (content-addressed), así que da igual.
            File.Move(tmp, path, overwrite: false);
        }
        catch (IOException) when (File.Exists(path))
        {
            File.Delete(tmp);
        }
    }

    /// <inheritdoc/>
    public async ValueTask<byte[]?> GetAsync(VersionId id, CancellationToken ct = default)
    {
        string path = PathFor(id);
        if (!File.Exists(path))
        {
            return null;
        }
        return await File.ReadAllBytesAsync(path, ct).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public ValueTask<bool> ExistsAsync(VersionId id, CancellationToken ct = default) =>
        ValueTask.FromResult(File.Exists(PathFor(id)));
}
