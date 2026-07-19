using System.Text;

namespace Weft.Server.Protocol;

/// <summary>
/// Minimal parsing of the <c>y-awareness</c> protocol — just enough for the relay to broadcast the <b>removal</b> of
/// a connection's state when it closes (FR-015). The relay does not interpret the <i>content</i> of the state (it is
/// opaque); it only needs the <c>clientID</c>s that a connection announced in order to mark them offline on leaving.
/// </summary>
/// <remarks>
/// Format of an awareness update (inner payload of the <see cref="MessageType.Awareness"/> message):
/// <code>
/// &lt;numClients:varUint&gt; ( &lt;clientID:varUint&gt; &lt;clock:varUint&gt; &lt;state:VarUint8Array (JSON UTF-8)&gt; )*
/// </code>
/// The removal is an update with <c>clock+1</c> and state <c>"null"</c> for each clientID, as
/// <c>y-protocols/awareness</c> does.
/// </remarks>
internal static class AwarenessProtocol
{
    private static readonly byte[] NullStateUtf8 = Encoding.UTF8.GetBytes("null");

    /// <summary>
    /// Extracts the <c>clientID → clock</c> pairs from an awareness update, accumulating into
    /// <paramref name="tracked"/> (the highest clock seen per client). Tolerant of payloads that do not parse
    /// (best-effort: awareness is not critical for the document's convergence).
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
                _ = r.ReadVarUint8Array(); // state (opaque): skipped
                // Insert the client even if its clock is 0 (common in the first awareness of a Yjs client);
                // indexing `tracked[clientId]` in the else with the absent key would throw KeyNotFoundException.
                if (!tracked.TryGetValue(clientId, out uint prevClock) || clock > prevClock)
                {
                    tracked[clientId] = clock;
                }
            }
        }
        catch (MalformedMessageException)
        {
            // Malformed awareness: ignored for tracking (the relay already handles the message's broadcast).
        }
    }

    /// <summary>
    /// Builds a complete <see cref="MessageType.Awareness"/> message that marks
    /// <paramref name="clients"/> offline (state <c>"null"</c>, <c>clock+1</c>). Returns <c>null</c> if there are no
    /// clients to remove.
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
