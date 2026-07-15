---
id: AIDEC-2026-07-15-002
title: "CHARTER-10: forma y semántica de los probes nativos de Loro (demostrativos, sin mutación, JSON a mano)"
status: accepted
created: 2026-07-15
agent: claude-opus-4-8
confidence: high
review_required: true
reviewed_by: Jose Villaseñor Montfort
reviewed_at: 2026-07-15
review_outcome: approved
risk_level: medium
eu_ai_act_risk: not_applicable
nist_genai_risks: []
iso_42001_clause: []
tags: [ffi-boundary, loro, native-versioning, probes, engine-abstraction, determinism]
related: [AILOG-2026-07-15-002]
originating_charter: CHARTER-10-superficie-inativeversioning-de-loro-probes
---

# AIDEC: forma y semántica de los probes nativos de Loro (INativeVersioning)

> Registra las decisiones de diseño de CHARTER-10 (FU-006) sobre los tres probes de `INativeVersioning`
> para Loro, anticipadas en el Charter §Tasks como candidatas a AIDEC.

## Context

`INativeVersioning` es una capacidad **opcional** (probes de paridad) que un motor con versionado nativo
puede exponer. Loro lo tiene (`fork`, `diff`, `ExportMode::shallow_snapshot`); yrs no. G1 (auditoría
CHARTER-02) pidió materializar la superficie para Loro (hoy `NativeVersioning == null`). Las firmas del
interface están fijas (`NativeDiffProbe`/`NativeBranchMergeProbe` → `string` JSON; `ShallowSnapshot` →
`byte[]`); lo abierto era la **semántica** de cada probe y su forma concreta.

---

## Decisión 1 — Probes DEMOSTRATIVOS, no content-addressing

### Problem

¿Qué son estos probes: un contrato de versionado nativo (bytes citables, deterministas) o una
demostración de la capacidad?

### Alternatives Considered

- **A1 — Probes como fuente de content-addressing** (el shallow snapshot alimenta un `VersionId` nativo,
  el diff es un delta citable). Requeriría determinismo byte a byte, pero el shallow snapshot de Loro lleva
  metadata de réplica (peer-ids, orden interno) — NO es determinista entre réplicas convergidas (el mismo
  motivo por el que `weft_loro_doc_export_state` usa `all_updates`, no `Snapshot`). **Rechazada.**
- **A2 (elegida) — Probes DEMOSTRATIVOS**: exhiben que Loro PUEDE versionar nativamente (fork/diff/shallow).
  Su salida es informativa, **no** determinista y **no** alimenta `VersionId` (que sigue con `ExportState`/
  `all_updates`, content-addressed engine-agnóstico). Cierra G1 sin prometer garantías que Loro no da.

### Rationale

FU-006 y el docstring del interface los llaman "probes de paridad" — el objetivo es materializar la
superficie diferida, no construir un segundo sistema de versionado. Prometer content-addressing sobre bytes
no deterministas sería falso. Documentado en el docstring de cada probe, en `LoroNativeVersioning`, en el
header y en el quickstart §US5. Los tests asertan reachability/round-trip/convergencia, **no**
byte-determinismo. Una API de versionado nativo rica (branches con nombre, time-travel) sería un charter
aparte si alguna vez se requiere (out of scope).

### Consequences

- `VersionStore`/`VersionId` intactos; los probes son una superficie lateral opcional.
- El shallow snapshot ES recargable (`LoadDoc`/`import`) — útil como capacidad, aunque no citable.

---

## Decisión 2 — El branch/merge probe NO muta el doc del caller; JSON a mano

### Problem

El probe de fork/merge necesita editar y mergear. ¿Muta el documento del caller? ¿Cómo se serializa el
resultado a JSON si el shim no tiene `serde_json`?

### Alternatives Considered

- **B1 — fork + editar + mergear DE VUELTA al doc original.** Simple, pero **muta el doc del caller** con una
  edición sintética — efecto secuandario sorpresa e inaceptable para un probe. **Rechazada.**
- **B2 (elegida) — fork + editar el fork + `import` en una COPIA aparte (`doc.fork()`), reportar
  convergencia.** El doc del caller queda intacto (verificado por test: su texto no cambia). Demuestra el
  ciclo nativo fork→editar→merge sin efectos secundarios.
- Serialización: **añadir `serde_json` al shim** (dep nueva, contra la minimalidad de la frontera nativa) vs
  **armar el JSON a mano**. Elegido **a mano** con un `json_escape` para el nombre del campo — los probes
  emiten solo campos numéricos/booleanos + el field escapado; sin dep nueva, `DiffBatch` no necesita ser
  `Serialize` (no lo es).

### Rationale

Un probe no debe mutar su entrada. Armar el JSON a mano evita una dependencia nativa por una salida trivial
(3-4 campos). El `json_escape` cubre comillas/backslash/controles del nombre del campo (única entrada de
texto que se incrusta). El diff probe reporta `containers_changed` (nº de containers en el `DiffBatch` de
`doc.diff(Frontiers::default(), state_frontiers)`) + `text_len_utf16` — demostrativo y estable.

### Consequences

- Placement (como CHARTER-09): `LoroNativeVersioning` es `internal`, castea `ICrdtDoc → LoroDoc` (excepción
  `ArgumentException` clara si se pasa un doc no-Loro), y delega en métodos `internal` de `LoroDoc` (el handle
  nativo queda encapsulado, como el resto del binding).
- ABI del shim Loro **v1→v2** (aditivo); `mem_asan.rs` actualiza su assert de ABI + prueba los 3 probes bajo
  ASan (reachability + sin fugas + sin mutación del caller).
- El shim Loro no tenía header (a diferencia de yrs); se **crea** `weft_loro_ffi.h`. El test automatizado de
  paridad header↔binding es **FU-017** (el shim yrs lo tiene; el Loro aún no).

## Approval

**Approved**: 2026-07-15 por `Jose Villaseñor Montfort`, en revisión interactiva. El operador autorizó la
ejecución continua de CHARTER-10 y el alcance demostrativo de los probes (declarado ex-ante en el Charter
§Context/§Scope). Compañero de AILOG-2026-07-15-002.
