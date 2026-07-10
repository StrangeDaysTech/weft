# Spike 03 — Plomería de versionado sobre yrs (vs primitivas nativas de Loro)

> ⚠️ **Código experimental y DESECHABLE.** Persisten la **tabla de esfuerzo/fricción**, el
> **veredicto** y el **borrador de `ICrdtEngine`**. Ver [`resultados-spike-03.md`](./resultados-spike-03.md),
> [`hallazgos-spike-03.md`](./hallazgos-spike-03.md) y [`ICrdtEngine-draft.md`](./ICrdtEngine-draft.md).

Resuelve la última incógnita de la elección de core: **¿cuánto cuesta construir nuestras features de
versionado sobre yrs, frente a las primitivas nativas de Loro?** Construye un slice mínimo (diff,
branch+merge, compactación citable) sobre yrs y lo contrasta con Loro.

## Veredicto: 🟢 yrs CIERRA

Construir el versionado sobre yrs es **acotado y limpio** (12 PASS, 0 FAIL). La tensión GC es real
(**medida: 7.3×**) pero **se evita** con blobs content-addressed + GC activo. La capa de versionado
(~58 LOC) es **engine-agnóstica** (corre idéntica en yrs y Loro). Las primitivas nativas de Loro son
más elegantes pero **no decisivas**. Se confirma yrs.

## Estructura

| Ruta | Qué es |
|---|---|
| `sdt_crdt_ffi_yrs/` | Shim de Spike 01 **+ 4 funciones** de versionado (state-vector, update-since, delete, no-gc) |
| `sdt_crdt_ffi_loro/` | Shim de Spike 02 **+ 3 sondas** nativas (diff, fork_at, delete) |
| `dotnet/Spike03/` | `ICrdtEngine` + `VersioningService` (dominio, portable) + adaptadores yrs/Loro + escenario |
| `hallazgos-spike-03.md` | Objetivos 1-a-1 + **tabla esfuerzo/fricción** + hallazgo tensión GC |
| `ICrdtEngine-draft.md` | Borrador de la interfaz (subproducto §10.4) |
| `resultados-spike-03.md` | Análisis ejecutivo |

## Reproducir

```bash
export SSL_CERT_FILE=/etc/pki/ca-trust/extracted/pem/tls-ca-bundle.pem
cd sdt_crdt_ffi_yrs && cargo build --release && cp target/release/libsdt_crdt_ffi.so ../dotnet/Spike03/runtimes/linux-x64/native/
cd ../sdt_crdt_ffi_loro && cargo build --release && cp target/release/libsdt_crdt_ffi_loro.so ../dotnet/Spike03/runtimes/linux-x64/native/
cd ../dotnet/Spike03 && dotnet run -c Release      # -> 12 PASS, 0 FAIL
```

Versiones: `yrs =0.27.2` · `loro =1.13.6` · .NET SDK 10.0.109 (`net10.0`).

## Relación con Spikes 01/02

Reutiliza y **extiende** los shims de Spike 01 (yrs) y Spike 02 (Loro); no los re-implementa. Cierra
la trilogía de spikes de elección de core CRDT: **01** (fundamento yrs) → **02** (comparación Loro) →
**03** (plomería de versionado, la decisión final).
