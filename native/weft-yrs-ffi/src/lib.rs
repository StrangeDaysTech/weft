//! # weft-yrs-ffi — shim C-ABI propio de Weft sobre `yrs`
//!
//! ABI estable y propia (constitución P-I): la superficie es nuestra; un bump de `yrs` cambia
//! este archivo, jamás la ABI. Consumido por `Weft.Core` vía P/Invoke. Contrato completo en
//! `include/weft_ffi.h` y `specs/001-weft-crdt-versioning/contracts/ffi-abi.md`.
//!
//! ## Contrato de ownership (LEER ANTES DE CONSUMIR)
//!
//! * **`WeftDoc*`** (aquí `*mut Doc`): lo crea `weft_doc_new`/`weft_doc_load` y SOLO se libera
//!   con `weft_doc_free`, exactamente una vez. Nunca con el GC/`Marshal` de .NET (sería UB).
//! * **Buffers de salida** (`out_ptr`/`out_len`): los asigna el shim como `Box<[u8]>`; el
//!   llamador los libera SOLO con `weft_buf_free(ptr, len)` con los mismos ptr/len.
//! * **Buffers de entrada** (`ptr`+`len`): prestados; el shim los lee durante la llamada y no
//!   toma posesión ni retiene el puntero tras el retorno.
//!
//! ## Reglas transversales
//! * **Panics**: cada cuerpo va envuelto en `catch_unwind`; un panic retorna `WEFT_ERR_PANIC`,
//!   jamás cruza la frontera C (sería UB).
//! * **Thread-safety**: ninguna función que reciba `WeftDoc*` es thread-safe respecto al mismo
//!   doc (`yrs` no es Send+Sync). El llamador serializa por documento. `weft_buf_free` sí lo es.
//! * **Índices**: `u32` en UTF-16 code units (semántica de yrs); fuera de rango →
//!   `WEFT_ERR_OUT_OF_BOUNDS`.

use std::os::raw::c_uchar;
use std::panic::{catch_unwind, AssertUnwindSafe};

use yrs::updates::decoder::Decode;
use yrs::updates::encoder::Encode;
use yrs::{ClientID, Doc, GetString, OffsetKind, Options, ReadTxn, StateVector, Text, Transact, Update};

/// Crea un `Doc` con índices en **UTF-16 code units** (no el default de yrs, que es bytes UTF-8).
/// Consistente con `string` de .NET y con Yjs (clientes de editor); crítico para que
/// insert/delete por índice sean correctos con texto no-ASCII.
fn new_doc() -> Doc {
    let opts = Options {
        offset_kind: OffsetKind::Utf16,
        ..Options::default()
    };
    Doc::with_options(opts)
}

/// Como [`new_doc`] pero con un `client_id` FIJO (siembra determinista para paridad
/// cross-implementación; FU-012/CHARTER-09). Mismo `OffsetKind::Utf16`.
fn new_doc_with_client_id(client_id: u64) -> Doc {
    // El guard `< 2^53` en la frontera (CLIENT_ID_MAX_EXCLUSIVE) garantiza que `ClientID::new`
    // no dispare su `debug_assert!(value & MASK == 0)` ni corrompa el id en release.
    let opts = Options {
        client_id: ClientID::new(client_id),
        offset_kind: OffsetKind::Utf16,
        ..Options::default()
    };
    Doc::with_options(opts)
}

/// Cota superior (exclusiva) del `client_id`: yrs 0.26+ codifica los client IDs en **53 bits**
/// (antes 64). Un id `>= 2^53` no round-trippea por el encoding → se rechaza en la frontera.
const CLIENT_ID_MAX_EXCLUSIVE: u64 = 1 << 53;

// ── Códigos de estado (deben coincidir con weft_ffi.h y el mapeo de excepciones en C#) ──
pub const WEFT_OK: i32 = 0;
pub const WEFT_ERR_NULL_ARG: i32 = -1;
pub const WEFT_ERR_DECODE: i32 = -2;
pub const WEFT_ERR_APPLY: i32 = -3;
pub const WEFT_ERR_UTF8: i32 = -4;
pub const WEFT_ERR_OUT_OF_BOUNDS: i32 = -5;
pub const WEFT_ERR_PANIC: i32 = -127;

/// Versión de la ABI. Se incrementa ante CUALQUIER cambio de firma o semántica; `Weft.Core` la
/// verifica al cargar el cdylib y lanza si no coincide con la esperada.
const WEFT_ABI_VERSION: u32 = 2;

// ── Helpers internos (no expuestos por la C-ABI) ────────────────────────────────────────────

/// Envuelve el cuerpo de una fn FFI para que ningún panic cruce la frontera C.
fn guard<F: FnOnce() -> i32>(f: F) -> i32 {
    match catch_unwind(AssertUnwindSafe(f)) {
        Ok(code) => code,
        Err(_) => WEFT_ERR_PANIC,
    }
}

/// Reconstruye un `&str` prestado desde (ptr, len). None si ptr es null o no es UTF-8.
///
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

/// Reconstruye un `&[u8]` prestado desde (ptr, len). None solo si ptr es null con len != 0.
///
/// # Safety
/// `ptr` debe apuntar a `len` bytes válidos y vivos durante la llamada.
unsafe fn borrow_bytes<'a>(ptr: *const c_uchar, len: usize) -> Option<&'a [u8]> {
    if ptr.is_null() {
        return if len == 0 { Some(&[]) } else { None };
    }
    Some(std::slice::from_raw_parts(ptr, len))
}

