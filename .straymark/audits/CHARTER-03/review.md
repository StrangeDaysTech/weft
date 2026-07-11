---
audit_role: calibrator-reconciler
calibrator: claude-opus-4-8
charter_id: CHARTER-03-concurrencia-broker-actor-canal-y-ciclo-de-vida-a
git_range: "origin/main..HEAD"
prompt_used: ../audit-prompt.md
calibrated_at: 2026-07-11
auditors_reconciled:
  - report-glm-5.2.md
  - report-gpt-5-5.md
  - report-qwen3-7-max.md
findings_consolidated: 11
findings_by_status:
  agreed: 3
  disputed: 0
  unique_glm-5.2: 3
  unique_gpt-5-5: 2
  unique_qwen3-7-max: 3
  rejected: 0
---

# Consolidated audit review — CHARTER-03

**Reviewer:** claude-opus-4-8
**Date:** 2026-07-11
**Confidence:** High

## 1. Executive summary

Tres auditores de familias distintas (glm-5.2, gpt-5-5, qwen3-7-max) auditaron CHARTER-03 de forma
**independiente** (verificado: ninguno referencia ni consolida los reportes ajenos — la convergencia es
señal genuina, no copia). Los tres coinciden: la implementación entrega el scope completo de M1
(T036–T042), satisface el contrato `core-api.md` §Concurrencia y el principio constitucional P-V
(acceso serializado por documento, imposible acceder al `ICrdtDoc` nativo desde la API pública), con 52
tests verdes y el load test validando SC-006 (~433k desalojos concurrentes, 0 inconsistencias, memoria
acotada). **Ningún auditor encontró hallucinations; cero falsos positivos tras verificación contra
código.**

Se consolidan **11 findings únicos**, todos `real_debt` salvo uno clasificado `implementation_gap`
(F, dispose vs carga en vuelo). **Ninguno es Critical o High tras calibración**: gpt-5-5 marcó dos como
High (hook swallow, dispose-vs-load); ambos se recalibran a **Medium** — el primero es una decisión de
diseño intencional documentada en el AILOG (P-I > datos) que solo carece de observabilidad; el segundo
es un no-determinismo real pero sin fuga permanente y con ventana estrecha. Los 11 findings son deuda de
calidad no bloqueante para el cierre; **5 conviene remediarlos ahora** (quick wins + los relevantes para
M2) y el resto va al follow-ups backlog.

El bug más material es **F (gpt-5-5)**: `DisposeAsync` no espera las cargas en vuelo de `OpenAsync`, así
que un documento creado durante el apagado se libera de forma asíncrona (fire-and-forget) *después* de
que `DisposeAsync` retorna — tensiona la garantía de liberación determinista del contrato. Lo encontró
solo gpt-5-5 (los otros dos no exploraron esa carrera). Los findings de mayor valor para M2 son
**G** (un handler de `UpdateApplied` que lanza faultea el actor — futuro punto único de fallo del relay)
e **I** (race `_state`/`_fault` que puede persistir un doc faulted).

## 2. Scope definition

| Tarea | Entregable | En scope |
|-------|-----------|----------|
| T036 | `DocumentBrokerOptions` | ✅ |
| T037 | `DocumentActor` (internal, canal single-reader) | ✅ |
| T038 | `DocumentBroker` (registro/idle+LRU/dispose) | ✅ |
| T039 | `DocumentSession` (espejo async, ExecuteAsync, UpdateApplied) | ✅ |
| T040 | Tests de concurrencia | ✅ |
| T041 | Load test harness (SC-006) | ✅ |
| T042 | Job CI nightly `load-test` | ✅ |

**Criterio de cierre** (§Charter Closure): drift limpio o documentado ✅; auditoría externa multi-modelo
(este ciclo) ✅; findings `real_debt` remediados o triados a follow-ups antes de cerrar ⏳.
**Fuera de scope**: hardening del decoder ante input no confiable (FU-002, M2); relay/persistencia/authz
(US3, M2); versionado nativo de Loro (FU-006). Ningún auditor trató estos como defectos de CHARTER-03
(correcto).

