---
id: AILOG-2026-07-15-001
title: "CHARTER-09: client-id determinista en el FFI de yrs + gate de paridad cross-impl (determinism-yjs) per-PR"
status: accepted
created: 2026-07-15
agent: claude-opus-4-8
confidence: high
review_required: true
reviewed_by: Jose Villaseñor Montfort
reviewed_at: 2026-07-15
review_outcome: approved
risk_level: medium
eu_ai_act_risk: not_applicable
nist_genai_risks: []
iso_42001_clause: []
observability_scope: none
tags: [ffi-boundary, abi-bump, determinism, yrs, yjs, cross-impl-parity, client-id, utf16, gate]
related: [AIDEC-2026-07-15-001, AILOG-2026-07-13-003]
originating_charter: CHARTER-09-client-id-determinista-en-el-ffi-de-yrs-gate-de
---

# AILOG: CHARTER-09 — client-id determinista + gate de paridad cross-impl (determinism-yjs)

## Summary

Despacho de CHARTER-09 (FU-012): expone la siembra de `client_id` determinista en el FFI de yrs y **promueve el
gate de determinismo cross-implementación (`determinism-yjs`, T058) de informativo a aserción per-PR bloqueante**.
El riesgo central **R1 se cumple**: yrs produce exports **byte-idénticos** a Yjs sobre el corpus compartido
(ASCII y unicode) → el determinismo de Weft es **por formato** (encoding v1 de Yjs/yrs), no un accidente de esta
versión de yrs (constitución **P-III**). Alcance **yrs-only** decidido; la promoción cross-engine (Loro) se
difiere a **FU-016**. Sin auditoría externa (no cierra hito).

## Actions Performed

1. **FFI yrs — siembra de client_id (ABI v1→v2)**: `weft_doc_new_with_client_id(u64, out)` en
   `native/weft-yrs-ffi/src/lib.rs` (`Options { client_id: ClientID::new(id), offset_kind: Utf16 }`), con
   **guard `client_id < 2^53`** → `WEFT_ERR_OUT_OF_BOUNDS` (yrs 0.26+ codifica los client IDs en 53 bits;
   `ClientID::new` tiene `debug_assert!(value & MASK == 0)` y corrompería en release sin el guard). `WEFT_ABI_VERSION`
   1→2 + declaración en `include/weft_ffi.h`. Símbolo verificado exportado (`nm -D`).
2. **Binding .NET**: `weft_doc_new_with_client_id` en `NativeMethods.cs`; `YrsDoc.Create(ulong)`;
   **`YrsEngine.CreateDoc(ulong)` método CONCRETO** (no en `ICrdtEngine` — ver AIDEC decisión 1);
   `ExpectedAbiVersion` 1→2 en `NativeLibraryResolver.cs`.
3. **Golden + corpus unicode**: `apply.mjs` parametrizado (corpus por argumento + self-check contra
   `golden.json`); `corpus-unicode.json` (BMP acentuado + CJK + astrales/emoji → índices UTF-16, R6);
   `golden.json` comprometido con los hashes de Yjs de ambos corpus.
4. **Aserción de paridad per-PR (BLOQUEANTE)**: `Yrs_export_matches_yjs_golden` (Theory ascii+unicode) en
   `Weft.Determinism.Tests` — aplica el corpus con yrs vía `CreateDoc(clientId)`, converge, y asierta
   `sha256(ExportState) == golden`. Corre en el job `test` existente (per-PR, costo ~0). **2/2 verde.**
5. **Job Node informativo promovido**: `release.yml determinism-yjs` corre ambos corpus (`npm test`) y
   **self-checkea** su hash de Yjs contra `golden.json` (caza drift de Yjs); permanece `continue-on-error`.
   README del harness actualizado (estado → aserción per-PR yrs).
6. **Backlog**: FU-012 → `closed`; **FU-016** registrado (promoción cross-engine Loro vía `set_peer_id`).

## Modified Files

