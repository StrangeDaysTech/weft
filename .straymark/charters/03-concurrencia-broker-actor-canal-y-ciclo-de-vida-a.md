---
charter_id: CHARTER-03-concurrencia-broker-actor-canal-y-ciclo-de-vida-a
status: in-progress
effort_estimate: L
trigger: "CHARTER-02 cerrado (M0: versionado content-addressed + dual-engine verde en main). tasks.md fija T036–T042 (US2 concurrencia) como corte de M1: broker actor/canal por documento + ciclo de vida a escala (pooling, desalojo idle+LRU, liberación determinista), activando y validando el principio constitucional P-V con prueba de carga (SC-006)."
originating_spec: specs/001-weft-crdt-versioning/spec.md
work_verb: implement
design_provenance: new
---

# Charter: Concurrencia broker actor-canal y ciclo de vida a escala

> **Status (mirrored from frontmatter — source of truth is above):** in-progress. Effort: L.
>
> **Origin:** Derivado de `specs/001-weft-crdt-versioning/spec.md`. Corte de M1 (T036–T042): capa de concurrencia serializada por documento (US2) — broker actor/canal, sesiones async y ciclo de vida a escala. Cierra M1.

## Context

Sobre M0 (binding `Weft.Core` + versionado engine-agnóstico), este corte añade la **capa de concurrencia**
que la constitución exige (P-V): el motor CRDT **no es thread-safe** y el acceso a un mismo documento DEBE
serializarse. Se implementa el patrón actor/canal —un único lector drenando una cola por documento— de modo
que el acceso concurrente directo al `ICrdtDoc` nativo sea **imposible desde la API pública**. Encima, el
`DocumentBroker` gestiona el ciclo de vida a escala: registro y reutilización por `docId`, desalojo por
inactividad y por presión de memoria (LRU), y liberación determinista de la memoria nativa (P-I).

Es prerequisito del servidor de sync: `DocumentSession` (T039) es la superficie que el relay de US3/M2
consumirá (T047). El diseño está ✅ CERRADO en el brief y el contrato `core-api.md` congela la API v1
(`DocumentBroker`/`DocumentSession`/`DocumentBrokerOptions`); trabajo de **implementación** contra ese
contrato. Cierra M1 validando P-V con una prueba de carga sostenida (SC-006).

## Scope

**In scope (T036–T042):**

1. **Opciones (T036)**: `DocumentBrokerOptions` — `IdleEviction` (default 5 min), `MaxActiveDocuments`
   (default 1024; al superarse → desalojo LRU), hook `OnEvicting(docId, exportState, ct)` (persistencia
   pre-desalojo; el desalojo espera su fin).
2. **Actor (T037)**: `DocumentActor` (`internal`) — `Channel` unbounded **single-reader**, estados
   Active/Idle/Evicted/Faulted, drenado de la cola en desalojo, doc nativo liberado **exactamente una vez**.
3. **Broker (T038)**: `DocumentBroker : IAsyncDisposable` — registro `docId→actor`, reutilización, desalojo
   por inactividad + LRU al superar `MaxActiveDocuments`, `OpenAsync` con `loader`, `ActiveDocumentCount`;
   `DisposeAsync` drena y libera todos los documentos exactamente una vez.
4. **Sesión (T039)**: `DocumentSession : IAsyncDisposable` — espejo async de `ICrdtDoc` (Insert/Delete/GetText/
   Export*/ApplyUpdate encolados al actor), `ExecuteAsync<T>` (turno atómico; el `ICrdtDoc` no se captura
   fuera del delegado), evento `UpdateApplied` (para relay/persistencia de M2). **Prerequisito de T047 (US3)**.
5. **Tests de concurrencia (T040)**: `DocumentBrokerTests` — serialización (nunca 2 ops simultáneas del mismo
   doc), FIFO por sesión, eviction→`OnEvicting`→reopen con `loader`, actor Faulted propaga la excepción
   causal, semántica de dispose (`ObjectDisposedException` predecible, nunca crash).
6. **Load test (T041)**: proyecto nuevo `Weft.LoadTest` — cientos de docs × tareas concurrentes sostenidas →
   consistencia final + memoria acotada (medición GC/working set; **SC-006**).
