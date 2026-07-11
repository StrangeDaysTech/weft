---
audit_role: calibrator-reconciler
calibrator: claude-opus-4-8
charter_id: CHARTER-02-versioning-dual-engine
git_range: "origin/main..HEAD"
prompt_used: ../audit-prompt.md
calibrated_at: 2026-07-10
auditors_reconciled:
  - report-gpt-5-5.md
  - report-qwen3-7-max.md
  - report-gemini-3-1-pro.md
findings_consolidated: 5
findings_by_status:
  agreed: 1
  disputed: 0
  unique_gpt-5-5: 1
  unique_qwen3-7-max: 2
  unique_gemini-3-1-pro: 0
  rejected: 0
---

# Consolidated audit review — CHARTER-02-versioning-dual-engine

**Reviewer:** claude-opus-4-8
**Date:** 2026-07-10
**Confidence:** High

## 1. Executive summary

Tres auditores de familias distintas (GPT, Qwen, Gemini) revisaron el corte T022–T035. Reportaron
7 findings en bruto que se consolidan en **5 únicos**, **todos VÁLIDOS, ninguno crítico ni bloqueante
para M0, cero falsos positivos y cero misattributions**. El código central de versionado
content-addressed y el gate dual-engine están correctos y bien probados: los tres gates de M0 (P-II
ASan, P-III determinismo, P-IV dual-engine) pasan en CI multiplataforma, y la misma suite de 7
postcondiciones corre idéntica sobre yrs y Loro.

El hallazgo de mayor peso es **de trazabilidad, no de código**: las tareas T032/T033 están marcadas
`[X]` (completas) pero sus deliverables incluyen la superficie de versionado nativo de Loro (probes
`native_diff`/`native_branch`/`shallow_snapshot`, header `weft_loro_ffi.h`, `LoroNativeVersioning.cs`)
que **no se implementó** — `LoroEngine.NativeVersioning` devuelve `null` (diferido por diseño), y
`quickstart.md:87` todavía exige `NativeVersioning != null`. Es una capacidad OPCIONAL de la que
ningún gate ni postcondición depende, pero el marcador `[X]` sobre-declara lo entregado. El segundo
gap es la **ausencia del AIDEC** que el Charter exigía. Los otros tres son deuda menor (real_debt) que
va al backlog.

**Veredicto: M0 es cerrable tras reconciliar la trazabilidad (G1) y crear el AIDEC (G2); G3–G5 a
follow-ups.** El fix R7 (Loro `all_updates`) que motivó la re-auditoría fue verificado por Gemini
(cita `lib.rs:213-225`) y no generó findings — buena señal de que el corte auditado es el correcto.

**Independence check**: ningún reporte referencia a los otros; sin contaminación. La convergencia
GPT↔Qwen sobre G1 es señal independiente genuina.

## 2. Scope definition

| Tareas | Criterio de cierre | En scope | Fuera de scope |
|--------|--------------------|----------|----------------|
| T022–T035 (US1 versionado + US5 dual-engine) | M0 cerrado: gates P-III (determinismo) y P-IV (dual-engine) activos + auditoría externa | Versionado content-addressed, adaptador Loro, suite dual-engine, gates CI, fix R6/R7 (scope expansion documentada) | Broker/US2 (M1), servidor/US3 (M2), NuGet multi-RID/US4 (M3), hardening de amplificación de decode |

Los findings se evalúan contra este scope. La superficie de `INativeVersioning` de Loro es una
capacidad **opcional** por contrato (core-api.md: "null si el motor no la ofrece"); su ausencia no
viola ningún gate de M0, pero SÍ contradice los marcadores `[X]` y el `quickstart.md`.

## 3. Per-auditor evaluation

### 3.1 gpt-5-5 (model: gpt-5-5)

