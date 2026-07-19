//! # weft-yrs-ffi — Weft's own C-ABI shim over `yrs`
//!
//! Stable, owned ABI (constitution P-I): the surface is ours; a `yrs` bump changes
//! this file, never the ABI. Consumed by `Weft.Core` via P/Invoke. Full contract in
//! `include/weft_ffi.h` and `specs/001-weft-crdt-versioning/contracts/ffi-abi.md`.
//!
//! ## Ownership contract (READ BEFORE CONSUMING)
//!
//! * **`WeftDoc*`** (here `*mut Doc`): created by `weft_doc_new`/`weft_doc_load` and freed ONLY
//!   with `weft_doc_free`, exactly once. Never with .NET's GC/`Marshal` (would be UB).
//! * **Output buffers** (`out_ptr`/`out_len`): allocated by the shim as `Box<[u8]>`; the
//!   caller frees them ONLY with `weft_buf_free(ptr, len)` using the same ptr/len.
//! * **Input buffers** (`ptr`+`len`): borrowed; the shim reads them during the call and does
//!   not take ownership nor retain the pointer after return.
//!
//! ## Cross-cutting rules
//! * **Panics**: every body is wrapped in `catch_unwind`; a panic returns `WEFT_ERR_PANIC`,
//!   never crosses the C boundary (would be UB).
//! * **Thread-safety**: no function taking `WeftDoc*` is thread-safe with respect to the same
//!   doc (`yrs` is not Send+Sync). The caller serializes per document. `weft_buf_free` is.
//! * **Indices**: `u32` in UTF-16 code units (yrs semantics); out of range →
//!   `WEFT_ERR_OUT_OF_BOUNDS`.

use std::os::raw::c_uchar;
use std::panic::{catch_unwind, AssertUnwindSafe};

use yrs::updates::decoder::Decode;
use yrs::updates::encoder::Encode;
use yrs::{ClientID, Doc, GetString, OffsetKind, Options, ReadTxn, StateVector, Text, Transact, Update};

/// Creates a `Doc` with indices in **UTF-16 code units** (not yrs's default, which is UTF-8 bytes).
/// Consistent with .NET `string` and with Yjs (editor clients); critical so that
/// index-based insert/delete are correct with non-ASCII text.
fn new_doc() -> Doc {
    let opts = Options {
        offset_kind: OffsetKind::Utf16,
        ..Options::default()
    };
    Doc::with_options(opts)
}

/// Like [`new_doc`] but with a FIXED `client_id` (deterministic seeding for cross-implementation
/// parity; FU-012/CHARTER-09). Same `OffsetKind::Utf16`.
fn new_doc_with_client_id(client_id: u64) -> Doc {
    // The `< 2^53` guard at the boundary (CLIENT_ID_MAX_EXCLUSIVE) ensures `ClientID::new`
    // neither trips its `debug_assert!(value & MASK == 0)` nor corrupts the id in release.
    let opts = Options {
        client_id: ClientID::new(client_id),
        offset_kind: OffsetKind::Utf16,
        ..Options::default()
    };
    Doc::with_options(opts)
}

/// Upper bound (exclusive) for `client_id`: yrs 0.26+ encodes client IDs in **53 bits**
/// (formerly 64). An id `>= 2^53` does not round-trip through the encoding → rejected at the boundary.
const CLIENT_ID_MAX_EXCLUSIVE: u64 = 1 << 53;

// ── Status codes (must match weft_ffi.h and the exception mapping in C#) ──
pub const WEFT_OK: i32 = 0;
pub const WEFT_ERR_NULL_ARG: i32 = -1;
pub const WEFT_ERR_DECODE: i32 = -2;
pub const WEFT_ERR_APPLY: i32 = -3;
pub const WEFT_ERR_UTF8: i32 = -4;
pub const WEFT_ERR_OUT_OF_BOUNDS: i32 = -5;
pub const WEFT_ERR_PANIC: i32 = -127;

/// ABI version. Incremented on ANY signature or semantic change; `Weft.Core`
/// checks it when loading the cdylib and throws if it does not match the expected one.
const WEFT_ABI_VERSION: u32 = 2;

// ── Internal helpers (not exposed by the C-ABI) ────────────────────────────────────────────

/// Wraps the body of an FFI fn so that no panic crosses the C boundary.
fn guard<F: FnOnce() -> i32>(f: F) -> i32 {
    match catch_unwind(AssertUnwindSafe(f)) {
        Ok(code) => code,
        Err(_) => WEFT_ERR_PANIC,
    }
}

