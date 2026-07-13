using System.Collections.Concurrent;
using Weft.Server.Persistence;

namespace Weft.Server.Tests;

/// <summary>
/// Contract suite compartida de <see cref="IDocumentStore"/> (T050). Se ejecuta <b>idéntica</b> contra cada
/// adaptador (aquí <see cref="InMemoryDocumentStore"/> y <see cref="FileSystemDocumentStore"/>; en CHARTER-06,
/// EFCore y Redis sin modificarla). Es la base de la intercambiabilidad de stores que exige el escenario de
/// aceptación de US3 y <c>contracts/server-api.md</c>.
/// </summary>
public abstract class DocumentStoreContractSuite
{
    /// <summary>Crea una instancia fresca y aislada del store bajo prueba.</summary>
    protected abstract IDocumentStore CreateStore();

    private const string DocId = "doc-α/β:1";  // opaco: incluye no-ASCII y '/' para tensar el mapeo a filename.

    private static ReadOnlyMemory<byte> Mem(params byte[] bytes) => bytes;

    private static IReadOnlyList<byte[]> Records(byte[]? framed)
    {
        Assert.NotNull(framed);
        return DocumentStateFraming.ReadRecords(framed);
    }

    [Fact]
    public async Task Load_of_unknown_doc_returns_null()
    {
        IDocumentStore store = CreateStore();
        Assert.Null(await store.LoadAsync("nunca-escrito"));
    }

    [Fact]
    public async Task Appends_load_in_order()
    {
        IDocumentStore store = CreateStore();
        byte[] a = [0xA0, 0xA1];
        byte[] b = [0xB0];
        byte[] c = [0xC0, 0xC1, 0xC2];

        await store.AppendUpdateAsync(DocId, a);
        await store.AppendUpdateAsync(DocId, b);
        await store.AppendUpdateAsync(DocId, c);

        IReadOnlyList<byte[]> records = Records(await store.LoadAsync(DocId));
        Assert.Equal(3, records.Count);
        Assert.Equal(a, records[0]);
        Assert.Equal(b, records[1]);
        Assert.Equal(c, records[2]);
    }

    [Fact]
    public async Task Snapshot_alone_loads_as_single_record()
    {
        IDocumentStore store = CreateStore();
        byte[] snapshot = [0x53, 0x53, 0x53];

        await store.SaveSnapshotAsync(DocId, snapshot);

        IReadOnlyList<byte[]> records = Records(await store.LoadAsync(DocId));
        Assert.Equal(snapshot, Assert.Single(records));
    }

    [Fact]
    public async Task Snapshot_replaces_accumulated_updates_then_new_updates_append()
    {
        IDocumentStore store = CreateStore();
        await store.AppendUpdateAsync(DocId, Mem(0x01));
        await store.AppendUpdateAsync(DocId, Mem(0x02));

        byte[] snapshot = [0x53];
        await store.SaveSnapshotAsync(DocId, snapshot);

        // Compaction: tras el snapshot solo queda el snapshot (los updates acumulados se descartaron).
        IReadOnlyList<byte[]> afterSnapshot = Records(await store.LoadAsync(DocId));
        Assert.Equal(snapshot, Assert.Single(afterSnapshot));

        // Updates posteriores se acumulan sobre el snapshot, en orden.
        byte[] c = [0xC0];
        await store.AppendUpdateAsync(DocId, c);
        IReadOnlyList<byte[]> afterAppend = Records(await store.LoadAsync(DocId));
        Assert.Equal(2, afterAppend.Count);
        Assert.Equal(snapshot, afterAppend[0]);
        Assert.Equal(c, afterAppend[1]);
    }

    [Fact]
    public async Task Documents_are_isolated()
    {
        IDocumentStore store = CreateStore();
        await store.AppendUpdateAsync("doc-1", Mem(0x11));
        await store.AppendUpdateAsync("doc-2", Mem(0x22));

        Assert.Equal([0x11], Assert.Single(Records(await store.LoadAsync("doc-1"))));
        Assert.Equal([0x22], Assert.Single(Records(await store.LoadAsync("doc-2"))));
    }

    [Fact]
    public async Task Large_blob_round_trips()
    {
        IDocumentStore store = CreateStore();
        byte[] big = new byte[4 * 1024 * 1024];
        for (int i = 0; i < big.Length; i++)
        {
            big[i] = (byte)(i * 31 + 7);
        }

        await store.SaveSnapshotAsync(DocId, big);
        IReadOnlyList<byte[]> records = Records(await store.LoadAsync(DocId));
        Assert.Equal(big, Assert.Single(records));
    }

    [Fact]
    public async Task Concurrent_appends_are_all_persisted()
    {
        IDocumentStore store = CreateStore();
        const int n = 250;

        // Cada update es su índice en 4 bytes → distinguible; el orden entre appends concurrentes no se
        // garantiza, así que verificamos el conjunto, no la secuencia.
        await Task.WhenAll(Enumerable.Range(0, n).Select(i =>
            store.AppendUpdateAsync(DocId, BitConverter.GetBytes(i)).AsTask()));

        IReadOnlyList<byte[]> records = Records(await store.LoadAsync(DocId));
        Assert.Equal(n, records.Count);

        var seen = new HashSet<int>(records.Select(r => BitConverter.ToInt32(r)));
        Assert.Equal(n, seen.Count);
        Assert.True(Enumerable.Range(0, n).All(seen.Contains));
    }

    [Fact]
    public async Task Concurrent_loads_and_writes_do_not_fault()
    {
        IDocumentStore store = CreateStore();
        await store.SaveSnapshotAsync(DocId, Mem(0x00));

        var errors = new ConcurrentQueue<Exception>();
        var tasks = new List<Task>();
        for (int i = 0; i < 100; i++)
        {
            int idx = i;
            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    if (idx % 2 == 0)
                    {
                        await store.AppendUpdateAsync(DocId, BitConverter.GetBytes(idx));
                    }
                    else
                    {
                        _ = await store.LoadAsync(DocId);
                    }
                }
                catch (Exception ex)
                {
                    errors.Enqueue(ex);
                }
            }));
        }

        await Task.WhenAll(tasks);
        Assert.Empty(errors);
    }
}

/// <summary>La contract suite contra <see cref="InMemoryDocumentStore"/>.</summary>
public sealed class InMemoryDocumentStoreContractTests : DocumentStoreContractSuite
{
    protected override IDocumentStore CreateStore() => new InMemoryDocumentStore();
}

/// <summary>La contract suite contra <see cref="FileSystemDocumentStore"/> (directorio temporal aislado).</summary>
public sealed class FileSystemDocumentStoreContractTests : DocumentStoreContractSuite, IDisposable
{
    private readonly string _root =
        Path.Combine(Path.GetTempPath(), "weft-fs-store-" + Guid.NewGuid().ToString("N"));

    protected override IDocumentStore CreateStore() => new FileSystemDocumentStore(_root);

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
