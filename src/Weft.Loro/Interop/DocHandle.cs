using Microsoft.Win32.SafeHandles;

namespace Weft.Loro.Interop;

/// <summary>Handle seguro para el puntero opaco <c>WeftLoroDoc*</c> (mismo patrón que Weft.Yrs).</summary>
internal sealed class DocHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    public DocHandle(nint handle) : base(ownsHandle: true) => SetHandle(handle);

    protected override bool ReleaseHandle()
    {
        NativeMethods.weft_loro_doc_free(handle);
        return true;
    }
}

/// <summary>Presta el puntero crudo con ref-count durante la llamada nativa (SYSLIB1051, research R2).</summary>
internal readonly ref struct HandleLease
{
    private readonly DocHandle _handle;
    private readonly bool _added;

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