## 3. Per-auditor evaluation

### 3.1 glm-5.2 (model: glm-5.2)

| # | Finding | Sev. reportada | Verdict | Justificación |
|---|---------|----------------|---------|---------------|
| 1 | `_persistOnEnd` dead code | Low | **VALID** (=A) | Confirmado: `:38` decl, `:159` lectura, nunca `= false`. |
| 2 | `MaxActiveDocuments` sin validación | Low | **VALID** (=C) | Sin guard; `0`/negativo rompería el pooling. Fail-fast razonable. |
| 3 | `OnEvicting` swallow sin observabilidad | Low | **VALID** (=B) | `catch {}` en `FinalizeAsync:166`. Converge con gpt F1. |
| 4 | LRU test asserta count, no identidad | Low | **VALID** (=D) | `DocumentBrokerTests:103` asserta `==2`, no cuál se desalojó. |
| 5 | Comentario "hard bound" inexacto | Low | **VALID** (=E) | `Program.cs:134` sigue afirmando la cota vieja; el criterio real es working-set. |

**Summary:** El único auditor que ejecutó build + tests + load test (evidencia empírica fuerte) y con la
mejor trazabilidad tarea-por-tarea. Sus 5 findings son todos de calidad y correctos, pero no exploró los
bugs de ciclo de vida más profundos (F dispose-load, G/I fault) que sí hallaron gpt/qwen. 0 FP.

### 3.2 gpt-5-5 (model: gpt-5-5)

| # | Finding | Sev. reportada | Verdict | Justificación |
|---|---------|----------------|---------|---------------|
| F1 | `OnEvicting` swallow → pérdida silenciosa | High | **PARTIALLY VALID → Medium** (=B) | Real, pero es diseño intencional (AILOG, P-I > datos); falta observabilidad, no corrupción. |
| F2 | `DisposeAsync` no espera cargas en vuelo | High (impl_gap) | **PARTIALLY VALID → Medium** (=F) | Confirmado: `DisposeAsync` espera `_evicting`/`_actors`, no `_loading`; `:158` libera fire-and-forget. No-determinismo real; sin fuga permanente ni trigger actual. |
| F3 | `UpdateApplied` handler faultea el actor | Medium | **VALID** (=G) | `NotifySessions:206` sin try/catch dentro del turno; converge con qwen F2. |
| F4 | Cancelación compartida en single-flight | Medium | **VALID** (=H) | El `ct` del primer caller contamina la carga compartida; otros waiters ven la task cancelada. |

**Summary:** La mayor profundidad técnica y densidad de evidencia (95 citaciones). Único que halló F (el
gap de determinismo más material) y H. Recalibro sus dos High a Medium por configuración/diseño, pero la
detección es excelente. No ejecutó tests (solo estático, por disciplina read-only) — legítimo pero pierde
la evidencia empírica que glm sí aportó. 0 FP.

### 3.3 qwen3-7-max (model: qwen3-7-max)

| # | Finding | Sev. reportada | Verdict | Justificación |
|---|---------|----------------|---------|---------------|
| F1 | Race `_state`/`_fault` persiste doc faulted | Medium | **VALID** (=I) | `FinalizeAsync:157` chequea `_state`, no `_fault` (autoritativo); race con el catch. Calibración de severidad honesta. |
| F2 | `UpdateApplied` handler faultea el actor | Medium | **VALID / DUPLICATE de gpt F3** (=G) | Mismo defecto, misma calibración. |
| F3 | `_persistOnEnd` dead code | Low | **VALID / DUPLICATE de glm #1** (=A) | Idéntico. |
| F4 | Falta test de `UpdateApplied` | Low | **VALID** (=J) | El evento (superficie pública) no tiene cobertura directa. |
| F5 | LINQ O(n²) en sweep (`toEvict.Contains`) | Low | **VALID** (=K) | `DocumentBroker:232`; negligible en default (1024), material a escala. |

