using System.Runtime.InteropServices;
using System.Text;

namespace Weft.Yrs;

/// <summary>
/// Documento CRDT respaldado por yrs. Envoltorio gestionado sobre <see cref="DocHandle"/> que
/// esconde punteros, longitudes y liberación manual. NO es thread-safe (constitución P-V): el dueño
/// serializa el acceso; el <c>DocumentBroker</c> (M1) lo garantiza para acceso compartido.
/// </summary>
internal sealed class YrsDoc : ICrdtDoc
{
    private readonly DocHandle _handle;

    private YrsDoc(DocHandle handle) => _handle = handle;

    internal static YrsDoc Create()
    {
        FfiStatus.ThrowIfError(NativeMethods.weft_doc_new(out nint raw));
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
    /// Copia un buffer cuya memoria pertenece a Rust a un <c>byte[]</c> gestionado y lo devuelve a
    /// Rust con <c>weft_buf_free</c>. Punto exacto del contrato de ownership (P-I): el GC jamás
    /// toca esa memoria.
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
