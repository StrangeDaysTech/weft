---
last_scan: 2026-07-15
schema_version: v1
total_open: 3
total_promoted: 0
total_closed_in_session: 17
total_phase_blocked: 0
total_suspected_closed: 0
buckets:
  - ready
  - time-triggered
  - charter-triggered
  - phase-blocked
  - operational
fully_extracted_ailogs:
  - AILOG-2026-07-10-001
  - AILOG-2026-07-10-002
  - AILOG-2026-07-14-002
  - AILOG-2026-07-15-001
  - AILOG-2026-07-15-002
  - AILOG-2026-07-15-003
---

# Follow-ups Backlog

> Central registry of `§Follow-ups` and `R<N> (new, not in Charter)` entries across AILOGs.
> Maintained by `straymark followups drift --apply`; counters are CLI-owned.
> Convention: `.straymark/00-governance/FOLLOW-UPS-BACKLOG-PATTERN.md` ·
> Schema: `.straymark/schemas/follow-ups-backlog.schema.v1.json`
>
> **Triaje 2026-07-10 (M0 cerrado)**: el extractor heurístico capturó secciones `## Risk: R<N>` de
> los AILOGs; varias eran riesgos ya RESUELTOS o duplicados, no follow-ups accionables. Reclasificadas
> a mano y añadidos los follow-ups reales de la auditoría externa de CHARTER-02 (G1/G3/G4/G5).

## Bucket: ready

### FU-007 — G3: usar `VersionId` directo como key de `InMemoryBlobStore`
- **Origin**: AILOG-2026-07-10-002 §Follow-ups (auditoría G3, qwen3-7-max) · review.md §4
- **Status**: closed
- **Trigger**: ready
- **Destination**: chore
- **Cost**: XS
- **Notes**: `ConcurrentDictionary<VersionId, byte[]>` en vez de `id.ToString()`; ahorra la asignación del hex de 64 chars por operación. `src/Weft.Versioning/Blobs/InMemoryBlobStore.cs:9`. Real_debt, sin impacto operativo (solo store en memoria de tests/dev). **Cerrado 2026-07-10** (AILOG-2026-07-10-003): key `VersionId` directo, `.ToString()` eliminado.

### FU-008 — G4: guard de compatibilidad de motor en `VersionStore.Merge`
- **Origin**: AILOG-2026-07-10-002 §Follow-ups (auditoría G4, qwen3-7-max) · review.md §4
- **Status**: closed
- **Trigger**: ready
- **Destination**: chore
- **Cost**: S
- **Notes**: un merge cross-engine (yrs↔Loro) hoy lanza `CorruptUpdateException` opaca. Añadir `EngineName` a `ICrdtDoc` (o similar) y lanzar `ArgumentException` clara antes del FFI. `src/Weft.Versioning/VersionStore.cs:67`. Ningún path actual lo dispara. **Cerrado 2026-07-10** (AILOG-2026-07-10-003): `ICrdtDoc.EngineName` + guards en `Merge`/`MergeAsync` + tests `CrossEngineMergeGuardTests`.

### FU-009 — G5: test directo de `FileSystemBlobStore`
- **Origin**: AILOG-2026-07-10-002 §Follow-ups (auditoría G5, hallado por el calibrador) · review.md §4
- **Status**: closed
- **Trigger**: ready
- **Destination**: chore
- **Cost**: S
- **Notes**: T024 sin cobertura directa; solo `InMemoryBlobStore` se ejercita en la suite. Añadir round-trip + sharding `aa/bb/hash` + escritura atómica con directorio temporal. **Cerrado 2026-07-10** (AILOG-2026-07-10-003): `FileSystemBlobStoreTests` (5 casos: round-trip, ausencia, sharding, idempotencia, sin temporales).

### FU-001 — (ruido del extractor: línea de resumen, no follow-up)
- **Origin**: AILOG-2026-07-10-001 §R1 (new, not in Charter)
- **Source-hash**: 356d0132850b
- **Status**: closed
- **Trigger**: ready
- **Destination**: chore
- **Cost**: XS
- **Notes**: Cerrado en triaje: capturó la línea "Riesgos R1–R5 mitigados…", no un follow-up accionable.

### FU-003 — (duplicado de FU-002: misma R6-amplificación de yrs)
- **Origin**: AILOG-2026-07-10-001 §R6 (new, not in Charter)
- **Source-hash**: f848fb99fdfb
- **Status**: superseded
- **Trigger**: ready
- **Destination**: chore
- **Cost**: XS
- **Notes**: Superseded por FU-002 (dos redacciones del mismo riesgo tras editar el AILOG).

