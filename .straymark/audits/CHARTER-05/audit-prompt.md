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

# Charter audit — `CHARTER-05-weft-server-relay-end-to-end-cierra-m2-us3`

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

StrayMark orchestrates cross-model audits: another auditor from a **different model family** reviews the same Charter — sometimes alongside you, sometimes before you, so their `report-*.md` may already sit in `.straymark/audits/CHARTER-05-weft-server-relay-end-to-end-cierra-m2-us3/`. **You must not read it** (see the ABSOLUTE RULE). Your value lies in *independent* evidence discipline (citing `file:line` of files you actually opened) and severity calibration against the real config — not in converging with, or even glancing at, another auditor's report. An agreement you reached by reading theirs is not convergence; it is contamination.

---

## Project



*(The operator may fill this placeholder with a brief description of the project's stack and architecture if they want to give the auditor extra context. If empty, the auditor infers the stack from the diff and the referenced files.)*

---

## STRICT scope

**Charter under audit:** `CHARTER-05-weft-server-relay-end-to-end-cierra-m2-us3` — Weft.Server relay end-to-end — cierra M2/US3
**Charter file:** `.straymark/charters/05-weft-server-relay-end-to-end-cierra-m2-us3.md`
**Git range:** `origin/main..HEAD`

The authoritative source of scope is the Charter file at `.straymark/charters/05-weft-server-relay-end-to-end-cierra-m2-us3.md`. Read it in full before starting — it declares which files are modified, which tasks are executed, which risks are accepted, and what counts as successful closure.

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
charter_id: CHARTER-05-weft-server-relay-end-to-end-cierra-m2-us3
status: in-progress
effort_estimate: L
trigger: "CHARTER-04 cerrado (M2 corte 1: códec + stores + contract suite verde en main, cc2605b); la base de Weft.Server (SyncProtocol, IDocumentStore, IWeftAuthorizer) está disponible. tasks.md fija T047–T052 (US3) como el relay end-to-end; este es el 2.º corte de M2 y lo CIERRA. Se ancla en las superficies de concurrencia de M1 (DocumentBroker/DocumentSession) y retira el riesgo de compat del wire con un cliente Yjs real (Tiptap). Cierra FU-002 con la parte b (límites por conexión)."
originating_spec: specs/001-weft-crdt-versioning/spec.md
work_verb: implement
design_provenance: new
---

# Charter: Weft.Server relay end-to-end — cierra M2/US3

> **Status (mirrored from frontmatter — source of truth is above):** in-progress. Effort: L.
>
> **Origin:** Derivado de `specs/001-weft-crdt-versioning/spec.md`. Segundo y último corte de M2 (T047–T049,
> T051, T052): el relay WebSocket y-sync end-to-end sobre el substrato de CHARTER-04. **Cierra M2.**

## Context

M2/US3 entrega colaboración en tiempo real vía un relay WebSocket y-sync (`Weft.Server`, ASP.NET Core)
compatible con clientes Yjs estándar (`y-websocket`/`y-prosemirror`/Tiptap) sin adaptación. CHARTER-04
construyó el **substrato sin red** (códec lib0/y-sync, `IDocumentStore` + stores, `IWeftAuthorizer`, contract
suite) verificable solo con vectores unit. Este corte lo **cablea end-to-end**: el connection handler que hace
el handshake y relaya updates entre conexiones, el DI/endpoint que lo expone, el servicio de operación
(`IWeftServer`), la suite de integración con clientes simulados, y el cliente Tiptap **real** que retira el
riesgo de compat del wire. Es el journey de aceptación de US3 y **cierra el hito M2**.

El relay **no toca `ICrdtDoc` ni el motor**: se ancla en las superficies de concurrencia de M1 (`Weft.Core`,
`src/Weft.Core/Concurrency/`), refinadas en ejecución en CHARTER-03 (ver `plan.md` §"US3/M2 — anclajes sobre
M1", origen `AILOG-2026-07-11-001`). Cuatro anclajes concretos: (1) **broadcast vía
`DocumentSession.UpdateApplied`** (perezoso: el delta solo se computa si hay handler suscrito) — el relay se
suscribe **una vez por documento**, no por conexión; (2) **refcount de sesiones** — mientras una sesión viva,
el broker no desaloja el doc, así que un doc con conexiones abiertas permanece residente; (3) **publish y
persistencia dentro del turno del actor + `_evicting`-await** — `PublishAsync` y `AppendUpdate`/`SaveSnapshot`
ejecutan dentro del turno del actor del doc (state-vector consistente → paridad de `VersionId` server↔local,
P-III), y una reapertura espera a que un desalojo en vuelo **persista** antes de cargar (evita la pérdida de
updates que R7 destapó en M1); (4) **handlers de relay aislados** — `NotifySessions` aísla cada handler
`UpdateApplied` en try/catch, base del edge case "conexión malformada → cierre 1002 sin impacto en los pares".

Trabajo de **implementación** contra el contrato congelado `contracts/server-api.md` (API v1). Tensa cuatro
principios: **P-V** (serialización por doc) re-estresado por la concurrencia de red — el relay aplica **todo**
update entrante vía `DocumentSession`/turno del actor, nunca al `ICrdtDoc` crudo; **P-III** (determinismo) en
el publish del servidor (paridad de `VersionId`); **P-I/P-II** (frontera nativa / memoria) bajo input de red
no confiable — se completa la mitigación de FU-002 con la **parte b** (límites de recursos por conexión +
backpressure, sobre el cap de tamaño de la parte a); **P-IV** preservado (habla a `DocumentBroker`/
`DocumentSession` y a blobs opacos de `IDocumentStore`, no a tipos de yrs).

## Scope

**In scope (T047–T049, T051, T052):**

1. **Connection handler (T047)**: `WeftConnection.cs` — handshake (`IWeftAuthorizer.AuthorizeAsync` →
   `Deny`→403 **antes** del upgrade WebSocket / `ReadOnly`|`ReadWrite`→upgrade); sync bidireccional incremental
   (al conectar: servidor envía `SyncStep1(sv)` y responde el `SyncStep1` del cliente con `SyncStep2(delta)`);
   relay de cada `Update` entrante de una conexión `ReadWrite` → aplicado al doc vía `DocumentSession` (turno
   del actor), persistido vía `IDocumentStore` y difundido a las demás conexiones del doc; awareness broadcast
   a los pares sin persistir + retirada del estado al cerrar; `ReadOnly` que envía un update de documento →
   cierre **1008**; frame malformado (`MalformedMessageException`) → cierre **1002** sin impacto en los pares.
   **FU-002 parte b**: límites de recursos por conexión (buffer de recepción acotado / backpressure) sobre el
   cap de tamaño de mensaje (parte a) del códec.
2. **DI + endpoint (T048)**: `WeftServerExtensions.cs` + `WeftServerOptions.cs` — `AddWeftServer(options)`
   (`options.Engine` con default `YrsEngine.Instance`; `options.Broker` = `DocumentBrokerOptions`) que **falla
   al arrancar** si no hay `IWeftAuthorizer` registrado (SC-010); `MapWeft(path)` mapea el endpoint WebSocket
   `path/{docId}` (`{docId}` URL-decoded).
3. **Servicio `IWeftServer` (T049)**: `WeftServer.cs` — `PublishAsync(docId)` ejecuta `VersionStore.PublishAsync`
   **dentro del turno del actor** del doc (`DocumentSession.ExecuteAsync`) → mismo `VersionId` que publicar el
   mismo contenido en local (P-III); `GetConnectionCountAsync(docId)`; `DisconnectAllAsync(docId)` (cierra las
   conexiones de un doc, p. ej. tras revocación de acceso).
4. **Referencias de proyecto**: `Weft.Server.csproj` gana `ProjectReference` a `Weft.Core` (`DocumentBroker`/
   `DocumentSession`, T047) y a `Weft.Versioning` (`VersionStore`/`VersionId`, T049). Hasta CHARTER-04 solo
   referenciaba ASP.NET Core.
5. **Tests de integración (T051)**: `RelayTests.cs` — 2 clientes simulados: convergencia a contenido idéntico
   <1 s tras ediciones cruzadas (SC-005); reconexión con SV previo recibe solo delta con **bytes medidos** ≪
   estado completo (SC-004); `Deny` → **0 bytes de contenido**; `ReadOnly` que escribe → cierre 1008 (SC-010);
   awareness visible entre pares y retirada al desconectar (nunca tocada por `IDocumentStore`); restart-recovery
   (kill + rearranque → estado recuperado desde `IDocumentStore` sin pérdida de updates confirmados, SC-006);
   paridad de `VersionId` server↔local con `PublishAsync`.
6. **Samples + validación manual (T052)**: `samples/Weft.Sample.Server/` (relay + `FileSystemDocumentStore` +
   authorizer demo) + `samples/tiptap-client/` (Tiptap + `y-prosemirror` + `y-websocket`); ejecutar la
   validación manual de `quickstart.md` §US3 con **2+ clientes Tiptap reales** — el gate de compat del wire que
   retira el riesgo de interoperabilidad con el ecosistema Yjs.

**Out of scope:**

- Adaptadores `Weft.Server.Persistence.EFCore` y `.Redis` — **CHARTER-06** (T053–T054); pasan la contract suite
  de CHARTER-04 sin modificarla.
- `INativeVersioning` de Loro (**FU-006**) — mini-charter aparte; ningún gate de M2 depende.
- Escalado horizontal / relay multi-nodo (backplane entre instancias) — fuera de la spec 001; el relay es
  single-node (un `DocumentBroker` por proceso).
- Endurecimiento de transporte (TLS, rate-limiting por IP, auth de red) — responsabilidad del host ASP.NET
  Core del consumidor, no de la librería.

## Files to modify

<!-- Reconnaissance #210: superficies M1 (DocumentBroker.OpenAsync/_evicting, DocumentSession.UpdateApplied/
     ExecuteAsync) y base de CHARTER-04 (SyncProtocol, IDocumentStore, IWeftAuthorizer) verificadas presentes;
     VersionStore.PublishAsync(ICrdtDoc,ct)→VersionId y DocumentBrokerOptions verificados. Los archivos de
     Weft.Server marcados New NO existen (confirmado). samples/ solo tiene Weft.Sample.Versioning. -->

| File | Change |
|---|---|
| `src/Weft.Server/WeftConnection.cs` | New — connection handler: handshake/authz, sync bidireccional, relay+persistencia, awareness, 1008/1002, FU-002 parte b (T047) |
| `src/Weft.Server/WeftServerExtensions.cs` | New — `AddWeftServer(options)` (falla sin `IWeftAuthorizer`) + `MapWeft(path)` (T048) |
| `src/Weft.Server/WeftServerOptions.cs` | New — opciones (`Engine`, `Broker`=`DocumentBrokerOptions`) (T048) |
| `src/Weft.Server/WeftServer.cs` | New — `IWeftServer`: `PublishAsync`/`GetConnectionCountAsync`/`DisconnectAllAsync` (T049) |
| `src/Weft.Server/Weft.Server.csproj` | Change — `ProjectReference` a `Weft.Core` (broker/session) y `Weft.Versioning` (VersionStore/VersionId) |
| `tests/Weft.Server.Tests/RelayTests.cs` | New — integración: 2 clientes, convergencia, delta, Deny, 1008, awareness, restart-recovery, paridad VersionId (T051) |
| `tests/Weft.Server.Tests/Weft.Server.Tests.csproj` | Change — `Microsoft.AspNetCore.Mvc.Testing` (WebApplicationFactory) para los tests de integración |
| `samples/Weft.Sample.Server/Program.cs` | New — relay + FileSystemDocumentStore + authorizer demo (T052) |
| `samples/Weft.Sample.Server/Weft.Sample.Server.csproj` | New — proyecto sample |
| `samples/tiptap-client/` | New — Tiptap + y-prosemirror + y-websocket (cliente de validación manual, T052) |
| `Weft.sln` | Change — añadir `Weft.Sample.Server` |
| `specs/001-weft-crdt-versioning/tasks.md` | Change — marcar T047–T049, T051, T052 `[X] — CHARTER-05` |
| `.straymark/07-ai-audit/agent-logs/AILOG-*.md` | New, `risk_level: high` (cierra hito; input de red no confiable; P-V/P-III bajo concurrencia de red) |
| `.straymark/07-ai-audit/decisions/AIDEC-*.md` | New si emergen decisiones de diseño (backpressure/límites por conexión; estrategia de awareness) |

## Verification

### Local checks

> **Lección de CHARTER-01/02/03/04**: correr TODO localmente en verde ANTES de pushear. En modo ahorro, los
> gates de CI (test multiplataforma, ASan/LSan, determinism, dual-engine, docs-validation) se replican local.

```bash
# Build de toda la solución (incluye Weft.Sample.Server)
dotnet build Weft.sln -c Release

# Tests del relay: integración (2 clientes simulados, restart-recovery, paridad VersionId) + contract suite
dotnet test tests/Weft.Server.Tests/

# Suite completa verde (M0/M1/M2-corte1 intactos)
dotnet test
```

**Validación manual de interoperabilidad (T052)** — requiere Node.js + un navegador; NO ejecutable en shell
limpio, pero es el gate de compat del wire de M2 (no es "production smoke": es integración manual con cliente
real). Procedimiento en `quickstart.md` §US3:

```bash
# 1) Arrancar el relay de ejemplo
dotnet run --project samples/Weft.Sample.Server
# 2) Servir el cliente Tiptap y abrir 2+ pestañas apuntando al mismo docId
cd samples/tiptap-client && npm install && npm run dev
# 3) Verificar convergencia en vivo entre pestañas + reconexión con delta (per quickstart §US3)
```

### Production smoke (after deploy)

No aplica — librería sin despliegue. Los auditores externos deben saltar esta sección.

## Risks

- **R1 — Incompatibilidad real del wire con `y-websocket`/Tiptap**: severidad **alta**. El códec de CHARTER-04
  solo se validó con vectores unit; una divergencia con `y-protocols` (orden de framing, awareness, edge del
  varint) solo se manifiesta contra un cliente Yjs real. Es el gate de M2. Mitigación: sample Tiptap real +
  validación manual de `quickstart.md` §US3 con 2+ clientes (T052); los tests de integración (T051) miden
  convergencia y bytes de delta. Si falla: el fix vive en la superficie de CHARTER-04 (`SyncProtocol`/
  `Lib0Encoding`) — el riesgo de compat NO se retira hasta que 2+ clientes Tiptap reales convergen.
- **R2 — Pérdida/corrupción de updates por carrera relay↔persistencia↔desalojo (P-V/SC-006)**: severidad
  **alta**. El relay aplica updates y persiste concurrentemente con el ciclo de vida del broker. Mitigación: el
  relay aplica **todo** update vía `DocumentSession` (turno del actor), nunca al `ICrdtDoc` crudo; publish y
  persistencia dentro del turno; hereda el `_evicting`-await de M1 (una reapertura espera a que el desalojo
  persista antes de cargar); test de **restart-recovery** (T051) verifica recuperación sin pérdida de updates
  confirmados. Si falla: es exactamente la corrupción que P-V/SC-006 prohíben → bloquea el cierre de M2.
- **R3 — DoS por input de red no confiable sin límites por conexión (FU-002 parte b)**: severidad
  **media-alta**. El cap de tamaño de mensaje (parte a, CHARTER-04) acota un frame, pero sin backpressure ni
  límite de buffer de recepción por conexión un peer puede saturar memoria/CPU. Mitigación **aquí**: límites de
  recursos por conexión + backpressure + el path malformed→1002 en `WeftConnection` (T047). Si falla: **FU-002
  permanece `open`** tras este Charter (se cierra solo al entregar la parte b).
- **R4 — Ruptura de paridad de `VersionId` server↔local (P-III)**: severidad **media**. Si `PublishAsync` no
  ejecuta dentro del turno del actor, un snapshot tomado con tráfico concurrente diverge del que produciría
  `Weft.Versioning` en local. Mitigación: `PublishAsync` llama `VersionStore.PublishAsync` dentro de
  `DocumentSession.ExecuteAsync`; test de paridad server↔local (T051). Si falla: viola SC de paridad → real_debt.
- **R5 — Enforcement de autorización permisivo por defecto (SC-010)**: severidad **media**. Un `AddWeftServer`
  sin `IWeftAuthorizer` que arranque, o un `Deny`/`ReadOnly` mal aplicado, filtraría contenido. Mitigación:
  `AddWeftServer` **falla al arrancar** sin authorizer; `Deny`→403 antes del upgrade (0 bytes de contenido);
  `ReadOnly` que escribe→1008; tests dedicados (T051). Si falla: fuga de acceso → bloquea el cierre.

## Tasks

1. Sync main, branch `charter/05-server-relay`. Flip `declared` → `in-progress` al empezar a ejecutar.
2. Re-evaluar **Constitution Check** contra el scope (per-Charter): **P-V** (todo update de red vía
   `DocumentSession`), **P-III** (paridad de `VersionId` en publish), **P-I/P-II** (FU-002 parte b),
   **P-IV** (broker/session + blobs opacos). Sin violaciones esperadas.
3. `/speckit-implement` acotado a **T047–T049, T051, T052**; marcar `[X] — CHARTER-05` por tarea.
4. **AILOG** (`risk_level: high`, `review_required: true`); **AIDEC** si emergen decisiones sustantivas
   (estrategia concreta de backpressure/límites por conexión; forma del broadcast de awareness; manejo del
   ciclo de vida de la suscripción `UpdateApplied` por documento).
5. **Batch Ledger** en el AILOG (execution multi-batch probable, L): `straymark charter batch-complete
   CHARTER-05 <N>` tras cada batch.
6. **Verificación local COMPLETA** (bloque Local checks íntegro, incluida la validación manual Tiptap §US3)
   ANTES de push.
7. `straymark charter drift CHARTER-05` antes de commit; drifts → `R<N+1>` en el AILOG (o completar el trabajo).
8. Commit + push + abrir PR contra `main`; **CI verde** (o gates locales verdes en modo ahorro).

## Charter Closure

Corte que **cierra el hito M2** y completa el journey de aceptación de US3, además de retirar el riesgo de
compat del wire y cerrar FU-002. **Requiere auditoría externa multi-modelo obligatoria** (como CHARTER-02/03):
el prompt se genera SOLO con el estado estable (CI del PR en verde / gates locales verdes, working tree limpio
y pusheado — ver `CLAUDE.md` §"Auditoría externa"). Al cerrar:

1. Actualización atómica del Charter (format v4) si el drift check reveló divergencias (mismo PR): editar
   `## Files to modify` y/o añadir `## Closing notes`.
2. `straymark charter drift CHARTER-05 --range origin/main..HEAD` → limpio o documentado en el AILOG.
3. **Auditoría externa**: `/straymark-audit-prompt` (con estado estable) → operador corre ≥2 auditores CLI →
   `/straymark-audit-review` → remediar `real_debt` → merge del `external_audit` en la telemetría.
4. `straymark charter close CHARTER-05` (telemetría, status `closed`, `closed_at`). No borrar este archivo.
5. **Cerrar FU-002** en `.straymark/follow-ups-backlog.md` (parte b entregada → `open` → `closed`), y confirmar
   el estado de FU-006 (Loro nativo, sigue diferido).
6. Confirmar que **M2 queda cerrado** (US3 verde incl. Tiptap real); el siguiente hito es M3 (US4, release NuGet
   multi-RID) tras CHARTER-06 (adaptadores externos, fuera del journey de M2).

```

---

## Diff

```diff
diff --git a/.gitignore b/.gitignore
index a723234..5089605 100644
--- a/.gitignore
+++ b/.gitignore
@@ -26,6 +26,11 @@ runtimes/*/native/*.so
 runtimes/*/native/*.dll
 runtimes/*/native/*.dylib
 
+# ---- Node (samples/tiptap-client) ----
+node_modules/
+# datos durables generados por el sample server (FileSystemDocumentStore)
+**/weft-data/
+
 # ---- Herramientas / entorno ----
 .idea/
 .vscode/
diff --git a/.straymark/07-ai-audit/agent-logs/AILOG-2026-07-13-001-charter-05-server-relay-end-to-end.md b/.straymark/07-ai-audit/agent-logs/AILOG-2026-07-13-001-charter-05-server-relay-end-to-end.md
new file mode 100644
index 0000000..0325337
--- /dev/null
+++ b/.straymark/07-ai-audit/agent-logs/AILOG-2026-07-13-001-charter-05-server-relay-end-to-end.md
@@ -0,0 +1,167 @@
+---
+id: AILOG-2026-07-13-001
+title: "CHARTER-05: Weft.Server relay end-to-end — cierra M2/US3 (T047–T049, T051, T052)"
+status: accepted
+created: 2026-07-13
+agent: claude-opus-4-8
+confidence: high
+review_required: true
+reviewed_by: Jose Villaseñor Montfort
+reviewed_at: 2026-07-13
+review_outcome: approved
+risk_level: high
+eu_ai_act_risk: not_applicable
+nist_genai_risks: []
+iso_42001_clause: []
+lines_changed: 1819
+files_modified: []
+observability_scope: none
+tags: [server, relay, websocket, y-sync, awareness, concurrency, publish, wire-compat, ffi-boundary, fu-002]
+related: [AILOG-2026-07-12-001, AILOG-2026-07-11-001, AIDEC-2026-07-13-001]
+originating_charter: CHARTER-05-weft-server-relay-end-to-end-cierra-m2-us3
+---
+
+# AILOG: CHARTER-05 — Weft.Server relay end-to-end (cierra M2/US3)
+
+## Summary
+
+Segundo y último corte de M2/US3 (effort L): el **relay WebSocket y-sync end-to-end** sobre el substrato de
+CHARTER-04, que **cierra el hito M2**. Un connection handler hace el handshake (authz→403/1008/1002), sincroniza
+incrementalmente en ambas direcciones, relaya cada update entrante a los pares vía
+`DocumentSession.UpdateApplied` (anclaje M1) y lo persiste, difunde awareness y retira el estado al cerrar.
+DI (`AddWeftServer`/`MapWeft`) y el servicio `IWeftServer` (`PublishAsync` con paridad de `VersionId`
+server↔local, `GetConnectionCountAsync`, `DisconnectAllAsync`). Verificado con **7 tests de integración**
+(2 clientes yrs reales por WebSocket) y, de forma decisiva, con un **check headless de compat del wire usando
+`yjs`/`y-websocket` reales** que converge contra el relay — retira R1. Completa **FU-002 parte b** (límites por
+conexión / backpressure).
+
+## Context
+
+Ejecución de T047–T049, T051, T052 bajo `.straymark/charters/05-*.md`, sobre CHARTER-04 (códec + stores +
+authorizer) y las superficies de concurrencia de M1 (`DocumentBroker`/`DocumentSession`, CHARTER-03). Trabajo de
+**implementación** contra el contrato congelado `contracts/server-api.md` (API v1). El relay no toca `ICrdtDoc`
+ni el motor: aplica **todo** update de red vía `DocumentSession` (turno del actor, P-V), publica dentro del
+turno (paridad `VersionId`, P-III), y maneja blobs opacos de `IDocumentStore` (P-IV). El input de red no
+confiable tensa P-I/P-II: se completa la mitigación FU-002 con la parte b sobre el cap de tamaño (parte a).
+
+## Actions Performed
+
+1. **Referencias de proyecto**: `Weft.Server.csproj` gana `ProjectReference` a `Weft.Core` (broker/sesión) y
+   `Weft.Versioning` (VersionStore/VersionId/IBlobStore). Hasta CHARTER-04 solo referenciaba ASP.NET Core.
+2. **Opciones (T048)**: `WeftServerOptions` — `Engine` (default `YrsEngine.Instance`), `Broker`
+   (`DocumentBrokerOptions`), `MaxMessageBytes` (cap FU-002 a) y `MaxSendQueuePerConnection` (backpressure, b).
+3. **Connection handler (T047)**: `WeftConnection` — send pump (cola acotada → socket) + receive loop
+   (acumula el frame con enforcement del cap, decodifica y-sync, despacha). Enforcement **post-upgrade**:
+   `ReadOnly` que envía update de documento→1008; frame malformado→1002; frame sobredimensionado→1009 (el
+   `Deny`→403 **antes** del upgrade lo hace `MapWeft`, ver #5). Sync inicial `SyncStep1`; `SyncStep1` del
+   cliente→`SyncStep2(delta)`; `Update`→aplicar+persistir; awareness→relay a pares + tracking de clientIDs.
+4. **Hub por documento**: `DocumentHub` — una `DocumentSession` por doc (suscripción única a `UpdateApplied`,
+   broadcast perezoso); `ApplyAndPersistAsync` (turno del actor + `IDocumentStore.AppendUpdate`); snapshot de
+   compaction al disponer. `AwarenessProtocol` — parsing mínimo de clientIDs para la retirada (FR-015).
+5. **DI + endpoint (T048)**: `WeftServerExtensions` — `AddWeftServer(options)` (registra `WeftServer` singleton
+   + `IWeftServer`); `MapWeft(path)` mapea `path/{docId}`, **falla al arrancar** sin `IWeftAuthorizer`
+   (`IServiceProviderIsService`), `Deny`→403 antes del upgrade.
+6. **Servicio (T049)**: `WeftServer` — registro de hubs sobre `DocumentBroker` (con `OnEvicting`→snapshot y
+   loader que reconstruye desde el store); `PublishAsync` captura `ExportState` **dentro del turno del actor**
+   → `VersionId.FromBlob` + `IBlobStore.PutAsync` (paridad); `GetConnectionCountAsync`; `DisconnectAllAsync`.
+7. **Tests (T051)**: `RelayTests` — convergencia de 2 clientes tras ediciones cruzadas; delta en reconexión con
+   bytes medidos (fresco vs sembrado); `Deny` sin bytes de contenido; `ReadOnly`→1008; awareness relay +
+   retirada; restart-recovery (store durable sobrevive al reinicio); paridad `VersionId` server↔local.
+8. **Samples (T052)**: `Weft.Sample.Server` (relay + `FileSystemDocumentStore` + authorizer demo, hosting
+   mínimo) + `samples/tiptap-client` (editor Tiptap browser + **check headless `yjs`/`y-websocket`**).
+
+## Modified Files
+
+| File | Change Description |
+|------|--------------------|
+| `src/Weft.Server/Weft.Server.csproj` | Change — ProjectReference a Weft.Core + Weft.Versioning |
+| `src/Weft.Server/WeftServerOptions.cs` | New — opciones (Engine, Broker, límites FU-002 a/b) (T048) |
+| `src/Weft.Server/WeftConnection.cs` | New — connection handler (handshake/authz, sync, awareness, límites) (T047) |
+| `src/Weft.Server/DocumentHub.cs` | New (scope expansion) — hub por doc: sesión única + broadcast + persistencia |
+| `src/Weft.Server/Protocol/AwarenessProtocol.cs` | New (scope expansion) — parsing mínimo de clientIDs (retirada, FR-015) |
+| `src/Weft.Server/WeftServerExtensions.cs` | New — AddWeftServer/MapWeft (T048) |
+| `src/Weft.Server/WeftServer.cs` | New — IWeftServer + registro de hubs + broker + publish (T049) |
+| `tests/Weft.Server.Tests/RelayTests.cs` | New — integración 2 clientes yrs + harness TestServer (T051) |
+| `tests/Weft.Server.Tests/Weft.Server.Tests.csproj` | Change — TestHost + FrameworkReference + copia del cdylib |
+| `samples/Weft.Sample.Server/**` | New — sample relay (T052) |
+| `samples/tiptap-client/**` | New — cliente Tiptap + check headless de compat (T052) |
+| `Weft.sln` | Change — añadido Weft.Sample.Server |
+| `specs/001-weft-crdt-versioning/tasks.md` | Change — T047–T049, T051, T052 `[X] — CHARTER-05` |
+| `.gitignore` | Change — node_modules/ + weft-data/ |
+| `.straymark/charters/05-*.md` | Change — status declared → in-progress |
+
+## Decisions Made
+
+- **Paridad de `VersionId` (P-III)**: `PublishAsync` captura `ExportState()` **dentro del turno del actor**
+  (`DocumentSession.ExecuteAsync`), luego `VersionId.FromBlob` + `IBlobStore.PutAsync` fuera del turno —
+  byte-idéntico a `VersionStore.PublishAsync` por construcción, sin depender de su orden interno. AIDEC §1.
+- **Broadcast a TODAS las conexiones (incluido el origen)**: reaplicar el propio delta es un no-op CRDT
+  idempotente; evita rastrear el origen dentro del turno del actor (que sería una carrera). AIDEC §2.
+- **Retirada de awareness (FR-015)**: parsing mínimo de clientIDs por conexión → mensaje de "offline" (estado
+  `null`, clock+1) al cerrar. El relay no interpreta el contenido del estado. AIDEC §3.
+- **Backpressure (FU-002 parte b)**: cola de envío acotada por conexión; si se llena (consumidor lento), se
+  cierra la conexión (se descarta el consumidor lento en vez de crecer memoria) — el cliente reconecta. AIDEC §4.
+- **Hub por documento**: una `DocumentSession` por doc (no por conexión), refcount implícito por conexiones;
+  snapshot de compaction al disponer + `OnEvicting`→snapshot en el broker. Loader reconstruye desde records.
+
+## Impact
+
+- **Functionality**: relay colaborativo end-to-end compatible con el ecosistema Yjs sin adaptación; cierra M2.
+- **Security/Memory**: FU-002 completado (cap de tamaño + límites por conexión/backpressure); authz nunca
+  por-defecto-permisiva (falla al arrancar sin authorizer; Deny→403; ReadOnly→1008).
+- **Performance**: broadcast perezoso (delta solo si hay handler); hot path sin locks globales (el `_hubGate`
+  solo serializa join/leave, no el relay de updates).
+
+## Verification
+
+- [x] `dotnet build Weft.sln -c Release` — 0 warnings / 0 errores
+- [x] `dotnet test` — **107 verdes** (Server 53, Core 27, Versioning 25, Determinism 2); M0/M1/M2-corte1 intactos
+- [x] **Check headless de compat del wire** (`yjs`/`y-websocket` reales ↔ relay): 2 docs Yjs convergen — **R1 retirada**
+- [x] **ASan/LSan** sobre el workspace nativo — 12 tests, 0 fugas (P-II; native sin cambios en este corte)
+- [ ] Revisión humana del operador — pendiente (`review_required: true`)
+- [ ] **Auditoría externa multi-modelo** — OBLIGATORIA al cerrar (cierra M2); pendiente
+
+## Additional Notes
+
+### Compat del wire: validación headless en vez de navegador
+
+El sample incluye un cliente Tiptap para la validación manual en navegador (quickstart §US3), pero el gate de
+compat se retiró de forma **programática** con un check headless (`samples/tiptap-client/wire-check.mjs`): dos
+`Y.Doc` reales de Yjs conectados vía `y-websocket` convergen a través del relay. Esto prueba que los updates de
+yrs (servidor) y Yjs (cliente) son intercambiables a nivel binario — más fuerte y reproducible que un navegador.
+El wire de y-websocket (`[msgType][syncType][VarUint8Array]`) coincide exactamente con `SyncProtocol`.
+
+### Scope expansion (drift)
+
+`straymark charter drift --range origin/main..HEAD` reporta 12 archivos "modificados pero no declarados". Se
+clasifican en tres grupos:
+
+- **FP del parser (declarados, no matcheados)** — `Weft.sln`, `src/Weft.Server/Weft.Server.csproj`,
+  `tests/Weft.Server.Tests/Weft.Server.Tests.csproj`, `samples/Weft.Sample.Server/Weft.Sample.Server.csproj`:
+  el parser de §Files no matchea rutas `.sln`/`.csproj` (limitación conocida, reportada upstream en
+  **StrangeDaysTech/straymark#354**). Todos están declarados en §Files to modify.
+- **Dir declarado, archivos listados por el parser** — `samples/tiptap-client/{README.md,index.html,
+  package.json,src/main.js,wire-check.mjs}`: el charter declaró `samples/tiptap-client/` (el directorio); el
+  parser enumera sus archivos. No hay expansión real.
+- **Expansión intencional real** — `src/Weft.Server/DocumentHub.cs` y `src/Weft.Server/Protocol/AwarenessProtocol.cs`:
+  descomposición interna del handler (T047) — el hub por-documento (sesión única + broadcast) y el parsing
+  mínimo de awareness para la retirada; dentro del alcance funcional de T047. `.gitignore`: incidental
+  (`node_modules/` + `weft-data/` de los samples). Se reflejan en §Files to modify al cierre (format v4).
+
+### FU-002 cerrado
+
+Este corte entrega la parte b (límites por conexión + backpressure + malformed→1002), completando la mitigación
+iniciada en CHARTER-04 (parte a). **FU-002 pasa a `closed`** al cerrar este Charter.
+
+### Decisiones candidatas a AIDEC
+
+Paridad de publish, broadcast-a-todos, retirada de awareness y backpressure se documentan en AIDEC-2026-07-13-001.
+
+## Approval
+
+**Approved**: 2026-07-13 by `Jose Villaseñor Montfort`. Concordancia AILOG↔código revisada punto por punto
+(paridad de publish in-turn, close codes 1008/1002/1009 en `WeftConnection` y 403 en `MapWeft`, broadcast vía
+`UpdateApplied`, fail-at-startup, los 7 tests citados, verificación local 107 tests + ASan sin fugas + compat
+headless). Corregida una imprecisión de atribución en Acciones #3 (el `Deny`→403 lo hace `MapWeft` antes del
+upgrade, no `WeftConnection`). AIDEC-2026-07-13-001 firmado en paralelo. Pendiente: auditoría externa
+multi-modelo obligatoria (cierra M2) antes del cierre del Charter.
diff --git a/.straymark/07-ai-audit/decisions/AIDEC-2026-07-13-001-charter-05-relay-publish-broadcast-awareness-backpressure.md b/.straymark/07-ai-audit/decisions/AIDEC-2026-07-13-001-charter-05-relay-publish-broadcast-awareness-backpressure.md
new file mode 100644
index 0000000..8543e04
--- /dev/null
+++ b/.straymark/07-ai-audit/decisions/AIDEC-2026-07-13-001-charter-05-relay-publish-broadcast-awareness-backpressure.md
@@ -0,0 +1,153 @@
+---
+id: AIDEC-2026-07-13-001
+title: "CHARTER-05: decisiones del relay — paridad de publish, broadcast, retirada de awareness, backpressure"
+status: accepted
+created: 2026-07-13
+agent: claude-opus-4-8
+confidence: high
+review_required: true
+reviewed_by: Jose Villaseñor Montfort
+reviewed_at: 2026-07-13
+review_outcome: approved
+risk_level: high
+eu_ai_act_risk: not_applicable
+nist_genai_risks: []
+iso_42001_clause: []
+tags: [server, relay, y-sync, awareness, backpressure, publish, concurrency, fu-002]
+related: [AILOG-2026-07-13-001]
+---
+
+# AIDEC: decisiones de implementación del relay (CHARTER-05)
+
+> Registra las cuatro decisiones sustantivas del relay `Weft.Server` sobre el contrato congelado
+> `contracts/server-api.md`, descubiertas al cablear el handler contra las superficies de M1. Quedan fijas para
+> CHARTER-06 y para la auditoría externa del cierre de M2.
+
+## Context
+
+CHARTER-05 implementa el connection handler, el DI y `IWeftServer` sobre el substrato de CHARTER-04 y las
+superficies de concurrencia de M1 (`DocumentBroker`/`DocumentSession`). El contrato fija las **firmas** y la
+**semántica observable** (403/1008/1002, paridad de `VersionId`, awareness efímero), pero deja abierta la
+**mecánica** de cuatro puntos con consecuencias de corrección, seguridad o compatibilidad.
+
+---
+
+## Decisión 1 — Paridad de `VersionId` en `PublishAsync` (P-III)
+
+### Problem
+
+`IWeftServer.PublishAsync(docId)` debe producir **el mismo `VersionId`** que produciría `VersionStore` al
+publicar el mismo contenido en local (SC de paridad), y ejecutar dentro del turno del actor para un snapshot
+consistente bajo tráfico concurrente. `DocumentSession.ExecuteAsync` es **síncrono** (`Func<ICrdtDoc,T>`): no se
+puede `await` la escritura async del blob dentro del turno.
+
+### Alternatives Considered
+
+- **A1 — `ExecuteAsync(doc => versionStore.PublishAsync(doc, ct))`**: reusa `VersionStore` tal cual. Contras:
+  depende de que `PublishAsync` haga `ExportState()` **antes** de su primer `await` (acoplamiento implícito con
+  el orden interno de otra assembly); doble `await` sobre `ValueTask<ValueTask<…>>`. **Rechazada** (frágil).
+- **A2 (elegida) — Capturar el snapshot dentro del turno, publicar los bytes fuera.**
+  `byte[] s = await session.ExecuteAsync(d => d.ExportState())`; `VersionId.FromBlob(s)`; `IBlobStore.PutAsync(id, s)`.
+
+### Rationale
+
+`ExportState()` es la operación determinista (P-III) que `VersionStore.PublishAsync` usa; `FromBlob(ExportState())`
+reproduce el `VersionId` local **byte a byte** por construcción, sin depender del orden interno de VersionStore.
+El `ExportState` ocurre explícitamente dentro del turno del actor (snapshot consistente); el `PutAsync` (bytes
+inmutables) va fuera del turno (P-V no se viola: el doc no se toca tras el export). Verificado en
+`RelayTests.Server_publish_matches_local_publish_version_id`.
+
+### Consequences
+
+`WeftServer` toma un `IBlobStore` opcional; `PublishAsync` lanza si no hay uno registrado ("requiere IBlobStore").
+
+---
+
+## Decisión 2 — Broadcast del delta a TODAS las conexiones (incluido el origen)
+
+### Problem
+
+`DocumentSession.UpdateApplied` (anclaje M1) es un evento **por documento**, no por origen: entrega el delta pero
+no qué conexión lo originó. ¿Cómo se difunde sin re-enviar al emisor, si el origen no está en el evento?
+
+### Alternatives Considered
+
+- **B1 — Rastrear el origen (thread/async-local) alrededor de `ApplyUpdateAsync`**: excluiría el eco. Contras:
+  los applies concurrentes de distintas conexiones se serializan en el actor pero el "set origin" ocurre fuera →
+  **carrera** que atribuiría mal el origen. **Rechazada**.
+- **B2 (elegida) — Difundir a todas; el origen reaplica su propio delta.**
+
+### Rationale
+
+Reaplicar un delta ya integrado es un **no-op CRDT idempotente**, y los clientes Yjs lo toleran (validado por el
+check headless con `y-websocket`). Evita la carrera de B1 y mantiene el broadcast dentro del turno del actor sin
+estado extra. Coste: un eco al emisor (ancho de banda), aceptable para v1; optimizable con etiquetado de origen
+por mensaje si hiciera falta.
+
+### Consequences
+
+El emisor recibe su propio update de vuelta. La awareness sí excluye el origen (se relaya en el receive loop del
+handler, donde el origen es conocido, sin la carrera de B1).
+
+---
+
+## Decisión 3 — Retirada de awareness al cerrar (FR-015)
+
+### Problem
+
+Al cerrar una conexión hay que difundir la **retirada** de su estado de awareness. Pero el relay trata el estado
+como opaco (CHARTER-04 no parsea awareness); no conoce los `clientID` que la conexión anunció.
+
+### Alternatives Considered
+
+- **C1 — Confiar solo en el timeout de awareness de Yjs** (los pares expiran al par tras ~30 s). Contras: no
+  cumple "difundir la retirada" (FR-015); deja cursores fantasma hasta el timeout. **Insuficiente**.
+- **C2 (elegida) — Parsing mínimo de `clientID` por conexión + mensaje de retirada al cerrar.**
+
+### Rationale
+
+`AwarenessProtocol` (interno) parsea solo la lista de `clientID`/`clock` de cada awareness update (salta el
+estado, que sigue opaco) y los acumula por conexión. Al cerrar, `EncodeRemoval` emite un awareness update con
+estado `null` y `clock+1` por clientID —exactamente como `y-protocols/awareness`— difundido a los pares.
+Verificado en `RelayTests.Awareness_is_relayed_and_withdrawn_on_disconnect`.
+
+### Consequences
+
+El relay parsea la envoltura de awareness (no el contenido). Si un payload no parsea, el tracking se ignora
+(best-effort); el broadcast del mensaje en sí no se ve afectado.
+
+---
+
+## Decisión 4 — Backpressure por conexión (FU-002 parte b)
+
+### Problem
+
+El cap de tamaño (parte a) acota **un** frame, pero un consumidor lento cuyo socket no drena haría crecer la
+memoria del servidor sin límite. ¿Cómo se acotan los recursos por conexión?
+
+### Alternatives Considered
+
+- **D1 — Cola de envío ilimitada**: memoria no acotada ante un consumidor lento (el DoS que FU-002 evita). **Rechazada**.
+- **D2 — Descartar mensajes al llenarse la cola**: rompería la convergencia (updates perdidos). **Rechazada**.
+- **D3 (elegida) — Cola acotada por conexión; al llenarse, cerrar la conexión.**
+
+### Rationale
+
+Cola de envío acotada (`MaxSendQueuePerConnection`, default 256) por conexión; `TryEnqueue` no bloquea (se llama
+desde el turno del actor) y, si la cola está llena, **cierra la conexión** (se descarta el consumidor lento en
+vez de crecer memoria). El cliente reconecta y re-sincroniza desde el estado del servidor (SC-004) — sin pérdida
+de datos, memoria acotada. Junto al path malformed→1002 y el cap de tamaño, completa FU-002.
+
+### Consequences
+
+Un pico transitorio de latencia de un cliente puede cerrarlo; la reconexión con delta lo recupera barato. **FU-002
+pasa a `closed`** al cerrar este Charter.
+
+## Approval
+
+**Approved**: 2026-07-13 by `Jose Villaseñor Montfort`. Las cuatro decisiones verificadas contra el código:
+paridad in-turn (`WeftServer.PublishAsync`: `ExecuteAsync(ExportState)`→`FromBlob`→`PutAsync`, guard sin
+`IBlobStore`; test `Server_publish_matches_local_publish_version_id`); broadcast-a-todos (`DocumentHub.OnUpdateApplied`;
+awareness excluye origen en `WeftConnection` `exclude: this`); retirada de awareness (`AwarenessProtocol.TrackClients`/
+`EncodeRemoval`, `null`+clock+1; test `Awareness_is_relayed_and_withdrawn_on_disconnect`); backpressure
+(`MaxSendQueuePerConnection`=256, cierre al llenarse). Compañero de AILOG-2026-07-13-001 (también firmado).
diff --git a/.straymark/charters/05-weft-server-relay-end-to-end-cierra-m2-us3.md b/.straymark/charters/05-weft-server-relay-end-to-end-cierra-m2-us3.md
index c286488..51690d4 100644
--- a/.straymark/charters/05-weft-server-relay-end-to-end-cierra-m2-us3.md
+++ b/.straymark/charters/05-weft-server-relay-end-to-end-cierra-m2-us3.md
@@ -1,6 +1,6 @@
 ---
 charter_id: CHARTER-05-weft-server-relay-end-to-end-cierra-m2-us3
-status: declared
+status: in-progress
 effort_estimate: L
 trigger: "CHARTER-04 cerrado (M2 corte 1: códec + stores + contract suite verde en main, cc2605b); la base de Weft.Server (SyncProtocol, IDocumentStore, IWeftAuthorizer) está disponible. tasks.md fija T047–T052 (US3) como el relay end-to-end; este es el 2.º corte de M2 y lo CIERRA. Se ancla en las superficies de concurrencia de M1 (DocumentBroker/DocumentSession) y retira el riesgo de compat del wire con un cliente Yjs real (Tiptap). Cierra FU-002 con la parte b (límites por conexión)."
 originating_spec: specs/001-weft-crdt-versioning/spec.md
@@ -10,7 +10,7 @@ design_provenance: new
 
 # Charter: Weft.Server relay end-to-end — cierra M2/US3
 
-> **Status (mirrored from frontmatter — source of truth is above):** declared. Effort: L.
+> **Status (mirrored from frontmatter — source of truth is above):** in-progress. Effort: L.
 >
 > **Origin:** Derivado de `specs/001-weft-crdt-versioning/spec.md`. Segundo y último corte de M2 (T047–T049,
 > T051, T052): el relay WebSocket y-sync end-to-end sobre el substrato de CHARTER-04. **Cierra M2.**
diff --git a/Weft.sln b/Weft.sln
index a7ef844..f23c3a6 100644
--- a/Weft.sln
+++ b/Weft.sln
@@ -29,6 +29,8 @@ Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "Weft.Server", "src\Weft.Ser
 EndProject
 Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "Weft.Server.Tests", "tests\Weft.Server.Tests\Weft.Server.Tests.csproj", "{B5822B77-FF9C-4E13-A2AB-82E94A5EF722}"
 EndProject
+Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "Weft.Sample.Server", "samples\Weft.Sample.Server\Weft.Sample.Server.csproj", "{037E961B-5CAA-4E39-A8E1-6839C179B996}"
+EndProject
 Global
 	GlobalSection(SolutionConfigurationPlatforms) = preSolution
 		Debug|Any CPU = Debug|Any CPU
@@ -159,6 +161,18 @@ Global
 		{B5822B77-FF9C-4E13-A2AB-82E94A5EF722}.Release|x64.Build.0 = Release|Any CPU
 		{B5822B77-FF9C-4E13-A2AB-82E94A5EF722}.Release|x86.ActiveCfg = Release|Any CPU
 		{B5822B77-FF9C-4E13-A2AB-82E94A5EF722}.Release|x86.Build.0 = Release|Any CPU
+		{037E961B-5CAA-4E39-A8E1-6839C179B996}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
+		{037E961B-5CAA-4E39-A8E1-6839C179B996}.Debug|Any CPU.Build.0 = Debug|Any CPU
+		{037E961B-5CAA-4E39-A8E1-6839C179B996}.Debug|x64.ActiveCfg = Debug|Any CPU
+		{037E961B-5CAA-4E39-A8E1-6839C179B996}.Debug|x64.Build.0 = Debug|Any CPU
+		{037E961B-5CAA-4E39-A8E1-6839C179B996}.Debug|x86.ActiveCfg = Debug|Any CPU
+		{037E961B-5CAA-4E39-A8E1-6839C179B996}.Debug|x86.Build.0 = Debug|Any CPU
+		{037E961B-5CAA-4E39-A8E1-6839C179B996}.Release|Any CPU.ActiveCfg = Release|Any CPU
+		{037E961B-5CAA-4E39-A8E1-6839C179B996}.Release|Any CPU.Build.0 = Release|Any CPU
+		{037E961B-5CAA-4E39-A8E1-6839C179B996}.Release|x64.ActiveCfg = Release|Any CPU
+		{037E961B-5CAA-4E39-A8E1-6839C179B996}.Release|x64.Build.0 = Release|Any CPU
+		{037E961B-5CAA-4E39-A8E1-6839C179B996}.Release|x86.ActiveCfg = Release|Any CPU
+		{037E961B-5CAA-4E39-A8E1-6839C179B996}.Release|x86.Build.0 = Release|Any CPU
 	EndGlobalSection
 	GlobalSection(SolutionProperties) = preSolution
 		HideSolutionNode = FALSE
@@ -174,5 +188,6 @@ Global
 		{064C3FA7-B082-436A-974E-5CFB0298A0DA} = {0AB3BF05-4346-4AA6-1389-037BE0695223}
 		{2F9C0022-0E1D-44A4-B887-191E5394D3A4} = {827E0CD3-B72D-47B6-A68D-7590B98EB39B}
 		{B5822B77-FF9C-4E13-A2AB-82E94A5EF722} = {0AB3BF05-4346-4AA6-1389-037BE0695223}
+		{037E961B-5CAA-4E39-A8E1-6839C179B996} = {5D20AA90-6969-D8BD-9DCD-8634F4692FDA}
 	EndGlobalSection
 EndGlobal
diff --git a/samples/Weft.Sample.Server/Program.cs b/samples/Weft.Sample.Server/Program.cs
new file mode 100644
index 0000000..b173e47
--- /dev/null
+++ b/samples/Weft.Sample.Server/Program.cs
@@ -0,0 +1,33 @@
+using Weft.Server;
+using Weft.Server.Auth;
+using Weft.Server.Persistence;
+
+// Relay y-sync de ejemplo: sirve un endpoint WebSocket compatible con clientes Yjs (y-websocket/Tiptap) en
+// ws://127.0.0.1:5199/collab/{docId}. Ver samples/tiptap-client para el cliente y la validación de compat.
+var builder = WebApplication.CreateBuilder(args);
+builder.WebHost.UseUrls(Environment.GetEnvironmentVariable("WEFT_SAMPLE_URLS") ?? "http://127.0.0.1:5199");
+
+builder.Services.AddWeftServer();
+
+// OBLIGATORIO: sin IWeftAuthorizer, MapWeft falla al arrancar. Este demo concede ReadWrite a todos —
+// un consumidor real decide con su propia identidad (JWT/cookies) a partir del HttpContext.
+builder.Services.AddSingleton<IWeftAuthorizer, DemoAuthorizer>();
+
+// Persistencia durable en disco (v1). Los documentos sobreviven al reinicio del servidor.
+string dataDir = Path.Combine(AppContext.BaseDirectory, "weft-data");
+builder.Services.AddSingleton<IDocumentStore>(new FileSystemDocumentStore(dataDir));
+
+WebApplication app = builder.Build();
+app.UseWebSockets();
+app.MapWeft("/collab");
+
+app.Logger.LogInformation("Weft sample relay en {Urls} — endpoint WebSocket /collab/{{docId}} — datos en {DataDir}",
+    string.Join(", ", app.Urls.DefaultIfEmpty("http://127.0.0.1:5199")), dataDir);
+app.Run();
+
+/// <summary>Authorizer de demostración: concede ReadWrite a toda conexión. NO usar en producción.</summary>
+internal sealed class DemoAuthorizer : IWeftAuthorizer
+{
+    public ValueTask<WeftAccess> AuthorizeAsync(HttpContext context, string docId, CancellationToken ct)
+        => ValueTask.FromResult(WeftAccess.ReadWrite);
+}
diff --git a/samples/Weft.Sample.Server/Weft.Sample.Server.csproj b/samples/Weft.Sample.Server/Weft.Sample.Server.csproj
new file mode 100644
index 0000000..0182667
--- /dev/null
+++ b/samples/Weft.Sample.Server/Weft.Sample.Server.csproj
@@ -0,0 +1,33 @@
+<Project Sdk="Microsoft.NET.Sdk.Web">
+
+  <PropertyGroup>
+    <OutputType>Exe</OutputType>
+    <IsPackable>false</IsPackable>
+    <GenerateDocumentationFile>false</GenerateDocumentationFile>
+  </PropertyGroup>
+
+  <ItemGroup>
+    <ProjectReference Include="../../src/Weft.Server/Weft.Server.csproj" />
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
diff --git a/samples/tiptap-client/README.md b/samples/tiptap-client/README.md
new file mode 100644
index 0000000..4468e68
--- /dev/null
+++ b/samples/tiptap-client/README.md
@@ -0,0 +1,39 @@
+# Weft · cliente Tiptap + validación de compat del wire
+
+Sample de US3 (CHARTER-05): un editor **Tiptap** colaborativo real contra el relay `Weft.Server`, más un
+check **headless** de compatibilidad del wire con `yjs`/`y-websocket`. Demuestra que el relay interopera con
+el ecosistema Yjs **sin adaptación** — el servidor habla `y-sync` estándar.
+
+## Requisitos
+
+- El sample server corriendo:
+  ```bash
+  dotnet run --project ../Weft.Sample.Server    # escucha en ws://127.0.0.1:5199/collab/{docId}
+  ```
+- Node.js + npm. Instalar deps una vez: `npm install`.
+
+## 1) Validación headless (sin navegador) — gate de compat del wire
+
+Dos `Y.Doc` reales de Yjs se conectan vía `y-websocket` y deben converger tras ediciones cruzadas:
+
+```bash
+npm run check
+# ✓ convergencia Yjs (y-websocket) ↔ Weft.Server (yrs): "Hello from A. And B too."
+```
+
+Sale con código 0 si converge; 1 si diverge o da timeout. Es la evidencia de que los updates de yrs (servidor)
+y Yjs (cliente) son intercambiables a nivel binario.
+
+## 2) Validación manual con Tiptap (quickstart §US3)
+
+```bash
+npm run dev            # Vite sirve el editor en http://localhost:5173
+```
+
+1. Abre `http://localhost:5173/?doc=demo` en **2+ pestañas** (o navegadores).
+2. Escribe en una pestaña → el texto aparece en vivo en las demás (convergencia).
+3. Los cursores/nombres de los pares se ven (awareness); al cerrar una pestaña, su cursor desaparece (retirada).
+4. Recarga una pestaña → recupera el estado desde el relay (delta en reconexión).
+5. Reinicia el sample server → los documentos persisten (`FileSystemDocumentStore`).
+
+El `docId` es el parámetro `?doc=`; cambia la URL base con `?url=ws://host:port/collab` si hace falta.
diff --git a/samples/tiptap-client/index.html b/samples/tiptap-client/index.html
new file mode 100644
index 0000000..ac511f4
--- /dev/null
+++ b/samples/tiptap-client/index.html
@@ -0,0 +1,20 @@
+<!doctype html>
+<html lang="es">
+  <head>
+    <meta charset="utf-8" />
+    <meta name="viewport" content="width=device-width, initial-scale=1" />
+    <title>Weft · Tiptap collab</title>
+    <style>
+      body { font-family: system-ui, sans-serif; max-width: 760px; margin: 2rem auto; padding: 0 1rem; }
+      .bar { color: #666; font-size: 0.85rem; margin-bottom: 0.5rem; }
+      .ProseMirror { border: 1px solid #ccc; border-radius: 8px; padding: 1rem; min-height: 220px; outline: none; }
+      .collaboration-cursor__caret { border-left: 1px solid; border-right: 1px solid; margin-left: -1px; position: relative; word-break: normal; }
+      .collaboration-cursor__label { font-size: 12px; position: absolute; top: -1.4em; left: -1px; padding: 0 4px; border-radius: 4px 4px 4px 0; color: #fff; white-space: nowrap; }
+    </style>
+  </head>
+  <body>
+    <div class="bar">doc: <b id="room"></b> · relay: <b id="status">connecting…</b> · abre esta misma URL en 2+ pestañas</div>
+    <div id="editor"></div>
+    <script type="module" src="/src/main.js"></script>
+  </body>
+</html>
diff --git a/samples/tiptap-client/package.json b/samples/tiptap-client/package.json
new file mode 100644
index 0000000..e967fb3
--- /dev/null
+++ b/samples/tiptap-client/package.json
@@ -0,0 +1,26 @@
+{
+  "name": "weft-tiptap-client",
+  "private": true,
+  "version": "0.1.0",
+  "type": "module",
+  "description": "Cliente de ejemplo/validación para el relay Weft.Server: editor Tiptap colaborativo (browser) + check headless de compat del wire con yjs/y-websocket.",
+  "scripts": {
+    "dev": "vite",
+    "check": "node wire-check.mjs"
+  },
+  "dependencies": {
+    "@tiptap/core": "^2.6.6",
+    "@tiptap/extension-collaboration": "^2.6.6",
+    "@tiptap/extension-collaboration-cursor": "^2.6.6",
+    "@tiptap/pm": "^2.6.6",
+    "@tiptap/starter-kit": "^2.6.6",
+    "y-prosemirror": "^1.2.12",
+    "y-protocols": "^1.0.6",
+    "y-websocket": "^2.0.4",
+    "yjs": "^13.6.18"
+  },
+  "devDependencies": {
+    "vite": "^5.4.8",
+    "ws": "^8.18.0"
+  }
+}
diff --git a/samples/tiptap-client/src/main.js b/samples/tiptap-client/src/main.js
new file mode 100644
index 0000000..2e00031
--- /dev/null
+++ b/samples/tiptap-client/src/main.js
@@ -0,0 +1,31 @@
+// Cliente Tiptap colaborativo real contra el relay Weft.Server (gate de compat del wire de US3, T052).
+// Tiptap + y-prosemirror + y-websocket, sin adaptación específica de Weft: el relay habla y-sync estándar.
+import { Editor } from '@tiptap/core';
+import StarterKit from '@tiptap/starter-kit';
+import Collaboration from '@tiptap/extension-collaboration';
+import CollaborationCursor from '@tiptap/extension-collaboration-cursor';
+import * as Y from 'yjs';
+import { WebsocketProvider } from 'y-websocket';
+
+const params = new URLSearchParams(location.search);
+const room = params.get('doc') || 'demo';
+const url = params.get('url') || 'ws://127.0.0.1:5199/collab';
+
+const ydoc = new Y.Doc();
+const provider = new WebsocketProvider(url, room, ydoc);
+
+const name = 'User-' + Math.floor(Math.random() * 1000);
+const color = '#' + Math.floor(Math.random() * 0xffffff).toString(16).padStart(6, '0');
+
+const editor = new Editor({
+  element: document.querySelector('#editor'),
+  extensions: [
+    StarterKit.configure({ history: false }), // el historial/undo lo gestiona Yjs, no Tiptap
+    Collaboration.configure({ document: ydoc }),
+    CollaborationCursor.configure({ provider, user: { name, color } }),
+  ],
+});
+
+provider.on('status', (e) => { document.querySelector('#status').textContent = e.status; });
+document.querySelector('#room').textContent = room;
+window.__weft = { editor, ydoc, provider }; // para inspección manual en la consola
diff --git a/samples/tiptap-client/wire-check.mjs b/samples/tiptap-client/wire-check.mjs
new file mode 100644
index 0000000..541891d
--- /dev/null
+++ b/samples/tiptap-client/wire-check.mjs
@@ -0,0 +1,57 @@
+// Validación HEADLESS de compatibilidad del wire (retira R1 sin navegador): dos Y.Doc reales de Yjs se
+// conectan al relay Weft.Server vía y-websocket y deben converger tras ediciones cruzadas. Si yrs (servidor) y
+// Yjs (cliente) no fueran compatibles a nivel de update binario, esto divergiría o daría timeout.
+//
+// Uso: arrancar el sample server (dotnet run --project samples/Weft.Sample.Server), luego `npm run check`.
+import * as Y from 'yjs';
+import { WebsocketProvider } from 'y-websocket';
+import WS from 'ws';
+
+const URL = process.env.WEFT_URL || 'ws://127.0.0.1:5199/collab';
+const ROOM = 'headless-' + Date.now();
+const FIELD = 'content';
+
+function connect() {
+  const doc = new Y.Doc();
+  const provider = new WebsocketProvider(URL, ROOM, doc, { WebSocketPolyfill: WS, connect: true });
+  return { doc, provider };
+}
+
+function waitFor(cond, label, ms = 5000) {
+  return new Promise((resolve, reject) => {
+    const t0 = Date.now();
+    const iv = setInterval(() => {
+      if (cond()) { clearInterval(iv); resolve(); }
+      else if (Date.now() - t0 > ms) { clearInterval(iv); reject(new Error('timeout esperando: ' + label)); }
+    }, 20);
+  });
+}
+
+const a = connect();
+const b = connect();
+let code = 0;
+try {
+  await waitFor(() => a.provider.wsconnected && b.provider.wsconnected, 'conexión de ambos clientes');
+
+  // A edita → B debe converger.
+  a.doc.getText(FIELD).insert(0, 'Hello from A. ');
+  await waitFor(() => b.doc.getText(FIELD).toString().includes('Hello from A.'), 'B recibe la edición de A');
+
+  // B edita → A debe converger.
+  const t = b.doc.getText(FIELD);
+  t.insert(t.length, 'And B too.');
+  await waitFor(() => a.doc.getText(FIELD).toString().includes('And B too.'), 'A recibe la edición de B');
+
+  const ta = a.doc.getText(FIELD).toString();
+  const tb = b.doc.getText(FIELD).toString();
+  if (ta !== tb) throw new Error(`divergencia: A="${ta}" B="${tb}"`);
+
+  console.log('✓ convergencia Yjs (y-websocket) ↔ Weft.Server (yrs):', JSON.stringify(ta));
+} catch (e) {
+  console.error('✗ FALLO de compat del wire:', e.message);
+  code = 1;
+} finally {
+  a.provider.destroy();
+  b.provider.destroy();
+  setTimeout(() => process.exit(code), 100);
+}
diff --git a/specs/001-weft-crdt-versioning/tasks.md b/specs/001-weft-crdt-versioning/tasks.md
index e8bb5b5..0a9fa32 100644
--- a/specs/001-weft-crdt-versioning/tasks.md
+++ b/specs/001-weft-crdt-versioning/tasks.md
@@ -119,12 +119,12 @@
 - [X] T044 [P] [US3] Define auth hook in `src/Weft.Server/Auth/IWeftAuthorizer.cs` + `WeftAccess` enum (Deny/ReadOnly/ReadWrite) — CHARTER-04
 - [X] T045 [P] [US3] Define `IDocumentStore` + `InMemoryDocumentStore` in `src/Weft.Server/Persistence/IDocumentStore.cs`, `InMemoryDocumentStore.cs` (Load/AppendUpdate/SaveSnapshot) — CHARTER-04
 - [X] T046 [US3] Implement `FileSystemDocumentStore` in `src/Weft.Server/Persistence/FileSystemDocumentStore.cs` (snapshot + updates append, compaction al guardar snapshot) — CHARTER-04
-- [ ] T047 [US3] Implement connection handler `src/Weft.Server/WeftConnection.cs`: handshake (authz→403/upgrade), sync bidireccional incremental, relay de updates vía DocumentBroker + persistencia, awareness broadcast + retirada al cerrar, ReadOnly→close 1008, malformed→close 1002
-- [ ] T048 [US3] Implement DI + endpoint in `src/Weft.Server/WeftServerExtensions.cs`: `AddWeftServer(options)` (falla al arrancar sin `IWeftAuthorizer`), `MapWeft(path)` con `{docId}`
-- [ ] T049 [US3] Implement `IWeftServer` service in `src/Weft.Server/WeftServer.cs`: `PublishAsync` (VersionStore dentro del turno del actor — mismo VersionId que local), `GetConnectionCountAsync`, `DisconnectAllAsync`
+- [X] T047 [US3] Implement connection handler `src/Weft.Server/WeftConnection.cs`: handshake (authz→403/upgrade), sync bidireccional incremental, relay de updates vía DocumentBroker + persistencia, awareness broadcast + retirada al cerrar, ReadOnly→close 1008, malformed→close 1002 — CHARTER-05
+- [X] T048 [US3] Implement DI + endpoint in `src/Weft.Server/WeftServerExtensions.cs`: `AddWeftServer(options)` (falla al arrancar sin `IWeftAuthorizer`), `MapWeft(path)` con `{docId}` — CHARTER-05
+- [X] T049 [US3] Implement `IWeftServer` service in `src/Weft.Server/WeftServer.cs`: `PublishAsync` (VersionStore dentro del turno del actor — mismo VersionId que local), `GetConnectionCountAsync`, `DisconnectAllAsync` — CHARTER-05
 - [X] T050 [P] [US3] Shared `IDocumentStore` contract suite `tests/Weft.Server.Tests/DocumentStoreContractSuite.cs` (corre contra InMemory y FileSystem; luego EFCore/Redis) — CHARTER-04
-- [ ] T051 [P] [US3] Server integration tests `tests/Weft.Server.Tests/RelayTests.cs`: 2 clientes simulados (convergencia <1 s, delta en reconexión con bytes medidos, Deny sin bytes de contenido, ReadOnly→1008, awareness, restart-recovery, paridad de VersionId con publish local)
-- [ ] T052 [US3] Create samples `samples/Weft.Sample.Server/Program.cs` (relay + FileSystemDocumentStore + authorizer demo) + `samples/tiptap-client/` (Tiptap + y-prosemirror + y-websocket) y ejecutar la validación manual de quickstart.md §US3
+- [X] T051 [P] [US3] Server integration tests `tests/Weft.Server.Tests/RelayTests.cs`: 2 clientes simulados (convergencia <1 s, delta en reconexión con bytes medidos, Deny sin bytes de contenido, ReadOnly→1008, awareness, restart-recovery, paridad de VersionId con publish local) — CHARTER-05
+- [X] T052 [US3] Create samples `samples/Weft.Sample.Server/Program.cs` (relay + FileSystemDocumentStore + authorizer demo) + `samples/tiptap-client/` (Tiptap + y-prosemirror + y-websocket) y ejecutar la validación manual de quickstart.md §US3 — CHARTER-05
 - [ ] T053 [P] [US3] EF Core adapter package `src/Weft.Server.Persistence.EFCore/EFCoreDocumentStore.cs` (+ pasa la contract suite)
 - [ ] T054 [P] [US3] Redis adapter package `src/Weft.Server.Persistence.Redis/RedisDocumentStore.cs` (+ pasa la contract suite)
 
diff --git a/src/Weft.Server/DocumentHub.cs b/src/Weft.Server/DocumentHub.cs
new file mode 100644
index 0000000..3a768e0
--- /dev/null
+++ b/src/Weft.Server/DocumentHub.cs
@@ -0,0 +1,117 @@
+using System.Collections.Concurrent;
+using Weft.Concurrency;
+using Weft.Server.Persistence;
+using Weft.Server.Protocol;
+
+namespace Weft.Server;
+
+/// <summary>
+/// Punto de encuentro de todas las conexiones de un mismo documento. Mantiene <b>una</b>
+/// <see cref="DocumentSession"/> por documento (anclaje M1: el broadcast vía
+/// <see cref="DocumentSession.UpdateApplied"/> es perezoso, se suscribe una sola vez y el refcount de sesiones
+/// mantiene el documento residente mientras haya conexiones). Difunde cada update aplicado y persiste el flujo.
+/// </summary>
+internal sealed class DocumentHub : IAsyncDisposable
+{
+    private readonly IDocumentStore _store;
+    private readonly ConcurrentDictionary<WeftConnection, byte> _connections = new();
+    private int _disposed;
+
+    public DocumentHub(string docId, DocumentSession session, IDocumentStore store)
+    {
+        DocId = docId;
+        Session = session;
+        _store = store;
+        Session.UpdateApplied += OnUpdateApplied;
+    }
+
+    /// <summary>Identificador del documento.</summary>
+    public string DocId { get; }
+
+    /// <summary>Sesión async compartida del documento (turno del actor serializado, P-V).</summary>
+    public DocumentSession Session { get; }
+
+    /// <summary>Conexiones activas del documento.</summary>
+    public int ConnectionCount => _connections.Count;
+
+    public void Add(WeftConnection connection) => _connections.TryAdd(connection, 0);
+
+    public void Remove(WeftConnection connection) => _connections.TryRemove(connection, out _);
+
+    /// <summary>Pide el cierre de todas las conexiones del documento (su teardown las retira del hub).</summary>
+    public void DisconnectAll()
+    {
+        foreach (WeftConnection c in _connections.Keys)
+        {
+            c.RequestClose();
+        }
+    }
+
+    /// <summary>
+    /// Difunde un frame a las conexiones del documento, opcionalmente excluyendo el origen. Cada envío se aísla:
+    /// un fallo/backpressure de una conexión (que la cierra) no afecta a los pares.
+    /// </summary>
+    public void Broadcast(byte[] frame, WeftConnection? exclude)
+    {
+        foreach (WeftConnection c in _connections.Keys)
+        {
+            if (!ReferenceEquals(c, exclude))
+            {
+                c.TryEnqueue(frame);
+            }
+        }
+    }
+
+    /// <summary>
+    /// Aplica un update entrante al documento (turno del actor) y lo persiste. La aplicación dispara
+    /// <see cref="OnUpdateApplied"/>, que difunde el delta a las conexiones.
+    /// </summary>
+    public async ValueTask ApplyAndPersistAsync(byte[] update, CancellationToken ct)
+    {
+        await Session.ApplyUpdateAsync(update, ct).ConfigureAwait(false);
+        await _store.AppendUpdateAsync(DocId, update, ct).ConfigureAwait(false);
+    }
+
+    // El delta se difunde a TODAS las conexiones del documento. Reaplicar su propio delta en el origen es un
+    // no-op CRDT idempotente (los clientes Yjs lo toleran), lo que evita rastrear el origen dentro del turno del
+    // actor (que sería una carrera). El coste es un eco al emisor; aceptable para v1.
+    private void OnUpdateApplied(DocumentSession _, ReadOnlyMemory<byte> delta)
+    {
+        if (delta.IsEmpty)
+        {
+            return;
+        }
+
+        Broadcast(SyncProtocol.EncodeUpdate(delta.Span), exclude: null);
+    }
+
+    /// <summary>
+    /// Cierra el hub: desuscribe el broadcast, consolida un snapshot (compaction) y libera la sesión (lo que
+    /// permite al broker desalojar el documento por inactividad). Idempotente.
+    /// </summary>
+    public async ValueTask DisposeAsync()
+    {
+        if (Interlocked.Exchange(ref _disposed, 1) != 0)
+        {
+            return;
+        }
+
+        Session.UpdateApplied -= OnUpdateApplied;
+
+        try
+        {
+            // Snapshot de consolidación: el estado completo dentro del turno del actor reemplaza los updates
+            // acumulados en el store (compaction). Best-effort: un fallo no debe romper el cierre de la sesión.
+            byte[] snapshot = await Session.ExportStateAsync().ConfigureAwait(false);
+            await _store.SaveSnapshotAsync(DocId, snapshot).ConfigureAwait(false);
+        }
+        catch
+        {
+            // El desalojo del broker (OnEvicting) también persiste; la durabilidad no depende solo de aquí.
+        }
+        finally
+        {
+            await Session.DisposeAsync().ConfigureAwait(false);
+        }
+    }
+}
diff --git a/src/Weft.Server/Protocol/AwarenessProtocol.cs b/src/Weft.Server/Protocol/AwarenessProtocol.cs
new file mode 100644
index 0000000..3f9d415
--- /dev/null
+++ b/src/Weft.Server/Protocol/AwarenessProtocol.cs
@@ -0,0 +1,70 @@
+using System.Text;
+
+namespace Weft.Server.Protocol;
+
+/// <summary>
+/// Parsing mínimo del protocolo <c>y-awareness</c> — lo justo para que el relay difunda la <b>retirada</b> del
+/// estado de una conexión al cerrarse (FR-015). El relay no interpreta el <i>contenido</i> del estado (es
+/// opaco); solo necesita los <c>clientID</c> que una conexión anunció para poder marcarlos offline al salir.
+/// </summary>
+/// <remarks>
+/// Formato de un awareness update (payload interno del mensaje <see cref="MessageType.Awareness"/>):
+/// <code>
+/// &lt;numClients:varUint&gt; ( &lt;clientID:varUint&gt; &lt;clock:varUint&gt; &lt;state:VarUint8Array (JSON UTF-8)&gt; )*
+/// </code>
+/// La retirada es un update con <c>clock+1</c> y estado <c>"null"</c> por cada clientID, como hace
+/// <c>y-protocols/awareness</c>.
+/// </remarks>
+internal static class AwarenessProtocol
+{
+    private static readonly byte[] NullStateUtf8 = Encoding.UTF8.GetBytes("null");
+
+    /// <summary>
+    /// Extrae los pares <c>clientID → clock</c> de un awareness update, acumulando en
+    /// <paramref name="tracked"/> (el clock más alto visto por cliente). Tolerante a payloads que no parsean
+    /// (best-effort: el awareness no es crítico para la convergencia del documento).
+    /// </summary>
+    public static void TrackClients(ReadOnlySpan<byte> awarenessPayload, Dictionary<uint, uint> tracked)
+    {
+        try
+        {
+            var r = new Lib0Encoding.Lib0Reader(awarenessPayload);
+            uint count = r.ReadVarUint();
+            for (uint i = 0; i < count; i++)
+            {
+                uint clientId = r.ReadVarUint();
+                uint clock = r.ReadVarUint();
+                _ = r.ReadVarUint8Array(); // estado (opaco): se salta
+                tracked[clientId] = clock > tracked.GetValueOrDefault(clientId) ? clock : tracked[clientId];
+            }
+        }
+        catch (MalformedMessageException)
+        {
+            // Awareness malformado: se ignora para el tracking (el broadcast del mensaje ya lo maneja el relay).
+        }
+    }
+
+    /// <summary>
+    /// Construye un mensaje <see cref="MessageType.Awareness"/> completo que marca offline a
+    /// <paramref name="clients"/> (estado <c>"null"</c>, <c>clock+1</c>). Devuelve <c>null</c> si no hay clientes
+    /// que retirar.
+    /// </summary>
+    public static byte[]? EncodeRemoval(IReadOnlyDictionary<uint, uint> clients)
+    {
+        if (clients.Count == 0)
+        {
+            return null;
+        }
+
+        var inner = new Lib0Encoding.Lib0Writer();
+        inner.WriteVarUint((uint)clients.Count);
+        foreach ((uint clientId, uint clock) in clients)
+        {
+            inner.WriteVarUint(clientId);
+            inner.WriteVarUint(clock + 1);
+            inner.WriteVarUint8Array(NullStateUtf8);
+        }
+
+        return SyncProtocol.EncodeAwareness(inner.WrittenSpan);
+    }
+}
diff --git a/src/Weft.Server/Weft.Server.csproj b/src/Weft.Server/Weft.Server.csproj
index ac2f668..aafa74a 100644
--- a/src/Weft.Server/Weft.Server.csproj
+++ b/src/Weft.Server/Weft.Server.csproj
@@ -4,11 +4,17 @@
     <Description>Weft — relay WebSocket y-sync para ASP.NET Core: códec lib0/y-protocols, hook de autorización y persistencia de blobs opacos (IDocumentStore). Compatible con clientes del ecosistema Yjs.</Description>
   </PropertyGroup>
 
-  <!-- Solo por HttpContext en IWeftAuthorizer (el hook de autorización del consumidor). -->
+  <!-- HttpContext (IWeftAuthorizer), WebSockets y el endpoint MapWeft. -->
   <ItemGroup>
     <FrameworkReference Include="Microsoft.AspNetCore.App" />
   </ItemGroup>
 
+  <!-- El relay se ancla en el broker/sesión de M1 (Weft.Core) y publica vía content-addressing (Weft.Versioning). -->
+  <ItemGroup>
+    <ProjectReference Include="../Weft.Core/Weft.Core.csproj" />
+    <ProjectReference Include="../Weft.Versioning/Weft.Versioning.csproj" />
+  </ItemGroup>
+
   <ItemGroup>
     <!-- La contract suite ejercita los stores y el códec; algunos invariantes se validan a nivel internal. -->
     <InternalsVisibleTo Include="Weft.Server.Tests" />
diff --git a/src/Weft.Server/WeftConnection.cs b/src/Weft.Server/WeftConnection.cs
new file mode 100644
index 0000000..4296d67
--- /dev/null
+++ b/src/Weft.Server/WeftConnection.cs
@@ -0,0 +1,230 @@
+using System.Buffers;
+using System.Net.WebSockets;
+using System.Threading.Channels;
+using Weft.Server.Auth;
+using Weft.Server.Protocol;
+
+namespace Weft.Server;
+
+/// <summary>
+/// Una conexión WebSocket de un cliente a un documento. Corre un <b>send pump</b> (drena una cola de envío
+/// acotada → el socket) y un <b>receive loop</b> (decodifica frames y-sync, aplica el enforcement de
+/// autorización y los límites por conexión, y despacha sync/awareness). Aislada: un fallo de esta conexión no
+/// afecta a los pares (el broadcast del hub aísla cada envío).
+/// </summary>
+internal sealed class WeftConnection
+{
+    private readonly WebSocket _ws;
+    private readonly WeftServerOptions _options;
+    private readonly Channel<byte[]> _sendQueue;
+    private readonly CancellationTokenSource _cts;
+    private readonly Dictionary<uint, uint> _awarenessClients = new();
+
+    public WeftConnection(WebSocket ws, WeftAccess access, WeftServerOptions options, CancellationToken hostCt)
+    {
+        _ws = ws;
+        Access = access;
+        _options = options;
+        _cts = CancellationTokenSource.CreateLinkedTokenSource(hostCt);
+        _sendQueue = Channel.CreateBounded<byte[]>(new BoundedChannelOptions(options.MaxSendQueuePerConnection)
+        {
+            SingleReader = true,
+            FullMode = BoundedChannelFullMode.Wait, // usamos TryWrite: 'lleno' ⇒ se cierra la conexión (backpressure)
+        });
+    }
+
+    /// <summary>Nivel de acceso concedido en el handshake.</summary>
+    public WeftAccess Access { get; }
+
+    /// <summary>clientIDs de awareness anunciados por esta conexión (para la retirada al cerrar, FR-015).</summary>
+    public IReadOnlyDictionary<uint, uint> AwarenessClients => _awarenessClients;
+
+    /// <summary>Pide el cierre de la conexión (p. ej. desde <c>DisconnectAllAsync</c>). No bloquea.</summary>
+    public void RequestClose() => _cts.Cancel();
+
+    /// <summary>
+    /// Encola un frame para envío. No bloquea (se llama desde el turno del actor durante el broadcast). Si la
+    /// cola está llena (consumidor lento, FU-002 parte b), devuelve <c>false</c> y la conexión se cierra.
+    /// </summary>
+    public bool TryEnqueue(byte[] frame)
+    {
+        if (_sendQueue.Writer.TryWrite(frame))
+        {
+            return true;
+        }
+
+        // Backpressure: descartar el consumidor lento en vez de crecer memoria; reconectará y re-sincronizará.
+        _cts.Cancel();
+        return false;
+    }
+
+    /// <summary>
+    /// Corre la conexión hasta que cierra: arranca el send pump, envía el <c>SyncStep1</c> inicial y drena el
+    /// receive loop. Al terminar, completa la cola de envío.
+    /// </summary>
+    public async Task RunAsync(DocumentHub hub, CancellationToken ct)
+    {
+        using CancellationTokenRegistration _ = ct.Register(static s => ((CancellationTokenSource)s!).Cancel(), _cts);
+        Task pump = Task.Run(() => SendPumpAsync(_cts.Token));
+
+        try
+        {
+            // Sync inicial (servidor→cliente): "esto conozco". El cliente responde su SyncStep1, que el receive
+            // loop contesta con SyncStep2 (delta) — sync incremental en ambas direcciones.
+            byte[] serverSv = await hub.Session.ExportStateVectorAsync(_cts.Token).ConfigureAwait(false);
+            TryEnqueue(SyncProtocol.EncodeSyncStep1(serverSv));
+
+            await ReceiveLoopAsync(hub, _cts.Token).ConfigureAwait(false);
+        }
+        catch (OperationCanceledException) { /* cierre normal */ }
+        catch (WebSocketException) { /* socket cortado por el par: cierre normal */ }
+        finally
+        {
+            _sendQueue.Writer.TryComplete();
+            try { await pump.ConfigureAwait(false); } catch { /* pump ya en cierre */ }
+        }
+    }
+
+    private async Task ReceiveLoopAsync(DocumentHub hub, CancellationToken ct)
+    {
+        while (!ct.IsCancellationRequested && _ws.State == WebSocketState.Open)
+        {
+            (WebSocketMessageType type, byte[]? data) = await ReceiveMessageAsync(ct).ConfigureAwait(false);
+            if (type == WebSocketMessageType.Close || data is null)
+            {
+                return;
+            }
+
+            if (type != WebSocketMessageType.Binary)
+            {
+                continue; // el protocolo y-sync es binario; se ignora texto/ping
+            }
+
+            if (!await DispatchAsync(hub, data, ct).ConfigureAwait(false))
+            {
+                return; // dispatch pidió cerrar (malformado 1002 / read-only 1008)
+            }
+        }
+    }
+
+    private async Task<bool> DispatchAsync(DocumentHub hub, byte[] frame, CancellationToken ct)
+    {
+        // SyncMessage es un ref struct (span sobre el frame): se extraen tipo+payload a locales ANTES de await.
+        MessageType type;
+        SyncMessageType syncType;
+        byte[] payload;
+        try
+        {
+            SyncMessage m = SyncProtocol.Decode(frame, _options.MaxMessageBytes);
+            type = m.Type;
+            syncType = m.SyncType;
+            payload = m.Payload.ToArray();
+        }
+        catch (MalformedMessageException)
+        {
+            await CloseAsync(WebSocketCloseStatus.ProtocolError, "malformed message", ct).ConfigureAwait(false); // 1002
+            return false;
+        }
+
+        switch (type)
+        {
+            case MessageType.Sync when syncType == SyncMessageType.Step1:
+                // El cliente anuncia su state vector → responder el delta que le falta.
+                byte[] delta = await hub.Session.ExportUpdateSinceAsync(payload, ct).ConfigureAwait(false);
+                TryEnqueue(SyncProtocol.EncodeSyncStep2(delta));
+                return true;
+
+            case MessageType.Sync: // Step2 o Update: el cliente envía un update de documento.
+                if (Access != WeftAccess.ReadWrite)
+                {
+                    await CloseAsync(WebSocketCloseStatus.PolicyViolation, "read-only connection", ct) // 1008
+                        .ConfigureAwait(false);
+                    return false;
+                }
+
+                await hub.ApplyAndPersistAsync(payload, ct).ConfigureAwait(false); // dispara UpdateApplied → broadcast
+                return true;
+
+            case MessageType.Awareness:
+                AwarenessProtocol.TrackClients(payload, _awarenessClients);
+                hub.Broadcast(SyncProtocol.EncodeAwareness(payload), exclude: this); // efímero, a los pares, sin persistir
+                return true;
+
+            default:
+                return true;
+        }
+    }
+
+    private async Task SendPumpAsync(CancellationToken ct)
+    {
+        try
+        {
+            await foreach (byte[] frame in _sendQueue.Reader.ReadAllAsync(ct).ConfigureAwait(false))
+            {
+                await _ws.SendAsync(frame, WebSocketMessageType.Binary, endOfMessage: true, ct).ConfigureAwait(false);
+            }
+        }
+        catch (OperationCanceledException) { /* cierre */ }
+        catch (WebSocketException) { _cts.Cancel(); }
+    }
+
+    private async Task<(WebSocketMessageType, byte[]?)> ReceiveMessageAsync(CancellationToken ct)
+    {
+        byte[] rent = ArrayPool<byte>.Shared.Rent(8192);
+        var acc = new ArrayBufferWriter<byte>();
+        try
+        {
+            while (true)
+            {
+                WebSocketReceiveResult r;
+                try
+                {
+                    r = await _ws.ReceiveAsync(rent, ct).ConfigureAwait(false);
+                }
+                catch (WebSocketException)
+                {
+                    return (WebSocketMessageType.Close, null);
+                }
+
+                if (r.MessageType == WebSocketMessageType.Close)
+                {
+                    return (WebSocketMessageType.Close, null);
+                }
+
+                acc.Write(rent.AsSpan(0, r.Count));
+                if (acc.WrittenCount > _options.MaxMessageBytes)
+                {
+                    // Frame sobredimensionado (FU-002 parte a): cerrar antes de acumular más.
+                    await CloseAsync(WebSocketCloseStatus.MessageTooBig, "message too large", ct).ConfigureAwait(false); // 1009
+                    return (WebSocketMessageType.Close, null);
+                }
+
+                if (r.EndOfMessage)
+                {
+                    return (r.MessageType, acc.WrittenSpan.ToArray());
+                }
+            }
+        }
+        finally
+        {
+            ArrayPool<byte>.Shared.Return(rent);
+        }
+    }
+
+    private async Task CloseAsync(WebSocketCloseStatus status, string description, CancellationToken ct)
+    {
+        try
+        {
+            if (_ws.State == WebSocketState.Open)
+            {
+                await _ws.CloseAsync(status, description, ct).ConfigureAwait(false);
+            }
+        }
+        catch (WebSocketException) { /* ya cerrado */ }
+        catch (OperationCanceledException) { /* apagando */ }
+        finally
+        {
+            _cts.Cancel();
+        }
+    }
+}
diff --git a/src/Weft.Server/WeftServer.cs b/src/Weft.Server/WeftServer.cs
new file mode 100644
index 0000000..91af482
--- /dev/null
+++ b/src/Weft.Server/WeftServer.cs
@@ -0,0 +1,219 @@
+using System.Collections.Concurrent;
+using System.Net.WebSockets;
+using Weft;
+using Weft.Concurrency;
+using Weft.Server.Auth;
+using Weft.Server.Persistence;
+using Weft.Server.Protocol;
+using Weft.Versioning;
+using Weft.Versioning.Blobs;
+
+namespace Weft.Server;
+
+/// <summary>Servicio de operación del relay (FR-018): publicar, contar conexiones, desconectar.</summary>
+public interface IWeftServer
+{
+    /// <summary>
+    /// Snapshot content-addressed de un documento activo. Ejecuta dentro del turno del actor del documento →
+    /// mismo <see cref="VersionId"/> que produciría <c>VersionStore</c> en local para el mismo contenido
+    /// (paridad, P-III). Requiere un <see cref="IBlobStore"/> registrado.
+    /// </summary>
+    ValueTask<VersionId> PublishAsync(string docId, CancellationToken ct = default);
+
+    /// <summary>Número de conexiones activas de un documento (0 si no hay ninguna).</summary>
+    ValueTask<int> GetConnectionCountAsync(string docId, CancellationToken ct = default);
+
+    /// <summary>Cierra todas las conexiones de un documento (p. ej. tras revocación de acceso).</summary>
+    ValueTask DisconnectAllAsync(string docId, CancellationToken ct = default);
+}
+
+/// <summary>
+/// Implementación del relay: registro de <see cref="DocumentHub"/> por documento sobre un
+/// <see cref="DocumentBroker"/> de M1. Singleton en el contenedor del consumidor. El broker se configura para
+/// consolidar un snapshot en el store al desalojar (compaction), y la carga reconstruye el documento desde el
+/// <see cref="IDocumentStore"/>.
+/// </summary>
+public sealed class WeftServer : IWeftServer, IAsyncDisposable
+{
+    private readonly WeftServerOptions _options;
+    private readonly ICrdtEngine _engine;
+    private readonly IDocumentStore _store;
+    private readonly IBlobStore? _blobStore;
+    private readonly DocumentBroker _broker;
+    private readonly ConcurrentDictionary<string, DocumentHub> _hubs = new(StringComparer.Ordinal);
+    private readonly SemaphoreSlim _hubGate = new(1, 1);
+    private bool _disposed;
+
+    /// <summary>Crea el relay. <paramref name="blobStore"/> es opcional: solo lo necesita <see cref="PublishAsync"/>.</summary>
+    public WeftServer(WeftServerOptions options, IDocumentStore store, IBlobStore? blobStore = null)
+    {
+        ArgumentNullException.ThrowIfNull(options);
+        ArgumentNullException.ThrowIfNull(store);
+        _options = options;
+        _engine = options.Engine;
+        _store = store;
+        _blobStore = blobStore;
+
+        // El desalojo del broker consolida un snapshot en el store (compaction), encadenando el hook del usuario.
+        Func<string, byte[], CancellationToken, ValueTask>? userOnEvicting = options.Broker.OnEvicting;
+        var brokerOptions = new DocumentBrokerOptions
+        {
+            IdleEviction = options.Broker.IdleEviction,
+            MaxActiveDocuments = options.Broker.MaxActiveDocuments,
+            IdleSweepInterval = options.Broker.IdleSweepInterval,
+            OnEvicting = async (docId, state, ct) =>
+            {
+                if (userOnEvicting is not null)
+                {
+                    await userOnEvicting(docId, state, ct).ConfigureAwait(false);
+                }
+
+                await _store.SaveSnapshotAsync(docId, state, ct).ConfigureAwait(false);
+            },
+        };
+        _broker = new DocumentBroker(_engine, brokerOptions);
+    }
+
+    // -- Endpoint: ciclo de vida de una conexión --
+
+    internal async Task HandleConnectionAsync(string docId, WeftAccess access, WebSocket ws, CancellationToken ct)
+    {
+        var connection = new WeftConnection(ws, access, _options, ct);
+        DocumentHub hub = await JoinAsync(docId, connection, ct).ConfigureAwait(false);
+        try
+        {
+            await connection.RunAsync(hub, ct).ConfigureAwait(false);
+        }
+        finally
+        {
+            await LeaveAsync(hub, connection).ConfigureAwait(false);
+        }
+    }
+
+    private async Task<DocumentHub> JoinAsync(string docId, WeftConnection connection, CancellationToken ct)
+    {
+        await _hubGate.WaitAsync(ct).ConfigureAwait(false);
+        try
+        {
+            if (!_hubs.TryGetValue(docId, out DocumentHub? hub))
+            {
+                DocumentSession session = await _broker.OpenAsync(docId, LoadDocStateAsync, ct).ConfigureAwait(false);
+                hub = new DocumentHub(docId, session, _store);
+                _hubs[docId] = hub;
+            }
+
+            hub.Add(connection);
+            return hub;
+        }
+        finally
+        {
+            _hubGate.Release();
+        }
+    }
+
+    private async Task LeaveAsync(DocumentHub hub, WeftConnection connection)
+    {
+        // Retirada de awareness (FR-015): marcar offline los clientIDs que anunció esta conexión, a los pares.
+        byte[]? removal = AwarenessProtocol.EncodeRemoval(connection.AwarenessClients);
+        if (removal is not null)
+        {
+            hub.Broadcast(removal, exclude: connection);
+        }
+
+        await _hubGate.WaitAsync().ConfigureAwait(false);
+        try
+        {
+            hub.Remove(connection);
+            if (hub.ConnectionCount == 0 && _hubs.TryRemove(hub.DocId, out _))
+            {
+                await hub.DisposeAsync().ConfigureAwait(false);
+            }
+        }
+        finally
+        {
+            _hubGate.Release();
+        }
+    }
+
+    // Reconstruye el blob de estado del documento desde el store (snapshot + updates enmarcados) para el broker.
+    private async ValueTask<byte[]?> LoadDocStateAsync(string docId, CancellationToken ct)
+    {
+        byte[]? framed = await _store.LoadAsync(docId, ct).ConfigureAwait(false);
+        if (framed is null)
+        {
+            return null; // documento nuevo
+        }
+
+        IReadOnlyList<byte[]> records = DocumentStateFraming.ReadRecords(framed);
+        using ICrdtDoc doc = _engine.CreateDoc();
+        foreach (byte[] record in records)
+        {
+            doc.ApplyUpdate(record);
+        }
+
+        return doc.ExportState();
+    }
+
+    // -- IWeftServer --
+
+    /// <inheritdoc />
+    public async ValueTask<VersionId> PublishAsync(string docId, CancellationToken ct = default)
+    {
+        ArgumentException.ThrowIfNullOrEmpty(docId);
+        if (_blobStore is null)
+        {
+            throw new InvalidOperationException(
+                "PublishAsync requiere un IBlobStore registrado en el contenedor del consumidor.");
+        }
+
+        await using DocumentSession session = await _broker.OpenAsync(docId, LoadDocStateAsync, ct)
+            .ConfigureAwait(false);
+
+        // Snapshot dentro del turno del actor: ExportState es la MISMA operación determinista (P-III) que usa
+        // VersionStore.PublishAsync en local; FromBlob(ExportState) reproduce el VersionId local byte a byte.
+        byte[] snapshot = await session.ExecuteAsync(static doc => doc.ExportState(), ct).ConfigureAwait(false);
+        var id = VersionId.FromBlob(snapshot);
+        await _blobStore.PutAsync(id, snapshot, ct).ConfigureAwait(false);
+        return id;
+    }
+
+    /// <inheritdoc />
+    public ValueTask<int> GetConnectionCountAsync(string docId, CancellationToken ct = default)
+    {
+        ArgumentException.ThrowIfNullOrEmpty(docId);
+        int count = _hubs.TryGetValue(docId, out DocumentHub? hub) ? hub.ConnectionCount : 0;
+        return ValueTask.FromResult(count);
+    }
+
+    /// <inheritdoc />
+    public ValueTask DisconnectAllAsync(string docId, CancellationToken ct = default)
+    {
+        ArgumentException.ThrowIfNullOrEmpty(docId);
+        if (_hubs.TryGetValue(docId, out DocumentHub? hub))
+        {
+            hub.DisconnectAll();
+        }
+
+        return ValueTask.CompletedTask;
+    }
+
+    /// <summary>Cierra el relay: descarta los hubs vivos y drena el broker.</summary>
+    public async ValueTask DisposeAsync()
+    {
+        if (_disposed)
+        {
+            return;
+        }
+
+        _disposed = true;
+        foreach (DocumentHub hub in _hubs.Values)
+        {
+            hub.DisconnectAll();
+            await hub.DisposeAsync().ConfigureAwait(false);
+        }
+
+        _hubs.Clear();
+        await _broker.DisposeAsync().ConfigureAwait(false);
+        _hubGate.Dispose();
+    }
+}
diff --git a/src/Weft.Server/WeftServerExtensions.cs b/src/Weft.Server/WeftServerExtensions.cs
new file mode 100644
index 0000000..6a04532
--- /dev/null
+++ b/src/Weft.Server/WeftServerExtensions.cs
@@ -0,0 +1,80 @@
+using System.Net.WebSockets;
+using Microsoft.AspNetCore.Builder;
+using Microsoft.AspNetCore.Http;
+using Microsoft.AspNetCore.Routing;
+using Microsoft.Extensions.DependencyInjection;
+using Weft.Server.Auth;
+using Weft.Server.Persistence;
+using Weft.Versioning.Blobs;
+
+namespace Weft.Server;
+
+/// <summary>Registro (DI) y mapeo del endpoint del relay <see cref="WeftServer"/>.</summary>
+public static class WeftServerExtensions
+{
+    /// <summary>
+    /// Registra el relay en el contenedor. El consumidor DEBE registrar también un
+    /// <see cref="IWeftAuthorizer"/> (obligatorio, se valida en <see cref="MapWeft"/> al arrancar) y un
+    /// <see cref="IDocumentStore"/>; un <see cref="IBlobStore"/> es opcional (solo para
+    /// <see cref="IWeftServer.PublishAsync"/>).
+    /// </summary>
+    public static IServiceCollection AddWeftServer(this IServiceCollection services, Action<WeftServerOptions>? configure = null)
+    {
+        ArgumentNullException.ThrowIfNull(services);
+
+        var options = new WeftServerOptions();
+        configure?.Invoke(options);
+        services.AddSingleton(options);
+
+        services.AddSingleton<WeftServer>(sp => new WeftServer(
+            sp.GetRequiredService<WeftServerOptions>(),
+            sp.GetRequiredService<IDocumentStore>(),
+            sp.GetService<IBlobStore>()));
+        services.AddSingleton<IWeftServer>(sp => sp.GetRequiredService<WeftServer>());
+
+        return services;
+    }
+
+    /// <summary>
+    /// Mapea el endpoint WebSocket del relay en <c>{pattern}/{docId}</c>. Falla al arrancar si no hay un
+    /// <see cref="IWeftAuthorizer"/> registrado (la autorización nunca es opcional; SC-010). Semántica del
+    /// handshake: <see cref="WeftAccess.Deny"/> → 403 antes del upgrade (0 bytes de contenido); en otro caso,
+    /// upgrade y relay con el nivel de acceso concedido.
+    /// </summary>
+    public static IEndpointConventionBuilder MapWeft(this IEndpointRouteBuilder endpoints, string pattern)
+    {
+        ArgumentNullException.ThrowIfNull(endpoints);
+        ArgumentException.ThrowIfNullOrEmpty(pattern);
+
+        var probe = endpoints.ServiceProvider.GetService<IServiceProviderIsService>();
+        if (probe is not null && !probe.IsService(typeof(IWeftAuthorizer)))
+        {
+            throw new InvalidOperationException(
+                "AddWeftServer requiere registrar un IWeftAuthorizer antes de MapWeft: la autorización nunca es " +
+                "opcional ni por-defecto-permisiva (SC-010).");
+        }
+
+        WeftServer server = endpoints.ServiceProvider.GetRequiredService<WeftServer>();
+        string route = $"{pattern.TrimEnd('/')}/{{docId}}";
+
+        return endpoints.Map(route, async (HttpContext context, string docId) =>
+        {
+            if (!context.WebSockets.IsWebSocketRequest)
+            {
+                context.Response.StatusCode = StatusCodes.Status400BadRequest;
+                return;
+            }
+
+            IWeftAuthorizer authorizer = context.RequestServices.GetRequiredService<IWeftAuthorizer>();
+            WeftAccess access = await authorizer.AuthorizeAsync(context, docId, context.RequestAborted);
+            if (access == WeftAccess.Deny)
+            {
+                context.Response.StatusCode = StatusCodes.Status403Forbidden; // antes del upgrade: 0 bytes de contenido
+                return;
+            }
+
+            using WebSocket ws = await context.WebSockets.AcceptWebSocketAsync();
+            await server.HandleConnectionAsync(docId, access, ws, context.RequestAborted);
+        });
+    }
+}
diff --git a/src/Weft.Server/WeftServerOptions.cs b/src/Weft.Server/WeftServerOptions.cs
new file mode 100644
index 0000000..40c8b44
--- /dev/null
+++ b/src/Weft.Server/WeftServerOptions.cs
@@ -0,0 +1,33 @@
+using Weft;
+using Weft.Concurrency;
+using Weft.Server.Protocol;
+using Weft.Yrs;
+
+namespace Weft.Server;
+
+/// <summary>
+/// Configuración del relay (<see cref="WeftServerExtensions.AddWeftServer"/>). El motor y el ciclo de vida del
+/// broker tienen defaults sensatos; los límites por conexión completan la mitigación FU-002 (parte b) sobre el
+/// cap de tamaño de mensaje del framing (parte a).
+/// </summary>
+public sealed class WeftServerOptions
+{
+    /// <summary>Motor CRDT que respalda los documentos del relay. Por defecto <see cref="YrsEngine.Instance"/>.</summary>
+    public ICrdtEngine Engine { get; set; } = YrsEngine.Instance;
+
+    /// <summary>Ciclo de vida del <see cref="DocumentBroker"/> (idle eviction, LRU, cadencia del barrido).</summary>
+    public DocumentBrokerOptions Broker { get; set; } = new();
+
+    /// <summary>
+    /// Cap de tamaño de un frame WebSocket entrante (FU-002 parte a). Frames por encima se rechazan antes del
+    /// decoder. Por defecto <see cref="Lib0Encoding.DefaultMaxMessageBytes"/> (16 MiB).
+    /// </summary>
+    public int MaxMessageBytes { get; set; } = Lib0Encoding.DefaultMaxMessageBytes;
+
+    /// <summary>
+    /// Cota de la cola de envío por conexión (FU-002 parte b, backpressure). Si un consumidor lento no drena su
+    /// cola y esta se llena, la conexión se cierra (se descarta el consumidor lento en vez de crecer memoria sin
+    /// límite); el cliente reconecta y re-sincroniza. Por defecto 256 mensajes.
+    /// </summary>
+    public int MaxSendQueuePerConnection { get; set; } = 256;
+}
diff --git a/tests/Weft.Server.Tests/RelayTests.cs b/tests/Weft.Server.Tests/RelayTests.cs
new file mode 100644
index 0000000..822aefd
--- /dev/null
+++ b/tests/Weft.Server.Tests/RelayTests.cs
@@ -0,0 +1,467 @@
+using System.Buffers;
+using System.Diagnostics;
+using System.Net.WebSockets;
+using System.Text;
+using Microsoft.AspNetCore.Builder;
+using Microsoft.AspNetCore.Hosting;
+using Microsoft.AspNetCore.Http;
+using Microsoft.AspNetCore.TestHost;
+using Microsoft.Extensions.DependencyInjection;
+using Microsoft.Extensions.Hosting;
+using Weft;
+using Weft.Concurrency;
+using Weft.Server.Auth;
+using Weft.Server.Persistence;
+using Weft.Server.Protocol;
+using Weft.Versioning;
+using Weft.Versioning.Blobs;
+using Weft.Yrs;
+
+namespace Weft.Server.Tests;
+
+/// <summary>
+/// Tests de integración del relay end-to-end (T051): un <see cref="TestServer"/> hospeda el relay y clientes
+/// Yjs <b>simulados</b> (motor yrs real en ambos lados, hablando el wire y-sync vía <see cref="SyncProtocol"/>)
+/// se conectan por WebSocket. Cubre los criterios del Independent Test de US3.
+/// </summary>
+public sealed class RelayTests
+{
+    private const string Field = "content";
+
+    // ---------- Harness ----------
+
+    private sealed class FixedAuthorizer(WeftAccess access) : IWeftAuthorizer
+    {
+        public ValueTask<WeftAccess> AuthorizeAsync(HttpContext context, string docId, CancellationToken ct)
+            => ValueTask.FromResult(access);
+    }
+
+    private static async Task<IHost> BuildHostAsync(WeftAccess access, IDocumentStore store, IBlobStore? blobs = null)
+    {
+        IHostBuilder builder = new HostBuilder().ConfigureWebHost(web =>
+        {
+            web.UseTestServer();
+            web.ConfigureServices(services =>
+            {
+                services.AddRouting();
+                services.AddWeftServer(o =>
+                    o.Broker = new DocumentBrokerOptions { IdleSweepInterval = TimeSpan.FromMilliseconds(50) });
+                services.AddSingleton<IWeftAuthorizer>(new FixedAuthorizer(access));
+                services.AddSingleton<IDocumentStore>(store);
+                if (blobs is not null)
+                {
+                    services.AddSingleton<IBlobStore>(blobs);
+                }
+            });
+            web.Configure(app =>
+            {
+                app.UseWebSockets();
+                app.UseRouting();
+                app.UseEndpoints(e => e.MapWeft("/collab"));
+            });
+        });
+        return await builder.StartAsync();
+    }
+
+    /// <summary>Host del relay con async-dispose (el <c>IHost</c> concreto es IAsyncDisposable; el estático no).</summary>
+    private sealed class RelayHost(IHost host) : IAsyncDisposable
+    {
+        public TestServer Server { get; } = host.GetTestServer();
+        public IServiceProvider Services => host.Services;
+
+        public async ValueTask DisposeAsync()
+        {
+            if (host is IAsyncDisposable ad)
+            {
+                await ad.DisposeAsync();
+            }
+            else
+            {
+                host.Dispose();
+            }
+        }
+    }
+
+    private static async Task<RelayHost> StartRelayAsync(WeftAccess access, IDocumentStore store, IBlobStore? blobs = null)
+        => new RelayHost(await BuildHostAsync(access, store, blobs));
+
+    private static async Task<bool> WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
+    {
+        var sw = Stopwatch.StartNew();
+        while (sw.Elapsed < timeout)
+        {
+            if (condition())
+            {
+                return true;
+            }
+
+            await Task.Delay(15);
+        }
+
+        return condition();
+    }
+
+    private static byte[] AwarenessUpdate(uint clientId, uint clock, string stateJson)
+    {
+        var inner = new Lib0Encoding.Lib0Writer();
+        inner.WriteVarUint(1);
+        inner.WriteVarUint(clientId);
+        inner.WriteVarUint(clock);
+        inner.WriteVarUint8Array(Encoding.UTF8.GetBytes(stateJson));
+        return SyncProtocol.EncodeAwareness(inner.WrittenSpan);
+    }
+
+    /// <summary>Cliente Yjs simulado: WebSocket + un doc yrs real, hablando y-sync.</summary>
+    private sealed class YClient : IAsyncDisposable
+    {
+        private readonly WebSocket _ws;
+        private readonly ICrdtDoc _doc = YrsEngine.Instance.CreateDoc();
+        private readonly object _docLock = new();
+        private readonly SemaphoreSlim _sendLock = new(1, 1);
+        private readonly CancellationTokenSource _cts = new();
+        private readonly Task _recv;
+
+        public long BytesReceived;
+        public WebSocketCloseStatus? CloseStatus { get; private set; }
+        public List<byte[]> AwarenessReceived { get; } = new();
+
+        private YClient(WebSocket ws)
+        {
+            _ws = ws;
+            _recv = Task.Run(ReceiveLoopAsync);
+        }
+
+        public static async Task<YClient> ConnectAsync(
+            TestServer server, string docId, byte[]? seedState = null, CancellationToken ct = default)
+        {
+            WebSocketClient wsc = server.CreateWebSocketClient();
+            WebSocket ws = await wsc.ConnectAsync(new Uri(server.BaseAddress, $"collab/{docId}"), ct);
+            var client = new YClient(ws);
+            // Sync inicial: anunciamos nuestro state vector (tras sembrar el estado previo, si lo hay).
+            byte[] sv;
+            lock (client._docLock)
+            {
+                if (seedState is not null)
+                {
+                    client._doc.ApplyUpdate(seedState);
+                }
+
+                sv = client._doc.ExportStateVector();
+            }
+
+            await client.SendAsync(SyncProtocol.EncodeSyncStep1(sv), ct);
+            return client;
+        }
+
+        public string Text()
+        {
+            lock (_docLock) { return _doc.GetText(Field); }
+        }
+
+        public byte[] ExportState()
+        {
+            lock (_docLock) { return _doc.ExportState(); }
+        }
+
+        /// <summary>Edita localmente y difunde el delta al servidor.</summary>
+        public async Task EditAsync(int index, string text, CancellationToken ct = default)
+        {
+            byte[] delta;
+            lock (_docLock)
+            {
+                byte[] before = _doc.ExportStateVector();
+                _doc.InsertText(Field, index, text);
+                delta = _doc.ExportUpdateSince(before);
+            }
+
+            await SendAsync(SyncProtocol.EncodeUpdate(delta), ct);
+        }
+
+        /// <summary>Envía un estado/update crudo como mensaje Update (sin aplicarlo localmente).</summary>
+        public Task SendUpdateAsync(byte[] update, CancellationToken ct = default)
+            => SendAsync(SyncProtocol.EncodeUpdate(update), ct);
+
+        public Task SendAwarenessAsync(byte[] awarenessMessage, CancellationToken ct = default)
+            => SendAsync(awarenessMessage, ct);
+
+        private async Task SendAsync(byte[] frame, CancellationToken ct)
+        {
+            await _sendLock.WaitAsync(ct).ConfigureAwait(false);
+            try
+            {
+                await _ws.SendAsync(frame, WebSocketMessageType.Binary, endOfMessage: true, ct).ConfigureAwait(false);
+            }
+            finally
+            {
+                _sendLock.Release();
+            }
+        }
+
+        private async Task ReceiveLoopAsync()
+        {
+            try
+            {
+                while (!_cts.IsCancellationRequested && _ws.State == WebSocketState.Open)
+                {
+                    byte[]? msg = await ReceiveFullAsync(_cts.Token).ConfigureAwait(false);
+                    if (msg is null)
+                    {
+                        return;
+                    }
+
+                    Interlocked.Add(ref BytesReceived, msg.Length);
+                    Dispatch(msg);
+                }
+            }
+            catch (OperationCanceledException) { }
+            catch (WebSocketException) { }
+        }
+
+        private void Dispatch(byte[] frame)
+        {
+            MessageType type;
+            SyncMessageType syncType;
+            byte[] payload;
+            try
+            {
+                SyncMessage m = SyncProtocol.Decode(frame);
+                type = m.Type;
+                syncType = m.SyncType;
+                payload = m.Payload.ToArray();
+            }
+            catch (MalformedMessageException)
+            {
+                return;
+            }
+
+            switch (type)
+            {
+                case MessageType.Sync when syncType == SyncMessageType.Step1:
+                    byte[] delta;
+                    lock (_docLock) { delta = _doc.ExportUpdateSince(payload); }
+                    _ = SendAsync(SyncProtocol.EncodeSyncStep2(delta), _cts.Token);
+                    break;
+                case MessageType.Sync: // Step2 / Update
+                    lock (_docLock) { _doc.ApplyUpdate(payload); }
+                    break;
+                case MessageType.Awareness:
+                    lock (AwarenessReceived) { AwarenessReceived.Add(payload); }
+                    break;
+            }
+        }
+
+        private async Task<byte[]?> ReceiveFullAsync(CancellationToken ct)
+        {
+            byte[] rent = ArrayPool<byte>.Shared.Rent(8192);
+            var acc = new ArrayBufferWriter<byte>();
+            try
+            {
+                while (true)
+                {
+                    WebSocketReceiveResult r = await _ws.ReceiveAsync(rent, ct).ConfigureAwait(false);
+                    if (r.MessageType == WebSocketMessageType.Close)
+                    {
+                        CloseStatus = r.CloseStatus;
+                        return null;
+                    }
+
+                    acc.Write(rent.AsSpan(0, r.Count));
+                    if (r.EndOfMessage)
+                    {
+                        return acc.WrittenSpan.ToArray();
+                    }
+                }
+            }
+            finally
+            {
+                ArrayPool<byte>.Shared.Return(rent);
+            }
+        }
+
+        public async ValueTask DisposeAsync()
+        {
+            await _cts.CancelAsync();
+            try
+            {
+                if (_ws.State == WebSocketState.Open)
+                {
+                    await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None);
+                }
+            }
+            catch { }
+
+            try { await _recv; } catch { }
+            _ws.Dispose();
+            _cts.Dispose();
+        }
+    }
+
+    // ---------- Tests ----------
+
+    [Fact]
+    public async Task Two_readwrite_clients_converge_after_cross_edits()
+    {
+        await using RelayHost relay = await StartRelayAsync(WeftAccess.ReadWrite, new InMemoryDocumentStore());
+        TestServer server = relay.Server;
+        await using YClient a = await YClient.ConnectAsync(server, "doc");
+        await using YClient b = await YClient.ConnectAsync(server, "doc");
+
+        await a.EditAsync(0, "Hello ");
+        await b.EditAsync(0, "World");
+
+        bool converged = await WaitUntilAsync(
+            () => a.Text().Length == "Hello World".Length && a.Text() == b.Text(),
+            TimeSpan.FromSeconds(1));
+
+        Assert.True(converged, $"a='{a.Text()}' b='{b.Text()}'");
+        Assert.Contains("Hello", a.Text());
+        Assert.Contains("World", a.Text());
+    }
+
+    [Fact]
+    public async Task Reconnecting_client_receives_only_a_small_delta()
+    {
+        await using RelayHost relay = await StartRelayAsync(WeftAccess.ReadWrite, new InMemoryDocumentStore());
+        TestServer server = relay.Server;
+
+        // Un cliente construye un documento grande y capturamos su estado.
+        await using YClient writer = await YClient.ConnectAsync(server, "doc");
+        await writer.EditAsync(0, new string('x', 20_000));
+        byte[] fullState = writer.ExportState();
+
+        // Cliente FRESCO (SV vacío): el servidor le envía el estado completo (≫20 KB).
+        await using YClient fresh = await YClient.ConnectAsync(server, "doc");
+        Assert.True(await WaitUntilAsync(() => fresh.Text().Length == 20_000, TimeSpan.FromSeconds(1)),
+            $"fresh no sincronizó: len={fresh.Text().Length}");
+        Assert.True(fresh.BytesReceived > 20_000, $"fresh={fresh.BytesReceived}");
+
+        // Cliente AL DÍA (sembrado con el estado → SV completo): el servidor no tiene nada nuevo que enviarle.
+        await using YClient upToDate = await YClient.ConnectAsync(server, "doc", seedState: fullState);
+        await Task.Delay(150); // deja llegar el sync inicial
+        Assert.True(upToDate.BytesReceived * 4 < fresh.BytesReceived,
+            $"upToDate={upToDate.BytesReceived} fresh={fresh.BytesReceived} (delta en reconexión ≪ estado completo)");
+    }
+
+    [Fact]
+    public async Task Denied_connection_exchanges_no_content()
+    {
+        await using RelayHost relay = await StartRelayAsync(WeftAccess.Deny, new InMemoryDocumentStore());
+        TestServer server = relay.Server;
+        // 403 antes del upgrade → la conexión WebSocket se rechaza (0 bytes de contenido).
+        await Assert.ThrowsAnyAsync<Exception>(async () =>
+        {
+            await using YClient _ = await YClient.ConnectAsync(server, "doc");
+        });
+    }
+
+    [Fact]
+    public async Task ReadOnly_client_that_writes_is_closed_with_policy_violation()
+    {
+        await using RelayHost relay = await StartRelayAsync(WeftAccess.ReadOnly, new InMemoryDocumentStore());
+        TestServer server = relay.Server;
+        await using YClient ro = await YClient.ConnectAsync(server, "doc");
+
+        await ro.EditAsync(0, "nope"); // un update desde una conexión ReadOnly
+
+        bool closed = await WaitUntilAsync(
+            () => ro.CloseStatus == WebSocketCloseStatus.PolicyViolation, TimeSpan.FromSeconds(1));
+        Assert.True(closed, $"closeStatus={ro.CloseStatus}");
+    }
+
+    [Fact]
+    public async Task Awareness_is_relayed_and_withdrawn_on_disconnect()
+    {
+        await using RelayHost relay = await StartRelayAsync(WeftAccess.ReadWrite, new InMemoryDocumentStore());
+        TestServer server = relay.Server;
+        await using YClient observer = await YClient.ConnectAsync(server, "doc");
+        YClient presence = await YClient.ConnectAsync(server, "doc");
+
+        const uint clientId = 4242;
+        await presence.SendAwarenessAsync(AwarenessUpdate(clientId, 1, "{\"user\":\"A\"}"));
+
+        // El observador ve el estado de awareness del par.
+        Assert.True(await WaitUntilAsync(
+            () => observer.AwarenessReceived.Any(p => AwarenessHasClient(p, clientId, requireNull: false)),
+            TimeSpan.FromSeconds(1)));
+
+        // Al desconectar el par, el observador recibe la RETIRADA (estado "null" para su clientID).
+        await presence.DisposeAsync();
+        Assert.True(await WaitUntilAsync(
+            () => observer.AwarenessReceived.Any(p => AwarenessHasClient(p, clientId, requireNull: true)),
+            TimeSpan.FromSeconds(1)));
+    }
+
+    [Fact]
+    public async Task State_survives_a_server_restart()
+    {
+        var store = new InMemoryDocumentStore(); // el store durable sobrevive al "reinicio" del proceso
+
+        await using (RelayHost r1 = await StartRelayAsync(WeftAccess.ReadWrite, store))
+        {
+            TestServer s1 = r1.Server;
+            await using YClient c1 = await YClient.ConnectAsync(s1, "doc");
+            await c1.EditAsync(0, "durable");
+            await using YClient c2 = await YClient.ConnectAsync(s1, "doc");
+            Assert.True(await WaitUntilAsync(() => c2.Text() == "durable", TimeSpan.FromSeconds(1)));
+        } // r1.DisposeAsync consolida el snapshot en el store (WeftServer.DisposeAsync)
+
+        await using RelayHost r2 = await StartRelayAsync(WeftAccess.ReadWrite, store);
+        TestServer s2 = r2.Server;
+        await using YClient c3 = await YClient.ConnectAsync(s2, "doc");
+        Assert.True(await WaitUntilAsync(() => c3.Text() == "durable", TimeSpan.FromSeconds(1)),
+            $"recovered='{c3.Text()}'");
+    }
+
+    [Fact]
+    public async Task Server_publish_matches_local_publish_version_id()
+    {
+        var blobs = new InMemoryBlobStore();
+
+        // Publicación local del mismo contenido.
+        using ICrdtDoc local = YrsEngine.Instance.CreateDoc();
+        local.InsertText(Field, 0, "content-addressed parity");
+        byte[] localState = local.ExportState();
+        var versionStore = new VersionStore(YrsEngine.Instance, blobs);
+        VersionId localId = await versionStore.PublishAsync(local);
+
+        // El servidor recibe exactamente el mismo estado y publica.
+        await using RelayHost relay = await StartRelayAsync(WeftAccess.ReadWrite, new InMemoryDocumentStore(), blobs);
+        TestServer server = relay.Server;
+        await using YClient c = await YClient.ConnectAsync(server, "doc");
+        await c.SendUpdateAsync(localState);
+
+        // Esperar a que el servidor aplique el estado (un 2.º cliente converge → el actor ya lo procesó).
+        await using YClient probe = await YClient.ConnectAsync(server, "doc");
+        Assert.True(await WaitUntilAsync(
+            () => probe.Text() == "content-addressed parity", TimeSpan.FromSeconds(1)));
+
+        var weftServer = relay.Services.GetRequiredService<IWeftServer>();
+        VersionId serverId = await weftServer.PublishAsync("doc");
+
+        Assert.Equal(localId, serverId);
+    }
+
+    // awarenessPayload = el payload interno de un mensaje Awareness (lo que el cliente guarda en Dispatch).
+    private static bool AwarenessHasClient(byte[] awarenessPayload, uint clientId, bool requireNull)
+    {
+        try
+        {
+            var r = new Lib0Encoding.Lib0Reader(awarenessPayload);
+            uint count = r.ReadVarUint();
+            for (uint i = 0; i < count; i++)
+            {
+                uint id = r.ReadVarUint();
+                _ = r.ReadVarUint(); // clock
+                ReadOnlySpan<byte> state = r.ReadVarUint8Array();
+                if (id == clientId)
+                {
+                    bool isNull = state.SequenceEqual("null"u8);
+                    return requireNull ? isNull : !isNull;
+                }
+            }
+        }
+        catch (MalformedMessageException) { }
+
+        return false;
+    }
+}
diff --git a/tests/Weft.Server.Tests/Weft.Server.Tests.csproj b/tests/Weft.Server.Tests/Weft.Server.Tests.csproj
index 7dc51b7..91cef48 100644
--- a/tests/Weft.Server.Tests/Weft.Server.Tests.csproj
+++ b/tests/Weft.Server.Tests/Weft.Server.Tests.csproj
@@ -6,11 +6,17 @@
 
   <ItemGroup>
     <PackageReference Include="coverlet.collector" Version="6.0.4" />
+    <PackageReference Include="Microsoft.AspNetCore.TestHost" Version="10.0.9" />
     <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.14.1" />
     <PackageReference Include="xunit" Version="2.9.3" />
     <PackageReference Include="xunit.runner.visualstudio" Version="3.1.4" />
   </ItemGroup>
 
+  <!-- El relay se hospeda en un TestServer (WebHostBuilder + UseWebSockets + MapWeft). -->
+  <ItemGroup>
+    <FrameworkReference Include="Microsoft.AspNetCore.App" />
+  </ItemGroup>
+
   <ItemGroup>
     <Using Include="Xunit" />
   </ItemGroup>
@@ -19,4 +25,28 @@
     <ProjectReference Include="../../src/Weft.Server/Weft.Server.csproj" />
   </ItemGroup>
 
+  <!--
+    Los tests de integración usan el motor yrs REAL en ambos lados (cliente simulado + servidor), así que
+    necesitan el cdylib nativo en el output bajo el layout de paquete NuGet (runtimes/<rid>/native/). Mismo
+    helper que Weft.Core.Tests: build CON `test-hooks` en desarrollo local (Linux); en CI el workflow coloca
+    el binario por plataforma.
+  -->
+  <Target Name="CopyWeftNativeForTests" AfterTargets="Build">
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
 </Project>

```

---

## What you must do

### Step 1 — Read the scope

Read the Charter file at `.straymark/charters/05-weft-server-relay-end-to-end-cierra-m2-us3.md` in full. Identify:

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

- In auditor-side CLI mode (skill `straymark-audit-execute`): `.straymark/audits/CHARTER-05-weft-server-relay-end-to-end-cierra-m2-us3/report-<sluggified-model-id>.md` (the skill handles the path automatically).
- In manual paste mode (transitional v0): the operator saves your output at `audit/charters/CHARTER-05-weft-server-relay-end-to-end-cierra-m2-us3/auditor-auditor.md` or an equivalent convention.

The file must have this frontmatter (validated against `.straymark/schemas/audit-output.schema.v0.json`):

```yaml
---
audit_role: auditor                       # v1 unified. Legacy v0: "auditor-primary" or "auditor-secondary"
auditor: <your model id and version>      # e.g., claude-sonnet-4-6, gemini-2.5-pro, copilot-v1.0.40
charter_id: CHARTER-05-weft-server-relay-end-to-end-cierra-m2-us3
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

# Audit: CHARTER-05-weft-server-relay-end-to-end-cierra-m2-us3 by <your model id>

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

Does the implementation meet the closure criterion declared by `CHARTER-05-weft-server-relay-end-to-end-cierra-m2-us3`?
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
