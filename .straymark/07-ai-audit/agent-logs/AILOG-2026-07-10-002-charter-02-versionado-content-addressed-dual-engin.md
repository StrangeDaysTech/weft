---
id: AILOG-2026-07-10-002
title: "CHARTER-02: versionado content-addressed + dual-engine (T022–T035)"
status: accepted
created: 2026-07-10
agent: claude-opus-4-8
confidence: high
review_required: true
reviewed_by: Jose Villaseñor Montfort
reviewed_at: 2026-07-11
review_outcome: approved
risk_level: high
eu_ai_act_risk: not_applicable
nist_genai_risks: []
iso_42001_clause: []
lines_changed: 1600
files_modified: []
observability_scope: none
tags: [versionado, crdt, dual-engine, loro, ffi, determinismo]
related: [AILOG-2026-07-10-001]
originating_charter: CHARTER-02-versioning-dual-engine
---

# AILOG: CHARTER-02 — versionado content-addressed + dual-engine (T022–T035)

## Summary

Segundo y último corte de M0: capa de dominio de versionado engine-agnóstica (`VersionId` SHA-256,
`IBlobStore` + In-Memory/FileSystem, `TextDiff` LCS, `VersionStore` publish/checkout/diff/branch/merge)
y adaptador dual-path Loro (`weft-loro-ffi` + `Weft.Loro`). **M0 cerrado**: la misma suite de
versionado (7 postcondiciones) pasa idéntica sobre yrs Y Loro (P-IV), con gate de determinismo activo
(P-III). Todas las verificaciones locales verdes ANTES de CI (lección de CHARTER-01).

## Context

Ejecución de T022–T035 bajo `.straymark/charters/02-versioning-dual-engine.md`, sobre la fundación
FFI de CHARTER-01. Contra `contracts/versioning-api.md` y `core-api.md`; código de referencia del
shim Loro en los spikes 02/03 (reescrito limpio con nombres `weft_loro_*`).

## Actions Performed

1. **US1 versionado (T022–T031)**: `VersionId` (SHA-256, hex 64, Parse/TryParse); `IBlobStore` +
   `InMemoryBlobStore` (dedup por hash) + `FileSystemBlobStore` (sharding `aa/bb/hash`, escritura
   atómica temp+rename); `TextDiff` (LCS por tokens palabra/espacio); `VersionStore` (verifica
   integridad → `BlobIntegrityException`); suite parametrizada `VersioningSuiteBase` (7 postcondiciones)
   + `YrsVersioningTests` + `TextDiffTests`; proyecto `Weft.Determinism.Tests` (gate P-III); sample
   runnable `Weft.Sample.Versioning`; wiring CI (`determinism` bloqueante).
2. **US5 dual-engine (T032–T035)**: crate `weft-loro-ffi` (12 fn C-ABI simétricas + test hook, índices
   UTF-16, export Snapshot, state-vector vía `VersionVector`) + suite ASan + fuzz targets; binding
   `Weft.Loro` (`LoroEngine`/`LoroDoc` + interop simétrico a Weft.Yrs); `LoroVersioningTests` (hereda
   `VersioningSuiteBase`); CI: `dual-engine` bloqueante (T034) + `asan` extendido a loro (T035).

## Modified Files

| File | Change Description |
|------|--------------------|
| `src/Weft.Versioning/*.cs`, `Blobs/*.cs` | VersionId, blob stores, TextDiff, VersionStore |
| `native/weft-loro-ffi/**` | Shim C-ABI sobre loro 1.13.6 + tests + fuzz |
| `native/weft-yrs-ffi/src/lib.rs` | **Fix R6** (OffsetKind::Utf16) — scope expansion |
| `native/Cargo.toml` | Añadido member weft-loro-ffi |
| `src/Weft.Loro/**` | Binding dual-path (LoroEngine/LoroDoc + interop) |
| `tests/Weft.Versioning.Tests/*`, `tests/Weft.Determinism.Tests/*` | Suite dual-engine + gate determinismo |
| `samples/Weft.Sample.Versioning/*` | User journey US1 |
| `.github/workflows/ci.yml`, `Weft.sln` | Gates determinism/dual-engine/asan-loro; proyectos |

## Decisions Made

- **Export de Loro para content-addressing = `all_updates`, NO `Snapshot`** (ver R7 abajo).
- **Índices UTF-16 en Loro**: se usan `insert_utf16`/`delete_utf16`/`len_utf16` para consistencia con
  la abstracción (.NET string) y con yrs.
- **`LoroEngine.NativeVersioning = null`**: los probes de versionado nativo de Loro
  (`INativeVersioning`: diff/branch/shallow) son capacidad OPCIONAL; el versionado del núcleo no los
  requiere y la suite dual-engine no los usa. Diferidos a una iteración posterior.

## Impact

- **Functionality**: publish/checkout/diff/branch/merge content-addressed sobre dos motores; identidad
  citable (SHA-256) reproducible.
- **Security**: segundo shim FFI con `catch_unwind` + ownership; ASan/LSan verde en ambos. **Riesgo alto**
  por memoria nativa, mitigado por gates.
- **Performance**: dedup natural por hash; compactación por construcción (GC del motor activo).

