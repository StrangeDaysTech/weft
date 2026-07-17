<!-- SPDX-License-Identifier: Apache-2.0 -->

# Contribuir a Weft

Gracias por tu interés. Weft es una librería .NET (Apache-2.0) de colaboración CRDT en tiempo real y
versionado content-addressed sobre el core Rust `yrs`, vía un shim C-ABI propio.

## CLA

Toda contribución requiere firmar el **CLA** (Contributor License Agreement) — un bot lo pide
automáticamente en tu primer PR. Ver [`CLA.md`](./CLA.md).

## Toolchain

- **.NET SDK 10** (`net10.0`, C# 13).
- **Rust stable** (fijado en `native/rust-toolchain.toml`); `nightly` solo para ASan/LSan (lo instala CI).
- Para cross-compilar los binarios nativos localmente (RIDs de Linux): `cargo-zigbuild` + `zig` 0.15.x.
- Opcional: **Node.js** para el gate de determinismo cross-implementación (`tests/determinism-yjs/`) y el
  cliente de ejemplo Tiptap.

## Construir y probar

```bash
# Shim nativo (con test-hooks para la suite de panic-safety, SC-009)
cd native && cargo build --release --features test-hooks && cargo test --features test-hooks && cd ..

# Solución .NET completa
dotnet build Weft.sln -c Release
dotnet test  Weft.sln -c Release        # el test de Redis se salta sin WEFT_TEST_REDIS (Valkey/Redis local)
```

> **No empaquetes (`dotnet pack`) desde un árbol compilado con `--features test-hooks`.** El pack lee
> el cdylib de `native/target/<triple>/release/`; si lo construiste con la feature (p. ej. tras
> `cargo build --release --target <triple> --features test-hooks`), el `.nupkg` incluiría el símbolo
> `weft_test_panic`/`weft_loro_test_panic`. El gate SC-009 que verifica su ausencia solo corre en el
> pipeline de `release.yml`, **no** en un pack local. Para publicar, compila el nativo **sin** la
> feature (FU-019).

## Gates (constitución)

La constitución del proyecto (`.specify/memory/constitution.md`) fija 6 principios **vinculantes**, cada uno
con su gate de CI. Un PR no se mergea sin ellos en verde:

| Principio | Gate |
|---|---|
| **P-I** FFI segura | ningún panic cruza la frontera C (`catch_unwind` en cada entrada) |
| **P-II** Memoria verificada | ASan/LSan sobre los tests Rust de ambos shims — 0 fugas / 0 double-free |
| **P-III** Determinismo | encoding reproducible cross-RID + paridad byte-idéntica cross-impl vs Yjs (**bloqueante** desde CHARTER-09) |
| **P-IV** Motor reemplazable | la suite de versionado corre idéntica sobre `yrs` **y** Loro (dual-engine) |
| **P-V** Concurrencia por doc | acceso a `ICrdtDoc` serializado; el broker usa actor/canal single-reader |
| **P-VI** Portabilidad por RID | *pack-smoke* del paquete en cada RID soportado — "soportado" = ejercitado |

Reglas duras al escribir código: buffers del shim liberados solo con `weft_buf_free` (el GC jamás toca memoria
nativa); nunca `skip_gc`; `Weft.Versioning` no referencia tipos de `yrs`/Loro (solo las abstracciones); API
pública con índices `int` validados y errores nativos → jerarquía `WeftException`.

## Protocolo de bump del motor (yrs / Loro) — research R16

Las versiones de los motores están **pinneadas exactas** (`yrs = "=0.27.2"`, `loro = "=1.13.6"`) con
`Cargo.lock` versionado — los nombres y firmas de `yrs` cambian entre minors, y el gate de determinismo
(P-III) exige reproducibilidad. Para subir un motor:

1. **Rama dedicada**; actualizar el pin exacto en `native/<crate>/Cargo.toml` + `Cargo.lock`.
2. **Ajustar el shim** (`native/<crate>/src/lib.rs`) a los cambios de API del motor. El shim aísla el bump: la
   **C-ABI propia y el C# no cambian** (esa es su razón de ser).
3. **Correr los gates completos**: sanitizers (P-II), determinismo cross-RID **y cross-implementación**
   (P-III — un bump puede cambiar el encoding y romper la citabilidad de versiones previas), convergencia y
   dual-engine (P-IV).
4. **Merge solo en verde.** Un cambio de encoding es *breaking* para el content-addressing → se trata como
   tal en el versionado SemVer del paquete.

## Flujo de trabajo

Spec-driven con [GitHub Spec Kit](https://github.com/github/spec-kit) (spec → plan → tasks → implement) y
gobernanza documental con [StrayMark](https://github.com/StrangeDaysTech/straymark) (Charters + AILOG/AIDEC).
Ver [`GOVERNANCE.md`](./GOVERNANCE.md). Las decisiones ✅ CERRADO del brief (`weft-design-brief.md`) no se
re-litigan.

## Reportar bugs / proponer cambios

Abre un issue con repro mínimo. Para cambios sustantivos, comenta el diseño en un issue antes del PR — los
cambios de contrato (FFI, `IDocumentStore`, protocolo de sync) requieren acuerdo previo.
