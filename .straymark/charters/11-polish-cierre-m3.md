---
charter_id: CHARTER-11-polish-cierre-m3
status: closed
closed_at: 2026-07-15
execution_ailogs: [AILOG-2026-07-15-003]
effort_estimate: L
trigger: "CHARTER-10 cerró G1/FU-006 (PR #26, merged 2026-07-16): ya no quedan mini-charters de follow-up en la secuencia de M3, y T061–T063 son el último bloque antes del publish operador-gated (T060). Es la primera vez que el runbook de quickstart.md se ejercitará end-to-end contra el árbol de HEAD."
originating_spec: specs/001-weft-crdt-versioning/spec.md
work_verb: implement
design_provenance: new
---

# Charter: Polish: cierre de M3 — doc de arquitectura, benchmark delta-size (SC-004) y validación quickstart

> **Status (mirrored from frontmatter — source of truth is above):** declared. Effort: L.
>
> **Origin:** Fase `Polish` (P8) de `specs/001-weft-crdt-versioning/`; ejecuta T061, T062 y T063, las tres tareas que CHARTER-07 declaró explícitamente fuera de su alcance y difirió a este bloque.

## Context

M3 tiene cerrado todo su trabajo de user-story: US4 (empaquetado multi-RID) quedó verde en CHARTER-07 y el dual-path de Loro cerró su último gap (G1/FU-006) en CHARTER-10. Lo que queda es la fase `Polish` de SpecKit: T061 (doc de arquitectura), T062 (benchmark de delta-size que aserta SC-004) y T063 (pase de validación end-to-end de `quickstart.md` US1–US5 + los 6 gates de CI). Sólo T060 —el publish real a NuGet.org, operador-gated— queda fuera, porque su ejecución es una decisión humana, no de código.

Este Charter se declara como **L** y no como M por la guía de [`POLISH-CHARTER-PATTERN.md`](../00-governance/POLISH-CHARTER-PATTERN.md): el Charter de cierre de una Etapa es un **gate de detección de deuda**, no limpieza cosmética, porque es el primer lugar donde el runbook documentado se ejercita contra el árbol real en vez de contra un harness de tests. Weft cumple los umbrales de adopción del patrón: `quickstart.md` documenta comandos que nunca se han corrido end-to-end desde HEAD, y hay artefactos cuyo sitio de declaración y sitio de cableado viven en módulos distintos (el header C `weft_ffi.h` vs `NativeMethods.cs` vs `lib.rs`; los gates declarados en `quickstart.md` §gates vs los jobs reales de `ci.yml`).

El patrón ya cobró su primera pieza **antes de empezar**: el reconocimiento previo destapó que `quickstart.md:45` manda `dotnet test --filter Category=Concurrency`, pero no existe ni un solo `[Trait("Category", ...)]` en el repo — el comando de US2 pasa en verde **ejecutando cero tests**. Es la sub-clase 1 del anti-patrón *"surface declaration without wiring"* (runbook declara, código no cablea), y es exactamente el tipo de hallazgo que las suites por-Charter no pueden ver.

## Scope

**In scope:**

1. **T061** — `docs/architecture.md` nuevo: módulos y grafo de dependencias, frontera FFI, flujo de sync, modelo de versionado, y el **contrato público de ownership de memoria** (hoy sólo vive en comentarios de `weft_ffi.h`/`lib.rs` y en `contracts/ffi-abi.md`, que es spec interna, no doc público). Enlaza a `research.md` R1–R17 para las decisiones en vez de reexplicarlas.
2. **T061** — `README.md` y `docs/api/README.md` enlazan el nuevo doc de arquitectura.
3. **T062** — `tests/Weft.Core.Tests/DeltaSizeBenchmark.cs` nuevo: **define** el escenario de referencia de reconexión (hoy inexistente como definición) y aserta reducción ≥ 90 % de `ExportUpdateSince(sv)` vs `ExportState()`, con mensaje diagnóstico que reporta ambos tamaños y el ratio medido.
4. **T063** — `DocumentBrokerTests` (y cualquier otro test de concurrencia) gana `[Trait("Category", "Concurrency")]`, de modo que el comando de US2 del runbook ejecute lo que dice ejecutar.
5. **T063** — Pase de validación end-to-end de `quickstart.md` US1–US5 + los 6 gates, con **triage explícito** de qué se ejecutó, qué no, y por qué.
6. **T063** — `checklists/requirements.md` gana la evidencia de cierre del pase (sección de checkboxes por US y por gate + notas con comando/resultado/fecha).
7. **T063** — Corrección atómica de la deriva de runbook que el pase destape en `quickstart.md` (el runbook es la especificación del test; si está mal, se corrige aquí).
8. `tasks.md` marca T061–T063; AILOG de ejecución.

