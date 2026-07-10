---
charter_id: CHARTER-02-versioning-dual-engine
status: in-progress
effort_estimate: L
trigger: "CHARTER-01 cerrado (binding seguro con gates P-I/P-II verdes en main). tasks.md fija T022–T035 (US1 versionado + US5 dual-engine) como el segundo y último corte de M0: cierra el hito activando los gates de determinismo (P-III) y dual-engine (P-IV)."
originating_spec: specs/001-weft-crdt-versioning/spec.md
work_verb: implement
design_provenance: new
---

# Charter: Versionado content-addressed y dual-engine

> **Status (mirrored from frontmatter — source of truth is above):** in-progress. Effort: L.
>
> **Origin:** Derivado de `specs/001-weft-crdt-versioning/spec.md`. Segundo y último corte de M0 (T022–T035): capa de dominio de versionado engine-agnóstica (US1) + adaptador Loro y suite dual-engine (US5). Cierra M0.

## Context

Sobre la fundación de CHARTER-01 (shim yrs + binding `Weft.Core`), este corte añade el **versionado content-addressed** (`VersionId` = SHA-256 del export determinista; publish/checkout/diff/branch/merge sobre `IBlobStore`) en una capa de dominio que depende SOLO de las abstracciones de `Weft.Core` (P-IV, nunca de tipos de yrs/Loro). Para probar que esa abstracción está viva, US5 añade un segundo shim (`weft-loro-ffi` sobre `loro = "=1.13.6"`) y el adaptador `Weft.Loro`, y ejecuta la MISMA suite de versionado sobre ambos motores.

Cierra M0 activando los dos gates que faltaban: **determinismo** del encoding (P-III) y **dual-engine** (P-IV). El diseño está ✅ CERRADO en el brief y validado en los spikes 01/03 (la capa de dominio ~58 LOC corrió idéntica sobre yrs y Loro); trabajo de **implementación** contra `contracts/versioning-api.md` y `core-api.md`.

## Scope

**In scope (T022–T035):**

1. **US1 — Versionado (T022–T031)**: `VersionId` (SHA-256, hex 64, Parse/TryParse/AsSpan);
   `IBlobStore` + `InMemoryBlobStore` (put idempotente, thread-safe) + `FileSystemBlobStore`
   (sharding `aa/bb/hash`, escritura atómica temp+rename); `TextDiff` (LCS por palabras,
   determinista); `VersionStore` (Publish/Checkout/Diff/Branch/Merge/MergeAsync, verifica
   integridad → `BlobIntegrityException`); suite parametrizada `VersioningSuiteBase` +
   `YrsVersioningTests` (las **7 postcondiciones** de versioning-api.md); `TextDiffTests`;
   `DeterminismTests` (gate P-III, client-ids fijos); sample runnable de US1; wiring CI
   (`determinism` bloqueante + versioning en la matriz).
2. **US5 — Dual-engine (T032–T035)**: crate `weft-loro-ffi` (ABI núcleo `weft_loro_*` + probes
   `native_diff`/`native_branch`/`shallow_snapshot` + header + tests/mem_asan); `Weft.Loro`
   (`LoroEngine`/`LoroDoc`/`LoroNativeVersioning` per core-api.md); `LoroVersioningTests`
   (hereda `VersioningSuiteBase` de T027) + **promover el gate `dual-engine` a bloqueante**
   (SC-008); extender la matriz `asan` a `weft-loro-ffi`.

**Out of scope:**

- Broker/concurrencia (US2, M1), relay servidor y protocolo y-sync (US3, M2), empaquetado NuGet
  multi-RID y release OSS (US4, M3) — hitos posteriores.
- Hardening del decoder ante input no confiable (R6 de CHARTER-01, amplificación de memoria) —
  sigue diferido a M2 (capa de red); aquí solo se verifica que el shim Loro no introduce UB nuevo.

## Files to modify

