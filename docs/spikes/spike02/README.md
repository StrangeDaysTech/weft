# Spike 02 — Comparación de core CRDT: yrs vs Loro

> ⚠️ **Código experimental y DESECHABLE.** El entregable es la **matriz comparativa** y el
> **veredicto**. Ver [`resultados-spike-02.md`](./resultados-spike-02.md) (análisis),
> [`hallazgos-spike-02.md`](./hallazgos-spike-02.md) (objetivos 1-a-1) y
> [`matriz-comparativa.md`](./matriz-comparativa.md).

Hace para **Loro** lo que Spike 01 hizo para **yrs** (shim C-ABI propio + P/Invoke, mismo escenario),
y sondea los diferenciadores de Loro (historial/time-travel, rich-text, encoding estable, UniFFI).

## Veredicto: 🟢 MANTENER yrs · postura DUAL-PATH con gatillo documentado

Loro pasa la **misma paridad** que yrs (8 PASS, 0 FAIL) y gana en 3 dimensiones técnicas
(thread-safety, encoding estable 1.0, **historial nativo**), pero **ninguna ventaja es decisiva** y
su **riesgo estratégico es descalificante** para .NET (bus-factor 1, sin funding, sin adopción
verificable, sin binding/servidor .NET oficial). Se construye sobre yrs manteniendo el motor abstraído.

## Estructura

| Ruta | Qué es |
|---|---|
| `sdt_crdt_ffi_loro/` | Shim Rust `cdylib` sobre `loro` 1.13.6 (paridad + sondas time-travel/shallow) |
| `dotnet/Spike02/` | Consumidor .NET (reutiliza andamiaje de Spike 01) + escenario + sondas |
| `uniffi-arm/` | Brazo secundario: evaluación UniFFI/`uniffi-bindgen-cs` (objetivo #15) |
| `matriz-comparativa.md` | **Matriz yrs vs Loro** con datos |
| `hallazgos-spike-02.md` | Objetivos 1-a-1 (A paridad, B diferenciadores, C estratégico) + gatillo |
| `resultados-spike-02.md` | Análisis ejecutivo |

## Reproducir

```bash
export SSL_CERT_FILE=/etc/pki/ca-trust/extracted/pem/tls-ca-bundle.pem
cd sdt_crdt_ffi_loro && cargo build --release
cp target/release/libsdt_crdt_ffi_loro.so ../dotnet/Spike02/runtimes/linux-x64/native/
cd ../dotnet/Spike02 && dotnet run -c Release      # -> 8 PASS, 0 FAIL

cd ../../sdt_crdt_ffi_loro && RUSTFLAGS="-Zsanitizer=address" \
  cargo +nightly test --release --target x86_64-unknown-linux-gnu --test mem_asan
```

Versiones: `loro =1.13.6` · .NET SDK 10.0.109 (`net10.0`) · Rust stable 1.96 / nightly 1.99.

## Relación con Spike 01

La línea base de yrs es Spike 01 (`../spike01/`). Este spike **no** re-implementa yrs; usa lo medido
en Spike 01 como columna de la matriz.
