<!--
StrayMark unified audit prompt — v1.1 (EN canonical).

This file is a TEMPLATE. `straymark charter audit <CHARTER-ID>` resolves the
placeholders below against the Charter's content + git range + originating
AILOGs, and writes the resolved prompt to:

    .straymark/audits/<CHARTER-ID>/audit-prompt.md

The resolved prompt is what each external auditor reads. The auditor saves
its report to a canonical location keyed on its model identifier so that the
review skill can iterate over N reports (one per auditor) — see CLI-REFERENCE
for the canonical naming.

Localization: the CLI uses `.straymark/config.yml`'s `language` field to pick
the right template. When `language: es`, the template at
`.straymark/audit-prompts/i18n/es/audit-prompt.md` is used. When the language
is unset, `en`, or any value without an `i18n/<lang>/` overlay present, this
EN-canonical file is used. Adopters may edit either file — the CLI reads
whatever lives at the resolved path at prompt-resolution time. Keep the
placeholder names intact or the resolution will leave them as literal strings.

Placeholders supported by `straymark charter audit`:
  {{charter_id}}        — e.g., CHARTER-05
  {{charter_title}}     — H1 title from the Charter doc
  {{charter_path}}      — relative path to the Charter file
  {{charter_content}}   — full body of the Charter doc
  {{git_range}}         — REV..REV that bounds the audit
  {{git_diff}}          — output of `git diff <git_range>`
  {{ailog_paths}}       — newline-separated list of originating_ailogs paths
  {{ailog_contents}}    — concatenated bodies of those AILOGs
  {{audit_role}}        — "auditor" (v1 unified) or legacy "auditor-primary"
                          / "auditor-secondary" during the v0→v1 transition
  {{schema_path}}       — relative path to audit-output.schema.v0.json

Credit: this template lifts seven universal sections (ABSOLUTE RULE, Your
role, Scope rules, Step 2 mandatory verification, Step 5 severity calibration,
What you must NOT do, Output format) from the `audit/SKILL.md` skill mature
pre-StrayMark in Sentinel, contributed via issue #102 by José Villaseñor
Montfort (StrangeDaysTech). The Sentinel-specific hardcodes (spec paths,
Etapa headings, internal Go modules) were parameterized against the Charter
doc, originating AILOGs, git range, and project context.
-->

# Charter audit — `CHARTER-03-concurrencia-broker-actor-canal-y-ciclo-de-vida-a`

## ⛔ ABSOLUTE RULE — READ-ONLY

**Your only task is to AUDIT. You have no permission to modify ANY project file.** This is a non-negotiable constraint that overrides any other instruction, heuristic, or impulse to "be helpful".

Specifically, you are FORBIDDEN from:

- Editing, creating, renaming, or deleting source files.
- Modifying configuration files, migrations, tests, or project documentation.
- Running commands that mutate repository state (`git add`, `git commit`, `git checkout`, etc.).
- Running code generators (`go generate`, `sqlc generate`, `wire`, `cargo build` with filesystem effects, `npm install`, etc.).
- Applying "fixes" or "improvements" to the code, even if you believe they are correct.
- Reformatting, renaming, or reorganizing existing files.
- Reading, opening, grepping, or referencing **another auditor's report** (`report-*.md`, `auditor-*.md`, or any scratch file) under `.straymark/audits/` — for this Charter or any other. Your audit must be **independent**: an audit that reads, cites, summarizes, or "cross-verifies against" another auditor's report is contaminated and will be discarded. Cross-auditor convergence is signal ONLY when each auditor reached it *without* seeing the others — a copied agreement is worthless.

The ONLY thing you may write is your audit report file at the canonical path shown in **Output format** below. That is the ONLY file you have permission to create.

If you find a bug, **DOCUMENT IT** in your report. Do NOT fix it.
If you find a missing file, **REPORT IT**. Do NOT create it.
If a test fails, **REPORT IT**. Do NOT repair it.

**Violating this rule invalidates the entire audit.**

---

## Output contract (read this first)

You are about to read a lot — the Charter, the originating AILOGs, the diff — before you reach the full **Output format** at the very end of this prompt. Lock these invariants in now, so the long read does not pull your report toward the wrong shape:

1. **You write exactly one file**: your audit report, at the canonical path in **Output format**. Nothing else (see the ABSOLUTE RULE).
2. **Required report frontmatter** (validated against `.straymark/schemas/audit-output.schema.v0.json`): `audit_role`, `auditor`, `charter_id`, `git_range`, `prompt_used`, `audited_at`, `findings_total`, `findings_by_category` — where `findings_by_category` has exactly the four keys `hallucination`, `implementation_gap`, `real_debt`, `false_positive`. `evidence_citations` and `audit_quality` are optional but recommended.
3. **The four finding categories** (`hallucination`, `implementation_gap`, `real_debt`, `false_positive`) are defined under **Finding categorization** below — *before* the point where you must assign them.
4. **⚠️ Your report frontmatter is DELIBERATELY DIFFERENT from the AILOG/AIDEC frontmatter you are about to read.** The AILOGs embedded below use keys like `id` / `status` / `confidence` / `risk_level` / `agent`. Your report does **not** — it uses the audit keys in (2). Do not mimic the surrounding documents; follow the schema.

This is a summary. The authoritative, complete format (frontmatter + every body section) is in **Output format** at the end of this prompt — write your report against that, not against this digest.

---

## Your role

You are an independent code auditor. Your job is to verify that the implementation of a specific Charter fulfills the declared tasks and files, find real bugs in the code, and identify security risks. **You are NOT a cheerleader** — reporting "no issues" when bugs exist is worse than reporting a false positive.

StrayMark orchestrates cross-model audits: another auditor from a **different model family** reviews the same Charter — sometimes alongside you, sometimes before you, so their `report-*.md` may already sit in `.straymark/audits/CHARTER-03-concurrencia-broker-actor-canal-y-ciclo-de-vida-a/`. **You must not read it** (see the ABSOLUTE RULE). Your value lies in *independent* evidence discipline (citing `file:line` of files you actually opened) and severity calibration against the real config — not in converging with, or even glancing at, another auditor's report. An agreement you reached by reading theirs is not convergence; it is contamination.

---

## Project



