---
charter_id: CHARTER-14-durabilidad-relay
status: closed
closed_at: 2026-07-19
effort_estimate: L
trigger: "El operador decide implementar FU-010 como último Charter del vaciado del backlog antes del publish (T060), con tres decisiones tomadas: fsync EN scope, default PersistThenBroadcast, y cobertura de carga del relay (hoy inexistente). Al cerrar, el backlog queda en 1 open (FU-015, bloqueado upstream)."
originating_spec: specs/001-weft-crdt-versioning/spec.md
work_verb: implement
design_provenance: new
---

# Charter: Durabilidad del relay — persist-before-broadcast + fsync

> **Status (mirrored from frontmatter — source of truth is above):** closed. Effort: L.
>
> **Origin:** FU-010 (AIDEC-2026-07-13-001 §5, auditoría de CHARTER-05 F3). El relay hace hoy
> **broadcast-then-persist**; este Charter lo invierte, endurece la durabilidad del store con fsync, y
> añade la cobertura de carga del relay que hoy no existe. Tercero y último del vaciado del backlog.

## Context

El relay difunde un update a los pares **antes** de persistirlo. El mecanismo real (verificado en HEAD)
es sutil: `DocumentHub.ApplyAndPersistAsync` hace `await Session.ApplyUpdateAsync` y luego
`await _store.AppendUpdateAsync`, pero el **broadcast no está ahí** — lo dispara el evento
`DocumentSession.UpdateApplied` **dentro** del turno del actor durante el apply, es decir *antes* de que
`AppendUpdateAsync` llegue a ejecutarse. Un fallo del append tras el broadcast deja a los pares con un
update que el store no tiene; en v1 single-node la auto-sanación CRDT (re-sync en reconexión) lo
recupera, y así lo documentó AIDEC-2026-07-13-001 §5 como decisión consciente.

**La premisa del follow-up sobre el coste es falsa, y eso simplifica el trabajo.** FU-010 temía que
persist-before-broadcast metiera I/O en el camino caliente del actor. No es así: `AppendUpdateAsync`
**ya se espera** en el receive loop (`WeftConnection.DispatchAsync`, awaited secuencialmente), así que
cada conexión ya paga la latencia del append antes de leer su siguiente frame. Invertir el orden **no
añade I/O a ningún hot path**: solo mueve el broadcast de dentro del turno a después del append.

**Refinamiento de diseño sobre el plan.** El plan proponía suscribir/no-suscribir el evento según el
modo. Es más limpio que el hub **deje de usar el evento** y haga el broadcast **explícito**, capturando
el delta como valor de retorno del turno (`DocumentSession.ApplyAndCaptureDeltaAsync`). Así el modo solo
intercambia dos líneas (append↔broadcast) y se elimina la doble computación de delta. Es race-free
porque cada llamada obtiene su propio valor de retorno — necesario, porque **varias conexiones del mismo
hub pueden llamar `ApplyAndPersistAsync` concurrentemente** (distintos receive loops), lo que descarta
capturar el delta en un campo compartido.

**fsync (decisión del operador: EN scope).** `FileSystemDocumentStore.AtomicWriteAsync` usa
`FlushAsync` → page cache del SO, no disco; `File.Move` tampoco es durable sin fsync del directorio. Sin
fsync, persist-before-broadcast protege solo contra crash de **proceso**, no de máquina — y el trigger
de FU-010 dice «SLA de no-pérdida». Se añade `Flush(flushToDisk: true)` + fsync del directorio (POSIX).

**Cobertura de carga (decisión del operador: EN scope).** `tests/Weft.LoadTest` conduce
`DocumentBroker` directamente: **cero** referencias a `DocumentHub`/`WeftServer`/`IDocumentStore`
(verificado). Los 45k ops/20s son ciegos a este cambio; no hay hoy ninguna cobertura de carga del path
de persistencia del relay. Se añade un modo que ejercita el relay real vía `TestServer`.

## Scope

**In scope:**

1. **`WeftServerOptions.DurabilityMode`** (enum `PersistThenBroadcast` | `BroadcastThenPersist`) +
   propiedad `Durability`. **Default `PersistThenBroadcast`** (el semantic seguro; el repo aún no es
   público, cambiar el default ahora es gratis y luego sería breaking).
2. **`DocumentSession.ApplyAndCaptureDeltaAsync`**: aplica el update en el turno del actor y **devuelve
   el delta** (vía la ruta de `ExecuteAsync`, race-free). El evento `UpdateApplied` se conserva para
   otros consumidores; el relay deja de depender de él.