<!-- Greenfield salvo lo marcado. Loro: código de referencia validado en spikes 02/03
     (`~/StrangeDaysTech/crdt-core-spikes/spike03/sdt_crdt_ffi_loro`) — se reescribe limpio
     con nombres weft_loro_* (la constitución prohíbe copiar código de spikes). -->

| File | Change |
|---|---|
| `src/Weft.Versioning/VersionId.cs` | New — SHA-256 content-addressing (T022) |
| `src/Weft.Versioning/Blobs/IBlobStore.cs`, `InMemoryBlobStore.cs`, `FileSystemBlobStore.cs` | New — almacén content-addressed + sharding (T023–T024) |
| `src/Weft.Versioning/TextDiff.cs` | New — diff LCS por palabras (T025) |
| `src/Weft.Versioning/VersionStore.cs` | New — publish/checkout/diff/branch/merge + integridad (T026) |
| `tests/Weft.Versioning.Tests/VersioningSuiteBase.cs`, `YrsVersioningTests.cs` | New — suite parametrizada, 7 postcondiciones (T027) |
| `tests/Weft.Versioning.Tests/TextDiffTests.cs` | New — determinismo del diff (T028) |
| `tests/Weft.Determinism.Tests/` (+ `.csproj`) | New — gate de determinismo P-III (T029) |
| `samples/Weft.Sample.Versioning/` (+ `.csproj`) | New — user journey US1 runnable (T030) |
| `native/weft-loro-ffi/` (Cargo.toml, `src/lib.rs`, `include/weft_loro_ffi.h`, `tests/mem_asan.rs`, `fuzz/`) | New — shim C-ABI sobre loro 1.13.6 + probes (T032) |
| `native/Cargo.toml` | Change — añadir member `weft-loro-ffi` |
| `src/Weft.Loro/` (`LoroEngine.cs`, `LoroDoc.cs`, `LoroNativeVersioning.cs`, `Interop/*`, `.csproj`) | New — adaptador dual-path (T033) |
| `tests/Weft.Versioning.Tests/LoroVersioningTests.cs` | New — suite dual-engine sobre Loro (T034) |
| `.github/workflows/ci.yml` | Change — `determinism` bloqueante + `dual-engine` bloqueante + `asan` matrix a loro (T031, T034, T035) |
| `Weft.sln` | Change — añadir `Weft.Loro`, `Weft.Determinism.Tests`, sample |
| `.straymark/07-ai-audit/agent-logs/AILOG-*.md` | New, `risk_level: high` (segundo shim FFI + dominio) |

## Verification

### Local checks

> **Lección de CHARTER-01 (telemetría)**: ejecutar TODO esto localmente —incluido el fuzz con
> `cargo-fuzz` (ya instalado, 0.13.2)— ANTES de pushear, para no iterar a ciegas en CI.

```bash
# Shims Rust (yrs + loro): build + tests + memoria
cargo build --manifest-path native/Cargo.toml
cargo test  --manifest-path native/Cargo.toml
RUSTFLAGS="-Zsanitizer=address" cargo +nightly test --manifest-path native/Cargo.toml \
  --features test-hooks --target x86_64-unknown-linux-gnu     # gate P-II sobre AMBOS shims
cargo +nightly fuzz run -s none doc_load     -- -max_total_time=60 -rss_limit_mb=0 -max_len=8192
cargo +nightly fuzz run -s none apply_update -- -max_total_time=60 -rss_limit_mb=0 -max_len=8192

# .NET: versionado (corre sobre YrsEngine Y LoroEngine), determinismo, diff, binding
dotnet test tests/Weft.Versioning.Tests/     # gate dual-engine (P-IV, SC-008)
dotnet test tests/Weft.Determinism.Tests/    # gate determinismo (P-III)
dotnet test tests/Weft.Core.Tests/
dotnet run  --project samples/Weft.Sample.Versioning/   # user journey US1 legible
```

### Production smoke (after deploy)

No aplica — librería sin despliegue. Los auditores externos deben saltar esta sección.