### FU-004 — R6 (índices UTF-16): RESUELTO en CHARTER-02
- **Origin**: AILOG-2026-07-10-002 §R6 (new, not in Charter)
- **Source-hash**: 24e92818b6c7
- **Status**: closed
- **Trigger**: ready
- **Destination**: chore
- **Cost**: XS
- **Notes**: Corregido a `OffsetKind::Utf16` + regresión `Utf16IndexingTests`. No es follow-up pendiente.

### FU-005 — R7 (export Loro): RESUELTO en CHARTER-02
- **Origin**: AILOG-2026-07-10-002 §R7 (new, not in Charter)
- **Source-hash**: 1d1c514561fe
- **Status**: closed
- **Trigger**: ready
- **Destination**: chore
- **Cost**: XS
- **Notes**: Corregido a `ExportMode::all_updates()` + AIDEC-2026-07-10-001 (aprobado). No es follow-up pendiente.

### FU-017 — test de paridad header↔binding para el shim Loro
- **Origin**: AILOG-2026-07-15-002 §Follow-ups · CHARTER-10 (se creó el header, sin test de paridad)
- **Source-hash**: be38c88a4e9c
- **Status**: closed
- **Trigger**: ready
- **Destination**: chore
- **Cost**: S
- **Notes**: CHARTER-10 creó `native/weft-loro-ffi/include/weft_loro_ffi.h`, pero ningún test automatizado verifica que las declaraciones `[LibraryImport]` de `Weft.Loro/Interop/NativeMethods.cs` coincidan con él. El shim yrs SÍ lo tiene (`weft_ffi.h` ↔ `Weft.Core`). Replicar ese test para Loro (paridad de firmas / regenerable con csbindgen). Mejora de robustez; ningún gate depende. **CERRADO 2026-07-16 (CHARTER-12, AILOG-2026-07-16-001)**: la premisa era FALSA — el test de yrs **no existía** (dos comentarios lo afirmaban sin implementación). `HeaderBindingParityTests` se creó por primera vez, para AMBOS shims, probado contra mutaciones reales del header; los dos comentarios que mentían quedaron corregidos.

### FU-018 — **Comentario obsoleto en `.github/workflows/ci.yml:76-81`** — **dos afirmaciones falsas**, y ya
- **Origin**: AILOG-2026-07-15-003 §Follow-ups
- **Source-hash**: 26789333fd7e
- **Status**: closed
- **Trigger**: ready
- **Destination**: chore
- **Cost**: M
- **Notes**: Comentarios falsos/obsoletos en workflows y docs. **CERRADO 2026-07-16 (CHARTER-12, AILOG-2026-07-16-001)**: ~10 corregidos —cabecera del job `fuzz` (falso `continue-on-error` + obsoleto «M2»), warnings de steps, `with_capacity` genérico, «matriz de sanitizers», nombre del job, «gates posteriores», cross-impl «se añade en US4», `CONTRIBUTING.md:41` paridad Yjs «no-bloqueante», `README.md` (`NOTICE`/`native/weft-ffi/`/ruta del brief)— todos anclados a HEAD + pase adversarial (R4 no se materializó).

### FU-019 — **Footgun de pack local contaminado con `test-hooks`**: el pack lee de
- **Origin**: AILOG-2026-07-15-003 §Follow-ups
- **Source-hash**: d0e84c351361
- **Status**: closed
- **Trigger**: ready
- **Destination**: chore
- **Cost**: S
- **Notes**: El pack lee de `native/target/<triple>/release/`; compilar con `--features test-hooks` y empaquetar en local metería el símbolo de test en el `.nupkg` (el gate SC-009 solo corre en `release.yml`). **CERRADO 2026-07-16 (CHARTER-12, AILOG-2026-07-16-001)**: documentado como aviso en `CONTRIBUTING.md` (§Construir y probar). Severidad baja; el gate de release es la red de seguridad real.

