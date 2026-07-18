---
id: AILOG-2026-07-16-002
title: "CHARTER-13: siembra de réplica cross-engine — IDeterministicSeeding + peer_id de Loro (ABI v3) y gate de auto-determinismo"
status: accepted
created: 2026-07-16
agent: claude-opus-4-8
confidence: high
review_required: true
risk_level: low
eu_ai_act_risk: not_applicable
nist_genai_risks: []
iso_42001_clause: []
observability_scope: none
tags: [follow-ups, cross-engine, deterministic-seeding, loro, peer-id, abi-bump, determinism-gate, ffi-boundary]
related: [AILOG-2026-07-16-001, AILOG-2026-07-15-001]
originating_charter: CHARTER-13-siembra-cross-engine
---

# AILOG: CHARTER-13 — siembra de réplica cross-engine (FU-016)

## Summary

Despacho de FU-016: promueve la siembra de identidad de réplica de capacidad concreta de `YrsEngine`
(CHARTER-09) a capacidad cross-engine, y añade el equivalente de Loro. Segundo de tres Charters que
vacían el backlog antes del publish; baja de 3 open a 2 (FU-010, FU-015). Esfuerzo **S**, no el M que
declaraba el registro: las dos incógnitas que justificaban M (API real de `set_peer_id`, default de
`record_timestamp`) se resolvieron verificándolas en las fuentes pinneadas antes de escribir código.

La investigación descartó dos premisas del follow-up:
- **El gate «Loro↔referencia» no es construible.** `loro-crdt` de npm es un build wasm del mismo core
  Rust; compararlo con nuestro `loro` nativo es tautológico. El gate yrs↔Yjs es significativo porque Yjs
  y yrs son implementaciones genuinamente independientes; Loro no tiene contraparte. El gate realizable
  es de **auto-determinismo** (cross-run/cross-RID con peer_id fijo), no de paridad.
- **Un `CreateDoc(ulong)` en `ICrdtEngine` filtraría la abstracción** por la asimetría de dominios
  (yrs `< 2^53`, Loro todo `u64` salvo `MAX`) — el llamador tendría que ramificar por motor. Se usó una
  capacidad opcional, `IDeterministicSeeding`, que hace del dominio parte del contrato.

## Actions Performed

1. **Shim de Loro — `weft_loro_doc_new_with_peer_id` (ABI v2→v3)**: guard de `u64::MAX` en la frontera
   (espejo del guard de rango de yrs) → `WEFT_ERR_OUT_OF_BOUNDS`; `catch_unwind` como toda entrada.
   `mem_asan.rs`: assert ABI v3 + `seeded_peer_id_reachable_guarded_and_nonleaking` (camino feliz +
   valor reservado + out nulo). Header y su comentario de ABI actualizados.
2. **`IDeterministicSeeding`** (nuevo, `Weft.Core.Abstractions`): `ulong MaxReplicaIdExclusive` +
   `ICrdtDoc CreateDoc(ulong replicaId)`. Añadida a `ICrdtEngine.DeterministicSeeding` (`null` si no se
   soporta). Espeja el patrón `NativeVersioning → INativeVersioning?`. Nombre `replicaId` (neutral;
   `client_id`/`peer_id` son dialectos).
3. **`YrsDeterministicSeeding`** (`Max = 1<<53`, delega en `CreateDoc(ulong)` existente) +
   **`LoroDeterministicSeeding`** (`Max = ulong.MaxValue`) + binding + `LoroDoc.Create(ulong)` +
   `NativeLibraryResolver.ExpectedAbiVersion = 3`. XML doc obsoleto de `YrsEngine.CreateDoc(ulong)`
   («se difiere a un follow-up») corregido.
4. **`TrackingEngine`** de los tests actualizado (`DeterministicSeeding => null`) — implementador de
   `ICrdtEngine` que había que cubrir al añadir el miembro.
5. **Gate de auto-determinismo** (`DeterminismTests`): `Loro_seeded_export_matches_golden` (ascii +
   unicode) contra `golden-loro.json` nuevo + `Loro_seeded_export_is_stable_across_runs` (la premisa del
   golden). `Weft.Determinism.Tests` gana ProjectReference a `Weft.Loro`. Golden bootstrappeado con un
   fact temporal (ya eliminado) que volcó los hashes exactos. Documentado como **testigo de regresión,
   no paridad** en el `_comment` del JSON y el doc-comment del test.
