namespace Weft;

/// <summary>Código de error del motor CRDT tal como cruza la frontera FFI.</summary>
public enum WeftErrorCode
{
    /// <summary>Blob/update no decodificable.</summary>
    Decode,

    /// <summary>Fallo aplicando un update.</summary>
    Apply,

    /// <summary>Texto de entrada no es UTF-8 válido.</summary>
    Utf8,

    /// <summary>Índice/longitud fuera de rango.</summary>
    OutOfBounds,

    /// <summary>Un panic del motor fue capturado en la frontera (P-I).</summary>
    Panic,
}

/// <summary>Base de toda excepción de Weft.</summary>
public class WeftException : Exception
{
    /// <summary>Crea una excepción con mensaje.</summary>
    public WeftException(string message) : base(message) { }

    /// <summary>Crea una excepción con mensaje y causa.</summary>
    public WeftException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>Un blob o update no se pudo decodificar (código FFI <c>DECODE</c>).</summary>
public sealed class CorruptUpdateException : WeftException
{
    /// <summary>Crea la excepción con un mensaje por defecto.</summary>
    public CorruptUpdateException()
        : base("El blob o update no es decodificable (formato corrupto o incompatible).") { }

    /// <summary>Crea la excepción con un mensaje explícito.</summary>
    public CorruptUpdateException(string message) : base(message) { }
}

/// <summary>Error del motor CRDT a través de la frontera (apply/utf8/panic).</summary>
public sealed class WeftEngineException : WeftException
{
    /// <summary>Código de error tipificado del motor.</summary>
    public WeftErrorCode ErrorCode { get; }

    /// <summary>Crea la excepción con su código y mensaje.</summary>
    public WeftEngineException(WeftErrorCode errorCode, string message) : base(message)
    {
        ErrorCode = errorCode;
    }
}

/// <summary>La integridad de un blob content-addressed no verifica (usado por Weft.Versioning).</summary>
public sealed class BlobIntegrityException : WeftException
{
    /// <summary>Crea la excepción con un mensaje explícito.</summary>
    public BlobIntegrityException(string message) : base(message) { }
}
