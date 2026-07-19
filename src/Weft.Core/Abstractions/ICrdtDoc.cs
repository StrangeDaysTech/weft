namespace Weft;

/// <summary>
/// Live CRDT document. NOT thread-safe: the owner serializes access (or uses the
/// <c>DocumentBroker</c>, which guarantees it). See constitution P-V.
/// </summary>
public interface ICrdtDoc : IDisposable
{
    /// <summary>
    /// Stable name of the engine backing this document ("yrs", "loro"). Matches
    /// <see cref="ICrdtEngine.Name"/>. Allows rejecting cross-engine mixes before crossing the FFI.
    /// </summary>
    string EngineName { get; }

    // -- Text by named field (v1) --

    /// <summary>Inserts <paramref name="text"/> into field <paramref name="field"/> at position <paramref name="index"/>.</summary>
    void InsertText(string field, int index, string text);

    /// <summary>Deletes <paramref name="length"/> units from <paramref name="index"/> in field <paramref name="field"/>.</summary>
    void DeleteText(string field, int index, int length);

    /// <summary>Returns the full content of field <paramref name="field"/>.</summary>
    string GetText(string field);

    // -- State and synchronization --

    /// <summary>Byte-deterministic export of the full state (basis of content-addressing, P-III).</summary>
    byte[] ExportState();

    /// <summary>"What I know" summary for incremental sync.</summary>
    byte[] ExportStateVector();

    /// <summary>Delta with the changes the sender of <paramref name="stateVector"/> does not know.</summary>
    byte[] ExportUpdateSince(ReadOnlySpan<byte> stateVector);

    /// <summary>Merges an update/state from another replica (convergent).</summary>
    void ApplyUpdate(ReadOnlySpan<byte> update);
}
