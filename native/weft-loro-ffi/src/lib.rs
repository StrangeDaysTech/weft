//! # weft-loro-ffi — shim C-ABI de Weft sobre `loro` (adaptador dual-path, P-IV)
//!
//! Réplica de la ABI de `weft-yrs-ffi` con prefijo `weft_loro_`, mapeada sobre la API de Loro.
//! Diferencias con yrs: `LoroDoc` es Send+Sync (locking interno; el shim no añade locks), y el
//! versionado nativo (diff/branch/shallow) se expone aparte como capacidad opcional
//! (`INativeVersioning`). Índices en UTF-16 code units (consistente con .NET y Yjs).

use std::os::raw::c_uchar;
use std::panic::{catch_unwind, AssertUnwindSafe};

use loro::{ExportMode, LoroDoc, VersionVector};

// ── Códigos de estado (idénticos a weft-yrs-ffi) ──
pub const WEFT_OK: i32 = 0;
pub const WEFT_ERR_NULL_ARG: i32 = -1;
pub const WEFT_ERR_DECODE: i32 = -2;
pub const WEFT_ERR_APPLY: i32 = -3;
pub const WEFT_ERR_UTF8: i32 = -4;
pub const WEFT_ERR_OUT_OF_BOUNDS: i32 = -5;
pub const WEFT_ERR_PANIC: i32 = -127;

const WEFT_ABI_VERSION: u32 = 1;

fn guard<F: FnOnce() -> i32>(f: F) -> i32 {
    match catch_unwind(AssertUnwindSafe(f)) {
        Ok(code) => code,
        Err(_) => WEFT_ERR_PANIC,
    }
}

/// # Safety
/// `ptr` debe apuntar a `len` bytes válidos y vivos durante la llamada.
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
/// `ptr` debe apuntar a `len` bytes válidos y vivos durante la llamada.
unsafe fn borrow_bytes<'a>(ptr: *const c_uchar, len: usize) -> Option<&'a [u8]> {
    if ptr.is_null() {
        return if len == 0 { Some(&[]) } else { None };
    }
    Some(std::slice::from_raw_parts(ptr, len))
}

/// # Safety
/// `out_ptr` y `out_len` deben ser punteros escribibles no nulos.
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
/// `doc` debe provenir de `weft_loro_doc_new`/`weft_loro_doc_load` y no haber sido liberado.
unsafe fn doc_ref<'a>(doc: *mut LoroDoc) -> Option<&'a LoroDoc> {
    if doc.is_null() {
        None
    } else {
        Some(&*doc)
    }
}

// ── Ciclo de vida ──

/// # Safety
/// `out_doc` debe ser un puntero escribible no nulo.
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

/// # Safety
/// `blob` debe apuntar a `blob_len` bytes válidos; `out_doc` escribible no nulo.
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
/// `doc` debe ser null o un puntero válido no liberado antes.
#[no_mangle]
pub unsafe extern "C" fn weft_loro_doc_free(doc: *mut LoroDoc) {
    if doc.is_null() {
        return;
    }
    let _ = catch_unwind(AssertUnwindSafe(|| drop(Box::from_raw(doc))));
}

// ── Texto por campo nombrado (índices UTF-16) ──

/// # Safety
/// Punteros válidos por sus longitudes; `doc` válido.
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
/// Punteros válidos por sus longitudes; `doc` válido.
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
/// Ver contrato del módulo.
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

// ── Estado y sincronización ──

/// Export determinista del estado completo para content-addressing (P-III). Usa `all_updates`
/// (no `Snapshot`): el snapshot incluye metadata dependiente de la réplica (peer-ids, orden interno)
/// y NO es byte-determinista entre réplicas convergidas; `all_updates` serializa el oplog de forma
/// canónica → réplicas convergidas producen bytes idénticos (mismo VersionId, SC-002).
///
/// # Safety
/// Ver contrato del módulo.
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

/// State vector del documento (VersionVector codificado).
///
/// # Safety
/// Ver contrato del módulo.
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

/// Delta de cambios que el poseedor de `sv` no conoce.
///
/// # Safety
/// Ver contrato del módulo.
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

/// Aplica un update/snapshot de otra réplica (convergente).
///
/// # Safety
/// Ver contrato del módulo.
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

// ── Memoria ──

/// # Safety
/// `ptr` debe ser null o provenir de una función de este shim, no liberado antes.
#[no_mangle]
pub unsafe extern "C" fn weft_loro_buf_free(ptr: *mut c_uchar, len: usize) {
    if ptr.is_null() {
        return;
    }
    let _ = catch_unwind(AssertUnwindSafe(|| {
        drop(Box::from_raw(std::ptr::slice_from_raw_parts_mut(ptr, len)));
    }));
}

// ── Diagnóstico ──

#[no_mangle]
pub extern "C" fn weft_loro_abi_version() -> u32 {
    WEFT_ABI_VERSION
}

// ── Test hooks ──

/// Provoca un panic deliberado para verificar catch_unwind (SC-009).
#[cfg(feature = "test-hooks")]
#[no_mangle]
pub extern "C" fn weft_loro_test_panic() -> i32 {
    guard(|| panic!("weft_loro_test_panic: panic deliberado"))
}
