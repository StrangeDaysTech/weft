//! Fuzz target: `weft_doc_export_since` con un state vector arbitrario (regresión R6).
//!
//! Ejercita la ruta RESIDUAL `state_vector::decode` (`yrs/src/state_vector.rs:120`,
//! `HashMap::with_capacity(len)` sin acotar) — la única brecha de amplificación de memoria de
//! yrs que NO cubre el `try_reserve` ya presente en `Update::decode`. `weft_doc_export_since`
//! decodifica el SV crudo vía `StateVector::decode_v1` antes de calcular el delta, así que un SV
//! adversarial (`[255,255,255,122]`: 4 bytes que declaran una longitud gigante) llega directo al
//! sitio residual.
//!
//! Invariante: ningún input cruza la frontera como panic ni UB — el shim lo contiene como código
//! de error (`WEFT_ERR_DECODE` en glibc por overcommit; el `abort` de `handle_alloc_error` solo en
//! hosts memory-constrained duros / allocators eager). Informativo hasta que se adopte el fix
//! upstream (`try_reserve`, FU-015); prueba la regresión cuando el bump aterrice.
#![no_main]

use std::ptr;
use std::sync::Once;

use libfuzzer_sys::fuzz_target;
use weft_yrs_ffi::*;
use yrs::Doc;

static INIT: Once = Once::new();

fuzz_target!(|data: &[u8]| {
    // Ver doc_load.rs: silenciamos el hook de libfuzzer-sys que aborta en panic, para ejercitar
    // el catch_unwind del shim como en producción. Un SIGSEGV/UB real sigue detectándose.
    INIT.call_once(|| std::panic::set_hook(Box::new(|_| {})));

    unsafe {
        let mut doc: *mut Doc = ptr::null_mut();
        if weft_doc_new(&mut doc) != WEFT_OK || doc.is_null() {
            return;
        }
        let mut out_ptr: *mut u8 = ptr::null_mut();
        let mut out_len: usize = 0;
        // `data` es el state vector crudo → `StateVector::decode_v1` (ruta residual R6). El código
        // de retorno es irrelevante para el fuzzer; lo que importa es que no haya UB.
        let code = weft_doc_export_since(doc, data.as_ptr(), data.len(), &mut out_ptr, &mut out_len);
        // En éxito el shim entregó un buffer nativo: liberarlo con weft_buf_free (ASan detectaría
        // fugas si no; el GC jamás toca esta memoria).
        if code == WEFT_OK && !out_ptr.is_null() {
            weft_buf_free(out_ptr, out_len);
        }
        weft_doc_free(doc);
    }
});
