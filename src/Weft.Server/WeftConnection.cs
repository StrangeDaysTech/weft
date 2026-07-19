using System.Buffers;
using System.Net.WebSockets;
using System.Threading.Channels;
using Weft.Server.Auth;
using Weft.Server.Protocol;

namespace Weft.Server;

/// <summary>
/// A client's WebSocket connection to a document. Runs a <b>send pump</b> (drains a bounded send queue
/// → the socket) and a <b>receive loop</b> (decodes y-sync frames, applies the authorization enforcement
/// and the per-connection limits, and dispatches sync/awareness). Isolated: a failure of this connection does not
/// affect the peers (the hub's broadcast isolates each send).
/// </summary>
internal sealed class WeftConnection
{
    private readonly WebSocket _ws;
    private readonly WeftServerOptions _options;
    private readonly Channel<byte[]> _sendQueue;
    private readonly CancellationTokenSource _cts;
    private readonly Dictionary<uint, uint> _awarenessClients = new();

    public WeftConnection(WebSocket ws, WeftAccess access, WeftServerOptions options, CancellationToken hostCt)
    {
        _ws = ws;
        Access = access;
        _options = options;
        _cts = CancellationTokenSource.CreateLinkedTokenSource(hostCt);
        _sendQueue = Channel.CreateBounded<byte[]>(new BoundedChannelOptions(options.MaxSendQueuePerConnection)
        {
            SingleReader = true,
            FullMode = BoundedChannelFullMode.Wait, // we use TryWrite: 'full' ⇒ the connection is closed (backpressure)
        });
    }

    /// <summary>Access level granted in the handshake.</summary>
    public WeftAccess Access { get; }

    /// <summary>Awareness clientIDs announced by this connection (for the removal on close, FR-015).</summary>
    public IReadOnlyDictionary<uint, uint> AwarenessClients => _awarenessClients;

    /// <summary>Requests the close of the connection (e.g. from <c>DisconnectAllAsync</c>). Non-blocking.</summary>
    public void RequestClose() => _cts.Cancel();

    /// <summary>
    /// Enqueues a frame for sending. Non-blocking (called from the actor turn during the broadcast). If the
    /// queue is full (slow consumer, FU-002 part b), returns <c>false</c> and the connection is closed.
    /// </summary>
    public bool TryEnqueue(byte[] frame)
    {
        if (_sendQueue.Writer.TryWrite(frame))
        {
            return true;
        }

        // Backpressure: drop the slow consumer instead of growing memory; it will reconnect and re-sync.
        _cts.Cancel();
        return false;
    }

