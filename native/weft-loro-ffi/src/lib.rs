//! # weft-loro-ffi — Weft's C-ABI shim over `loro` (dual-path adapter, P-IV)
//!
//! Replica of the `weft-yrs-ffi` ABI with the `weft_loro_` prefix, mapped onto Loro's API.
//! Differences from yrs: `LoroDoc` is Send+Sync (internal locking; the shim adds no locks), and
//! native versioning (diff/branch/shallow) is exposed separately as an optional capability
//! (`INativeVersioning`). Indices in UTF-16 code units (consistent with .NET and Yjs).

use std::os::raw::c_uchar;
use std::panic::{catch_unwind, AssertUnwindSafe};

use loro::{ExportMode, Frontiers, LoroDoc, VersionVector};

// ── Status codes (identical to weft-yrs-ffi) ──
pub const WEFT_OK: i32 = 0;
pub const WEFT_ERR_NULL_ARG: i32 = -1;
pub const WEFT_ERR_DECODE: i32 = -2;
pub const WEFT_ERR_APPLY: i32 = -3;
pub const WEFT_ERR_UTF8: i32 = -4;
pub const WEFT_ERR_OUT_OF_BOUNDS: i32 = -5;
pub const WEFT_ERR_PANIC: i32 = -127;

const WEFT_ABI_VERSION: u32 = 3;

/// PeerID reserved by Loro (`loro-internal/src/loro.rs`: `set_peer_id(u64::MAX)` →
/// `LoroError::InvalidPeerID`). Rejected at the boundary so as not to depend on the internal error.
const PEER_ID_RESERVED: u64 = u64::MAX;

fn guard<F: FnOnce() -> i32>(f: F) -> i32 {
    match catch_unwind(AssertUnwindSafe(f)) {
        Ok(code) => code,
        Err(_) => WEFT_ERR_PANIC,
    }
}

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

/// # Safety
/// `ptr` must point to `len` valid bytes that stay alive during the call.
unsafe fn borrow_bytes<'a>(ptr: *const c_uchar, len: usize) -> Option<&'a [u8]> {
    if ptr.is_null() {
        return if len == 0 { Some(&[]) } else { None };
    }
    Some(std::slice::from_raw_parts(ptr, len))
}

/// # Safety
/// `out_ptr` and `out_len` must be non-null writable pointers.
unsafe fn hand_out_buffer(data: Vec<u8>, out_ptr: *mut *mut c_uchar, out_len: *mut usize) -> i32 {
    if out_ptr.is_null() || out_len.is_null() {
        return WEFT_ERR_NULL_ARG;
    }
    let mut boxed = data.into_boxed_slice();
    let len = boxed.len();
    let ptr = boxed.as_mut_ptr();
    std::mem::forget(boxed);
    *out_ptr = ptr;
    *out_len = len;
    WEFT_OK
}

/// # Safety
/// `doc` must come from `weft_loro_doc_new`/`weft_loro_doc_load` and must not have been freed.
unsafe fn doc_ref<'a>(doc: *mut LoroDoc) -> Option<&'a LoroDoc> {
    if doc.is_null() {
        None
    } else {
        Some(&*doc)
    }
}

// ── Lifecycle ──

/// # Safety
/// `out_doc` must be a non-null writable pointer.
#[no_mangle]
pub unsafe extern "C" fn weft_loro_doc_new(out_doc: *mut *mut LoroDoc) -> i32 {
    guard(|| {
        if out_doc.is_null() {
            return WEFT_ERR_NULL_ARG;
        }
        *out_doc = Box::into_raw(Box::new(LoroDoc::new()));
        WEFT_OK
    })
}

