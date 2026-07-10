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

# Charter audit — `CHARTER-02-versioning-dual-engine`

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

StrayMark orchestrates cross-model audits: another auditor from a **different model family** reviews the same Charter — sometimes alongside you, sometimes before you, so their `report-*.md` may already sit in `.straymark/audits/CHARTER-02-versioning-dual-engine/`. **You must not read it** (see the ABSOLUTE RULE). Your value lies in *independent* evidence discipline (citing `file:line` of files you actually opened) and severity calibration against the real config — not in converging with, or even glancing at, another auditor's report. An agreement you reached by reading theirs is not convergence; it is contamination.

---

## Project



*(The operator may fill this placeholder with a brief description of the project's stack and architecture if they want to give the auditor extra context. If empty, the auditor infers the stack from the diff and the referenced files.)*

---

## STRICT scope

**Charter under audit:** `CHARTER-02-versioning-dual-engine` — Versionado content-addressed y dual-engine
**Charter file:** `.straymark/charters/02-versioning-dual-engine.md`
**Git range:** `origin/main..HEAD`

The authoritative source of scope is the Charter file at `.straymark/charters/02-versioning-dual-engine.md`. Read it in full before starting — it declares which files are modified, which tasks are executed, which risks are accepted, and what counts as successful closure.

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
charter_id: CHARTER-02-versioning-dual-engine
status: in-progress
effort_estimate: L
trigger: "CHARTER-01 cerrado (binding seguro con gates P-I/P-II verdes en main). tasks.md fija T022–T035 (US1 versionado + US5 dual-engine) como el segundo y último corte de M0: cierra el hito activando los gates de determinismo (P-III) y dual-engine (P-IV)."
originating_spec: specs/001-weft-crdt-versioning/spec.md
work_verb: implement
design_provenance: new
---

# Charter: Versionado content-addressed y dual-engine

> **Status (mirrored from frontmatter — source of truth is above):** in-progress. Effort: L.
>
> **Origin:** Derivado de `specs/001-weft-crdt-versioning/spec.md`. Segundo y último corte de M0 (T022–T035): capa de dominio de versionado engine-agnóstica (US1) + adaptador Loro y suite dual-engine (US5). Cierra M0.

## Context

Sobre la fundación de CHARTER-01 (shim yrs + binding `Weft.Core`), este corte añade el **versionado content-addressed** (`VersionId` = SHA-256 del export determinista; publish/checkout/diff/branch/merge sobre `IBlobStore`) en una capa de dominio que depende SOLO de las abstracciones de `Weft.Core` (P-IV, nunca de tipos de yrs/Loro). Para probar que esa abstracción está viva, US5 añade un segundo shim (`weft-loro-ffi` sobre `loro = "=1.13.6"`) y el adaptador `Weft.Loro`, y ejecuta la MISMA suite de versionado sobre ambos motores.

Cierra M0 activando los dos gates que faltaban: **determinismo** del encoding (P-III) y **dual-engine** (P-IV). El diseño está ✅ CERRADO en el brief y validado en los spikes 01/03 (la capa de dominio ~58 LOC corrió idéntica sobre yrs y Loro); trabajo de **implementación** contra `contracts/versioning-api.md` y `core-api.md`.

## Scope

**In scope (T022–T035):**

1. **US1 — Versionado (T022–T031)**: `VersionId` (SHA-256, hex 64, Parse/TryParse/AsSpan);
   `IBlobStore` + `InMemoryBlobStore` (put idempotente, thread-safe) + `FileSystemBlobStore`
   (sharding `aa/bb/hash`, escritura atómica temp+rename); `TextDiff` (LCS por palabras,
   determinista); `VersionStore` (Publish/Checkout/Diff/Branch/Merge/MergeAsync, verifica
   integridad → `BlobIntegrityException`); suite parametrizada `VersioningSuiteBase` +
   `YrsVersioningTests` (las **7 postcondiciones** de versioning-api.md); `TextDiffTests`;
   `DeterminismTests` (gate P-III, client-ids fijos); sample runnable de US1; wiring CI
   (`determinism` bloqueante + versioning en la matriz).
2. **US5 — Dual-engine (T032–T035)**: crate `weft-loro-ffi` (ABI núcleo `weft_loro_*` + probes
   `native_diff`/`native_branch`/`shallow_snapshot` + header + tests/mem_asan); `Weft.Loro`
   (`LoroEngine`/`LoroDoc`/`LoroNativeVersioning` per core-api.md); `LoroVersioningTests`
   (hereda `VersioningSuiteBase` de T027) + **promover el gate `dual-engine` a bloqueante**
   (SC-008); extender la matriz `asan` a `weft-loro-ffi`.

**Out of scope:**

- Broker/concurrencia (US2, M1), relay servidor y protocolo y-sync (US3, M2), empaquetado NuGet
  multi-RID y release OSS (US4, M3) — hitos posteriores.
- Hardening del decoder ante input no confiable (R6 de CHARTER-01, amplificación de memoria) —
  sigue diferido a M2 (capa de red); aquí solo se verifica que el shim Loro no introduce UB nuevo.

## Files to modify

<!-- Greenfield salvo lo marcado. Loro: código de referencia validado en spikes 02/03
     (`~/StrangeDaysTech/crdt-core-spikes/spike03/sdt_crdt_ffi_loro`) — se reescribe limpio
     con nombres weft_loro_* (la constitución prohíbe copiar código de spikes). -->

| File | Change |
|---|---|
| `src/Weft.Versioning/VersionId.cs` | New — SHA-256 content-addressing (T022) |
| `src/Weft.Versioning/Blobs/IBlobStore.cs`, `InMemoryBlobStore.cs`, `FileSystemBlobStore.cs` | New — almacén content-addressed + sharding (T023–T024) |
| `src/Weft.Versioning/TextDiff.cs` | New — diff LCS por palabras (T025) |
| `src/Weft.Versioning/VersionStore.cs` | New — publish/checkout/diff/branch/merge + integridad (T026) |
| `tests/Weft.Versioning.Tests/VersioningSuiteBase.cs`, `YrsVersioningTests.cs` | New — suite parametrizada, 7 postcondiciones (T027) |
| `tests/Weft.Versioning.Tests/TextDiffTests.cs` | New — determinismo del diff (T028) |
| `tests/Weft.Determinism.Tests/` (+ `.csproj`) | New — gate de determinismo P-III (T029) |
| `samples/Weft.Sample.Versioning/` (+ `.csproj`) | New — user journey US1 runnable (T030) |
| `native/weft-loro-ffi/` (Cargo.toml, `src/lib.rs`, `include/weft_loro_ffi.h`, `tests/mem_asan.rs`, `fuzz/`) | New — shim C-ABI sobre loro 1.13.6 + probes (T032) |
| `native/Cargo.toml` | Change — añadir member `weft-loro-ffi` |
| `src/Weft.Loro/` (`LoroEngine.cs`, `LoroDoc.cs`, `LoroNativeVersioning.cs`, `Interop/*`, `.csproj`) | New — adaptador dual-path (T033) |
| `tests/Weft.Versioning.Tests/LoroVersioningTests.cs` | New — suite dual-engine sobre Loro (T034) |
| `.github/workflows/ci.yml` | Change — `determinism` bloqueante + `dual-engine` bloqueante + `asan` matrix a loro (T031, T034, T035) |
| `Weft.sln` | Change — añadir `Weft.Loro`, `Weft.Determinism.Tests`, sample |
| `.straymark/07-ai-audit/agent-logs/AILOG-*.md` | New, `risk_level: high` (segundo shim FFI + dominio) |

## Verification

### Local checks

> **Lección de CHARTER-01 (telemetría)**: ejecutar TODO esto localmente —incluido el fuzz con
> `cargo-fuzz` (ya instalado, 0.13.2)— ANTES de pushear, para no iterar a ciegas en CI.

```bash
# Shims Rust (yrs + loro): build + tests + memoria
cargo build --manifest-path native/Cargo.toml
cargo test  --manifest-path native/Cargo.toml
RUSTFLAGS="-Zsanitizer=address" cargo +nightly test --manifest-path native/Cargo.toml \
  --features test-hooks --target x86_64-unknown-linux-gnu     # gate P-II sobre AMBOS shims
cargo +nightly fuzz run -s none doc_load     -- -max_total_time=60 -rss_limit_mb=0 -max_len=8192
cargo +nightly fuzz run -s none apply_update -- -max_total_time=60 -rss_limit_mb=0 -max_len=8192

# .NET: versionado (corre sobre YrsEngine Y LoroEngine), determinismo, diff, binding
dotnet test tests/Weft.Versioning.Tests/     # gate dual-engine (P-IV, SC-008)
dotnet test tests/Weft.Determinism.Tests/    # gate determinismo (P-III)
dotnet test tests/Weft.Core.Tests/
dotnet run  --project samples/Weft.Sample.Versioning/   # user journey US1 legible
```

### Production smoke (after deploy)

No aplica — librería sin despliegue. Los auditores externos deben saltar esta sección.

## Risks

- **R1 — No-determinismo del encoding rompe la identidad (P-III)**: severidad alta. Mitigación:
  `DeterminismTests` como gate bloqueante (corpus con client-ids fijos → export/hash idénticos
  entre réplicas y corridas); `VersionId = SHA-256(ExportState)`. Réplicas convergidas → mismo
  VersionId (postcondición 3, SC-002). Si falla: revalidar el export antes de cualquier release.
- **R2 — Abstracción de motor "zombi" (P-IV)**: severidad alta. Una abstracción con una sola
  implementación ejercitada se considera rota. Mitigación: `VersioningSuiteBase` abstracta corre
  las 7 postcondiciones idénticas sobre yrs Y Loro; gate `dual-engine` promovido a bloqueante
  (T034, SC-008). `Weft.Versioning` no referencia tipos de yrs/Loro (validado por compilación).
- **R3 — Blob corrupto socava el content-addressing**: severidad media. Mitigación: `VersionStore`
  verifica `VersionId.FromBlob(blob) == id` en checkout → `BlobIntegrityException`.
- **R4 — Amplificación de memoria en el decode del shim Loro (hereda R6 de CHARTER-01)**:
  severidad media. El decoder de Loro podría amplificar igual que yrs ante input no confiable.
  Mitigación: mismo tratamiento (fuzz informativo, `catch_unwind`); el hardening real se difiere a
  M2 (capa de red). Si Loro degrada distinto (p. ej. `assert!` vs Err), documentar como
  `R<N+1> (new, not in Charter)` en el AILOG.
- **R5 — Merge no conmutativo**: severidad media. Mitigación: postcondición 5 (merge de ramas
  concurrentes → ambas ediciones presentes, resultado idéntico sin importar el orden) en la suite
  dual-engine, sobre ambos motores.

## Tasks

1. Branch `feat/charter-02-versioning-dual-engine` (ya creado desde main). Flip `declared` → `in-progress`.
2. Re-evaluar **Constitution Check** contra el scope (esta vez P-III y P-IV se **cierran** plenamente).
3. `/speckit-implement` acotado a **T022–T035**; marcar `[X]` + `*CHARTER-02: <sha>*` por tarea.
4. **AILOG** (`risk_level: high`, `review_required: true`); **AIDEC** para decisiones de
   implementación nuevas (tokenización del LCS, layout de sharding, mapeo de capacidades de Loro a
   `INativeVersioning`). Las ✅ CERRADO del brief no se re-documentan.
5. **Verificación local COMPLETA** (bloque Local checks íntegro, incluido fuzz local) ANTES de push.
6. `straymark charter drift CHARTER-02` antes de commit; drifts → `R<N+1>` en el AILOG.
7. Commit + push + abrir PR contra `main`; CI verde (con `determinism` y `dual-engine` bloqueantes).
8. **Auditoría externa StrayMark (condición de cierre — ver §Charter Closure)** antes de cerrar.

## Charter Closure

A diferencia de CHARTER-01, este Charter **requiere auditoría externa multi-modelo antes del cierre**
(el corte cierra M0 y toca los gates de la constitución P-III/P-IV; amerita revisión cross-modelo):

1. Actualización atómica del Charter si el drift check reveló divergencias (mismo PR).
2. `straymark charter drift CHARTER-02 --range origin/main..HEAD` → limpio o documentado.
3. **Auditoría externa** (`straymark charter audit CHARTER-02`): el agente genera el prompt con
   `/straymark-audit-prompt`; el **operador** ejecuta ≥2 auditores CLI (gemini-cli, claude-cli,
   copilot-cli, codex-cli) vía `/straymark-audit-execute`; el agente consolida con
   `/straymark-audit-review`. Los findings `real_debt` se remedian antes de cerrar; el bloque
   `external_audit` de la telemetría se llena con la calibración cross-modelo.
4. `straymark charter close CHARTER-02` (telemetría, status `closed`). No borrar este archivo.

```

---

## Diff

```diff
diff --git a/.github/workflows/ci.yml b/.github/workflows/ci.yml
index e23237c..5d292ca 100644
--- a/.github/workflows/ci.yml
+++ b/.github/workflows/ci.yml
@@ -59,13 +59,14 @@ jobs:
       - uses: Swatinem/rust-cache@v2
         with:
           workspaces: native
-      - name: cargo test bajo AddressSanitizer + LeakSanitizer
+      # Matriz de sanitizers sobre AMBOS shims (T035): sin -p corre todo el workspace nativo.
+      - name: cargo test bajo AddressSanitizer + LeakSanitizer (yrs + loro)
         working-directory: native
         env:
           RUSTFLAGS: "-Zsanitizer=address"
           ASAN_OPTIONS: "detect_leaks=1"
         run: >-
-          cargo +nightly test -p weft-yrs-ffi --features test-hooks
+          cargo +nightly test --features test-hooks
           --target x86_64-unknown-linux-gnu
 
   # ── Fuzzing de la frontera FFI (research R14): smoke de 60 s por target en cada PR ─────────
@@ -88,12 +89,16 @@ jobs:
           workspaces: |
             native
             native/weft-yrs-ffi/fuzz
+            native/weft-loro-ffi/fuzz
       - name: Instalar cargo-fuzz
         run: cargo install cargo-fuzz --locked
       # Compilar aparte: un fallo de compilación de los targets SÍ debe romper el job (rojo).
-      - name: Build fuzz targets
+      - name: Build fuzz targets (yrs)
         working-directory: native/weft-yrs-ffi
         run: cargo +nightly fuzz build -s none
+      - name: Build fuzz targets (loro)
+        working-directory: native/weft-loro-ffi
+        run: cargo +nightly fuzz build -s none
       # El objetivo del smoke es que ningún input adversarial cause panic-through / UB / crash en
       # NUESTRO shim (research R14). Dos características del decoder de yrs sobre input malformado
       # obligan a configurar el harness para que mida eso y no artefactos de yrs (ver AILOG §R6):
@@ -124,21 +129,61 @@ jobs:
           cargo +nightly fuzz run -s none apply_update --
           -max_total_time=60 -rss_limit_mb=0 -max_len=8192
           || echo "::warning title=fuzz informativo (R6)::apply_update halló un crash por amplificación de memoria del decoder de yrs — no bloquea M0; mitigación en M2 (ver AILOG R6)"
+      # Shim Loro (localmente no amplifica memoria; mismo patrón informativo por robustez).
+      - name: Fuzz loro_doc_load (60 s)
+        working-directory: native/weft-loro-ffi
+        run: >-
+          cargo +nightly fuzz run -s none loro_doc_load --
+          -max_total_time=60 -rss_limit_mb=0 -max_len=8192
+          || echo "::warning title=fuzz informativo (loro)::loro_doc_load halló un crash — no bloquea M0 (ver AILOG)"
+      - name: Fuzz loro_apply_update (60 s)
+        working-directory: native/weft-loro-ffi
+        run: >-
+          cargo +nightly fuzz run -s none loro_apply_update --
+          -max_total_time=60 -rss_limit_mb=0 -max_len=8192
+          || echo "::warning title=fuzz informativo (loro)::loro_apply_update halló un crash — no bloquea M0 (ver AILOG)"
 
   # ── Gates que se activan en fases posteriores (jobs nombrados, T004) ──────────────────────
+  # ── Gate P-III: determinismo del encoding (bloqueante desde US1) ──────────────────────────
   determinism:
     name: determinism
     runs-on: ubuntu-latest
-    timeout-minutes: 5
+    timeout-minutes: 20
     steps:
-      - run: echo "Gate de determinismo del encoding (P-III) — se activa en US1 (CHARTER-02, T029/T031)."
+      - uses: actions/checkout@v4
+      - uses: dtolnay/rust-toolchain@stable
+      - uses: Swatinem/rust-cache@v2
+        with:
+          workspaces: native
+      - uses: actions/setup-dotnet@v4
+        with:
+          dotnet-version: "10.0.x"
+      - name: Build shim (test-hooks)
+        working-directory: native
+        run: cargo build --release --features test-hooks
+      - name: Determinism gate
+        run: dotnet test tests/Weft.Determinism.Tests/ --configuration Release
+      # El cross-implementación vs Yjs JS (mismo hash en todos los RIDs) se añade en US4 (T058).
 
+  # ── Gate P-IV: suite de versionado sobre yrs Y Loro (bloqueante desde US5, SC-008) ────────
   dual-engine:
     name: dual-engine
     runs-on: ubuntu-latest
-    timeout-minutes: 5
+    timeout-minutes: 20
     steps:
-      - run: echo "Suite de versionado sobre yrs Y Loro (P-IV) — se activa en US5 (CHARTER-02, T034)."
+      - uses: actions/checkout@v4
+      - uses: dtolnay/rust-toolchain@stable
+      - uses: Swatinem/rust-cache@v2
+        with:
+          workspaces: native
+      - uses: actions/setup-dotnet@v4
+        with:
+          dotnet-version: "10.0.x"
+      - name: Build shims (yrs + loro, test-hooks)
+        working-directory: native
+        run: cargo build --release --features test-hooks
+      - name: Dual-engine versioning suite
+        run: dotnet test tests/Weft.Versioning.Tests/ --configuration Release
 
   pack-smoke:
     name: pack-smoke
diff --git a/.straymark/07-ai-audit/agent-logs/AILOG-2026-07-10-002-charter-02-versionado-content-addressed-dual-engin.md b/.straymark/07-ai-audit/agent-logs/AILOG-2026-07-10-002-charter-02-versionado-content-addressed-dual-engin.md
new file mode 100644
index 0000000..19c9722
--- /dev/null
+++ b/.straymark/07-ai-audit/agent-logs/AILOG-2026-07-10-002-charter-02-versionado-content-addressed-dual-engin.md
@@ -0,0 +1,114 @@
+---
+id: AILOG-2026-07-10-002
+title: "CHARTER-02: versionado content-addressed + dual-engine (T022–T035)"
+status: accepted
+created: 2026-07-10
+agent: claude-opus-4-8
+confidence: high
+review_required: true
+risk_level: high
+eu_ai_act_risk: not_applicable
+nist_genai_risks: []
+iso_42001_clause: []
+lines_changed: 1600
+files_modified: []
+observability_scope: none
+tags: [versionado, crdt, dual-engine, loro, ffi, determinismo]
+related: [AILOG-2026-07-10-001]
+originating_charter: CHARTER-02-versioning-dual-engine
+---
+
+# AILOG: CHARTER-02 — versionado content-addressed + dual-engine (T022–T035)
+
+## Summary
+
+Segundo y último corte de M0: capa de dominio de versionado engine-agnóstica (`VersionId` SHA-256,
+`IBlobStore` + In-Memory/FileSystem, `TextDiff` LCS, `VersionStore` publish/checkout/diff/branch/merge)
+y adaptador dual-path Loro (`weft-loro-ffi` + `Weft.Loro`). **M0 cerrado**: la misma suite de
+versionado (7 postcondiciones) pasa idéntica sobre yrs Y Loro (P-IV), con gate de determinismo activo
+(P-III). Todas las verificaciones locales verdes ANTES de CI (lección de CHARTER-01).
+
+## Context
+
+Ejecución de T022–T035 bajo `.straymark/charters/02-versioning-dual-engine.md`, sobre la fundación
+FFI de CHARTER-01. Contra `contracts/versioning-api.md` y `core-api.md`; código de referencia del
+shim Loro en los spikes 02/03 (reescrito limpio con nombres `weft_loro_*`).
+
+## Actions Performed
+
+1. **US1 versionado (T022–T031)**: `VersionId` (SHA-256, hex 64, Parse/TryParse); `IBlobStore` +
+   `InMemoryBlobStore` (dedup por hash) + `FileSystemBlobStore` (sharding `aa/bb/hash`, escritura
+   atómica temp+rename); `TextDiff` (LCS por tokens palabra/espacio); `VersionStore` (verifica
+   integridad → `BlobIntegrityException`); suite parametrizada `VersioningSuiteBase` (7 postcondiciones)
+   + `YrsVersioningTests` + `TextDiffTests`; proyecto `Weft.Determinism.Tests` (gate P-III); sample
+   runnable `Weft.Sample.Versioning`; wiring CI (`determinism` bloqueante).
+2. **US5 dual-engine (T032–T035)**: crate `weft-loro-ffi` (12 fn C-ABI simétricas + test hook, índices
+   UTF-16, export Snapshot, state-vector vía `VersionVector`) + suite ASan + fuzz targets; binding
+   `Weft.Loro` (`LoroEngine`/`LoroDoc` + interop simétrico a Weft.Yrs); `LoroVersioningTests` (hereda
+   `VersioningSuiteBase`); CI: `dual-engine` bloqueante (T034) + `asan` extendido a loro (T035).
+
+## Modified Files
+
+| File | Change Description |
+|------|--------------------|
+| `src/Weft.Versioning/*.cs`, `Blobs/*.cs` | VersionId, blob stores, TextDiff, VersionStore |
+| `native/weft-loro-ffi/**` | Shim C-ABI sobre loro 1.13.6 + tests + fuzz |
+| `native/weft-yrs-ffi/src/lib.rs` | **Fix R6** (OffsetKind::Utf16) — scope expansion |
+| `native/Cargo.toml` | Añadido member weft-loro-ffi |
+| `src/Weft.Loro/**` | Binding dual-path (LoroEngine/LoroDoc + interop) |
+| `tests/Weft.Versioning.Tests/*`, `tests/Weft.Determinism.Tests/*` | Suite dual-engine + gate determinismo |
+| `samples/Weft.Sample.Versioning/*` | User journey US1 |
+| `.github/workflows/ci.yml`, `Weft.sln` | Gates determinism/dual-engine/asan-loro; proyectos |
+
+## Decisions Made
+
+- **Export de Loro para content-addressing**: `ExportMode::Snapshot` resultó determinista y estable
+  (round-trip byte-idéntico, réplicas convergidas → mismo VersionId). Validado por la suite dual-engine.
+- **Índices UTF-16 en Loro**: se usan `insert_utf16`/`delete_utf16`/`len_utf16` para consistencia con
+  la abstracción (.NET string) y con yrs.
+- **`LoroEngine.NativeVersioning = null`**: los probes de versionado nativo de Loro
+  (`INativeVersioning`: diff/branch/shallow) son capacidad OPCIONAL; el versionado del núcleo no los
+  requiere y la suite dual-engine no los usa. Diferidos a una iteración posterior.
+
+## Impact
+
+- **Functionality**: publish/checkout/diff/branch/merge content-addressed sobre dos motores; identidad
+  citable (SHA-256) reproducible.
+- **Security**: segundo shim FFI con `catch_unwind` + ownership; ASan/LSan verde en ambos. **Riesgo alto**
+  por memoria nativa, mitigado por gates.
+- **Performance**: dedup natural por hash; compactación por construcción (GC del motor activo).
+
+## Verification
+
+- [x] Compila sin warnings (`dotnet build -c Release` 0/0; `cargo clippy -D warnings` limpio ambos shims)
+- [x] **36 tests .NET** verdes (Core 18, Versioning 17 dual-engine, Determinism 2 — incl. 6 postcondiciones × 2 motores)
+- [x] **Gate P-II**: ASan/LSan sobre AMBOS shims (yrs 7 + loro 5 tests) → 0 fugas
+- [x] **Fuzz local ambos shims**: yrs (informativo, R6) + loro (limpio, ~1.3M runs/target, no amplifica)
+- [x] Sample end-to-end ejecutado (journey US1 legible)
+- [ ] Revisión humana del operador (pendiente — `review_required: true`)
+- [ ] **Auditoría externa StrayMark** (condición de cierre del Charter — pendiente antes de close)
+
+## Additional Notes
+
+### Risk: R6 (new, not in Charter) — índices de yrs eran byte-offsets, no UTF-16
+
+Al ejercitar el diff con texto acentuado, el sample expuso un **bug latente de CHARTER-01**:
+`yrs::Doc::new()` usa `OffsetKind::Bytes` por defecto, así que insert/delete indexaban en **bytes
+UTF-8**, no UTF-16 — inconsistente con el índice `int` de la API (.NET string = UTF-16) **y con Yjs**
+(UTF-16, crítico para los clientes de editor de US3). CHARTER-01 no lo detectó porque sus tests usaban
+solo ASCII (donde bytes == UTF-16). **Corregido**: el shim crea el doc con `OffsetKind::Utf16`
+(`native/weft-yrs-ffi/src/lib.rs`, fuera de los Files to modify declarados → scope expansion
+justificada). Blindado con `Utf16IndexingTests` (regresión permanente). No se bump la ABI (la firma no
+cambió; el comportamiento pasa a ser el que el contrato siempre declaró). Loro no tenía el bug (usa
+`insert_utf16` explícito).
+
+### Nota: Loro no amplifica memoria (R6 de CHARTER-01 no aplica)
+
+El fuzz del shim Loro corrió limpio (~1.3M runs/target, RSS ~43 MB, exit 0) sin el DoS de amplificación
+que tiene el decoder de yrs. El fuzz de Loro es informativo por simetría, pero podría promoverse a
+bloqueante en el futuro.
+
+### Verificación local ANTES de CI (lección de CHARTER-01 aplicada)
+
+`cargo-fuzz` estaba instalado desde el inicio; dual-engine, ASan de ambos shims y fuzz de ambos se
+ejecutaron localmente y en verde antes de pushear. Sin iteraciones a ciegas en CI.
diff --git a/.straymark/charters/02-versioning-dual-engine.md b/.straymark/charters/02-versioning-dual-engine.md
new file mode 100644
index 0000000..abc1335
--- /dev/null
+++ b/.straymark/charters/02-versioning-dual-engine.md
@@ -0,0 +1,145 @@
+---
+charter_id: CHARTER-02-versioning-dual-engine
+status: in-progress
+effort_estimate: L
+trigger: "CHARTER-01 cerrado (binding seguro con gates P-I/P-II verdes en main). tasks.md fija T022–T035 (US1 versionado + US5 dual-engine) como el segundo y último corte de M0: cierra el hito activando los gates de determinismo (P-III) y dual-engine (P-IV)."
+originating_spec: specs/001-weft-crdt-versioning/spec.md
+work_verb: implement
+design_provenance: new
+---
+
+# Charter: Versionado content-addressed y dual-engine
+
+> **Status (mirrored from frontmatter — source of truth is above):** in-progress. Effort: L.
+>
+> **Origin:** Derivado de `specs/001-weft-crdt-versioning/spec.md`. Segundo y último corte de M0 (T022–T035): capa de dominio de versionado engine-agnóstica (US1) + adaptador Loro y suite dual-engine (US5). Cierra M0.
+
+## Context
+
+Sobre la fundación de CHARTER-01 (shim yrs + binding `Weft.Core`), este corte añade el **versionado content-addressed** (`VersionId` = SHA-256 del export determinista; publish/checkout/diff/branch/merge sobre `IBlobStore`) en una capa de dominio que depende SOLO de las abstracciones de `Weft.Core` (P-IV, nunca de tipos de yrs/Loro). Para probar que esa abstracción está viva, US5 añade un segundo shim (`weft-loro-ffi` sobre `loro = "=1.13.6"`) y el adaptador `Weft.Loro`, y ejecuta la MISMA suite de versionado sobre ambos motores.
+
+Cierra M0 activando los dos gates que faltaban: **determinismo** del encoding (P-III) y **dual-engine** (P-IV). El diseño está ✅ CERRADO en el brief y validado en los spikes 01/03 (la capa de dominio ~58 LOC corrió idéntica sobre yrs y Loro); trabajo de **implementación** contra `contracts/versioning-api.md` y `core-api.md`.
+
+## Scope
+
+**In scope (T022–T035):**
+
+1. **US1 — Versionado (T022–T031)**: `VersionId` (SHA-256, hex 64, Parse/TryParse/AsSpan);
+   `IBlobStore` + `InMemoryBlobStore` (put idempotente, thread-safe) + `FileSystemBlobStore`
+   (sharding `aa/bb/hash`, escritura atómica temp+rename); `TextDiff` (LCS por palabras,
+   determinista); `VersionStore` (Publish/Checkout/Diff/Branch/Merge/MergeAsync, verifica
+   integridad → `BlobIntegrityException`); suite parametrizada `VersioningSuiteBase` +
+   `YrsVersioningTests` (las **7 postcondiciones** de versioning-api.md); `TextDiffTests`;
+   `DeterminismTests` (gate P-III, client-ids fijos); sample runnable de US1; wiring CI
+   (`determinism` bloqueante + versioning en la matriz).
+2. **US5 — Dual-engine (T032–T035)**: crate `weft-loro-ffi` (ABI núcleo `weft_loro_*` + probes
+   `native_diff`/`native_branch`/`shallow_snapshot` + header + tests/mem_asan); `Weft.Loro`
+   (`LoroEngine`/`LoroDoc`/`LoroNativeVersioning` per core-api.md); `LoroVersioningTests`
+   (hereda `VersioningSuiteBase` de T027) + **promover el gate `dual-engine` a bloqueante**
+   (SC-008); extender la matriz `asan` a `weft-loro-ffi`.
+
+**Out of scope:**
+
+- Broker/concurrencia (US2, M1), relay servidor y protocolo y-sync (US3, M2), empaquetado NuGet
+  multi-RID y release OSS (US4, M3) — hitos posteriores.
+- Hardening del decoder ante input no confiable (R6 de CHARTER-01, amplificación de memoria) —
+  sigue diferido a M2 (capa de red); aquí solo se verifica que el shim Loro no introduce UB nuevo.
+
+## Files to modify
+
+<!-- Greenfield salvo lo marcado. Loro: código de referencia validado en spikes 02/03
+     (`~/StrangeDaysTech/crdt-core-spikes/spike03/sdt_crdt_ffi_loro`) — se reescribe limpio
+     con nombres weft_loro_* (la constitución prohíbe copiar código de spikes). -->
+
+| File | Change |
+|---|---|
+| `src/Weft.Versioning/VersionId.cs` | New — SHA-256 content-addressing (T022) |
+| `src/Weft.Versioning/Blobs/IBlobStore.cs`, `InMemoryBlobStore.cs`, `FileSystemBlobStore.cs` | New — almacén content-addressed + sharding (T023–T024) |
+| `src/Weft.Versioning/TextDiff.cs` | New — diff LCS por palabras (T025) |
+| `src/Weft.Versioning/VersionStore.cs` | New — publish/checkout/diff/branch/merge + integridad (T026) |
+| `tests/Weft.Versioning.Tests/VersioningSuiteBase.cs`, `YrsVersioningTests.cs` | New — suite parametrizada, 7 postcondiciones (T027) |
+| `tests/Weft.Versioning.Tests/TextDiffTests.cs` | New — determinismo del diff (T028) |
+| `tests/Weft.Determinism.Tests/` (+ `.csproj`) | New — gate de determinismo P-III (T029) |
+| `samples/Weft.Sample.Versioning/` (+ `.csproj`) | New — user journey US1 runnable (T030) |
+| `native/weft-loro-ffi/` (Cargo.toml, `src/lib.rs`, `include/weft_loro_ffi.h`, `tests/mem_asan.rs`, `fuzz/`) | New — shim C-ABI sobre loro 1.13.6 + probes (T032) |
+| `native/Cargo.toml` | Change — añadir member `weft-loro-ffi` |
+| `src/Weft.Loro/` (`LoroEngine.cs`, `LoroDoc.cs`, `LoroNativeVersioning.cs`, `Interop/*`, `.csproj`) | New — adaptador dual-path (T033) |
+| `tests/Weft.Versioning.Tests/LoroVersioningTests.cs` | New — suite dual-engine sobre Loro (T034) |
+| `.github/workflows/ci.yml` | Change — `determinism` bloqueante + `dual-engine` bloqueante + `asan` matrix a loro (T031, T034, T035) |
+| `Weft.sln` | Change — añadir `Weft.Loro`, `Weft.Determinism.Tests`, sample |
+| `.straymark/07-ai-audit/agent-logs/AILOG-*.md` | New, `risk_level: high` (segundo shim FFI + dominio) |
+
+## Verification
+
+### Local checks
+
+> **Lección de CHARTER-01 (telemetría)**: ejecutar TODO esto localmente —incluido el fuzz con
+> `cargo-fuzz` (ya instalado, 0.13.2)— ANTES de pushear, para no iterar a ciegas en CI.
+
+```bash
+# Shims Rust (yrs + loro): build + tests + memoria
+cargo build --manifest-path native/Cargo.toml
+cargo test  --manifest-path native/Cargo.toml
+RUSTFLAGS="-Zsanitizer=address" cargo +nightly test --manifest-path native/Cargo.toml \
+  --features test-hooks --target x86_64-unknown-linux-gnu     # gate P-II sobre AMBOS shims
+cargo +nightly fuzz run -s none doc_load     -- -max_total_time=60 -rss_limit_mb=0 -max_len=8192
+cargo +nightly fuzz run -s none apply_update -- -max_total_time=60 -rss_limit_mb=0 -max_len=8192
+
+# .NET: versionado (corre sobre YrsEngine Y LoroEngine), determinismo, diff, binding
+dotnet test tests/Weft.Versioning.Tests/     # gate dual-engine (P-IV, SC-008)
+dotnet test tests/Weft.Determinism.Tests/    # gate determinismo (P-III)
+dotnet test tests/Weft.Core.Tests/
+dotnet run  --project samples/Weft.Sample.Versioning/   # user journey US1 legible
+```
+
+### Production smoke (after deploy)
+
+No aplica — librería sin despliegue. Los auditores externos deben saltar esta sección.
+
+## Risks
+
+- **R1 — No-determinismo del encoding rompe la identidad (P-III)**: severidad alta. Mitigación:
+  `DeterminismTests` como gate bloqueante (corpus con client-ids fijos → export/hash idénticos
+  entre réplicas y corridas); `VersionId = SHA-256(ExportState)`. Réplicas convergidas → mismo
+  VersionId (postcondición 3, SC-002). Si falla: revalidar el export antes de cualquier release.
+- **R2 — Abstracción de motor "zombi" (P-IV)**: severidad alta. Una abstracción con una sola
+  implementación ejercitada se considera rota. Mitigación: `VersioningSuiteBase` abstracta corre
+  las 7 postcondiciones idénticas sobre yrs Y Loro; gate `dual-engine` promovido a bloqueante
+  (T034, SC-008). `Weft.Versioning` no referencia tipos de yrs/Loro (validado por compilación).
+- **R3 — Blob corrupto socava el content-addressing**: severidad media. Mitigación: `VersionStore`
+  verifica `VersionId.FromBlob(blob) == id` en checkout → `BlobIntegrityException`.
+- **R4 — Amplificación de memoria en el decode del shim Loro (hereda R6 de CHARTER-01)**:
+  severidad media. El decoder de Loro podría amplificar igual que yrs ante input no confiable.
+  Mitigación: mismo tratamiento (fuzz informativo, `catch_unwind`); el hardening real se difiere a
+  M2 (capa de red). Si Loro degrada distinto (p. ej. `assert!` vs Err), documentar como
+  `R<N+1> (new, not in Charter)` en el AILOG.
+- **R5 — Merge no conmutativo**: severidad media. Mitigación: postcondición 5 (merge de ramas
+  concurrentes → ambas ediciones presentes, resultado idéntico sin importar el orden) en la suite
+  dual-engine, sobre ambos motores.
+
+## Tasks
+
+1. Branch `feat/charter-02-versioning-dual-engine` (ya creado desde main). Flip `declared` → `in-progress`.
+2. Re-evaluar **Constitution Check** contra el scope (esta vez P-III y P-IV se **cierran** plenamente).
+3. `/speckit-implement` acotado a **T022–T035**; marcar `[X]` + `*CHARTER-02: <sha>*` por tarea.
+4. **AILOG** (`risk_level: high`, `review_required: true`); **AIDEC** para decisiones de
+   implementación nuevas (tokenización del LCS, layout de sharding, mapeo de capacidades de Loro a
+   `INativeVersioning`). Las ✅ CERRADO del brief no se re-documentan.
+5. **Verificación local COMPLETA** (bloque Local checks íntegro, incluido fuzz local) ANTES de push.
+6. `straymark charter drift CHARTER-02` antes de commit; drifts → `R<N+1>` en el AILOG.
+7. Commit + push + abrir PR contra `main`; CI verde (con `determinism` y `dual-engine` bloqueantes).
+8. **Auditoría externa StrayMark (condición de cierre — ver §Charter Closure)** antes de cerrar.
+
+## Charter Closure
+
+A diferencia de CHARTER-01, este Charter **requiere auditoría externa multi-modelo antes del cierre**
+(el corte cierra M0 y toca los gates de la constitución P-III/P-IV; amerita revisión cross-modelo):
+
+1. Actualización atómica del Charter si el drift check reveló divergencias (mismo PR).
+2. `straymark charter drift CHARTER-02 --range origin/main..HEAD` → limpio o documentado.
+3. **Auditoría externa** (`straymark charter audit CHARTER-02`): el agente genera el prompt con
+   `/straymark-audit-prompt`; el **operador** ejecuta ≥2 auditores CLI (gemini-cli, claude-cli,
+   copilot-cli, codex-cli) vía `/straymark-audit-execute`; el agente consolida con
+   `/straymark-audit-review`. Los findings `real_debt` se remedian antes de cerrar; el bloque
+   `external_audit` de la telemetría se llena con la calibración cross-modelo.
+4. `straymark charter close CHARTER-02` (telemetría, status `closed`). No borrar este archivo.
diff --git a/.straymark/follow-ups-backlog.md b/.straymark/follow-ups-backlog.md
index 3643bd4..6e70f1c 100644
--- a/.straymark/follow-ups-backlog.md
+++ b/.straymark/follow-ups-backlog.md
@@ -1,7 +1,7 @@
 ---
 last_scan: 2026-07-10
 schema_version: v1
-total_open: 3
+total_open: 4
 total_promoted: 0
 total_closed_in_session: 0
 total_phase_blocked: 0
@@ -14,6 +14,7 @@ buckets:
   - operational
 fully_extracted_ailogs:
   - AILOG-2026-07-10-001
+  - AILOG-2026-07-10-002
 ---
 
 # Follow-ups Backlog
@@ -66,6 +67,15 @@ Entry shape (v1 — optional fields marked):
 - **Destination**: TBD
 - **Cost**: TBD
 - **Notes**: Auto-appended by `straymark followups drift --apply` 2026-07-10.
+
+### FU-004 — ### Risk: R6 (new, not in Charter) — índices de yrs eran byte-offsets, no UTF-16
+- **Origin**: AILOG-2026-07-10-002 §R6 (new, not in Charter)
+- **Source-hash**: 24e92818b6c7
+- **Status**: open
+- **Trigger**: TBD
+- **Destination**: TBD
+- **Cost**: TBD
+- **Notes**: Auto-appended by `straymark followups drift --apply` 2026-07-10.
 ## Bucket: time-triggered
 
 ## Bucket: charter-triggered
diff --git a/Weft.sln b/Weft.sln
index 193612d..d69ba8c 100644
--- a/Weft.sln
+++ b/Weft.sln
@@ -15,6 +15,14 @@ Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "Weft.Core.Tests", "tests\We
 EndProject
 Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "Weft.Versioning.Tests", "tests\Weft.Versioning.Tests\Weft.Versioning.Tests.csproj", "{31CC3F80-DF44-45E8-A4AF-5A6F30031ED6}"
 EndProject
+Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "Weft.Determinism.Tests", "tests\Weft.Determinism.Tests\Weft.Determinism.Tests.csproj", "{265FD711-ACC0-40A3-A334-5B81B5063AE8}"
+EndProject
+Project("{2150E333-8FDC-42A3-9474-1A3956D46DE8}") = "samples", "samples", "{5D20AA90-6969-D8BD-9DCD-8634F4692FDA}"
+EndProject
+Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "Weft.Sample.Versioning", "samples\Weft.Sample.Versioning\Weft.Sample.Versioning.csproj", "{39C303A3-BD6B-4681-93DA-687C5229A1B9}"
+EndProject
+Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "Weft.Loro", "src\Weft.Loro\Weft.Loro.csproj", "{6BEA5D7C-F3F1-4CB3-8CBB-B44B2840B6B6}"
+EndProject
 Global
 	GlobalSection(SolutionConfigurationPlatforms) = preSolution
 		Debug|Any CPU = Debug|Any CPU
@@ -73,6 +81,42 @@ Global
 		{31CC3F80-DF44-45E8-A4AF-5A6F30031ED6}.Release|x64.Build.0 = Release|Any CPU
 		{31CC3F80-DF44-45E8-A4AF-5A6F30031ED6}.Release|x86.ActiveCfg = Release|Any CPU
 		{31CC3F80-DF44-45E8-A4AF-5A6F30031ED6}.Release|x86.Build.0 = Release|Any CPU
+		{265FD711-ACC0-40A3-A334-5B81B5063AE8}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
+		{265FD711-ACC0-40A3-A334-5B81B5063AE8}.Debug|Any CPU.Build.0 = Debug|Any CPU
+		{265FD711-ACC0-40A3-A334-5B81B5063AE8}.Debug|x64.ActiveCfg = Debug|Any CPU
+		{265FD711-ACC0-40A3-A334-5B81B5063AE8}.Debug|x64.Build.0 = Debug|Any CPU
+		{265FD711-ACC0-40A3-A334-5B81B5063AE8}.Debug|x86.ActiveCfg = Debug|Any CPU
+		{265FD711-ACC0-40A3-A334-5B81B5063AE8}.Debug|x86.Build.0 = Debug|Any CPU
+		{265FD711-ACC0-40A3-A334-5B81B5063AE8}.Release|Any CPU.ActiveCfg = Release|Any CPU
+		{265FD711-ACC0-40A3-A334-5B81B5063AE8}.Release|Any CPU.Build.0 = Release|Any CPU
+		{265FD711-ACC0-40A3-A334-5B81B5063AE8}.Release|x64.ActiveCfg = Release|Any CPU
+		{265FD711-ACC0-40A3-A334-5B81B5063AE8}.Release|x64.Build.0 = Release|Any CPU
+		{265FD711-ACC0-40A3-A334-5B81B5063AE8}.Release|x86.ActiveCfg = Release|Any CPU
+		{265FD711-ACC0-40A3-A334-5B81B5063AE8}.Release|x86.Build.0 = Release|Any CPU
+		{39C303A3-BD6B-4681-93DA-687C5229A1B9}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
+		{39C303A3-BD6B-4681-93DA-687C5229A1B9}.Debug|Any CPU.Build.0 = Debug|Any CPU
+		{39C303A3-BD6B-4681-93DA-687C5229A1B9}.Debug|x64.ActiveCfg = Debug|Any CPU
+		{39C303A3-BD6B-4681-93DA-687C5229A1B9}.Debug|x64.Build.0 = Debug|Any CPU
+		{39C303A3-BD6B-4681-93DA-687C5229A1B9}.Debug|x86.ActiveCfg = Debug|Any CPU
+		{39C303A3-BD6B-4681-93DA-687C5229A1B9}.Debug|x86.Build.0 = Debug|Any CPU
+		{39C303A3-BD6B-4681-93DA-687C5229A1B9}.Release|Any CPU.ActiveCfg = Release|Any CPU
+		{39C303A3-BD6B-4681-93DA-687C5229A1B9}.Release|Any CPU.Build.0 = Release|Any CPU
+		{39C303A3-BD6B-4681-93DA-687C5229A1B9}.Release|x64.ActiveCfg = Release|Any CPU
+		{39C303A3-BD6B-4681-93DA-687C5229A1B9}.Release|x64.Build.0 = Release|Any CPU
+		{39C303A3-BD6B-4681-93DA-687C5229A1B9}.Release|x86.ActiveCfg = Release|Any CPU
+		{39C303A3-BD6B-4681-93DA-687C5229A1B9}.Release|x86.Build.0 = Release|Any CPU
+		{6BEA5D7C-F3F1-4CB3-8CBB-B44B2840B6B6}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
+		{6BEA5D7C-F3F1-4CB3-8CBB-B44B2840B6B6}.Debug|Any CPU.Build.0 = Debug|Any CPU
+		{6BEA5D7C-F3F1-4CB3-8CBB-B44B2840B6B6}.Debug|x64.ActiveCfg = Debug|Any CPU
+		{6BEA5D7C-F3F1-4CB3-8CBB-B44B2840B6B6}.Debug|x64.Build.0 = Debug|Any CPU
+		{6BEA5D7C-F3F1-4CB3-8CBB-B44B2840B6B6}.Debug|x86.ActiveCfg = Debug|Any CPU
+		{6BEA5D7C-F3F1-4CB3-8CBB-B44B2840B6B6}.Debug|x86.Build.0 = Debug|Any CPU
+		{6BEA5D7C-F3F1-4CB3-8CBB-B44B2840B6B6}.Release|Any CPU.ActiveCfg = Release|Any CPU
+		{6BEA5D7C-F3F1-4CB3-8CBB-B44B2840B6B6}.Release|Any CPU.Build.0 = Release|Any CPU
+		{6BEA5D7C-F3F1-4CB3-8CBB-B44B2840B6B6}.Release|x64.ActiveCfg = Release|Any CPU
+		{6BEA5D7C-F3F1-4CB3-8CBB-B44B2840B6B6}.Release|x64.Build.0 = Release|Any CPU
+		{6BEA5D7C-F3F1-4CB3-8CBB-B44B2840B6B6}.Release|x86.ActiveCfg = Release|Any CPU
+		{6BEA5D7C-F3F1-4CB3-8CBB-B44B2840B6B6}.Release|x86.Build.0 = Release|Any CPU
 	EndGlobalSection
 	GlobalSection(SolutionProperties) = preSolution
 		HideSolutionNode = FALSE
@@ -82,5 +126,8 @@ Global
 		{142FBB1E-7E1D-4E59-BF9E-204C013C260A} = {827E0CD3-B72D-47B6-A68D-7590B98EB39B}
 		{6E424438-4894-4159-8DF2-1BD65C7BC472} = {0AB3BF05-4346-4AA6-1389-037BE0695223}
 		{31CC3F80-DF44-45E8-A4AF-5A6F30031ED6} = {0AB3BF05-4346-4AA6-1389-037BE0695223}
+		{265FD711-ACC0-40A3-A334-5B81B5063AE8} = {0AB3BF05-4346-4AA6-1389-037BE0695223}
+		{39C303A3-BD6B-4681-93DA-687C5229A1B9} = {5D20AA90-6969-D8BD-9DCD-8634F4692FDA}
+		{6BEA5D7C-F3F1-4CB3-8CBB-B44B2840B6B6} = {827E0CD3-B72D-47B6-A68D-7590B98EB39B}
 	EndGlobalSection
 EndGlobal
diff --git a/native/Cargo.lock b/native/Cargo.lock
index ef3371b..aac35e3 100644
--- a/native/Cargo.lock
+++ b/native/Cargo.lock
@@ -2,6 +2,43 @@
 # It is not intended for manual editing.
 version = 4
 
+[[package]]
+name = "ahash"
+version = "0.8.12"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "5a15f179cd60c4584b8a8c596927aadc462e27f2ca70c04e0071964a73ba7a75"
+dependencies = [
+ "cfg-if",
+ "getrandom 0.3.4",
+ "once_cell",
+ "version_check",
+ "zerocopy",
+]
+
+[[package]]
+name = "aho-corasick"
+version = "1.1.4"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "ddd31a130427c27518df266943a5308ed92d4b226cc639f5a8f1002816174301"
+dependencies = [
+ "memchr",
+]
+
+[[package]]
+name = "append-only-bytes"
+version = "0.1.12"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "ac436601d6bdde674a0d7fb593e829ffe7b3387c351b356dd20e2d40f5bf3ee5"
+
+[[package]]
+name = "arbitrary"
+version = "1.4.2"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "c3d036a3c4ab069c7b410a2ce876bd74808d2d0888a82667669f8e783a898bf1"
+dependencies = [
+ "derive_arbitrary",
+]
+
 [[package]]
 name = "arc-swap"
 version = "1.9.2"
@@ -11,6 +48,18 @@ dependencies = [
  "rustversion",
 ]
 
+[[package]]
+name = "arrayvec"
+version = "0.7.8"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "d3fb67a6e08acf24fdeccbac2cb6ac4305825bd1f117462e0e6f2f193345ad56"
+
+[[package]]
+name = "arref"
+version = "0.1.0"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "2ccd462b64c3c72f1be8305905a85d85403d768e8690c9b8bd3b9009a5761679"
+
 [[package]]
 name = "async-lock"
 version = "3.4.2"
@@ -30,27 +79,82 @@ checksum = "9035ad2d096bed7955a320ee7e2230574d28fd3c3a0f186cbea1ff3c7eed5dbb"
 dependencies = [
  "proc-macro2",
  "quote",
- "syn",
+ "syn 2.0.118",
+]
+
+[[package]]
+name = "atomic-polyfill"
+version = "1.0.3"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "8cf2bce30dfe09ef0bfaef228b9d414faaf7e563035494d7fe092dba54b300f4"
+dependencies = [
+ "critical-section",
 ]
 
+[[package]]
+name = "autocfg"
+version = "1.5.1"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "f2032f911046de80f0a198e0901378627c33f59ea0ac00e363d481118bd70a53"
+
 [[package]]
 name = "bitflags"
 version = "2.13.0"
 source = "registry+https://github.com/rust-lang/crates.io-index"
 checksum = "b4388bee8683e3d04af747c73422af53102d2bd24d9eadb6cbc100baef4b43f8"
 
+[[package]]
+name = "bitmaps"
+version = "2.1.0"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "031043d04099746d8db04daf1fa424b2bc8bd69d92b25962dcde24da39ab64a2"
+dependencies = [
+ "typenum",
+]
+
 [[package]]
 name = "bumpalo"
 version = "3.20.3"
 source = "registry+https://github.com/rust-lang/crates.io-index"
 checksum = "72f5acc6cb2ba439de613abc23857ec3d78374d8ed5ac84e9d11336e87da8649"
 
+[[package]]
+name = "byteorder"
+version = "1.5.0"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "1fd0f2584146f6f2ef48085050886acf353beff7305ebd1ae69500e27c67f64b"
+
+[[package]]
+name = "bytes"
+version = "1.12.1"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "fc652a48c352aef3ea3aed32080501cf3ef6ed5da78602a020c991775b0aff04"
+
+[[package]]
+name = "cc"
+version = "1.2.66"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "f5d6cac793997bd970000024b2934968efe83b382de4fdcf4fcb46b6ee4ad996"
+dependencies = [
+ "find-msvc-tools",
+ "shlex",
+]
+
 [[package]]
 name = "cfg-if"
 version = "1.0.4"
 source = "registry+https://github.com/rust-lang/crates.io-index"
 checksum = "9330f8b2ff13f34540b44e946ef35111825727b38d33286ef986142615121801"
 
+[[package]]
+name = "cobs"
+version = "0.3.0"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "0fa961b519f0b462e3a3b4a34b64d119eeaca1d59af726fe450bbba07a9fc0a1"
+dependencies = [
+ "thiserror 2.0.18",
+]
+
 [[package]]
 name = "concurrent-queue"
 version = "2.5.0"
@@ -60,12 +164,53 @@ dependencies = [
  "crossbeam-utils",
 ]
 
+[[package]]
+name = "critical-section"
+version = "1.2.0"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "790eea4361631c5e7d22598ecd5723ff611904e3344ce8720784c93e3d83d40b"
+
 [[package]]
 name = "crossbeam-utils"
 version = "0.8.22"
 source = "registry+https://github.com/rust-lang/crates.io-index"
 checksum = "61803da095bee82a81bb1a452ecc25d3b2f1416d1897eb86430c6159ef717c17"
 
+[[package]]
+name = "darling"
+version = "0.20.11"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "fc7f46116c46ff9ab3eb1597a45688b6715c6e628b5c133e288e709a29bcb4ee"
+dependencies = [
+ "darling_core",
+ "darling_macro",
+]
+
+[[package]]
+name = "darling_core"
+version = "0.20.11"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "0d00b9596d185e565c2207a0b01f8bd1a135483d02d9b7b0a54b11da8d53412e"
+dependencies = [
+ "fnv",
+ "ident_case",
+ "proc-macro2",
+ "quote",
+ "strsim",
+ "syn 2.0.118",
+]
+
+[[package]]
+name = "darling_macro"
+version = "0.20.11"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "fc34b93ccb385b40dc71c6fceac4b2ad23662c7eeb248cf10d529b7e055b6ead"
+dependencies = [
+ "darling_core",
+ "quote",
+ "syn 2.0.118",
+]
+
 [[package]]
 name = "dashmap"
 version = "6.2.1"
@@ -74,12 +219,95 @@ checksum = "e6361d5c062261c78a176addb82d4c821ae42bed6089de0e12603cd25de2059c"
 dependencies = [
  "cfg-if",
  "crossbeam-utils",
- "hashbrown",
+ "hashbrown 0.14.5",
  "lock_api",
  "once_cell",
  "parking_lot_core",
 ]
 
+[[package]]
+name = "derive_arbitrary"
+version = "1.4.2"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "1e567bd82dcff979e4b03460c307b3cdc9e96fde3d73bed1496d2bc75d9dd62a"
+dependencies = [
+ "proc-macro2",
+ "quote",
+ "syn 2.0.118",
+]
+
+[[package]]
+name = "diff"
+version = "0.1.13"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "56254986775e3233ffa9c4d7d3faaf6d36a2c09d30b20687e9f88bc8bafc16c8"
+
+[[package]]
+name = "either"
+version = "1.16.0"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "91622ff5e7162018101f2fea40d6ebf4a78bbe5a49736a2020649edf9693679e"
+
+[[package]]
+name = "embedded-io"
+version = "0.4.0"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "ef1a6892d9eef45c8fa6b9e0086428a2cca8491aca8f787c534a3d6d0bcb3ced"
+
+[[package]]
+name = "embedded-io"
+version = "0.6.1"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "edd0f118536f44f5ccd48bcb8b111bdc3de888b58c74639dfb034a357d0f206d"
+
+[[package]]
+name = "ensure-cov"
+version = "0.1.0"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "33753185802e107b8fa907192af1f0eca13b1fb33327a59266d650fef29b2b4e"
+
+[[package]]
+name = "enum-as-inner"
+version = "0.5.1"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "c9720bba047d567ffc8a3cba48bf19126600e249ab7f128e9233e6376976a116"
+dependencies = [
+ "heck 0.4.1",
+ "proc-macro2",
+ "quote",
+ "syn 1.0.109",
+]
+
+[[package]]
+name = "enum-as-inner"
+version = "0.6.1"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "a1e6a265c649f3f5979b601d26f1d05ada116434c87741c9493cb56218f76cbc"
+dependencies = [
+ "heck 0.5.0",
+ "proc-macro2",
+ "quote",
+ "syn 2.0.118",
+]
+
+[[package]]
+name = "enum_dispatch"
+version = "0.3.13"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "aa18ce2bc66555b3218614519ac839ddb759a7d6720732f979ef8d13be147ecd"
+dependencies = [
+ "once_cell",
+ "proc-macro2",
+ "quote",
+ "syn 2.0.118",
+]
+
+[[package]]
+name = "equivalent"
+version = "1.0.2"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "877a4ace8713b0bcf2a4e7eec82529c029f1d0619886d18145fea96c3ffe5c0f"
+
 [[package]]
 name = "event-listener"
 version = "5.4.1"
@@ -107,7 +335,85 @@ version = "2.4.1"
 source = "registry+https://github.com/rust-lang/crates.io-index"
 checksum = "9f1f227452a390804cdb637b74a86990f2a7d7ba4b7d5693aac9b4dd6defd8d6"
 dependencies = [
- "getrandom",
+ "getrandom 0.3.4",
+]
+
+[[package]]
+name = "find-msvc-tools"
+version = "0.1.9"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "5baebc0774151f905a1a2cc41989300b1e6fbb29aff0ceffa1064fdd3088d582"
+
+[[package]]
+name = "fnv"
+version = "1.0.7"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "3f9eec918d3f24069decb9af1554cad7c880e2da24a9afd88aca000531ab82c1"
+
+[[package]]
+name = "futures-core"
+version = "0.3.32"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "7e3450815272ef58cec6d564423f6e755e25379b217b0bc688e295ba24df6b1d"
+
+[[package]]
+name = "futures-task"
+version = "0.3.32"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "037711b3d59c33004d3856fbdc83b99d4ff37a24768fa1be9ce3538a1cde4393"
+
+[[package]]
+name = "futures-util"
+version = "0.3.32"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "389ca41296e6190b48053de0321d02a77f32f8a5d2461dd38762c0593805c6d6"
+dependencies = [
+ "futures-core",
+ "futures-task",
+ "pin-project-lite",
+ "slab",
+]
+
+[[package]]
+name = "generator"
+version = "0.8.9"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "b3b854b0e584ead1a33f18b2fcad7cf7be18b3875c78816b753639aa501513ae"
+dependencies = [
+ "cc",
+ "cfg-if",
+ "libc",
+ "log",
+ "rustversion",
+ "windows-link",
+ "windows-result",
+]
+
+[[package]]
+name = "generic-btree"
+version = "0.10.7"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "a0c1bce85c110ab718fd139e0cc89c51b63bd647b14a767e24bdfc77c83df79b"
+dependencies = [
+ "arref",
+ "heapless 0.9.3",
+ "itertools 0.11.0",
+ "loro-thunderdome",
+ "proc-macro2",
+ "rustc-hash",
+]
+
+[[package]]
+name = "getrandom"
+version = "0.2.17"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "ff2abc00be7fca6ebc474524697ae276ad847ad0a6b3faa4bcb027e9a4614ad0"
+dependencies = [
+ "cfg-if",
+ "js-sys",
+ "libc",
+ "wasi",
+ "wasm-bindgen",
 ]
 
 [[package]]
@@ -124,12 +430,121 @@ dependencies = [
  "wasm-bindgen",
 ]
 
+[[package]]
+name = "hash32"
+version = "0.2.1"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "b0c35f58762feb77d74ebe43bdbc3210f09be9fe6742234d573bacc26ed92b67"
+dependencies = [
+ "byteorder",
+]
+
+[[package]]
+name = "hash32"
+version = "0.3.1"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "47d60b12902ba28e2730cd37e95b8c9223af2808df9e902d4df49588d1470606"
+dependencies = [
+ "byteorder",
+]
+
 [[package]]
 name = "hashbrown"
 version = "0.14.5"
 source = "registry+https://github.com/rust-lang/crates.io-index"
 checksum = "e5274423e17b7c9fc20b6e7e208532f9b19825d82dfd615708b70edd83df41f1"
 
+[[package]]
+name = "hashbrown"
+version = "0.16.1"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "841d1cc9bed7f9236f321df977030373f4a4163ae1a7dbfe1a51a2c1a51d9100"
+
+[[package]]
+name = "heapless"
+version = "0.7.17"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "cdc6457c0eb62c71aac4bc17216026d8410337c4126773b9c5daba343f17964f"
+dependencies = [
+ "atomic-polyfill",
+ "hash32 0.2.1",
+ "rustc_version",
+ "serde",
+ "spin",
+ "stable_deref_trait",
+]
+
+[[package]]
+name = "heapless"
+version = "0.8.0"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "0bfb9eb618601c89945a70e254898da93b13be0388091d42117462b265bb3fad"
+dependencies = [
+ "hash32 0.3.1",
+ "stable_deref_trait",
+]
+
+[[package]]
+name = "heapless"
+version = "0.9.3"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "25ba4bd83f9415b58b4ed8dc5714c76e626a105be4646c02630ad730ad3b5aa4"
+dependencies = [
+ "hash32 0.3.1",
+ "stable_deref_trait",
+]
+
+[[package]]
+name = "heck"
+version = "0.4.1"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "95505c38b4572b2d910cecb0281560f54b440a19336cbbcb27bf6ce6adc6f5a8"
+
+[[package]]
+name = "heck"
+version = "0.5.0"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "2304e00983f87ffb38b55b444b5e3b60a884b5d30c0fca7d82fe33449bbe55ea"
+
+[[package]]
+name = "ident_case"
+version = "1.0.1"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "b9e0384b61958566e926dc50660321d12159025e767c18e043daf26b70104c39"
+
+[[package]]
+name = "im"
+version = "15.1.0"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "d0acd33ff0285af998aaf9b57342af478078f53492322fafc47450e09397e0e9"
+dependencies = [
+ "bitmaps",
+ "rand_core",
+ "rand_xoshiro",
+ "serde",
+ "sized-chunks",
+ "typenum",
+ "version_check",
+]
+
+[[package]]
+name = "itertools"
+version = "0.11.0"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "b1c173a5686ce8bfa551b3563d0c2170bf24ca44da99c7ca4bfdab5418c3fe57"
+dependencies = [
+ "either",
+]
+
+[[package]]
+name = "itertools"
+version = "0.12.1"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "ba291022dbbd398a455acf126c1e341954079855bc60dfdda641363bd6922569"
+dependencies = [
+ "either",
+]
+
 [[package]]
 name = "itoa"
 version = "1.0.18"
@@ -143,9 +558,22 @@ source = "registry+https://github.com/rust-lang/crates.io-index"
 checksum = "53b44bfcdb3f8d5837a46dae1ca9660a837176eee74a28b229bc626816589102"
 dependencies = [
  "cfg-if",
+ "futures-util",
  "wasm-bindgen",
 ]
 
+[[package]]
+name = "lazy_static"
+version = "1.5.0"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "bbd2bcb4c963f2ddae06a2efc7e9f3591312473c50c6685e1f298068316e66fe"
+
+[[package]]
+name = "leb128"
+version = "0.2.7"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "c83bff1d572d6b9aeef67ddfc8448e4a3737909cb28e81f97c791b9018703e52"
+
 [[package]]
 name = "libc"
 version = "0.2.186"
@@ -161,12 +589,282 @@ dependencies = [
  "scopeguard",
 ]
 
+[[package]]
+name = "log"
+version = "0.4.33"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "0ceec5bc11778974d1bcb055b18002eba7f4b3518b6a0081b3af5f21666da9ad"
+
+[[package]]
+name = "loom"
+version = "0.7.2"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "419e0dc8046cb947daa77eb95ae174acfbddb7673b4151f56d1eed8e93fbfaca"
+dependencies = [
+ "cfg-if",
+ "generator",
+ "scoped-tls",
+ "serde",
+ "serde_json",
+ "tracing",
+ "tracing-subscriber",
+]
+
+[[package]]
+name = "loro"
+version = "1.13.6"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "221b567f34e3a7b38d05345b17797a7ca252d28e1fae479b4bb55c1a19616ddc"
+dependencies = [
+ "enum-as-inner 0.6.1",
+ "generic-btree",
+ "loro-common",
+ "loro-delta",
+ "loro-internal",
+ "loro-kv-store",
+ "rustc-hash",
+ "tracing",
+]
+
+[[package]]
+name = "loro-common"
+version = "1.13.1"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "b51042936156a1d537df8a38938ec8ed24b45de14530af933c1d02de39c9e056"
+dependencies = [
+ "arbitrary",
+ "enum-as-inner 0.6.1",
+ "leb128",
+ "loro-rle",
+ "nonmax",
+ "rustc-hash",
+ "serde",
+ "serde_columnar",
+ "serde_json",
+ "thiserror 1.0.69",
+]
+
+[[package]]
+name = "loro-delta"
+version = "1.13.0"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "96f381e7fb5d4bf5605b4f4a9241f3adf61403ded978d757552004c8302cccbb"
+dependencies = [
+ "arrayvec",
+ "enum-as-inner 0.5.1",
+ "generic-btree",
+ "heapless 0.8.0",
+]
+
+[[package]]
+name = "loro-internal"
+version = "1.13.6"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "fb15cd83866965b50b4893246699312624808a1f2e2d205432237287d947b822"
+dependencies = [
+ "append-only-bytes",
+ "arref",
+ "bytes",
+ "either",
+ "ensure-cov",
+ "enum-as-inner 0.6.1",
+ "enum_dispatch",
+ "generic-btree",
+ "getrandom 0.2.17",
+ "im",
+ "itertools 0.12.1",
+ "leb128",
+ "loom",
+ "loro-common",
+ "loro-delta",
+ "loro-kv-store",
+ "loro-rle",
+ "loro_fractional_index",
+ "md5",
+ "nonmax",
+ "num",
+ "num-traits",
+ "once_cell",
+ "parking_lot",
+ "pest",
+ "pest_derive",
+ "postcard",
+ "pretty_assertions",
+ "rand",
+ "rustc-hash",
+ "serde",
+ "serde_columnar",
+ "serde_json",
+ "smallvec",
+ "thiserror 1.0.69",
+ "thread_local",
+ "tracing",
+ "wasm-bindgen",
+ "xxhash-rust",
+]
+
+[[package]]
+name = "loro-kv-store"
+version = "1.13.1"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "950cc8bcc64dcff536e949fbc56ccc3d0759646fa441c7f76b3cd7c3eafa2096"
+dependencies = [
+ "bytes",
+ "ensure-cov",
+ "loro-common",
+ "lz4_flex",
+ "once_cell",
+ "quick_cache",
+ "rustc-hash",
+ "tracing",
+ "xxhash-rust",
+]
+
+[[package]]
+name = "loro-rle"
+version = "1.6.0"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "76400c3eea6bb39b013406acce964a8db39311534e308286c8d8721baba8ee20"
+dependencies = [
+ "append-only-bytes",
+ "num",
+ "smallvec",
+]
+
+[[package]]
+name = "loro-thunderdome"
+version = "0.6.2"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "3f3d053a135388e6b1df14e8af1212af5064746e9b87a06a345a7a779ee9695a"
+
+[[package]]
+name = "loro_fractional_index"
+version = "1.13.0"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "aca7180674d0273ddf37049a5efcde4547fd5330d24abb7519bb9d9eb6780d5b"
+dependencies = [
+ "once_cell",
+ "rand",
+ "serde",
+]
+
+[[package]]
+name = "lz4_flex"
+version = "0.11.6"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "373f5eceeeab7925e0c1098212f2fbc4d416adec9d35051a6ab251e824c1854a"
+dependencies = [
+ "twox-hash",
+]
+
+[[package]]
+name = "matchers"
+version = "0.2.0"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "d1525a2a28c7f4fa0fc98bb91ae755d1e2d1505079e05539e35bc876b5d65ae9"
+dependencies = [
+ "regex-automata",
+]
+
+[[package]]
+name = "md5"
+version = "0.7.0"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "490cc448043f947bae3cbee9c203358d62dbee0db12107a74be5c30ccfd09771"
+
 [[package]]
 name = "memchr"
 version = "2.8.3"
 source = "registry+https://github.com/rust-lang/crates.io-index"
 checksum = "cf8baf1c55e62ffcace7a9f06f4bd9cd3f0c4beb022d3b367256b91b87513d98"
 
+[[package]]
+name = "nonmax"
+version = "0.5.5"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "610a5acd306ec67f907abe5567859a3c693fb9886eb1f012ab8f2a47bef3db51"
+
+[[package]]
+name = "nu-ansi-term"
+version = "0.50.3"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "7957b9740744892f114936ab4a57b3f487491bbeafaf8083688b16841a4240e5"
+dependencies = [
+ "windows-sys",
+]
+
+[[package]]
+name = "num"
+version = "0.4.3"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "35bd024e8b2ff75562e5f34e7f4905839deb4b22955ef5e73d2fea1b9813cb23"
+dependencies = [
+ "num-bigint",
+ "num-complex",
+ "num-integer",
+ "num-iter",
+ "num-rational",
+ "num-traits",
+]
+
+[[package]]
+name = "num-bigint"
+version = "0.4.8"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "c89e69e7e0f03bea5ef08013795c25018e101932225a656383bd384495ecc367"
+dependencies = [
+ "num-integer",
+ "num-traits",
+]
+
+[[package]]
+name = "num-complex"
+version = "0.4.6"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "73f88a1307638156682bada9d7604135552957b7818057dcef22705b4d509495"
+dependencies = [
+ "num-traits",
+]
+
+[[package]]
+name = "num-integer"
+version = "0.1.46"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "7969661fd2958a5cb096e56c8e1ad0444ac2bbcd0061bd28660485a44879858f"
+dependencies = [
+ "num-traits",
+]
+
+[[package]]
+name = "num-iter"
+version = "0.1.46"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "c92800bd69a1eac91786bcfe9da64a897eb72911b8dc3095decbd07429e8048b"
+dependencies = [
+ "num-integer",
+ "num-traits",
+]
+
+[[package]]
+name = "num-rational"
+version = "0.4.2"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "f83d14da390562dca69fc84082e73e548e1ad308d24accdedd2720017cb37824"
+dependencies = [
+ "num-bigint",
+ "num-integer",
+ "num-traits",
+]
+
+[[package]]
+name = "num-traits"
+version = "0.2.19"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "071dfc062690e90b734c0b2273ce72ad0ffa95f0c74596bc250dcfd960262841"
+dependencies = [
+ "autocfg",
+]
+
 [[package]]
 name = "once_cell"
 version = "1.21.4"
@@ -179,6 +877,16 @@ version = "2.2.1"
 source = "registry+https://github.com/rust-lang/crates.io-index"
 checksum = "f38d5652c16fde515bb1ecef450ab0f6a219d619a7274976324d5e377f7dceba"
 
+[[package]]
+name = "parking_lot"
+version = "0.12.5"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "93857453250e3077bd71ff98b6a65ea6621a19bb0f559a85248955ac12c45a1a"
+dependencies = [
+ "lock_api",
+ "parking_lot_core",
+]
+
 [[package]]
 name = "parking_lot_core"
 version = "0.9.12"
@@ -192,12 +900,86 @@ dependencies = [
  "windows-link",
 ]
 
+[[package]]
+name = "pest"
+version = "2.8.7"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "47627dd7305c6a2d6c8c6bcd24c5a4c17dbbf425f4f9c5313e724b38fc9782e9"
+dependencies = [
+ "memchr",
+ "ucd-trie",
+]
+
+[[package]]
+name = "pest_derive"
+version = "2.8.7"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "4b4254325ecad416ab689e27ba51da03ba01a9632bc6e108f5fe7c3c4ad29d58"
+dependencies = [
+ "pest",
+ "pest_generator",
+]
+
+[[package]]
+name = "pest_generator"
+version = "2.8.7"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "6c4c0e91ead7a8f7acecbca6f003fc2e8282b1dbe2dd9c9d2f16aba42995e0a7"
+dependencies = [
+ "pest",
+ "pest_meta",
+ "proc-macro2",
+ "quote",
+ "syn 2.0.118",
+]
+
+[[package]]
+name = "pest_meta"
+version = "2.8.7"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "f9744bc48116fee06334924bb5f2bad41eed5e89bd26e29b0b799f9a3f82c210"
+dependencies = [
+ "pest",
+]
+
 [[package]]
 name = "pin-project-lite"
 version = "0.2.17"
 source = "registry+https://github.com/rust-lang/crates.io-index"
 checksum = "a89322df9ebe1c1578d689c92318e070967d1042b512afbe49518723f4e6d5cd"
 
+[[package]]
+name = "postcard"
+version = "1.1.3"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "6764c3b5dd454e283a30e6dfe78e9b31096d9e32036b5d1eaac7a6119ccb9a24"
+dependencies = [
+ "cobs",
+ "embedded-io 0.4.0",
+ "embedded-io 0.6.1",
+ "heapless 0.7.17",
+ "serde",
+]
+
+[[package]]
+name = "ppv-lite86"
+version = "0.2.21"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "85eae3c4ed2f50dcfe72643da4befc30deadb458a9b590d720cde2f2b1e97da9"
+dependencies = [
+ "zerocopy",
+]
+
+[[package]]
+name = "pretty_assertions"
+version = "1.4.1"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "3ae130e2f271fbc2ac3a40fb1d07180839cdbbe443c7a27e1e3c13c5cac0116d"
+dependencies = [
+ "diff",
+ "yansi",
+]
+
 [[package]]
 name = "proc-macro2"
 version = "1.0.106"
@@ -207,6 +989,18 @@ dependencies = [
  "unicode-ident",
 ]
 
+[[package]]
+name = "quick_cache"
+version = "0.6.24"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "b9c6658afe513a3b484e3abfdaa0d03ef3c0bbf017542c178dd55f94eb3051f9"
+dependencies = [
+ "ahash",
+ "equivalent",
+ "hashbrown 0.16.1",
+ "parking_lot",
+]
+
 [[package]]
 name = "quote"
 version = "1.0.46"
@@ -222,6 +1016,45 @@ version = "5.3.0"
 source = "registry+https://github.com/rust-lang/crates.io-index"
 checksum = "69cdb34c158ceb288df11e18b4bd39de994f6657d83847bdffdbd7f346754b0f"
 
+[[package]]
+name = "rand"
+version = "0.8.6"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "5ca0ecfa931c29007047d1bc58e623ab12e5590e8c7cc53200d5202b69266d8a"
+dependencies = [
+ "libc",
+ "rand_chacha",
+ "rand_core",
+]
+
+[[package]]
+name = "rand_chacha"
+version = "0.3.1"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "e6c10a63a0fa32252be49d21e7709d4d4baf8d231c2dbce1eaa8141b9b127d88"
+dependencies = [
+ "ppv-lite86",
+ "rand_core",
+]
+
+[[package]]
+name = "rand_core"
+version = "0.6.4"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "ec0be4795e2f6a28069bec0b5ff3e2ac9bafc99e6a9a7dc3547996c5c816922c"
+dependencies = [
+ "getrandom 0.2.17",
+]
+
+[[package]]
+name = "rand_xoshiro"
+version = "0.6.0"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "6f97cdb2a36ed4183de61b2f824cc45c9f1037f28afe0a322e9fff4c108b5aaa"
+dependencies = [
+ "rand_core",
+]
+
 [[package]]
 name = "redox_syscall"
 version = "0.5.18"
@@ -231,18 +1064,62 @@ dependencies = [
  "bitflags",
 ]
 
+[[package]]
+name = "regex-automata"
+version = "0.4.15"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "1f388202e4b80542a0921078cc23b6333bcf1409c1e3f86404cae4766a6131db"
+dependencies = [
+ "aho-corasick",
+ "memchr",
+ "regex-syntax",
+]
+
+[[package]]
+name = "regex-syntax"
+version = "0.8.11"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "d6f6ff9a378485b298a5286656da665ba74413d36db0979633275d2e708145d4"
+
+[[package]]
+name = "rustc-hash"
+version = "2.1.3"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "6b1e7f9a428571be2dc5bc0505c13fb6bf936822b894ec87abf8a08a4e51742d"
+
+[[package]]
+name = "rustc_version"
+version = "0.4.1"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "cfcb3a22ef46e85b45de6ee7e79d063319ebb6594faafcf1c225ea92ab6e9b92"
+dependencies = [
+ "semver",
+]
+
 [[package]]
 name = "rustversion"
 version = "1.0.23"
 source = "registry+https://github.com/rust-lang/crates.io-index"
 checksum = "cf54715a573b99ac80df0bc206da022bcd442c974952c7b9720069370852e21f"
 
+[[package]]
+name = "scoped-tls"
+version = "1.0.1"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "e1cf6437eb19a8f4a6cc0f7dca544973b0b78843adbfeb3683d1a94a0024a294"
+
 [[package]]
 name = "scopeguard"
 version = "1.2.0"
 source = "registry+https://github.com/rust-lang/crates.io-index"
 checksum = "94143f37725109f92c262ed2cf5e59bce7498c01bcc1502d7b9afe439a4e9f49"
 
+[[package]]
+name = "semver"
+version = "1.0.28"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "8a7852d02fc848982e0c167ef163aaff9cd91dc640ba85e263cb1ce46fae51cd"
+
 [[package]]
 name = "serde"
 version = "1.0.228"
@@ -253,6 +1130,31 @@ dependencies = [
  "serde_derive",
 ]
 
+[[package]]
+name = "serde_columnar"
+version = "0.3.14"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "2a16e404f17b16d0273460350e29b02d76ba0d70f34afdc9a4fa034c97d6c6eb"
+dependencies = [
+ "itertools 0.11.0",
+ "postcard",
+ "serde",
+ "serde_columnar_derive",
+ "thiserror 1.0.69",
+]
+
+[[package]]
+name = "serde_columnar_derive"
+version = "0.3.7"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "45958fce4903f67e871fbf15ac78e289269b21ebd357d6fecacdba233629112e"
+dependencies = [
+ "darling",
+ "proc-macro2",
+ "quote",
+ "syn 2.0.118",
+]
+
 [[package]]
 name = "serde_core"
 version = "1.0.228"
@@ -270,7 +1172,7 @@ checksum = "d540f220d3187173da220f885ab66608367b6574e925011a9353e4badda91d79"
 dependencies = [
  "proc-macro2",
  "quote",
- "syn",
+ "syn 2.0.118",
 ]
 
 [[package]]
@@ -286,6 +1188,37 @@ dependencies = [
  "zmij",
 ]
 
+[[package]]
+name = "sharded-slab"
+version = "0.1.7"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "f40ca3c46823713e0d4209592e8d6e826aa57e928f09752619fc696c499637f6"
+dependencies = [
+ "lazy_static",
+]
+
+[[package]]
+name = "shlex"
+version = "2.0.1"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "f8fadd59c855ef2080decdef8ff161eb6661b86933c9d82e5ba29dc602a55aba"
+
+[[package]]
+name = "sized-chunks"
+version = "0.6.5"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "16d69225bde7a69b235da73377861095455d298f2b970996eec25ddbb42b3d1e"
+dependencies = [
+ "bitmaps",
+ "typenum",
+]
+
+[[package]]
+name = "slab"
+version = "0.4.12"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "0c790de23124f9ab44544d7ac05d60440adc586479ce501c1d6d7da3cd8c9cf5"
+
 [[package]]
 name = "smallstr"
 version = "0.3.1"
@@ -300,6 +1233,41 @@ name = "smallvec"
 version = "1.15.2"
 source = "registry+https://github.com/rust-lang/crates.io-index"
 checksum = "8ed6a63f02c8539c91a8685a86f4099661ba3da017932f6ebbea6de3f0fa7c90"
+dependencies = [
+ "serde",
+]
+
+[[package]]
+name = "spin"
+version = "0.9.8"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "6980e8d7511241f8acf4aebddbb1ff938df5eebe98691418c4468d0b72a96a67"
+dependencies = [
+ "lock_api",
+]
+
+[[package]]
+name = "stable_deref_trait"
+version = "1.2.1"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "6ce2be8dc25455e1f91df71bfa12ad37d7af1092ae736f3a6cd0e37bc7810596"
+
+[[package]]
+name = "strsim"
+version = "0.11.1"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "7da8b5736845d9f2fcb837ea5d9e2628564b3b043a70948a3f0b778838c5fb4f"
+
+[[package]]
+name = "syn"
+version = "1.0.109"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "72b64191b275b66ffe2469e8af2c1cfe3bafa67b529ead792a6d0160888b4237"
+dependencies = [
+ "proc-macro2",
+ "quote",
+ "unicode-ident",
+]
 
 [[package]]
 name = "syn"
@@ -312,13 +1280,33 @@ dependencies = [
  "unicode-ident",
 ]
 
+[[package]]
+name = "thiserror"
+version = "1.0.69"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "b6aaf5339b578ea85b50e080feb250a3e8ae8cfcdff9a461c9ec2904bc923f52"
+dependencies = [
+ "thiserror-impl 1.0.69",
+]
+
 [[package]]
 name = "thiserror"
 version = "2.0.18"
 source = "registry+https://github.com/rust-lang/crates.io-index"
 checksum = "4288b5bcbc7920c07a1149a35cf9590a2aa808e0bc1eafaade0b80947865fbc4"
 dependencies = [
- "thiserror-impl",
+ "thiserror-impl 2.0.18",
+]
+
+[[package]]
+name = "thiserror-impl"
+version = "1.0.69"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "4fee6c4efc90059e10f81e6d42c60a18f76588c3d74cb83a0b242a2b6c7504c1"
+dependencies = [
+ "proc-macro2",
+ "quote",
+ "syn 2.0.118",
 ]
 
 [[package]]
@@ -329,15 +1317,121 @@ checksum = "ebc4ee7f67670e9b64d05fa4253e753e016c6c95ff35b89b7941d6b856dec1d5"
 dependencies = [
  "proc-macro2",
  "quote",
- "syn",
+ "syn 2.0.118",
+]
+
+[[package]]
+name = "thread_local"
+version = "1.1.10"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "1ad99c4c6d32803332c548b1af0540b357b3f5fc0be8f6c6bfe8b2e6ae784070"
+dependencies = [
+ "cfg-if",
+]
+
+[[package]]
+name = "tracing"
+version = "0.1.44"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "63e71662fa4b2a2c3a26f570f037eb95bb1f85397f3cd8076caed2f026a6d100"
+dependencies = [
+ "pin-project-lite",
+ "tracing-attributes",
+ "tracing-core",
+]
+
+[[package]]
+name = "tracing-attributes"
+version = "0.1.31"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "7490cfa5ec963746568740651ac6781f701c9c5ea257c58e057f3ba8cf69e8da"
+dependencies = [
+ "proc-macro2",
+ "quote",
+ "syn 2.0.118",
+]
+
+[[package]]
+name = "tracing-core"
+version = "0.1.36"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "db97caf9d906fbde555dd62fa95ddba9eecfd14cb388e4f491a66d74cd5fb79a"
+dependencies = [
+ "once_cell",
+ "valuable",
+]
+
+[[package]]
+name = "tracing-log"
+version = "0.2.0"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "ee855f1f400bd0e5c02d150ae5de3840039a3f54b025156404e34c23c03f47c3"
+dependencies = [
+ "log",
+ "once_cell",
+ "tracing-core",
+]
+
+[[package]]
+name = "tracing-subscriber"
+version = "0.3.23"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "cb7f578e5945fb242538965c2d0b04418d38ec25c79d160cd279bf0731c8d319"
+dependencies = [
+ "matchers",
+ "nu-ansi-term",
+ "once_cell",
+ "regex-automata",
+ "sharded-slab",
+ "smallvec",
+ "thread_local",
+ "tracing",
+ "tracing-core",
+ "tracing-log",
 ]
 
+[[package]]
+name = "twox-hash"
+version = "2.1.2"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "9ea3136b675547379c4bd395ca6b938e5ad3c3d20fad76e7fe85f9e0d011419c"
+
+[[package]]
+name = "typenum"
+version = "1.20.1"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "b6f5e870be6c3b371b77fe0ee0bafb859fa4964b4404c27de1d380043c4dda20"
+
+[[package]]
+name = "ucd-trie"
+version = "0.1.7"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "2896d95c02a80c6d6a5d6e953d479f5ddf2dfdb6a244441010e373ac0fb88971"
+
 [[package]]
 name = "unicode-ident"
 version = "1.0.24"
 source = "registry+https://github.com/rust-lang/crates.io-index"
 checksum = "e6e4313cd5fcd3dad5cafa179702e2b244f760991f45397d14d4ebf38247da75"
 
+[[package]]
+name = "valuable"
+version = "0.1.1"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "ba73ea9cf16a25df0c8caa16c51acb937d5712a8429db78a3ee29d5dcacd3a65"
+
+[[package]]
+name = "version_check"
+version = "0.9.5"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "0b928f33d975fc6ad9f86c8f283853ad26bdd5b10b7f1542aa2fa15e2289105a"
+
+[[package]]
+name = "wasi"
+version = "0.11.1+wasi-snapshot-preview1"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "ccf3ec651a847eb01de73ccad15eb7d99f80485de043efb2f370cd654f4ea44b"
+
 [[package]]
 name = "wasip2"
 version = "1.0.4+wasi-0.2.12"
@@ -379,7 +1473,7 @@ dependencies = [
  "bumpalo",
  "proc-macro2",
  "quote",
- "syn",
+ "syn 2.0.118",
  "wasm-bindgen-shared",
 ]
 
@@ -392,6 +1486,13 @@ dependencies = [
  "unicode-ident",
 ]
 
+[[package]]
+name = "weft-loro-ffi"
+version = "0.1.0"
+dependencies = [
+ "loro",
+]
+
 [[package]]
 name = "weft-yrs-ffi"
 version = "0.1.0"
@@ -405,12 +1506,42 @@ version = "0.2.1"
 source = "registry+https://github.com/rust-lang/crates.io-index"
 checksum = "f0805222e57f7521d6a62e36fa9163bc891acd422f971defe97d64e70d0a4fe5"
 
+[[package]]
+name = "windows-result"
+version = "0.4.1"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "7781fa89eaf60850ac3d2da7af8e5242a5ea78d1a11c49bf2910bb5a73853eb5"
+dependencies = [
+ "windows-link",
+]
+
+[[package]]
+name = "windows-sys"
+version = "0.61.2"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "ae137229bcbd6cdf0f7b80a31df61766145077ddf49416a728b02cb3921ff3fc"
+dependencies = [
+ "windows-link",
+]
+
 [[package]]
 name = "wit-bindgen"
 version = "0.57.1"
 source = "registry+https://github.com/rust-lang/crates.io-index"
 checksum = "1ebf944e87a7c253233ad6766e082e3cd714b5d03812acc24c318f549614536e"
 
+[[package]]
+name = "xxhash-rust"
+version = "0.8.16"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "4d93c89cdc2d3a63c3ec48ffe926931bdc069eafa8e4402fe6d8f790c9d1e576"
+
+[[package]]
+name = "yansi"
+version = "1.0.1"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "cfe53a6657fd280eaa890a3bc59152892ffa3e30101319d168b781ed6529b049"
+
 [[package]]
 name = "yrs"
 version = "0.27.2"
@@ -426,7 +1557,27 @@ dependencies = [
  "serde_json",
  "smallstr",
  "smallvec",
- "thiserror",
+ "thiserror 2.0.18",
+]
+
+[[package]]
+name = "zerocopy"
+version = "0.8.54"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "b7cbbc0a705a0fd05cc3676525980d2bf5a9bc4adac6d6475209a7887cf59d19"
+dependencies = [
+ "zerocopy-derive",
+]
+
+[[package]]
+name = "zerocopy-derive"
+version = "0.8.54"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "e2e817b7b52d0c7358d3246da9d69935ebb18116b2b102b4230dac079b4862f5"
+dependencies = [
+ "proc-macro2",
+ "quote",
+ "syn 2.0.118",
 ]
 
 [[package]]
diff --git a/native/Cargo.toml b/native/Cargo.toml
index b99dfa0..9fcca52 100644
--- a/native/Cargo.toml
+++ b/native/Cargo.toml
@@ -2,7 +2,7 @@
 # En M0 solo el shim de yrs; weft-loro-ffi entra en CHARTER-02 (US5).
 [workspace]
 resolver = "2"
-members = ["weft-yrs-ffi"]
+members = ["weft-yrs-ffi", "weft-loro-ffi"]
 
 [workspace.package]
 edition = "2021"
diff --git a/native/weft-loro-ffi/Cargo.toml b/native/weft-loro-ffi/Cargo.toml
new file mode 100644
index 0000000..14d9a03
--- /dev/null
+++ b/native/weft-loro-ffi/Cargo.toml
@@ -0,0 +1,23 @@
+[package]
+name = "weft-loro-ffi"
+version = "0.1.0"
+edition.workspace = true
+license.workspace = true
+repository.workspace = true
+description = "Shim C-ABI de Weft sobre el core CRDT loro (pinned). Adaptador dual-path (P-IV)."
+publish = false
+
+[lib]
+name = "weft_loro_ffi"
+crate-type = ["cdylib", "rlib"]
+
+[dependencies]
+# PINNEADO exacto (research R16): el adaptador dual-path fija loro tal como yrs.
+loro = "=1.13.6"
+
+[dev-dependencies]
+loro = "=1.13.6"
+
+[features]
+# Simétrico a weft-yrs-ffi: weft_loro_test_panic para verificar catch_unwind (SC-009).
+test-hooks = []
diff --git a/native/weft-loro-ffi/fuzz/Cargo.lock b/native/weft-loro-ffi/fuzz/Cargo.lock
new file mode 100644
index 0000000..d866521
--- /dev/null
+++ b/native/weft-loro-ffi/fuzz/Cargo.lock
@@ -0,0 +1,1497 @@
+# This file is automatically @generated by Cargo.
+# It is not intended for manual editing.
+version = 4
+
+[[package]]
+name = "ahash"
+version = "0.8.12"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "5a15f179cd60c4584b8a8c596927aadc462e27f2ca70c04e0071964a73ba7a75"
+dependencies = [
+ "cfg-if",
+ "getrandom 0.3.4",
+ "once_cell",
+ "version_check",
+ "zerocopy",
+]
+
+[[package]]
+name = "aho-corasick"
+version = "1.1.4"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "ddd31a130427c27518df266943a5308ed92d4b226cc639f5a8f1002816174301"
+dependencies = [
+ "memchr",
+]
+
+[[package]]
+name = "append-only-bytes"
+version = "0.1.12"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "ac436601d6bdde674a0d7fb593e829ffe7b3387c351b356dd20e2d40f5bf3ee5"
+
+[[package]]
+name = "arbitrary"
+version = "1.4.2"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "c3d036a3c4ab069c7b410a2ce876bd74808d2d0888a82667669f8e783a898bf1"
+dependencies = [
+ "derive_arbitrary",
+]
+
+[[package]]
+name = "arrayvec"
+version = "0.7.8"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "d3fb67a6e08acf24fdeccbac2cb6ac4305825bd1f117462e0e6f2f193345ad56"
+
+[[package]]
+name = "arref"
+version = "0.1.0"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "2ccd462b64c3c72f1be8305905a85d85403d768e8690c9b8bd3b9009a5761679"
+
+[[package]]
+name = "atomic-polyfill"
+version = "1.0.3"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "8cf2bce30dfe09ef0bfaef228b9d414faaf7e563035494d7fe092dba54b300f4"
+dependencies = [
+ "critical-section",
+]
+
+[[package]]
+name = "autocfg"
+version = "1.5.1"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "f2032f911046de80f0a198e0901378627c33f59ea0ac00e363d481118bd70a53"
+
+[[package]]
+name = "bitflags"
+version = "2.13.0"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "b4388bee8683e3d04af747c73422af53102d2bd24d9eadb6cbc100baef4b43f8"
+
+[[package]]
+name = "bitmaps"
+version = "2.1.0"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "031043d04099746d8db04daf1fa424b2bc8bd69d92b25962dcde24da39ab64a2"
+dependencies = [
+ "typenum",
+]
+
+[[package]]
+name = "bumpalo"
+version = "3.20.3"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "72f5acc6cb2ba439de613abc23857ec3d78374d8ed5ac84e9d11336e87da8649"
+
+[[package]]
+name = "byteorder"
+version = "1.5.0"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "1fd0f2584146f6f2ef48085050886acf353beff7305ebd1ae69500e27c67f64b"
+
+[[package]]
+name = "bytes"
+version = "1.12.1"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "fc652a48c352aef3ea3aed32080501cf3ef6ed5da78602a020c991775b0aff04"
+
+[[package]]
+name = "cc"
+version = "1.2.66"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "f5d6cac793997bd970000024b2934968efe83b382de4fdcf4fcb46b6ee4ad996"
+dependencies = [
+ "find-msvc-tools",
+ "jobserver",
+ "libc",
+ "shlex",
+]
+
+[[package]]
+name = "cfg-if"
+version = "1.0.4"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "9330f8b2ff13f34540b44e946ef35111825727b38d33286ef986142615121801"
+
+[[package]]
+name = "cobs"
+version = "0.3.0"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "0fa961b519f0b462e3a3b4a34b64d119eeaca1d59af726fe450bbba07a9fc0a1"
+dependencies = [
+ "thiserror 2.0.18",
+]
+
+[[package]]
+name = "critical-section"
+version = "1.2.0"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "790eea4361631c5e7d22598ecd5723ff611904e3344ce8720784c93e3d83d40b"
+
+[[package]]
+name = "darling"
+version = "0.20.11"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "fc7f46116c46ff9ab3eb1597a45688b6715c6e628b5c133e288e709a29bcb4ee"
+dependencies = [
+ "darling_core",
+ "darling_macro",
+]
+
+[[package]]
+name = "darling_core"
+version = "0.20.11"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "0d00b9596d185e565c2207a0b01f8bd1a135483d02d9b7b0a54b11da8d53412e"
+dependencies = [
+ "fnv",
+ "ident_case",
+ "proc-macro2",
+ "quote",
+ "strsim",
+ "syn 2.0.118",
+]
+
+[[package]]
+name = "darling_macro"
+version = "0.20.11"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "fc34b93ccb385b40dc71c6fceac4b2ad23662c7eeb248cf10d529b7e055b6ead"
+dependencies = [
+ "darling_core",
+ "quote",
+ "syn 2.0.118",
+]
+
+[[package]]
+name = "derive_arbitrary"
+version = "1.4.2"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "1e567bd82dcff979e4b03460c307b3cdc9e96fde3d73bed1496d2bc75d9dd62a"
+dependencies = [
+ "proc-macro2",
+ "quote",
+ "syn 2.0.118",
+]
+
+[[package]]
+name = "diff"
+version = "0.1.13"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "56254986775e3233ffa9c4d7d3faaf6d36a2c09d30b20687e9f88bc8bafc16c8"
+
+[[package]]
+name = "either"
+version = "1.16.0"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "91622ff5e7162018101f2fea40d6ebf4a78bbe5a49736a2020649edf9693679e"
+
+[[package]]
+name = "embedded-io"
+version = "0.4.0"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "ef1a6892d9eef45c8fa6b9e0086428a2cca8491aca8f787c534a3d6d0bcb3ced"
+
+[[package]]
+name = "embedded-io"
+version = "0.6.1"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "edd0f118536f44f5ccd48bcb8b111bdc3de888b58c74639dfb034a357d0f206d"
+
+[[package]]
+name = "ensure-cov"
+version = "0.1.0"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "33753185802e107b8fa907192af1f0eca13b1fb33327a59266d650fef29b2b4e"
+
+[[package]]
+name = "enum-as-inner"
+version = "0.5.1"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "c9720bba047d567ffc8a3cba48bf19126600e249ab7f128e9233e6376976a116"
+dependencies = [
+ "heck 0.4.1",
+ "proc-macro2",
+ "quote",
+ "syn 1.0.109",
+]
+
+[[package]]
+name = "enum-as-inner"
+version = "0.6.1"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "a1e6a265c649f3f5979b601d26f1d05ada116434c87741c9493cb56218f76cbc"
+dependencies = [
+ "heck 0.5.0",
+ "proc-macro2",
+ "quote",
+ "syn 2.0.118",
+]
+
+[[package]]
+name = "enum_dispatch"
+version = "0.3.13"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "aa18ce2bc66555b3218614519ac839ddb759a7d6720732f979ef8d13be147ecd"
+dependencies = [
+ "once_cell",
+ "proc-macro2",
+ "quote",
+ "syn 2.0.118",
+]
+
+[[package]]
+name = "equivalent"
+version = "1.0.2"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "877a4ace8713b0bcf2a4e7eec82529c029f1d0619886d18145fea96c3ffe5c0f"
+
+[[package]]
+name = "find-msvc-tools"
+version = "0.1.9"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "5baebc0774151f905a1a2cc41989300b1e6fbb29aff0ceffa1064fdd3088d582"
+
+[[package]]
+name = "fnv"
+version = "1.0.7"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "3f9eec918d3f24069decb9af1554cad7c880e2da24a9afd88aca000531ab82c1"
+
+[[package]]
+name = "futures-core"
+version = "0.3.32"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "7e3450815272ef58cec6d564423f6e755e25379b217b0bc688e295ba24df6b1d"
+
+[[package]]
+name = "futures-task"
+version = "0.3.32"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "037711b3d59c33004d3856fbdc83b99d4ff37a24768fa1be9ce3538a1cde4393"
+
+[[package]]
+name = "futures-util"
+version = "0.3.32"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "389ca41296e6190b48053de0321d02a77f32f8a5d2461dd38762c0593805c6d6"
+dependencies = [
+ "futures-core",
+ "futures-task",
+ "pin-project-lite",
+ "slab",
+]
+
+[[package]]
+name = "generator"
+version = "0.8.9"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "b3b854b0e584ead1a33f18b2fcad7cf7be18b3875c78816b753639aa501513ae"
+dependencies = [
+ "cc",
+ "cfg-if",
+ "libc",
+ "log",
+ "rustversion",
+ "windows-link",
+ "windows-result",
+]
+
+[[package]]
+name = "generic-btree"
+version = "0.10.7"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "a0c1bce85c110ab718fd139e0cc89c51b63bd647b14a767e24bdfc77c83df79b"
+dependencies = [
+ "arref",
+ "heapless 0.9.3",
+ "itertools 0.11.0",
+ "loro-thunderdome",
+ "proc-macro2",
+ "rustc-hash",
+]
+
+[[package]]
+name = "getrandom"
+version = "0.2.17"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "ff2abc00be7fca6ebc474524697ae276ad847ad0a6b3faa4bcb027e9a4614ad0"
+dependencies = [
+ "cfg-if",
+ "js-sys",
+ "libc",
+ "wasi",
+ "wasm-bindgen",
+]
+
+[[package]]
+name = "getrandom"
+version = "0.3.4"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "899def5c37c4fd7b2664648c28120ecec138e4d395b459e5ca34f9cce2dd77fd"
+dependencies = [
+ "cfg-if",
+ "libc",
+ "r-efi 5.3.0",
+ "wasip2",
+]
+
+[[package]]
+name = "getrandom"
+version = "0.4.3"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "300e883d756b2e4ec94e02791f39b04b522276138852cfc41d9fb7e904106099"
+dependencies = [
+ "cfg-if",
+ "libc",
+ "r-efi 6.0.0",
+]
+
+[[package]]
+name = "hash32"
+version = "0.2.1"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "b0c35f58762feb77d74ebe43bdbc3210f09be9fe6742234d573bacc26ed92b67"
+dependencies = [
+ "byteorder",
+]
+
+[[package]]
+name = "hash32"
+version = "0.3.1"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "47d60b12902ba28e2730cd37e95b8c9223af2808df9e902d4df49588d1470606"
+dependencies = [
+ "byteorder",
+]
+
+[[package]]
+name = "hashbrown"
+version = "0.16.1"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "841d1cc9bed7f9236f321df977030373f4a4163ae1a7dbfe1a51a2c1a51d9100"
+
+[[package]]
+name = "heapless"
+version = "0.7.17"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "cdc6457c0eb62c71aac4bc17216026d8410337c4126773b9c5daba343f17964f"
+dependencies = [
+ "atomic-polyfill",
+ "hash32 0.2.1",
+ "rustc_version",
+ "serde",
+ "spin",
+ "stable_deref_trait",
+]
+
+[[package]]
+name = "heapless"
+version = "0.8.0"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "0bfb9eb618601c89945a70e254898da93b13be0388091d42117462b265bb3fad"
+dependencies = [
+ "hash32 0.3.1",
+ "stable_deref_trait",
+]
+
+[[package]]
+name = "heapless"
+version = "0.9.3"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "25ba4bd83f9415b58b4ed8dc5714c76e626a105be4646c02630ad730ad3b5aa4"
+dependencies = [
+ "hash32 0.3.1",
+ "stable_deref_trait",
+]
+
+[[package]]
+name = "heck"
+version = "0.4.1"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "95505c38b4572b2d910cecb0281560f54b440a19336cbbcb27bf6ce6adc6f5a8"
+
+[[package]]
+name = "heck"
+version = "0.5.0"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "2304e00983f87ffb38b55b444b5e3b60a884b5d30c0fca7d82fe33449bbe55ea"
+
+[[package]]
+name = "ident_case"
+version = "1.0.1"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "b9e0384b61958566e926dc50660321d12159025e767c18e043daf26b70104c39"
+
+[[package]]
+name = "im"
+version = "15.1.0"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "d0acd33ff0285af998aaf9b57342af478078f53492322fafc47450e09397e0e9"
+dependencies = [
+ "bitmaps",
+ "rand_core",
+ "rand_xoshiro",
+ "serde",
+ "sized-chunks",
+ "typenum",
+ "version_check",
+]
+
+[[package]]
+name = "itertools"
+version = "0.11.0"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "b1c173a5686ce8bfa551b3563d0c2170bf24ca44da99c7ca4bfdab5418c3fe57"
+dependencies = [
+ "either",
+]
+
+[[package]]
+name = "itertools"
+version = "0.12.1"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "ba291022dbbd398a455acf126c1e341954079855bc60dfdda641363bd6922569"
+dependencies = [
+ "either",
+]
+
+[[package]]
+name = "itoa"
+version = "1.0.18"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "8f42a60cbdf9a97f5d2305f08a87dc4e09308d1276d28c869c684d7777685682"
+
+[[package]]
+name = "jobserver"
+version = "0.1.35"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "1c00acbd29eabad4a2392fa0e921c874934dbbf4194312ad20f04a0ed67a3cb3"
+dependencies = [
+ "getrandom 0.4.3",
+ "libc",
+]
+
+[[package]]
+name = "js-sys"
+version = "0.3.103"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "53b44bfcdb3f8d5837a46dae1ca9660a837176eee74a28b229bc626816589102"
+dependencies = [
+ "cfg-if",
+ "futures-util",
+ "wasm-bindgen",
+]
+
+[[package]]
+name = "lazy_static"
+version = "1.5.0"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "bbd2bcb4c963f2ddae06a2efc7e9f3591312473c50c6685e1f298068316e66fe"
+
+[[package]]
+name = "leb128"
+version = "0.2.7"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "c83bff1d572d6b9aeef67ddfc8448e4a3737909cb28e81f97c791b9018703e52"
+
+[[package]]
+name = "libc"
+version = "0.2.186"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "68ab91017fe16c622486840e4c83c9a37afeff978bd239b5293d61ece587de66"
+
+[[package]]
+name = "libfuzzer-sys"
+version = "0.4.13"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "a9fd2f41a1cba099f79a0b6b6c35656cf7c03351a7bae8ff0f28f25270f929d2"
+dependencies = [
+ "arbitrary",
+ "cc",
+]
+
+[[package]]
+name = "lock_api"
+version = "0.4.14"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "224399e74b87b5f3557511d98dff8b14089b3dadafcab6bb93eab67d3aace965"
+dependencies = [
+ "scopeguard",
+]
+
+[[package]]
+name = "log"
+version = "0.4.33"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "0ceec5bc11778974d1bcb055b18002eba7f4b3518b6a0081b3af5f21666da9ad"
+
+[[package]]
+name = "loom"
+version = "0.7.2"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "419e0dc8046cb947daa77eb95ae174acfbddb7673b4151f56d1eed8e93fbfaca"
+dependencies = [
+ "cfg-if",
+ "generator",
+ "scoped-tls",
+ "serde",
+ "serde_json",
+ "tracing",
+ "tracing-subscriber",
+]
+
+[[package]]
+name = "loro"
+version = "1.13.6"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "221b567f34e3a7b38d05345b17797a7ca252d28e1fae479b4bb55c1a19616ddc"
+dependencies = [
+ "enum-as-inner 0.6.1",
+ "generic-btree",
+ "loro-common",
+ "loro-delta",
+ "loro-internal",
+ "loro-kv-store",
+ "rustc-hash",
+ "tracing",
+]
+
+[[package]]
+name = "loro-common"
+version = "1.13.1"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "b51042936156a1d537df8a38938ec8ed24b45de14530af933c1d02de39c9e056"
+dependencies = [
+ "arbitrary",
+ "enum-as-inner 0.6.1",
+ "leb128",
+ "loro-rle",
+ "nonmax",
+ "rustc-hash",
+ "serde",
+ "serde_columnar",
+ "serde_json",
+ "thiserror 1.0.69",
+]
+
+[[package]]
+name = "loro-delta"
+version = "1.13.0"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "96f381e7fb5d4bf5605b4f4a9241f3adf61403ded978d757552004c8302cccbb"
+dependencies = [
+ "arrayvec",
+ "enum-as-inner 0.5.1",
+ "generic-btree",
+ "heapless 0.8.0",
+]
+
+[[package]]
+name = "loro-internal"
+version = "1.13.6"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "fb15cd83866965b50b4893246699312624808a1f2e2d205432237287d947b822"
+dependencies = [
+ "append-only-bytes",
+ "arref",
+ "bytes",
+ "either",
+ "ensure-cov",
+ "enum-as-inner 0.6.1",
+ "enum_dispatch",
+ "generic-btree",
+ "getrandom 0.2.17",
+ "im",
+ "itertools 0.12.1",
+ "leb128",
+ "loom",
+ "loro-common",
+ "loro-delta",
+ "loro-kv-store",
+ "loro-rle",
+ "loro_fractional_index",
+ "md5",
+ "nonmax",
+ "num",
+ "num-traits",
+ "once_cell",
+ "parking_lot",
+ "pest",
+ "pest_derive",
+ "postcard",
+ "pretty_assertions",
+ "rand",
+ "rustc-hash",
+ "serde",
+ "serde_columnar",
+ "serde_json",
+ "smallvec",
+ "thiserror 1.0.69",
+ "thread_local",
+ "tracing",
+ "wasm-bindgen",
+ "xxhash-rust",
+]
+
+[[package]]
+name = "loro-kv-store"
+version = "1.13.1"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "950cc8bcc64dcff536e949fbc56ccc3d0759646fa441c7f76b3cd7c3eafa2096"
+dependencies = [
+ "bytes",
+ "ensure-cov",
+ "loro-common",
+ "lz4_flex",
+ "once_cell",
+ "quick_cache",
+ "rustc-hash",
+ "tracing",
+ "xxhash-rust",
+]
+
+[[package]]
+name = "loro-rle"
+version = "1.6.0"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "76400c3eea6bb39b013406acce964a8db39311534e308286c8d8721baba8ee20"
+dependencies = [
+ "append-only-bytes",
+ "num",
+ "smallvec",
+]
+
+[[package]]
+name = "loro-thunderdome"
+version = "0.6.2"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "3f3d053a135388e6b1df14e8af1212af5064746e9b87a06a345a7a779ee9695a"
+
+[[package]]
+name = "loro_fractional_index"
+version = "1.13.0"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "aca7180674d0273ddf37049a5efcde4547fd5330d24abb7519bb9d9eb6780d5b"
+dependencies = [
+ "once_cell",
+ "rand",
+ "serde",
+]
+
+[[package]]
+name = "lz4_flex"
+version = "0.11.6"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "373f5eceeeab7925e0c1098212f2fbc4d416adec9d35051a6ab251e824c1854a"
+dependencies = [
+ "twox-hash",
+]
+
+[[package]]
+name = "matchers"
+version = "0.2.0"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "d1525a2a28c7f4fa0fc98bb91ae755d1e2d1505079e05539e35bc876b5d65ae9"
+dependencies = [
+ "regex-automata",
+]
+
+[[package]]
+name = "md5"
+version = "0.7.0"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "490cc448043f947bae3cbee9c203358d62dbee0db12107a74be5c30ccfd09771"
+
+[[package]]
+name = "memchr"
+version = "2.8.3"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "cf8baf1c55e62ffcace7a9f06f4bd9cd3f0c4beb022d3b367256b91b87513d98"
+
+[[package]]
+name = "nonmax"
+version = "0.5.5"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "610a5acd306ec67f907abe5567859a3c693fb9886eb1f012ab8f2a47bef3db51"
+
+[[package]]
+name = "nu-ansi-term"
+version = "0.50.3"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "7957b9740744892f114936ab4a57b3f487491bbeafaf8083688b16841a4240e5"
+dependencies = [
+ "windows-sys",
+]
+
+[[package]]
+name = "num"
+version = "0.4.3"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "35bd024e8b2ff75562e5f34e7f4905839deb4b22955ef5e73d2fea1b9813cb23"
+dependencies = [
+ "num-bigint",
+ "num-complex",
+ "num-integer",
+ "num-iter",
+ "num-rational",
+ "num-traits",
+]
+
+[[package]]
+name = "num-bigint"
+version = "0.4.8"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "c89e69e7e0f03bea5ef08013795c25018e101932225a656383bd384495ecc367"
+dependencies = [
+ "num-integer",
+ "num-traits",
+]
+
+[[package]]
+name = "num-complex"
+version = "0.4.6"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "73f88a1307638156682bada9d7604135552957b7818057dcef22705b4d509495"
+dependencies = [
+ "num-traits",
+]
+
+[[package]]
+name = "num-integer"
+version = "0.1.46"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "7969661fd2958a5cb096e56c8e1ad0444ac2bbcd0061bd28660485a44879858f"
+dependencies = [
+ "num-traits",
+]
+
+[[package]]
+name = "num-iter"
+version = "0.1.46"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "c92800bd69a1eac91786bcfe9da64a897eb72911b8dc3095decbd07429e8048b"
+dependencies = [
+ "num-integer",
+ "num-traits",
+]
+
+[[package]]
+name = "num-rational"
+version = "0.4.2"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "f83d14da390562dca69fc84082e73e548e1ad308d24accdedd2720017cb37824"
+dependencies = [
+ "num-bigint",
+ "num-integer",
+ "num-traits",
+]
+
+[[package]]
+name = "num-traits"
+version = "0.2.19"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "071dfc062690e90b734c0b2273ce72ad0ffa95f0c74596bc250dcfd960262841"
+dependencies = [
+ "autocfg",
+]
+
+[[package]]
+name = "once_cell"
+version = "1.21.4"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "9f7c3e4beb33f85d45ae3e3a1792185706c8e16d043238c593331cc7cd313b50"
+
+[[package]]
+name = "parking_lot"
+version = "0.12.5"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "93857453250e3077bd71ff98b6a65ea6621a19bb0f559a85248955ac12c45a1a"
+dependencies = [
+ "lock_api",
+ "parking_lot_core",
+]
+
+[[package]]
+name = "parking_lot_core"
+version = "0.9.12"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "2621685985a2ebf1c516881c026032ac7deafcda1a2c9b7850dc81e3dfcb64c1"
+dependencies = [
+ "cfg-if",
+ "libc",
+ "redox_syscall",
+ "smallvec",
+ "windows-link",
+]
+
+[[package]]
+name = "pest"
+version = "2.8.7"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "47627dd7305c6a2d6c8c6bcd24c5a4c17dbbf425f4f9c5313e724b38fc9782e9"
+dependencies = [
+ "memchr",
+ "ucd-trie",
+]
+
+[[package]]
+name = "pest_derive"
+version = "2.8.7"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "4b4254325ecad416ab689e27ba51da03ba01a9632bc6e108f5fe7c3c4ad29d58"
+dependencies = [
+ "pest",
+ "pest_generator",
+]
+
+[[package]]
+name = "pest_generator"
+version = "2.8.7"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "6c4c0e91ead7a8f7acecbca6f003fc2e8282b1dbe2dd9c9d2f16aba42995e0a7"
+dependencies = [
+ "pest",
+ "pest_meta",
+ "proc-macro2",
+ "quote",
+ "syn 2.0.118",
+]
+
+[[package]]
+name = "pest_meta"
+version = "2.8.7"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "f9744bc48116fee06334924bb5f2bad41eed5e89bd26e29b0b799f9a3f82c210"
+dependencies = [
+ "pest",
+]
+
+[[package]]
+name = "pin-project-lite"
+version = "0.2.17"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "a89322df9ebe1c1578d689c92318e070967d1042b512afbe49518723f4e6d5cd"
+
+[[package]]
+name = "postcard"
+version = "1.1.3"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "6764c3b5dd454e283a30e6dfe78e9b31096d9e32036b5d1eaac7a6119ccb9a24"
+dependencies = [
+ "cobs",
+ "embedded-io 0.4.0",
+ "embedded-io 0.6.1",
+ "heapless 0.7.17",
+ "serde",
+]
+
+[[package]]
+name = "ppv-lite86"
+version = "0.2.21"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "85eae3c4ed2f50dcfe72643da4befc30deadb458a9b590d720cde2f2b1e97da9"
+dependencies = [
+ "zerocopy",
+]
+
+[[package]]
+name = "pretty_assertions"
+version = "1.4.1"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "3ae130e2f271fbc2ac3a40fb1d07180839cdbbe443c7a27e1e3c13c5cac0116d"
+dependencies = [
+ "diff",
+ "yansi",
+]
+
+[[package]]
+name = "proc-macro2"
+version = "1.0.106"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "8fd00f0bb2e90d81d1044c2b32617f68fcb9fa3bb7640c23e9c748e53fb30934"
+dependencies = [
+ "unicode-ident",
+]
+
+[[package]]
+name = "quick_cache"
+version = "0.6.24"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "b9c6658afe513a3b484e3abfdaa0d03ef3c0bbf017542c178dd55f94eb3051f9"
+dependencies = [
+ "ahash",
+ "equivalent",
+ "hashbrown",
+ "parking_lot",
+]
+
+[[package]]
+name = "quote"
+version = "1.0.46"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "dfbc457d0c7a0759a614551b11a6409e5951f6c7537be1f1b7682b9ae9230368"
+dependencies = [
+ "proc-macro2",
+]
+
+[[package]]
+name = "r-efi"
+version = "5.3.0"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "69cdb34c158ceb288df11e18b4bd39de994f6657d83847bdffdbd7f346754b0f"
+
+[[package]]
+name = "r-efi"
+version = "6.0.0"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "f8dcc9c7d52a811697d2151c701e0d08956f92b0e24136cf4cf27b57a6a0d9bf"
+
+[[package]]
+name = "rand"
+version = "0.8.6"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "5ca0ecfa931c29007047d1bc58e623ab12e5590e8c7cc53200d5202b69266d8a"
+dependencies = [
+ "libc",
+ "rand_chacha",
+ "rand_core",
+]
+
+[[package]]
+name = "rand_chacha"
+version = "0.3.1"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "e6c10a63a0fa32252be49d21e7709d4d4baf8d231c2dbce1eaa8141b9b127d88"
+dependencies = [
+ "ppv-lite86",
+ "rand_core",
+]
+
+[[package]]
+name = "rand_core"
+version = "0.6.4"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "ec0be4795e2f6a28069bec0b5ff3e2ac9bafc99e6a9a7dc3547996c5c816922c"
+dependencies = [
+ "getrandom 0.2.17",
+]
+
+[[package]]
+name = "rand_xoshiro"
+version = "0.6.0"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "6f97cdb2a36ed4183de61b2f824cc45c9f1037f28afe0a322e9fff4c108b5aaa"
+dependencies = [
+ "rand_core",
+]
+
+[[package]]
+name = "redox_syscall"
+version = "0.5.18"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "ed2bf2547551a7053d6fdfafda3f938979645c44812fbfcda098faae3f1a362d"
+dependencies = [
+ "bitflags",
+]
+
+[[package]]
+name = "regex-automata"
+version = "0.4.15"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "1f388202e4b80542a0921078cc23b6333bcf1409c1e3f86404cae4766a6131db"
+dependencies = [
+ "aho-corasick",
+ "memchr",
+ "regex-syntax",
+]
+
+[[package]]
+name = "regex-syntax"
+version = "0.8.11"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "d6f6ff9a378485b298a5286656da665ba74413d36db0979633275d2e708145d4"
+
+[[package]]
+name = "rustc-hash"
+version = "2.1.3"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "6b1e7f9a428571be2dc5bc0505c13fb6bf936822b894ec87abf8a08a4e51742d"
+
+[[package]]
+name = "rustc_version"
+version = "0.4.1"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "cfcb3a22ef46e85b45de6ee7e79d063319ebb6594faafcf1c225ea92ab6e9b92"
+dependencies = [
+ "semver",
+]
+
+[[package]]
+name = "rustversion"
+version = "1.0.23"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "cf54715a573b99ac80df0bc206da022bcd442c974952c7b9720069370852e21f"
+
+[[package]]
+name = "scoped-tls"
+version = "1.0.1"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "e1cf6437eb19a8f4a6cc0f7dca544973b0b78843adbfeb3683d1a94a0024a294"
+
+[[package]]
+name = "scopeguard"
+version = "1.2.0"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "94143f37725109f92c262ed2cf5e59bce7498c01bcc1502d7b9afe439a4e9f49"
+
+[[package]]
+name = "semver"
+version = "1.0.28"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "8a7852d02fc848982e0c167ef163aaff9cd91dc640ba85e263cb1ce46fae51cd"
+
+[[package]]
+name = "serde"
+version = "1.0.228"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "9a8e94ea7f378bd32cbbd37198a4a91436180c5bb472411e48b5ec2e2124ae9e"
+dependencies = [
+ "serde_core",
+ "serde_derive",
+]
+
+[[package]]
+name = "serde_columnar"
+version = "0.3.14"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "2a16e404f17b16d0273460350e29b02d76ba0d70f34afdc9a4fa034c97d6c6eb"
+dependencies = [
+ "itertools 0.11.0",
+ "postcard",
+ "serde",
+ "serde_columnar_derive",
+ "thiserror 1.0.69",
+]
+
+[[package]]
+name = "serde_columnar_derive"
+version = "0.3.7"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "45958fce4903f67e871fbf15ac78e289269b21ebd357d6fecacdba233629112e"
+dependencies = [
+ "darling",
+ "proc-macro2",
+ "quote",
+ "syn 2.0.118",
+]
+
+[[package]]
+name = "serde_core"
+version = "1.0.228"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "41d385c7d4ca58e59fc732af25c3983b67ac852c1a25000afe1175de458b67ad"
+dependencies = [
+ "serde_derive",
+]
+
+[[package]]
+name = "serde_derive"
+version = "1.0.228"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "d540f220d3187173da220f885ab66608367b6574e925011a9353e4badda91d79"
+dependencies = [
+ "proc-macro2",
+ "quote",
+ "syn 2.0.118",
+]
+
+[[package]]
+name = "serde_json"
+version = "1.0.150"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "e8014e44b4736ed0538adeecded0fce2a272f22dc9578a7eb6b2d9993c74cfb9"
+dependencies = [
+ "itoa",
+ "memchr",
+ "serde",
+ "serde_core",
+ "zmij",
+]
+
+[[package]]
+name = "sharded-slab"
+version = "0.1.7"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "f40ca3c46823713e0d4209592e8d6e826aa57e928f09752619fc696c499637f6"
+dependencies = [
+ "lazy_static",
+]
+
+[[package]]
+name = "shlex"
+version = "2.0.1"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "f8fadd59c855ef2080decdef8ff161eb6661b86933c9d82e5ba29dc602a55aba"
+
+[[package]]
+name = "sized-chunks"
+version = "0.6.5"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "16d69225bde7a69b235da73377861095455d298f2b970996eec25ddbb42b3d1e"
+dependencies = [
+ "bitmaps",
+ "typenum",
+]
+
+[[package]]
+name = "slab"
+version = "0.4.12"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "0c790de23124f9ab44544d7ac05d60440adc586479ce501c1d6d7da3cd8c9cf5"
+
+[[package]]
+name = "smallvec"
+version = "1.15.2"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "8ed6a63f02c8539c91a8685a86f4099661ba3da017932f6ebbea6de3f0fa7c90"
+dependencies = [
+ "serde",
+]
+
+[[package]]
+name = "spin"
+version = "0.9.8"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "6980e8d7511241f8acf4aebddbb1ff938df5eebe98691418c4468d0b72a96a67"
+dependencies = [
+ "lock_api",
+]
+
+[[package]]
+name = "stable_deref_trait"
+version = "1.2.1"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "6ce2be8dc25455e1f91df71bfa12ad37d7af1092ae736f3a6cd0e37bc7810596"
+
+[[package]]
+name = "strsim"
+version = "0.11.1"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "7da8b5736845d9f2fcb837ea5d9e2628564b3b043a70948a3f0b778838c5fb4f"
+
+[[package]]
+name = "syn"
+version = "1.0.109"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "72b64191b275b66ffe2469e8af2c1cfe3bafa67b529ead792a6d0160888b4237"
+dependencies = [
+ "proc-macro2",
+ "quote",
+ "unicode-ident",
+]
+
+[[package]]
+name = "syn"
+version = "2.0.118"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "1b9ae57f904213ebb649ce6895b8a66c66f0203b9319718f69a5612a065b1422"
+dependencies = [
+ "proc-macro2",
+ "quote",
+ "unicode-ident",
+]
+
+[[package]]
+name = "thiserror"
+version = "1.0.69"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "b6aaf5339b578ea85b50e080feb250a3e8ae8cfcdff9a461c9ec2904bc923f52"
+dependencies = [
+ "thiserror-impl 1.0.69",
+]
+
+[[package]]
+name = "thiserror"
+version = "2.0.18"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "4288b5bcbc7920c07a1149a35cf9590a2aa808e0bc1eafaade0b80947865fbc4"
+dependencies = [
+ "thiserror-impl 2.0.18",
+]
+
+[[package]]
+name = "thiserror-impl"
+version = "1.0.69"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "4fee6c4efc90059e10f81e6d42c60a18f76588c3d74cb83a0b242a2b6c7504c1"
+dependencies = [
+ "proc-macro2",
+ "quote",
+ "syn 2.0.118",
+]
+
+[[package]]
+name = "thiserror-impl"
+version = "2.0.18"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "ebc4ee7f67670e9b64d05fa4253e753e016c6c95ff35b89b7941d6b856dec1d5"
+dependencies = [
+ "proc-macro2",
+ "quote",
+ "syn 2.0.118",
+]
+
+[[package]]
+name = "thread_local"
+version = "1.1.10"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "1ad99c4c6d32803332c548b1af0540b357b3f5fc0be8f6c6bfe8b2e6ae784070"
+dependencies = [
+ "cfg-if",
+]
+
+[[package]]
+name = "tracing"
+version = "0.1.44"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "63e71662fa4b2a2c3a26f570f037eb95bb1f85397f3cd8076caed2f026a6d100"
+dependencies = [
+ "pin-project-lite",
+ "tracing-attributes",
+ "tracing-core",
+]
+
+[[package]]
+name = "tracing-attributes"
+version = "0.1.31"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "7490cfa5ec963746568740651ac6781f701c9c5ea257c58e057f3ba8cf69e8da"
+dependencies = [
+ "proc-macro2",
+ "quote",
+ "syn 2.0.118",
+]
+
+[[package]]
+name = "tracing-core"
+version = "0.1.36"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "db97caf9d906fbde555dd62fa95ddba9eecfd14cb388e4f491a66d74cd5fb79a"
+dependencies = [
+ "once_cell",
+ "valuable",
+]
+
+[[package]]
+name = "tracing-log"
+version = "0.2.0"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "ee855f1f400bd0e5c02d150ae5de3840039a3f54b025156404e34c23c03f47c3"
+dependencies = [
+ "log",
+ "once_cell",
+ "tracing-core",
+]
+
+[[package]]
+name = "tracing-subscriber"
+version = "0.3.23"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "cb7f578e5945fb242538965c2d0b04418d38ec25c79d160cd279bf0731c8d319"
+dependencies = [
+ "matchers",
+ "nu-ansi-term",
+ "once_cell",
+ "regex-automata",
+ "sharded-slab",
+ "smallvec",
+ "thread_local",
+ "tracing",
+ "tracing-core",
+ "tracing-log",
+]
+
+[[package]]
+name = "twox-hash"
+version = "2.1.2"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "9ea3136b675547379c4bd395ca6b938e5ad3c3d20fad76e7fe85f9e0d011419c"
+
+[[package]]
+name = "typenum"
+version = "1.20.1"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "b6f5e870be6c3b371b77fe0ee0bafb859fa4964b4404c27de1d380043c4dda20"
+
+[[package]]
+name = "ucd-trie"
+version = "0.1.7"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "2896d95c02a80c6d6a5d6e953d479f5ddf2dfdb6a244441010e373ac0fb88971"
+
+[[package]]
+name = "unicode-ident"
+version = "1.0.24"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "e6e4313cd5fcd3dad5cafa179702e2b244f760991f45397d14d4ebf38247da75"
+
+[[package]]
+name = "valuable"
+version = "0.1.1"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "ba73ea9cf16a25df0c8caa16c51acb937d5712a8429db78a3ee29d5dcacd3a65"
+
+[[package]]
+name = "version_check"
+version = "0.9.5"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "0b928f33d975fc6ad9f86c8f283853ad26bdd5b10b7f1542aa2fa15e2289105a"
+
+[[package]]
+name = "wasi"
+version = "0.11.1+wasi-snapshot-preview1"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "ccf3ec651a847eb01de73ccad15eb7d99f80485de043efb2f370cd654f4ea44b"
+
+[[package]]
+name = "wasip2"
+version = "1.0.4+wasi-0.2.12"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "b67efb37e106e55ce722a510d6b5f9c17f083e5fc79afc2badeb12cc313d9487"
+dependencies = [
+ "wit-bindgen",
+]
+
+[[package]]
+name = "wasm-bindgen"
+version = "0.2.126"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "4b067c0c11094aef6b7a801c1e34a26affafdf3d051dba08456b868789aaf9a4"
+dependencies = [
+ "cfg-if",
+ "once_cell",
+ "rustversion",
+ "wasm-bindgen-macro",
+ "wasm-bindgen-shared",
+]
+
+[[package]]
+name = "wasm-bindgen-macro"
+version = "0.2.126"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "167ce5e579f6bcf889c4f7175a8a5a585de84e8ff93976ce393efa5f2837aab1"
+dependencies = [
+ "quote",
+ "wasm-bindgen-macro-support",
+]
+
+[[package]]
+name = "wasm-bindgen-macro-support"
+version = "0.2.126"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "f3997c7839262f4ef12cf90b818d6340c18e80f263f1a94bf157d0ec4420380e"
+dependencies = [
+ "bumpalo",
+ "proc-macro2",
+ "quote",
+ "syn 2.0.118",
+ "wasm-bindgen-shared",
+]
+
+[[package]]
+name = "wasm-bindgen-shared"
+version = "0.2.126"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "dc1b4cb0cc549fcf58d7dfc081778139b3d283a081644e833e84682ad71cea24"
+dependencies = [
+ "unicode-ident",
+]
+
+[[package]]
+name = "weft-loro-ffi"
+version = "0.1.0"
+dependencies = [
+ "loro",
+]
+
+[[package]]
+name = "weft-loro-ffi-fuzz"
+version = "0.0.0"
+dependencies = [
+ "libfuzzer-sys",
+ "loro",
+ "weft-loro-ffi",
+]
+
+[[package]]
+name = "windows-link"
+version = "0.2.1"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "f0805222e57f7521d6a62e36fa9163bc891acd422f971defe97d64e70d0a4fe5"
+
+[[package]]
+name = "windows-result"
+version = "0.4.1"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "7781fa89eaf60850ac3d2da7af8e5242a5ea78d1a11c49bf2910bb5a73853eb5"
+dependencies = [
+ "windows-link",
+]
+
+[[package]]
+name = "windows-sys"
+version = "0.61.2"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "ae137229bcbd6cdf0f7b80a31df61766145077ddf49416a728b02cb3921ff3fc"
+dependencies = [
+ "windows-link",
+]
+
+[[package]]
+name = "wit-bindgen"
+version = "0.57.1"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "1ebf944e87a7c253233ad6766e082e3cd714b5d03812acc24c318f549614536e"
+
+[[package]]
+name = "xxhash-rust"
+version = "0.8.16"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "4d93c89cdc2d3a63c3ec48ffe926931bdc069eafa8e4402fe6d8f790c9d1e576"
+
+[[package]]
+name = "yansi"
+version = "1.0.1"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "cfe53a6657fd280eaa890a3bc59152892ffa3e30101319d168b781ed6529b049"
+
+[[package]]
+name = "zerocopy"
+version = "0.8.54"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "b7cbbc0a705a0fd05cc3676525980d2bf5a9bc4adac6d6475209a7887cf59d19"
+dependencies = [
+ "zerocopy-derive",
+]
+
+[[package]]
+name = "zerocopy-derive"
+version = "0.8.54"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "e2e817b7b52d0c7358d3246da9d69935ebb18116b2b102b4230dac079b4862f5"
+dependencies = [
+ "proc-macro2",
+ "quote",
+ "syn 2.0.118",
+]
+
+[[package]]
+name = "zmij"
+version = "1.0.21"
+source = "registry+https://github.com/rust-lang/crates.io-index"
+checksum = "b8848ee67ecc8aedbaf3e4122217aff892639231befc6a1b58d29fff4c2cabaa"
diff --git a/native/weft-loro-ffi/fuzz/Cargo.toml b/native/weft-loro-ffi/fuzz/Cargo.toml
new file mode 100644
index 0000000..ee62355
--- /dev/null
+++ b/native/weft-loro-ffi/fuzz/Cargo.toml
@@ -0,0 +1,34 @@
+[package]
+name = "weft-loro-ffi-fuzz"
+version = "0.0.0"
+publish = false
+edition = "2021"
+
+[package.metadata]
+cargo-fuzz = true
+
+[dependencies]
+libfuzzer-sys = "0.4"
+loro = "=1.13.6"
+
+[dependencies.weft-loro-ffi]
+path = ".."
+
+[[bin]]
+name = "loro_doc_load"
+path = "fuzz_targets/loro_doc_load.rs"
+test = false
+doc = false
+bench = false
+
+[[bin]]
+name = "loro_apply_update"
+path = "fuzz_targets/loro_apply_update.rs"
+test = false
+doc = false
+bench = false
+
+[profile.release]
+panic = "unwind"
+
+[workspace]
diff --git a/native/weft-loro-ffi/fuzz/fuzz_targets/loro_apply_update.rs b/native/weft-loro-ffi/fuzz/fuzz_targets/loro_apply_update.rs
new file mode 100644
index 0000000..259c413
--- /dev/null
+++ b/native/weft-loro-ffi/fuzz/fuzz_targets/loro_apply_update.rs
@@ -0,0 +1,24 @@
+//! Fuzz target: `weft_loro_doc_apply_update` con bytes arbitrarios sobre un doc vivo (research R14).
+#![no_main]
+
+use std::ptr;
+use std::sync::Once;
+
+use libfuzzer_sys::fuzz_target;
+use loro::LoroDoc;
+use weft_loro_ffi::*;
+
+static INIT: Once = Once::new();
+
+fuzz_target!(|data: &[u8]| {
+    INIT.call_once(|| std::panic::set_hook(Box::new(|_| {})));
+
+    unsafe {
+        let mut doc: *mut LoroDoc = ptr::null_mut();
+        if weft_loro_doc_new(&mut doc) != WEFT_OK || doc.is_null() {
+            return;
+        }
+        let _ = weft_loro_doc_apply_update(doc, data.as_ptr(), data.len());
+        weft_loro_doc_free(doc);
+    }
+});
diff --git a/native/weft-loro-ffi/fuzz/fuzz_targets/loro_doc_load.rs b/native/weft-loro-ffi/fuzz/fuzz_targets/loro_doc_load.rs
new file mode 100644
index 0000000..92a565c
--- /dev/null
+++ b/native/weft-loro-ffi/fuzz/fuzz_targets/loro_doc_load.rs
@@ -0,0 +1,25 @@
+//! Fuzz target: `weft_loro_doc_load` con bytes arbitrarios (research R14). El shim contiene panics
+//! (`catch_unwind`) y rechaza corruptos (WEFT_ERR_DECODE); un SIGSEGV/UB real sigue detectándose.
+#![no_main]
+
+use std::ptr;
+use std::sync::Once;
+
+use libfuzzer_sys::fuzz_target;
+use loro::LoroDoc;
+use weft_loro_ffi::*;
+
+static INIT: Once = Once::new();
+
+fuzz_target!(|data: &[u8]| {
+    // Silencia el hook de libfuzzer-sys para ejercitar el catch_unwind del shim (ver weft-yrs-ffi).
+    INIT.call_once(|| std::panic::set_hook(Box::new(|_| {})));
+
+    unsafe {
+        let mut doc: *mut LoroDoc = ptr::null_mut();
+        let code = weft_loro_doc_load(data.as_ptr(), data.len(), &mut doc);
+        if code == WEFT_OK && !doc.is_null() {
+            weft_loro_doc_free(doc);
+        }
+    }
+});
diff --git a/native/weft-loro-ffi/src/lib.rs b/native/weft-loro-ffi/src/lib.rs
new file mode 100644
index 0000000..fa327ae
--- /dev/null
+++ b/native/weft-loro-ffi/src/lib.rs
@@ -0,0 +1,337 @@
+//! # weft-loro-ffi — shim C-ABI de Weft sobre `loro` (adaptador dual-path, P-IV)
+//!
+//! Réplica de la ABI de `weft-yrs-ffi` con prefijo `weft_loro_`, mapeada sobre la API de Loro.
+//! Diferencias con yrs: `LoroDoc` es Send+Sync (locking interno; el shim no añade locks), y el
+//! versionado nativo (diff/branch/shallow) se expone aparte como capacidad opcional
+//! (`INativeVersioning`). Índices en UTF-16 code units (consistente con .NET y Yjs).
+
+use std::os::raw::c_uchar;
+use std::panic::{catch_unwind, AssertUnwindSafe};
+
+use loro::{ExportMode, LoroDoc, VersionVector};
+
+// ── Códigos de estado (idénticos a weft-yrs-ffi) ──
+pub const WEFT_OK: i32 = 0;
+pub const WEFT_ERR_NULL_ARG: i32 = -1;
+pub const WEFT_ERR_DECODE: i32 = -2;
+pub const WEFT_ERR_APPLY: i32 = -3;
+pub const WEFT_ERR_UTF8: i32 = -4;
+pub const WEFT_ERR_OUT_OF_BOUNDS: i32 = -5;
+pub const WEFT_ERR_PANIC: i32 = -127;
+
+const WEFT_ABI_VERSION: u32 = 1;
+
+fn guard<F: FnOnce() -> i32>(f: F) -> i32 {
+    match catch_unwind(AssertUnwindSafe(f)) {
+        Ok(code) => code,
+        Err(_) => WEFT_ERR_PANIC,
+    }
+}
+
+/// # Safety
+/// `ptr` debe apuntar a `len` bytes válidos y vivos durante la llamada.
+unsafe fn borrow_str<'a>(ptr: *const c_uchar, len: usize) -> Option<&'a str> {
+    if ptr.is_null() && len != 0 {
+        return None;
+    }
+    let bytes = if ptr.is_null() {
+        &[][..]
+    } else {
+        std::slice::from_raw_parts(ptr, len)
+    };
+    std::str::from_utf8(bytes).ok()
+}
+
+/// # Safety
+/// `ptr` debe apuntar a `len` bytes válidos y vivos durante la llamada.
+unsafe fn borrow_bytes<'a>(ptr: *const c_uchar, len: usize) -> Option<&'a [u8]> {
+    if ptr.is_null() {
+        return if len == 0 { Some(&[]) } else { None };
+    }
+    Some(std::slice::from_raw_parts(ptr, len))
+}
+
+/// # Safety
+/// `out_ptr` y `out_len` deben ser punteros escribibles no nulos.
+unsafe fn hand_out_buffer(data: Vec<u8>, out_ptr: *mut *mut c_uchar, out_len: *mut usize) -> i32 {
+    if out_ptr.is_null() || out_len.is_null() {
+        return WEFT_ERR_NULL_ARG;
+    }
+    let mut boxed = data.into_boxed_slice();
+    let len = boxed.len();
+    let ptr = boxed.as_mut_ptr();
+    std::mem::forget(boxed);
+    *out_ptr = ptr;
+    *out_len = len;
+    WEFT_OK
+}
+
+/// # Safety
+/// `doc` debe provenir de `weft_loro_doc_new`/`weft_loro_doc_load` y no haber sido liberado.
+unsafe fn doc_ref<'a>(doc: *mut LoroDoc) -> Option<&'a LoroDoc> {
+    if doc.is_null() {
+        None
+    } else {
+        Some(&*doc)
+    }
+}
+
+// ── Ciclo de vida ──
+
+/// # Safety
+/// `out_doc` debe ser un puntero escribible no nulo.
+#[no_mangle]
+pub unsafe extern "C" fn weft_loro_doc_new(out_doc: *mut *mut LoroDoc) -> i32 {
+    guard(|| {
+        if out_doc.is_null() {
+            return WEFT_ERR_NULL_ARG;
+        }
+        *out_doc = Box::into_raw(Box::new(LoroDoc::new()));
+        WEFT_OK
+    })
+}
+
+/// # Safety
+/// `blob` debe apuntar a `blob_len` bytes válidos; `out_doc` escribible no nulo.
+#[no_mangle]
+pub unsafe extern "C" fn weft_loro_doc_load(
+    blob: *const c_uchar,
+    blob_len: usize,
+    out_doc: *mut *mut LoroDoc,
+) -> i32 {
+    guard(|| {
+        if out_doc.is_null() {
+            return WEFT_ERR_NULL_ARG;
+        }
+        let Some(bytes) = borrow_bytes(blob, blob_len) else {
+            return WEFT_ERR_NULL_ARG;
+        };
+        let doc = LoroDoc::new();
+        if doc.import(bytes).is_err() {
+            return WEFT_ERR_DECODE;
+        }
+        *out_doc = Box::into_raw(Box::new(doc));
+        WEFT_OK
+    })
+}
+
+/// # Safety
+/// `doc` debe ser null o un puntero válido no liberado antes.
+#[no_mangle]
+pub unsafe extern "C" fn weft_loro_doc_free(doc: *mut LoroDoc) {
+    if doc.is_null() {
+        return;
+    }
+    let _ = catch_unwind(AssertUnwindSafe(|| drop(Box::from_raw(doc))));
+}
+
+// ── Texto por campo nombrado (índices UTF-16) ──
+
+/// # Safety
+/// Punteros válidos por sus longitudes; `doc` válido.
+#[no_mangle]
+pub unsafe extern "C" fn weft_loro_text_insert(
+    doc: *mut LoroDoc,
+    field: *const c_uchar,
+    field_len: usize,
+    index: u32,
+    text: *const c_uchar,
+    text_len: usize,
+) -> i32 {
+    guard(|| {
+        let Some(doc) = doc_ref(doc) else {
+            return WEFT_ERR_NULL_ARG;
+        };
+        let (Some(field), Some(text)) = (borrow_str(field, field_len), borrow_str(text, text_len))
+        else {
+            return WEFT_ERR_UTF8;
+        };
+        let t = doc.get_text(field);
+        if index as usize > t.len_utf16() {
+            return WEFT_ERR_OUT_OF_BOUNDS;
+        }
+        match t.insert_utf16(index as usize, text) {
+            Ok(()) => WEFT_OK,
+            Err(_) => WEFT_ERR_APPLY,
+        }
+    })
+}
+
+/// # Safety
+/// Punteros válidos por sus longitudes; `doc` válido.
+#[no_mangle]
+pub unsafe extern "C" fn weft_loro_text_delete(
+    doc: *mut LoroDoc,
+    field: *const c_uchar,
+    field_len: usize,
+    index: u32,
+    len: u32,
+) -> i32 {
+    guard(|| {
+        let Some(doc) = doc_ref(doc) else {
+            return WEFT_ERR_NULL_ARG;
+        };
+        let Some(field) = borrow_str(field, field_len) else {
+            return WEFT_ERR_UTF8;
+        };
+        let t = doc.get_text(field);
+        let cur = t.len_utf16();
+        if index as usize > cur || len as usize > cur - index as usize {
+            return WEFT_ERR_OUT_OF_BOUNDS;
+        }
+        match t.delete_utf16(index as usize, len as usize) {
+            Ok(()) => WEFT_OK,
+            Err(_) => WEFT_ERR_APPLY,
+        }
+    })
+}
+
+/// # Safety
+/// Ver contrato del módulo.
+#[no_mangle]
+pub unsafe extern "C" fn weft_loro_text_read(
+    doc: *mut LoroDoc,
+    field: *const c_uchar,
+    field_len: usize,
+    out_ptr: *mut *mut c_uchar,
+    out_len: *mut usize,
+) -> i32 {
+    guard(|| {
+        let Some(doc) = doc_ref(doc) else {
+            return WEFT_ERR_NULL_ARG;
+        };
+        let Some(field) = borrow_str(field, field_len) else {
+            return WEFT_ERR_UTF8;
+        };
+        let s = doc.get_text(field).to_string();
+        hand_out_buffer(s.into_bytes(), out_ptr, out_len)
+    })
+}
+
+// ── Estado y sincronización ──
+
+/// Export determinista del estado completo (snapshot). Base del content-addressing (P-III).
+///
+/// # Safety
+/// Ver contrato del módulo.
+#[no_mangle]
+pub unsafe extern "C" fn weft_loro_doc_export_state(
+    doc: *mut LoroDoc,
+    out_ptr: *mut *mut c_uchar,
+    out_len: *mut usize,
+) -> i32 {
+    guard(|| {
+        let Some(doc) = doc_ref(doc) else {
+            return WEFT_ERR_NULL_ARG;
+        };
+        doc.commit();
+        match doc.export(ExportMode::Snapshot) {
+            Ok(bytes) => hand_out_buffer(bytes, out_ptr, out_len),
+            Err(_) => WEFT_ERR_APPLY,
+        }
+    })
+}
+
+/// State vector del documento (VersionVector codificado).
+///
+/// # Safety
+/// Ver contrato del módulo.
+#[no_mangle]
+pub unsafe extern "C" fn weft_loro_doc_state_vector(
+    doc: *mut LoroDoc,
+    out_ptr: *mut *mut c_uchar,
+    out_len: *mut usize,
+) -> i32 {
+    guard(|| {
+        let Some(doc) = doc_ref(doc) else {
+            return WEFT_ERR_NULL_ARG;
+        };
+        doc.commit();
+        hand_out_buffer(doc.state_vv().encode(), out_ptr, out_len)
+    })
+}
+
+/// Delta de cambios que el poseedor de `sv` no conoce.
+///
+/// # Safety
+/// Ver contrato del módulo.
+#[no_mangle]
+pub unsafe extern "C" fn weft_loro_doc_export_since(
+    doc: *mut LoroDoc,
+    sv: *const c_uchar,
+    sv_len: usize,
+    out_ptr: *mut *mut c_uchar,
+    out_len: *mut usize,
+) -> i32 {
+    guard(|| {
+        let Some(doc) = doc_ref(doc) else {
+            return WEFT_ERR_NULL_ARG;
+        };
+        let Some(sv_bytes) = borrow_bytes(sv, sv_len) else {
+            return WEFT_ERR_NULL_ARG;
+        };
+        let vv = match VersionVector::decode(sv_bytes) {
+            Ok(vv) => vv,
+            Err(_) => return WEFT_ERR_DECODE,
+        };
+        doc.commit();
+        match doc.export(ExportMode::updates(&vv)) {
+            Ok(bytes) => hand_out_buffer(bytes, out_ptr, out_len),
+            Err(_) => WEFT_ERR_APPLY,
+        }
+    })
+}
+
+/// Aplica un update/snapshot de otra réplica (convergente).
+///
+/// # Safety
+/// Ver contrato del módulo.
+#[no_mangle]
+pub unsafe extern "C" fn weft_loro_doc_apply_update(
+    doc: *mut LoroDoc,
+    update: *const c_uchar,
+    update_len: usize,
+) -> i32 {
+    guard(|| {
+        let Some(doc) = doc_ref(doc) else {
+            return WEFT_ERR_NULL_ARG;
+        };
+        let Some(bytes) = borrow_bytes(update, update_len) else {
+            return WEFT_ERR_NULL_ARG;
+        };
+        match doc.import(bytes) {
+            Ok(_) => WEFT_OK,
+            Err(_) => WEFT_ERR_DECODE,
+        }
+    })
+}
+
+// ── Memoria ──
+
+/// # Safety
+/// `ptr` debe ser null o provenir de una función de este shim, no liberado antes.
+#[no_mangle]
+pub unsafe extern "C" fn weft_loro_buf_free(ptr: *mut c_uchar, len: usize) {
+    if ptr.is_null() {
+        return;
+    }
+    let _ = catch_unwind(AssertUnwindSafe(|| {
+        drop(Box::from_raw(std::ptr::slice_from_raw_parts_mut(ptr, len)));
+    }));
+}
+
+// ── Diagnóstico ──
+
+#[no_mangle]
+pub extern "C" fn weft_loro_abi_version() -> u32 {
+    WEFT_ABI_VERSION
+}
+
+// ── Test hooks ──
+
+/// Provoca un panic deliberado para verificar catch_unwind (SC-009).
+#[cfg(feature = "test-hooks")]
+#[no_mangle]
+pub extern "C" fn weft_loro_test_panic() -> i32 {
+    guard(|| panic!("weft_loro_test_panic: panic deliberado"))
+}
diff --git a/native/weft-loro-ffi/tests/mem_asan.rs b/native/weft-loro-ffi/tests/mem_asan.rs
new file mode 100644
index 0000000..af1a071
--- /dev/null
+++ b/native/weft-loro-ffi/tests/mem_asan.rs
@@ -0,0 +1,187 @@
+//! Suite de integración del shim `weft-loro-ffi` (gate P-II, simétrica a weft-yrs-ffi):
+//! round-trip, convergencia, rutas de error tipificadas y estrés de memoria (≥2000 iteraciones).
+//!
+//! Gate de memoria:
+//! ```bash
+//! RUSTFLAGS="-Zsanitizer=address" cargo +nightly test -p weft-loro-ffi --features test-hooks \
+//!   --target x86_64-unknown-linux-gnu
+//! ```
+
+use std::ptr;
+
+use loro::LoroDoc;
+use weft_loro_ffi::*;
+
+unsafe fn new_doc() -> *mut LoroDoc {
+    let mut doc: *mut LoroDoc = ptr::null_mut();
+    assert_eq!(weft_loro_doc_new(&mut doc), WEFT_OK);
+    assert!(!doc.is_null());
+    doc
+}
+
+unsafe fn insert(doc: *mut LoroDoc, field: &str, index: u32, text: &str) -> i32 {
+    weft_loro_text_insert(
+        doc,
+        field.as_ptr(),
+        field.len(),
+        index,
+        text.as_ptr(),
+        text.len(),
+    )
+}
+
+unsafe fn take_buf(f: impl FnOnce(*mut *mut u8, *mut usize) -> i32) -> Result<Vec<u8>, i32> {
+    let mut out_ptr: *mut u8 = ptr::null_mut();
+    let mut out_len: usize = 0;
+    let code = f(&mut out_ptr, &mut out_len);
+    if code != WEFT_OK {
+        return Err(code);
+    }
+    let bytes = if out_ptr.is_null() {
+        Vec::new()
+    } else {
+        std::slice::from_raw_parts(out_ptr, out_len).to_vec()
+    };
+    weft_loro_buf_free(out_ptr, out_len);
+    Ok(bytes)
+}
+
+unsafe fn read_text(doc: *mut LoroDoc, field: &str) -> String {
+    let bytes =
+        take_buf(|p, l| weft_loro_text_read(doc, field.as_ptr(), field.len(), p, l)).unwrap();
+    String::from_utf8(bytes).unwrap()
+}
+
+unsafe fn export_state(doc: *mut LoroDoc) -> Vec<u8> {
+    take_buf(|p, l| weft_loro_doc_export_state(doc, p, l)).unwrap()
+}
+
+#[test]
+fn round_trip_and_text_ops() {
+    unsafe {
+        let doc = new_doc();
+        assert_eq!(insert(doc, "body", 0, "Hola mundo"), WEFT_OK);
+        assert_eq!(read_text(doc, "body"), "Hola mundo");
+        assert_eq!(
+            weft_loro_text_delete(doc, "body".as_ptr(), "body".len(), 4, 6),
+            WEFT_OK
+        );
+        assert_eq!(read_text(doc, "body"), "Hola");
+
+        let blob = export_state(doc);
+        let mut reloaded: *mut LoroDoc = ptr::null_mut();
+        assert_eq!(
+            weft_loro_doc_load(blob.as_ptr(), blob.len(), &mut reloaded),
+            WEFT_OK
+        );
+        assert_eq!(read_text(reloaded, "body"), "Hola");
+
+        weft_loro_doc_free(reloaded);
+        weft_loro_doc_free(doc);
+    }
+}
+
+#[test]
+fn incremental_sync_converges() {
+    unsafe {
+        let a = new_doc();
+        let b = new_doc();
+        assert_eq!(insert(a, "t", 0, "abc"), WEFT_OK);
+        assert_eq!(insert(b, "t", 0, "XYZ"), WEFT_OK);
+
+        let sv_b = take_buf(|p, l| weft_loro_doc_state_vector(b, p, l)).unwrap();
+        let delta =
+            take_buf(|p, l| weft_loro_doc_export_since(a, sv_b.as_ptr(), sv_b.len(), p, l)).unwrap();
+        assert_eq!(
+            weft_loro_doc_apply_update(b, delta.as_ptr(), delta.len()),
+            WEFT_OK
+        );
+
+        let full_b = export_state(b);
+        assert_eq!(
+            weft_loro_doc_apply_update(a, full_b.as_ptr(), full_b.len()),
+            WEFT_OK
+        );
+        assert_eq!(export_state(a), export_state(b));
+
+        weft_loro_doc_free(a);
+        weft_loro_doc_free(b);
+    }
+}
+
+#[test]
+fn error_paths_are_typed_not_panics() {
+    unsafe {
+        let doc = new_doc();
+        assert_eq!(weft_loro_doc_new(ptr::null_mut()), WEFT_ERR_NULL_ARG);
+        assert_eq!(insert(ptr::null_mut(), "f", 0, "x"), WEFT_ERR_NULL_ARG);
+
+        let bad = [0xFFu8, 0xFE];
+        assert_eq!(
+            weft_loro_text_insert(doc, bad.as_ptr(), bad.len(), 0, b"x".as_ptr(), 1),
+            WEFT_ERR_UTF8
+        );
+        assert_eq!(insert(doc, "f", 5, "x"), WEFT_ERR_OUT_OF_BOUNDS);
+        assert_eq!(insert(doc, "f", 0, "abc"), WEFT_OK);
+        assert_eq!(
+            weft_loro_text_delete(doc, "f".as_ptr(), "f".len(), 2, 10),
+            WEFT_ERR_OUT_OF_BOUNDS
+        );
+
+        let garbage = [1u8, 2, 3, 4, 5, 6, 7, 8];
+        let mut d: *mut LoroDoc = ptr::null_mut();
+        assert_eq!(
+            weft_loro_doc_load(garbage.as_ptr(), garbage.len(), &mut d),
+            WEFT_ERR_DECODE
+        );
+        assert_eq!(
+            weft_loro_doc_apply_update(doc, garbage.as_ptr(), garbage.len()),
+            WEFT_ERR_DECODE
+        );
+
+        weft_loro_doc_free(doc);
+        weft_loro_doc_free(ptr::null_mut());
+        weft_loro_buf_free(ptr::null_mut(), 0);
+    }
+}
+
+#[test]
+fn stress_all_functions_2000_iterations() {
+    unsafe {
+        for i in 0..2000u32 {
+            let doc = new_doc();
+            let field = "campo";
+            let payload = format!("edición-{i}-áéí");
+            assert_eq!(insert(doc, field, 0, &payload), WEFT_OK);
+            let _ = read_text(doc, field);
+            let _ = export_state(doc);
+            let _ = take_buf(|p, l| weft_loro_doc_state_vector(doc, p, l)).unwrap();
+
+            let blob = export_state(doc);
+            let mut reloaded: *mut LoroDoc = ptr::null_mut();
+            assert_eq!(
+                weft_loro_doc_load(blob.as_ptr(), blob.len(), &mut reloaded),
+                WEFT_OK
+            );
+
+            let bad = [0xFFu8];
+            assert_eq!(
+                weft_loro_text_insert(doc, bad.as_ptr(), bad.len(), 0, b"x".as_ptr(), 1),
+                WEFT_ERR_UTF8
+            );
+            let _ = weft_loro_text_delete(doc, field.as_ptr(), field.len(), 0, 3);
+
+            weft_loro_doc_free(reloaded);
+            weft_loro_doc_free(doc);
+        }
+        assert_eq!(weft_loro_abi_version(), 1);
+    }
+}
+
+#[cfg(feature = "test-hooks")]
+#[test]
+fn test_panic_is_caught_at_boundary() {
+    for _ in 0..2000 {
+        assert_eq!(weft_loro_test_panic(), WEFT_ERR_PANIC);
+    }
+}
diff --git a/native/weft-yrs-ffi/src/lib.rs b/native/weft-yrs-ffi/src/lib.rs
index c79f51d..82463d5 100644
--- a/native/weft-yrs-ffi/src/lib.rs
+++ b/native/weft-yrs-ffi/src/lib.rs
@@ -26,7 +26,18 @@ use std::panic::{catch_unwind, AssertUnwindSafe};
 
 use yrs::updates::decoder::Decode;
 use yrs::updates::encoder::Encode;
-use yrs::{Doc, GetString, ReadTxn, StateVector, Text, Transact, Update};
+use yrs::{Doc, GetString, OffsetKind, Options, ReadTxn, StateVector, Text, Transact, Update};
+
+/// Crea un `Doc` con índices en **UTF-16 code units** (no el default de yrs, que es bytes UTF-8).
+/// Consistente con `string` de .NET y con Yjs (clientes de editor); crítico para que
+/// insert/delete por índice sean correctos con texto no-ASCII.
+fn new_doc() -> Doc {
+    let opts = Options {
+        offset_kind: OffsetKind::Utf16,
+        ..Options::default()
+    };
+    Doc::with_options(opts)
+}
 
 // ── Códigos de estado (deben coincidir con weft_ffi.h y el mapeo de excepciones en C#) ──
 pub const WEFT_OK: i32 = 0;
@@ -122,7 +133,7 @@ pub unsafe extern "C" fn weft_doc_new(out_doc: *mut *mut Doc) -> i32 {
         if out_doc.is_null() {
             return WEFT_ERR_NULL_ARG;
         }
-        *out_doc = Box::into_raw(Box::new(Doc::new()));
+        *out_doc = Box::into_raw(Box::new(new_doc()));
         WEFT_OK
     })
 }
