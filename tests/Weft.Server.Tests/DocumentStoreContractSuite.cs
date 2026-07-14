using System.Collections.Concurrent;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using Weft.Server.Persistence;
using Weft.Server.Persistence.EFCore;
using Weft.Server.Persistence.Redis;

namespace Weft.Server.Tests;

/// <summary>
/// Contract suite compartida de <see cref="IDocumentStore"/> (T050). Se ejecuta <b>idéntica</b> contra cada
/// adaptador (<see cref="InMemoryDocumentStore"/>, <see cref="FileSystemDocumentStore"/> y, desde CHARTER-06,
/// <see cref="EFCoreDocumentStore"/> y <see cref="RedisDocumentStore"/>). Es la base de la intercambiabilidad
/// de stores que exige el escenario de aceptación de US3 y <c>contracts/server-api.md</c>.
/// </summary>
/// <remarks>
/// Los tests usan <c>[SkippableFact]</c> (no <c>[Fact]</c>) para que un adaptador cuyo backend externo no esté
/// disponible pueda <b>omitirse</b> en vez de fallar: la subclase Redis llama <c>Skip.IfNot(...)</c> en
/// <see cref="CreateStore"/> (la primera línea de cada test), así que sin Redis/Valkey el test se salta.
/// Para los adaptadores in-proceso (InMemory, FileSystem, EFCore/SQLite) el comportamiento es idéntico a
/// <c>[Fact]</c> — nunca se salta.
/// </remarks>
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

    [SkippableFact]
    public async Task Load_of_unknown_doc_returns_null()
    {
        IDocumentStore store = CreateStore();
        Assert.Null(await store.LoadAsync("nunca-escrito"));
    }

    [SkippableFact]
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

    [SkippableFact]
    public async Task Snapshot_alone_loads_as_single_record()
    {
        IDocumentStore store = CreateStore();
        byte[] snapshot = [0x53, 0x53, 0x53];

        await store.SaveSnapshotAsync(DocId, snapshot);

        IReadOnlyList<byte[]> records = Records(await store.LoadAsync(DocId));
        Assert.Equal(snapshot, Assert.Single(records));
    }

    [SkippableFact]
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

    [SkippableFact]
    public async Task Documents_are_isolated()
    {
        IDocumentStore store = CreateStore();
        await store.AppendUpdateAsync("doc-1", Mem(0x11));
        await store.AppendUpdateAsync("doc-2", Mem(0x22));

        Assert.Equal([0x11], Assert.Single(Records(await store.LoadAsync("doc-1"))));
        Assert.Equal([0x22], Assert.Single(Records(await store.LoadAsync("doc-2"))));
    }

    [SkippableFact]
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

    [SkippableFact]
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

    [SkippableFact]
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

/// <summary>
/// La contract suite contra <see cref="EFCoreDocumentStore"/> (CHARTER-06/T053) sobre <b>SQLite</b> en un archivo
/// temporal aislado — provider real, relacional, cross-plataforma, sin infraestructura externa.
/// </summary>
public sealed class EFCoreDocumentStoreContractTests : DocumentStoreContractSuite, IDisposable
{
    private readonly string _dbPath =
        Path.Combine(Path.GetTempPath(), "weft-efcore-store-" + Guid.NewGuid().ToString("N") + ".db");

    protected override IDocumentStore CreateStore()
    {
        DbContextOptions<WeftDocumentStoreContext> options =
            new DbContextOptionsBuilder<WeftDocumentStoreContext>()
                .UseSqlite($"Data Source={_dbPath}")
                .Options;

        var factory = new SqliteContextFactory(options);
        using (WeftDocumentStoreContext ctx = factory.CreateDbContext())
        {
            ctx.Database.EnsureCreated();
        }

        return new EFCoreDocumentStore(factory);
    }

