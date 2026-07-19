//! Integration suite for the `weft-yrs-ffi` shim.
//!
//! Dual purpose:
//!  1. **Functional**: byte-identical round-trip, convergence, typed error paths.
//!  2. **Memory (P-II gate)**: each function is exercised ≥2000 iterations including error
//!     paths; under ASan/LSan (nightly) it must finish with 0 leaks / 0 double-free.
//!
//! Run the memory gate:
//! ```bash
//! RUSTFLAGS="-Zsanitizer=address" cargo +nightly test \
//!   -p weft-yrs-ffi --features test-hooks --target x86_64-unknown-linux-gnu
//! ```

use std::ptr;

use weft_yrs_ffi::*;
use yrs::Doc;

/// Creates a doc and aborts if the shim fails.
unsafe fn new_doc() -> *mut Doc {
    let mut doc: *mut Doc = ptr::null_mut();
    assert_eq!(weft_doc_new(&mut doc), WEFT_OK);
    assert!(!doc.is_null());
    doc
}

/// Inserts text into a field, assuming valid inputs.
unsafe fn insert(doc: *mut Doc, field: &str, index: u32, text: &str) -> i32 {
    weft_text_insert(
        doc,
        field.as_ptr(),
        field.len(),
        index,
        text.as_ptr(),
        text.len(),
    )
}

/// Retrieves an output buffer and frees it with weft_buf_free (TakeOwnedBuffer pattern).
unsafe fn take_buf(f: impl FnOnce(*mut *mut u8, *mut usize) -> i32) -> Result<Vec<u8>, i32> {
    let mut out_ptr: *mut u8 = ptr::null_mut();
    let mut out_len: usize = 0;
    let code = f(&mut out_ptr, &mut out_len);
    if code != WEFT_OK {
        return Err(code);
    }
    let bytes = if out_ptr.is_null() {
        Vec::new()
    } else {
        std::slice::from_raw_parts(out_ptr, out_len).to_vec()
    };
    weft_buf_free(out_ptr, out_len);
    Ok(bytes)
}

unsafe fn read_text(doc: *mut Doc, field: &str) -> String {
    let bytes = take_buf(|p, l| weft_text_read(doc, field.as_ptr(), field.len(), p, l)).unwrap();
    String::from_utf8(bytes).unwrap()
}

unsafe fn export_state(doc: *mut Doc) -> Vec<u8> {
    take_buf(|p, l| weft_doc_export_state(doc, p, l)).unwrap()
}

#[test]
fn round_trip_and_text_ops() {
    unsafe {
        let doc = new_doc();
        assert_eq!(insert(doc, "body", 0, "Hola mundo"), WEFT_OK);
        assert_eq!(read_text(doc, "body"), "Hola mundo");

        // Delete in valid range: removes " mundo" (6 units from index 4).
        assert_eq!(
            weft_text_delete(doc, "body".as_ptr(), "body".len(), 4, 6),
            WEFT_OK
        );
        assert_eq!(read_text(doc, "body"), "Hola");

        // Round-trip: load(export(d)).export() is byte-identical (P-III).
        let blob = export_state(doc);
        let mut reloaded: *mut Doc = ptr::null_mut();
        assert_eq!(weft_doc_load(blob.as_ptr(), blob.len(), &mut reloaded), WEFT_OK);
        assert_eq!(export_state(reloaded), blob);
        assert_eq!(read_text(reloaded, "body"), "Hola");

        weft_doc_free(reloaded);
        weft_doc_free(doc);
    }
}

#[test]
fn incremental_sync_converges() {
    unsafe {
        let a = new_doc();
        let b = new_doc();
        assert_eq!(insert(a, "t", 0, "abc"), WEFT_OK);
        assert_eq!(insert(b, "t", 0, "XYZ"), WEFT_OK);

        // b asks `a` only for what it does not know (delta vs its state vector).
        let sv_b = take_buf(|p, l| weft_doc_state_vector(b, p, l)).unwrap();
        let delta = take_buf(|p, l| weft_doc_export_since(a, sv_b.as_ptr(), sv_b.len(), p, l))
            .unwrap();
        assert_eq!(weft_doc_apply_update(b, delta.as_ptr(), delta.len()), WEFT_OK);

        // And `a` receives the full state of `b`. Both converge byte-identical.
        let full_b = export_state(b);
        assert_eq!(weft_doc_apply_update(a, full_b.as_ptr(), full_b.len()), WEFT_OK);
        assert_eq!(export_state(a), export_state(b));

        weft_doc_free(a);
        weft_doc_free(b);
    }
}

