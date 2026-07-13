---
id: AILOG-2026-07-12-001
title: "CHARTER-04: Weft.Server foundation — códec y-sync + stores + contract suite (T043–T046, T050)"
status: draft
created: 2026-07-12
agent: claude-opus-4-8
confidence: high
review_required: true
reviewed_by: ""
reviewed_at: ""
review_outcome: pending
risk_level: medium
eu_ai_act_risk: not_applicable
nist_genai_risks: []
iso_42001_clause: []
lines_changed: 0
files_modified: []
observability_scope: none
tags: [server, y-sync, lib0, codec, persistence, document-store, contract-suite, ffi-boundary]
related: [AILOG-2026-07-11-001, AIDEC-2026-07-12-001]
originating_charter: CHARTER-04-server-foundation-codec-stores
---

# AILOG: CHARTER-04 — Weft.Server foundation (códec y-sync + stores + contract suite)

## Summary

Primer corte de M2/US3 (foundation, effort M): el **substrato sin red** del relay `Weft.Server`, verificable
por unit tests y una contract suite compartida. Dos piezas: (1) el **códec** lib0/y-sync (varint +
framing SyncStep1/2/Update/Awareness) compatible con `y-protocols`/`y-websocket`, con un **cap configurable de
tamaño de mensaje** que rechaza frames sobredimensionados y prefijos de longitud mentirosos **antes** de
entregar bytes al decoder de yrs (FU-002 parte a); y (2) la **persistencia** `IDocumentStore` de blobs opacos
con dos adaptadores (`InMemoryDocumentStore`, `FileSystemDocumentStore` con escritura atómica y compaction),
ambos validados por la misma `DocumentStoreContractSuite`. No hay connection handler, relay, DI ni publish:
eso es CHARTER-05. **No cierra M2.**

## Context

Ejecución de T043–T046, T050 bajo `.straymark/charters/04-server-foundation-codec-stores.md`, sobre M1
(concurrencia broker) y M0 (binding + versionado). Trabajo de **implementación** contra el contrato congelado
`contracts/server-api.md` (API v1). Proyecto nuevo `src/Weft.Server/` (namespace `Weft.Server`; `net10.0`;
`FrameworkReference` a ASP.NET Core solo por `HttpContext` en `IWeftAuthorizer`) + `tests/Weft.Server.Tests/`.
El relay end-to-end que consume esta base se anclará en las superficies de concurrencia de M1 (CHARTER-05).

## Actions Performed

1. **Códec lib0 (T043)**: `Protocol/Lib0Encoding.cs` — `Lib0Reader` (ref struct, zero-copy) y `Lib0Writer`
   (`ArrayBufferWriter`) para varint sin signo (32 bits) y `VarUint8Array`. Guardas estructurales:
   varint truncado/sobredimensionado → `MalformedMessageException`; `ReadVarUint8Array` nunca asigna ni
   avanza según una longitud mayor que los bytes restantes (núcleo anti-DoS de FU-002 parte a).
2. **Framing y-sync (T043)**: `Protocol/SyncProtocol.cs` — `MessageType` (Sync/Awareness), `SyncMessageType`
   (Step1/2/Update), encoders y `Decode(frame, maxMessageBytes)` que aplica el cap de tamaño antes de
   parsear y rechaza tipos/sub-tipos desconocidos, varints inválidos y bytes sobrantes. El payload es opaco
   (se pasa tal cual al decoder yrs en CHARTER-05).
3. **Hook de autorización (T044)**: `Auth/IWeftAuthorizer.cs` — `interface IWeftAuthorizer` +
   `enum WeftAccess { Deny, ReadOnly, ReadWrite }`. Solo contrato; el enforcement es CHARTER-05.
4. **Persistencia interfaz + memoria (T045)**: `Persistence/IDocumentStore.cs`
   (`LoadAsync`/`AppendUpdateAsync`/`SaveSnapshotAsync`, blobs opacos, thread-safe) +
   `InMemoryDocumentStore.cs` (lock por documento, mapa `ConcurrentDictionary`) +
   `DocumentStateFraming.cs` (formato del estado que devuelve `LoadAsync`; ver AIDEC).
