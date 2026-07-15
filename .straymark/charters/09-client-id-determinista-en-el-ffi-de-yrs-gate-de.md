---
charter_id: CHARTER-09-client-id-determinista-en-el-ffi-de-yrs-gate-de
status: in-progress
effort_estimate: M
trigger: "FU-012 (backlog, charter-triggered): promover el determinismo cross-implementación (harness determinism-yjs, T058/CHARTER-07) de informativo a gate con aserción. Disparado por decisión del operador (2026-07-15) de cerrar FU-012 tras CHARTER-08; antes del primer bump de motor con impacto de encoding (R16). Alcance yrs-only decidido (Loro diferido)."
originating_spec: specs/001-weft-crdt-versioning/spec.md
work_verb: implement
design_provenance: new
---

# Charter: Client-id determinista en el FFI de yrs + gate de paridad cross-impl (determinism-yjs)

> **Status (mirrored from frontmatter — source of truth is above):** declared. Effort: M.
>
> **Origin:** Follow-up **FU-012**, sobre la spec 001 (constitución **P-III** determinismo / **P-IV** abstracción
> de motor viva). Cierra el gating del harness `determinism-yjs` (T058) sobre client-ids deterministas, para el
> motor **yrs**. Alcance **yrs-only** decidido por el operador; la promoción cross-engine (Loro) se difiere a un FU.

## Context

El harness `tests/determinism-yjs/` (T058, CHARTER-07) aplica un **corpus compartido** con **Yjs JS** y emite el
SHA-256 del export v1, para verificar que el determinismo de Weft es **por formato** (el encoding v1 de Yjs/yrs),
no un accidente de esta versión de `yrs` — la garantía que distingue "content-addressing estable" de "estable
hasta el próximo bump" (research **R13**, constitución **P-III**; ver **R16**). Hoy es **no-bloqueante** porque la
paridad byte-idéntica con yrs está **gated en client-ids deterministas**: `ICrdtEngine.CreateDoc()` no toma
parámetro y el shim `weft-yrs-ffi` no expone fijar el `client_id`, así que yrs asigna uno no controlable y su
export no es reproducible cross-implementación.

Este Charter cierra ese gate **para yrs**: expone la siembra de `client_id` en el FFI + binding, y promueve la
paridad a una **aserción per-PR barata** (test .NET en el job `test` existente, costo marginal ~0, bloqueante de
facto) contra un **hash golden de Yjs comprometido**. **Nota de encoding**: yrs **0.26.0** pasó los client IDs a
**53 bits** (antes 64) — la API debe acotar `client_id < 2^53`. **Alcance yrs-only** (el gate es yrs↔Yjs; Loro es
otro formato, no participa): para no tensar **P-IV**, el método de siembra va como capacidad **concreta de
`YrsEngine`** (no en `ICrdtEngine`), y la promoción a capacidad cross-engine (Loro vía `set_peer_id`) se difiere a
un FU nuevo.

## Scope

**In scope:**

1. **FFI yrs — siembra de client_id (ABI bump v1→v2)**: `weft_doc_new_with_client_id(uint64_t client_id,
   WeftDoc** out_doc)` en `weft-yrs-ffi` — crea el `Doc` con `Options { client_id, offset_kind: Utf16, ..default }`.
   **Guarda** `client_id < 2^53` (encoding de 53 bits de yrs 0.26+) → `WEFT_ERR_OUT_OF_BOUNDS` si no. Incrementa
   `WEFT_ABI_VERSION` **1→2** en `lib.rs` + declara la fn nueva en `include/weft_ffi.h` (comentario de ABI).
2. **Binding .NET — siembra yrs-específica**: `weft_doc_new_with_client_id` en `NativeMethods.cs` (LibraryImport);
   `YrsDoc.Create(ulong clientId)` (valida/propaga); `YrsEngine.CreateDoc(ulong clientId)` **método concreto, NO en
   `ICrdtEngine`** (yrs-específico; el test usa `YrsEngine` directo). Sube `ExpectedAbiVersion` **1→2** en
   `NativeLibraryResolver.cs`.
