// Real collaborative Tiptap client against the Weft.Server relay (US3 wire-compat gate, T052).
// Tiptap + y-prosemirror + y-websocket, with no Weft-specific adaptation: the relay speaks standard y-sync.
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
    StarterKit.configure({ history: false }), // history/undo is handled by Yjs, not Tiptap
    Collaboration.configure({ document: ydoc }),
    CollaborationCursor.configure({ provider, user: { name, color } }),
  ],
});

provider.on('status', (e) => { document.querySelector('#status').textContent = e.status; });
document.querySelector('#room').textContent = room;
window.__weft = { editor, ydoc, provider }; // for manual inspection in the console
