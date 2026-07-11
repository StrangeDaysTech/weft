//! Fuzz target: `weft_loro_doc_load` con bytes arbitrarios (research R14). El shim contiene panics
//! (`catch_unwind`) y rechaza corruptos (WEFT_ERR_DECODE); un SIGSEGV/UB real sigue detectándose.
#![no_main]

use std::ptr;
use std::sync::Once;

use libfuzzer_sys::fuzz_target;
use loro::LoroDoc;
use weft_loro_ffi::*;

static INIT: Once = Once::new();

fuzz_target!(|data: &[u8]| {
    // Silencia el hook de libfuzzer-sys para ejercitar el catch_unwind del shim (ver weft-yrs-ffi).
    INIT.call_once(|| std::panic::set_hook(Box::new(|_| {})));

    unsafe {
        let mut doc: *mut LoroDoc = ptr::null_mut();
        let code = weft_loro_doc_load(data.as_ptr(), data.len(), &mut doc);
        if code == WEFT_OK && !doc.is_null() {
            weft_loro_doc_free(doc);
        }
    }
});
