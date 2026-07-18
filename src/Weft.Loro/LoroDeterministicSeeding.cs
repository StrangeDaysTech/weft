namespace Weft.Loro;

/// <summary>
/// Siembra determinista para <see cref="LoroEngine"/>: fija el <c>peer_id</c> del documento. El
/// dominio válido es todo <c>ulong</c> salvo <see cref="ulong.MaxValue"/> (reservado por Loro).
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
