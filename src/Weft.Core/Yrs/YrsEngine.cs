namespace Weft.Yrs;

/// <summary>
/// Motor CRDT respaldado por <c>yrs</c> vía el shim <c>weft-yrs-ffi</c>. Sin estado propio: el
/// motor es una fábrica de documentos. <see cref="NativeVersioning"/> es <c>null</c> (yrs no ofrece
/// versionado nativo; el versionado vive en la capa de dominio engine-agnóstica).
/// </summary>
public sealed class YrsEngine : ICrdtEngine
{
    private YrsEngine() { }

    /// <summary>Instancia compartida del motor (sin estado, thread-safe).</summary>
    public static YrsEngine Instance { get; } = new();

    /// <summary>Nombre estable del motor; fuente única compartida con <see cref="YrsDoc.EngineName"/>.</summary>
    internal const string EngineName = "yrs";

    /// <inheritdoc/>
    public string Name => EngineName;

    /// <inheritdoc/>
    public INativeVersioning? NativeVersioning => null;

    /// <inheritdoc/>
    public ICrdtDoc CreateDoc() => YrsDoc.Create();

    /// <inheritdoc/>
    public ICrdtDoc LoadDoc(ReadOnlySpan<byte> blob) => YrsDoc.Load(blob);
}
