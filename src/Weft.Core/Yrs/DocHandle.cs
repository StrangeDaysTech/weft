using Microsoft.Win32.SafeHandles;

namespace Weft.Yrs;

/// <summary>
/// Handle seguro para el puntero opaco <c>WeftDoc*</c>. Un <c>SafeHandle</c> resuelve de
/// raíz las tres patologías de FFI: fuga (finalizador de respaldo), double-free (el runtime
/// garantiza un solo <see cref="ReleaseHandle"/>) y use-after-free (ref-count durante la llamada
/// nativa vía <see cref="HandleLease"/>).
/// </summary>
/// <remarks>
/// FRICCIÓN FFI (research R2): el source generator <c>[LibraryImport]</c> no marshala
/// <c>SafeHandle</c> (SYSLIB1051). Por eso las declaraciones P/Invoke usan <c>nint</c> crudo
/// y las llamadas prestan el puntero con <see cref="HandleLease"/>.
/// </remarks>
internal sealed class DocHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    public DocHandle(nint handle) : base(ownsHandle: true) => SetHandle(handle);

    protected override bool ReleaseHandle()
    {
        // El documento se libera SOLO con la función del shim, nunca con el GC/Marshal (P-I).
        NativeMethods.weft_doc_free(handle);
        return true;
    }
}

/// <summary>
/// Presta el puntero crudo de un <see cref="DocHandle"/> incrementando su ref-count mientras dura
/// la llamada nativa (equivalente manual al marshalling de SafeHandle que <c>[LibraryImport]</c> no
/// ofrece). Liberar con <c>using</c>: el ref-count se decrementa al salir del ámbito.
/// </summary>
internal readonly ref struct HandleLease
{
    private readonly DocHandle _handle;
    private readonly bool _added;

    /// <summary>Puntero nativo válido durante la vida del lease.</summary>
    public readonly nint Ptr;

    public HandleLease(DocHandle handle)
    {
        _handle = handle;
        bool added = false;
        handle.DangerousAddRef(ref added);
        _added = added;
        Ptr = handle.DangerousGetHandle();
    }

    public void Dispose()
    {
        if (_added)
        {
            _handle.DangerousRelease();
        }
    }
}
