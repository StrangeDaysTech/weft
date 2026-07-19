namespace Weft.Server.Protocol;

/// <summary>Top-level wire message type (<c>y-protocols</c>).</summary>
public enum MessageType
{
    /// <summary>Document synchronization (sub-typed by <see cref="SyncMessageType"/>).</summary>
    Sync = 0,

    /// <summary>Ephemeral per-client state (<c>y-awareness</c>); never persisted.</summary>
    Awareness = 1,
}

/// <summary>Sub-type of a <see cref="MessageType.Sync"/> message (<c>y-sync</c> protocol).</summary>
public enum SyncMessageType
{
    /// <summary>SyncStep1: "here's what I know" — carries the sender's state vector.</summary>
    Step1 = 0,

    /// <summary>SyncStep2: reply with the delta (update) the state-vector sender is missing.</summary>
    Step2 = 1,

    /// <summary>Live incremental update.</summary>
    Update = 2,
}

/// <summary>
/// A decoded y-sync message. <c>ref struct</c>: <see cref="Payload"/> is a zero-copy view of the source
/// frame (the state vector for <see cref="SyncMessageType.Step1"/>; the update for
/// <see cref="SyncMessageType.Step2"/>/<see cref="SyncMessageType.Update"/>; the awareness update for
/// <see cref="MessageType.Awareness"/>). The payload bytes are <b>opaque</b> to this layer: they are handed
/// as-is to the broker/yrs decoder upstream (CHARTER-05).
/// </summary>
public readonly ref struct SyncMessage
{
    internal SyncMessage(MessageType type, SyncMessageType syncType, ReadOnlySpan<byte> payload)
    {
        Type = type;
        SyncType = syncType;
        Payload = payload;
    }

    /// <summary>Top-level type.</summary>
    public MessageType Type { get; }

    /// <summary>Sub-type; only meaningful when <see cref="Type"/> is <see cref="MessageType.Sync"/>.</summary>
    public SyncMessageType SyncType { get; }

    /// <summary>Opaque payload (state vector / update / awareness).</summary>
    public ReadOnlySpan<byte> Payload { get; }
}

/// <summary>
/// <c>y-sync</c> framing over the lib0 encoding (<see cref="Lib0Encoding"/>), compatible with standard Yjs
/// clients. Wire structure (every integer is a varint):
/// <code>
/// SYNC      := 0 &lt;syncType&gt; &lt;VarUint8Array payload&gt;
/// AWARENESS := 1 &lt;VarUint8Array payload&gt;
/// </code>
/// </summary>
/// <remarks>
/// The decoder applies the frame-size cap (FU-002 part a) <b>before</b> parsing and rejects unknown types,
/// truncated/oversized varints and trailing bytes with <see cref="MalformedMessageException"/>. It does not
/// interpret the payload: that is the responsibility of the relay/yrs decoder (CHARTER-05). This layer is
/// pure transport.
/// </remarks>
public static class SyncProtocol
{
    /// <summary>Encodes <c>SyncStep1(stateVector)</c> — sent by whoever connects.</summary>
    public static byte[] EncodeSyncStep1(ReadOnlySpan<byte> stateVector) =>
        EncodeSync(SyncMessageType.Step1, stateVector);

    /// <summary>Encodes <c>SyncStep2(update)</c> — the delta that answers a SyncStep1.</summary>
    public static byte[] EncodeSyncStep2(ReadOnlySpan<byte> update) =>
        EncodeSync(SyncMessageType.Step2, update);

    /// <summary>Encodes <c>Update(update)</c> — a live incremental update.</summary>
    public static byte[] EncodeUpdate(ReadOnlySpan<byte> update) =>
        EncodeSync(SyncMessageType.Update, update);

    /// <summary>Encodes an <c>AWARENESS(update)</c> message (ephemeral per-client state).</summary>
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
    /// Decodes a binary WebSocket frame. Rejects (with <see cref="MalformedMessageException"/>) a frame that
    /// exceeds <paramref name="maxMessageBytes"/> before parsing, any unknown type/sub-type, invalid varints
    /// and trailing bytes after the message.
    /// </summary>
    /// <param name="frame">Raw bytes of the WebSocket frame.</param>
    /// <param name="maxMessageBytes">
    /// Frame-size cap (FU-002 part a). Defaults to <see cref="Lib0Encoding.DefaultMaxMessageBytes"/>.
    /// </param>
    /// <returns>The decoded message; <see cref="SyncMessage.Payload"/> is a view over <paramref name="frame"/>.</returns>
    public static SyncMessage Decode(
        ReadOnlySpan<byte> frame,
        int maxMessageBytes = Lib0Encoding.DefaultMaxMessageBytes)
    {
        // Size guard (FU-002 part a): reject the oversized frame BEFORE touching the decoder.
        if (frame.Length > maxMessageBytes)
        {
            throw new MalformedMessageException(
                $"The frame ({frame.Length} bytes) exceeds the cap of {maxMessageBytes} bytes.");
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
                    throw new MalformedMessageException($"Unknown SYNC sub-type: {rawSync}.");
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
                throw new MalformedMessageException($"Unknown message type: {rawType}.");
        }

        if (!r.AtEnd)
        {
            throw new MalformedMessageException(
                $"Trailing bytes after the message: {r.Remaining} byte(s) left unconsumed.");
        }

        return message;
    }
}