**Out of scope:**

- **T060 (publish real a NuGet.org)** — operador-gated por diseño; el `dry_run` ya validó el pipeline en CHARTER-07. Es una decisión humana, no de este Charter.
- **Los gaps que el pase de T063 destape** — se **triagean** a follow-ups/Charters follow-on, no se absorben aquí. Es la regla explícita del patrón de Polish (paso 2 del walkthrough): el Charter de Polish es el vehículo de descubrimiento, no el de remediación. Única excepción: la deriva de documentación del propio runbook (punto 7), que el patrón manda corregir atómicamente.
- **FU-017 (test de paridad header↔binding del shim Loro)** — es literalmente la sub-clase 5 del anti-patrón y este Charter la deja anotada como tal, pero su implementación tiene Charter propio pendiente.
- **FU-010, FU-015, FU-016** — follow-ups abiertos, ninguno bloqueante para M3.
- **Guards de CI para las sub-clases del anti-patrón** — el patrón los sugiere (paso 5) tras el retrospectivo; candidatos a follow-on, no scope de aquí.

## Files to modify

| File | Change |
|---|---|
| `docs/architecture.md` | New — módulos + grafo de dependencias, frontera FFI, flujo de sync, modelo de versionado, contrato público de ownership; decisiones enlazadas a `research.md` |
| `tests/Weft.Core.Tests/DeltaSizeBenchmark.cs` | New — escenario de referencia SC-004 definido + assert de reducción ≥ 90 % |
| `tests/Weft.Core.Tests/DocumentBrokerTests.cs` | `[Trait("Category", "Concurrency")]` a nivel de clase — cablea el filtro que US2 declara |
| `specs/001-weft-crdt-versioning/checklists/requirements.md` | Evidencia de cierre del pase US1–US5 + 6 gates (checkboxes + notas con comando/resultado/fecha) |
| `specs/001-weft-crdt-versioning/quickstart.md` | Corrección atómica de la deriva de runbook detectada durante el pase |
| `specs/001-weft-crdt-versioning/tasks.md` | Marcar T061, T062, T063 |
| `README.md` | Enlace al doc de arquitectura |
| `docs/api/README.md` | Enlace al doc de arquitectura (relación overview-por-paquete ↔ arquitectura) |
| `.straymark/follow-ups-backlog.md` | Follow-ups nuevos que el pase destape (triage, no remediación) |
| `.straymark/07-ai-audit/agent-logs/AILOG-2026-07-15-003-charter-11-polish-cierre-m3.md` | New, `risk_level: low` |

## Verification

### Local checks

```bash
# Setup: el nativo debe existir antes de cualquier test .NET
cargo build --release --manifest-path native/weft-yrs-ffi/Cargo.toml
cargo build --release --manifest-path native/weft-loro-ffi/Cargo.toml

# Build
dotnet build Weft.sln -c Release

# T062 — el benchmark de delta-size, aislado
dotnet test tests/Weft.Core.Tests -c Release --filter "FullyQualifiedName~DeltaSizeBenchmark"

# T063 — el filtro de US2 debe ejecutar >0 tests (hoy ejecuta 0: el bug que este Charter cablea)
dotnet test tests/Weft.Core.Tests -c Release --filter Category=Concurrency

# T063 — suite completa + gates locales
dotnet test Weft.sln -c Release
cargo test --manifest-path native/weft-yrs-ffi/Cargo.toml
cargo test --manifest-path native/weft-loro-ffi/Cargo.toml

# Gate de memoria (linux-x64 + nightly)
RUSTFLAGS="-Zsanitizer=address" cargo +nightly test \
  --target x86_64-unknown-linux-gnu --manifest-path native/weft-yrs-ffi/Cargo.toml

# Governance
straymark validate --include-charters
```

### Production smoke (after deploy)

No aplica: Weft es una librería, no un servicio desplegado. **Pero** dos escenarios del runbook no son ejecutables en un shell limpio de esta máquina y NO deben clasificarse como `real_debt` si no se ejecutan aquí:

