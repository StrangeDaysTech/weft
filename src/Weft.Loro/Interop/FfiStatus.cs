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
                throw new WeftException("Argumento nulo inesperado en la frontera FFI (Loro).");
            case -2:
                throw new CorruptUpdateException();
            case -3:
                throw new WeftEngineException(WeftErrorCode.Apply, "El motor Loro no pudo aplicar el update.");
            case -4:
                throw new WeftEngineException(WeftErrorCode.Utf8, "El texto de entrada no es UTF-8 válido.");
            case -5:
                throw new ArgumentOutOfRangeException("index", "El índice o la longitud están fuera de rango.");
            case -127:
                throw new WeftEngineException(WeftErrorCode.Panic, "El motor Loro sufrió un panic capturado en la frontera.");
            default:
                throw new WeftEngineException(WeftErrorCode.Apply, $"Código FFI desconocido del shim Loro: {rc}.");
        }
    }
}
