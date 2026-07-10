//! Fuzz target: `weft_doc_apply_update` con bytes arbitrarios sobre un doc vivo (research R14).
//! Invariante: ningún input cruza la frontera como panic ni UB — el shim los contiene como
//! códigos de error. Un SIGSEGV/UB real sigue abortando y se detecta.
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
        // El código de retorno es irrelevante para el fuzzer; lo que importa es que no haya UB.
        let _ = weft_doc_apply_update(doc, data.as_ptr(), data.len());
        weft_doc_free(doc);
    }
});
