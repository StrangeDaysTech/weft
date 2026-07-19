using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;

namespace Weft.Server.Persistence.EFCore;

/// <summary>
/// <see cref="IDocumentStore"/> backed by Entity Framework Core. Each document is a consolidated snapshot
/// (at most one) plus the incremental updates accumulated since it, in rows of <c>WeftDocumentRecords</c>.
/// Provider-agnostic: the consumer configures the provider via <see cref="WeftDocumentStoreContext"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Concurrency.</b> One <see cref="SemaphoreSlim"/> per document serializes <b>all</b> operations of
/// that doc (like <c>FileSystemDocumentStore</c>): it assigns the monotonic <c>Seq</c> without races and avoids
/// the backend's write contention (e.g. <c>SQLITE_BUSY</c> under concurrent appends to the same doc).
/// Different documents use different semaphores and progress in parallel. Each operation opens its own
/// <see cref="WeftDocumentStoreContext"/> via the factory (a <c>DbContext</c> is a single-use unit of work,
/// not thread-safe).
/// </para>
/// <para>
/// <b>Compaction.</b> <see cref="SaveSnapshotAsync"/> deletes the doc's records and inserts the snapshot as base
/// inside a <b>transaction</b>: a concurrent reader sees either the whole previous state or the new one, never a
/// partial mix.
/// </para>
/// </remarks>
public sealed class EFCoreDocumentStore : IDocumentStore
{
    private readonly IDbContextFactory<WeftDocumentStoreContext> _factory;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new(StringComparer.Ordinal);

    /// <summary>Creates the store over the context factory configured by the consumer.</summary>
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

            // Atomic compaction: discards what was accumulated and inserts the snapshot as base (Seq 0).
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
