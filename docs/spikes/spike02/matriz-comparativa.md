# Matriz comparativa yrs vs Loro (§8)

_yrs = línea base medida en Spike 01 · Loro = medido en Spike 02 · 2026-07-09 · mismo entorno,
mismo enfoque de binding (shim propio + `[LibraryImport]`), mismo escenario end-to-end._

| Dimensión | **yrs** (Spike 01) | **Loro** (Spike 02) | Ganador |
|---|---|---|---|
| Convergencia simple + concurrente | ✅ | ✅ `"[B] Hola mundo [A]"` | empate |
| Rich-text alcanzable vía FFI | ✅ `XmlFragment` → `<paragraph>` | ✅ `LoroText`+marca → `{bold:true}` | empate |
| Memoria segura (ASan/LSan) | ✅ 0 errores (2000 iter) | ✅ 0 errores (2000 iter) | empate |
| **Content-addressing determinista** | ✅ update v1 **byte-idéntico** cross-nodo | ✅ snapshot **y** updates **byte-idénticos** cross-nodo | empate |
| Manejo de errores (blob corrupto) | ✅ → excepción `-2 DECODE` | ✅ → excepción `-2 DECODE` | empate |
| Funciones C-ABI (paridad) | 9 | 9 (+3 sondas = 12) | empate |
| LOC Rust shim | 205 | 268 (incluye 3 sondas) | ~empate |
| LOC C# (NativeMethods / wrapper) | 39 / 138 | 37 / 99 | Loro (sin lock) |
| Tamaño `.so` (stripped) | **1.1 MB** | 3.8 MB | **yrs** |
| Build limpio (release) | **~7.8 s** | ~25 s | **yrs** |
| **Thread-safety** | ❌ no thread-safe → `lock` por doc en C# | ✅ `LoroDoc` es Send+Sync (locking interno) | **Loro** |
| Version-pinning (dolor de bump) | 0.x; bump toca solo `lib.rs` | 1.x **encoding estable** desde oct-2024; bump toca solo `lib.rs` | **Loro** (promesa 1.0) |
| **Historial/versionado nativo** | básico (construimos la capa) | ✅ **time-travel** (checkout a frontiers), shallow snapshot, fork/branch, DAG | **Loro** |
| Substrato rich-text | `XmlFragment`/`XmlElement` (mapea directo a ProseMirror) | `LoroText`+marcas (Fugue) + `LoroTree`; `loro-prosemirror` existe | matiz (ver nota) |
| Rough perf (import/export) | (no cronometrado formalmente) | 5000 inserts + export(10KB) + import = **8 ms** | Loro (declara 10-100×; no decisivo) |
| Ergonomía FFI (binding) | shim + `[LibraryImport]` (elegido) | shim igual de limpio; UniFFI genera **31.885 LOC** pero hereda superficie + acople triple de versiones | empate (shim); yrs en madurez de patrón |
| **Estatus estratégico** | Kevin Jahns co-mant.; adopción verificable (y-sweet, Jupyter, BlockNote); formato Yjs estándar de facto | **bus-factor 1** (solo zxch3n activo); **sin funding** (solo GH Sponsors); **adopción en prod no verificable** (~121× menos npm que Yjs); **sin .NET oficial** | **yrs (decisivo)** |

## Lectura de la matriz

- **Paridad técnica: empate casi total.** Loro pasa exactamente los mismos criterios obligatorios
  que yrs, incluido el determinismo de content-addressing (nuestra fila crítica) — que Loro cumple
  **tanto en snapshot como en updates**, igual de bien que yrs.
- **Loro gana en 3 dimensiones técnicas:** thread-safety (sin lock por doc), promesa de encoding
  estable 1.0, e **historial/versionado nativo** (time-travel + shallow snapshot + branch).
- **yrs gana en:** tamaño/tiempo de build, y —de forma **decisiva**— en **estatus estratégico**.
- **La ventaja de historial de Loro NO es decisiva para nosotros** porque: (a) Spike 01 ya demostró
  que yrs nos da IDs content-addressed deterministas gratis, sobre los que construimos NUESTRA capa
  de versionado (que necesitamos igual, para citabilidad/inmutabilidad a nuestro modelo); (b) el
  motor está abstraído tras nuestra interfaz (ADR-4), así que el time-travel de Loro sería un plus
  agradable, no un pilar que yrs no pueda igualar con nuestra capa.
