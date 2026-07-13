using System.Text;

namespace Weft.Server.Protocol;

/// <summary>
/// Parsing mínimo del protocolo <c>y-awareness</c> — lo justo para que el relay difunda la <b>retirada</b> del
/// estado de una conexión al cerrarse (FR-015). El relay no interpreta el <i>contenido</i> del estado (es
/// opaco); solo necesita los <c>clientID</c> que una conexión anunció para poder marcarlos offline al salir.
/// </summary>
/// <remarks>
/// Formato de un awareness update (payload interno del mensaje <see cref="MessageType.Awareness"/>):
/// <code>
/// &lt;numClients:varUint&gt; ( &lt;clientID:varUint&gt; &lt;clock:varUint&gt; &lt;state:VarUint8Array (JSON UTF-8)&gt; )*
/// </code>
/// La retirada es un update con <c>clock+1</c> y estado <c>"null"</c> por cada clientID, como hace
/// <c>y-protocols/awareness</c>.
/// </remarks>
internal static class AwarenessProtocol
{
    private static readonly byte[] NullStateUtf8 = Encoding.UTF8.GetBytes("null");

    /// <summary>
    /// Extrae los pares <c>clientID → clock</c> de un awareness update, acumulando en
    /// <paramref name="tracked"/> (el clock más alto visto por cliente). Tolerante a payloads que no parsean
    /// (best-effort: el awareness no es crítico para la convergencia del documento).
    /// </summary>
    public static void TrackClients(ReadOnlySpan<byte> awarenessPayload, Dictionary<uint, uint> tracked)
    {
        try
        {
            var r = new Lib0Encoding.Lib0Reader(awarenessPayload);
            uint count = r.ReadVarUint();
            for (uint i = 0; i < count; i++)
            {
                uint clientId = r.ReadVarUint();
                uint clock = r.ReadVarUint();
                _ = r.ReadVarUint8Array(); // estado (opaco): se salta
                // Insertar el cliente aunque su clock sea 0 (común en el primer awareness de un cliente Yjs);
                // indexar `tracked[clientId]` en el else con la clave ausente lanzaría KeyNotFoundException.
                if (!tracked.TryGetValue(clientId, out uint prevClock) || clock > prevClock)
                {
                    tracked[clientId] = clock;
                }
            }
        }
        catch (MalformedMessageException)
        {
            // Awareness malformado: se ignora para el tracking (el broadcast del mensaje ya lo maneja el relay).
        }
    }

    /// <summary>
    /// Construye un mensaje <see cref="MessageType.Awareness"/> completo que marca offline a
    /// <paramref name="clients"/> (estado <c>"null"</c>, <c>clock+1</c>). Devuelve <c>null</c> si no hay clientes
    /// que retirar.
    /// </summary>
    public static byte[]? EncodeRemoval(IReadOnlyDictionary<uint, uint> clients)
    {
        if (clients.Count == 0)
        {
            return null;
        }

        var inner = new Lib0Encoding.Lib0Writer();
        inner.WriteVarUint((uint)clients.Count);
        foreach ((uint clientId, uint clock) in clients)
        {
            inner.WriteVarUint(clientId);
            inner.WriteVarUint(clock + 1);
            inner.WriteVarUint8Array(NullStateUtf8);
        }

        return SyncProtocol.EncodeAwareness(inner.WrittenSpan);
    }
}
