# Hallazgos — Spike 03: plomería de versionado sobre yrs vs primitivas nativas de Loro

_StrangeDaysTech · 2026-07-09 · Código desechable; persisten la tabla de esfuerzo/fricción, el
veredicto y el borrador de `ICrdtEngine`._

## Veredicto de la puerta de decisión (§9): 🟢 **yrs CIERRA**

Construir nuestras features de versionado sobre `yrs` es **acotado y limpio**. La hipótesis de la
Ronda 13 —"la tensión GC-vs-versionado es manejable con snapshots content-addressed de dominio, así
que la ventaja de continuidad + editor de yrs no se erosiona"— **se confirma con código que corre**.

- **12 PASS, 0 FAIL.** El diff, branch+merge (incl. merge concurrente de 2 ramas) y la compactación
  citable funcionan y son **correctos** sobre yrs.
- **La tensión GC es real (medida: 7.3× más grande sin GC) pero NO nos muerde:** nuestra estrategia
  (blobs content-addressed por versión + GC activo en el doc vivo) la **evita por completo** — nunca
  necesitamos `skip_gc`/`gc:false`.
- **La capa de versionado es engine-agnóstica:** la MISMA capa de dominio (~58 LOC) corre **idéntica**
  sobre yrs y Loro. El versionado no depende de primitivas nativas del motor.
- Loro tiene primitivas nativas genuinamente más elegantes (diff semántico, `fork_at`, shallow-snapshot
  con GC real), pero **NO son decisivas**: lo construido sobre yrs es limpio, correcto y acotado.

→ Se **confirma yrs** como core (gana por continuidad multi-implementación + madurez de editor +
fork-safety) y se procede al brief de construcción. El motor sigue abstraído tras `ICrdtEngine`.

---

## Tabla de esfuerzo/fricción (§8)

| Feature | **yrs (construido)** | **Loro (nativo)** |
|---|---|---|
| **Diff v1↔v2** | Reconstruir ambos estados + **~30 LOC de text-diff propio** (LCS de palabras). Correcto bajo concurrencia (diffea estados materializados). Sin fricción; el coste es escribir el diff. | `DiffCalculator` nativo: diff **semántico** (`Retain{14}`, insert…) sin reconstruir. Más rico de fábrica. |
| **Compactación citable** | Blobs content-addressed por versión + **GC activo**. Store **acotado** (23 versiones = 3527 B). v1 restaurable. **La tensión GC NO aparece** (no usamos `skip_gc`). | Shallow-snapshot con GC real de historia (2648→824 B). Nativo, pero el snapshot base es más grande (incluye historia). |
| **Branch + merge** | Reconstruir doc desde blob → editar → merge CRDT (import). **Plomería trivial**, correcta (incl. 2 ramas concurrentes). | `fork_at(frontiers)` + import. Nativo, una llamada. Igual de correcto. |
| **Blame por rango** | Feasibility: yrs **no** expone `PermanentUserData` en el port Rust → habría que rastrear ops en capa de dominio. Coste medio, no bloqueante. | `get_change(id) -> ChangeMeta` (peer/timestamp/lamport) nativo → base de blame más directa. |
| **Funciones C-ABI nuevas** | **4** (`doc_new_no_gc`*, `text_delete`, `export_state_vector`, `export_update_since`) · ~90 LOC Rust | **3** (`text_delete`, `probe_native_diff`, `probe_native_branch`) · ~110 LOC Rust |
| **LOC capa de dominio C#** | **~58** (VersioningService 28 + TextDiff 30) — engine-agnóstica | Las mismas ~58 + glue mínimo para las primitivas nativas |
| **Superficie `ICrdtEngine`** | **6 primitivas** bastan (NewDoc/LoadDoc/InsertText/DeleteText/ReadText/ExportState/Import) | Igual núcleo + capacidad opcional `INativeVersioning` |
| **Rough perf** | Reconstruir+diff/branch: instantáneo a esta escala. Delta incremental: 523 B→29 B. | Diff/fork_at nativos: instantáneos. |

_(*) `doc_new_no_gc` existe SOLO para medir la tensión GC; la estrategia real no lo usa._

---

## Respuesta 1-a-1 a los objetivos (§4)

### 1. Diff entre dos versiones
yrs **no** ofrece diff semántico nativo: se reconstruyen ambos estados (desde sus blobs) y se diffea
el texto en la capa de dominio (**~30 LOC**, `TextDiff.cs`). **Correcto bajo concurrencia** porque
compara estados materializados, no ops (probado: diff tras merges concurrentes refleja la realidad
fusionada). Loro **sí** tiene `DiffCalculator` nativo (diff semántico rico). **Veredicto:** para
texto, el coste yrs es trivial y correcto; para **diff estructural rich-text** (árbol ProseMirror),
Loro ahorraría más — pero también se puede construir sobre yrs (reconstruir + tree-diff), a más LOC.

