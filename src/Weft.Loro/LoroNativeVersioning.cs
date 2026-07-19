namespace Weft.Loro;

/// <summary>
/// Implementation of <see cref="INativeVersioning"/> for Loro (CHARTER-10/FU-006). <b>Demonstrative</b>
/// probes of Loro's native versioning capability (diff/fork/shallow snapshot),
/// which yrs does not have. They are <b>not</b> content-addressing: their output is not byte-deterministic across
/// replicas and does not feed <c>VersionId</c> (which uses the deterministic export of <c>ICrdtDoc.ExportState</c>).
/// Stateless; exposed as a singleton via <see cref="LoroEngine.NativeVersioning"/>.
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

    // Loro's native versioning only operates on Loro documents. A doc from another engine
    // (e.g. yrs) is a clear usage error, not a failure at the native boundary.
    private static LoroDoc AsLoro(ICrdtDoc doc)
    {
        ArgumentNullException.ThrowIfNull(doc);
        return doc as LoroDoc
            ?? throw new ArgumentException(
                $"El versionado nativo de Loro requiere un documento de Loro, no '{doc.GetType().Name}' (motor '{doc.EngineName}').",
                nameof(doc));
    }
}
