---
id: AIDEC-2026-07-13-001
title: "CHARTER-05: decisiones del relay — paridad de publish, broadcast, retirada de awareness, backpressure"
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
tags: [server, relay, y-sync, awareness, backpressure, publish, concurrency, fu-002]
related: [AILOG-2026-07-13-001]
---

# AIDEC: decisiones de implementación del relay (CHARTER-05)

> Registra las cuatro decisiones sustantivas del relay `Weft.Server` sobre el contrato congelado
> `contracts/server-api.md`, descubiertas al cablear el handler contra las superficies de M1. Quedan fijas para
> CHARTER-06 y para la auditoría externa del cierre de M2.

## Context

CHARTER-05 implementa el connection handler, el DI y `IWeftServer` sobre el substrato de CHARTER-04 y las
superficies de concurrencia de M1 (`DocumentBroker`/`DocumentSession`). El contrato fija las **firmas** y la
**semántica observable** (403/1008/1002, paridad de `VersionId`, awareness efímero), pero deja abierta la
**mecánica** de cuatro puntos con consecuencias de corrección, seguridad o compatibilidad.

---

## Decisión 1 — Paridad de `VersionId` en `PublishAsync` (P-III)

### Problem

`IWeftServer.PublishAsync(docId)` debe producir **el mismo `VersionId`** que produciría `VersionStore` al
publicar el mismo contenido en local (SC de paridad), y ejecutar dentro del turno del actor para un snapshot
consistente bajo tráfico concurrente. `DocumentSession.ExecuteAsync` es **síncrono** (`Func<ICrdtDoc,T>`): no se
puede `await` la escritura async del blob dentro del turno.

### Alternatives Considered

- **A1 — `ExecuteAsync(doc => versionStore.PublishAsync(doc, ct))`**: reusa `VersionStore` tal cual. Contras:
  depende de que `PublishAsync` haga `ExportState()` **antes** de su primer `await` (acoplamiento implícito con
  el orden interno de otra assembly); doble `await` sobre `ValueTask<ValueTask<…>>`. **Rechazada** (frágil).
- **A2 (elegida) — Capturar el snapshot dentro del turno, publicar los bytes fuera.**
  `byte[] s = await session.ExecuteAsync(d => d.ExportState())`; `VersionId.FromBlob(s)`; `IBlobStore.PutAsync(id, s)`.

### Rationale

`ExportState()` es la operación determinista (P-III) que `VersionStore.PublishAsync` usa; `FromBlob(ExportState())`
reproduce el `VersionId` local **byte a byte** por construcción, sin depender del orden interno de VersionStore.
El `ExportState` ocurre explícitamente dentro del turno del actor (snapshot consistente); el `PutAsync` (bytes
inmutables) va fuera del turno (P-V no se viola: el doc no se toca tras el export). Verificado en
`RelayTests.Server_publish_matches_local_publish_version_id`.

### Consequences

`WeftServer` toma un `IBlobStore` opcional; `PublishAsync` lanza si no hay uno registrado ("requiere IBlobStore").

---

## Decisión 2 — Broadcast del delta a TODAS las conexiones (incluido el origen)

### Problem

`DocumentSession.UpdateApplied` (anclaje M1) es un evento **por documento**, no por origen: entrega el delta pero
no qué conexión lo originó. ¿Cómo se difunde sin re-enviar al emisor, si el origen no está en el evento?

### Alternatives Considered

- **B1 — Rastrear el origen (thread/async-local) alrededor de `ApplyUpdateAsync`**: excluiría el eco. Contras:
  los applies concurrentes de distintas conexiones se serializan en el actor pero el "set origin" ocurre fuera →
  **carrera** que atribuiría mal el origen. **Rechazada**.
- **B2 (elegida) — Difundir a todas; el origen reaplica su propio delta.**

### Rationale

Reaplicar un delta ya integrado es un **no-op CRDT idempotente**, y los clientes Yjs lo toleran (validado por el
check headless con `y-websocket`). Evita la carrera de B1 y mantiene el broadcast dentro del turno del actor sin
estado extra. Coste: un eco al emisor (ancho de banda), aceptable para v1; optimizable con etiquetado de origen
por mensaje si hiciera falta.

### Consequences

El emisor recibe su propio update de vuelta. La awareness sí excluye el origen (se relaya en el receive loop del
handler, donde el origen es conocido, sin la carrera de B1).

---

## Decisión 3 — Retirada de awareness al cerrar (FR-015)

### Problem

Al cerrar una conexión hay que difundir la **retirada** de su estado de awareness. Pero el relay trata el estado
como opaco (CHARTER-04 no parsea awareness); no conoce los `clientID` que la conexión anunció.

### Alternatives Considered

- **C1 — Confiar solo en el timeout de awareness de Yjs** (los pares expiran al par tras ~30 s). Contras: no
  cumple "difundir la retirada" (FR-015); deja cursores fantasma hasta el timeout. **Insuficiente**.
- **C2 (elegida) — Parsing mínimo de `clientID` por conexión + mensaje de retirada al cerrar.**

### Rationale

`AwarenessProtocol` (interno) parsea solo la lista de `clientID`/`clock` de cada awareness update (salta el
estado, que sigue opaco) y los acumula por conexión. Al cerrar, `EncodeRemoval` emite un awareness update con
estado `null` y `clock+1` por clientID —exactamente como `y-protocols/awareness`— difundido a los pares.
Verificado en `RelayTests.Awareness_is_relayed_and_withdrawn_on_disconnect`.

### Consequences

El relay parsea la envoltura de awareness (no el contenido). Si un payload no parsea, el tracking se ignora
(best-effort); el broadcast del mensaje en sí no se ve afectado.

---

## Decisión 4 — Backpressure por conexión (FU-002 parte b)

### Problem

El cap de tamaño (parte a) acota **un** frame, pero un consumidor lento cuyo socket no drena haría crecer la
memoria del servidor sin límite. ¿Cómo se acotan los recursos por conexión?

### Alternatives Considered

- **D1 — Cola de envío ilimitada**: memoria no acotada ante un consumidor lento (el DoS que FU-002 evita). **Rechazada**.
- **D2 — Descartar mensajes al llenarse la cola**: rompería la convergencia (updates perdidos). **Rechazada**.
- **D3 (elegida) — Cola acotada por conexión; al llenarse, cerrar la conexión.**

### Rationale

Cola de envío acotada (`MaxSendQueuePerConnection`, default 256) por conexión; `TryEnqueue` no bloquea (se llama
desde el turno del actor) y, si la cola está llena, **cierra la conexión** (se descarta el consumidor lento en
vez de crecer memoria). El cliente reconecta y re-sincroniza desde el estado del servidor (SC-004) — sin pérdida
de datos, memoria acotada. Junto al path malformed→1002 y el cap de tamaño, completa FU-002.

### Consequences

Un pico transitorio de latencia de un cliente puede cerrarlo; la reconexión con delta lo recupera barato. **FU-002
pasa a `closed`** al cerrar este Charter.
