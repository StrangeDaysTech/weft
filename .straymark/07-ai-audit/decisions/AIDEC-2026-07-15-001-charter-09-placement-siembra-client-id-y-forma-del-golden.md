---
id: AIDEC-2026-07-15-001
title: "CHARTER-09: placement de la siembra de client-id (YrsEngine concreto vs ICrdtEngine) y forma del golden de paridad"
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
tags: [ffi-boundary, determinism, yrs, yjs, cross-impl-parity, abi, engine-abstraction, client-id]
related: [AILOG-2026-07-15-001, weft-speckit-estado]
originating_charter: CHARTER-09-client-id-determinista-en-el-ffi-de-yrs-gate-de
---

# AIDEC: placement de la siembra de client-id y forma del golden de paridad

> Registra las dos decisiones sustantivas de CHARTER-09 (FU-012), anticipadas en el Charter §Tasks
> como candidatas a AIDEC, más el resultado del riesgo central **R1** (paridad byte-idéntica yrs↔Yjs).

## Context

FU-012 promueve el gate de determinismo cross-implementación (`determinism-yjs`, T058) de informativo a
aserción. La paridad byte-idéntica con Yjs exige **client-ids deterministas**, que el binding de yrs no
exponía. El operador decidió **alcance yrs-only** (el gate es yrs↔Yjs; Loro es otro formato) y **aserción
per-PR barata** (test .NET en el job `test` existente). Dos formas quedaban abiertas: dónde vive la
capacidad de siembra, y cómo se estructura el golden.

---

## Decisión 1 — Placement de la siembra de client-id

### Problem

`weft_doc_new_with_client_id` (FFI) necesita una superficie .NET. ¿Va en la interfaz compartida
`ICrdtEngine` (P-IV: abstracción de motor viva) o como capacidad concreta de `YrsEngine`?

### Alternatives Considered

- **A1 — `CreateDoc(ulong clientId)` en `ICrdtEngine`, `LoroEngine` lanza `NotSupported`.** Mantiene la
  interfaz simétrica, pero introduce una capacidad que un motor **no honra en runtime** — tensiona P-IV
  (la abstracción "promete" algo que un impl rechaza) y contradice el patrón ya establecido en el repo
  (`INativeVersioning?` como capacidad **opcional** vía propiedad, no un método que lanza). **Rechazada.**
- **A2 — capacidad opcional tipo `INativeVersioning`** (interfaz `ISeedableClientId` expuesta por propiedad).
  P-IV-correcta y descubrible, pero sobre-ingeniería para un solo método cuyo único consumidor es el test
  de paridad (yrs-específico por naturaleza: el gate es yrs↔Yjs). **Rechazada por ahora.**
- **A3 (elegida) — `CreateDoc(ulong clientId)` como método CONCRETO de `YrsEngine`.** No toca `ICrdtEngine`
  (que conserva `CreateDoc()` sin parámetro). El test de paridad usa `YrsEngine` directo. Honesto: la
  capacidad es yrs-específica, no se declara en la abstracción algo que Loro no da hoy.

### Rationale

El gate de paridad es **intrínsecamente yrs↔Yjs** (misma familia de formato v1). Poner la siembra en la
interfaz compartida obligaría a Loro a implementarla (o a lanzar), sin beneficio para el gate y tensando
P-IV. La capacidad concreta en `YrsEngine` es el mínimo honesto; la promoción a capacidad **cross-engine**
(Loro vía `set_peer_id`) se difiere a **FU-016** — a materializar si/cuando se quiera un gate de determinismo
para Loro. El ABI bump (**v1→v2**, `weft_doc_new_with_client_id` aditivo) y el guard `client_id < 2^53`
(encoding de 53 bits de yrs 0.26+; `ClientID::new` tiene `debug_assert` de ello y corrompería en release)
son mecánicos y quedan documentados en el AILOG.

### Consequences

- `ICrdtEngine` intacto; `Weft.Versioning`/broker/relay no cambian. Superficie nueva mínima.
- **FU-016** registrado (promoción cross-engine Loro). No bloquea nada.

---

## Decisión 2 — Forma del golden y dónde asierta (bloqueante) la paridad

### Problem

¿Cómo se estructura el hash golden y qué componente **asierta** la paridad de forma bloqueante, respetando
el presupuesto de minutos de CI?

### Alternatives Considered

- **B1 — job Node en `release.yml` asertivo (quitar `continue-on-error`), pasar `WEFT_GOLDEN_HASH`.** Fiel al
  diseño original del harness, pero la paridad **no se verifica per-PR** (release es `workflow_dispatch`) y
  añade un job Node bloqueante. **Rechazada.**
- **B2 (elegida) — golden de Yjs comprometido (`golden.json`) + aserción per-PR en `Weft.Determinism.Tests`.**
  `apply.mjs` produce el hash de Yjs de cada corpus → se compromete en `golden.json` (`ascii`/`unicode`). El
  **test .NET** (`Yrs_export_matches_yjs_golden`, Theory ascii+unicode) aplica el corpus con yrs y asierta
  `sha256(export) == golden` — corre en el job `test` existente (**bloqueante de facto, per-PR, costo ~0**).
  El **job Node** de `release.yml` queda `continue-on-error` pero **self-checkea** su hash de Yjs contra el
  mismo `golden.json` → caza **drift de Yjs** (bump con impacto de encoding).

### Rationale

Un único golden comprometido sirve a **ambos** lados: el test .NET verifica yrs↔golden (la paridad que
importa) donde es barato (job existente); el job Node verifica Yjs↔golden (vigencia del golden) donde el
entorno Node ya existe (release). Separa las dos preguntas —"¿yrs iguala a Yjs?" (bloqueante) y "¿el golden
sigue siendo el hash real de Yjs?" (informativo)— sin duplicar costo de CI.

### Consequences

- Regenerar `golden.json` es un paso **deliberado** documentado (README) para cambios de corpus, distinguible
  de un drift accidental (que el self-check del job Node destapa).
- El corpus unicode (BMP acentuado + CJK + astrales) ejercita los índices **UTF-16** (R6) en el mismo gate.

---

## Resultado de R1 (riesgo central del Charter)

**La paridad byte-idéntica yrs↔Yjs SE CUMPLE.** El test `Yrs_export_matches_yjs_golden` pasa **2/2** (ascii +
unicode): yrs produce exactamente los hashes de Yjs (`27a8...3243` ascii, `afd1...9e02` unicode). Por tanto el
gate se fija como **bloqueante** (no fue necesario el plan B de "dejarlo informativo"). Bonus: la variante
unicode (con surrogate pairs astrales) confirma también la **paridad de índices UTF-16** (mitiga R5 del Charter
con evidencia, no argumentación). El determinismo de Weft queda demostrado **por formato**, no por versión de yrs.

## Approval

**Approved**: 2026-07-15 por `Jose Villaseñor Montfort`, en revisión interactiva. El operador decidió ex-ante el
alcance yrs-only (Loro diferido) y la aserción per-PR (AskUserQuestion al declarar CHARTER-09), y autorizó la
ejecución continua. Compañero de AILOG-2026-07-15-001.
