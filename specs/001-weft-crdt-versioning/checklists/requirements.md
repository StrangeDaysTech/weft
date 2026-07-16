# Specification Quality Checklist: Weft — Colaboración CRDT en tiempo real y versionado content-addressed para .NET

**Purpose**: Validate specification completeness and quality before proceeding to planning
**Created**: 2026-07-10
**Feature**: [spec.md](../spec.md)

## Content Quality

- [x] No implementation details (languages, frameworks, APIs)
- [x] Focused on user value and business needs
- [x] Written for non-technical stakeholders
- [x] All mandatory sections completed

## Requirement Completeness

- [x] No [NEEDS CLARIFICATION] markers remain
- [x] Requirements are testable and unambiguous
- [x] Success criteria are measurable
- [x] Success criteria are technology-agnostic (no implementation details)
- [x] All acceptance scenarios are defined
- [x] Edge cases are identified
- [x] Scope is clearly bounded
- [x] Dependencies and assumptions identified

## Feature Readiness

- [x] All functional requirements have clear acceptance criteria
- [x] User scenarios cover primary flows
- [x] Feature meets measurable outcomes defined in Success Criteria
- [x] No implementation details leak into specification

## Quickstart validation pass (T063 · CHARTER-11 · 2026-07-15)

Pase end-to-end de [quickstart.md](../quickstart.md) contra el árbol de `charter/11-polish-cierre-m3`
(HEAD sobre `main`@`603efce`), linux-x64, .NET 10.0.x + Rust stable/nightly.

**Convención de evidencia** — `[x]` significa *«lo ejecuté y lo vi»*, nunca *«debería funcionar»*:

- **Ejecutado**: corrido en esta máquina, con su resultado.
- **CI**: no ejecutable aquí (requiere runners por RID / matriz); lo cubre un job, que se nombra.
- **No ejecutado**: sin evidencia. Se queda **sin marcar**, con el motivo.

### User stories

- [x] **US1 — Editar y versionar desde .NET**. `dotnet test tests/Weft.Core.Tests tests/Weft.Versioning.Tests -c Release` → 58/58 verdes. `dotnet run --project samples/Weft.Sample.Versioning` → journey completo: `Publish` v1 (`c0d1c698…`) → `Diff(v1,v2)` por palabras → `Checkout(v1)` → `Merge` convergente.
- [x] **US2 — Concurrencia a escala**. `--filter Category=Concurrency` → 9/9 verdes (**antes del cableado de este Charter: 0 tests**). `dotnet run --project tests/Weft.LoadTest -c Release` → `PASS`: 300 docs, 45 004 ops, 449 327 desalojos, 0 errores, managed-heap 1 MB / working-set 101 MB (`consistencia=OK memoria-acotada=OK sin-errores=OK`).
- [x] **US3 — Colaboración en tiempo real**. `dotnet test tests/Weft.Server.Tests -c Release` → 70/70 verdes. Relay real arrancado (`:5199`, `FileSystemDocumentStore`) y smoke headless `npm run check` → convergencia real de 2 clientes Yjs vía `y-websocket` contra el relay: `"Hello from A. And B too."`. *Parcial*: ver «no ejecutado» abajo.
- [x] **US5 — Motor reemplazable**. `dotnet test tests/Weft.Versioning.Tests -c Release` → 30/30 verdes sobre `YrsEngine` **y** `LoroEngine` (SC-008). `LoroEngine.NativeVersioning` expone los 3 probes de CHARTER-10; `YrsEngine.NativeVersioning == null` sin romper ningún flujo.
- [ ] **US4 — Instalación multiplataforma**. Parcial. Ejecutado aquí: `dotnet pack src/Weft.Core -c Release` → `Weft.Core.1.0.0.nupkg` + `.snupkg`, con `runtimes/{linux-x64,linux-arm64}/native/` y **`weft_test_panic` ausente** en ambos `.so` (verificado con `nm -D`). Sin marcar porque el criterio de US4 es *instalación verde en máquina limpia en los 4 RIDs*, que no es ejecutable aquí → job `pack-smoke` / `pack-smoke-arm`.

### Gates

