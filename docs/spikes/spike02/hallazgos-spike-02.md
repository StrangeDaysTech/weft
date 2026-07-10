# Hallazgos — Spike 02: comparación de core CRDT yrs vs Loro

_StrangeDaysTech · 2026-07-09 · Código desechable; persiste la matriz + el veredicto._

## Veredicto de la puerta de decisión (§9): 🟡→🟢 **MANTENER yrs · postura DUAL-PATH documentada**

**Se confirma `yrs` como core** y se procede a la fase de construcción de la capa .NET sobre el
shim de Spike 01. Loro es un **fundamento técnicamente válido** (pasa la misma paridad, con ventajas
reales en historial/thread-safety), pero **NO muestra una ventaja decisiva** en las dimensiones que
importan a nuestro diseño, y su **riesgo estratégico sigue siendo descalificante** para un proyecto
.NET. Se mantiene y refuerza la **abstracción del motor** (ADR-4) para que Loro siga siendo
intercambiable, y se documenta el **gatillo** concreto de reevaluación.

La hipótesis de §3 —"Loro no ofrece ventaja neta suficiente para justificar el cambio"— **se
confirma**. La barra alta y deliberada para cambiar no se alcanzó.

---

## A · Paridad con Spike 01 (objetivos 1–10, ahora sobre Loro)

Todos los criterios obligatorios **PASS** (`8 PASS, 0 FAIL`). Resumen por objetivo:

1. **Ergonomía/esfuerzo FFI.** Shim propio sobre el crate `loro`: **268 LOC Rust / 12 funciones**
   (9 de paridad + 3 sondas), **37 LOC** de P/Invoke C#. Esfuerzo casi idéntico a yrs (205/9). El
   camino conocido de Spike 01 se reutilizó tal cual (SafeHandle, resolver por RID, harness ASan).
2. **Ciclo de vida de handles.** Mismo `SafeHandle` que Spike 01. Sin cambios.
3. **Marshalling de bytes.** Idéntico: `ReadOnlySpan<byte>` de entrada (pin, cero copias), out-params
   + `sdt_buf_free` de salida (una copia).
4. **Ownership entre fronteras.** Mismo contrato documentado; ASan/LSan lo validan (0 fugas).
5. **Thread-safety — DIFERENCIA A FAVOR DE LORO.** `LoroDoc` es **Send+Sync** (verificado con
   `_assert_send_sync::<LoroDoc>()` en compilación): Loro serializa internamente. El wrapper C#
   **no necesita `lock` por documento** (yrs sí lo necesitaba). Ergonomía algo mejor y menos código.
6. **Manejo de errores.** Mismo esquema i32 + `catch_unwind`; blob corrupto → `-2 DECODE` → excepción.
7. **Version-pinning — LEVE VENTAJA LORO.** `loro = "=1.13.6"`, **1.x con encoding estable** desde
   oct-2024 (verificado: sin breaking al formato 1.0; la única breaking post-1.0 fue quitar el
   decoder legado v0.x en 1.9.0). yrs es 0.x. En ambos, un bump toca **solo `lib.rs`** (el shim
   aísla). La promesa nominal 1.0 de Loro da más confianza para persistir años (ver #13).
8. **Empaquetado nativo.** Mismo patrón RID. `.so` **3.8 MB stripped** (vs 1.1 MB yrs) y build **~25 s**
   (vs ~7.8 s) — Loro tiene bastantes más dependencias. Desventaja menor pero real.
9. **Reachability rich-text.** ✅ vía `LoroText` + marcas (Fugue). Loro **no tiene `XmlFragment`**;
   su rich-text es texto-con-marcas. Alcanzable sin fricción por el shim (exponemos lo que queremos).
10. **Content-addressing — CLAVE, EMPATE.** El export de Loro es **byte-determinista**: dos docs
    convergidos producen **snapshot idéntico Y updates idénticos** (SHA-256 iguales cross-nodo),
    igual que el update v1 de yrs. `SHA-256(snapshot)` sirve como id de versión estable cross-nodo,
    sin forma canónica. **Loro iguala a yrs en nuestra fila más importante.**

## B · Diferenciadores de Loro (objetivos 11–15, la razón del spike)

11. **Historial/versionado nativo — la mayor ventaja de Loro, pero NO decisiva.**
    Sondas ejecutadas: **time-travel** (`checkout` a `frontiers` pasadas: leímos estado histórico
    `"version-1"` y re-attachamos a `"version-1 [FUTURO]"`) y **shallow snapshot** (poda de historia).
    Loro da nativo lo que con yrs construiríamos: consultar estado pasado, podar historia, fork/branch.
    **Por qué no es decisivo:** (a) Spike 01 ya nos da IDs content-addressed deterministas gratis, y
    **construimos nuestra capa de versionado igual** (la necesitamos a nuestro modelo citable); (b) el
    motor está abstraído, así que el time-travel de Loro sería un plus, no un pilar insustituible.
    _Matiz honesto de la sonda:_ con historia pequeña (500 ediciones, doc diminuto) el shallow
    snapshot (357 B) salió **mayor** que el completo (329 B) por overhead — el beneficio de poda solo
    aparece con historia grande.
12. **Substrato rich-text.** Loro: `LoroText`+marcas (Fugue) + `LoroTree` para jerarquía; existe
    `loro-prosemirror`. yrs: `XmlFragment`/`XmlElement`, que mapea muy directo al árbol de ProseMirror
    (y `y-prosemirror` es el estándar de facto del editor). Para ProseMirror/Tiptap, el modelo XML de
    yrs es el **más batido en producción**; el de Loro es limpio pero menos probado. Ligera ventaja
    de madurez para yrs, no de capacidad.
13. **Estabilidad de encoding a largo plazo.** Loro **promete** 1.0 estable (explícito, verificado sin
    breaking). Yjs es "estándar de facto batido en producción" sin promesa nominal. Para persistir
    años: Loro gana en promesa explícita; yrs gana en años de datos reales en producción. **Empate de
    confianza por vías distintas.**
14. **Rough perf (no decisivo).** 5000 inserts + export(10 KB) + import = **8 ms**. Muy rápido; sin
    fricción. Loro declara 10-100× en import; no lo medimos rigurosamente (no era el objetivo).
15. **UniFFI vs shim manual — objetivo #15.** Generamos el binding C# oficial: `loro-ffi` (UniFFI
    0.31.1, UDL 2192 LOC, Rust 4627 LOC) + `uniffi-bindgen-cs` 0.11.0 → **31.885 LOC C# auto-generadas**.
    UniFFI da apalancamiento enorme (API completa, cero P/Invoke a mano) **pero** hereda la superficie
    de un tercero, no controla los patrones, y añade un **acople triple de versiones** (`loro` ↔
    `loro-ffi`/UniFFI ↔ `uniffi-bindgen-cs`, este último un tercero —NordSecurity— que va detrás:
    soporta hasta UniFFI 0.31.0 mientras loro-ffi ya usa 0.31.1). **No cambia nuestra preferencia:**
    el shim manual sigue ganando para "diseñar a nuestros patrones". (Detalle en `uniffi-arm/README.md`.)

