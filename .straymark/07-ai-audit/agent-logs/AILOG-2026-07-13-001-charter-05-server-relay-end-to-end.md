---
id: AILOG-2026-07-13-001
title: "CHARTER-05: Weft.Server relay end-to-end — cierra M2/US3 (T047–T049, T051, T052)"
status: draft
created: 2026-07-13
agent: claude-opus-4-8
confidence: high
review_required: true
reviewed_by: ""
reviewed_at: ""
review_outcome: pending
risk_level: high
eu_ai_act_risk: not_applicable
nist_genai_risks: []
iso_42001_clause: []
lines_changed: 1819
files_modified: []
observability_scope: none
tags: [server, relay, websocket, y-sync, awareness, concurrency, publish, wire-compat, ffi-boundary, fu-002]
related: [AILOG-2026-07-12-001, AILOG-2026-07-11-001, AIDEC-2026-07-13-001]
originating_charter: CHARTER-05-weft-server-relay-end-to-end-cierra-m2-us3
---

# AILOG: CHARTER-05 — Weft.Server relay end-to-end (cierra M2/US3)

## Summary

Segundo y último corte de M2/US3 (effort L): el **relay WebSocket y-sync end-to-end** sobre el substrato de
CHARTER-04, que **cierra el hito M2**. Un connection handler hace el handshake (authz→403/1008/1002), sincroniza
incrementalmente en ambas direcciones, relaya cada update entrante a los pares vía
`DocumentSession.UpdateApplied` (anclaje M1) y lo persiste, difunde awareness y retira el estado al cerrar.
DI (`AddWeftServer`/`MapWeft`) y el servicio `IWeftServer` (`PublishAsync` con paridad de `VersionId`
server↔local, `GetConnectionCountAsync`, `DisconnectAllAsync`). Verificado con **7 tests de integración**
(2 clientes yrs reales por WebSocket) y, de forma decisiva, con un **check headless de compat del wire usando
`yjs`/`y-websocket` reales** que converge contra el relay — retira R1. Completa **FU-002 parte b** (límites por
conexión / backpressure).

## Context

Ejecución de T047–T049, T051, T052 bajo `.straymark/charters/05-*.md`, sobre CHARTER-04 (códec + stores +
authorizer) y las superficies de concurrencia de M1 (`DocumentBroker`/`DocumentSession`, CHARTER-03). Trabajo de
**implementación** contra el contrato congelado `contracts/server-api.md` (API v1). El relay no toca `ICrdtDoc`
ni el motor: aplica **todo** update de red vía `DocumentSession` (turno del actor, P-V), publica dentro del
turno (paridad `VersionId`, P-III), y maneja blobs opacos de `IDocumentStore` (P-IV). El input de red no
confiable tensa P-I/P-II: se completa la mitigación FU-002 con la parte b sobre el cap de tamaño (parte a).

## Actions Performed

1. **Referencias de proyecto**: `Weft.Server.csproj` gana `ProjectReference` a `Weft.Core` (broker/sesión) y
   `Weft.Versioning` (VersionStore/VersionId/IBlobStore). Hasta CHARTER-04 solo referenciaba ASP.NET Core.
2. **Opciones (T048)**: `WeftServerOptions` — `Engine` (default `YrsEngine.Instance`), `Broker`
   (`DocumentBrokerOptions`), `MaxMessageBytes` (cap FU-002 a) y `MaxSendQueuePerConnection` (backpressure, b).
3. **Connection handler (T047)**: `WeftConnection` — send pump (cola acotada → socket) + receive loop
   (acumula el frame con enforcement del cap, decodifica y-sync, despacha). Handshake: `Deny`→403 antes del
   upgrade; `ReadOnly` que envía update de documento→1008; frame malformado→1002. Sync inicial `SyncStep1`;
   `SyncStep1` del cliente→`SyncStep2(delta)`; `Update`→aplicar+persistir; awareness→relay a pares + tracking
   de clientIDs.
4. **Hub por documento**: `DocumentHub` — una `DocumentSession` por doc (suscripción única a `UpdateApplied`,
   broadcast perezoso); `ApplyAndPersistAsync` (turno del actor + `IDocumentStore.AppendUpdate`); snapshot de
   compaction al disponer. `AwarenessProtocol` — parsing mínimo de clientIDs para la retirada (FR-015).
