---
id: AILOG-2026-07-11-001
title: "CHARTER-03: concurrencia — broker actor-canal y ciclo de vida a escala (T036–T042)"
status: accepted
created: 2026-07-11
agent: claude-opus-4-8
confidence: high
review_required: true
risk_level: high
eu_ai_act_risk: not_applicable
nist_genai_risks: []
iso_42001_clause: []
lines_changed: 1438
files_modified: []
observability_scope: none
tags: [concurrencia, actor, broker, ciclo-de-vida, memoria, crdt]
related: [AILOG-2026-07-10-002]
originating_charter: CHARTER-03-concurrencia-broker-actor-canal-y-ciclo-de-vida-a
---

# AILOG: CHARTER-03 — concurrencia broker actor-canal y ciclo de vida a escala (T036–T042)

## Summary

Corte de M1 (US2): capa de concurrencia serializada por documento (constitución P-V). Un actor por
`docId` drena un canal single-reader —el acceso concurrente directo al `ICrdtDoc` nativo es imposible
desde la API pública— y el `DocumentBroker` gestiona el ciclo de vida a escala: registro/reutilización,
desalojo por inactividad y por presión de memoria (LRU), y liberación determinista. `DocumentSession`
es el espejo async de `ICrdtDoc` (con `ExecuteAsync` atómico y evento `UpdateApplied` para M2). Validado
con `DocumentBrokerTests` (7 casos) y un harness de carga (`Weft.LoadTest`) que ejerció **~433k
desalojos con reapertura concurrente sin una sola inconsistencia** (SC-006). Cierra M1.

## Context

Ejecución de T036–T042 bajo `.straymark/charters/03-concurrencia-broker-actor-canal-y-ciclo-de-vida-a.md`,
sobre M0 (binding + versionado). El motor CRDT no es thread-safe (invariante de diseño P-V); esta capa
es el único camino soportado para compartir un documento entre hilos y es prerequisito del relay de
US3/M2 (`DocumentSession` = T047). Trabajo de **implementación** contra `contracts/core-api.md`
(§Concurrencia, que congela la API v1). Namespace público `Weft.Concurrency` en `src/Weft.Core/Concurrency/`.

## Actions Performed

1. **Opciones (T036)**: `DocumentBrokerOptions` — `IdleEviction`, `MaxActiveDocuments` (LRU al superar),
   `OnEvicting`, `IdleSweepInterval` (cadencia del barrido, con clamp por defecto).
2. **Actor (T037)**: `DocumentActor` (`internal`) — `Channel` unbounded single-reader; cola de
   `IWorkItem` genéricos; estados Active/Idle/Evicted/Faulted; el documento se libera **exactamente una
   vez** al terminar el bucle (grácil o por fallo); `UpdateApplied` se computa (delta desde el state
   vector previo) solo si alguna sesión lo escucha.
3. **Broker (T038)**: `DocumentBroker : IAsyncDisposable` — registro `docId→actor` con carga
   single-flight, barrido periódico (idle + LRU + limpieza de actores terminados), `OpenAsync` con
   `loader`, `DisposeAsync` que drena todo. Serialización por `_gate`.
4. **Sesión (T039)**: `DocumentSession : IAsyncDisposable` — espejo async (validación de argumentos
   síncrona antes de encolar, copia defensiva de buffers), `ExecuteAsync<T>` (turno atómico), evento
   `UpdateApplied`, refcount implícito de sesiones en el actor.
5. **Tests (T040)**: `DocumentBrokerTests` — serialización estricta (motor de prueba que detecta
   solape), FIFO por sesión, eviction→OnEvicting→reopen, actor Faulted propaga causal, dispose semantics.
6. **Load test (T041)**: proyecto `Weft.LoadTest` — cientos/miles de docs × tareas concurrentes;
   consistencia (longitud == inserciones confirmadas) + memoria acotada (working set). Tamaño por doc
   acotado (cap) y número de docs activos acotado (LRU) → memoria estable.
7. **CI (T042)**: job nightly `load-test` (schedule + workflow_dispatch, `if:` que lo excluye de PRs;
   bloqueante para el cierre de M1). Añadido `Weft.LoadTest` a `Weft.sln`.

## Modified Files

| File | Change Description |
|------|--------------------|
| `src/Weft.Core/Concurrency/DocumentBrokerOptions.cs` | New — opciones de ciclo de vida (T036) |
| `src/Weft.Core/Concurrency/DocumentActor.cs` | New — actor canal single-reader (T037) |
| `src/Weft.Core/Concurrency/DocumentBroker.cs` | New — registro/pooling/idle+LRU/dispose (T038) |
| `src/Weft.Core/Concurrency/DocumentSession.cs` | New — espejo async + ExecuteAsync + UpdateApplied (T039) |
| `tests/Weft.Core.Tests/DocumentBrokerTests.cs` | New — 7 tests de concurrencia (T040) |
| `tests/Weft.LoadTest/**` | New — harness de carga SC-006 (T041) |
| `.github/workflows/ci.yml` | Change — job nightly `load-test` + trigger schedule (T042) |
| `Weft.sln` | Change — añadido `Weft.LoadTest` |
| `specs/001-weft-crdt-versioning/tasks.md` | Change — T036–T042 marcadas `[X]` |
| `.straymark/charters/03-*.md` | Change — declaración + status in-progress |