## Risks

- **R1 — No-determinismo del encoding rompe la identidad (P-III)**: severidad alta. Mitigación:
  `DeterminismTests` como gate bloqueante (corpus con client-ids fijos → export/hash idénticos
  entre réplicas y corridas); `VersionId = SHA-256(ExportState)`. Réplicas convergidas → mismo
  VersionId (postcondición 3, SC-002). Si falla: revalidar el export antes de cualquier release.
- **R2 — Abstracción de motor "zombi" (P-IV)**: severidad alta. Una abstracción con una sola
  implementación ejercitada se considera rota. Mitigación: `VersioningSuiteBase` abstracta corre
  las 7 postcondiciones idénticas sobre yrs Y Loro; gate `dual-engine` promovido a bloqueante
  (T034, SC-008). `Weft.Versioning` no referencia tipos de yrs/Loro (validado por compilación).
- **R3 — Blob corrupto socava el content-addressing**: severidad media. Mitigación: `VersionStore`
  verifica `VersionId.FromBlob(blob) == id` en checkout → `BlobIntegrityException`.
- **R4 — Amplificación de memoria en el decode del shim Loro (hereda R6 de CHARTER-01)**:
  severidad media. El decoder de Loro podría amplificar igual que yrs ante input no confiable.
  Mitigación: mismo tratamiento (fuzz informativo, `catch_unwind`); el hardening real se difiere a
  M2 (capa de red). Si Loro degrada distinto (p. ej. `assert!` vs Err), documentar como
  `R<N+1> (new, not in Charter)` en el AILOG.
- **R5 — Merge no conmutativo**: severidad media. Mitigación: postcondición 5 (merge de ramas
  concurrentes → ambas ediciones presentes, resultado idéntico sin importar el orden) en la suite
  dual-engine, sobre ambos motores.

## Tasks

1. Branch `feat/charter-02-versioning-dual-engine` (ya creado desde main). Flip `declared` → `in-progress`.
2. Re-evaluar **Constitution Check** contra el scope (esta vez P-III y P-IV se **cierran** plenamente).
3. `/speckit-implement` acotado a **T022–T035**; marcar `[X]` + `*CHARTER-02: <sha>*` por tarea.
4. **AILOG** (`risk_level: high`, `review_required: true`); **AIDEC** para decisiones de
   implementación nuevas (tokenización del LCS, layout de sharding, mapeo de capacidades de Loro a
   `INativeVersioning`). Las ✅ CERRADO del brief no se re-documentan.
5. **Verificación local COMPLETA** (bloque Local checks íntegro, incluido fuzz local) ANTES de push.
6. `straymark charter drift CHARTER-02` antes de commit; drifts → `R<N+1>` en el AILOG.
7. Commit + push + abrir PR contra `main`; CI verde (con `determinism` y `dual-engine` bloqueantes).
8. **Auditoría externa StrayMark (condición de cierre — ver §Charter Closure)** antes de cerrar.

## Charter Closure

A diferencia de CHARTER-01, este Charter **requiere auditoría externa multi-modelo antes del cierre**
(el corte cierra M0 y toca los gates de la constitución P-III/P-IV; amerita revisión cross-modelo):

1. Actualización atómica del Charter si el drift check reveló divergencias (mismo PR).
2. `straymark charter drift CHARTER-02 --range origin/main..HEAD` → limpio o documentado.
3. **Auditoría externa** (`straymark charter audit CHARTER-02`): el agente genera el prompt con
   `/straymark-audit-prompt`; el **operador** ejecuta ≥2 auditores CLI (gemini-cli, claude-cli,
   copilot-cli, codex-cli) vía `/straymark-audit-execute`; el agente consolida con
   `/straymark-audit-review`. Los findings `real_debt` se remedian antes de cerrar; el bloque
   `external_audit` de la telemetría se llena con la calibración cross-modelo.
4. `straymark charter close CHARTER-02` (telemetría, status `closed`). No borrar este archivo.
