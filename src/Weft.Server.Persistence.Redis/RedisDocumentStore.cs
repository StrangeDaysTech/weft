using System.Security.Cryptography;
using System.Text;
using StackExchange.Redis;

namespace Weft.Server.Persistence.Redis;

/// <summary>
/// <see cref="IDocumentStore"/> backed by Redis (via <c>StackExchange.Redis</c>). Each document uses two
/// keys derived from a hash of the opaque <c>docId</c>: a <b>string</b> with the consolidated snapshot and a
/// <b>list</b> with the accumulated updates (in <c>RPUSH</c> order). Compatible with any wire-Redis server
/// (Redis, Valkey).
/// </summary>
/// <remarks>
/// <para>
/// <b>Keys.</b> The <c>docId</c> is opaque (may contain any byte, incl. <c>:</c> and <c>/</c>): it is mapped
/// by SHA-256 hex to a stable prefix, onto which the suffixes <c>:s</c> (snapshot) and <c>:u</c>
/// (updates) are appended. The hash is fixed hex, so no <c>docId</c> can collide with another's keys.
/// </para>
/// <para>
/// <b>Atomicity.</b> <see cref="AppendUpdateAsync"/> is an atomic <c>RPUSH</c> by itself. <see cref="SaveSnapshotAsync"/>
/// performs the compaction in a <b>transaction</b> (<c>MULTI/EXEC</c>): it sets the snapshot and deletes the updates list
/// as a single unit, so a concurrent reader never sees the new snapshot alongside the old updates.
/// </para>
/// </remarks>
public sealed class RedisDocumentStore : IDocumentStore
{
    private const string SnapshotSuffix = ":s";
    private const string UpdatesSuffix = ":u";

    private readonly IConnectionMultiplexer _redis;
    private readonly string _keyPrefix;
    private readonly int _database;

    /// <summary>Creates the store over a shared Redis connection.</summary>
    /// <param name="redis"><c>StackExchange.Redis</c> multiplexer (shared, thread-safe).</param>
    /// <param name="keyPrefix">Prefix for all keys (isolation per instance/environment).</param>
    /// <param name="database">Redis database index; <c>-1</c> uses the connection's default.</param>
    public RedisDocumentStore(IConnectionMultiplexer redis, string keyPrefix = "weft:doc:", int database = -1)
    {
        ArgumentNullException.ThrowIfNull(redis);
        ArgumentNullException.ThrowIfNull(keyPrefix);
        _redis = redis;
        _keyPrefix = keyPrefix;
        _database = database;
    }

    /// <inheritdoc />
    public async ValueTask<byte[]?> LoadAsync(string docId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(docId);
        ct.ThrowIfCancellationRequested();

        IDatabase db = Db();
        (RedisKey snapshotKey, RedisKey updatesKey) = Keys(docId);

        RedisValue snapshotValue = await db.StringGetAsync(snapshotKey).ConfigureAwait(false);
        RedisValue[] updateValues = await db.ListRangeAsync(updatesKey).ConfigureAwait(false);

        byte[]? snapshot = snapshotValue.IsNull ? null : (byte[]?)snapshotValue;
        var updates = new List<byte[]>(updateValues.Length);
        foreach (RedisValue value in updateValues)
        {
            updates.Add((byte[]?)value ?? []);
        }

        return DocumentStateFraming.Frame(snapshot, updates);
    }

    /// <inheritdoc />
    public async ValueTask AppendUpdateAsync(string docId, ReadOnlyMemory<byte> update, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(docId);
        ct.ThrowIfCancellationRequested();

        IDatabase db = Db();
        (_, RedisKey updatesKey) = Keys(docId);
        await db.ListRightPushAsync(updatesKey, update.ToArray()).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask SaveSnapshotAsync(string docId, ReadOnlyMemory<byte> state, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(docId);
        ct.ThrowIfCancellationRequested();

        IDatabase db = Db();
        (RedisKey snapshotKey, RedisKey updatesKey) = Keys(docId);

        // Atomic compaction: sets the snapshot and discards the accumulated updates as a single unit.
        ITransaction tx = db.CreateTransaction();
        _ = tx.StringSetAsync(snapshotKey, state.ToArray());
        _ = tx.KeyDeleteAsync(updatesKey);
        await tx.ExecuteAsync().ConfigureAwait(false);
    }

    private IDatabase Db() => _redis.GetDatabase(_database);

    private (RedisKey Snapshot, RedisKey Updates) Keys(string docId)
    {
        string keyBase = _keyPrefix + HashDocId(docId);
        return (keyBase + SnapshotSuffix, keyBase + UpdatesSuffix);
    }

    private static string HashDocId(string docId)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(docId));
        return Convert.ToHexStringLower(hash);
    }
}
