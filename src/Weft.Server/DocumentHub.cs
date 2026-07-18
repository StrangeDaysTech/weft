using System.Collections.Concurrent;
using Weft.Concurrency;
using Weft.Server.Persistence;
using Weft.Server.Protocol;

namespace Weft.Server;

/// <summary>
/// Punto de encuentro de todas las conexiones de un mismo documento. Mantiene <b>una</b>
/// <see cref="DocumentSession"/> por documento (el refcount de sesiones mantiene el documento residente
/// mientras haya conexiones). Aplica cada update, lo persiste y difunde su delta; el orden entre persistir y
/// difundir lo fija <see cref="WeftServerOptions.Durability"/> (FU-010).
/// </summary>
internal sealed class DocumentHub : IAsyncDisposable
{
    private readonly IDocumentStore _store;
    private readonly DurabilityMode _durability;
    private readonly ConcurrentDictionary<WeftConnection, byte> _connections = new();
    private int _disposed;

    public DocumentHub(string docId, DocumentSession session, IDocumentStore store, DurabilityMode durability)
    {
        DocId = docId;
        Session = session;
        _store = store;
        _durability = durability;
        // El broadcast es EXPLÍCITO (ApplyAndPersistAsync), no vía el evento UpdateApplied: capturar el
        // delta como valor de retorno del turno permite ordenar append/broadcast según el modo y es
        // race-free entre conexiones concurrentes del mismo documento. El evento se conserva para otros
        // consumidores de DocumentSession, pero el relay ya no depende de él.
    }

    /// <summary>Identificador del documento.</summary>
    public string DocId { get; }

    /// <summary>Sesión async compartida del documento (turno del actor serializado, P-V).</summary>
    public DocumentSession Session { get; }

    /// <summary>Conexiones activas del documento.</summary>
    public int ConnectionCount => _connections.Count;

    public void Add(WeftConnection connection) => _connections.TryAdd(connection, 0);

    public void Remove(WeftConnection connection) => _connections.TryRemove(connection, out _);

    /// <summary>Pide el cierre de todas las conexiones del documento (su teardown las retira del hub).</summary>
    public void DisconnectAll()
    {
        foreach (WeftConnection c in _connections.Keys)
        {
            c.RequestClose();
        }
    }

    /// <summary>
    /// Difunde un frame a las conexiones del documento, opcionalmente excluyendo el origen. Cada envío se aísla:
    /// un fallo/backpressure de una conexión (que la cierra) no afecta a los pares.
    /// </summary>
    public void Broadcast(byte[] frame, WeftConnection? exclude)
    {
        foreach (WeftConnection c in _connections.Keys)
        {
            if (!ReferenceEquals(c, exclude))
            {
                c.TryEnqueue(frame);
            }
        }
    }

    /// <summary>
    /// Aplica un update entrante al documento (turno del actor), lo persiste y difunde su delta. El orden
    /// entre persistir y difundir lo fija <see cref="WeftServerOptions.Durability"/> (FU-010).
    /// </summary>
    /// <remarks>
    /// En <see cref="DurabilityMode.PersistThenBroadcast"/>, si el append falla, el update ya está en el
    /// documento vivo pero NUNCA se difundió: los pares quedarían callados y desactualizados para siempre
    /// (la próxima edición difunde solo su delta, no el que faltó). Por eso un fallo de append cierra
    /// TODAS las conexiones del documento (el llamador las cierra con 1011): reconectan, mandan SyncStep1
    /// y el servidor —autoritativo, que sí tiene el update— reenvía el estado. Convergencia recuperada.
    /// </remarks>
    public async ValueTask ApplyAndPersistAsync(byte[] update, CancellationToken ct)
    {
        byte[] delta = await Session.ApplyAndCaptureDeltaAsync(update, ct).ConfigureAwait(false);

        if (_durability == DurabilityMode.BroadcastThenPersist)
        {
            BroadcastDelta(delta);
            await _store.AppendUpdateAsync(DocId, update, ct).ConfigureAwait(false);
            return;
        }

        // PersistThenBroadcast (default): persistir antes de que ningún par lo vea.
        try
        {
            await _store.AppendUpdateAsync(DocId, update, ct).ConfigureAwait(false);
        }
        catch
        {
            // El update quedó aplicado pero sin difundir: cerrar el documento entero fuerza el re-sync
            // autoritativo. Relanzar deja que el llamador cierre esta conexión con 1011.
            DisconnectAll();
            throw;
        }

        BroadcastDelta(delta);
    }

    // El delta se difunde a TODAS las conexiones del documento. Reaplicar su propio delta en el origen es un
    // no-op CRDT idempotente (los clientes Yjs lo toleran), lo que evita rastrear el origen dentro del turno del
    // actor (que sería una carrera). El coste es un eco al emisor; aceptable para v1.
    private void BroadcastDelta(byte[] delta)
    {
        if (delta.Length == 0)
        {
            return;
        }

        Broadcast(SyncProtocol.EncodeUpdate(delta), exclude: null);
    }

    /// <summary>
    /// Cierra el hub: desuscribe el broadcast, consolida un snapshot (compaction) y libera la sesión (lo que
    /// permite al broker desalojar el documento por inactividad). Idempotente.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        try
        {
            // Snapshot de consolidación: el estado completo dentro del turno del actor reemplaza los updates
            // acumulados en el store (compaction). Best-effort: un fallo no debe romper el cierre de la sesión.
            byte[] snapshot = await Session.ExportStateAsync().ConfigureAwait(false);
            await _store.SaveSnapshotAsync(DocId, snapshot).ConfigureAwait(false);
        }
        catch
        {
            // El desalojo del broker (OnEvicting) también persiste; la durabilidad no depende solo de aquí.
        }
        finally
        {
            await Session.DisposeAsync().ConfigureAwait(false);
        }
    }
}