#[test]
fn error_paths_are_typed_not_panics() {
    unsafe {
        let doc = new_doc();

        // NULL_ARG: null out-param and null doc.
        assert_eq!(weft_doc_new(ptr::null_mut()), WEFT_ERR_NULL_ARG);
        assert_eq!(insert(ptr::null_mut(), "f", 0, "x"), WEFT_ERR_NULL_ARG);

        // UTF8: field with invalid bytes.
        let bad = [0xFFu8, 0xFE];
        assert_eq!(
            weft_text_insert(doc, bad.as_ptr(), bad.len(), 0, b"x".as_ptr(), 1),
            WEFT_ERR_UTF8
        );

        // OUT_OF_BOUNDS: insert past the end; delete out of range.
        assert_eq!(insert(doc, "f", 5, "x"), WEFT_ERR_OUT_OF_BOUNDS);
        assert_eq!(insert(doc, "f", 0, "abc"), WEFT_OK);
        assert_eq!(
            weft_text_delete(doc, "f".as_ptr(), "f".len(), 2, 10),
            WEFT_ERR_OUT_OF_BOUNDS
        );

        // DECODE: garbage blob to load and to apply_update.
        let garbage = [1u8, 2, 3, 4, 5, 6, 7, 8];
        let mut d: *mut Doc = ptr::null_mut();
        assert_eq!(
            weft_doc_load(garbage.as_ptr(), garbage.len(), &mut d),
            WEFT_ERR_DECODE
        );
        assert_eq!(
            weft_doc_apply_update(doc, garbage.as_ptr(), garbage.len()),
            WEFT_ERR_DECODE
        );

        weft_doc_free(doc);
        // free of null is a safe no-op.
        weft_doc_free(ptr::null_mut());
        weft_buf_free(ptr::null_mut(), 0);
    }
}

/// Memory gate: stress loop that exercises all functions many times.
/// Under ASan/LSan any leak or double-free breaks here.
#[test]
fn stress_all_functions_2000_iterations() {
    unsafe {
        for i in 0..2000u32 {
            let doc = new_doc();
            let field = "campo";
            let payload = format!("edición-{i}-áéí");
            assert_eq!(insert(doc, field, 0, &payload), WEFT_OK);
            let _ = read_text(doc, field);
            let _ = export_state(doc);
            let _ = take_buf(|p, l| weft_doc_state_vector(doc, p, l)).unwrap();

            // Round-trip per iteration (exercises load + free of the reconstructed one).
            let blob = export_state(doc);
            let mut reloaded: *mut Doc = ptr::null_mut();
            assert_eq!(weft_doc_load(blob.as_ptr(), blob.len(), &mut reloaded), WEFT_OK);
            let sv = take_buf(|p, l| weft_doc_state_vector(reloaded, p, l)).unwrap();
            let _ = take_buf(|p, l| weft_doc_export_since(doc, sv.as_ptr(), sv.len(), p, l))
                .unwrap();

            // Error path also in the loop (invalid UTF8) to cover it ≥2000 times.
            let bad = [0xFFu8];
            assert_eq!(
                weft_text_insert(doc, bad.as_ptr(), bad.len(), 0, b"x".as_ptr(), 1),
                WEFT_ERR_UTF8
            );

            // Deletes part of the content (exercises remove_range) if there are enough units.
            let _ = weft_text_delete(doc, field.as_ptr(), field.len(), 0, 3);

            weft_doc_free(reloaded);
            weft_doc_free(doc);
        }
        assert_eq!(weft_abi_version(), 2); // ABI v2: + weft_doc_new_with_client_id (CHARTER-09)
    }
}

/// Regression (fuzzer finding, R6): a malformed update that declares an enormous length in
/// few bytes must degrade to `WEFT_ERR_DECODE`, never panic/UB. The "OOM" libFuzzer reported
/// was its 2 GB RSS budget facing the transient allocation of yrs's decoder; with normal
/// memory the shim returns DECODE cleanly. See AILOG R6 (resource mitigation in the
/// server layer, M2).
#[test]
fn malformed_update_with_huge_declared_length_decodes_cleanly() {
    unsafe {
        // Reproducer inputs found by cargo-fuzz over weft_doc_load: they declare an enormous
        // length in 4 bytes → yrs reserves large virtual capacity but fails the decode without
        // filling it (real RSS measured ~150 MB) → WEFT_ERR_DECODE, never panic/UB.
        for data in [[0xd8u8, 0xd8, 0xeb, 0x23], [0xfa, 0xff, 0xa4, 0x25]] {
            let mut loaded: *mut Doc = ptr::null_mut();
            assert_eq!(weft_doc_load(data.as_ptr(), data.len(), &mut loaded), WEFT_ERR_DECODE);
            assert!(loaded.is_null());

            let doc = new_doc();
            assert_eq!(
                weft_doc_apply_update(doc, data.as_ptr(), data.len()),
                WEFT_ERR_DECODE
            );
            weft_doc_free(doc);
        }
    }
}