    /// <summary>Factory mínima de contextos SQLite para los tests (una unidad de trabajo fresca por operación).</summary>
    private sealed class SqliteContextFactory(DbContextOptions<WeftDocumentStoreContext> options)
        : IDbContextFactory<WeftDocumentStoreContext>
    {
        public WeftDocumentStoreContext CreateDbContext() => new(options);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();  // suelta el handle del archivo antes de borrarlo.
        if (File.Exists(_dbPath))
        {
            try
            {
                File.Delete(_dbPath);
            }
            catch (IOException)
            {
                // best-effort: un handle rezagado del pool deja el archivo temporal; inofensivo.
            }
        }
    }
}

/// <summary>
/// Conexión Redis/Valkey compartida para la suite (una por clase). Si el servidor no está disponible en
/// <c>WEFT_TEST_REDIS</c> (o <c>localhost:6379</c>), <see cref="Available"/> es <c>false</c> y los tests se
/// omiten. Usa la base de datos <see cref="TestDb"/> (aislada de la 0 por defecto) y la limpia al cerrar.
/// </summary>
public sealed class RedisConnectionFixture : IDisposable
{
    /// <summary>Índice de base de datos dedicado a los tests (no toca la db 0 por defecto del servidor).</summary>
    public const int TestDb = 15;

    /// <summary>La conexión, o <c>null</c> si el servidor no respondió.</summary>
    public IConnectionMultiplexer? Connection { get; }

    /// <summary><c>true</c> si hay una conexión viva contra Redis/Valkey.</summary>
    public bool Available => Connection is { IsConnected: true };

    /// <summary>Intenta conectar una sola vez (sin abortar si falla) para decidir si la suite corre o se omite.</summary>
    public RedisConnectionFixture()
    {
        string config = Environment.GetEnvironmentVariable("WEFT_TEST_REDIS") ?? "localhost:6379";
        var options = ConfigurationOptions.Parse(config);
        options.AbortOnConnectFail = false;   // no lanza si el servidor no está: devuelve un mux desconectado.
        options.ConnectTimeout = 1000;
        options.ConnectRetry = 1;
        options.AllowAdmin = true;            // habilita FLUSHDB en el cleanup.

        ConnectionMultiplexer mux = ConnectionMultiplexer.Connect(options);
        if (mux.IsConnected)
        {
            Connection = mux;
        }
        else
        {
            mux.Dispose();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Connection is null)
        {
            return;
        }

        try
        {
            foreach (System.Net.EndPoint endpoint in Connection.GetEndPoints())
            {
                Connection.GetServer(endpoint).FlushDatabase(TestDb);
            }
        }
        catch (RedisException)
        {
            // best-effort: si el servidor deshabilita comandos admin, dejamos las claves (prefijo único por test).
        }

        Connection.Dispose();
    }
}

/// <summary>
/// La contract suite contra <see cref="RedisDocumentStore"/> (CHARTER-06/T054). Corre contra el Redis/Valkey
/// real de <see cref="RedisConnectionFixture"/>; se <b>omite</b> (no falla) cuando no hay servidor. Cada
/// <see cref="CreateStore"/> usa un prefijo de claves único → aislamiento entre tests sobre la misma conexión.
/// </summary>
public sealed class RedisDocumentStoreContractTests : DocumentStoreContractSuite, IClassFixture<RedisConnectionFixture>
{
    private readonly RedisConnectionFixture _fixture;

    /// <summary>Recibe la conexión compartida vía el class fixture de xUnit.</summary>
    public RedisDocumentStoreContractTests(RedisConnectionFixture fixture) => _fixture = fixture;

    protected override IDocumentStore CreateStore()
    {
        Skip.IfNot(
            _fixture.Available,
            "Redis/Valkey no disponible en WEFT_TEST_REDIS/localhost:6379 — test omitido (corre local con el servidor levantado).");

        string prefix = "weft-test:" + Guid.NewGuid().ToString("N") + ":";
        return new RedisDocumentStore(_fixture.Connection!, prefix, RedisConnectionFixture.TestDb);
    }
}
