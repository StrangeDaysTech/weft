# Weft.Server.Persistence.EFCore

An `IDocumentStore` adapter for [Weft.Server](https://www.nuget.org/packages/Weft.Server) backed by
Entity Framework Core: opaque per-document blob persistence (a consolidated snapshot + accumulated
updates) with transactional compaction.

Part of **[Weft](https://github.com/StrangeDaysTech/weft)** — real-time CRDT collaboration and
content-addressed document versioning for .NET.

## Install

```bash
dotnet add package Weft.Server.Persistence.EFCore
```

## What it provides

- **`AddWeftEFCoreDocumentStore(...)`** — registers the adapter.
- **Provider-agnostic** — the consumer configures the EF Core provider (SQLite, PostgreSQL,
  SQL Server, …) via `DbContextOptions`.
- Passes the same shared `IDocumentStore` contract suite as the in-memory, filesystem, and Redis
  adapters, so behavior is interchangeable.

## Links

- Repository & docs: <https://github.com/StrangeDaysTech/weft>
- Architecture: <https://github.com/StrangeDaysTech/weft/blob/main/docs/architecture.md>

Licensed under Apache-2.0.