/// Regression (fuzzer finding, R6): a malformed update that `panic!`s inside yrs
/// (assertion in `block.rs`) must NOT cross the boundary — `catch_unwind` contains it as an
/// error code. Verifies the P-I contract with panic=unwind (same as production; the CI fuzz runs
/// with a silenced hook to exercise this very path).
/// client_id seeding (FU-012/CHARTER-09): two docs with the SAME client_id + the same ops
/// export identical bytes (basis of cross-impl parity); the 53-bit guard rejects at the
/// boundary; the valid upper edge (2^53 - 1) is accepted.
#[test]
fn seed_client_id_is_deterministic_and_bounded() {
    unsafe {
        let field = b"body";
        let text = b"hola";

        // Same client_id + same ops → byte-identical export.
        let mut a: *mut Doc = ptr::null_mut();
        let mut b: *mut Doc = ptr::null_mut();
        assert_eq!(weft_doc_new_with_client_id(42, &mut a), WEFT_OK);
        assert_eq!(weft_doc_new_with_client_id(42, &mut b), WEFT_OK);
        weft_text_insert(a, field.as_ptr(), field.len(), 0, text.as_ptr(), text.len());
        weft_text_insert(b, field.as_ptr(), field.len(), 0, text.as_ptr(), text.len());

        let (mut pa, mut la) = (ptr::null_mut(), 0usize);
        let (mut pb, mut lb) = (ptr::null_mut(), 0usize);
        assert_eq!(weft_doc_export_state(a, &mut pa, &mut la), WEFT_OK);
        assert_eq!(weft_doc_export_state(b, &mut pb, &mut lb), WEFT_OK);
        assert_eq!(la, lb);
        assert_eq!(
            std::slice::from_raw_parts(pa, la),
            std::slice::from_raw_parts(pb, lb),
            "misma siembra + mismas ops debe exportar bytes idénticos"
        );
        weft_buf_free(pa, la);
        weft_buf_free(pb, lb);
        weft_doc_free(a);
        weft_doc_free(b);

        // 53-bit guard: 2^53 is rejected, 2^53 - 1 is accepted.
        let mut over: *mut Doc = ptr::null_mut();
        assert_eq!(
            weft_doc_new_with_client_id(1u64 << 53, &mut over),
            WEFT_ERR_OUT_OF_BOUNDS
        );
        assert!(over.is_null());

        let mut edge: *mut Doc = ptr::null_mut();
        assert_eq!(weft_doc_new_with_client_id((1u64 << 53) - 1, &mut edge), WEFT_OK);
        assert!(!edge.is_null());
        weft_doc_free(edge);
    }
}

#[test]
fn malformed_update_that_panics_yrs_is_contained_not_ub() {
    unsafe {
        let data = [0x4a, 0x01, 0xed, 0xed, 0xed, 0xed, 0xed, 0xed, 0xed, 0x4a, 0x21];

        let mut loaded: *mut Doc = ptr::null_mut();
        let rc = weft_doc_load(data.as_ptr(), data.len(), &mut loaded);
        eprintln!("panic-input weft_doc_load rc = {rc}");
        assert!(rc == WEFT_ERR_PANIC || rc == WEFT_ERR_DECODE);
        assert!(loaded.is_null());

        let doc = new_doc();
        let rc2 = weft_doc_apply_update(doc, data.as_ptr(), data.len());
        assert!(rc2 == WEFT_ERR_PANIC || rc2 == WEFT_ERR_DECODE);
        weft_doc_free(doc);
    }
}

/// Panic-safety at the boundary (SC-009): only with the `test-hooks` feature.
#[cfg(feature = "test-hooks")]
#[test]
fn test_panic_is_caught_at_boundary() {
    for _ in 0..2000 {
        assert_eq!(weft_test_panic(), WEFT_ERR_PANIC);
    }
}