5. **Persistencia FileSystem (T046)**: `Persistence/FileSystemDocumentStore.cs` — un directorio por doc
   (nombre = hash SHA-256 del `docId`, evita traversal), snapshot + un archivo por update, **escritura
   atómica** (temp + rename), **compaction** al guardar snapshot (reemplaza snapshot y borra los updates),
   serialización por doc con `SemaphoreSlim`.
6. **Contract suite (T050)**: `tests/Weft.Server.Tests/DocumentStoreContractSuite.cs` — clase base abstracta
   ejecutada **idéntica** contra InMemory y FileSystem: `LoadAsync`-null de doc inexistente, orden
   append-then-load, snapshot-compaction reemplaza updates, aislamiento entre docs, blob grande (4 MiB),
   thread-safety (250 appends concurrentes + mix load/write). `Lib0EncodingTests.cs` — vectores byte a byte
   de varint y framing y-sync + tests del cap y de los frames malformados.

## Modified Files

| File | Change Description |
|------|--------------------|
| `src/Weft.Server/Weft.Server.csproj` | New — proyecto `Weft.Server` (net10.0; FrameworkReference ASP.NET Core) |
| `src/Weft.Server/Protocol/Lib0Encoding.cs` | New — varint lib0 + cap de tamaño (T043, FU-002 a) |
| `src/Weft.Server/Protocol/SyncProtocol.cs` | New — framing y-sync + Decode con cap (T043) |
| `src/Weft.Server/Auth/IWeftAuthorizer.cs` | New — `IWeftAuthorizer` + `enum WeftAccess` (T044) |
| `src/Weft.Server/Persistence/IDocumentStore.cs` | New — contrato de persistencia de blobs opacos (T045) |
| `src/Weft.Server/Persistence/DocumentStateFraming.cs` | New — formato del estado de `LoadAsync` (T045; ver AIDEC) |
| `src/Weft.Server/Persistence/InMemoryDocumentStore.cs` | New — store en memoria (T045) |
| `src/Weft.Server/Persistence/FileSystemDocumentStore.cs` | New — snapshot+updates, compaction atómica (T046) |
| `tests/Weft.Server.Tests/Weft.Server.Tests.csproj` | New — proyecto de tests (xUnit) |
| `tests/Weft.Server.Tests/DocumentStoreContractSuite.cs` | New — suite compartida InMemory+FileSystem (T050) |
| `tests/Weft.Server.Tests/Lib0EncodingTests.cs` | New — vectores lib0/y-sync + cap + malformed (T043) |
| `Weft.sln` | Change — añadidos `Weft.Server` y `Weft.Server.Tests` |
| `specs/001-weft-crdt-versioning/tasks.md` | Change — T043–T046, T050 marcadas `[X] — CHARTER-04` |
| `.straymark/charters/04-*.md` | Change — status declared → in-progress |

## Decisions Made

- **Forma del cap de tamaño (FU-002 a)**: dos guardas. (a) rechazo del frame completo por encima de
  `maxMessageBytes` (default 16 MiB) **antes** de parsear; (b) `ReadVarUint8Array` rechaza toda longitud
  declarada mayor que los bytes restantes — un frame de pocos bytes no puede inducir una asignación gigante.
  Ambas fallan con `MalformedMessageException`, jamás con abort (P-I/P-II). Detalle en AIDEC-2026-07-12-001.
- **Forma del estado de `LoadAsync`**: como los blobs son opacos (P-IV, el store no fusiona updates de yrs),
  `LoadAsync` devuelve una **secuencia plana de records** (snapshot primero + updates en orden), enmarcada con
  `VarUint8Array` lib0; el relay aplica cada record a un doc fresco (todos son `apply_update` idempotentes).
  `null` = doc inexistente. Detalle en AIDEC-2026-07-12-001.
- **Atomicidad del FileSystem store**: cada update es su propio archivo escrito con temp+rename (nunca un
  append truncable); el snapshot se reemplaza con temp+rename y luego se borran los updates ya incorporados.
  Un fallo entre ambos pasos solo deja updates idempotentes → recuperación segura.
