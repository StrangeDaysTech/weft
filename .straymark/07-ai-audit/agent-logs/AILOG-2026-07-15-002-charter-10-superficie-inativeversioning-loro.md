---
id: AILOG-2026-07-15-002
title: "CHARTER-10: superficie INativeVersioning de Loro — probes nativos diff/fork/shallow (cierra G1)"
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
observability_scope: none
tags: [ffi-boundary, abi-bump, loro, native-versioning, probes, engine-abstraction, g1]
related: [AIDEC-2026-07-15-002, AILOG-2026-07-10-002]
originating_charter: CHARTER-10-superficie-inativeversioning-de-loro-probes
---

# AILOG: CHARTER-10 — superficie INativeVersioning de Loro (probes nativos, cierra G1)

## Summary

Despacho de CHARTER-10 (FU-006, hallazgo **G1** de la auditoría CHARTER-02): materializa la capacidad
**opcional** `INativeVersioning` para el motor **Loro** — tres probes **demostrativos** del versionado
nativo de Loro (`ShallowSnapshot`, `NativeDiffProbe`, `NativeBranchMergeProbe`) que yrs no tiene. Cierra G1:
`LoroEngine.NativeVersioning` pasa de `null` a una implementación real. Los probes **no** son
content-addressing (salida no determinista, no alimentan `VersionId`); exhiben la capacidad nativa (P-IV).
Sin auditoría externa (no cierra hito).

## Actions Performed

1. **Shim `weft-loro-ffi` — 3 probes (ABI v1→v2)**: `weft_loro_shallow_snapshot` (export
   `ExportMode::shallow_snapshot(&state_frontiers())`), `weft_loro_native_diff_probe` (JSON de
   `doc.diff(Frontiers::default(), state_frontiers)` — containers + text_len), `weft_loro_native_branch_merge_probe`
   (fork → editar el fork → `import` en una copia aparte → JSON con convergencia; **no muta el caller**).
   `WEFT_ABI_VERSION` 1→2; `catch_unwind` en cada entrada; JSON armado a mano (sin `serde_json`) con
   `json_escape`. `mem_asan.rs`: assert ABI 1→2 + test de reachability/no-fugas/no-mutación de los 3 probes.
2. **Header `weft_loro_ffi.h` (NEW)**: el shim Loro no tenía header; se crea espejando `weft_ffi.h` (todas
   las funciones + las 3 nuevas + el contrato de ownership).
3. **Binding**: 3 P/Invokes en `Interop/NativeMethods.cs`; `ExpectedAbiVersion` 1→2 en
   `Interop/NativeLibraryResolver.cs`; 3 métodos `internal` en `LoroDoc.cs` (delegan vía `HandleLease`).
4. **`LoroNativeVersioning.cs` (NEW)** + **`LoroEngine.NativeVersioning`** no-nulo (singleton). Castea
   `ICrdtDoc → LoroDoc` con `ArgumentException` clara si se pasa un doc no-Loro.
5. **Tests** (`LoroNativeVersioningTests`, NEW, 5/5): shallow no-vacío y recargable; diff refleja ediciones
   (JSON parseado); branch/merge converge y NO muta el caller; guard de doc no-Loro lanza; yrs `null`.
6. **Quickstart §US5** reconciliado (G1 cerrado, `NativeVersioning` ya no null). **FU-006** → `closed`.

## Modified Files

**Nativo**: `native/weft-loro-ffi/src/lib.rs` (3 probes + ABI v2 + json_escape),
`native/weft-loro-ffi/include/weft_loro_ffi.h` (NEW), `native/weft-loro-ffi/tests/mem_asan.rs` (ABI + probes).
**Binding**: `src/Weft.Loro/Interop/NativeMethods.cs`, `NativeLibraryResolver.cs`, `LoroDoc.cs`,
`LoroNativeVersioning.cs` (NEW), `LoroEngine.cs`. **Tests/spec**:
`tests/Weft.Versioning.Tests/LoroNativeVersioningTests.cs` (NEW), `specs/001-weft-crdt-versioning/quickstart.md`.
**Gobernanza**: `.straymark/follow-ups-backlog.md` (FU-006 closed, FU-017), `.straymark/charters/10-*.md`,
AIDEC-2026-07-15-002 (NEW).

## Risk

- **R1 (medio, del Charter) — API nativa de Loro**: RESUELTO. `ExportMode::shallow_snapshot(&Frontiers)`,
  `diff(a,b)→DiffBatch`, `fork()`, `state_frontiers()` confirmados y usados; los 3 probes compilan y pasan.
- **R2 (bajo, del Charter) — salida no determinista**: aceptado por diseño. Los probes son demostrativos, NO
  content-addressing (documentado en docstrings/header/quickstart/AIDEC). Tests asertan round-trip/convergencia.
- **R3 (bajo) — ABI bump v1→v2 del shim Loro**: bump atómico Rust + .NET; exports aditivos; `mem_asan.rs`
  actualizado. Desalineación → `NativeLibraryResolver` lanza explícito.
- **R4 (bajo) — cast ICrdtDoc→LoroDoc**: guard con `ArgumentException`; test cubre un doc yrs → lanza.

## Verification

```bash
# Shim Loro: compila + símbolos + ABI v2 + cargo test (incl. probes bajo ASan)
cd native/weft-loro-ffi && cargo build --release && cargo test   # 5/5
nm -D ../target/release/libweft_loro_ffi.so | grep -E "weft_loro_shallow_snapshot|native_diff_probe|native_branch_merge_probe"

# Suite .NET completa incl. LoroNativeVersioningTests (5/5)
cd ../.. && dotnet test Weft.sln -c Release
```

## Follow-ups

Derivado de que el shim Loro no tenía header (a diferencia de yrs). No bloquea:

- **Follow-up (test infra, baja)**: añadir un test de paridad **header↔binding** para el shim Loro
  (`weft_loro_ffi.h` ↔ `Weft.Loro/Interop/NativeMethods.cs`), como el que ya valida el shim yrs
  (`weft_ffi.h` ↔ `Weft.Core`). El header se creó en CHARTER-10 pero ningún test automatizado verifica que
  las declaraciones `[LibraryImport]` coincidan con él. **Trigger**: ready (mejora de robustez). **Destination**:
  chore. **Cost**: S.

## Additional Notes

- El shallow snapshot ES recargable (`LoadDoc`/`import`) — capacidad real de Loro, solo que no citable (no
  determinista). El diff probe reporta `containers_changed` (≥1 tras editar) + `text_len_utf16`.
- El branch/merge probe forkea DOS veces (branch para editar + target para mergear), dejando el doc del caller
  intacto — verificado por test (`Assert.Equal("base", doc.GetText("body"))`).

## Approval

Trabajo de frontera nativa (`risk_level: medium`, `review_required: true`) con ABI bump. El operador autorizó
ejecución continua y el alcance demostrativo de los probes (ex-ante en el Charter). Verificación local citada;
el CI del PR valida en toda la matriz. Compañero de AIDEC-2026-07-15-002.