### FU-020 — **Guards de CI para las sub-clases del anti-patrón** (paso 5 del walkthrough del patrón de Polish). El
- **Origin**: AILOG-2026-07-15-003 §Follow-ups
- **Source-hash**: fc2f384b719f
- **Status**: closed
- **Trigger**: ready
- **Destination**: chore
- **Cost**: M
- **Notes**: Guard mecánico contra «verificaciones fantasma». **CERRADO 2026-07-16 (CHARTER-12, AILOG-2026-07-16-001)**: `.github/scripts/check-test-filters.sh` + job bloqueante `test-filters`, para la sub-clase que este repo sí sufre (filtros de test que no casan → verde con 0 tests). Cubre el fantasma vivo `FullyQualifiedName~RedisDocumentStoreContractTests`. Probado: pasa con los 2 filtros reales, falla con uno inyectado. Las otras sub-clases del patrón (env-vars, instrumentos, rutas HTML) no aplican a Weft.
## Bucket: time-triggered

## Bucket: charter-triggered

### FU-016 — promover la siembra de client-id a capacidad cross-engine (Loro peer_id)
- **Origin**: AILOG-2026-07-15-001 §Follow-ups · CHARTER-09 (alcance yrs-only)
- **Source-hash**: 346b9c62a979
- **Status**: open
- **Trigger**: when se requiera paridad determinista para el motor Loro (gate Loro↔referencia)
- **Destination**: mini-charter
- **Cost**: M
- **Notes**: CHARTER-09 expuso la siembra de client-id como capacidad **concreta de `YrsEngine`** (`CreateDoc(ulong)`), no en `ICrdtEngine`, porque el gate `determinism-yjs` es yrs↔Yjs (misma familia de formato). Para un gate de determinismo de Loro: promover a capacidad cross-engine — `CreateDoc(clientId)` en `ICrdtEngine` (o una interfaz opcional tipo `INativeVersioning`) + `weft_loro_doc_new_with_peer_id` en `weft-loro-ffi` (Loro vía `set_peer_id`). Ningún gate depende hoy.

### FU-015 — adopción del fix de R6 vía bump de yrs (protocolo R16)
- **Origin**: AILOG-2026-07-14-002 §Follow-ups · CHARTER-08 (d) · PR upstream y-crdt/y-crdt#639
- **Source-hash**: 72ad54b51cf2
- **Status**: open
- **Trigger**: when el PR upstream #639 se mergee y publique en un release de crates.io
- **Destination**: chore
- **Cost**: S
- **Notes**: Bump `yrs = "=0.27.x"` en `native/weft-yrs-ffi/Cargo.toml` (+ el crate de fuzz) a la versión con el fix de R6; re-correr el fuzz `export_since` (debe pasar a RSS acotado, probando el fix) y revertir el fork `StrangeDaysTech/y-crdt` (volver a consumir yrs de crates.io). No bloquea el cierre de CHARTER-08 (entregable diferido por diseño, fuera de nuestro control: timeline de revisión de y-crdt).

