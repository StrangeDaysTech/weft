# Resultados — Spike 03: plomería de versionado (yrs construido vs Loro nativo)

_StrangeDaysTech · análisis de la ejecución · 2026-07-09_

Resumen ejecutivo. Detalle en [`hallazgos-spike-03.md`](./hallazgos-spike-03.md); interfaz en
[`ICrdtEngine-draft.md`](./ICrdtEngine-draft.md).

---

## 1. Veredicto

# 🟢 yrs CIERRA — construir el versionado sobre yrs es acotado y limpio

La última incógnita de la elección de core queda resuelta con evidencia: la plomería de versionado
sobre yrs **no es dolorosa ni frágil**. La tensión GC-vs-versionado, que era el riesgo, **se
neutraliza** con content-addressing de dominio. Se **confirma yrs** y se procede al brief de construcción.

---

## 2. Qué salió (evidencia ejecutable) — 12 PASS, 0 FAIL

| Feature | yrs (construido) | Loro (nativo/genérico) |
|---|---|---|
| Diff v1↔v2 | ✅ reconstruir + text-diff (~30 LOC) → `insertó: [y, calculo]` | ✅ `DiffCalculator` semántico nativo |
| Branch(v1) + merge | ✅ reconstruir + merge CRDT | ✅ genérico + `fork_at` nativo |
| Merge concurrente 2 ramas | ✅ converge (X e Y presentes) | ✅ converge |
| Compactación citable | ✅ store acotado (3527 B/23 vers.), v1 restaurable | ✅ + shallow-snapshot (GC real) |
| Memoria (ASan/LSan) | ✅ 0 errores | ✅ 0 errores |

**Y el hallazgo central, medido:** la **tensión GC de Yjs es real** (insertar 1000/borrar 900 → export
**142 B con GC** vs **1042 B con `skip_gc`** = **7.3× mayor**), pero **nuestra estrategia la evita**:
blobs content-addressed por versión + GC activo → doc vivo acotado, versiones citables auto-contenidas,
**nunca `skip_gc`**.

## 3. El hallazgo que decide

**La capa de versionado es engine-agnóstica.** La MISMA capa de dominio (~58 LOC) corrió **idéntica**
sobre yrs y Loro, usando solo **6 primitivas del núcleo**. El versionado no depende de primitivas
nativas del motor: diff, branch/merge y compactación se construyen encima, portables. Eso significa:
1. **Construir sobre yrs es limpio** (no peleamos con el framework; el time-box no se desbordó).
2. La ventaja de plomería de Loro (diff semántico, `fork_at`, shallow-snapshot) es **real pero no
   decisiva** — lo genérico basta y es correcto.
3. Si algún día se cambia de motor, la capa de versionado **sobrevive** (solo cambia el adaptador).

## 4. Dónde Loro es más elegante (honesto)

- **Diff semántico nativo** (`DiffCalculator`): más rico que nuestro text-diff; para **diff estructural
  rich-text** (árbol ProseMirror) ahorraría más trabajo (sobre yrs habría que construir tree-diff).
- **`fork_at`** y **shallow-snapshot con GC de historia**: primitivas nativas de una llamada.
- **`ChangeMeta`** (peer/timestamp): base de blame más directa.

Ninguna es decisiva: todas se construyen sobre yrs con esfuerzo acotado, y la tensión GC —el único
riesgo estructural— no aplica a nuestro diseño content-addressed.

## 5. Bonus a favor de yrs

Para versiones content-addressed, los blobs de yrs (update con GC) salieron **~4× más pequeños** que
los snapshots de Loro (3527 vs 14499 B / 23 versiones), porque el snapshot de Loro incluye historia
(podable con shallow-snapshot, pero es trabajo extra).

## 6. Qué sigue

- **Proceder al brief de construcción** de la capa .NET de versionado sobre yrs, usando el
  **`ICrdtEngine`** validado aquí (6 primitivas núcleo + capacidad opcional).
- Mantener la abstracción del motor: el brazo Loro de este spike demuestra que la capa es portable,
  así que el gatillo de reevaluación de Spike 02 sigue vivo a bajo coste.
- Diseñar el diff **estructural** (rich-text) como capa de dominio sobre reconstrucción (el punto donde
  Loro ahorraría más — cuantificarlo si el editor lo exige).

---

_El código es desechable. Persisten: la tabla de esfuerzo/fricción, el veredicto (🟢 yrs cierra) y el
borrador de `ICrdtEngine`. Este spike convierte en evidencia la última incógnita de la elección de core._
