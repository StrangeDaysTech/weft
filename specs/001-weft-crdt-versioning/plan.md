# Implementation Plan: Weft — Colaboración CRDT en tiempo real y versionado content-addressed para .NET

**Branch**: `001-weft-crdt-versioning` | **Date**: 2026-07-10 | **Spec**: [spec.md](./spec.md)

> **Refresh 2026-07-11 (scope-limitado a US3/M2)**: tras cerrar M0 (CHARTER-01/02) y M1 (CHARTER-03),
> se refresca **solo** la planificación de US3/M2 para reflejar los refinamientos empíricos de M1 que el
> relay hereda (ver §"US3/M2 — anclajes sobre M1"). Las secciones de M0/M1 (Summary de Core/Versioning/Loro,
> Constitution Check P-I..P-VI, Project Structure de `native/`/`Weft.Core`/`Weft.Versioning`/`Weft.Loro`,
> Technical Context de M0/M1) son **inmutables**: el código shippeado es la verdad, no este plan.
> `tasks.md` NO se regenera (conserva los `[X]` + `*CHARTER-NN: <sha>*`). Fuente: AILOG-2026-07-11-001.

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

**Re-check per spec-refresh (2026-07-11, cadencia del bridge)**: ✅ PASS. El refresh de US3/M2 no altera ningún veredicto. Cómo se tensa cada principio en US3 (detalle en §"US3/M2 — anclajes sobre M1"): **P-V** (serialización por doc, cerrado en M1) es re-estresado por la concurrencia de red — el relay aplica **todo** update entrante vía `DocumentSession`/turno del actor, nunca al `ICrdtDoc` crudo. **P-III** (determinismo) se activa en el publish del servidor (paridad de `VersionId` server↔local). **P-I/P-II** (frontera nativa / memoria) quedan bajo nueva presión por input de red **no confiable** — mitigado por el cap de tamaño de mensaje (FU-002). **P-IV** (abstracción de motor) se preserva: el servidor habla a `DocumentBroker`/`DocumentSession` y a blobs **opacos** de `IDocumentStore`, no a tipos de yrs. Ninguna fila locked se reescribe.

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

## US3/M2 — anclajes sobre M1 (refresh 2026-07-11)

> Refresh **scope-limitado**: refina la planificación de US3/M2 sin regenerar el plan ni alterar M0/M1.
> Fuente empírica: `AILOG-2026-07-11-001` (CHARTER-03) §R6/R7/R8 y §Auditoría. El contrato de API v1 del
> servidor no cambia (`contracts/server-api.md`); esto documenta **cómo** el relay consume las superficies
> de concurrencia de M1, que se refinaron en ejecución respecto a la anticipación original del plan.

El relay `Weft.Server` (US3) no toca `ICrdtDoc` ni el motor: se ancla en `DocumentBroker`/`DocumentSession`
(M1, `Weft.Concurrency`) y en `VersionStore` (M0). Cuatro anclajes concretos que el diseño de M2 debe respetar,
derivados de cómo M1 quedó realmente implementado:

1. **Broadcast vía `DocumentSession.UpdateApplied` (perezoso)**. El evento solo computa el delta (2 llamadas
   FFI extra) si hay un handler suscrito. El relay se suscribe **una vez por documento** (no por conexión) y
   difunde el delta a las demás conexiones del doc. Un doc sin clientes no paga el coste del delta.
2. **Refcount de sesiones = no desalojo con clientes vivos**. Mientras una `DocumentSession` viva, el broker
   nunca desaloja su documento. El relay mantiene una sesión por documento activo → un doc con conexiones
   abiertas permanece residente; el desalojo (y su `OnEvicting`→persistencia) solo ocurre cuando la última
   conexión cierra y expira el idle.
3. **Publish y persistencia dentro del turno del actor + `_evicting`-await (R7)**. `IWeftServer.PublishAsync`
   y la persistencia (`IDocumentStore.AppendUpdate`/`SaveSnapshot`) ejecutan **dentro del turno del actor**
   del doc: el state-vector/export es consistente aunque haya tráfico concurrente, garantizando paridad de
   `VersionId` server↔local (P-III). El broker rastrea desalojos en vuelo (`_evicting`) y una reapertura
   espera a que el desalojo **persista** antes de cargar — el relay hereda esta garantía y no debe cargar del
   `IDocumentStore` un snapshot a medio escribir (evita la pérdida de updates que R7 destapó en M1).
4. **Handlers de relay aislados (finding G)**. `NotifySessions` aísla cada handler `UpdateApplied` en
   try/catch: un fallo en el broadcast de una conexión **no faultea el actor** ni afecta a los pares. El relay
   se apoya en esto para el edge case "conexión malformada → cierre 1002 sin impacto en los demás".

**FU-002 — hardening del decoder ante input de red no confiable** (`.straymark/follow-ups-backlog.md`,
`charter-triggered`, trigger "when M2"). US3 es el punto donde el motor recibe bytes de red no confiables:
un update malformado puede amplificar memoria (pocos bytes → asignación gigante en el decoder yrs → posible
abort), tensando P-I/P-II. Mitigación en dos capas: **(a)** cap configurable de tamaño de mensaje en el
framing lib0/y-sync (rechazar antes del decoder); **(b)** límites de recursos por conexión (buffer de
recepción acotado, backpressure) + el path malformed→1002. Se evaluará un bump de `yrs` con validación de
longitud si procede.

**Ejecución de M2 en 3 cortes** (granularidad por *shippable cut*, bridge §granularidad; espeja los 2 cortes
de M0):

| Corte | Tareas | Effort | Entrega |
|---|---|---|---|
| Foundation — códec y-sync + stores + contract suite | T043, T044, T045, T046, T050 | M | Substrato sin red: framing lib0/y-sync unit-tested (cap FU-002 parte a), `IDocumentStore`+InMemory+FileSystem pasando una contract suite compartida. |
| Relay end-to-end + journey US3 | T047, T048, T049, T051, T052 | L | Los 5 criterios del Independent Test de US3 + cliente Tiptap real. Límites de conexión (FU-002 parte b). **Cierra M2.** |
| Adaptadores externos | T053, T054 | S/M | EFCore + Redis contra la contract suite ya escrita. Fuera del journey; no bloquea M2. |

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

Sin violaciones — tabla no aplica.
