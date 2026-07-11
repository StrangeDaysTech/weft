using System.Text;
using Weft.Concurrency;
using Weft.Yrs;

namespace Weft.Core.Tests;

/// <summary>
/// Contrato de concurrencia del <see cref="DocumentBroker"/> (T040, US2, constitución P-V): serialización
/// estricta por documento, orden FIFO por sesión, desalojo→persistencia→reapertura, propagación de fallo y
/// semántica de dispose. Los casos de serialización/fallo usan un motor de prueba que instrumenta la
/// concurrencia; los de ciclo de vida usan el motor yrs real.
/// </summary>
public sealed class DocumentBrokerTests
{
    // -- Serialización (Acceptance Scenario 1): nunca dos operaciones simultáneas del mismo documento --

    [Fact]
    public async Task Operations_on_same_document_never_run_concurrently()
    {
        var engine = new TrackingEngine();
        await using var broker = new DocumentBroker(engine);
        await using DocumentSession session = await broker.OpenAsync("doc");

        Task[] writers = Enumerable.Range(0, 50).Select(_ => Task.Run(async () =>
        {
            for (int k = 0; k < 20; k++)
            {
                await session.InsertTextAsync("body", 0, "x");
            }
        })).ToArray();
        await Task.WhenAll(writers);

        TrackingDoc doc = Assert.Single(engine.Docs);
        Assert.Equal(1, doc.PeakConcurrency);            // el actor serializó todo el acceso
        Assert.Equal(50 * 20, (await session.GetTextAsync("body")).Length);
    }

    // -- FIFO por sesión: las operaciones encoladas se aplican en el orden de encolado --

    [Fact]
    public async Task Operations_from_a_session_apply_in_FIFO_order()
    {
        await using var broker = new DocumentBroker(YrsEngine.Instance);
        await using DocumentSession session = await broker.OpenAsync("doc");

        var pending = new List<Task>();
        for (int i = 0; i < 10; i++)
        {
            // encolado síncrono en orden; inserta el dígito i en la posición i
            pending.Add(session.InsertTextAsync("body", i, i.ToString()).AsTask());
        }
        await Task.WhenAll(pending);

        Assert.Equal("0123456789", await session.GetTextAsync("body"));
    }

    // -- Acceptance Scenario 2: desalojo por inactividad → OnEvicting → reapertura desde lo persistido --

    [Fact]
    public async Task Idle_document_is_evicted_persisted_and_can_be_reopened()
    {
        byte[]? persisted = null;
        int evictions = 0;
        var options = new DocumentBrokerOptions
        {
            IdleEviction = TimeSpan.FromMilliseconds(20),
            IdleSweepInterval = TimeSpan.FromHours(1), // barrido automático desactivado: lo disparamos manual
            OnEvicting = (id, state, ct) =>
            {
                persisted = state;
                Interlocked.Increment(ref evictions);
                return ValueTask.CompletedTask;
            },
        };
        await using var broker = new DocumentBroker(YrsEngine.Instance, options);

        DocumentSession session = await broker.OpenAsync("doc-1");
        await session.InsertTextAsync("body", 0, "hola");
        await session.DisposeAsync();          // sin sesiones vivas → candidato a desalojo
        await Task.Delay(60);                  // superar IdleEviction (20ms)
        await broker.SweepOnceAsync();         // fuerza el barrido (no esperar al timer)

        Assert.Equal(1, evictions);
        Assert.NotNull(persisted);
        Assert.Equal(0, broker.ActiveDocumentCount);

        // Reabrir con un loader que devuelve el estado persistido → contenido restaurado.
        await using DocumentSession reopened = await broker.OpenAsync(
            "doc-1", (id, ct) => ValueTask.FromResult<byte[]?>(persisted));
        Assert.Equal("hola", await reopened.GetTextAsync("body"));
    }

    // -- LRU: al superar el máximo, se desaloja el menos recientemente usado (sin sesiones vivas) --