## C · Re-verificación estratégica (objetivo 16) — el factor decisivo

Research con fuentes (jul-2026, ver detalle abajo). **El disparador de reevaluación NO se activó:**

- **Bus-factor = 1.** Zixuan Chen (@zxch3n) es el único autor humano activo (~76% de contribuciones);
  el 2.º dev (Leon Zhao) sin commits desde abr-2026. La org tiene 4 miembros pero la "cola larga" son
  aportes de 1 commit. _(api.github.com/repos/loro-dev/loro/contributors, /commits)_
- **Sin funding institucional.** Solo GitHub Sponsors (~10 sponsors); ni VC ni grants verificables.
  _(github.com/sponsors/loro-dev, FUNDING.yml)_
- **Adopción en producción no verificable.** npm ~51 K/sem vs Yjs ~6.2 M/sem (**~121×**); crates ~2.6×
  menos que yrs. Sin sección "Used by"; testimonios anónimos. Yjs tiene y-sweet, Jupyter, BlockNote
  verificables. _(api.npmjs.org, crates.io/api)_
- **Sin binding/servidor .NET oficial** — el gatillo clave. Solo `sensslen/loro-cs` comunitario
  ("very bare bones", 9 stars) + NuGet comunitario `LoroCs`. loro-ffi lista C# como *community*, no
  oficial. Sin SaaS/servidor gestionado (solo `protocol` self-host v0.x). _(github.com/loro-dev/loro-ffi,
  sensslen/loro-cs, nuget.org/packages/LoroCs)_
- **Lo único sólido de Loro:** excelencia técnica del core (encoding 1.0 estable, releases mensuales,
  última 1.13.6 de jun-2026). Pero eso ya era cierto en la Ronda 12.

**Conclusión estratégica:** para StrangeDaysTech (que necesita .NET), adoptar Loro pondría el peso
de la integración sobre un binding comunitario frágil, sin red de funding ni de equipo. El riesgo de
sostenibilidad (bus-factor 1) es alto y **no compensa** la ventaja técnica del historial.

---

## Gatillo de reevaluación (para la postura dual-path)

Volver a evaluar Loro **solo si aparece al menos uno** de:
1. **Binding y/o servidor .NET OFICIAL** de loro-dev (no comunitario), **o**
2. **Casos de producción verificables de terceros con nombre** (a la escala de y-sweet/Jupyter), **o**
3. **Funding + ampliación de equipo** que baje el bus-factor por debajo de 1 (≥2 mantenedores activos
   sostenidos + respaldo financiero).

Mientras tanto, **la abstracción del motor (ADR-4) se mantiene y se refuerza** en la capa .NET, para
que el coste de cambiar —si el gatillo se activa— siga acotado. El brazo Loro de este spike
(`sdt_crdt_ffi_loro`) queda como prueba viva de que la paridad es alcanzable rápido si hiciera falta.

## Cómo reproducir

```bash
export SSL_CERT_FILE=/etc/pki/ca-trust/extracted/pem/tls-ca-bundle.pem
cd sdt_crdt_ffi_loro && cargo build --release
cp target/release/libsdt_crdt_ffi_loro.so ../dotnet/Spike02/runtimes/linux-x64/native/
cd ../dotnet/Spike02 && dotnet run -c Release            # -> 8 PASS, 0 FAIL
cd ../../sdt_crdt_ffi_loro && RUSTFLAGS="-Zsanitizer=address" \
  cargo +nightly test --release --target x86_64-unknown-linux-gnu --test mem_asan
# UniFFI (objetivo #15): ver uniffi-arm/README.md
```

Versiones: `loro =1.13.6` · .NET SDK 10.0.109 (`net10.0`) · Rust stable 1.96 / nightly 1.99 ·
`loro-ffi` UniFFI 0.31.1 · `uniffi-bindgen-cs` 0.11.0+v0.31.0.