/// Reconstructs a borrowed `&str` from (ptr, len). None if ptr is null or not UTF-8.
///
/// # Safety
/// `ptr` must point to `len` valid bytes that stay alive during the call.
unsafe fn borrow_str<'a>(ptr: *const c_uchar, len: usize) -> Option<&'a str> {
    if ptr.is_null() && len != 0 {
        return None;
    }
    let bytes = if ptr.is_null() {
        &[][..]
    } else {
        std::slice::from_raw_parts(ptr, len)
    };
    std::str::from_utf8(bytes).ok()
}

/// Reconstructs a borrowed `&[u8]` from (ptr, len). None only if ptr is null with len != 0.
///
/// # Safety
/// `ptr` must point to `len` valid bytes that stay alive during the call.
unsafe fn borrow_bytes<'a>(ptr: *const c_uchar, len: usize) -> Option<&'a [u8]> {
    if ptr.is_null() {
        return if len == 0 { Some(&[]) } else { None };
    }
    Some(std::slice::from_raw_parts(ptr, len))
}

/// Hands out a `Vec<u8>` through out-params, transferring its ownership to the caller.
/// The memory is reclaimed with `weft_buf_free(ptr, len)`.
///
/// # Safety
/// `out_ptr` and `out_len` must be non-null writable pointers.
unsafe fn hand_out_buffer(data: Vec<u8>, out_ptr: *mut *mut c_uchar, out_len: *mut usize) -> i32 {
    if out_ptr.is_null() || out_len.is_null() {
        return WEFT_ERR_NULL_ARG;
    }
    // Box<[u8]>: ptr+len are enough to reconstruct and free without knowing the capacity.
    let mut boxed = data.into_boxed_slice();
    let len = boxed.len();
    let ptr = boxed.as_mut_ptr();
    std::mem::forget(boxed); // reclaimed in weft_buf_free
    *out_ptr = ptr;
    *out_len = len;
    WEFT_OK
}

/// `*mut Doc` → borrowed `&Doc` (without taking ownership).
///
/// # Safety
/// `doc` must come from `weft_doc_new`/`weft_doc_load` and must not have been freed.
unsafe fn doc_ref<'a>(doc: *mut Doc) -> Option<&'a Doc> {
    if doc.is_null() {
        None
    } else {
        Some(&*doc)
    }
}

// ── Document lifecycle ─────────────────────────────────────────────────────────────

/// Creates a new, empty CRDT document (engine GC ALWAYS on). Writes the opaque pointer
/// into `out_doc`. Free ONLY with `weft_doc_free`.
///
/// # Safety
/// `out_doc` must be a non-null writable pointer.
#[no_mangle]
pub unsafe extern "C" fn weft_doc_new(out_doc: *mut *mut Doc) -> i32 {
    guard(|| {
        if out_doc.is_null() {
            return WEFT_ERR_NULL_ARG;
        }
        *out_doc = Box::into_raw(Box::new(new_doc()));
        WEFT_OK
    })
}

/// Creates a new CRDT document with a **fixed** `client_id` (deterministic seeding for
/// cross-implementation parity with Yjs; FU-012). Identical to [`weft_doc_new`] except for the controlled id.
/// `client_id` must fit in **53 bits** (yrs 0.26+ encoding): `client_id >= 2^53` →
/// `WEFT_ERR_OUT_OF_BOUNDS`. Free ONLY with `weft_doc_free`.
///
/// # Safety
/// `out_doc` must be a non-null writable pointer.
#[no_mangle]
pub unsafe extern "C" fn weft_doc_new_with_client_id(client_id: u64, out_doc: *mut *mut Doc) -> i32 {
    guard(|| {
        if out_doc.is_null() {
            return WEFT_ERR_NULL_ARG;
        }
        if client_id >= CLIENT_ID_MAX_EXCLUSIVE {
            return WEFT_ERR_OUT_OF_BOUNDS;
        }
        *out_doc = Box::into_raw(Box::new(new_doc_with_client_id(client_id)));
        WEFT_OK
    })
}

