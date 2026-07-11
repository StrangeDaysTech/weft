using System.Threading.Channels;

namespace Weft.Concurrency;

/// <summary>Estado observable del actor de un documento (constitución P-V).</summary>
internal enum DocumentActorState
{
    /// <summary>Acepta y procesa operaciones.</summary>
    Active,

    /// <summary>Seleccionado para desalojo; drenando la cola pendiente antes de liberar.</summary>
    Idle,

    /// <summary>Terminado con normalidad; documento persistido (si había hook) y liberado.</summary>
    Evicted,

    /// <summary>Terminado por fallo irrecuperable; documento liberado sin persistir.</summary>
    Faulted,
}

/// <summary>
/// Serializa TODO el acceso a un <see cref="ICrdtDoc"/> nativo mediante un único lector que drena un
/// canal de operaciones (patrón actor, constitución P-V). Nunca ejecuta dos operaciones del mismo
/// documento a la vez; libera el documento exactamente una vez al terminar. <c>internal</c>: se usa a
/// través de <see cref="DocumentBroker"/> y <see cref="DocumentSession"/>.
/// </summary>
internal sealed class DocumentActor
{
    private readonly ICrdtDoc _doc;
    private readonly Channel<IWorkItem> _channel;
    private readonly Task _runLoop;
    private readonly Func<string, byte[], CancellationToken, ValueTask>? _onEvicting;
    private readonly object _sessionsLock = new();
    private readonly List<DocumentSession> _sessions = [];

    private volatile DocumentActorState _state = DocumentActorState.Active;
    private volatile Exception? _fault;
    private volatile bool _persistOnEnd = true;
    private long _lastActivityTick = Environment.TickCount64;

    internal DocumentActor(string docId, ICrdtDoc doc, Func<string, byte[], CancellationToken, ValueTask>? onEvicting)
    {
        DocId = docId;
        _doc = doc;
        _onEvicting = onEvicting;
        _channel = Channel.CreateUnbounded<IWorkItem>(new UnboundedChannelOptions
        {
            SingleReader = true,   // un único lector: la garantía de serialización (P-V)
            SingleWriter = false,  // varias sesiones/hilos encolan
        });
        _runLoop = Task.Run(RunAsync);
    }

    internal string DocId { get; }

    internal DocumentActorState State => _state;

    /// <summary>Milisegundos transcurridos desde la última operación procesada (para desalojo idle).</summary>
    internal long IdleMilliseconds => Environment.TickCount64 - Interlocked.Read(ref _lastActivityTick);

    internal int SessionCount
    {
        get { lock (_sessionsLock) { return _sessions.Count; } }
    }

    internal void AddSession(DocumentSession session)
    {
        lock (_sessionsLock) { _sessions.Add(session); }
    }

    internal void RemoveSession(DocumentSession session)
    {
        lock (_sessionsLock) { _sessions.Remove(session); }
    }

    /// <summary>
    /// Encola una operación sobre el documento y devuelve su resultado de forma asíncrona. La operación
    /// se ejecuta dentro del turno del actor (nunca concurrente con otra del mismo documento).
    /// </summary>
    internal ValueTask<T> EnqueueAsync<T>(Func<ICrdtDoc, T> op, bool mutating, CancellationToken ct)
    {
        var item = new WorkItem<T>(op, mutating, ct);
        if (!_channel.Writer.TryWrite(item))
        {
            item.Fail(ClosedReason());
        }
        return new ValueTask<T>(item.Task);
    }

    /// <summary>
    /// Inicia el desalojo cooperativo: no acepta más operaciones, drena las pendientes, persiste con el
    /// hook (si aplica) y libera el documento. El <see cref="Task"/> devuelto completa cuando el documento
    /// ha sido liberado.
    /// </summary>
    internal Task BeginEvictionAsync()
    {
        if (_state is DocumentActorState.Active or DocumentActorState.Idle)
        {
            _state = DocumentActorState.Idle;
            _channel.Writer.TryComplete();
        }
        return _runLoop;
    }

    private Exception ClosedReason() =>
        _fault ?? new ObjectDisposedException(nameof(DocumentSession),
            $"El documento '{DocId}' fue desalojado o el broker se cerró.");

