# Quickstart — Validación end-to-end de Weft

**Feature**: [spec.md](./spec.md) · **Contratos**: [contracts/](./contracts/) · **Fecha**: 2026-07-10

Guía de validación: cómo probar que cada user story funciona de punta a punta, y qué gates de
CI protegen la constitución. No contiene implementación; las rutas siguen la estructura de
[plan.md](./plan.md).

## Prerrequisitos

- .NET SDK 10.x · Rust stable (según `rust-toolchain.toml`) · cargo
- Solo jobs de memoria: Rust nightly + linux-x64
- Solo cross-check de determinismo: Node LTS (Yjs JS)

## Build local

```bash
# 1. Shims nativos (yrs + loro). `native/` es un workspace cargo: un solo build cubre ambos
#    y deja los artefactos en native/target/release/ (NO en native/<crate>/target/).
#    `--features test-hooks` exporta `weft_test_panic`, que la suite de panic-safety (SC-009)
#    necesita; sin él, PanicSafetyTests falla con EntryPointNotFoundException. Es lo mismo que
#    hace el CI. El símbolo NUNCA viaja en release: el pipeline de release compila sin la feature
#    y el gate `pack-smoke` verifica su ausencia en los binarios empaquetados.
cargo build --release --features test-hooks --manifest-path native/Cargo.toml

# 2. Solución .NET. No hace falta copiar el .so a mano: los .csproj de test lo copian desde
#    native/target/release/, y el pack lo toma de native/target/<triple>/release/ (ver
#    build/Weft.Native.targets).
dotnet build Weft.sln -c Release
```

## Validación por user story

### US1 (P1 · M0) — Editar y versionar desde .NET

```bash
dotnet test tests/Weft.Core.Tests tests/Weft.Versioning.Tests -c Release
dotnet run --project samples/Weft.Sample.Versioning   # escenario ejecutable de la historia
```

**Esperado** (postcondiciones de [versioning-api.md](./contracts/versioning-api.md)):
crear → editar → `Publish` (hash hex de 64 chars) → editar → `Publish` v2 → `Diff(v1,v2)`
muestra los cambios por palabras → `Branch(v1)` + ediciones divergentes + `Merge` converge →
tras compactación implícita, `Checkout(v1)` sigue siendo byte-idéntico. Réplicas convergidas
publican el mismo `VersionId`.

### US2 (P2 · M1) — Concurrencia a escala

```bash
dotnet test tests/Weft.Core.Tests --filter Category=Concurrency -c Release
dotnet run --project tests/Weft.LoadTest -c Release   # cientos de docs, tareas concurrentes
```

**Esperado**: estado final consistente en todos los docs; memoria acotada (sin crecimiento
monótono); desalojo por inactividad invoca `OnEvicting` y el doc reabre desde lo persistido;
uso tras dispose → `ObjectDisposedException`; nunca corrupción (SC-006).

### US3 (P3 · M2) — Colaboración en tiempo real

```bash
dotnet test tests/Weft.Server.Tests -c Release        # protocolo + 2 clientes simulados

# Relay real (:5199 por defecto; override con WEFT_SAMPLE_URLS) + FileSystemDocumentStore:
dotnet run --project samples/Weft.Sample.Server

# Smoke headless de compat del wire: 2 clientes Yjs reales vía y-websocket contra el relay.
# Valida convergencia sin navegador — es el check ejecutable en CI/servidor sin display.
cd samples/tiptap-client && npm install && npm run check

# Manual con editor real (exigido por el criterio de cierre de M2):
npm run dev                                           # 2 pestañas → mismo doc
```

**Esperado** (postcondiciones de [server-api.md](./contracts/server-api.md)): convergencia en
vivo < 1 s; presencia visible y retirada al cerrar; reconexión transfiere solo delta
(SC-004: ≥ 90 % menos bytes); `Deny` → 403 sin contenido; `ReadOnly` que escribe → cierre 1008;
`IWeftServer.PublishAsync` → mismo `VersionId` que en local; rearranque del servidor recupera
estado sin pérdida.

### US4 (P4 · M3) — Instalación multiplataforma

```bash
dotnet pack src/Weft.Core -c Release -o ./artifacts   # incluye runtimes/<rid>/native/
# En máquina limpia por RID:
dotnet new console && dotnet add package Weft.Core --source ./artifacts && dotnet run  # hello Weft
```

**Esperado**: el binario nativo se resuelve automáticamente en los 4 RIDs; `weft_abi_version`
coincide (si no, excepción clara al cargar); ejemplo mínimo verde al primer intento (SC-007).