**Summary:** La mejor calibración de severidad (marca explícitamente ventana de race y ausencia de
trigger actual). Único que halló I (la race `_state`/`_fault`, sutil) y K. Ejecutó build + tests. 0 FP.

## 4. Remediation plan — VALID and PARTIALLY VALID findings

### P1 — Integrity / lifecycle
- **F — `DisposeAsync` no espera cargas en vuelo (`_loading`)**
  - Files: `src/Weft.Core/Concurrency/DocumentBroker.cs:284` (dispose), `:158` (fire-and-forget)
  - Problema: un `OpenAsync` cuya carga resuelve durante el apagado crea un actor que se libera async
    tras `DisposeAsync` retornar → liberación no determinista (contrato §core-api.md:160).
  - Remediación: rastrear/esperar las tareas de `_loading` en `DisposeAsync` (snapshot + `WhenAll`), o
    esperar sincrónicamente el `BeginEvictionAsync` del actor creado durante shutdown antes de retornar.
  - Complejidad: Media. **Detected by:** gpt-5-5.
- **I — race `_state`/`_fault` puede persistir un doc faulted (viola R4)**
  - Files: `src/Weft.Core/Concurrency/DocumentActor.cs:157`
  - Problema: `FinalizeAsync` decide persistir según `_state != Faulted`; si el desalojo marca `Idle`
    tras el catch marcar `Faulted`, persiste estado potencialmente corrupto vía `OnEvicting`.
  - Remediación: chequear `_fault is null` (autoritativo) en `FinalizeAsync`, no (o además de) `_state`.
  - Complejidad: Baja. **Detected by:** qwen3-7-max.

### P3 — Robustness
- **G — un handler de `UpdateApplied` que lanza faultea el actor** (relevante para M2)
  - Files: `src/Weft.Core/Concurrency/DocumentActor.cs:181-206`
  - Problema: `NotifySessions` invoca los handlers dentro del `try` del turno; una excepción del
    suscriptor faultea el documento para TODAS las sesiones, aunque la op CRDT ya completó.
  - Remediación: envolver `RaiseUpdateApplied` en try/catch dentro de `NotifySessions` (un bug del
    consumidor no debe matar al publicador). Añadir test de regresión con handler que lanza.
  - Complejidad: Baja. **Detected by:** gpt-5-5, qwen3-7-max.
- **B — fallo de `OnEvicting` tragado sin observabilidad**
  - Files: `src/Weft.Core/Concurrency/DocumentActor.cs:163-168`
  - Problema: `catch {}` traga cualquier fallo del hook; el consumidor no puede detectar persistencia
    fallida (diseño intencional P-I > datos, pero ciego).
  - Remediación: `Debug.WriteLine` o callback opcional `OnEvictionFailed`; el path de liberación no
    cambia. Complejidad: Baja. **Detected by:** glm-5.2, gpt-5-5.
- **H — cancelación compartida en la carga single-flight**
  - Files: `src/Weft.Core/Concurrency/DocumentBroker.cs:85-93`
  - Problema: el `ct` del primer caller se pasa al loader compartido; su cancelación falla a otros
    waiters no cancelados.
  - Remediación: usar un token interno del broker para la carga compartida; aplicar el `ct` de cada
    caller solo al esperar; si la carga falla por cancelación, retirarla y dejar reintentar a los demás.
  - Complejidad: Media. **Detected by:** gpt-5-5.

### P2 — Consistency / quality (quick wins)
- **A — `_persistOnEnd` dead code** (`DocumentActor.cs:38,159`): eliminar el campo y simplificar la
  condición. Baja. glm-5.2, qwen3-7-max.
- **E — comentario "hard bound" obsoleto** (`Program.cs:134`): corregir para reflejar el criterio real
  (memoria acotada por working-set + tamaño/pool acotados; la cota de activos es suave). Baja. glm-5.2.