```bash
# US3 — validación manual con Tiptap real, 2+ clientes en navegador.
# Requiere interacción humana; el criterio de cierre de M2 la exige explícitamente.
dotnet run --project samples/Weft.Sample.Server
cd samples/tiptap-client && npm install && npm run dev   # abrir 2 pestañas

# US4 — instalación en máquina limpia por RID (win-x64, osx-arm64, linux-arm64).
# Requiere runners/máquinas por RID; en CI lo cubre el job `pack-smoke`.
dotnet new console && dotnet add package Weft.Core --source ./artifacts && dotnet run
```

## Risks

- **R1 — Tunear el escenario del benchmark hasta que pase (fraude de benchmark)**: probabilidad media, severidad alta. SC-004 cita `523 B→29 B`, pero el reconocimiento confirmó que ese dato sale de una celda etiquetada *"Rough perf"* en `docs/spikes/spike03/hallazgos-spike-03.md:38`, de un spike cuyo código es **desechable por diseño** (`docs/spikes/README.md:4-5`) y no está en este repo. No hay protocolo, ni tamaño de doc, ni nº de ediciones. El riesgo es elegir el escenario *después* de ver qué números dan ≥ 90 %.
  Mitigación: el escenario se define **primero** y por criterio semántico (reconexión: par al día + una edición incremental pequeña — la forma que SC-004 describe en prosa), y se escribe en el doc-comment **antes** de medir. Si el ratio medido no alcanza el 90 %, **no se ajusta el escenario para que pase**: se reporta el número real, se abre follow-up y se escala a decisión del operador. `523→29` se cita como contexto histórico, nunca como assert.
- **R2 — El pase de T063 destapa gaps y el Charter los absorbe (scope creep)**: probabilidad alta, severidad media. El patrón de Polish predice ~10 gaps en la primera sesión de la Etapa de referencia. Ya hay 1 confirmado antes de empezar (filtro `Category=Concurrency`).
  Mitigación: triage estricto — cada gap va a `.straymark/follow-ups-backlog.md` con su sub-clase del anti-patrón, y sólo se remedia aquí (a) la deriva del propio runbook y (b) el cableado del filtro de US2, ambos declarados ex-ante en `## Scope`. Si un gap es tan grave que bloquea el cierre de M3, se para el Charter y se escala al operador en vez de ampliarlo en silencio.
- **R3 — `docs/architecture.md` duplica `docs/api/README.md`/`plan.md` y se pudre**: probabilidad media, severidad media. `plan.md` ya tiene §Summary por paquete y §Project Structure; `docs/api/README.md` ya tiene la tabla de dependencias.
  Mitigación: el doc enlaza en vez de copiar (decisiones→`research.md` R1–R17, contratos→`contracts/`, API por paquete→`docs/api/README.md`) y su valor propio es lo que ningún otro doc tiene: la frontera FFI y el contrato público de ownership explicados para un consumidor externo. Si una sección no puede justificar por qué no es un enlace, se borra.
- **R4 — Marcar verde lo que no se ejecutó**: probabilidad media, severidad alta. US3 exige validación manual con Tiptap (2+ clientes) y US4 exige máquinas limpias por RID; ninguna es ejecutable en este shell. La tentación es marcar el checkbox porque «CI lo cubre».
  Mitigación: la evidencia de cierre distingue tres estados explícitos — ejecutado aquí (con comando y resultado), cubierto por CI (con el job y el run), y **no ejecutado** (con el motivo). Un ítem sin evidencia se queda sin marcar; `[x]` significa "lo corrí y lo vi", no "debería funcionar".
- **R5 — Añadir `[Trait]` destapa que los tests de concurrencia fallan al aislarse**: probabilidad baja, severidad media. Hoy corren siempre dentro de la suite completa; nunca se han ejecutado solos.
  Mitigación: se corre el filtro aislado como check local explícito. Si falla en aislamiento, es un hallazgo real (acoplamiento entre tests) y se trata bajo R2 (triage), no se revierte el `[Trait]` para ocultarlo.
- **R6 — El doc de arquitectura afirma cosas que el código ya no hace**: probabilidad media, severidad alta — un doc de arquitectura equivocado es peor que ninguno. `plan.md:5-11` advierte que sus secciones M0/M1 son inmutables y que «el código shippeado es la verdad, no este plan».
  Mitigación: cada afirmación estructural del doc se ancla a `archivo:línea` leído en HEAD, no a `plan.md` ni al brief. Lo que no se pueda anclar, no se afirma.

## Tasks

