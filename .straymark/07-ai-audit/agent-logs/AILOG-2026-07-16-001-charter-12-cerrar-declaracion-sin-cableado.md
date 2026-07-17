---
id: AILOG-2026-07-16-001
title: "CHARTER-12: cerrar la clase «declaración de superficie sin cableado» — paridad header↔binding, guard de filtros, comentarios que mienten"
status: accepted
created: 2026-07-16
agent: claude-opus-4-8
confidence: high
review_required: true
risk_level: low
eu_ai_act_risk: not_applicable
nist_genai_risks: []
iso_42001_clause: []
observability_scope: none
tags: [follow-ups, phantom-verification, header-binding-parity, ci-guard, sc-009, false-comments, ffi-boundary]
related: [AILOG-2026-07-15-003, AILOG-2026-07-15-002]
originating_charter: CHARTER-12-cerrar-declaracion-sin-cableado
---

# AILOG: CHARTER-12 — cerrar la clase «declaración de superficie sin cableado»

## Summary

Despacho de los cuatro follow-ups accionables del backlog (FU-017/018/019/020) más los hallazgos
adyacentes que la investigación destapó, todos de la misma clase: *verificación fantasma* — una
declaración que pasa en verde sin verificar nada, nombrada en CHARTER-11. Primero de tres Charters que
vacían el backlog antes del publish (T060).

El hallazgo que ordenó el Charter: **el repo afirmaba en dos sitios que existía un test de CI que no
existía.** `NativeMethods.cs:8` y `weft_ffi.h:4-6` decían «un test de CI valida que las declaraciones
`[LibraryImport]` coinciden con este header». No había ninguno —ni para yrs ni para Loro—, y esa
afirmación imaginaria fue la que engañó a quien redactó FU-017 («replicar para Loro el test que yrs
tiene»). No había nada que replicar; había que crearlo por primera vez.

Con esto el backlog baja de 7 open a 3 (FU-010, FU-015, FU-016).

## Actions Performed

1. **FU-017 — `tests/Weft.Versioning.Tests/HeaderBindingParityTests.cs`** (nuevo): paridad
   header↔binding para **ambos** shims. Parser acotado del subconjunto de C que estos headers usan +
   reflexión sobre `NativeMethods`; compara conjunto de funciones, aridad, orden y tipos (mapa C↔.NET).
   **Probado contra mutaciones reales del header** (función extra → detectada; `size_t`→`uint32_t` →
   detectada) y con dos casos negativos en el propio archivo (el parser detecta divergencias sintéticas
   y **revienta ante lo que no entiende** en vez de igncrarlo — ignorar sería el fantasma que persigue).
   El doc-comment declara explícitamente **qué NO cubre** (semántica, marshalling, ownership: eso sigue
   siendo ASan + round-trips), porque exagerar el alcance de un test es la clase que este Charter cierra.
2. **FU-017 (b)** — corregidos los dos comentarios que afirmaban el test inexistente (`NativeMethods.cs:8`,
   `weft_ffi.h:4-6`); ahora son ciertos, y `weft_ffi.h` aclara que el header se mantiene a mano (no hay
   csbindgen; la mención en research R1 era aspiracional).
3. **FU-017 (c)** — completado el header de Loro: declara `weft_loro_test_panic` bajo
   `#ifdef WEFT_TEST_HOOKS` (el shim lo exporta, `lib.rs:472`) y marca su ABI (v2) explícitamente.
4. **Hueco de gate SC-009 (NUEVO)** — `release.yml` usaba `grep -q weft_test_panic`, que **no caza**
   `weft_loro_test_panic` (no es substring): el shim de Loro quedaba fuera del gate. Ampliado a ambos
   símbolos. **Verificado localmente** contra un cdylib de Loro con `test-hooks`: el gate viejo lo dejaba
   pasar; el nuevo lo caza.
5. **Control positivo del gate SC-009 (NUEVO)** — el gate pasaba en verde si `nm`/`strings` faltaban
   (`... 2>/dev/null | grep -q` no distingue «no está el símbolo» de «no está la herramienta»). Era la
   clase de FU-020 **dentro del propio gate**. Ahora: la herramienta debe existir, y debe demostrar que
   ve `abi_version` (símbolo de control que SÍ está) antes de creer una ausencia. **Al montarlo apareció
   un bug propio**: el control buscaba `weft_abi_version`, que por el mismo motivo de substring no casa
   con `weft_loro_abi_version`; corregido a `abi_version`. Probado en 4 casos (limpio/contaminado ×
   yrs/loro).
6. **FU-020 — `.github/scripts/check-test-filters.sh` + job `test-filters`** (bloqueante): descubre cada
   `dotnet test --filter X` en los archivos vigilados y exige que `--list-tests --filter X` case con ≥1
   test (señal: el texto «No test matches the given testcase filter»). Cubre el fantasma vivo de
   `ci.yml` (`FullyQualifiedName~RedisDocumentStoreContractTests`). El guard **falla si deja de
   encontrar filtros** (no se deja ciego a sí mismo). **Probado**: pasa con los 2 filtros reales; falla
   con un fantasma inyectado.