- **`docId` → filename por hash SHA-256**: el `docId` es opaco (puede contener `/`, no-ASCII, longitud
  arbitraria); el hash hex evita traversal de rutas y nombres ilegales.

## Impact

- **Functionality**: substrato del relay listo (códec interoperable + persistencia intercambiable); ninguna
  superficie de red todavía.
- **Security/Memory**: el cap de tamaño es la primera línea ante input de red no confiable (FU-002 parte a);
  la parte b (límites por conexión/backpressure) queda para CHARTER-05, por lo que **FU-002 sigue `open`**.
- **Performance**: códec zero-copy en lectura (spans sobre el frame); stores con lock/semaphore por doc.

## Verification

- [x] `dotnet build Weft.sln -c Release` — 0 warnings / 0 errores (TreatWarningsAsErrors)
- [x] `dotnet test tests/Weft.Server.Tests/` — **46 verdes** (contract suite ×2 adaptadores + vectores códec + cap)
- [x] `dotnet test` (suite completa) — **100 verdes** (Server 46, Core 27, Versioning 25, Determinism 2); M0/M1 intactos
- [ ] Revisión humana del operador — pendiente (`review_required: true`)
- [ ] Auditoría externa — NO obligatoria en este corte (reservada a CHARTER-05, que cierra M2)

## Additional Notes

### Bug de conversión nullable destapado por la dual-suite (no llegó a main)

Durante la implementación, la contract suite corriendo **idéntica** contra InMemory y FileSystem destapó un
bug sutil de C# antes de cualquier push: `DocumentStateFraming.Frame` recibía el snapshot como
`ReadOnlyMemory<byte>?`. Un snapshot ausente pasado como `byte[]`/`ReadOnlyMemory` **nulo** se convierte
implícitamente en un `ReadOnlyMemory<byte>` **vacío NO-nulo** (`default`), no en un `Nullable` sin valor →
`Frame` escribía un record de snapshot vacío espurio, y `LoadAsync` devolvía un record de más. **Corregido**
cambiando la firma a `byte[]? snapshot` (referencia nullable, sin la conversión struct traicionera). La señal
fue exactamente la intención de R2 del Charter (contract suite fuerte contra dos implementaciones reales
desde el día uno): el mismo pitfall latente vivía en los dos callers y la suite obligó a ambos a aflorar.

### FU-002 permanece `open` (mitigación parcial)

Este corte entrega la parte a (cap de tamaño en el framing). La parte b (límites de recursos por conexión,
backpressure, path malformed→1002 en el handler real) es CHARTER-05. `FU-002` NO se cierra aquí.

### Decisiones candidatas a AIDEC

La forma del cap y la forma del estado de `LoadAsync` son decisiones de diseño sustantivas sobre el contrato
congelado; se documentan en AIDEC-2026-07-12-001.

### Scope expansion del drift check (`straymark charter drift --range origin/main..HEAD`)

El drift reporta 4 archivos "modificados pero no declarados". Tres son **falsos positivos del parser** (misma
limitación que CHARTER-01/03): `Weft.sln` y los dos `.csproj` (`src/Weft.Server/Weft.Server.csproj`,
`tests/Weft.Server.Tests/Weft.Server.Tests.csproj`) SÍ están declarados en §Files to modify —el parser no
matchea rutas de `.sln`/`.csproj`—; no hay expansión real.

El cuarto, `src/Weft.Server/Persistence/DocumentStateFraming.cs`, es una **expansión de scope intencional**:
un helper nuevo que encapsula el formato del estado que devuelve `LoadAsync` (secuencia de records
`VarUint8Array`). No estaba en la tabla original del Charter porque la forma del contenedor se decidió en
ejecución (AIDEC-2026-07-12-001, Decisión 2); extraerlo a su propio archivo mantiene los blobs opacos (P-IV),
lo comparten los dos adaptadores y la contract suite lo verifica. Dentro del alcance funcional de T045 (la
persistencia de `IDocumentStore`), no fuera de T043–T046/T050.
