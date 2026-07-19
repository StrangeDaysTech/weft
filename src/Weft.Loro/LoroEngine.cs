namespace Weft.Loro;

/// <summary>
/// CRDT engine backed by Loro (via the <c>weft-loro-ffi</c> shim). Dual-path adapter that proves
/// the portability of the <see cref="ICrdtEngine"/> abstraction (constitution P-IV): the same
/// versioning suite runs identically over yrs and Loro.
/// </summary>
public sealed class LoroEngine : ICrdtEngine
{
    private LoroEngine() { }

    /// <summary>Shared engine instance (stateless, thread-safe).</summary>
    public static LoroEngine Instance { get; } = new();

    /// <summary>Stable engine name; single source shared with <see cref="LoroDoc.EngineName"/>.</summary>
    internal const string EngineName = "loro";

    /// <inheritdoc/>
    public string Name => EngineName;

    /// <inheritdoc/>
    /// <remarks>
    /// Loro offers native versioning (diff/branch/shallow-snapshot); exposed as an optional
    /// <see cref="INativeVersioning"/> via demonstrative probes (CHARTER-10/FU-006). The
    /// core versioning (content-addressed, engine-agnostic) does NOT depend on these probes; their output
    /// is not deterministic and does not feed <c>VersionId</c>.
    /// </remarks>
    public INativeVersioning? NativeVersioning => LoroNativeVersioning.Instance;

    /// <inheritdoc/>
    public IDeterministicSeeding? DeterministicSeeding => LoroDeterministicSeeding.Instance;

    /// <inheritdoc/>
    public ICrdtDoc CreateDoc() => LoroDoc.Create();

    /// <inheritdoc/>
    public ICrdtDoc LoadDoc(ReadOnlySpan<byte> blob) => LoroDoc.Load(blob);
}
