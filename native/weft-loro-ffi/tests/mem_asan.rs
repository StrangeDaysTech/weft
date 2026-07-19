//! Integration suite for the `weft-loro-ffi` shim (P-II gate, symmetric to weft-yrs-ffi):
//! round-trip, convergence, typed error paths and memory stress (≥2000 iterations).
//!
//! Memory gate:
//! ```bash
//! RUSTFLAGS="-Zsanitizer=address" cargo +nightly test -p weft-loro-ffi --features test-hooks \
//!   --target x86_64-unknown-linux-gnu
//! ```

use std::ptr;

use loro::LoroDoc;
use weft_loro_ffi::*;

unsafe fn new_doc() -> *mut LoroDoc {
    let mut doc: *mut LoroDoc = ptr::null_mut();
    assert_eq!(weft_loro_doc_new(&mut doc), WEFT_OK);
    assert!(!doc.is_null());
    doc
}

unsafe fn insert(doc: *mut LoroDoc, field: &str, index: u32, text: &str) -> i32 {
    weft_loro_text_insert(
        doc,
        field.as_ptr(),
        field.len(),
        index,
        text.as_ptr(),
        text.len(),
    )
}

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
    weft_loro_buf_free(out_ptr, out_len);
    Ok(bytes)
}

unsafe fn read_text(doc: *mut LoroDoc, field: &str) -> String {
    let bytes =
        take_buf(|p, l| weft_loro_text_read(doc, field.as_ptr(), field.len(), p, l)).unwrap();
    String::from_utf8(bytes).unwrap()
}

unsafe fn export_state(doc: *mut LoroDoc) -> Vec<u8> {
    take_buf(|p, l| weft_loro_doc_export_state(doc, p, l)).unwrap()
}

#[test]
fn round_trip_and_text_ops() {
    unsafe {
        let doc = new_doc();
        assert_eq!(insert(doc, "body", 0, "Hola mundo"), WEFT_OK);
        assert_eq!(read_text(doc, "body"), "Hola mundo");
        assert_eq!(
            weft_loro_text_delete(doc, "body".as_ptr(), "body".len(), 4, 6),
            WEFT_OK
        );
        assert_eq!(read_text(doc, "body"), "Hola");

        let blob = export_state(doc);
        let mut reloaded: *mut LoroDoc = ptr::null_mut();
        assert_eq!(
            weft_loro_doc_load(blob.as_ptr(), blob.len(), &mut reloaded),
            WEFT_OK
        );
        assert_eq!(read_text(reloaded, "body"), "Hola");

        weft_loro_doc_free(reloaded);
        weft_loro_doc_free(doc);
    }
}

#[test]
fn incremental_sync_converges() {
    unsafe {
        let a = new_doc();
        let b = new_doc();
        assert_eq!(insert(a, "t", 0, "abc"), WEFT_OK);
        assert_eq!(insert(b, "t", 0, "XYZ"), WEFT_OK);

        let sv_b = take_buf(|p, l| weft_loro_doc_state_vector(b, p, l)).unwrap();
        let delta =
            take_buf(|p, l| weft_loro_doc_export_since(a, sv_b.as_ptr(), sv_b.len(), p, l)).unwrap();
        assert_eq!(
            weft_loro_doc_apply_update(b, delta.as_ptr(), delta.len()),
            WEFT_OK
        );

        let full_b = export_state(b);
        assert_eq!(
            weft_loro_doc_apply_update(a, full_b.as_ptr(), full_b.len()),
            WEFT_OK
        );
        assert_eq!(export_state(a), export_state(b));

        weft_loro_doc_free(a);
        weft_loro_doc_free(b);
    }
}

#[test]
fn error_paths_are_typed_not_panics() {
    unsafe {
        let doc = new_doc();
        assert_eq!(weft_loro_doc_new(ptr::null_mut()), WEFT_ERR_NULL_ARG);
        assert_eq!(insert(ptr::null_mut(), "f", 0, "x"), WEFT_ERR_NULL_ARG);

        let bad = [0xFFu8, 0xFE];
        assert_eq!(
            weft_loro_text_insert(doc, bad.as_ptr(), bad.len(), 0, b"x".as_ptr(), 1),
            WEFT_ERR_UTF8
        );
        assert_eq!(insert(doc, "f", 5, "x"), WEFT_ERR_OUT_OF_BOUNDS);
        assert_eq!(insert(doc, "f", 0, "abc"), WEFT_OK);
        assert_eq!(
            weft_loro_text_delete(doc, "f".as_ptr(), "f".len(), 2, 10),
            WEFT_ERR_OUT_OF_BOUNDS
        );

        let garbage = [1u8, 2, 3, 4, 5, 6, 7, 8];
        let mut d: *mut LoroDoc = ptr::null_mut();
        assert_eq!(
            weft_loro_doc_load(garbage.as_ptr(), garbage.len(), &mut d),
            WEFT_ERR_DECODE
        );
        assert_eq!(
            weft_loro_doc_apply_update(doc, garbage.as_ptr(), garbage.len()),
            WEFT_ERR_DECODE
        );

        weft_loro_doc_free(doc);
        weft_loro_doc_free(ptr::null_mut());
        weft_loro_buf_free(ptr::null_mut(), 0);
    }
}

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
            let _ = take_buf(|p, l| weft_loro_doc_state_vector(doc, p, l)).unwrap();

            let blob = export_state(doc);
            let mut reloaded: *mut LoroDoc = ptr::null_mut();
            assert_eq!(
                weft_loro_doc_load(blob.as_ptr(), blob.len(), &mut reloaded),
                WEFT_OK
            );

            let bad = [0xFFu8];
            assert_eq!(
                weft_loro_text_insert(doc, bad.as_ptr(), bad.len(), 0, b"x".as_ptr(), 1),
                WEFT_ERR_UTF8
            );
            let _ = weft_loro_text_delete(doc, field.as_ptr(), field.len(), 0, 3);

            weft_loro_doc_free(reloaded);
            weft_loro_doc_free(doc);
        }
        assert_eq!(weft_loro_abi_version(), 3); // ABI v3: + weft_loro_doc_new_with_peer_id (CHARTER-13)
    }
}

