namespace Weft.Versioning.Blobs;

/// <summary>
/// Content-addressed store over the file system (v1). Sharding <c>aa/bb/hash</c> to
/// avoid huge directories; atomic write (temp + rename) so blobs are never left half-written.
/// Thread-safe: content-addressing means two writers of the same hash write the same thing.
/// </summary>
public sealed class FileSystemBlobStore : IBlobStore
{
    private readonly string _root;

    /// <summary>Creates the store rooted at <paramref name="rootDirectory"/> (creates it if it does not exist).</summary>
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
            return; // idempotent (dedup by hash)
        }
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        string tmp = path + ".tmp-" + Path.GetRandomFileName();
        await File.WriteAllBytesAsync(tmp, blob.ToArray(), ct).ConfigureAwait(false);
        try
        {
            // Atomic rename within the same directory; overwrite:false → if another writer won
            // the race, both wrote the SAME content (content-addressed), so it does not matter.
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
