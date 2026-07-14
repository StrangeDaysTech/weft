<!-- SPDX-License-Identifier: Apache-2.0 -->

# determinism-yjs — gate de determinismo cross-implementación

Parte del gate de determinismo de Weft (research **R13**, constitución **P-III**). Complementa a
`Weft.Determinism.Tests` (que fija el determinismo *cross-RID*: mismo corpus → mismo hash en todos los
RIDs de la matriz) con la dimensión **cross-implementación**: aplicar el **mismo corpus** con **Yjs JS** y
comparar el export contra el de **yrs**.

Si los hashes coinciden, el determinismo de Weft es **por formato** (el encoding v1 de Yjs/yrs), no un
accidente de esta versión de `yrs`. Esa es la garantía que distingue "content-addressing estable" de
"estable hasta el próximo bump del motor" (ver R16).

## Estado: no-bloqueante / promovible

Adoptado como **job no-bloqueante primero, promovible a gate** (R13). Hoy la paridad byte-idéntica con yrs
está **gated en client IDs deterministas**: el binding de yrs no expone fijar el `client_id`
(`ICrdtEngine.CreateDoc()` no toma parámetro y el shim FFI no lo soporta), así que yrs asigna un `client_id`
no controlable y su export no es reproducible entre corridas/implementaciones. **Follow-up FU-012**: exponer
`CreateDoc(clientId)` en el FFI + binding, emitir el hash golden de yrs para este corpus, y promover este job
a comparación con aserción.

Mientras tanto el harness **corre y emite el hash de Yjs** para el corpus compartido (`corpus.json`), y —si
se le pasa `WEFT_GOLDEN_HASH` con el hash de yrs— **compara y reporta** la (dis)paridad **sin fallar**
(informativo, insumo para R16).

## Uso

```bash
npm install
npm test                                  # emite el hash de Yjs (informativo)
WEFT_GOLDEN_HASH=<hash-de-yrs> npm test    # compara con yrs (no falla ante divergencia)
```

## Corpus

`corpus.json` es la fuente única de la secuencia: `clientIds` fijos por réplica, `ops` (`ins`/`del` sobre
un texto `body`) y `syncPasses`. La variante con texto **unicode** (que ejercita los índices UTF-16, ver R6)
es una extensión promovible tras estabilizar la paridad ASCII base.
