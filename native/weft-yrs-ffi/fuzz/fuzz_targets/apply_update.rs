//! Fuzz target: `weft_doc_apply_update` con bytes arbitrarios sobre un doc vivo (research R14).
//! Invariante: nunca panic-through ni UB — solo códigos de error. Corre bajo ASan.
#![no_main]

use std::ptr;

use libfuzzer_sys::fuzz_target;
use weft_yrs_ffi::*;
use yrs::Doc;

fuzz_target!(|data: &[u8]| {
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
