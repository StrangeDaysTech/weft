using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;

namespace Weft.Versioning;

/// <summary>
/// Identidad content-addressed de una versión: SHA-256 del export determinista (constitución P-III).
/// Value type inmutable; igualdad por valor; representación hex lowercase de 64 caracteres.
/// </summary>
public readonly struct VersionId : IEquatable<VersionId>
{
    private readonly byte[]? _bytes; // 32 bytes; null solo en el valor `default`.

    private VersionId(byte[] bytes) => _bytes = bytes;

    /// <summary>Calcula la identidad de un blob (SHA-256).</summary>
    public static VersionId FromBlob(ReadOnlySpan<byte> blob) => new(SHA256.HashData(blob));

    /// <summary>Parsea una representación hex de 64 caracteres.</summary>
    /// <exception cref="FormatException">La cadena no es un hash SHA-256 hex válido.</exception>
    public static VersionId Parse(string hex)
    {
        if (!TryParse(hex, out VersionId id))
        {
            throw new FormatException("Un VersionId debe ser 64 caracteres hexadecimales (SHA-256).");
        }
        return id;
    }

    /// <summary>Intenta parsear una representación hex de 64 caracteres.</summary>
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

    /// <summary>Los 32 bytes del hash.</summary>
    public ReadOnlySpan<byte> AsSpan() => _bytes ?? [];

    /// <summary>Representación hex lowercase (64 caracteres).</summary>
    public override string ToString() => _bytes is null ? "" : Convert.ToHexStringLower(_bytes);

    /// <inheritdoc/>
    public bool Equals(VersionId other) => AsSpan().SequenceEqual(other.AsSpan());

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is VersionId other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var span = AsSpan();
        // Los bytes de un SHA-256 ya están uniformemente distribuidos: los primeros 4 bastan.
        return span.Length >= 4 ? BitConverter.ToInt32(span[..4]) : 0;
    }

    /// <summary>Igualdad por valor.</summary>
    public static bool operator ==(VersionId left, VersionId right) => left.Equals(right);

    /// <summary>Desigualdad por valor.</summary>
    public static bool operator !=(VersionId left, VersionId right) => !left.Equals(right);
}
