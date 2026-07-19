using Weft;
using Weft.Yrs;
using Xunit.Abstractions;

namespace Weft.Core.Tests;

/// <summary>
/// Delta size benchmark (T062, SC-004): in the reference reconnection scenario, incremental
/// sync transfers ≥ 90 % fewer bytes than resending the full state.
/// </summary>
/// <remarks>
/// <para>
/// <b>The reference scenario is defined here</b> because the spec did not define it. SC-004
/// (<c>spec.md:175</c>) cites «523 B → 29 B», but that figure comes from a cell labeled
/// «Rough perf» in <c>docs/spikes/spike03/hallazgos-spike-03.md:38</c>, from a spike whose code
/// is throwaway by design (<c>docs/spikes/README.md:4-5</c>) and does not live in this repo: it
/// documents neither document size, nor number of edits, nor exactly what was exported. It is cited
/// as historical context —523→29 is 94.5 %, consistent with the threshold— and NOT as a
/// byte-for-byte expectation: asserting those absolutes would tie the suite to an irreproducible
/// spike and would break it on any yrs bump with nothing actually wrong. What is binding in SC-004
/// is the ratio.
/// </para>
/// <para>
/// The shape of the scenario comes from the prose of SC-004 («reconnection») and from the
/// postcondition of the relay in <c>contracts/server-api.md:116</c> («a reconnection with a prior
/// SV receives only the delta»):
/// </para>
/// <list type="number">
///   <item>An author has a reference document: a paragraph of prose, the order of magnitude
///   (~500 B of state) of the spike reference.</item>
///   <item>A peer catches up and captures its state vector — the «what I know» that it will send
///   when reconnecting.</item>
///   <item>While the peer is disconnected, the author receives ONE small edit.</item>
///   <item>On reconnecting, the peer requests only what it is missing.</item>
/// </list>
/// <para>
/// We measure <c>ExportUpdateSince(sv).Length</c> (what travels) against <c>ExportState().Length</c>
/// (what would travel without incremental sync). The scenario was fixed before measuring and the
/// assert is the spec threshold, not a number calibrated after the fact.
/// </para>
/// <para>
/// The client-ids are fixed (a <c>YrsEngine</c> capability, CHARTER-09/FU-012) so that the measured
/// size does not depend on the varint of a random id: a 53-bit id takes several more bytes than a
/// small one, and that is noise that does not belong in the measurement.
/// </para>
/// </remarks>
public sealed class DeltaSizeBenchmark
{
    private readonly ITestOutputHelper _output;

    public DeltaSizeBenchmark(ITestOutputHelper output) => _output = output;

    private const ulong AutorClientId = 1;
    private const ulong ParClientId = 2;

    /// <summary>Reference document: prose, ~500 B of exported state.</summary>
    private const string ParrafoReferencia =
        "El telar levanta la urdimbre y la trama cruza entre los hilos tensados; cada pasada fija " +
        "el dibujo que ya no podrá deshacerse sin destejer lo anterior. Quien mira la tela " +
        "terminada no ve las decisiones intermedias, sino el patrón que sobrevivió a todas ellas. " +
        "Un documento colaborativo se teje igual: muchas manos empujan la lanzadera a la vez, y el " +
        "orden final no lo dicta quien llegó primero, sino la regla que todos aceptaron de " +
        "antemano.";

    /// <summary>The edit that occurs while the peer is disconnected.</summary>
    private const string EdicionDuranteDesconexion = "Nota al margen: ";

    [Fact]
    public void Reconnect_delta_is_at_least_90_pct_smaller_than_full_state()
    {
        using ICrdtDoc autor = YrsEngine.Instance.CreateDoc(AutorClientId);
        autor.InsertText("body", 0, ParrafoReferencia);

        // The peer catches up and captures its SV just before disconnecting.
        using ICrdtDoc par = YrsEngine.Instance.CreateDoc(ParClientId);
        par.ApplyUpdate(autor.ExportState());
        byte[] svAlDesconectar = par.ExportStateVector();

        // While the peer is away, the document moves forward.
        autor.InsertText("body", 0, EdicionDuranteDesconexion);

        // Reconnection: what travels vs what would travel by resending the full state.
        byte[] delta = autor.ExportUpdateSince(svAlDesconectar);
        byte[] estadoCompleto = autor.ExportState();

        // A delta that does not converge is not a cheap delta, it is a broken delta: measuring its
        // size without checking that it syncs would let an empty buffer with a 100 % reduction pass.
        par.ApplyUpdate(delta);
        Assert.Equal(autor.ExportState(), par.ExportState());
        Assert.Equal(EdicionDuranteDesconexion + ParrafoReferencia, par.GetText("body"));

        double reduccion = 1.0 - ((double)delta.Length / estadoCompleto.Length);

        // A benchmark reports its measurement: the number is the deliverable, not just the green/red.
        _output.WriteLine(
            $"SC-004 · escenario de reconexión: estado completo={estadoCompleto.Length} B, " +
            $"delta={delta.Length} B, reducción={reduccion:P1} (umbral ≥ 90 %). " +
            $"Referencia histórica del spike03: 523 B → 29 B (94,5 %).");

        Assert.True(
            reduccion >= 0.90,
            $"SC-004: delta={delta.Length} B vs estado completo={estadoCompleto.Length} B → " +
            $"reducción medida {reduccion:P1}, se exige ≥ 90 %.");
    }
}
