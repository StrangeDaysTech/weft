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
    public IDeterministicSeeding? DeterministicSeeding => YrsDeterministicSeeding.Instance;

    /// <inheritdoc/>
    public ICrdtDoc CreateDoc() => YrsDoc.Create();

    /// <summary>
    /// Crea un documento con un <paramref name="clientId"/> FIJO. Habilita la paridad byte-idéntica
    /// cross-implementación con Yjs (gate <c>determinism-yjs</c>, FU-012), que exige client-ids
    /// deterministas. El id debe caber en 53 bits (encoding de yrs 0.26+). Este método concreto se
    /// conserva; la capacidad cross-engine equivalente vive en <see cref="DeterministicSeeding"/>
    /// (CHARTER-13/FU-016), que Loro también implementa vía <c>set_peer_id</c>.
    /// </summary>
    public ICrdtDoc CreateDoc(ulong clientId) => YrsDoc.Create(clientId);

    /// <inheritdoc/>
    public ICrdtDoc LoadDoc(ReadOnlySpan<byte> blob) => YrsDoc.Load(blob);
}