/// Reconstructs a document from an exported blob (update v1). Writes the pointer into `out_doc`.
///
/// # Safety
/// `blob` must point to `blob_len` valid bytes; `out_doc` non-null writable.
#[no_mangle]
pub unsafe extern "C" fn weft_doc_load(
    blob: *const c_uchar,
    blob_len: usize,
    out_doc: *mut *mut Doc,
) -> i32 {
    guard(|| {
        if out_doc.is_null() {
            return WEFT_ERR_NULL_ARG;
        }
        let Some(bytes) = borrow_bytes(blob, blob_len) else {
            return WEFT_ERR_NULL_ARG;
        };
        let update = match Update::decode_v1(bytes) {
            Ok(u) => u,
            Err(_) => return WEFT_ERR_DECODE,
        };
        let doc = new_doc();
        {
            let mut txn = doc.transact_mut();
            if txn.apply_update(update).is_err() {
                return WEFT_ERR_DECODE;
            }
        }
        *out_doc = Box::into_raw(Box::new(doc));
        WEFT_OK
    })
}

/// Frees a document. NULL is a no-op. Idempotency NOT guaranteed: call exactly once.
///
/// # Safety
/// `doc` must be null or a valid pointer from `weft_doc_new`/`weft_doc_load` not freed before.
#[no_mangle]
pub unsafe extern "C" fn weft_doc_free(doc: *mut Doc) {
    if doc.is_null() {
        return;
    }
    let _ = catch_unwind(AssertUnwindSafe(|| {
        drop(Box::from_raw(doc));
    }));
}

// ── Text by named field ────────────────────────────────────────────────────────────────

/// Inserts `text` (UTF-8) into the root `Text` `field`, at position `index` (UTF-16 units).
///
/// # Safety
/// Pointers valid for their lengths; `doc` valid.
#[no_mangle]
pub unsafe extern "C" fn weft_text_insert(
    doc: *mut Doc,
    field: *const c_uchar,
    field_len: usize,
    index: u32,
    text: *const c_uchar,
    text_len: usize,
) -> i32 {
    guard(|| {
        let Some(doc) = doc_ref(doc) else {
            return WEFT_ERR_NULL_ARG;
        };
        let (Some(field), Some(text)) = (borrow_str(field, field_len), borrow_str(text, text_len))
        else {
            return WEFT_ERR_UTF8;
        };
        let text_ref = doc.get_or_insert_text(field);
        let mut txn = doc.transact_mut();
        if index > text_ref.len(&txn) {
            return WEFT_ERR_OUT_OF_BOUNDS;
        }
        text_ref.insert(&mut txn, index, text);
        WEFT_OK
    })
}

/// Deletes `len` units starting at `index` in the root `Text` `field`.
///
/// # Safety
/// Pointers valid for their lengths; `doc` valid.
#[no_mangle]
pub unsafe extern "C" fn weft_text_delete(
    doc: *mut Doc,
    field: *const c_uchar,
    field_len: usize,
    index: u32,
    len: u32,
) -> i32 {
    guard(|| {
        let Some(doc) = doc_ref(doc) else {
            return WEFT_ERR_NULL_ARG;
        };
        let Some(field) = borrow_str(field, field_len) else {
            return WEFT_ERR_UTF8;
        };
        let text_ref = doc.get_or_insert_text(field);
        let mut txn = doc.transact_mut();
        let cur = text_ref.len(&txn);
        if index > cur || len > cur - index {
            return WEFT_ERR_OUT_OF_BOUNDS;
        }
        text_ref.remove_range(&mut txn, index, len);
        WEFT_OK
    })
}

/// Reads the entire root `Text` `field` as UTF-8 into an output buffer (free with
/// `weft_buf_free`).
///
/// # Safety
/// See the module contract.
#[no_mangle]
pub unsafe extern "C" fn weft_text_read(
    doc: *mut Doc,
    field: *const c_uchar,
    field_len: usize,
    out_ptr: *mut *mut c_uchar,
    out_len: *mut usize,
) -> i32 {
    guard(|| {
        let Some(doc) = doc_ref(doc) else {
            return WEFT_ERR_NULL_ARG;
        };
        let Some(field) = borrow_str(field, field_len) else {
            return WEFT_ERR_UTF8;
        };
        let text_ref = doc.get_or_insert_text(field);
        let txn = doc.transact();
        let s = text_ref.get_string(&txn);
        hand_out_buffer(s.into_bytes(), out_ptr, out_len)
    })
}

// ── State and synchronization ─────────────────────────────────────────────────────────────────

