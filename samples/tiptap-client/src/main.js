// Cliente Tiptap colaborativo real contra el relay Weft.Server (gate de compat del wire de US3, T052).
// Tiptap + y-prosemirror + y-websocket, sin adaptación específica de Weft: el relay habla y-sync estándar.
import { Editor } from '@tiptap/core';
import StarterKit from '@tiptap/starter-kit';
import Collaboration from '@tiptap/extension-collaboration';
import CollaborationCursor from '@tiptap/extension-collaboration-cursor';
import * as Y from 'yjs';
import { WebsocketProvider } from 'y-websocket';

const params = new URLSearchParams(location.search);
const room = params.get('doc') || 'demo';
const url = params.get('url') || 'ws://127.0.0.1:5199/collab';

const ydoc = new Y.Doc();
const provider = new WebsocketProvider(url, room, ydoc);

const name = 'User-' + Math.floor(Math.random() * 1000);
const color = '#' + Math.floor(Math.random() * 0xffffff).toString(16).padStart(6, '0');

const editor = new Editor({
  element: document.querySelector('#editor'),
  extensions: [
    StarterKit.configure({ history: false }), // el historial/undo lo gestiona Yjs, no Tiptap
    Collaboration.configure({ document: ydoc }),
    CollaborationCursor.configure({ provider, user: { name, color } }),
  ],
});

provider.on('status', (e) => { document.querySelector('#status').textContent = e.status; });
document.querySelector('#room').textContent = room;
window.__weft = { editor, ydoc, provider }; // para inspección manual en la consola
