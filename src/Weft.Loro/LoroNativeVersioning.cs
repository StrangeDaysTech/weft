namespace Weft.Loro;

/// <summary>
/// Implementación de <see cref="INativeVersioning"/> para Loro (CHARTER-10/FU-006). Probes
/// <b>demostrativos</b> de la capacidad de versionado nativo de Loro (diff/fork/shallow snapshot),
/// que yrs no tiene. <b>No</b> son content-addressing: su salida no es byte-determinista entre
/// réplicas y no alimenta <c>VersionId</c> (que usa el export determinista de <c>ICrdtDoc.ExportState</c>).
/// Sin estado; se expone como singleton vía <see cref="LoroEngine.NativeVersioning"/>.
/// </summary>
internal sealed class LoroNativeVersioning : INativeVersioning
{
    internal static readonly LoroNativeVersioning Instance = new();

    private LoroNativeVersioning() { }

    /// <inheritdoc/>
    public string NativeDiffProbe(ICrdtDoc doc, string field) => AsLoro(doc).NativeDiffProbeJson(field);

    /// <inheritdoc/>
    public string NativeBranchMergeProbe(ICrdtDoc doc, string field) => AsLoro(doc).NativeBranchMergeProbeJson(field);

    /// <inheritdoc/>
    public byte[] ShallowSnapshot(ICrdtDoc doc) => AsLoro(doc).ShallowSnapshotNative();

    // El versionado nativo de Loro solo opera sobre documentos de Loro. Un doc de otro motor
    // (p. ej. yrs) es un error de uso claro, no un fallo en la frontera nativa.
    private static LoroDoc AsLoro(ICrdtDoc doc)
    {
        ArgumentNullException.ThrowIfNull(doc);
        return doc as LoroDoc
            ?? throw new ArgumentException(
                $"El versionado nativo de Loro requiere un documento de Loro, no '{doc.GetType().Name}' (motor '{doc.EngineName}').",
                nameof(doc));
    }
}
