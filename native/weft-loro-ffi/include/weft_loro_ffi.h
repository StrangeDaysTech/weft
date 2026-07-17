/*
 * weft_loro_ffi.h — contrato C-ABI del shim `weft-loro-ffi` (Weft, Apache-2.0).
 *
 * Réplica de la ABI de `weft-yrs-ffi` con prefijo `weft_loro_`, mapeada sobre `loro`. Fuente de
 * verdad del contrato de ownership. `HeaderBindingParityTests` valida que las declaraciones
 * `[LibraryImport]` de Weft.Loro coinciden con este header (paridad sintáctica: conjunto de
 * funciones, aridad, orden y tipos). Se mantiene A MANO: no hay csbindgen en el repo. La ABI es
 * propia y estable: un bump de `loro` cambia lib.rs, jamás este header sin incrementar
 * weft_loro_abi_version().
 *
 * ── Reglas transversales (no negociables) ──────────────────────────────────────────────
 *  1. Panics: cada función envuelve su cuerpo en catch_unwind; un panic retorna
 *     WEFT_ERR_PANIC, jamás cruza la frontera (sería UB).
 *  2. Ownership de buffers:
 *       - Salida (out_ptr/out_len): asignados por el shim (Box<[u8]>); el llamador los libera
 *         SOLO con weft_loro_buf_free(ptr, len), exactamente una vez, nunca con el GC/Marshal.
 *       - Entrada (ptr+len): prestados; el shim no toma posesión ni retiene el puntero.
 *       - WeftLoroDoc*: se libera SOLO con weft_loro_doc_free, exactamente una vez.
 *  3. Thread-safety: LoroDoc ES Send+Sync (locking interno); aun así, el contrato de Weft serializa
 *     por documento (el broker es single-reader). weft_loro_buf_free es thread-safe.
 *  4. Strings: entradas de texto son UTF-8 (ptr+len, sin NUL); UTF-8 inválido -> WEFT_ERR_UTF8.
 *  5. Índices: uint32_t en UTF-16 code units (consistente con .NET/Yjs); fuera de rango ->
 *     WEFT_ERR_OUT_OF_BOUNDS.
 *
 * Postcondiciones: en error, los out-params quedan sin escribir; en éxito con contenido vacío,
 * out_ptr puede ser válido con out_len == 0 (liberar igual).
 */
#ifndef WEFT_LORO_FFI_H
#define WEFT_LORO_FFI_H

#include <stdint.h>
#include <stddef.h>

#ifdef __cplusplus
extern "C" {
#endif

/* Puntero opaco al documento CRDT de Loro. Se libera SOLO con weft_loro_doc_free. */
typedef struct WeftLoroDoc WeftLoroDoc;

/* ── Códigos de estado (int32_t, idénticos a weft-yrs-ffi) ─────────────────────────────── */
#define WEFT_OK                 0
#define WEFT_ERR_NULL_ARG      -1   /* puntero requerido nulo */
#define WEFT_ERR_DECODE        -2   /* blob/update no decodificable */
#define WEFT_ERR_APPLY         -3   /* fallo aplicando update */
#define WEFT_ERR_UTF8          -4   /* texto de entrada no UTF-8 */
#define WEFT_ERR_OUT_OF_BOUNDS -5   /* índice/longitud fuera de rango */
#define WEFT_ERR_PANIC       -127   /* panic capturado en el shim */

/* ── Ciclo de vida del documento ──────────────────────────────────────────────────────── */
int32_t weft_loro_doc_new(WeftLoroDoc** out_doc);
int32_t weft_loro_doc_load(const uint8_t* blob, size_t blob_len, WeftLoroDoc** out_doc);
void    weft_loro_doc_free(WeftLoroDoc* doc);

/* ── Texto por campo nombrado (índices UTF-16) ────────────────────────────────────────── */
int32_t weft_loro_text_insert(WeftLoroDoc* doc, const uint8_t* field, size_t field_len,
                              uint32_t index, const uint8_t* text, size_t text_len);
int32_t weft_loro_text_delete(WeftLoroDoc* doc, const uint8_t* field, size_t field_len,
                              uint32_t index, uint32_t len);
int32_t weft_loro_text_read(WeftLoroDoc* doc, const uint8_t* field, size_t field_len,
                            uint8_t** out_ptr, size_t* out_len);

/* ── Estado y sincronización ──────────────────────────────────────────────────────────── */
int32_t weft_loro_doc_export_state(WeftLoroDoc* doc, uint8_t** out_ptr, size_t* out_len);
int32_t weft_loro_doc_state_vector(WeftLoroDoc* doc, uint8_t** out_ptr, size_t* out_len);
int32_t weft_loro_doc_export_since(WeftLoroDoc* doc, const uint8_t* sv, size_t sv_len,
                                   uint8_t** out_ptr, size_t* out_len);
int32_t weft_loro_doc_apply_update(WeftLoroDoc* doc, const uint8_t* update, size_t update_len);

/* ── Versionado nativo (INativeVersioning, capacidad opcional — CHARTER-10/FU-006) ──────
 * Probes DEMOSTRATIVOS de la capacidad nativa de Loro (diff/fork/shallow). Su salida NO es
 * byte-determinista y NO alimenta VersionId (usar weft_loro_doc_export_state para eso). */
int32_t weft_loro_shallow_snapshot(WeftLoroDoc* doc, uint8_t** out_ptr, size_t* out_len);
int32_t weft_loro_native_diff_probe(WeftLoroDoc* doc, const uint8_t* field, size_t field_len,
                                    uint8_t** out_ptr, size_t* out_len);
int32_t weft_loro_native_branch_merge_probe(WeftLoroDoc* doc, const uint8_t* field, size_t field_len,
                                            uint8_t** out_ptr, size_t* out_len);

/* ── Memoria ──────────────────────────────────────────────────────────────────────────── */
void    weft_loro_buf_free(uint8_t* ptr, size_t len);

/* ── Diagnóstico ──────────────────────────────────────────────────────────────────────────
 * Versión de ESTA ABI. Hoy: v2 (v1→v2 añadió los tres probes de versionado nativo, CHARTER-10).
 * El resolver del binding la verifica al cargar y rechaza un shim con versión distinta. */
uint32_t weft_loro_abi_version(void);

/* ── Test hooks (SOLO en builds con la feature de Cargo `test-hooks`) ─────────────────────
 * Provoca un panic interno deliberado para verificar catch_unwind end-to-end (SC-009).
 * NUNCA presente en binarios de release: el job `native` de release.yml verifica con `nm` que el
 * símbolo no está exportado en los cdylibs antes de empaquetarlos. */
#ifdef WEFT_TEST_HOOKS
int32_t weft_loro_test_panic(void);   /* retorna WEFT_ERR_PANIC si el shim es correcto */
#endif

#ifdef __cplusplus
}
#endif

#endif /* WEFT_LORO_FFI_H */
