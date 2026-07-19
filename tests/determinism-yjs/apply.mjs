// Cross-implementation determinism gate (CHARTER-07/T058, research R13 (b)).
//
// Applies the shared corpus (corpus.json) with Yjs JS using FIXED client IDs, syncs the replicas until
// they converge, and emits the SHA-256 of replica 0's v1 export (encodeStateAsUpdate). The goal is to
// compare that hash against the one yrs produces with the SAME corpus: if they match, determinism is
// "by format" and not "by accident of this version of yrs" (P-III).
//
// NON-BLOCKING / PROMOTABLE: today the yrs binding does not expose fixing the client_id (CreateDoc()
// with no parameter), so byte-identical parity with yrs is gated on that capability (follow-up). This
// harness already runs and emits the Yjs hash; once yrs can fix the client_id, it is compared and
// promoted to a gate. If WEFT_GOLDEN_HASH (the yrs hash) is passed, it compares and reports — without
// failing (informative).

import * as Y from 'yjs';
import { createHash } from 'node:crypto';
import { existsSync, readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { basename, dirname, join } from 'node:path';

const here = dirname(fileURLToPath(import.meta.url));

// Corpus by argument (`node apply.mjs [corpus-unicode.json]`), default corpus.json (FU-012).
const corpusFile = process.argv[2] ?? 'corpus.json';
const corpus = JSON.parse(readFileSync(join(here, corpusFile), 'utf8'));

// Golden key by corpus: corpus.json → "ascii", corpus-unicode.json → "unicode".
const goldenKey = basename(corpusFile) === 'corpus-unicode.json' ? 'unicode' : 'ascii';

// One replica = one Y.Doc with a fixed clientID from the corpus.
const replicas = corpus.clientIds.map((id) => {
  const doc = new Y.Doc();
  doc.clientID = id; // fix before any operation
  return doc;
});

// Apply each operation to its replica.
for (const step of corpus.ops) {
  const text = replicas[step.replica].getText(corpus.type);
  if (step.op === 'ins') {
    text.insert(step.index, step.text);
  } else if (step.op === 'del') {
    text.delete(step.index, step.len);
  } else {
    throw new Error(`unknown op: ${step.op}`);
  }
}

// Sync all-against-all until convergence (same scheme as Weft.Determinism.Tests).
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

// Hash of replica 0's v1 export (already converged).
const update = Y.encodeStateAsUpdate(replicas[0]);
const hash = createHash('sha256').update(Buffer.from(update)).digest('hex');

const finalText = replicas[0].getText(corpus.type).toString();
console.log(`Yjs  corpus:          ${corpusFile}`);
console.log(`Yjs  converged text:  "${finalText}"`);
console.log(`Yjs  export SHA-256:  ${hash}`);

// Self-check against the committed golden (golden.json[goldenKey]). Catches Yjs drift: if Yjs bumps
// and changes the encoding, the emitted hash stops matching the golden. The real BLOCKING yrs↔Yjs
// parity assertion lives in Weft.Determinism.Tests (per-PR); this job is informative
// (`continue-on-error` in release.yml). WEFT_GOLDEN_HASH is still supported as an override.
const goldenPath = join(here, 'golden.json');
const override = process.env.WEFT_GOLDEN_HASH;
const golden = override
  ?? (existsSync(goldenPath) ? JSON.parse(readFileSync(goldenPath, 'utf8'))[goldenKey] : undefined);
if (golden) {
  if (golden === hash) {
    console.log(`✓ Yjs hash matches the committed golden (${goldenKey}).`);
  } else {
    console.log(`⚠ DIVERGENCE (non-blocking): Yjs differs from the golden (${goldenKey}).`);
    console.log(`   golden: ${golden}`);
    console.log('   Possible Yjs drift (bump with encoding impact). Regenerate golden. See README.');
  }
} else {
  console.log('ℹ No golden.json nor WEFT_GOLDEN_HASH: informative harness (only emits the Yjs hash).');
}