/// Entrega un `Vec<u8>` a través de out-params, cediendo su posesión al llamador.
/// La memoria se recupera con `weft_buf_free(ptr, len)`.
///
/// # Safety
/// `out_ptr` y `out_len` deben ser punteros escribibles no nulos.
unsafe fn hand_out_buffer(data: Vec<u8>, out_ptr: *mut *mut c_uchar, out_len: *mut usize) -> i32 {
    if out_ptr.is_null() || out_len.is_null() {
        return WEFT_ERR_NULL_ARG;
    }
    // Box<[u8]>: ptr+len bastan para reconstruir y liberar sin conocer la capacity.
    let mut boxed = data.into_boxed_slice();
    let len = boxed.len();
    let ptr = boxed.as_mut_ptr();
    std::mem::forget(boxed); // se recupera en weft_buf_free
    *out_ptr = ptr;
    *out_len = len;
    WEFT_OK
}

/// `*mut Doc` → `&Doc` prestado (sin tomar posesión).
///
/// # Safety
/// `doc` debe provenir de `weft_doc_new`/`weft_doc_load` y no haber sido liberado.
unsafe fn doc_ref<'a>(doc: *mut Doc) -> Option<&'a Doc> {
    if doc.is_null() {
        None
    } else {
        Some(&*doc)
    }
}

// ── Ciclo de vida del documento ─────────────────────────────────────────────────────────────

/// Crea un documento CRDT nuevo y vacío (GC del motor SIEMPRE activo). Escribe el puntero opaco
/// en `out_doc`. Liberar SOLO con `weft_doc_free`.
///
/// # Safety
/// `out_doc` debe ser un puntero escribible no nulo.
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

/// Crea un documento CRDT nuevo con un `client_id` **fijo** (siembra determinista para paridad
/// cross-implementación con Yjs; FU-012). Idéntico a [`weft_doc_new`] salvo por el id controlado.
/// `client_id` debe caber en **53 bits** (encoding de yrs 0.26+): `client_id >= 2^53` →
/// `WEFT_ERR_OUT_OF_BOUNDS`. Liberar SOLO con `weft_doc_free`.
///
/// # Safety
/// `out_doc` debe ser un puntero escribible no nulo.
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

/// Reconstruye un documento desde un blob exportado (update v1). Escribe el puntero en `out_doc`.
///
/// # Safety
/// `blob` debe apuntar a `blob_len` bytes válidos; `out_doc` escribible no nulo.
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

/// Libera un documento. NULL es no-op. Idempotencia NO garantizada: llamar exactamente una vez.
///
/// # Safety
/// `doc` debe ser null o un puntero válido de `weft_doc_new`/`weft_doc_load` no liberado antes.
#[no_mangle]
pub unsafe extern "C" fn weft_doc_free(doc: *mut Doc) {
    if doc.is_null() {
        return;
    }
    let _ = catch_unwind(AssertUnwindSafe(|| {
        drop(Box::from_raw(doc));
    }));
}

// ── Texto por campo nombrado ────────────────────────────────────────────────────────────────

/// Inserta `text` (UTF-8) en el `Text` raíz `field`, en la posición `index` (UTF-16 units).
///
/// # Safety
/// Punteros válidos por sus longitudes; `doc` válido.
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

/// Borra `len` unidades desde `index` en el `Text` raíz `field`.
///
/// # Safety
/// Punteros válidos por sus longitudes; `doc` válido.
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

/// Lee el `Text` raíz `field` completo como UTF-8 en un buffer de salida (liberar con
/// `weft_buf_free`).
///
/// # Safety
/// Ver contrato del módulo.
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

// ── Estado y sincronización ─────────────────────────────────────────────────────────────────

/// Export determinista del estado completo (update v1 vs state-vector vacío). Base del
/// content-addressing (P-III).
///
/// # Safety
/// Ver contrato del módulo.
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

/// State vector del documento (resumen "qué conozco" para sync incremental).
///
/// # Safety
/// Ver contrato del módulo.
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

/// Delta de cambios que el poseedor de `sv` (state vector v1) no conoce.
///
/// # Safety
/// Ver contrato del módulo.
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

/// Aplica un update/estado de otra réplica (convergente).
///
/// # Safety
/// Ver contrato del módulo.
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

// ── Memoria ─────────────────────────────────────────────────────────────────────────────────

/// Libera un buffer devuelto por el shim (ptr+len exactamente los recibidos). NULL es no-op.
/// Thread-safe.
///
/// # Safety
/// `ptr` debe ser null o provenir de una función de este shim que entregó (ptr, len), no
/// liberado antes.
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

// ── Diagnóstico ─────────────────────────────────────────────────────────────────────────────

/// Versión de la ABI (entero monotónico) para detectar desalineación paquete/binario.
#[no_mangle]
pub extern "C" fn weft_abi_version() -> u32 {
    WEFT_ABI_VERSION
}

// ── Test hooks (SOLO con la feature de Cargo `test-hooks`) ──────────────────────────────────

/// Provoca un `panic!` interno deliberado para verificar `catch_unwind` end-to-end (SC-009).
/// NUNCA presente en binarios de release. Retorna `WEFT_ERR_PANIC` si el shim es correcto.
#[cfg(feature = "test-hooks")]
#[no_mangle]
pub extern "C" fn weft_test_panic() -> i32 {
    guard(|| panic!("weft_test_panic: panic deliberado para verificar la frontera"))
}
