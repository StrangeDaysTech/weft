namespace Weft.Yrs;

/// <summary>
/// Translates an <c>i32</c> status code from the shim into the corresponding idiomatic exception
/// (mapping from <c>contracts/ffi-abi.md</c>). Centralized so the mapping is verifiable
/// end-to-end (e.g. the panic path, SC-009).
/// </summary>
internal static class FfiStatus
{
    internal static void ThrowIfError(int rc)
    {
        switch (rc)
        {
            case 0: // WEFT_OK
                return;
            case -1: // NULL_ARG — defense; the C# layer validates before crossing
                throw new WeftException("Unexpected null argument at the FFI boundary.");
            case -2: // DECODE
                throw new CorruptUpdateException();
            case -3: // APPLY
                throw new WeftEngineException(WeftErrorCode.Apply, "The engine could not apply the update.");
            case -4: // UTF8
                throw new WeftEngineException(WeftErrorCode.Utf8, "The input text is not valid UTF-8.");
            case -5: // OUT_OF_BOUNDS
                throw new ArgumentOutOfRangeException("index", "The index or length is out of range.");
            case -127: // PANIC
                throw new WeftEngineException(WeftErrorCode.Panic, "The engine hit a panic captured at the boundary.");
            default:
                throw new WeftEngineException(WeftErrorCode.Apply, $"Unknown FFI code from the shim: {rc}.");
        }
    }
}
