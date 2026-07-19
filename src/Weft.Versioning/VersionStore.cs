using Weft;
using Weft.Versioning.Blobs;

namespace Weft.Versioning;

/// <summary>
/// Publish/load/compare/branch/merge document versions, content-addressed and
/// engine-agnostic (constitution P-IV: depends only on the Weft.Core abstractions).
/// Thread-safe (no mutable state of its own; serializing the live doc is the responsibility of the
/// caller or of the DocumentBroker).
/// </summary>
public sealed class VersionStore
{
    private readonly ICrdtEngine _engine;
    private readonly IBlobStore _blobs;

    /// <summary>Creates the version store over an engine and a blob store.</summary>
    public VersionStore(ICrdtEngine engine, IBlobStore blobs)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(blobs);
        _engine = engine;
        _blobs = blobs;
    }

    /// <summary>Exports, hashes and persists the document. Returns the citable identity.</summary>
    public async ValueTask<VersionId> PublishAsync(ICrdtDoc doc, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(doc);
        byte[] blob = doc.ExportState();
        var id = VersionId.FromBlob(blob);
        await _blobs.PutAsync(id, blob, ct).ConfigureAwait(false);
        return id;
    }

    /// <summary>Rebuilds a live document from a published version (verifies integrity).</summary>
    /// <exception cref="KeyNotFoundException">The version does not exist in the store.</exception>
    /// <exception cref="BlobIntegrityException">The stored blob does not verify against its hash.</exception>
    public async ValueTask<ICrdtDoc> CheckoutAsync(VersionId version, CancellationToken ct = default)
    {
        byte[] blob = await LoadVerifiedAsync(version, ct).ConfigureAwait(false);
        return _engine.LoadDoc(blob);
    }

    /// <summary>Word-level text diff between two published versions, in a given field.</summary>
    public async ValueTask<TextDiff> DiffAsync(VersionId a, VersionId b, string field, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(field);
        using ICrdtDoc da = await CheckoutAsync(a, ct).ConfigureAwait(false);
        using ICrdtDoc db = await CheckoutAsync(b, ct).ConfigureAwait(false);
        return TextDiff.Compute(da.GetText(field), db.GetText(field));
    }

    /// <summary>Branch: independent live document starting from the base version (alias of Checkout).</summary>
    public ValueTask<ICrdtDoc> BranchAsync(VersionId from, CancellationToken ct = default) =>
        CheckoutAsync(from, ct);

    /// <summary>CRDT merge: imports the branch state into the target (convergent, conflict-free).</summary>
    /// <exception cref="ArgumentException">The documents belong to different engines (yrs↔Loro).</exception>
    public void Merge(ICrdtDoc target, ICrdtDoc branch)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(branch);
        if (target.EngineName != branch.EngineName)
        {
            throw new ArgumentException(
                $"No se pueden mezclar documentos de motores distintos: destino '{target.EngineName}', " +
                $"rama '{branch.EngineName}'. El formato de update no es intercambiable entre motores.",
                nameof(branch));
        }
        target.ApplyUpdate(branch.ExportState());
    }

    /// <summary>CRDT merge from a published version into a target live document.</summary>
    /// <exception cref="ArgumentException">The target belongs to a different engine than the store.</exception>
    public async ValueTask MergeAsync(ICrdtDoc target, VersionId branchVersion, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (target.EngineName != _engine.Name)
        {
            throw new ArgumentException(
                $"El documento destino pertenece al motor '{target.EngineName}', pero el almacén decodifica " +
                $"con '{_engine.Name}'. El formato de update no es intercambiable entre motores.",
                nameof(target));
        }
        byte[] blob = await LoadVerifiedAsync(branchVersion, ct).ConfigureAwait(false);
        target.ApplyUpdate(blob);
    }

    private async ValueTask<byte[]> LoadVerifiedAsync(VersionId version, CancellationToken ct)
    {
        byte[]? blob = await _blobs.GetAsync(version, ct).ConfigureAwait(false);
        if (blob is null)
        {
            throw new KeyNotFoundException($"No existe una versión publicada con id {version}.");
        }
        if (VersionId.FromBlob(blob) != version)
        {
            throw new BlobIntegrityException(
                $"El blob almacenado para {version} no verifica contra su hash (corrupción).");
        }
        return blob;
    }
}