### FU-002 — R6 (CHARTER-01): hardening del decoder ante amplificación de memoria (DoS)
- **Origin**: AILOG-2026-07-10-001 §R6 (new, not in Charter)
- **Source-hash**: 69e431c0f7d9
- **Status**: closed
- **Trigger**: when M2 (servidor relay recibe input de red no confiable)
- **Destination**: charter-replanning
- **Cost**: M
- **Notes**: El decoder de yrs amplifica memoria con update malformado (pocos bytes → asignación gigante → posible abort). Mitigar en la capa de red (M2/US3): límite de tamaño de mensaje + límite de recursos del proceso. Evaluar bump de yrs con validación de longitud. El fuzz es informativo hasta entonces. **Mitigación PARCIAL en CHARTER-04 (2026-07-13)**: parte a entregada — cap configurable de tamaño de mensaje en el framing lib0 + guarda anti-DoS de prefijo de longitud mentiroso (`SyncProtocol.Decode`/`Lib0Reader`, unit-tested). **CERRADO 2026-07-13 (CHARTER-05, PR #18)**: parte b entregada — límites de recursos por conexión / backpressure (`WeftServerOptions.MaxSendQueuePerConnection`, cola de envío acotada que cierra al consumidor lento) + path malformed→1002 + oversized→1009 en `WeftConnection`. FU-002 completo (parte a en CHARTER-04, parte b en CHARTER-05).

### FU-006 — G1: implementar la superficie `INativeVersioning` de Loro (diferida)
- **Origin**: AILOG-2026-07-10-002 §Follow-ups (auditoría G1, gpt-5-5 + qwen3-7-max) · review.md §4 · Charter-02 Closing notes
- **Status**: closed
- **Trigger**: when se requiera versionado nativo de Loro (probes de paridad)
- **Destination**: mini-charter
- **Cost**: M
- **Notes**: Diferido en CHARTER-02. Implementar probes `native_diff`/`native_branch`/`shallow_snapshot` en `weft-loro-ffi` + header `include/weft_loro_ffi.h` + `LoroNativeVersioning.cs` (`LoroEngine.NativeVersioning` pasaría de `null` a la implementación). Capacidad opcional; ningún gate depende. Reconciliar quickstart §US5 al implementarlo. **CERRADO 2026-07-15 (CHARTER-10, AILOG-2026-07-15-002)**: los 3 probes en `weft-loro-ffi` (ABI v2) + header `weft_loro_ffi.h` (creado) + binding + `LoroNativeVersioning` (cast+delega) + `LoroEngine.NativeVersioning` no-nulo + `LoroNativeVersioningTests` (5/5). Probes demostrativos (no content-addressing). Quickstart §US5 reconciliado. FU-017 registrado (test de paridad header↔binding del shim Loro).

### FU-010 — endurecimiento de durabilidad del relay: persist-before-broadcast (opcional)
- **Origin**: AIDEC-2026-07-13-001 §5 (CHARTER-05) · review.md F3 (auditoría gpt-5-5 + glm-5-2)
- **Status**: open
- **Trigger**: when se requieran garantías de durabilidad duras (relay multi-nodo, o SLA de no-pérdida sin depender de la reconexión)
- **Destination**: charter-replanning
- **Cost**: M
- **Notes**: El relay hace **broadcast-then-persist** (AIDEC §5): aplica el update dentro del turno del actor (difunde a los pares) y persiste `IDocumentStore.AppendUpdate` **después**, fuera del turno. Un fallo del append + crash antes del snapshot pierde ese update del store; en v1 (single-node) la auto-sanación CRDT (re-sync en reconexión) lo recupera. Endurecer SOLO si se requieren semánticas duras: persist-before-broadcast (reestructurar el orden) o manejar el fallo de append cerrando la conexión + test de crash mid-operation. **Ningún gate depende hoy**; decisión consciente documentada, no un bug.

### FU-011 — reponer la cobertura del adaptador Redis en CI (job Linux-only con service container)
- **Origin**: CHARTER-06 §Scope/§Out of scope / R4 · AILOG-2026-07-13-002 §Decisions #4 (registro hand-add + recount, vía §13; el follow-up nace en tiempo de declaración de Charter, sin sección extraíble — cf. issue straymark #360)
- **Status**: closed
- **Trigger**: when el presupuesto de minutos de GitHub Actions lo permita (resetea mensualmente)
- **Destination**: chore
- **Cost**: S
- **Notes**: CHARTER-06 no añadió job de CI para el adaptador Redis por coste de minutos (GH Actions ~agotado al ejecutarlo). El test `RedisDocumentStoreContractTests` es `[SkippableFact]`: corre local con `WEFT_TEST_REDIS=localhost:6379` (Valkey) y se **omite** en CI (sin servidor). Reponer: job Linux-only en `.github/workflows/ci.yml` con service container `redis:7`/`valkey` + `WEFT_TEST_REDIS` apuntándolo, para que el adaptador Redis se ejercite en CI. **Ningún gate depende hoy**: el gate local cubre la ruta funcional y el adaptador es .NET managed puro (sin comportamiento por-plataforma). EF Core/SQLite ya corre en CI (sin infra). **CERRADO 2026-07-14 (AILOG-2026-07-14-001)**: job `server-adapters` (ubuntu + service `redis:7`) corre los 8 tests del adaptador Redis vía `--filter RedisDocumentStoreContractTests`; verificado 8/8 verde local con Valkey.

### FU-012 — determinism-yjs: exponer client-id determinista + promover a gate de paridad cross-impl
- **Origin**: CHARTER-07 §Scope (T058) · AILOG-2026-07-13-003 · tests/determinism-yjs/README.md (registro hand-add + recount, §13)
- **Status**: closed
- **Trigger**: when se quiera promover el determinismo cross-implementación de informativo a gate bloqueante (o antes del primer bump de motor con impacto de encoding, R16)
- **Destination**: mini-charter
- **Cost**: M
- **Notes**: El harness `tests/determinism-yjs/` (T058) aplica el corpus compartido con Yjs y emite el SHA-256 del export, pero la paridad byte-idéntica con yrs está **gated en client-ids deterministas**: `ICrdtEngine.CreateDoc()` no toma parámetro y el shim FFI (`weft-yrs-ffi`) no expone fijar `client_id`, así que yrs asigna uno no controlable y su export no es reproducible cross-impl. Promover: (1) añadir `weft_doc_new_with_client_id` al FFI + `CreateDoc(ulong clientId)` al binding (aísla el bump, P-IV); (2) emitir el hash golden de yrs para el corpus; (3) pasar `WEFT_GOLDEN_HASH` al job y promoverlo a comparación con aserción; (4) añadir la variante unicode del corpus (índices UTF-16, R6). Hoy no-bloqueante; ningún gate depende. **CERRADO 2026-07-15 (CHARTER-09, AILOG-2026-07-15-001)**: entregadas las 4 partes — `weft_doc_new_with_client_id` (ABI v2, guard < 2^53) + `YrsEngine.CreateDoc(clientId)`; golden de Yjs comprometido (`golden.json` ascii+unicode); aserción de paridad **per-PR bloqueante** en `Weft.Determinism.Tests` (`Yrs_export_matches_yjs_golden`, 2/2 verde — **R1 se cumple: yrs == Yjs byte-idéntico**); corpus unicode + `apply.mjs` parametrizado + self-check del golden en `release.yml`. Loro diferido a FU-016.

### FU-013 — bump de GitHub Actions fuera de Node 20 (deprecado)
- **Origin**: CHARTER-07 (dry-run release.yml run 29307786498, annotations) · AILOG-2026-07-13-003 (registro hand-add + recount, §13)
- **Status**: closed
- **Trigger**: when Node 20 se retire de los runners de GH (o al tocar los workflows por otra razón)
- **Destination**: chore
- **Cost**: XS
- **Notes**: El dry-run anotó "Node.js 20 is deprecated" para `actions/checkout@v4`, `actions/setup-node@v4`, `actions/upload-artifact@v4`, `mlugg/setup-zig@v2` (forzados a Node 24). Bump a `@v5`/equivalentes en `.github/workflows/{ci.yml,release.yml,docs-validation.yml}` cuando toque. También aviso informativo: `macos-latest` migra a macOS 26 el 2026-06-15 (revisar el runner de `native (osx-arm64)` / pack-smoke). Cosmético hoy; ningún gate depende. **CERRADO 2026-07-14 (AILOG-2026-07-14-001)**: familia `actions/*` → `@v5` (checkout/setup-node/setup-dotnet/upload-artifact/download-artifact) + `setup-qemu-action@v4` en ci.yml + release.yml (cla.yml/docs-validation.yml ya estaban en v5). `mlugg/setup-zig@v2` y `rust-cache@v2` se dejan (última major). `macos-latest` se deja (migración ya pasada, dry-run verde en él).

### FU-014 — endurecer la ruta directa (no-relay) ante amplificación de memoria del decoder de yrs (R6)
- **Origin**: CHARTER-07 (annotations del job `fuzz`, run de PR #23) · AILOG-2026-07-10-001 §R6 · complementa FU-002 (cerrado, mitigación de capa de relay) — registro hand-add + recount (§13)
- **Status**: closed
- **Trigger**: antes de hacer el repo público, o cuando un consumidor ingiera bytes/updates CRDT no confiables fuera del relay
- **Destination**: mini-charter
- **Cost**: M
- **Notes**: FU-002 (cerrado) mitigó R6 en la **capa de relay** (cap de tamaño de mensaje + recursos antes de decodificar). El job `fuzz` (informativo, no-bloqueante) ejercita la ruta **cruda del FFI** (`weft_doc_load`/`weft_doc_apply_update`), que **NO** está capeada por FU-002: un consumidor que alimente bytes no confiables **directamente** a la API pública (fuera del relay) podría disparar la amplificación de memoria del decoder de yrs (`with_capacity(N)` sin cota → `handle_alloc_error`, no capturable por `catch_unwind`). El shim es memory-safe (ASan verde); la amplificación es **upstream yrs**. Dos partes: **(a) [XS]** nota de seguridad en `GOVERNANCE.md`/README — "si ingieres updates/blobs CRDT no confiables fuera del relay, aplica un cap de tamaño como hace el servidor"; **(b) [M]** evaluar bump de yrs con validación de longitud (protocolo R16) o un guard de tamaño a nivel FFI en `weft_doc_load`/`apply_update`. Ningún gate depende hoy; no urge (no se publica ya). Abordar junto con FU-006/FU-010/FU-012 (mini-charters).

## Bucket: phase-blocked

## Bucket: operational
