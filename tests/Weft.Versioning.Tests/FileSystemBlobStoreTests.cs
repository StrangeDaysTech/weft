using System.Text;
using Weft.Versioning.Blobs;

namespace Weft.Versioning.Tests;

/// <summary>
/// Cobertura directa de <see cref="FileSystemBlobStore"/> (FU-009 / T024): round-trip, ausencia,
/// layout de sharding <c>aa/bb/hash</c>, idempotencia por content-addressing y limpieza de temporales.
/// El content-addressing es puro (no depende de ningún motor): se prueba con blobs arbitrarios.
/// </summary>
public sealed class FileSystemBlobStoreTests : IDisposable
{
    private readonly string _root =
        Path.Combine(Path.GetTempPath(), "weft-fsblob-" + Path.GetRandomFileName());

    private FileSystemBlobStore NewStore() => new(_root);

    private static (VersionId id, byte[] blob) Blob(string content)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(content);
        return (VersionId.FromBlob(bytes), bytes);
    }

    [Fact]
    public async Task Put_then_Get_roundtrips_bytes()
    {
        FileSystemBlobStore store = NewStore();
        (VersionId id, byte[] blob) = Blob("contenido content-addressed áéí");

        await store.PutAsync(id, blob);

        Assert.True(await store.ExistsAsync(id));
        Assert.Equal(blob, await store.GetAsync(id));
    }

    [Fact]
    public async Task Missing_id_returns_null_and_false()
    {
        FileSystemBlobStore store = NewStore();
        (VersionId id, _) = Blob("nunca almacenado");

        Assert.False(await store.ExistsAsync(id));
        Assert.Null(await store.GetAsync(id));
    }

    [Fact]
    public async Task Put_lays_blob_under_two_level_shard()
    {
        FileSystemBlobStore store = NewStore();
        (VersionId id, byte[] blob) = Blob("sharding aa/bb/hash");

        await store.PutAsync(id, blob);

        string hex = id.ToString();
        string expected = Path.Combine(_root, hex[..2], hex[2..4], hex);
        Assert.True(File.Exists(expected), $"Se esperaba el blob en {expected}");
    }

    [Fact]
    public async Task Put_twice_same_blob_is_idempotent()
    {
        FileSystemBlobStore store = NewStore();
        (VersionId id, byte[] blob) = Blob("dedup por hash");

        await store.PutAsync(id, blob);
        await store.PutAsync(id, blob); // no debe fallar (idempotente)

        string hex = id.ToString();
        string shardDir = Path.Combine(_root, hex[..2], hex[2..4]);
        Assert.Single(Directory.GetFiles(shardDir));
        Assert.Equal(blob, await store.GetAsync(id));
    }

    [Fact]
    public async Task Put_leaves_no_temp_files()
    {
        FileSystemBlobStore store = NewStore();
        (VersionId id, byte[] blob) = Blob("escritura atómica");

        await store.PutAsync(id, blob);

        string[] temps = Directory.GetFiles(_root, "*.tmp-*", SearchOption.AllDirectories);
        Assert.Empty(temps);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
