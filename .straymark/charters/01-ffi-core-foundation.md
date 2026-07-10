---
charter_id: CHARTER-01-ffi-core-foundation
status: declared
effort_estimate: L
trigger: "tasks.md fija 21 tareas ordenadas (T001–T021, fases Setup + Foundational) que forman el primer corte shippable de M0: el shim yrs invocable de forma segura desde .NET con los gates de memoria P-I/P-II verdes (Checkpoint tasks.md L54)."
originating_spec: specs/001-weft-crdt-versioning/spec.md
work_verb: implement
design_provenance: new
---

# Charter: Fundación FFI y binding Core

> **Status (mirrored from frontmatter — source of truth is above):** declared. Effort: L.
>
> **Origin:** Derivado de `specs/001-weft-crdt-versioning/spec.md`. Primer corte shippable de M0 (T001–T021): scaffolding de solución + shim C-ABI sobre `yrs` con `catch_unwind` en cada entrada, y binding .NET (`ICrdtEngine`/`ICrdtDoc`) con memoria verificada en CI.

## Context

Weft necesita, antes que cualquier versionado o colaboración, una frontera FFI segura sobre el core Rust `yrs` y un binding .NET que la exponga sin que el GC toque memoria nativa. Este Charter cubre las dos primeras fases de la feature 001 (Setup + Foundational): crear el esqueleto de solución/workspace y entregar el shim C-ABI de 12 funciones (`contracts/ffi-abi.md`) más su binding `Weft.Core` (`contracts/core-api.md`), con los gates de memoria (P-I FFI segura, P-II memoria verificada) activos en CI.

Es la base bloqueante de todas las user stories: sin binding seguro no arranca ni el versionado (US1) ni el motor dual (US5). El diseño mayor está ✅ CERRADO en el brief y no se re-litiga; aquí el trabajo es de **implementación** contra contratos ya fijados.

## Scope

**In scope:**

