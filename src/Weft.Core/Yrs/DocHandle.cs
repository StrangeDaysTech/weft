using Microsoft.Win32.SafeHandles;

namespace Weft.Yrs;

/// <summary>
/// Safe handle for the opaque <c>WeftDoc*</c> pointer. A <c>SafeHandle</c> resolves at the
/// root the three FFI pathologies: leak (backing finalizer), double-free (the runtime
/// guarantees a single <see cref="ReleaseHandle"/>) and use-after-free (ref-count during the native
/// call via <see cref="HandleLease"/>).
/// </summary>
/// <remarks>
/// FFI FRICTION (research R2): the source generator <c>[LibraryImport]</c> does not marshal
/// <c>SafeHandle</c> (SYSLIB1051). That is why the P/Invoke declarations use a raw <c>nint</c>
/// and the calls lend the pointer with <see cref="HandleLease"/>.
/// </remarks>
internal sealed class DocHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    public DocHandle(nint handle) : base(ownsHandle: true) => SetHandle(handle);

    protected override bool ReleaseHandle()
    {
        // The document is freed ONLY with the shim function, never with the GC/Marshal (P-I).
        NativeMethods.weft_doc_free(handle);
        return true;
    }
}

/// <summary>
/// Lends the raw pointer of a <see cref="DocHandle"/> by incrementing its ref-count for the duration
/// of the native call (manual equivalent of the SafeHandle marshalling that <c>[LibraryImport]</c>
/// does not offer). Release with <c>using</c>: the ref-count is decremented on leaving the scope.
/// </summary>
internal readonly ref struct HandleLease
{
    private readonly DocHandle _handle;
    private readonly bool _added;

    /// <summary>Native pointer valid for the lifetime of the lease.</summary>
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
