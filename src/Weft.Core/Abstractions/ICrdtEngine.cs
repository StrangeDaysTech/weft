namespace Weft;

/// <summary>Document factory of a CRDT engine. Thread-safe.</summary>
public interface ICrdtEngine
{
    /// <summary>Stable engine name ("yrs", "loro").</summary>
    string Name { get; }

    /// <summary>Creates an empty document.</summary>
    ICrdtDoc CreateDoc();

    /// <summary>Rebuilds a document from an exported blob.</summary>
    /// <param name="blob">State exported by <see cref="ICrdtDoc.ExportState"/> (update v1).</param>
    /// <exception cref="CorruptUpdateException">The blob is not decodable.</exception>
    ICrdtDoc LoadDoc(ReadOnlySpan<byte> blob);

    /// <summary>Optional native versioning capability; <c>null</c> if the engine does not offer it.</summary>
    INativeVersioning? NativeVersioning { get; }

    /// <summary>
    /// Optional capability to seed the replica identity (test/corpus determinism);
    /// <c>null</c> if the engine does not offer it.
    /// </summary>
    IDeterministicSeeding? DeterministicSeeding { get; }
}
