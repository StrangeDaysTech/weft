# Resultados — Spike 02: comparación de core CRDT yrs vs Loro

_StrangeDaysTech · análisis de la ejecución · 2026-07-09_

Resumen ejecutivo. Detalle por objetivo en [`hallazgos-spike-02.md`](./hallazgos-spike-02.md);
datos lado a lado en [`matriz-comparativa.md`](./matriz-comparativa.md).

---

## 1. Veredicto

# 🟢 MANTENER yrs · postura DUAL-PATH con gatillo documentado

La hipótesis a refutar —"Loro no ofrece ventaja neta suficiente para justificar el cambio de core"—
**se confirma**. Loro es un fundamento **técnicamente válido** (paridad completa + ventajas reales
en historial y thread-safety), pero **ninguna ventaja es decisiva** para nuestro diseño, y su
**riesgo estratégico es descalificante** para un proyecto .NET. Se procede a construir la capa .NET
sobre **yrs** (Spike 01), manteniendo el motor abstraído para que Loro siga intercambiable.

---

## 2. Qué salió (evidencia ejecutable)

Corrida `dotnet run -c Release` sobre Loro → **8 PASS, 0 FAIL obligatorios**. Loro pasó **exactamente
la misma paridad** que yrs:

| Criterio | yrs (Spike 01) | Loro (Spike 02) |
|---|---|---|
| Convergencia simple + concurrente | ✅ | ✅ |
| Rich-text vía FFI | ✅ XmlFragment | ✅ LoroText+marca |
| Memoria (ASan/LSan) | ✅ 0 errores | ✅ 0 errores |
| **Content-addressing determinista** | ✅ update byte-idéntico | ✅ snapshot **y** updates byte-idénticos |
| Blob corrupto → excepción | ✅ | ✅ |

Y las **sondas de diferenciadores** de Loro funcionaron: **time-travel** (leer estado histórico y
re-attach), **shallow snapshot** (poda), perf 8 ms para 5000 inserts+roundtrip.

## 3. Dónde gana cada uno (honesto)

- **Loro gana (técnico):** thread-safety (Send+Sync → sin lock por doc; wrapper C# más simple),
  promesa de encoding estable 1.0, e **historial nativo** (time-travel, shallow snapshot, fork/branch).
- **yrs gana:** `.so` 3.5× más pequeño (1.1 vs 3.8 MB), build 3× más rápido (7.8 vs 25 s), madurez
  del substrato rich-text para ProseMirror (`y-prosemirror`), y —**decisivo**— estatus estratégico.
- **Empate en lo crítico:** content-addressing determinista (ambos byte-idénticos cross-nodo) y toda
  la paridad de FFI/memoria/errores.

## 4. Por qué la ventaja de Loro NO basta

La mejor carta de Loro es su **modelo de historial**. No alcanza para cambiar porque:
1. Spike 01 ya nos da IDs content-addressed deterministas **gratis**, y **construimos nuestra capa
   de versionado igual** (la necesitamos a nuestro modelo citable/inmutable). El time-travel de Loro
   sería un plus, no un pilar que yrs no pueda igualar con nuestra capa.
2. El motor está **abstraído (ADR-4)**: el coste de equivocarse está acotado, así que la barra para
   cambiar es —correctamente— alta.
3. El **riesgo estratégico** de Loro pesa más que su ventaja técnica (ver §5).

## 5. El factor decisivo: estatus estratégico (research jul-2026)

El disparador de reevaluación **no se activó**. Datos con fuentes:
- **Bus-factor 1**: solo @zxch3n activo; 2.º dev inactivo desde abr-2026.
- **Sin funding**: solo GitHub Sponsors; ni VC ni grants.
- **Adopción no verificable**: ~121× menos descargas npm que Yjs; sin "Used by"; testimonios anónimos.
- **Sin .NET oficial**: solo `loro-cs` comunitario "bare bones" + NuGet comunitario; sin servidor oficial.
- Lo único sólido: excelencia del core (encoding estable, releases mensuales) — ya cierto en Ronda 12.

Para un proyecto .NET, adoptar Loro cargaría la integración sobre un binding comunitario frágil sin
red de equipo ni funding. No compensa.

## 6. Sobre el enfoque de binding (objetivo #15, con datos)

`uniffi-bindgen-cs` genera **31.885 LOC C#** de la API completa de Loro — apalancamiento enorme,
pero hereda la superficie de un tercero, no controla los patrones y suma un **acople triple de
versiones** (loro ↔ loro-ffi/UniFFI ↔ generador de NordSecurity, que va detrás). El **shim manual
+ `[LibraryImport]`** (nuestras ~40 LOC a medida) sigue ganando para "diseñar a nuestros patrones".
Misma conclusión que Spike 01, ahora con evidencia.

## 7. Gatillo de reevaluación (dual-path)

Reconsiderar Loro **solo si** aparece: (a) binding/servidor **.NET oficial** de loro-dev, **o**
(b) **producción verificable** de terceros con nombre, **o** (c) **funding + equipo** que baje el
bus-factor de 1. Hasta entonces: **yrs**, con la abstracción del motor reforzada.

---

## 8. Qué sigue

- **Proceder a la fase de construcción** de la capa .NET sobre el shim de yrs (Spike 01).
- **Reforzar la abstracción del motor** (interfaz `ICrdtEngine` o equivalente) para preservar la
  opción Loro a bajo coste — este spike prueba que la paridad Loro es alcanzable en horas si el
  gatillo se activa.
- Loro queda **archivado como alternativa viva**, no descartado.

---

_El código es desechable. Persisten la matriz, el veredicto (mantener yrs / dual-path) y el gatillo._