3. **Golden de Yjs comprometido**: correr `apply.mjs` para producir el SHA-256 de Yjs del corpus ASCII y del
   unicode; comprometer en `tests/determinism-yjs/golden.json` (`{ "ascii": "<hash>", "unicode": "<hash>" }`).
4. **Aserción de paridad per-PR** en `Weft.Determinism.Tests/DeterminismTests.cs`: aplica `corpus.json` con yrs
   (réplicas vía `YrsEngine.CreateDoc(clientId)` con los `clientIds` fijos, `InsertText`/`DeleteText` por índice,
   `syncPasses`), toma `ExportState()` de la réplica 0, SHA-256, y **asierta** `== golden.ascii`. Ídem para el
   corpus unicode `== golden.unicode`. Corre en el job `test` existente (bloqueante de facto, per-PR).
5. **Variante unicode del corpus (UTF-16, R6)**: `tests/determinism-yjs/corpus-unicode.json` (texto no-ASCII que
   ejercita los índices UTF-16) + `apply.mjs` acepta el corpus por argumento/env y emite su hash.
6. **Promover el job de `release.yml`**: el job `determinism-yjs` emite los hashes (ascii + unicode) y **compara
   contra `golden.json`** (self-check de drift de Yjs); permanece `continue-on-error` (la aserción bloqueante real
   es el test .NET per-PR). Actualiza `tests/determinism-yjs/README.md §Estado` (promovido a aserción per-PR yrs).
7. **Backlog**: FU-012 → `closed`; registrar **FU-nuevo** (promover la siembra de client_id a capacidad
   cross-engine — `CreateDoc(clientId)` en `ICrdtEngine` + `weft_loro_doc_new_with_peer_id` vía `set_peer_id`).

**Out of scope:**

- **Loro `CreateDoc(peer_id)` / promoción cross-engine** de la capacidad de siembra → **FU-nuevo** (parte 7).
- **Hacer bloqueante el job Node de `release.yml`**: la aserción bloqueante es el test .NET per-PR; el job Node
  queda informativo con self-check de golden (decisión del operador: presupuesto de minutos CI).
- **Paridad Loro↔Yjs**: formatos distintos; fuera del gate cross-impl.
- Cambiar `ICrdtEngine.CreateDoc()` (sin parámetro) — se conserva; la siembra es capacidad concreta de `YrsEngine`.

## Files to modify

<!-- Reconnaissance (#210): todas las rutas leídas antes de declararlas. weft_doc_new + new_doc() (Options,
     OffsetKind::Utf16) en lib.rs:35,131; weft_ffi.h declara weft_doc_new + weft_abi_version; ExpectedAbiVersion=1
     en NativeLibraryResolver.cs:14; YrsDoc.Create()/YrsEngine.CreateDoc() confirmados; ICrdtDoc expone
     InsertText/DeleteText/GetText/ExportState; corpus.json (clientIds/ops/syncPasses) + apply.mjs (clientID fijo,
     encodeStateAsUpdate, sha256) + README + job en release.yml:208 leídos. golden.json y corpus-unicode.json son
     NEW. La API tocada (weft_doc_new_with_client_id) es aditiva: no hay consumidores del contrato viejo que
     romper; el ABI bump se aplica atómicamente en ambos lados. -->

