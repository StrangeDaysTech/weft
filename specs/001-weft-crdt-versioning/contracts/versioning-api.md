# Contract — `Weft.Versioning` API pública

**Paquete**: `Weft.Versioning` (namespace `Weft.Versioning`) · **Hito**: M0

Capa de dominio **engine-agnóstica** (constitución P-IV): depende solo de las abstracciones de
`Weft.Core` (`ICrdtEngine`/`ICrdtDoc`), jamás de un motor concreto. Es la generalización de la
capa de ~58 LOC validada en Spike 03 sobre ambos motores. Racional en
[research.md](../research.md) R9–R10.

## Identidad de versión

```csharp
/// <summary>Identidad content-addressed de una versión: SHA-256 del export determinista.
/// Value type inmutable; igualdad por valor; hex lowercase de 64 chars.</summary>
public readonly struct VersionId : IEquatable<VersionId>
{
    public static VersionId FromBlob(ReadOnlySpan<byte> blob);   // SHA256.HashData
    public static VersionId Parse(string hex);                   // + TryParse
    public override string ToString();                           // hex lowercase
    public ReadOnlySpan<byte> AsSpan();                          // 32 bytes
}
```

## Almacén de blobs

```csharp
/// <summary>Almacén content-addressed (hash → blob). Implementaciones deben ser thread-safe.</summary>
public interface IBlobStore
{
    /// <summary>Idempotente: put del mismo contenido es no-op (dedup natural por hash).</summary>
    ValueTask PutAsync(VersionId id, ReadOnlyMemory<byte> blob, CancellationToken ct = default);
    /// <summary>null si no existe.</summary>
    ValueTask<byte[]?> GetAsync(VersionId id, CancellationToken ct = default);
    ValueTask<bool> ExistsAsync(VersionId id, CancellationToken ct = default);
}
// Incluidos en el paquete: InMemoryBlobStore, FileSystemBlobStore (sharding aa/bb/hash).
// Sin delete en v1: las versiones publicadas son inmutables (FR-012); retención = dominio del consumidor.
```

**Invariante de integridad**: toda implementación puede verificar `VersionId.FromBlob(blob) == id`
en `Get`; el `VersionStore` SIEMPRE lo verifica al cargar y lanza `BlobIntegrityException` si
no coincide (edge case de spec: blob corrupto).

## Operaciones de versionado

```csharp
/// <summary>Publicar/cargar/comparar/ramificar versiones de documentos. Thread-safe
/// (sin estado mutable propio; la serialización del doc vivo es responsabilidad del llamador
/// o del DocumentBroker).</summary>
public sealed class VersionStore
{
    public VersionStore(ICrdtEngine engine, IBlobStore blobs);

    /// <summary>Exporta, hashea y persiste. Devuelve la identidad citable.</summary>
    public ValueTask<VersionId> PublishAsync(ICrdtDoc doc, CancellationToken ct = default);

    /// <summary>Reconstruye un documento vivo desde una versión publicada (verifica integridad).</summary>
    public ValueTask<ICrdtDoc> CheckoutAsync(VersionId version, CancellationToken ct = default);

    /// <summary>Diff de texto por palabras entre dos versiones publicadas (research R9).</summary>
    public ValueTask<TextDiff> DiffAsync(VersionId a, VersionId b, string field, CancellationToken ct = default);

    /// <summary>Rama: documento vivo independiente partiendo de la versión base.
    /// (Alias semántico de Checkout; existe para expresar intención y para telemetría futura.)</summary>
    public ValueTask<ICrdtDoc> BranchAsync(VersionId from, CancellationToken ct = default);

    /// <summary>Merge CRDT: importa el estado de la rama en el destino (convergente, sin conflictos).</summary>
    public void Merge(ICrdtDoc target, ICrdtDoc branch);
    public ValueTask MergeAsync(ICrdtDoc target, VersionId branchVersion, CancellationToken ct = default);
}
```

**Compactación**: es una *estrategia*, no un método — se obtiene por construcción (research R10,
Spike 03 §2): el doc vivo mantiene GC del motor activo (los tombstones no se acumulan) y cada
versión publicada es un blob autocontenido en el `IBlobStore`. No existe operación que borre
versiones publicadas (FR-012). El único acto de "compactar" el doc vivo es
`ExportState` + `LoadDoc` (re-materialización), que `DocumentBroker` aplica en el desalojo.

## Diff de texto

```csharp
public sealed record TextDiff(IReadOnlyList<TextDiffSegment> Segments)
{
    public bool HasChanges { get; }
}
public readonly record struct TextDiffSegment(DiffOp Op, string Text);
public enum DiffOp { Equal, Inserted, Deleted }
```

- Algoritmo: LCS sobre tokens palabra/espacio; determinista (mismas entradas → mismos segmentos).
- Alcance v1: texto plano por campo. **Diferido**: diff estructural rich-text (tree-diff del
  modelo ProseMirror) — la firma `DiffAsync` admite evolución aditiva (`RichDiffAsync` futura).

## Postcondiciones de contrato (base de la suite dual-engine)

1. `PublishAsync(doc)` dos veces sin cambios → mismo `VersionId`, un solo blob (dedup).
2. `CheckoutAsync(Publish(doc))` → `ExportState()` byte-idéntico al blob publicado.
3. Réplicas convergidas publican el MISMO `VersionId` (SC-002).
4. `Diff(a, a)` → `HasChanges == false`; `Diff(a, b)` refleja exactamente las ediciones aplicadas.
5. Merge de dos ramas concurrentes: ambas ediciones presentes; resultado idéntico sin importar
   el orden de merge (conmutatividad observable, edge case de spec).
6. Toda esta suite corre idéntica sobre `YrsEngine` y `LoroEngine` (P-IV, research R15).
7. Compactación por construcción (FR-012): tras publicar ≥ 20 versiones de un documento con
   ciclos de inserción+borrado (historial intermedio no publicado entre ellas),
   (a) TODAS las versiones siguen recuperables por su hash con checkout byte-idéntico,
   (b) el tamaño agregado del store queda acotado por la suma de los blobs publicados
   (sin acumulación de tombstones — el GC del motor permanece activo), y
   (c) el blob de la versión N no crece de forma monótona con la longitud del historial
   (referencia medida en Spike 03: 23 versiones = 3 527 B; con `skip_gc` sería 7.3× mayor).
