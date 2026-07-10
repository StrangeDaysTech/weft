# Hallazgos — Spike 01: fundamento del binding CRDT para .NET

_StrangeDaysTech · 2026-07-09 · brazo principal (yrs). Código desechable; esto es lo que persiste._

## Veredicto de la puerta de decisión (§9): 🟢 **VERDE — "este es nuestro cimiento"**

Construir **nuestra propia capa .NET delgada sobre `yrs` vía un shim C-ABI propio** es un
fundamento **sólido, controlable y de esfuerzo acotado**. Los cinco criterios obligatorios de §8
pasaron con código que corre:

- ✅ `docB` converge a `docA` tras importar el update.
- ✅ Ediciones concurrentes convergen a estado idéntico (intercambio bidireccional).
- ✅ Tipos **XML rich-text alcanzables** vía FFI (`<paragraph></paragraph>`).
- ✅ `SHA-256(export)` **estable y reproducible**; además los blobs de docs convergidos son
  **byte-a-byte idénticos** → el hash del update v1 sirve como id de versión citable cross-nodo.
- ✅ **Sin fugas ni double-free** (AddressSanitizer + LeakSanitizer, 2000 iteraciones, 0 errores).

Y los dos criterios cualitativos también se cumplen: la superficie FFI es pequeña y comprensible
(**9 funciones, 205 LOC Rust, ~180 LOC C#**), y el build es reproducible con el patrón de
empaquetado por RID entendido y demostrado.

**Recomendación de enfoque de binding:** **shim Rust propio + P/Invoke `[LibraryImport]` a mano**
sobre él. Ver objetivo #1 abajo.

---

## Respuesta 1-a-1 a los objetivos de aprendizaje (§4)

### 1. Ergonomía y esfuerzo de la FFI · enfoque de binding

Se implementó el **shim propio** completo y se comparó con **csbindgen** (generado) y con la idea
de **bindear `yffi` directo**. Recomendación: **shim propio + `[LibraryImport]` manual**.

| Enfoque | Qué da | Qué cuesta / hereda |
|---|---|---|
| **Shim propio (elegido)** | Control total de la superficie **en Rust y en C#**; exponemos exactamente lo que nuestra arquitectura necesita (p. ej. `sdt_import_update` devuelve status para content-addressing). Alinea con "diseñar a nuestros patrones". | Escribir ~205 LOC Rust + ~40 LOC de declaraciones P/Invoke. Esfuerzo bajo. |
| **csbindgen (Cysharp)** | Genera las declaraciones P/Invoke desde nuestro `lib.rs`; preserva doc-comments. Elimina el boilerplate de declaración. | Genera **`[DllImport]` clásico con punteros crudos** (`Doc*`, `byte**`), **no** `[LibraryImport]` ni `Span`/`SafeHandle`. **No** genera la capa segura (`CrdtDoc`/`SafeHandle`) — que es justo la que aporta valor. Útil como acelerador de las declaraciones, no como sustituto de la capa. |
| **Bindear `yffi` directo** | Menos código Rust (no hay shim). | Heredamos la superficie C completa de un tercero y su ownership; menos control; y reintroduce el riesgo de reachability (objetivo #9) que el shim elimina. Descartado como base. |

**Conclusión:** el shim propio es el que encaja con el principio de selección de dependencias.
csbindgen queda como **opción de conveniencia** para regenerar las declaraciones tras un bump de
versión (ver objetivo #7), pero la capa segura siempre es nuestra y a mano.

### 2. Ciclo de vida de handles

`YDoc*` se modela con un **`SafeHandle`** (`DocSafeHandle : SafeHandleZeroOrMinusOneIsInvalid`).
Resuelve las tres patologías: **fuga** (finalizer llama a `ReleaseHandle` si se olvida `Dispose`),
**double-free** (el runtime garantiza `ReleaseHandle` una sola vez), **use-after-free** (ref-count
incrementado durante la llamada nativa). El shim libera el doc con `sdt_doc_free` (nunca el GC).
**Fricción encontrada:** ver objetivo #6/nota — `[LibraryImport]` no marshala `SafeHandle`.

### 3. Marshalling de bytes a través de la frontera

- **Entrada** (name/text/update): `ReadOnlySpan<byte>` en `[LibraryImport]` → el generador **fija
  (pin)** el span y pasa el puntero; longitud aparte. **Cero copias** en el envío.
- **Salida** (export/read): out-params `out nint ptr, out nuint len`; se copia una vez a `byte[]`
  gestionado con `Marshal.Copy` y se devuelve la memoria nativa con `sdt_buf_free`. `Span<byte>`
  ayuda del lado de entrada; en salida hay **una copia** inevitable (la memoria es de Rust).

### 4. Propiedad de la memoria entre fronteras (contrato de ownership)

Documentado explícitamente en `lib.rs` y `include/sdt_crdt_ffi.h`:

- Buffers **devueltos** por el shim (export/read) → liberar **solo** con `sdt_buf_free(ptr, len)`.
  Se asignan como `Box<[u8]>` (ptr+len bastan para reconstruir y liberar). **Jamás** el GC/`Marshal.Free*`.
- Buffers **pasados** a shim (name/text/update) → **prestados**; el shim no toma posesión.
- `*mut Doc` → solo `sdt_doc_free`.

Verificado con ASan/LSan: el punto exacto del contrato (`CrdtDoc.TakeOwnedBuffer`) copia y libera
sin que el GC toque memoria nativa. 0 fugas.

### 5. Thread-safety

`yrs` **no es thread-safe**. El shim **no** añade locks (lo deja explícito). El consumidor
serializa por-documento con un **`lock` por instancia** en `CrdtDoc` (un lector/escritor a la vez;
documentos distintos sí en paralelo). Coste: nula concurrencia intra-doc. En la capa .NET final
esto se elevaría a un **modelo actor/canal por documento**; el `lock` basta para el spike y deja
claro el patrón. La `TransactionMut` de `yrs` ya fuerza exclusión a nivel de doc, así que el lock
de C# la complementa evitando panics por transacciones solapadas.

### 6. Manejo de errores a través de FFI

El core cruza la frontera como **códigos de retorno `i32`** (`0` ok, negativos = error). El shim
además envuelve cada cuerpo en **`catch_unwind`** para que un panic de `yrs` **no cruce** la
frontera C (sería UB) → se convierte en `SDT_ERR_PANIC`. En C#, un helper traduce el código a
**`CrdtException`** idiomática. Probado: importar bytes corruptos → `decode_v1` falla →
`SDT_ERR_DECODE (-2)` → excepción `.NET`. (Estados posibles: ok / decode / apply / utf8 / null / panic.)

### 7. Disciplina de version-pinning

Versión fijada con `yrs = "=0.27.2"` desde el inicio. Coste de un bump de minor: regenerar contra
la nueva API (los nombres/firmas de `yrs` cambian entre minors — p. ej. `Transact`, `ReadTxn`,
`get_or_insert_*` están estables en la 0.2x pero no garantizados). Como el shim aísla `yrs` de la
C-ABI, **un cambio de `yrs` solo toca `lib.rs`**, no el C#: la superficie C que exponemos es
nuestra y estable. csbindgen puede **regenerar las declaraciones P/Invoke** automáticamente tras el
bump. Carga de mantenimiento: **baja y bajo nuestro control** (a diferencia de perseguir la API de
un binding de terceros).

### 8. Empaquetado y despliegue de binarios nativos

`cdylib` → `libsdt_crdt_ffi.so` (**1.1 MB** stripped, 11 MB con símbolos). Colocado en
`runtimes/linux-x64/native/` (layout idéntico al de un NuGet nativo). Un **`DllImportResolver`**
(`NativeLibraryResolver.cs`) lo resuelve por RID desde `AppContext.BaseDirectory` — el mismo árbol
funciona en dev y en el paquete. **Patrón multi-RID** entendido: replicar `runtimes/<rid>/native/`
por RID (`win-x64`, `osx-arm64`, `linux-arm64`) y cross-compilar con `cross`/`cargo-zigbuild`; cada
`.so`/`.dll`/`.dylib` va en su carpeta y el mismo resolver elige por `RuntimeInformation.RuntimeIdentifier`.
Demostrado en **1 RID** (linux-x64), que era el objetivo.

### 9. Reachability de tipos rich-text (XML) — **era el riesgo bloqueante**

✅ **Alcanzables sin fricción.** Como el shim está sobre la **API Rust de `yrs`** (no sobre la
C-ABI de `yffi`), los tipos `XmlFragment`/`XmlElement` (`get_or_insert_xml_fragment`,
`XmlElementPrelim`, `GetString`) están **directamente disponibles**; nosotros decidimos exponerlos.
El escenario insertó un `<paragraph>` y lo leyó tras converger en otro doc. **El diseño del shim
neutraliza de raíz** el fallo que tenía el port puro `Ycs` y la incertidumbre de `yffi`. Este es
uno de los argumentos más fuertes a favor del enfoque elegido.

### 10. Encaje con content-addressing

✅ Exportamos el estado a un blob (`encode_state_as_update_v1` con `StateVector::default()`),
lo hasheamos **fuera del core** con `SHA256.HashData` en C#, y el hash es **estable y reproducible**
para el mismo estado. **Hallazgo relevante:** dos docs que convergen al mismo contenido producen el
**mismo blob byte-a-byte** (yrs ordena el update v1 de forma determinista por client-id), así que
`SHA-256(update)` es un **id de versión inmutable citable estable entre nodos** — base directa de
nuestro versionado content-addressed, sin necesidad de una forma canónica adicional. _Caveat a
vigilar en fases posteriores:_ esto vale para el estado completo; si en el futuro se hashean
updates incrementales o snapshots parciales, revalidar el determinismo del orden.

---

## Notas para la fase de construcción de la capa .NET propia

- **Superficie mínima probada**: 9 funciones cubren create/edit(text+xml)/export/import/free.
  La capa final añadirá tipos (Map/Array), observadores/eventos para el sync, subdocs y state-vector
  diffing (`encode_state_as_update` con SV remoto) para updates incrementales — todo alcanzable en `yrs`.
- **`[LibraryImport]` ∌ `SafeHandle`** (SYSLIB1051): mantener el patrón `HandleLease`
  (DangerousAddRef/Release) o encapsularlo en un `source generator`/helper propio.
- **Sync incremental**: el spike usó export de estado completo; para el relay habrá que exponer
  `state_vector` + `encode_state_as_update(sv)` para enviar solo el delta.
- **Empaquetado**: montar CI que cross-compile el `cdylib` por RID y arme el NuGet nativo
  (`runtimes/<rid>/native/`), estilo SkiaSharp.
- **Loro**: no evaluado en este spike (fuera de alcance por decisión). Como el core está abstraído
  tras nuestra C-ABI, reevaluarlo después es de bajo coste.

## Cómo reproducir

```bash
# 1. Shim Rust
cd sdt_crdt_ffi && cargo build --release
cp target/release/libsdt_crdt_ffi.so ../dotnet/Spike01/runtimes/linux-x64/native/

# 2. Escenario end-to-end (imprime PASS/FAIL + hashes; exit!=0 si falla un obligatorio)
cd ../dotnet/Spike01 && dotnet run -c Release

# 3. Memoria (ASan + LeakSanitizer)  [requiere: export SSL_CERT_FILE=/etc/pki/ca-trust/extracted/pem/tls-ca-bundle.pem]
cd ../../sdt_crdt_ffi && RUSTFLAGS="-Zsanitizer=address" \
  cargo +nightly test --release --target x86_64-unknown-linux-gnu --test mem_asan

# 4. Comparación csbindgen (genera C# desde el shim -> csbindgen-compare/generated/)
cd ../csbindgen-compare && cargo build
```
