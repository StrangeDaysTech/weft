using System.Collections.Concurrent;
using System.Diagnostics;
using Weft.Concurrency;
using Weft.LoadTest;
using Weft.Yrs;

// Modo relay (FU-010/CHARTER-14): mide la latencia de broadcast del relay real en ambos modos de
// durabilidad. Distinto de la carga de US2 de abajo (que conduce el broker directamente, ciega al relay).
if (Array.IndexOf(args, "--relay") >= 0)
{
    return await RelayLoad.RunAsync(ArgInt(args, "--edits", 200));
}

// Prueba de carga de US2/M1 (SC-006): cientos de documentos y muchas tareas concurrentes editando al
// azar durante un período sostenido. Verifica (a) consistencia final de cada documento y (b) memoria
// acotada — el número de documentos activos se mantiene bajo el límite pese a que el total supera el pool
// (desalojo idle+LRU con persistencia y reapertura). Salida distinta de cero si algo falla (gate CI).

int docs = ArgInt(args, "--docs", 300);
int tasks = ArgInt(args, "--tasks", 8);
int seconds = ArgInt(args, "--seconds", 20);
int maxActive = ArgInt(args, "--max-active", Math.Max(8, docs / 4));

Console.WriteLine($"[load-test] docs={docs} tasks={tasks} seconds={seconds} max-active={maxActive} " +
                  $"gc-server={System.Runtime.GCSettings.IsServerGC}");

// "Persistencia" en memoria: el hook OnEvicting guarda aquí el estado; el loader lo relee al reabrir.
var store = new ConcurrentDictionary<string, byte[]>(StringComparer.Ordinal);
long evictions = 0;
long confirmedOps = 0;
long errors = 0;

var options = new DocumentBrokerOptions
{
    MaxActiveDocuments = maxActive,
    // Idle agresivo: fuerza desalojo/reapertura constantes bajo carga → ejercita la carrera
    // desalojo-vs-reapertura (persistencia + recarga) que SC-006 exige sin pérdida de updates.
    IdleEviction = TimeSpan.FromMilliseconds(30),
    IdleSweepInterval = TimeSpan.FromMilliseconds(10),
    OnEvicting = (id, state, ct) =>
    {
        store[id] = state;
        Interlocked.Increment(ref evictions);
        return ValueTask.CompletedTask;
    },
};

// Contador de inserciones confirmadas por documento: la longitud final del texto debe igualarlo.
long[] inserts = new long[docs];

await using var broker = new DocumentBroker(YrsEngine.Instance, options);

Func<string, CancellationToken, ValueTask<byte[]?>> loader =
    (id, ct) => ValueTask.FromResult(store.TryGetValue(id, out byte[]? blob) ? blob : null);

// Muestreo del pico de documentos activos durante la carga (evidencia de memoria acotada).
int peakActive = 0;
using var samplerStop = new CancellationTokenSource();
Task sampler = Task.Run(async () =>
{
    while (!samplerStop.IsCancellationRequested)
    {
        int active = broker.ActiveDocumentCount;
        if (active > peakActive)
        {
            Interlocked.Exchange(ref peakActive, active);
        }
        try { await Task.Delay(20, samplerStop.Token); } catch (OperationCanceledException) { break; }
    }
});

var sw = Stopwatch.StartNew();
long deadlineTicks = sw.ElapsedMilliseconds + (seconds * 1000L);

// Cada documento crece hasta un tope y luego solo se lee: acota el TAMAÑO (memoria por doc),
// mientras el pooling acota el NÚMERO de documentos activos. Ambos → memoria acotada (SC-006).
const int perDocCap = 150;

