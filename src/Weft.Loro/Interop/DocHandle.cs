using Microsoft.Win32.SafeHandles;

namespace Weft.Loro.Interop;

/// <summary>Safe handle for the opaque <c>WeftLoroDoc*</c> pointer (same pattern as Weft.Yrs).</summary>
internal sealed class DocHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    public DocHandle(nint handle) : base(ownsHandle: true) => SetHandle(handle);

    protected override bool ReleaseHandle()
    {
        NativeMethods.weft_loro_doc_free(handle);
        return true;
    }
}

/// <summary>Leases the raw pointer with ref-count for the duration of the native call (SYSLIB1051, research R2).</summary>
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
