using System.Collections.Concurrent;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using Weft.Server.Persistence;
using Weft.Server.Persistence.EFCore;
using Weft.Server.Persistence.Redis;

namespace Weft.Server.Tests;

/// <summary>
/// Shared <see cref="IDocumentStore"/> contract suite (T050). It runs <b>identically</b> against each
/// adapter (<see cref="InMemoryDocumentStore"/>, <see cref="FileSystemDocumentStore"/> and, since CHARTER-06,
/// <see cref="EFCoreDocumentStore"/> and <see cref="RedisDocumentStore"/>). It is the basis of the store
/// interchangeability required by the US3 acceptance scenario and <c>contracts/server-api.md</c>.
/// </summary>
/// <remarks>
/// The tests use <c>[SkippableFact]</c> (not <c>[Fact]</c>) so that an adapter whose external backend is not
/// available can be <b>skipped</b> instead of failing: the Redis subclass calls <c>Skip.IfNot(...)</c> in
/// <see cref="CreateStore"/> (the first line of each test), so without Redis/Valkey the test is skipped.
/// For the in-process adapters (InMemory, FileSystem, EFCore/SQLite) the behavior is identical to
/// <c>[Fact]</c> — never skipped.
/// </remarks>
public abstract class DocumentStoreContractSuite
{
    /// <summary>Creates a fresh, isolated instance of the store under test.</summary>
    protected abstract IDocumentStore CreateStore();

    private const string DocId = "doc-α/β:1";  // opaque: includes non-ASCII and '/' to stress the filename mapping.

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

        // Compaction: after the snapshot only the snapshot remains (the accumulated updates were discarded).
        IReadOnlyList<byte[]> afterSnapshot = Records(await store.LoadAsync(DocId));
        Assert.Equal(snapshot, Assert.Single(afterSnapshot));

        // Subsequent updates accumulate on top of the snapshot, in order.
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

        // Each update is its index in 4 bytes → distinguishable; the order among concurrent appends is not
        // guaranteed, so we verify the set, not the sequence.
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

/// <summary>The contract suite against <see cref="InMemoryDocumentStore"/>.</summary>
public sealed class InMemoryDocumentStoreContractTests : DocumentStoreContractSuite
{
    protected override IDocumentStore CreateStore() => new InMemoryDocumentStore();
}

/// <summary>The contract suite against <see cref="FileSystemDocumentStore"/> (isolated temp directory).</summary>
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
/// The contract suite against <see cref="EFCoreDocumentStore"/> (CHARTER-06/T053) over <b>SQLite</b> in an
/// isolated temp file — a real, relational, cross-platform provider, with no external infrastructure.
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

    /// <summary>Minimal SQLite context factory for the tests (a fresh unit of work per operation).</summary>
    private sealed class SqliteContextFactory(DbContextOptions<WeftDocumentStoreContext> options)
        : IDbContextFactory<WeftDocumentStoreContext>
    {
        public WeftDocumentStoreContext CreateDbContext() => new(options);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();  // releases the file handle before deleting it.
        if (File.Exists(_dbPath))
        {
            try
            {
                File.Delete(_dbPath);
            }
            catch (IOException)
            {
                // best-effort: a straggler handle from the pool leaves the temp file behind; harmless.
            }
        }
    }
}

/// <summary>
/// Shared Redis/Valkey connection for the suite (one per class). If the server is not available at
/// <c>WEFT_TEST_REDIS</c> (or <c>localhost:6379</c>), <see cref="Available"/> is <c>false</c> and the tests are
/// skipped. Uses the <see cref="TestDb"/> database (isolated from the default db 0) and flushes it on close.
/// </summary>
public sealed class RedisConnectionFixture : IDisposable
{
    /// <summary>Database index dedicated to the tests (does not touch the server's default db 0).</summary>
    public const int TestDb = 15;

    /// <summary>The connection, or <c>null</c> if the server did not respond.</summary>
    public IConnectionMultiplexer? Connection { get; }

    /// <summary><c>true</c> if there is a live connection to Redis/Valkey.</summary>
    public bool Available => Connection is { IsConnected: true };

    /// <summary>Tries to connect once (without aborting on failure) to decide whether the suite runs or is skipped.</summary>
    public RedisConnectionFixture()
    {
        string config = Environment.GetEnvironmentVariable("WEFT_TEST_REDIS") ?? "localhost:6379";
        var options = ConfigurationOptions.Parse(config);
        options.AbortOnConnectFail = false;   // does not throw if the server is absent: returns a disconnected mux.
        options.ConnectTimeout = 1000;
        options.ConnectRetry = 1;
        options.AllowAdmin = true;            // enables FLUSHDB in the cleanup.

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
            // best-effort: if the server disables admin commands, we leave the keys (unique prefix per test).
        }

        Connection.Dispose();
    }
}

/// <summary>
/// The contract suite against <see cref="RedisDocumentStore"/> (CHARTER-06/T054). Runs against the real
/// Redis/Valkey from <see cref="RedisConnectionFixture"/>; it is <b>skipped</b> (not failed) when there is no server. Each
/// <see cref="CreateStore"/> uses a unique key prefix → isolation between tests over the same connection.
/// </summary>
public sealed class RedisDocumentStoreContractTests : DocumentStoreContractSuite, IClassFixture<RedisConnectionFixture>
{
    private readonly RedisConnectionFixture _fixture;

    /// <summary>Receives the shared connection via the xUnit class fixture.</summary>
    public RedisDocumentStoreContractTests(RedisConnectionFixture fixture) => _fixture = fixture;

    protected override IDocumentStore CreateStore()
    {
        Skip.IfNot(
            _fixture.Available,
            "Redis/Valkey not available at WEFT_TEST_REDIS/localhost:6379 — test skipped (run locally with the server up).");

        string prefix = "weft-test:" + Guid.NewGuid().ToString("N") + ":";
        return new RedisDocumentStore(_fixture.Connection!, prefix, RedisConnectionFixture.TestDb);
    }
}
