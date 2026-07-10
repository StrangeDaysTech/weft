# Resultados — Spike 01: fundamento del binding CRDT para .NET

_StrangeDaysTech · análisis de la ejecución · 2026-07-09_

Este documento resume **qué se hizo, qué salió y qué significa**. El detalle técnico por objetivo
está en [`hallazgos-spike-01.md`](./hallazgos-spike-01.md); las mediciones en [`metrics.md`](./metrics.md).

---

## 1. Veredicto

# 🟢 VERDE — "este es nuestro cimiento"

La hipótesis del spike queda **confirmada con código que corre**: podemos envolver el core Rust
`yrs` con una capa .NET propia, delgada y a nuestra medida, con esfuerzo acotado y sin heredar
patrones ajenos. Se aprueba construir la **capa .NET propia** sobre `yrs` como componente,
empezando por el binding y el server relay.

**Ningún criterio obligatorio falló. Ninguna sorpresa bloqueante apareció.** El único riesgo
marcado como potencialmente bloqueante en la spec (reachability de XML rich-text, objetivo #9)
resultó ser de **bajo riesgo por diseño** del enfoque elegido.

---

## 2. Qué se validó (evidencia ejecutable)

Corrida `dotnet run -c Release` → **6 PASS, 0 FAIL obligatorios**:

| # | Criterio (§8) | Resultado observado |
|---|---|---|
| 1 | `docB` converge a `docA` tras importar update | ✅ texto y XML idénticos |
| 2 | Ediciones **concurrentes** convergen (intercambio bidireccional) | ✅ `"[editado-en-B] Hola mundo [editado-en-A]"` en ambos docs |
| 3 | XML rich-text **alcanzable vía FFI** | ✅ `<paragraph></paragraph>` insertado, sincronizado y leído |
| 4 | `SHA-256(export)` **estable/reproducible** | ✅ hash idéntico en re-exports |
| 5 | Sin fugas ni double-free | ✅ ASan + LeakSanitizer: 0 errores en 2000 iteraciones |
| 6 | Errores del core → excepción .NET | ✅ bytes corruptos → `CrdtException(-2 DECODE)` |

**Hallazgo extra (content-addressing, objetivo #10):** dos documentos que convergen al mismo
contenido producen un update v1 **byte-a-byte idéntico** (yrs ordena de forma determinista por
client-id). Por tanto `SHA-256(update)` es un **identificador de versión inmutable citable y
estable entre nodos** — base directa de nuestro versionado content-addressed, sin necesidad de una
forma canónica adicional.

---

## 3. Análisis del esfuerzo (¿fue "acotado"?)

Sí. La superficie completa que ejercita el ciclo end-to-end es pequeña:

| Componente | Tamaño |
|---|---|
| Shim Rust C-ABI (`lib.rs`) | **205 LOC** · 9 funciones `extern "C"` |
| Declaraciones P/Invoke C# (`NativeMethods.cs`) | **39 LOC** |
| Capa segura C# (`SafeHandle` + `CrdtDoc`) | **138 LOC** |
| Build limpio del shim (`cargo build --release`) | **~7.8 s** |
| `.so` resultante | **1.1 MB** stripped (11 MB con símbolos) |

El esfuerzo **no se disparó** en ningún punto — la señal amarilla/roja del time-box (§12) nunca se
activó. La mayor parte del "trabajo" fue de comprensión (contrato de ownership, API de `yrs`), no
de volumen de código.

---

## 4. Fricciones reales encontradas (y cómo se resolvieron)

Estas son el verdadero valor del spike: se conocen **antes** de comprometer semanas.

1. **`[LibraryImport]` no marshala `SafeHandle`** (`SYSLIB1051`) — a diferencia del `[DllImport]`
   clásico. → Se pasa el puntero crudo (`nint`) con `DangerousAddRef`/`DangerousRelease` (patrón
   `HandleLease`), conservando el `SafeHandle` para el ciclo de vida. **Impacto en la capa final:
   bajo**, pero hay que estandarizar el patrón.
2. **`yrs` no es thread-safe** → serialización por-documento con `lock` en C#. En producción se
   elevará a un modelo actor/canal por documento. Coste conocido y contenido.
3. **Entorno**: nightly desactualizado no compilaba `yrs 0.27.2` (`if_let_guard`) → `rustup update`;
   `rustup`/ASan requieren `SSL_CERT_FILE` explícito; sin `valgrind` → se usó **ASan + LeakSanitizer**
   (más preciso para use-after-free/double-free). `.NET 8` no estaba instalado → se targeteó `net10.0`
   (el aprendizaje FFI es independiente de la versión).

Ninguna fricción es estructural ni fuera de nuestro control.

---

## 5. Decisión de enfoque de binding

**Recomendado: shim Rust propio + P/Invoke `[LibraryImport]` escrito a mano.**

- **Shim propio** (elegido): control total de la superficie en Rust **y** en C#; aísla `yrs` de
  nuestra ABI (un bump de `yrs` solo toca `lib.rs`); **elimina el riesgo de reachability** porque
  exponemos los tipos nosotros. Alinea con el principio "diseñar a nuestros patrones".
- **csbindgen**: útil como **acelerador** para regenerar las declaraciones P/Invoke tras un cambio
  de versión, pero genera `[DllImport]` con punteros crudos y **no** produce la capa segura (que es
  justo donde está el valor). Queda como conveniencia, no como base.
- **Bindear `yffi` directo**: descartado como cimiento — heredaríamos superficie y ownership ajenos
  y reintroduciríamos el riesgo XML.

---

## 6. Qué sigue (fuera de este spike)

- Construir la **capa .NET propia** completa (más tipos: Map/Array/subdocs; observadores/eventos).
- **Sync incremental**: exponer `state_vector` + `encode_state_as_update(sv)` para enviar solo el
  delta (el spike usó export de estado completo).
- **Server relay** WebSocket y persistencia (spikes/fases posteriores).
- **Empaquetado multi-RID**: CI que cross-compile el `cdylib` por RID (`win-x64`, `osx-arm64`,
  `linux-arm64`) con `cross`/`cargo-zigbuild` y arme el NuGet nativo, estilo SkiaSharp.
- **Loro**: no evaluado (decisión de alcance); reevaluable a bajo coste porque el core está abstraído
  tras nuestra C-ABI.

---

_El código de este spike es desechable. Lo que persiste es este resultado: sabemos —no creemos—
que el path `.NET → shim Rust propio → yrs` es nuestro cimiento._