1. Sync main, branch `charter/11-polish-cierre-m3`.
2. T062: definir el escenario en el doc-comment, luego implementar `DeltaSizeBenchmark.cs` y medir (en ese orden — R1).
3. T063: cablear `[Trait("Category", "Concurrency")]` y verificar que el filtro de US2 ejecuta > 0 tests.
4. T063: pase end-to-end US1–US5 + 6 gates, con triage de gaps a follow-ups (R2) y estados de evidencia explícitos (R4).
5. T063: escribir la evidencia de cierre en `checklists/requirements.md`; corregir atómicamente la deriva del runbook.
6. T061: escribir `docs/architecture.md` anclado a HEAD (R6) y enlazarlo desde `README.md` y `docs/api/README.md`.
7. Marcar T061–T063 en `tasks.md`.
8. AILOG (`risk_level: low`, `review_required: false`).
9. Verificación local limpia.
10. `straymark charter drift CHARTER-11 --range origin/main..HEAD` antes de commit; documentar drift en el AILOG.
11. Commit + push + PR.

## Charter Closure

Al cerrar este Charter:

1. **Atomic update (format v4)**: si el drift check reporta deriva no capturada en el AILOG, editar `## Files to modify` y/o añadir `## Closing notes` **en este mismo PR**.
2. **Post-merge drift check**: `straymark charter drift CHARTER-11 --range origin/main..HEAD`.
3. ~~**Mover la fila** en `.straymark/charters/README.md`~~ — no aplica: este repo no mantiene ese
   índice (el estado vive en el frontmatter y en `straymark charter list`).
4. **Status frontmatter** `in-progress` → `closed` + `closed_at`. ✔
5. **Retrospectivo del patrón de Polish** (paso 4 del walkthrough): ver §Retrospectivo. ✔
6. **No borrar** este archivo.

## Closing notes

- `.straymark/07-ai-audit/agent-logs/AILOG-2026-07-16-NNN.md` → renombrado a
  `AILOG-2026-07-15-003-charter-11-polish-cierre-m3.md`. La declaración usaba un placeholder con fecha
  estimada (`2026-07-16`) y sin slug; el Charter se ejecutó entero el 2026-07-15. Corregido
  atómicamente en el mismo PR. Único drift del Charter: los otros 10 archivos declarados se tocaron
  tal cual, sin omisiones ni expansión de scope.

## Retrospectivo (patrón de Polish, paso 4)

**Resultado: 9 gaps, 0 fallos de código de producción.** El pase confirmó la tesis del patrón —el
Charter de Polish como gate de detección de deuda— pero con un perfil de causa raíz muy concentrado.
No hace falta un AIDEC: el volumen es alto, la clasificación es de una sola categoría, y el AILOG ya
la documenta entera.

| Causa raíz | Gaps | Comentario |
|---|---|---|
| *Surface declaration without wiring* (sub-clase 1: el runbook declara, nada cablea) | **9 de 9** | Todos |
| Ambient dependency rot | 0 | El pinning (`rust-toolchain.toml`, versiones exactas) hizo su trabajo |
| Fallo de código de producción | 0 | El código estaba bien en los 9 casos |

Tres observaciones que valen para el próximo Polish:

1. **El runbook fue la única fuente de deuda**, y no es casualidad: es el único artefacto del repo que
   *nadie ejecuta* en CI. El código, los tests y los gates se ejercitan en cada PR y por eso no
   derivan. Un documento que describe comandos y no los corre es, estructuralmente, donde la verdad se
   pudre primero.
2. **Los dos gaps más graves eran promesas de verificación falsas, no comandos rotos.** El filtro de
   US2 (#1) y el `pack-smoke` por-PR (#9) no fallaban: **pasaban en verde sin verificar nada**. Un
   comando roto se detecta la primera vez que alguien lo corre; una verificación vacía puede vivir
   indefinidamente porque su síntoma es idéntico al del éxito. Esta subclase —*verificación fantasma*—
   es la que más merece un guard mecánico (→ FU-020).
3. **La documentación derivada de comentarios de CI hereda su obsolescencia.** El único error de hecho
   que este Charter propagó a la documentación (afirmar que `fuzz` es `continue-on-error`) no salió de
   leer mal el YAML, sino de creerle a un comentario que lo afirma y lleva meses siendo falso. El pase
   adversarial lo cazó; el comentario sigue ahí (→ FU-018). La regla de anclar a `archivo:línea` en
   HEAD hay que aplicarla también a la infraestructura, no sólo al código.

**Predicción falsable** (el patrón la pide): el próximo Charter de Polish sobre este repo destapará
**menos gaps de runbook** —acaba de ejecutarse entero y corregirse—, pero seguirá destapando
*verificaciones fantasma* mientras no exista el guard de FU-020, porque hoy nada en CI las distingue
de un verde legítimo.
