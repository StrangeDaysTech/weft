namespace Weft.Server.Persistence;

/// <summary>
/// Estado durable por documento (FR-017). Los blobs son <b>opacos</b>: el store nunca interpreta bytes de yrs
/// (P-IV). Toda implementación es <b>thread-safe</b> por documento y pasa la misma contract suite compartida
/// (<c>DocumentStoreContractSuite</c>), garantía de intercambiabilidad que exige el escenario de aceptación de
/// US3.
/// </summary>
/// <remarks>
/// El estado persistido de un documento es un snapshot consolidado más los updates incrementales acumulados
/// desde ese snapshot. <see cref="LoadAsync"/> los devuelve enmarcados por <see cref="DocumentStateFraming"/>
/// (snapshot primero, luego los updates en orden de append); el relay aplica cada record en orden para
/// reconstruir el documento. <see cref="SaveSnapshotAsync"/> hace <b>compaction</b>: reemplaza el snapshot y
/// descarta los updates acumulados.
/// </remarks>
public interface IDocumentStore
{
    /// <summary>
    /// Estado completo persistido del documento (snapshot + updates acumulados, enmarcados por
    /// <see cref="DocumentStateFraming"/>), o <c>null</c> si el documento no existe.
    /// </summary>
    ValueTask<byte[]?> LoadAsync(string docId, CancellationToken ct = default);

    /// <summary>Añade un update incremental a la cola durable del documento (durabilidad entre snapshots).</summary>
    ValueTask AppendUpdateAsync(string docId, ReadOnlyMemory<byte> update, CancellationToken ct = default);

    /// <summary>
    /// Guarda un snapshot consolidado: reemplaza el estado y descarta los updates acumulados (compaction).
    /// </summary>
    ValueTask SaveSnapshotAsync(string docId, ReadOnlyMemory<byte> state, CancellationToken ct = default);
}
