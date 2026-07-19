# Weft.Versioning

Engine-agnostic, content-addressed document versioning for .NET: publish immutable versions
identified by the `SHA-256` of a deterministic export, then diff, branch, merge, and check out — over
a pluggable blob store.

Part of **[Weft](https://github.com/StrangeDaysTech/weft)** — real-time CRDT collaboration and
content-addressed document versioning for .NET.

## Install

```bash
dotnet add package Weft.Versioning   # brings in Weft.Core
```

## What it provides

- **`VersionStore`** — `PublishAsync` → `VersionId` (same content → same id, always),
  `CheckoutAsync`, `DiffAsync` (word-level), `BranchAsync`, `Merge`.
- **`IBlobStore`** — `InMemoryBlobStore`, `FileSystemBlobStore` (per-version, content-addressed,
  deduplicated). On read it re-hashes and verifies integrity.
- **Engine-agnostic** — depends only on the `Weft.Core` abstractions, so it runs identically over
  `yrs` and the optional Loro engine.

```csharp
var store = new VersionStore(YrsEngine.Instance, new InMemoryBlobStore());
VersionId v1 = await store.PublishAsync(doc);
TextDiff diff = await store.DiffAsync(v1, v2, "title");
```

## Links

- Repository & docs: <https://github.com/StrangeDaysTech/weft>
- Architecture: <https://github.com/StrangeDaysTech/weft/blob/main/docs/architecture.md>

Licensed under Apache-2.0.
