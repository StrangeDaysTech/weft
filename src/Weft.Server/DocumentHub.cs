using System.Collections.Concurrent;
using Weft.Concurrency;
using Weft.Server.Persistence;
using Weft.Server.Protocol;

namespace Weft.Server;

/// <summary>
/// Punto de encuentro de todas las conexiones de un mismo documento. Mantiene <b>una</b>
/// <see cref="DocumentSession"/> por documento (anclaje M1: el broadcast vía
/// <see cref="DocumentSession.UpdateApplied"/> es perezoso, se suscribe una sola vez y el refcount de sesiones
/// mantiene el documento residente mientras haya conexiones). Difunde cada update aplicado y persiste el flujo.
/// </summary>
internal sealed class DocumentHub : IAsyncDisposable
{
    private readonly IDocumentStore _store;
    private readonly ConcurrentDictionary<WeftConnection, byte> _connections = new();
    private int _disposed;

    public DocumentHub(string docId, DocumentSession session, IDocumentStore store)
    {
        DocId = docId;
        Session = session;
        _store = store;
        Session.UpdateApplied += OnUpdateApplied;
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
    /// Aplica un update entrante al documento (turno del actor) y lo persiste. La aplicación dispara
    /// <see cref="OnUpdateApplied"/>, que difunde el delta a las conexiones.
    /// </summary>
    public async ValueTask ApplyAndPersistAsync(byte[] update, CancellationToken ct)
    {
        await Session.ApplyUpdateAsync(update, ct).ConfigureAwait(false);
        await _store.AppendUpdateAsync(DocId, update, ct).ConfigureAwait(false);
    }

    // El delta se difunde a TODAS las conexiones del documento. Reaplicar su propio delta en el origen es un
    // no-op CRDT idempotente (los clientes Yjs lo toleran), lo que evita rastrear el origen dentro del turno del
    // actor (que sería una carrera). El coste es un eco al emisor; aceptable para v1.
    private void OnUpdateApplied(DocumentSession _, ReadOnlyMemory<byte> delta)
    {
        if (delta.IsEmpty)
        {
            return;
        }

        Broadcast(SyncProtocol.EncodeUpdate(delta.Span), exclude: null);
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

        Session.UpdateApplied -= OnUpdateApplied;

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