3. **`DocumentHub`**: deja de suscribir `UpdateApplied`; `ApplyAndPersistAsync` captura el delta,
   ordena append/broadcast según el modo, y difunde explícito (eco al emisor conservado, `exclude:
   null`). En `PersistThenBroadcast`, **un fallo de append → `DisconnectAll()` + relanza** (los pares
   quedarían callados y desactualizados para siempre si no; reconectan y el servidor autoritativo
   reenvía el delta).
4. **`WeftConnection`**: un fallo del store en `ApplyAndPersistAsync` cierra la conexión con **1011**
   (InternalError) en vez de escapar de `RunAsync` (hoy solo captura OCE/WebSocketException).
5. **`FileSystemDocumentStore`**: `Flush(flushToDisk: true)` + fsync del directorio tras `File.Move`.
6. **Cobertura de carga del relay**: modo nuevo en `tests/Weft.LoadTest` (o sibling) que conduce N
   clientes WebSocket vía `TestServer` contra `FileSystemDocumentStore`, reportando latencia de
   broadcast en ambos modos — la evidencia que respalda el default.
7. **AIDEC** que supersede a `AIDEC-2026-07-13-001` §5.

**Out of scope:**

- **Ack de aplicación al emisor.** y-protocols no lo tiene para `Update`; el emisor nunca sabe si su
  update se persistió, con orden o sin él. La propiedad que se compra es exactamente: *ningún par
  observa estado que el servidor no haya aceptado de forma durable*. No se inventa un ack propio.
- **Orden estricto de broadcast cross-conexión** (una «durability pump» por-hub). El reordenamiento
  cross-conexión es aceptable: los appends ya son desordenados hoy, yrs bufferea updates causalmente
  incompletos en su pending store, y el reload reproduce en orden de append. Se documenta la ruta de
  escalada sin construirla.
- **FU-015** — bloqueado por upstream.

## Files to modify

| File | Change |
|---|---|
| `src/Weft.Server/WeftServerOptions.cs` | `+ DurabilityMode Durability` (default `PersistThenBroadcast`) + el enum |
| `src/Weft.Core/Concurrency/DocumentSession.cs` | `+ ApplyAndCaptureDeltaAsync` (aplica + devuelve el delta) |
| `src/Weft.Server/DocumentHub.cs` | Broadcast explícito según modo; fallo de append → `DisconnectAll` + relanza; deja de suscribir el evento |
| `src/Weft.Server/WeftServer.cs` | Pasa `options.Durability` al `new DocumentHub(...)` (:102) |
| `src/Weft.Server/WeftConnection.cs` | Fallo del store → cierre 1011 (no escapar de `RunAsync`) |
| `src/Weft.Server/Persistence/FileSystemDocumentStore.cs` | `Flush(flushToDisk: true)` + fsync del directorio |
| `tests/Weft.Server.Tests/RelayTests.cs` | Tests: fallo de append no se observa; cierre 1011; reconexión resincroniza; modo legacy; testigo de orden |
| `tests/Weft.LoadTest/RelayLoad.cs` | New — módulo de carga del relay (N clientes WS vía TestServer, p50/p99 por modo) |
| `tests/Weft.LoadTest/Program.cs` | Cablea el modo `--relay` al inicio |
| `tests/Weft.LoadTest/Weft.LoadTest.csproj` | ProjectReference a `Weft.Server` + `Microsoft.AspNetCore.TestHost` |
| `.straymark/follow-ups-backlog.md` | Cierre de FU-010 |
| `.straymark/07-ai-audit/decisions/AIDEC-2026-07-16-001-durabilidad-relay-persist-before-broadcast.md` | New — supersede de AIDEC-2026-07-13-001 §5 |
| `.straymark/07-ai-audit/agent-logs/AILOG-2026-07-16-003-charter-14-durabilidad-relay.md` | New, `risk_level: medium` (cambia comportamiento del relay) |

## Verification

### Local checks

```bash
cargo build --release --features test-hooks --manifest-path native/Cargo.toml
dotnet build Weft.sln -c Release

# El contrato de IDocumentStore NO cambia: la contract suite (4 adaptadores) sigue verde sin tocar
dotnet test tests/Weft.Server.Tests -c Release
dotnet test Weft.sln -c Release

# La carga del relay en ambos modos (evidencia del default)
dotnet run --project tests/Weft.LoadTest -c Release -- --relay

# Convergencia real end-to-end (crítica: el reorden no debe romper clientes Yjs)
cd samples/tiptap-client && npm run check

straymark validate --include-charters
```

### Production smoke (after deploy)

