using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Weft.Server.Persistence;

/// <summary>
/// <see cref="IDocumentStore"/> backed by the file system: durable persistence for v1. Each
/// document lives in its own directory under the configured root, with a <c>snapshot</c> file and one
/// file per accumulated update (<c>u-{seq}</c>).
/// </summary>
/// <remarks>
/// <para>
/// <b>Atomicity.</b> Every write (snapshot and each update) is materialized with temp + <c>rename</c>: it is
/// written to a temporary file, flushed to disk and renamed over the final target (rename is atomic on
/// POSIX and equivalent on Windows). An update is its own file with an atomic rename —never a truncatable
/// append—, so a failure mid-write leaves the target file intact or nonexistent, never a
/// corrupt record that breaks the load.
/// </para>
/// <para>
/// <b>Compaction.</b> <see cref="SaveSnapshotAsync"/> replaces the snapshot atomically and then deletes all
/// the accumulated update files. The snapshot is written before deleting the updates: a failure between the two
/// steps only leaves updates that are already incorporated into the snapshot, and reapplying them is an
/// idempotent CRDT no-op (see <see cref="DocumentStateFraming"/>).
/// </para>
/// <para>
/// <b>Concurrency.</b> A <see cref="SemaphoreSlim"/> per document serializes that doc's operations; the
/// global map of semaphores is concurrent. The <c>docId</c> (opaque, may contain any character) is
/// mapped to a directory name by SHA-256 hex hash — avoids path traversal and illegal lengths.
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

        /// <summary>Next update index; <c>-1</c> = not yet initialized from disk.</summary>
        public long NextSeq = -1;
    }

    private readonly string _root;
    private readonly ConcurrentDictionary<string, DocLock> _locks = new(StringComparer.Ordinal);

    /// <summary>Creates the store under <paramref name="rootDirectory"/> (created if it does not exist).</summary>
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

            // 1) Atomic replacement of the snapshot.
            string snapshotPath = Path.Combine(dir, SnapshotFileName);
            await AtomicWriteAsync(snapshotPath, state, ct).ConfigureAwait(false);

            // 2) Compaction: delete the updates already incorporated into the snapshot and reset the numbering.
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

    // Name zero-padded to 20 digits → the lexicographic order matches the numeric append order.
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
            // Durability (FU-010): FlushAsync only flushes to the OS page cache; Flush(flushToDisk: true)
            // forces the fsync to disk. There is no async variant of fsync in .NET, so this Flush is
            // synchronous (blocks the thread during the fsync; the append is already outside the actor turn).
            fs.Flush(flushToDisk: true);
        }

        File.Move(tmpPath, finalPath, overwrite: true);

        // A rename is durable only if the directory containing it is also synced (POSIX). On
        // Windows there is no equivalent directory handle; it is skipped (NTFS orders metadata such that the
        // rename does not precede the content already synced above).
        FsyncDirectory(Path.GetDirectoryName(finalPath));
    }

    private static void FsyncDirectory(string? dir)
    {
        if (string.IsNullOrEmpty(dir) || OperatingSystem.IsWindows())
        {
            return;
        }

        // Open the directory and fsync it: makes the rename entry durable. Best-effort: if the OS does not
        // allow opening a directory as a file, the content's durability (above) is what is essential.
        try
        {
            using var dirHandle = File.OpenHandle(dir, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            RandomAccess.FlushToDisk(dirHandle);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            System.Diagnostics.Debug.WriteLine($"[FileSystemDocumentStore] fsync de directorio '{dir}' omitido: {ex.Message}");
        }
    }

    private static string HashDocId(string docId)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(docId));
        return Convert.ToHexStringLower(hash);
    }
}