5. **DI + endpoint (T048)**: `WeftServerExtensions` — `AddWeftServer(options)` (registra `WeftServer` singleton
   + `IWeftServer`); `MapWeft(path)` mapea `path/{docId}`, **falla al arrancar** sin `IWeftAuthorizer`
   (`IServiceProviderIsService`), `Deny`→403 antes del upgrade.
6. **Servicio (T049)**: `WeftServer` — registro de hubs sobre `DocumentBroker` (con `OnEvicting`→snapshot y
   loader que reconstruye desde el store); `PublishAsync` captura `ExportState` **dentro del turno del actor**
   → `VersionId.FromBlob` + `IBlobStore.PutAsync` (paridad); `GetConnectionCountAsync`; `DisconnectAllAsync`.
7. **Tests (T051)**: `RelayTests` — convergencia de 2 clientes tras ediciones cruzadas; delta en reconexión con
   bytes medidos (fresco vs sembrado); `Deny` sin bytes de contenido; `ReadOnly`→1008; awareness relay +
   retirada; restart-recovery (store durable sobrevive al reinicio); paridad `VersionId` server↔local.
8. **Samples (T052)**: `Weft.Sample.Server` (relay + `FileSystemDocumentStore` + authorizer demo, hosting
   mínimo) + `samples/tiptap-client` (editor Tiptap browser + **check headless `yjs`/`y-websocket`**).

## Modified Files

| File | Change Description |
|------|--------------------|
| `src/Weft.Server/Weft.Server.csproj` | Change — ProjectReference a Weft.Core + Weft.Versioning |
| `src/Weft.Server/WeftServerOptions.cs` | New — opciones (Engine, Broker, límites FU-002 a/b) (T048) |
| `src/Weft.Server/WeftConnection.cs` | New — connection handler (handshake/authz, sync, awareness, límites) (T047) |
| `src/Weft.Server/DocumentHub.cs` | New (scope expansion) — hub por doc: sesión única + broadcast + persistencia |
| `src/Weft.Server/Protocol/AwarenessProtocol.cs` | New (scope expansion) — parsing mínimo de clientIDs (retirada, FR-015) |
| `src/Weft.Server/WeftServerExtensions.cs` | New — AddWeftServer/MapWeft (T048) |
| `src/Weft.Server/WeftServer.cs` | New — IWeftServer + registro de hubs + broker + publish (T049) |
| `tests/Weft.Server.Tests/RelayTests.cs` | New — integración 2 clientes yrs + harness TestServer (T051) |
| `tests/Weft.Server.Tests/Weft.Server.Tests.csproj` | Change — TestHost + FrameworkReference + copia del cdylib |
| `samples/Weft.Sample.Server/**` | New — sample relay (T052) |
| `samples/tiptap-client/**` | New — cliente Tiptap + check headless de compat (T052) |
| `Weft.sln` | Change — añadido Weft.Sample.Server |
| `specs/001-weft-crdt-versioning/tasks.md` | Change — T047–T049, T051, T052 `[X] — CHARTER-05` |
| `.gitignore` | Change — node_modules/ + weft-data/ |
| `.straymark/charters/05-*.md` | Change — status declared → in-progress |

## Decisions Made

- **Paridad de `VersionId` (P-III)**: `PublishAsync` captura `ExportState()` **dentro del turno del actor**
  (`DocumentSession.ExecuteAsync`), luego `VersionId.FromBlob` + `IBlobStore.PutAsync` fuera del turno —
  byte-idéntico a `VersionStore.PublishAsync` por construcción, sin depender de su orden interno. AIDEC §1.
- **Broadcast a TODAS las conexiones (incluido el origen)**: reaplicar el propio delta es un no-op CRDT
  idempotente; evita rastrear el origen dentro del turno del actor (que sería una carrera). AIDEC §2.
- **Retirada de awareness (FR-015)**: parsing mínimo de clientIDs por conexión → mensaje de "offline" (estado
  `null`, clock+1) al cerrar. El relay no interpreta el contenido del estado. AIDEC §3.
