# Tasks: Weft — Colaboración CRDT en tiempo real y versionado content-addressed para .NET

**Input**: Design documents from `/specs/001-weft-crdt-versioning/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/ (core-api, ffi-abi, versioning-api, server-api), quickstart.md

**Tests**: INCLUIDOS — la spec los exige como gates (FR-023) y la constitución los hace vinculantes (P-II memoria, P-III determinismo, P-IV dual-engine). Convención: los tests de contrato/propiedades se escriben con (o antes de) su implementación y DEBEN fallar si el contrato se rompe.

**Organization**: Tareas agrupadas por user story, cada fase independientemente testeable según su "Independent Test". **Orden de ejecución ≠ prioridad de negocio**: US5 (P5) se ejecuta inmediatamente después de US1 porque la constitución (P-IV) exige el gate dual-engine activo desde el cierre de M0 — la etiqueta `[US5]` conserva la prioridad de la spec.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: puede correr en paralelo (archivos distintos, sin dependencia de tareas incompletas)
- **[Story]**: US1..US5 — solo en fases de user story
- Rutas exactas según la estructura de plan.md

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: esqueleto de repo — solución .NET, workspace Rust, tooling

- [X] T001 Create solution and directory skeleton per plan.md: `Weft.sln`, `src/`, `tests/`, `native/`, `samples/`, project stubs `src/Weft.Core/Weft.Core.csproj`, `src/Weft.Versioning/Weft.Versioning.csproj`, `tests/Weft.Core.Tests/`, `tests/Weft.Versioning.Tests/` — CHARTER-01
- [X] T002 Create Rust workspace `native/Cargo.toml` + `native/rust-toolchain.toml` (stable pinned) with member crate `native/weft-yrs-ffi/` (cdylib, `yrs = "=0.27.2"`, `Cargo.lock` versionado) — CHARTER-01
- [X] T003 [P] Create `Directory.Build.props` (net10.0, C# 13, `Nullable=enable`, analyzers, license Apache-2.0, SourceLink) and `.editorconfig` + `rustfmt.toml` — CHARTER-01
- [X] T004 [P] Create CI skeleton `.github/workflows/ci.yml` (jobs vacíos nombrados: test-linux/win/mac, asan, determinism, dual-engine, fuzz, pack-smoke — se llenan por fase) — CHARTER-01
- [X] T005 [P] Add `.gitignore` entries for `target/`, `bin/`, `obj/`, `artifacts/` and verify `LICENSE` (Apache-2.0) at root — CHARTER-01

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: shim FFI `weft-yrs-ffi` completo + binding seguro `Weft.Core` — TODAS las stories dependen de esto

**⚠️ CRITICAL**: ninguna user story arranca sin esta fase verde (incluidos los gates P-I/P-II en CI)

- [X] T006 Implement shim scaffolding in `native/weft-yrs-ffi/src/lib.rs`: error codes (`WEFT_OK`..`WEFT_ERR_PANIC` per contracts/ffi-abi.md), `catch_unwind` wrapper helper, `weft_abi_version` — CHARTER-01
- [X] T007 Implement doc lifecycle FFI in `native/weft-yrs-ffi/src/lib.rs`: `weft_doc_new`, `weft_doc_load`, `weft_doc_free`, `weft_buf_free` (Box<[u8]> ownership, GC del motor siempre activo) — CHARTER-01
- [X] T008 Implement text FFI in `native/weft-yrs-ffi/src/lib.rs`: `weft_text_insert`, `weft_text_delete`, `weft_text_read` (UTF-8 ptr+len, `OUT_OF_BOUNDS`/`UTF8` errors) — CHARTER-01
- [X] T009 Implement state/sync FFI in `native/weft-yrs-ffi/src/lib.rs`: `weft_doc_export_state` (determinista), `weft_doc_state_vector`, `weft_doc_export_since`, `weft_doc_apply_update` — CHARTER-01
- [X] T010 Write ownership-contract header `native/weft-yrs-ffi/include/weft_ffi.h` (las 12 funciones + test hook + reglas transversales de contracts/ffi-abi.md) — CHARTER-01
- [X] T011 [P] Rust test suite `native/weft-yrs-ffi/tests/`: unit + `mem_asan.rs` (≥2000 iteraciones por función incl. rutas de error; gate 0 fugas/0 double-free) — CHARTER-01
- [X] T012 [P] cargo-fuzz targets `native/weft-yrs-ffi/fuzz/fuzz_targets/`: `doc_load.rs`, `apply_update.rs` (bytes arbitrarios → solo códigos de error, nunca panic-through) — CHARTER-01
- [X] T013 [P] Define abstractions per contracts/core-api.md in `src/Weft.Core/Abstractions/`: `ICrdtEngine.cs`, `ICrdtDoc.cs`, `INativeVersioning.cs` — CHARTER-01
- [X] T014 [P] Implement exception hierarchy in `src/Weft.Core/WeftException.cs`: `WeftException`, `CorruptUpdateException`, `WeftEngineException` + `WeftErrorCode` enum — CHARTER-01
- [X] T015 Implement `src/Weft.Core/Yrs/DocHandle.cs` (SafeHandleZeroOrMinusOneIsInvalid) + `HandleLease` helper (DangerousAddRef/Release, research R2) — CHARTER-01
- [X] T016 Implement `src/Weft.Core/Yrs/NativeMethods.cs` (`[LibraryImport]` de las 12 funciones) + `src/Weft.Core/Yrs/NativeLibraryResolver.cs` (resolución por RID + check `weft_abi_version`) — CHARTER-01
- [X] T017 Implement `src/Weft.Core/Yrs/YrsEngine.cs` + `YrsDoc.cs`: `ICrdtEngine`/`ICrdtDoc` completos (índices `int` validados, `TakeOwnedBuffer`, códigos→excepciones, `ObjectDisposedException`) — CHARTER-01
- [X] T018 [P] Unit tests `tests/Weft.Core.Tests/YrsDocTests.cs`: round-trip export/load byte-idéntico, errores (blob corrupto→`CorruptUpdateException`, índices fuera de rango), dispose semantics, buffers vacíos — CHARTER-01
- [X] T019 [P] Property tests `tests/Weft.Core.Tests/ConvergenceTests.cs` (CsCheck): secuencias aleatorias de ops en N réplicas + intercambio de updates/deltas → convergencia byte-idéntica (SC-001) — CHARTER-01
- [X] T020 [P] Panic-injection coverage (SC-009): añadir `weft_test_panic` tras feature de Cargo `test-hooks` en `native/weft-yrs-ffi/src/lib.rs` (+ declaración test-only en `include/weft_ffi.h`) y test `tests/Weft.Core.Tests/PanicSafetyTests.cs`: la llamada produce `WeftEngineException(ErrorCode.Panic)`, el proceso sigue estable y la ruta corre bajo ASan sin fugas — CHARTER-01
- [X] T021 Wire CI foundational gates in `.github/workflows/ci.yml`: build shim + `dotnet test` (linux/win/mac), job `asan` (nightly, x86_64-unknown-linux-gnu), job `fuzz` smoke (60 s por target en PR); los jobs asan/fuzz compilan el shim con `--features test-hooks` (pack-smoke en US4 verifica la ausencia del símbolo en release) — CHARTER-01

**Checkpoint**: binding seguro funcionando con gates P-I/P-II activos — las user stories pueden arrancar

---

## Phase 3: User Story 1 — Editar y versionar documentos desde .NET (Priority: P1) 🎯 MVP

**Goal**: publicar/checkout/diff/branch/merge content-addressed sobre `IBlobStore` (hito M0)

**Independent Test** (spec US1): consola con solo la librería: crear→editar→publicar v1→editar→publicar v2→diff→branch+merge→compactación implícita; réplicas convergidas publican el mismo hash

- [X] T022 [P] [US1] Implement `VersionId` struct in `src/Weft.Versioning/VersionId.cs` (SHA-256, hex lowercase, Parse/TryParse/AsSpan, igualdad por valor) — CHARTER-02
- [X] T023 [P] [US1] Define `IBlobStore` + `InMemoryBlobStore` in `src/Weft.Versioning/Blobs/IBlobStore.cs`, `InMemoryBlobStore.cs` (put idempotente, thread-safe) — CHARTER-02
- [X] T024 [US1] Implement `FileSystemBlobStore` in `src/Weft.Versioning/Blobs/FileSystemBlobStore.cs` (sharding `aa/bb/hash`, escritura atómica temp+rename) — CHARTER-02
- [X] T025 [P] [US1] Implement word-level LCS diff in `src/Weft.Versioning/TextDiff.cs` (`TextDiff`, `TextDiffSegment`, `DiffOp` per contracts/versioning-api.md) — CHARTER-02
- [X] T026 [US1] Implement `VersionStore` in `src/Weft.Versioning/VersionStore.cs`: `PublishAsync`/`CheckoutAsync` (verifica integridad → `BlobIntegrityException`)/`DiffAsync`/`BranchAsync`/`Merge`/`MergeAsync` — CHARTER-02
- [X] T027 [P] [US1] Create engine-parametrized versioning suite `tests/Weft.Versioning.Tests/VersioningSuiteBase.cs` + `YrsVersioningTests.cs`: las **7** postcondiciones de contracts/versioning-api.md (dedup, round-trip, mismo VersionId cross-réplica, diff, conmutatividad de merge, compactación acotada FR-012) — CHARTER-02
- [X] T028 [P] [US1] Unit tests `tests/Weft.Versioning.Tests/TextDiffTests.cs` (Equal/Insert/Delete, determinismo del diff, casos límite: campo vacío, sin cambios) — CHARTER-02
- [X] T029 [P] [US1] Create determinism gate `tests/Weft.Determinism.Tests/DeterminismTests.cs`: corpus de secuencias con client-ids fijos → export/hash idénticos entre réplicas y corridas (P-III; base del job cross-RID) — CHARTER-02
- [X] T030 [US1] Create runnable sample `samples/Weft.Sample.Versioning/Program.cs` ejecutando el user journey completo de US1 (salida legible con hashes y diff) — CHARTER-02
- [X] T031 [US1] Wire CI jobs in `.github/workflows/ci.yml`: `determinism` (bloqueante) + versioning tests en la matriz de plataformas — CHARTER-02

**Checkpoint**: capa de versionado completa sobre yrs — falta la evidencia dual-engine para cerrar M0 (siguiente fase)

---

## Phase 4: User Story 5 — Sustituir el motor CRDT sin reescribir el versionado (Priority: P5)

**Goal**: adaptador Loro compilable y ejercitado — abstracción viva (P-IV). Se ejecuta aquí (no al final) porque la constitución exige la evidencia dual-engine desde el cierre de M0; coincide con quickstart.md §Criterio de cierre ("M0: … gates memoria/determinismo/dual-engine activos")

**Independent Test** (spec US5): la MISMA suite de versionado verde sobre yrs y Loro; probes nativos responden en Loro y su ausencia en yrs no rompe nada

- [X] T032 [P] [US5] Create crate `native/weft-loro-ffi/` (`loro = "=1.13.6"`): ABI núcleo `weft_loro_*` + tests/mem_asan + fuzz — CHARTER-02. **DIFERIDO a follow-up** (auditoría CHARTER-02, G1): probes `native_diff_probe`/`native_branch_probe`/`shallow_snapshot` + header `include/weft_loro_ffi.h` — capacidad opcional `INativeVersioning`; ningún gate de M0 depende de ella.
- [X] T033 [US5] Implement `src/Weft.Loro/LoroEngine.cs` + `LoroDoc.cs` (`ICrdtEngine` per contracts/core-api.md) — CHARTER-02. **DIFERIDO a follow-up** (auditoría CHARTER-02, G1): `LoroNativeVersioning.cs` (`INativeVersioning`); `LoroEngine.NativeVersioning = null` en M0.
- [X] T034 [US5] Activate dual-engine theory in `tests/Weft.Versioning.Tests/LoroVersioningTests.cs` (hereda `VersioningSuiteBase` de T027) + promote CI job `dual-engine` a gate bloqueante (SC-008) — CHARTER-02
- [X] T035 [P] [US5] Extend `asan` CI job matrix to `weft-loro-ffi` in `.github/workflows/ci.yml` (P-II cubre ambos shims) — CHARTER-02

**Checkpoint**: **M0 se declara cerrado aquí** (US1 + US5): API mínima estable con gates de memoria, determinismo **y dual-engine** activos — evidencia completa para la revisión de hito de la constitución (P-IV)

---

## Phase 5: User Story 2 — Operar muchos documentos concurrentes sin corrupción (Priority: P2)

**Goal**: actor/canal por documento, pooling y desalojo (hito M1)

**Independent Test** (spec US2): prueba de carga con cientos de docs y tareas concurrentes → estados consistentes, memoria acotada, cero recursos sin liberar

- [ ] T036 [P] [US2] Define `DocumentBrokerOptions` in `src/Weft.Core/Concurrency/DocumentBrokerOptions.cs` (IdleEviction, MaxActiveDocuments, OnEvicting)
- [ ] T037 [US2] Implement `DocumentActor` (internal) in `src/Weft.Core/Concurrency/DocumentActor.cs`: Channel unbounded single-reader, estados Active/Idle/Evicted/Faulted, drenado en desalojo, doc liberado exactamente una vez
- [ ] T038 [US2] Implement `DocumentBroker` in `src/Weft.Core/Concurrency/DocumentBroker.cs`: registro docId→actor, reutilización, desalojo por inactividad + LRU al superar máximo, `DisposeAsync` drena todo
- [ ] T039 [US2] Implement `DocumentSession` in `src/Weft.Core/Concurrency/DocumentSession.cs`: espejo async de `ICrdtDoc`, `ExecuteAsync` (turno atómico), evento `UpdateApplied`, `IAsyncDisposable`
- [ ] T040 [P] [US2] Concurrency tests `tests/Weft.Core.Tests/DocumentBrokerTests.cs`: serialización (nunca 2 ops simultáneas del mismo doc), FIFO por sesión, eviction→OnEvicting→reopen con loader, actor Faulted propaga excepción causal, dispose semantics
- [ ] T041 [P] [US2] Load test harness `tests/Weft.LoadTest/Program.cs`: cientos de docs × tareas concurrentes sostenidas → consistencia final + memoria acotada (medición GC/working set; SC-006)
- [ ] T042 [US2] Add CI nightly job `load-test` in `.github/workflows/ci.yml` (no bloqueante en PR, bloqueante para cierre de M1)

**Checkpoint**: M1 — concurrencia a escala validada

---

## Phase 6: User Story 3 — Colaboración en tiempo real entre clientes de editor (Priority: P3)

**Goal**: relay WebSocket y-sync + awareness + persistencia + publish + authz (hito M2)

**Independent Test** (spec US3): dos clientes simulados/Tiptap convergen en vivo; reconexión solo delta; Deny/ReadOnly efectivos; publish produce hash citable; restart recupera estado

- [ ] T043 [P] [US3] Implement lib0 varint + y-sync framing in `src/Weft.Server/Protocol/Lib0Encoding.cs`, `SyncProtocol.cs` (SyncStep1/2, Update, Awareness per contracts/server-api.md)
- [ ] T044 [P] [US3] Define auth hook in `src/Weft.Server/Auth/IWeftAuthorizer.cs` + `WeftAccess` enum (Deny/ReadOnly/ReadWrite)
- [ ] T045 [P] [US3] Define `IDocumentStore` + `InMemoryDocumentStore` in `src/Weft.Server/Persistence/IDocumentStore.cs`, `InMemoryDocumentStore.cs` (Load/AppendUpdate/SaveSnapshot)
- [ ] T046 [US3] Implement `FileSystemDocumentStore` in `src/Weft.Server/Persistence/FileSystemDocumentStore.cs` (snapshot + updates append, compaction al guardar snapshot)
- [ ] T047 [US3] Implement connection handler `src/Weft.Server/WeftConnection.cs`: handshake (authz→403/upgrade), sync bidireccional incremental, relay de updates vía DocumentBroker + persistencia, awareness broadcast + retirada al cerrar, ReadOnly→close 1008, malformed→close 1002
- [ ] T048 [US3] Implement DI + endpoint in `src/Weft.Server/WeftServerExtensions.cs`: `AddWeftServer(options)` (falla al arrancar sin `IWeftAuthorizer`), `MapWeft(path)` con `{docId}`
- [ ] T049 [US3] Implement `IWeftServer` service in `src/Weft.Server/WeftServer.cs`: `PublishAsync` (VersionStore dentro del turno del actor — mismo VersionId que local), `GetConnectionCountAsync`, `DisconnectAllAsync`
- [ ] T050 [P] [US3] Shared `IDocumentStore` contract suite `tests/Weft.Server.Tests/DocumentStoreContractSuite.cs` (corre contra InMemory y FileSystem; luego EFCore/Redis)
- [ ] T051 [P] [US3] Server integration tests `tests/Weft.Server.Tests/RelayTests.cs`: 2 clientes simulados (convergencia <1 s, delta en reconexión con bytes medidos, Deny sin bytes de contenido, ReadOnly→1008, awareness, restart-recovery, paridad de VersionId con publish local)
- [ ] T052 [US3] Create samples `samples/Weft.Sample.Server/Program.cs` (relay + FileSystemDocumentStore + authorizer demo) + `samples/tiptap-client/` (Tiptap + y-prosemirror + y-websocket) y ejecutar la validación manual de quickstart.md §US3
- [ ] T053 [P] [US3] EF Core adapter package `src/Weft.Server.Persistence.EFCore/EFCoreDocumentStore.cs` (+ pasa la contract suite)
- [ ] T054 [P] [US3] Redis adapter package `src/Weft.Server.Persistence.Redis/RedisDocumentStore.cs` (+ pasa la contract suite)

**Checkpoint**: M2 — colaboración real vía servidor .NET con clientes Yjs estándar

---

## Phase 7: User Story 4 — Instalar y adoptar el componente multiplataforma (Priority: P4)

**Goal**: NuGet nativo multi-RID + gates completos + release OSS (hito M3). **Requiere el gate dual-engine activo (T034) antes del release** — con el orden de fases actual ya lo está

**Independent Test** (spec US4): máquina limpia por RID: instalar paquete → hello Weft verde sin pasos manuales

- [ ] T055 [US4] NuGet packaging of native binaries in `src/Weft.Core/Weft.Core.csproj` (+`buildTransitive/` targets si aplica): layout `runtimes/{linux-x64,linux-arm64,win-x64,osx-arm64}/native/`, pack de los paquetes (`Weft.Core`, `Weft.Versioning`, `Weft.Server`, `Weft.Loro`, adaptadores)
- [ ] T056 [US4] Cross-compile matrix in `.github/workflows/release.yml`: cargo-zigbuild (linux x64/arm64), runners nativos (win-x64, osx-arm64), artefactos → pack (research R12)
- [ ] T057 [P] [US4] `pack-smoke` CI matrix: instalar paquete desde artifacts y correr hello-Weft en linux-x64, win-x64, osx-arm64 + linux-arm64 (QEMU/runner arm) — SC-007; verifica además que el símbolo `weft_test_panic` NO está exportado en los binarios empaquetados (test-hooks fuera de release)
- [ ] T058 [P] [US4] Cross-implementation determinism job `tests/determinism-yjs/` (Node + Yjs JS aplica el corpus compartido y compara blobs/hashes; job no-bloqueante, promovible — research R13)
- [ ] T059 [P] [US4] Public docs: `README.md` quickstart de consumo (install→edit→publish→server), `docs/api/` overview por paquete, `CONTRIBUTING.md` (incl. protocolo de bump del motor, research R16), `GOVERNANCE.md`
- [ ] T060 [US4] Release pipeline in `.github/workflows/release.yml`: versión SemVer, symbols+SourceLink, publish a NuGet.org, tag + GitHub Release con notas

**Checkpoint**: M3 — paquete instalable multiplataforma, repo público listo (con los 6 gates de la constitución activos)

---

## Phase 8: Polish & Cross-Cutting Concerns

- [ ] T061 [P] Architecture doc `docs/architecture.md` (módulos, frontera FFI, flujo de sync, decisiones→research.md) + doc público del contrato de ownership
- [ ] T062 Delta-size benchmark in `tests/Weft.Core.Tests/DeltaSizeBenchmark.cs`: medir escenario de referencia y asertar reducción ≥90 % vs estado completo (SC-004; referencia 523 B→29 B)
- [ ] T063 Full quickstart validation pass (quickstart.md US1–US5 + gates) y actualizar `specs/001-weft-crdt-versioning/checklists/requirements.md` con evidencia de cierre

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (P1)** → nada
- **Foundational (P2)** → Setup. **BLOQUEA todas las stories** (shim + binding + gates P-I/P-II)
- **US1 (P3)** → Foundational. No depende de otras stories
- **US5 (P4 de ejecución)** → Foundational + T027 (suite parametrizada de US1). Cierra M0 junto con US1
- **US2 (P5 de ejecución)** → Foundational. Independiente de US1/US5; T039 (`DocumentSession`) es prerequisito de T047 (relay de US3)
- **US3 (P6 de ejecución)** → Foundational + US2 (broker) + US1 (`VersionStore` para T049 publish)
- **US4 (P7 de ejecución)** → US1+US3 completas para empaquetar el conjunto; **gate dual-engine (T034) debe estar activo antes del release**
- **Polish (P8)** → stories deseadas completas

### Story Dependency Notes

Tras Foundational, US1, US2 y US5 pueden avanzar en paralelo (equipos distintos); en solitario, el orden de fases de este documento es el camino recomendado. US3 es la única story con dependencia dura de dos stories previas (US1+US2). M0 requiere US1 **y** US5 (evidencia dual-engine, constitución P-IV).

### Within Each Story

Contratos/tests junto a (o antes de) la implementación → implementación → integración → CI job de la story en verde antes del checkpoint.

---

## Parallel Example: Foundational

```bash
# Tras T006–T010 (shim secuencial, mismo lib.rs):
# En paralelo: T011 (tests Rust), T012 (fuzz targets), T013 (abstractions), T014 (excepciones)
# Tras T015–T017 (binding secuencial):
# En paralelo: T018 (unit tests), T019 (property tests), T020 (panic-injection)
```

## Parallel Example: User Story 1

```bash
# Arranque en paralelo: T022 (VersionId), T023 (IBlobStore+InMemory), T025 (TextDiff)
# Tras T026 (VersionStore): T027, T028, T029 en paralelo; T030 (sample) al final
```

---

## Implementation Strategy

### MVP First (US1 + US5 = M0)

1. Setup + Foundational (T001–T021) — gates P-I/P-II activos desde el día uno
2. US1 completa (T022–T031) → checkpoint técnico: quickstart §US1 + determinism gate verde
3. US5 (T032–T035) → **STOP & VALIDATE**: M0 cerrado con evidencia dual-engine (P-IV)
4. Demo consumible: versionado content-addressed local (valor real para el LMS consumidor)

### Incremental Delivery (mapa a hitos del brief)

1. US1 + US5 → M0 (API estable mínima, tests verdes, memoria limpia, abstracción viva)
2. US2 → M1 (carga sin corrupción/fugas)
3. US3 → M2 (2+ clientes Tiptap vía servidor .NET; publish citable por hash)
4. US4 → M3 (NuGet multi-RID, release Apache-2.0 con los 6 gates activos)

### Parallel Team Strategy

Tras Foundational: Dev A → US1 (y US5 al terminar la suite T027); Dev B → US2; Dev C → US5 en cuanto exista T027. US3 arranca cuando US1+US2 cierran; US4 al final con todo empaquetable.

---

## Notes

- Total: **63 tareas** (Setup 5 · Foundational 16 · US1 10 · US5 4 · US2 7 · US3 12 · US4 6 · Polish 3)
- Los gates de la constitución se activan lo antes posible (T021 memoria/fuzz, T031 determinismo, T034 dual-engine — todos antes de cerrar M0): un gate rojo bloquea merge desde ese momento
- Commit por tarea o grupo lógico; cada checkpoint es un punto válido de pausa/validación
