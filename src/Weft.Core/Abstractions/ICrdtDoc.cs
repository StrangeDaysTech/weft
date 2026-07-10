namespace Weft;

/// <summary>
/// Documento CRDT vivo. NO es thread-safe: el dueño serializa el acceso (o usa el
/// <c>DocumentBroker</c>, que lo garantiza). Ver constitución P-V.
/// </summary>
public interface ICrdtDoc : IDisposable
{
    // -- Texto por campo nombrado (v1) --

    /// <summary>Inserta <paramref name="text"/> en el campo <paramref name="field"/> en la posición <paramref name="index"/>.</summary>
    void InsertText(string field, int index, string text);

    /// <summary>Borra <paramref name="length"/> unidades desde <paramref name="index"/> en el campo <paramref name="field"/>.</summary>
    void DeleteText(string field, int index, int length);

    /// <summary>Devuelve el contenido completo del campo <paramref name="field"/>.</summary>
    string GetText(string field);

    // -- Estado y sincronización --

    /// <summary>Export byte-determinista del estado completo (base del content-addressing, P-III).</summary>
    byte[] ExportState();

    /// <summary>Resumen "qué conozco" para sync incremental.</summary>
    byte[] ExportStateVector();

    /// <summary>Delta con los cambios que el emisor del <paramref name="stateVector"/> no conoce.</summary>
    byte[] ExportUpdateSince(ReadOnlySpan<byte> stateVector);

    /// <summary>Fusiona un update/estado de otra réplica (convergente).</summary>
    void ApplyUpdate(ReadOnlySpan<byte> update);
}
