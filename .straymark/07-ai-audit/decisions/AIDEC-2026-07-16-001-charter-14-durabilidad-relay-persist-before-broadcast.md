---
id: AIDEC-2026-07-16-001
title: "CHARTER-14: durabilidad del relay — persist-before-broadcast por defecto (supersede de AIDEC-2026-07-13-001 §5)"
status: accepted
created: 2026-07-16
agent: claude-opus-4-8
confidence: high
review_required: true
risk_level: medium
eu_ai_act_risk: not_applicable
nist_genai_risks: []
iso_42001_clause: []
tags: [relay, durability, persist-before-broadcast, fsync, backpressure, ordering]
related: [AILOG-2026-07-16-003, AIDEC-2026-07-13-001]
originating_charter: CHARTER-14-durabilidad-relay
---

# AIDEC: durabilidad del relay — persist-before-broadcast por defecto

> Registra las decisiones de diseño de CHARTER-14 (FU-010). **Supersede la decisión §5 de
> AIDEC-2026-07-13-001** (CHARTER-05), que fijó broadcast-then-persist como comportamiento del relay con
> la auto-sanación CRDT como red. Aquella decisión sigue siendo válida como *modo opcional*; deja de ser
> el default.

## Context

El relay difundía cada update a los pares **antes** de persistirlo (`DocumentSession.UpdateApplied` se
dispara dentro del turno del actor, antes de `AppendUpdateAsync`). AIDEC-2026-07-13-001 §5 lo justificó:
en single-node, un append fallido + crash se recupera por re-sync en la reconexión. La auditoría de
CHARTER-05 (F3) lo marcó como follow-up (FU-010) para cuando se requieran garantías de durabilidad duras.
El operador decidió implementarlo como último Charter del vaciado del backlog, con tres sub-decisiones
tomadas ex-ante (fsync en scope, default persist-first, cobertura de carga). Este AIDEC registra el
*cómo* y el *porqué* de la forma concreta.

---

## Decisión 1 — Default = PersistThenBroadcast (no el comportamiento heredado)

### Problem

¿El default debe ser el orden seguro (ningún par ve lo no persistido) o el heredado (menor latencia,
auto-sanación como red)? Cambiar el default es un breaking change si el repo ya es público.

### Decision

**Default `PersistThenBroadcast`**, con `BroadcastThenPersist` como válvula de escape opt-in
(`WeftServerOptions.Durability`).

### Rationale

- El coste es **una latencia que el emisor ya paga**: `AppendUpdateAsync` ya se espera en el receive
  loop antes de leer el siguiente frame; invertir el orden no añade I/O a ningún hot path, solo mueve el
  broadcast a después del append. **Medido** (carga del relay, ambos modos, `FileSystemDocumentStore`
  con fsync): p50 y p99 **idénticos** (2.1ms), el coste del orden seguro es ~0 en percentiles típicos;
  solo el `max` sube (18.6ms vs 2.5ms) por picos ocasionales de fsync. La premisa del diseño se confirma
  con datos, no con criterio.
- El semantic seguro debe ser el default. La garantía que compra —*ningún par observa estado que el
  servidor no haya aceptado de forma durable*— es la que un operador espera por defecto de un relay.
- El repo aún no es público (FU-014): cambiar el default ahora es gratis; hacerlo tras el publish sería
  breaking. Es el momento correcto.

---

## Decisión 2 — Fallo de append ⇒ cerrar TODAS las conexiones del documento (1011)

### Problem

Con persist-first, un append fallido deja el update en el doc vivo pero **nunca difundido**. Los pares
quedan callados y desactualizados; la próxima edición difunde solo su delta, jamás el que faltó →
divergencia permanente. ¿Cómo se recupera?

### Decision

En `PersistThenBroadcast`, un fallo de `AppendUpdateAsync` dispara `DocumentHub.DisconnectAll()` y la
conexión emisora cierra con **1011** (InternalError). Todos reconectan, mandan SyncStep1, y el servidor
—autoritativo, que sí tiene el update en el doc vivo— reenvía el estado. Convergencia recuperada.

