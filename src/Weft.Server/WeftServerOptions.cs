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
}
