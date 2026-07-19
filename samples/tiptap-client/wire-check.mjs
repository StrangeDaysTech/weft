// HEADLESS wire-compatibility check (retires R1 without a browser): two real Yjs Y.Docs connect to the
// Weft.Server relay via y-websocket and must converge after cross edits. If yrs (server) and Yjs (client)
// were not compatible at the binary update level, this would diverge or time out.
//
// Usage: start the sample server (dotnet run --project samples/Weft.Sample.Server), then `npm run check`.
import * as Y from 'yjs';
import { WebsocketProvider } from 'y-websocket';
import WS from 'ws';

const URL = process.env.WEFT_URL || 'ws://127.0.0.1:5199/collab';
const ROOM = 'headless-' + Date.now();
const FIELD = 'content';

function connect() {
  const doc = new Y.Doc();
  const provider = new WebsocketProvider(URL, ROOM, doc, { WebSocketPolyfill: WS, connect: true });
  return { doc, provider };
}

function waitFor(cond, label, ms = 5000) {
  return new Promise((resolve, reject) => {
    const t0 = Date.now();
    const iv = setInterval(() => {
      if (cond()) { clearInterval(iv); resolve(); }
      else if (Date.now() - t0 > ms) { clearInterval(iv); reject(new Error('timeout waiting for: ' + label)); }
    }, 20);
  });
}

const a = connect();
const b = connect();
let code = 0;
try {
  await waitFor(() => a.provider.wsconnected && b.provider.wsconnected, 'both clients to connect');

  // A edits → B must converge.
  a.doc.getText(FIELD).insert(0, 'Hello from A. ');
  await waitFor(() => b.doc.getText(FIELD).toString().includes('Hello from A.'), 'B to receive A\'s edit');

  // B edits → A must converge.
  const t = b.doc.getText(FIELD);
  t.insert(t.length, 'And B too.');
  await waitFor(() => a.doc.getText(FIELD).toString().includes('And B too.'), 'A to receive B\'s edit');

  const ta = a.doc.getText(FIELD).toString();
  const tb = b.doc.getText(FIELD).toString();
  if (ta !== tb) throw new Error(`divergence: A="${ta}" B="${tb}"`);

  console.log('✓ convergence Yjs (y-websocket) ↔ Weft.Server (yrs):', JSON.stringify(ta));
} catch (e) {
  console.error('✗ wire-compat FAILURE:', e.message);
  code = 1;
} finally {
  a.provider.destroy();
  b.provider.destroy();
  setTimeout(() => process.exit(code), 100);
}