    private async Task RunAsync()
    {
        try
        {
            await foreach (IWorkItem item in _channel.Reader.ReadAllAsync().ConfigureAwait(false))
            {
                if (_fault is not null)
                {
                    item.Fail(_fault); // tras faultear, drenamos la cola fallando cada pendiente
                    continue;
                }

                try
                {
                    bool wantUpdates = item.IsMutating && AnySessionWantsUpdates();
                    byte[]? stateVector = wantUpdates ? _doc.ExportStateVector() : null;

                    item.Execute(_doc);
                    Interlocked.Exchange(ref _lastActivityTick, Environment.TickCount64);

                    if (stateVector is not null)
                    {
                        byte[] delta = _doc.ExportUpdateSince(stateVector);
                        if (delta.Length > 0)
                        {
                            NotifySessions(delta);
                        }
                    }
                }
                catch (Exception ex)
                {
                    // La operación falló DENTRO del turno: el estado del documento puede ser inválido.
                    // El item ya propagó la excepción a su llamador; el actor entra en Faulted y drena.
                    _fault = ex;
                    _state = DocumentActorState.Faulted;
                    _channel.Writer.TryComplete();
                }
            }
        }
        finally
        {
            await FinalizeAsync().ConfigureAwait(false);
        }
    }

    private async ValueTask FinalizeAsync()
    {
        // Persistencia solo en desalojo grácil (no en fallo): drenar → OnEvicting → liberar.
        if (_state != DocumentActorState.Faulted)
        {
            if (_persistOnEnd && _onEvicting is not null)
            {
                try
                {
                    byte[] state = _doc.ExportState();
                    await _onEvicting(DocId, state, CancellationToken.None).ConfigureAwait(false);
                }
                catch
                {
                    // La persistencia es best-effort: si el hook falla, igual liberamos el documento
                    // (no dejar memoria nativa colgada prima sobre no perder el snapshot). El fallo del
                    // hook es responsabilidad del consumidor.
                }
            }
            _state = DocumentActorState.Evicted;
        }

        _doc.Dispose(); // exactamente una vez: el bucle termina una sola vez (P-I)
    }

    private bool AnySessionWantsUpdates()
    {
        lock (_sessionsLock)
        {
            foreach (DocumentSession s in _sessions)
            {
                if (s.WantsUpdates)
                {
                    return true;
                }
            }
        }
        return false;
    }

    private void NotifySessions(byte[] delta)
    {
        DocumentSession[] snapshot;
        lock (_sessionsLock)
        {
            if (_sessions.Count == 0)
            {
                return;
            }
            snapshot = _sessions.ToArray();
        }
        var mem = new ReadOnlyMemory<byte>(delta);
        foreach (DocumentSession s in snapshot)
        {
            s.RaiseUpdateApplied(mem);
        }
    }

    // -- Unidades de trabajo encoladas --

    private interface IWorkItem
    {
        bool IsMutating { get; }
        void Execute(ICrdtDoc doc);
        void Fail(Exception ex);
    }

    private sealed class WorkItem<T> : IWorkItem
    {
        private readonly Func<ICrdtDoc, T> _op;
        private readonly TaskCompletionSource<T> _tcs =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly CancellationToken _ct;

        internal WorkItem(Func<ICrdtDoc, T> op, bool mutating, CancellationToken ct)
        {
            _op = op;
            IsMutating = mutating;
            _ct = ct;
        }

        public bool IsMutating { get; }

        internal Task<T> Task => _tcs.Task;

        public void Execute(ICrdtDoc doc)
        {
            if (_ct.IsCancellationRequested)
            {
                _tcs.TrySetCanceled(_ct); // cancelación no faultea el actor
                return;
            }
            try
            {
                _tcs.TrySetResult(_op(doc));
            }
            catch (Exception ex)
            {
                _tcs.TrySetException(ex); // el llamador ve la excepción causal...
                throw;                    // ...y el actor faultea (turno abortado)
            }
        }

        public void Fail(Exception ex) => _tcs.TrySetException(ex);
    }
}