## Decisions Made

- **Modelo de fallo del actor**: una excepción lanzada DENTRO del turno (op del doc o delegado de
  `ExecuteAsync`) se propaga al llamador y **faultea el actor**: las operaciones pendientes y futuras
  fallan con la MISMA excepción causal y el documento se libera **sin** `OnEvicting` (estado
  potencialmente inválido). La validación de argumentos ocurre síncronamente ANTES de encolar, así los
  errores del llamador (índice inválido, etc.) NO faultean el actor. Conservador por P-V (no arriesgar
  estado nativo inconsistente).
- **`UpdateApplied` perezoso**: el delta solo se computa (2 llamadas FFI extra) si alguna sesión tiene
  handler suscrito — el load test sin relay no paga ese coste.
- **Límite `MaxActiveDocuments` "suave"**: se reafirma en el barrido, no síncronamente en `OpenAsync`, y
  nunca desaloja un documento con sesiones vivas. Puede excederse transitoriamente (pico inicial antes
  del primer barrido). El LRU desaloja los menos-recientes SIN sesión bajo presión, aunque estén
  "tibios" — la corrección de la ventana de creación la garantiza el reintento de `OpenAsync`.
- **Refcount de sesiones (no re-resolución)**: mientras una sesión viva, su documento no se desaloja;
  `DocumentSession.DisposeAsync` no toca el ciclo de vida del documento (lo gestiona el broker).

## Impact

- **Functionality**: API async completa para operar muchos documentos concurrentes con serialización
  garantizada, pooling y persistencia/reapertura transparente.
- **Security/Memory**: la liberación exactamente-una-vez y el drenado en dispose preservan el contrato
  de ownership de M0 (P-I); el pooling acota la memoria nativa viva (SC-006).
- **Performance**: canal unbounded single-reader; el broker serializa el registro con un lock corto.

## Verification

- [x] `dotnet build Weft.sln -c Release` — 0 warnings / 0 errores
- [x] **52 tests .NET** verdes (Core 25 [18 M0 + 7 concurrencia], Versioning 25, Determinism 2)
- [x] **Load test** (2000 docs × 16 tareas × 30 s): ops=300k, **evictions=433k, 0 inconsistencias,
      0 errores**, working set 211 MB (acotado), pool acotado (peak 969 < 2000)
- [x] Serialización verificada con motor instrumentado (pico de concurrencia observada = 1)
- [ ] Revisión humana del operador (pendiente — `review_required: true`)
- [x] Auditoría externa StrayMark (3 auditores, 0 críticos/altos, 0 FP; **los 11 findings remediados** — ver abajo)

## Additional Notes

Tres riesgos NO anticipados en el Charter emergieron durante la ejecución (todos corregidos y con
regresión). El harness de carga (T041) fue determinante: destapó R7 y R8, que los tests unitarios
single-thread no exponían.

### Risk: R6 (new, not in Charter) — livelock en `OpenAsync` por entrada rancia de carga

Cuando el `loader` completaba **síncronamente**, `LoadAndRegisterAsync` corría hasta el final dentro del
lock de `OpenAsync` (lock re-entrante), se retiraba de `_loading` y registraba en `_actors`; acto seguido
`OpenAsync` **re-insertaba** el task ya completado en `_loading`. Esa entrada nunca se retiraba y, tras un
desalojo, apuntaba a un actor muerto → reapertura encontraba el task rancio, obtenía el actor muerto,
fallaba la verificación, reintentaba, y giraba al 99 % CPU. **Corregido**: `_loading` lo gestiona solo
`OpenAsync` (alta en el lock, baja en un `finally` tras el `await`); `LoadAndRegisterAsync` ya no lo toca.

### Risk: R7 (new, not in Charter) — carrera desalojo-vs-reapertura pierde updates (SC-006)

El barrido retiraba el actor de `_actors` y persistía su estado de forma **asíncrona** (drenar → export →
`OnEvicting`). En esa ventana, una reapertura concurrente del mismo `docId` no lo encontraba activo y
cargaba del store un snapshot **a medio escribir (o ausente)** → creaba un documento divergente/vacío;
su desalojo posterior sobrescribía el estado bueno → **updates perdidos** (el load test mostró
`len=0 esperado=241959`). Esta es exactamente la corrupción que P-V/SC-006 prohíben. **Corregido**:
el broker rastrea desalojos en vuelo (`_evicting: docId→Task`); `OpenAsync` que encuentra un desalojo en
curso **espera a que persista** antes de cargar. Validado con ~433k desalojos concurrentes y 0
inconsistencias.