/// New doc with a FIXED `peer_id` (deterministic seeding, FU-016; `IDeterministicSeeding` capability).
/// Enables reproducible exports cross-run/cross-RID (a normal `LoroDoc` gets a random
/// `peer_id`). `peer_id == u64::MAX` is reserved by Loro → `WEFT_ERR_OUT_OF_BOUNDS`. ABI v3.
///
/// WARNING: reusing a `peer_id` across concurrent writers corrupts the document (Loro). This
/// function is for deterministic test/corpus use, not for per-user/per-device identity.
///
/// # Safety
/// `out_doc` must be a non-null writable pointer.
#[no_mangle]
pub unsafe extern "C" fn weft_loro_doc_new_with_peer_id(
    peer_id: u64,
    out_doc: *mut *mut LoroDoc,
) -> i32 {
    guard(|| {
        if out_doc.is_null() {
            return WEFT_ERR_NULL_ARG;
        }
        if peer_id == PEER_ID_RESERVED {
            return WEFT_ERR_OUT_OF_BOUNDS;
        }
        let doc = LoroDoc::new();
        if doc.set_peer_id(peer_id).is_err() {
            // Should not happen: the only documented failure of set_peer_id is the reserved value,
            // already filtered above. Mapped defensively instead of panicking.
            return WEFT_ERR_APPLY;
        }
        *out_doc = Box::into_raw(Box::new(doc));
        WEFT_OK
    })
}

/// # Safety
/// `blob` must point to `blob_len` valid bytes; `out_doc` non-null writable.
#[no_mangle]
pub unsafe extern "C" fn weft_loro_doc_load(
    blob: *const c_uchar,
    blob_len: usize,
    out_doc: *mut *mut LoroDoc,
) -> i32 {
    guard(|| {
        if out_doc.is_null() {
            return WEFT_ERR_NULL_ARG;
        }
        let Some(bytes) = borrow_bytes(blob, blob_len) else {
            return WEFT_ERR_NULL_ARG;
        };
        let doc = LoroDoc::new();
        if doc.import(bytes).is_err() {
            return WEFT_ERR_DECODE;
        }
        *out_doc = Box::into_raw(Box::new(doc));
        WEFT_OK
    })
}

/// # Safety
/// `doc` must be null or a valid pointer not freed before.
#[no_mangle]
pub unsafe extern "C" fn weft_loro_doc_free(doc: *mut LoroDoc) {
    if doc.is_null() {
        return;
    }
    let _ = catch_unwind(AssertUnwindSafe(|| drop(Box::from_raw(doc))));
}

// ── Text by named field (UTF-16 indices) ──

/// # Safety
/// Pointers valid for their lengths; `doc` valid.
#[no_mangle]
pub unsafe extern "C" fn weft_loro_text_insert(
    doc: *mut LoroDoc,
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
        let t = doc.get_text(field);
        if index as usize > t.len_utf16() {
            return WEFT_ERR_OUT_OF_BOUNDS;
        }
        match t.insert_utf16(index as usize, text) {
            Ok(()) => WEFT_OK,
            Err(_) => WEFT_ERR_APPLY,
        }
    })
}

/// # Safety
/// Pointers valid for their lengths; `doc` valid.
#[no_mangle]
pub unsafe extern "C" fn weft_loro_text_delete(
    doc: *mut LoroDoc,
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
        let t = doc.get_text(field);
        let cur = t.len_utf16();
        if index as usize > cur || len as usize > cur - index as usize {
            return WEFT_ERR_OUT_OF_BOUNDS;
        }
        match t.delete_utf16(index as usize, len as usize) {
            Ok(()) => WEFT_OK,
            Err(_) => WEFT_ERR_APPLY,
        }
    })
}

/// # Safety
/// See the module contract.
#[no_mangle]
pub unsafe extern "C" fn weft_loro_text_read(
    doc: *mut LoroDoc,
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
        let s = doc.get_text(field).to_string();
        hand_out_buffer(s.into_bytes(), out_ptr, out_len)
    })
}

// ── State and synchronization ──