### US5 (P5 · dual-path) — Motor reemplazable

```bash
# El shim de Loro ya está construido por el paso 1 de «Build local» (workspace cargo).
dotnet test tests/Weft.Versioning.Tests -c Release    # theory: YrsEngine Y LoroEngine
```

**Esperado**: la MISMA suite de versionado verde sobre ambos motores (SC-008);
`YrsEngine.NativeVersioning == null` sin romper ningún flujo. **Superficie `INativeVersioning` de Loro
(G1 CERRADO, CHARTER-10/FU-006)**: `LoroEngine.NativeVersioning` ya **no** es null — expone tres probes
**demostrativos** del versionado nativo de Loro (`ShallowSnapshot`, `NativeDiffProbe`,
`NativeBranchMergeProbe`; `LoroNativeVersioningTests`). Son opcionales y **no** content-addressing: su
salida no es determinista y no alimenta `VersionId` (que usa `ExportState`); ningún gate depende de ellos.
`YrsEngine.NativeVersioning == null` permanente (yrs no tiene versionado nativo).

## Gates de CI (constitución)

Un rojo bloquea merge, **con dos excepciones** que conviene conocer antes de fiarse de la columna:
`fuzz` bloquea sólo a medias (si los targets no compilan, rojo; si encuentran un crash, sólo
`::warning`) y el `pack-smoke` real **no corre por PR** — vive en `release.yml`
(`workflow_dispatch`). Ambas están detalladas en su fila.

| Gate | Job | Comando (esencia) | Principio |
|---|---|---|---|
| Build+tests multiplataforma | `test-{linux,win,mac}` | `dotnet test Weft.sln` + `cargo test` | P-VI |
| Memoria | `asan` (linux, nightly) | `RUSTFLAGS="-Zsanitizer=address" cargo +nightly test --target x86_64-unknown-linux-gnu` en ambos shims → 0 fugas/0 double-free | P-II |
| Determinismo | `determinism` | `dotnet test tests/Weft.Determinism.Tests` cross-RID. La paridad yrs↔Yjs es **bloqueante** y vive aquí (`Yrs_export_matches_yjs_golden`, contra `tests/determinism-yjs/golden.json`) desde CHARTER-09/FU-012 — la promoción que research R13 anticipaba ya ocurrió. El job Node `determinism-yjs` (`release.yml`, `continue-on-error`) es **informativo**: regenera el hash de Yjs para cazar drift del upstream, no es la aserción de paridad | P-III |
| Dual-engine | `dual-engine` | suite Versioning con ambos motores | P-IV |
| Fuzzing | `fuzz` (smoke 60 s/target en PR; extendido nightly) — **bloquea a medias** | `cargo fuzz run doc_load` / `apply_update`; CsCheck convergencia. Un fallo de **compilación** de los targets pone el job rojo (deliberado); un **crash encontrado** sólo emite `::warning` — es un `\|\| echo` por paso, **no** `continue-on-error` en el job. Razón: los targets **reproducen R6 hoy** (un input de ~4 B hace que el decoder de yrs reserve sin cota → `handle_alloc_error`, que `catch_unwind` no puede contener). El shim es correcto (contiene panics, sin UB); el fix vive upstream ([y-crdt#639](https://github.com/y-crdt/y-crdt/pull/639), aprobado) y se adopta vía bump (FU-015). Caveat de la ruta directa en `GOVERNANCE.md` §Seguridad (CHARTER-08) | P-I/P-II |
| Empaquetado | `pack-smoke` — **no corre por PR** | El job `pack-smoke` de `ci.yml` es un **marcador** (sólo hace `echo`): no empaqueta ni valida nada. La matriz real (pack + instalar + hello-Weft por RID, SC-007) vive en `release.yml`, que es `workflow_dispatch` únicamente porque la matriz cross-compile es cara → se valida en el **dry-run del release**, no en cada PR. La verificación de que `weft_test_panic` no está exportado (SC-009) la hace el job **`native`** de ese mismo workflow, con `nm` sobre los cdylibs antes del pack | P-VI |

## Criterio de cierre por hito

- **M0**: US1 verde + gates memoria/determinismo/dual-engine activos en CI.
- **M1**: US2 verde (prueba de carga en CI nightly).
- **M2**: US3 verde incluida la validación manual con Tiptap real (2+ clientes).
- **M3**: US4 verde en los 4 RIDs + release Apache-2.0 publicado.
