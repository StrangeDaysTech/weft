using System.Buffers;
using System.Net.WebSockets;
using System.Threading.Channels;
using Weft.Server.Auth;
using Weft.Server.Protocol;

namespace Weft.Server;

/// <summary>
/// Una conexión WebSocket de un cliente a un documento. Corre un <b>send pump</b> (drena una cola de envío
/// acotada → el socket) y un <b>receive loop</b> (decodifica frames y-sync, aplica el enforcement de
/// autorización y los límites por conexión, y despacha sync/awareness). Aislada: un fallo de esta conexión no
/// afecta a los pares (el broadcast del hub aísla cada envío).
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
            FullMode = BoundedChannelFullMode.Wait, // usamos TryWrite: 'lleno' ⇒ se cierra la conexión (backpressure)
        });
    }

    /// <summary>Nivel de acceso concedido en el handshake.</summary>
    public WeftAccess Access { get; }

    /// <summary>clientIDs de awareness anunciados por esta conexión (para la retirada al cerrar, FR-015).</summary>
    public IReadOnlyDictionary<uint, uint> AwarenessClients => _awarenessClients;

    /// <summary>Pide el cierre de la conexión (p. ej. desde <c>DisconnectAllAsync</c>). No bloquea.</summary>
    public void RequestClose() => _cts.Cancel();

    /// <summary>
    /// Encola un frame para envío. No bloquea (se llama desde el turno del actor durante el broadcast). Si la
    /// cola está llena (consumidor lento, FU-002 parte b), devuelve <c>false</c> y la conexión se cierra.
    /// </summary>
    public bool TryEnqueue(byte[] frame)
    {
        if (_sendQueue.Writer.TryWrite(frame))
        {
            return true;
        }

        // Backpressure: descartar el consumidor lento en vez de crecer memoria; reconectará y re-sincronizará.
        _cts.Cancel();
        return false;
    }

    /// <summary>
    /// Corre la conexión hasta que cierra: arranca el send pump, envía el <c>SyncStep1</c> inicial y drena el
    /// receive loop. Al terminar, completa la cola de envío.
    /// </summary>
    public async Task RunAsync(DocumentHub hub, CancellationToken ct)
    {
        using CancellationTokenRegistration _ = ct.Register(static s => ((CancellationTokenSource)s!).Cancel(), _cts);
        Task pump = Task.Run(() => SendPumpAsync(_cts.Token));

        try
        {
            // Sync inicial (servidor→cliente): "esto conozco". El cliente responde su SyncStep1, que el receive
            // loop contesta con SyncStep2 (delta) — sync incremental en ambas direcciones.
            byte[] serverSv = await hub.Session.ExportStateVectorAsync(_cts.Token).ConfigureAwait(false);
            TryEnqueue(SyncProtocol.EncodeSyncStep1(serverSv));

            await ReceiveLoopAsync(hub, _cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { /* cierre normal */ }
        catch (WebSocketException) { /* socket cortado por el par: cierre normal */ }
        finally
        {
            _sendQueue.Writer.TryComplete();
            try { await pump.ConfigureAwait(false); } catch { /* pump ya en cierre */ }
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
                continue; // el protocolo y-sync es binario; se ignora texto/ping
            }

            if (!await DispatchAsync(hub, data, ct).ConfigureAwait(false))
            {
                return; // dispatch pidió cerrar (malformado 1002 / read-only 1008)
            }
        }
    }

    private async Task<bool> DispatchAsync(DocumentHub hub, byte[] frame, CancellationToken ct)
    {
        // SyncMessage es un ref struct (span sobre el frame): se extraen tipo+payload a locales ANTES de await.
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
                // El cliente anuncia su state vector → responder el delta que le falta.
                byte[] delta = await hub.Session.ExportUpdateSinceAsync(payload, ct).ConfigureAwait(false);
                TryEnqueue(SyncProtocol.EncodeSyncStep2(delta));
                return true;

            case MessageType.Sync when syncType == SyncMessageType.Step2:
                // SyncStep2 = respuesta del handshake al SyncStep1 del servidor (parte del protocolo y-sync, no
                // una edición deliberada). Una conexión ReadWrite lo aplica (aporta su estado inicial); una
                // ReadOnly lo IGNORA (no contribuye estado, pero NO se cierra: cerrar aquí rompería el handshake
                // y-websocket estándar y dejaría ReadOnly inusable con clientes Yjs — el enforcement de escritura
                // es solo para Update en vivo, FR-019).
                if (Access == WeftAccess.ReadWrite)
                {
                    await hub.ApplyAndPersistAsync(payload, ct).ConfigureAwait(false);
                }

                return true;

            case MessageType.Sync: // Update (subtipo 2): edición de documento en vivo.
                if (Access != WeftAccess.ReadWrite)
                {
                    await CloseAsync(WebSocketCloseStatus.PolicyViolation, "read-only connection", ct) // 1008
                        .ConfigureAwait(false);
                    return false;
                }

                await hub.ApplyAndPersistAsync(payload, ct).ConfigureAwait(false); // dispara UpdateApplied → broadcast
                return true;

            case MessageType.Awareness:
                AwarenessProtocol.TrackClients(payload, _awarenessClients);
                hub.Broadcast(SyncProtocol.EncodeAwareness(payload), exclude: this); // efímero, a los pares, sin persistir
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
        catch (OperationCanceledException) { /* cierre */ }
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
                    // Frame sobredimensionado (FU-002 parte a): cerrar antes de acumular más.
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
        catch (WebSocketException) { /* ya cerrado */ }
        catch (OperationCanceledException) { /* apagando */ }
        finally
        {
            _cts.Cancel();
        }
    }
}
