---
id: AILOG-2026-07-15-003
title: "CHARTER-11: Polish cierre de M3 — doc de arquitectura, benchmark delta-size (SC-004) y validación quickstart"
status: accepted
created: 2026-07-15
agent: claude-opus-4-8
confidence: high
review_required: true
risk_level: low
eu_ai_act_risk: not_applicable
nist_genai_risks: []
iso_42001_clause: []
observability_scope: none
tags: [polish, architecture-doc, benchmark, sc-004, quickstart-validation, runbook-drift, m3]
related: [AILOG-2026-07-15-002, AILOG-2026-07-14-002]
originating_charter: CHARTER-11-polish-cierre-m3
---

# AILOG: CHARTER-11 — Polish, cierre de M3 (T061 + T062 + T063)

## Summary

Despacho de la fase `Polish` (P8) de `specs/001-weft-crdt-versioning/`: las tres tareas que CHARTER-07
declaró fuera de su alcance y difirió a este bloque. Ejecutado siguiendo
[`POLISH-CHARTER-PATTERN.md`](../../00-governance/POLISH-CHARTER-PATTERN.md) — el Charter de cierre de
Etapa como **gate de detección de deuda**, no como limpieza cosmética.

El patrón se validó: el pase de T063 fue la primera ejecución end-to-end de `quickstart.md` contra HEAD
y destapó **9 gaps**, todos del anti-patrón *«declaración de superficie sin cableado»* (sub-clase 1:
el runbook declara comandos y garantías que nadie había ejecutado ni releído). **Ninguno era un fallo
del código de producción** — el código estaba bien; el runbook mentía. Los dos más graves: el paso de
US2 pasaba en verde **ejecutando cero tests** desde que se escribió, y la tabla de gates promete que
cada PR valida el empaquetado cuando el `pack-smoke` por-PR es **un marcador que sólo hace `echo`**.

Con esto M3 queda cerrado salvo T060 (publish real a NuGet.org), operador-gated por diseño.

## Actions Performed

1. **T062 — `tests/Weft.Core.Tests/DeltaSizeBenchmark.cs`** (nuevo). El escenario de referencia de
   SC-004 **no existía como definición**: `523 B→29 B` sale de una celda etiquetada «Rough perf» en
   `docs/spikes/spike03/hallazgos-spike-03.md:38`, de un spike cuyo código es desechable por diseño y
   no vive en el repo — sin protocolo, sin tamaño de doc, sin nº de ediciones. El benchmark **define**
   el escenario por criterio semántico (reconexión: par al día + 1 edición pequeña, tomado de la prosa
   de SC-004 y de `contracts/server-api.md:116`), lo fija en el doc-comment **antes** de medir, y
   aserta el **ratio** de la spec (≥ 90 %), citando 523→29 sólo como contexto histórico. Client-ids
   fijos (capacidad de `YrsEngine`, CHARTER-09) para que el varint de un id aleatorio no meta ruido en
   la medida. El test aserta **convergencia además del tamaño**: sin eso, un delta vacío daría una
   «reducción» del 100 %. Reporta su medición vía `ITestOutputHelper`.
   **Medido: 479 B → 26 B = 94,6 %** (la referencia histórica era 94,5 % — corroboración independiente
   de que el dato del spike, aunque irreproducible, era del orden correcto).
2. **T063 — cableado del filtro de US2**: `[Trait("Category","Concurrency")]` en `DocumentBrokerTests`.
   El filtro pasó de **0 tests a 9**, verdes también en aislamiento (R5 no se materializó).
3. **T063 — pase de validación end-to-end** de US1–US5 + los 6 gates, con evidencia de **tres estados**
   (ejecutado aquí / cubierto por CI / no ejecutado con motivo) en `checklists/requirements.md`. Un
   ítem sin evidencia se queda sin marcar: `[x]` significa «lo corrí y lo vi».
4. **T063 — corrección atómica de la deriva del runbook** (8 gaps, tabla en `checklists/requirements.md`).
5. **T061 — `docs/architecture.md`** (nuevo): mapa de módulos, frontera FFI con el **contrato público de
   ownership de memoria** (las 3 clases de memoria y sus reglas, las postcondiciones que ahorran
   depuración, y el único punto del lado .NET donde puede fugarse: `YrsDoc.TakeOwnedBuffer`), flujo de
   sync, modelo de versionado, concurrencia, gates, **§Límites conocidos** (R6 y su mitigación para la
   ruta directa) e índice R1–R17 → `research.md`. Enlazado desde `README.md` §Arquitectura y
   `docs/api/README.md`. Escrito anclando cada afirmación estructural a HEAD y verificado después con
   un pase adversarial contra el código.
