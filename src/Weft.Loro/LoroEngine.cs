namespace Weft.Loro;

/// <summary>
/// Motor CRDT respaldado por Loro (vía el shim <c>weft-loro-ffi</c>). Adaptador dual-path que prueba
/// la portabilidad de la abstracción <see cref="ICrdtEngine"/> (constitución P-IV): la misma suite de
/// versionado corre idéntica sobre yrs y Loro.
/// </summary>
public sealed class LoroEngine : ICrdtEngine
{
    private LoroEngine() { }

    /// <summary>Instancia compartida del motor (sin estado, thread-safe).</summary>
    public static LoroEngine Instance { get; } = new();

    /// <inheritdoc/>
    public string Name => "loro";

    /// <inheritdoc/>
    /// <remarks>
    /// Loro ofrece versionado nativo (diff/branch/shallow-snapshot); esas capacidades se exponen como
    /// <see cref="INativeVersioning"/> opcional en una iteración posterior. El versionado del núcleo
    /// (content-addressed, engine-agnóstico) no depende de ellas.
    /// </remarks>
    public INativeVersioning? NativeVersioning => null;

    /// <inheritdoc/>
    public ICrdtDoc CreateDoc() => LoroDoc.Create();

    /// <inheritdoc/>
    public ICrdtDoc LoadDoc(ReadOnlySpan<byte> blob) => LoroDoc.Load(blob);
}