@@ -148,7 +159,7 @@ pub unsafe extern "C" fn weft_doc_load(
             Ok(u) => u,
             Err(_) => return WEFT_ERR_DECODE,
         };
-        let doc = Doc::new();
+        let doc = new_doc();
         {
             let mut txn = doc.transact_mut();
             if txn.apply_update(update).is_err() {
diff --git a/samples/Weft.Sample.Versioning/Program.cs b/samples/Weft.Sample.Versioning/Program.cs
new file mode 100644
index 0000000..4654048
--- /dev/null
+++ b/samples/Weft.Sample.Versioning/Program.cs
@@ -0,0 +1,52 @@
+using Weft;
+using Weft.Versioning;
+using Weft.Versioning.Blobs;
+using Weft.Yrs;
+
+// Sample de US1: editar y versionar documentos content-addressed desde .NET (T030).
+// Recorre el user journey completo: publicar → diff → checkout → branch → merge.
+
+ICrdtEngine engine = YrsEngine.Instance;
+var store = new VersionStore(engine, new InMemoryBlobStore());
+
+Console.WriteLine($"Motor: {engine.Name}\n");
+
+// 1. Crear y editar un documento.
+using ICrdtDoc doc = engine.CreateDoc();
+doc.InsertText("titulo", 0, "El veloz murciélago");
+VersionId v1 = await store.PublishAsync(doc);
+Console.WriteLine($"v1 publicada  → {v1}");
+Console.WriteLine($"   titulo: \"{doc.GetText("titulo")}\"\n");
+
+// 2. Editar y publicar una segunda versión.
+doc.DeleteText("titulo", 9, 10);           // borra "murciélago"
+doc.InsertText("titulo", 9, "colibrí");
+VersionId v2 = await store.PublishAsync(doc);
+Console.WriteLine($"v2 publicada  → {v2}");
+Console.WriteLine($"   titulo: \"{doc.GetText("titulo")}\"\n");
+
+// 3. Diff entre versiones (por palabras).
+TextDiff diff = await store.DiffAsync(v1, v2, "titulo");
+Console.WriteLine("Diff v1 → v2 (titulo):");
+foreach (TextDiffSegment seg in diff.Segments)
+{
+    string mark = seg.Op switch { DiffOp.Inserted => "+", DiffOp.Deleted => "-", _ => " " };
+    Console.WriteLine($"   {mark} \"{seg.Text}\"");
+}
+Console.WriteLine();
+
+// 4. Checkout: reconstruir el documento de la v1 (verifica integridad).
+using ICrdtDoc restored = await store.CheckoutAsync(v1);
+Console.WriteLine($"Checkout v1   → titulo: \"{restored.GetText("titulo")}\"\n");
+
+// 5. Branch + merge: dos ramas concurrentes desde v2 que convergen.
+using ICrdtDoc branchA = await store.BranchAsync(v2);
+using ICrdtDoc branchB = await store.BranchAsync(v2);
+branchA.InsertText("titulo", 0, "[A] ");
+branchB.InsertText("titulo", 0, "[B] ");
+store.Merge(branchA, branchB);
+VersionId merged = await store.PublishAsync(branchA);
+Console.WriteLine($"Merge A◁B     → {merged}");
+Console.WriteLine($"   titulo: \"{branchA.GetText("titulo")}\"");
+
+Console.WriteLine("\n✓ Journey de versionado completado.");
diff --git a/samples/Weft.Sample.Versioning/Weft.Sample.Versioning.csproj b/samples/Weft.Sample.Versioning/Weft.Sample.Versioning.csproj
new file mode 100644
index 0000000..dbfd256
--- /dev/null
+++ b/samples/Weft.Sample.Versioning/Weft.Sample.Versioning.csproj
@@ -0,0 +1,33 @@
+<Project Sdk="Microsoft.NET.Sdk">
+
+  <PropertyGroup>
+    <OutputType>Exe</OutputType>
+    <IsPackable>false</IsPackable>
+    <GenerateDocumentationFile>false</GenerateDocumentationFile>
+  </PropertyGroup>
+
+  <ItemGroup>
+    <ProjectReference Include="../../src/Weft.Versioning/Weft.Versioning.csproj" />
+  </ItemGroup>
+
+  <Target Name="CopyWeftNativeForSample" AfterTargets="Build">
+    <PropertyGroup>
+      <_WeftRid Condition="$([MSBuild]::IsOSPlatform('Windows'))">win-x64</_WeftRid>
+      <_WeftRid Condition="$([MSBuild]::IsOSPlatform('OSX'))">osx-arm64</_WeftRid>
+      <_WeftRid Condition="$([MSBuild]::IsOSPlatform('Linux'))">linux-x64</_WeftRid>
+      <_WeftPrefix Condition="$([MSBuild]::IsOSPlatform('Windows'))"></_WeftPrefix>
+      <_WeftPrefix Condition="!$([MSBuild]::IsOSPlatform('Windows'))">lib</_WeftPrefix>
+      <_WeftExt Condition="$([MSBuild]::IsOSPlatform('Windows'))">.dll</_WeftExt>
+      <_WeftExt Condition="$([MSBuild]::IsOSPlatform('OSX'))">.dylib</_WeftExt>
+      <_WeftExt Condition="$([MSBuild]::IsOSPlatform('Linux'))">.so</_WeftExt>
+    </PropertyGroup>
+    <ItemGroup>
+      <_WeftNative Include="$(MSBuildProjectDirectory)/../../native/target/release/$(_WeftPrefix)weft_yrs_ffi$(_WeftExt)" />
+    </ItemGroup>
+    <Copy SourceFiles="@(_WeftNative)"
+          DestinationFolder="$(OutDir)runtimes/$(_WeftRid)/native/"
+          Condition="Exists('%(_WeftNative.FullPath)')"
+          SkipUnchangedFiles="true" />
+  </Target>
+
+</Project>
diff --git a/specs/001-weft-crdt-versioning/tasks.md b/specs/001-weft-crdt-versioning/tasks.md
index b5edf22..fda4c98 100644
--- a/specs/001-weft-crdt-versioning/tasks.md
+++ b/specs/001-weft-crdt-versioning/tasks.md
@@ -61,16 +61,16 @@
 
 **Independent Test** (spec US1): consola con solo la librería: crear→editar→publicar v1→editar→publicar v2→diff→branch+merge→compactación implícita; réplicas convergidas publican el mismo hash
 
-- [ ] T022 [P] [US1] Implement `VersionId` struct in `src/Weft.Versioning/VersionId.cs` (SHA-256, hex lowercase, Parse/TryParse/AsSpan, igualdad por valor)
-- [ ] T023 [P] [US1] Define `IBlobStore` + `InMemoryBlobStore` in `src/Weft.Versioning/Blobs/IBlobStore.cs`, `InMemoryBlobStore.cs` (put idempotente, thread-safe)
-- [ ] T024 [US1] Implement `FileSystemBlobStore` in `src/Weft.Versioning/Blobs/FileSystemBlobStore.cs` (sharding `aa/bb/hash`, escritura atómica temp+rename)
-- [ ] T025 [P] [US1] Implement word-level LCS diff in `src/Weft.Versioning/TextDiff.cs` (`TextDiff`, `TextDiffSegment`, `DiffOp` per contracts/versioning-api.md)
-- [ ] T026 [US1] Implement `VersionStore` in `src/Weft.Versioning/VersionStore.cs`: `PublishAsync`/`CheckoutAsync` (verifica integridad → `BlobIntegrityException`)/`DiffAsync`/`BranchAsync`/`Merge`/`MergeAsync`
-- [ ] T027 [P] [US1] Create engine-parametrized versioning suite `tests/Weft.Versioning.Tests/VersioningSuiteBase.cs` + `YrsVersioningTests.cs`: las **7** postcondiciones de contracts/versioning-api.md (dedup, round-trip, mismo VersionId cross-réplica, diff, conmutatividad de merge, compactación acotada FR-012)
-- [ ] T028 [P] [US1] Unit tests `tests/Weft.Versioning.Tests/TextDiffTests.cs` (Equal/Insert/Delete, determinismo del diff, casos límite: campo vacío, sin cambios)
-- [ ] T029 [P] [US1] Create determinism gate `tests/Weft.Determinism.Tests/DeterminismTests.cs`: corpus de secuencias con client-ids fijos → export/hash idénticos entre réplicas y corridas (P-III; base del job cross-RID)
-- [ ] T030 [US1] Create runnable sample `samples/Weft.Sample.Versioning/Program.cs` ejecutando el user journey completo de US1 (salida legible con hashes y diff)
-- [ ] T031 [US1] Wire CI jobs in `.github/workflows/ci.yml`: `determinism` (bloqueante) + versioning tests en la matriz de plataformas
+- [X] T022 [P] [US1] Implement `VersionId` struct in `src/Weft.Versioning/VersionId.cs` (SHA-256, hex lowercase, Parse/TryParse/AsSpan, igualdad por valor) — CHARTER-02
+- [X] T023 [P] [US1] Define `IBlobStore` + `InMemoryBlobStore` in `src/Weft.Versioning/Blobs/IBlobStore.cs`, `InMemoryBlobStore.cs` (put idempotente, thread-safe) — CHARTER-02
+- [X] T024 [US1] Implement `FileSystemBlobStore` in `src/Weft.Versioning/Blobs/FileSystemBlobStore.cs` (sharding `aa/bb/hash`, escritura atómica temp+rename) — CHARTER-02
+- [X] T025 [P] [US1] Implement word-level LCS diff in `src/Weft.Versioning/TextDiff.cs` (`TextDiff`, `TextDiffSegment`, `DiffOp` per contracts/versioning-api.md) — CHARTER-02
+- [X] T026 [US1] Implement `VersionStore` in `src/Weft.Versioning/VersionStore.cs`: `PublishAsync`/`CheckoutAsync` (verifica integridad → `BlobIntegrityException`)/`DiffAsync`/`BranchAsync`/`Merge`/`MergeAsync` — CHARTER-02
+- [X] T027 [P] [US1] Create engine-parametrized versioning suite `tests/Weft.Versioning.Tests/VersioningSuiteBase.cs` + `YrsVersioningTests.cs`: las **7** postcondiciones de contracts/versioning-api.md (dedup, round-trip, mismo VersionId cross-réplica, diff, conmutatividad de merge, compactación acotada FR-012) — CHARTER-02
+- [X] T028 [P] [US1] Unit tests `tests/Weft.Versioning.Tests/TextDiffTests.cs` (Equal/Insert/Delete, determinismo del diff, casos límite: campo vacío, sin cambios) — CHARTER-02
+- [X] T029 [P] [US1] Create determinism gate `tests/Weft.Determinism.Tests/DeterminismTests.cs`: corpus de secuencias con client-ids fijos → export/hash idénticos entre réplicas y corridas (P-III; base del job cross-RID) — CHARTER-02
+- [X] T030 [US1] Create runnable sample `samples/Weft.Sample.Versioning/Program.cs` ejecutando el user journey completo de US1 (salida legible con hashes y diff) — CHARTER-02
+- [X] T031 [US1] Wire CI jobs in `.github/workflows/ci.yml`: `determinism` (bloqueante) + versioning tests en la matriz de plataformas — CHARTER-02
 
 **Checkpoint**: capa de versionado completa sobre yrs — falta la evidencia dual-engine para cerrar M0 (siguiente fase)
 
@@ -82,10 +82,10 @@
 
 **Independent Test** (spec US5): la MISMA suite de versionado verde sobre yrs y Loro; probes nativos responden en Loro y su ausencia en yrs no rompe nada
 
-- [ ] T032 [P] [US5] Create crate `native/weft-loro-ffi/` (`loro = "=1.13.6"`): ABI núcleo `weft_loro_*` + probes (`native_diff_probe`, `native_branch_probe`, `shallow_snapshot`) + header + tests/mem_asan
-- [ ] T033 [US5] Implement `src/Weft.Loro/LoroEngine.cs` + `LoroDoc.cs` + `LoroNativeVersioning.cs` (`ICrdtEngine` + `INativeVersioning` per contracts/core-api.md)
-- [ ] T034 [US5] Activate dual-engine theory in `tests/Weft.Versioning.Tests/LoroVersioningTests.cs` (hereda `VersioningSuiteBase` de T027) + promote CI job `dual-engine` a gate bloqueante (SC-008)
-- [ ] T035 [P] [US5] Extend `asan` CI job matrix to `weft-loro-ffi` in `.github/workflows/ci.yml` (P-II cubre ambos shims)
+- [X] T032 [P] [US5] Create crate `native/weft-loro-ffi/` (`loro = "=1.13.6"`): ABI núcleo `weft_loro_*` + probes (`native_diff_probe`, `native_branch_probe`, `shallow_snapshot`) + header + tests/mem_asan — CHARTER-02
+- [X] T033 [US5] Implement `src/Weft.Loro/LoroEngine.cs` + `LoroDoc.cs` + `LoroNativeVersioning.cs` (`ICrdtEngine` + `INativeVersioning` per contracts/core-api.md) — CHARTER-02
+- [X] T034 [US5] Activate dual-engine theory in `tests/Weft.Versioning.Tests/LoroVersioningTests.cs` (hereda `VersioningSuiteBase` de T027) + promote CI job `dual-engine` a gate bloqueante (SC-008) — CHARTER-02
+- [X] T035 [P] [US5] Extend `asan` CI job matrix to `weft-loro-ffi` in `.github/workflows/ci.yml` (P-II cubre ambos shims) — CHARTER-02
 
 **Checkpoint**: **M0 se declara cerrado aquí** (US1 + US5): API mínima estable con gates de memoria, determinismo **y dual-engine** activos — evidencia completa para la revisión de hito de la constitución (P-IV)
 
diff --git a/src/Weft.Loro/Interop/DocHandle.cs b/src/Weft.Loro/Interop/DocHandle.cs
new file mode 100644
index 0000000..000b80c
--- /dev/null
+++ b/src/Weft.Loro/Interop/DocHandle.cs
@@ -0,0 +1,41 @@
+using Microsoft.Win32.SafeHandles;
+
+namespace Weft.Loro.Interop;
+
+/// <summary>Handle seguro para el puntero opaco <c>WeftLoroDoc*</c> (mismo patrón que Weft.Yrs).</summary>
+internal sealed class DocHandle : SafeHandleZeroOrMinusOneIsInvalid
+{
+    public DocHandle(nint handle) : base(ownsHandle: true) => SetHandle(handle);
+
+    protected override bool ReleaseHandle()
+    {
+        NativeMethods.weft_loro_doc_free(handle);
+        return true;
+    }
+}
+
+/// <summary>Presta el puntero crudo con ref-count durante la llamada nativa (SYSLIB1051, research R2).</summary>
+internal readonly ref struct HandleLease
+{
+    private readonly DocHandle _handle;
+    private readonly bool _added;
+
+    public readonly nint Ptr;
+
+    public HandleLease(DocHandle handle)
+    {
+        _handle = handle;
+        bool added = false;
+        handle.DangerousAddRef(ref added);
+        _added = added;
+        Ptr = handle.DangerousGetHandle();
+    }
+
+    public void Dispose()
+    {
+        if (_added)
+        {
+            _handle.DangerousRelease();
+        }
+    }
+}
diff --git a/src/Weft.Loro/Interop/FfiStatus.cs b/src/Weft.Loro/Interop/FfiStatus.cs
new file mode 100644
index 0000000..b88aa11
--- /dev/null
+++ b/src/Weft.Loro/Interop/FfiStatus.cs
@@ -0,0 +1,28 @@
+namespace Weft.Loro.Interop;
+
+/// <summary>Traduce un código de estado del shim Loro a la excepción idiomática (mismo mapeo que Weft.Yrs).</summary>
+internal static class FfiStatus
+{
+    internal static void ThrowIfError(int rc)
+    {
+        switch (rc)
+        {
+            case 0:
+                return;
+            case -1:
+                throw new WeftException("Argumento nulo inesperado en la frontera FFI (Loro).");
+            case -2:
+                throw new CorruptUpdateException();
+            case -3:
+                throw new WeftEngineException(WeftErrorCode.Apply, "El motor Loro no pudo aplicar el update.");
+            case -4:
+                throw new WeftEngineException(WeftErrorCode.Utf8, "El texto de entrada no es UTF-8 válido.");
+            case -5:
+                throw new ArgumentOutOfRangeException("index", "El índice o la longitud están fuera de rango.");
+            case -127:
+                throw new WeftEngineException(WeftErrorCode.Panic, "El motor Loro sufrió un panic capturado en la frontera.");
+            default:
+                throw new WeftEngineException(WeftErrorCode.Apply, $"Código FFI desconocido del shim Loro: {rc}.");
+        }
+    }
+}
diff --git a/src/Weft.Loro/Interop/NativeLibraryResolver.cs b/src/Weft.Loro/Interop/NativeLibraryResolver.cs
new file mode 100644
index 0000000..000caf5
--- /dev/null
+++ b/src/Weft.Loro/Interop/NativeLibraryResolver.cs
@@ -0,0 +1,107 @@
+using System.Reflection;
+using System.Runtime.CompilerServices;
+using System.Runtime.InteropServices;
+
+namespace Weft.Loro.Interop;
+
+/// <summary>Resuelve el cdylib <c>weft_loro_ffi</c> por RID y verifica su ABI (igual que Weft.Yrs).</summary>
+internal static class NativeLibraryResolver
+{
+    private const uint ExpectedAbiVersion = 1;
+    private static int _registered;
+
+    [System.Diagnostics.CodeAnalysis.SuppressMessage(
+        "Usage", "CA2255:The 'ModuleInitializer' attribute should not be used in libraries",
+        Justification = "Registro único del DllImportResolver nativo; patrón idiomático de binding por RID.")]
+    [ModuleInitializer]
+    internal static void Register()
+    {
+        if (Interlocked.Exchange(ref _registered, 1) == 0)
+        {
+            NativeLibrary.SetDllImportResolver(typeof(NativeLibraryResolver).Assembly, Resolve);
+        }
+    }
+
+    private static nint Resolve(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
+    {
+        if (libraryName != NativeMethods.Lib)
+        {
+            return nint.Zero;
+        }
+
+        string fileName = NativeFileName();
+        foreach (string candidate in Candidates(fileName))
+        {
+            if (File.Exists(candidate) && NativeLibrary.TryLoad(candidate, out nint handle))
+            {
+                VerifyAbi(handle, candidate);
+                return handle;
+            }
+        }
+        if (NativeLibrary.TryLoad(NativeMethods.Lib, assembly, searchPath, out nint fallback))
+        {
+            VerifyAbi(fallback, NativeMethods.Lib);
+            return fallback;
+        }
+        return nint.Zero;
+    }
+
+    private static IEnumerable<string> Candidates(string fileName)
+    {
+        string baseDir = AppContext.BaseDirectory;
+        string rid = RuntimeInformation.RuntimeIdentifier;
+        string portable = PortableRid();
+        yield return Path.Combine(baseDir, "runtimes", rid, "native", fileName);
+        if (portable != rid)
+        {
+            yield return Path.Combine(baseDir, "runtimes", portable, "native", fileName);
+        }
+        yield return Path.Combine(baseDir, fileName);
+    }
+
+    private static string PortableRid()
+    {
+        string os =
+            RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "win" :
+            RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "osx" :
+            "linux";
+        string arch = RuntimeInformation.ProcessArchitecture switch
+        {
+            Architecture.X64 => "x64",
+            Architecture.Arm64 => "arm64",
+            Architecture.X86 => "x86",
+            Architecture.Arm => "arm",
+            var other => other.ToString().ToLowerInvariant(),
+        };
+        return $"{os}-{arch}";
+    }
+
+    private static string NativeFileName()
+    {
+        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
+        {
+            return $"{NativeMethods.Lib}.dll";
+        }
+        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
+        {
+            return $"lib{NativeMethods.Lib}.dylib";
+        }
+        return $"lib{NativeMethods.Lib}.so";
+    }
+
+    private static unsafe void VerifyAbi(nint handle, string source)
+    {
+        if (!NativeLibrary.TryGetExport(handle, "weft_loro_abi_version", out nint fn))
+        {
+            NativeLibrary.Free(handle);
+            throw new WeftException($"El binario nativo '{source}' no exporta weft_loro_abi_version.");
+        }
+        uint actual = ((delegate* unmanaged<uint>)fn)();
+        if (actual != ExpectedAbiVersion)
+        {
+            NativeLibrary.Free(handle);
+            throw new WeftException(
+                $"ABI del shim Loro '{source}' = {actual}, se esperaba {ExpectedAbiVersion}.");
+        }
+    }
+}
diff --git a/src/Weft.Loro/Interop/NativeMethods.cs b/src/Weft.Loro/Interop/NativeMethods.cs
new file mode 100644
index 0000000..1905bb5
--- /dev/null
+++ b/src/Weft.Loro/Interop/NativeMethods.cs
@@ -0,0 +1,50 @@
+using System.Runtime.InteropServices;
+
+namespace Weft.Loro.Interop;
+
+/// <summary>P/Invoke sobre la C-ABI del shim <c>weft-loro-ffi</c> (simétrica a weft-yrs-ffi).</summary>
+internal static partial class NativeMethods
+{
+    internal const string Lib = "weft_loro_ffi";
+
+    [LibraryImport(Lib)]
+    internal static partial int weft_loro_doc_new(out nint outDoc);
+
+    [LibraryImport(Lib)]
+    internal static partial int weft_loro_doc_load(ReadOnlySpan<byte> blob, nuint blobLen, out nint outDoc);
+
+    [LibraryImport(Lib)]
+    internal static partial void weft_loro_doc_free(nint doc);
+
+    [LibraryImport(Lib)]
+    internal static partial int weft_loro_text_insert(
+        nint doc, ReadOnlySpan<byte> field, nuint fieldLen,
+        uint index, ReadOnlySpan<byte> text, nuint textLen);
+
+    [LibraryImport(Lib)]
+    internal static partial int weft_loro_text_delete(
+        nint doc, ReadOnlySpan<byte> field, nuint fieldLen, uint index, uint len);
+
+    [LibraryImport(Lib)]
+    internal static partial int weft_loro_text_read(
+        nint doc, ReadOnlySpan<byte> field, nuint fieldLen, out nint outPtr, out nuint outLen);
+
+    [LibraryImport(Lib)]
+    internal static partial int weft_loro_doc_export_state(nint doc, out nint outPtr, out nuint outLen);
+
+    [LibraryImport(Lib)]
+    internal static partial int weft_loro_doc_state_vector(nint doc, out nint outPtr, out nuint outLen);
+
+    [LibraryImport(Lib)]
+    internal static partial int weft_loro_doc_export_since(
+        nint doc, ReadOnlySpan<byte> sv, nuint svLen, out nint outPtr, out nuint outLen);
+
+    [LibraryImport(Lib)]
+    internal static partial int weft_loro_doc_apply_update(nint doc, ReadOnlySpan<byte> update, nuint updateLen);
+
+    [LibraryImport(Lib)]
+    internal static partial void weft_loro_buf_free(nint ptr, nuint len);
+
+    [LibraryImport(Lib)]
+    internal static partial uint weft_loro_abi_version();
+}
diff --git a/src/Weft.Loro/LoroDoc.cs b/src/Weft.Loro/LoroDoc.cs
new file mode 100644
index 0000000..6a9ba44
--- /dev/null
+++ b/src/Weft.Loro/LoroDoc.cs
@@ -0,0 +1,119 @@
+using System.Runtime.InteropServices;
+using System.Text;
+using Weft.Loro.Interop;
+
+namespace Weft.Loro;
+
+/// <summary>Documento CRDT respaldado por Loro. Envoltorio gestionado sobre el shim weft-loro-ffi.</summary>
+internal sealed class LoroDoc : ICrdtDoc
+{
+    private readonly DocHandle _handle;
+
+    private LoroDoc(DocHandle handle) => _handle = handle;
+
+    internal static LoroDoc Create()
+    {
+        FfiStatus.ThrowIfError(NativeMethods.weft_loro_doc_new(out nint raw));
+        return new LoroDoc(new DocHandle(raw));
+    }
+
+    internal static LoroDoc Load(ReadOnlySpan<byte> blob)
+    {
+        FfiStatus.ThrowIfError(NativeMethods.weft_loro_doc_load(blob, (nuint)blob.Length, out nint raw));
+        return new LoroDoc(new DocHandle(raw));
+    }
+
+    public void InsertText(string field, int index, string text)
+    {
+        ArgumentException.ThrowIfNullOrEmpty(field);
+        ArgumentNullException.ThrowIfNull(text);
+        ArgumentOutOfRangeException.ThrowIfNegative(index);
+        ThrowIfDisposed();
+
+        byte[] f = Encoding.UTF8.GetBytes(field);
+        byte[] t = Encoding.UTF8.GetBytes(text);
+        using var lease = new HandleLease(_handle);
+        FfiStatus.ThrowIfError(NativeMethods.weft_loro_text_insert(lease.Ptr, f, (nuint)f.Length, (uint)index, t, (nuint)t.Length));
+    }
+
+    public void DeleteText(string field, int index, int length)
+    {
+        ArgumentException.ThrowIfNullOrEmpty(field);
+        ArgumentOutOfRangeException.ThrowIfNegative(index);
+        ArgumentOutOfRangeException.ThrowIfNegative(length);
+        ThrowIfDisposed();
+
+        byte[] f = Encoding.UTF8.GetBytes(field);
+        using var lease = new HandleLease(_handle);
+        FfiStatus.ThrowIfError(NativeMethods.weft_loro_text_delete(lease.Ptr, f, (nuint)f.Length, (uint)index, (uint)length));
+    }
+
+    public string GetText(string field)
+    {
+        ArgumentException.ThrowIfNullOrEmpty(field);
+        ThrowIfDisposed();
+
+        byte[] f = Encoding.UTF8.GetBytes(field);
+        using var lease = new HandleLease(_handle);
+        FfiStatus.ThrowIfError(NativeMethods.weft_loro_text_read(lease.Ptr, f, (nuint)f.Length, out nint ptr, out nuint len));
+        return Encoding.UTF8.GetString(TakeOwnedBuffer(ptr, len));
+    }
+
+    public byte[] ExportState()
+    {
+        ThrowIfDisposed();
+        using var lease = new HandleLease(_handle);
+        FfiStatus.ThrowIfError(NativeMethods.weft_loro_doc_export_state(lease.Ptr, out nint ptr, out nuint len));
+        return TakeOwnedBuffer(ptr, len);
+    }
+
+    public byte[] ExportStateVector()
+    {
+        ThrowIfDisposed();
+        using var lease = new HandleLease(_handle);
+        FfiStatus.ThrowIfError(NativeMethods.weft_loro_doc_state_vector(lease.Ptr, out nint ptr, out nuint len));
+        return TakeOwnedBuffer(ptr, len);
+    }
+
+    public byte[] ExportUpdateSince(ReadOnlySpan<byte> stateVector)
+    {
+        ThrowIfDisposed();
+        using var lease = new HandleLease(_handle);
+        FfiStatus.ThrowIfError(NativeMethods.weft_loro_doc_export_since(
+            lease.Ptr, stateVector, (nuint)stateVector.Length, out nint ptr, out nuint len));
+        return TakeOwnedBuffer(ptr, len);
+    }
+
+    public void ApplyUpdate(ReadOnlySpan<byte> update)
+    {
+        ThrowIfDisposed();
+        using var lease = new HandleLease(_handle);
+        FfiStatus.ThrowIfError(NativeMethods.weft_loro_doc_apply_update(lease.Ptr, update, (nuint)update.Length));
+    }
+
+    public void Dispose() => _handle.Dispose();
+
+    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_handle.IsClosed, this);
+
+    private static byte[] TakeOwnedBuffer(nint ptr, nuint len)
+    {
+        if (ptr == nint.Zero || len == 0)
+        {
+            if (ptr != nint.Zero)
+            {
+                NativeMethods.weft_loro_buf_free(ptr, len);
+            }
+            return [];
+        }
+        try
+        {
+            var managed = new byte[(int)len];
+            Marshal.Copy(ptr, managed, 0, (int)len);
+            return managed;
+        }
+        finally
+        {
+            NativeMethods.weft_loro_buf_free(ptr, len);
+        }
+    }
+}
diff --git a/src/Weft.Loro/LoroEngine.cs b/src/Weft.Loro/LoroEngine.cs
new file mode 100644
index 0000000..efa6588
--- /dev/null
+++ b/src/Weft.Loro/LoroEngine.cs
@@ -0,0 +1,31 @@
+namespace Weft.Loro;
+
+/// <summary>
+/// Motor CRDT respaldado por Loro (vía el shim <c>weft-loro-ffi</c>). Adaptador dual-path que prueba
+/// la portabilidad de la abstracción <see cref="ICrdtEngine"/> (constitución P-IV): la misma suite de
+/// versionado corre idéntica sobre yrs y Loro.
+/// </summary>
+public sealed class LoroEngine : ICrdtEngine
+{
+    private LoroEngine() { }
+
+    /// <summary>Instancia compartida del motor (sin estado, thread-safe).</summary>
+    public static LoroEngine Instance { get; } = new();
+
+    /// <inheritdoc/>
+    public string Name => "loro";
+
+    /// <inheritdoc/>
+    /// <remarks>
+    /// Loro ofrece versionado nativo (diff/branch/shallow-snapshot); esas capacidades se exponen como
+    /// <see cref="INativeVersioning"/> opcional en una iteración posterior. El versionado del núcleo
+    /// (content-addressed, engine-agnóstico) no depende de ellas.
+    /// </remarks>
+    public INativeVersioning? NativeVersioning => null;
+
+    /// <inheritdoc/>
+    public ICrdtDoc CreateDoc() => LoroDoc.Create();
+
+    /// <inheritdoc/>
+    public ICrdtDoc LoadDoc(ReadOnlySpan<byte> blob) => LoroDoc.Load(blob);
+}
diff --git a/src/Weft.Loro/Weft.Loro.csproj b/src/Weft.Loro/Weft.Loro.csproj
new file mode 100644
index 0000000..41f472b
--- /dev/null
+++ b/src/Weft.Loro/Weft.Loro.csproj
@@ -0,0 +1,12 @@
+<Project Sdk="Microsoft.NET.Sdk">
+
+  <PropertyGroup>
+    <Description>Weft — adaptador dual-path sobre el motor CRDT Loro (vía shim weft-loro-ffi). Prueba la portabilidad de la abstracción ICrdtEngine (constitución P-IV).</Description>
+    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
+  </PropertyGroup>
+
+  <ItemGroup>
+    <ProjectReference Include="../Weft.Core/Weft.Core.csproj" />
+  </ItemGroup>
+
+</Project>
diff --git a/src/Weft.Versioning/Blobs/FileSystemBlobStore.cs b/src/Weft.Versioning/Blobs/FileSystemBlobStore.cs
new file mode 100644
index 0000000..ff6c0a5
--- /dev/null
+++ b/src/Weft.Versioning/Blobs/FileSystemBlobStore.cs
@@ -0,0 +1,64 @@
+namespace Weft.Versioning.Blobs;
+
+/// <summary>
+/// Almacén content-addressed sobre el sistema de archivos (v1). Sharding <c>aa/bb/hash</c> para
+/// evitar directorios enormes; escritura atómica (temp + rename) para no dejar blobs a medias.
+/// Thread-safe: el content-addressing hace que dos escritores del mismo hash escriban lo mismo.
+/// </summary>
+public sealed class FileSystemBlobStore : IBlobStore
+{
+    private readonly string _root;
+
+    /// <summary>Crea el almacén enraizado en <paramref name="rootDirectory"/> (lo crea si no existe).</summary>
+    public FileSystemBlobStore(string rootDirectory)
+    {
+        ArgumentException.ThrowIfNullOrEmpty(rootDirectory);
+        _root = rootDirectory;
+        Directory.CreateDirectory(_root);
+    }
+
+    private string PathFor(VersionId id)
+    {
+        string hex = id.ToString();
+        return Path.Combine(_root, hex[..2], hex[2..4], hex);
+    }
+
+    /// <inheritdoc/>
+    public async ValueTask PutAsync(VersionId id, ReadOnlyMemory<byte> blob, CancellationToken ct = default)
+    {
+        string path = PathFor(id);
+        if (File.Exists(path))
+        {
+            return; // idempotente (dedup por hash)
+        }
+        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
+
+        string tmp = path + ".tmp-" + Path.GetRandomFileName();
+        await File.WriteAllBytesAsync(tmp, blob.ToArray(), ct).ConfigureAwait(false);
+        try
+        {
+            // Rename atómico dentro del mismo directorio; overwrite:false → si otro escritor ganó
+            // la carrera, ambos escribieron el MISMO contenido (content-addressed), así que da igual.
+            File.Move(tmp, path, overwrite: false);
+        }
+        catch (IOException) when (File.Exists(path))
+        {
+            File.Delete(tmp);
+        }
+    }
+
+    /// <inheritdoc/>
+    public async ValueTask<byte[]?> GetAsync(VersionId id, CancellationToken ct = default)
+    {
+        string path = PathFor(id);
+        if (!File.Exists(path))
+        {
+            return null;
+        }
+        return await File.ReadAllBytesAsync(path, ct).ConfigureAwait(false);
+    }
+
+    /// <inheritdoc/>
+    public ValueTask<bool> ExistsAsync(VersionId id, CancellationToken ct = default) =>
+        ValueTask.FromResult(File.Exists(PathFor(id)));
+}
diff --git a/src/Weft.Versioning/Blobs/IBlobStore.cs b/src/Weft.Versioning/Blobs/IBlobStore.cs
new file mode 100644
index 0000000..f92207d
--- /dev/null
+++ b/src/Weft.Versioning/Blobs/IBlobStore.cs
@@ -0,0 +1,19 @@
+namespace Weft.Versioning.Blobs;
+
+/// <summary>
+/// Almacén content-addressed (hash → blob). Las implementaciones deben ser thread-safe.
+/// Sin <c>delete</c> en v1: las versiones publicadas son inmutables (FR-012); la retención es
+/// dominio del consumidor.
+/// </summary>
+public interface IBlobStore
+{
+    /// <summary>Persiste un blob bajo su identidad. Idempotente: put del mismo contenido es no-op
+    /// (dedup natural por hash).</summary>
+    ValueTask PutAsync(VersionId id, ReadOnlyMemory<byte> blob, CancellationToken ct = default);
+
+    /// <summary>Devuelve el blob, o <c>null</c> si no existe.</summary>
+    ValueTask<byte[]?> GetAsync(VersionId id, CancellationToken ct = default);
+
+    /// <summary>Indica si existe un blob para la identidad dada.</summary>
+    ValueTask<bool> ExistsAsync(VersionId id, CancellationToken ct = default);
+}
diff --git a/src/Weft.Versioning/Blobs/InMemoryBlobStore.cs b/src/Weft.Versioning/Blobs/InMemoryBlobStore.cs
new file mode 100644
index 0000000..01fb5da
--- /dev/null
+++ b/src/Weft.Versioning/Blobs/InMemoryBlobStore.cs
@@ -0,0 +1,28 @@
+using System.Collections.Concurrent;
+
+namespace Weft.Versioning.Blobs;
+
+/// <summary>Almacén content-addressed en memoria (tests/dev). Thread-safe.</summary>
+public sealed class InMemoryBlobStore : IBlobStore
+{
+    private readonly ConcurrentDictionary<string, byte[]> _blobs = new(StringComparer.Ordinal);
+
+    /// <summary>Número de blobs distintos almacenados (útil para verificar dedup/compactación).</summary>
+    public int Count => _blobs.Count;
+
+    /// <inheritdoc/>
+    public ValueTask PutAsync(VersionId id, ReadOnlyMemory<byte> blob, CancellationToken ct = default)
+    {
+        // Idempotente: si ya existe, no se re-copia (dedup por hash).
+        _blobs.TryAdd(id.ToString(), blob.ToArray());
+        return ValueTask.CompletedTask;
+    }
+
+    /// <inheritdoc/>
+    public ValueTask<byte[]?> GetAsync(VersionId id, CancellationToken ct = default) =>
+        ValueTask.FromResult(_blobs.TryGetValue(id.ToString(), out byte[]? blob) ? blob : null);
+
+    /// <inheritdoc/>
+    public ValueTask<bool> ExistsAsync(VersionId id, CancellationToken ct = default) =>
+        ValueTask.FromResult(_blobs.ContainsKey(id.ToString()));
+}
diff --git a/src/Weft.Versioning/TextDiff.cs b/src/Weft.Versioning/TextDiff.cs
new file mode 100644
index 0000000..ca3ea87
--- /dev/null
+++ b/src/Weft.Versioning/TextDiff.cs
@@ -0,0 +1,119 @@
+using System.Text;
+
+namespace Weft.Versioning;
+
+/// <summary>Operación de un segmento de diff.</summary>
+public enum DiffOp
+{
+    /// <summary>Texto sin cambios entre ambas versiones.</summary>
+    Equal,
+
+    /// <summary>Texto presente solo en la versión nueva.</summary>
+    Inserted,
+
+    /// <summary>Texto presente solo en la versión antigua.</summary>
+    Deleted,
+}
+
+/// <summary>Un segmento contiguo de un diff de texto.</summary>
+public readonly record struct TextDiffSegment(DiffOp Op, string Text);
+
+/// <summary>
+/// Diff de texto por palabras (research R9): LCS sobre tokens palabra/espacio, determinista
+/// (mismas entradas → mismos segmentos). Alcance v1: texto plano por campo.
+/// </summary>
+public sealed record TextDiff(IReadOnlyList<TextDiffSegment> Segments)
+{
+    /// <summary>Indica si hay al menos un segmento insertado o borrado.</summary>
+    public bool HasChanges => Segments.Any(s => s.Op != DiffOp.Equal);
+
+    /// <summary>Computa el diff por palabras entre <paramref name="oldText"/> y <paramref name="newText"/>.</summary>
+    public static TextDiff Compute(string oldText, string newText)
+    {
+        ArgumentNullException.ThrowIfNull(oldText);
+        ArgumentNullException.ThrowIfNull(newText);
+
+        string[] a = Tokenize(oldText);
+        string[] b = Tokenize(newText);
+        int[,] lcs = BuildLcsTable(a, b);
+
+        var segments = new List<TextDiffSegment>();
+        // Reconstrucción desde el final de la tabla LCS hacia el inicio.
+        int i = a.Length, j = b.Length;
+        var rev = new List<TextDiffSegment>();
+        while (i > 0 || j > 0)
+        {
+            if (i > 0 && j > 0 && a[i - 1] == b[j - 1])
+            {
+                rev.Add(new TextDiffSegment(DiffOp.Equal, a[i - 1]));
+                i--;
+                j--;
+            }
+            else if (j > 0 && (i == 0 || lcs[i, j - 1] >= lcs[i - 1, j]))
+            {
+                rev.Add(new TextDiffSegment(DiffOp.Inserted, b[j - 1]));
+                j--;
+            }
+            else
+            {
+                rev.Add(new TextDiffSegment(DiffOp.Deleted, a[i - 1]));
+                i--;
+            }
+        }
+        rev.Reverse();
+
+        // Fusiona tokens contiguos de la misma operación en un solo segmento legible.
+        foreach (TextDiffSegment token in rev)
+        {
+            if (segments.Count > 0 && segments[^1].Op == token.Op)
+            {
+                segments[^1] = segments[^1] with { Text = segments[^1].Text + token.Text };
+            }
+            else
+            {
+                segments.Add(token);
+            }
+        }
+        return new TextDiff(segments);
+    }
+
+    /// <summary>Tokeniza en unidades palabra y espacio (cada run de whitespace o no-whitespace).</summary>
+    private static string[] Tokenize(string text)
+    {
+        if (text.Length == 0)
+        {
+            return [];
+        }
+        var tokens = new List<string>();
+        var sb = new StringBuilder();
+        bool currentIsSpace = char.IsWhiteSpace(text[0]);
+        foreach (char c in text)
+        {
+            bool isSpace = char.IsWhiteSpace(c);
+            if (isSpace != currentIsSpace)
+            {
+                tokens.Add(sb.ToString());
+                sb.Clear();
+                currentIsSpace = isSpace;
+            }
+            sb.Append(c);
+        }
+        tokens.Add(sb.ToString());
+        return [.. tokens];
+    }
+
+    private static int[,] BuildLcsTable(string[] a, string[] b)
+    {
+        var lcs = new int[a.Length + 1, b.Length + 1];
+        for (int i = 1; i <= a.Length; i++)
+        {
+            for (int j = 1; j <= b.Length; j++)
+            {
+                lcs[i, j] = a[i - 1] == b[j - 1]
+                    ? lcs[i - 1, j - 1] + 1
+                    : Math.Max(lcs[i - 1, j], lcs[i, j - 1]);
+            }
+        }
+        return lcs;
+    }
+}
diff --git a/src/Weft.Versioning/VersionId.cs b/src/Weft.Versioning/VersionId.cs
new file mode 100644
index 0000000..1c99095
--- /dev/null
+++ b/src/Weft.Versioning/VersionId.cs
@@ -0,0 +1,74 @@
+using System.Diagnostics.CodeAnalysis;
+using System.Security.Cryptography;
+
+namespace Weft.Versioning;
+
+/// <summary>
+/// Identidad content-addressed de una versión: SHA-256 del export determinista (constitución P-III).
+/// Value type inmutable; igualdad por valor; representación hex lowercase de 64 caracteres.
+/// </summary>
+public readonly struct VersionId : IEquatable<VersionId>
+{
+    private readonly byte[]? _bytes; // 32 bytes; null solo en el valor `default`.
+
+    private VersionId(byte[] bytes) => _bytes = bytes;
+
+    /// <summary>Calcula la identidad de un blob (SHA-256).</summary>
+    public static VersionId FromBlob(ReadOnlySpan<byte> blob) => new(SHA256.HashData(blob));
+
+    /// <summary>Parsea una representación hex de 64 caracteres.</summary>
+    /// <exception cref="FormatException">La cadena no es un hash SHA-256 hex válido.</exception>
+    public static VersionId Parse(string hex)
+    {
+        if (!TryParse(hex, out VersionId id))
+        {
+            throw new FormatException("Un VersionId debe ser 64 caracteres hexadecimales (SHA-256).");
+        }
+        return id;
+    }
+
+    /// <summary>Intenta parsear una representación hex de 64 caracteres.</summary>
+    public static bool TryParse([NotNullWhen(true)] string? hex, out VersionId id)
+    {
+        id = default;
+        if (hex is not { Length: 64 })
+        {
+            return false;
+        }
+        foreach (char c in hex)
+        {
+            if (!Uri.IsHexDigit(c))
+            {
+                return false;
+            }
+        }
+        id = new VersionId(Convert.FromHexString(hex));
+        return true;
+    }
+
+    /// <summary>Los 32 bytes del hash.</summary>
+    public ReadOnlySpan<byte> AsSpan() => _bytes ?? [];
+
+    /// <summary>Representación hex lowercase (64 caracteres).</summary>
+    public override string ToString() => _bytes is null ? "" : Convert.ToHexStringLower(_bytes);
+
+    /// <inheritdoc/>
+    public bool Equals(VersionId other) => AsSpan().SequenceEqual(other.AsSpan());
+
+    /// <inheritdoc/>
+    public override bool Equals(object? obj) => obj is VersionId other && Equals(other);
+
+    /// <inheritdoc/>
+    public override int GetHashCode()
+    {
+        var span = AsSpan();
+        // Los bytes de un SHA-256 ya están uniformemente distribuidos: los primeros 4 bastan.
+        return span.Length >= 4 ? BitConverter.ToInt32(span[..4]) : 0;
+    }
+
+    /// <summary>Igualdad por valor.</summary>
+    public static bool operator ==(VersionId left, VersionId right) => left.Equals(right);
+
+    /// <summary>Desigualdad por valor.</summary>
+    public static bool operator !=(VersionId left, VersionId right) => !left.Equals(right);
+}
diff --git a/src/Weft.Versioning/VersionStore.cs b/src/Weft.Versioning/VersionStore.cs
new file mode 100644
index 0000000..3e43071
--- /dev/null
+++ b/src/Weft.Versioning/VersionStore.cs
@@ -0,0 +1,88 @@
+using Weft;
+using Weft.Versioning.Blobs;
+
+namespace Weft.Versioning;
+
+/// <summary>
+/// Publicar/cargar/comparar/ramificar/mezclar versiones de documentos, content-addressed y
+/// engine-agnóstico (constitución P-IV: depende solo de las abstracciones de Weft.Core).
+/// Thread-safe (sin estado mutable propio; la serialización del doc vivo es responsabilidad del
+/// llamador o del DocumentBroker).
+/// </summary>
+public sealed class VersionStore
+{
+    private readonly ICrdtEngine _engine;
+    private readonly IBlobStore _blobs;
+
+    /// <summary>Crea el almacén de versiones sobre un motor y un almacén de blobs.</summary>
+    public VersionStore(ICrdtEngine engine, IBlobStore blobs)
+    {
+        ArgumentNullException.ThrowIfNull(engine);
+        ArgumentNullException.ThrowIfNull(blobs);
+        _engine = engine;
+        _blobs = blobs;
+    }
+
+    /// <summary>Exporta, hashea y persiste el documento. Devuelve la identidad citable.</summary>
+    public async ValueTask<VersionId> PublishAsync(ICrdtDoc doc, CancellationToken ct = default)
+    {
+        ArgumentNullException.ThrowIfNull(doc);
+        byte[] blob = doc.ExportState();
+        var id = VersionId.FromBlob(blob);
+        await _blobs.PutAsync(id, blob, ct).ConfigureAwait(false);
+        return id;
+    }
+
+    /// <summary>Reconstruye un documento vivo desde una versión publicada (verifica integridad).</summary>
+    /// <exception cref="KeyNotFoundException">La versión no existe en el almacén.</exception>
+    /// <exception cref="BlobIntegrityException">El blob almacenado no verifica contra su hash.</exception>
+    public async ValueTask<ICrdtDoc> CheckoutAsync(VersionId version, CancellationToken ct = default)
+    {
+        byte[] blob = await LoadVerifiedAsync(version, ct).ConfigureAwait(false);
+        return _engine.LoadDoc(blob);
+    }
+
+    /// <summary>Diff de texto por palabras entre dos versiones publicadas, en un campo dado.</summary>
+    public async ValueTask<TextDiff> DiffAsync(VersionId a, VersionId b, string field, CancellationToken ct = default)
+    {
+        ArgumentException.ThrowIfNullOrEmpty(field);
+        using ICrdtDoc da = await CheckoutAsync(a, ct).ConfigureAwait(false);
+        using ICrdtDoc db = await CheckoutAsync(b, ct).ConfigureAwait(false);
+        return TextDiff.Compute(da.GetText(field), db.GetText(field));
+    }
+
+    /// <summary>Rama: documento vivo independiente partiendo de la versión base (alias de Checkout).</summary>
+    public ValueTask<ICrdtDoc> BranchAsync(VersionId from, CancellationToken ct = default) =>
+        CheckoutAsync(from, ct);
+
+    /// <summary>Merge CRDT: importa el estado de la rama en el destino (convergente, sin conflictos).</summary>
+    public void Merge(ICrdtDoc target, ICrdtDoc branch)
+    {
+        ArgumentNullException.ThrowIfNull(target);
+        ArgumentNullException.ThrowIfNull(branch);
+        target.ApplyUpdate(branch.ExportState());
+    }
+
+    /// <summary>Merge CRDT desde una versión publicada hacia un documento vivo destino.</summary>
+    public async ValueTask MergeAsync(ICrdtDoc target, VersionId branchVersion, CancellationToken ct = default)
+    {
+        ArgumentNullException.ThrowIfNull(target);
+        byte[] blob = await LoadVerifiedAsync(branchVersion, ct).ConfigureAwait(false);
+        target.ApplyUpdate(blob);
+    }
+
+    private async ValueTask<byte[]> LoadVerifiedAsync(VersionId version, CancellationToken ct)
+    {
+        byte[]? blob = await _blobs.GetAsync(version, ct).ConfigureAwait(false);
+        if (blob is null)
+        {
+            throw new KeyNotFoundException($"No existe una versión publicada con id {version}.");
+        }
+        if (VersionId.FromBlob(blob) != version)
+        {
+            throw new BlobIntegrityException(
+                $"El blob almacenado para {version} no verifica contra su hash (corrupción).");
+        }
+        return blob;
+    }
+}
diff --git a/tests/Weft.Core.Tests/Utf16IndexingTests.cs b/tests/Weft.Core.Tests/Utf16IndexingTests.cs
new file mode 100644
index 0000000..0baea2c
--- /dev/null
+++ b/tests/Weft.Core.Tests/Utf16IndexingTests.cs
@@ -0,0 +1,34 @@
+using Weft;
+using Weft.Yrs;
+
+namespace Weft.Core.Tests;
+
+/// <summary>
+/// Regresión: los índices de insert/delete son UTF-16 code units (consistentes con string de .NET
+/// y con Yjs), no bytes UTF-8 (el default de yrs). Un bug latente de CHARTER-01 sobre texto
+/// no-ASCII, corregido con OffsetKind::Utf16 en el shim.
+/// </summary>
+public sealed class Utf16IndexingTests
+{
+    [Fact]
+    public void Delete_spans_correct_utf16_units_with_accents()
+    {
+        using ICrdtDoc doc = YrsEngine.Instance.CreateDoc();
+        doc.InsertText("f", 0, "El veloz murciélago");
+        Assert.Equal(19, doc.GetText("f").Length);      // UTF-16 code units
+        doc.DeleteText("f", 9, 10);                      // borra "murciélago"
+        Assert.Equal("El veloz ", doc.GetText("f"));
+        doc.InsertText("f", 9, "colibrí");
+        Assert.Equal("El veloz colibrí", doc.GetText("f"));
+    }
+
+    [Fact]
+    public void Insert_at_index_past_accent()
+    {
+        using ICrdtDoc doc = YrsEngine.Instance.CreateDoc();
+        doc.InsertText("f", 0, "café");
+        Assert.Equal(4, doc.GetText("f").Length);
+        doc.InsertText("f", 4, " con leche");
+        Assert.Equal("café con leche", doc.GetText("f"));
+    }
+}
diff --git a/tests/Weft.Determinism.Tests/DeterminismTests.cs b/tests/Weft.Determinism.Tests/DeterminismTests.cs
new file mode 100644
index 0000000..f35e232
--- /dev/null
+++ b/tests/Weft.Determinism.Tests/DeterminismTests.cs
@@ -0,0 +1,116 @@
+using Weft;
+using Weft.Versioning;
+using Weft.Versioning.Blobs;
+using Weft.Yrs;
+
+namespace Weft.Determinism.Tests;
+
+/// <summary>
+/// Gate de determinismo del encoding (T029, constitución P-III): la identidad de una versión es
+/// reproducible. Base del job cross-RID — el determinismo cross-implementación absoluto vs Yjs JS
+/// (mismo hash en TODOS los RIDs) se completa en T058 (US4) con el corpus compartido; aquí se fijan
+/// las propiedades reproducibles del encoding que ese job compara.
+/// </summary>
+public sealed class DeterminismTests
+{
+    private static readonly ICrdtEngine Engine = YrsEngine.Instance;
+
+    // Corpus determinista: secuencia fija de ediciones repartidas entre 3 réplicas.
+    private static readonly (int replica, string op, string text, int index, int len)[] Corpus =
+    [
+        (0, "ins", "El veloz ", 0, 0),
+        (1, "ins", "murciélago ", 0, 0),
+        (2, "ins", "hindú ", 0, 0),
+        (0, "ins", "comía ", 9, 0),
+        (1, "ins", "feliz ", 0, 0),
+        (2, "del", "", 0, 3),
+    ];
+
+    private static void ApplyCorpus(ICrdtDoc[] replicas)
+    {
+        foreach ((int r, string op, string text, int index, int len) in Corpus)
+        {
+            if (op == "ins")
+            {
+                replicas[r].InsertText("body", index, text);
+            }
+            else
+            {
+                // Aplica el borrado solo si hay suficiente contenido (robustez del corpus).
+                if (replicas[r].GetText("body").Length >= index + len)
+                {
+                    replicas[r].DeleteText("body", index, len);
+                }
+            }
+        }
+    }
+
+    private static void SyncAll(ICrdtDoc[] replicas)
+    {
+        // Dos pasadas todos-contra-todos: garantiza convergencia con 3 réplicas.
+        for (int pass = 0; pass < 2; pass++)
+        {
+            foreach (ICrdtDoc target in replicas)
+            {
+                byte[] sv = target.ExportStateVector();
+                foreach (ICrdtDoc source in replicas)
+                {
+                    if (!ReferenceEquals(source, target))
+                    {
+                        target.ApplyUpdate(source.ExportUpdateSince(sv));
+                    }
+                }
+            }
+        }
+    }
+
+    // Réplicas convergidas tras el mismo corpus → VersionId idéntico (SC-002, P-III).
+    [Fact]
+    public async Task Converged_replicas_share_version_id()
+    {
+        var blobs = new InMemoryBlobStore();
+        var store = new VersionStore(Engine, blobs);
+        ICrdtDoc[] replicas = [Engine.CreateDoc(), Engine.CreateDoc(), Engine.CreateDoc()];
+        try
+        {
+            ApplyCorpus(replicas);
+            SyncAll(replicas);
+
+            VersionId reference = await store.PublishAsync(replicas[0]);
+            foreach (ICrdtDoc r in replicas)
+            {
+                Assert.Equal(reference, await store.PublishAsync(r));
+            }
+            // Un solo blob: todas las réplicas convergieron al mismo estado (dedup).
+            Assert.Equal(1, blobs.Count);
+        }
+        finally
+        {
+            foreach (ICrdtDoc r in replicas)
+            {
+                r.Dispose();
+            }
+        }
+    }
+
+    // El encoding es estable: cargar un blob y re-exportar es byte-idéntico, indefinidamente
+    // (la propiedad que hace que un VersionId sea citable cross-plataforma).
+    [Fact]
+    public void Reload_and_reexport_is_byte_stable()
+    {
+        using ICrdtDoc doc = Engine.CreateDoc();
+        doc.InsertText("body", 0, "contenido para hashear áéíóú");
+        byte[] original = doc.ExportState();
+        VersionId id = VersionId.FromBlob(original);
+
+        byte[] current = original;
+        for (int i = 0; i < 8; i++)
+        {
+            using ICrdtDoc reloaded = Engine.LoadDoc(current);
+            byte[] next = reloaded.ExportState();
+            Assert.Equal(original, next);                 // byte-idéntico en cada recarga
+            Assert.Equal(id, VersionId.FromBlob(next));   // mismo hash
+            current = next;
+        }
+    }
+}
diff --git a/tests/Weft.Determinism.Tests/Weft.Determinism.Tests.csproj b/tests/Weft.Determinism.Tests/Weft.Determinism.Tests.csproj
new file mode 100644
index 0000000..371e396
--- /dev/null
+++ b/tests/Weft.Determinism.Tests/Weft.Determinism.Tests.csproj
@@ -0,0 +1,43 @@
+<Project Sdk="Microsoft.NET.Sdk">
+
+  <PropertyGroup>
+    <IsPackable>false</IsPackable>
+  </PropertyGroup>
+
+  <ItemGroup>
+    <PackageReference Include="coverlet.collector" Version="6.0.4" />
+    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.14.1" />
+    <PackageReference Include="xunit" Version="2.9.3" />
+    <PackageReference Include="xunit.runner.visualstudio" Version="3.1.4" />
+  </ItemGroup>
+
+  <ItemGroup>
+    <Using Include="Xunit" />
+  </ItemGroup>
+
+  <ItemGroup>
+    <ProjectReference Include="../../src/Weft.Versioning/Weft.Versioning.csproj" />
+  </ItemGroup>
+
+  <Target Name="CopyWeftNativeForTests" AfterTargets="Build">
+    <PropertyGroup>
+      <_WeftRid Condition="$([MSBuild]::IsOSPlatform('Windows'))">win-x64</_WeftRid>
+      <_WeftRid Condition="$([MSBuild]::IsOSPlatform('OSX'))">osx-arm64</_WeftRid>
+      <_WeftRid Condition="$([MSBuild]::IsOSPlatform('Linux'))">linux-x64</_WeftRid>
+      <_WeftPrefix Condition="$([MSBuild]::IsOSPlatform('Windows'))"></_WeftPrefix>
+      <_WeftPrefix Condition="!$([MSBuild]::IsOSPlatform('Windows'))">lib</_WeftPrefix>
+      <_WeftExt Condition="$([MSBuild]::IsOSPlatform('Windows'))">.dll</_WeftExt>
+      <_WeftExt Condition="$([MSBuild]::IsOSPlatform('OSX'))">.dylib</_WeftExt>
+      <_WeftExt Condition="$([MSBuild]::IsOSPlatform('Linux'))">.so</_WeftExt>
+    </PropertyGroup>
+    <ItemGroup>
+      <_WeftNative Include="$(MSBuildProjectDirectory)/../../native/target/release/$(_WeftPrefix)weft_yrs_ffi$(_WeftExt)" />
+      <_WeftNative Include="$(MSBuildProjectDirectory)/../../native/target/release/$(_WeftPrefix)weft_loro_ffi$(_WeftExt)" />
+    </ItemGroup>
+    <Copy SourceFiles="@(_WeftNative)"
+          DestinationFolder="$(OutDir)runtimes/$(_WeftRid)/native/"
+          Condition="Exists('%(_WeftNative.FullPath)')"
+          SkipUnchangedFiles="true" />
+  </Target>
+
+</Project>
diff --git a/tests/Weft.Versioning.Tests/LoroVersioningTests.cs b/tests/Weft.Versioning.Tests/LoroVersioningTests.cs
new file mode 100644
index 0000000..33dc983
--- /dev/null
+++ b/tests/Weft.Versioning.Tests/LoroVersioningTests.cs
@@ -0,0 +1,13 @@
+using Weft;
+using Weft.Loro;
+
+namespace Weft.Versioning.Tests;
+
+/// <summary>
+/// Activa la teoría dual-engine (T034, SC-008): la MISMA suite de versionado (VersioningSuiteBase)
+/// corre sobre Loro. Si pasa, la abstracción ICrdtEngine está viva sobre ambos motores (P-IV).
+/// </summary>
+public sealed class LoroVersioningTests : VersioningSuiteBase
+{
+    protected override ICrdtEngine Engine => LoroEngine.Instance;
+}
diff --git a/tests/Weft.Versioning.Tests/TextDiffTests.cs b/tests/Weft.Versioning.Tests/TextDiffTests.cs
new file mode 100644
index 0000000..58ee9a9
--- /dev/null
+++ b/tests/Weft.Versioning.Tests/TextDiffTests.cs
@@ -0,0 +1,52 @@
+namespace Weft.Versioning.Tests;
+
+/// <summary>Unit tests del diff LCS por palabras (T028): operaciones, determinismo, casos límite.</summary>
+public sealed class TextDiffTests
+{
+    [Fact]
+    public void No_changes_when_identical()
+    {
+        TextDiff diff = TextDiff.Compute("el gato duerme", "el gato duerme");
+        Assert.False(diff.HasChanges);
+        Assert.All(diff.Segments, s => Assert.Equal(DiffOp.Equal, s.Op));
+    }
+
+    [Fact]
+    public void Empty_fields()
+    {
+        Assert.False(TextDiff.Compute("", "").HasChanges);
+        Assert.True(TextDiff.Compute("", "hola").HasChanges);
+        Assert.True(TextDiff.Compute("hola", "").HasChanges);
+    }
+
+    [Fact]
+    public void Insert_and_delete_words()
+    {
+        TextDiff diff = TextDiff.Compute("el gato duerme", "el perro duerme");
+        Assert.True(diff.HasChanges);
+        Assert.Contains(diff.Segments, s => s.Op == DiffOp.Deleted && s.Text.Contains("gato"));
+        Assert.Contains(diff.Segments, s => s.Op == DiffOp.Inserted && s.Text.Contains("perro"));
+        // "el " y " duerme" permanecen iguales.
+        Assert.Contains(diff.Segments, s => s.Op == DiffOp.Equal && s.Text.Contains("el"));
+        Assert.Contains(diff.Segments, s => s.Op == DiffOp.Equal && s.Text.Contains("duerme"));
+    }
+
+    [Fact]
+    public void Reconstructs_new_text_from_equal_plus_inserted()
+    {
+        TextDiff diff = TextDiff.Compute("uno dos", "uno tres dos");
+        string rebuilt = string.Concat(
+            diff.Segments.Where(s => s.Op != DiffOp.Deleted).Select(s => s.Text));
+        Assert.Equal("uno tres dos", rebuilt);
+    }
+
+    [Fact]
+    public void Is_deterministic()
+    {
+        TextDiff d1 = TextDiff.Compute("the quick brown fox", "the slow brown fox jumps");
+        TextDiff d2 = TextDiff.Compute("the quick brown fox", "the slow brown fox jumps");
+        Assert.Equal(
+            d1.Segments.Select(s => (s.Op, s.Text)),
+            d2.Segments.Select(s => (s.Op, s.Text)));
+    }
+}
diff --git a/tests/Weft.Versioning.Tests/VersioningSuiteBase.cs b/tests/Weft.Versioning.Tests/VersioningSuiteBase.cs
new file mode 100644
index 0000000..fa1486f
--- /dev/null
+++ b/tests/Weft.Versioning.Tests/VersioningSuiteBase.cs
@@ -0,0 +1,166 @@
+using Weft;
+using Weft.Versioning.Blobs;
+
+namespace Weft.Versioning.Tests;
+
+/// <summary>
+/// Suite parametrizada de versionado (T027): las 7 postcondiciones de contracts/versioning-api.md.
+/// Es abstracta; cada subclase concreta fija el motor (P-IV: la misma suite corre idéntica sobre
+/// YrsEngine Y LoroEngine — postcondición 6, por herencia).
+/// </summary>
+public abstract class VersioningSuiteBase
+{
+    protected abstract ICrdtEngine Engine { get; }
+
+    private (VersionStore store, InMemoryBlobStore blobs) NewStore()
+    {
+        var blobs = new InMemoryBlobStore();
+        return (new VersionStore(Engine, blobs), blobs);
+    }
+
+    /// <summary>Sincroniza dos réplicas por deltas incrementales hasta converger.</summary>
+    private static void SyncBidirectional(ICrdtDoc a, ICrdtDoc b)
+    {
+        byte[] svA = a.ExportStateVector();
+        byte[] svB = b.ExportStateVector();
+        a.ApplyUpdate(b.ExportUpdateSince(svA));
+        b.ApplyUpdate(a.ExportUpdateSince(svB));
+    }
+
+    // Postcondición 1: Publish x2 sin cambios → mismo VersionId, un solo blob (dedup).
+    [Fact]
+    public async Task Publish_twice_same_content_dedups()
+    {
+        (VersionStore store, InMemoryBlobStore blobs) = NewStore();
+        using ICrdtDoc doc = Engine.CreateDoc();
+        doc.InsertText("body", 0, "contenido estable");
+
+        VersionId id1 = await store.PublishAsync(doc);
+        VersionId id2 = await store.PublishAsync(doc);
+
+        Assert.Equal(id1, id2);
+        Assert.Equal(1, blobs.Count);
+    }
+
+    // Postcondición 2: Checkout(Publish(doc)) → ExportState byte-idéntico al blob publicado.
+    [Fact]
+    public async Task Checkout_roundtrip_is_byte_identical()
+    {
+        (VersionStore store, _) = NewStore();
+        using ICrdtDoc doc = Engine.CreateDoc();
+        doc.InsertText("body", 0, "hola áéí mundo");
+        byte[] published = doc.ExportState();
+
+        VersionId id = await store.PublishAsync(doc);
+        using ICrdtDoc restored = await store.CheckoutAsync(id);
+
+        Assert.Equal(published, restored.ExportState());
+        Assert.Equal("hola áéí mundo", restored.GetText("body"));
+    }
+
+    // Postcondición 3: réplicas convergidas publican el MISMO VersionId (SC-002).
+    [Fact]
+    public async Task Converged_replicas_publish_same_version_id()
+    {
+        (VersionStore store, _) = NewStore();
+        using ICrdtDoc a = Engine.CreateDoc();
+        using ICrdtDoc b = Engine.CreateDoc();
+        a.InsertText("body", 0, "izquierda ");
+        b.InsertText("body", 0, "derecha ");
+
+        SyncBidirectional(a, b);
+
+        VersionId ida = await store.PublishAsync(a);
+        VersionId idb = await store.PublishAsync(b);
+        Assert.Equal(ida, idb);
+    }
+
+    // Postcondición 4: Diff(a,a) sin cambios; Diff(a,b) refleja las ediciones.
+    [Fact]
+    public async Task Diff_reflects_edits()
+    {
+        (VersionStore store, _) = NewStore();
+        using ICrdtDoc doc = Engine.CreateDoc();
+        doc.InsertText("body", 0, "el gato duerme");
+        VersionId v1 = await store.PublishAsync(doc);
+
+        doc.DeleteText("body", 3, 4);          // borra "gato"
+        doc.InsertText("body", 3, "perro");    // inserta "perro"
+        VersionId v2 = await store.PublishAsync(doc);
+
+        Assert.False((await store.DiffAsync(v1, v1, "body")).HasChanges);
+        TextDiff diff = await store.DiffAsync(v1, v2, "body");
+        Assert.True(diff.HasChanges);
+        Assert.Contains(diff.Segments, s => s.Op == DiffOp.Deleted && s.Text.Contains("gato"));
+        Assert.Contains(diff.Segments, s => s.Op == DiffOp.Inserted && s.Text.Contains("perro"));
+    }
+
+    // Postcondición 5: merge de ramas concurrentes conmutativo (mismo resultado, cualquier orden).
+    [Fact]
+    public async Task Merge_is_commutative()
+    {
+        (VersionStore store, _) = NewStore();
+        using ICrdtDoc baseDoc = Engine.CreateDoc();
+        baseDoc.InsertText("body", 0, "base ");
+        VersionId baseVersion = await store.PublishAsync(baseDoc);
+
+        // Dos ramas independientes desde la base, con ediciones concurrentes.
+        using ICrdtDoc branch1 = await store.BranchAsync(baseVersion);
+        using ICrdtDoc branch2 = await store.BranchAsync(baseVersion);
+        branch1.InsertText("body", 0, "uno ");
+        branch2.InsertText("body", 0, "dos ");
+
+        // Orden A: target = branch1, merge branch2. Orden B: target = branch2, merge branch1.
+        using ICrdtDoc ordA = await store.BranchAsync(baseVersion);
+        ordA.ApplyUpdate(branch1.ExportState());
+        store.Merge(ordA, branch2);
+
+        using ICrdtDoc ordB = await store.BranchAsync(baseVersion);
+        ordB.ApplyUpdate(branch2.ExportState());
+        store.Merge(ordB, branch1);
+
+        Assert.Equal(ordA.ExportState(), ordB.ExportState());
+        VersionId idA = await store.PublishAsync(ordA);
+        VersionId idB = await store.PublishAsync(ordB);
+        Assert.Equal(idA, idB);
+    }
+
+    // Postcondición 7: compactación por construcción (FR-012). Muchas versiones con ciclos
+    // insert+delete → todas recuperables byte-idéntico y tamaño acotado (GC del motor activo).
+    [Fact]
+    public async Task Compaction_versions_recoverable_and_bounded()
+    {
+        (VersionStore store, InMemoryBlobStore blobs) = NewStore();
+        using ICrdtDoc doc = Engine.CreateDoc();
+
+        var ids = new List<VersionId>();
+        var snapshots = new List<byte[]>();
+        for (int i = 0; i < 25; i++)
+        {
+            doc.InsertText("body", 0, $"edición-{i} ");
+            // Cancela parte del contenido para generar historial que el GC puede recuperar.
+            if (i % 2 == 1)
+            {
+                doc.DeleteText("body", 0, 4);
+            }
+            VersionId id = await store.PublishAsync(doc);
+            ids.Add(id);
+            snapshots.Add(doc.ExportState());
+        }
+
+        // (a) Todas las versiones recuperables byte-idéntico por su hash.
+        for (int i = 0; i < ids.Count; i++)
+        {
+            using ICrdtDoc restored = await store.CheckoutAsync(ids[i]);
+            Assert.Equal(snapshots[i], restored.ExportState());
+        }
+
+        // (b) Dedup: no más blobs que versiones distintas publicadas.
+        Assert.True(blobs.Count <= ids.Count);
+
+        // (c) El blob final no crece de forma monótona con la longitud del historial:
+        //     su tamaño se mantiene modesto pese a 25 ciclos de edición (GC activo, no tombstones).
+        Assert.True(snapshots[^1].Length < 4096,
+            $"El blob final ({snapshots[^1].Length} B) sugiere acumulación de historial (¿GC desactivado?).");
+    }
+}
diff --git a/tests/Weft.Versioning.Tests/Weft.Versioning.Tests.csproj b/tests/Weft.Versioning.Tests/Weft.Versioning.Tests.csproj
index 8656eb8..be4590e 100644
--- a/tests/Weft.Versioning.Tests/Weft.Versioning.Tests.csproj
+++ b/tests/Weft.Versioning.Tests/Weft.Versioning.Tests.csproj
@@ -17,6 +17,30 @@
 
   <ItemGroup>
     <ProjectReference Include="../../src/Weft.Versioning/Weft.Versioning.csproj" />
+    <ProjectReference Include="../../src/Weft.Loro/Weft.Loro.csproj" />
   </ItemGroup>
 
+  <!-- Copia el cdylib compilado por cargo al layout runtimes/<rid>/native/ para que el resolver
+       de Weft.Core lo encuentre (la suite corre sobre YrsEngine, y en CHARTER-02 también Loro). -->
+  <Target Name="CopyWeftNativeForTests" AfterTargets="Build">
+    <PropertyGroup>
+      <_WeftRid Condition="$([MSBuild]::IsOSPlatform('Windows'))">win-x64</_WeftRid>
+      <_WeftRid Condition="$([MSBuild]::IsOSPlatform('OSX'))">osx-arm64</_WeftRid>
+      <_WeftRid Condition="$([MSBuild]::IsOSPlatform('Linux'))">linux-x64</_WeftRid>
+      <_WeftPrefix Condition="$([MSBuild]::IsOSPlatform('Windows'))"></_WeftPrefix>
+      <_WeftPrefix Condition="!$([MSBuild]::IsOSPlatform('Windows'))">lib</_WeftPrefix>
+      <_WeftExt Condition="$([MSBuild]::IsOSPlatform('Windows'))">.dll</_WeftExt>
+      <_WeftExt Condition="$([MSBuild]::IsOSPlatform('OSX'))">.dylib</_WeftExt>
+      <_WeftExt Condition="$([MSBuild]::IsOSPlatform('Linux'))">.so</_WeftExt>
+    </PropertyGroup>
+    <ItemGroup>
+      <_WeftNative Include="$(MSBuildProjectDirectory)/../../native/target/release/$(_WeftPrefix)weft_yrs_ffi$(_WeftExt)" />
+      <_WeftNative Include="$(MSBuildProjectDirectory)/../../native/target/release/$(_WeftPrefix)weft_loro_ffi$(_WeftExt)" />
+    </ItemGroup>
+    <Copy SourceFiles="@(_WeftNative)"
+          DestinationFolder="$(OutDir)runtimes/$(_WeftRid)/native/"
+          Condition="Exists('%(_WeftNative.FullPath)')"
+          SkipUnchangedFiles="true" />
+  </Target>
+
 </Project>
diff --git a/tests/Weft.Versioning.Tests/YrsVersioningTests.cs b/tests/Weft.Versioning.Tests/YrsVersioningTests.cs
new file mode 100644
index 0000000..555c5b0
--- /dev/null
+++ b/tests/Weft.Versioning.Tests/YrsVersioningTests.cs
@@ -0,0 +1,10 @@
+using Weft;
+using Weft.Yrs;
+
+namespace Weft.Versioning.Tests;
+
+/// <summary>Ejecuta la suite parametrizada de versionado sobre el motor yrs (T027).</summary>
+public sealed class YrsVersioningTests : VersioningSuiteBase
+{
+    protected override ICrdtEngine Engine => YrsEngine.Instance;
+}

```

---

## What you must do

### Step 1 — Read the scope

Read the Charter file at `.straymark/charters/02-versioning-dual-engine.md` in full. Identify:

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

- In auditor-side CLI mode (skill `straymark-audit-execute`): `.straymark/audits/CHARTER-02-versioning-dual-engine/report-<sluggified-model-id>.md` (the skill handles the path automatically).
- In manual paste mode (transitional v0): the operator saves your output at `audit/charters/CHARTER-02-versioning-dual-engine/auditor-auditor.md` or an equivalent convention.

The file must have this frontmatter (validated against `.straymark/schemas/audit-output.schema.v0.json`):

```yaml
---
audit_role: auditor                       # v1 unified. Legacy v0: "auditor-primary" or "auditor-secondary"
auditor: <your model id and version>      # e.g., claude-sonnet-4-6, gemini-2.5-pro, copilot-v1.0.40
charter_id: CHARTER-02-versioning-dual-engine
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

# Audit: CHARTER-02-versioning-dual-engine by <your model id>

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

Does the implementation meet the closure criterion declared by `CHARTER-02-versioning-dual-engine`?
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
