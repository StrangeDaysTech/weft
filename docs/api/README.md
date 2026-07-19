<!-- SPDX-License-Identifier: Apache-2.0 -->

# Weft — per-package API overview

Weft ships as several layered NuGet packages. Reference only the ones you need; dependencies point
**toward the core**, never the other way.

> This page is the **per-package** reference: what to install and which types to use. For how they fit
> together — FFI boundary, memory ownership contract, sync flow, versioning model — see
> [**Weft architecture**](../architecture.md).

| Package | Depends on | For |
| --- | --- | --- |
| **Weft.Core** | — (brings the native `yrs` engine) | Safe binding + abstractions + concurrency broker |
| **Weft.Versioning** | Weft.Core | Content-addressed versioning (publish/diff/branch/merge) |
| **Weft.Server** | Weft.Core, Weft.Versioning | y-sync WebSocket relay for ASP.NET Core |
| **Weft.Server.Persistence.EFCore** | Weft.Server | `IDocumentStore` adapter over EF Core |
| **Weft.Server.Persistence.Redis** | Weft.Server | `IDocumentStore` adapter over Redis/Valkey |
| **Weft.Loro** | Weft.Core | Alternative CRDT engine (Loro) behind the same abstraction |

## Weft.Core

The binding and the abstractions. Brings the native `weft_yrs_ffi` shim (`yrs` engine) packaged per RID.

- **`ICrdtEngine`** (`YrsEngine.Instance`) — document factory: `CreateDoc()`, `LoadDoc(blob)`.
- **`ICrdtDoc`** — CRDT document: `InsertText`/`DeleteText`/`GetText`, `ExportState`/`ExportStateVector`/
  `ExportUpdateSince`/`ApplyUpdate`. **Not thread-safe**: serialized access (or via the broker).
- **Concurrency** (`Weft.Concurrency`): `DocumentBroker`/`DocumentSession` — one actor per document
  (single-reader) for safe concurrent access at scale.
- **Errors**: `WeftException` hierarchy (`CorruptUpdateException`, `WeftEngineException`) + `WeftErrorCode`.
- Native lifecycle: `SafeHandle` + ownership contract; the GC never touches native memory.

## Weft.Versioning

Immutable content-addressed versioning, **engine-agnostic** (does not reference `yrs`/Loro types).

- **`VersionStore(engine, IBlobStore)`** — `PublishAsync(doc)` → `VersionId` (`SHA-256` of the
  deterministic export), `CheckoutAsync`/`BranchAsync`/`DiffAsync`, `Merge`.
- **`IBlobStore`** — `InMemoryBlobStore`, `FileSystemBlobStore` (per-version blobs, citable).
- **`TextDiff`** — word-level diff (`Equal`/`Insert`/`Delete` segments).

## Weft.Server

WebSocket relay compatible with standard Yjs clients (`y-websocket`/`y-prosemirror`/Tiptap), with no adaptation.

- **DI**: `AddWeftServer(options)` (fails at startup without an `IWeftAuthorizer`) + `MapWeft(path)` → `path/{docId}`.
- **`IWeftAuthorizer`** — the consumer's access hook (`Deny`/`ReadOnly`/`ReadWrite`); Weft never decides identity.
- **`IDocumentStore`** — durable state of **opaque** blobs (`LoadAsync`/`AppendUpdateAsync`/`SaveSnapshotAsync`);
  `InMemory`/`FileSystem` implementations included, `EFCore`/`Redis` in separate packages.
- **`IWeftServer`** — `PublishAsync` (server↔local `VersionId` parity), `GetConnectionCountAsync`,
  `DisconnectAllAsync`.

## Weft.Server.Persistence.EFCore / .Redis

External, interchangeable `IDocumentStore` adapters (they pass the same contract suite). EF Core is
provider-agnostic (SQLite/PostgreSQL/SQL Server); Redis is Valkey-compatible. DI registration:
`AddWeftEFCoreDocumentStore(...)` / `AddWeftRedisDocumentStore(...)`.

## Weft.Loro

An alternative CRDT engine (Loro) behind the same `ICrdtEngine`/`ICrdtDoc` — living proof that the
abstraction is replaceable (P-IV). It also offers the optional `INativeVersioning` capability for native
versioning.

---

> Per-symbol reference documentation: the packages include their XML comments (IntelliSense) and
> SourceLink (navigation to source). Formal contracts in `specs/001-weft-crdt-versioning/contracts/`.