| File | Change |
|---|---|
| `native/weft-yrs-ffi/src/lib.rs` | `weft_doc_new_with_client_id` (Options.client_id + guard < 2^53) + `WEFT_ABI_VERSION` 1→2 |
| `native/weft-yrs-ffi/include/weft_ffi.h` | Declara `weft_doc_new_with_client_id`; nota de ABI bump |
| `native/weft-yrs-ffi/tests/mem_asan.rs` | ABI assert 1→2 + test `seed_client_id_is_deterministic_and_bounded` (round-trip + guard) |
| `tests/determinism-yjs/package.json` | Script `test` corre ambos corpus (ascii + unicode) |
| `src/Weft.Core/Yrs/NativeMethods.cs` | P/Invoke `weft_doc_new_with_client_id` |
| `src/Weft.Core/Yrs/YrsDoc.cs` | `Create(ulong clientId)` |
| `src/Weft.Core/Yrs/YrsEngine.cs` | `CreateDoc(ulong clientId)` (concreto, yrs-específico) |
| `src/Weft.Core/Yrs/NativeLibraryResolver.cs` | `ExpectedAbiVersion` 1→2 |
| `tests/Weft.Determinism.Tests/DeterminismTests.cs` | Test de paridad cross-impl (asierta yrs SHA-256 == golden Yjs), ASCII + unicode |
| `tests/determinism-yjs/golden.json` | New — hashes golden de Yjs (`ascii`, `unicode`) |
| `tests/determinism-yjs/corpus-unicode.json` | New — corpus con texto no-ASCII (índices UTF-16, R6) |
| `tests/determinism-yjs/apply.mjs` | Acepta corpus por arg/env; emite hash + compara contra golden.json |
| `tests/determinism-yjs/README.md` | Estado → aserción per-PR (yrs); uso del corpus unicode |
| `.github/workflows/release.yml` | Job `determinism-yjs`: emite ascii+unicode, self-check contra golden |
| `.straymark/follow-ups-backlog.md` | FU-012 → `closed`; registrar FU-nuevo (Loro peer_id cross-engine) |
| `.straymark/07-ai-audit/agent-logs/AILOG-*.md` | New, `risk_level: medium` (frontera FFI, ABI bump, gate de determinismo) |
| `.straymark/07-ai-audit/decisions/AIDEC-*.md` | New — placement de la siembra (concreto en YrsEngine vs ICrdtEngine) + forma del golden |

## Verification

### Local checks

```bash
# Shim yrs: compila + tests (incl. el guard de client_id y el round-trip de la siembra)
cd native/weft-yrs-ffi && cargo test && cargo build --release && cd ../..

# El hash de Yjs del corpus (fuente del golden); ambos corpus
cd tests/determinism-yjs && npm install
node apply.mjs                          # emite hash ascii
node apply.mjs --corpus corpus-unicode.json   # emite hash unicode
cd ../..

# Suite .NET completa incl. la aserción de paridad cross-impl per-PR (bloqueante de facto)
dotnet test Weft.sln -c Release         # Weft.Determinism.Tests asierta yrs SHA-256 == golden Yjs (ascii+unicode)
```

### Production smoke (after deploy)

No aplica — librería sin despliegue. Los auditores externos deben saltar esta sección.

## Risks

- **R1 — el export de yrs NO es byte-idéntico al de Yjs**: severidad **media-alta** (riesgo central). Si diverge,
  la aserción no puede ser bloqueante. Mitigación: producir **ambos hashes temprano** en la implementación; si
  divergen, investigar la causa de encoding (orden de bloques, delete sets, offset). Si es irreconciliable, el gate
  **permanece informativo** (no se promueve a bloqueante), se documenta como finding + FU, y NO se falsifica la
  paridad. La promoción a "bloqueante" de este Charter es **contingente** a que la paridad realmente se cumpla —
  verificada durante la ejecución, no asumida.
- **R2 — client_id ≥ 2^53 rompe el encoding de 53 bits de yrs**: severidad **media**. Mitigación: el FFI acota
  `client_id < 2^53` → `WEFT_ERR_OUT_OF_BOUNDS`; test cubre el borde (2^53-1 ok, 2^53 rechazado). El corpus usa
  1/2/3 (seguros). Si el guard falla, es un error de decode limpio, no UB.
- **R3 — el ABI bump v1→v2 rompe consumidores**: severidad **baja**. Un shim viejo (v1) fallaría el check de
  versión. Mitigación: bump atómico de `WEFT_ABI_VERSION` (Rust) y `ExpectedAbiVersion` (.NET) en el **mismo PR**;
  el nuevo export es **aditivo** (no cambia `weft_doc_new`); CI construye el shim fresco. Si desalinea, el error es
  explícito (`NativeLibraryResolver` lanza), no un crash.
- **R4 — golden frágil ante drift de Yjs**: severidad **baja**. Si Yjs bumpea y cambia el encoding, el golden
  comprometido queda stale. Mitigación: el job Node de `release.yml` recomputa el hash de Yjs y lo compara contra
  `golden.json` → caza el drift; la regeneración del golden es un paso documentado en el README.
