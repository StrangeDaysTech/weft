//! Fuzz target: `weft_doc_apply_update` with arbitrary bytes over a live doc (research R14).
//! Invariant: no input crosses the boundary as panic or UB — the shim contains them as
//! error codes. A real SIGSEGV/UB still aborts and is detected.
#![no_main]

use std::ptr;
use std::sync::Once;

use libfuzzer_sys::fuzz_target;
use weft_yrs_ffi::*;
use yrs::Doc;

static INIT: Once = Once::new();

fuzz_target!(|data: &[u8]| {
    // See doc_load.rs: we silence libfuzzer-sys's hook that aborts on panic, to exercise
    // the shim's catch_unwind as in production. A real SIGSEGV/UB is still detected.
    INIT.call_once(|| std::panic::set_hook(Box::new(|_| {})));

    unsafe {
        let mut doc: *mut Doc = ptr::null_mut();
        if weft_doc_new(&mut doc) != WEFT_OK || doc.is_null() {
            return;
        }
        // The return code is irrelevant to the fuzzer; what matters is that there is no UB.
        let _ = weft_doc_apply_update(doc, data.as_ptr(), data.len());
        weft_doc_free(doc);
    }
});
