# Spike 01 — Fundamento del binding CRDT para .NET

> ⚠️ **Código experimental y DESECHABLE.** El entregable es el aprendizaje, no esta
> implementación. Ver **[`hallazgos-spike-01.md`](./hallazgos-spike-01.md)** (respuestas a los 10
> objetivos + veredicto) y **[`metrics.md`](./metrics.md)** (mediciones).

Valida que construir nuestra propia capa .NET delgada sobre el core CRDT `yrs` (Rust) vía un
**shim C-ABI propio** es un fundamento sólido para la edición colaborativa del LMS.

**Veredicto: 🟢 VERDE** — convergencia OK, XML rich-text alcanzable, memoria segura (ASan),
content-addressing estable, esfuerzo acotado (9 funciones, ~205 LOC Rust).

## Arquitectura

```
C# / .NET 10  (dotnet/Spike01)          consumidor: P/Invoke [LibraryImport] + SafeHandle + SHA-256
      │  C ABI (cdylib)
Rust shim propio  (sdt_crdt_ffi)         expone SOLO la C-ABI que queremos
      │
   yrs 0.27.2  (core, se adopta)         "rueda profunda" madura (port oficial de Yjs)
```

## Estructura

| Ruta | Qué es |
|---|---|
| `sdt_crdt_ffi/` | Crate Rust `cdylib` shim sobre `yrs` (C-ABI + contrato de ownership) |
| `sdt_crdt_ffi/include/sdt_crdt_ffi.h` | Header C explícito (contrato) |
| `sdt_crdt_ffi/tests/mem_asan.rs` | Harness de memoria (nightly + AddressSanitizer) |
| `dotnet/Spike01/` | Consola .NET: P/Invoke, `DocSafeHandle`, `CrdtDoc`, escenario end-to-end |
| `csbindgen-compare/` | Binding C# generado por csbindgen (brazo comparativo) |
| `hallazgos-spike-01.md` | **Entregable**: 10 objetivos + veredicto |
| `metrics.md` | Mediciones §8 |

## Reproducir

```bash
export SSL_CERT_FILE=/etc/pki/ca-trust/extracted/pem/tls-ca-bundle.pem   # rustup/ASan

# shim + escenario
cd sdt_crdt_ffi && cargo build --release
cp target/release/libsdt_crdt_ffi.so ../dotnet/Spike01/runtimes/linux-x64/native/
cd ../dotnet/Spike01 && dotnet run -c Release      # -> 6 PASS, 0 FAIL

# memoria
cd ../../sdt_crdt_ffi && RUSTFLAGS="-Zsanitizer=address" \
  cargo +nightly test --release --target x86_64-unknown-linux-gnu --test mem_asan
```

Versiones: `yrs =0.27.2` · .NET SDK 10.0.109 (`net10.0`) · Rust stable 1.96 / nightly 1.99.
