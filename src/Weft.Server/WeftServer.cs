using System.Collections.Concurrent;
using System.Net.WebSockets;
using Weft;
using Weft.Concurrency;
using Weft.Server.Auth;
using Weft.Server.Persistence;
using Weft.Server.Protocol;
using Weft.Versioning;
using Weft.Versioning.Blobs;

namespace Weft.Server;

/// <summary>Servicio de operación del relay (FR-018): publicar, contar conexiones, desconectar.</summary>
public interface IWeftServer
{
    /// <summary>
    /// Snapshot content-addressed de un documento activo. Ejecuta dentro del turno del actor del documento →
    /// mismo <see cref="VersionId"/> que produciría <c>VersionStore</c> en local para el mismo contenido
    /// (paridad, P-III). Requiere un <see cref="IBlobStore"/> registrado.
    /// </summary>
    ValueTask<VersionId> PublishAsync(string docId, CancellationToken ct = default);

    /// <summary>Número de conexiones activas de un documento (0 si no hay ninguna).</summary>
    ValueTask<int> GetConnectionCountAsync(string docId, CancellationToken ct = default);

    /// <summary>Cierra todas las conexiones de un documento (p. ej. tras revocación de acceso).</summary>
    ValueTask DisconnectAllAsync(string docId, CancellationToken ct = default);
}

/// <summary>
/// Implementación del relay: registro de <see cref="DocumentHub"/> por documento sobre un
/// <see cref="DocumentBroker"/> de M1. Singleton en el contenedor del consumidor. El broker se configura para
/// consolidar un snapshot en el store al desalojar (compaction), y la carga reconstruye el documento desde el
/// <see cref="IDocumentStore"/>.
/// </summary>
public sealed class WeftServer : IWeftServer, IAsyncDisposable
{
    private readonly WeftServerOptions _options;
    private readonly ICrdtEngine _engine;
    private readonly IDocumentStore _store;
    private readonly IBlobStore? _blobStore;
    private readonly DocumentBroker _broker;
    private readonly ConcurrentDictionary<string, DocumentHub> _hubs = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _hubGate = new(1, 1);
    private bool _disposed;

    /// <summary>Crea el relay. <paramref name="blobStore"/> es opcional: solo lo necesita <see cref="PublishAsync"/>.</summary>
    public WeftServer(WeftServerOptions options, IDocumentStore store, IBlobStore? blobStore = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(store);
        _options = options;
        _engine = options.Engine;
        _store = store;
        _blobStore = blobStore;

        // El desalojo del broker consolida un snapshot en el store (compaction), encadenando el hook del usuario.
        Func<string, byte[], CancellationToken, ValueTask>? userOnEvicting = options.Broker.OnEvicting;
        var brokerOptions = new DocumentBrokerOptions
        {
            IdleEviction = options.Broker.IdleEviction,
            MaxActiveDocuments = options.Broker.MaxActiveDocuments,
            IdleSweepInterval = options.Broker.IdleSweepInterval,
            OnEvicting = async (docId, state, ct) =>
            {
                if (userOnEvicting is not null)
                {
                    await userOnEvicting(docId, state, ct).ConfigureAwait(false);
                }

                await _store.SaveSnapshotAsync(docId, state, ct).ConfigureAwait(false);
            },
        };
        _broker = new DocumentBroker(_engine, brokerOptions);
    }

    // -- Endpoint: ciclo de vida de una conexión --

