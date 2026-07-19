namespace Weft.Loro.Interop;

/// <summary>Translates a status code from the Loro shim into the idiomatic exception (same mapping as Weft.Yrs).</summary>
internal static class FfiStatus
{
    internal static void ThrowIfError(int rc)
    {
        switch (rc)
        {
            case 0:
                return;
            case -1:
                throw new WeftException("Unexpected null argument at the FFI boundary (Loro).");
            case -2:
                throw new CorruptUpdateException();
            case -3:
                throw new WeftEngineException(WeftErrorCode.Apply, "The Loro engine could not apply the update.");
            case -4:
                throw new WeftEngineException(WeftErrorCode.Utf8, "The input text is not valid UTF-8.");
            case -5:
                throw new ArgumentOutOfRangeException("index", "The index or length is out of range.");
            case -127:
                throw new WeftEngineException(WeftErrorCode.Panic, "The Loro engine hit a panic captured at the boundary.");
            default:
                throw new WeftEngineException(WeftErrorCode.Apply, $"Unknown FFI code from the Loro shim: {rc}.");
        }
    }
}
