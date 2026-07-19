//! Fuzz target: `weft_doc_load` with arbitrary bytes (research R14).
//! Invariant: no input crosses the boundary as panic or UB — the shim contains them as
//! error codes (`catch_unwind` → WEFT_ERR_PANIC) or rejects them (WEFT_ERR_DECODE). A
//! real SIGSEGV/UB still aborts and is detected.
#![no_main]

use std::ptr;
use std::sync::Once;

use libfuzzer_sys::fuzz_target;
use weft_yrs_ffi::*;
use yrs::Doc;

static INIT: Once = Once::new();

fuzz_target!(|data: &[u8]| {
    // libfuzzer-sys installs a panic hook that aborts at the panic site, BEFORE the
    // unwinding reaches the shim's `catch_unwind`. We silence it to exercise the same mode
    // as production (engine panic → WEFT_ERR_PANIC contained at the boundary). A real
    // memory fault (SIGSEGV) does not go through the hook and is still detected.
    INIT.call_once(|| std::panic::set_hook(Box::new(|_| {})));

    unsafe {
        let mut doc: *mut Doc = ptr::null_mut();
        let code = weft_doc_load(data.as_ptr(), data.len(), &mut doc);
        // On success the shim handed out a doc: free it (ASan would detect leaks otherwise).
        if code == WEFT_OK && !doc.is_null() {
            weft_doc_free(doc);
        }
    }
});
