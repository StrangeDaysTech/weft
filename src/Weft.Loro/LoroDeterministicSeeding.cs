namespace Weft.Loro;

/// <summary>
/// Deterministic seeding for <see cref="LoroEngine"/>: pins the document's <c>peer_id</c>. The
/// valid domain is every <c>ulong</c> except <see cref="ulong.MaxValue"/> (reserved by Loro).
/// </summary>
internal sealed class LoroDeterministicSeeding : IDeterministicSeeding
{
    internal static LoroDeterministicSeeding Instance { get; } = new();

    private LoroDeterministicSeeding() { }

    /// <inheritdoc/>
    public ulong MaxReplicaIdExclusive => ulong.MaxValue;

    /// <inheritdoc/>
    public ICrdtDoc CreateDoc(ulong replicaId) => LoroDoc.Create(replicaId);
}