6. `tasks.md`: T061/T062/T063 marcadas con su desenlace.

## Gaps detectados por el pase (los 9)

Todos de deriva del runbook, todos corregidos atómicamente (estaban en `## Scope` punto 7):

1. `--filter Category=Concurrency` sin ningún `[Trait]` en el repo → **US2 verde con 0 tests**.
2. Build local sin `--features test-hooks` → `PanicSafetyTests` rojo (`EntryPointNotFoundException:
   weft_test_panic`) siguiendo el runbook literalmente. El CI sí usa la feature (`ci.yml:44`).
3. `cp` desde `native/weft-yrs-ffi/target/release/` → ruta **inexistente**: `native/` es un workspace
   cargo y comparte `native/target/`.
4. Ese `cp` es además **innecesario**: los `.csproj` de test copian desde `native/target/release/` y el
   pack lee de `native/target/<triple>/release/` (`build/Weft.Native.targets`). El destino ni existe.
5. US3 decía «relay en `:5000`»; el sample escucha en **`:5199`** (`WEFT_SAMPLE_URLS`).
6. US3 sólo documentaba `npm run dev` (navegador). `npm run check` —smoke headless de convergencia real
   vía `y-websocket`— existía y no estaba en el runbook: no había forma documentada de validar US3 sin
   display. **Ejecutado en este pase**: convergencia real de 2 clientes Yjs contra el relay.
7. Gate de determinismo descrito como «no-bloqueante al inicio, promovible». La paridad yrs↔Yjs **sí es
   bloqueante** desde CHARTER-09/FU-012 (vive en `Weft.Determinism.Tests`, job `test`); el job Node de
   `release.yml` es informativo y caza drift del upstream. La redacción confundía ambos.
8. La tabla de gates promete «un rojo bloquea merge» y lista `fuzz` sin matiz. `fuzz` **bloquea a
   medias**: un crash encontrado sólo emite `::warning` (`|| echo` por paso), pero un fallo de
   compilación de los targets **sí** pone el job rojo, deliberadamente (`ci.yml:98`). No hay
   `continue-on-error` en el job.
9. **El más grave.** La tabla lista `pack-smoke` como gate bloqueante por PR. El job `pack-smoke` de
   `ci.yml:229-235` es un **marcador que sólo hace `echo`**; la matriz real (SC-007) y la verificación
   de que `weft_test_panic` no está exportado (SC-009, job `native`, `nm` sobre los cdylibs
   pre-pack) viven en `release.yml`, que es `workflow_dispatch` **únicamente** — la matriz
   cross-compile es cara y se valida en el dry-run del release. El runbook prometía que cada PR valida
   el empaquetado; **ningún PR lo valida**. Detectado por el pase adversarial del doc, no por el pase
   del runbook: sólo se ve leyendo el YAML.

## Risk

Riesgos del Charter (R1–R6) y su desenlace:

- **R1 (tunear el escenario del benchmark hasta que pase)** — mitigado y no materializado. El escenario
  se fijó por criterio semántico y se escribió antes de medir; salió 94,6 % sin ajustar nada.
- **R2 (scope creep por los gaps del pase)** — materializado como se predijo (8 gaps) y contenido: los 8
  son deriva de runbook, explícitamente en scope ex-ante. Lo que **no** era runbook se triagea abajo, no
  se absorbe.
- **R3 (el doc duplica y se pudre)** — mitigado: el doc enlaza en vez de copiar; su valor propio es la
  frontera FFI y el contrato de ownership, que ningún otro doc público explica.
- **R4 (marcar verde lo no ejecutado)** — mitigado con los 3 estados de evidencia. US4 queda
  **sin marcar** pese al pack local verde, porque su criterio es instalación en máquina limpia por RID.
- **R5 (los tests de concurrencia fallan al aislarse)** — no materializado: 9/9 verdes en aislamiento.
- **R6 (el doc afirma cosas que el código ya no hace)** — **se materializó, y la mitigación lo cazó.**
  El pase adversarial contra HEAD encontró **5 afirmaciones falsas** en el primer borrador de
  `docs/architecture.md`: (a) atribuía la verificación de `weft_test_panic` a `pack-smoke` sobre los
  binarios empaquetados, cuando la hace el job `native` con `nm` sobre los cdylibs pre-pack, en un
  workflow que ni siquiera corre por PR; (b) acoplaba la retirada de awareness al último cliente,
  cuando es por conexión —de ser cierto habría sido un bug de presencia—; (c) decía «un solo sitio»
  para `TakeOwnedBuffer` habiendo dos (yrs y Loro), lo que mandaría a un auditor de fugas a la mitad
  de la superficie; (d) enumeraba 11 de las 12 funciones omitiendo justo `weft_doc_new_with_client_id`;
  (e) llamaba a `fuzz` `continue-on-error`.

  El caso (e) merece constar como lección: **el error se cometió exactamente por el mecanismo que R6
  anticipaba.** No salió de leer el YAML sino de heredar el comentario de `ci.yml:76`, que afirma
  `continue-on-error` y es falso. Un doc derivado de un comentario obsoleto propaga la obsolescencia y
  le añade autoridad. La regla de «anclar a `archivo:línea` leído en HEAD» se aplicó al código C#/Rust
  pero no al YAML de CI, y ahí es donde falló. Todas corregidas antes del commit; el comentario que lo
  originó va a follow-up.

