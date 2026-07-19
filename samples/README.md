# Weft samples

Runnable examples, one per core user story of the library. Each is self-contained and
mirrors a scenario from the feature quickstart
([`specs/001-weft-crdt-versioning/quickstart.md`](../specs/001-weft-crdt-versioning/quickstart.md)).

| Sample | Demonstrates | Needs a server? | Run |
| --- | --- | --- | --- |
| [`Weft.Sample.Versioning`](./Weft.Sample.Versioning) | **US1** — local content-addressed versioning (publish → diff → checkout → branch → merge) | No | `dotnet run --project samples/Weft.Sample.Versioning` |
| [`Weft.Sample.Server`](./Weft.Sample.Server) | **US3** — the y-sync relay server (`Weft.Server`) over WebSocket | It *is* the server | `dotnet run --project samples/Weft.Sample.Server` |
| [`tiptap-client`](./tiptap-client) | **US3** — a real Tiptap editor + a headless wire-compat check against the relay | Yes — the sample server | `cd samples/tiptap-client && npm install && npm run dev` |

## Prerequisites

- **.NET 10 SDK** (`net10.0`) for the two C# samples.
- **Node.js + npm** for `tiptap-client` only.

The native shims (`weft-yrs-ffi`, `weft-loro-ffi`) are built and resolved automatically by
the project references — no manual native setup.

## The 60-second tour

```bash
# 1. Local versioning — no network, prints a full publish/diff/branch/merge journey.
dotnet run --project samples/Weft.Sample.Versioning

# 2. Start the relay (leave it running); serves ws://127.0.0.1:5199/collab/{docId}.
dotnet run --project samples/Weft.Sample.Server

# 3. In another shell: prove the wire is Yjs-compatible without a browser.
cd samples/tiptap-client && npm install && npm run check

# 4. Or open the real editor and collaborate across browser tabs.
npm run dev   # then open http://localhost:5173/?doc=demo in 2+ tabs
```

## Notes

- The relay listens on **port 5199** by default; the Tiptap client points there out of the box,
  so no configuration is needed when running both as-is.
- `Weft.Sample.Server` persists documents to disk (`FileSystemDocumentStore`), so they survive a
  restart. The two US3 samples together cover live convergence, awareness (presence), incremental
  reconnect, and persistence.
- Not every user story has an interactive demo: **US2** (concurrency at scale) and **US5**
  (dual-engine Loro) are exercised by the load tests and the dual-engine test suite rather than a
  sample app.