No aplica: Weft es una librería. El fsync se ejercita en los tests de `FileSystemDocumentStore`; la
durabilidad ante crash de máquina real no es verificable en un shell (requiere corte de energía físico),
y NO debe clasificarse como `real_debt` si no se ejecuta aquí.

## Risks

- **R1 — El fsync no basta para el SLA si el store del consumidor no es durable**: probabilidad media,
  severidad media. `FileSystemDocumentStore` gana fsync, pero Redis/EFCore/InMemory tienen su propia
  semántica de durabilidad, ajena a Weft.
  Mitigación: se documenta que la garantía de orden es *«ningún par observa lo no aceptado por el
  store»*; la durabilidad física del aceptado la aporta el adaptador. El fsync cubre el store de
  referencia (filesystem); los demás son responsabilidad de su backend.
- **R2 — Divergencia permanente si el fallo de append no cierra las conexiones**: probabilidad media,
  severidad **alta**. Es el único modo de equivocarse: un append fallido deja el update en el doc vivo
  pero nunca difundido; la próxima edición difunde solo su delta, nunca el que faltó → pares callados
  para siempre.
  Mitigación: `DisconnectAll` + cierre 1011 es **obligatorio** en `PersistThenBroadcast`, con un test
  dedicado (`FaultyDocumentStore` que lanza en la N-ésima llamada → asertar que ningún par recibió el
  delta y que las conexiones cerraron). Reconexión → SyncStep1 → el servidor reenvía. **Testeable de
  forma determinista inyectando el fallo del store, sin matar procesos.**
- **R3 — Regresión de latencia de broadcast bajo un store lento**: probabilidad media, severidad media.
  Persist-before-broadcast retrasa el broadcast en la latencia del append (con fsync, ~1-10ms).
  Mitigación: el knob (`BroadcastThenPersist`) como válvula de escape documentada + la carga del relay
  como evidencia. Sin la medición, el default sería una afirmación no medida.
- **R4 — Reorden de broadcast cross-conexión sorprende a un cliente no-Yjs**: probabilidad baja,
  severidad media.
  Mitigación: acotado por el pending store de yrs (updates causalmente incompletos se bufferean); el
  smoke headless de `y-websocket` lo valida end-to-end; se documenta en el AIDEC y la ruta de escalada
  (pump por-hub) queda registrada, no construida.
- **R5 — Un blip transitorio del store desconecta todas las conexiones de un doc**: probabilidad media,
  severidad baja.
  Mitigación: retry/backoff es responsabilidad del adaptador del store, no de Weft; los clientes
  reconectan solos. Se documenta.
- **R6 — La doble ruta (dos modos) duplica el espacio de estados del relay**: probabilidad baja,
  severidad baja.
  Mitigación: ambos modos ejercitados por la misma clase de tests; el modo legacy existe como válvula
  de escape, no como camino divergente (comparten todo salvo el orden de dos líneas). Candidato a borrar
  `BroadcastThenPersist` en 1.0 si nadie lo usa.
- **R7 — Añadir fsync síncrono (`Flush(flushToDisk:true)` no tiene variante async en .NET) bloquea un
  hilo del pool**: probabilidad media, severidad baja.
  Mitigación: el append ya está fuera del turno del actor (no bloquea lecturas del doc); el coste es un
  hilo del pool por append durante el fsync, que la carga del relay debe medir. Si resulta caro, evaluar
  `RandomAccess.FlushToDisk` o mover a un hilo dedicado — decisión informada por la medición.

## Tasks

1. Sync main (con CHARTER-13), branch `charter/15-durabilidad-relay` (número de Charter 14).
2. `DurabilityMode` + `WeftServerOptions.Durability`.
3. `DocumentSession.ApplyAndCaptureDeltaAsync`.
4. `DocumentHub`: broadcast explícito por modo + fallo de append → `DisconnectAll` + relanza; wiring en
   `WeftServer`.
5. `WeftConnection`: fallo del store → 1011.
6. `FileSystemDocumentStore`: fsync de archivo + directorio.
7. Cobertura de carga del relay (modo `--relay`).
8. Tests de relay (fallo inyectado, orden, reconexión, modo legacy).
9. AIDEC (supersede §5) + AILOG (`risk_level: medium`) + cerrar FU-010 + `recount`.
10. Verificación local limpia (incl. convergencia headless + carga en ambos modos) + `charter drift`.
11. Commit + push + PR.

## Charter Closure

1. **Atomic update (format v4)** si el drift reporta deriva no capturada.
2. **Post-merge drift check** `--range origin/main..HEAD`.
3. **Status** `in-progress` → `closed` + `closed_at`.
4. Al cerrar, `straymark followups status` debe bajar a **1 open** (FU-015).
5. **No borrar** este archivo.
