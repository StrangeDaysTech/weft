# Mediciones — Spike 01 (§8)

Entorno: Fedora Linux (kernel 7.0.14), x86_64. Recogidas el 2026-07-09.

| Métrica | Valor |
|---|---|
| **Versión `yrs`** | `=0.27.2` (pinneada), dep transitiva `lib0` incluida en yrs |
| **Toolchain Rust** | stable 1.96.0 (build) · nightly 1.99.0 (ASan) |
| **.NET SDK / target** | SDK 10.0.109 · target `net10.0` |
| **Funciones C-ABI expuestas** | **9** (`sdt_doc_new/free`, `sdt_text_insert/read`, `sdt_xml_insert/read`, `sdt_export_update`, `sdt_import_update`, `sdt_buf_free`) |
| **LOC Rust superficie FFI** (`lib.rs`, sin comentarios) | **205** (318 con comentarios/contrato) |
| **LOC C# — P/Invoke manual** (`NativeMethods.cs`) | **39** |
| **LOC C# — capa segura** (`CrdtDoc.cs` + `DocSafeHandle.cs`) | **138** (124 + 14) |
| **Tamaño `.so`** (release, `debug=1`) | **11 MB** |
| **Tamaño `.so`** (release, `strip`) | **1.1 MB** |
| **Build limpio del shim** (`cargo build --release`) | **~7.8 s** (incluye compilar `yrs` + deps) |
| **Build incremental del shim** | ~0.3 s |
| **Generación csbindgen** | ~3 s (build del crate generador) |

## Fricciones encontradas y cómo se resolvieron

1. **`[LibraryImport]` no marshala `SafeHandle`** (`SYSLIB1051`). → Se pasa `nint` crudo con
   `DangerousAddRef`/`DangerousGetHandle`/`DangerousRelease` (patrón `HandleLease` en `CrdtDoc.cs`);
   el `SafeHandle` se conserva para el ciclo de vida (finalizer/single-free).
2. **`curl` a crates.io/NuGet devolvía 403** con user-agent por defecto (Cloudflare). → Usar UA de
   navegador; `cargo`/`dotnet` no se ven afectados (usan sus propios clientes).
3. **`rustup update` fallaba: "No CA certificates were loaded"**. → Exportar
   `SSL_CERT_FILE=/etc/pki/ca-trust/extracted/pem/tls-ca-bundle.pem`.
4. **Nightly viejo (2026-02-04) no compilaba `yrs` 0.27.2** (`if_let_guard` inestable en esa fecha).
   → `rustup update nightly` a 1.99.0 (2026-07-08).
5. **Sin `valgrind`** en el entorno. → Verificación de memoria con AddressSanitizer + LeakSanitizer
   vía nightly (`RUSTFLAGS="-Zsanitizer=address"`), ejercitando las rutas de ownership del shim.

## Resultado de la corrida (`dotnet run -c Release`)

```
6 PASS, 0 FAIL obligatorios, 0 WARN opcionales
```

- Convergencia simple y concurrente-bidireccional: OK.
- XML rich-text alcanzable: `<paragraph></paragraph>`.
- `SHA-256(export)` estable entre re-exports del mismo doc: OK.
- **docA y docB convergidos producen blobs byte-a-byte idénticos** → hash de update v1 estable cross-nodo.
- Import de bytes corruptos → `CrdtException` código `-2 (DECODE)`.

## Resultado ASan/LSan (`cargo +nightly test --target x86_64-unknown-linux-gnu`)

```
2 passed; 0 failed  ·  0 fugas, 0 errores de dirección (2000 iteraciones de roundtrip)
```
