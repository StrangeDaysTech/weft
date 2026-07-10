using Weft;
using Weft.Versioning.Blobs;

namespace Weft.Versioning;

/// <summary>
/// Publicar/cargar/comparar/ramificar/mezclar versiones de documentos, content-addressed y
/// engine-agnóstico (constitución P-IV: depende solo de las abstracciones de Weft.Core).
/// Thread-safe (sin estado mutable propio; la serialización del doc vivo es responsabilidad del
/// llamador o del DocumentBroker).
/// </summary>
public sealed class VersionStore
{
    private readonly ICrdtEngine _engine;
    private readonly IBlobStore _blobs;

    /// <summary>Crea el almacén de versiones sobre un motor y un almacén de blobs.</summary>
    public VersionStore(ICrdtEngine engine, IBlobStore blobs)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(blobs);
        _engine = engine;
        _blobs = blobs;
    }

    /// <summary>Exporta, hashea y persiste el documento. Devuelve la identidad citable.</summary>
    public async ValueTask<VersionId> PublishAsync(ICrdtDoc doc, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(doc);
        byte[] blob = doc.ExportState();
        var id = VersionId.FromBlob(blob);
        await _blobs.PutAsync(id, blob, ct).ConfigureAwait(false);
        return id;
    }

    /// <summary>Reconstruye un documento vivo desde una versión publicada (verifica integridad).</summary>
    /// <exception cref="KeyNotFoundException">La versión no existe en el almacén.</exception>
    /// <exception cref="BlobIntegrityException">El blob almacenado no verifica contra su hash.</exception>
    public async ValueTask<ICrdtDoc> CheckoutAsync(VersionId version, CancellationToken ct = default)
    {
        byte[] blob = await LoadVerifiedAsync(version, ct).ConfigureAwait(false);
        return _engine.LoadDoc(blob);
    }

    /// <summary>Diff de texto por palabras entre dos versiones publicadas, en un campo dado.</summary>
    public async ValueTask<TextDiff> DiffAsync(VersionId a, VersionId b, string field, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(field);
        using ICrdtDoc da = await CheckoutAsync(a, ct).ConfigureAwait(false);
        using ICrdtDoc db = await CheckoutAsync(b, ct).ConfigureAwait(false);
        return TextDiff.Compute(da.GetText(field), db.GetText(field));
    }

    /// <summary>Rama: documento vivo independiente partiendo de la versión base (alias de Checkout).</summary>
    public ValueTask<ICrdtDoc> BranchAsync(VersionId from, CancellationToken ct = default) =>
        CheckoutAsync(from, ct);

    /// <summary>Merge CRDT: importa el estado de la rama en el destino (convergente, sin conflictos).</summary>
    public void Merge(ICrdtDoc target, ICrdtDoc branch)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(branch);
        target.ApplyUpdate(branch.ExportState());
    }

    /// <summary>Merge CRDT desde una versión publicada hacia un documento vivo destino.</summary>
    public async ValueTask MergeAsync(ICrdtDoc target, VersionId branchVersion, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(target);
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