**R7 (nuevo, no en el Charter) — el fuzz reproduce R6 en local y puede leerse como regresión.**
`cargo +nightly fuzz run doc_load` OOMea con un input de **4 bytes** (`f6f4d621`). **No es una
regresión**: es R6, el job es `continue-on-error` por esta razón exacta, el shim es correcto (contiene
panics, sin UB) y el fix vive upstream (y-crdt#639, aprobado) → adopción vía FU-015. El riesgo real es
de **legibilidad**: un contribuidor que corra el gate del quickstart lo lee como rojo nuevo. Mitigado
documentándolo en la tabla de gates (`quickstart.md`) y en `docs/architecture.md` §Límites conocidos.

## Follow-ups

- **Comentario obsoleto en `.github/workflows/ci.yml:76-81`** — **dos afirmaciones falsas**, y ya
  demostró que hace daño: (1) dice que el job es `continue-on-error`, y **no lo es** (no existe esa
  clave; lo informativo son los `|| echo` de los pasos `fuzz run`, mientras que un fallo de compilación
  sí pone el job rojo); (2) dice que la mitigación real de R6 «llega en M2, donde entra input de red no
  confiable», cuando llegó en CHARTER-08 (M3) como PR upstream + caveat en `GOVERNANCE.md`, y lo que
  resta es la adopción vía bump (FU-015). **Este comentario es el origen probado del único error de
  hecho que se propagó a la documentación en este Charter** (ver §Risk R6, caso (e)): la
  documentación derivada de comentarios de CI hereda su obsolescencia. Severidad: baja en código, media
  en efecto — es un comentario que activamente desinforma. Un mismo commit debería corregir el
  comentario y `ci.yml:180` (que también afirma, falsamente, que la paridad con Yjs no es bloqueante).
  No se corrige aquí para no ampliar el scope del Polish más allá del runbook.
- **Footgun de pack local contaminado con `test-hooks`**: el pack lee de
  `native/target/<triple>/release/`. Quien compile con `cargo build --release --target <triple>
  --features test-hooks` y luego haga `dotnet pack` empaquetaría `weft_test_panic`. Hoy no ocurre (el
  pipeline de release compila sin la feature y `pack-smoke` verifica la ausencia), y en este pase se
  verificó que los `.so` que alimentan el pack están limpios. Pero el gate sólo existe en CI: un pack
  local no lo caza. Severidad baja. Candidato: extender la verificación del símbolo a un target de pack
  local, o documentarlo en `CONTRIBUTING.md`.
- **Guards de CI para las sub-clases del anti-patrón** (paso 5 del walkthrough del patrón de Polish). El
  gap #1 (comando del runbook que no casa con ningún test) es mecánicamente detectable: un check que
  verifique que cada `--filter Category=X` documentado casa con ≥1 test lo habría cazado el día que se
  escribió. Candidato natural tras este Charter, y el más portable de los tres que sugiere el patrón.

## Verification

```bash
cargo build --release --features test-hooks --manifest-path native/Cargo.toml
dotnet test Weft.sln -c Release                              # 132/132 verdes
dotnet test tests/Weft.Core.Tests -c Release --filter Category=Concurrency   # 9/9 (antes: 0)
dotnet run --project tests/Weft.LoadTest -c Release          # PASS, 0 errores
RUSTFLAGS="-Zsanitizer=address" cargo +nightly test --features test-hooks \
  --target x86_64-unknown-linux-gnu                          # 14/14, 0 fugas
cd tests/determinism-yjs && npm test                         # golden ascii + unicode OK
cd samples/tiptap-client && npm run check                    # convergencia real vs relay
straymark validate --include-charters
```

Evidencia completa del pase en
[`checklists/requirements.md`](../../../specs/001-weft-crdt-versioning/checklists/requirements.md)
§Quickstart validation pass.
