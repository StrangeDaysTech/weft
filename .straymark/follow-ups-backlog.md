---
last_scan: 2026-07-10
schema_version: v1
total_open: 5
total_promoted: 0
total_closed_in_session: 0
total_phase_blocked: 0
total_suspected_closed: 0
buckets:
  - ready
  - time-triggered
  - charter-triggered
  - phase-blocked
  - operational
fully_extracted_ailogs:
  - AILOG-2026-07-10-001
  - AILOG-2026-07-10-002
---

# Follow-ups Backlog

> Central registry of `§Follow-ups` and `R<N> (new, not in Charter)` entries across AILOGs.
> Maintained by `straymark followups drift --apply`; counters are CLI-owned.
> Convention: `.straymark/00-governance/FOLLOW-UPS-BACKLOG-PATTERN.md` ·
> Schema: `.straymark/schemas/follow-ups-backlog.schema.v1.json`

<!--
Entry shape (v1 — optional fields marked):

### FU-NNN — <short description>
- **Origin**: AILOG-NNNN-NN-NN-NNN <pointer to source section>
- **Origin-class**: ex-ante-planning | testing | telemetry | staging | real-env-bug   (optional)
- **Status**: open | in-progress | suspected-closed | closed | superseded | promoted
- **Severity**: normal | blocking                                                     (optional)
- **Trigger**: ready | <calendar date> | when <X> | <other>
- **Destination**: chore | mini-charter | charter-replanning | operations | <charter-id> | <TDE id>
- **Cost**: <effort estimate>
- **Labels**: <free tags, comma-separated>                                            (optional)
- **Notes**: <free-form context>
-->

## Bucket: ready

### FU-001 — Riesgos R1–R5 del Charter mitigados según lo declarado. Emergió R6 (new, not in Charter)
- **Origin**: AILOG-2026-07-10-001 §R1 (new, not in Charter)
- **Source-hash**: 356d0132850b
- **Status**: open
- **Trigger**: TBD
- **Destination**: TBD
- **Cost**: TBD
- **Notes**: Auto-appended by `straymark followups drift --apply` 2026-07-10.

### FU-002 — ### Risk: R6 (new, not in Charter) — amplificación de memoria en decode de update no confiable
- **Origin**: AILOG-2026-07-10-001 §R6 (new, not in Charter)
- **Source-hash**: 69e431c0f7d9
- **Status**: open
- **Trigger**: TBD
- **Destination**: TBD
- **Cost**: TBD
- **Notes**: Auto-appended by `straymark followups drift --apply` 2026-07-10.

### FU-003 — ### Risk: R6 (new, not in Charter) — robustez del decoder de yrs ante update no confiable
- **Origin**: AILOG-2026-07-10-001 §R6 (new, not in Charter)
- **Source-hash**: f848fb99fdfb
- **Status**: open
- **Trigger**: TBD
- **Destination**: TBD
- **Cost**: TBD
- **Notes**: Auto-appended by `straymark followups drift --apply` 2026-07-10.

### FU-004 — ### Risk: R6 (new, not in Charter) — índices de yrs eran byte-offsets, no UTF-16
- **Origin**: AILOG-2026-07-10-002 §R6 (new, not in Charter)
- **Source-hash**: 24e92818b6c7
- **Status**: open
- **Trigger**: TBD
- **Destination**: TBD
- **Cost**: TBD
- **Notes**: Auto-appended by `straymark followups drift --apply` 2026-07-10.

### FU-005 — ### Risk: R7 (new, not in Charter) — Snapshot de Loro no es content-addressable determinista
- **Origin**: AILOG-2026-07-10-002 §R7 (new, not in Charter)
- **Source-hash**: 1d1c514561fe
- **Status**: open
- **Trigger**: TBD
- **Destination**: TBD
- **Cost**: TBD
- **Notes**: Auto-appended by `straymark followups drift --apply` 2026-07-10.
## Bucket: time-triggered

## Bucket: charter-triggered

## Bucket: phase-blocked

## Bucket: operational
