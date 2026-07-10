<!-- SPDX-License-Identifier: Apache-2.0 -->

# Weft

**Colaboración en tiempo real (CRDT) y versionado content-addressed de documentos, para .NET.**

Weft es una librería .NET que envuelve el core CRDT [`yrs`](https://github.com/y-crdt/y-crdt) (el port oficial en Rust de [Yjs](https://github.com/yjs/yjs)) mediante un binding propio, y construye encima una capa de **versionado inmutable** (identificado por contenido) más un **servidor de sincronización** — diseñada a nuestros patrones, no heredando los de un binding de terceros.

> **El nombre.** `yrs` se pronuncia *"wires"* (hilos). El **weft** son los hilos que se tejen a través de la urdimbre en un telar: el motor aporta los hilos, Weft los teje en colaboración. Un guiño a la familia de proyectos de [Strange Days Tech](https://strangedays.tech/es) — Arborist, StrayMark, Weft.

## Estado

🚧 **Diseño / construcción temprana.** El diseño se elabora antes de implementar (spec-driven). La base técnica está validada por tres experimentos previos (ver `docs/`): fundamento del binding, comparación de motores CRDT, y plomería de versionado. El código llegará por hitos.

## Qué ofrece (frontera del componente)

Weft es un **building block reutilizable**, no una aplicación. Provee:

- **Binding .NET a `yrs`** vía un shim C-ABI propio (`ICrdtEngine` / `ICrdtDoc`), con ciclo de vida seguro (`SafeHandle`), contrato de ownership explícito y seguridad de memoria verificada.
- **Versionado de documento content-addressed:** publicar versiones (`SHA-256` del export byte-determinista), diff, ramas/merge y compactación — **engine-agnóstico** (probado corriendo idéntico sobre `yrs` y Loro).
- **Sincronización:** sync incremental (state-vector + delta), servidor relay WebSocket (protocolo Yjs), presencia/awareness y adaptadores de persistencia.
- **Capacidad opcional `INativeVersioning`** para motores que la ofrezcan (p. ej. Loro), manteniendo el motor reemplazable.

Lo que **no** es Weft: modelo de contenido de dominio de una app concreta, editor de frontend, ni lógica de negocio. Esas viven en el consumidor (p. ej. un LMS), que **depende de Weft, nunca al revés**.

## Motor

- **Core: `yrs`** (adoptado, no reimplementado). Elegido por continuidad (el formato Yjs tiene múltiples implementaciones independientes → fork = elegir entre implementaciones), madurez del ecosistema de editores (Tiptap/ProseMirror + `y-prosemirror`) y fork-safety.
- **Cliente de editor recomendado:** [Tiptap](https://tiptap.dev) (sobre ProseMirror) + `y-prosemirror`, conectado al servidor relay de Weft.
- **Dual-path:** [Loro](https://github.com/loro-dev/loro) queda como alternativa viva tras la abstracción; cambiar de motor = cambiar el adaptador, no la capa de versionado.

## Estructura del repo (propuesta)

```
weft/
├── LICENSE                     # Apache-2.0
├── README.md
├── NOTICE
├── .gitignore
├── native/
│   └── weft-ffi/               # crate Rust cdylib: shim C-ABI sobre yrs (+ contrato de ownership)
│       ├── Cargo.toml          # yrs pinneado (=X.Y.Z)
│       ├── src/lib.rs
│       ├── include/weft_ffi.h  # header C (contrato)
│       └── tests/mem_asan.rs   # harness de memoria (ASan/LSan)
├── src/
│   ├── Weft.Core/              # ICrdtEngine/ICrdtDoc, P/Invoke [LibraryImport], SafeHandle
│   ├── Weft.Versioning/        # publish/diff/branch/merge/compact (content-addressed)
│   ├── Weft.Server/            # relay WebSocket, awareness, persistencia
│   └── Weft.Loro/              # adaptador opcional (INativeVersioning) — dual-path
├── tests/
├── docs/                       # briefs, ICrdtEngine, decisiones
├── .specify/                   # GitHub Spec Kit (spec/plan/tasks)
└── .github/workflows/          # CI: build multi-RID, tests, ASan, fuzzing, determinismo
```

## Desarrollo

Spec-driven, con [GitHub Spec Kit](https://github.com/github/spec-kit): **Spec → Plan → Tasks → Implement**. El diseño (`/specify`, `/plan`) y las tandas de implementación (`/tasks`, `/implement`) se realizan en Claude Code. Ver el **brief de diseño** en `docs/weft-design-brief.md`.

Toolchain: Rust (con `yrs` pinneado) · .NET SDK 10 (LTS) · empaquetado nativo por RID (Linux/Windows/macOS, x64/arm64).

## Licencia

[Apache-2.0](./LICENSE) © 2026 [Strange Days Tech](https://strangedays.tech/es). Librería permisiva con concesión explícita de patentes; reciproca a los motores MIT sobre los que se apoya (`yrs`, Loro).