    [Fact]
    public async Task Over_capacity_evicts_least_recently_used_without_sessions()
    {
        var options = new DocumentBrokerOptions
        {
            MaxActiveDocuments = 2,
            IdleEviction = TimeSpan.FromHours(1), // aislar: solo queremos ver el desalojo por LRU
        };
        await using var broker = new DocumentBroker(YrsEngine.Instance, options);

        // Tres documentos, cerrando cada sesión (sin sesiones vivas → elegibles para LRU). El delay
        // separa los timestamps de inactividad, así 'a' es el menos recientemente usado.
        foreach (string id in new[] { "a", "b", "c" })
        {
            DocumentSession s = await broker.OpenAsync(id);
            await s.InsertTextAsync("body", 0, id);
            await s.DisposeAsync();
            await Task.Delay(EvictionGrace);
        }

        Assert.Equal(3, broker.ActiveDocumentCount); // el límite es suave hasta el barrido
        await broker.SweepOnceAsync();
        Assert.Equal(2, broker.ActiveDocumentCount); // 'a' (LRU) desalojado

        // Verificar la IDENTIDAD del desalojado, no solo el conteo: 'a' (LRU, sin OnEvicting/loader) se
        // perdió → reabrir da documento vacío; 'b' y 'c' siguen activos con su contenido. Un LRU que
        // hubiera desalojado el MRU pasaría el assert de conteo pero fallaría estos.
        await using (DocumentSession ra = await broker.OpenAsync("a"))
        {
            Assert.Equal("", await ra.GetTextAsync("body"));
        }
        await using (DocumentSession rb = await broker.OpenAsync("b"))
        {
            Assert.Equal("b", await rb.GetTextAsync("body"));
        }
        await using (DocumentSession rc = await broker.OpenAsync("c"))
        {
            Assert.Equal("c", await rc.GetTextAsync("body"));
        }
    }

    // -- Un handler de UpdateApplied que lanza NO debe faultear el actor (finding G, para M2) --

    [Fact]
    public async Task Throwing_UpdateApplied_handler_does_not_fault_the_actor()
    {
        await using var broker = new DocumentBroker(YrsEngine.Instance);
        await using DocumentSession session = await broker.OpenAsync("doc");
        session.UpdateApplied += (_, _) => throw new InvalidOperationException("handler boom");

        // La inserción dispara UpdateApplied → el handler lanza, pero el actor debe seguir sano.
        await session.InsertTextAsync("body", 0, "hola");
        Assert.Equal("hola", await session.GetTextAsync("body"));

        // Operaciones subsiguientes siguen funcionando (el actor no faulteó).
        await session.InsertTextAsync("body", 0, "! ");
        Assert.Equal("! hola", await session.GetTextAsync("body"));
    }

    // -- UpdateApplied se dispara con un delta aplicable, para otras sesiones del mismo doc (finding J) --

    [Fact]
    public async Task UpdateApplied_fires_with_applicable_delta_for_other_sessions()
    {
        await using var broker = new DocumentBroker(YrsEngine.Instance);
        await using DocumentSession writer = await broker.OpenAsync("doc");
        await using DocumentSession observer = await broker.OpenAsync("doc"); // misma doc, otra sesión

        var gotDelta = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        observer.UpdateApplied += (_, delta) => gotDelta.TrySetResult(delta.ToArray());

        await writer.InsertTextAsync("body", 0, "hola");

        byte[] applied = await gotDelta.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.NotEmpty(applied);

        // El delta es aplicable a un documento fresco y reproduce el texto (superficie de relay para M2).
        using ICrdtDoc fresh = YrsEngine.Instance.CreateDoc();
        fresh.ApplyUpdate(applied);
        Assert.Equal("hola", fresh.GetText("body"));
    }

    // -- Actor en fallo irrecuperable: propaga la excepción causal a las operaciones pendientes/futuras --

