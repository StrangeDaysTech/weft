namespace Weft;

/// <summary>Capacidad opcional para motores con versionado nativo (Loro). Probes de paridad.</summary>
public interface INativeVersioning
{
    /// <summary>Descripción (JSON) del diff nativo del motor para el campo dado.</summary>
    string NativeDiffProbe(ICrdtDoc doc, string field);

    /// <summary>Descripción (JSON) de la operación nativa de fork/merge del motor.</summary>
    string NativeBranchMergeProbe(ICrdtDoc doc, string field);

    /// <summary>Snapshot superficial (shallow) nativo del documento.</summary>
    byte[] ShallowSnapshot(ICrdtDoc doc);
}
