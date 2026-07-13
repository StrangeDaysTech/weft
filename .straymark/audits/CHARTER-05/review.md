---
audit_role: calibrator-reconciler
calibrator: claude-opus-4-8
charter_id: CHARTER-05-weft-server-relay-end-to-end-cierra-m2-us3
git_range: "origin/main..HEAD"
prompt_used: ../audit-prompt.md
calibrated_at: 2026-07-13
auditors_reconciled:
  - report-gpt-5-5.md
  - report-glm-5-2.md
  - report-qwen3.7-max.md
findings_consolidated: 7
findings_by_status:
  agreed: 2
  disputed: 0
  unique_gpt-5-5: 1
  unique_glm-5-2: 1
  unique_qwen3.7-max: 2
  rejected: 1
---

# Consolidated audit review — CHARTER-05

**Reviewer:** claude-opus-4-8
**Date:** 2026-07-13
**Confidence:** High

## 1. Executive summary

Tres auditores de familias distintas (gpt-5-5, glm-5-2, qwen3.7-max) auditaron CHARTER-05 de forma
**independiente** (sin contaminación cruzada: ningún reporte referencia a otro). Los tres confirman que el
relay end-to-end está implementado (T047–T049, T051, T052), build limpio (0 warnings) y 107 tests verdes, con
la compat del wire (R1) retirada por el check headless `yjs`/`y-websocket`. Ninguno halló Critical.

El hallazgo más material es **convergente (gpt+glm)**: el enforcement de `ReadOnly` cierra la conexión con 1008
al recibir el `SyncStep2` del **handshake estándar** de y-websocket, no solo ante un update en vivo — haciendo
el modo `ReadOnly` **inusable con clientes Yjs estándar**, y el test `ReadOnly_...` pasa **por la razón
equivocada** (lo cierra el Step2 de handshake, no el `EditAsync`). Calibrado a **High**: rompe un criterio de
aceptación declarado (SC-010) y da falsa confianza. Un segundo bug de código (único de gpt-5-5) **confirmado**:
`AwarenessProtocol.TrackClients` lanza `KeyNotFoundException` para un `clientID` nuevo con `clock == 0` (común
en clientes Yjs), tumbando esa conexión. Un tercer hallazgo convergente (gpt+glm) es una **discrepancia doc↔código**:
el §Context del Charter afirma que `AppendUpdate` ejecuta *dentro del turno del actor*, pero la implementación
lo hace *fuera* (tras el apply) — el diseño es razonable (idempotencia CRDT + snapshot al desalojo), pero la
afirmación del Charter sobre-declara.

Veredicto: **cierre condicionado a remediación** de F1 (ReadOnly) y F2 (awareness crash) — ambos bugs de
código en superficie declarada — más la corrección documental de F3. Los hallazgos Low (F4–F6) son robustez
barata; F7 es una observación de estilo cuya remediación sugerida (DropWrite) sería **incorrecta**.

## 2. Scope definition

CHARTER-05 = corte 2 de M2/US3, cierra el hito M2. Tareas en scope: **T047** (`WeftConnection` +
`DocumentHub` + `AwarenessProtocol`), **T048** (`WeftServerExtensions` + `WeftServerOptions`), **T049**
(`WeftServer`/`IWeftServer`), **T051** (`RelayTests`), **T052** (samples + wire-check). Criterio de cierre:
retirar R1 (compat del wire), cerrar FU-002 (parte b), completar el journey US3. Contrato congelado:
`specs/001-weft-crdt-versioning/contracts/server-api.md`. Fuera de scope: adaptadores EFCore/Redis (CHARTER-06),
Loro nativo (FU-006), relay multi-nodo.

## 3. Per-auditor evaluation

### 3.1 gpt-5-5 (model: gpt-5-5)