**Nativo**: `native/weft-yrs-ffi/src/lib.rs` (fn + guard + ABI v2), `native/weft-yrs-ffi/include/weft_ffi.h`.
**Binding**: `src/Weft.Core/Yrs/NativeMethods.cs`, `YrsDoc.cs`, `YrsEngine.cs`, `NativeLibraryResolver.cs`.
**Gate**: `tests/Weft.Determinism.Tests/DeterminismTests.cs` (test de paridad),
`tests/determinism-yjs/{apply.mjs, package.json, golden.json (new), corpus-unicode.json (new), README.md}`,
`.github/workflows/release.yml` (step del job). **Gobernanza**: `.straymark/follow-ups-backlog.md`
(FU-012 closed, FU-016), `.straymark/charters/09-*.md` (status), AIDEC-2026-07-15-001 (new).

## Risk

- **R1 (medio-alto, del Charter) — paridad yrs↔Yjs**: **RESUELTO POSITIVO.** El test asierta 2/2 (ascii+unicode):
  yrs == Yjs byte-idéntico. El gate se fija bloqueante; no fue necesario el plan B (dejarlo informativo).
- **R2 (medio) — client_id ≥ 2^53**: mitigado con el guard en la frontera (`WEFT_ERR_OUT_OF_BOUNDS`); alinea con
  el `debug_assert` de `ClientID::new`. El corpus usa 1/2/3 (seguros).
- **R3 (bajo) — ABI bump v1→v2**: bump atómico Rust (`WEFT_ABI_VERSION`) + .NET (`ExpectedAbiVersion`) en el mismo
  PR; el export es aditivo (no cambia `weft_doc_new`); desalineación → error explícito de `NativeLibraryResolver`.
- **R5 (bajo, del Charter) — índices UTF-16 en unicode**: mitigado con evidencia — la variante unicode (surrogate
  pairs astrales) pasa la aserción, confirmando la paridad de índices UTF-16 (`OffsetKind::Utf16`).

## Verification

```bash
# Shim yrs: compila + símbolo exportado + ABI v2
cd native/weft-yrs-ffi && cargo build --release && nm -D ../target/release/libweft_yrs_ffi.so | grep weft_doc_new_with_client_id

# Hash de Yjs de ambos corpus + self-check contra golden.json
cd ../../tests/determinism-yjs && npm install && npm test   # ✓ ascii + unicode coinciden con golden

# Aserción de paridad per-PR (bloqueante) + suite completa
cd ../.. && dotnet test tests/Weft.Determinism.Tests -c Release   # Yrs_export_matches_yjs_golden 2/2
dotnet test Weft.sln -c Release                                    # suite completa intacta
```

## Follow-ups

Derivado del alcance yrs-only de CHARTER-09. No bloquea nada:

- **Follow-up (cross-engine, baja)**: promover la siembra de client-id de capacidad **concreta de `YrsEngine`** a
  capacidad **cross-engine** — `CreateDoc(clientId)` en `ICrdtEngine` (o una interfaz opcional tipo
  `INativeVersioning`) + `weft_loro_doc_new_with_peer_id` en `weft-loro-ffi` (Loro vía `set_peer_id`), para
  habilitar un gate de determinismo Loro↔referencia si/cuando se quiera. **Trigger**: when se requiera paridad
  determinista para el motor Loro. **Destination**: mini-charter. **Cost**: M.

## Additional Notes

- El test de paridad localiza `tests/determinism-yjs/` subiendo desde `AppContext.BaseDirectory` hasta la raíz del
  repo (no requiere copiar el corpus al output del test); funciona local y en CI (checkout completo).
- La convergencia del test .NET espeja `apply.mjs` **exactamente** (delete sin guard de longitud, `syncPasses`
  del corpus, hash del `ExportState` de la réplica 0) — cualquier divergencia de esquema rompería la paridad.

## Approval

Trabajo de frontera nativa (`risk_level: medium`, `review_required: true`) con ABI bump. El operador decidió
ex-ante el alcance (yrs-only, aserción per-PR) y autorizó ejecución continua. Verificación local citada; el CI del
PR valida la aserción per-PR bloqueante en toda la matriz. Compañero de AIDEC-2026-07-15-001.