| # | Finding | Sev. reportada | Verdicto | Justificación |
|---|---------|----------------|----------|---------------|
| M1 | Superficie Loro native-versioning ausente (probes+header+`LoroNativeVersioning`), tareas `[X]`, quickstart espera `!= null` | Medium | **VALID** | Verificado: quickstart:87 exige `!= null`; grep sin `LoroNativeVersioning`/probes; `include/` no existe; T032/T033 `[X]`. No bloqueante (opcional). |
| M2 | Falta AIDEC exigido por el Charter | Medium | **VALID** | Verificado: `.straymark/07-ai-audit/decisions/` vacío. El Charter §Tasks lo pedía. |

**Summary:** El mejor reporte: 170 citas con `path:line`, traceabilidad task-by-task completa, y
respeto estricto de la disciplina solo-lectura (declaró explícitamente que NO corrió build/test para
no escribir artefactos). Único auditor que detectó G2 (AIDEC). Calibración de severidad correcta
(Medium, no bloqueante).

### 3.2 qwen3-7-max (model: qwen3-7-max)

| # | Finding | Sev. reportada | Verdicto | Justificación |
|---|---------|----------------|----------|---------------|
| 1,2,3 | Probes / header / `LoroNativeVersioning` ausentes (desglose de G1) | Medium | **VALID (= G1)** | Mismo gap que gpt M1, correctamente desglosado en sus 3 componentes. |
| 4 | `InMemoryBlobStore` key = hex string vs `VersionId` directo | Low (real_debt) | **VALID** | Verificado: `ConcurrentDictionary<string,byte[]>` con `id.ToString()` por operación. Mejora menor. |
| 5 | `Merge` sin engine-compatibility check (cross-engine → `CorruptUpdateException` opaca) | Low (real_debt) | **VALID** | Verificado: `target.ApplyUpdate(branch.ExportState())` sin guard. Ningún path actual lo dispara. |

**Summary:** Muy sólido: task-by-task con verificación, y el único que aportó los dos real_debt
(G3/G4) que gpt no vio. **Salvedad de disciplina**: corrió `dotnet test`/`cargo test`/`cargo build`
(sección "Compilation and test verification" con outputs), escribiendo artefactos bajo el repo — el
prompt permite build/test "cuando aplica" pero declara que la única escritura permitida es el reporte;
tensión menor, no invalida los hallazgos. No detectó G2.

### 3.3 gemini-3-1-pro (model: gemini-3-1-pro)

| # | Finding | Sev. reportada | Verdicto | Justificación |
|---|---------|----------------|----------|---------------|
| — | Ninguno (0 findings) | — | — | No detectó ninguno de los 4 gaps reales. |

**Summary:** Auditoría superficial: solo 2 citas de evidencia, traceabilidad de 2 de 14 tareas (T022,
T032), "closure highly recommended" sin ver la desalineación T032/T033-vs-código ni la falta de AIDEC
que sus pares sí encontraron. Verificó correctamente el fix R7 (`all_updates`), lo cual es útil, pero
la cobertura es insuficiente para un veredicto de cierre.

## 4. Remediation plan — VALID y PARTIALLY VALID findings

### P2 — Consistency (trazabilidad — antes del cierre)
- **G1** — Superficie Loro native-versioning declarada pero no entregada.
- **Files:** `specs/001-weft-crdt-versioning/tasks.md` (T032/T033 `[X]`), `.../quickstart.md:87`, `src/Weft.Loro/LoroEngine.cs:24`
- **Problem:** `INativeVersioning` de Loro (probes + header + `LoroNativeVersioning.cs`) marcada completa pero diferida (`NativeVersioning => null`). Ningún gate depende de ella, pero `[X]` y quickstart sobre-declaran.
- **Remediation (recomendada):** RECONCILIAR — marcar los sub-deliverables de native-versioning de T032/T033 como diferidos (no `[X]` completo) + nota en `quickstart.md` §US5 de que `NativeVersioning != null` es post-M0 + entrada de follow-up. Alternativa: implementar la superficie (más trabajo, capacidad opcional). **Detected by:** gpt-5-5, qwen3-7-max.
- **Complexity:** Low (reconciliación documental).