## Verification

- [x] Compila sin warnings (`dotnet build -c Release` 0/0; `cargo clippy -D warnings` limpio ambos shims)
- [x] **36 tests .NET** verdes (Core 18, Versioning 17 dual-engine, Determinism 2 — incl. 6 postcondiciones × 2 motores)
- [x] **Gate P-II**: ASan/LSan sobre AMBOS shims (yrs 7 + loro 5 tests) → 0 fugas
- [x] **Fuzz local ambos shims**: yrs (informativo, R6) + loro (limpio, ~1.3M runs/target, no amplifica)
- [x] Sample end-to-end ejecutado (journey US1 legible)
- [x] Revisión humana del operador — aprobada 2026-07-11 (ver §Approval)
- [ ] **Auditoría externa StrayMark** (condición de cierre del Charter — pendiente antes de close)

## Additional Notes

### Risk: R6 (new, not in Charter) — índices de yrs eran byte-offsets, no UTF-16

Al ejercitar el diff con texto acentuado, el sample expuso un **bug latente de CHARTER-01**:
`yrs::Doc::new()` usa `OffsetKind::Bytes` por defecto, así que insert/delete indexaban en **bytes
UTF-8**, no UTF-16 — inconsistente con el índice `int` de la API (.NET string = UTF-16) **y con Yjs**
(UTF-16, crítico para los clientes de editor de US3). CHARTER-01 no lo detectó porque sus tests usaban
solo ASCII (donde bytes == UTF-16). **Corregido**: el shim crea el doc con `OffsetKind::Utf16`
(`native/weft-yrs-ffi/src/lib.rs`, fuera de los Files to modify declarados → scope expansion
justificada). Blindado con `Utf16IndexingTests` (regresión permanente). No se bump la ABI (la firma no
cambió; el comportamiento pasa a ser el que el contrato siempre declaró). Loro no tenía el bug (usa
`insert_utf16` explícito).

### Risk: R7 (new, not in Charter) — Snapshot de Loro no es content-addressable determinista

El primer intento usó `ExportMode::Snapshot` para el export de content-addressing de Loro. El
snapshot incluye metadata dependiente de la réplica (peer-ids aleatorios, orden interno del estado
materializado) y **NO es byte-determinista entre réplicas convergidas**: dos docs con el mismo estado
lógico producen snapshots distintos → VersionId distinto (viola SC-002). Los tests eran **flaky**:
verde en local (Linux) y macOS por casualidad, **rojo en ubuntu-latest y windows-latest** (misma
arquitectura que local → no es cross-plataforma, es no-determinismo genuino). **El CI multiplataforma
lo destapó** — un solo runner habría dejado pasar el bug. Corregido a `ExportMode::all_updates()`, que
serializa el oplog de forma canónica (el spike 03 ya lo señalaba como "el análogo del update v1 de yrs
para content-addressing"); verificado con **20 corridas consecutivas sin flakiness**. Lección: el gate
dual-engine + matriz multiplataforma es lo que hace visible el no-determinismo del content-addressing.

### Nota: Loro no amplifica memoria (R6 de CHARTER-01 no aplica)

El fuzz del shim Loro corrió limpio (~1.3M runs/target, RSS ~43 MB, exit 0) sin el DoS de amplificación
que tiene el decoder de yrs. El fuzz de Loro es informativo por simetría, pero podría promoverse a
bloqueante en el futuro.

### Verificación local ANTES de CI (lección de CHARTER-01 aplicada)

`cargo-fuzz` estaba instalado desde el inicio; dual-engine, ASan de ambos shims y fuzz de ambos se
ejecutaron localmente y en verde antes de pushear. Sin iteraciones a ciegas en CI.

## Follow-ups (auditoría externa CHARTER-02)

Derivados de la auditoría multi-modelo (gpt-5-5, qwen3-7-max, gemini-3-1-pro; ver
`.straymark/audits/CHARTER-02/review.md`). Ninguno bloquea el cierre de M0:

- **Follow-up (G1, alta)**: implementar la superficie `INativeVersioning` de Loro diferida — probes
  `native_diff_probe`/`native_branch_probe`/`shallow_snapshot` en `weft-loro-ffi`, header
  `include/weft_loro_ffi.h`, y `LoroNativeVersioning.cs` (`LoroEngine.NativeVersioning` pasaría de
  `null` a la implementación). Capacidad opcional; reconciliado en tasks.md/quickstart como diferido.
- **Follow-up (G3, baja)**: usar `VersionId` directo como key de `InMemoryBlobStore` en vez de
  `id.ToString()` (ahorra la asignación del hex de 64 chars por operación).
- **Follow-up (G4, baja)**: añadir un guard de compatibilidad de motor en `VersionStore.Merge` para
  que un merge cross-engine lance `ArgumentException` clara en vez de `CorruptUpdateException` opaca.
- **Follow-up (G5, baja)**: añadir un test directo de `FileSystemBlobStore` (round-trip + sharding con
  directorio temporal); hoy solo `InMemoryBlobStore` se ejercita en la suite.

## Approval

**Approved**: 2026-07-11 by `Jose Villaseñor Montfort`.
