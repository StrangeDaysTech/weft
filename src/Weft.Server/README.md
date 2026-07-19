# Weft.Server

A y-sync WebSocket relay for ASP.NET Core: it speaks the standard Yjs `y-sync` protocol, so existing
editor clients (Tiptap + `y-prosemirror`, `y-websocket`) connect with no adaptation.

Part of **[Weft](https://github.com/StrangeDaysTech/weft)** — real-time CRDT collaboration and
content-addressed document versioning for .NET.

## Install

```bash
dotnet add package Weft.Server
```

## What it provides

- **DI wiring** — `AddWeftServer(...)` + `MapWeft("/path")` → `path/{docId}`.
- **`IWeftAuthorizer`** — the consumer's access hook (`Deny` / `ReadOnly` / `ReadWrite`); Weft never
  implements identity itself.
- **`IDocumentStore`** — durable persistence of opaque blobs; `InMemory` / `FileSystem` included,
  EF Core and Redis adapters in separate packages.
- Ephemeral awareness/presence, incremental reconnect (state vectors), and per-connection backpressure.

```csharp
builder.Services.AddWeftServer();
builder.Services.AddSingleton<IWeftAuthorizer, MyAuthorizer>();
app.MapWeft("/collab");   // ws://host/collab/{docId}
```

## Links

- Repository & docs: <https://github.com/StrangeDaysTech/weft>
- Architecture: <https://github.com/StrangeDaysTech/weft/blob/main/docs/architecture.md>

Licensed under Apache-2.0.
