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

    /// <summary>Nombre estable del motor; fuente única compartida con <see cref="LoroDoc.EngineName"/>.</summary>
    internal const string EngineName = "loro";

    /// <inheritdoc/>
    public string Name => EngineName;

    /// <inheritdoc/>
    /// <remarks>
    /// Loro ofrece versionado nativo (diff/branch/shallow-snapshot); expuesto como
    /// <see cref="INativeVersioning"/> opcional vía probes demostrativos (CHARTER-10/FU-006). El
    /// versionado del núcleo (content-addressed, engine-agnóstico) NO depende de estos probes; su salida
    /// no es determinista y no alimenta <c>VersionId</c>.
    /// </remarks>
    public INativeVersioning? NativeVersioning => LoroNativeVersioning.Instance;

    /// <inheritdoc/>
    public ICrdtDoc CreateDoc() => LoroDoc.Create();

    /// <inheritdoc/>
    public ICrdtDoc LoadDoc(ReadOnlySpan<byte> blob) => LoroDoc.Load(blob);
}
