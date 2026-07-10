namespace Weft;

/// <summary>Fábrica de documentos de un motor CRDT. Thread-safe.</summary>
public interface ICrdtEngine
{
    /// <summary>Nombre estable del motor ("yrs", "loro").</summary>
    string Name { get; }

    /// <summary>Crea un documento vacío.</summary>
    ICrdtDoc CreateDoc();

    /// <summary>Reconstruye un documento desde un blob exportado.</summary>
    /// <param name="blob">Estado exportado por <see cref="ICrdtDoc.ExportState"/> (update v1).</param>
    /// <exception cref="CorruptUpdateException">El blob no es decodificable.</exception>
    ICrdtDoc LoadDoc(ReadOnlySpan<byte> blob);

    /// <summary>Capacidad opcional de versionado nativo; <c>null</c> si el motor no la ofrece.</summary>
    INativeVersioning? NativeVersioning { get; }
}
