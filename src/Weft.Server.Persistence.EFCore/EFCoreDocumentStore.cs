using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;

namespace Weft.Server.Persistence.EFCore;

/// <summary>
/// <see cref="IDocumentStore"/> respaldado por Entity Framework Core. Cada documento es un snapshot consolidado
/// (a lo sumo uno) más los updates incrementales acumulados desde él, en filas de <c>WeftDocumentRecords</c>.
/// Provider-agnóstico: el consumidor configura el provider vía <see cref="WeftDocumentStoreContext"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Concurrencia.</b> Un <see cref="SemaphoreSlim"/> por documento serializa <b>todas</b> las operaciones de
/// ese doc (igual que <c>FileSystemDocumentStore</c>): asigna el <c>Seq</c> monotónico sin carreras y evita la
/// contención de escritura del backend (p. ej. <c>SQLITE_BUSY</c> bajo appends concurrentes al mismo doc).
/// Documentos distintos usan semáforos distintos y progresan en paralelo. Cada operación abre su propio
/// <see cref="WeftDocumentStoreContext"/> vía la factory (un <c>DbContext</c> es una unidad de trabajo de un
/// solo uso, no es thread-safe).
/// </para>
/// <para>
/// <b>Compaction.</b> <see cref="SaveSnapshotAsync"/> borra los records del doc e inserta el snapshot como base
/// dentro de una <b>transacción</b>: un lector concurrente ve el estado previo íntegro o el nuevo, nunca una
/// mezcla parcial.
/// </para>
/// </remarks>
public sealed class EFCoreDocumentStore : IDocumentStore
{
    private readonly IDbContextFactory<WeftDocumentStoreContext> _factory;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new(StringComparer.Ordinal);

    /// <summary>Crea el store sobre la factory de contextos configurada por el consumidor.</summary>
    public EFCoreDocumentStore(IDbContextFactory<WeftDocumentStoreContext> contextFactory)
    {
        ArgumentNullException.ThrowIfNull(contextFactory);
        _factory = contextFactory;
    }

    /// <inheritdoc />
    public async ValueTask<byte[]?> LoadAsync(string docId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(docId);
        SemaphoreSlim gate = Gate(docId);

        await gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await using WeftDocumentStoreContext ctx = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);

            var rows = await ctx.Records
                .Where(r => r.DocId == docId)
                .OrderBy(r => r.Seq)
                .Select(r => new { r.Kind, r.Payload })
                .ToListAsync(ct)
                .ConfigureAwait(false);

            if (rows.Count == 0)
            {
                return null;
            }

            byte[]? snapshot = null;
            var updates = new List<byte[]>(rows.Count);
            foreach (var row in rows)
            {
                if (row.Kind == RecordKind.Snapshot)
                {
                    snapshot = row.Payload;
                }
                else
                {
                    updates.Add(row.Payload);
                }
            }

            return DocumentStateFraming.Frame(snapshot, updates);
        }
        finally
        {
            gate.Release();
        }
    }

    /// <inheritdoc />
    public async ValueTask AppendUpdateAsync(string docId, ReadOnlyMemory<byte> update, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(docId);
        SemaphoreSlim gate = Gate(docId);

        await gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await using WeftDocumentStoreContext ctx = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);

            long nextSeq = await NextSeqAsync(ctx, docId, ct).ConfigureAwait(false);
            ctx.Records.Add(new DocumentRecord
            {
                DocId = docId,
                Seq = nextSeq,
                Kind = RecordKind.Update,
                Payload = update.ToArray(),
            });
            await ctx.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    /// <inheritdoc />
    public async ValueTask SaveSnapshotAsync(string docId, ReadOnlyMemory<byte> state, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(docId);
        SemaphoreSlim gate = Gate(docId);

        await gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await using WeftDocumentStoreContext ctx = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
            await using var tx = await ctx.Database.BeginTransactionAsync(ct).ConfigureAwait(false);

            // Compaction atómica: descarta lo acumulado e inserta el snapshot como base (Seq 0).
            await ctx.Records.Where(r => r.DocId == docId).ExecuteDeleteAsync(ct).ConfigureAwait(false);
            ctx.Records.Add(new DocumentRecord
            {
                DocId = docId,
                Seq = 0,
                Kind = RecordKind.Snapshot,
                Payload = state.ToArray(),
            });
            await ctx.SaveChangesAsync(ct).ConfigureAwait(false);
            await tx.CommitAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    private SemaphoreSlim Gate(string docId) =>
        _locks.GetOrAdd(docId, static _ => new SemaphoreSlim(1, 1));

    private static async Task<long> NextSeqAsync(WeftDocumentStoreContext ctx, string docId, CancellationToken ct)
    {
        long? max = await ctx.Records
            .Where(r => r.DocId == docId)
            .MaxAsync(r => (long?)r.Seq, ct)
            .ConfigureAwait(false);
        return (max ?? -1) + 1;
    }
}
