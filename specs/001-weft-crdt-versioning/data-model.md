# Data Model — Weft (Phase 1)

**Feature**: [spec.md](./spec.md) · **Research**: [research.md](./research.md) · **Fecha**: 2026-07-10

Modelo de entidades del dominio. Los tipos concretos y firmas viven en [contracts/](./contracts/);
aquí se definen entidades, campos, relaciones, invariantes y transiciones.

## Entidades

### CrdtDocument (documento colaborativo vivo)

Instancia editable respaldada por un documento nativo del motor.

| Campo | Tipo | Notas |
|---|---|---|
| handle | recurso nativo | privado; ciclo de vida vía SafeHandle (P-I) |
| fields | texto por nombre | campos de texto nombrados (`string → text`) |
| engine | referencia a motor | el motor que lo creó |

**Invariantes**:
- Todo acceso serializado: fuera del `DocumentBroker`, el dueño de la instancia es responsable
  de no compartirla entre hilos; dentro del broker, solo el bucle del actor la toca (P-V).
- Tras `Dispose`: cualquier operación lanza `ObjectDisposedException`; liberar dos veces es no-op.
- `ExportState()` es determinista: mismo contenido lógico → mismos bytes (P-III).

**Transiciones**: `Created (vacío | desde blob) → Active (edits/imports) → Disposed`.

### PublishedVersion (versión publicada)

Instantánea inmutable citada por su contenido.

| Campo | Tipo | Notas |
|---|---|---|
| id | `VersionId` (32 bytes) | `SHA-256(blob)`; identidad global, sin autoridad central |
| blob | `VersionBlob` | 1:1; el hash del blob ES el id |

**Invariantes**: inmutable una vez publicada; `Load(blob)` reconstruye un documento cuyo
re-export produce el mismo blob; una versión publicada jamás se vuelve irrecuperable por
compactación (FR-012).

**Relaciones**: se crea desde un `CrdtDocument`; origen de `Branch`; entrada de `Diff`.

### VersionBlob (blob de versión)

Bytes opacos y autocontenidos del export de estado. Almacenado/deduplicado por hash en el
`IBlobStore`. Sin estructura visible para Weft.Versioning ni para los stores (opacidad, FR-017).

### VersionId

Value type de 32 bytes (hash SHA-256). Igualdad por valor; representación textual hex
lowercase (64 chars); parse/format round-trip. Es la única identidad de versión del sistema.

### Branch (rama)

Línea de trabajo derivada: un `CrdtDocument` reconstruido desde el blob de una
`PublishedVersion` base y editado de forma aislada.

**Invariantes**: nace de una versión publicada; su merge es un import CRDT (conmutativo:
el orden de fusión no cambia el resultado — edge case cubierto en spec); puede publicar sus
propias versiones.

**Nota de diseño**: una rama NO es una entidad persistida con nombre en v1 — es un documento
vivo + la referencia a su versión base. El naming/registro de ramas es dominio del consumidor.

### StateVector / UpdateDelta

- **StateVector**: resumen compacto "qué conozco" de una réplica (bytes opacos del motor).
- **UpdateDelta**: paquete de cambios posteriores a un state vector dado.

**Invariantes**: `Import(delta)` sobre la réplica que emitió el SV la lleva al estado de la
emisora (convergencia); tamaño delta ≪ estado completo (SC-004).

### DocumentActor / DocumentSession (concurrencia, M1)

- **DocumentActor** (interno): cola single-reader + bucle único dueño del `CrdtDocument`.
  Estados: `Active → Idle → Evicted` (persistiendo antes de desalojar) | `Faulted` (doc
  irrecuperable: se desaloja y las operaciones pendientes fallan con la excepción causal).
- **DocumentSession** (público): facade async por la que el consumidor encola operaciones;
  n sesiones pueden apuntar al mismo actor. `IAsyncDisposable`; usar tras dispose lanza.

**Invariantes**: nunca dos operaciones del mismo doc en ejecución simultánea; el desalojo
espera el drenado de la cola; el doc nativo se libera exactamente una vez.

### SyncConnection (sesión de colaboración, M2)

| Campo | Tipo | Notas |
|---|---|---|
| docId | string | documento al que se conecta |
| access | `Deny/ReadOnly/ReadWrite` | resultado del `IWeftAuthorizer` en el handshake |
| awarenessState | bytes opacos | presencia efímera; se difunde, no se persiste |

**Transiciones**: `Handshake (authz) → Syncing (SyncStep1/2) → Live (updates/awareness) → Closed`.
En `ReadOnly`, updates entrantes ⇒ cierre de protocolo. Al cerrar, su awareness se retira del doc.

### BlobStore / DocumentStore (persistencia)

- **IBlobStore** (versionado): mapa content-addressed `VersionId → VersionBlob`. Put idempotente
  (mismo hash = mismo contenido), Get por id, sin update ni delete en v1 (inmutabilidad;
  la compactación opera sobre el doc vivo, no sobre versiones publicadas).
- **IDocumentStore** (servidor): estado durable por `docId` (blob opaco + updates pendientes).
  Estrategia append-then-compact (research R8). Adaptadores intercambiables sin cambio
  de comportamiento observable.

### CrdtEngine (abstracción de motor)

Punto de sustitución (P-IV): crea documentos (vacíos o desde blob) y declara capacidades
opcionales (`INativeVersioning?`). Implementaciones v1: `YrsEngine` (default), `LoroEngine`
(dual-path). La capa de versionado y el servidor dependen SOLO de esta abstracción.

## Diagrama de relaciones

```text
ICrdtEngine ──crea──▶ CrdtDocument ──publish──▶ PublishedVersion ─1:1─ VersionBlob
     │                     ▲   │                      │                    │
     │ (opcional)          │   └─export/import─ StateVector/Delta         │
INativeVersioning       Branch (doc + versión base)  │              IBlobStore (hash→blob)
                                                      ▼
DocumentBroker ──gestiona──▶ DocumentActor ─posee 1:1─▶ CrdtDocument
     ▲                                                        ▲
DocumentSession (N por actor)                                 │ (solo vía actor)
                                                              │
Weft.Server: SyncConnection ──relay updates──▶ doc del broker ──persiste──▶ IDocumentStore
```

## Reglas de validación (derivadas de FRs)

- Índices/longitudes de texto: `>= 0` y dentro del rango del campo → si no,
  `ArgumentOutOfRangeException` en C# (antes de cruzar la frontera) o error `OUT_OF_BOUNDS`
  del shim traducido (FR-002).
- Blob al cargar: decode fallido → `CorruptUpdateException` (FR-007); hash que no verifica
  contra el `VersionId` pedido → `BlobIntegrityException` (edge case de spec).
- `field` vacío o null → `ArgumentException`.
- Acceso `Deny` en handshake → conexión rechazada antes de cualquier intercambio de contenido
  (FR-019, SC-010).
