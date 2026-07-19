using System.Runtime.InteropServices;
using System.Text;
using Weft.Loro.Interop;

namespace Weft.Loro;

/// <summary>CRDT document backed by Loro. Managed wrapper over the weft-loro-ffi shim.</summary>
internal sealed class LoroDoc : ICrdtDoc
{
    private readonly DocHandle _handle;

    private LoroDoc(DocHandle handle) => _handle = handle;

    public string EngineName => LoroEngine.EngineName;

    internal static LoroDoc Create()
    {
        FfiStatus.ThrowIfError(NativeMethods.weft_loro_doc_new(out nint raw));
        return new LoroDoc(new DocHandle(raw));
    }

    internal static LoroDoc Create(ulong peerId)
    {
        FfiStatus.ThrowIfError(NativeMethods.weft_loro_doc_new_with_peer_id(peerId, out nint raw));
        return new LoroDoc(new DocHandle(raw));
    }

    internal static LoroDoc Load(ReadOnlySpan<byte> blob)
    {
        FfiStatus.ThrowIfError(NativeMethods.weft_loro_doc_load(blob, (nuint)blob.Length, out nint raw));
        return new LoroDoc(new DocHandle(raw));
    }

    public void InsertText(string field, int index, string text)
    {
        ArgumentException.ThrowIfNullOrEmpty(field);
        ArgumentNullException.ThrowIfNull(text);
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ThrowIfDisposed();

        byte[] f = Encoding.UTF8.GetBytes(field);
        byte[] t = Encoding.UTF8.GetBytes(text);
        using var lease = new HandleLease(_handle);
        FfiStatus.ThrowIfError(NativeMethods.weft_loro_text_insert(lease.Ptr, f, (nuint)f.Length, (uint)index, t, (nuint)t.Length));
    }

    public void DeleteText(string field, int index, int length)
    {
        ArgumentException.ThrowIfNullOrEmpty(field);
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        ThrowIfDisposed();

        byte[] f = Encoding.UTF8.GetBytes(field);
        using var lease = new HandleLease(_handle);
        FfiStatus.ThrowIfError(NativeMethods.weft_loro_text_delete(lease.Ptr, f, (nuint)f.Length, (uint)index, (uint)length));
    }

    public string GetText(string field)
    {
        ArgumentException.ThrowIfNullOrEmpty(field);
        ThrowIfDisposed();

        byte[] f = Encoding.UTF8.GetBytes(field);
        using var lease = new HandleLease(_handle);
        FfiStatus.ThrowIfError(NativeMethods.weft_loro_text_read(lease.Ptr, f, (nuint)f.Length, out nint ptr, out nuint len));
        return Encoding.UTF8.GetString(TakeOwnedBuffer(ptr, len));
    }

    public byte[] ExportState()
    {
        ThrowIfDisposed();
        using var lease = new HandleLease(_handle);
        FfiStatus.ThrowIfError(NativeMethods.weft_loro_doc_export_state(lease.Ptr, out nint ptr, out nuint len));
        return TakeOwnedBuffer(ptr, len);
    }

    public byte[] ExportStateVector()
    {
        ThrowIfDisposed();
        using var lease = new HandleLease(_handle);
        FfiStatus.ThrowIfError(NativeMethods.weft_loro_doc_state_vector(lease.Ptr, out nint ptr, out nuint len));
        return TakeOwnedBuffer(ptr, len);
    }

    public byte[] ExportUpdateSince(ReadOnlySpan<byte> stateVector)
    {
        ThrowIfDisposed();
        using var lease = new HandleLease(_handle);
        FfiStatus.ThrowIfError(NativeMethods.weft_loro_doc_export_since(
            lease.Ptr, stateVector, (nuint)stateVector.Length, out nint ptr, out nuint len));
        return TakeOwnedBuffer(ptr, len);
    }

    public void ApplyUpdate(ReadOnlySpan<byte> update)
    {
        ThrowIfDisposed();
        using var lease = new HandleLease(_handle);
        FfiStatus.ThrowIfError(NativeMethods.weft_loro_doc_apply_update(lease.Ptr, update, (nuint)update.Length));
    }

    // ── Native versioning (INativeVersioning via LoroNativeVersioning — CHARTER-10/FU-006) ──
    // DEMONSTRATIVE probes of Loro's native capability. Their output is NOT deterministic and does NOT feed
    // VersionId (use ExportState for that). They do not mutate this document (branch/merge forks separately).

    internal byte[] ShallowSnapshotNative()
    {
        ThrowIfDisposed();
        using var lease = new HandleLease(_handle);
        FfiStatus.ThrowIfError(NativeMethods.weft_loro_shallow_snapshot(lease.Ptr, out nint ptr, out nuint len));
        return TakeOwnedBuffer(ptr, len);
    }

    internal string NativeDiffProbeJson(string field)
    {
        ArgumentException.ThrowIfNullOrEmpty(field);
        ThrowIfDisposed();
        byte[] f = Encoding.UTF8.GetBytes(field);
        using var lease = new HandleLease(_handle);
        FfiStatus.ThrowIfError(NativeMethods.weft_loro_native_diff_probe(lease.Ptr, f, (nuint)f.Length, out nint ptr, out nuint len));
        return Encoding.UTF8.GetString(TakeOwnedBuffer(ptr, len));
    }

    internal string NativeBranchMergeProbeJson(string field)
    {
        ArgumentException.ThrowIfNullOrEmpty(field);
        ThrowIfDisposed();
        byte[] f = Encoding.UTF8.GetBytes(field);
        using var lease = new HandleLease(_handle);
        FfiStatus.ThrowIfError(NativeMethods.weft_loro_native_branch_merge_probe(lease.Ptr, f, (nuint)f.Length, out nint ptr, out nuint len));
        return Encoding.UTF8.GetString(TakeOwnedBuffer(ptr, len));
    }

    public void Dispose() => _handle.Dispose();

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_handle.IsClosed, this);

    private static byte[] TakeOwnedBuffer(nint ptr, nuint len)
    {
        if (ptr == nint.Zero || len == 0)
        {
            if (ptr != nint.Zero)
            {
                NativeMethods.weft_loro_buf_free(ptr, len);
            }
            return [];
        }
        try
        {
            var managed = new byte[(int)len];
            Marshal.Copy(ptr, managed, 0, (int)len);
            return managed;
        }
        finally
        {
            NativeMethods.weft_loro_buf_free(ptr, len);
        }
    }
}
