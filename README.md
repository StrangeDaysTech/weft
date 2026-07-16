<!-- SPDX-License-Identifier: Apache-2.0 -->

# Weft

**Colaboración en tiempo real (CRDT) y versionado content-addressed de documentos, para .NET.**

Weft es una librería .NET que envuelve el core CRDT [`yrs`](https://github.com/y-crdt/y-crdt) (el port oficial en Rust de [Yjs](https://github.com/yjs/yjs)) mediante un binding propio, y construye encima una capa de **versionado inmutable** (identificado por contenido) más un **servidor de sincronización** — diseñada a nuestros patrones, no heredando los de un binding de terceros.

> **El nombre.** `yrs` se pronuncia *"wires"* (hilos). El **weft** son los hilos que se tejen a través de la urdimbre en un telar: el motor aporta los hilos, Weft los teje en colaboración. Un guiño a la familia de proyectos de [Strange Days Tech](https://strangedays.tech/es) — Arborist, StrayMark, Weft.

## Estado

✅ **Release-ready (hito M3).** Los hitos M0 (versionado + dual-engine), M1 (concurrencia a escala) y M2
(servidor relay) están cerrados y verdes en CI. El empaquetado NuGet nativo multi-RID (`linux-x64`,
`linux-arm64`, `win-x64`, `osx-arm64`) está construido y verificado por *pack-smoke*. Los paquetes se
**publican a NuGet.org con el corte de release M3** (el pipeline queda a un `workflow_dispatch` de distancia).

## Qué ofrece (frontera del componente)

Weft es un **building block reutilizable**, no una aplicación. Provee:

- **Binding .NET a `yrs`** vía un shim C-ABI propio (`ICrdtEngine` / `ICrdtDoc`), con ciclo de vida seguro (`SafeHandle`), contrato de ownership explícito y seguridad de memoria verificada.
- **Versionado de documento content-addressed:** publicar versiones (`SHA-256` del export byte-determinista), diff, ramas/merge y compactación — **engine-agnóstico** (probado corriendo idéntico sobre `yrs` y Loro).
- **Sincronización:** sync incremental (state-vector + delta), servidor relay WebSocket (protocolo Yjs), presencia/awareness y adaptadores de persistencia.
- **Capacidad opcional `INativeVersioning`** para motores que la ofrezcan (p. ej. Loro), manteniendo el motor reemplazable.

Lo que **no** es Weft: modelo de contenido de dominio de una app concreta, editor de frontend, ni lógica de negocio. Esas viven en el consumidor (p. ej. un LMS), que **depende de Weft, nunca al revés**.

## Instalación

Weft se distribuye como paquetes NuGet con los binarios nativos incluidos por RID — **sin pasos manuales**:
el binario correcto (`linux-x64`/`linux-arm64`/`win-x64`/`osx-arm64`) se resuelve solo desde
`runtimes/<rid>/native/`.

```bash
dotnet add package Weft.Core         # binding + versionado base (motor yrs incluido)
dotnet add package Weft.Versioning   # publish / diff / branch / merge content-addressed
dotnet add package Weft.Server       # relay WebSocket y-sync para ASP.NET Core (opcional)
```

Paquetes: `Weft.Core`, `Weft.Versioning`, `Weft.Server`, `Weft.Loro` (motor alternativo) y los adaptadores
de persistencia `Weft.Server.Persistence.EFCore` / `.Redis`.

## Quickstart

**Editar y versionar** un documento (content-addressed, `SHA-256` del export determinista):

```csharp
using Weft;
using Weft.Versioning;
using Weft.Versioning.Blobs;
using Weft.Yrs;

ICrdtEngine engine = YrsEngine.Instance;
var store = new VersionStore(engine, new InMemoryBlobStore());

using ICrdtDoc doc = engine.CreateDoc();
doc.InsertText("titulo", 0, "hola weft");
VersionId v1 = await store.PublishAsync(doc);   // v1 = hash citable del contenido

doc.InsertText("titulo", 9, " en tiempo real");
VersionId v2 = await store.PublishAsync(doc);

TextDiff diff = await store.DiffAsync(v1, v2, "titulo");   // segmentos Equal/Insert/Delete
using ICrdtDoc restored = await store.CheckoutAsync(v1);   // reconstruye cualquier versión
```

**Servir colaboración en vivo** — relay WebSocket compatible con clientes Yjs estándar
(`y-websocket`/`y-prosemirror`/Tiptap), en ASP.NET Core:

```csharp
builder.Services.AddWeftServer(options => { /* Engine, Broker, ... */ });
builder.Services.AddSingleton<IWeftAuthorizer, MyAuthorizer>();       // decisión de acceso del consumidor
builder.Services.AddWeftRedisDocumentStore("localhost:6379");         // o EFCore / FileSystem / InMemory

app.MapWeft("/ws");   // endpoint WebSocket: /ws/{docId}
```

Recorrido end-to-end (editar → publicar → servir → cliente Tiptap) en
[`samples/`](./samples) y en `specs/001-weft-crdt-versioning/quickstart.md`.

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

## Arquitectura

[**docs/architecture.md**](docs/architecture.md) explica cómo encaja todo: mapa de módulos, la
frontera FFI y su **contrato de ownership de memoria**, el flujo de sync, el modelo de versionado
content-addressed y los límites conocidos. Es la lectura recomendada antes de integrar Weft o de
tocar el shim. La referencia por paquete está en [docs/api/](docs/api/README.md).

## Desarrollo

Spec-driven, con [GitHub Spec Kit](https://github.com/github/spec-kit): **Spec → Plan → Tasks → Implement**. El diseño (`/specify`, `/plan`) y las tandas de implementación (`/tasks`, `/implement`) se realizan en Claude Code. Ver el **brief de diseño** en `docs/weft-design-brief.md`.

Toolchain: Rust (con `yrs` pinneado) · .NET SDK 10 (LTS) · empaquetado nativo por RID (Linux/Windows/macOS, x64/arm64).

## Seguridad

El relay (`Weft.Server`) capea el input de red no confiable. Si ingieres bytes CRDT **no confiables directamente**
por la API (`weft_doc_load` / `apply_update` / `export_since`) fuera del relay, aplica un cap de tamaño y un límite
de memoria del proceso — el decoder de `yrs` puede amplificar memoria. Detalle y reporte de vulnerabilidades:
[GOVERNANCE.md § Seguridad](./GOVERNANCE.md#seguridad).

## Licencia

[Apache-2.0](./LICENSE) © 2026 [Strange Days Tech](https://strangedays.tech/es). Librería permisiva con concesión explícita de patentes; reciproca a los motores MIT sobre los que se apoya (`yrs`, Loro).
