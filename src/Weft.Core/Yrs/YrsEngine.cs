namespace Weft.Yrs;

/// <summary>
/// CRDT engine backed by <c>yrs</c> via the <c>weft-yrs-ffi</c> shim. Stateless: the
/// engine is a document factory. <see cref="NativeVersioning"/> is <c>null</c> (yrs offers no
/// native versioning; versioning lives in the engine-agnostic domain layer).
/// </summary>
public sealed class YrsEngine : ICrdtEngine
{
    private YrsEngine() { }

    /// <summary>Shared engine instance (stateless, thread-safe).</summary>
    public static YrsEngine Instance { get; } = new();

    /// <summary>Stable engine name; single source shared with <see cref="YrsDoc.EngineName"/>.</summary>
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
    /// Creates a document with a FIXED <paramref name="clientId"/>. Enables byte-identical
    /// cross-implementation parity with Yjs (gate <c>determinism-yjs</c>, FU-012), which requires
    /// deterministic client-ids. The id must fit in 53 bits (yrs 0.26+ encoding). This concrete method
    /// is kept; the equivalent cross-engine capability lives in <see cref="DeterministicSeeding"/>
    /// (CHARTER-13/FU-016), which Loro also implements via <c>set_peer_id</c>.
    /// </summary>
    public ICrdtDoc CreateDoc(ulong clientId) => YrsDoc.Create(clientId);

    /// <inheritdoc/>
    public ICrdtDoc LoadDoc(ReadOnlySpan<byte> blob) => YrsDoc.Load(blob);
}
