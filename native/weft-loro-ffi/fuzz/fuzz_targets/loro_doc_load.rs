//! Fuzz target: `weft_loro_doc_load` with arbitrary bytes (research R14). The shim contains panics
//! (`catch_unwind`) and rejects corrupt inputs (WEFT_ERR_DECODE); a real SIGSEGV/UB is still detected.
#![no_main]

use std::ptr;
use std::sync::Once;

use libfuzzer_sys::fuzz_target;
use loro::LoroDoc;
use weft_loro_ffi::*;

static INIT: Once = Once::new();

fuzz_target!(|data: &[u8]| {
    // Silences libfuzzer-sys's hook to exercise the shim's catch_unwind (see weft-yrs-ffi).
    INIT.call_once(|| std::panic::set_hook(Box::new(|_| {})));

    unsafe {
        let mut doc: *mut LoroDoc = ptr::null_mut();
        let code = weft_loro_doc_load(data.as_ptr(), data.len(), &mut doc);
        if code == WEFT_OK && !doc.is_null() {
            weft_loro_doc_free(doc);
        }
    }
});