1. Esqueleto de solución `Weft.sln` + proyectos stub (`Weft.Core`, `Weft.Versioning`), suites de test, `native/` workspace Rust con crate `weft-yrs-ffi` (cdylib, `yrs = "=0.27.2"`).
2. Configuración transversal: `Directory.Build.props` (net10.0, C# 13, Nullable, analyzers, Apache-2.0, SourceLink), `.editorconfig`, `rustfmt.toml`; esqueleto CI `.github/workflows/ci.yml` con jobs nombrados vacíos.
3. Shim `weft-yrs-ffi`: códigos de error + `catch_unwind` wrapper + `weft_abi_version`; ciclo de vida del doc, FFI de texto, y FFI de estado/sync (12 funciones de `contracts/ffi-abi.md`), con `weft_test_panic` tras feature `test-hooks`.
4. Header de contrato de ownership `include/weft_ffi.h`; suite Rust `tests/` + `mem_asan.rs` (≥2000 iteraciones/función incl. rutas de error); targets cargo-fuzz `doc_load`/`apply_update`.
5. Binding `Weft.Core`: abstracciones (`ICrdtEngine`, `ICrdtDoc`, `INativeVersioning`), jerarquía `WeftException` + `WeftErrorCode`, `DocHandle` (SafeHandle) + `HandleLease`, `NativeMethods` (`[LibraryImport]`) + `NativeLibraryResolver` (por RID + check ABI), `YrsEngine`/`YrsDoc`.
6. Tests .NET: `YrsDocTests` (round-trip byte-idéntico, errores, dispose), `ConvergenceTests` (CsCheck, SC-001), `PanicSafetyTests` (SC-009); wiring de los gates foundational en CI (build shim + `dotnet test` linux/win/mac, job `asan` nightly, `fuzz` smoke).

**Out of scope:**

- Versionado content-addressed (`VersionStore`, `IBlobStore`, `TextDiff`), gate de determinismo y motor Loro / dual-engine — diferido a **CHARTER-02** (US1 + US5), que cierra M0.
- Broker/concurrencia (US2), relay servidor (US3), empaquetado NuGet multi-RID (US4) — hitos M1–M3.
- Verificación de ausencia del símbolo `weft_test_panic` en binarios de release — pertenece a `pack-smoke` (US4); aquí solo se garantiza que vive tras `--features test-hooks`.

## Files to modify

<!-- Greenfield: todos los paths de código son New (el validador CHARTER-FILES-EXIST
     salta la comprobación de existencia en filas cuyo Change empieza con "New").
     Excepciones ya existentes en el árbol: .gitignore y LICENSE (commit 56c7174). -->

| File | Change |
|---|---|
| `Weft.sln`, `src/Weft.Core/Weft.Core.csproj`, `src/Weft.Versioning/Weft.Versioning.csproj` | New — solución + proyectos stub (T001) |
| `tests/Weft.Core.Tests/`, `tests/Weft.Versioning.Tests/` | New — proyectos de test stub (T001) |
| `native/Cargo.toml`, `native/rust-toolchain.toml`, `native/weft-yrs-ffi/Cargo.toml` | New — workspace Rust + crate cdylib pinneado (T002) |
| `Directory.Build.props`, `.editorconfig`, `rustfmt.toml` | New — config transversal (T003) |
| `.github/workflows/ci.yml` | New — esqueleto de jobs (T004); se puebla con gates foundational (T021) |
| `.gitignore` | Change — verificar entradas `target/`,`bin/`,`obj/`,`artifacts/` (T005) |
| `LICENSE` | Verify — Apache-2.0 en raíz ya presente (T005) |
| `native/weft-yrs-ffi/src/lib.rs` | New — shim completo: error codes, `catch_unwind`, `weft_abi_version`, doc/text/state FFI, `weft_test_panic` (T006–T009, T020) |
| `native/weft-yrs-ffi/include/weft_ffi.h` | New — header de ownership, 12 funciones + hook de test (T010) |
| `native/weft-yrs-ffi/tests/`, `native/weft-yrs-ffi/tests/mem_asan.rs` | New — unit + ASan ≥2000 iters/función (T011) |
| `native/weft-yrs-ffi/fuzz/fuzz_targets/doc_load.rs`, `apply_update.rs` | New — cargo-fuzz targets (T012) |
| `src/Weft.Core/Abstractions/ICrdtEngine.cs`, `ICrdtDoc.cs`, `INativeVersioning.cs` | New — abstracciones core (T013) |
| `src/Weft.Core/WeftException.cs` | New — jerarquía de excepciones + `WeftErrorCode` (T014) |
| `src/Weft.Core/Yrs/DocHandle.cs` | New — SafeHandle + `HandleLease` (T015, research R2) |
| `src/Weft.Core/Yrs/NativeMethods.cs`, `NativeLibraryResolver.cs` | New — `[LibraryImport]` + resolución por RID/ABI (T016) |
| `src/Weft.Core/Yrs/YrsEngine.cs`, `YrsDoc.cs` | New — `ICrdtEngine`/`ICrdtDoc` completos (T017) |
| `tests/Weft.Core.Tests/YrsDocTests.cs`, `ConvergenceTests.cs`, `PanicSafetyTests.cs` | New — round-trip, convergencia (SC-001), panic-safety (SC-009) (T018–T020) |
| `.straymark/07-ai-audit/agent-logs/AILOG-*.md` | New, `risk_level: high` — traza de ejecución del shim + binding |

## Verification

### Local checks

```bash
# Shim Rust: build + tests + memoria (ASan requiere nightly)
cargo build --manifest-path native/Cargo.toml
cargo test  --manifest-path native/Cargo.toml
RUSTFLAGS="-Zsanitizer=address" cargo +nightly test \
  --manifest-path native/Cargo.toml -p weft-yrs-ffi --features test-hooks \
  --target x86_64-unknown-linux-gnu            # gate P-II: 0 fugas / 0 double-free

# Fuzz smoke (bytes arbitrarios → solo códigos de error, nunca panic-through)
cargo +nightly fuzz run doc_load     -- -max_total_time=60
cargo +nightly fuzz run apply_update -- -max_total_time=60

# Binding .NET: build + tests (round-trip, convergencia, panic-safety)
dotnet test tests/Weft.Core.Tests/
```

### Production smoke (after deploy)

No aplica — Weft es una librería/building block sin despliegue. Los auditores externos deben **saltar** esta sección; no hay comandos post-deploy y su ausencia no es `real_debt`.

## Risks

- **R1 — Panic de Rust cruzando la frontera C (P-I)**: severidad crítica. Mitigación: cada entrada del shim envuelta en `catch_unwind` → `WEFT_ERR_PANIC`; `weft_test_panic` (feature `test-hooks`) + `PanicSafetyTests` verifican que produce `WeftEngineException(ErrorCode.Panic)` y el proceso sigue estable. Si la mitigación falla: el job `asan`/panic-injection en CI bloquea el merge.
- **R2 — Fuga o double-free en la frontera (P-II)**: severidad crítica. Mitigación: ownership `Box<[u8]>`, liberación **solo** vía `weft_buf_free`; en C#, `SafeHandle` + `HandleLease` (DangerousAddRef/Release); ASan/LSan ≥2000 iters/función incluidas rutas de error. Si falla: job `asan` (nightly) bloquea el cierre.
- **R3 — Acceso concurrente a `ICrdtDoc` (no thread-safe)**: severidad media. En M0 el acceso serializado es responsabilidad del dueño (el broker llega en US2/M1); los tests no comparten doc entre hilos. Mitigación: contrato de ownership explícito en `weft_ffi.h`; `ObjectDisposedException` tras dispose.
- **R4 — ABI mismatch / resolución nativa por RID falla (P-VI)**: severidad media. Mitigación: `NativeLibraryResolver` resuelve el binario por RID y verifica `weft_abi_version` contra la esperada; el mismatch produce una excepción clara al cargar, no un fallo silencioso.
- **R5 — Export no determinista socava el round-trip (P-III)**: severidad media. El gate de determinismo pertenece a CHARTER-02 (US1), pero el round-trip export/load byte-idéntico (T018) se asienta aquí con client-ids controlados. Si emerge no-determinismo, se documenta como `R<N+1> (new, not in Charter)` en el AILOG.

## Tasks

1. Sync `main`; branch `feat/charter-01-ffi-core`. Flip frontmatter `declared` → `in-progress`.
2. Re-evaluar **Constitution Check** (`.specify/memory/constitution.md`) contra este scope antes de ejecutar.
3. `/speckit-implement` acotado a **T001–T021**; marcar `[X]` + anotar `*CHARTER-01: <sha>*` por tarea en `tasks.md`.
4. Emitir **AILOG** (`risk_level: high`, `review_required: true`) cubriendo shim + binding; **AIDEC** para decisiones de implementación nuevas (p.ej. estrategia de `HandleLease`, layout de códigos de error). Las decisiones ✅ CERRADO del brief no se documentan de nuevo.
5. Verificación local pasa limpia (bloque Local checks completo, gates P-I/P-II verdes).
6. `straymark charter drift CHARTER-01` antes de commit; drifts no previstos → `R<N+1> (new, not in Charter)` en el AILOG; expansión de scope → justificar en el AILOG.
7. Commit + push + abrir PR contra `main`.

## Charter Closure

Al cerrar: aplicar actualización atómica del Charter si el drift check reveló divergencias (mismo PR), correr `straymark charter drift CHARTER-01 --range origin/main..HEAD`, mover la fila a `## Closed` en `.straymark/charters/README.md`, y `straymark charter close CHARTER-01` (telemetría: estimado vs real, drift, lecciones). No borrar este archivo.