7. **FU-018 — ~10 comentarios falsos** corregidos en `ci.yml` (cabecera del job `fuzz` con el falso
   `continue-on-error` y el obsoleto «M2»; warnings de los steps; `with_capacity` genérico ya no cierto
   para `apply_update`; «matriz de sanitizers» inexistente; nombre del job; «gates que se activan en
   fases posteriores» ya activos; cross-impl «se añade en US4» ya hecho y bloqueante), `CONTRIBUTING.md`
   (paridad Yjs marcada «no-bloqueante» → bloqueante; ruta del brief) y `README.md` (`NOTICE`
   inexistente, `native/weft-ffi/` layout muerto, ruta del brief).
8. **Comando roto + evidencia falsa (NUEVO, error propio de CHARTER-11)** — `quickstart.md:37`
   (`dotnet test tests/A tests/B` → MSB1008) partido en dos comandos; `checklists/requirements.md:45`
   afirmaba «ese comando → 58/58», que era falso (se corrió `dotnet test Weft.sln` y se sumó a ojo; el
   real es 28+36=64). Corregido con la nota de que fue R4 de CHARTER-11 materializándose.
9. **FU-019 — footgun de pack local** documentado en `CONTRIBUTING.md`: empaquetar desde un árbol
   compilado con `--features test-hooks` metería el símbolo de test en el `.nupkg`; el gate solo corre
   en `release.yml`.
10. **Pase adversarial** de las ~10 correcciones contra el código en HEAD (mitigación de R4): sin
    afirmaciones nuevas falsas. Incluyó verificar contra la fuente de yrs 0.27.2 en el caché de cargo
    que `Update::decode` usa `try_reserve` (afirmación sobre código de terceros).

## Risk

Riesgos del Charter (R1–R6) y su desenlace:

- **R1 (el parser de C se vuelve el problema)** — mitigado y probado: el parser revienta ante lo que no
  entiende (test dedicado) en vez de ignorarlo, y su capacidad de fallar se verificó con mutaciones
  reales del header, no solo con verde.
- **R2 (falsa seguridad: paridad sintáctica, no semántica)** — mitigado: el doc-comment declara el
  alcance exacto; la semántica sigue en ASan + round-trips.
- **R3 (el guard solo cubre `--filter`)** — aceptado y acotado por escrito: los comandos rotos fallan en
  rojo (otra clase), como demostró `quickstart.md:37`.
- **R4 (corregir comentarios introduce falsedades nuevas)** — **no se materializó**, y esta vez la
  mitigación se aplicó con disciplina: cada corrección anclada a `archivo:línea` en HEAD (incluido el
  YAML, que fue donde falló en CHARTER-11) + pase adversarial. Contraste explícito con CHARTER-11, donde
  sí se coló una falsedad heredada de un comentario.
- **R5 (control positivo del gate difícil sin binario con el símbolo)** — resuelto usando un símbolo de
  control ya presente (`abi_version`), sin duplicar la matriz.
- **R6 (colisión con CHARTER-14 en el header de Loro)** — es la razón del orden 12→14; este Charter no
  toca el valor del ABI, solo declara el símbolo de test y documenta v2. CHARTER-14 sube a v3 y el test
  de paridad —ya existente— validará ese cambio.

**R7 (nuevo, no en el Charter) — mi propio gate reescrito tenía el mismo bug que arreglaba.** El control
positivo del gate SC-009 buscaba `weft_abi_version`, que no casa con `weft_loro_abi_version` por el mismo
motivo de substring que dejaba fuera a `weft_loro_test_panic`. Lo cacé **porque probé el gate contra un
binario de Loro real**, no porque lo revisara. Es la lección recurrente en una variante nueva: un gate no
verificado ejecutándose es tan fantasma como el que corrige. Corregido a `abi_version` y reprobado.

## Follow-ups

Ninguno nuevo. Los cuatro accionables quedan cerrados; el pase adversarial no dejó residuo.

Nota sobre lo NO tocado (decisión consciente, ver §Out of scope del Charter): el `pack-smoke` por-PR
sigue siendo un marcador `echo` —su refactor a validación real es coste de matriz cross-compile, decisión
de CHARTER-07— pero su comentario ahora lo dice con honestidad en vez de fingir que valida el empaquetado.

## Verification

```bash
cargo build --release --features test-hooks --manifest-path native/Cargo.toml
dotnet test Weft.sln -c Release                                          # 138/138 (6 nuevos de paridad)
dotnet test tests/Weft.Versioning.Tests -c Release --filter "FullyQualifiedName~HeaderBindingParity"  # 6/6
bash .github/scripts/check-test-filters.sh                              # los 2 filtros casan; falla con un fantasma
# Gate SC-009 probado local: caza weft_test_panic Y weft_loro_test_panic; falla si la herramienta está ciega
python3 -c "import yaml; yaml.safe_load(open('.github/workflows/ci.yml')); yaml.safe_load(open('.github/workflows/release.yml'))"
straymark validate --include-charters
```