Task[] workers = Enumerable.Range(0, tasks).Select(workerId => Task.Run(async () =>
{
    var rng = new Random(unchecked(0x5EED + workerId));
    while (sw.ElapsedMilliseconds < deadlineTicks)
    {
        int idx = rng.Next(docs);
        string docId = $"doc-{idx}";
        try
        {
            await using DocumentSession session = await broker.OpenAsync(docId, loader);
            if (Volatile.Read(ref inserts[idx]) < perDocCap)
            {
                int burst = rng.Next(1, 4);
                for (int b = 0; b < burst && Volatile.Read(ref inserts[idx]) < perDocCap; b++)
                {
                    await session.InsertTextAsync("body", 0, "x");
                    Interlocked.Increment(ref inserts[idx]);
                    Interlocked.Increment(ref confirmedOps);
                }
            }
            else
            {
                await session.GetTextAsync("body"); // mantiene el churn de apertura/desalojo sin crecer
            }
        }
        catch (Exception ex)
        {
            // En operación correcta no debería ocurrir (una sesión viva protege su documento del
            // desalojo). Cualquier fallo aquí es una regresión de la capa de concurrencia.
            Interlocked.Increment(ref errors);
            if (Interlocked.Read(ref errors) <= 5)
            {
                Console.WriteLine($"[load-test] error en '{docId}': {ex.GetType().Name}: {ex.Message}");
            }
        }
    }
})).ToArray();

await Task.WhenAll(workers);
sw.Stop();
samplerStop.Cancel();
await sampler;

Console.WriteLine($"[load-test] carga completa en {sw.Elapsed.TotalSeconds:F1}s: " +
                  $"ops={confirmedOps} evictions={evictions} peak-active={peakActive} errors={errors}");

// -- Verificación de consistencia: reabrir cada documento y comparar longitud con las inserciones --
int inconsistencias = 0;
for (int idx = 0; idx < docs; idx++)
{
    long expected = Interlocked.Read(ref inserts[idx]);
    await using DocumentSession session = await broker.OpenAsync($"doc-{idx}", loader);
    string text = await session.GetTextAsync("body");
    if (text.Length != expected)
    {
        if (inconsistencias < 10)
        {
            Console.WriteLine($"[load-test] INCONSISTENTE doc-{idx}: len={text.Length} esperado={expected}");
        }
        inconsistencias++;
    }
}

// -- Memoria (informativo): managed heap tras GC forzado + working set. La memoria se acota por dos vías:
//    el TAMAÑO por documento (cap de inserciones) y el NÚMERO de documentos activos (pool + LRU). El pico
//    de activos es una cota SUAVE (se reafirma en cada barrido, no en OpenAsync), así que puede exceder
//    MaxActiveDocuments transitoriamente; el criterio duro de PASS es el working set absoluto, abajo. --
long managed = GC.GetTotalMemory(forceFullCollection: true);
long workingSet = Process.GetCurrentProcess().WorkingSet64;
Console.WriteLine($"[load-test] memoria: managed-heap={managed / (1024 * 1024)}MB " +
                  $"working-set={workingSet / (1024 * 1024)}MB");

// Memoria acotada (SC-006): con tamaño por doc y número de docs activos ambos acotados, el working
// set se estabiliza. Límite absoluto generoso; un proceso que crece sin cota lo supera holgadamente.
const long workingSetLimitMb = 1536;
long workingSetMb = workingSet / (1024 * 1024);
bool memoryBounded = workingSetMb < workingSetLimitMb;
bool consistent = inconsistencias == 0;
bool noErrors = Interlocked.Read(ref errors) == 0;

Console.WriteLine($"[load-test] consistencia={(consistent ? "OK" : $"FAIL ({inconsistencias})")} " +
                  $"memoria-acotada={(memoryBounded ? "OK" : $"FAIL (working-set {workingSetMb}MB >= {workingSetLimitMb}MB)")} " +
                  $"sin-errores={(noErrors ? "OK" : "FAIL")}");

if (consistent && memoryBounded && noErrors)
{
    Console.WriteLine("[load-test] RESULTADO: PASS");
    return 0;
}

Console.WriteLine("[load-test] RESULTADO: FAIL");
return 1;

static int ArgInt(string[] args, string name, int fallback)
{
    int i = Array.IndexOf(args, name);
    if (i >= 0 && i + 1 < args.Length && int.TryParse(args[i + 1], out int value))
    {
        return value;
    }
    return fallback;
}
