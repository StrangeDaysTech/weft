//! # weft-loro-ffi — shim C-ABI de Weft sobre `loro` (adaptador dual-path, P-IV)
//!
//! Réplica de la ABI de `weft-yrs-ffi` con prefijo `weft_loro_`, mapeada sobre la API de Loro.
//! Diferencias con yrs: `LoroDoc` es Send+Sync (locking interno; el shim no añade locks), y el
//! versionado nativo (diff/branch/shallow) se expone aparte como capacidad opcional
//! (`INativeVersioning`). Índices en UTF-16 code units (consistente con .NET y Yjs).

use std::os::raw::c_uchar;
use std::panic::{catch_unwind, AssertUnwindSafe};

use loro::{ExportMode, Frontiers, LoroDoc, VersionVector};

// ── Códigos de estado (idénticos a weft-yrs-ffi) ──
pub const WEFT_OK: i32 = 0;
pub const WEFT_ERR_NULL_ARG: i32 = -1;
pub const WEFT_ERR_DECODE: i32 = -2;
pub const WEFT_ERR_APPLY: i32 = -3;
pub const WEFT_ERR_UTF8: i32 = -4;
pub const WEFT_ERR_OUT_OF_BOUNDS: i32 = -5;
pub const WEFT_ERR_PANIC: i32 = -127;

const WEFT_ABI_VERSION: u32 = 3;

/// PeerID reservado por Loro (`loro-internal/src/loro.rs`: `set_peer_id(u64::MAX)` →
/// `LoroError::InvalidPeerID`). Se rechaza en la frontera para no depender del error interno.
const PEER_ID_RESERVED: u64 = u64::MAX;

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

/// Doc nuevo con `peer_id` FIJO (siembra determinista, FU-016; capacidad `IDeterministicSeeding`).
/// Habilita exports reproducibles cross-run/cross-RID (un `LoroDoc` normal recibe un `peer_id`
/// aleatorio). `peer_id == u64::MAX` está reservado por Loro → `WEFT_ERR_OUT_OF_BOUNDS`. ABI v3.
///
/// AVISO: reusar un `peer_id` entre escritores concurrentes corrompe el documento (Loro). Esta
/// función es para uso determinista de test/corpus, no para identidad por usuario/dispositivo.
///
/// # Safety
/// `out_doc` debe ser un puntero escribible no nulo.
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
            // No debería ocurrir: el único fallo documentado de set_peer_id es el valor reservado,
            // ya filtrado arriba. Se mapea defensivamente en vez de entrar en pánico.
            return WEFT_ERR_APPLY;
        }
        *out_doc = Box::into_raw(Box::new(doc));
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

// ── Versionado nativo (INativeVersioning, capacidad opcional — CHARTER-10/FU-006) ──
//
// Probes DEMOSTRATIVOS de la capacidad de versionado nativo de Loro (diff/fork/shallow snapshot) que
// yrs no tiene. NO son content-addressing: su salida NO es byte-determinista entre réplicas (el shallow
// snapshot y los frontiers llevan metadata de réplica) y NO alimenta VersionId (que usa export_state /
// all_updates). Exhiben que Loro PUEDE versionar nativamente; el JSON se arma a mano (sin serde).

/// Escapa una cadena para incrustarla como valor JSON (comillas, backslash y controles).
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

/// Snapshot **shallow** (GC'd al estado actual) del documento. Bytes opacos, NO deterministas —
/// probe de la capacidad nativa, no un blob citable. Liberar con `weft_loro_buf_free`.
///
/// # Safety
/// `out_ptr`/`out_len` escribibles no nulos; `doc` válido.
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

/// Probe del **diff nativo**: describe (JSON) el diff de Loro entre frontiers vacíos y el estado
/// actual para el campo dado (nº de containers cambiados + longitud del texto). Demostrativo.
///
/// # Safety
/// Punteros válidos por sus longitudes; `doc` válido.
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

/// Probe de **fork/merge nativo**: forkea el doc, edita el fork, y lo mergea (import) en una copia
/// aparte — SIN mutar el `doc` del caller — reportando (JSON) si el merge convergió. Demostrativo.
///
/// # Safety
/// Punteros válidos por sus longitudes; `doc` válido.
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

        // Branch: fork + edición local (marcador ① fuera del texto del caller).
        const MARK: &str = "\u{2460}";
        let branch = doc.fork();
        if branch.get_text(field).insert_utf16(0, MARK).is_err() {
            return WEFT_ERR_APPLY;
        }
        branch.commit();

        // Merge: importar la edición del branch en una copia independiente del doc (no toca al caller).
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
