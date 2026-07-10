using System.Runtime.InteropServices;

namespace Weft.Yrs;

/// <summary>
/// Declaraciones P/Invoke sobre la C-ABI del shim <c>weft-yrs-ffi</c>, generadas por el source
/// generator <see cref="LibraryImportAttribute"/> (marshalling en compilación, sin stubs IL).
/// Deben coincidir con <c>native/weft-yrs-ffi/include/weft_ffi.h</c> (un test de CI lo valida).
/// </summary>
/// <remarks>
/// Convenciones de marshalling: bytes de ENTRADA como <see cref="ReadOnlySpan{Byte}"/> (pin
/// automático, cero copias); bytes de SALIDA como <c>out nint</c> + <c>out nuint</c> (memoria de
/// Rust, se copia a gestionada y se libera con <see cref="weft_buf_free"/>); documento como
/// <c>nint</c> crudo prestado por <see cref="HandleLease"/> (SYSLIB1051, research R2).
/// </remarks>
internal static partial class NativeMethods
{
    internal const string Lib = "weft_yrs_ffi";

    // ── Ciclo de vida ──
    [LibraryImport(Lib)]
    internal static partial int weft_doc_new(out nint outDoc);

    [LibraryImport(Lib)]
    internal static partial int weft_doc_load(ReadOnlySpan<byte> blob, nuint blobLen, out nint outDoc);

    [LibraryImport(Lib)]
    internal static partial void weft_doc_free(nint doc);

    // ── Texto ──
    [LibraryImport(Lib)]
    internal static partial int weft_text_insert(
        nint doc, ReadOnlySpan<byte> field, nuint fieldLen,
        uint index, ReadOnlySpan<byte> text, nuint textLen);

    [LibraryImport(Lib)]
    internal static partial int weft_text_delete(
        nint doc, ReadOnlySpan<byte> field, nuint fieldLen, uint index, uint len);

    [LibraryImport(Lib)]
    internal static partial int weft_text_read(
        nint doc, ReadOnlySpan<byte> field, nuint fieldLen, out nint outPtr, out nuint outLen);

    // ── Estado y sincronización ──
    [LibraryImport(Lib)]
    internal static partial int weft_doc_export_state(nint doc, out nint outPtr, out nuint outLen);

    [LibraryImport(Lib)]
    internal static partial int weft_doc_state_vector(nint doc, out nint outPtr, out nuint outLen);

    [LibraryImport(Lib)]
    internal static partial int weft_doc_export_since(
        nint doc, ReadOnlySpan<byte> sv, nuint svLen, out nint outPtr, out nuint outLen);

    [LibraryImport(Lib)]
    internal static partial int weft_doc_apply_update(nint doc, ReadOnlySpan<byte> update, nuint updateLen);

    // ── Memoria ──
    [LibraryImport(Lib)]
    internal static partial void weft_buf_free(nint ptr, nuint len);

    // ── Diagnóstico ──
    [LibraryImport(Lib)]
    internal static partial uint weft_abi_version();
}
