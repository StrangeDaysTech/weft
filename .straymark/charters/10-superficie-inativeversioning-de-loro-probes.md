---
charter_id: CHARTER-10-superficie-inativeversioning-de-loro-probes
status: in-progress
effort_estimate: M
trigger: "FU-006 (backlog, charter-triggered): implementar la superficie opcional INativeVersioning de Loro (probes nativos), diferida en CHARTER-02 (auditoría G1). Disparado por decisión del operador (2026-07-15) de despacharla tras CHARTER-09. Cierra G1: LoroEngine.NativeVersioning pasa de null a una implementación real."
originating_spec: specs/001-weft-crdt-versioning/spec.md
work_verb: implement
design_provenance: new
---

# Charter: Superficie INativeVersioning de Loro — probes nativos diff/fork/shallow (cierra G1)

> **Status (mirrored from frontmatter — source of truth is above):** declared. Effort: M.
>
> **Origin:** Follow-up **FU-006** (auditoría externa CHARTER-02, hallazgo **G1**), sobre la spec 001
> (constitución **P-IV** abstracción de motor viva). Materializa la capacidad **opcional** `INativeVersioning`
> para el motor **Loro**, diferida a post-M0. Ningún gate depende de ella.

## Context

`INativeVersioning` (`src/Weft.Core/Abstractions/INativeVersioning.cs`) es una capacidad **opcional** que un motor
CRDT puede exponer si tiene versionado nativo — tres probes de paridad: `NativeDiffProbe(doc, field) → string`
(JSON), `NativeBranchMergeProbe(doc, field) → string` (JSON) y `ShallowSnapshot(doc) → byte[]`. yrs **no** tiene
versionado nativo (`YrsEngine.NativeVersioning == null`, permanente); **Loro sí** (`fork`/`fork_at`, `diff`,
`ExportMode::shallow_snapshot`), pero la superficie se **difirió** en CHARTER-02 (auditoría G1) → hoy
`LoroEngine.NativeVersioning == null` y el quickstart §US5 lo documenta como diferido.

Este Charter cierra G1: implementa los tres probes en el shim `weft-loro-ffi` (ABI **v1→v2**) + binding, hace
`LoroEngine.NativeVersioning` no-nulo, y reconcilia el quickstart. Los probes son **demostrativos** (exhiben la
capacidad nativa de Loro que el versionado engine-agnóstico de `Weft.Versioning` no usa), **no** un contrato de
content-addressing: sus bytes/JSON son informativos, no alimentan `VersionId` (que sigue usando `export_state`
determinista vía `all_updates`). No es una API de versionado nativo completa — es la superficie de paridad que G1
pedía materializar; una API más rica sería un charter aparte si alguna vez se requiere.

## Scope

**In scope:**

