//! Fuzz target: `weft_loro_doc_apply_update` with arbitrary bytes over a live doc (research R14).
#![no_main]

use std::ptr;
use std::sync::Once;

use libfuzzer_sys::fuzz_target;
use loro::LoroDoc;
use weft_loro_ffi::*;

static INIT: Once = Once::new();

fuzz_target!(|data: &[u8]| {
    INIT.call_once(|| std::panic::set_hook(Box::new(|_| {})));

    unsafe {
        let mut doc: *mut LoroDoc = ptr::null_mut();
        if weft_loro_doc_new(&mut doc) != WEFT_OK || doc.is_null() {
            return;
        }
        let _ = weft_loro_doc_apply_update(doc, data.as_ptr(), data.len());
        weft_loro_doc_free(doc);
    }
});