- **K — LINQ O(n²) en sweep** (`DocumentBroker.cs:232`): sustituir `toEvict.Contains` por un `HashSet`.
  Baja. qwen3-7-max.
- **C — `MaxActiveDocuments` sin validación** (`DocumentBrokerOptions.cs:20`): guard
  `ThrowIfNegativeOrZero` en el ctor del broker. Baja. glm-5.2.

### P4 — Test coverage
- **D — test LRU asserta count, no identidad** (`DocumentBrokerTests.cs:103`): verificar que 'a' (LRU)
  fue el desalojado (reabrir 'a' con loader null → vacío; 'b'/'c' conservan contenido). Baja. glm-5.2.
- **J — falta test de `UpdateApplied`** (`DocumentBrokerTests.cs`): dos sesiones sobre el mismo doc, una
  suscribe, la otra inserta, asertar delta no vacío y aplicable. Baja. qwen3-7-max.

## 5. Discarded findings — misattributions and false positives

| Finding | Type | Charter / area | Auditor |
|---------|------|----------------|---------|
| _(ninguno)_ | — | — | — |

Cero falsos positivos y cero misattributions: los 3 auditores fueron precisos. Las únicas
recalibraciones fueron de severidad (gpt B/F: High → Medium), no de validez.

**Missed by all auditors:** ninguno material. La verificación del calibrador contra el código no
encontró defectos de correctitud no reportados; los tres cubrieron bien el ciclo de vida. Los tres
validaron positivamente el fix R7 (`_evicting`-await) como bien diseñado.

## 6. Auditor ratings

| Auditor | Scope precision (25%) | Technical depth (25%) | Bug detection (30%) | False positive rate (20%) | **Overall** |
|---------|:-:|:-:|:-:|:-:|:-:|
| glm-5.2 | 9 | 7 | 7 | 10 | **8.1** |
| gpt-5-5 | 9 | 10 | 9 | 10 | **9.4** |
| qwen3-7-max | 9 | 9 | 8 | 10 | **8.7** |

### Justifications

**glm-5.2 — 8.1/10**: Trazabilidad ejemplar y única evidencia empírica (ejecutó build+tests+load test).
Sus 5 findings son correctos y 0 FP, pero se quedó en la capa de calidad (dead code, comentarios,
validación) y no alcanzó los bugs de ciclo de vida (F/G/H/I) que exigían modelar carreras.

**gpt-5-5 — 9.4/10**: La auditoría más profunda: 95 citaciones y los dos hallazgos más materiales (F
dispose-vs-load, único que lo vio; H cancelación compartida). Perdió una fracción por inflar dos
severidades a High (recalibradas a Medium) y por no ejecutar la suite. 0 FP.

**qwen3-7-max — 8.7/10**: La mejor disciplina de calibración de severidad (declara ventanas de race y
ausencia de trigger). Único en hallar I (race `_state`/`_fault`) y K. Profundidad alta, 0 FP.

## 7. Conclusion

**Estado del Charter: limpio para cierre (sin bloqueadores).** Cero findings Critical/High tras
calibración; cero falsos positivos. La implementación satisface P-V, el contrato v1 y SC-006, con los
tres riesgos emergentes (R6/R7/R8) ya corregidos y validados. Los 11 findings son deuda de calidad
`real_debt`.

**Recomendación antes de cerrar M1:** remediar en este mismo PR los quick wins y los defectos de
lifecycle de bajo coste y alto valor — **A, D, E, K, I, G, B** (todos complejidad Baja; A/D/E/K son
higiene, I/G/B endurecen los paths de fallo y persistencia que M2 estresará). Diferir a follow-ups los de
complejidad Media sin trigger actual — **F** (dispose-vs-load) y **H** (cancelación compartida) — con
trigger explícito hacia M2. Luego `straymark charter close CHARTER-03` pegando el bloque `external_audit`.

**Siguiente paso recomendado:** decidir el conjunto a remediar ahora, aplicarlo con regresión, re-verde
de CI, y cerrar el Charter con la telemetría de auditoría.
