using Weft;
using Weft.Concurrency;
using Weft.Server.Protocol;
using Weft.Yrs;

namespace Weft.Server;

/// <summary>
/// Relay configuration (<see cref="WeftServerExtensions.AddWeftServer"/>). The engine and the broker lifecycle
/// have sensible defaults; the per-connection limits complete the FU-002 mitigation (part b) on top of the
/// framing's message-size cap (part a).
/// </summary>
public sealed class WeftServerOptions
{
    /// <summary>CRDT engine backing the relay's documents. Defaults to <see cref="YrsEngine.Instance"/>.</summary>
    public ICrdtEngine Engine { get; set; } = YrsEngine.Instance;

    /// <summary>Lifecycle of the <see cref="DocumentBroker"/> (idle eviction, LRU, sweep cadence).</summary>
    public DocumentBrokerOptions Broker { get; set; } = new();

    /// <summary>
    /// Size cap of an incoming WebSocket frame (FU-002 part a). Frames above it are rejected before the
    /// decoder. Defaults to <see cref="Lib0Encoding.DefaultMaxMessageBytes"/> (16 MiB).
    /// </summary>
    public int MaxMessageBytes { get; set; } = Lib0Encoding.DefaultMaxMessageBytes;

    /// <summary>
    /// Bound on the per-connection send queue (FU-002 part b, backpressure). If a slow consumer does not drain
    /// its queue and it fills up, the connection is closed (the slow consumer is dropped instead of letting
    /// memory grow unbounded); the client reconnects and re-syncs. Defaults to 256 messages.
    /// </summary>
    public int MaxSendQueuePerConnection { get; set; } = 256;

    /// <summary>
    /// Ordering between persisting an update and broadcasting it to peers (FU-010). Defaults to
    /// <see cref="DurabilityMode.PersistThenBroadcast"/>: no peer observes an update that the store has not
    /// accepted. See <see cref="DurabilityMode"/> for the trade-off with broadcast latency.
    /// </summary>
    public DurabilityMode Durability { get; set; } = DurabilityMode.PersistThenBroadcast;
}

/// <summary>Ordering between persisting an update in the <see cref="Persistence.IDocumentStore"/> and broadcasting it (FU-010).</summary>
public enum DurabilityMode
{
    /// <summary>
    /// Persist BEFORE broadcasting (default). Guarantees no peer observes state the server has not durably
    /// accepted. An append failure closes the document's connections (1011): they reconnect and the authoritative
    /// server resends the state. The cost is that the broadcast is delayed by the append latency (which the sender
    /// already pays today in the receive loop). Note: y-protocols has no application-level ack for <c>Update</c>,
    /// so the sender never knows whether its update was persisted — the guarantee is about what peers OBSERVE.
    /// </summary>
    PersistThenBroadcast,

    /// <summary>
    /// Broadcast BEFORE persisting (legacy behavior, CHARTER-05). Lower broadcast latency at the cost of a
    /// window in which peers hold an update the store has not yet accepted; on single-node the CRDT
    /// self-healing recovers it on reconnection. Escape valve for latency-sensitive deployments over a fast or
    /// in-memory store.
    /// </summary>
    BroadcastThenPersist,
}
