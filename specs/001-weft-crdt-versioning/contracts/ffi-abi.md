# Contract — C-ABI del shim `weft-yrs-ffi`

**Crate**: `native/weft-yrs-ffi` (cdylib `weft_yrs_ffi`) · **Header de referencia**: `include/weft_ffi.h` · **Hito**: M0

ABI propia y estable (constitución P-I): la superficie es nuestra; un bump de `yrs` cambia
`lib.rs`, jamás esta ABI. Evolución del shim del Spike 01/03 (`sdt_*` → `weft_*`, + state-vector).
El shim de Loro (`weft-loro-ffi`) replica esta misma ABI con prefijo `weft_loro_` más los
probes nativos.

## Reglas transversales (no negociables)

1. **Panics**: toda función envuelve su cuerpo en `catch_unwind`; un panic retorna
   `WEFT_ERR_PANIC`, jamás cruza la frontera (UB).
2. **Ownership de buffers**:
   - Buffers **de salida** (`out_ptr`/`out_len`): asignados por el shim como `Box<[u8]>`;
     el llamador DEBE liberarlos con `weft_buf_free(ptr, len)` — exactamente una vez, y
     nunca con el GC/`Marshal`.
   - Buffers **de entrada** (`ptr` + `len`): prestados; el shim no toma posesión ni retiene
     el puntero después del retorno.
   - `WeftDoc*`: se libera SOLO con `weft_doc_free` (exactamente una vez).
3. **Thread-safety**: NINGUNA función que reciba `WeftDoc*` es thread-safe respecto al mismo
   doc (yrs no es Send+Sync). El llamador serializa por documento. Funciones sin doc
   (`weft_buf_free`) son thread-safe.
4. **Strings**: entradas de texto son UTF-8 (`ptr+len`, sin NUL terminator); UTF-8 inválido
   retorna `WEFT_ERR_UTF8`.
5. **Índices**: `u32` en unidades del motor (UTF-16 code units en yrs para texto — la capa C#
   es responsable de la conversión de índices si expone otra semántica); fuera de rango
   retorna `WEFT_ERR_OUT_OF_BOUNDS`.

## Códigos de estado (`int32_t`)

