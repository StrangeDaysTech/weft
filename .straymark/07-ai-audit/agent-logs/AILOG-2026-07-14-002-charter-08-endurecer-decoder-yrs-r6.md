---
id: AILOG-2026-07-14-002
title: "CHARTER-08: endurecer el decoder de yrs contra amplificación de memoria (R6) — PR upstream + doc + fuzz de regresión"
status: accepted
created: 2026-07-14
agent: claude-opus-4-8
confidence: high
review_required: true
reviewed_by: Jose Villaseñor Montfort
reviewed_at: 2026-07-14
review_outcome: approved
risk_level: medium
eu_ai_act_risk: not_applicable
nist_genai_risks: []
iso_42001_clause: []
observability_scope: none
tags: [ffi-boundary, dos, yrs, upstream-pr, try-reserve, decoder, memory-amplification, fuzz, security-doc]
related: [AIDEC-2026-07-14-001, AILOG-2026-07-10-001]
originating_charter: CHARTER-08-endurecer-decoder-yrs-r6
---

# AILOG: CHARTER-08 — endurecer el decoder de yrs contra amplificación de memoria (R6)

## Summary

Despacho de CHARTER-08 (FU-014): endurecer la ruta **directa del FFI** ante la amplificación de memoria del
decoder de yrs (**R6**), que FU-002 (cerrado) mitigó solo en la capa de relay. La corrección canónica vive
**upstream** — se envió un PR a `y-crdt/y-crdt` extendiendo el patrón `try_reserve` que yrs mismo estableció en
`Update::decode` (`b234ef4e`). Una **revisión de completitud** del crate durante la implementación amplió el
alcance del PR de los **2 sitios** del Charter a los **5 sitios reales** de la clase (ver AIDEC-2026-07-14-001,
decisión 2). Entregables de este repo: nota de seguridad del caveat de ingesta directa (ship-now) + fuzz target
de regresión de la ruta residual `state_vector::decode`. La adopción vía bump es **FU-015** (fuera de scope, no
bloquea el cierre).

## Actions Performed

