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

/// <summary>Relay operation service (FR-018): publish, count connections, disconnect.</summary>
public interface IWeftServer
{
    /// <summary>
    /// Content-addressed snapshot of an active document. Runs inside the document's actor turn →
    /// same <see cref="VersionId"/> that <c>VersionStore</c> would produce locally for the same content
    /// (parity, P-III). Requires a registered <see cref="IBlobStore"/>.
    /// </summary>
    ValueTask<VersionId> PublishAsync(string docId, CancellationToken ct = default);

    /// <summary>Number of active connections of a document (0 if there are none).</summary>
    ValueTask<int> GetConnectionCountAsync(string docId, CancellationToken ct = default);

    /// <summary>Closes all connections of a document (e.g. after access revocation).</summary>
    ValueTask DisconnectAllAsync(string docId, CancellationToken ct = default);
}

/// <summary>
/// Relay implementation: a registry of <see cref="DocumentHub"/> per document over an M1
/// <see cref="DocumentBroker"/>. Singleton in the consumer's container. The broker is configured to
/// consolidate a snapshot into the store on eviction (compaction), and loading reconstructs the document from the
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

    /// <summary>Creates the relay. <paramref name="blobStore"/> is optional: only <see cref="PublishAsync"/> needs it.</summary>
    public WeftServer(WeftServerOptions options, IDocumentStore store, IBlobStore? blobStore = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(store);
        _options = options;
        _engine = options.Engine;
        _store = store;
        _blobStore = blobStore;

        // The broker's eviction consolidates a snapshot into the store (compaction), chaining the user's hook.
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

    // -- Endpoint: lifecycle of a connection --

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
        // Awareness removal (FR-015): mark offline, to the peers, the clientIDs this connection announced.
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
            return; // the server is shutting down: DisposeAsync released the gate and already disposes the hubs
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

    // Reconstructs the document's state blob from the store (snapshot + framed updates) for the broker.
    private async ValueTask<byte[]?> LoadDocStateAsync(string docId, CancellationToken ct)
    {
        byte[]? framed = await _store.LoadAsync(docId, ct).ConfigureAwait(false);
        if (framed is null)
        {
            return null; // new document
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
                "PublishAsync requires an IBlobStore registered in the consumer's container.");
        }

        await using DocumentSession session = await _broker.OpenAsync(docId, LoadDocStateAsync, ct)
            .ConfigureAwait(false);

        // Snapshot within the actor turn: ExportState is the SAME deterministic operation (P-III) that
        // VersionStore.PublishAsync uses locally; FromBlob(ExportState) reproduces the local VersionId byte for byte.
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

    /// <summary>Closes the relay: disposes the live hubs and drains the broker.</summary>
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
