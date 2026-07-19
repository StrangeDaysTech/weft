//! Fuzz target: `weft_doc_export_since` with an arbitrary state vector (R6 regression).
//!
//! Exercises the RESIDUAL path `state_vector::decode` (`yrs/src/state_vector.rs:120`,
//! `HashMap::with_capacity(len)` unbounded) — the only memory-amplification gap in
//! yrs NOT covered by the `try_reserve` already present in `Update::decode`. `weft_doc_export_since`
//! decodes the raw SV via `StateVector::decode_v1` before computing the delta, so an adversarial
//! SV (`[255,255,255,122]`: 4 bytes declaring a gigantic length) reaches the residual site
//! directly.
//!
//! Invariant: no input crosses the boundary as panic or UB — the shim contains it as an error
//! code (`WEFT_ERR_DECODE` on glibc via overcommit; the `abort` of `handle_alloc_error` only on
//! hard memory-constrained hosts / eager allocators). Informative until the upstream fix
//! (`try_reserve`, FU-015) is adopted; it tests the regression once the bump lands.
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
        let mut out_ptr: *mut u8 = ptr::null_mut();
        let mut out_len: usize = 0;
        // `data` is the raw state vector → `StateVector::decode_v1` (residual path R6). The return
        // code is irrelevant to the fuzzer; what matters is that there is no UB.
        let code = weft_doc_export_since(doc, data.as_ptr(), data.len(), &mut out_ptr, &mut out_len);
        // On success the shim handed out a native buffer: free it with weft_buf_free (ASan would
        // detect leaks otherwise; the GC never touches this memory).
        if code == WEFT_OK && !out_ptr.is_null() {
            weft_buf_free(out_ptr, out_len);
        }
        weft_doc_free(doc);
    }
});
