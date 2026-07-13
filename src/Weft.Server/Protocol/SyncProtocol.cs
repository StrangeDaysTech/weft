namespace Weft.Server.Protocol;

/// <summary>Tipo de mensaje de wire de primer nivel (<c>y-protocols</c>).</summary>
public enum MessageType
{
    /// <summary>Sincronización de documento (sub-tipado por <see cref="SyncMessageType"/>).</summary>
    Sync = 0,

    /// <summary>Estado efímero por cliente (<c>y-awareness</c>); nunca se persiste.</summary>
    Awareness = 1,
}

/// <summary>Sub-tipo de un mensaje <see cref="MessageType.Sync"/> (protocolo <c>y-sync</c>).</summary>
public enum SyncMessageType
{
    /// <summary>SyncStep1: "esto conozco" — lleva el state vector del emisor.</summary>
    Step1 = 0,

    /// <summary>SyncStep2: respuesta con el delta (update) que le falta al emisor del state vector.</summary>
    Step2 = 1,

    /// <summary>Update incremental en vivo.</summary>
    Update = 2,
}

/// <summary>
/// Un mensaje y-sync decodificado. <c>ref struct</c>: <see cref="Payload"/> es una vista zero-copy del frame
/// de origen (el state vector para <see cref="SyncMessageType.Step1"/>; el update para
/// <see cref="SyncMessageType.Step2"/>/<see cref="SyncMessageType.Update"/>; el update de awareness para
/// <see cref="MessageType.Awareness"/>). Los bytes de payload son <b>opacos</b> para esta capa: se entregan
/// tal cual al broker/decoder de yrs aguas arriba (CHARTER-05).
/// </summary>
public readonly ref struct SyncMessage
{
    internal SyncMessage(MessageType type, SyncMessageType syncType, ReadOnlySpan<byte> payload)
    {
        Type = type;
        SyncType = syncType;
        Payload = payload;
    }

    /// <summary>Tipo de primer nivel.</summary>
    public MessageType Type { get; }

    /// <summary>Sub-tipo; solo significativo cuando <see cref="Type"/> es <see cref="MessageType.Sync"/>.</summary>
    public SyncMessageType SyncType { get; }

    /// <summary>Payload opaco (state vector / update / awareness).</summary>
    public ReadOnlySpan<byte> Payload { get; }
}

/// <summary>
/// Framing <c>y-sync</c> sobre el encoding lib0 (<see cref="Lib0Encoding"/>), compatible con clientes Yjs
/// estándar. Estructura del wire (todo entero es varint):
/// <code>
/// SYNC      := 0 &lt;syncType&gt; &lt;VarUint8Array payload&gt;
/// AWARENESS := 1 &lt;VarUint8Array payload&gt;
/// </code>
/// </summary>
/// <remarks>
/// El decoder aplica el cap de tamaño de frame (FU-002 parte a) <b>antes</b> de parsear y rechaza tipos
/// desconocidos, varints truncados/sobredimensionados y bytes sobrantes con
/// <see cref="MalformedMessageException"/>. No interpreta el payload: eso es responsabilidad del relay/decoder
/// yrs (CHARTER-05). Esta capa es puro transporte.
/// </remarks>
public static class SyncProtocol
{
    /// <summary>Codifica <c>SyncStep1(stateVector)</c> — lo envía quien se conecta.</summary>
    public static byte[] EncodeSyncStep1(ReadOnlySpan<byte> stateVector) =>
        EncodeSync(SyncMessageType.Step1, stateVector);

    /// <summary>Codifica <c>SyncStep2(update)</c> — el delta que responde a un SyncStep1.</summary>
    public static byte[] EncodeSyncStep2(ReadOnlySpan<byte> update) =>
        EncodeSync(SyncMessageType.Step2, update);

    /// <summary>Codifica <c>Update(update)</c> — un update incremental en vivo.</summary>
    public static byte[] EncodeUpdate(ReadOnlySpan<byte> update) =>
        EncodeSync(SyncMessageType.Update, update);

    /// <summary>Codifica un mensaje <c>AWARENESS(update)</c> (estado efímero por cliente).</summary>
    public static byte[] EncodeAwareness(ReadOnlySpan<byte> awarenessUpdate)
    {
        var w = new Lib0Encoding.Lib0Writer();
        w.WriteVarUint((uint)MessageType.Awareness);
        w.WriteVarUint8Array(awarenessUpdate);
        return w.ToArray();
    }

    private static byte[] EncodeSync(SyncMessageType syncType, ReadOnlySpan<byte> payload)
    {
        var w = new Lib0Encoding.Lib0Writer();
        w.WriteVarUint((uint)MessageType.Sync);
        w.WriteVarUint((uint)syncType);
        w.WriteVarUint8Array(payload);
        return w.ToArray();
    }

    /// <summary>
    /// Decodifica un frame WebSocket binario. Rechaza (con <see cref="MalformedMessageException"/>) el frame
    /// que exceda <paramref name="maxMessageBytes"/> antes de parsear, cualquier tipo/sub-tipo desconocido,
    /// varints inválidos y bytes sobrantes tras el mensaje.
    /// </summary>
    /// <param name="frame">Bytes crudos del frame WebSocket.</param>
    /// <param name="maxMessageBytes">
    /// Cap de tamaño del frame (FU-002 parte a). Por defecto <see cref="Lib0Encoding.DefaultMaxMessageBytes"/>.
    /// </param>
    /// <returns>El mensaje decodificado; <see cref="SyncMessage.Payload"/> es una vista de <paramref name="frame"/>.</returns>
    public static SyncMessage Decode(
        ReadOnlySpan<byte> frame,
        int maxMessageBytes = Lib0Encoding.DefaultMaxMessageBytes)
    {
        // Guarda de tamaño (FU-002 parte a): rechazar el frame sobredimensionado ANTES de tocar el decoder.
        if (frame.Length > maxMessageBytes)
        {
            throw new MalformedMessageException(
                $"El frame ({frame.Length} bytes) excede el cap de {maxMessageBytes} bytes.");
        }

        var r = new Lib0Encoding.Lib0Reader(frame);
        uint rawType = r.ReadVarUint();

        SyncMessage message;
        switch (rawType)
        {
            case (uint)MessageType.Sync:
            {
                uint rawSync = r.ReadVarUint();
                if (rawSync > (uint)SyncMessageType.Update)
                {
                    throw new MalformedMessageException($"Sub-tipo SYNC desconocido: {rawSync}.");
                }

                ReadOnlySpan<byte> payload = r.ReadVarUint8Array();
                message = new SyncMessage(MessageType.Sync, (SyncMessageType)rawSync, payload);
                break;
            }

            case (uint)MessageType.Awareness:
            {
                ReadOnlySpan<byte> payload = r.ReadVarUint8Array();
                message = new SyncMessage(MessageType.Awareness, default, payload);
                break;
            }

            default:
                throw new MalformedMessageException($"Tipo de mensaje desconocido: {rawType}.");
        }

        if (!r.AtEnd)
        {
            throw new MalformedMessageException(
                $"Bytes sobrantes tras el mensaje: {r.Remaining} byte(s) sin consumir.");
        }

        return message;
    }
}
