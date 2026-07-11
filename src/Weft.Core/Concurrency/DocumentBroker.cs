namespace Weft.Concurrency;

/// <summary>
/// Gestiona documentos activos con acceso serializado por documento (un actor/canal por <c>docId</c>,
/// constitución P-V). Thread-safe y el único camino soportado para compartir un documento entre hilos.
/// Registra y reutiliza actores por identidad, los desaloja por inactividad y por presión de memoria
/// (LRU), y libera los recursos nativos de forma determinista.
/// </summary>
/// <remarks>
/// El límite <see cref="DocumentBrokerOptions.MaxActiveDocuments"/> es "suave": se reafirma en el barrido
/// periódico, no de forma síncrona en <see cref="OpenAsync"/>, y nunca desaloja un documento con sesiones
/// vivas. Puede excederse transitoriamente bajo ráfagas de aperturas o cuando todos los documentos activos
/// tienen sesiones abiertas.
/// </remarks>
public sealed class DocumentBroker : IAsyncDisposable
{
    private readonly ICrdtEngine _engine;
    private readonly DocumentBrokerOptions _options;
    private readonly object _gate = new();
    private readonly Dictionary<string, DocumentActor> _actors = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Task<DocumentActor>> _loading = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Task> _evicting = new(StringComparer.Ordinal);
    private readonly CancellationTokenSource _shutdown = new();
    private readonly Task _sweeper;
    private bool _disposed;

    /// <summary>Crea el broker sobre un motor CRDT y opciones de ciclo de vida (por defecto si se omiten).</summary>
    public DocumentBroker(ICrdtEngine engine, DocumentBrokerOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(engine);
        _engine = engine;
        _options = options ?? new DocumentBrokerOptions();
        _sweeper = Task.Run(SweepLoopAsync);
    }

    /// <summary>Número de documentos actualmente activos (registrados).</summary>
    public int ActiveDocumentCount
    {
        get { lock (_gate) { return _actors.Count; } }
    }

