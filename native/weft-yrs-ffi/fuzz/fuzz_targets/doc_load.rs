//! Fuzz target: `weft_doc_load` con bytes arbitrarios (research R14).
//! Invariante: ningún input cruza la frontera como panic ni UB — el shim los contiene como
//! códigos de error (`catch_unwind` → WEFT_ERR_PANIC) o los rechaza (WEFT_ERR_DECODE). Un
//! SIGSEGV/UB real sigue abortando y se detecta.
#![no_main]

use std::ptr;
use std::sync::Once;

use libfuzzer_sys::fuzz_target;
use weft_yrs_ffi::*;
use yrs::Doc;

static INIT: Once = Once::new();

fuzz_target!(|data: &[u8]| {
    // libfuzzer-sys instala un panic hook que aborta en el sitio del panic, ANTES de que el
    // unwinding llegue al `catch_unwind` del shim. Lo silenciamos para ejercitar el mismo modo
    // que producción (panic del motor → WEFT_ERR_PANIC contenido en la frontera). Un fallo de
    // memoria real (SIGSEGV) no pasa por el hook y sigue detectándose.
    INIT.call_once(|| std::panic::set_hook(Box::new(|_| {})));

    unsafe {
        let mut doc: *mut Doc = ptr::null_mut();
        let code = weft_doc_load(data.as_ptr(), data.len(), &mut doc);
        // En éxito el shim entregó un doc: liberarlo (ASan detectaría fugas si no).
        if code == WEFT_OK && !doc.is_null() {
            weft_doc_free(doc);
        }
    }
});
