namespace Weft;

/// <summary>Error code of the CRDT engine as it crosses the FFI boundary.</summary>
public enum WeftErrorCode
{
    /// <summary>Blob/update not decodable.</summary>
    Decode,

    /// <summary>Failure applying an update.</summary>
    Apply,

    /// <summary>Input text is not valid UTF-8.</summary>
    Utf8,

    /// <summary>Index/length out of range.</summary>
    OutOfBounds,

    /// <summary>An engine panic was captured at the boundary (P-I).</summary>
    Panic,
}

/// <summary>Base of every Weft exception.</summary>
public class WeftException : Exception
{
    /// <summary>Creates an exception with a message.</summary>
    public WeftException(string message) : base(message) { }

    /// <summary>Creates an exception with a message and cause.</summary>
    public WeftException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>A blob or update could not be decoded (FFI code <c>DECODE</c>).</summary>
public sealed class CorruptUpdateException : WeftException
{
    /// <summary>Creates the exception with a default message.</summary>
    public CorruptUpdateException()
        : base("The blob or update is not decodable (corrupt or incompatible format).") { }

    /// <summary>Creates the exception with an explicit message.</summary>
    public CorruptUpdateException(string message) : base(message) { }
}

/// <summary>CRDT engine error across the boundary (apply/utf8/panic).</summary>
public sealed class WeftEngineException : WeftException
{
    /// <summary>Typed error code of the engine.</summary>
    public WeftErrorCode ErrorCode { get; }

    /// <summary>Creates the exception with its code and message.</summary>
    public WeftEngineException(WeftErrorCode errorCode, string message) : base(message)
    {
        ErrorCode = errorCode;
    }
}

/// <summary>The integrity of a content-addressed blob does not verify (used by Weft.Versioning).</summary>
public sealed class BlobIntegrityException : WeftException
{
    /// <summary>Creates the exception with an explicit message.</summary>
    public BlobIntegrityException(string message) : base(message) { }
}
