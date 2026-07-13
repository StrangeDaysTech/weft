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
