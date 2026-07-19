# Weft.Server.Persistence.Redis

An `IDocumentStore` adapter for [Weft.Server](https://www.nuget.org/packages/Weft.Server) backed by
Redis (via StackExchange.Redis): the snapshot as a string + updates as a list, with atomic compaction
via a transaction.

Part of **[Weft](https://github.com/StrangeDaysTech/weft)** — real-time CRDT collaboration and
content-addressed document versioning for .NET.

## Install

```bash
dotnet add package Weft.Server.Persistence.Redis
```

## What it provides

- **`AddWeftRedisDocumentStore(...)`** — registers the adapter.
- Compatible with any Redis-wire server (Redis, Valkey).
- Passes the same shared `IDocumentStore` contract suite as the in-memory, filesystem, and EF Core
  adapters, so behavior is interchangeable.

## Links

- Repository & docs: <https://github.com/StrangeDaysTech/weft>
- Architecture: <https://github.com/StrangeDaysTech/weft/blob/main/docs/architecture.md>

Licensed under Apache-2.0.
