using System.Buffers;

namespace Weft.Server.Protocol;

/// <summary>
/// Se lanza cuando un frame de red no respeta el formato lib0/y-sync esperado: varint truncado o
/// sobredimensionado, longitud declarada mayor que los bytes disponibles, frame que excede el cap de
/// tamaño, o bytes sobrantes tras un mensaje completo. El relay traduce esto a un cierre WebSocket 1002
/// de la conexión afectada, sin impacto en los pares (edge case de spec, CHARTER-05).
/// </summary>
public sealed class MalformedMessageException : Exception
{
    /// <summary>Crea la excepción con un mensaje explícito.</summary>
    public MalformedMessageException(string message) : base(message) { }
}

/// <summary>
/// Encoding lib0 (varint) compatible con <c>y-protocols</c>/<c>y-websocket</c> v1/v2. Provee el lector y el
/// escritor de bajo nivel sobre los que se construye el framing y-sync (<see cref="SyncProtocol"/>).
/// </summary>
/// <remarks>
/// <para>
/// El varint lib0 es un entero sin signo de ancho variable, grupos de 7 bits en orden little-endian, con el
/// bit alto (<c>0x80</c>) como bandera de continuación. Un <c>VarUint8Array</c> es un varint de longitud
/// seguido de esos bytes.
/// </para>
/// <para>
/// <b>Frontera de confianza (P-I/P-II, FU-002 parte a).</b> Este es el primer punto que recibe bytes de red
/// <b>no confiables</b>: antes de que cualquier update llegue al decoder de yrs (cuya amplificación de memoria
/// es el DoS que describe FU-002). Dos guardas estructurales lo contienen: (1) el frame completo se rechaza si
/// excede <see cref="DefaultMaxMessageBytes"/> (o el cap que pase el llamador) <b>antes</b> de parsear; y (2)
/// <see cref="Lib0Reader.ReadVarUint8Array"/> nunca asigna ni avanza según una longitud declarada mayor que
/// los bytes que realmente quedan — un prefijo mentiroso de pocos bytes no puede inducir una asignación
/// gigante. Ambas fallan con <see cref="MalformedMessageException"/>, jamás con un abort.
/// </para>
/// </remarks>
public static class Lib0Encoding
{
    /// <summary>
    /// Cap por defecto del tamaño de un frame WebSocket entrante (16 MiB). Configurable por el llamador:
    /// documentos grandes legítimos consolidan vía snapshot; los updates incrementales en vivo son pequeños.
    /// </summary>
    public const int DefaultMaxMessageBytes = 16 * 1024 * 1024;

    /// <summary>
    /// Lector de bajo nivel sobre un frame en memoria. <c>ref struct</c>: los <see cref="ReadOnlySpan{T}"/>
    /// que devuelve apuntan al buffer del frame (zero-copy), válidos mientras viva el frame de origen.
    /// </summary>
    public ref struct Lib0Reader
    {
        private readonly ReadOnlySpan<byte> _buffer;
        private int _pos;

        /// <summary>Crea un lector posicionado al inicio de <paramref name="buffer"/>.</summary>
        public Lib0Reader(ReadOnlySpan<byte> buffer)
        {
            _buffer = buffer;
            _pos = 0;
        }

        /// <summary>Bytes aún no consumidos.</summary>
        public readonly int Remaining => _buffer.Length - _pos;

        /// <summary><c>true</c> si se consumió el frame completo.</summary>
        public readonly bool AtEnd => _pos >= _buffer.Length;

        /// <summary>
        /// Lee un varint sin signo (hasta 32 bits). Falla si el varint se trunca al final del buffer o si
        /// codifica un valor que no cabe en 32 bits (defensa contra prefijos degenerados).
        /// </summary>
        public uint ReadVarUint()
        {
            uint value = 0;
            int shift = 0;
            while (true)
            {
                if (_pos >= _buffer.Length)
                {
                    throw new MalformedMessageException("varint truncado: fin del buffer antes de cerrar el número.");
                }

                byte b = _buffer[_pos++];
                // Los grupos de 7 bits caben en 32 bits solo hasta shift=28 (5 bytes); el 5.º no debe aportar
                // bits por encima del bit 31.
                if (shift > 28 || (shift == 28 && (b & 0x7F) > 0x0F))
                {
                    throw new MalformedMessageException("varint sobredimensionado: excede 32 bits.");
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
        /// Lee un bloque <c>VarUint8Array</c> (longitud varint + bytes) y devuelve una vista zero-copy de esos
        /// bytes. <b>Guarda clave anti-DoS:</b> si la longitud declarada supera los bytes restantes, falla en
        /// vez de intentar leer/asignar — un frame pequeño no puede reclamar un array gigante.
        /// </summary>
        public ReadOnlySpan<byte> ReadVarUint8Array()
        {
            uint len = ReadVarUint();
            if (len > (uint)Remaining)
            {
                throw new MalformedMessageException(
                    $"VarUint8Array declara {len} bytes pero solo quedan {Remaining} en el frame.");
            }

            ReadOnlySpan<byte> slice = _buffer.Slice(_pos, (int)len);
            _pos += (int)len;
            return slice;
        }
    }

    /// <summary>
    /// Escritor de bajo nivel con buffer creciente. El <see cref="System.Buffers.ArrayBufferWriter{T}"/>
    /// subyacente amortiza las reasignaciones; <see cref="WrittenSpan"/>/<see cref="ToArray"/> exponen el
    /// resultado. Un uso, un mensaje: no reutilizar entre frames.
    /// </summary>
    public sealed class Lib0Writer
    {
        private readonly ArrayBufferWriter<byte> _writer = new();

        /// <summary>Bytes escritos hasta el momento.</summary>
        public int Length => _writer.WrittenCount;

        /// <summary>Vista de los bytes escritos.</summary>
        public ReadOnlySpan<byte> WrittenSpan => _writer.WrittenSpan;

        /// <summary>Copia los bytes escritos en un nuevo array (el frame listo para enviar).</summary>
        public byte[] ToArray() => _writer.WrittenSpan.ToArray();

        /// <summary>Escribe un varint sin signo (grupos de 7 bits little-endian, bit alto de continuación).</summary>
        public void WriteVarUint(uint value)
        {
            // A lo sumo 5 bytes para 32 bits.
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

        /// <summary>Escribe un bloque <c>VarUint8Array</c>: longitud varint seguida de los bytes.</summary>
        public void WriteVarUint8Array(ReadOnlySpan<byte> bytes)
        {
            WriteVarUint((uint)bytes.Length);
            _writer.Write(bytes);
        }
    }
}