### P4 — Documentation
- **G2** — Falta el AIDEC exigido por el Charter.
- **Files:** `.straymark/07-ai-audit/decisions/` (vacío), `.straymark/charters/02-versioning-dual-engine.md` §Tasks
- **Problem:** Hubo decisiones sustantivas (export `all_updates` vs Snapshot [R7], `OffsetKind::Utf16` [R6], diferir `NativeVersioning`, tokenización LCS, sharding) documentadas solo en el AILOG en prosa.
- **Remediation:** Crear un AIDEC que registre esas decisiones con alternativas consideradas (`/straymark-aidec`), o enmendar el Charter con el racional de por qué el AILOG basta. **Detected by:** gpt-5-5.
- **Complexity:** Low.

### Follow-ups (real_debt — al backlog, no bloqueantes)
- **G3** (`InMemoryBlobStore` key `VersionId` directo, ahorra alloc del hex) — `src/Weft.Versioning/Blobs/InMemoryBlobStore.cs:9`. Detected by qwen3-7-max.
- **G4** (guard de engine-compatibility en `Merge`/`ExecuteAsync`) — `src/Weft.Versioning/VersionStore.cs:67`. Detected by qwen3-7-max.
- **G5** (`FileSystemBlobStore` T024 sin test directo — solo InMemory se ejercita) — `tests/`. **Missed by all auditors** (gpt/qwen lo notaron como observación pero no lo elevaron a finding). Complexity Low: añadir un test de round-trip + sharding con dir temporal.

## 5. Discarded findings — misattributions and false positives

| Finding | Type | Charter / area | Auditor |
|---------|------|----------------|---------|
| (ninguno) | — | — | — |

Cero falsos positivos y cero misattributions across los tres auditores. La corrección de índices
UTF-16 (R6) fue correctamente tratada por gpt-5-5 como scope expansion documentada, no como defecto.

## 6. Auditor ratings

| Auditor | Scope precision (25%) | Technical depth (25%) | Bug detection (30%) | False positive rate (20%) | **Overall** |
|---------|:-:|:-:|:-:|:-:|:-:|
| gpt-5-5 | 10 | 9 | 8 | 10 | **9.2** |
| qwen3-7-max | 8 | 9 | 9 | 10 | **8.9** |
| gemini-3-1-pro | 6 | 3 | 2 | 10 | **4.1** |

### Justifications

**gpt-5-5 — 9.2/10**: Traceabilidad exhaustiva (170 citas), disciplina solo-lectura impecable, y
único en detectar el gap del AIDEC. Pierde algo en bug detection por no aportar los real_debt de qwen.

**qwen3-7-max — 8.9/10**: Detección más amplia (los dos real_debt que nadie más vio), buena
profundidad. Penalizado en scope precision por correr build/test (escribió artefactos, tensión con la
disciplina solo-lectura declarada) y por no ver el AIDEC.

**gemini-3-1-pro — 4.1/10**: Cero falsos positivos (no arriesgó), pero cobertura y detección muy por
debajo: 2/14 tareas traceadas, ninguno de los 4 gaps reales encontrado, veredicto de cierre emitido
sin la evidencia que lo sustente. Útil solo como confirmación del fix R7.

## 7. Conclusion

**Estado del Charter: limpio en código, con reconciliación de trazatabilidad pendiente.** Cero bugs
de correctitud, seguridad o lógica; cero findings críticos. El versionado content-addressed y el gate
dual-engine cumplen su contrato sobre ambos motores.

**Antes de `straymark charter close CHARTER-02`:**
1. **G1** — reconciliar T032/T033 (marcar native-versioning como diferido) + `quickstart.md` §US5, o implementar la superficie. Recomendado: diferir explícitamente (es opcional).
2. **G2** — crear el AIDEC de las decisiones (R6, R7, diferido de NativeVersioning, LCS, sharding).
3. **G3/G4/G5** — al follow-ups backlog (no bloqueantes).

Convergencia cross-familia: GPT y Qwen coinciden independientemente en G1, lo que le da alta
confianza. Gemini no aporta cobertura suficiente. Recomendación: remediar G1+G2 en el mismo PR de
CHARTER-02, registrar G3–G5 como follow-ups, y cerrar.
