namespace Weft.Concurrency;

/// <summary>
/// Fachada asíncrona de un documento gestionado por el <see cref="DocumentBroker"/>. Espejo de
/// <see cref="ICrdtDoc"/> donde cada llamada se encola al actor del documento y se ejecuta serializada
/// (constitución P-V). Varias sesiones pueden compartir el mismo documento; todas reciben el evento
/// <see cref="UpdateApplied"/>. No expone el <see cref="ICrdtDoc"/> subyacente salvo, transitoriamente,
/// dentro del delegado de <see cref="ExecuteAsync{T}"/>.
/// </summary>
public sealed class DocumentSession : IAsyncDisposable
{
    private readonly DocumentActor _actor;
    private bool _disposed;

    internal DocumentSession(DocumentActor actor)
    {
        _actor = actor;
        DocId = actor.DocId;
    }

    /// <summary>Identificador lógico del documento.</summary>
    public string DocId { get; }

    /// <summary>
    /// Se dispara tras cada update aplicado al documento (propio o importado por otra sesión), con el
    /// delta correspondiente. Pensado para relay/persistencia (M2). El handler se invoca dentro del turno
    /// del actor: no debe bloquear esperando otra operación del mismo documento.
    /// </summary>
    public event Action<DocumentSession, ReadOnlyMemory<byte>>? UpdateApplied;

    /// <summary>Inserta texto en el campo indicado (encolado y serializado).</summary>
    public async ValueTask InsertTextAsync(string field, int index, string text, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(field);
        ArgumentNullException.ThrowIfNull(text);
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ThrowIfDisposed();
        await _actor.EnqueueAsync(doc => { doc.InsertText(field, index, text); return true; }, mutating: true, ct)
            .ConfigureAwait(false);
    }

    /// <summary>Borra texto del campo indicado (encolado y serializado).</summary>
    public async ValueTask DeleteTextAsync(string field, int index, int length, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(field);
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        ThrowIfDisposed();
        await _actor.EnqueueAsync(doc => { doc.DeleteText(field, index, length); return true; }, mutating: true, ct)
            .ConfigureAwait(false);
    }

    /// <summary>Lee el contenido completo del campo indicado.</summary>
    public ValueTask<string> GetTextAsync(string field, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(field);
        ThrowIfDisposed();
        return _actor.EnqueueAsync(doc => doc.GetText(field), mutating: false, ct);
    }

    /// <summary>Exporta el estado completo del documento (base del content-addressing).</summary>
    public ValueTask<byte[]> ExportStateAsync(CancellationToken ct = default)
    {
        ThrowIfDisposed();
        return _actor.EnqueueAsync(doc => doc.ExportState(), mutating: false, ct);
    }

    /// <summary>Exporta el state vector ("qué conozco") para sync incremental.</summary>
    public ValueTask<byte[]> ExportStateVectorAsync(CancellationToken ct = default)
    {
        ThrowIfDisposed();
        return _actor.EnqueueAsync(doc => doc.ExportStateVector(), mutating: false, ct);
    }

    /// <summary>Exporta el delta con los cambios que el emisor del state vector no conoce.</summary>
    public ValueTask<byte[]> ExportUpdateSinceAsync(ReadOnlyMemory<byte> stateVector, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        byte[] sv = stateVector.ToArray(); // copia defensiva: el buffer del llamador puede cambiar antes del turno
        return _actor.EnqueueAsync(doc => doc.ExportUpdateSince(sv), mutating: false, ct);
    }

    /// <summary>Fusiona un update/estado de otra réplica (convergente); dispara <see cref="UpdateApplied"/>.</summary>
    public async ValueTask ApplyUpdateAsync(ReadOnlyMemory<byte> update, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        byte[] u = update.ToArray(); // copia defensiva (ver ExportUpdateSinceAsync)
        await _actor.EnqueueAsync(doc => { doc.ApplyUpdate(u); return true; }, mutating: true, ct)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Ejecuta un delegado como turno atómico respecto a las demás operaciones del mismo documento
    /// (transacción lógica). El <see cref="ICrdtDoc"/> recibido NO debe capturarse ni usarse fuera del
    /// delegado: solo es válido durante la ejecución del turno.
    /// </summary>
    public ValueTask<T> ExecuteAsync<T>(Func<ICrdtDoc, T> operation, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ThrowIfDisposed();
        return _actor.EnqueueAsync(operation, mutating: true, ct);
    }

    internal bool WantsUpdates => UpdateApplied is not null;

    internal void RaiseUpdateApplied(ReadOnlyMemory<byte> delta) => UpdateApplied?.Invoke(this, delta);

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

    /// <summary>Cierra la sesión: deja de recibir eventos y libera su referencia en el actor. No desaloja
    /// el documento (su ciclo de vida lo gestiona el broker por inactividad/LRU).</summary>
    public ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return ValueTask.CompletedTask;
        }
        _disposed = true;
        UpdateApplied = null;
        _actor.RemoveSession(this);
        return ValueTask.CompletedTask;
    }
}