/// Deterministic export of the full state for content-addressing (P-III). Uses `all_updates`
/// (not `Snapshot`): the snapshot includes replica-dependent metadata (peer-ids, internal order)
/// and is NOT byte-deterministic across converged replicas; `all_updates` serializes the oplog
/// canonically → converged replicas produce identical bytes (same VersionId, SC-002).
///
/// # Safety
/// See the module contract.
#[no_mangle]
pub unsafe extern "C" fn weft_loro_doc_export_state(
    doc: *mut LoroDoc,
    out_ptr: *mut *mut c_uchar,
    out_len: *mut usize,
) -> i32 {
    guard(|| {
        let Some(doc) = doc_ref(doc) else {
            return WEFT_ERR_NULL_ARG;
        };
        doc.commit();
        match doc.export(ExportMode::all_updates()) {
            Ok(bytes) => hand_out_buffer(bytes, out_ptr, out_len),
            Err(_) => WEFT_ERR_APPLY,
        }
    })
}

/// State vector of the document (encoded VersionVector).
///
/// # Safety
/// See the module contract.
#[no_mangle]
pub unsafe extern "C" fn weft_loro_doc_state_vector(
    doc: *mut LoroDoc,
    out_ptr: *mut *mut c_uchar,
    out_len: *mut usize,
) -> i32 {
    guard(|| {
        let Some(doc) = doc_ref(doc) else {
            return WEFT_ERR_NULL_ARG;
        };
        doc.commit();
        hand_out_buffer(doc.state_vv().encode(), out_ptr, out_len)
    })
}

/// Delta of changes the holder of `sv` does not know.
///
/// # Safety
/// See the module contract.
#[no_mangle]
pub unsafe extern "C" fn weft_loro_doc_export_since(
    doc: *mut LoroDoc,
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
        let vv = match VersionVector::decode(sv_bytes) {
            Ok(vv) => vv,
            Err(_) => return WEFT_ERR_DECODE,
        };
        doc.commit();
        match doc.export(ExportMode::updates(&vv)) {
            Ok(bytes) => hand_out_buffer(bytes, out_ptr, out_len),
            Err(_) => WEFT_ERR_APPLY,
        }
    })
}

/// Applies an update/snapshot from another replica (convergent).
///
/// # Safety
/// See the module contract.
#[no_mangle]
pub unsafe extern "C" fn weft_loro_doc_apply_update(
    doc: *mut LoroDoc,
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
        match doc.import(bytes) {
            Ok(_) => WEFT_OK,
            Err(_) => WEFT_ERR_DECODE,
        }
    })
}

// ── Native versioning (INativeVersioning, optional capability — CHARTER-10/FU-006) ──
//
// DEMONSTRATIVE probes of Loro's native versioning capability (diff/fork/shallow snapshot) that
// yrs lacks. They are NOT content-addressing: their output is NOT byte-deterministic across replicas (the
// shallow snapshot and the frontiers carry replica metadata) and does NOT feed VersionId (which uses
// export_state / all_updates). They exhibit that Loro CAN version natively; the JSON is built by hand (no serde).

/// Escapes a string to embed it as a JSON value (quotes, backslash and control chars).
fn json_escape(s: &str) -> String {
    let mut out = String::with_capacity(s.len() + 2);
    for c in s.chars() {
        match c {
            '"' => out.push_str("\\\""),
            '\\' => out.push_str("\\\\"),
            '\n' => out.push_str("\\n"),
            '\r' => out.push_str("\\r"),
            '\t' => out.push_str("\\t"),
            c if (c as u32) < 0x20 => out.push_str(&format!("\\u{:04x}", c as u32)),
            c => out.push(c),
        }
    }
    out
}