    [Fact]
    public async Task Faulted_actor_propagates_causal_exception()
    {
        await using var broker = new DocumentBroker(YrsEngine.Instance);
        await using DocumentSession session = await broker.OpenAsync("doc");

        var boom = new InvalidOperationException("boom");
        var gate = new TaskCompletionSource();

        // Turno que bloquea el actor hasta 'gate' y luego lanza: garantiza que 'pending' quede encolada
        // DETRÁS antes de que el fallo ocurra (test determinista).
        Task<int> faulting = session.ExecuteAsync<int>(_ => { gate.Task.Wait(); throw boom; }).AsTask();
        Task<string> pending = session.GetTextAsync("body").AsTask();
        gate.SetResult();

        InvalidOperationException fromFaulting =
            await Assert.ThrowsAsync<InvalidOperationException>(() => faulting);
        InvalidOperationException fromPending =
            await Assert.ThrowsAsync<InvalidOperationException>(() => pending);
        Assert.Same(boom, fromFaulting);
        Assert.Same(boom, fromPending);

        // Operaciones futuras también fallan con la misma causal.
        InvalidOperationException fromFuture =
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await session.GetTextAsync("body"));
        Assert.Same(boom, fromFuture);
    }

    // -- Dispose semantics: error predecible de la plataforma, nunca crash (Acceptance Scenario 3) --

    [Fact]
    public async Task Using_a_disposed_session_throws_ObjectDisposedException()
    {
        await using var broker = new DocumentBroker(YrsEngine.Instance);
        DocumentSession session = await broker.OpenAsync("doc");
        await session.DisposeAsync();

        await Assert.ThrowsAsync<ObjectDisposedException>(
            async () => await session.InsertTextAsync("body", 0, "x"));
    }

    [Fact]
    public async Task Operations_after_broker_dispose_fail_predictably()
    {
        var broker = new DocumentBroker(YrsEngine.Instance);
        DocumentSession session = await broker.OpenAsync("doc");
        await broker.DisposeAsync();

        await Assert.ThrowsAsync<ObjectDisposedException>(
            async () => await session.GetTextAsync("body"));
        await Assert.ThrowsAsync<ObjectDisposedException>(
            async () => await broker.OpenAsync("other"));
    }

    private static readonly TimeSpan EvictionGrace = TimeSpan.FromMilliseconds(300);

    // -- Motor de prueba que instrumenta la concurrencia observada por documento --

    private sealed class TrackingEngine : ICrdtEngine
    {
        public List<TrackingDoc> Docs { get; } = [];
        public string Name => "tracking";
        public INativeVersioning? NativeVersioning => null;

        public ICrdtDoc CreateDoc()
        {
            var doc = new TrackingDoc();
            lock (Docs) { Docs.Add(doc); }
            return doc;
        }

        public ICrdtDoc LoadDoc(ReadOnlySpan<byte> blob)
        {
            var doc = new TrackingDoc(blob);
            lock (Docs) { Docs.Add(doc); }
            return doc;
        }
    }

    private sealed class TrackingDoc : ICrdtDoc
    {
        private readonly StringBuilder _text = new();
        private int _inside;
        private int _peak;

        public TrackingDoc() { }

        public TrackingDoc(ReadOnlySpan<byte> blob) => _text.Append(Encoding.UTF8.GetString(blob));

        public int PeakConcurrency => Volatile.Read(ref _peak);

        public string EngineName => "tracking";

        public void InsertText(string field, int index, string text) =>
            Guarded(() => _text.Insert(index, text));

        public void DeleteText(string field, int index, int length) =>
            Guarded(() => _text.Remove(index, length));

        public string GetText(string field) => Guarded(() => _text.ToString());

        public byte[] ExportState() => Guarded(() => Encoding.UTF8.GetBytes(_text.ToString()));

        public byte[] ExportStateVector() => [];

        public byte[] ExportUpdateSince(ReadOnlySpan<byte> stateVector) => ExportState();

        public void ApplyUpdate(ReadOnlySpan<byte> update)
        {
            byte[] copy = update.ToArray();
            Guarded(() => _text.Append(Encoding.UTF8.GetString(copy)));
        }

        public void Dispose() { }

        private void Guarded(Action body) => Guarded(() => { body(); return true; });

        private T Guarded<T>(Func<T> body)
        {
            int now = Interlocked.Increment(ref _inside);
            InterlockedMax(ref _peak, now);
            try
            {
                Thread.SpinWait(100); // ensanchar la ventana para delatar cualquier solape
                return body();
            }
            finally
            {
                Interlocked.Decrement(ref _inside);
            }
        }

        private static void InterlockedMax(ref int target, int value)
        {
            int current;
            do
            {
                current = Volatile.Read(ref target);
                if (value <= current)
                {
                    return;
                }
            }
            while (Interlocked.CompareExchange(ref target, value, current) != current);
        }
    }
}