- **Backpressure (FU-002 parte b)**: cola de envío acotada por conexión; si se llena (consumidor lento), se
  cierra la conexión (se descarta el consumidor lento en vez de crecer memoria) — el cliente reconecta. AIDEC §4.
- **Hub por documento**: una `DocumentSession` por doc (no por conexión), refcount implícito por conexiones;
  snapshot de compaction al disponer + `OnEvicting`→snapshot en el broker. Loader reconstruye desde records.

## Impact

- **Functionality**: relay colaborativo end-to-end compatible con el ecosistema Yjs sin adaptación; cierra M2.
- **Security/Memory**: FU-002 completado (cap de tamaño + límites por conexión/backpressure); authz nunca
  por-defecto-permisiva (falla al arrancar sin authorizer; Deny→403; ReadOnly→1008).
- **Performance**: broadcast perezoso (delta solo si hay handler); hot path sin locks globales (el `_hubGate`
  solo serializa join/leave, no el relay de updates).

## Verification

- [x] `dotnet build Weft.sln -c Release` — 0 warnings / 0 errores
- [x] `dotnet test` — **107 verdes** (Server 53, Core 27, Versioning 25, Determinism 2); M0/M1/M2-corte1 intactos
- [x] **Check headless de compat del wire** (`yjs`/`y-websocket` reales ↔ relay): 2 docs Yjs convergen — **R1 retirada**
- [x] **ASan/LSan** sobre el workspace nativo — 12 tests, 0 fugas (P-II; native sin cambios en este corte)
- [ ] Revisión humana del operador — pendiente (`review_required: true`)
- [ ] **Auditoría externa multi-modelo** — OBLIGATORIA al cerrar (cierra M2); pendiente

## Additional Notes

### Compat del wire: validación headless en vez de navegador

El sample incluye un cliente Tiptap para la validación manual en navegador (quickstart §US3), pero el gate de
compat se retiró de forma **programática** con un check headless (`samples/tiptap-client/wire-check.mjs`): dos
`Y.Doc` reales de Yjs conectados vía `y-websocket` convergen a través del relay. Esto prueba que los updates de
yrs (servidor) y Yjs (cliente) son intercambiables a nivel binario — más fuerte y reproducible que un navegador.
El wire de y-websocket (`[msgType][syncType][VarUint8Array]`) coincide exactamente con `SyncProtocol`.

### Scope expansion (drift)

`straymark charter drift --range origin/main..HEAD` reporta 12 archivos "modificados pero no declarados". Se
clasifican en tres grupos:

- **FP del parser (declarados, no matcheados)** — `Weft.sln`, `src/Weft.Server/Weft.Server.csproj`,
  `tests/Weft.Server.Tests/Weft.Server.Tests.csproj`, `samples/Weft.Sample.Server/Weft.Sample.Server.csproj`:
  el parser de §Files no matchea rutas `.sln`/`.csproj` (limitación conocida, reportada upstream en
  **StrangeDaysTech/straymark#354**). Todos están declarados en §Files to modify.
- **Dir declarado, archivos listados por el parser** — `samples/tiptap-client/{README.md,index.html,
  package.json,src/main.js,wire-check.mjs}`: el charter declaró `samples/tiptap-client/` (el directorio); el
  parser enumera sus archivos. No hay expansión real.
- **Expansión intencional real** — `src/Weft.Server/DocumentHub.cs` y `src/Weft.Server/Protocol/AwarenessProtocol.cs`:
  descomposición interna del handler (T047) — el hub por-documento (sesión única + broadcast) y el parsing
  mínimo de awareness para la retirada; dentro del alcance funcional de T047. `.gitignore`: incidental
  (`node_modules/` + `weft-data/` de los samples). Se reflejan en §Files to modify al cierre (format v4).

### FU-002 cerrado

Este corte entrega la parte b (límites por conexión + backpressure + malformed→1002), completando la mitigación
iniciada en CHARTER-04 (parte a). **FU-002 pasa a `closed`** al cerrar este Charter.

### Decisiones candidatas a AIDEC

Paridad de publish, broadcast-a-todos, retirada de awareness y backpressure se documentan en AIDEC-2026-07-13-001.