6. **Tests de la capacidad** (`DeterministicSeedingTests`): ambos motores exponen la capacidad; sus
   `MaxReplicaIdExclusive` (fija la asimetría que justificó el diseño); guard de rango en ambos
   (`u64::MAX` en Loro, `1<<53` en yrs → `ArgumentOutOfRangeException`); convergencia con un par no
   sembrado; estabilidad del export de Loro con el mismo/distinto peer_id.
7. **Pago del orden 12→13**: `HeaderBindingParityTests` (de CHARTER-12) validó automáticamente la
   función nueva y el bump de ABI **sin tocarlo** — 6/6 verde. Era exactamente la razón de hacer el 12
   antes.

## Risk

Riesgos del Charter (R1–R6) y su desenlace:

- **R1 (gate «Loro↔referencia» no construible)** — confirmado y evitado: se implementó el de
  auto-determinismo, documentado como testigo de regresión.
- **R2 (golden leído como paridad)** — mitigado: el `_comment` y el doc-comment lo dicen literalmente.
- **R3 (`record_timestamp` cambia de default → auto-determinismo muere en silencio)** — decisión tomada:
  **NO se aserta el config explícitamente en el shim; se confía en el default (`false`, verificado en
  loro 1.13.6).** Razón: (a) el gate lo caza al bumpear —el golden cambiaría—, que es el R16 funcionando;
  (b) asertar el config duplicaría la verdad (una en el default de loro, otra en el shim) y crearía una
  divergencia que mantener. La red de seguridad es el golden + el pin exacto de versión. Si un bump
  futuro pusiera `record_timestamp=true`, el golden divergiría y el gate obligaría a investigar antes de
  regenerarlo — que es el comportamiento correcto.
- **R4 (footgun de siembra en producción)** — mitigado: el XML doc de `IDeterministicSeeding` lo dice; el
  relay/broker NO se tocaron; ningún test cablea la siembra en un escenario multi-cliente.
- **R5 (ABI v3 vs `.so` cacheado v2)** — el resolver rechaza con excepción clara (su trabajo); el flujo
  recompila el nativo primero. Sin incidencia.
- **R6 (promover tensa P-IV)** — evitado con el patrón de capacidad opcional; el test asierta la forma
  (`DeterministicSeeding` no null en ambos), como `LoroNativeVersioningTests` hace con `NativeVersioning`.

Sin R7: no surgió ningún riesgo nuevo durante la ejecución.

## Follow-ups

Ninguno nuevo. FU-016 cerrado, con su premisa corregida (gate de auto-determinismo, no de paridad) en la
nota de cierre del registro.

**Nota de drift (no accionable): falso positivo conocido de `charter drift` con `.csproj`.** El drift
reporta `tests/Weft.Determinism.Tests/Weft.Determinism.Tests.csproj` como «modified but not declared»
pese a estar declarado en `## Files to modify`. Es el bug de parser de StrayMark #354 (el matcher no
casa rutas `.csproj`/`.sln`), ya corroborado en CHARTER-06. El archivo está declarado y su cambio (la
ProjectReference a `Weft.Loro`) es intencional; no hay deriva real.

## Verification

```bash
cargo build --release --features test-hooks --manifest-path native/Cargo.toml
dotnet test Weft.sln -c Release                                        # 151/151 (Determinism 4→8, Versioning 36→45)
cargo test --features test-hooks --manifest-path native/weft-loro-ffi/Cargo.toml   # 7/7 (+ seeded_peer_id)
RUSTFLAGS="-Zsanitizer=address" cargo +nightly test --features test-hooks \
  --target x86_64-unknown-linux-gnu --manifest-path native/weft-loro-ffi/Cargo.toml # 0 fugas sobre la fn nueva
dotnet test tests/Weft.Versioning.Tests -c Release --filter "FullyQualifiedName~HeaderBindingParity"  # 6/6: valida ABI v3
cd tests/determinism-yjs && npm test                                  # golden yrs↔Yjs intacto (CHARTER-09 sin regresión)
```