    /// <summary>
    /// Abre (o reutiliza) el documento <paramref name="docId"/>. Si no está activo, lo carga con
    /// <paramref name="loader"/> (un <c>loader</c> que devuelve <c>null</c>/vacío ⇒ documento nuevo).
    /// Devuelve una <see cref="DocumentSession"/> para operarlo de forma asíncrona.
    /// </summary>
    public async ValueTask<DocumentSession> OpenAsync(
        string docId,
        Func<string, CancellationToken, ValueTask<byte[]?>>? loader = null,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(docId);
        ObjectDisposedException.ThrowIf(_disposed, this);

        while (true)
        {
            DocumentActor? existing;
            Task<DocumentActor> loadTask;
            Task? inflightEviction = null;
            bool startedLoad = false;
            lock (_gate)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                if (_evicting.TryGetValue(docId, out Task? eviction))
                {
                    // Hay un desalojo de este documento en vuelo: esperar a que persista su estado antes
                    // de cargar, o cargaríamos un snapshot a medio escribir (updates perdidos, SC-006).
                    existing = null;
                    loadTask = null!;
                    inflightEviction = eviction;
                }
                else if (_actors.TryGetValue(docId, out DocumentActor? found)
                    && found.State is DocumentActorState.Active or DocumentActorState.Idle)
                {
                    existing = found;
                    loadTask = System.Threading.Tasks.Task.FromResult(found);
                }
                else
                {
                    // Un actor terminado (Faulted/Evicted) que quedó registrado se descarta para recrearlo
                    // limpio — nunca reintentar sobre él (evita el giro infinito sobre un actor muerto).
                    if (found is not null)
                    {
                        _actors.Remove(docId);
                    }
                    existing = null;
                    if (_loading.TryGetValue(docId, out Task<DocumentActor>? pending))
                    {
                        loadTask = pending;
                    }
                    else
                    {
                        loadTask = LoadAndRegisterAsync(docId, loader, ct);
                        _loading[docId] = loadTask;
                        startedLoad = true;
                    }
                }
            }

            if (inflightEviction is not null)
            {
                try { await inflightEviction.ConfigureAwait(false); } catch { /* el desalojo reporta aparte */ }
                continue; // el estado ya está persistido; reintentar cargará el snapshot correcto
            }

            DocumentActor actor;
            try
            {
                actor = existing ?? await loadTask.ConfigureAwait(false);
            }
            finally
            {
                // Solo el iniciador retira la entrada de carga, y SIEMPRE después del await: si el loader
                // completa síncronamente, la carga se registra en _actors dentro de este mismo lock, pero
                // _loading NO debe quedar con una entrada rancia (esa era la causa del livelock en reopen).
                if (startedLoad)
                {
                    lock (_gate)
                    {
                        _loading.Remove(docId);
                    }
                }
            }

            // Añadir la sesión atómicamente respecto al barrido: solo si el actor sigue registrado y
            // no terminó. Si fue desalojado en la ventana, reintentar (reabrirá o reutilizará).
            lock (_gate)
            {
                if (_actors.TryGetValue(docId, out DocumentActor? still)
                    && ReferenceEquals(still, actor)
                    && actor.State is DocumentActorState.Active or DocumentActorState.Idle)
                {
                    var session = new DocumentSession(actor);
                    actor.AddSession(session);
                    return session;
                }
            }
            // desalojado entre carga y registro de la sesión (raro): ceder y reintentar sin quemar CPU
            await System.Threading.Tasks.Task.Yield();
        }
    }

    private async Task<DocumentActor> LoadAndRegisterAsync(
        string docId,
        Func<string, CancellationToken, ValueTask<byte[]?>>? loader,
        CancellationToken ct)
    {
        // No toca `_loading`: su ciclo de vida lo gestiona OpenAsync (alta dentro del lock, baja en el
        // finally tras el await). Así se evita la entrada rancia cuando el loader completa síncronamente.
        byte[]? initial = loader is not null ? await loader(docId, ct).ConfigureAwait(false) : null;
        ICrdtDoc doc = initial is { Length: > 0 } ? _engine.LoadDoc(initial) : _engine.CreateDoc();
        var actor = new DocumentActor(docId, doc, _options.OnEvicting);

        lock (_gate)
        {
            if (_disposed)
            {
                _ = actor.BeginEvictionAsync(); // broker cerrado durante la carga: liberar el actor nuevo
                throw new ObjectDisposedException(nameof(DocumentBroker));
            }
            _actors[docId] = actor;
        }
        return actor;
    }

    private async Task SweepLoopAsync()
    {
        try
        {
            using var timer = new PeriodicTimer(_options.ResolveSweepInterval());
            while (await timer.WaitForNextTickAsync(_shutdown.Token).ConfigureAwait(false))
            {
                try
                {
                    await SweepOnceAsync().ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // un barrido fallido no debe matar el barrido de fondo (quedaría sin desalojar nunca).
                    System.Diagnostics.Debug.WriteLine($"[DocumentBroker] barrido falló: {ex}");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // shutdown
        }
    }

    /// <summary>Un pase de desalojo: inactividad (idle) + presión de memoria (LRU). Visible para tests.</summary>
    internal async ValueTask SweepOnceAsync()
    {
        List<Task> evictions = [];
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            List<DocumentActor> toEvict = [];
            long idleThreshold = (long)_options.IdleEviction.TotalMilliseconds;
            foreach (DocumentActor a in _actors.Values)
            {
                bool terminated = a.State is DocumentActorState.Faulted or DocumentActorState.Evicted;
                bool idle = a.SessionCount == 0 && a.IdleMilliseconds >= idleThreshold;
                if (terminated || idle)
                {
                    toEvict.Add(a);
                }
            }

            int remaining = _actors.Count - toEvict.Count;
            int over = remaining - _options.MaxActiveDocuments;
            if (over > 0)
            {
                // Presión de memoria: desalojar los menos recientemente usados SIN sesión, aunque estén
                // "tibios". El orden por inactividad descendente protege a los recién usados/creados; si
                // alguno se desaloja en la ventana previa a su primera sesión, OpenAsync reintenta.
                List<DocumentActor> lru = _actors.Values
                    .Where(a => !toEvict.Contains(a) && a.SessionCount == 0)
                    .OrderByDescending(a => a.IdleMilliseconds)
                    .Take(over)
                    .ToList();
                toEvict.AddRange(lru);
            }

            foreach (DocumentActor a in toEvict)
            {
                _actors.Remove(a.DocId);
                Task eviction = EvictActorAsync(a);
                _evicting[a.DocId] = eviction; // los OpenAsync concurrentes esperan a que persista
                evictions.Add(eviction);
            }
        }

        // Esperar a que los desalojos que este barrido inició terminen (persistencia incluida). Da
        // determinismo a los tests; en el barrido de fondo solo pausa hasta el siguiente tick.
        await Task.WhenAll(evictions).ConfigureAwait(false);
    }

    private async Task EvictActorAsync(DocumentActor actor)
    {
        await System.Threading.Tasks.Task.Yield(); // no completar síncronamente: _evicting se asigna antes del finally
        try
        {
            await actor.BeginEvictionAsync().ConfigureAwait(false);
        }
        catch
        {
            // el desalojo de un actor no debe tumbar el barrido de los demás
        }
        finally
        {
            lock (_gate)
            {
                _evicting.Remove(actor.DocId);
            }
        }
    }

    /// <summary>Drena y libera todos los documentos exactamente una vez; detiene el barrido.</summary>
    public async ValueTask DisposeAsync()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
        }

        _shutdown.Cancel();
        try
        {
            await _sweeper.ConfigureAwait(false);
        }
        catch
        {
            // el sweeper ya está terminando
        }

        // Esperar los desalojos en vuelo (persisten estado) antes de drenar el resto.
        Task[] inflight;
        List<DocumentActor> all;
        lock (_gate)
        {
            inflight = [.. _evicting.Values];
            all = [.. _actors.Values];
            _actors.Clear();
        }
        try
        {
            await Task.WhenAll(inflight).ConfigureAwait(false);
        }
        catch
        {
            // cada desalojo reporta su propio fallo; no bloquear el cierre
        }

        foreach (DocumentActor a in all)
        {
            try
            {
                await a.BeginEvictionAsync().ConfigureAwait(false);
            }
            catch
            {
                // liberar el resto pese a un fallo aislado
            }
        }

        _shutdown.Dispose();
    }
}
