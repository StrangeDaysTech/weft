---
id: AIDEC-2026-07-10-001
title: "CHARTER-02: content-addressing determinista de Loro y decisiones de implementación"
status: accepted
created: 2026-07-10
agent: claude-opus-4-8
confidence: high
review_required: true
risk_level: medium
eu_ai_act_risk: not_applicable
nist_genai_risks: []
iso_42001_clause: []
tags: [crdt, loro, determinismo, content-addressing, dual-engine]
related: [AILOG-2026-07-10-002]
---

# AIDEC: content-addressing determinista de Loro (y decisiones de implementación de CHARTER-02)

> Registra las decisiones de implementación nuevas de CHARTER-02 (exigido por el Charter §Tasks).
> La decisión principal —el modo de export de Loro— tuvo un trade-off técnico real descubierto
> empíricamente (R7); las relacionadas se documentan al final.

## Context

CHARTER-02 implementa versionado content-addressed engine-agnóstico: `VersionId = SHA-256(ExportState)`.
La identidad debe ser **determinista y reproducible entre réplicas convergidas** (SC-002, constitución
P-III): dos réplicas con el mismo estado lógico DEBEN producir el mismo `VersionId`. yrs lo cumple con
su update v1 canónico. Loro ofrece varios modos de export, y no todos son byte-deterministas
cross-réplica.

## Problem

¿Qué modo de export de Loro produce un blob byte-determinista entre réplicas convergidas, de modo que
el content-addressing (hash del blob) sea una identidad citable cross-nodo?

## Alternatives Considered

### Alternative 1: `ExportMode::Snapshot`

**Description**: export del estado materializado + historial comprimido.

**Pros**:
- Compacto para historiales largos (estado consolidado).
- Primer candidato "natural" (análogo aparente al export completo).

**Cons**:
- **NO es byte-determinista cross-réplica**: el snapshot incluye metadata dependiente de la réplica
  (peer-ids aleatorios, orden interno del estado materializado). Dos docs con el mismo estado lógico
  producen snapshots distintos → VersionId distinto (viola SC-002).
- Los tests resultaron **flaky** (verde en local/macOS por casualidad, rojo en ubuntu/windows) — R7.

### Alternative 2: `ExportMode::all_updates()`

**Description**: export del oplog completo (todas las operaciones) en orden canónico.

**Pros**:
- **Byte-determinista cross-réplica**: réplicas convergidas tienen el mismo conjunto de ops con los
  mismos op-ids → mismo orden canónico → mismo blob → mismo VersionId.
- El spike 03 ya lo señalaba como "el análogo más cercano al update v1 de yrs para content-addressing".

**Cons**:
- Para historiales muy largos puede ser mayor que un snapshot consolidado (mitigado: el GC del motor
  permanece activo y la compactación es por construcción, FR-012).

### Alternative 3: hashear el estado materializado (el texto), no el export binario

**Description**: `VersionId = SHA-256(texto canónico de los campos)`.

**Pros**:
- Trivialmente determinista respecto al contenido visible.

**Cons**:
- **Pierde fidelidad CRDT**: dos estados CRDT distintos (distinto historial/frontera) con el mismo
  texto darían el mismo hash — incorrecto para una identidad de versión que debe distinguir estados
  CRDT. Rompe el round-trip `Checkout(Publish(doc))` byte-idéntico.

## Decision

**Chosen**: Alternative 2 — `ExportMode::all_updates()`.

**Justification**: es el único modo que garantiza determinismo cross-réplica (verificado con 20
corridas consecutivas sin flakiness + la matriz de CI multiplataforma en verde) sin sacrificar la
fidelidad CRDT del round-trip. Alinea el content-addressing de Loro con el de yrs (ambos hashean un
export canónico del oplog).

## Consequences

### Positive
- El gate `dual-engine` y `Converged_replicas_publish_same_version_id` pasan de forma consistente en
  las tres plataformas.
- Content-addressing de Loro con las mismas garantías que yrs (SC-002).

### Negative / Trade-offs
- `all_updates` no consolida el historial como un snapshot; para historiales largos el blob puede ser
  mayor. Acotado por el GC del motor (compactación por construcción, no tombstones acumulados).

### Risks
- **Canonicidad de `all_updates` bajo merges adversariales**: la suite cubre merge conmutativo y
  convergencia, pero escenarios de merge más adversariales quedan como área de verificación futura
  (follow-up). Mitigación actual: postcondiciones 3 y 5 sobre ambos motores + 20 corridas.

## Implementation

`native/weft-loro-ffi/src/lib.rs` → `weft_loro_doc_export_state` usa `ExportMode::all_updates()`.
El bug del Snapshot (R7) lo destapó el CI multiplataforma, no la verificación local (un solo runner
lo habría dejado pasar) — evidencia del valor del gate dual-engine + matriz de 3 plataformas.

## Decisiones relacionadas (resumen)

- **Índices UTF-16 (R6)**: yrs se crea con `OffsetKind::Utf16` (su default es bytes UTF-8) y Loro usa
  `insert_utf16`/`delete_utf16`, para que el índice `int` de la API sea consistente con `string` de
  .NET y con Yjs. Alternativa (bytes/code-points) descartada por inconsistencia con el ecosistema.
- **Diferir `INativeVersioning` de Loro**: `LoroEngine.NativeVersioning = null` en M0. Es capacidad
  OPCIONAL (core-api.md); ningún gate ni postcondición depende de ella. Se difiere a follow-up
  (auditoría CHARTER-02, G1). Alternativa (implementarla ahora) descartada: no requerida para cerrar M0.
- **Tokenización del diff LCS** por runs de palabra/espacio (no por carácter ni por línea): balancea
  legibilidad y determinismo para texto corrido (research R9).
- **Sharding `aa/bb/hash` en `FileSystemBlobStore`** con escritura atómica temp+rename: evita
  directorios enormes y blobs a medias; el content-addressing hace segura la carrera de escritores.

## References

- research.md R9 (diff), R10 (content-addressing), R15 (dual-engine), R16 (pinning)
- `docs/spikes/spike03/` (Loro export para content-addressing)
- AILOG-2026-07-10-002 §R6/§R7 · `.straymark/audits/CHARTER-02/review.md` (G1, G2)
