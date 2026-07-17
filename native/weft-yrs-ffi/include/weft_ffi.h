/*
 * weft_ffi.h — contrato C-ABI del shim `weft-yrs-ffi` (Weft, Apache-2.0).
 *
 * Fuente de verdad del contrato de ownership. `HeaderBindingParityTests` valida que las
 * declaraciones `[LibraryImport]` de Weft.Core coinciden con este header (paridad sintáctica:
 * conjunto de funciones, aridad, orden y tipos). Este header se mantiene A MANO: no hay csbindgen
 * en el repo — la mención en research R1 era una verificación cruzada opcional que nunca se
 * implementó. La ABI es propia y estable: un bump de `yrs` cambia lib.rs, jamás este header sin
 * incrementar weft_abi_version().
 *
 * ── Reglas transversales (no negociables) ──────────────────────────────────────────────
 *  1. Panics: cada función envuelve su cuerpo en catch_unwind; un panic retorna
 *     WEFT_ERR_PANIC, jamás cruza la frontera (sería UB).
 *  2. Ownership de buffers:
 *       - Salida (out_ptr/out_len): asignados por el shim (Box<[u8]>); el llamador los libera
 *         SOLO con weft_buf_free(ptr, len), exactamente una vez, nunca con el GC/Marshal.
 *       - Entrada (ptr+len): prestados; el shim no toma posesión ni retiene el puntero.
 *       - WeftDoc*: se libera SOLO con weft_doc_free, exactamente una vez.
 *  3. Thread-safety: NINGUNA función que reciba WeftDoc* es thread-safe respecto al mismo doc
 *     (yrs no es Send+Sync). El llamador serializa por documento. weft_buf_free es thread-safe.
 *  4. Strings: entradas de texto son UTF-8 (ptr+len, sin NUL); UTF-8 inválido -> WEFT_ERR_UTF8.
 *  5. Índices: uint32_t en UTF-16 code units (semántica de yrs); fuera de rango ->
 *     WEFT_ERR_OUT_OF_BOUNDS.
 *
 * Postcondiciones: en error, los out-params quedan sin escribir (el llamador no libera nada);
 * en éxito con contenido vacío, out_ptr puede ser válido con out_len == 0 (liberar igual).
 */
#ifndef WEFT_FFI_H
#define WEFT_FFI_H

#include <stdint.h>
#include <stddef.h>

#ifdef __cplusplus
extern "C" {
#endif

/* Puntero opaco al documento CRDT. Se libera SOLO con weft_doc_free. */
typedef struct WeftDoc WeftDoc;

/* ── Códigos de estado (int32_t) ──────────────────────────────────────────────────────── */
#define WEFT_OK                 0
#define WEFT_ERR_NULL_ARG      -1   /* puntero requerido nulo */
#define WEFT_ERR_DECODE        -2   /* blob/update no decodificable */
#define WEFT_ERR_APPLY         -3   /* fallo aplicando update */
#define WEFT_ERR_UTF8          -4   /* texto de entrada no UTF-8 */
#define WEFT_ERR_OUT_OF_BOUNDS -5   /* índice/longitud fuera de rango */
#define WEFT_ERR_PANIC       -127   /* panic capturado en el shim */

/* ── Ciclo de vida del documento ──────────────────────────────────────────────────────── */
int32_t weft_doc_new(WeftDoc** out_doc);
/* Doc nuevo con client_id FIJO (siembra determinista, FU-012). client_id debe caber en 53 bits
 * (encoding de yrs 0.26+): client_id >= 2^53 -> WEFT_ERR_OUT_OF_BOUNDS. ABI v2. */
int32_t weft_doc_new_with_client_id(uint64_t client_id, WeftDoc** out_doc);
int32_t weft_doc_load(const uint8_t* blob, size_t blob_len, WeftDoc** out_doc);
void    weft_doc_free(WeftDoc* doc);

/* ── Texto por campo nombrado ─────────────────────────────────────────────────────────── */
int32_t weft_text_insert(WeftDoc* doc, const uint8_t* field, size_t field_len,
                         uint32_t index, const uint8_t* text, size_t text_len);
int32_t weft_text_delete(WeftDoc* doc, const uint8_t* field, size_t field_len,
                         uint32_t index, uint32_t len);
int32_t weft_text_read(WeftDoc* doc, const uint8_t* field, size_t field_len,
                       uint8_t** out_ptr, size_t* out_len);

/* ── Estado y sincronización ──────────────────────────────────────────────────────────── */
int32_t weft_doc_export_state(WeftDoc* doc, uint8_t** out_ptr, size_t* out_len);
int32_t weft_doc_state_vector(WeftDoc* doc, uint8_t** out_ptr, size_t* out_len);
int32_t weft_doc_export_since(WeftDoc* doc, const uint8_t* sv, size_t sv_len,
                              uint8_t** out_ptr, size_t* out_len);
int32_t weft_doc_apply_update(WeftDoc* doc, const uint8_t* update, size_t update_len);

/* ── Memoria ──────────────────────────────────────────────────────────────────────────── */
void    weft_buf_free(uint8_t* ptr, size_t len);

/* ── Diagnóstico ──────────────────────────────────────────────────────────────────────── */
uint32_t weft_abi_version(void);

/* ── Test hooks (SOLO en builds con la feature de Cargo `test-hooks`) ─────────────────────
 * Provoca un panic interno deliberado para verificar catch_unwind end-to-end (SC-009).
 * NUNCA presente en binarios de release: el job `native` de release.yml verifica con `nm` que el
 * símbolo no está exportado en los cdylibs antes de empaquetarlos. */
#ifdef WEFT_TEST_HOOKS
int32_t weft_test_panic(void);   /* retorna WEFT_ERR_PANIC si el shim es correcto */
#endif

#ifdef __cplusplus
} /* extern "C" */
#endif

#endif /* WEFT_FFI_H */
