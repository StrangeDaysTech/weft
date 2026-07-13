---
charter_id: CHARTER-04-server-foundation-codec-stores
status: in-progress
effort_estimate: M
trigger: "CHARTER-03 cerrado (M1: concurrencia broker verde en main) + spec-refresh de US3/M2 mergeado (PR #13, plan.md §'US3/M2 — anclajes sobre M1'). tasks.md fija T043–T054 (US3) como M2; este es el primer corte (foundation): códec lib0/y-sync + IDocumentStore (InMemory/FileSystem) verificables por una contract suite compartida, SIN red todavía. Activa la mitigación parte-a de FU-002 (cap de tamaño de mensaje) y establece la base de intercambiabilidad de stores (P-IV)."
originating_spec: specs/001-weft-crdt-versioning/spec.md
work_verb: implement
design_provenance: new
---

# Charter: Weft.Server foundation — códec y-sync + stores + contract suite

> **Status (mirrored from frontmatter — source of truth is above):** in-progress. Effort: M.
>
> **Origin:** Derivado de `specs/001-weft-crdt-versioning/spec.md`. Primer corte de M2 (T043–T046, T050):
> substrato del relay `Weft.Server` —códec lib0/y-sync y persistencia de blobs opacos— verificable sin red.
> No cierra M2 (lo hace CHARTER-05); habilita el relay end-to-end que sigue.

## Context

M2/US3 entrega colaboración en tiempo real vía un relay WebSocket y-sync (`Weft.Server`, ASP.NET Core)
compatible con clientes Yjs estándar. Es demasiado para un solo corte (12 tareas T043–T054, dispara la
heurística de granularidad del bridge: L es el cap), así que M2 se ejecuta en **3 cortes** espejando cómo
M0 se cerró en dos: **foundation (este Charter) → relay end-to-end (CHARTER-05, cierra M2) → adaptadores
externos EFCore/Redis (CHARTER-06)**. Ver `plan.md` §"US3/M2 — anclajes sobre M1".

Este corte construye el **substrato sin red**: (1) el **códec** lib0/y-sync (varint + framing SyncStep1/2/
Update/Awareness) que interopera con `y-websocket`, y (2) la **persistencia** `IDocumentStore` de blobs
opacos con dos adaptadores (InMemory para tests/dev, FileSystem para v1), ambos validados por una **contract
suite compartida** que todo adaptador futuro pasará idéntica (la intercambiabilidad que exige el escenario 4
de aceptación de US3 y `contracts/server-api.md`). El connection handler, el relay, el DI y el publish
consumen esta base pero pertenecen a CHARTER-05.

Trabajo de **implementación** contra el contrato congelado `contracts/server-api.md` (API v1). Toca tres
principios constitucionales: **P-I/P-II** (frontera nativa / memoria) se activan porque el códec es la
primera capa que recibirá bytes de red **no confiables** —el cap de tamaño de mensaje (FU-002 parte a)
rechaza frames sobredimensionados **antes** de que lleguen al decoder yrs, cuya amplificación de memoria es
el DoS que FU-002 describe—; **P-IV** (abstracción de motor viva) se ejerce porque `IDocumentStore` maneja
**blobs opacos** (no tipos de yrs) y la contract suite es la base de su intercambiabilidad. El relay que
sigue (CHARTER-05) se anclará en las superficies de concurrencia de M1 (`DocumentSession.UpdateApplied`,
refcount de sesiones, `_evicting`-await, aislamiento de handlers) — fuera de alcance aquí.

## Scope

**In scope (T043–T046, T050):**

1. **Códec y framing (T043)**: `Protocol/Lib0Encoding.cs` (varint lib0 encode/decode compatible con
   `y-protocols`, con **cap configurable de tamaño de mensaje** que rechaza frames sobredimensionados antes
   del decoder — FU-002 parte a) + `Protocol/SyncProtocol.cs` (framing y-sync: `msgType` SYNC con subtipos
   SyncStep1(stateVector)/SyncStep2(update)/Update, y AWARENESS, per `contracts/server-api.md` §Protocolo).
   Unit-tested contra vectores de `y-protocols` capturados y con test del cap de tamaño.
2. **Hook de autorización (T044)**: `Auth/IWeftAuthorizer.cs` — `interface IWeftAuthorizer`
   (`ValueTask<WeftAccess> AuthorizeAsync(HttpContext, string docId, CancellationToken)`) + `enum WeftAccess
   { Deny, ReadOnly, ReadWrite }`. Solo tipos/contrato; el enforcement en el handshake es CHARTER-05.