| # | Finding | Reported severity | Verdict | Justification |
|---|---------|-------------------|---------|---------------|
| H1 | ReadOnly cierra en el SyncStep2 del handshake | High | **VALID** | Confirmado: `WeftConnection.cs:137` `case Sync:` cierra 1008 para no-ReadWrite; el handshake Yjs envía Step2. El test pasa por la razón equivocada. Calibrado High. |
| M1 | Update aplicado/difundido antes de persistir | Medium | **PARTIALLY VALID** | Real: `AppendUpdate` va fuera del turno; contradice el §Context. Mitigado por idempotencia CRDT + snapshot al desalojo. Remediación = doc + robustez. |
| M2 | Awareness crash con clock==0 para cliente nuevo | Medium | **VALID** | Confirmado: `AwarenessProtocol.cs:38` `tracked[clientId]` lanza `KeyNotFoundException` (clientId ausente, `0>0` falso). Único de gpt-5-5. |

**Summary:** El auditor más profundo (59 citas). Único en hallar el crash de awareness (F2) y en calibrar el ReadOnly como High. Trazó los flujos completos y verificó contra el contrato spec-local. 0 falsos positivos.

### 3.2 glm-5-2 (model: glm-5-2)

| # | Finding | Reported severity | Verdict | Justification |
|---|---------|-------------------|---------|---------------|
| M-1 | ReadOnly cierra en SyncStep2 del handshake | Medium | **VALID** (= gpt H1) | Mismo bug; glm lo calibra Medium, calibrador lo sube a High (rompe SC-010 + falsa confianza). Convergencia gpt+glm. |
| M-2 | AppendUpdate fuera del turno del actor | Medium | **PARTIALLY VALID** (= gpt M1) | Mismo hallazgo; correcto que el §Context sobre-declara. Convergencia gpt+glm. |
| L-1 | Race en shutdown: `DisposeAsync` libera `_hubGate` sin esperar `LeaveAsync` en vuelo | Low | **VALID** | Confirmado: una `LeaveAsync` en vuelo lanzaría `ObjectDisposedException`. Único de glm. |
| L-2 | Charter declara Mvc.Testing, impl usa TestHost | false_positive | **FALSE POSITIVE** (auto-marcado) | glm lo auto-clasifica como FP documentado. Buena calibración. |

**Summary:** Excelente calibración — auto-marcó su propio hallazgo débil (L-2) como FP y verificó la paridad de publish contra `VersionStore`. Convergió con gpt en los dos hallazgos principales. No halló el crash de awareness.

### 3.3 qwen3.7-max (model: qwen3.7-max)

| # | Finding | Reported severity | Verdict | Justification |
|---|---------|-------------------|---------|---------------|
| 1 | `WeftServer` sin guardas de disposed en métodos públicos | Low | **VALID** | Confirmado: `PublishAsync`/`GetConnectionCountAsync`/`DisconnectAllAsync` no chequean `_disposed`. Robustez barata. |
| 2 | `AddWeftServer` no valida `IDocumentStore` (sí `IWeftAuthorizer`) | Low | **VALID** | Confirmado: inconsistencia de failure mode; una `IDocumentStore` ausente falla tarde en el factory. |
| 3 | `BoundedChannelFullMode.Wait` con semántica `TryWrite` | Low | **PARTIALLY VALID** | La observación es justa, pero ya hay comentario aclaratorio; la remediación sugerida (**DropWrite**) sería **incorrecta** — con `TryWrite` DropWrite *aceptaría y descartaría* el mensaje silenciosamente, rompiendo el cierre por backpressure y perdiendo updates. Mantener `Wait`. |

**Summary:** Verificación de contratos cross-boundary excepcional (todas las firmas contra las definiciones reales). Pero **no halló los dos bugs de comportamiento** (ReadOnly, awareness) ni el apply-before-persist, y su veredicto de cierre ("Yes, ready") fue erróneo. Una sugerencia (DropWrite) sutilmente incorrecta.

## 4. Remediation plan — VALID and PARTIALLY VALID findings

### P1 — Correctness (bloquean el cierre)