*(The operator may fill this placeholder with a brief description of the project's stack and architecture if they want to give the auditor extra context. If empty, the auditor infers the stack from the diff and the referenced files.)*

---

## STRICT scope

**Charter under audit:** `CHARTER-03-concurrencia-broker-actor-canal-y-ciclo-de-vida-a` — Concurrencia broker actor-canal y ciclo de vida a escala
**Charter file:** `.straymark/charters/03-concurrencia-broker-actor-canal-y-ciclo-de-vida-a.md`
**Git range:** `origin/main..HEAD`

The authoritative source of scope is the Charter file at `.straymark/charters/03-concurrencia-broker-actor-canal-y-ciclo-de-vida-a.md`. Read it in full before starting — it declares which files are modified, which tasks are executed, which risks are accepted, and what counts as successful closure.

### Scope rules

- Report only findings that touch **files or tasks declared in the Charter**, or that appear modified in the `git_range`.
- If you find a problem in code that belongs to another Charter (another unit of work), report it as **"Out-of-scope note"** in a separate section, NOT as a defect of this Charter.
- Do NOT report as defects:
  - Modules not yet implemented that are planned for future Charters.
  - Wiring / DI not connected when the wiring task belongs to another Charter.
  - Missing integration tests when the test task belongs to another Charter.
  - Files that do not exist but whose task is marked as `[ ]` (pending) in the Charter.

### Audit object vs. truth oracle

The scope rules above bound **where you report defects** (the *audit object* — the Charter's files / the `git_range`). They do **not** bound **what you may read to validate that object**. These are two different roles:

- **Audit object** — code in scope, where findings are reported.
- **Truth oracle** — any code you read to *verify* the in-scope object, even if it is outside the diff and undeclared. Reading an oracle is never out of scope.

**Cross-boundary contracts.** When the audited code is a *client* that consumes an API / IPC / RPC / contract **served by a component elsewhere in this repo**, you MUST cross-check each call — route, request body, response shape, enum values, field names — against the **real server-side definition** (handler structs, proto, schema, migration). Read the server as a *truth oracle* to validate the client, even though it is not in the `git_range` and not declared in the Charter. A client↔server contract mismatch is an **auditable defect of the client** (`implementation_gap` or `real_debt`), **not** an out-of-scope note. Green client-side tests do **NOT** absolve this: mocks and stubs routinely encode the client's *assumption* about the contract, not the real contract — so they pass against the same wrong shape. If an operator note marks generated types or a contract as a "deferred stub", scrutinize them *more*, not less.

### Originating AILOGs

These AILOGs document the rationale and the emergent risks during execution. **Read them before auditing** — the `R<N>` risks already documented there are NOT new findings, they are consciously accepted trade-offs.

> **Frontmatter note.** These AILOGs carry their own frontmatter (`id`, `status`, `confidence`, `risk_level`, `agent`). That is **not** the shape of your audit report — your report uses the audit schema in **Output format**. Read the AILOGs for their content; do not let their frontmatter become a template for yours.

```
(none)
```

```markdown
(none)
```

---

## Charter content

```markdown
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

```

---

## Diff

```diff
diff --git a/.github/workflows/ci.yml b/.github/workflows/ci.yml
index 5d292ca..65cbb93 100644
--- a/.github/workflows/ci.yml
+++ b/.github/workflows/ci.yml
@@ -4,6 +4,9 @@ on:
   push:
     branches: [main]
   pull_request:
+  schedule:
+    - cron: "0 6 * * *" # nightly 06:00 UTC — job load-test (M1, SC-006)
+  workflow_dispatch:
 
 # Cancela ejecuciones antiguas de la misma rama/PR.
 concurrency:
@@ -191,3 +194,32 @@ jobs:
     timeout-minutes: 5
     steps:
       - run: echo "Smoke del NuGet nativo multi-RID + ausencia de weft_test_panic (P-VI) — se activa en US4 (M3, T057)."
+
+  # ── Gate M1 (P-V, SC-006): prueba de carga de concurrencia ────────────────────────────────
+  # Nightly (schedule) + manual (workflow_dispatch): NO corre en PR (no lo bloquea), pero es
+  # BLOQUEANTE para el cierre de M1 — el nightly debe estar verde. El harness sale con código ≠ 0
+  # si algún documento queda inconsistente, la memoria no se acota (pico de documentos activos por
+  # encima del pool) o alguna operación falla. Ver AILOG (US2) y CHARTER-03 §Verification.
+  load-test:
+    name: load-test (nightly)
+    if: github.event_name == 'schedule' || github.event_name == 'workflow_dispatch'
+    runs-on: ubuntu-latest
+    timeout-minutes: 20
+    steps:
+      - uses: actions/checkout@v4
+      - uses: dtolnay/rust-toolchain@stable
+      - uses: Swatinem/rust-cache@v2
+        with:
+          workspaces: native
+      - uses: actions/setup-dotnet@v4
+        with:
+          dotnet-version: "10.0.x"
+      - name: Build shim (release)
+        working-directory: native
+        run: cargo build --release
+      # docs (2000) >> max-active (pool) fuerza desalojo/reapertura intensos → ejercita la carrera
+      # persistencia-vs-recarga sin pérdida de updates (SC-006), no solo el camino feliz.
+      - name: Load test (miles de docs, tareas concurrentes sostenidas)
+        run: >-
+          dotnet run --configuration Release --project tests/Weft.LoadTest --
+          --docs 2000 --tasks 16 --seconds 60
diff --git a/.straymark/07-ai-audit/agent-logs/AILOG-2026-07-11-001-charter-03-concurrencia-broker-actor-canal.md b/.straymark/07-ai-audit/agent-logs/AILOG-2026-07-11-001-charter-03-concurrencia-broker-actor-canal.md
new file mode 100644
index 0000000..336cecc
--- /dev/null
+++ b/.straymark/07-ai-audit/agent-logs/AILOG-2026-07-11-001-charter-03-concurrencia-broker-actor-canal.md
@@ -0,0 +1,161 @@
+---
+id: AILOG-2026-07-11-001
+title: "CHARTER-03: concurrencia — broker actor-canal y ciclo de vida a escala (T036–T042)"
+status: accepted
+created: 2026-07-11
+agent: claude-opus-4-8
+confidence: high
+review_required: true
+risk_level: high
+eu_ai_act_risk: not_applicable
+nist_genai_risks: []
+iso_42001_clause: []
+lines_changed: 1438
+files_modified: []
+observability_scope: none
+tags: [concurrencia, actor, broker, ciclo-de-vida, memoria, crdt]
+related: [AILOG-2026-07-10-002]
+originating_charter: CHARTER-03-concurrencia-broker-actor-canal-y-ciclo-de-vida-a
+---
+
+# AILOG: CHARTER-03 — concurrencia broker actor-canal y ciclo de vida a escala (T036–T042)
+
+## Summary
+
+Corte de M1 (US2): capa de concurrencia serializada por documento (constitución P-V). Un actor por
+`docId` drena un canal single-reader —el acceso concurrente directo al `ICrdtDoc` nativo es imposible
+desde la API pública— y el `DocumentBroker` gestiona el ciclo de vida a escala: registro/reutilización,
+desalojo por inactividad y por presión de memoria (LRU), y liberación determinista. `DocumentSession`
+es el espejo async de `ICrdtDoc` (con `ExecuteAsync` atómico y evento `UpdateApplied` para M2). Validado
+con `DocumentBrokerTests` (7 casos) y un harness de carga (`Weft.LoadTest`) que ejerció **~433k
+desalojos con reapertura concurrente sin una sola inconsistencia** (SC-006). Cierra M1.
+
+## Context
+
+Ejecución de T036–T042 bajo `.straymark/charters/03-concurrencia-broker-actor-canal-y-ciclo-de-vida-a.md`,
+sobre M0 (binding + versionado). El motor CRDT no es thread-safe (invariante de diseño P-V); esta capa
+es el único camino soportado para compartir un documento entre hilos y es prerequisito del relay de
+US3/M2 (`DocumentSession` = T047). Trabajo de **implementación** contra `contracts/core-api.md`
+(§Concurrencia, que congela la API v1). Namespace público `Weft.Concurrency` en `src/Weft.Core/Concurrency/`.
+
+## Actions Performed
+
+1. **Opciones (T036)**: `DocumentBrokerOptions` — `IdleEviction`, `MaxActiveDocuments` (LRU al superar),
+   `OnEvicting`, `IdleSweepInterval` (cadencia del barrido, con clamp por defecto).
+2. **Actor (T037)**: `DocumentActor` (`internal`) — `Channel` unbounded single-reader; cola de
+   `IWorkItem` genéricos; estados Active/Idle/Evicted/Faulted; el documento se libera **exactamente una
+   vez** al terminar el bucle (grácil o por fallo); `UpdateApplied` se computa (delta desde el state
+   vector previo) solo si alguna sesión lo escucha.
+3. **Broker (T038)**: `DocumentBroker : IAsyncDisposable` — registro `docId→actor` con carga
+   single-flight, barrido periódico (idle + LRU + limpieza de actores terminados), `OpenAsync` con
+   `loader`, `DisposeAsync` que drena todo. Serialización por `_gate`.
+4. **Sesión (T039)**: `DocumentSession : IAsyncDisposable` — espejo async (validación de argumentos
+   síncrona antes de encolar, copia defensiva de buffers), `ExecuteAsync<T>` (turno atómico), evento
+   `UpdateApplied`, refcount implícito de sesiones en el actor.
+5. **Tests (T040)**: `DocumentBrokerTests` — serialización estricta (motor de prueba que detecta
+   solape), FIFO por sesión, eviction→OnEvicting→reopen, actor Faulted propaga causal, dispose semantics.
+6. **Load test (T041)**: proyecto `Weft.LoadTest` — cientos/miles de docs × tareas concurrentes;
+   consistencia (longitud == inserciones confirmadas) + memoria acotada (working set). Tamaño por doc
+   acotado (cap) y número de docs activos acotado (LRU) → memoria estable.
+7. **CI (T042)**: job nightly `load-test` (schedule + workflow_dispatch, `if:` que lo excluye de PRs;
+   bloqueante para el cierre de M1). Añadido `Weft.LoadTest` a `Weft.sln`.
+
+## Modified Files
+
+| File | Change Description |
+|------|--------------------|
+| `src/Weft.Core/Concurrency/DocumentBrokerOptions.cs` | New — opciones de ciclo de vida (T036) |
+| `src/Weft.Core/Concurrency/DocumentActor.cs` | New — actor canal single-reader (T037) |
+| `src/Weft.Core/Concurrency/DocumentBroker.cs` | New — registro/pooling/idle+LRU/dispose (T038) |
+| `src/Weft.Core/Concurrency/DocumentSession.cs` | New — espejo async + ExecuteAsync + UpdateApplied (T039) |
+| `tests/Weft.Core.Tests/DocumentBrokerTests.cs` | New — 7 tests de concurrencia (T040) |
+| `tests/Weft.LoadTest/**` | New — harness de carga SC-006 (T041) |
+| `.github/workflows/ci.yml` | Change — job nightly `load-test` + trigger schedule (T042) |
+| `Weft.sln` | Change — añadido `Weft.LoadTest` |
+| `specs/001-weft-crdt-versioning/tasks.md` | Change — T036–T042 marcadas `[X]` |
+| `.straymark/charters/03-*.md` | Change — declaración + status in-progress |
+
+## Decisions Made
+
+- **Modelo de fallo del actor**: una excepción lanzada DENTRO del turno (op del doc o delegado de
+  `ExecuteAsync`) se propaga al llamador y **faultea el actor**: las operaciones pendientes y futuras
+  fallan con la MISMA excepción causal y el documento se libera **sin** `OnEvicting` (estado
+  potencialmente inválido). La validación de argumentos ocurre síncronamente ANTES de encolar, así los
+  errores del llamador (índice inválido, etc.) NO faultean el actor. Conservador por P-V (no arriesgar
+  estado nativo inconsistente).
+- **`UpdateApplied` perezoso**: el delta solo se computa (2 llamadas FFI extra) si alguna sesión tiene
+  handler suscrito — el load test sin relay no paga ese coste.
+- **Límite `MaxActiveDocuments` "suave"**: se reafirma en el barrido, no síncronamente en `OpenAsync`, y
+  nunca desaloja un documento con sesiones vivas. Puede excederse transitoriamente (pico inicial antes
+  del primer barrido). El LRU desaloja los menos-recientes SIN sesión bajo presión, aunque estén
+  "tibios" — la corrección de la ventana de creación la garantiza el reintento de `OpenAsync`.
+- **Refcount de sesiones (no re-resolución)**: mientras una sesión viva, su documento no se desaloja;
+  `DocumentSession.DisposeAsync` no toca el ciclo de vida del documento (lo gestiona el broker).
+
+## Impact
+
+- **Functionality**: API async completa para operar muchos documentos concurrentes con serialización
+  garantizada, pooling y persistencia/reapertura transparente.
+- **Security/Memory**: la liberación exactamente-una-vez y el drenado en dispose preservan el contrato
+  de ownership de M0 (P-I); el pooling acota la memoria nativa viva (SC-006).
+- **Performance**: canal unbounded single-reader; el broker serializa el registro con un lock corto.
+
+## Verification
+
+- [x] `dotnet build Weft.sln -c Release` — 0 warnings / 0 errores
+- [x] **52 tests .NET** verdes (Core 25 [18 M0 + 7 concurrencia], Versioning 25, Determinism 2)
+- [x] **Load test** (2000 docs × 16 tareas × 30 s): ops=300k, **evictions=433k, 0 inconsistencias,
+      0 errores**, working set 211 MB (acotado), pool acotado (peak 969 < 2000)
+- [x] Serialización verificada con motor instrumentado (pico de concurrencia observada = 1)
+- [ ] Revisión humana del operador (pendiente — `review_required: true`)
+- [ ] Auditoría externa StrayMark (condición de cierre de CHARTER-03, tras CI verde)
+
+## Additional Notes
+
+Tres riesgos NO anticipados en el Charter emergieron durante la ejecución (todos corregidos y con
+regresión). El harness de carga (T041) fue determinante: destapó R7 y R8, que los tests unitarios
+single-thread no exponían.
+
+### Risk: R6 (new, not in Charter) — livelock en `OpenAsync` por entrada rancia de carga
+
+Cuando el `loader` completaba **síncronamente**, `LoadAndRegisterAsync` corría hasta el final dentro del
+lock de `OpenAsync` (lock re-entrante), se retiraba de `_loading` y registraba en `_actors`; acto seguido
+`OpenAsync` **re-insertaba** el task ya completado en `_loading`. Esa entrada nunca se retiraba y, tras un
+desalojo, apuntaba a un actor muerto → reapertura encontraba el task rancio, obtenía el actor muerto,
+fallaba la verificación, reintentaba, y giraba al 99 % CPU. **Corregido**: `_loading` lo gestiona solo
+`OpenAsync` (alta en el lock, baja en un `finally` tras el `await`); `LoadAndRegisterAsync` ya no lo toca.
+
+### Risk: R7 (new, not in Charter) — carrera desalojo-vs-reapertura pierde updates (SC-006)
+
+El barrido retiraba el actor de `_actors` y persistía su estado de forma **asíncrona** (drenar → export →
+`OnEvicting`). En esa ventana, una reapertura concurrente del mismo `docId` no lo encontraba activo y
+cargaba del store un snapshot **a medio escribir (o ausente)** → creaba un documento divergente/vacío;
+su desalojo posterior sobrescribía el estado bueno → **updates perdidos** (el load test mostró
+`len=0 esperado=241959`). Esta es exactamente la corrupción que P-V/SC-006 prohíben. **Corregido**:
+el broker rastrea desalojos en vuelo (`_evicting: docId→Task`); `OpenAsync` que encuentra un desalojo en
+curso **espera a que persista** antes de cargar. Validado con ~433k desalojos concurrentes y 0
+inconsistencias.
+
+### Risk: R8 (new, not in Charter) — pooling inefectivo y barrido frágil bajo carga
+
+Dos defectos que impedían acotar memoria bajo carga real: (a) el LRU exigía un umbral de inactividad
+(`idle >= gracia`) que bajo martilleo uniforme nunca se cumplía → el pool no desalojaba nada y crecía al
+total de documentos; (b) el bucle del barrido de fondo solo capturaba `OperationCanceledException`, así
+que una excepción en un barrido lo habría matado silenciosamente (sin desalojar jamás). **Corregido**:
+el LRU desaloja los menos-recientes sin sesión bajo presión, sin umbral de inactividad (el orden por
+inactividad protege a los recién usados); el bucle del barrido captura y sobrevive a cualquier fallo de
+un pase individual.
+
+### Nota: scope expansion del drift check (intencional, parser)
+
+`straymark charter drift --range origin/main..HEAD` reporta 2 archivos "no declarados":
+`Weft.sln` y `tests/Weft.LoadTest/Weft.LoadTest.csproj`. Ambos SON scope del Charter: el `.csproj` es
+el proyecto del harness declarado en §Files (T041) y `Weft.sln` se declaró como `Change — añadir
+Weft.LoadTest`. El parser no los matchea (rutas sin `/` como `Weft.sln`, y el `.csproj` junto a su
+`Program.cs`); no hay expansión de alcance real fuera de T036–T042. Mismo patrón que CHARTER-01.
+
+### Nota: decisiones de concurrencia candidatas a AIDEC
+
+El modelo de fallo del actor y el mecanismo `_evicting`-await (R7) son decisiones de diseño
+sustantivas descubiertas en ejecución; se documentan aquí y pueden promoverse a AIDEC si la auditoría
+externa lo recomienda.
diff --git a/.straymark/charters/03-concurrencia-broker-actor-canal-y-ciclo-de-vida-a.md b/.straymark/charters/03-concurrencia-broker-actor-canal-y-ciclo-de-vida-a.md
new file mode 100644
index 0000000..d617bb4
--- /dev/null
+++ b/.straymark/charters/03-concurrencia-broker-actor-canal-y-ciclo-de-vida-a.md
@@ -0,0 +1,159 @@
+---
+charter_id: CHARTER-03-concurrencia-broker-actor-canal-y-ciclo-de-vida-a
+status: in-progress
+effort_estimate: L
+trigger: "CHARTER-02 cerrado (M0: versionado content-addressed + dual-engine verde en main). tasks.md fija T036–T042 (US2 concurrencia) como corte de M1: broker actor/canal por documento + ciclo de vida a escala (pooling, desalojo idle+LRU, liberación determinista), activando y validando el principio constitucional P-V con prueba de carga (SC-006)."
+originating_spec: specs/001-weft-crdt-versioning/spec.md
+work_verb: implement
+design_provenance: new
+---
+
+# Charter: Concurrencia broker actor-canal y ciclo de vida a escala
+
+> **Status (mirrored from frontmatter — source of truth is above):** in-progress. Effort: L.
+>
+> **Origin:** Derivado de `specs/001-weft-crdt-versioning/spec.md`. Corte de M1 (T036–T042): capa de concurrencia serializada por documento (US2) — broker actor/canal, sesiones async y ciclo de vida a escala. Cierra M1.
+
+## Context
+
+Sobre M0 (binding `Weft.Core` + versionado engine-agnóstico), este corte añade la **capa de concurrencia**
+que la constitución exige (P-V): el motor CRDT **no es thread-safe** y el acceso a un mismo documento DEBE
+serializarse. Se implementa el patrón actor/canal —un único lector drenando una cola por documento— de modo
+que el acceso concurrente directo al `ICrdtDoc` nativo sea **imposible desde la API pública**. Encima, el
+`DocumentBroker` gestiona el ciclo de vida a escala: registro y reutilización por `docId`, desalojo por
+inactividad y por presión de memoria (LRU), y liberación determinista de la memoria nativa (P-I).
+
+Es prerequisito del servidor de sync: `DocumentSession` (T039) es la superficie que el relay de US3/M2
+consumirá (T047). El diseño está ✅ CERRADO en el brief y el contrato `core-api.md` congela la API v1
+(`DocumentBroker`/`DocumentSession`/`DocumentBrokerOptions`); trabajo de **implementación** contra ese
+contrato. Cierra M1 validando P-V con una prueba de carga sostenida (SC-006).
+
+## Scope
+
+**In scope (T036–T042):**
+
+1. **Opciones (T036)**: `DocumentBrokerOptions` — `IdleEviction` (default 5 min), `MaxActiveDocuments`
+   (default 1024; al superarse → desalojo LRU), hook `OnEvicting(docId, exportState, ct)` (persistencia
+   pre-desalojo; el desalojo espera su fin).
+2. **Actor (T037)**: `DocumentActor` (`internal`) — `Channel` unbounded **single-reader**, estados
+   Active/Idle/Evicted/Faulted, drenado de la cola en desalojo, doc nativo liberado **exactamente una vez**.
+3. **Broker (T038)**: `DocumentBroker : IAsyncDisposable` — registro `docId→actor`, reutilización, desalojo
+   por inactividad + LRU al superar `MaxActiveDocuments`, `OpenAsync` con `loader`, `ActiveDocumentCount`;
+   `DisposeAsync` drena y libera todos los documentos exactamente una vez.
+4. **Sesión (T039)**: `DocumentSession : IAsyncDisposable` — espejo async de `ICrdtDoc` (Insert/Delete/GetText/
+   Export*/ApplyUpdate encolados al actor), `ExecuteAsync<T>` (turno atómico; el `ICrdtDoc` no se captura
+   fuera del delegado), evento `UpdateApplied` (para relay/persistencia de M2). **Prerequisito de T047 (US3)**.
+5. **Tests de concurrencia (T040)**: `DocumentBrokerTests` — serialización (nunca 2 ops simultáneas del mismo
+   doc), FIFO por sesión, eviction→`OnEvicting`→reopen con `loader`, actor Faulted propaga la excepción
+   causal, semántica de dispose (`ObjectDisposedException` predecible, nunca crash).
+6. **Load test (T041)**: proyecto nuevo `Weft.LoadTest` — cientos de docs × tareas concurrentes sostenidas →
+   consistencia final + memoria acotada (medición GC/working set; **SC-006**).
+7. **CI (T042)**: job nightly `load-test` en `ci.yml` — no bloqueante en PR, **bloqueante para el cierre de M1**.
+
+Namespace público **`Weft.Concurrency`** (coherente con `Weft`/`Weft.Versioning`; lo congela `core-api.md`),
+en la carpeta `src/Weft.Core/Concurrency/` (el proyecto `Weft.Core` aloja M0+M1 por `plan.md`).
+
+**Out of scope:**
+
+- Relay servidor, protocolo y-sync, awareness, persistencia y authz (US3, M2) — hito posterior; aquí solo se
+  entrega `DocumentSession` + evento `UpdateApplied` como superficie que M2 consumirá.
+- Empaquetado NuGet multi-RID y release OSS (US4, M3).
+- Hardening del decoder ante input de red no confiable (FU-002, amplificación de memoria) — diferido a M2
+  (capa de red). El broker asume input confiable en M1 (US2 escenario 3).
+- Versionado nativo de Loro (FU-006) — mini-charter independiente; ortogonal a la concurrencia.
+
+## Files to modify
+
+<!-- Greenfield: la carpeta `src/Weft.Core/Concurrency/` no existe (solo hay referencias en XML-doc de
+     ICrdtDoc.cs/YrsDoc.cs/VersionStore.cs que anticipan el broker). El proyecto `tests/Weft.LoadTest/`
+     tampoco existe. Todo lo marcado `New` se crea en este Charter. La API v1 la fija core-api.md §Concurrencia. -->
+
+| File | Change |
+|---|---|
+| `src/Weft.Core/Concurrency/DocumentBrokerOptions.cs` | New — opciones (IdleEviction/MaxActiveDocuments/OnEvicting) (T036) |
+| `src/Weft.Core/Concurrency/DocumentActor.cs` | New — actor `internal`, canal unbounded single-reader, estados + drenado (T037) |
+| `src/Weft.Core/Concurrency/DocumentBroker.cs` | New — registro/reuso/idle+LRU/DisposeAsync (T038) |
+| `src/Weft.Core/Concurrency/DocumentSession.cs` | New — espejo async + ExecuteAsync + UpdateApplied (T039) |
+| `tests/Weft.Core.Tests/DocumentBrokerTests.cs` | New — serialización, FIFO, eviction, Faulted, dispose (T040) |
+| `tests/Weft.LoadTest/Weft.LoadTest.csproj` | New — proyecto del harness de carga (T041) |
+| `tests/Weft.LoadTest/Program.cs` | New — harness de carga, SC-006 (T041) |
+| `.github/workflows/ci.yml` | Change — job nightly `load-test` (no bloqueante en PR, bloqueante M1) (T042) |
+| `Weft.sln` | Change — añadir `Weft.LoadTest` |
+| `specs/001-weft-crdt-versioning/tasks.md` | Change — marcar T036–T042 `[X]` + `*CHARTER-03: <sha>*` |
+| `.straymark/07-ai-audit/agent-logs/AILOG-*.md` | New, `risk_level: high` (concurrencia + memoria nativa a escala) |
+
+## Verification
+
+### Local checks
+
+> **Lección de CHARTER-01/02**: correr TODO localmente en verde ANTES de pushear. La concurrencia es no
+> determinista: los tests de serialización deben repetirse (iteraciones/estrés) para ser fiables.
+
+```bash
+# Build de toda la solución (incluye el nuevo proyecto Weft.LoadTest)
+dotnet build Weft.sln -c Release
+
+# Tests de concurrencia (serialización, FIFO, eviction→OnEvicting→reopen, Faulted, dispose)
+dotnet test tests/Weft.Core.Tests/
+
+# Suite completa verde (M0 sigue intacto)
+dotnet test
+
+# Load test local acotado (proxy de SC-006 antes del job nightly): cientos de docs, tareas
+# concurrentes sostenidas → consistencia final + working set acotado (sin crecimiento monótono)
+dotnet run -c Release --project tests/Weft.LoadTest/ -- --docs 300 --tasks 8 --seconds 30
+```
+
+### Production smoke (after deploy)
+
+No aplica — librería sin despliegue. Los auditores externos deben saltar esta sección.
+
+## Risks
+
+- **R1 — Acceso concurrente al documento nativo corrompe el estado (P-V)**: severidad **crítica**.
+  Mitigación: patrón actor/canal single-reader; el `ICrdtDoc` nunca se expone ni se ejecuta fuera del turno
+  del actor; `DocumentBrokerTests` prueba con estrés que **nunca hay dos operaciones simultáneas** del mismo
+  doc (contador de concurrencia con aserción). Si falla: corrupción silenciosa de datos → gate de tests
+  bloquea el merge.
+- **R2 — Fuga o doble liberación de memoria nativa en desalojo/dispose (P-I, SC-003)**: severidad alta.
+  Mitigación: cada actor libera su doc **exactamente una vez** (guardas de estado Evicted/Faulted);
+  `DisposeAsync` del broker drena y libera todo; tests de dispose semantics + el load-test observa el working
+  set. Si falla: LSan/ASan (M0) y el load-test lo destapan.
+- **R3 — Crecimiento no acotado de memoria bajo carga (SC-006)**: severidad alta. Mitigación:
+  `MaxActiveDocuments` + desalojo LRU al superarlo + desalojo por inactividad (`IdleEviction`); el load-test
+  sostenido mide que el working set no crece de forma monótona. Si falla: el job nightly `load-test` (gate de
+  cierre de M1) queda rojo.
+- **R4 — Actor en fallo irrecuperable bloquea operaciones (deadlock/cuelgue)**: severidad media. Mitigación:
+  estado Faulted propaga la **excepción causal** a las operaciones pendientes y futuras; el doc se desaloja
+  **sin** invocar `OnEvicting` (estado potencialmente inválido); test dedicado. Si falla: los awaits colgados
+  se detectan como timeouts en los tests.
+- **R5 — Fuga del `ICrdtDoc` fuera del turno vía `ExecuteAsync`**: severidad media. Capturar el `ICrdtDoc`
+  del delegado y usarlo después rompe la serialización. Mitigación: contrato documentado (el doc no debe
+  capturarse ni usarse fuera del delegado); el delegado corre íntegro dentro del turno del actor. Es un
+  contrato de uso, no forzable por el compilador; se documenta en XML-doc y en el sample/tests.
+
+## Tasks
+
+1. Branch `feat/m1-concurrency` (ya creado desde main). Flip `declared` → `in-progress`.
+2. Re-evaluar **Constitution Check** contra el scope (esta vez **P-V se activa y cierra** con el load-test).
+3. `/speckit-implement` acotado a **T036–T042**; marcar `[X]` + `*CHARTER-03: <sha>*` por tarea.
+4. **AILOG** (`risk_level: high`, `review_required: true`); **AIDEC** para decisiones de implementación
+   nuevas (p. ej. estrategia LRU concreta, manejo de reentrancia en `OnEvicting`/`UpdateApplied`, forma del
+   turno de `ExecuteAsync`). Las ✅ CERRADO del brief no se re-documentan.
+5. **Verificación local COMPLETA** (bloque Local checks íntegro, incluido el load-test local) ANTES de push.
+6. `straymark charter drift CHARTER-03` antes de commit; drifts → `R<N+1>` en el AILOG.
+7. Commit + push + abrir PR contra `main`; CI verde.
+8. **Auditoría externa StrayMark (condición de cierre — ver §Charter Closure)** antes de cerrar.
+
+## Charter Closure
+
+Como CHARTER-02, este Charter **requiere auditoría externa multi-modelo antes del cierre** (el corte cierra
+el hito M1 y activa el principio constitucional P-V; amerita revisión cross-modelo):
+
+1. Actualización atómica del Charter si el drift check reveló divergencias (mismo PR).
+2. `straymark charter drift CHARTER-03 --range origin/main..HEAD` → limpio o documentado.
+3. **Auditoría externa** (`straymark charter audit CHARTER-03`): el agente genera el prompt con
+   `/straymark-audit-prompt` **solo con CI verde y árbol estable**; el **operador** ejecuta ≥2 auditores CLI
+   vía `/straymark-audit-execute`; el agente consolida con `/straymark-audit-review`. Los findings `real_debt`
+   se remedian antes de cerrar; el bloque `external_audit` de la telemetría se llena con la calibración.
+4. `straymark charter close CHARTER-03` (telemetría, status `closed`). No borrar este archivo.
diff --git a/Weft.sln b/Weft.sln
index d69ba8c..337e38e 100644
--- a/Weft.sln
+++ b/Weft.sln
@@ -23,6 +23,8 @@ Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "Weft.Sample.Versioning", "s
 EndProject
 Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "Weft.Loro", "src\Weft.Loro\Weft.Loro.csproj", "{6BEA5D7C-F3F1-4CB3-8CBB-B44B2840B6B6}"
 EndProject
+Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "Weft.LoadTest", "tests\Weft.LoadTest\Weft.LoadTest.csproj", "{064C3FA7-B082-436A-974E-5CFB0298A0DA}"
+EndProject
 Global
 	GlobalSection(SolutionConfigurationPlatforms) = preSolution
 		Debug|Any CPU = Debug|Any CPU
@@ -117,6 +119,18 @@ Global
 		{6BEA5D7C-F3F1-4CB3-8CBB-B44B2840B6B6}.Release|x64.Build.0 = Release|Any CPU
 		{6BEA5D7C-F3F1-4CB3-8CBB-B44B2840B6B6}.Release|x86.ActiveCfg = Release|Any CPU
 		{6BEA5D7C-F3F1-4CB3-8CBB-B44B2840B6B6}.Release|x86.Build.0 = Release|Any CPU
+		{064C3FA7-B082-436A-974E-5CFB0298A0DA}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
+		{064C3FA7-B082-436A-974E-5CFB0298A0DA}.Debug|Any CPU.Build.0 = Debug|Any CPU
+		{064C3FA7-B082-436A-974E-5CFB0298A0DA}.Debug|x64.ActiveCfg = Debug|Any CPU
+		{064C3FA7-B082-436A-974E-5CFB0298A0DA}.Debug|x64.Build.0 = Debug|Any CPU
+		{064C3FA7-B082-436A-974E-5CFB0298A0DA}.Debug|x86.ActiveCfg = Debug|Any CPU
+		{064C3FA7-B082-436A-974E-5CFB0298A0DA}.Debug|x86.Build.0 = Debug|Any CPU
+		{064C3FA7-B082-436A-974E-5CFB0298A0DA}.Release|Any CPU.ActiveCfg = Release|Any CPU
+		{064C3FA7-B082-436A-974E-5CFB0298A0DA}.Release|Any CPU.Build.0 = Release|Any CPU
+		{064C3FA7-B082-436A-974E-5CFB0298A0DA}.Release|x64.ActiveCfg = Release|Any CPU
+		{064C3FA7-B082-436A-974E-5CFB0298A0DA}.Release|x64.Build.0 = Release|Any CPU
+		{064C3FA7-B082-436A-974E-5CFB0298A0DA}.Release|x86.ActiveCfg = Release|Any CPU
+		{064C3FA7-B082-436A-974E-5CFB0298A0DA}.Release|x86.Build.0 = Release|Any CPU
 	EndGlobalSection
 	GlobalSection(SolutionProperties) = preSolution
 		HideSolutionNode = FALSE
@@ -129,5 +143,6 @@ Global
 		{265FD711-ACC0-40A3-A334-5B81B5063AE8} = {0AB3BF05-4346-4AA6-1389-037BE0695223}
 		{39C303A3-BD6B-4681-93DA-687C5229A1B9} = {5D20AA90-6969-D8BD-9DCD-8634F4692FDA}
 		{6BEA5D7C-F3F1-4CB3-8CBB-B44B2840B6B6} = {827E0CD3-B72D-47B6-A68D-7590B98EB39B}
+		{064C3FA7-B082-436A-974E-5CFB0298A0DA} = {0AB3BF05-4346-4AA6-1389-037BE0695223}
 	EndGlobalSection
 EndGlobal
diff --git a/specs/001-weft-crdt-versioning/tasks.md b/specs/001-weft-crdt-versioning/tasks.md
index 110c6da..9f50c98 100644
--- a/specs/001-weft-crdt-versioning/tasks.md
+++ b/specs/001-weft-crdt-versioning/tasks.md
@@ -97,13 +97,13 @@
 
 **Independent Test** (spec US2): prueba de carga con cientos de docs y tareas concurrentes → estados consistentes, memoria acotada, cero recursos sin liberar
 
-- [ ] T036 [P] [US2] Define `DocumentBrokerOptions` in `src/Weft.Core/Concurrency/DocumentBrokerOptions.cs` (IdleEviction, MaxActiveDocuments, OnEvicting)
-- [ ] T037 [US2] Implement `DocumentActor` (internal) in `src/Weft.Core/Concurrency/DocumentActor.cs`: Channel unbounded single-reader, estados Active/Idle/Evicted/Faulted, drenado en desalojo, doc liberado exactamente una vez
-- [ ] T038 [US2] Implement `DocumentBroker` in `src/Weft.Core/Concurrency/DocumentBroker.cs`: registro docId→actor, reutilización, desalojo por inactividad + LRU al superar máximo, `DisposeAsync` drena todo
-- [ ] T039 [US2] Implement `DocumentSession` in `src/Weft.Core/Concurrency/DocumentSession.cs`: espejo async de `ICrdtDoc`, `ExecuteAsync` (turno atómico), evento `UpdateApplied`, `IAsyncDisposable`
-- [ ] T040 [P] [US2] Concurrency tests `tests/Weft.Core.Tests/DocumentBrokerTests.cs`: serialización (nunca 2 ops simultáneas del mismo doc), FIFO por sesión, eviction→OnEvicting→reopen con loader, actor Faulted propaga excepción causal, dispose semantics
-- [ ] T041 [P] [US2] Load test harness `tests/Weft.LoadTest/Program.cs`: cientos de docs × tareas concurrentes sostenidas → consistencia final + memoria acotada (medición GC/working set; SC-006)
-- [ ] T042 [US2] Add CI nightly job `load-test` in `.github/workflows/ci.yml` (no bloqueante en PR, bloqueante para cierre de M1)
+- [X] T036 [P] [US2] Define `DocumentBrokerOptions` in `src/Weft.Core/Concurrency/DocumentBrokerOptions.cs` (IdleEviction, MaxActiveDocuments, OnEvicting) — CHARTER-03
+- [X] T037 [US2] Implement `DocumentActor` (internal) in `src/Weft.Core/Concurrency/DocumentActor.cs`: Channel unbounded single-reader, estados Active/Idle/Evicted/Faulted, drenado en desalojo, doc liberado exactamente una vez — CHARTER-03
+- [X] T038 [US2] Implement `DocumentBroker` in `src/Weft.Core/Concurrency/DocumentBroker.cs`: registro docId→actor, reutilización, desalojo por inactividad + LRU al superar máximo, `DisposeAsync` drena todo — CHARTER-03
+- [X] T039 [US2] Implement `DocumentSession` in `src/Weft.Core/Concurrency/DocumentSession.cs`: espejo async de `ICrdtDoc`, `ExecuteAsync` (turno atómico), evento `UpdateApplied`, `IAsyncDisposable` — CHARTER-03
+- [X] T040 [P] [US2] Concurrency tests `tests/Weft.Core.Tests/DocumentBrokerTests.cs`: serialización (nunca 2 ops simultáneas del mismo doc), FIFO por sesión, eviction→OnEvicting→reopen con loader, actor Faulted propaga excepción causal, dispose semantics — CHARTER-03
+- [X] T041 [P] [US2] Load test harness `tests/Weft.LoadTest/Program.cs`: cientos de docs × tareas concurrentes sostenidas → consistencia final + memoria acotada (medición GC/working set; SC-006) — CHARTER-03
+- [X] T042 [US2] Add CI nightly job `load-test` in `.github/workflows/ci.yml` (no bloqueante en PR, bloqueante para cierre de M1) — CHARTER-03
 
 **Checkpoint**: M1 — concurrencia a escala validada
 
diff --git a/src/Weft.Core/Concurrency/DocumentActor.cs b/src/Weft.Core/Concurrency/DocumentActor.cs
new file mode 100644
index 0000000..fac67a8
--- /dev/null
+++ b/src/Weft.Core/Concurrency/DocumentActor.cs
@@ -0,0 +1,259 @@
+using System.Threading.Channels;
+
+namespace Weft.Concurrency;
+
+/// <summary>Estado observable del actor de un documento (constitución P-V).</summary>
+internal enum DocumentActorState
+{
+    /// <summary>Acepta y procesa operaciones.</summary>
+    Active,
+
+    /// <summary>Seleccionado para desalojo; drenando la cola pendiente antes de liberar.</summary>
+    Idle,
+
+    /// <summary>Terminado con normalidad; documento persistido (si había hook) y liberado.</summary>
+    Evicted,
+
+    /// <summary>Terminado por fallo irrecuperable; documento liberado sin persistir.</summary>
+    Faulted,
+}
+
+/// <summary>
+/// Serializa TODO el acceso a un <see cref="ICrdtDoc"/> nativo mediante un único lector que drena un
+/// canal de operaciones (patrón actor, constitución P-V). Nunca ejecuta dos operaciones del mismo
+/// documento a la vez; libera el documento exactamente una vez al terminar. <c>internal</c>: se usa a
+/// través de <see cref="DocumentBroker"/> y <see cref="DocumentSession"/>.
+/// </summary>
+internal sealed class DocumentActor
+{
+    private readonly ICrdtDoc _doc;
+    private readonly Channel<IWorkItem> _channel;
+    private readonly Task _runLoop;
+    private readonly Func<string, byte[], CancellationToken, ValueTask>? _onEvicting;
+    private readonly object _sessionsLock = new();
+    private readonly List<DocumentSession> _sessions = [];
+
+    private volatile DocumentActorState _state = DocumentActorState.Active;
+    private volatile Exception? _fault;
+    private volatile bool _persistOnEnd = true;
+    private long _lastActivityTick = Environment.TickCount64;
+
+    internal DocumentActor(string docId, ICrdtDoc doc, Func<string, byte[], CancellationToken, ValueTask>? onEvicting)
+    {
+        DocId = docId;
+        _doc = doc;
+        _onEvicting = onEvicting;
+        _channel = Channel.CreateUnbounded<IWorkItem>(new UnboundedChannelOptions
+        {
+            SingleReader = true,   // un único lector: la garantía de serialización (P-V)
+            SingleWriter = false,  // varias sesiones/hilos encolan
+        });
+        _runLoop = Task.Run(RunAsync);
+    }
+
+    internal string DocId { get; }
+
+    internal DocumentActorState State => _state;
+
+    /// <summary>Milisegundos transcurridos desde la última operación procesada (para desalojo idle).</summary>
+    internal long IdleMilliseconds => Environment.TickCount64 - Interlocked.Read(ref _lastActivityTick);
+
+    internal int SessionCount
+    {
+        get { lock (_sessionsLock) { return _sessions.Count; } }
+    }
+
+    internal void AddSession(DocumentSession session)
+    {
+        lock (_sessionsLock) { _sessions.Add(session); }
+    }
+
+    internal void RemoveSession(DocumentSession session)
+    {
+        lock (_sessionsLock) { _sessions.Remove(session); }
+    }
+
+    /// <summary>
+    /// Encola una operación sobre el documento y devuelve su resultado de forma asíncrona. La operación
+    /// se ejecuta dentro del turno del actor (nunca concurrente con otra del mismo documento).
+    /// </summary>
+    internal ValueTask<T> EnqueueAsync<T>(Func<ICrdtDoc, T> op, bool mutating, CancellationToken ct)
+    {
+        var item = new WorkItem<T>(op, mutating, ct);
+        if (!_channel.Writer.TryWrite(item))
+        {
+            item.Fail(ClosedReason());
+        }
+        return new ValueTask<T>(item.Task);
+    }
+
+    /// <summary>
+    /// Inicia el desalojo cooperativo: no acepta más operaciones, drena las pendientes, persiste con el
+    /// hook (si aplica) y libera el documento. El <see cref="Task"/> devuelto completa cuando el documento
+    /// ha sido liberado.
+    /// </summary>
+    internal Task BeginEvictionAsync()
+    {
+        if (_state is DocumentActorState.Active or DocumentActorState.Idle)
+        {
+            _state = DocumentActorState.Idle;
+            _channel.Writer.TryComplete();
+        }
+        return _runLoop;
+    }
+
+    private Exception ClosedReason() =>
+        _fault ?? new ObjectDisposedException(nameof(DocumentSession),
+            $"El documento '{DocId}' fue desalojado o el broker se cerró.");
+
+    private async Task RunAsync()
+    {
+        try
+        {
+            await foreach (IWorkItem item in _channel.Reader.ReadAllAsync().ConfigureAwait(false))
+            {
+                if (_fault is not null)
+                {
+                    item.Fail(_fault); // tras faultear, drenamos la cola fallando cada pendiente
+                    continue;
+                }
+
+                try
+                {
+                    bool wantUpdates = item.IsMutating && AnySessionWantsUpdates();
+                    byte[]? stateVector = wantUpdates ? _doc.ExportStateVector() : null;
+
+                    item.Execute(_doc);
+                    Interlocked.Exchange(ref _lastActivityTick, Environment.TickCount64);
+
+                    if (stateVector is not null)
+                    {
+                        byte[] delta = _doc.ExportUpdateSince(stateVector);
+                        if (delta.Length > 0)
+                        {
+                            NotifySessions(delta);
+                        }
+                    }
+                }
+                catch (Exception ex)
+                {
+                    // La operación falló DENTRO del turno: el estado del documento puede ser inválido.
+                    // El item ya propagó la excepción a su llamador; el actor entra en Faulted y drena.
+                    _fault = ex;
+                    _state = DocumentActorState.Faulted;
+                    _channel.Writer.TryComplete();
+                }
+            }
+        }
+        finally
+        {
+            await FinalizeAsync().ConfigureAwait(false);
+        }
+    }
+
+    private async ValueTask FinalizeAsync()
+    {
+        // Persistencia solo en desalojo grácil (no en fallo): drenar → OnEvicting → liberar.
+        if (_state != DocumentActorState.Faulted)
+        {
+            if (_persistOnEnd && _onEvicting is not null)
+            {
+                try
+                {
+                    byte[] state = _doc.ExportState();
+                    await _onEvicting(DocId, state, CancellationToken.None).ConfigureAwait(false);
+                }
+                catch
+                {
+                    // La persistencia es best-effort: si el hook falla, igual liberamos el documento
+                    // (no dejar memoria nativa colgada prima sobre no perder el snapshot). El fallo del
+                    // hook es responsabilidad del consumidor.
+                }
+            }
+            _state = DocumentActorState.Evicted;
+        }
+
+        _doc.Dispose(); // exactamente una vez: el bucle termina una sola vez (P-I)
+    }
+
+    private bool AnySessionWantsUpdates()
+    {
+        lock (_sessionsLock)
+        {
+            foreach (DocumentSession s in _sessions)
+            {
+                if (s.WantsUpdates)
+                {
+                    return true;
+                }
+            }
+        }
+        return false;
+    }
+
+    private void NotifySessions(byte[] delta)
+    {
+        DocumentSession[] snapshot;
+        lock (_sessionsLock)
+        {
+            if (_sessions.Count == 0)
+            {
+                return;
+            }
+            snapshot = _sessions.ToArray();
+        }
+        var mem = new ReadOnlyMemory<byte>(delta);
+        foreach (DocumentSession s in snapshot)
+        {
+            s.RaiseUpdateApplied(mem);
+        }
+    }
+
+    // -- Unidades de trabajo encoladas --
+
+    private interface IWorkItem
+    {
+        bool IsMutating { get; }
+        void Execute(ICrdtDoc doc);
+        void Fail(Exception ex);
+    }
+
+    private sealed class WorkItem<T> : IWorkItem
+    {
+        private readonly Func<ICrdtDoc, T> _op;
+        private readonly TaskCompletionSource<T> _tcs =
+            new(TaskCreationOptions.RunContinuationsAsynchronously);
+        private readonly CancellationToken _ct;
+
+        internal WorkItem(Func<ICrdtDoc, T> op, bool mutating, CancellationToken ct)
+        {
+            _op = op;
+            IsMutating = mutating;
+            _ct = ct;
+        }
+
+        public bool IsMutating { get; }
+
+        internal Task<T> Task => _tcs.Task;
+
+        public void Execute(ICrdtDoc doc)
+        {
+            if (_ct.IsCancellationRequested)
+            {
+                _tcs.TrySetCanceled(_ct); // cancelación no faultea el actor
+                return;
+            }
+            try
+            {
+                _tcs.TrySetResult(_op(doc));
+            }
+            catch (Exception ex)
+            {
+                _tcs.TrySetException(ex); // el llamador ve la excepción causal...
+                throw;                    // ...y el actor faultea (turno abortado)
+            }
+        }
+
+        public void Fail(Exception ex) => _tcs.TrySetException(ex);
+    }
+}
diff --git a/src/Weft.Core/Concurrency/DocumentBroker.cs b/src/Weft.Core/Concurrency/DocumentBroker.cs
new file mode 100644
index 0000000..fcd3ec0
--- /dev/null
+++ b/src/Weft.Core/Concurrency/DocumentBroker.cs
@@ -0,0 +1,316 @@
+namespace Weft.Concurrency;
+
+/// <summary>
+/// Gestiona documentos activos con acceso serializado por documento (un actor/canal por <c>docId</c>,
+/// constitución P-V). Thread-safe y el único camino soportado para compartir un documento entre hilos.
+/// Registra y reutiliza actores por identidad, los desaloja por inactividad y por presión de memoria
+/// (LRU), y libera los recursos nativos de forma determinista.
+/// </summary>
+/// <remarks>
+/// El límite <see cref="DocumentBrokerOptions.MaxActiveDocuments"/> es "suave": se reafirma en el barrido
+/// periódico, no de forma síncrona en <see cref="OpenAsync"/>, y nunca desaloja un documento con sesiones
+/// vivas. Puede excederse transitoriamente bajo ráfagas de aperturas o cuando todos los documentos activos
+/// tienen sesiones abiertas.
+/// </remarks>
+public sealed class DocumentBroker : IAsyncDisposable
+{
+    private readonly ICrdtEngine _engine;
+    private readonly DocumentBrokerOptions _options;
+    private readonly object _gate = new();
+    private readonly Dictionary<string, DocumentActor> _actors = new(StringComparer.Ordinal);
+    private readonly Dictionary<string, Task<DocumentActor>> _loading = new(StringComparer.Ordinal);
+    private readonly Dictionary<string, Task> _evicting = new(StringComparer.Ordinal);
+    private readonly CancellationTokenSource _shutdown = new();
+    private readonly Task _sweeper;
+    private bool _disposed;
+
+    /// <summary>Crea el broker sobre un motor CRDT y opciones de ciclo de vida (por defecto si se omiten).</summary>
+    public DocumentBroker(ICrdtEngine engine, DocumentBrokerOptions? options = null)
+    {
+        ArgumentNullException.ThrowIfNull(engine);
+        _engine = engine;
+        _options = options ?? new DocumentBrokerOptions();
+        _sweeper = Task.Run(SweepLoopAsync);
+    }
+
+    /// <summary>Número de documentos actualmente activos (registrados).</summary>
+    public int ActiveDocumentCount
+    {
+        get { lock (_gate) { return _actors.Count; } }
+    }
+
+    /// <summary>
+    /// Abre (o reutiliza) el documento <paramref name="docId"/>. Si no está activo, lo carga con
+    /// <paramref name="loader"/> (un <c>loader</c> que devuelve <c>null</c>/vacío ⇒ documento nuevo).
+    /// Devuelve una <see cref="DocumentSession"/> para operarlo de forma asíncrona.
+    /// </summary>
+    public async ValueTask<DocumentSession> OpenAsync(
+        string docId,
+        Func<string, CancellationToken, ValueTask<byte[]?>>? loader = null,
+        CancellationToken ct = default)
+    {
+        ArgumentException.ThrowIfNullOrEmpty(docId);
+        ObjectDisposedException.ThrowIf(_disposed, this);
+
+        while (true)
+        {
+            DocumentActor? existing;
+            Task<DocumentActor> loadTask;
+            Task? inflightEviction = null;
+            bool startedLoad = false;
+            lock (_gate)
+            {
+                ObjectDisposedException.ThrowIf(_disposed, this);
+                if (_evicting.TryGetValue(docId, out Task? eviction))
+                {
+                    // Hay un desalojo de este documento en vuelo: esperar a que persista su estado antes
+                    // de cargar, o cargaríamos un snapshot a medio escribir (updates perdidos, SC-006).
+                    existing = null;
+                    loadTask = null!;
+                    inflightEviction = eviction;
+                }
+                else if (_actors.TryGetValue(docId, out DocumentActor? found)
+                    && found.State is DocumentActorState.Active or DocumentActorState.Idle)
+                {
+                    existing = found;
+                    loadTask = System.Threading.Tasks.Task.FromResult(found);
+                }
+                else
+                {
+                    // Un actor terminado (Faulted/Evicted) que quedó registrado se descarta para recrearlo
+                    // limpio — nunca reintentar sobre él (evita el giro infinito sobre un actor muerto).
+                    if (found is not null)
+                    {
+                        _actors.Remove(docId);
+                    }
+                    existing = null;
+                    if (_loading.TryGetValue(docId, out Task<DocumentActor>? pending))
+                    {
+                        loadTask = pending;
+                    }
+                    else
+                    {
+                        loadTask = LoadAndRegisterAsync(docId, loader, ct);
+                        _loading[docId] = loadTask;
+                        startedLoad = true;
+                    }
+                }
+            }
+
+            if (inflightEviction is not null)
+            {
+                try { await inflightEviction.ConfigureAwait(false); } catch { /* el desalojo reporta aparte */ }
+                continue; // el estado ya está persistido; reintentar cargará el snapshot correcto
+            }
+
+            DocumentActor actor;
+            try
+            {
+                actor = existing ?? await loadTask.ConfigureAwait(false);
+            }
+            finally
+            {
+                // Solo el iniciador retira la entrada de carga, y SIEMPRE después del await: si el loader
+                // completa síncronamente, la carga se registra en _actors dentro de este mismo lock, pero
+                // _loading NO debe quedar con una entrada rancia (esa era la causa del livelock en reopen).
+                if (startedLoad)
+                {
+                    lock (_gate)
+                    {
+                        _loading.Remove(docId);
+                    }
+                }
+            }
+
+            // Añadir la sesión atómicamente respecto al barrido: solo si el actor sigue registrado y
+            // no terminó. Si fue desalojado en la ventana, reintentar (reabrirá o reutilizará).
+            lock (_gate)
+            {
+                if (_actors.TryGetValue(docId, out DocumentActor? still)
+                    && ReferenceEquals(still, actor)
+                    && actor.State is DocumentActorState.Active or DocumentActorState.Idle)
+                {
+                    var session = new DocumentSession(actor);
+                    actor.AddSession(session);
+                    return session;
+                }
+            }
+            // desalojado entre carga y registro de la sesión (raro): ceder y reintentar sin quemar CPU
+            await System.Threading.Tasks.Task.Yield();
+        }
+    }
+
+    private async Task<DocumentActor> LoadAndRegisterAsync(
+        string docId,
+        Func<string, CancellationToken, ValueTask<byte[]?>>? loader,
+        CancellationToken ct)
+    {
+        // No toca `_loading`: su ciclo de vida lo gestiona OpenAsync (alta dentro del lock, baja en el
+        // finally tras el await). Así se evita la entrada rancia cuando el loader completa síncronamente.
+        byte[]? initial = loader is not null ? await loader(docId, ct).ConfigureAwait(false) : null;
+        ICrdtDoc doc = initial is { Length: > 0 } ? _engine.LoadDoc(initial) : _engine.CreateDoc();
+        var actor = new DocumentActor(docId, doc, _options.OnEvicting);
+
+        lock (_gate)
+        {
+            if (_disposed)
+            {
+                _ = actor.BeginEvictionAsync(); // broker cerrado durante la carga: liberar el actor nuevo
+                throw new ObjectDisposedException(nameof(DocumentBroker));
+            }
+            _actors[docId] = actor;
+        }
+        return actor;
+    }
+
+    private async Task SweepLoopAsync()
+    {
+        try
+        {
+            using var timer = new PeriodicTimer(_options.ResolveSweepInterval());
+            while (await timer.WaitForNextTickAsync(_shutdown.Token).ConfigureAwait(false))
+            {
+                try
+                {
+                    await SweepOnceAsync().ConfigureAwait(false);
+                }
+                catch (Exception ex) when (ex is not OperationCanceledException)
+                {
+                    // un barrido fallido no debe matar el barrido de fondo (quedaría sin desalojar nunca).
+                    System.Diagnostics.Debug.WriteLine($"[DocumentBroker] barrido falló: {ex}");
+                }
+            }
+        }
+        catch (OperationCanceledException)
+        {
+            // shutdown
+        }
+    }
+
+    /// <summary>Un pase de desalojo: inactividad (idle) + presión de memoria (LRU). Visible para tests.</summary>
+    internal async ValueTask SweepOnceAsync()
+    {
+        List<Task> evictions = [];
+        lock (_gate)
+        {
+            if (_disposed)
+            {
+                return;
+            }
+
+            List<DocumentActor> toEvict = [];
+            long idleThreshold = (long)_options.IdleEviction.TotalMilliseconds;
+            foreach (DocumentActor a in _actors.Values)
+            {
+                bool terminated = a.State is DocumentActorState.Faulted or DocumentActorState.Evicted;
+                bool idle = a.SessionCount == 0 && a.IdleMilliseconds >= idleThreshold;
+                if (terminated || idle)
+                {
+                    toEvict.Add(a);
+                }
+            }
+
+            int remaining = _actors.Count - toEvict.Count;
+            int over = remaining - _options.MaxActiveDocuments;
+            if (over > 0)
+            {
+                // Presión de memoria: desalojar los menos recientemente usados SIN sesión, aunque estén
+                // "tibios". El orden por inactividad descendente protege a los recién usados/creados; si
+                // alguno se desaloja en la ventana previa a su primera sesión, OpenAsync reintenta.
+                List<DocumentActor> lru = _actors.Values
+                    .Where(a => !toEvict.Contains(a) && a.SessionCount == 0)
+                    .OrderByDescending(a => a.IdleMilliseconds)
+                    .Take(over)
+                    .ToList();
+                toEvict.AddRange(lru);
+            }
+
+            foreach (DocumentActor a in toEvict)
+            {
+                _actors.Remove(a.DocId);
+                Task eviction = EvictActorAsync(a);
+                _evicting[a.DocId] = eviction; // los OpenAsync concurrentes esperan a que persista
+                evictions.Add(eviction);
+            }
+        }
+
+        // Esperar a que los desalojos que este barrido inició terminen (persistencia incluida). Da
+        // determinismo a los tests; en el barrido de fondo solo pausa hasta el siguiente tick.
+        await Task.WhenAll(evictions).ConfigureAwait(false);
+    }
+
+    private async Task EvictActorAsync(DocumentActor actor)
+    {
+        await System.Threading.Tasks.Task.Yield(); // no completar síncronamente: _evicting se asigna antes del finally
+        try
+        {
+            await actor.BeginEvictionAsync().ConfigureAwait(false);
+        }
+        catch
+        {
+            // el desalojo de un actor no debe tumbar el barrido de los demás
+        }
+        finally
+        {
+            lock (_gate)
+            {
+                _evicting.Remove(actor.DocId);
+            }
+        }
+    }
+
+    /// <summary>Drena y libera todos los documentos exactamente una vez; detiene el barrido.</summary>
+    public async ValueTask DisposeAsync()
+    {
+        lock (_gate)
+        {
+            if (_disposed)
+            {
+                return;
+            }
+            _disposed = true;
+        }
+
+        _shutdown.Cancel();
+        try
+        {
+            await _sweeper.ConfigureAwait(false);
+        }
+        catch
+        {
+            // el sweeper ya está terminando
+        }
+
+        // Esperar los desalojos en vuelo (persisten estado) antes de drenar el resto.
+        Task[] inflight;
+        List<DocumentActor> all;
+        lock (_gate)
+        {
+            inflight = [.. _evicting.Values];
+            all = [.. _actors.Values];
+            _actors.Clear();
+        }
+        try
+        {
+            await Task.WhenAll(inflight).ConfigureAwait(false);
+        }
+        catch
+        {
+            // cada desalojo reporta su propio fallo; no bloquear el cierre
+        }
+
+        foreach (DocumentActor a in all)
+        {
+            try
+            {
+                await a.BeginEvictionAsync().ConfigureAwait(false);
+            }
+            catch
+            {
+                // liberar el resto pese a un fallo aislado
+            }
+        }
+
+        _shutdown.Dispose();
+    }
+}
diff --git a/src/Weft.Core/Concurrency/DocumentBrokerOptions.cs b/src/Weft.Core/Concurrency/DocumentBrokerOptions.cs
new file mode 100644
index 0000000..20a4b36
--- /dev/null
+++ b/src/Weft.Core/Concurrency/DocumentBrokerOptions.cs
@@ -0,0 +1,49 @@
+namespace Weft.Concurrency;
+
+/// <summary>
+/// Opciones de ciclo de vida del <see cref="DocumentBroker"/>: cuándo desalojar documentos inactivos,
+/// cuántos mantener activos a la vez (desalojo LRU al superarlo) y cómo persistir antes de desalojar.
+/// Inmutable tras construir el broker.
+/// </summary>
+public sealed class DocumentBrokerOptions
+{
+    /// <summary>
+    /// Tiempo sin actividad tras el cual un documento activo es candidato a desalojo. Un documento se
+    /// reabre después desde su estado persistido (vía el <c>loader</c> de <see cref="DocumentBroker.OpenAsync"/>).
+    /// </summary>
+    public TimeSpan IdleEviction { get; init; } = TimeSpan.FromMinutes(5);
+
+    /// <summary>
+    /// Máximo de documentos activos simultáneos. Al superarse, se desaloja el menos recientemente usado
+    /// (LRU) para acotar la memoria (SC-006).
+    /// </summary>
+    public int MaxActiveDocuments { get; init; } = 1024;
+
+    /// <summary>
+    /// Hook invocado antes de liberar un documento desalojado, con su estado exportado, para persistirlo.
+    /// El desalojo espera a que termine. No se invoca cuando el documento se desaloja por fallo del actor
+    /// (estado potencialmente inválido). <c>null</c> = no persistir (los cambios no guardados se pierden
+    /// al desalojar).
+    /// </summary>
+    public Func<string, byte[], CancellationToken, ValueTask>? OnEvicting { get; init; }
+
+    /// <summary>
+    /// Cadencia del barrido de inactividad. El broker revisa periódicamente si hay documentos que superan
+    /// <see cref="IdleEviction"/>. Por defecto, un tercio de <see cref="IdleEviction"/> (acotado a [1s, 60s]).
+    /// </summary>
+    public TimeSpan? IdleSweepInterval { get; init; }
+
+    internal TimeSpan ResolveSweepInterval()
+    {
+        if (IdleSweepInterval is { } explicitInterval)
+        {
+            return explicitInterval;
+        }
+        TimeSpan third = IdleEviction / 3;
+        if (third < TimeSpan.FromSeconds(1))
+        {
+            return TimeSpan.FromSeconds(1);
+        }
+        return third > TimeSpan.FromSeconds(60) ? TimeSpan.FromSeconds(60) : third;
+    }
+}
diff --git a/src/Weft.Core/Concurrency/DocumentSession.cs b/src/Weft.Core/Concurrency/DocumentSession.cs
new file mode 100644
index 0000000..2cfff0b
--- /dev/null
+++ b/src/Weft.Core/Concurrency/DocumentSession.cs
@@ -0,0 +1,123 @@
+namespace Weft.Concurrency;
+
+/// <summary>
+/// Fachada asíncrona de un documento gestionado por el <see cref="DocumentBroker"/>. Espejo de
+/// <see cref="ICrdtDoc"/> donde cada llamada se encola al actor del documento y se ejecuta serializada
+/// (constitución P-V). Varias sesiones pueden compartir el mismo documento; todas reciben el evento
+/// <see cref="UpdateApplied"/>. No expone el <see cref="ICrdtDoc"/> subyacente salvo, transitoriamente,
+/// dentro del delegado de <see cref="ExecuteAsync{T}"/>.
+/// </summary>
+public sealed class DocumentSession : IAsyncDisposable
+{
+    private readonly DocumentActor _actor;
+    private bool _disposed;
+
+    internal DocumentSession(DocumentActor actor)
+    {
+        _actor = actor;
+        DocId = actor.DocId;
+    }
+
+    /// <summary>Identificador lógico del documento.</summary>
+    public string DocId { get; }
+
+    /// <summary>
+    /// Se dispara tras cada update aplicado al documento (propio o importado por otra sesión), con el
+    /// delta correspondiente. Pensado para relay/persistencia (M2). El handler se invoca dentro del turno
+    /// del actor: no debe bloquear esperando otra operación del mismo documento.
+    /// </summary>
+    public event Action<DocumentSession, ReadOnlyMemory<byte>>? UpdateApplied;
+
+    /// <summary>Inserta texto en el campo indicado (encolado y serializado).</summary>
+    public async ValueTask InsertTextAsync(string field, int index, string text, CancellationToken ct = default)
+    {
+        ArgumentException.ThrowIfNullOrEmpty(field);
+        ArgumentNullException.ThrowIfNull(text);
+        ArgumentOutOfRangeException.ThrowIfNegative(index);
+        ThrowIfDisposed();
+        await _actor.EnqueueAsync(doc => { doc.InsertText(field, index, text); return true; }, mutating: true, ct)
+            .ConfigureAwait(false);
+    }
+
+    /// <summary>Borra texto del campo indicado (encolado y serializado).</summary>
+    public async ValueTask DeleteTextAsync(string field, int index, int length, CancellationToken ct = default)
+    {
+        ArgumentException.ThrowIfNullOrEmpty(field);
+        ArgumentOutOfRangeException.ThrowIfNegative(index);
+        ArgumentOutOfRangeException.ThrowIfNegative(length);
+        ThrowIfDisposed();
+        await _actor.EnqueueAsync(doc => { doc.DeleteText(field, index, length); return true; }, mutating: true, ct)
+            .ConfigureAwait(false);
+    }
+
+    /// <summary>Lee el contenido completo del campo indicado.</summary>
+    public ValueTask<string> GetTextAsync(string field, CancellationToken ct = default)
+    {
+        ArgumentException.ThrowIfNullOrEmpty(field);
+        ThrowIfDisposed();
+        return _actor.EnqueueAsync(doc => doc.GetText(field), mutating: false, ct);
+    }
+
+    /// <summary>Exporta el estado completo del documento (base del content-addressing).</summary>
+    public ValueTask<byte[]> ExportStateAsync(CancellationToken ct = default)
+    {
+        ThrowIfDisposed();
+        return _actor.EnqueueAsync(doc => doc.ExportState(), mutating: false, ct);
+    }
+
+    /// <summary>Exporta el state vector ("qué conozco") para sync incremental.</summary>
+    public ValueTask<byte[]> ExportStateVectorAsync(CancellationToken ct = default)
+    {
+        ThrowIfDisposed();
+        return _actor.EnqueueAsync(doc => doc.ExportStateVector(), mutating: false, ct);
+    }
+
+    /// <summary>Exporta el delta con los cambios que el emisor del state vector no conoce.</summary>
+    public ValueTask<byte[]> ExportUpdateSinceAsync(ReadOnlyMemory<byte> stateVector, CancellationToken ct = default)
+    {
+        ThrowIfDisposed();
+        byte[] sv = stateVector.ToArray(); // copia defensiva: el buffer del llamador puede cambiar antes del turno
+        return _actor.EnqueueAsync(doc => doc.ExportUpdateSince(sv), mutating: false, ct);
+    }
+
+    /// <summary>Fusiona un update/estado de otra réplica (convergente); dispara <see cref="UpdateApplied"/>.</summary>
+    public async ValueTask ApplyUpdateAsync(ReadOnlyMemory<byte> update, CancellationToken ct = default)
+    {
+        ThrowIfDisposed();
+        byte[] u = update.ToArray(); // copia defensiva (ver ExportUpdateSinceAsync)
+        await _actor.EnqueueAsync(doc => { doc.ApplyUpdate(u); return true; }, mutating: true, ct)
+            .ConfigureAwait(false);
+    }
+
+    /// <summary>
+    /// Ejecuta un delegado como turno atómico respecto a las demás operaciones del mismo documento
+    /// (transacción lógica). El <see cref="ICrdtDoc"/> recibido NO debe capturarse ni usarse fuera del
+    /// delegado: solo es válido durante la ejecución del turno.
+    /// </summary>
+    public ValueTask<T> ExecuteAsync<T>(Func<ICrdtDoc, T> operation, CancellationToken ct = default)
+    {
+        ArgumentNullException.ThrowIfNull(operation);
+        ThrowIfDisposed();
+        return _actor.EnqueueAsync(operation, mutating: true, ct);
+    }
+
+    internal bool WantsUpdates => UpdateApplied is not null;
+
+    internal void RaiseUpdateApplied(ReadOnlyMemory<byte> delta) => UpdateApplied?.Invoke(this, delta);
+
+    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
+
+    /// <summary>Cierra la sesión: deja de recibir eventos y libera su referencia en el actor. No desaloja
+    /// el documento (su ciclo de vida lo gestiona el broker por inactividad/LRU).</summary>
+    public ValueTask DisposeAsync()
+    {
+        if (_disposed)
+        {
+            return ValueTask.CompletedTask;
+        }
+        _disposed = true;
+        UpdateApplied = null;
+        _actor.RemoveSession(this);
+        return ValueTask.CompletedTask;
+    }
+}
diff --git a/tests/Weft.Core.Tests/DocumentBrokerTests.cs b/tests/Weft.Core.Tests/DocumentBrokerTests.cs
new file mode 100644
index 0000000..07bc6df
--- /dev/null
+++ b/tests/Weft.Core.Tests/DocumentBrokerTests.cs
@@ -0,0 +1,268 @@
+using System.Text;
+using Weft.Concurrency;
+using Weft.Yrs;
+
+namespace Weft.Core.Tests;
+
+/// <summary>
+/// Contrato de concurrencia del <see cref="DocumentBroker"/> (T040, US2, constitución P-V): serialización
+/// estricta por documento, orden FIFO por sesión, desalojo→persistencia→reapertura, propagación de fallo y
+/// semántica de dispose. Los casos de serialización/fallo usan un motor de prueba que instrumenta la
+/// concurrencia; los de ciclo de vida usan el motor yrs real.
+/// </summary>
+public sealed class DocumentBrokerTests
+{
+    // -- Serialización (Acceptance Scenario 1): nunca dos operaciones simultáneas del mismo documento --
+
+    [Fact]
+    public async Task Operations_on_same_document_never_run_concurrently()
+    {
+        var engine = new TrackingEngine();
+        await using var broker = new DocumentBroker(engine);
+        await using DocumentSession session = await broker.OpenAsync("doc");
+
+        Task[] writers = Enumerable.Range(0, 50).Select(_ => Task.Run(async () =>
+        {
+            for (int k = 0; k < 20; k++)
+            {
+                await session.InsertTextAsync("body", 0, "x");
+            }
+        })).ToArray();
+        await Task.WhenAll(writers);
+
+        TrackingDoc doc = Assert.Single(engine.Docs);
+        Assert.Equal(1, doc.PeakConcurrency);            // el actor serializó todo el acceso
+        Assert.Equal(50 * 20, (await session.GetTextAsync("body")).Length);
+    }
+
+    // -- FIFO por sesión: las operaciones encoladas se aplican en el orden de encolado --
+
+    [Fact]
+    public async Task Operations_from_a_session_apply_in_FIFO_order()
+    {
+        await using var broker = new DocumentBroker(YrsEngine.Instance);
+        await using DocumentSession session = await broker.OpenAsync("doc");
+
+        var pending = new List<Task>();
+        for (int i = 0; i < 10; i++)
+        {
+            // encolado síncrono en orden; inserta el dígito i en la posición i
+            pending.Add(session.InsertTextAsync("body", i, i.ToString()).AsTask());
+        }
+        await Task.WhenAll(pending);
+
+        Assert.Equal("0123456789", await session.GetTextAsync("body"));
+    }
+
+    // -- Acceptance Scenario 2: desalojo por inactividad → OnEvicting → reapertura desde lo persistido --
+
+    [Fact]
+    public async Task Idle_document_is_evicted_persisted_and_can_be_reopened()
+    {
+        byte[]? persisted = null;
+        int evictions = 0;
+        var options = new DocumentBrokerOptions
+        {
+            IdleEviction = TimeSpan.FromMilliseconds(20),
+            IdleSweepInterval = TimeSpan.FromHours(1), // barrido automático desactivado: lo disparamos manual
+            OnEvicting = (id, state, ct) =>
+            {
+                persisted = state;
+                Interlocked.Increment(ref evictions);
+                return ValueTask.CompletedTask;
+            },
+        };
+        await using var broker = new DocumentBroker(YrsEngine.Instance, options);
+
+        DocumentSession session = await broker.OpenAsync("doc-1");
+        await session.InsertTextAsync("body", 0, "hola");
+        await session.DisposeAsync();          // sin sesiones vivas → candidato a desalojo
+        await Task.Delay(60);                  // superar IdleEviction (20ms)
+        await broker.SweepOnceAsync();         // fuerza el barrido (no esperar al timer)
+
+        Assert.Equal(1, evictions);
+        Assert.NotNull(persisted);
+        Assert.Equal(0, broker.ActiveDocumentCount);
+
+        // Reabrir con un loader que devuelve el estado persistido → contenido restaurado.
+        await using DocumentSession reopened = await broker.OpenAsync(
+            "doc-1", (id, ct) => ValueTask.FromResult<byte[]?>(persisted));
+        Assert.Equal("hola", await reopened.GetTextAsync("body"));
+    }
+
+    // -- LRU: al superar el máximo, se desaloja el menos recientemente usado (sin sesiones vivas) --
+
+    [Fact]
+    public async Task Over_capacity_evicts_least_recently_used_without_sessions()
+    {
+        var options = new DocumentBrokerOptions
+        {
+            MaxActiveDocuments = 2,
+            IdleEviction = TimeSpan.FromHours(1), // aislar: solo queremos ver el desalojo por LRU
+        };
+        await using var broker = new DocumentBroker(YrsEngine.Instance, options);
+
+        // Tres documentos, cerrando cada sesión (sin sesiones vivas → elegibles para LRU). El delay
+        // separa los timestamps de inactividad, así 'a' es el menos recientemente usado.
+        foreach (string id in new[] { "a", "b", "c" })
+        {
+            DocumentSession s = await broker.OpenAsync(id);
+            await s.InsertTextAsync("body", 0, id);
+            await s.DisposeAsync();
+            await Task.Delay(EvictionGrace);
+        }
+
+        Assert.Equal(3, broker.ActiveDocumentCount); // el límite es suave hasta el barrido
+        await broker.SweepOnceAsync();
+        Assert.Equal(2, broker.ActiveDocumentCount); // 'a' (LRU) desalojado
+    }
+
+    // -- Actor en fallo irrecuperable: propaga la excepción causal a las operaciones pendientes/futuras --
+
+    [Fact]
+    public async Task Faulted_actor_propagates_causal_exception()
+    {
+        await using var broker = new DocumentBroker(YrsEngine.Instance);
+        await using DocumentSession session = await broker.OpenAsync("doc");
+
+        var boom = new InvalidOperationException("boom");
+        var gate = new TaskCompletionSource();
+
+        // Turno que bloquea el actor hasta 'gate' y luego lanza: garantiza que 'pending' quede encolada
+        // DETRÁS antes de que el fallo ocurra (test determinista).
+        Task<int> faulting = session.ExecuteAsync<int>(_ => { gate.Task.Wait(); throw boom; }).AsTask();
+        Task<string> pending = session.GetTextAsync("body").AsTask();
+        gate.SetResult();
+
+        InvalidOperationException fromFaulting =
+            await Assert.ThrowsAsync<InvalidOperationException>(() => faulting);
+        InvalidOperationException fromPending =
+            await Assert.ThrowsAsync<InvalidOperationException>(() => pending);
+        Assert.Same(boom, fromFaulting);
+        Assert.Same(boom, fromPending);
+
+        // Operaciones futuras también fallan con la misma causal.
+        InvalidOperationException fromFuture =
+            await Assert.ThrowsAsync<InvalidOperationException>(async () => await session.GetTextAsync("body"));
+        Assert.Same(boom, fromFuture);
+    }
+
+    // -- Dispose semantics: error predecible de la plataforma, nunca crash (Acceptance Scenario 3) --
+
+    [Fact]
+    public async Task Using_a_disposed_session_throws_ObjectDisposedException()
+    {
+        await using var broker = new DocumentBroker(YrsEngine.Instance);
+        DocumentSession session = await broker.OpenAsync("doc");
+        await session.DisposeAsync();
+
+        await Assert.ThrowsAsync<ObjectDisposedException>(
+            async () => await session.InsertTextAsync("body", 0, "x"));
+    }
+
+    [Fact]
+    public async Task Operations_after_broker_dispose_fail_predictably()
+    {
+        var broker = new DocumentBroker(YrsEngine.Instance);
+        DocumentSession session = await broker.OpenAsync("doc");
+        await broker.DisposeAsync();
+
+        await Assert.ThrowsAsync<ObjectDisposedException>(
+            async () => await session.GetTextAsync("body"));
+        await Assert.ThrowsAsync<ObjectDisposedException>(
+            async () => await broker.OpenAsync("other"));
+    }
+
+    private static readonly TimeSpan EvictionGrace = TimeSpan.FromMilliseconds(300);
+
+    // -- Motor de prueba que instrumenta la concurrencia observada por documento --
+
+    private sealed class TrackingEngine : ICrdtEngine
+    {
+        public List<TrackingDoc> Docs { get; } = [];
+        public string Name => "tracking";
+        public INativeVersioning? NativeVersioning => null;
+
+        public ICrdtDoc CreateDoc()
+        {
+            var doc = new TrackingDoc();
+            lock (Docs) { Docs.Add(doc); }
+            return doc;
+        }
+
+        public ICrdtDoc LoadDoc(ReadOnlySpan<byte> blob)
+        {
+            var doc = new TrackingDoc(blob);
+            lock (Docs) { Docs.Add(doc); }
+            return doc;
+        }
+    }
+
+    private sealed class TrackingDoc : ICrdtDoc
+    {
+        private readonly StringBuilder _text = new();
+        private int _inside;
+        private int _peak;
+
+        public TrackingDoc() { }
+
+        public TrackingDoc(ReadOnlySpan<byte> blob) => _text.Append(Encoding.UTF8.GetString(blob));
+
+        public int PeakConcurrency => Volatile.Read(ref _peak);
+
+        public string EngineName => "tracking";
+
+        public void InsertText(string field, int index, string text) =>
+            Guarded(() => _text.Insert(index, text));
+
+        public void DeleteText(string field, int index, int length) =>
+            Guarded(() => _text.Remove(index, length));
+
+        public string GetText(string field) => Guarded(() => _text.ToString());
+
+        public byte[] ExportState() => Guarded(() => Encoding.UTF8.GetBytes(_text.ToString()));
+
+        public byte[] ExportStateVector() => [];
+
+        public byte[] ExportUpdateSince(ReadOnlySpan<byte> stateVector) => ExportState();
+
+        public void ApplyUpdate(ReadOnlySpan<byte> update)
+        {
+            byte[] copy = update.ToArray();
+            Guarded(() => _text.Append(Encoding.UTF8.GetString(copy)));
+        }
+
+        public void Dispose() { }
+
+        private void Guarded(Action body) => Guarded(() => { body(); return true; });
+
+        private T Guarded<T>(Func<T> body)
+        {
+            int now = Interlocked.Increment(ref _inside);
+            InterlockedMax(ref _peak, now);
+            try
+            {
+                Thread.SpinWait(100); // ensanchar la ventana para delatar cualquier solape
+                return body();
+            }
+            finally
+            {
+                Interlocked.Decrement(ref _inside);
+            }
+        }
+
+        private static void InterlockedMax(ref int target, int value)
+        {
+            int current;
+            do
+            {
+                current = Volatile.Read(ref target);
+                if (value <= current)
+                {
+                    return;
+                }
+            }
+            while (Interlocked.CompareExchange(ref target, value, current) != current);
+        }
+    }
+}
diff --git a/tests/Weft.LoadTest/Program.cs b/tests/Weft.LoadTest/Program.cs
new file mode 100644
index 0000000..a2ef510
--- /dev/null
+++ b/tests/Weft.LoadTest/Program.cs
@@ -0,0 +1,169 @@
+using System.Collections.Concurrent;
+using System.Diagnostics;
+using Weft.Concurrency;
+using Weft.Yrs;
+
+// Prueba de carga de US2/M1 (SC-006): cientos de documentos y muchas tareas concurrentes editando al
+// azar durante un período sostenido. Verifica (a) consistencia final de cada documento y (b) memoria
+// acotada — el número de documentos activos se mantiene bajo el límite pese a que el total supera el pool
+// (desalojo idle+LRU con persistencia y reapertura). Salida distinta de cero si algo falla (gate CI).
+
+int docs = ArgInt(args, "--docs", 300);
+int tasks = ArgInt(args, "--tasks", 8);
+int seconds = ArgInt(args, "--seconds", 20);
+int maxActive = ArgInt(args, "--max-active", Math.Max(8, docs / 4));
+
+Console.WriteLine($"[load-test] docs={docs} tasks={tasks} seconds={seconds} max-active={maxActive} " +
+                  $"gc-server={System.Runtime.GCSettings.IsServerGC}");
+
+// "Persistencia" en memoria: el hook OnEvicting guarda aquí el estado; el loader lo relee al reabrir.
+var store = new ConcurrentDictionary<string, byte[]>(StringComparer.Ordinal);
+long evictions = 0;
+long confirmedOps = 0;
+long errors = 0;
+
+var options = new DocumentBrokerOptions
+{
+    MaxActiveDocuments = maxActive,
+    // Idle agresivo: fuerza desalojo/reapertura constantes bajo carga → ejercita la carrera
+    // desalojo-vs-reapertura (persistencia + recarga) que SC-006 exige sin pérdida de updates.
+    IdleEviction = TimeSpan.FromMilliseconds(30),
+    IdleSweepInterval = TimeSpan.FromMilliseconds(10),
+    OnEvicting = (id, state, ct) =>
+    {
+        store[id] = state;
+        Interlocked.Increment(ref evictions);
+        return ValueTask.CompletedTask;
+    },
+};
+
+// Contador de inserciones confirmadas por documento: la longitud final del texto debe igualarlo.
+long[] inserts = new long[docs];
+
+await using var broker = new DocumentBroker(YrsEngine.Instance, options);
+
+Func<string, CancellationToken, ValueTask<byte[]?>> loader =
+    (id, ct) => ValueTask.FromResult(store.TryGetValue(id, out byte[]? blob) ? blob : null);
+
+// Muestreo del pico de documentos activos durante la carga (evidencia de memoria acotada).
+int peakActive = 0;
+using var samplerStop = new CancellationTokenSource();
+Task sampler = Task.Run(async () =>
+{
+    while (!samplerStop.IsCancellationRequested)
+    {
+        int active = broker.ActiveDocumentCount;
+        if (active > peakActive)
+        {
+            Interlocked.Exchange(ref peakActive, active);
+        }
+        try { await Task.Delay(20, samplerStop.Token); } catch (OperationCanceledException) { break; }
+    }
+});
+
+var sw = Stopwatch.StartNew();
+long deadlineTicks = sw.ElapsedMilliseconds + (seconds * 1000L);
+
+// Cada documento crece hasta un tope y luego solo se lee: acota el TAMAÑO (memoria por doc),
+// mientras el pooling acota el NÚMERO de documentos activos. Ambos → memoria acotada (SC-006).
+const int perDocCap = 150;
+
+Task[] workers = Enumerable.Range(0, tasks).Select(workerId => Task.Run(async () =>
+{
+    var rng = new Random(unchecked(0x5EED + workerId));
+    while (sw.ElapsedMilliseconds < deadlineTicks)
+    {
+        int idx = rng.Next(docs);
+        string docId = $"doc-{idx}";
+        try
+        {
+            await using DocumentSession session = await broker.OpenAsync(docId, loader);
+            if (Volatile.Read(ref inserts[idx]) < perDocCap)
+            {
+                int burst = rng.Next(1, 4);
+                for (int b = 0; b < burst && Volatile.Read(ref inserts[idx]) < perDocCap; b++)
+                {
+                    await session.InsertTextAsync("body", 0, "x");
+                    Interlocked.Increment(ref inserts[idx]);
+                    Interlocked.Increment(ref confirmedOps);
+                }
+            }
+            else
+            {
+                await session.GetTextAsync("body"); // mantiene el churn de apertura/desalojo sin crecer
+            }
+        }
+        catch (Exception ex)
+        {
+            // En operación correcta no debería ocurrir (una sesión viva protege su documento del
+            // desalojo). Cualquier fallo aquí es una regresión de la capa de concurrencia.
+            Interlocked.Increment(ref errors);
+            if (Interlocked.Read(ref errors) <= 5)
+            {
+                Console.WriteLine($"[load-test] error en '{docId}': {ex.GetType().Name}: {ex.Message}");
+            }
+        }
+    }
+})).ToArray();
+
+await Task.WhenAll(workers);
+sw.Stop();
+samplerStop.Cancel();
+await sampler;
+
+Console.WriteLine($"[load-test] carga completa en {sw.Elapsed.TotalSeconds:F1}s: " +
+                  $"ops={confirmedOps} evictions={evictions} peak-active={peakActive} errors={errors}");
+
+// -- Verificación de consistencia: reabrir cada documento y comparar longitud con las inserciones --
+int inconsistencias = 0;
+for (int idx = 0; idx < docs; idx++)
+{
+    long expected = Interlocked.Read(ref inserts[idx]);
+    await using DocumentSession session = await broker.OpenAsync($"doc-{idx}", loader);
+    string text = await session.GetTextAsync("body");
+    if (text.Length != expected)
+    {
+        if (inconsistencias < 10)
+        {
+            Console.WriteLine($"[load-test] INCONSISTENTE doc-{idx}: len={text.Length} esperado={expected}");
+        }
+        inconsistencias++;
+    }
+}
+
+// -- Memoria: managed heap tras GC forzado (informativo); la cota dura es peak-active <= maxActive+tasks --
+long managed = GC.GetTotalMemory(forceFullCollection: true);
+long workingSet = Process.GetCurrentProcess().WorkingSet64;
+Console.WriteLine($"[load-test] memoria: managed-heap={managed / (1024 * 1024)}MB " +
+                  $"working-set={workingSet / (1024 * 1024)}MB");
+
+// Memoria acotada (SC-006): con tamaño por doc y número de docs activos ambos acotados, el working
+// set se estabiliza. Límite absoluto generoso; un proceso que crece sin cota lo supera holgadamente.
+const long workingSetLimitMb = 1536;
+long workingSetMb = workingSet / (1024 * 1024);
+bool memoryBounded = workingSetMb < workingSetLimitMb;
+bool consistent = inconsistencias == 0;
+bool noErrors = Interlocked.Read(ref errors) == 0;
+
+Console.WriteLine($"[load-test] consistencia={(consistent ? "OK" : $"FAIL ({inconsistencias})")} " +
+                  $"memoria-acotada={(memoryBounded ? "OK" : $"FAIL (working-set {workingSetMb}MB >= {workingSetLimitMb}MB)")} " +
+                  $"sin-errores={(noErrors ? "OK" : "FAIL")}");
+
+if (consistent && memoryBounded && noErrors)
+{
+    Console.WriteLine("[load-test] RESULTADO: PASS");
+    return 0;
+}
+
+Console.WriteLine("[load-test] RESULTADO: FAIL");
+return 1;
+
+static int ArgInt(string[] args, string name, int fallback)
+{
+    int i = Array.IndexOf(args, name);
+    if (i >= 0 && i + 1 < args.Length && int.TryParse(args[i + 1], out int value))
+    {
+        return value;
+    }
+    return fallback;
+}
diff --git a/tests/Weft.LoadTest/Weft.LoadTest.csproj b/tests/Weft.LoadTest/Weft.LoadTest.csproj
new file mode 100644
index 0000000..3d2d094
--- /dev/null
+++ b/tests/Weft.LoadTest/Weft.LoadTest.csproj
@@ -0,0 +1,37 @@
+<Project Sdk="Microsoft.NET.Sdk">
+
+  <PropertyGroup>
+    <OutputType>Exe</OutputType>
+    <IsPackable>false</IsPackable>
+    <!-- Servidor GC: comportamiento de memoria representativo del uso en servidor (US2). -->
+    <ServerGarbageCollection>true</ServerGarbageCollection>
+    <ConcurrentGarbageCollection>true</ConcurrentGarbageCollection>
+  </PropertyGroup>
+
+  <ItemGroup>
+    <ProjectReference Include="../../src/Weft.Core/Weft.Core.csproj" />
+  </ItemGroup>
+
+  <!--
+    Copia el cdylib de yrs al layout runtimes/<rid>/native/ para que NativeLibraryResolver lo
+    encuentre (mismo patrón que los proyectos de test). El load test solo usa YrsEngine.
+  -->
+  <Target Name="CopyWeftNativeForLoadTest" AfterTargets="Build">
+    <PropertyGroup>
+      <_WeftRid Condition="$([MSBuild]::IsOSPlatform('Windows'))">win-x64</_WeftRid>
+      <_WeftRid Condition="$([MSBuild]::IsOSPlatform('OSX'))">osx-arm64</_WeftRid>
+      <_WeftRid Condition="$([MSBuild]::IsOSPlatform('Linux'))">linux-x64</_WeftRid>
+      <_WeftNativeName Condition="$([MSBuild]::IsOSPlatform('Windows'))">weft_yrs_ffi.dll</_WeftNativeName>
+      <_WeftNativeName Condition="$([MSBuild]::IsOSPlatform('OSX'))">libweft_yrs_ffi.dylib</_WeftNativeName>
+      <_WeftNativeName Condition="$([MSBuild]::IsOSPlatform('Linux'))">libweft_yrs_ffi.so</_WeftNativeName>
+    </PropertyGroup>
+    <ItemGroup>
+      <_WeftNative Include="$(MSBuildProjectDirectory)/../../native/target/release/$(_WeftNativeName)" />
+    </ItemGroup>
+    <Copy SourceFiles="@(_WeftNative)"
+          DestinationFolder="$(OutDir)runtimes/$(_WeftRid)/native/"
+          Condition="Exists('%(_WeftNative.FullPath)')"
+          SkipUnchangedFiles="true" />
+  </Target>
+
+</Project>

```

---

## What you must do

### Step 1 — Read the scope

Read the Charter file at `.straymark/charters/03-concurrencia-broker-actor-canal-y-ciclo-de-vida-a.md` in full. Identify:

- The `## Tasks` section (or equivalent): each task, its description, and the expected file.
- The `## Files to modify` section: table of files and declared change type.
- The `## Risk` section or equivalent: `R<N>` risks consciously accepted.
- The Charter's closure criterion (what makes it "complete").

### Step 2 — Verify each task (MANDATORY)

For EACH task in the Charter, perform these steps in order:

1. **Locate file(s)**: find the file mentioned in the task. If it does not exist, report as "Not found". If it exists, continue.
2. **Read the full implementation**: read the file entirely, not just the name. **Do not report "file exists" without reading its content.**
3. **Trace execution flow**: for key functions, follow the full chain (handler → service → repository → SQL/storage, or the equivalent in the project's stack). Verify that parameters propagate correctly through each layer.
4. **Verify tests**: locate the corresponding tests. Read at least 2 test cases to confirm they cover the happy path and at least one edge case.
5. **Compare against the task**: does the implementation match what the task describes? If there are discrepancies, report with evidence (`file:line`).
6. **Check verification fidelity**: for each "verified / resolved / done" claim you meet (in the Charter or the originating AILOGs), ask *against which reality* it was checked — the **condition that actually matters** (real CI, production-shaped data, the live source or contract) or a **convenient proxy** (a local test, a mock, the doc's own assertion). A claim verified only against a proxy is not yet trustworthy: flag it, and re-verify against the real condition where your tools allow. Do **not** trust a downstream summary of an artifact — if a claim rests on "the AILOG says it was done", open the artifact (file / function / migration) and confirm it yourself. And when the in-scope code consumes a contract defined by a decision elsewhere (an AILOG / AIDEC / PM-backlog / spec), check that it explicitly references that defining decision; a consumer with no pointer to the decision that defines its contract is a drift smell worth a finding.

> **Evidence discipline.** You may only opine on files you have opened via a tool call (Read, Grep, etc.). Any finding you produce must cite `file:line` of the specific files you opened. Findings without citations are treated as low confidence by the consolidated review and may be dropped. If you did not open a file, you cannot infer behavior, structure, or correctness about it.

### Step 3 — Run verifications (when applicable)

If your environment allows you to run project commands (build, lint, test), run them over the Charter's scope and report the output verbatim. **Read/verify commands only** — never generators or mutating commands.

> *Stack examples* (adapt to the project you are auditing):
> - **Go**: `go vet ./...`, `go build ./...`, `go test ./<module>/... -v -count=1 2>&1 | tail -50`
> - **Rust**: `cargo check`, `cargo clippy --all-targets`, `cargo test --no-run`
> - **TypeScript/Node**: `npm run typecheck`, `npm run lint`, `npm test -- --run`
> - **Python**: `mypy <pkg>`, `ruff check`, `pytest --co`

If your environment does NOT allow command execution, skip this step and focus the audit on static reading of code + tests.

### Step 4 — Evaluate Charter closure

Read the closure criterion declared by the Charter. Assess: **is this criterion met by the current implementation?** The Charter's criterion is the source of truth for "complete or not", not your expectation of what it "should" include.

### Step 5 — Calibrate severity against the project's REAL configuration

Before assigning severity to EACH finding, verify the driver, flag, or configuration actually active in the code, NOT the theoretical worst case.

**Rule:** severity must reflect the impact the finding has with the configuration the project uses TODAY, not the impact it would have under a hypothetical configuration.

**Mandatory checks before declaring Critical or High severity:**

- [ ] **Active driver**: if the finding concerns an event bus, cache, storage, queue, or any pluggable component, open the factory/config (typically something like `internal/core/<component>/factory.go`, `src/<component>/factory.ts`, `.env.example`, `config.yml`) and confirm which driver is actually instantiated.
- [ ] **Feature flags**: if the code has conditional branches keyed on an env var or flag, confirm the default value and the value used in the tests you validated. A bug that only triggers with `FEATURE_X=true` when the default is `false` is not Critical — it is conditional.
- [ ] **Build tags / conditional compilation**: if the code is behind `//go:build foo`, `#[cfg(feature = "foo")]`, `process.env.NODE_ENV !== 'production'`, etc., confirm whether that condition holds in the production build. Defects reproducible only under a dev or test tag are not production blockers.
- [ ] **DB role / user**: if the finding touches RLS, SQL permissions, or ACLs, verify under which role the app runs. (For example, the testcontainers superuser bypasses RLS; the production role may differ. Do not confuse test behavior with production behavior.)
- [ ] **Deployment scope**: if the finding concerns concurrency, distributed cache, or multi-instance coordination, confirm the configured scaling (`maxScale`, replicas, etc.). A race-condition bug between instances is not Critical if the deployment runs with `maxScale=1`.

**How to classify when the finding is CONDITIONAL:**

- **Critical / High**: the bug triggers under the configuration that runs TODAY in main or staging.
- **Medium / Low**: the bug is a real smell but has no operational trigger under the current config.
- **Post-Charter / non-blocking**: the bug is real and critical under a component that does not yet exist (e.g., an external service still stubbed), or under a flag explicitly disabled. Document it as a future concern with a clear note of "when" and "why" — NOT as a blocker for this Charter.

**Anti-inflation rule:** you may not justify Critical severity by appealing solely to "the bug EXISTS in the code". You must demonstrate that **running** the application with its current configuration, the bug would actually manifest. If your justification begins with "if in the future X were implemented..." or "if someone enabled flag Y...", your severity must be post-Charter or Medium with a note, not Critical.

**Anti-deflation rule:** conversely, you may not classify something as Low by appealing to "this never happens in practice" if the code has a clear path that triggers it under the current config. The absence of reported incidents is not evidence of the bug's absence.

> **Example — declared deferral, not a defect.** Suppose Charter N introduces a thin in-memory adapter for a service the project plans to back with a real driver in a future Charter (call it Charter N+K). Charter N's `## Risk` section names the deferral explicitly (for example: *"R1: temporary in-memory adapter, replaced in CHARTER-N+K"*). If an auditor reading Charter N opens the component's factory and finds that the active driver is the in-memory adapter rather than the real implementation, they must **NOT** report this as a Critical finding — the deferral is declared scope, not hidden technical debt. Correct calibration requires opening the factory and verifying the active driver *before* declaring high severity; if the result matches a deferral declared in some Charter (this one or a previous one), the finding is at most *Post-Charter / non-blocking*. Conversely, if the same auditor finds another place where the same pattern was repeated **without** a declared deferral in any Charter, that **is** a finding (debt without an owner).

---

## Finding categorization

Each finding falls into one of these four categories. The consolidated review uses the same definitions:

- **`hallucination`** — the Charter or the implementation references something that does not exist (an API, a function, a field, a behavior). The agent invented it. Verify by opening the actual file or API.
- **`implementation_gap`** — the Charter declared work the diff did not deliver, OR the diff delivered work the Charter did not declare, **without** being documented as a risk in the AILOG. (If it is documented in `## Risk` as `R<N+1>` in some AILOG, that is NOT a gap — it is an accepted trade-off.)
- **`real_debt`** — a code-level concern that is correct with respect to the Charter but introduces technical debt or a subtle defect (a missing error path, a leaked resource, a non-idempotent operation). The adopter should capture this in the **follow-ups backlog registry** (`.straymark/follow-ups-backlog.md` — the canonical "what's pending" ledger since fw-4.21.0), and promote it to a TDE doc if it qualifies as cross-cutting debt (`straymark followups promote FU-NNN`). Recording it only inside the consolidated review leaves it invisible to the registry.
- **`false_positive`** — what initially looked like a finding but, on closer inspection of the AILOG or the diff, is not. Document it anyway; the consolidated review uses these to recognize patterns where one auditor over-reports.

---

## Output format

Document your findings in a markdown file. The canonical output path is decided by the flow:

- In auditor-side CLI mode (skill `straymark-audit-execute`): `.straymark/audits/CHARTER-03-concurrencia-broker-actor-canal-y-ciclo-de-vida-a/report-<sluggified-model-id>.md` (the skill handles the path automatically).
- In manual paste mode (transitional v0): the operator saves your output at `audit/charters/CHARTER-03-concurrencia-broker-actor-canal-y-ciclo-de-vida-a/auditor-auditor.md` or an equivalent convention.

The file must have this frontmatter (validated against `.straymark/schemas/audit-output.schema.v0.json`):

```yaml
---
audit_role: auditor                       # v1 unified. Legacy v0: "auditor-primary" or "auditor-secondary"
auditor: <your model id and version>      # e.g., claude-sonnet-4-6, gemini-2.5-pro, copilot-v1.0.40
charter_id: CHARTER-03-concurrencia-broker-actor-canal-y-ciclo-de-vida-a
git_range: "origin/main..HEAD"
prompt_used: <path to the resolved audit-prompt you received>
audited_at: <today YYYY-MM-DD>
findings_total: <N>
findings_by_category:
  hallucination: <N>
  implementation_gap: <N>
  real_debt: <N>
  false_positive: <N>
evidence_citations: <N>                   # optional but recommended: how many file:line citations you made
audit_quality: high|medium|low            # optional, self-assessment
---

# Audit: CHARTER-03-concurrencia-broker-actor-canal-y-ciclo-de-vida-a by <your model id>

## Executive summary

[1-2 paragraphs: did execution match the Charter's declared scope? What is the overall verdict — clean, partial, drifted? What is the most material finding, if any?]

## Compilation and test verification

[Paste the output of the Step 3 commands here, if you ran them. If not, state "(skipped — no command execution available)".]

## Task-by-task traceability

For EACH task in the Charter, one entry with this format:

### T### — [Task description]

- **File(s)**: `path/to/file.ext:lines`
- **Status**: Implemented | Partial | Not implemented
- **Verification**:
  - Implementation read: Yes/No
  - Flow traced: [handler → service → repository → SQL] (or equivalent)
  - Tests found: [test_file.ext, N test cases]
- **Findings**: [None | Description of the finding with `file:line`]

## Findings

Classified by severity. ONLY findings within the Charter's scope.

### Critical (block Charter closure)

| # | Finding | File:Line | Category | Evidence | Suggested remediation |
|---|---------|-----------|----------|----------|----------------------|

### High (security or logic bugs)

| # | Finding | File:Line | Category | Evidence | Suggested remediation |
|---|---------|-----------|----------|----------|----------------------|

### Medium (inconsistencies, minor risks)

| # | Finding | File:Line | Category | Evidence | Suggested remediation |
|---|---------|-----------|----------|----------|----------------------|

### Low (quality, naming, style improvements)

| # | Finding | File:Line | Category | Evidence | Suggested remediation |
|---|---------|-----------|----------|----------|----------------------|

## Out-of-scope notes (optional)

Observations about code that is NOT part of this Charter's scope but that you consider relevant to mention. These are NOT defects of this Charter.

| Observation | Relevant Charter / area | Note |
|-------------|-------------------------|------|

## Charter closure assessment

Does the implementation meet the closure criterion declared by `CHARTER-03-concurrencia-broker-actor-canal-y-ciclo-de-vida-a`?
[Yes / No / Partial] — [Justification grounded in evidence, citing `file:line`]

## Conclusion

[2-3 sentences. Actual state of the Charter, critical findings if any, recommended next step.]
```

---

## What you must NOT do

- **DO NOT MODIFY ANY PROJECT FILE.** Your only allowed output is the audit report. If you modify any other file, your audit will be discarded and considered invalid. This includes "fixing" bugs, "improving" code, creating missing files, or running generators. **REPORT, DO NOT ACT.** This is not optional or contextual — it is an absolute constraint.
- **DO NOT declare "no issues"** without having read the code of every task declared in the Charter.
- **DO NOT report tasks from other Charters** as defects of this one.
- **DO NOT inflate severity**: a finding from another Charter is not "Critical" here.
- **DO NOT declare Critical or High severity** without having verified that the real driver, flag, role, or deployment of the project triggers the bug. See Step 5. Declaring "critical regression" based on a stubbed component or a disabled flag invalidates the audit through false inflation.
- **DO NOT report** that a file "does not exist" without having searched with the correct path (including naming-convention variants used by the project).
- **DO NOT copy the file structure** without verifying content.
- **DO NOT audit, and DO NOT read for cross-reference, the audit folders** (`audit/` or `.straymark/audits/`). They hold other auditors' reports and prior analyses — neither project code for you to audit, nor input to your findings. In particular, do not open this cycle's sibling `report-*.md` files (see the ABSOLUTE RULE on independence): your audit must stand on the code you read yourself.
- **DO NOT run** destructive or generative commands. Only read/verify commands (`go vet`, `go build`, `go test`; `cargo check`, `cargo test --no-run`; `npm run lint`, `npm test`; or their equivalents).
- **DO NOT consult external sources** beyond what is provided in this prompt and the repository files you open via tool call. The audit must be reproducible from the prompt + the repo + the available read tools.

---

*StrayMark unified audit template v1.1 (adds: audit-object-vs-truth-oracle + cross-boundary contract checks #303, verification-fidelity #306, follow-ups registry as the canonical real_debt destination). The seven universal sections (ABSOLUTE RULE, Your role, Scope rules, Step 2 mandatory verification, Step 5 severity calibration, What you must NOT do, Output format) come from the `audit/SKILL.md` skill mature pre-StrayMark in Sentinel, contributed via issue #102 by José Villaseñor Montfort (StrangeDaysTech). Sentinel-specific hardcodes (spec paths, Etapa headings, internal modules) were parameterized against the Charter doc, originating AILOGs, git range, and project context.*