/// peer_id seeding (FU-016): reachability, reserved-value guard and no leaks (ASan/LSan).
#[test]
fn seeded_peer_id_reachable_guarded_and_nonleaking() {
    unsafe {
        // Happy path: fixed peer_id → valid doc, editable, freeable without leaks.
        let mut doc: *mut LoroDoc = ptr::null_mut();
        assert_eq!(weft_loro_doc_new_with_peer_id(42, &mut doc), WEFT_OK);
        assert!(!doc.is_null());
        let field = b"body";
        assert_eq!(
            weft_loro_text_insert(doc, field.as_ptr(), field.len(), 0, b"hola".as_ptr(), 4),
            WEFT_OK
        );
        weft_loro_doc_free(doc);

        // Reserved value (u64::MAX) → OUT_OF_BOUNDS, without allocating a doc (out stays intact).
        let mut reserved: *mut LoroDoc = ptr::null_mut();
        assert_eq!(
            weft_loro_doc_new_with_peer_id(u64::MAX, &mut reserved),
            WEFT_ERR_OUT_OF_BOUNDS
        );
        assert!(reserved.is_null());

        // null out_doc → NULL_ARG.
        assert_eq!(
            weft_loro_doc_new_with_peer_id(1, ptr::null_mut()),
            WEFT_ERR_NULL_ARG
        );
    }
}

/// Native probes (INativeVersioning, FU-006): reachability + no leaks (ASan) + no mutation of the caller.
#[test]
fn native_versioning_probes_reachable_and_nonleaking() {
    unsafe {
        let field = b"body";
        let text = "hola".as_bytes();

        let mut doc: *mut LoroDoc = ptr::null_mut();
        assert_eq!(weft_loro_doc_new(&mut doc), WEFT_OK);
        weft_loro_text_insert(doc, field.as_ptr(), field.len(), 0, text.as_ptr(), text.len());

        // Shallow snapshot: non-empty, reloadable.
        let (mut sp, mut sl) = (ptr::null_mut(), 0usize);
        assert_eq!(weft_loro_shallow_snapshot(doc, &mut sp, &mut sl), WEFT_OK);
        assert!(sl > 0);
        let mut reloaded: *mut LoroDoc = ptr::null_mut();
        assert_eq!(
            weft_loro_doc_load(sp, sl, &mut reloaded),
            WEFT_OK,
            "the shallow snapshot must be reloadable"
        );
        weft_loro_doc_free(reloaded);
        weft_loro_buf_free(sp, sl);

        // Diff probe: JSON with the field and the count.
        let (mut dp, mut dl) = (ptr::null_mut(), 0usize);
        assert_eq!(
            weft_loro_native_diff_probe(doc, field.as_ptr(), field.len(), &mut dp, &mut dl),
            WEFT_OK
        );
        let diff_json = std::str::from_utf8(std::slice::from_raw_parts(dp, dl)).unwrap();
        assert!(diff_json.contains("\"containers_changed\""));
        weft_loro_buf_free(dp, dl);

        // Branch/merge probe: reports convergence and does NOT mutate the caller's doc.
        let (mut bp, mut bl) = (ptr::null_mut(), 0usize);
        assert_eq!(
            weft_loro_native_branch_merge_probe(doc, field.as_ptr(), field.len(), &mut bp, &mut bl),
            WEFT_OK
        );
        let branch_json = std::str::from_utf8(std::slice::from_raw_parts(bp, bl)).unwrap();
        assert!(branch_json.contains("\"converged\":true"), "the native merge must converge");
        weft_loro_buf_free(bp, bl);

        // The caller's text remains intact (the probe forks, does not mutate the original).
        let (mut rp, mut rl) = (ptr::null_mut(), 0usize);
        weft_loro_text_read(doc, field.as_ptr(), field.len(), &mut rp, &mut rl);
        let caller_text = std::str::from_utf8(std::slice::from_raw_parts(rp, rl)).unwrap();
        assert_eq!(caller_text, "hola", "the probe must not mutate the caller's doc");
        weft_loro_buf_free(rp, rl);

        weft_loro_doc_free(doc);
    }
}

#[cfg(feature = "test-hooks")]
#[test]
fn test_panic_is_caught_at_boundary() {
    for _ in 0..2000 {
        assert_eq!(weft_loro_test_panic(), WEFT_ERR_PANIC);
    }
}
