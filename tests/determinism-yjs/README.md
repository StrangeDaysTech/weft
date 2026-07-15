<!-- SPDX-License-Identifier: Apache-2.0 -->

# determinism-yjs — gate de determinismo cross-implementación

Parte del gate de determinismo de Weft (research **R13**, constitución **P-III**). Complementa a
`Weft.Determinism.Tests` (que fija el determinismo *cross-RID*: mismo corpus → mismo hash en todos los
RIDs de la matriz) con la dimensión **cross-implementación**: aplicar el **mismo corpus** con **Yjs JS** y
comparar el export contra el de **yrs**.

Si los hashes coinciden, el determinismo de Weft es **por formato** (el encoding v1 de Yjs/yrs), no un
accidente de esta versión de `yrs`. Esa es la garantía que distingue "content-addressing estable" de
"estable hasta el próximo bump del motor" (ver R16).

## Estado: PROMOVIDO a aserción per-PR (yrs), CHARTER-09/FU-012

La paridad byte-idéntica yrs↔Yjs **se cumple** y está **aserida en cada PR** (bloqueante de facto). FU-012 la
habilitó exponiendo client-ids deterministas en el FFI de yrs (`weft_doc_new_with_client_id` + `YrsEngine.
CreateDoc(clientId)`, ABI v2). La aserción vive en **`tests/Weft.Determinism.Tests`**
(`Yrs_export_matches_yjs_golden`): aplica el corpus con yrs y compara el SHA-256 del export contra el **hash
golden de Yjs** comprometido en `golden.json` — corre en el job `test` existente, costo marginal ~0.

Este harness Node es el **complemento informativo** (job `determinism-yjs` en `release.yml`, `continue-on-error`):
emite el hash de Yjs de ambos corpus y lo **self-checkea contra `golden.json`** — así, si Yjs bumpea y cambia el
encoding, el hash deja de coincidir con el golden y se detecta el drift (insumo para R16). La verificación de
paridad real (yrs == Yjs) es el test .NET; este job protege la vigencia del golden.

**Alcance yrs-only**: el gate es yrs↔Yjs (misma familia de formato). Loro es otro formato y no participa; la
promoción de la siembra de client-id a capacidad cross-engine (Loro vía `set_peer_id`) es un follow-up aparte.

## Uso

```bash
npm install
npm test                                   # emite el hash de Yjs de ambos corpus + self-check vs golden.json
node apply.mjs corpus-unicode.json         # solo la variante unicode
WEFT_GOLDEN_HASH=<hash> node apply.mjs ...  # override manual del golden (debug)

# Regenerar golden.json tras un cambio DELIBERADO de corpus (no un drift):
node apply.mjs corpus.json                 # copiar el hash a golden.json["ascii"]
node apply.mjs corpus-unicode.json         # copiar el hash a golden.json["unicode"]
```

## Corpus

`corpus.json` (ASCII) y `corpus-unicode.json` (texto no-ASCII: BMP acentuado, CJK y astrales/emoji — ejercita
los índices **UTF-16**, ver R6) son la fuente única de la secuencia: `clientIds` fijos por réplica, `ops`
(`ins`/`del` sobre un texto `body`, índices en UTF-16 code units) y `syncPasses`. `golden.json` guarda el hash
de Yjs de cada uno (`ascii`/`unicode`).