- [x] **Memoria (P-II)** — `RUSTFLAGS="-Zsanitizer=address" cargo +nightly test --features test-hooks --target x86_64-unknown-linux-gnu` → 14/14 verdes en ambos shims, **0 fugas / 0 double-free**, incluido `native_versioning_probes_reachable_and_nonleaking`.
- [x] **Determinismo (P-III)** — `dotnet test tests/Weft.Determinism.Tests` → 4/4, incluida la aserción **bloqueante** de paridad `Yrs_export_matches_yjs_golden`. Harness Node (`npm test`) → hash de Yjs coincide con `golden.json` en ascii (`27a84875…`) y unicode (`afd15f9c…`).
- [x] **Dual-engine (P-IV)** — suite de versionado verde sobre ambos motores (ver US5).
- [x] **Build + tests (P-VI)** — `dotnet test Weft.sln -c Release` → **132/132 verdes**; `cargo test --features test-hooks` → 14/14. Ejecutado en linux-x64; win-x64/osx-arm64 → jobs `test-win` / `test-mac`.
- [x] **Fuzzing (P-I/P-II)** — `cargo +nightly fuzz run doc_load -- -max_total_time=45` → **OOM reproducido** con un input de 4 bytes (`f6f4d621`). **Es el resultado esperado, no una regresión**: es R6; el shim es correcto (contiene panics, sin UB) y el fix vive upstream en [y-crdt#639](https://github.com/y-crdt/y-crdt/pull/639) (aprobado), adopción vía FU-015. En CI el job **bloquea a medias**: un crash sólo emite `::warning` (`|| echo` por paso), pero un fallo de compilación de los targets sí lo pone rojo. Caveat de la ruta directa en `GOVERNANCE.md` §Seguridad.
- [x] **Empaquetado (P-VI)** — pack local verde + ausencia del símbolo de test verificada con `nm -D` (ver US4). **El gate real no corre por PR**: el `pack-smoke` de `ci.yml` es un marcador; la matriz por RID y la verificación del símbolo viven en `release.yml` (`workflow_dispatch`) → se validan en el dry-run del release. Ver gap #9.

### No ejecutado (sin evidencia — deliberadamente sin marcar)

- **US3, validación manual con Tiptap real (2+ pestañas)**: requiere navegador e interacción humana. Es el criterio de cierre de **M2**, no de M3, y se cubrió en su momento. El `npm run check` headless valida la convergencia del wire, pero **no** sustituye la comprobación visual de presencia/cursores.
- **US4, instalación en máquina limpia por RID** (win-x64, osx-arm64, linux-arm64): requiere runners por RID.
- **T060, publish real a NuGet.org**: operador-gated por diseño; el `dry_run` se validó en CHARTER-07.

### Deriva del runbook detectada y corregida atómicamente

El pase fue la primera ejecución end-to-end de `quickstart.md` contra HEAD, y destapó **9 gaps**, todos
de *«declaración de superficie sin cableado»* ([POLISH-CHARTER-PATTERN.md](../../../.straymark/00-governance/POLISH-CHARTER-PATTERN.md)):
el runbook declaraba comandos y garantías que nadie había ejecutado ni releído. Ninguno era un fallo
del código de producción — el código estaba bien; el runbook mentía.

| # | Gap | Efecto real | Corrección |
|---|---|---|---|
| 1 | `--filter Category=Concurrency` sin ningún `[Trait]` en el repo | El paso de US2 pasaba **en verde ejecutando 0 tests** | `[Trait("Category","Concurrency")]` en `DocumentBrokerTests` → 9 tests |
| 2 | Build local sin `--features test-hooks` | `PanicSafetyTests` rojo (`EntryPointNotFoundException: weft_test_panic`) siguiendo el runbook al pie de la letra | Feature añadida al comando, con la razón y la garantía de que no viaja en release |
| 3 | `cp` desde `native/weft-yrs-ffi/target/release/` | Ruta **inexistente**: `native/` es un workspace cargo y comparte `native/target/` → el comando falla | Ruta corregida |
| 4 | El `cp` a `src/Weft.Core/runtimes/` | **Innecesario**: los `.csproj` de test ya copian desde `native/target/release/`, y el pack lee de `native/target/<triple>/release/` (`build/Weft.Native.targets`). El directorio destino ni existe | Paso eliminado |
| 5 | US3: «relay en `:5000`» | El sample escucha en **`:5199`** (`WEFT_SAMPLE_URLS`) | Puerto corregido + env var documentada |
| 6 | US3 sólo documentaba `npm run dev` (navegador) | `npm run check` —smoke headless de convergencia real vía `y-websocket`— existía y no estaba en el runbook: no había forma documentada de validar US3 sin display | Documentado |
| 7 | Gate de determinismo descrito como «no-bloqueante al inicio, promovible» | Induce a creer que la paridad yrs↔Yjs no bloquea. **Sí bloquea** desde CHARTER-09/FU-012 (vive en `Weft.Determinism.Tests`, job `test`); el job Node de `release.yml` es informativo y caza drift del upstream | Redacción corregida, separando ambos |
| 8 | Tabla de gates: «un rojo bloquea merge», con `fuzz` listado sin matiz | `fuzz` **bloquea a medias**: un crash sólo emite `::warning` (`\|\| echo` por paso), pero un fallo de compilación de los targets sí lo pone rojo. La promesa era imprecisa en ambos sentidos | Encabezado matizado + fila con la semántica exacta y su porqué (R6/FU-015) |
| 9 | Tabla de gates: `pack-smoke` listado como gate bloqueante por PR | **El más grave.** El `pack-smoke` de `ci.yml` es un **marcador que sólo hace `echo`**: no empaqueta ni valida nada. La matriz real y la verificación de `weft_test_panic` viven en `release.yml`, que es `workflow_dispatch` únicamente. El runbook prometía que cada PR valida el empaquetado; ningún PR lo valida | Fila reescrita diciendo dónde vive el gate real y cuándo corre |

## Notes

- **Lente aplicada a "no implementation details"**: Weft es una librería para desarrolladores — su API y sus contratos SON el producto. Conceptos de contrato que la spec sí nombra deliberadamente: content-addressing con SHA-256 (identidad de versión, decisión ✅ CERRADA del brief), protocolo de sync del ecosistema Yjs sobre WebSocket (requisito de interoperabilidad con clientes de editor existentes) y distribución NuGet multi-RID (requisito de entrega). Las decisiones de implementación **interna** (motor `yrs`, shim C-ABI en Rust, P/Invoke, ASan/LSan concretos) quedan fuera de los FRs y viven solo en Assumptions como contexto firme.
- 0 marcadores [NEEDS CLARIFICATION]: el brief de diseño es autocontenido y sus decisiones cerradas cubren los puntos que normalmente requerirían aclaración (alcance, licencia, auth delegada al consumidor, diferidos explícitos).
- Validación completada en 1 iteración (2026-07-10). La spec está lista para `/speckit-clarify` (opcional) o `/speckit-plan`.
