# Weft.Sample.Server

**User Story 3 (M2): the y-sync relay server.**

A minimal ASP.NET Core host (~30 lines) that stands up the Weft relay: a WebSocket endpoint
compatible with the Yjs ecosystem (`y-websocket` / Tiptap), with no Weft-specific client adaptation.

## Run

```bash
dotnet run --project samples/Weft.Sample.Server
```

Then it serves:

```text
ws://127.0.0.1:5199/collab/{docId}
```

Wait for the log line `Weft sample relay on http://127.0.0.1:5199 …` before connecting a client.

## What it shows

- **`AddWeftServer()` + `MapWeft("/collab")`** — the whole server wiring.
- **Authorization extension point (`IWeftAuthorizer`).** This demo ships a `DemoAuthorizer` that
  grants `ReadWrite` to everyone — **not for production**. A real consumer decides access from the
  `HttpContext` using its own identity (JWT/cookies). Without an `IWeftAuthorizer` registered,
  `MapWeft` fails at startup by design.
- **Durable persistence** via `FileSystemDocumentStore` — documents are written under
  `bin/<config>/net10.0/weft-data/` and survive a server restart.

## Configuration

| Env var            | Default                 | Purpose                       |
| ------------------ | ----------------------- | ----------------------------- |
| `WEFT_SAMPLE_URLS` | `http://127.0.0.1:5199` | Address(es) the host binds to |

## Pair it with a client

- **Browser editor + headless wire check:** [`../tiptap-client`](../tiptap-client) — a real Tiptap
  editor plus `npm run check`, the headless proof that yrs (server) and Yjs (client) updates are
  binary-interchangeable.