| Código | Valor | Significado | Excepción .NET |
|---|---|---|---|
| `WEFT_OK` | 0 | éxito | — |
| `WEFT_ERR_NULL_ARG` | -1 | puntero requerido nulo | `ArgumentNullException` (defensa; C# valida antes) |
| `WEFT_ERR_DECODE` | -2 | blob/update no decodificable | `CorruptUpdateException` |
| `WEFT_ERR_APPLY` | -3 | fallo aplicando update | `WeftEngineException(Apply)` |
| `WEFT_ERR_UTF8` | -4 | texto de entrada no UTF-8 | `WeftEngineException(Utf8)` |
| `WEFT_ERR_OUT_OF_BOUNDS` | -5 | índice/longitud fuera de rango | `ArgumentOutOfRangeException` |
| `WEFT_ERR_PANIC` | -127 | panic capturado en el shim | `WeftEngineException(Panic)` |

## Funciones (12)

```c
// ── Ciclo de vida del documento ──────────────────────────────────────────────
// Doc nuevo vacío (GC del motor SIEMPRE activo — no existe variante no-GC en la ABI).
int32_t weft_doc_new(WeftDoc** out_doc);

// Reconstruye desde blob exportado. Errores: DECODE, NULL_ARG, PANIC.
int32_t weft_doc_load(const uint8_t* blob, uintptr_t blob_len, WeftDoc** out_doc);

// Libera el doc. Idempotencia NO garantizada: llamar exactamente una vez. NULL es no-op.
void    weft_doc_free(WeftDoc* doc);

// ── Texto por campo nombrado ─────────────────────────────────────────────────
int32_t weft_text_insert(WeftDoc* doc, const uint8_t* field, uintptr_t field_len,
                         uint32_t index, const uint8_t* text, uintptr_t text_len);
int32_t weft_text_delete(WeftDoc* doc, const uint8_t* field, uintptr_t field_len,
                         uint32_t index, uint32_t len);
// Lee el campo completo; out buffer es UTF-8 (liberar con weft_buf_free).
int32_t weft_text_read (WeftDoc* doc, const uint8_t* field, uintptr_t field_len,
                        uint8_t** out_ptr, uintptr_t* out_len);

// ── Estado y sincronización ──────────────────────────────────────────────────
// Export determinista del estado completo (update v1 vs state-vector vacío).
int32_t weft_doc_export_state(WeftDoc* doc, uint8_t** out_ptr, uintptr_t* out_len);
// State vector del doc.
int32_t weft_doc_state_vector(WeftDoc* doc, uint8_t** out_ptr, uintptr_t* out_len);
// Delta de cambios que el poseedor de `sv` no conoce. Errores: DECODE (sv inválido).
int32_t weft_doc_export_since(WeftDoc* doc, const uint8_t* sv, uintptr_t sv_len,
                              uint8_t** out_ptr, uintptr_t* out_len);
// Aplica update/estado de otra réplica. Errores: DECODE, APPLY.
int32_t weft_doc_apply_update(WeftDoc* doc, const uint8_t* update, uintptr_t update_len);

// ── Memoria ──────────────────────────────────────────────────────────────────
// Libera un buffer devuelto por el shim (ptr+len deben ser exactamente los recibidos).
void    weft_buf_free(uint8_t* ptr, uintptr_t len);

// ── Diagnóstico ──────────────────────────────────────────────────────────────
// Versión de la ABI (entero monotónico) para detectar desalineación paquete/binario.
uint32_t weft_abi_version(void);

// ── Test hooks (SOLO compilación con feature de Cargo `test-hooks`) ──────────
// Provoca un panic! interno deliberado para verificar catch_unwind end-to-end
// (SC-009). NUNCA presente en binarios de release: el job pack-smoke verifica
// que el símbolo NO está exportado en los binarios empaquetados.
int32_t weft_test_panic(void);   // retorna WEFT_ERR_PANIC si el shim es correcto
```

**Postcondiciones**: en error, los out-params quedan sin escribir (el llamador no libera nada);
en éxito con contenido vacío, `out_ptr` puede ser válido con `out_len == 0` (liberar igual).

## Extensión Loro (`weft-loro-ffi`)

Misma ABI núcleo con prefijo `weft_loro_` (doc/new/load/free, text, export/state-vector/since,
apply, buf_free, abi_version) más las capacidades nativas (superficie de `INativeVersioning`):

```c
int32_t weft_loro_native_diff_probe  (WeftLoroDoc* doc, const uint8_t* field, uintptr_t field_len,
                                      uint8_t** out_ptr, uintptr_t* out_len);   // JSON descriptivo
int32_t weft_loro_native_branch_probe(WeftLoroDoc* doc, const uint8_t* field, uintptr_t field_len,
                                      uint8_t** out_ptr, uintptr_t* out_len);   // JSON descriptivo
int32_t weft_loro_shallow_snapshot   (WeftLoroDoc* doc, uint8_t** out_ptr, uintptr_t* out_len);
```

## Verificación del contrato (gates)

- **ASan+LSan** (P-II): suite Rust `mem_asan` ejercita cada función ≥ 2000 iteraciones,
  incluidas rutas de error; 0 fugas / 0 double-free.
- **Fuzzing** (research R14): targets `cargo-fuzz` sobre `weft_doc_load` y
  `weft_doc_apply_update` con bytes arbitrarios → nunca panic-through ni UB, solo códigos.
- **Panic-safety (SC-009)**: test .NET invoca `weft_test_panic` (build con `test-hooks`) →
  `WeftEngineException(ErrorCode.Panic)`, proceso estable, 0 fugas bajo ASan.
- **Header como fuente de verdad**: `include/weft_ffi.h` se versiona junto al crate; un test
  de CI valida que las declaraciones `[LibraryImport]` de C# coinciden con el header
  (regenerables con csbindgen como verificación cruzada, research R1).
- `weft_abi_version` se incrementa ante CUALQUIER cambio de firma/semántica; `Weft.Core` lo
  verifica al cargar la librería y lanza si no coincide con el esperado.