    internal async Task HandleConnectionAsync(string docId, WeftAccess access, WebSocket ws, CancellationToken ct)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var connection = new WeftConnection(ws, access, _options, ct);
        DocumentHub hub = await JoinAsync(docId, connection, ct).ConfigureAwait(false);
        try
        {
            await connection.RunAsync(hub, ct).ConfigureAwait(false);
        }
        finally
        {
            await LeaveAsync(hub, connection).ConfigureAwait(false);
        }
    }

    private async Task<DocumentHub> JoinAsync(string docId, WeftConnection connection, CancellationToken ct)
    {
        await _hubGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (!_hubs.TryGetValue(docId, out DocumentHub? hub))
            {
                DocumentSession session = await _broker.OpenAsync(docId, LoadDocStateAsync, ct).ConfigureAwait(false);
                hub = new DocumentHub(docId, session, _store, _options.Durability);
                _hubs[docId] = hub;
            }

            hub.Add(connection);
            return hub;
        }
        finally
        {
            _hubGate.Release();
        }
    }

    private async Task LeaveAsync(DocumentHub hub, WeftConnection connection)
    {
        // Retirada de awareness (FR-015): marcar offline los clientIDs que anunció esta conexión, a los pares.
        byte[]? removal = AwarenessProtocol.EncodeRemoval(connection.AwarenessClients);
        if (removal is not null)
        {
            hub.Broadcast(removal, exclude: connection);
        }

        try
        {
            await _hubGate.WaitAsync().ConfigureAwait(false);
        }
        catch (ObjectDisposedException)
        {
            return; // el servidor se está cerrando: DisposeAsync liberó el gate y ya desecha los hubs
        }

        try
        {
            hub.Remove(connection);
            if (hub.ConnectionCount == 0 && _hubs.TryRemove(hub.DocId, out _))
            {
                await hub.DisposeAsync().ConfigureAwait(false);
            }
        }
        finally
        {
            _hubGate.Release();
        }
    }

    // Reconstruye el blob de estado del documento desde el store (snapshot + updates enmarcados) para el broker.
    private async ValueTask<byte[]?> LoadDocStateAsync(string docId, CancellationToken ct)
    {
        byte[]? framed = await _store.LoadAsync(docId, ct).ConfigureAwait(false);
        if (framed is null)
        {
            return null; // documento nuevo
        }

        IReadOnlyList<byte[]> records = DocumentStateFraming.ReadRecords(framed);
        using ICrdtDoc doc = _engine.CreateDoc();
        foreach (byte[] record in records)
        {
            doc.ApplyUpdate(record);
        }

        return doc.ExportState();
    }

    // -- IWeftServer --

    /// <inheritdoc />
    public async ValueTask<VersionId> PublishAsync(string docId, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrEmpty(docId);
        if (_blobStore is null)
        {
            throw new InvalidOperationException(
                "PublishAsync requiere un IBlobStore registrado en el contenedor del consumidor.");
        }

        await using DocumentSession session = await _broker.OpenAsync(docId, LoadDocStateAsync, ct)
            .ConfigureAwait(false);

        // Snapshot dentro del turno del actor: ExportState es la MISMA operación determinista (P-III) que usa
        // VersionStore.PublishAsync en local; FromBlob(ExportState) reproduce el VersionId local byte a byte.
        byte[] snapshot = await session.ExecuteAsync(static doc => doc.ExportState(), ct).ConfigureAwait(false);
        var id = VersionId.FromBlob(snapshot);
        await _blobStore.PutAsync(id, snapshot, ct).ConfigureAwait(false);
        return id;
    }

    /// <inheritdoc />
    public ValueTask<int> GetConnectionCountAsync(string docId, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrEmpty(docId);
        int count = _hubs.TryGetValue(docId, out DocumentHub? hub) ? hub.ConnectionCount : 0;
        return ValueTask.FromResult(count);
    }

    /// <inheritdoc />
    public ValueTask DisconnectAllAsync(string docId, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrEmpty(docId);
        if (_hubs.TryGetValue(docId, out DocumentHub? hub))
        {
            hub.DisconnectAll();
        }

        return ValueTask.CompletedTask;
    }

    /// <summary>Cierra el relay: descarta los hubs vivos y drena el broker.</summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        foreach (DocumentHub hub in _hubs.Values)
        {
            hub.DisconnectAll();
            await hub.DisposeAsync().ConfigureAwait(false);
        }

        _hubs.Clear();
        await _broker.DisposeAsync().ConfigureAwait(false);
        _hubGate.Dispose();
    }
}
