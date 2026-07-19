# Weft · Tiptap client + wire-compat validation

Sample for US3 (CHARTER-05): a real collaborative **Tiptap** editor against the `Weft.Server` relay,
plus a **headless** wire-compatibility check with `yjs`/`y-websocket`. Demonstrates that the relay
interoperates with the Yjs ecosystem **without adaptation** — the server speaks standard `y-sync`.

## Requirements

- The sample server running (listens on `ws://127.0.0.1:5199/collab/{docId}`):

  ```bash
  dotnet run --project ../Weft.Sample.Server
  ```

- Node.js + npm. Install deps once: `npm install`.

## 1) Headless validation (no browser) — wire-compat gate

Two real Yjs `Y.Doc`s connect via `y-websocket` and must converge after cross edits:

```bash
npm run check
# ✓ convergencia Yjs (y-websocket) ↔ Weft.Server (yrs): "Hello from A. And B too."
```

Exits 0 if they converge; 1 if they diverge or time out. This is the evidence that yrs updates
(server) and Yjs updates (client) are interchangeable at the binary level.

## 2) Manual validation with Tiptap (quickstart §US3)

```bash
npm run dev            # Vite serves the editor at http://localhost:5173
```

1. Open `http://localhost:5173/?doc=demo` in **2+ tabs** (or browsers).
2. Type in one tab → the text appears live in the others (convergence).
3. Peer cursors/names are visible (awareness); closing a tab makes its cursor disappear (retirement).
4. Reload a tab → it recovers state from the relay (delta on reconnect).
5. Restart the sample server → documents persist (`FileSystemDocumentStore`).

The `docId` is the `?doc=` query parameter; change the base URL with `?url=ws://host:port/collab`
if needed.
