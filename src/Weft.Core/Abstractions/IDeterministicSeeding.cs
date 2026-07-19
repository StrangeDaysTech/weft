namespace Weft;

/// <summary>
/// Optional capability to seed the replica identity of a new document (yrs's <c>client_id</c>,
/// Loro's <c>peer_id</c>), enabling reproducible exports cross-run and cross-RID.
/// </summary>
/// <remarks>
/// <para>
/// It is exposed as an optional capability —not as a method of <see cref="ICrdtEngine"/>— because of the
/// <b>asymmetry of the valid domain</b> between engines: yrs accepts ids <c>&lt; 2^53</c> (53-bit
/// encoding), Loro accepts every <c>ulong</c> except <c>ulong.MaxValue</c> (reserved). A single method
/// could not state a uniform contract over that domain, forcing the caller to branch by engine
/// — the leak the constitution P-IV exists to prevent. <see cref="MaxReplicaIdExclusive"/> makes the
/// domain part of the contract. It is the same pattern as <see cref="ICrdtEngine.NativeVersioning"/>.
/// </para>
/// <para>
/// <b>Intended use: test/corpus determinism, NOT production identity.</b> Reusing the same
/// replica identity across concurrent writers breaks the CRDT guarantee (Loro documents it as
/// document corruption). The relay and the broker create documents with
/// <see cref="ICrdtEngine.CreateDoc()"/>, which assigns a random identity; they do not seed.
/// </para>
/// </remarks>
public interface IDeterministicSeeding
{
    /// <summary>
    /// EXCLUSIVE upper bound of the valid replica identity for this engine. yrs: <c>1UL &lt;&lt;
    /// 53</c>. Loro: <see cref="ulong.MaxValue"/> (every <c>ulong</c> except the reserved value).
    /// </summary>
    ulong MaxReplicaIdExclusive { get; }

    /// <summary>
    /// Creates an empty document with the fixed replica identity <paramref name="replicaId"/>.
    /// </summary>
    /// <param name="replicaId">Replica identity; must be <c>&lt; <see cref="MaxReplicaIdExclusive"/></c>.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="replicaId"/> outside the valid domain.</exception>
    ICrdtDoc CreateDoc(ulong replicaId);
}