**F1 — ReadOnly cierra en el SyncStep2 del handshake (High).**
- **Files:** `src/Weft.Server/WeftConnection.cs:137`
- **Problem:** Toda conexión no-ReadWrite se cierra 1008 al enviar cualquier mensaje SYNC no-Step1, incluido el `SyncStep2` que el protocolo y-websocket exige como respuesta al `SyncStep1` del servidor. `ReadOnly` queda inusable con clientes estándar.
- **Remediation:** Separar `Step2` (handshake) de `Update` (edición en vivo) en `DispatchAsync`. Para `ReadOnly`: el `Step2` se **ignora** (no se aplica, no cierra — read-only no contribuye estado); un `Update` (edición en vivo) → 1008. Para `ReadWrite`: ambos se aplican. Actualizar el test `ReadOnly_...` para (a) afirmar que la conexión **sobrevive** el handshake y recibe updates, (b) cerrar 1008 solo ante un `Update`.
- **Complexity:** Low. **Detected by:** gpt-5-5 (High), glm-5-2 (Medium).

**F2 — Awareness crash con `clock == 0` para cliente nuevo (Medium).**
- **Files:** `src/Weft.Server/Protocol/AwarenessProtocol.cs:38`
- **Problem:** `tracked[clientId] = clock > tracked.GetValueOrDefault(clientId) ? clock : tracked[clientId];` lanza `KeyNotFoundException` cuando `clientId` es nuevo y `clock == 0` (el else indexa una clave ausente). Escapa el `catch (MalformedMessageException)` y tumba la conexión. Clock 0 es común en el primer awareness de un cliente Yjs.
- **Remediation:** Reemplazar por un patrón que inserte el cliente sin importar el clock, p. ej. `if (!tracked.TryGetValue(clientId, out uint prev) || clock > prev) tracked[clientId] = clock;`. Añadir test de awareness con `clock == 0` para un cliente nuevo.
- **Complexity:** Low. **Detected by:** gpt-5-5.

### P2 — Consistency

**F3 — `AppendUpdate` fuera del turno del actor contradice el §Context del Charter (Medium, doc↔código).**
- **Files:** `src/Weft.Server/DocumentHub.cs:62-65` (impl), `.straymark/charters/05-*.md` §Context (afirmación)
- **Problem:** El §Context afirma que `AppendUpdate`/`SaveSnapshot` ejecutan *dentro del turno del actor*. La impl aplica en el turno y persiste **después**, fuera del turno (broadcast-antes-de-persistir). Es un diseño razonable (idempotencia CRDT + snapshot al desalojo + self-heal en reconexión), análogo a `PublishAsync` (AIDEC §1, cuyo `PutAsync` también va fuera del turno). Pero la afirmación del Charter sobre-declara.
- **Remediation:** **Documental** — corregir el §Context del Charter (el `AppendUpdate` va tras el apply, fuera del turno) y añadir la decisión al AIDEC (broadcast-then-persist consciente; durabilidad por snapshot-al-desalojo + self-heal). Opcional (robustez): manejar/loggear un fallo de `AppendUpdateAsync`. No se mueve la persistencia al turno (el turno es síncrono; `AppendUpdate` es async).
- **Complexity:** Low (doc). **Detected by:** gpt-5-5, glm-5-2.

**F6 — `AddWeftServer` no valida `IDocumentStore` (Low).**
- **Files:** `src/Weft.Server/WeftServerExtensions.cs:22-38`
- **Remediation:** Añadir un probe de `IDocumentStore` (mismo patrón `IServiceProviderIsService` que `IWeftAuthorizer`) en `MapWeft`, para un failure mode consistente y temprano.
- **Complexity:** Low. **Detected by:** qwen3.7-max.

### P3 — Robustness

**F4 — Race de shutdown `DisposeAsync`↔`LeaveAsync` (Low).**
- **Files:** `src/Weft.Server/WeftServer.cs` (`DisposeAsync` / `LeaveAsync`)
- **Remediation:** Capturar `ObjectDisposedException` en el `_hubGate.WaitAsync()` de `LeaveAsync` (o marcar `_disposed` y saltar). **Detected by:** glm-5-2.

