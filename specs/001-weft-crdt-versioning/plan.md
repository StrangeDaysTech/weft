# Implementation Plan: Weft — Colaboración CRDT en tiempo real y versionado content-addressed para .NET

**Branch**: `001-weft-crdt-versioning` | **Date**: 2026-07-10 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/001-weft-crdt-versioning/spec.md`

**Note**: This template is filled in by the `/speckit-plan` command. See `.specify/templates/plan-template.md` for the execution workflow.

## Summary

Weft se construye como una solución .NET multi-paquete sobre un shim C-ABI propio en Rust que envuelve `yrs`:

- **`Weft.Core`**: binding seguro (SafeHandle, ownership explícito, `catch_unwind`, excepciones tipificadas), abstracción `ICrdtEngine`/`ICrdtDoc`, y el modelo de concurrencia actor/canal por documento (`DocumentBroker`).
- **`Weft.Versioning`**: capa de dominio engine-agnóstica de versionado content-addressed (`VersionId` = SHA-256 del export determinista): publish/diff/branch/merge/compact sobre un `IBlobStore`.
- **`Weft.Server`**: servidor relay WebSocket (ASP.NET Core) con protocolo y-sync + awareness, sync incremental por state-vector, adaptadores de persistencia de blobs opacos, snapshot content-addressed al publicar y hook de authz inyectado por el consumidor.
- **`Weft.Loro`**: adaptador dual-path que prueba la portabilidad de la abstracción (misma suite de versionado sobre ambos motores).
- **Nativo/CI**: crates Rust `weft-yrs-ffi` (y `weft-loro-ffi`), cross-compilados por RID y empaquetados como NuGet nativo (patrón SkiaSharp); gates de CI: build+tests multiplataforma, ASan/LSan, fuzzing FFI/convergencia y test de determinismo del encoding.

El enfoque técnico completo está validado por los spikes 01–03 (`docs/spikes/`); este plan convierte esa evidencia en API pública, contratos y estructura de módulos.

## Technical Context

**Language/Version**: C# 13 / .NET 10 (`net10.0`) para la librería; Rust stable (fijado, ver `rust-toolchain.toml`) para los shims; Rust nightly solo para los jobs de sanitizers en CI.

**Primary Dependencies**: `yrs = "=0.27.2"` (motor principal, pinned exacto); `loro = "=1.13.6"` (adaptador dual-path, pinned exacto); ASP.NET Core (solo `Weft.Server`); `System.Threading.Channels` (actor por documento); sin dependencias de terceros en `Weft.Core`/`Weft.Versioning` más allá de la BCL.

**Storage**: `IBlobStore` (content-addressed, hash→blob) para versionado; `IDocumentStore` (docId→blob opaco) para persistencia del servidor, con adaptadores: in-memory (tests/dev), sistema de archivos (v1), EF Core y Redis (M2; blobs opacos, intercambiables).

**Testing**: xUnit (.NET) + `cargo test` (Rust); CsCheck/property-based para convergencia CRDT; `cargo-fuzz` en la frontera FFI; ASan+LSan (nightly) como gate; test de determinismo del encoding cross-implementación (yrs vs Yjs JS vía Node en CI); suite de versionado ejecutada contra ambos motores.

**Target Platform**: `linux-x64`, `linux-arm64`, `win-x64`, `osx-arm64` (NuGet nativo multi-RID; cross-compilación con `cargo-zigbuild`, fallback `cross`).

**Project Type**: librería .NET multi-paquete + crates nativos (building block OSS, no aplicación).

**Performance Goals**: propagación entre clientes < 1 s en red local (SC-005); sync incremental ≥ 90 % menos bytes que estado completo (SC-004, referencia medida 523 B → 29 B); cientos de documentos activos por proceso sin degradación (SC-006).

**Constraints**: `yrs` no es `Send+Sync` → acceso por documento estrictamente serializado (constitución P-V); GC del motor siempre activo — nunca `skip_gc` (P-III); buffers nativos liberados solo por la FFI (P-I); memoria 0 fugas / 0 double-free bajo sanitizers (P-II); export byte-determinista como base de identidad (P-III).

**Scale/Scope**: v1 = texto colaborativo por campos nombrados; 4 paquetes .NET + 2 crates; hitos M0–M3 de la spec; diferidos explícitos: diff estructural rich-text, blame por rango, motor-en-servidor.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principio | Cumplimiento en este diseño | Estado |
|---|---|---|
| **P-I · Frontera nativa segura** | Shim C-ABI propio (`weft-yrs-ffi`); nunca se bindea `yffi`. `DocHandle : SafeHandle`; contrato de ownership documentado en el header y en `contracts/ffi-abi.md`; `catch_unwind` en cada entrada; códigos `i32` → jerarquía `WeftException`. | ✅ PASS |
| **P-II · Memoria verificada (gate CI)** | Job de CI con ASan+LSan sobre los tests Rust de ambos shims + stress desde .NET; 0 fugas/0 double-free requerido para merge (definido en `quickstart.md` §CI). | ✅ PASS |
| **P-III · Determinismo verificable** | `VersionId = SHA-256(ExportState)`; test de determinismo como gate (mismo contenido → mismo blob byte a byte, cross-plataforma y contrastado con Yjs JS); GC del motor siempre activo; blobs por versión publicada para citabilidad. | ✅ PASS |
| **P-IV · Abstracción de motor viva** | `Weft.Versioning` referencia solo las abstracciones de `Weft.Core` (sin tipos de yrs); `Weft.Loro` compila y corre la MISMA suite de versionado en CI; `yrs`/`loro` pinned exactos; capacidades extra vía `INativeVersioning` opcional. | ✅ PASS |
| **P-V · Concurrencia serializada por documento** | `ICrdtDoc` crudo nunca se expone desde `DocumentBroker`: el acceso público a docs gestionados es vía `DocumentSession` que despacha a un canal single-reader por documento; pooling y desalojo por inactividad con `DisposeAsync` determinista. | ✅ PASS |
| **P-VI · Portabilidad probada por RID** | Matriz de CI compila el cdylib por los 4 RIDs y corre smoke test del paquete en linux-x64, win-x64 y osx-arm64 (linux-arm64 vía QEMU/runner arm). "Soportado" = ejercitado. | ✅ PASS |

**Decisiones cerradas**: respetadas íntegras (motor yrs, shim propio, content-addressing, actor por doc, Apache-2.0, Tiptap recomendado, dual-path Loro). No se detectó contradicción técnica dura.

**Re-check post-Phase 1**: ✅ PASS — los contratos generados (`contracts/`) no introducen violaciones: ninguna API pública expone punteros nativos ni tipos del motor; la superficie de `Weft.Versioning` depende solo de las abstracciones.

## Project Structure

### Documentation (this feature)

```text
specs/001-weft-crdt-versioning/
├── plan.md              # Este archivo
├── research.md          # Phase 0: decisiones consolidadas (evidencia de spikes)
├── data-model.md        # Phase 1: entidades y relaciones
├── quickstart.md        # Phase 1: guía de validación end-to-end + gates de CI
├── contracts/           # Phase 1: contratos de API pública y ABI
│   ├── core-api.md      #   ICrdtEngine/ICrdtDoc, excepciones, DocumentBroker
│   ├── ffi-abi.md       #   C-ABI del shim: funciones, códigos, ownership
│   ├── versioning-api.md#   VersionId, publish/diff/branch/merge/compact, IBlobStore
│   └── server-api.md    #   Protocolo y-sync/awareness, persistencia, auth hook
└── tasks.md             # Phase 2 (/speckit-tasks — no lo crea /speckit-plan)
```

### Source Code (repository root)

```text
native/
├── weft-yrs-ffi/            # Shim C-ABI sobre yrs (Rust, cdylib) — M0
│   ├── src/lib.rs
│   ├── include/weft_ffi.h   # Header de referencia (contrato de ownership)
│   └── tests/               # tests + mem_asan + fuzz targets
└── weft-loro-ffi/           # Shim del adaptador Loro — dual-path (P-IV)

