using System.Security.Cryptography;
using System.Text;
using StackExchange.Redis;

namespace Weft.Server.Persistence.Redis;

/// <summary>
/// <see cref="IDocumentStore"/> respaldado por Redis (vía <c>StackExchange.Redis</c>). Cada documento usa dos
/// claves derivadas de un hash del <c>docId</c> opaco: un <b>string</b> con el snapshot consolidado y una
/// <b>lista</b> con los updates acumulados (en orden de <c>RPUSH</c>). Compatible con cualquier servidor
/// wire-Redis (Redis, Valkey).
/// </summary>
/// <remarks>
/// <para>
/// <b>Claves.</b> El <c>docId</c> es opaco (puede contener cualquier byte, incl. <c>:</c> y <c>/</c>): se mapea
/// por SHA-256 hex a un prefijo estable, sobre el que se cuelgan los sufijos <c>:s</c> (snapshot) y <c>:u</c>
/// (updates). El hash es hex fijo, así que ningún <c>docId</c> puede colisionar con las claves de otro.
/// </para>
/// <para>
/// <b>Atomicidad.</b> <see cref="AppendUpdateAsync"/> es un <c>RPUSH</c> atómico de suyo. <see cref="SaveSnapshotAsync"/>
/// hace la compaction en una <b>transacción</b> (<c>MULTI/EXEC</c>): fija el snapshot y borra la lista de updates
/// como una sola unidad, de modo que un lector concurrente nunca ve el snapshot nuevo junto a los updates viejos.
/// </para>
/// </remarks>
public sealed class RedisDocumentStore : IDocumentStore
{
    private const string SnapshotSuffix = ":s";
    private const string UpdatesSuffix = ":u";

    private readonly IConnectionMultiplexer _redis;
    private readonly string _keyPrefix;
    private readonly int _database;

    /// <summary>Crea el store sobre una conexión Redis compartida.</summary>
    /// <param name="redis">Multiplexer de <c>StackExchange.Redis</c> (compartido, thread-safe).</param>
    /// <param name="keyPrefix">Prefijo de todas las claves (aislamiento por instancia/entorno).</param>
    /// <param name="database">Índice de base de datos Redis; <c>-1</c> usa la por defecto de la conexión.</param>
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

        // Compaction atómica: fija el snapshot y descarta los updates acumulados como una sola unidad.
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
