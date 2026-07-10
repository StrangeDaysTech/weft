# Contract — `Weft.Core` API pública

**Paquete**: `Weft.Core` (namespace raíz `Weft`) · **Hito**: M0 (abstracciones + binding), M1 (concurrencia)

Este contrato es la forma idiomática final del borrador `ICrdtEngine` del brief
(cambios respecto al borrador: índices `int`, nombres alineados a convenciones .NET
—`CreateDoc`, `ApplyUpdate`, `GetText`—, state-vector/delta promovidos a miembros de
`ICrdtDoc`, y documentación de thread-safety). Racional en [research.md](../research.md) R1–R6.

## Abstracciones (namespace `Weft`)

```csharp
/// <summary>Fábrica de documentos de un motor CRDT. Thread-safe.</summary>
public interface ICrdtEngine
{
    /// <summary>Nombre estable del motor ("yrs", "loro").</summary>
    string Name { get; }

    /// <summary>Documento vacío.</summary>
    ICrdtDoc CreateDoc();

    /// <summary>Reconstruye un documento desde un blob exportado.</summary>
    /// <exception cref="CorruptUpdateException">blob no decodificable</exception>
    ICrdtDoc LoadDoc(ReadOnlySpan<byte> blob);

    /// <summary>Capacidad opcional de versionado nativo; null si el motor no la ofrece.</summary>
    INativeVersioning? NativeVersioning { get; }
}

/// <summary>
/// Documento CRDT vivo. NO thread-safe: el dueño serializa el acceso
/// (o usa DocumentBroker, que lo garantiza). Ver constitución P-V.
/// </summary>
public interface ICrdtDoc : IDisposable
{
    // -- Texto por campo nombrado (v1) --
    void   InsertText(string field, int index, string text);
    void   DeleteText(string field, int index, int length);
    string GetText(string field);

    // -- Estado y sincronización --
    /// <summary>Export byte-determinista del estado completo (base del content-addressing).</summary>
    byte[] ExportState();
    /// <summary>Resumen "qué conozco" para sync incremental.</summary>
    byte[] ExportStateVector();
    /// <summary>Delta con los cambios que el emisor del state vector no conoce.</summary>
    byte[] ExportUpdateSince(ReadOnlySpan<byte> stateVector);
    /// <summary>Fusiona un update/estado de otra réplica (convergente).</summary>
    void   ApplyUpdate(ReadOnlySpan<byte> update);
}

/// <summary>Capacidad opcional para motores con versionado nativo (Loro). Probes de paridad.</summary>
public interface INativeVersioning
{
    string NativeDiffProbe(ICrdtDoc doc, string field);
    string NativeBranchMergeProbe(ICrdtDoc doc, string field);
    byte[] ShallowSnapshot(ICrdtDoc doc);
}
```

**Postcondiciones clave**

- `LoadDoc(doc.ExportState()).ExportState()` es byte-idéntico al original (round-trip, P-III).
- `a.ApplyUpdate(b.ExportUpdateSince(a.ExportStateVector()))` deja `a` convergido con `b`.
- Los `byte[]` devueltos son SIEMPRE memoria gestionada .NET: la copia desde el buffer nativo
  y su liberación (`weft_buf_free`) ocurren dentro de la llamada; ningún puntero nativo escapa.

## Errores (namespace `Weft`)

```csharp
public class WeftException : Exception;                    // base de todo error de Weft
public sealed class CorruptUpdateException : WeftException;   // decode fallido (blob/update)
public sealed class WeftEngineException : WeftException      // apply/utf8/panic del motor
{
    public WeftErrorCode ErrorCode { get; }               // Decode/Apply/Utf8/OutOfBounds/Panic
}
public sealed class BlobIntegrityException : WeftException;  // hash no verifica (Weft.Versioning)
```

- Validación de argumentos (null, índice negativo, fuera de rango, campo vacío) lanza las
  excepciones BCL idiomáticas (`ArgumentNullException`, `ArgumentOutOfRangeException`,
  `ArgumentException`) — en C#, antes de cruzar la frontera.
- Uso tras `Dispose` → `ObjectDisposedException`.
- Un panic del motor llega como `WeftEngineException(ErrorCode.Panic)`; el proceso sigue
  estable (P-I; SC-009).

## Implementación yrs (namespace `Weft.Yrs`)

```csharp
public sealed class YrsEngine : ICrdtEngine   // Name == "yrs"; NativeVersioning == null
{
    public static YrsEngine Instance { get; } // sin estado propio; el motor es una fábrica
}
```

