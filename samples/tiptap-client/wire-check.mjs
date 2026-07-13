// Validación HEADLESS de compatibilidad del wire (retira R1 sin navegador): dos Y.Doc reales de Yjs se
// conectan al relay Weft.Server vía y-websocket y deben converger tras ediciones cruzadas. Si yrs (servidor) y
// Yjs (cliente) no fueran compatibles a nivel de update binario, esto divergiría o daría timeout.
//
// Uso: arrancar el sample server (dotnet run --project samples/Weft.Sample.Server), luego `npm run check`.
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
      else if (Date.now() - t0 > ms) { clearInterval(iv); reject(new Error('timeout esperando: ' + label)); }
    }, 20);
  });
}

const a = connect();
const b = connect();
let code = 0;
try {
  await waitFor(() => a.provider.wsconnected && b.provider.wsconnected, 'conexión de ambos clientes');

  // A edita → B debe converger.
  a.doc.getText(FIELD).insert(0, 'Hello from A. ');
  await waitFor(() => b.doc.getText(FIELD).toString().includes('Hello from A.'), 'B recibe la edición de A');

  // B edita → A debe converger.
  const t = b.doc.getText(FIELD);
  t.insert(t.length, 'And B too.');
  await waitFor(() => a.doc.getText(FIELD).toString().includes('And B too.'), 'A recibe la edición de B');

  const ta = a.doc.getText(FIELD).toString();
  const tb = b.doc.getText(FIELD).toString();
  if (ta !== tb) throw new Error(`divergencia: A="${ta}" B="${tb}"`);

  console.log('✓ convergencia Yjs (y-websocket) ↔ Weft.Server (yrs):', JSON.stringify(ta));
} catch (e) {
  console.error('✗ FALLO de compat del wire:', e.message);
  code = 1;
} finally {
  a.provider.destroy();
  b.provider.destroy();
  setTimeout(() => process.exit(code), 100);
}