3. **Persistencia — interfaz + memoria (T045)**: `Persistence/IDocumentStore.cs`
   (`LoadAsync`/`AppendUpdateAsync`/`SaveSnapshotAsync`, blobs opacos, thread-safe) +
   `Persistence/InMemoryDocumentStore.cs`.
4. **Persistencia — sistema de archivos (T046)**: `Persistence/FileSystemDocumentStore.cs` — snapshot +
   append de updates, **compaction** al guardar snapshot (reemplaza estado + updates acumulados), escritura
   atómica.
5. **Contract suite compartida (T050)**: `tests/Weft.Server.Tests/DocumentStoreContractSuite.cs` — suite
   base que corre **idéntica** contra `InMemoryDocumentStore` y `FileSystemDocumentStore` (y, en CHARTER-06,
   contra EFCore/Redis). Cubre: `LoadAsync` de doc inexistente → `null`; orden append-then-load; snapshot
   reemplaza los updates acumulados (compaction); thread-safety bajo acceso concurrente; blob grande.

Proyecto nuevo `src/Weft.Server/` (namespace `Weft.Server`; `net10.0`, referencia a ASP.NET Core solo por
`HttpContext` en `IWeftAuthorizer`) + `tests/Weft.Server.Tests/`, ambos añadidos a `Weft.sln`.

**Out of scope:**

- Connection handler, handshake/authz enforcement, sync bidireccional, relay vía `DocumentBroker`, awareness
  broadcast, DI (`AddWeftServer`/`MapWeft`), `IWeftServer.PublishAsync` — **CHARTER-05** (T047–T049, T051–T052).
- Adaptadores `Weft.Server.Persistence.EFCore` y `.Redis` — **CHARTER-06** (T053–T054); pasan esta misma
  contract suite sin modificarla.
- **Interoperabilidad real con `y-websocket`/Tiptap** (el gate de compat del wire) — **CHARTER-05** (T052).
  Aquí el códec se valida solo con vectores unit; su riesgo de compat no se retira hasta el cliente real.
- FU-002 **parte b** (límites de recursos por conexión / backpressure) — **CHARTER-05** (T047). Aquí solo el
  cap de tamaño en el framing (parte a).

## Files to modify