/// Deterministic export of the full state (update v1 vs empty state-vector). Basis of
/// content-addressing (P-III).
///
/// # Safety
/// See the module contract.
#[no_mangle]
pub unsafe extern "C" fn weft_doc_export_state(
    doc: *mut Doc,
    out_ptr: *mut *mut c_uchar,
    out_len: *mut usize,
) -> i32 {
    guard(|| {
        let Some(doc) = doc_ref(doc) else {
            return WEFT_ERR_NULL_ARG;
        };
        let update = doc
            .transact()
            .encode_state_as_update_v1(&StateVector::default());
        hand_out_buffer(update, out_ptr, out_len)
    })
}

/// State vector of the document ("what I know" summary for incremental sync).
///
/// # Safety
/// See the module contract.
#[no_mangle]
pub unsafe extern "C" fn weft_doc_state_vector(
    doc: *mut Doc,
    out_ptr: *mut *mut c_uchar,
    out_len: *mut usize,
) -> i32 {
    guard(|| {
        let Some(doc) = doc_ref(doc) else {
            return WEFT_ERR_NULL_ARG;
        };
        let sv = doc.transact().state_vector().encode_v1();
        hand_out_buffer(sv, out_ptr, out_len)
    })
}

/// Delta of changes the holder of `sv` (state vector v1) does not know.
///
/// # Safety
/// See the module contract.
#[no_mangle]
pub unsafe extern "C" fn weft_doc_export_since(
    doc: *mut Doc,
    sv: *const c_uchar,
    sv_len: usize,
    out_ptr: *mut *mut c_uchar,
    out_len: *mut usize,
) -> i32 {
    guard(|| {
        let Some(doc) = doc_ref(doc) else {
            return WEFT_ERR_NULL_ARG;
        };
        let Some(sv_bytes) = borrow_bytes(sv, sv_len) else {
            return WEFT_ERR_NULL_ARG;
        };
        let sv = match StateVector::decode_v1(sv_bytes) {
            Ok(sv) => sv,
            Err(_) => return WEFT_ERR_DECODE,
        };
        let update = doc.transact().encode_state_as_update_v1(&sv);
        hand_out_buffer(update, out_ptr, out_len)
    })
}

/// Applies an update/state from another replica (convergent).
///
/// # Safety
/// See the module contract.
#[no_mangle]
pub unsafe extern "C" fn weft_doc_apply_update(
    doc: *mut Doc,
    update: *const c_uchar,
    update_len: usize,
) -> i32 {
    guard(|| {
        let Some(doc) = doc_ref(doc) else {
            return WEFT_ERR_NULL_ARG;
        };
        let Some(bytes) = borrow_bytes(update, update_len) else {
            return WEFT_ERR_NULL_ARG;
        };
        let update = match Update::decode_v1(bytes) {
            Ok(u) => u,
            Err(_) => return WEFT_ERR_DECODE,
        };
        let mut txn = doc.transact_mut();
        match txn.apply_update(update) {
            Ok(()) => WEFT_OK,
            Err(_) => WEFT_ERR_APPLY,
        }
    })
}

// ── Memory ─────────────────────────────────────────────────────────────────────────────────

/// Frees a buffer returned by the shim (ptr+len exactly as received). NULL is a no-op.
/// Thread-safe.
///
/// # Safety
/// `ptr` must be null or come from a function of this shim that handed out (ptr, len), not
/// freed before.
#[no_mangle]
pub unsafe extern "C" fn weft_buf_free(ptr: *mut c_uchar, len: usize) {
    if ptr.is_null() {
        return;
    }
    let _ = catch_unwind(AssertUnwindSafe(|| {
        let slice = std::slice::from_raw_parts_mut(ptr, len);
        drop(Box::from_raw(slice as *mut [u8]));
    }));
}

// ── Diagnostics ─────────────────────────────────────────────────────────────────────────────

/// ABI version (monotonic integer) to detect package/binary misalignment.
#[no_mangle]
pub extern "C" fn weft_abi_version() -> u32 {
    WEFT_ABI_VERSION
}

// ── Test hooks (ONLY with the Cargo `test-hooks` feature) ──────────────────────────────────

/// Triggers a deliberate internal `panic!` to verify `catch_unwind` end-to-end (SC-009).
/// NEVER present in release binaries. Returns `WEFT_ERR_PANIC` if the shim is correct.
#[cfg(feature = "test-hooks")]
#[no_mangle]
pub extern "C" fn weft_test_panic() -> i32 {
    guard(|| panic!("weft_test_panic: deliberate panic to verify the boundary"))
}
