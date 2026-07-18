namespace Weft.Yrs;

/// <summary>
/// Siembra determinista para <see cref="YrsEngine"/>: fija el <c>client_id</c> del documento. El
/// dominio válido es <c>&lt; 2^53</c> (encoding de client-ids de 53 bits de yrs 0.26+).
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
