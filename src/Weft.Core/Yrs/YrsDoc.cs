using System.Runtime.InteropServices;
using System.Text;

namespace Weft.Yrs;

/// <summary>
/// CRDT document backed by yrs. Managed wrapper over <see cref="DocHandle"/> that
/// hides pointers, lengths and manual freeing. NOT thread-safe (constitution P-V): the owner
/// serializes access; the <c>DocumentBroker</c> (M1) guarantees it for shared access.
/// </summary>
internal sealed class YrsDoc : ICrdtDoc
{
    private readonly DocHandle _handle;

    private YrsDoc(DocHandle handle) => _handle = handle;

    public string EngineName => YrsEngine.EngineName;

    internal static YrsDoc Create()
    {
        FfiStatus.ThrowIfError(NativeMethods.weft_doc_new(out nint raw));
        return new YrsDoc(new DocHandle(raw));
    }

    /// <summary>
    /// Creates a doc with a FIXED <paramref name="clientId"/> (deterministic seeding for
    /// cross-implementation parity with Yjs; FU-012). Must fit in 53 bits (yrs 0.26+ encoding);
    /// a larger value throws via <c>WEFT_ERR_OUT_OF_BOUNDS</c>.
    /// </summary>
    internal static YrsDoc Create(ulong clientId)
    {
        FfiStatus.ThrowIfError(NativeMethods.weft_doc_new_with_client_id(clientId, out nint raw));
        return new YrsDoc(new DocHandle(raw));
    }

    internal static YrsDoc Load(ReadOnlySpan<byte> blob)
    {
        FfiStatus.ThrowIfError(NativeMethods.weft_doc_load(blob, (nuint)blob.Length, out nint raw));
        return new YrsDoc(new DocHandle(raw));
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
        FfiStatus.ThrowIfError(NativeMethods.weft_text_insert(lease.Ptr, f, (nuint)f.Length, (uint)index, t, (nuint)t.Length));
    }

    public void DeleteText(string field, int index, int length)
    {
        ArgumentException.ThrowIfNullOrEmpty(field);
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        ThrowIfDisposed();

        byte[] f = Encoding.UTF8.GetBytes(field);
        using var lease = new HandleLease(_handle);
        FfiStatus.ThrowIfError(NativeMethods.weft_text_delete(lease.Ptr, f, (nuint)f.Length, (uint)index, (uint)length));
    }

    public string GetText(string field)
    {
        ArgumentException.ThrowIfNullOrEmpty(field);
        ThrowIfDisposed();

        byte[] f = Encoding.UTF8.GetBytes(field);
        using var lease = new HandleLease(_handle);
        FfiStatus.ThrowIfError(NativeMethods.weft_text_read(lease.Ptr, f, (nuint)f.Length, out nint ptr, out nuint len));
        return Encoding.UTF8.GetString(TakeOwnedBuffer(ptr, len));
    }

    public byte[] ExportState()
    {
        ThrowIfDisposed();
        using var lease = new HandleLease(_handle);
        FfiStatus.ThrowIfError(NativeMethods.weft_doc_export_state(lease.Ptr, out nint ptr, out nuint len));
        return TakeOwnedBuffer(ptr, len);
    }

    public byte[] ExportStateVector()
    {
        ThrowIfDisposed();
        using var lease = new HandleLease(_handle);
        FfiStatus.ThrowIfError(NativeMethods.weft_doc_state_vector(lease.Ptr, out nint ptr, out nuint len));
        return TakeOwnedBuffer(ptr, len);
    }

    public byte[] ExportUpdateSince(ReadOnlySpan<byte> stateVector)
    {
        ThrowIfDisposed();
        using var lease = new HandleLease(_handle);
        FfiStatus.ThrowIfError(NativeMethods.weft_doc_export_since(
            lease.Ptr, stateVector, (nuint)stateVector.Length, out nint ptr, out nuint len));
        return TakeOwnedBuffer(ptr, len);
    }

    public void ApplyUpdate(ReadOnlySpan<byte> update)
    {
        ThrowIfDisposed();
        using var lease = new HandleLease(_handle);
        FfiStatus.ThrowIfError(NativeMethods.weft_doc_apply_update(lease.Ptr, update, (nuint)update.Length));
    }

    public void Dispose() => _handle.Dispose();

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_handle.IsClosed, this);

    /// <summary>
    /// Copies a buffer whose memory belongs to Rust into a managed <c>byte[]</c> and returns it to
    /// Rust with <c>weft_buf_free</c>. The exact point of the ownership contract (P-I): the GC never
    /// touches that memory.
    /// </summary>
    private static byte[] TakeOwnedBuffer(nint ptr, nuint len)
    {
        if (ptr == nint.Zero || len == 0)
        {
            if (ptr != nint.Zero)
            {
                NativeMethods.weft_buf_free(ptr, len);
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
            NativeMethods.weft_buf_free(ptr, len);
        }
    }
}
