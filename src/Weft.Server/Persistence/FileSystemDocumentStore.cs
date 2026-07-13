using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Weft.Server.Persistence;

/// <summary>
/// <see cref="IDocumentStore"/> respaldado por el sistema de archivos: persistencia durable para v1. Cada
/// documento vive en su propio directorio bajo la raíz configurada, con un archivo <c>snapshot</c> y un
/// archivo por update acumulado (<c>u-{seq}</c>).
/// </summary>
/// <remarks>
/// <para>
/// <b>Atomicidad.</b> Toda escritura (snapshot y cada update) se materializa con temp + <c>rename</c>: se
/// escribe a un archivo temporal, se vuelca a disco y se renombra sobre el destino final (rename es atómico en
/// POSIX y equivalente en Windows). Un update es su propio archivo con rename atómico —nunca un append
/// truncable—, así que un fallo a media escritura deja el archivo destino intacto o inexistente, jamás un
/// record corrupto que rompa la carga.
/// </para>
/// <para>
/// <b>Compaction.</b> <see cref="SaveSnapshotAsync"/> reemplaza el snapshot atómicamente y luego borra todos
/// los archivos de update acumulados. El snapshot se escribe antes de borrar los updates: un fallo entre ambos
/// pasos solo deja updates que ya están incorporados al snapshot, y su reaplicación es un no-op CRDT
/// idempotente (ver <see cref="DocumentStateFraming"/>).
/// </para>
/// <para>
/// <b>Concurrencia.</b> Un <see cref="SemaphoreSlim"/> por documento serializa las operaciones de ese doc; el
/// mapa global de semáforos es concurrente. El <c>docId</c> (opaco, puede contener cualquier carácter) se
/// mapea a un nombre de directorio por hash SHA-256 hex — evita traversal de rutas y longitudes ilegales.
/// </para>
/// </remarks>
public sealed class FileSystemDocumentStore : IDocumentStore
{
    private const string SnapshotFileName = "snapshot";
    private const string UpdatePrefix = "u-";
    private const string TempSuffix = ".tmp";

    private sealed class DocLock
    {
        public readonly SemaphoreSlim Gate = new(1, 1);

        /// <summary>Siguiente índice de update; <c>-1</c> = aún no inicializado desde disco.</summary>
        public long NextSeq = -1;
    }

    private readonly string _root;
    private readonly ConcurrentDictionary<string, DocLock> _locks = new(StringComparer.Ordinal);

    /// <summary>Crea el store bajo <paramref name="rootDirectory"/> (se crea si no existe).</summary>
    public FileSystemDocumentStore(string rootDirectory)
    {
        ArgumentException.ThrowIfNullOrEmpty(rootDirectory);
        _root = Path.GetFullPath(rootDirectory);
        Directory.CreateDirectory(_root);
    }

    /// <inheritdoc />
    public async ValueTask<byte[]?> LoadAsync(string docId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(docId);
        DocLock docLock = GetLock(docId);
        string dir = DocDir(docId);

        await docLock.Gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (!Directory.Exists(dir))
            {
                return null;
            }

            string snapshotPath = Path.Combine(dir, SnapshotFileName);
            byte[]? snapshot = File.Exists(snapshotPath)
                ? await File.ReadAllBytesAsync(snapshotPath, ct).ConfigureAwait(false)
                : null;

            var updates = new List<byte[]>();
            foreach (string updatePath in EnumerateUpdateFilesOrdered(dir))
            {
                updates.Add(await File.ReadAllBytesAsync(updatePath, ct).ConfigureAwait(false));
            }

            return DocumentStateFraming.Frame(snapshot, updates);
        }
        finally
        {
            docLock.Gate.Release();
        }
    }

    /// <inheritdoc />
    public async ValueTask AppendUpdateAsync(string docId, ReadOnlyMemory<byte> update, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(docId);
        DocLock docLock = GetLock(docId);
        string dir = DocDir(docId);

        await docLock.Gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(dir);
            EnsureSeqInitialized(docLock, dir);

            long seq = docLock.NextSeq++;
            string path = Path.Combine(dir, UpdateFileName(seq));
            await AtomicWriteAsync(path, update, ct).ConfigureAwait(false);
        }
        finally
        {
            docLock.Gate.Release();
        }
    }

    /// <inheritdoc />
    public async ValueTask SaveSnapshotAsync(string docId, ReadOnlyMemory<byte> state, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(docId);
        DocLock docLock = GetLock(docId);
        string dir = DocDir(docId);

        await docLock.Gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(dir);

            // 1) Reemplazo atómico del snapshot.
            string snapshotPath = Path.Combine(dir, SnapshotFileName);
            await AtomicWriteAsync(snapshotPath, state, ct).ConfigureAwait(false);

            // 2) Compaction: borrar los updates ya incorporados al snapshot y reiniciar la numeración.
            foreach (string updatePath in EnumerateUpdateFilesOrdered(dir))
            {
                File.Delete(updatePath);
            }

            docLock.NextSeq = 0;
        }
        finally
        {
            docLock.Gate.Release();
        }
    }

    private DocLock GetLock(string docId) =>
        _locks.GetOrAdd(docId, static _ => new DocLock());

    private string DocDir(string docId) => Path.Combine(_root, HashDocId(docId));

    private static void EnsureSeqInitialized(DocLock docLock, string dir)
    {
        if (docLock.NextSeq >= 0)
        {
            return;
        }

        long max = -1;
        foreach (string path in Directory.EnumerateFiles(dir, UpdatePrefix + "*"))
        {
            if (TryParseSeq(Path.GetFileName(path), out long seq) && seq > max)
            {
                max = seq;
            }
        }

        docLock.NextSeq = max + 1;
    }

    private static IEnumerable<string> EnumerateUpdateFilesOrdered(string dir) =>
        Directory.EnumerateFiles(dir, UpdatePrefix + "*")
            .Where(p => TryParseSeq(Path.GetFileName(p), out _))
            .OrderBy(p => Path.GetFileName(p), StringComparer.Ordinal);

    // Nombre zero-padded a 20 dígitos → el orden lexicográfico coincide con el orden numérico de append.
    private static string UpdateFileName(long seq) =>
        UpdatePrefix + seq.ToString("D20", CultureInfo.InvariantCulture);

    private static bool TryParseSeq(string fileName, out long seq)
    {
        seq = 0;
        if (!fileName.StartsWith(UpdatePrefix, StringComparison.Ordinal) ||
            fileName.EndsWith(TempSuffix, StringComparison.Ordinal))
        {
            return false;
        }

        return long.TryParse(
            fileName.AsSpan(UpdatePrefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out seq);
    }

    private static async Task AtomicWriteAsync(string finalPath, ReadOnlyMemory<byte> bytes, CancellationToken ct)
    {
        string tmpPath = finalPath + TempSuffix;
        await using (var fs = new FileStream(
            tmpPath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 4096, FileOptions.None))
        {
            await fs.WriteAsync(bytes, ct).ConfigureAwait(false);
            await fs.FlushAsync(ct).ConfigureAwait(false);
        }

        File.Move(tmpPath, finalPath, overwrite: true);
    }

    private static string HashDocId(string docId)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(docId));
        return Convert.ToHexStringLower(hash);
    }
}
