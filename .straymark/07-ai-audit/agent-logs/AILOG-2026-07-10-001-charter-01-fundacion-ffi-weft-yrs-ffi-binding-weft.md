---
id: AILOG-2026-07-10-001
title: "CHARTER-01: fundación FFI weft-yrs-ffi + binding Weft.Core (T001–T021)"
status: accepted
created: 2026-07-10
agent: claude-opus-4-8
confidence: high
review_required: true
risk_level: high
eu_ai_act_risk: not_applicable
nist_genai_risks: []
iso_42001_clause: []
lines_changed: 1400
files_modified: []
observability_scope: none
tags: [ffi, rust, dotnet, memoria, crdt]
related: []
originating_charter: CHARTER-01-ffi-core-foundation
---

# AILOG: CHARTER-01 — fundación FFI weft-yrs-ffi + binding Weft.Core (T001–T021)

## Summary

Primer corte shippable de M0: esqueleto de solución/workspace + shim C-ABI propio sobre `yrs`
(`weft-yrs-ffi`, 12 funciones + hook de test) y binding seguro `Weft.Core` (`ICrdtEngine`/`ICrdtDoc`,
`SafeHandle`+`HandleLease`, `[LibraryImport]`, resolución por RID). Gates de memoria (P-I/P-II)
verdes local: shim compila con clippy limpio, 5 tests Rust pasan bajo AddressSanitizer+LeakSanitizer
(0 fugas / 0 double-free), y 16 tests .NET verdes (round-trip byte-idéntico, convergencia
property-based con CsCheck, panic-safety).

## Context

Weft necesita una frontera FFI segura y su binding .NET antes de cualquier versionado o
colaboración. Ejecución del subset T001–T021 de `specs/001-weft-crdt-versioning/tasks.md` bajo el
contrato de `.straymark/charters/01-ffi-core-foundation.md`. El diseño mayor está ✅ CERRADO en el
brief; el trabajo aquí es de implementación contra los contratos `ffi-abi.md` y `core-api.md`, con
código de referencia validado en los spikes 01/03 (reescrito limpio con nombres `weft_*`).

## Actions Performed

1. **Setup (T001–T005)**: `Weft.sln` + proyectos (`Weft.Core`, `Weft.Versioning`, dos suites de
   test); `Directory.Build.props` (net10.0/C#13, Nullable, analyzers, `TreatWarningsAsErrors`,
   Apache-2.0, SourceLink), `.editorconfig`, `rustfmt.toml`; workspace Rust `native/` con crate
   `weft-yrs-ffi` (cdylib+rlib, `yrs = "=0.27.2"`); esqueleto CI `.github/workflows/ci.yml`.
2. **Shim (T006–T010, T020)**: `lib.rs` con `catch_unwind` en cada entrada, ownership `Box<[u8]>`,
   12 funciones de la ABI (doc lifecycle, texto con validación de rango, estado/sync), `weft_test_panic`
   tras feature `test-hooks`, y header de ownership `include/weft_ffi.h`.
3. **Tests nativos (T011–T012)**: suite `mem_asan.rs` (round-trip, convergencia, rutas de error,
   estrés ≥2000 iteraciones) + targets cargo-fuzz `doc_load`/`apply_update`.
4. **Binding (T013–T017)**: abstracciones, jerarquía `WeftException`+`WeftErrorCode`, `DocHandle`+
   `HandleLease`, `NativeMethods`+`NativeLibraryResolver` (RID + check `weft_abi_version`),
   `YrsEngine`/`YrsDoc`.
5. **Tests .NET (T018–T020)**: `YrsDocTests` (round-trip, errores tipificados, dispose),
   `ConvergenceTests` (CsCheck, SC-001), `PanicSafetyTests` (SC-009).
6. **CI (T021)**: gates `test` (linux/win/mac), `asan` (nightly), `fuzz` (smoke 60 s) activos;
   `determinism`/`dual-engine`/`pack-smoke` nombrados como placeholders para fases posteriores.

## Modified Files