1. **(a) PR upstream a `y-crdt/y-crdt` — [#639](https://github.com/y-crdt/y-crdt/pull/639)**. Fork
   `StrangeDaysTech/y-crdt`, rama `harden/decode-try-reserve-idset-statevector` (base 0.27.3). Fix de la clase
   completa de allocation-bomb por prefijo de longitud, en **5 sitios**:
   - `state_vector.rs` (`StateVector::decode`), `any.rs` (`Any::Map` + `Any::Array`), `sync/awareness.rs`
     (`AwarenessUpdate::decode`) → `try_reserve(len)?` alimentando la variante existente `Error::NotEnoughMemory`
     (idéntico a `Update::decode`).
   - `id_set.rs` (`IdRanges::decode`, delete sets) → grow-on-push (`SmallVec::try_reserve` da otro tipo de error;
     ver AIDEC decisión 1). 5 tests de regresión upstream (uno por ruta), input adversarial `[255,255,255,122]`.
   - Suite `cargo test -p yrs` verde en el fork: **377 + 34 tests, 0 fallos**. Commit sin trailer de coautoría
     (autoría humana; disclosure honesto de uso de IA + responsabilidad humana en el cuerpo del PR).
2. **(b) Doc del caveat (ship-now)**: nota de seguridad en `GOVERNANCE.md §Seguridad` (subsección "Ingesta directa
   de bytes CRDT no confiables (caveat R6)") + pointer breve en `README.md §Seguridad`: la ruta directa del FFI
   (`weft_doc_load`/`apply_update`/`export_since`) ante bytes no confiables fuera del relay debe protegerse con un
   cap de tamaño + límite de memoria del proceso, como hace el relay (FU-002). Calibrada honesto (glibc = error
   limpio; `apply_update` ya endurecido upstream).
3. **(c) Fuzz de regresión**: nuevo target `export_since` (`native/weft-yrs-ffi/fuzz/fuzz_targets/export_since.rs`
   + `[[bin]]` en `fuzz/Cargo.toml` + step informativo en `ci.yml`) que alimenta bytes arbitrarios como state
   vector a `weft_doc_export_since` → ejercita la ruta residual `state_vector::decode`. Validado localmente: el
   seed `[255,255,255,122]` dispara RSS de **~553 MB** (vs ~34 MB baseline) y completa con error limpio (exit 0,
   sin abort) en glibc — confirma que el target alcanza el residual (mitiga R2 del Charter).
4. **(d) FU-015 registrado** (adopción vía bump R16 al mergear+publicar upstream) — ver §Follow-ups; no ejecutado
   aquí.

## Modified Files

**Este repo** — `native/weft-yrs-ffi/fuzz/fuzz_targets/export_since.rs` (nuevo, T-c),
`native/weft-yrs-ffi/fuzz/Cargo.toml` (`[[bin]] export_since`, T-c), `.github/workflows/ci.yml` (step fuzz
`export_since`, T-c), `GOVERNANCE.md` (nota de seguridad, T-b), `README.md` (pointer, T-b),
`.straymark/follow-ups-backlog.md` (FU-014 → `closed`, FU-015 registrado), `.straymark/charters/08-*.md`
(status → in-progress; reconciliación §Context/§Scope 2→5 sitios), AIDEC-2026-07-14-001 (nuevo).

**Externo (no en este repo)** — fork `StrangeDaysTech/y-crdt`, rama `harden/decode-try-reserve-idset-statevector`,
commit `2ee533e` (5 sitios + 5 tests) → PR `y-crdt/y-crdt#639`.

## Risk

- **R1 (medio) — el merge upstream no llega / tarda / se rechaza**: el fix canónico vive en y-crdt, timeline no
  controlado. Mitigación: el cierre de CHARTER-08 **NO** depende del merge — cierra con nuestros entregables
  (fuzz + doc + PR enviado); la adopción es **FU-015**. Nada regresiona mientras tanto (comportamiento actual =
  error limpio en glibc). Plan B si se rechaza: guard de pre-validación en el shim, como riesgo emergente.
- **R2 (bajo) — asimetría del guard (grow-on-push en SmallVec vs try_reserve en el resto)**: decisión consciente
  para no ampliar el enum público `Error` de yrs (AIDEC decisión 1); el PR ofrece la alternativa simétrica al
  maintainer. Sin impacto de comportamiento.
- **R3 (bajo) — sobre-estimar la severidad en la doc**: en glibc es error limpio, no crash. Mitigación: la nota
  calibra honesto (afecta hosts memory-constrained; `apply_update` ya endurecido; el relay ya capea). No es un CVE
  nuestro.

## Verification

```bash
# (c) El fuzz de regresión compila y alcanza el residual state_vector::decode
cd native/weft-yrs-ffi && cargo +nightly fuzz build -s none export_since
cargo +nightly fuzz run -s none fuzz/corpus/export_since/seed_r6 -- -rss_limit_mb=0
#   → seed [255,255,255,122]: RSS ~553 MB, exit 0 (error de decode limpio, sin abort) en glibc

# Suite .NET completa intacta (P-II ASan sobre la suite determinista)
cd ../.. && dotnet test Weft.sln -c Release

# (a) PR upstream: la suite de yrs pasa con el fix + los 5 tests adversariales EN EL FORK
#   (cd <fork>/y-crdt && cargo test -p yrs)  → 377 + 34 tests, 0 fallos
```

## Follow-ups

Derivado del entregable (d) de CHARTER-08. No bloquea el cierre:

- **Follow-up (adopción, media)**: adoptar el fix de R6 vía **bump de yrs** (protocolo **R16**) cuando el PR
  upstream `y-crdt/y-crdt#639` se mergee y publique en un release de crates.io. Actualizar `yrs = "=0.27.x"` en
  `native/weft-yrs-ffi/Cargo.toml` (+ el fuzz) a la versión con el fix, re-correr el fuzz `export_since` (que
  debe pasar a RSS acotado), y revertir el fork `StrangeDaysTech/y-crdt` (volver a consumir yrs de crates.io).
  **Trigger**: merge + publish upstream. **Destination**: chore. **Cost**: S.

## Additional Notes

- La revisión de completitud (regla operativa: revisar más ancho que el cambio, para no enviar un fix que deja
  gemelos idénticos vivos) encontró 3 sitios más allá de los 2 del Charter (`any.rs` Map+Array, `awareness.rs`).
  Descartó correctamente los `with_capacity` de longitud **local** (Serializer `serde/ser.rs`, `self.len(txn)`,
  `blocks.len()`, constantes). Ver AIDEC-2026-07-14-001 decisión 2.
- El fuzz `apply_update` (existente) ya cubre la ruta delete-set (`id_set`) vía update y el contenido `Any`; el
  nuevo `export_since` cubre la ruta residual `state_vector`. La ruta `awareness` no tiene target de fuzz en Weft
  (es .NET managed en el relay, ya capeada por FU-002); los 5 tests upstream la cubren en el lado de yrs.

## Approval

Trabajo de frontera nativa (`risk_level: medium`, `review_required: true`) con un entregable externo (PR upstream).
Revisión interactiva del operador (`Jose Villaseñor Montfort`, 2026-07-14): revisó el diff final de los 5 sitios +
tests, aprobó la expansión 2→5, la forma del guard, la identidad del commit y el envío del PR. Verificación local
citada. Compañero de AIDEC-2026-07-14-001.
