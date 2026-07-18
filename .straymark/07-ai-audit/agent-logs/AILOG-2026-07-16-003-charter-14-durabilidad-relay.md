---
id: AILOG-2026-07-16-003
title: "CHARTER-14: durabilidad del relay — persist-before-broadcast + fsync + cobertura de carga del relay"
status: accepted
created: 2026-07-16
agent: claude-opus-4-8
confidence: high
review_required: true
risk_level: medium
eu_ai_act_risk: not_applicable
nist_genai_risks: []
iso_42001_clause: []
observability_scope: none
tags: [relay, durability, persist-before-broadcast, fsync, load-test, follow-ups, m3]
related: [AIDEC-2026-07-16-001, AILOG-2026-07-16-002]
originating_charter: CHARTER-14-durabilidad-relay
---

# AILOG: CHARTER-14 — durabilidad del relay (FU-010)

## Summary

Despacho de FU-010, último Charter del vaciado del backlog antes del publish (T060). Invierte el orden
del relay a **persist-before-broadcast** (default), añade **fsync** al store de referencia y la
**cobertura de carga del relay** que no existía. Con esto el backlog baja de 2 open a **1** (FU-015,
bloqueado upstream) — el vaciado está completo salvo lo que depende de terceros.

Decisiones de diseño en AIDEC-2026-07-16-001 (supersede de AIDEC-2026-07-13-001 §5). Tres las fijó el
operador ex-ante (fsync en scope, default persist-first, cobertura de carga); el AILOG registra la
ejecución y los hallazgos.

## Actions Performed

1. **`WeftServerOptions.Durability`** (enum `DurabilityMode`, default `PersistThenBroadcast`).
2. **`DocumentSession.ApplyAndCaptureDeltaAsync`**: aplica en el turno del actor y **devuelve el delta**
   (race-free vía valor de retorno, frente a conexiones concurrentes del mismo hub). Refinamiento sobre
   el plan: el hub deja de usar el evento `UpdateApplied` para el broadcast (ver AIDEC Decisión 4).
3. **`DocumentHub`**: broadcast explícito ordenado por modo; en `PersistThenBroadcast`, fallo de append
   → `DisconnectAll()` + relanza (el update quedó en el doc vivo sin difundir → cerrar el documento
   fuerza el re-sync autoritativo).
4. **`WeftConnection.ApplyOrCloseAsync`**: fallo del store → cierre **1011** (antes escapaba de
   `RunAsync`, que solo capturaba OCE/WebSocketException). Cancelación y socket cortado se dejan propagar.
5. **`FileSystemDocumentStore`**: `Flush(flushToDisk: true)` + fsync del directorio (POSIX, vía
   `RandomAccess.FlushToDisk`; omitido en Windows).
6. **`WeftServer`**: pasa `_options.Durability` al `new DocumentHub(...)`.
7. **Cobertura de carga del relay** (`Weft.LoadTest`, modo `--relay`): editor→observador vía TestServer
   real contra `FileSystemDocumentStore` con fsync, en ambos modos, con p50/p99. Nuevas refs:
   `Weft.Server` + `Microsoft.AspNetCore.TestHost`.
8. **Tests de relay** (`RelayTests`, +4): fallo de append no observado + cierre 1011; reconexión
   resincroniza; orden en ambos modos (testigo con store armado). `ControllableAppendStore` con «armado»
   —los appends del handshake pasan, el test arma el fallo/bloqueo justo antes de la edición— porque
   contar appends por número es frágil (cada conexión hace un append en el SyncStep2).
9. **AIDEC** (supersede §5) + este AILOG + cierre de FU-010 + `recount`.

## Risk

Riesgos del Charter (R1–R7) y su desenlace:

- **R1 (fsync no basta si el store del consumidor no es durable)** — mitigado y documentado: la garantía
  es «ningún par observa lo no aceptado por el store»; la durabilidad física del aceptado la aporta el
  adaptador. El fsync cubre el store de referencia (filesystem).
- **R2 (divergencia permanente si el fallo de append no cierra las conexiones)** — mitigado, es
  obligatorio: `DisconnectAll` + 1011, con test dedicado de fallo inyectado determinista.
- **R3 (regresión de latencia)** — **medido, y no se materializó**: p50/p99 idénticos entre modos
  (2.1ms), el coste del orden seguro es ~0 en percentiles típicos. Solo el `max` sube (18.6 vs 2.5ms)
  por picos de fsync. La carga del relay es la evidencia que respalda el default.
- **R4 (reorden cross-conexión rompe un cliente no-Yjs)** — mitigado: acotado por el pending store de
  yrs; convergencia real de 2 clientes `y-websocket` verificada end-to-end con el default nuevo.
- **R5 (blip del store desconecta todo un doc)** — aceptado y documentado: retry/backoff es del
  adaptador; los clientes reconectan.
- **R6 (doble ruta duplica el espacio de estados)** — mitigado: ambos modos por la misma clase de tests;
  comparten todo salvo el orden de dos líneas.
- **R7 (fsync síncrono bloquea un hilo del pool)** — medido: despreciable en p50/p99 (el append ya está
  fuera del turno del actor). Si escalara, `RandomAccess.FlushToDisk` ya se usa para el directorio; el
  archivo usa `Flush(flushToDisk:true)`. No hizo falta optimizar.

Sin R8: no surgió ningún riesgo nuevo no anticipado.

## Follow-ups

Ninguno nuevo. FU-010 cerrado. El vaciado del backlog (CHARTER-12/13/14) deja **1 open**: FU-015
(adopción del fix de R6 vía bump de yrs), bloqueado por el merge upstream de y-crdt#639 — no accionable
por nosotros.

Nota (no accionable): un falso positivo conocido de `charter drift` con `.csproj`/`.sln` (#354) puede
reaparecer al declarar `Weft.LoadTest.csproj`; el archivo está declarado y su cambio (refs a Weft.Server
+ TestHost) es intencional.

## Verification

```bash
cargo build --release --features test-hooks --manifest-path native/Cargo.toml
dotnet test Weft.sln -c Release                                        # 155/155 (Server 70→74)
dotnet test tests/Weft.Server.Tests -c Release                         # incl. contract suite intacta (IDocumentStore no cambia)
dotnet run --project tests/Weft.LoadTest -c Release -- --relay --edits 200  # PASS; p50/p99 idénticos entre modos
# Convergencia real con el default nuevo (reorden no rompe Yjs):
dotnet run --project samples/Weft.Sample.Server -c Release &
cd samples/tiptap-client && npm run check                              # "Hello from A. And B too."
straymark validate --include-charters
```
