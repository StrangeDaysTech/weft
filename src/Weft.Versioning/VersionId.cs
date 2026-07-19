using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;

namespace Weft.Versioning;

/// <summary>
/// Content-addressed identity of a version: SHA-256 of the deterministic export (constitution P-III).
/// Immutable value type; equality by value; 64-character lowercase hex representation.
/// </summary>
public readonly struct VersionId : IEquatable<VersionId>
{
    private readonly byte[]? _bytes; // 32 bytes; null only in the `default` value.

    private VersionId(byte[] bytes) => _bytes = bytes;

    /// <summary>Computes the identity of a blob (SHA-256).</summary>
    public static VersionId FromBlob(ReadOnlySpan<byte> blob) => new(SHA256.HashData(blob));

    /// <summary>Parses a 64-character hex representation.</summary>
    /// <exception cref="FormatException">The string is not a valid SHA-256 hex hash.</exception>
    public static VersionId Parse(string hex)
    {
        if (!TryParse(hex, out VersionId id))
        {
            throw new FormatException("Un VersionId debe ser 64 caracteres hexadecimales (SHA-256).");
        }
        return id;
    }

    /// <summary>Attempts to parse a 64-character hex representation.</summary>
    public static bool TryParse([NotNullWhen(true)] string? hex, out VersionId id)
    {
        id = default;
        if (hex is not { Length: 64 })
        {
            return false;
        }
        foreach (char c in hex)
        {
            if (!Uri.IsHexDigit(c))
            {
                return false;
            }
        }
        id = new VersionId(Convert.FromHexString(hex));
        return true;
    }

    /// <summary>The 32 bytes of the hash.</summary>
    public ReadOnlySpan<byte> AsSpan() => _bytes ?? [];

    /// <summary>Lowercase hex representation (64 characters).</summary>
    public override string ToString() => _bytes is null ? "" : Convert.ToHexStringLower(_bytes);

    /// <inheritdoc/>
    public bool Equals(VersionId other) => AsSpan().SequenceEqual(other.AsSpan());

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is VersionId other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var span = AsSpan();
        // The bytes of a SHA-256 are already uniformly distributed: the first 4 suffice.
        return span.Length >= 4 ? BitConverter.ToInt32(span[..4]) : 0;
    }

    /// <summary>Equality by value.</summary>
    public static bool operator ==(VersionId left, VersionId right) => left.Equals(right);

    /// <summary>Inequality by value.</summary>
    public static bool operator !=(VersionId left, VersionId right) => !left.Equals(right);
}
