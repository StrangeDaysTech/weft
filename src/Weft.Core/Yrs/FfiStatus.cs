namespace Weft.Yrs;

/// <summary>
/// Traduce un código de estado <c>i32</c> del shim a la excepción idiomática correspondiente
/// (mapeo de <c>contracts/ffi-abi.md</c>). Centralizado para que el mapeo sea verificable
/// end-to-end (p. ej. la ruta de panic, SC-009).
/// </summary>
internal static class FfiStatus
{
    internal static void ThrowIfError(int rc)
    {
        switch (rc)
        {
            case 0: // WEFT_OK
                return;
            case -1: // NULL_ARG — defensa; la capa C# valida antes de cruzar
                throw new WeftException("Argumento nulo inesperado en la frontera FFI.");
            case -2: // DECODE
                throw new CorruptUpdateException();
            case -3: // APPLY
                throw new WeftEngineException(WeftErrorCode.Apply, "El motor no pudo aplicar el update.");
            case -4: // UTF8
                throw new WeftEngineException(WeftErrorCode.Utf8, "El texto de entrada no es UTF-8 válido.");
            case -5: // OUT_OF_BOUNDS
                throw new ArgumentOutOfRangeException("index", "El índice o la longitud están fuera de rango.");
            case -127: // PANIC
                throw new WeftEngineException(WeftErrorCode.Panic, "El motor sufrió un panic capturado en la frontera.");
            default:
                throw new WeftEngineException(WeftErrorCode.Apply, $"Código FFI desconocido del shim: {rc}.");
        }
    }
}
