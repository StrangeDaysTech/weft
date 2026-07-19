using System.Buffers;

namespace Weft.Server.Protocol;

/// <summary>
/// Thrown when a network frame does not respect the expected lib0/y-sync format: truncated or oversized
/// varint, declared length larger than the available bytes, frame exceeding the size cap, or trailing bytes
/// after a complete message. The relay translates this into a WebSocket 1002 close of the affected
/// connection, with no impact on the peers (spec edge case, CHARTER-05).
/// </summary>
public sealed class MalformedMessageException : Exception
{
    /// <summary>Creates the exception with an explicit message.</summary>
    public MalformedMessageException(string message) : base(message) { }
}

/// <summary>
/// lib0 (varint) encoding compatible with <c>y-protocols</c>/<c>y-websocket</c> v1/v2. Provides the low-level
/// reader and writer on top of which the y-sync framing (<see cref="SyncProtocol"/>) is built.
/// </summary>
/// <remarks>
/// <para>
/// The lib0 varint is a variable-width unsigned integer, groups of 7 bits in little-endian order, with the
/// high bit (<c>0x80</c>) as the continuation flag. A <c>VarUint8Array</c> is a length varint followed by
/// those bytes.
/// </para>
/// <para>
/// <b>Trust boundary (P-I/P-II, FU-002 part a).</b> This is the first point that receives <b>untrusted</b>
/// network bytes: before any update reaches the yrs decoder (whose memory amplification is the DoS that
/// FU-002 describes). Two structural guards contain it: (1) the whole frame is rejected if it exceeds
/// <see cref="DefaultMaxMessageBytes"/> (or the cap the caller passes) <b>before</b> parsing; and (2)
/// <see cref="Lib0Reader.ReadVarUint8Array"/> never allocates or advances based on a declared length larger
/// than the bytes that actually remain — a lying prefix of a few bytes cannot induce a giant allocation.
/// Both fail with <see cref="MalformedMessageException"/>, never with an abort.
/// </para>
/// </remarks>
public static class Lib0Encoding
{
    /// <summary>
    /// Default size cap of an incoming WebSocket frame (16 MiB). Configurable by the caller:
    /// large legitimate documents consolidate via snapshot; live incremental updates are small.
    /// </summary>
    public const int DefaultMaxMessageBytes = 16 * 1024 * 1024;

    /// <summary>
    /// Low-level reader over an in-memory frame. <c>ref struct</c>: the <see cref="ReadOnlySpan{T}"/>
    /// it returns point to the frame buffer (zero-copy), valid while the source frame lives.
    /// </summary>
    public ref struct Lib0Reader
    {
        private readonly ReadOnlySpan<byte> _buffer;
        private int _pos;

        /// <summary>Creates a reader positioned at the start of <paramref name="buffer"/>.</summary>
        public Lib0Reader(ReadOnlySpan<byte> buffer)
        {
            _buffer = buffer;
            _pos = 0;
        }

        /// <summary>Bytes not yet consumed.</summary>
        public readonly int Remaining => _buffer.Length - _pos;

        /// <summary><c>true</c> if the whole frame was consumed.</summary>
        public readonly bool AtEnd => _pos >= _buffer.Length;

        /// <summary>
        /// Reads an unsigned varint (up to 32 bits). Fails if the varint is truncated at the end of the buffer
        /// or if it encodes a value that does not fit in 32 bits (defense against degenerate prefixes).
        /// </summary>
        public uint ReadVarUint()
        {
            uint value = 0;
            int shift = 0;
            while (true)
            {
                if (_pos >= _buffer.Length)
                {
                    throw new MalformedMessageException("truncated varint: end of buffer before the number was closed.");
                }

                byte b = _buffer[_pos++];
                // The 7-bit groups fit in 32 bits only up to shift=28 (5 bytes); the 5th must not contribute
                // bits above bit 31.
                if (shift > 28 || (shift == 28 && (b & 0x7F) > 0x0F))
                {
                    throw new MalformedMessageException("oversized varint: exceeds 32 bits.");
                }

                value |= (uint)(b & 0x7F) << shift;
                if ((b & 0x80) == 0)
                {
                    return value;
                }

                shift += 7;
            }
        }

        /// <summary>
        /// Reads a <c>VarUint8Array</c> block (varint length + bytes) and returns a zero-copy view of those
        /// bytes. <b>Key anti-DoS guard:</b> if the declared length exceeds the remaining bytes, it fails
        /// instead of trying to read/allocate — a small frame cannot claim a giant array.
        /// </summary>
        public ReadOnlySpan<byte> ReadVarUint8Array()
        {
            uint len = ReadVarUint();
            if (len > (uint)Remaining)
            {
                throw new MalformedMessageException(
                    $"VarUint8Array declares {len} bytes but only {Remaining} remain in the frame.");
            }

            ReadOnlySpan<byte> slice = _buffer.Slice(_pos, (int)len);
            _pos += (int)len;
            return slice;
        }
    }

    /// <summary>
    /// Low-level writer with a growing buffer. The underlying <see cref="System.Buffers.ArrayBufferWriter{T}"/>
    /// amortizes the reallocations; <see cref="WrittenSpan"/>/<see cref="ToArray"/> expose the
    /// result. One use, one message: do not reuse across frames.
    /// </summary>
    public sealed class Lib0Writer
    {
        private readonly ArrayBufferWriter<byte> _writer = new();

        /// <summary>Bytes written so far.</summary>
        public int Length => _writer.WrittenCount;

        /// <summary>View of the written bytes.</summary>
        public ReadOnlySpan<byte> WrittenSpan => _writer.WrittenSpan;

        /// <summary>Copies the written bytes into a new array (the frame ready to send).</summary>
        public byte[] ToArray() => _writer.WrittenSpan.ToArray();

        /// <summary>Writes an unsigned varint (little-endian 7-bit groups, high continuation bit).</summary>
        public void WriteVarUint(uint value)
        {
            // At most 5 bytes for 32 bits.
            Span<byte> tmp = stackalloc byte[5];
            int n = 0;
            while (value >= 0x80)
            {
                tmp[n++] = (byte)(value | 0x80);
                value >>= 7;
            }

            tmp[n++] = (byte)value;
            _writer.Write(tmp[..n]);
        }

        /// <summary>Writes a <c>VarUint8Array</c> block: varint length followed by the bytes.</summary>
        public void WriteVarUint8Array(ReadOnlySpan<byte> bytes)
        {
            WriteVarUint((uint)bytes.Length);
            _writer.Write(bytes);
        }
    }
}