### Risk: R8 (new, not in Charter) — pooling inefectivo y barrido frágil bajo carga

Dos defectos que impedían acotar memoria bajo carga real: (a) el LRU exigía un umbral de inactividad
(`idle >= gracia`) que bajo martilleo uniforme nunca se cumplía → el pool no desalojaba nada y crecía al
total de documentos; (b) el bucle del barrido de fondo solo capturaba `OperationCanceledException`, así
que una excepción en un barrido lo habría matado silenciosamente (sin desalojar jamás). **Corregido**:
el LRU desaloja los menos-recientes sin sesión bajo presión, sin umbral de inactividad (el orden por
inactividad protege a los recién usados); el bucle del barrido captura y sobrevive a cualquier fallo de
un pase individual.

### Nota: scope expansion del drift check (intencional, parser)

`straymark charter drift --range origin/main..HEAD` reporta 2 archivos "no declarados":
`Weft.sln` y `tests/Weft.LoadTest/Weft.LoadTest.csproj`. Ambos SON scope del Charter: el `.csproj` es
el proyecto del harness declarado en §Files (T041) y `Weft.sln` se declaró como `Change — añadir
Weft.LoadTest`. El parser no los matchea (rutas sin `/` como `Weft.sln`, y el `.csproj` junto a su
`Program.cs`); no hay expansión de alcance real fuera de T036–T042. Mismo patrón que CHARTER-01.

### Nota: decisiones de concurrencia candidatas a AIDEC

El modelo de fallo del actor y el mecanismo `_evicting`-await (R7) son decisiones de diseño
sustantivas descubiertas en ejecución; se documentan aquí y pueden promoverse a AIDEC si la auditoría
externa lo recomienda.

## Auditoría externa y remediación (2026-07-11)

Auditoría multi-modelo de 3 auditores independientes de familias distintas (glm-5.2 8.1, gpt-5-5 9.4,
qwen3-7-max 8.7). Consolidada en `.straymark/audits/CHARTER-03/review.md`: **11 findings únicos, 0
Critical/High tras calibración, 0 falsos positivos, 0 misattributions**. La convergencia de gpt+qwen en
el handler de `UpdateApplied` (G) y de glm+qwen en el dead code (A) fue señal genuina (independencia
verificada). El load test (SC-006) fue el instrumento que expuso los tres riesgos R6/R7/R8 en ejecución.

**Los 11 findings se remediaron en este mismo PR** (backlog a cero), con regresión:

| # | Finding | Remediación | Auditor(es) |
|---|---------|-------------|-------------|
| F | `DisposeAsync` no esperaba cargas en vuelo → liberación no determinista | `DisposeAsync` espera `_loading`; la carga durante apagado libera su actor con `await` (no fire-and-forget) | gpt-5-5 |
| I | race `_state`/`_fault` podía persistir un doc faulted | `FinalizeAsync` decide por `_fault` (autoritativo), no por `_state` | qwen3-7-max |
| G | handler de `UpdateApplied` que lanza faulteaba el actor | `NotifySessions` aísla cada handler en try/catch + traza | gpt-5-5, qwen3-7-max |
| B | fallo de `OnEvicting` tragado sin observabilidad | `Debug.WriteLine` del fallo del hook (path de liberación intacto) | glm-5.2, gpt-5-5 |
| H | cancelación de un caller envenenaba la carga single-flight compartida | la carga usa el token del broker; cada caller aplica su `ct` vía `WaitAsync` | gpt-5-5 |
| A | `_persistOnEnd` dead code | campo eliminado, condición simplificada | glm-5.2, qwen3-7-max |
| C | `MaxActiveDocuments` sin validación | guard `ThrowIfNegativeOrZero` en el ctor del broker | glm-5.2 |
| K | LINQ O(n²) en el barrido (`toEvict.Contains`) | `toEvict` pasa a `HashSet` (exclusión O(1)) | qwen3-7-max |
| E | comentario "cota dura" obsoleto en el harness | comentario corregido (cota de activos suave; criterio duro = working set) | glm-5.2 |
| D | test LRU asertaba conteo, no identidad | verifica que 'a' (LRU) fue el desalojado y 'b'/'c' conservan contenido | glm-5.2 |
| J | falta test de `UpdateApplied` | test nuevo: delta aplicable notificado a otra sesión del mismo doc | qwen3-7-max |

El rediseño del ciclo de vida de `_loading` (F+H+R6 reconciliados con un `Task.Yield()` inicial en
`LoadAndRegisterAsync` que garantiza gestión consistente de la entrada) se revalidó con el load test
(1.3M desalojos, 0 inconsistencias) y 54 tests verdes (Core 27, incl. 2 regresiones nuevas G/J).