7. **CI (T042)**: job nightly `load-test` en `ci.yml` — no bloqueante en PR, **bloqueante para el cierre de M1**.

Namespace público **`Weft.Concurrency`** (coherente con `Weft`/`Weft.Versioning`; lo congela `core-api.md`),
en la carpeta `src/Weft.Core/Concurrency/` (el proyecto `Weft.Core` aloja M0+M1 por `plan.md`).

**Out of scope:**

- Relay servidor, protocolo y-sync, awareness, persistencia y authz (US3, M2) — hito posterior; aquí solo se
  entrega `DocumentSession` + evento `UpdateApplied` como superficie que M2 consumirá.
- Empaquetado NuGet multi-RID y release OSS (US4, M3).
- Hardening del decoder ante input de red no confiable (FU-002, amplificación de memoria) — diferido a M2
  (capa de red). El broker asume input confiable en M1 (US2 escenario 3).
- Versionado nativo de Loro (FU-006) — mini-charter independiente; ortogonal a la concurrencia.

## Files to modify

<!-- Greenfield: la carpeta `src/Weft.Core/Concurrency/` no existe (solo hay referencias en XML-doc de
     ICrdtDoc.cs/YrsDoc.cs/VersionStore.cs que anticipan el broker). El proyecto `tests/Weft.LoadTest/`
     tampoco existe. Todo lo marcado `New` se crea en este Charter. La API v1 la fija core-api.md §Concurrencia. -->

| File | Change |
|---|---|
| `src/Weft.Core/Concurrency/DocumentBrokerOptions.cs` | New — opciones (IdleEviction/MaxActiveDocuments/OnEvicting) (T036) |
| `src/Weft.Core/Concurrency/DocumentActor.cs` | New — actor `internal`, canal unbounded single-reader, estados + drenado (T037) |
| `src/Weft.Core/Concurrency/DocumentBroker.cs` | New — registro/reuso/idle+LRU/DisposeAsync (T038) |
| `src/Weft.Core/Concurrency/DocumentSession.cs` | New — espejo async + ExecuteAsync + UpdateApplied (T039) |
| `tests/Weft.Core.Tests/DocumentBrokerTests.cs` | New — serialización, FIFO, eviction, Faulted, dispose (T040) |
| `tests/Weft.LoadTest/Weft.LoadTest.csproj` | New — proyecto del harness de carga (T041) |
| `tests/Weft.LoadTest/Program.cs` | New — harness de carga, SC-006 (T041) |
| `.github/workflows/ci.yml` | Change — job nightly `load-test` (no bloqueante en PR, bloqueante M1) (T042) |
| `Weft.sln` | Change — añadir `Weft.LoadTest` |
| `specs/001-weft-crdt-versioning/tasks.md` | Change — marcar T036–T042 `[X]` + `*CHARTER-03: <sha>*` |
| `.straymark/07-ai-audit/agent-logs/AILOG-*.md` | New, `risk_level: high` (concurrencia + memoria nativa a escala) |

## Verification

### Local checks

> **Lección de CHARTER-01/02**: correr TODO localmente en verde ANTES de pushear. La concurrencia es no
> determinista: los tests de serialización deben repetirse (iteraciones/estrés) para ser fiables.

```bash
# Build de toda la solución (incluye el nuevo proyecto Weft.LoadTest)
dotnet build Weft.sln -c Release

# Tests de concurrencia (serialización, FIFO, eviction→OnEvicting→reopen, Faulted, dispose)
dotnet test tests/Weft.Core.Tests/

# Suite completa verde (M0 sigue intacto)
dotnet test

# Load test local acotado (proxy de SC-006 antes del job nightly): cientos de docs, tareas
# concurrentes sostenidas → consistencia final + working set acotado (sin crecimiento monótono)
dotnet run -c Release --project tests/Weft.LoadTest/ -- --docs 300 --tasks 8 --seconds 30
```

### Production smoke (after deploy)

No aplica — librería sin despliegue. Los auditores externos deben saltar esta sección.

## Risks