<!-- Greenfield: src/Weft.Server/ y tests/Weft.Server.Tests/ NO existen (reconnaissance #210 confirmado);
     todo lo marcado New se crea en este Charter. Weft.sln y tasks.md existen → Change. La API v1 la fija
     contracts/server-api.md. -->

| File | Change |
|---|---|
| `src/Weft.Server/Weft.Server.csproj` | New — proyecto `Weft.Server` (net10.0; ref ASP.NET Core por `HttpContext`) |
| `src/Weft.Server/Protocol/Lib0Encoding.cs` | New — varint lib0 encode/decode + cap de tamaño (T043, FU-002 a) |
| `src/Weft.Server/Protocol/SyncProtocol.cs` | New — framing y-sync SyncStep1/2, Update, Awareness (T043) |
| `src/Weft.Server/Auth/IWeftAuthorizer.cs` | New — `IWeftAuthorizer` + `enum WeftAccess` (T044) |
| `src/Weft.Server/Persistence/IDocumentStore.cs` | New — Load/AppendUpdate/SaveSnapshot, blobs opacos (T045) |
| `src/Weft.Server/Persistence/InMemoryDocumentStore.cs` | New — store en memoria (T045) |
| `src/Weft.Server/Persistence/FileSystemDocumentStore.cs` | New — snapshot+updates append, compaction atómica (T046) |
| `tests/Weft.Server.Tests/Weft.Server.Tests.csproj` | New — proyecto de tests (xUnit) |
| `tests/Weft.Server.Tests/DocumentStoreContractSuite.cs` | New — suite compartida InMemory+FileSystem (T050) |
| `tests/Weft.Server.Tests/Lib0EncodingTests.cs` | New — vectores lib0/y-sync + test del cap de tamaño (T043) |
| `Weft.sln` | Change — añadir `Weft.Server` y `Weft.Server.Tests` |
| `specs/001-weft-crdt-versioning/tasks.md` | Change — marcar T043–T046, T050 `[X]` + `*CHARTER-04: <sha>*` |
| `.straymark/07-ai-audit/agent-logs/AILOG-*.md` | New, `risk_level: medium` |

## Verification

### Local checks

> **Lección de CHARTER-01/02/03**: correr TODO localmente en verde ANTES de pushear.

```bash
# Build de toda la solución (incluye los nuevos proyectos Weft.Server y Weft.Server.Tests)
dotnet build Weft.sln -c Release

# Tests del nuevo proyecto: contract suite (InMemory + FileSystem) + vectores del códec + cap de tamaño
dotnet test tests/Weft.Server.Tests/

# Suite completa verde (M0/M1 intactos)
dotnet test
```

### Production smoke (after deploy)

No aplica — librería sin despliegue. Los auditores externos deben saltar esta sección.

## Risks

- **R1 — Amplificación de memoria por update malformado (FU-002 parte a, DoS)**: severidad **alta**.
  Un frame de red malformado con pocos bytes puede inducir una asignación gigante en el decoder yrs → posible
  abort (tensa P-I "ningún panic/abort cruza la frontera" y P-II "memoria acotada"). Mitigación **aquí**: cap
  **configurable** de tamaño de mensaje en el framing lib0 (T043) que rechaza el frame **antes** de entregar
  bytes al decoder; test dedicado de frame sobredimensionado. Si falla: la amplificación llega al decoder →
  el complemento (límites de recursos por conexión, FU-002 **parte b**) queda para CHARTER-05; **FU-002
  permanece `open`** tras este Charter (mitigación parcial), y se cierra al completar la parte b.
- **R2 — Contract suite débil deja pasar adaptadores divergentes**: severidad **media**. Si
  `DocumentStoreContractSuite` es floja, los adaptadores de CHARTER-06 (EFCore/Redis) la pasan y rompen en
  runtime. Mitigación: la suite cubre `LoadAsync`-null, orden append-then-load, snapshot-compaction reemplaza
  los updates acumulados, thread-safety bajo concurrencia y blob grande; corre **idéntica** contra InMemory y
  FileSystem en este Charter (dos implementaciones reales la ejercitan desde el día uno). Si falla: la
  divergencia se manifiesta tarde, en CHARTER-06 → coste de retrabajo.

## Tasks

1. Sync main, branch `charter/04-server-foundation` (ya creado desde main post-merge de PR #13). Flip
   `declared` → `in-progress` al empezar a ejecutar.
2. Re-evaluar **Constitution Check** contra el scope (per-Charter, cadencia del bridge): **P-I/P-II** se
   activan (cap FU-002 protege la frontera nativa de input no confiable); **P-IV** se ejerce (contract suite =
   base de intercambiabilidad; `IDocumentStore` maneja blobs opacos, no tipos de yrs). Sin violaciones esperadas.
3. `/speckit-implement` acotado a **T043–T046, T050**; marcar `[X]` + `*CHARTER-04: <sha>*` por tarea.
4. **AILOG** (`risk_level: medium`, `review_required: true`); **AIDEC** si emergen decisiones de diseño
   sustantivas (p. ej. forma concreta del cap de tamaño, estrategia de compaction atómica del FileSystem store).
5. **Verificación local COMPLETA** (bloque Local checks íntegro) ANTES de push.
6. `straymark charter drift CHARTER-04` antes de commit; drifts → `R<N+1>` en el AILOG (o completar el trabajo).
7. Commit + push + abrir PR contra `main`; CI verde.

## Charter Closure

Corte de foundation (effort M) sobre contrato congelado. Cierre estándar (sin auditoría externa multi-modelo
obligatoria — a diferencia de CHARTER-02/03, este corte no cierra un hito ni activa/cierra un principio
constitucional nuevo; la auditoría se reserva para CHARTER-05, que cierra M2). Al cerrar:

1. Actualización atómica del Charter (format v4) si el drift check reveló divergencias (mismo PR): editar
   `## Files to modify` y/o añadir `## Closing notes`.
2. `straymark charter drift CHARTER-04 --range origin/main..HEAD` → limpio o documentado en el AILOG.
3. Mover la fila en `.straymark/charters/README.md` (si aplica) y referenciar el PR.
4. `straymark charter close CHARTER-04` (telemetría, status `closed`, `closed_at`). No borrar este archivo.
5. Confirmar que **FU-002 sigue `open`** en `.straymark/follow-ups-backlog.md` con nota de mitigación parcial
   (parte a entregada); se cierra en CHARTER-05 al añadir la parte b.
