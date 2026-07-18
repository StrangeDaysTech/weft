using Weft;
using Weft.Concurrency;
using Weft.Server.Protocol;
using Weft.Yrs;

namespace Weft.Server;

/// <summary>
/// Configuración del relay (<see cref="WeftServerExtensions.AddWeftServer"/>). El motor y el ciclo de vida del
/// broker tienen defaults sensatos; los límites por conexión completan la mitigación FU-002 (parte b) sobre el
/// cap de tamaño de mensaje del framing (parte a).
/// </summary>
public sealed class WeftServerOptions
{
    /// <summary>Motor CRDT que respalda los documentos del relay. Por defecto <see cref="YrsEngine.Instance"/>.</summary>
    public ICrdtEngine Engine { get; set; } = YrsEngine.Instance;

    /// <summary>Ciclo de vida del <see cref="DocumentBroker"/> (idle eviction, LRU, cadencia del barrido).</summary>
    public DocumentBrokerOptions Broker { get; set; } = new();

    /// <summary>
    /// Cap de tamaño de un frame WebSocket entrante (FU-002 parte a). Frames por encima se rechazan antes del
    /// decoder. Por defecto <see cref="Lib0Encoding.DefaultMaxMessageBytes"/> (16 MiB).
    /// </summary>
    public int MaxMessageBytes { get; set; } = Lib0Encoding.DefaultMaxMessageBytes;

    /// <summary>
    /// Cota de la cola de envío por conexión (FU-002 parte b, backpressure). Si un consumidor lento no drena su
    /// cola y esta se llena, la conexión se cierra (se descarta el consumidor lento en vez de crecer memoria sin
    /// límite); el cliente reconecta y re-sincroniza. Por defecto 256 mensajes.
    /// </summary>
    public int MaxSendQueuePerConnection { get; set; } = 256;

    /// <summary>
    /// Orden entre persistir un update y difundirlo a los pares (FU-010). Por defecto
    /// <see cref="DurabilityMode.PersistThenBroadcast"/>: ningún par observa un update que el store no haya
    /// aceptado. Ver <see cref="DurabilityMode"/> para el trade-off con la latencia de broadcast.
    /// </summary>
    public DurabilityMode Durability { get; set; } = DurabilityMode.PersistThenBroadcast;
}

/// <summary>Orden entre persistir un update en el <see cref="Persistence.IDocumentStore"/> y difundirlo (FU-010).</summary>
public enum DurabilityMode
{
    /// <summary>
    /// Persistir ANTES de difundir (default). Garantiza que ningún par observa estado que el servidor no haya
    /// aceptado de forma durable. Un fallo del append cierra las conexiones del documento (1011): reconectan y el
    /// servidor autoritativo reenvía el estado. El coste es que el broadcast se retrasa la latencia del append (que
    /// el emisor ya paga hoy en el receive loop). Nota: y-protocols no tiene ack de aplicación para <c>Update</c>,
    /// así que el emisor nunca sabe si su update se persistió — la garantía es sobre lo que OBSERVAN los pares.
    /// </summary>
    PersistThenBroadcast,

    /// <summary>
    /// Difundir ANTES de persistir (comportamiento heredado, CHARTER-05). Menor latencia de broadcast a costa de
    /// una ventana en la que los pares tienen un update que el store aún no aceptó; en single-node la auto-sanación
    /// CRDT lo recupera en la reconexión. Válvula de escape para deployments sensibles a la latencia sobre un store
    /// rápido o en memoria.
    /// </summary>
    BroadcastThenPersist,
}
