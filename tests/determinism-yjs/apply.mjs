// Gate de determinismo cross-implementación (CHARTER-07/T058, research R13 (b)).
//
// Aplica el corpus compartido (corpus.json) con Yjs JS usando client IDs FIJOS, sincroniza las
// réplicas hasta converger, y emite el SHA-256 del export v1 (encodeStateAsUpdate) de la réplica 0.
// El objetivo es comparar ese hash contra el que produce yrs con el MISMO corpus: si coinciden, el
// determinismo es "por formato" y no "por accidente de esta versión de yrs" (P-III).
//
// NO-BLOQUEANTE / PROMOVIBLE: hoy el binding de yrs no expone fijar el client_id (CreateDoc() sin
// parámetro), así que la paridad byte-idéntica con yrs está gated en esa capacidad (follow-up). Este
// harness ya corre y emite el hash de Yjs; cuando yrs pueda fijar client_id, se compara y se promueve
// a gate. Si se pasa WEFT_GOLDEN_HASH (el hash de yrs), compara y reporta — sin fallar (informativo).

import * as Y from 'yjs';
import { createHash } from 'node:crypto';
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';

const here = dirname(fileURLToPath(import.meta.url));
const corpus = JSON.parse(readFileSync(join(here, 'corpus.json'), 'utf8'));

// Una réplica = un Y.Doc con clientID fijo del corpus.
const replicas = corpus.clientIds.map((id) => {
  const doc = new Y.Doc();
  doc.clientID = id; // fijar antes de cualquier operación
  return doc;
});

// Aplicar cada operación a su réplica.
for (const step of corpus.ops) {
  const text = replicas[step.replica].getText(corpus.type);
  if (step.op === 'ins') {
    text.insert(step.index, step.text);
  } else if (step.op === 'del') {
    text.delete(step.index, step.len);
  } else {
    throw new Error(`op desconocida: ${step.op}`);
  }
}

// Sincronizar todas-contra-todas hasta converger (mismo esquema que Weft.Determinism.Tests).
for (let pass = 0; pass < (corpus.syncPasses ?? 2); pass++) {
  for (const target of replicas) {
    const sv = Y.encodeStateVector(target);
    for (const source of replicas) {
      if (source !== target) {
        Y.applyUpdate(target, Y.encodeStateAsUpdate(source, sv));
      }
    }
  }
}

// Hash del export v1 de la réplica 0 (ya convergida).
const update = Y.encodeStateAsUpdate(replicas[0]);
const hash = createHash('sha256').update(Buffer.from(update)).digest('hex');

const finalText = replicas[0].getText(corpus.type).toString();
console.log(`Yjs  texto convergido: "${finalText}"`);
console.log(`Yjs  export SHA-256:   ${hash}`);

const golden = process.env.WEFT_GOLDEN_HASH;
if (golden) {
  if (golden === hash) {
    console.log('✓ PARIDAD cross-implementación: el hash de Yjs coincide con el de yrs.');
  } else {
    console.log('⚠ DIVERGENCIA (no-bloqueante): Yjs vs yrs difieren.');
    console.log(`   yrs (golden): ${golden}`);
    console.log('   Insumo para R16 (bump del motor) / promoción del gate. Ver README.');
  }
} else {
  console.log('ℹ Sin WEFT_GOLDEN_HASH (hash de yrs): harness informativo. Paridad con yrs = paso promovible.');
}
