# Weft.Core

Safe .NET binding to [`yrs`](https://github.com/y-crdt/y-crdt) (the Rust core of Yjs) via an in-house
C-ABI shim, plus the engine abstractions and the concurrency broker the rest of Weft builds on.

Part of **[Weft](https://github.com/StrangeDaysTech/weft)** — real-time CRDT collaboration and
content-addressed document versioning for .NET.

## Install

```bash
dotnet add package Weft.Core
```

The native `yrs` shim ships in the package and is resolved per RID (`linux-x64`, `linux-arm64`,
`win-x64`, `osx-arm64`) automatically — no manual native setup.

## What it provides

- **`ICrdtEngine` / `ICrdtDoc`** — create/load documents, text operations, export
  state / state-vector / delta, apply updates.
- **Safe native lifecycle** — `SafeHandle` + an explicit ownership contract; the GC never touches
  native memory and no panic crosses the C boundary.
- **Concurrency** (`Weft.Concurrency`) — `DocumentBroker` / `DocumentSession`, one actor per document
  for safe concurrent access at scale. `ICrdtDoc` itself is not thread-safe.

## Links

- Repository & docs: <https://github.com/StrangeDaysTech/weft>
- Architecture: <https://github.com/StrangeDaysTech/weft/blob/main/docs/architecture.md>
- Per-package API overview: <https://github.com/StrangeDaysTech/weft/blob/main/docs/api/README.md>

Licensed under Apache-2.0.
