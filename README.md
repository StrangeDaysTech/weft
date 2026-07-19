<!-- SPDX-License-Identifier: Apache-2.0 -->

# Weft

**Real-time collaboration (CRDT) and content-addressed document versioning, for .NET.**

Weft is a .NET library that wraps the [`yrs`](https://github.com/y-crdt/y-crdt) CRDT core (the official Rust port of [Yjs](https://github.com/yjs/yjs)) through an in-house binding, and builds on top an **immutable versioning** layer (content-identified) plus a **synchronization server** — designed to our own patterns, not inheriting those of a third-party binding.

> **The name.** `yrs` is pronounced *"wires"*. The **weft** is the set of threads woven across the warp on a loom: the engine provides the threads, Weft weaves them into collaboration. A nod to the [Strange Days Tech](https://strangedays.tech/en) family of projects — Arborist, StrayMark, Weft.

## Status

✅ **Release-ready (milestone M3).** Milestones M0 (versioning + dual-engine), M1 (concurrency at scale) and M2
(relay server) are closed and green in CI. The multi-RID native NuGet packaging (`linux-x64`,
`linux-arm64`, `win-x64`, `osx-arm64`) is built and verified by *pack-smoke*. Packages are
**published to NuGet.org with the M3 release cut** (the pipeline is one `workflow_dispatch` away).

## What it offers (the component boundary)

Weft is a **reusable building block**, not an application. It provides:

- **.NET binding to `yrs`** via an in-house C-ABI shim (`ICrdtEngine` / `ICrdtDoc`), with a safe lifecycle (`SafeHandle`), an explicit ownership contract, and verified memory safety.
- **Content-addressed document versioning:** publish versions (`SHA-256` of the byte-deterministic export), diff, branches/merge and compaction — **engine-agnostic** (proven running identically over `yrs` and Loro).
- **Synchronization:** incremental sync (state-vector + delta), a WebSocket relay server (Yjs protocol), presence/awareness and persistence adapters.
- **Optional `INativeVersioning` capability** for engines that offer it (e.g. Loro), keeping the engine replaceable.

What Weft is **not**: the domain content model of a concrete app, a frontend editor, or business logic. Those live in the consumer (e.g. a collaborative content editor for an LMS), which **depends on Weft, never the other way around**.

## Installation

Weft ships as NuGet packages with the native binaries included per RID — **no manual steps**:
the correct binary (`linux-x64`/`linux-arm64`/`win-x64`/`osx-arm64`) resolves itself from
`runtimes/<rid>/native/`.

```bash
dotnet add package Weft.Core         # binding + base versioning (yrs engine included)
dotnet add package Weft.Versioning   # content-addressed publish / diff / branch / merge
dotnet add package Weft.Server       # y-sync WebSocket relay for ASP.NET Core (optional)
```

Packages: `Weft.Core`, `Weft.Versioning`, `Weft.Server`, `Weft.Loro` (alternative engine) and the
persistence adapters `Weft.Server.Persistence.EFCore` / `.Redis`.

## Quickstart

**Edit and version** a document (content-addressed, `SHA-256` of the deterministic export):

```csharp
using Weft;
using Weft.Versioning;
using Weft.Versioning.Blobs;
using Weft.Yrs;

ICrdtEngine engine = YrsEngine.Instance;
var store = new VersionStore(engine, new InMemoryBlobStore());

using ICrdtDoc doc = engine.CreateDoc();
doc.InsertText("title", 0, "hello weft");
VersionId v1 = await store.PublishAsync(doc);   // v1 = citable hash of the content

doc.InsertText("title", 10, " in real time");
VersionId v2 = await store.PublishAsync(doc);

TextDiff diff = await store.DiffAsync(v1, v2, "title");    // Equal/Insert/Delete segments
using ICrdtDoc restored = await store.CheckoutAsync(v1);   // reconstruct any version
```

**Serve live collaboration** — a WebSocket relay compatible with standard Yjs clients
(`y-websocket`/`y-prosemirror`/Tiptap), in ASP.NET Core:

```csharp
builder.Services.AddWeftServer(options => { /* Engine, Broker, ... */ });
builder.Services.AddSingleton<IWeftAuthorizer, MyAuthorizer>();       // consumer's access decision
builder.Services.AddWeftRedisDocumentStore("localhost:6379");         // or EFCore / FileSystem / InMemory

app.MapWeft("/ws");   // WebSocket endpoint: /ws/{docId}
```

End-to-end walkthrough (edit → publish → serve → Tiptap client) in
[`samples/`](./samples) and in `specs/001-weft-crdt-versioning/quickstart.md`.

## Engine

- **Core: `yrs`** (adopted, not reimplemented). Chosen for continuity (the Yjs format has multiple independent implementations → a fork = choosing among implementations), the maturity of the editor ecosystem (Tiptap/ProseMirror + `y-prosemirror`) and fork-safety.
- **Recommended editor client:** [Tiptap](https://tiptap.dev) (on ProseMirror) + `y-prosemirror`, connected to Weft's relay server.
- **Dual-path:** [Loro](https://github.com/loro-dev/loro) stays as a living alternative behind the abstraction; switching engines = switching the adapter, not the versioning layer.

## Repository structure

```text
weft/
├── LICENSE                     # Apache-2.0
├── README.md
├── native/                     # cargo workspace (a single build covers both shims)
│   ├── weft-yrs-ffi/           # cdylib: C-ABI shim over yrs (default engine)
│   │   ├── src/lib.rs          # Cargo.toml pins yrs = "=X.Y.Z"
│   │   ├── include/weft_ffi.h  # C header (ownership contract; source of truth)
│   │   └── tests/mem_asan.rs   # memory harness (ASan/LSan)
│   └── weft-loro-ffi/          # cdylib: C-ABI shim over Loro (dual-path)
├── src/
│   ├── Weft.Core/              # ICrdtEngine/ICrdtDoc, P/Invoke [LibraryImport], SafeHandle, broker
│   ├── Weft.Versioning/        # publish/diff/branch/merge/compact (content-addressed)
│   ├── Weft.Server/            # WebSocket relay, awareness, persistence
│   ├── Weft.Server.Persistence.EFCore/   # IDocumentStore adapter over EF Core
│   ├── Weft.Server.Persistence.Redis/    # IDocumentStore adapter over Redis/Valkey
│   └── Weft.Loro/              # optional adapter (INativeVersioning) — dual-path
├── tests/
├── samples/                    # runnable examples (versioning, relay server, Tiptap client)
├── docs/                       # architecture.md, api/, spikes/
├── .specify/                   # GitHub Spec Kit (constitution, spec/plan/tasks)
├── .straymark/                 # StrayMark: Charters (scope + telemetry), AILOG/AIDEC/ADR, external audits
└── .github/workflows/          # CI: multi-RID build, tests, ASan, fuzzing, determinism
```

## Architecture

[**docs/architecture.md**](docs/architecture.md) explains how everything fits together: the module
map, the FFI boundary and its **memory ownership contract**, the sync flow, the content-addressed
versioning model and the known limits. It's the recommended read before integrating Weft or touching
the shim. The per-package reference is in [docs/api/](docs/api/README.md).

## Development

Weft is built **spec-driven and governed by Charters** — the pairing of two complementary frameworks:

- **[GitHub Spec Kit](https://github.com/github/spec-kit) defines *what* to build.** The flow is
  **Spec → Plan → Tasks → Implement**: design (`/specify`, `/plan`) and implementation runs
  (`/tasks`, `/implement`) are carried out in Claude Code. The active feature lives in
  `specs/001-weft-crdt-versioning/`, and the project **constitution**
  (`.specify/memory/constitution.md`, binding — 6 principles) sits at the root of that flow. See the
  **design brief** in `weft-design-brief.md`.
- **[StrayMark](https://github.com/StrangeDaysTech/straymark) governs *how* the work is executed and
  recorded.** It matters because it keeps the *why* of every decision inside the repository, not in
  oral folklore. Its unit of work is the **Charter**: a bounded block that pairs declarative *ex-ante*
  scope (what's in, what's out, risks) with *ex-post* telemetry (real effort vs. estimate). Technical
  decisions are recorded as **AILOG / AIDEC / ADR** documents under `.straymark/`, and milestone
  closes require a **multi-model external audit** before merging. See
  [`STRAYMARK.md`](./STRAYMARK.md) for the documentation governance rules.

**How the two integrate.** Spec Kit produces the task list (`tasks.md`, T001…T063); StrayMark groups
those tasks into **Charters** for execution. Each Charter declares its scope up front, the
implementation lands in a PR with the **6 constitutional gates** green (see
[`CONTRIBUTING.md`](./CONTRIBUTING.md) and the [Gates](docs/architecture.md#gates) section), decisions
are logged as AILOG/AIDEC/ADR, and the Charter closes with telemetry. The constitution lives on the
Spec Kit side but is **enforced** as executable CI gates on the StrayMark side — the bridge between
the two. Governance model: [`GOVERNANCE.md`](./GOVERNANCE.md).

Toolchain: Rust (with `yrs` pinned) · .NET SDK 10 (LTS) · native packaging per RID (Linux/Windows/macOS, x64/arm64).

## Security

The relay (`Weft.Server`) caps untrusted network input. If you ingest **untrusted** CRDT bytes
**directly** through the API (`weft_doc_load` / `apply_update` / `export_since`) outside the relay, apply
a size cap and a process memory limit — the `yrs` decoder can amplify memory. Details and vulnerability
reporting: [`SECURITY.md`](./SECURITY.md) and [GOVERNANCE.md § Security](./GOVERNANCE.md#seguridad).

## License

[Apache-2.0](./LICENSE) © 2026 [Strange Days Tech](https://strangedays.tech/en). A permissive library with an explicit patent grant; reciprocal to the MIT engines it builds on (`yrs`, Loro).
