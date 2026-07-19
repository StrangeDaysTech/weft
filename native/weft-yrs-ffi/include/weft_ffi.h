/*
 * weft_ffi.h — C-ABI contract of the `weft-yrs-ffi` shim (Weft, Apache-2.0).
 *
 * Source of truth for the ownership contract. `HeaderBindingParityTests` validates that the
 * `[LibraryImport]` declarations of Weft.Core match this header (syntactic parity:
 * function set, arity, order and types). This header is maintained BY HAND: there is no csbindgen
 * in the repo — the mention in research R1 was an optional cross-check that was never
 * implemented. The ABI is owned and stable: a `yrs` bump changes lib.rs, never this header without
 * incrementing weft_abi_version().
 *
 * ── Cross-cutting rules (non-negotiable) ──────────────────────────────────────────────
 *  1. Panics: each function wraps its body in catch_unwind; a panic returns
 *     WEFT_ERR_PANIC, never crosses the boundary (would be UB).
 *  2. Buffer ownership:
 *       - Output (out_ptr/out_len): allocated by the shim (Box<[u8]>); the caller frees them
 *         ONLY with weft_buf_free(ptr, len), exactly once, never with the GC/Marshal.
 *       - Input (ptr+len): borrowed; the shim takes no ownership nor retains the pointer.
 *       - WeftDoc*: freed ONLY with weft_doc_free, exactly once.
 *  3. Thread-safety: NO function taking WeftDoc* is thread-safe with respect to the same doc
 *     (yrs is not Send+Sync). The caller serializes per document. weft_buf_free is thread-safe.
 *  4. Strings: text inputs are UTF-8 (ptr+len, no NUL); invalid UTF-8 -> WEFT_ERR_UTF8.
 *  5. Indices: uint32_t in UTF-16 code units (yrs semantics); out of range ->
 *     WEFT_ERR_OUT_OF_BOUNDS.
 *
 * Postconditions: on error, the out-params are left unwritten (the caller frees nothing);
 * on success with empty content, out_ptr may be valid with out_len == 0 (free it anyway).
 */
#ifndef WEFT_FFI_H
#define WEFT_FFI_H

#include <stdint.h>
#include <stddef.h>

#ifdef __cplusplus
extern "C" {
#endif

/* Opaque pointer to the CRDT document. Freed ONLY with weft_doc_free. */
typedef struct WeftDoc WeftDoc;

/* ── Status codes (int32_t) ──────────────────────────────────────────────────────── */
#define WEFT_OK                 0
#define WEFT_ERR_NULL_ARG      -1   /* required pointer null */
#define WEFT_ERR_DECODE        -2   /* blob/update not decodable */
#define WEFT_ERR_APPLY         -3   /* failure applying update */
#define WEFT_ERR_UTF8          -4   /* input text not UTF-8 */
#define WEFT_ERR_OUT_OF_BOUNDS -5   /* index/length out of range */
#define WEFT_ERR_PANIC       -127   /* panic caught in the shim */

/* ── Document lifecycle ──────────────────────────────────────────────────────── */
int32_t weft_doc_new(WeftDoc** out_doc);
/* New doc with FIXED client_id (deterministic seeding, FU-012). client_id must fit in 53 bits
 * (yrs 0.26+ encoding): client_id >= 2^53 -> WEFT_ERR_OUT_OF_BOUNDS. ABI v2. */
int32_t weft_doc_new_with_client_id(uint64_t client_id, WeftDoc** out_doc);
int32_t weft_doc_load(const uint8_t* blob, size_t blob_len, WeftDoc** out_doc);
void    weft_doc_free(WeftDoc* doc);

/* ── Text by named field ─────────────────────────────────────────────────────────── */
int32_t weft_text_insert(WeftDoc* doc, const uint8_t* field, size_t field_len,
                         uint32_t index, const uint8_t* text, size_t text_len);
int32_t weft_text_delete(WeftDoc* doc, const uint8_t* field, size_t field_len,
                         uint32_t index, uint32_t len);
int32_t weft_text_read(WeftDoc* doc, const uint8_t* field, size_t field_len,
                       uint8_t** out_ptr, size_t* out_len);

/* ── State and synchronization ──────────────────────────────────────────────────────────── */
int32_t weft_doc_export_state(WeftDoc* doc, uint8_t** out_ptr, size_t* out_len);
int32_t weft_doc_state_vector(WeftDoc* doc, uint8_t** out_ptr, size_t* out_len);
int32_t weft_doc_export_since(WeftDoc* doc, const uint8_t* sv, size_t sv_len,
                              uint8_t** out_ptr, size_t* out_len);
int32_t weft_doc_apply_update(WeftDoc* doc, const uint8_t* update, size_t update_len);

/* ── Memory ──────────────────────────────────────────────────────────────────────────── */
void    weft_buf_free(uint8_t* ptr, size_t len);

/* ── Diagnostics ──────────────────────────────────────────────────────────────────────── */
uint32_t weft_abi_version(void);

/* ── Test hooks (ONLY in builds with the Cargo `test-hooks` feature) ─────────────────────
 * Triggers a deliberate internal panic to verify catch_unwind end-to-end (SC-009).
 * NEVER present in release binaries: the `native` job in release.yml verifies with `nm` that the
 * symbol is not exported in the cdylibs before packaging them. */
#ifdef WEFT_TEST_HOOKS
int32_t weft_test_panic(void);   /* returns WEFT_ERR_PANIC if the shim is correct */
#endif

#ifdef __cplusplus
} /* extern "C" */
#endif

#endif /* WEFT_FFI_H */