### Rationale

Es el **único modo de equivocarse** en persist-first, así que la remediación es obligatoria, no
opcional. Cerrar el documento entero es contundente pero correcto: fuerza el re-sync autoritativo desde
la única fuente que tiene el estado completo. Retry/backoff es responsabilidad del adaptador del store,
no de Weft; los clientes reconectan solos. Cubierto por un test con fallo de append inyectado
determinista (sin matar procesos): se aserta que ningún par recibió el delta, que la conexión cerró
1011, y que un cliente que reconecta resincroniza.

---

## Decisión 3 — fsync de archivo + directorio en FileSystemDocumentStore

### Problem

`FlushAsync` solo vacía al page cache del SO; `File.Move` no es durable sin fsync del directorio. Sin
fsync, persist-first protege solo contra crash de **proceso**, no de máquina — y el trigger de FU-010
dice «SLA de no-pérdida».

### Decision

`fs.Flush(flushToDisk: true)` (síncrono; no hay fsync async en .NET) + fsync del directorio contenedor
en POSIX vía `RandomAccess.FlushToDisk`. En Windows se omite el fsync de directorio (no hay handle
equivalente; NTFS ordena la metadata de forma que el rename no precede al contenido ya sincronizado).

### Rationale

La garantía de orden («ningún par observa lo no aceptado») solo vale si «aceptado» significa «durable en
disco», no «en page cache». El fsync síncrono bloquea un hilo del pool durante la operación, pero el
append ya está fuera del turno del actor (no bloquea lecturas del doc). El coste se midió (ver Decisión
1): despreciable en p50/p99. **Límite honesto**: esto cubre el store de referencia (filesystem); Redis,
EFCore e InMemory tienen su propia semántica de durabilidad, ajena a Weft — la durabilidad física del
«aceptado» la aporta el adaptador de cada backend.

---

## Decisión 4 — Broadcast explícito, no vía el evento (refinamiento sobre el plan)

### Problem

El plan proponía suscribir/no-suscribir `UpdateApplied` según el modo. Pero varias conexiones del mismo
hub pueden llamar `ApplyAndPersistAsync` concurrentemente (distintos receive loops), lo que descarta
capturar el delta en un campo compartido (carrera).

### Decision

El hub **deja de usar el evento** para el broadcast del relay. `DocumentSession.ApplyAndCaptureDeltaAsync`
aplica y **devuelve el delta como valor de retorno del turno** (race-free: cada llamada tiene su propio
retorno). `ApplyAndPersistAsync` ordena append/broadcast según el modo. El evento `UpdateApplied` se
conserva para otros consumidores; el relay ya no depende de él.

### Rationale

Más limpio que el condicional de suscripción: el modo solo intercambia dos líneas (append↔broadcast) y se
elimina la doble computación de delta (el actor ya no la computa si nadie está suscrito). Race-free por
construcción. El comportamiento de `BroadcastThenPersist` se preserva fielmente (difunde antes del
append), verificado con un testigo de orden por modo.

---

## Consecuencias

- `WeftServerOptions.Durability` (default `PersistThenBroadcast`); `DocumentSession.ApplyAndCaptureDeltaAsync`;
  `DocumentHub` con broadcast explícito + `DisconnectAll` en fallo; `WeftConnection` cierra 1011;
  `FileSystemDocumentStore` con fsync; modo `--relay` en `Weft.LoadTest`.
- **No** se cablea ack de aplicación al emisor: y-protocols no lo tiene para `Update`, y la garantía es
  sobre lo que observan los pares, no sobre lo que sabe el emisor.
- Reorden de broadcast cross-conexión aceptado (acotado por el pending store de yrs); ruta de escalada
  (pump por-hub) registrada, no construida.
- El contrato de `IDocumentStore` **no cambia**: la contract suite (4 adaptadores) sigue verde sin tocar.