Internos (no públicos, definidos aquí como contrato de implementación): `YrsDoc : ICrdtDoc`
con `DocHandle : SafeHandleZeroOrMinusOneIsInvalid`; llamadas nativas vía `HandleLease`
(`DangerousAddRef/Release`, research R2); `NativeMethods` con `[LibraryImport]` según
[ffi-abi.md](./ffi-abi.md); resolución del cdylib por RID con `NativeLibrary.SetDllImportResolver`.

## Concurrencia (namespace `Weft.Concurrency`) — M1

```csharp
/// <summary>
/// Gestiona documentos activos con acceso serializado por documento (actor/canal).
/// Thread-safe. Único camino soportado para compartir un documento entre hilos.
/// </summary>
public sealed class DocumentBroker : IAsyncDisposable
{
    public DocumentBroker(ICrdtEngine engine, DocumentBrokerOptions? options = null);

    /// <summary>Abre (o reutiliza) el documento; lo carga con el loader si no está activo.</summary>
    public ValueTask<DocumentSession> OpenAsync(
        string docId,
        Func<string, CancellationToken, ValueTask<byte[]?>>? loader = null, // null/[] => doc vacío
        CancellationToken ct = default);

    public int ActiveDocumentCount { get; }
}

/// <summary>Facade async de un documento gestionado. Varias sesiones pueden compartir doc.</summary>
public sealed class DocumentSession : IAsyncDisposable
{
    public string DocId { get; }

    // Espejo async de ICrdtDoc — cada llamada se encola al actor del documento:
    public ValueTask InsertTextAsync(string field, int index, string text, CancellationToken ct = default);
    public ValueTask DeleteTextAsync(string field, int index, int length, CancellationToken ct = default);
    public ValueTask<string> GetTextAsync(string field, CancellationToken ct = default);
    public ValueTask<byte[]> ExportStateAsync(CancellationToken ct = default);
    public ValueTask<byte[]> ExportStateVectorAsync(CancellationToken ct = default);
    public ValueTask<byte[]> ExportUpdateSinceAsync(ReadOnlyMemory<byte> stateVector, CancellationToken ct = default);
    public ValueTask ApplyUpdateAsync(ReadOnlyMemory<byte> update, CancellationToken ct = default);

    /// <summary>Operación compuesta atómica respecto a otras operaciones del mismo doc.
    /// El ICrdtDoc recibido NO debe capturarse ni usarse fuera del delegado.</summary>
    public ValueTask<T> ExecuteAsync<T>(Func<ICrdtDoc, T> operation, CancellationToken ct = default);

    /// <summary>Se dispara tras cada update aplicado (propio o importado); para relay/persistencia.</summary>
    public event Action<DocumentSession, ReadOnlyMemory<byte>>? UpdateApplied;
}

public sealed class DocumentBrokerOptions
{
    public TimeSpan IdleEviction { get; init; } = TimeSpan.FromMinutes(5);
    public int MaxActiveDocuments { get; init; } = 1024;      // al superarse: desalojo LRU
    /// <summary>Hook invocado antes de desalojar (persistir estado). El desalojo espera su fin.</summary>
    public Func<string, byte[], CancellationToken, ValueTask>? OnEvicting { get; init; }
}
```

**Garantías de contrato**

- Nunca dos operaciones del mismo documento en ejecución simultánea; orden FIFO por sesión.
- `ExecuteAsync` corre el delegado completo dentro del turno del actor (transacción lógica).
- Desalojo: espera drenar la cola → invoca `OnEvicting(docId, exportState)` → libera el doc.
  Reabrir después usa el `loader`.
- Actor en fallo irrecuperable: operaciones pendientes y futuras fallan con la excepción
  causal; el doc se desaloja sin invocar `OnEvicting` (estado potencialmente inválido).
- `DisposeAsync` del broker drena y libera todos los documentos exactamente una vez.

## Compatibilidad y evolución

- v1 congela: `ICrdtEngine`, `ICrdtDoc`, `INativeVersioning`, jerarquía de excepciones,
  `DocumentBroker/Session/Options`. Cambios aditivos permitidos (nuevos miembros default en
  interfaces se evitan: se preferirán interfaces nuevas `ICrdtDoc2`-style o extension members).
- Tipos compartidos futuros (Map/Array/XML del editor) entrarán como interfaces opcionales
  (`ICrdtXmlDoc`), sin romper `ICrdtDoc` (assumption de spec).