- **R5 — la variante unicode desalinea índices UTF-16**: severidad **baja**. Mitigación: `new_doc()` ya fija
  `OffsetKind::Utf16`; el corpus unicode ejercita exactamente esto (si diverge, revela un bug de offset como el R6
  de CHARTER-02). La aserción unicode es parte del gate.

## Tasks

1. Sync main, branch `charter/09-determinism-client-id` (**ya creada**). Flip `declared` → `in-progress` al empezar.
2. Re-evaluar **Constitution Check**: **P-III** (determinismo por formato), **P-IV** (abstracción viva — siembra
   como capacidad concreta de yrs, no en la interfaz; Loro diferido). Sin violaciones esperadas.
3. **(1)** FFI: `weft_doc_new_with_client_id` + guard 2^53 + ABI bump v1→v2 + header. `cargo test` con el round-trip
   de la siembra y el borde del guard.
4. **(2)** Binding: NativeMethods + `YrsDoc.Create(ulong)` + `YrsEngine.CreateDoc(ulong)` + `ExpectedAbiVersion` 2.
5. **(5)** Corpus unicode + `apply.mjs` parametrizado. **(3)** Correr `apply.mjs` (ambos corpus) → comprometer
   `golden.json`. **VERIFICAR R1**: producir el hash de yrs (vía el test .NET) y confirmar que iguala el golden de
   Yjs ANTES de fijar el gate como bloqueante.
6. **(4)** Test de paridad en `Weft.Determinism.Tests` (ASCII + unicode), asertivo.
7. **(6)** Promover el job de `release.yml` (emite ambos + self-check golden) + actualizar README.
8. **(7)** Backlog: FU-012 → `closed` + `recount`; registrar el FU-nuevo (Loro peer_id cross-engine).
9. **AILOG** (`risk_level: medium`, `review_required: true`) + **AIDEC** (placement de la siembra + forma del
   golden + resultado de R1). Verificación local completa.
10. `straymark charter drift CHARTER-09` (posible FP del parser #354 en `.json`/`.mjs`/`.h`/`.cs` — documentar).
    Commit + push + PR contra `main`; CI verde (incl. la nueva aserción per-PR).

## Charter Closure

**No cierra hito** (FU-012 es promoción de un gate informativo; no requiere auditoría externa multi-modelo). Al cerrar:

1. **Atomic update (format v4)**: si el drift reveló divergencias (p. ej. R1 forzó dejar el gate informativo en vez
   de bloqueante), edita `## Scope`/`## Files to modify` + `## Closing notes` en el **mismo PR**.
2. `straymark charter drift CHARTER-09 --range origin/main..HEAD` → limpio o documentado (incl. FP del parser #354).
3. `straymark charter close CHARTER-09` (telemetría; registrar el resultado de R1 — paridad cumplida o no).
4. **No borrar** este archivo.
5. Backlog: **FU-012 `closed`**, **FU-016 `open`** (Loro peer_id). Siguiente: **CHARTER-10 (FU-006, Loro nativo)**.

## Closing notes

Drift (`origin/main..HEAD`) reportó 3 archivos modificados no declarados; reconciliados atómicamente aquí
(format v4), ninguno cambia el alcance sustantivo:

- `native/weft-yrs-ffi/tests/mem_asan.rs` — **añadido a la tabla**. Consecuencia del ABI bump (assert
  `weft_abi_version() == 1` → `2`) + un test Rust del round-trip de la siembra y el borde del guard 2^53.
  Ref: AILOG-2026-07-15-001 §Verification.
- `tests/determinism-yjs/package.json` — **añadido a la tabla**. El script `test` ahora corre ambos corpus
  (ascii + unicode); no se anticipó como archivo aparte al declarar.
- `tests/determinism-yjs/apply.mjs` — **falso positivo del parser #354**: SÍ está declarado en §Files to modify,
  pero el parser de drift no matchea la extensión `.mjs` (mismo bug que `.csproj`/`.sln`; corroborado aquí para
  `.mjs`). No es expansión de alcance.
