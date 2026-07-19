/*
 * weft_loro_ffi.h — C-ABI contract of the `weft-loro-ffi` shim (Weft, Apache-2.0).
 *
 * Replica of the `weft-yrs-ffi` ABI with the `weft_loro_` prefix, mapped onto `loro`. Source of
 * truth for the ownership contract. `HeaderBindingParityTests` validates that the
 * `[LibraryImport]` declarations of Weft.Loro match this header (syntactic parity: function
 * set, arity, order and types). Maintained BY HAND: there is no csbindgen in the repo. The ABI is
 * owned and stable: a `loro` bump changes lib.rs, never this header without incrementing
 * weft_loro_abi_version().
 *
 * ── Cross-cutting rules (non-negotiable) ──────────────────────────────────────────────
 *  1. Panics: each function wraps its body in catch_unwind; a panic returns
 *     WEFT_ERR_PANIC, never crosses the boundary (would be UB).
 *  2. Buffer ownership:
 *       - Output (out_ptr/out_len): allocated by the shim (Box<[u8]>); the caller frees them
 *         ONLY with weft_loro_buf_free(ptr, len), exactly once, never with the GC/Marshal.
 *       - Input (ptr+len): borrowed; the shim takes no ownership nor retains the pointer.
 *       - WeftLoroDoc*: freed ONLY with weft_loro_doc_free, exactly once.
 *  3. Thread-safety: LoroDoc IS Send+Sync (internal locking); even so, Weft's contract serializes
 *     per document (the broker is single-reader). weft_loro_buf_free is thread-safe.
 *  4. Strings: text inputs are UTF-8 (ptr+len, no NUL); invalid UTF-8 -> WEFT_ERR_UTF8.
 *  5. Indices: uint32_t in UTF-16 code units (consistent with .NET/Yjs); out of range ->
 *     WEFT_ERR_OUT_OF_BOUNDS.
 *
 * Postconditions: on error, the out-params are left unwritten; on success with empty content,
 * out_ptr may be valid with out_len == 0 (free it anyway).
 */
#ifndef WEFT_LORO_FFI_H
#define WEFT_LORO_FFI_H

#include <stdint.h>
#include <stddef.h>

#ifdef __cplusplus
extern "C" {
#endif

/* Opaque pointer to Loro's CRDT document. Freed ONLY with weft_loro_doc_free. */
typedef struct WeftLoroDoc WeftLoroDoc;

/* ── Status codes (int32_t, identical to weft-yrs-ffi) ─────────────────────────────── */
#define WEFT_OK                 0
#define WEFT_ERR_NULL_ARG      -1   /* required pointer null */
#define WEFT_ERR_DECODE        -2   /* blob/update not decodable */
#define WEFT_ERR_APPLY         -3   /* failure applying update */
#define WEFT_ERR_UTF8          -4   /* input text not UTF-8 */
#define WEFT_ERR_OUT_OF_BOUNDS -5   /* index/length out of range */
#define WEFT_ERR_PANIC       -127   /* panic caught in the shim */

/* ── Document lifecycle ──────────────────────────────────────────────────────── */
int32_t weft_loro_doc_new(WeftLoroDoc** out_doc);
/* New doc with FIXED peer_id (deterministic seeding, FU-016). peer_id == u64::MAX is reserved by
 * Loro -> WEFT_ERR_OUT_OF_BOUNDS. For deterministic test/corpus use (reusing a peer_id across
 * concurrent writers corrupts the doc). ABI v3. */
int32_t weft_loro_doc_new_with_peer_id(uint64_t peer_id, WeftLoroDoc** out_doc);
int32_t weft_loro_doc_load(const uint8_t* blob, size_t blob_len, WeftLoroDoc** out_doc);
void    weft_loro_doc_free(WeftLoroDoc* doc);

/* ── Text by named field (UTF-16 indices) ────────────────────────────────────────── */
int32_t weft_loro_text_insert(WeftLoroDoc* doc, const uint8_t* field, size_t field_len,
                              uint32_t index, const uint8_t* text, size_t text_len);
int32_t weft_loro_text_delete(WeftLoroDoc* doc, const uint8_t* field, size_t field_len,
                              uint32_t index, uint32_t len);
int32_t weft_loro_text_read(WeftLoroDoc* doc, const uint8_t* field, size_t field_len,
                            uint8_t** out_ptr, size_t* out_len);

/* ── State and synchronization ──────────────────────────────────────────────────────────── */
int32_t weft_loro_doc_export_state(WeftLoroDoc* doc, uint8_t** out_ptr, size_t* out_len);
int32_t weft_loro_doc_state_vector(WeftLoroDoc* doc, uint8_t** out_ptr, size_t* out_len);
int32_t weft_loro_doc_export_since(WeftLoroDoc* doc, const uint8_t* sv, size_t sv_len,
                                   uint8_t** out_ptr, size_t* out_len);
int32_t weft_loro_doc_apply_update(WeftLoroDoc* doc, const uint8_t* update, size_t update_len);

/* ── Native versioning (INativeVersioning, optional capability — CHARTER-10/FU-006) ──────
 * DEMONSTRATIVE probes of Loro's native capability (diff/fork/shallow). Their output is NOT
 * byte-deterministic and does NOT feed VersionId (use weft_loro_doc_export_state for that). */
int32_t weft_loro_shallow_snapshot(WeftLoroDoc* doc, uint8_t** out_ptr, size_t* out_len);
int32_t weft_loro_native_diff_probe(WeftLoroDoc* doc, const uint8_t* field, size_t field_len,
                                    uint8_t** out_ptr, size_t* out_len);
int32_t weft_loro_native_branch_merge_probe(WeftLoroDoc* doc, const uint8_t* field, size_t field_len,
                                            uint8_t** out_ptr, size_t* out_len);

/* ── Memory ──────────────────────────────────────────────────────────────────────────── */
void    weft_loro_buf_free(uint8_t* ptr, size_t len);

/* ── Diagnostics ──────────────────────────────────────────────────────────────────────────
 * Version of THIS ABI. Today: v3 (v2→v3 added weft_loro_doc_new_with_peer_id, CHARTER-13/FU-016;
 * v1→v2 added the three native-versioning probes, CHARTER-10).
 * The binding resolver checks it when loading and rejects a shim with a different version. */
uint32_t weft_loro_abi_version(void);

/* ── Test hooks (ONLY in builds with the Cargo `test-hooks` feature) ─────────────────────
 * Triggers a deliberate internal panic to verify catch_unwind end-to-end (SC-009).
 * NEVER present in release binaries: the `native` job in release.yml verifies with `nm` that the
 * symbol is not exported in the cdylibs before packaging them. */
#ifdef WEFT_TEST_HOOKS
int32_t weft_loro_test_panic(void);   /* returns WEFT_ERR_PANIC if the shim is correct */
#endif

#ifdef __cplusplus
}
#endif

#endif /* WEFT_LORO_FFI_H */