1. **Shim `weft-loro-ffi` — 3 probes nativos (ABI bump v1→v2):**
   - `weft_loro_shallow_snapshot(doc, out_ptr, out_len)` → `doc.export(ExportMode::shallow_snapshot(&doc.state_frontiers()))` (snapshot GC'd al estado actual); bytes por `hand_out_buffer`.
   - `weft_loro_native_diff_probe(doc, field, field_len, out_ptr, out_len)` → JSON que describe `doc.diff(Frontiers::default(), doc.state_frontiers())` para el campo (containers cambiados / resumen del diff del texto).
   - `weft_loro_native_branch_merge_probe(doc, field, field_len, out_ptr, out_len)` → JSON de un ciclo fork→editar→merge (`fork()` + op en el fork + `import` de vuelta), reportando frontiers antes/después y convergencia.
   - Incrementa `WEFT_ABI_VERSION` **1→2** en `lib.rs`; `catch_unwind` en cada entrada (P-I).
2. **Header `include/weft_loro_ffi.h` (NEW)**: crear el header C del shim Loro (no existía) — declara TODAS las
   funciones (ciclo de vida, texto, estado, memoria, diagnóstico) + las 3 nuevas. Paridad con `weft_ffi.h` (yrs).
3. **Binding .NET**: 3 P/Invokes en `Interop/NativeMethods.cs`; `ExpectedAbiVersion` **1→2** en
   `Interop/NativeLibraryResolver.cs`; 3 métodos `internal` en `LoroDoc.cs` (delegan al FFI vía `HandleLease`,
   encapsulando el handle como el resto).
4. **`LoroNativeVersioning.cs` (NEW)**: implementa `INativeVersioning` — castea `ICrdtDoc → LoroDoc` (excepción
   clara si se pasa un doc no-Loro) y delega en los métodos de `LoroDoc`.
5. **`LoroEngine.NativeVersioning`**: de `null` a una instancia de `LoroNativeVersioning` (singleton sin estado).
6. **Tests** (`tests/Weft.Versioning.Tests/LoroNativeVersioningTests.cs`, NEW): los 3 probes ejercitados —
   `ShallowSnapshot` no-vacío y recargable (`LoadDoc` round-trip); `NativeDiffProbe` refleja ediciones (JSON no
   trivial tras insertar); `NativeBranchMergeProbe` reporta convergencia; guard de doc no-Loro lanza.
7. **Reconciliar quickstart §US5**: `LoroEngine.NativeVersioning` ya **no** es null; actualizar la nota de
   diferimiento (G1 cerrado).
8. **Backlog**: FU-006 → `closed`.

**Out of scope:**

- **API de versionado nativo completa** (branches con nombre, time-travel, checkout persistente) — los probes son
  demostrativos; una API rica sería un charter aparte, sin demanda hoy.
- **Content-addressing sobre los bytes de los probes**: `VersionId` sigue con `export_state` determinista
  (`all_updates`); los probes NO son deterministas byte a byte (el shallow snapshot lleva metadata de réplica).
- **`INativeVersioning` para yrs**: yrs no tiene versionado nativo → `YrsEngine.NativeVersioning == null` permanente.
- **Test de paridad header↔binding para el shim Loro**: el shim yrs lo tiene, el Loro no; crear el header aquí, el
  test de paridad automatizado es un FU aparte si se quiere (no bloquea).

## Files to modify

<!-- Reconnaissance (#210): leídas todas las rutas. weft-loro-ffi/src/lib.rs (WEFT_ABI_VERSION=1:22, funciones
     doc/text/export, hand_out_buffer, doc_ref, HandleLease pattern); NO existe include/ en weft-loro-ffi (header
     NEW). INativeVersioning.cs (3 firmas). LoroEngine.cs:27 (NativeVersioning => null), LoroDoc.cs (DocHandle +
     HandleLease), NativeMethods.cs / NativeLibraryResolver.cs:10 (ExpectedAbiVersion=1). loro 1.13.6: fork():151,
     fork_at:159, diff:1496, export:1306, state_frontiers:967, ExportMode::shallow_snapshot (doc lib.rs:1280).
     Tests de Loro en tests/Weft.Versioning.Tests/ (LoroVersioningTests, VersioningSuiteBase). quickstart §US5:87-89
     documenta el diferimiento G1. LoroNativeVersioning.cs y el header y el test son NEW. -->

| File | Change |
|---|---|
| `native/weft-loro-ffi/src/lib.rs` | 3 probes (shallow_snapshot, native_diff_probe, native_branch_merge_probe) + `WEFT_ABI_VERSION` 1→2 |
| `native/weft-loro-ffi/include/weft_loro_ffi.h` | New — header C del shim Loro (todas las fns + las 3 nuevas) |
| `native/weft-loro-ffi/tests/mem_asan.rs` | ABI assert 1→2 + test de los 3 probes bajo ASan (reachability + sin fugas + no muta el caller) |
| `src/Weft.Loro/Interop/NativeMethods.cs` | 3 P/Invokes |
| `src/Weft.Loro/Interop/NativeLibraryResolver.cs` | `ExpectedAbiVersion` 1→2 |
| `src/Weft.Loro/LoroDoc.cs` | 3 métodos `internal` (delegan al FFI vía `HandleLease`) |
| `src/Weft.Loro/LoroNativeVersioning.cs` | New — implementa `INativeVersioning` (cast ICrdtDoc→LoroDoc + delega) |
| `src/Weft.Loro/LoroEngine.cs` | `NativeVersioning` => instancia de `LoroNativeVersioning` (era `null`) |
| `tests/Weft.Versioning.Tests/LoroNativeVersioningTests.cs` | New — tests de los 3 probes + guard de doc no-Loro |
| `specs/001-weft-crdt-versioning/quickstart.md` | Reconciliar §US5 (NativeVersioning ya no null; G1 cerrado) |
| `.straymark/follow-ups-backlog.md` | FU-006 → `closed` |
| `.straymark/07-ai-audit/agent-logs/AILOG-*.md` | New, `risk_level: medium` (frontera FFI Loro, ABI bump) |
| `.straymark/07-ai-audit/decisions/AIDEC-*.md` | New — forma/semántica de los 3 probes (JSON + shallow) |

## Verification

### Local checks

```bash
# Shim Loro: compila + símbolos exportados + ABI v2
cd native/weft-loro-ffi && cargo build --release && cargo test
nm -D ../target/release/libweft_loro_ffi.so | grep -E "weft_loro_shallow_snapshot|native_diff_probe|native_branch_merge_probe"

# Suite .NET completa incl. los tests de los probes nativos de Loro
cd ../.. && dotnet test Weft.sln -c Release   # LoroNativeVersioningTests + dual-engine intactos
```

### Production smoke (after deploy)

No aplica — librería sin despliegue. Los auditores externos deben saltar esta sección.

## Risks

- **R1 — la API nativa de Loro difiere de lo asumido** (firma exacta de `ExportMode::shallow_snapshot`, forma de
  `DiffBatch`): severidad **media**. Mitigación: `fork`/`fork_at`/`diff`/`export`/`state_frontiers` confirmados en
  loro 1.13.6 por código; el constructor exacto de `ExportMode::shallow_snapshot` se resuelve en la implementación.
  Si un probe no se expresa limpio, documentar la limitación honesta (es un probe, no un contrato) — no forzar.
- **R2 — los bytes/JSON de los probes NO son deterministas**: severidad **baja**. El shallow snapshot lleva
  metadata de réplica (peer-ids, orden interno), como el `Snapshot` que `export_state` evita a propósito. Los
  probes son **demostrativos**, no content-addressing → NO alimentan `VersionId`. Los tests asertan
  round-trip/reachability, NO byte-determinismo. Documentado en el AIDEC y en el docstring.
- **R3 — el ABI bump v1→v2 del shim Loro rompe consumidores**: severidad **baja**. Bump atómico Rust
  (`WEFT_ABI_VERSION`) + .NET (`ExpectedAbiVersion`) en el mismo PR; exports aditivos; desalineación →
  `NativeLibraryResolver` lanza explícito. Nota: el shim Loro no tiene test de paridad header↔binding (a diferencia
  de yrs); se crea el header, el test automatizado es un FU si se quiere.
- **R4 — el cast `ICrdtDoc → LoroDoc` falla con un doc de otro motor**: severidad **baja**. Mitigación:
  `LoroNativeVersioning` solo es alcanzable vía `LoroEngine.NativeVersioning`; el cast lanza una excepción clara
  (`ArgumentException`) si se le pasa un doc no-Loro. Test cubre el guard.

## Tasks

1. Sync main, branch `charter/10-loro-native-versioning` (**ya creada**). Flip `declared` → `in-progress`.
2. Re-evaluar **Constitution Check**: **P-IV** (capacidad opcional del motor, alcance Loro), **P-I** (catch_unwind
   en las 3 entradas nuevas), **P-II** (buffers vía `hand_out_buffer`/`weft_loro_buf_free`). Sin violaciones.
3. **(1)** FFI: los 3 probes + ABI bump + `cargo test`; verificar símbolos exportados.
4. **(2)** Header `weft_loro_ffi.h` (NEW, todas las fns).
5. **(3)** Binding: NativeMethods + `ExpectedAbiVersion` 2 + métodos internal en `LoroDoc`.
6. **(4,5)** `LoroNativeVersioning.cs` + `LoroEngine.NativeVersioning` no-nulo.
7. **(6)** Tests de los 3 probes + guard. **(7)** Reconciliar quickstart §US5.
8. **(8)** Backlog: FU-006 → `closed` + `recount`.
9. **AILOG** (`risk_level: medium`, `review_required: true`) + **AIDEC** (forma de los probes). Verificación local.
10. `straymark charter drift CHARTER-10` (posible FP del parser #354 en `.h`/`.rs`/`.cs`). Commit + push + PR;
    CI verde.

## Charter Closure

**No cierra hito** (capacidad opcional; sin auditoría externa multi-modelo). Al cerrar:

1. **Atomic update (format v4)**: si el drift reveló divergencias (p. ej. un probe no expresable → limitación
   documentada), edita `## Scope`/`## Files to modify` + `## Closing notes` en el mismo PR.
2. `straymark charter drift CHARTER-10 --range origin/main..HEAD` → limpio o documentado (incl. FP del parser #354).
3. `straymark charter close CHARTER-10` (telemetría).
4. **No borrar** este archivo.
5. Backlog: **FU-006 `closed`**. Restan open: FU-010 (durabilidad relay), FU-015 (adopción R6 bump), FU-016 (Loro
   client-id cross-engine), FU-017 (test paridad header↔binding Loro). Para cerrar **M3**: publish real
   operador-gated + Polish (T061–T063).

## Closing notes

Drift (`origin/main..HEAD`) reportó 1 archivo modificado no declarado; reconciliado atómicamente (format v4):

- `native/weft-loro-ffi/tests/mem_asan.rs` — **añadido a la tabla**. Consecuencia del ABI bump (assert
  `weft_loro_abi_version() == 1` → `2`) + un test de los 3 probes bajo ASan (reachability + sin fugas + que el
  branch/merge NO muta el doc del caller). Ref: AILOG-2026-07-15-002 §Verification.
