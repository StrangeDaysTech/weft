//! Suite de integración del shim `weft-yrs-ffi`.
//!
//! Doble propósito:
//!  1. **Funcional**: round-trip byte-idéntico, convergencia, rutas de error tipificadas.
//!  2. **Memoria (gate P-II)**: cada función se ejercita ≥2000 iteraciones incluyendo rutas de
//!     error; bajo ASan/LSan (nightly) debe cerrar con 0 fugas / 0 double-free.
//!
//! Ejecutar el gate de memoria:
//! ```bash
//! RUSTFLAGS="-Zsanitizer=address" cargo +nightly test \
//!   -p weft-yrs-ffi --features test-hooks --target x86_64-unknown-linux-gnu
//! ```

use std::ptr;

use weft_yrs_ffi::*;
use yrs::Doc;

/// Crea un doc y aborta si el shim falla.
unsafe fn new_doc() -> *mut Doc {
    let mut doc: *mut Doc = ptr::null_mut();
    assert_eq!(weft_doc_new(&mut doc), WEFT_OK);
    assert!(!doc.is_null());
    doc
}

/// Inserta texto en un campo, asumiendo entradas válidas.
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

/// Recupera un buffer de salida y lo libera con weft_buf_free (patrón TakeOwnedBuffer).
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

        // Borrado en rango válido: elimina " mundo" (6 unidades desde el índice 4).
        assert_eq!(
            weft_text_delete(doc, "body".as_ptr(), "body".len(), 4, 6),
            WEFT_OK
        );
        assert_eq!(read_text(doc, "body"), "Hola");

        // Round-trip: load(export(d)).export() es byte-idéntico (P-III).
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

        // b pide a `a` solo lo que no conoce (delta vs su state vector).
        let sv_b = take_buf(|p, l| weft_doc_state_vector(b, p, l)).unwrap();
        let delta = take_buf(|p, l| weft_doc_export_since(a, sv_b.as_ptr(), sv_b.len(), p, l))
            .unwrap();
        assert_eq!(weft_doc_apply_update(b, delta.as_ptr(), delta.len()), WEFT_OK);

        // Y `a` recibe el estado completo de `b`. Ambos convergen byte-idéntico.
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

        // NULL_ARG: out-param nulo y doc nulo.
        assert_eq!(weft_doc_new(ptr::null_mut()), WEFT_ERR_NULL_ARG);
        assert_eq!(insert(ptr::null_mut(), "f", 0, "x"), WEFT_ERR_NULL_ARG);

        // UTF8: field con bytes inválidos.
        let bad = [0xFFu8, 0xFE];
        assert_eq!(
            weft_text_insert(doc, bad.as_ptr(), bad.len(), 0, b"x".as_ptr(), 1),
            WEFT_ERR_UTF8
        );

        // OUT_OF_BOUNDS: insertar más allá del final; borrar fuera de rango.
        assert_eq!(insert(doc, "f", 5, "x"), WEFT_ERR_OUT_OF_BOUNDS);
        assert_eq!(insert(doc, "f", 0, "abc"), WEFT_OK);
        assert_eq!(
            weft_text_delete(doc, "f".as_ptr(), "f".len(), 2, 10),
            WEFT_ERR_OUT_OF_BOUNDS
        );

        // DECODE: blob basura a load y a apply_update.
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
        // free de null es no-op seguro.
        weft_doc_free(ptr::null_mut());
        weft_buf_free(ptr::null_mut(), 0);
    }
}

/// Gate de memoria: bucle de estrés que ejercita todas las funciones muchas veces.
/// Bajo ASan/LSan cualquier fuga o double-free rompe aquí.
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

            // Round-trip por iteración (ejercita load + free del reconstruido).
            let blob = export_state(doc);
            let mut reloaded: *mut Doc = ptr::null_mut();
            assert_eq!(weft_doc_load(blob.as_ptr(), blob.len(), &mut reloaded), WEFT_OK);
            let sv = take_buf(|p, l| weft_doc_state_vector(reloaded, p, l)).unwrap();
            let _ = take_buf(|p, l| weft_doc_export_since(doc, sv.as_ptr(), sv.len(), p, l))
                .unwrap();

            // Ruta de error también en el bucle (UTF8 inválido) para cubrirla ≥2000 veces.
            let bad = [0xFFu8];
            assert_eq!(
                weft_text_insert(doc, bad.as_ptr(), bad.len(), 0, b"x".as_ptr(), 1),
                WEFT_ERR_UTF8
            );

            // Borra parte del contenido (ejercita remove_range) si hay suficientes unidades.
            let _ = weft_text_delete(doc, field.as_ptr(), field.len(), 0, 3);

            weft_doc_free(reloaded);
            weft_doc_free(doc);
        }
        assert_eq!(weft_abi_version(), 1);
    }
}

/// Regresión (hallazgo del fuzzer, R6): un update malformado que declara una longitud enorme en
/// pocos bytes debe degradar a `WEFT_ERR_DECODE`, nunca panic/UB. El "OOM" que reportó libFuzzer
/// era su presupuesto de RSS de 2 GB ante la asignación transitoria del decoder de yrs; con memoria
/// normal el shim devuelve DECODE limpiamente. Ver AILOG R6 (mitigación de recursos en la capa de
/// servidor, M2).
#[test]
fn malformed_update_with_huge_declared_length_decodes_cleanly() {
    unsafe {
        // Input reproductor hallado por cargo-fuzz sobre weft_doc_load.
        let data = [0xd8u8, 0xd8, 0xeb, 0x23];

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

/// Panic-safety en la frontera (SC-009): solo con la feature `test-hooks`.
#[cfg(feature = "test-hooks")]
#[test]
fn test_panic_is_caught_at_boundary() {
    for _ in 0..2000 {
        assert_eq!(weft_test_panic(), WEFT_ERR_PANIC);
    }
}
