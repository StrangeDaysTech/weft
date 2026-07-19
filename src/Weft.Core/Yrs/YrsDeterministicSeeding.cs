namespace Weft.Yrs;

/// <summary>
/// Deterministic seeding for <see cref="YrsEngine"/>: fixes the document's <c>client_id</c>. The
/// valid domain is <c>&lt; 2^53</c> (yrs 0.26+ 53-bit client-id encoding).
/// </summary>
internal sealed class YrsDeterministicSeeding : IDeterministicSeeding
{
    internal static YrsDeterministicSeeding Instance { get; } = new();

    private YrsDeterministicSeeding() { }

    /// <inheritdoc/>
    public ulong MaxReplicaIdExclusive => 1UL << 53;

    /// <inheritdoc/>
    public ICrdtDoc CreateDoc(ulong replicaId) => YrsEngine.Instance.CreateDoc(replicaId);
}