| File | Change Description |
|------|--------------------|
| `native/weft-yrs-ffi/src/lib.rs`, `include/weft_ffi.h` | Shim C-ABI (12 fn + test hook) y header de ownership |
| `native/weft-yrs-ffi/tests/mem_asan.rs`, `fuzz/**` | Suite ASan + targets de fuzz |
| `native/Cargo.toml`, `rust-toolchain.toml`, `weft-yrs-ffi/Cargo.toml` | Workspace Rust pinneado |
| `src/Weft.Core/Abstractions/*.cs`, `WeftException.cs`, `Yrs/*.cs` | Binding seguro completo |
| `tests/Weft.Core.Tests/*.cs` | 16 tests (unit, convergencia, panic-safety) |
| `Directory.Build.props`, `.editorconfig`, `rustfmt.toml`, `Weft.sln`, `*.csproj` | Config transversal |
| `.github/workflows/ci.yml` | Gates foundational + placeholders |

## Decisions Made

- **Validación de rango en el shim** (no en el spike): `weft_text_insert/delete` verifican el índice
  contra la longitud del campo y devuelven `WEFT_ERR_OUT_OF_BOUNDS`, cumpliendo `ffi-abi.md`.
- **`FfiStatus` interno + `InternalsVisibleTo`**: el mapeo código→excepción se extrajo a un helper
  verificable; `PanicSafetyTests` carga el cdylib con `NativeLibrary` e invoca `weft_test_panic`
  directamente, manteniendo el hook fuera de la superficie de producción.
- **`rust-toolchain.toml` canal `stable`** (no versión exacta): la reproducibilidad del determinismo
  la garantizan el pin exacto de `yrs` + `Cargo.lock` versionado, no la versión de `rustc`.
- **`.gitignore`**: se dejó de ignorar `Cargo.lock` (research R16) y se añadieron artefactos de fuzz.
- **`Weft.sln` clásico** en vez de `.slnx` (compatibilidad de tooling) y **CA2255 suprimido** con
  justificación en `NativeLibraryResolver` (ModuleInitializer es el patrón idiomático del resolver).

## Impact

- **Functionality**: `Weft.Core` puede crear/cargar documentos yrs, editar texto por campo,
  exportar/aplicar estado y deltas incrementales; API idiomática con índices `int` validados.
- **Performance**: entrada zero-copy (span pinned), salida con una copia inevitable (memoria de Rust).
- **Security**: superficie FFI con `catch_unwind` en cada entrada y ownership estricto; ningún panic
  cruza la frontera; el GC jamás toca memoria nativa. **Riesgo alto** por naturaleza (memoria nativa)
  — mitigado por los gates de sanitizers.
- **Privacy / Environmental**: N/A.

## Verification

- [x] Compila sin errores ni warnings (`dotnet build -c Release`: 0/0; `cargo clippy -D warnings`: limpio)
- [x] Tests pasan (16 .NET verdes; 5 Rust verdes)
- [x] Gate de memoria P-II verde local: 5 tests bajo `-Zsanitizer=address` + `detect_leaks=1`, 0 fugas
- [x] Panic-safety SC-009: `weft_test_panic` → `WEFT_ERR_PANIC` (100 iter, proceso estable) → `WeftEngineException(Panic)`
- [ ] Revisión humana del operador (pendiente — `review_required: true`)
- [ ] Fuzz smoke y ASan en CI (se ejecutan en el PR; `cargo-fuzz`/nightly no disponibles en local)

## Additional Notes

Riesgos R1–R5 del Charter mitigados según lo declarado. **Emergió R6 (new, not in Charter)**
durante el fuzz de CI (ver abajo). El corte cierra el Checkpoint de `tasks.md` L54 (binding seguro
con gates P-I/P-II).

### Risk: R6 (new, not in Charter) — robustez del decoder de yrs ante update no confiable

El fuzz smoke destapó dos características del decoder de yrs 0.27.2 sobre input malformado. El shim
FFI es correcto (contiene panics con `catch_unwind`, sin UB), pero **(1) es un DoS real**, no un
artefacto:

1. **Amplificación de memoria → DoS (severidad alta).** Updates como `[0xd8,0xd8,0xeb,0x23]`,
   `[0xfa,0xff,0xa4,0x25]` declaran una longitud `N` enorme en pocos bytes; yrs hace
   `with_capacity(N)` **sin cota**. En un entorno con memoria suficiente la reserva es virtual, no
   se llena, y yrs falla el decode → `WEFT_ERR_DECODE` (medido local: RSS real ~150 MB). Pero en un
   entorno con **memoria limitada** (runner CI de 7 GB, contenedor de servidor) la asignación de
   ~14 GB **falla y Rust aborta** (`handle_alloc_error` → SIGABRT, **NO capturable por
   catch_unwind**). Un update malicioso de 4 bytes puede así **tumbar el proceso**. El límite de
   *tamaño de mensaje* no lo previene (4 bytes → 14 GB); requiere validar el *contenido* del update
   o acotar la memoria del decode.
2. **Panics del decoder (contenidos).** Updates como `[0x4a,0x01,0xed,…,0x21]` disparan un
   `assert!` en `yrs/src/block.rs`. El shim los contiene con `catch_unwind` → `WEFT_ERR_PANIC`
   (cumple R14). libfuzzer-sys aborta en su panic hook antes de que catch_unwind actúe, así que el
   harness lo silencia para ejercitar el camino de producción.

**Decisión (operador, 2026-07-10)**: la mitigación robusta (validar longitudes del update
pre-decode, o límite de recursos, o bump de yrs que devuelva `Err`) es un ciclo de diseño propio
que NO pertenece a la fundación FFI. Arquitectónicamente el input no confiable *de red* llega en
**M2** (servidor relay); en M0 el consumidor controla el input. Por tanto:
- El job `fuzz` pasa a **informativo** en M0 (`continue-on-error`): sigue corriendo y reportando,
  sin bloquear el cierre. El fuzz con `-s none -rss_limit_mb=0 -max_len=8192` + hook silenciado
  detecta SIGSEGV/UB/crashes reales del shim; la memory-safety la cubre el job `asan`.
- Tests de regresión permanentes fijan el comportamiento observado:
  `malformed_update_with_huge_declared_length_decodes_cleanly` y
  `malformed_update_that_panics_yrs_is_contained_not_ub`.
- **Follow-up de alta prioridad (M2)**: mitigar la amplificación en la capa que recibe tráfico de
  red (validación de update + límite de recursos del proceso) y hacer el gate `fuzz` bloqueante de
  nuevo. Evaluar un bump de `yrs` (protocolo R16). El consumidor de M0/M1 debe tratar el input de
  `ApplyUpdate`/`LoadDoc` como confiable hasta entonces (documentar en la API pública).

Mitigación de producto (diferida, **follow-up**): la resistencia a amplificación de memoria con
input no confiable se endurece en la capa que recibe tráfico de red — el servidor relay (M2):
límite de tamaño de mensaje + límite de recursos del proceso/contenedor. Investigar además si un
bump de `yrs` (protocolo R16) introduce validación de longitud en `decode_v1`.

CHARTER-02 (US1 + US5) continúa con versionado content-addressed, gate de determinismo y dual-engine
para cerrar M0.

**Scope expansion del drift check (intencional)**: `straymark charter drift` reporta 23 archivos
modificados no declarados. Corresponden en su totalidad al scope de T001–T021, declarado en el
Charter con notación compacta que el parser no expande: (a) rutas de código con llaves
(`src/Weft.Core/Yrs/{DocHandle,NativeMethods,…}.cs`, `Abstractions/*.cs`); (b) config transversal
de T003–T005 (`Directory.Build.props`, `.editorconfig`, `rustfmt.toml`, `Weft.sln`, `*.csproj`,
`native/Cargo.toml`, `rust-toolchain.toml`, `Cargo.lock`); (c) `tasks.md` (marcado de progreso);
(d) `FfiStatus.cs`, archivo nuevo por la decisión de extraer el mapeo código→excepción (ver
Decisions). No hay expansión de alcance real fuera de T001–T021.