/// **Shallow** snapshot (GC'd to the current state) of the document. Opaque bytes, NOT deterministic —
/// a probe of the native capability, not a citable blob. Free with `weft_loro_buf_free`.
///
/// # Safety
/// `out_ptr`/`out_len` non-null writable; `doc` valid.
#[no_mangle]
pub unsafe extern "C" fn weft_loro_shallow_snapshot(
    doc: *mut LoroDoc,
    out_ptr: *mut *mut c_uchar,
    out_len: *mut usize,
) -> i32 {
    guard(|| {
        let Some(doc) = doc_ref(doc) else {
            return WEFT_ERR_NULL_ARG;
        };
        doc.commit();
        match doc.export(ExportMode::shallow_snapshot(&doc.state_frontiers())) {
            Ok(bytes) => hand_out_buffer(bytes, out_ptr, out_len),
            Err(_) => WEFT_ERR_APPLY,
        }
    })
}

/// **Native diff** probe: describes (JSON) Loro's diff between empty frontiers and the current
/// state for the given field (number of changed containers + text length). Demonstrative.
///
/// # Safety
/// Pointers valid for their lengths; `doc` valid.
#[no_mangle]
pub unsafe extern "C" fn weft_loro_native_diff_probe(
    doc: *mut LoroDoc,
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
        doc.commit();
        let batch = match doc.diff(&Frontiers::default(), &doc.state_frontiers()) {
            Ok(b) => b,
            Err(_) => return WEFT_ERR_APPLY,
        };
        let containers_changed = batch.iter().count();
        let text_len_utf16 = doc.get_text(field).len_utf16();
        let json = format!(
            "{{\"field\":\"{}\",\"containers_changed\":{},\"text_len_utf16\":{}}}",
            json_escape(field),
            containers_changed,
            text_len_utf16
        );
        hand_out_buffer(json.into_bytes(), out_ptr, out_len)
    })
}

/// **Native fork/merge** probe: forks the doc, edits the fork, and merges it (import) into a
/// separate copy — WITHOUT mutating the caller's `doc` — reporting (JSON) whether the merge converged. Demonstrative.
///
/// # Safety
/// Pointers valid for their lengths; `doc` valid.
#[no_mangle]
pub unsafe extern "C" fn weft_loro_native_branch_merge_probe(
    doc: *mut LoroDoc,
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
        doc.commit();

        // Branch: fork + local edit (marker ① outside the caller's text).
        const MARK: &str = "\u{2460}";
        let branch = doc.fork();
        if branch.get_text(field).insert_utf16(0, MARK).is_err() {
            return WEFT_ERR_APPLY;
        }
        branch.commit();

        // Merge: import the branch's edit into an independent copy of the doc (does not touch the caller).
        let target = doc.fork();
        let Ok(update) = branch.export(ExportMode::all_updates()) else {
            return WEFT_ERR_APPLY;
        };
        if target.import(&update).is_err() {
            return WEFT_ERR_APPLY;
        }
        target.commit();

        let converged = target.get_text(field).to_string().contains(MARK);
        let json = format!(
            "{{\"field\":\"{}\",\"forked\":true,\"merged\":true,\"converged\":{}}}",
            json_escape(field),
            converged
        );
        hand_out_buffer(json.into_bytes(), out_ptr, out_len)
    })
}

// ── Memory ──

/// # Safety
/// `ptr` must be null or come from a function of this shim, not freed before.
#[no_mangle]
pub unsafe extern "C" fn weft_loro_buf_free(ptr: *mut c_uchar, len: usize) {
    if ptr.is_null() {
        return;
    }
    let _ = catch_unwind(AssertUnwindSafe(|| {
        drop(Box::from_raw(std::ptr::slice_from_raw_parts_mut(ptr, len)));
    }));
}

// ── Diagnostics ──

#[no_mangle]
pub extern "C" fn weft_loro_abi_version() -> u32 {
    WEFT_ABI_VERSION
}

// ── Test hooks ──

/// Triggers a deliberate panic to verify catch_unwind (SC-009).
#[cfg(feature = "test-hooks")]
#[no_mangle]
pub extern "C" fn weft_loro_test_panic() -> i32 {
    guard(|| panic!("weft_loro_test_panic: deliberate panic"))
}