src/
├── Weft.Core/               # Binding yrs + abstracciones + concurrencia — M0/M1
│   ├── Abstractions/        #   ICrdtEngine, ICrdtDoc, INativeVersioning
│   ├── Yrs/                 #   YrsEngine, YrsDoc, DocHandle(SafeHandle), NativeMethods
│   ├── Concurrency/         #   DocumentBroker, DocumentSession, opciones — M1
│   └── WeftException.cs     #   jerarquía de errores
├── Weft.Versioning/         # Capa de dominio engine-agnóstica — M0
│   ├── VersionId.cs, VersionStore.cs, TextDiff.cs
│   └── Blobs/               #   IBlobStore + InMemory/FileSystem
├── Weft.Server/             # Relay WebSocket ASP.NET Core — M2
│   ├── Protocol/            #   y-sync framing (SyncStep1/2, Update, Awareness)
│   ├── Persistence/         #   IDocumentStore + in-memory (EFCore/Redis aparte)
│   └── Auth/                #   IWeftAuthorizer (hook del consumidor)
└── Weft.Loro/               # Adaptador ICrdtEngine + INativeVersioning — P-IV

tests/
├── Weft.Core.Tests/         # unit + convergencia (property-based)
├── Weft.Versioning.Tests/   # suite dual-engine (corre vs yrs Y vs Loro)
├── Weft.Server.Tests/       # protocolo + integración (2 clientes simulados)
└── Weft.Determinism.Tests/  # gate de determinismo (incl. cross-check Yjs JS)

docs/                        # evidencia de spikes + docs públicos
.github/workflows/           # ci.yml (gates), release.yml (NuGet multi-RID)
```

**Structure Decision**: solución única .NET (`Weft.sln`) con crates Rust en `native/` — un solo repo, frontera clara binding/nativo. `Weft.Core` contiene las abstracciones (subcarpeta `Abstractions/`, mismo ensamblado en v1 para minimizar paquetes; si un consumidor exigiera las abstracciones sin binarios nativos, extraer `Weft.Abstractions` es un refactor no-breaking diferido). Los adaptadores EF Core/Redis del servidor viven como paquetes separados (`Weft.Server.Persistence.*`) para no arrastrar sus dependencias al relay básico.

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

Sin violaciones — tabla no aplica.