### 2. Compactación de historia citable — el corazón de la tensión GC
**Hallazgo central, medido:** en yrs, insertar 1000 chars y borrar 900 produce un export de **142 B
con GC activo** vs **1042 B con `skip_gc`** (= **7.3× mayor**; sin GC los tombstones se acumulan sin
cota). El `Snapshot`/time-travel nativo de Yjs **exige `skip_gc=true`** → crecimiento sin cota. **PERO
nuestra estrategia lo evita:** guardamos un **blob content-addressed por versión publicada**
(`SHA-256(export)`), auto-contenido y restaurable, con **GC activo** en el doc vivo. Resultado: store
**acotado** (23 versiones = 3527 B), cada versión **restaurable/citable**, y **nunca tocamos `skip_gc`**.
La tensión GC de Yjs es real pero **no nos aplica** con este diseño. Loro ofrece shallow-snapshot con
GC de historia (nativo), pero no lo necesitamos.

### 3. Rama de borrador + merge
Sobre yrs: reconstruir doc desde el blob de la versión → editar aislado → **merge CRDT** (import del
estado de la rama en el doc principal). **Plomería trivial y correcta**, incluida la fusión concurrente
de dos ramas (ambas ediciones presentes, convergencia idéntica). Loro: `fork_at(frontiers)` nativo +
import. Ambos correctos; la diferencia de esfuerzo es marginal.

### 4. Blame por rango (esbozo)
yrs Rust **no** expone `PermanentUserData` (existe en Yjs JS, no en el port) → el blame se construiría
rastreando autoría en la capa de dominio (medio esfuerzo, factible). Loro expone `ChangeMeta`
(peer/timestamp/lamport) por `Change` → base de blame más directa. **Ambos** requieren capa de dominio;
Loro parte con algo más de metadata. No bloqueante en ninguno.

### 5. Superficie de `ICrdtEngine`
El versionado necesita **solo 6 primitivas** del motor (crear/cargar/insertar/borrar/leer/exportar/
importar). Diff, branch, compactación se construyen **encima**, sin tocar el motor. Esa **pequeñez es
el hallazgo**: la plomería de versionado vive en la capa de dominio, portable entre motores. Ver el
borrador en [`ICrdtEngine-draft.md`](./ICrdtEngine-draft.md) y `dotnet/Spike03/ICrdtEngine.cs`.

---

## Lo que esto significa para la decisión de core

- **La ventaja de plomería de Loro es real pero no decisiva.** Su diff semántico y `fork_at` son más
  elegantes, pero lo construido sobre yrs es limpio, correcto y **portable** — y la tensión GC, que era
  el riesgo, **se neutraliza** con content-addressing de dominio (que necesitamos igual para citabilidad).
- **yrs conserva sus ventajas** (continuidad multi-implementación del formato Yjs, madurez de
  y-prosemirror/Tiptap, fork-safety) **sin erosión** por el lado del versionado.
- **Bonus a favor de yrs:** para versiones content-addressed, los blobs de yrs (update con GC) salieron
  **~4× más pequeños** que los snapshots de Loro (3527 vs 14499 B para 23 versiones), porque el snapshot
  de Loro incluye historia (podable con shallow-snapshot, pero ese es trabajo extra).

## Memoria
ASan + LeakSanitizer sobre ambos shims extendidos: **0 fugas, 0 errores**. El stress del escenario C#
ejercitó las rutas nuevas (delete, state-vector, probes) miles de veces sin crash.

## Cómo reproducir
```bash
export SSL_CERT_FILE=/etc/pki/ca-trust/extracted/pem/tls-ca-bundle.pem
cd sdt_crdt_ffi_yrs && cargo build --release && cp target/release/libsdt_crdt_ffi.so ../dotnet/Spike03/runtimes/linux-x64/native/
cd ../sdt_crdt_ffi_loro && cargo build --release && cp target/release/libsdt_crdt_ffi_loro.so ../dotnet/Spike03/runtimes/linux-x64/native/
cd ../dotnet/Spike03 && dotnet run -c Release          # -> 12 PASS, 0 FAIL
# ASan:
cd ../../sdt_crdt_ffi_yrs && RUSTFLAGS="-Zsanitizer=address" cargo +nightly test --release --target x86_64-unknown-linux-gnu --test mem_asan
```
Versiones: `yrs =0.27.2` · `loro =1.13.6` · .NET SDK 10.0.109 (`net10.0`) · Rust stable 1.96 / nightly 1.99.