- **R1 — Acceso concurrente al documento nativo corrompe el estado (P-V)**: severidad **crítica**.
  Mitigación: patrón actor/canal single-reader; el `ICrdtDoc` nunca se expone ni se ejecuta fuera del turno
  del actor; `DocumentBrokerTests` prueba con estrés que **nunca hay dos operaciones simultáneas** del mismo
  doc (contador de concurrencia con aserción). Si falla: corrupción silenciosa de datos → gate de tests
  bloquea el merge.
- **R2 — Fuga o doble liberación de memoria nativa en desalojo/dispose (P-I, SC-003)**: severidad alta.
  Mitigación: cada actor libera su doc **exactamente una vez** (guardas de estado Evicted/Faulted);
  `DisposeAsync` del broker drena y libera todo; tests de dispose semantics + el load-test observa el working
  set. Si falla: LSan/ASan (M0) y el load-test lo destapan.
- **R3 — Crecimiento no acotado de memoria bajo carga (SC-006)**: severidad alta. Mitigación:
  `MaxActiveDocuments` + desalojo LRU al superarlo + desalojo por inactividad (`IdleEviction`); el load-test
  sostenido mide que el working set no crece de forma monótona. Si falla: el job nightly `load-test` (gate de
  cierre de M1) queda rojo.
- **R4 — Actor en fallo irrecuperable bloquea operaciones (deadlock/cuelgue)**: severidad media. Mitigación:
  estado Faulted propaga la **excepción causal** a las operaciones pendientes y futuras; el doc se desaloja
  **sin** invocar `OnEvicting` (estado potencialmente inválido); test dedicado. Si falla: los awaits colgados
  se detectan como timeouts en los tests.
- **R5 — Fuga del `ICrdtDoc` fuera del turno vía `ExecuteAsync`**: severidad media. Capturar el `ICrdtDoc`
  del delegado y usarlo después rompe la serialización. Mitigación: contrato documentado (el doc no debe
  capturarse ni usarse fuera del delegado); el delegado corre íntegro dentro del turno del actor. Es un
  contrato de uso, no forzable por el compilador; se documenta en XML-doc y en el sample/tests.

## Tasks

1. Branch `feat/m1-concurrency` (ya creado desde main). Flip `declared` → `in-progress`.
2. Re-evaluar **Constitution Check** contra el scope (esta vez **P-V se activa y cierra** con el load-test).
3. `/speckit-implement` acotado a **T036–T042**; marcar `[X]` + `*CHARTER-03: <sha>*` por tarea.
4. **AILOG** (`risk_level: high`, `review_required: true`); **AIDEC** para decisiones de implementación
   nuevas (p. ej. estrategia LRU concreta, manejo de reentrancia en `OnEvicting`/`UpdateApplied`, forma del
   turno de `ExecuteAsync`). Las ✅ CERRADO del brief no se re-documentan.
5. **Verificación local COMPLETA** (bloque Local checks íntegro, incluido el load-test local) ANTES de push.
6. `straymark charter drift CHARTER-03` antes de commit; drifts → `R<N+1>` en el AILOG.
7. Commit + push + abrir PR contra `main`; CI verde.
8. **Auditoría externa StrayMark (condición de cierre — ver §Charter Closure)** antes de cerrar.

## Charter Closure

Como CHARTER-02, este Charter **requiere auditoría externa multi-modelo antes del cierre** (el corte cierra
el hito M1 y activa el principio constitucional P-V; amerita revisión cross-modelo):

1. Actualización atómica del Charter si el drift check reveló divergencias (mismo PR).
2. `straymark charter drift CHARTER-03 --range origin/main..HEAD` → limpio o documentado.
3. **Auditoría externa** (`straymark charter audit CHARTER-03`): el agente genera el prompt con
   `/straymark-audit-prompt` **solo con CI verde y árbol estable**; el **operador** ejecuta ≥2 auditores CLI
   vía `/straymark-audit-execute`; el agente consolida con `/straymark-audit-review`. Los findings `real_debt`
   se remedian antes de cerrar; el bloque `external_audit` de la telemetría se llena con la calibración.
4. `straymark charter close CHARTER-03` (telemetría, status `closed`). No borrar este archivo.
