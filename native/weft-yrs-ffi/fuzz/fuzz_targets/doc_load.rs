//! Fuzz target: `weft_doc_load` con bytes arbitrarios (research R14).
//! Invariante: nunca panic-through ni UB — solo códigos de error. Corre bajo ASan.
#![no_main]

use std::ptr;

use libfuzzer_sys::fuzz_target;
use weft_yrs_ffi::*;
use yrs::Doc;

fuzz_target!(|data: &[u8]| {
    unsafe {
        let mut doc: *mut Doc = ptr::null_mut();
        let code = weft_doc_load(data.as_ptr(), data.len(), &mut doc);
        // En éxito el shim entregó un doc: liberarlo (ASan detectaría fugas si no).
        if code == WEFT_OK && !doc.is_null() {
            weft_doc_free(doc);
        }
    }
});
