using System.Runtime.InteropServices;

namespace Weft.Loro.Interop;

/// <summary>P/Invoke sobre la C-ABI del shim <c>weft-loro-ffi</c> (simétrica a weft-yrs-ffi).</summary>
internal static partial class NativeMethods
{
    internal const string Lib = "weft_loro_ffi";

    [LibraryImport(Lib)]
    internal static partial int weft_loro_doc_new(out nint outDoc);

    [LibraryImport(Lib)]
    internal static partial int weft_loro_doc_new_with_peer_id(ulong peerId, out nint outDoc);

    [LibraryImport(Lib)]
    internal static partial int weft_loro_doc_load(ReadOnlySpan<byte> blob, nuint blobLen, out nint outDoc);

    [LibraryImport(Lib)]
    internal static partial void weft_loro_doc_free(nint doc);

    [LibraryImport(Lib)]
    internal static partial int weft_loro_text_insert(
        nint doc, ReadOnlySpan<byte> field, nuint fieldLen,
        uint index, ReadOnlySpan<byte> text, nuint textLen);

    [LibraryImport(Lib)]
    internal static partial int weft_loro_text_delete(
        nint doc, ReadOnlySpan<byte> field, nuint fieldLen, uint index, uint len);

    [LibraryImport(Lib)]
    internal static partial int weft_loro_text_read(
        nint doc, ReadOnlySpan<byte> field, nuint fieldLen, out nint outPtr, out nuint outLen);

    [LibraryImport(Lib)]
    internal static partial int weft_loro_doc_export_state(nint doc, out nint outPtr, out nuint outLen);

    [LibraryImport(Lib)]
    internal static partial int weft_loro_doc_state_vector(nint doc, out nint outPtr, out nuint outLen);

    [LibraryImport(Lib)]
    internal static partial int weft_loro_doc_export_since(
        nint doc, ReadOnlySpan<byte> sv, nuint svLen, out nint outPtr, out nuint outLen);

    [LibraryImport(Lib)]
    internal static partial int weft_loro_doc_apply_update(nint doc, ReadOnlySpan<byte> update, nuint updateLen);

    // ── Versionado nativo (INativeVersioning, capacidad opcional — CHARTER-10/FU-006) ──
    [LibraryImport(Lib)]
    internal static partial int weft_loro_shallow_snapshot(nint doc, out nint outPtr, out nuint outLen);

    [LibraryImport(Lib)]
    internal static partial int weft_loro_native_diff_probe(
        nint doc, ReadOnlySpan<byte> field, nuint fieldLen, out nint outPtr, out nuint outLen);

    [LibraryImport(Lib)]
    internal static partial int weft_loro_native_branch_merge_probe(
        nint doc, ReadOnlySpan<byte> field, nuint fieldLen, out nint outPtr, out nuint outLen);

    [LibraryImport(Lib)]
    internal static partial void weft_loro_buf_free(nint ptr, nuint len);

    [LibraryImport(Lib)]
    internal static partial uint weft_loro_abi_version();
}
