namespace Weft;

/// <summary>
/// Capacidad opcional para sembrar la identidad de réplica de un documento nuevo (el <c>client_id</c>
/// de yrs, el <c>peer_id</c> de Loro), habilitando exports reproducibles cross-run y cross-RID.
/// </summary>
/// <remarks>
/// <para>
/// Se expone como capacidad opcional —no como un método de <see cref="ICrdtEngine"/>— por la
/// <b>asimetría del dominio válido</b> entre motores: yrs acepta ids <c>&lt; 2^53</c> (encoding de 53
/// bits), Loro acepta todo <c>ulong</c> salvo <c>ulong.MaxValue</c> (reservado). Un método único no
/// podría enunciar un contrato uniforme sobre ese dominio, forzando al llamador a ramificar por motor
/// — la fuga que la constitución P-IV existe para evitar. <see cref="MaxReplicaIdExclusive"/> hace del
/// dominio parte del contrato. Es el mismo patrón que <see cref="ICrdtEngine.NativeVersioning"/>.
/// </para>
/// <para>
/// <b>Uso previsto: determinismo de test/corpus, NO identidad de producción.</b> Reusar la misma
/// identidad de réplica entre escritores concurrentes rompe la garantía CRDT (Loro lo documenta como
/// corrupción del documento). El relay y el broker crean documentos con
/// <see cref="ICrdtEngine.CreateDoc()"/>, que asigna una identidad aleatoria; no siembran.
/// </para>
/// </remarks>
public interface IDeterministicSeeding
{
    /// <summary>
    /// Cota superior EXCLUSIVA de la identidad de réplica válida para este motor. yrs: <c>1UL &lt;&lt;
    /// 53</c>. Loro: <see cref="ulong.MaxValue"/> (todo <c>ulong</c> salvo el valor reservado).
    /// </summary>
    ulong MaxReplicaIdExclusive { get; }

    /// <summary>
    /// Crea un documento vacío con la identidad de réplica <paramref name="replicaId"/> fija.
    /// </summary>
    /// <param name="replicaId">Identidad de réplica; debe ser <c>&lt; <see cref="MaxReplicaIdExclusive"/></c>.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="replicaId"/> fuera del dominio válido.</exception>
    ICrdtDoc CreateDoc(ulong replicaId);
}
