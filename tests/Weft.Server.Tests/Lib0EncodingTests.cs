using Weft.Server.Protocol;

namespace Weft.Server.Tests;

/// <summary>
/// Vectores del encoding lib0 y del framing y-sync (T043), más el test del cap de tamaño de mensaje
/// (FU-002 parte a). Los vectores de bytes son el formato de wire de <c>y-protocols</c>/<c>y-websocket</c>:
/// se afirman byte a byte para blindar la compatibilidad con clientes Yjs.
/// </summary>
public sealed class Lib0EncodingTests
{
    // --- Varint: vectores conocidos (little-endian, bit 0x80 de continuación) ---

    [Theory]
    [InlineData(0u, new byte[] { 0x00 })]
    [InlineData(1u, new byte[] { 0x01 })]
    [InlineData(127u, new byte[] { 0x7F })]
    [InlineData(128u, new byte[] { 0x80, 0x01 })]
    [InlineData(300u, new byte[] { 0xAC, 0x02 })]
    [InlineData(16384u, new byte[] { 0x80, 0x80, 0x01 })]
    [InlineData(uint.MaxValue, new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0x0F })]
    public void VarUint_encodes_to_known_bytes(uint value, byte[] expected)
    {
        var w = new Lib0Encoding.Lib0Writer();
        w.WriteVarUint(value);
        Assert.Equal(expected, w.ToArray());

        var r = new Lib0Encoding.Lib0Reader(expected);
        Assert.Equal(value, r.ReadVarUint());
        Assert.True(r.AtEnd);
    }

    [Theory]
    [InlineData(0u)]
    [InlineData(1u)]
    [InlineData(63u)]
    [InlineData(64u)]
    [InlineData(8191u)]
    [InlineData(2097152u)]
    [InlineData(uint.MaxValue)]
    public void VarUint_round_trips(uint value)
    {
        var w = new Lib0Encoding.Lib0Writer();
        w.WriteVarUint(value);
        var r = new Lib0Encoding.Lib0Reader(w.WrittenSpan);
        Assert.Equal(value, r.ReadVarUint());
        Assert.True(r.AtEnd);
    }

    [Fact]
    public void VarUint8Array_round_trips()
    {
        byte[] payload = [0xDE, 0xAD, 0xBE, 0xEF, 0x00, 0x7F];
        var w = new Lib0Encoding.Lib0Writer();
        w.WriteVarUint8Array(payload);

        var r = new Lib0Encoding.Lib0Reader(w.WrittenSpan);
        Assert.Equal(payload, r.ReadVarUint8Array().ToArray());
        Assert.True(r.AtEnd);
    }

    // --- Framing y-sync: vectores conocidos ---

    [Fact]
    public void EncodeSyncStep1_produces_known_frame()
    {
        // SYNC(0) · Step1(0) · VarUint8Array([0x01])
        byte[] frame = SyncProtocol.EncodeSyncStep1([0x01]);
        Assert.Equal(new byte[] { 0x00, 0x00, 0x01, 0x01 }, frame);
    }

    [Fact]
    public void EncodeUpdate_produces_known_frame()
    {
        // SYNC(0) · Update(2) · VarUint8Array([0xAA, 0xBB])
        byte[] frame = SyncProtocol.EncodeUpdate([0xAA, 0xBB]);
        Assert.Equal(new byte[] { 0x00, 0x02, 0x02, 0xAA, 0xBB }, frame);
    }

    [Fact]
    public void EncodeAwareness_produces_known_frame()
    {
        // AWARENESS(1) · VarUint8Array([0x09])
        byte[] frame = SyncProtocol.EncodeAwareness([0x09]);
        Assert.Equal(new byte[] { 0x01, 0x01, 0x09 }, frame);
    }

    [Theory]
    [InlineData(SyncMessageType.Step1)]
    [InlineData(SyncMessageType.Step2)]
    [InlineData(SyncMessageType.Update)]
    public void Sync_messages_round_trip(SyncMessageType syncType)
    {
        byte[] payload = [0x10, 0x20, 0x30];
        byte[] frame = syncType switch
        {
            SyncMessageType.Step1 => SyncProtocol.EncodeSyncStep1(payload),
            SyncMessageType.Step2 => SyncProtocol.EncodeSyncStep2(payload),
            _ => SyncProtocol.EncodeUpdate(payload),
        };

        SyncMessage decoded = SyncProtocol.Decode(frame);
        Assert.Equal(MessageType.Sync, decoded.Type);
        Assert.Equal(syncType, decoded.SyncType);
        Assert.Equal(payload, decoded.Payload.ToArray());
    }

    [Fact]
    public void Awareness_message_round_trips()
    {
        byte[] payload = [0x01, 0x02, 0x03];
        SyncMessage decoded = SyncProtocol.Decode(SyncProtocol.EncodeAwareness(payload));
        Assert.Equal(MessageType.Awareness, decoded.Type);
        Assert.Equal(payload, decoded.Payload.ToArray());
    }

    [Fact]
    public void Decode_accepts_empty_payload()
    {
        SyncMessage decoded = SyncProtocol.Decode(SyncProtocol.EncodeUpdate([]));
        Assert.Equal(MessageType.Sync, decoded.Type);
        Assert.Equal(SyncMessageType.Update, decoded.SyncType);
        Assert.True(decoded.Payload.IsEmpty);
    }

    // --- Cap de tamaño de mensaje (FU-002 parte a) ---

    [Fact]
    public void Decode_rejects_frame_exceeding_size_cap_before_parsing()
    {
        byte[] frame = SyncProtocol.EncodeUpdate(new byte[100]);

        // El mismo frame pasa con el cap por defecto y se rechaza con un cap por debajo de su tamaño.
        _ = SyncProtocol.Decode(frame);
        var ex = Assert.Throws<MalformedMessageException>(
            () => SyncProtocol.Decode(frame, maxMessageBytes: 10));
        Assert.Contains("cap", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Decode_rejects_lying_length_prefix_without_allocating()
    {
        // SYNC · Update · longitud declarada = uint.MaxValue (≈4 GiB) pero SIN payload: la guarda estructural
        // rechaza antes de asignar nada (el DoS de amplificación de memoria que describe FU-002).
        byte[] frame = [0x00, 0x02, 0xFF, 0xFF, 0xFF, 0xFF, 0x0F];
        Assert.Throws<MalformedMessageException>(() => SyncProtocol.Decode(frame));
    }

    // --- Frames malformados → MalformedMessageException (relay: cierre 1002) ---

    [Fact]
    public void Decode_rejects_unknown_message_type()
    {
        byte[] frame = [0x07, 0x00];
        Assert.Throws<MalformedMessageException>(() => SyncProtocol.Decode(frame));
    }

    [Fact]
    public void Decode_rejects_unknown_sync_subtype()
    {
        // SYNC(0) · sub-tipo 9 (desconocido)
        byte[] frame = [0x00, 0x09, 0x00];
        Assert.Throws<MalformedMessageException>(() => SyncProtocol.Decode(frame));
    }

    [Fact]
    public void Decode_rejects_trailing_bytes()
    {
        byte[] frame = [0x00, 0x02, 0x01, 0xAA, 0xFF]; // un byte de más tras el payload
        Assert.Throws<MalformedMessageException>(() => SyncProtocol.Decode(frame));
    }

    [Fact]
    public void Decode_rejects_truncated_varint()
    {
        byte[] frame = [0x80]; // continuación sin byte final
        Assert.Throws<MalformedMessageException>(() => SyncProtocol.Decode(frame));
    }

    [Fact]
    public void ReadVarUint_rejects_overflow_beyond_32_bits()
    {
        // 5.º byte aporta bits por encima de 32 → sobredimensionado.
        byte[] bytes = [0x80, 0x80, 0x80, 0x80, 0x10];
        Assert.Throws<MalformedMessageException>(() =>
        {
            var r = new Lib0Encoding.Lib0Reader(bytes);
            r.ReadVarUint();
        });
    }
}
