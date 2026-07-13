using Weft.Server.Protocol;

namespace Weft.Server.Persistence;

/// <summary>
/// Framing compartido del estado persistido de un documento. Como los blobs de <see cref="IDocumentStore"/>
/// son <b>opacos</b> (P-IV: el store no conoce tipos de yrs y no puede fusionar updates), el estado que
/// devuelve <see cref="IDocumentStore.LoadAsync"/> es una <b>secuencia plana de records</b>: el snapshot (si
/// existe) seguido de cada update acumulado desde ese snapshot, en orden de append. Cada record se enmarca
/// como un <c>VarUint8Array</c> lib0.
/// </summary>
/// <remarks>
/// El relay (CHARTER-05) reconstruye el documento aplicando cada record en orden a un doc fresco de yrs
/// (todos son operaciones <c>apply_update</c> idempotentes: reaplicar un update ya integrado es un no-op CRDT,
/// lo que hace la recuperación tolerante a un snapshot y updates que se solapen). Esta clase es el único punto
/// que conoce el formato del contenedor; los adaptadores lo comparten y la contract suite lo verifica.
/// </remarks>
public static class DocumentStateFraming
{
    /// <summary>
    /// Enmarca <paramref name="snapshot"/> (opcional) y <paramref name="updates"/> en una secuencia de records.
    /// Devuelve <c>null</c> si no hay nada persistido (sin snapshot y sin updates) — la señal de "doc inexistente".
    /// </summary>
    public static byte[]? Frame(byte[]? snapshot, IReadOnlyList<byte[]> updates)
    {
        if (snapshot is null && updates.Count == 0)
        {
            return null;
        }

        var w = new Lib0Encoding.Lib0Writer();
        if (snapshot is not null)
        {
            w.WriteVarUint8Array(snapshot);
        }

        foreach (byte[] update in updates)
        {
            w.WriteVarUint8Array(update);
        }

        return w.ToArray();
    }

    /// <summary>
    /// Lee de vuelta los records de un estado enmarcado por <see cref="Frame"/>. Cada elemento es una copia de
    /// un record; se aplican en orden. Un contenedor truncado falla con <see cref="MalformedMessageException"/>
    /// (la misma guarda estructural del códec).
    /// </summary>
    public static IReadOnlyList<byte[]> ReadRecords(ReadOnlySpan<byte> framed)
    {
        var records = new List<byte[]>();
        var r = new Lib0Encoding.Lib0Reader(framed);
        while (!r.AtEnd)
        {
            records.Add(r.ReadVarUint8Array().ToArray());
        }

        return records;
    }
}
