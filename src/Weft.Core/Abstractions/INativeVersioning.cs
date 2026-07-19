namespace Weft;

/// <summary>Optional capability for engines with native versioning (Loro). Parity probes.</summary>
public interface INativeVersioning
{
    /// <summary>Description (JSON) of the engine's native diff for the given field.</summary>
    string NativeDiffProbe(ICrdtDoc doc, string field);

    /// <summary>Description (JSON) of the engine's native fork/merge operation.</summary>
    string NativeBranchMergeProbe(ICrdtDoc doc, string field);

    /// <summary>Native shallow snapshot of the document.</summary>
    byte[] ShallowSnapshot(ICrdtDoc doc);
}
