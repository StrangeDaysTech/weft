using Weft;
using Weft.Yrs;
using Xunit.Abstractions;

namespace Weft.Core.Tests;

/// <summary>
/// Benchmark de tamaño de delta (T062, SC-004): en el escenario de referencia de reconexión, el
/// sync incremental transfiere ≥ 90 % menos bytes que reenviar el estado completo.
/// </summary>
/// <remarks>
/// <para>
/// <b>El escenario de referencia se define aquí</b> porque la spec no lo definía. SC-004
/// (<c>spec.md:175</c>) cita «523 B → 29 B», pero ese dato sale de una celda etiquetada
/// «Rough perf» en <c>docs/spikes/spike03/hallazgos-spike-03.md:38</c>, de un spike cuyo código
/// es desechable por diseño (<c>docs/spikes/README.md:4-5</c>) y no vive en este repo: no
/// documenta tamaño de documento, ni número de ediciones, ni qué se exportó exactamente. Se cita
/// como contexto histórico —523→29 es un 94,5 %, consistente con el umbral— y NO como expectativa
/// byte a byte: asertar esos absolutos ataría la suite a un spike irreproducible y la rompería
/// cualquier bump de yrs sin que nada estuviera mal. Lo vinculante de SC-004 es el ratio.
/// </para>
/// <para>
/// La forma del escenario sale de la prosa de SC-004 («reconexión») y de la postcondición del
/// relay en <c>contracts/server-api.md:116</c> («una reconexión con SV previo recibe solo el
/// delta»):
/// </para>
/// <list type="number">
///   <item>Un autor tiene un documento de referencia: un párrafo de prosa, el orden de magnitud
///   (~500 B de estado) de la referencia del spike.</item>
///   <item>Un par se pone al día y captura su state vector — el «qué conozco» que enviará al
///   reconectar.</item>
///   <item>Mientras el par está desconectado, el autor recibe UNA edición pequeña.</item>
///   <item>Al reconectar, el par pide solo lo que le falta.</item>
/// </list>
/// <para>
/// Se mide <c>ExportUpdateSince(sv).Length</c> (lo que viaja) contra <c>ExportState().Length</c>
/// (lo que viajaría sin sync incremental). El escenario se fijó antes de medir y el assert es el
/// umbral de la spec, no un número calibrado a posteriori.
/// </para>
/// <para>
/// Los client-ids son fijos (capacidad de <c>YrsEngine</c>, CHARTER-09/FU-012) para que el tamaño
/// medido no dependa del varint de un id aleatorio: un id de 53 bits ocupa varios bytes más que
/// uno pequeño, y eso es ruido que no pertenece a la medición.
/// </para>
/// </remarks>
public sealed class DeltaSizeBenchmark
{
    private readonly ITestOutputHelper _output;

    public DeltaSizeBenchmark(ITestOutputHelper output) => _output = output;

    private const ulong AutorClientId = 1;
    private const ulong ParClientId = 2;

    /// <summary>Documento de referencia: prosa, ~500 B de estado exportado.</summary>
    private const string ParrafoReferencia =
        "El telar levanta la urdimbre y la trama cruza entre los hilos tensados; cada pasada fija " +
        "el dibujo que ya no podrá deshacerse sin destejer lo anterior. Quien mira la tela " +
        "terminada no ve las decisiones intermedias, sino el patrón que sobrevivió a todas ellas. " +
        "Un documento colaborativo se teje igual: muchas manos empujan la lanzadera a la vez, y el " +
        "orden final no lo dicta quien llegó primero, sino la regla que todos aceptaron de " +
        "antemano.";

    /// <summary>La edición que ocurre mientras el par está desconectado.</summary>
    private const string EdicionDuranteDesconexion = "Nota al margen: ";

    [Fact]
    public void Reconnect_delta_is_at_least_90_pct_smaller_than_full_state()
    {
        using ICrdtDoc autor = YrsEngine.Instance.CreateDoc(AutorClientId);
        autor.InsertText("body", 0, ParrafoReferencia);

        // El par se pone al día y captura su SV justo antes de desconectarse.
        using ICrdtDoc par = YrsEngine.Instance.CreateDoc(ParClientId);
        par.ApplyUpdate(autor.ExportState());
        byte[] svAlDesconectar = par.ExportStateVector();

        // Mientras el par no está, el documento avanza.
        autor.InsertText("body", 0, EdicionDuranteDesconexion);

        // Reconexión: lo que viaja vs lo que viajaría reenviando el estado completo.
        byte[] delta = autor.ExportUpdateSince(svAlDesconectar);
        byte[] estadoCompleto = autor.ExportState();

        // Un delta que no converge no es un delta barato, es un delta roto: medir su tamaño sin
        // comprobar que sincroniza dejaría pasar un buffer vacío con una reducción del 100 %.
        par.ApplyUpdate(delta);
        Assert.Equal(autor.ExportState(), par.ExportState());
        Assert.Equal(EdicionDuranteDesconexion + ParrafoReferencia, par.GetText("body"));

        double reduccion = 1.0 - ((double)delta.Length / estadoCompleto.Length);

        // Un benchmark reporta su medición: el número es el entregable, no solo el verde/rojo.
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