    /// <summary>
    /// Runs the connection until it closes: starts the send pump, sends the initial <c>SyncStep1</c> and drains the
    /// receive loop. On finishing, completes the send queue.
    /// </summary>
    public async Task RunAsync(DocumentHub hub, CancellationToken ct)
    {
        using CancellationTokenRegistration _ = ct.Register(static s => ((CancellationTokenSource)s!).Cancel(), _cts);
        Task pump = Task.Run(() => SendPumpAsync(_cts.Token));

        try
        {
            // Initial sync (server→client): "here's what I know". The client responds with its SyncStep1, which the
            // receive loop answers with SyncStep2 (delta) — incremental sync in both directions.
            byte[] serverSv = await hub.Session.ExportStateVectorAsync(_cts.Token).ConfigureAwait(false);
            TryEnqueue(SyncProtocol.EncodeSyncStep1(serverSv));

            await ReceiveLoopAsync(hub, _cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { /* normal close */ }
        catch (WebSocketException) { /* socket cut by the peer: normal close */ }
        finally
        {
            _sendQueue.Writer.TryComplete();
            try { await pump.ConfigureAwait(false); } catch { /* pump already closing */ }
        }
    }

    private async Task ReceiveLoopAsync(DocumentHub hub, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _ws.State == WebSocketState.Open)
        {
            (WebSocketMessageType type, byte[]? data) = await ReceiveMessageAsync(ct).ConfigureAwait(false);
            if (type == WebSocketMessageType.Close || data is null)
            {
                return;
            }

            if (type != WebSocketMessageType.Binary)
            {
                continue; // the y-sync protocol is binary; text/ping is ignored
            }

            if (!await DispatchAsync(hub, data, ct).ConfigureAwait(false))
            {
                return; // dispatch requested close (malformed 1002 / read-only 1008)
            }
        }
    }

    // Applies+persists+broadcasts an update; if PERSISTENCE fails, closes this connection with 1011. In
    // persist-before-broadcast the hub already closed all the document's connections (DisconnectAll) to
    // force the authoritative re-sync; here the failure is only translated into a clean close. Cancellation and
    // a cut socket are allowed to propagate (RunAsync handles them as a normal close).
    private async Task<bool> ApplyOrCloseAsync(DocumentHub hub, byte[] payload, CancellationToken ct)
    {
        try
        {
            await hub.ApplyAndPersistAsync(payload, ct).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (WebSocketException)
        {
            throw;
        }
        catch
        {
            await CloseAsync(WebSocketCloseStatus.InternalServerError, "persist failed", ct) // 1011
                .ConfigureAwait(false);
            return false;
        }
    }

    private async Task<bool> DispatchAsync(DocumentHub hub, byte[] frame, CancellationToken ct)
    {
        // SyncMessage is a ref struct (span over the frame): type+payload are extracted to locals BEFORE await.
        MessageType type;
        SyncMessageType syncType;
        byte[] payload;
        try
        {
            SyncMessage m = SyncProtocol.Decode(frame, _options.MaxMessageBytes);
            type = m.Type;
            syncType = m.SyncType;
            payload = m.Payload.ToArray();
        }
        catch (MalformedMessageException)
        {
            await CloseAsync(WebSocketCloseStatus.ProtocolError, "malformed message", ct).ConfigureAwait(false); // 1002
            return false;
        }

        switch (type)
        {
            case MessageType.Sync when syncType == SyncMessageType.Step1:
                // The client announces its state vector → respond with the delta it is missing.
                byte[] delta = await hub.Session.ExportUpdateSinceAsync(payload, ct).ConfigureAwait(false);
                TryEnqueue(SyncProtocol.EncodeSyncStep2(delta));
                return true;

            case MessageType.Sync when syncType == SyncMessageType.Step2:
                // SyncStep2 = the handshake's response to the server's SyncStep1 (part of the y-sync protocol, not
                // a deliberate edit). A ReadWrite connection applies it (contributes its initial state); a
                // ReadOnly one IGNORES it (contributes no state, but is NOT closed: closing here would break the
                // standard y-websocket handshake and leave ReadOnly unusable with Yjs clients — the write
                // enforcement is only for live Update, FR-019).
                if (Access == WeftAccess.ReadWrite)
                {
                    return await ApplyOrCloseAsync(hub, payload, ct).ConfigureAwait(false);
                }

                return true;

            case MessageType.Sync: // Update (sub-type 2): live document edit.
                if (Access != WeftAccess.ReadWrite)
                {
                    await CloseAsync(WebSocketCloseStatus.PolicyViolation, "read-only connection", ct) // 1008
                        .ConfigureAwait(false);
                    return false;
                }

                return await ApplyOrCloseAsync(hub, payload, ct).ConfigureAwait(false);

            case MessageType.Awareness:
                AwarenessProtocol.TrackClients(payload, _awarenessClients);
                hub.Broadcast(SyncProtocol.EncodeAwareness(payload), exclude: this); // ephemeral, to the peers, not persisted
                return true;

            default:
                return true;
        }
    }

    private async Task SendPumpAsync(CancellationToken ct)
    {
        try
        {
            await foreach (byte[] frame in _sendQueue.Reader.ReadAllAsync(ct).ConfigureAwait(false))
            {
                await _ws.SendAsync(frame, WebSocketMessageType.Binary, endOfMessage: true, ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) { /* close */ }
        catch (WebSocketException) { _cts.Cancel(); }
    }

    private async Task<(WebSocketMessageType, byte[]?)> ReceiveMessageAsync(CancellationToken ct)
    {
        byte[] rent = ArrayPool<byte>.Shared.Rent(8192);
        var acc = new ArrayBufferWriter<byte>();
        try
        {
            while (true)
            {
                WebSocketReceiveResult r;
                try
                {
                    r = await _ws.ReceiveAsync(rent, ct).ConfigureAwait(false);
                }
                catch (WebSocketException)
                {
                    return (WebSocketMessageType.Close, null);
                }

                if (r.MessageType == WebSocketMessageType.Close)
                {
                    return (WebSocketMessageType.Close, null);
                }

                acc.Write(rent.AsSpan(0, r.Count));
                if (acc.WrittenCount > _options.MaxMessageBytes)
                {
                    // Oversized frame (FU-002 part a): close before accumulating more.
                    await CloseAsync(WebSocketCloseStatus.MessageTooBig, "message too large", ct).ConfigureAwait(false); // 1009
                    return (WebSocketMessageType.Close, null);
                }

                if (r.EndOfMessage)
                {
                    return (r.MessageType, acc.WrittenSpan.ToArray());
                }
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rent);
        }
    }

    private async Task CloseAsync(WebSocketCloseStatus status, string description, CancellationToken ct)
    {
        try
        {
            if (_ws.State == WebSocketState.Open)
            {
                await _ws.CloseAsync(status, description, ct).ConfigureAwait(false);
            }
        }
        catch (WebSocketException) { /* already closed */ }
        catch (OperationCanceledException) { /* shutting down */ }
        finally
        {
            _cts.Cancel();
        }
    }
}