**F5 — Guardas de disposed en métodos públicos de `WeftServer` (Low).**
- **Files:** `src/Weft.Server/WeftServer.cs` (`PublishAsync`, `GetConnectionCountAsync`, `DisconnectAllAsync`)
- **Remediation:** `ObjectDisposedException.ThrowIf(_disposed, this)` al inicio. **Detected by:** qwen3.7-max.

### P4 — Documentation / no-action

**F7 — `BoundedChannelFullMode.Wait` con `TryWrite` (Low).** Ya existe comentario aclaratorio en `WeftConnection.cs:30`. La remediación sugerida por qwen (DropWrite) sería **incorrecta** (rompería el cierre por backpressure). Acción: ninguna, o reforzar el comentario. **Detected by:** qwen3.7-max, glm-5-2 (nota).

### Missed by all auditors

Ninguno significativo. La verificación cruzada fue sólida: gpt+glm cubrieron el comportamiento (ReadOnly, persistencia), gpt el crash de awareness, qwen los contratos cross-boundary y la robustez de disposal. El calibrador no halló bugs adicionales de corrección/seguridad no reportados.

## 5. Discarded findings — misattributions and false positives

| Finding | Type | Charter / area | Auditor |
|---------|------|----------------|---------|
| Charter declara Mvc.Testing pero impl usa TestHost (L-2) | FALSE POSITIVE (sustitución documentada; TestHost es suficiente y más ligero) | CHARTER-05 §Files | glm-5-2 (auto-marcado) |

## 6. Auditor ratings

| Auditor | Scope precision (25%) | Technical depth (25%) | Bug detection (30%) | False positive rate (20%) | **Overall** |
|---------|:-:|:-:|:-:|:-:|:-:|
| gpt-5-5 | 9 | 9 | 10 | 10 | **9.4** |
| glm-5-2 | 9 | 8 | 8 | 9 | **8.6** |
| qwen3.7-max | 8 | 9 | 5 | 8 | **7.0** |

### Justifications

**gpt-5-5 — 9.4/10**: El auditor más fuerte de la ronda. Único en el crash de awareness (F2) y en calibrar el ReadOnly como High con evidencia del test que pasa por la razón equivocada. Máxima densidad de citas (59), 0 falsos positivos, trazado de flujos completo.

**glm-5-2 — 8.6/10**: Excelente calibración (auto-descartó su propio hallazgo débil como FP; verificó la paridad de publish contra `VersionStore`). Convergió con gpt en los dos hallazgos principales y aportó el race de shutdown (único). No halló el crash de awareness.

**qwen3.7-max — 7.0/10**: Verificación de contratos cross-boundary excepcional (la más rigurosa en firmas/tipos) y 3 hallazgos Low válidos de robustez. Pero **no detectó los dos bugs de comportamiento** (ReadOnly High, awareness Medium) ni el apply-before-persist, y su veredicto de cierre fue erróneo; una sugerencia (DropWrite) sutilmente incorrecta. La profundidad no se tradujo en detección de bugs de lógica.

## 7. Conclusion

**Estado: parcial — cierre condicionado a remediación.** CHARTER-05 entrega un relay y-sync funcional y bien
estructurado, con P-V/P-III/P-IV respetados para el camino `ReadWrite` (el común), compat del wire retirada y
107 tests verdes. Pero dos bugs de código en superficie declarada deben remediarse antes de cerrar M2:
**F1** (ReadOnly inusable con clientes estándar, High) y **F2** (crash de awareness con clock 0, Medium),
más la corrección documental **F3** (el §Context sobre-declara la persistencia dentro del turno). Los Low
(F4/F5/F6) son robustez barata recomendable en el mismo pase; F7 no requiere acción (y su fix sugerido sería
un bug).

0 Critical, 1 High (F1), 2 Medium (F2, F3), 3 Low (F4/F5/F6), 1 FP descartado. **Próximo paso recomendado:**
remediar F1+F2 (con tests que fallen sin el fix), corregir F3 en el Charter/AIDEC, aplicar F4/F5/F6, y luego
`straymark charter close CHARTER-05` con el bloque `external_audit` en la telemetría, todo en el mismo PR #18.
