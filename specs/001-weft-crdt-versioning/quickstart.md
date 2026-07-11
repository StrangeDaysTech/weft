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
# 1. Shim nativo (yrs) y copia al árbol de runtimes
cargo build --release --manifest-path native/weft-yrs-ffi/Cargo.toml
cp native/weft-yrs-ffi/target/release/libweft_yrs_ffi.so \
   src/Weft.Core/runtimes/linux-x64/native/          # (.dll/.dylib según plataforma)

# 2. Solución .NET
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
# Manual con editor real:
dotnet run --project samples/Weft.Sample.Server       # relay en :5000 + FileSystemDocumentStore
cd samples/tiptap-client && npm install && npm run dev # 2 pestañas → mismo doc
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
cargo build --release --manifest-path native/weft-loro-ffi/Cargo.toml
dotnet test tests/Weft.Versioning.Tests -c Release    # theory: YrsEngine Y LoroEngine
```

**Esperado**: la MISMA suite de versionado verde sobre ambos motores (SC-008);
`YrsEngine.NativeVersioning == null` sin romper ningún flujo. **Nota (M0)**: la superficie
`INativeVersioning` de Loro (probes nativos) está **diferida a post-M0** (auditoría CHARTER-02, G1) —
`LoroEngine.NativeVersioning == null` en M0; es capacidad opcional y ningún gate depende de ella.

## Gates de CI (constitución — un rojo bloquea merge)

| Gate | Job | Comando (esencia) | Principio |
|---|---|---|---|
| Build+tests multiplataforma | `test-{linux,win,mac}` | `dotnet test Weft.sln` + `cargo test` | P-VI |
| Memoria | `asan` (linux, nightly) | `RUSTFLAGS="-Zsanitizer=address" cargo +nightly test --target x86_64-unknown-linux-gnu` en ambos shims → 0 fugas/0 double-free | P-II |
| Determinismo | `determinism` | `dotnet test tests/Weft.Determinism.Tests` cross-RID; job Node compara blobs vs Yjs JS (no-bloqueante al inicio, promovible — research R13) | P-III |
| Dual-engine | `dual-engine` | suite Versioning con ambos motores | P-IV |
| Fuzzing | `fuzz` (acotado por tiempo en PR; extendido nightly) | `cargo fuzz run doc_load` / `apply_update`; CsCheck convergencia | P-I/P-II |
| Empaquetado | `pack-smoke` | matriz: pack + instalar + hello-Weft por RID | P-VI |

## Criterio de cierre por hito

- **M0**: US1 verde + gates memoria/determinismo/dual-engine activos en CI.
- **M1**: US2 verde (prueba de carga en CI nightly).
- **M2**: US3 verde incluida la validación manual con Tiptap real (2+ clientes).
- **M3**: US4 verde en los 4 RIDs + release Apache-2.0 publicado.
