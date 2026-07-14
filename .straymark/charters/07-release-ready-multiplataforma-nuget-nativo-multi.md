---
charter_id: CHARTER-07-release-ready-multiplataforma-nuget-nativo-multi
status: in-progress
effort_estimate: L
trigger: "US3 100% completa (CHARTER-06 en main, d494105): los 6 paquetes (.NET) y 2 shims (Rust) están construidos y verdes; el gate dual-engine (T034) y todos los gates de M0/M1/M2 activos. tasks.md fija US4 (T055–T060) como el empaquetado NuGet nativo multi-RID + release OSS. Este Charter deja el release **listo para disparar** (packaging + cross-compile + pack-smoke + docs + release.yml en dry-run) SIN ejecutar el paso irreversible (publish a NuGet.org / repo público), que queda operador-gated."
originating_spec: specs/001-weft-crdt-versioning/spec.md
work_verb: implement
design_provenance: new
---

# Charter: Release-ready multiplataforma — NuGet nativo multi-RID + gates (publish gated)

> **Status (mirrored from frontmatter — source of truth is above):** in-progress. Effort: L.
>
> **Origin:** Derivado de `specs/001-weft-crdt-versioning/spec.md` (US4, hito M3; research R11/R12/R13/R16).
> Deja Weft **release-ready**: entrega T055–T059 y **autora** T060 (pipeline de release) en modo **dry-run**.
> El paso **irreversible** — `dotnet nuget push` a NuGet.org, flip del repo a público, tag/GitHub Release — es
> **operador-gated** y queda **fuera** de la ejecución de este Charter. **Este Charter NO cierra M3**; lo deja a
> un botón de distancia.

## Context

US4 entrega el hito M3: Weft instalable en una máquina limpia de cada RID soportado (`linux-x64`, `linux-arm64`,
`win-x64`, `osx-arm64`) con un `dotnet add package` y sin configurar binarios nativos a mano (SC-007, P-VI). El
binding ya resuelve el cdylib desde `runtimes/<rid>/native/<lib>` (`NativeLibraryResolver`, research R11); lo que
falta es **producir** ese layout en los `.nupkg`, **cross-compilar** los 2 shims para los 4 RIDs (research R12,
`cargo-zigbuild` + runners nativos), **verificarlo** en máquinas limpias (pack-smoke, SC-007) incluyendo la
**ausencia del símbolo `weft_test_panic`** en release (SC-009, test-hooks fuera del binario empaquetado), añadir
el **gate de determinismo cross-implementación** vs Yjs JS (research R13, no-bloqueante), y los **docs públicos**
de consumo y gobernanza (research R16 para el protocolo de bump del motor).

Decisión de alcance del operador (2 preguntas al declarar): (a) **release-ready con publish gated** — se autora
`release.yml` con el pipeline completo pero el `nuget push` queda tras un input `dry_run` (default `true`), y el
flip del repo a público + el tag/Release son acto deliberado del operador **después** de este Charter; (b) el
**Polish** (T061–T063: doc de arquitectura, benchmark delta-size, validación quickstart end-to-end) va a un
**CHARTER-08 aparte** (patrón polish = detección de deuda). Restricción operativa viva: **presupuesto de minutos
de CI ~agotado** — `release.yml` corre por `workflow_dispatch` (NO en cada PR), y su validación es **un** dry-run
deliberado, no una corrida por push.

Trabajo de **implementación** contra decisiones ✅ CERRADO del brief y research congelado. Tensa **P-VI**
(portabilidad probada por RID — "soportado" = ejercitado en pack-smoke), **P-I/P-II** (el binario empaquetado es
release puro, sin test-hooks; el pack-smoke lo verifica), **P-III** (determinismo cross-RID + cross-impl) y
**P-IV** (el gate dual-engine T034 ya activo antes del release).

## Scope

**In scope (T055–T059 + T060 en dry-run):**

1. **Packaging nativo (T055)**: `src/Weft.Core/Weft.Core.csproj` empaqueta el cdylib `weft_yrs_ffi` y
   `src/Weft.Loro/Weft.Loro.csproj` el `weft_loro_ffi`, ambos bajo `runtimes/<rid>/native/` (patrón SkiaSharp,
   R11) para los 4 RIDs, consumiendo los artefactos cross-compilados. Los 6 paquetes (`Weft.Core`,
   `Weft.Versioning`, `Weft.Server`, `Weft.Loro`, `Weft.Server.Persistence.EFCore`, `.Redis`) producen `.nupkg`
   con símbolos + SourceLink (metadata ya en `Directory.Build.props`). `buildTransitive/` targets si el layout lo
   requiere. La resolución en consumidor ya existe (`NativeLibraryResolver` lee ese árbol) — no se toca.
2. **Cross-compile (T056)**: `.github/workflows/release.yml` — matriz que compila los 2 shims en **release sin
   `test-hooks`**: `cargo-zigbuild` para `linux-x64`/`linux-arm64` (glibc mínima fijada), runners nativos de GH
   para `win-x64`/`osx-arm64`; `cross` como fallback. Los cdylibs → artefactos → job de pack que arma los
   `runtimes/` y produce los `.nupkg`.
3. **pack-smoke (T057)**: job de matriz que instala el paquete empaquetado **desde los artefactos** y corre un
   consumidor **hello-Weft** en `linux-x64`, `win-x64`, `osx-arm64` y `linux-arm64` (QEMU/runner arm) → SC-007.
   Además verifica que **`weft_test_panic` NO está exportado** en los cdylibs empaquetados (`nm`/`dumpbin`) →
   invariante de SC-009 (test-hooks fuera de release). Vive en `release.yml` (dry-run), no en cada PR.
4. **Determinismo cross-impl (T058)**: `tests/determinism-yjs/` — un job Node aplica el **corpus compartido** con
   Yjs JS y compara blobs/hashes contra los de `Weft.Determinism.Tests` (R13 (b)). **No-bloqueante**, promovible
   a gate al estabilizarse.
5. **Docs públicos (T059)**: `README.md` gana un **quickstart de consumo** (install → edit → publish → server);
   `docs/api/` overview por paquete; `CONTRIBUTING.md` con el **protocolo de bump del motor** (R16);
   `GOVERNANCE.md`.
6. **Pipeline de release AUTORADO en dry-run (T060, parcial)**: `release.yml` incluye versionado SemVer,
   symbols+SourceLink, y el paso de `dotnet nuget push` + tag + GitHub Release **gateado tras `inputs.dry_run`
   (default `true`)**. Con `dry_run=true` construye/empaqueta/pack-smoke pero **NO publica**. El pipeline queda
   **validado** con **un** `workflow_dispatch` dry-run verde.

**Out of scope:**

- **El paso irreversible (T060 real)**: `dotnet nuget push` a NuGet.org, flip del repo a **público**, `git tag` +
  GitHub Release con notas. Es **acto deliberado del operador** tras este Charter (dispatch de `release.yml` con
  `dry_run=false` + API key). No se ejecuta aquí; `release.yml` queda listo para hacerlo. **Cierra M3** cuando
  el operador lo dispare.
- **Polish (T061–T063)** — doc de arquitectura, benchmark delta-size (SC-004), validación quickstart end-to-end —
  **CHARTER-08 aparte**.
- `docs/architecture.md` — es **T061 (Polish/CHARTER-08)**, no este Charter.
- Paquetes runtime separados por RID (`Weft.Core.runtime.<rid>`) — optimización no-breaking diferida (R11).
- `INativeVersioning` de Loro (**FU-006**), durabilidad del relay (**FU-010**), job CI de Redis (**FU-011**, se
  pliega a este PR pero no se ejecuta) — sin relación con el release.

## Files to modify

<!-- Reconnaissance #210: Weft.Core.csproj / Weft.Loro.csproj (sin packaging nativo aún — verificado),
     NativeLibraryResolver.cs (ya lee runtimes/<rid>/native/ — NO se toca, solo se alimenta), ci.yml (stub
     pack-smoke placeholder en línea ~191; NO existe release.yml), README.md (existe, 65 líneas, falta
     quickstart de consumo), CONTRIBUTING.md/GOVERNANCE.md/docs/architecture.md (NO existen), Directory.Build.props
     (metadata de paquete + SourceLink ya heredada), crates weft-yrs-ffi/weft-loro-ffi (cdylib+rlib) verificados. -->

| File | Change |
|---|---|
| `src/Weft.Core/Weft.Core.csproj` | Change — empaquetar `weft_yrs_ffi` en `runtimes/<rid>/native/` (4 RIDs) + `buildTransitive/` si aplica (T055) |
| `src/Weft.Loro/Weft.Loro.csproj` | Change — empaquetar `weft_loro_ffi` en `runtimes/<rid>/native/` (T055) |
| `.github/workflows/release.yml` | New — cross-compile (zigbuild + runners nativos) + pack + pack-smoke + publish **gateado tras `dry_run`** (T056/T057/T060); `workflow_dispatch` (T056) |
| `.github/workflows/ci.yml` | Change — el stub `pack-smoke` apunta a `release.yml` (la matriz real corre en dispatch, no en cada PR — coste de minutos) |
| `tests/determinism-yjs/` | New — job Node + Yjs sobre el corpus compartido, compara hashes; no-bloqueante (T058) |
| `tests/pack-smoke/` | New — consumidor mínimo "hello-Weft" que instala el `.nupkg` y ejercita edit→publish (T057) |
| `README.md` | Change — quickstart de consumo (install→edit→publish→server) (T059) |
| `docs/api/` | New — overview por paquete (T059) |
| `CONTRIBUTING.md` | New — incl. protocolo de bump del motor (R16) (T059) |
| `GOVERNANCE.md` | New — gobernanza OSS (T059) |
| `specs/001-weft-crdt-versioning/tasks.md` | Change — marcar T055–T059 `[X] — CHARTER-07`; T060 `[~] — CHARTER-07 (autorado dry-run; publish operador-gated)` |
| `.straymark/07-ai-audit/agent-logs/AILOG-*.md` | New, `risk_level: medium` (release infra; frontera de packaging P-I/P-II; paso irreversible gateado) |
| `.straymark/07-ai-audit/decisions/AIDEC-*.md` | New si emerge decisión sustantiva (estrategia de gating del publish; forma del corpus determinism-yjs) |

## Verification

### Local checks

> **Lección de CHARTER-01..06**: correr lo replicable localmente en verde ANTES de pushear. Cross-compile y la
> matriz multi-RID **no** son locales (necesitan `zig`/runners CI); su gate es **un** dry-run de `release.yml`.

```bash
# Build + suite completa intacta (M0/M1/M2)
dotnet build Weft.sln -c Release
WEFT_TEST_REDIS=localhost:6379 dotnet test Weft.sln -c Release

# Pack local de los 6 paquetes (símbolos + SourceLink); inspeccionar el layout runtimes/ del RID local
dotnet pack Weft.sln -c Release -o artifacts/nupkg
unzip -l artifacts/nupkg/Weft.Core.*.nupkg | grep 'runtimes/linux-x64/native/'   # cdylib presente

# pack-smoke LOCAL (solo linux-x64): consumidor hello-Weft desde el .nupkg empaquetado
dotnet test tests/pack-smoke/ -c Release   # o el script de smoke; edit→publish verde en linux-x64

# Ausencia de test-hooks en el binario release local (SC-009)
nm -D native/target/release/libweft_yrs_ffi.so | grep -q weft_test_panic && echo "FALLO: símbolo presente" || echo "OK: sin weft_test_panic"

# Determinism-yjs local (si hay Node): aplicar corpus con Yjs y comparar hashes (no-bloqueante)
cd tests/determinism-yjs && npm install && npm test
```

**Validación de la matriz multi-RID (T056/T057)** — NO ejecutable en shell limpio: es **un** dry-run deliberado
de `release.yml` (coste de minutos):

```bash
# Un solo dispatch en dry-run (NO publica): cross-compile 4 RIDs + pack + pack-smoke + check de símbolo
gh workflow run release.yml -f dry_run=true
gh run watch   # verde = release-ready
```

### Production smoke (after deploy)

No aplica a la ejecución de este Charter — **no hay deploy** (el publish es operador-gated, fuera de scope). Los
auditores externos deben saltar esta sección. El "deploy" real (publish a NuGet.org + repo público) es el paso
gated posterior; su smoke (instalar desde NuGet.org en máquina limpia) vive con ese acto del operador.

## Risks

- **R1 — Publish accidental a NuGet.org (IRREVERSIBLE)**: severidad **alta**. Un paquete publicado en NuGet.org no
  se despublica; un repo flipado a público queda indexado. Mitigación: el paso `nuget push`/tag/Release en
  `release.yml` va **gateado tras `inputs.dry_run` (default `true`)** y **no se dispara** en este Charter; no se
  usa API key; el default de todo dispatch es dry-run. Si falla la mitigación (se publica algo): es exactamente
  el daño irreversible que el alcance "publish gated" del operador busca evitar → bloquea el diseño.
- **R2 — El binario empaquetado no resuelve en máquina limpia (SC-007)**: severidad **media-alta**. Un layout
  `runtimes/` mal armado o un RID faltante deja al consumidor sin cdylib. Mitigación: el `NativeLibraryResolver`
  ya lee `runtimes/<rid>/native/`; el pack-smoke instala el `.nupkg` real y corre hello-Weft en cada RID
  (incl. arm vía QEMU). Si falla: SC-007 roto → no release-ready.
- **R3 — `weft_test_panic` filtrado en el binario release (SC-009)**: severidad **media-alta**. Si la matriz
  compila con `--features test-hooks`, el símbolo de inyección de panic viaja en el paquete. Mitigación: la
  matriz de `release.yml` compila **sin** `test-hooks`; el pack-smoke **asierta** la ausencia del símbolo
  (`nm`/`dumpbin`). Si falla: superficie de test en producción → bloquea el pack.
- **R4 — Cross-compile falla para un RID (zigbuild glibc/linking; firma osx-arm64)**: severidad **media**.
  Mitigación: `cargo-zigbuild` (linux) + runners nativos (win/mac) + `cross` como fallback (R12); el dry-run
  destapa el fallo por RID antes de cualquier publish. Si falla: ese RID no entra a v1 (documentar), no bloquea
  los demás.
- **R5 — Divergencia determinism-yjs (yrs vs Yjs JS)**: severidad **baja**. Mitigación: el job es
  **no-bloqueante** por diseño (R13 (b), promovible); una divergencia se documenta y NO bloquea M3. Si falla:
  señal de que el determinismo es "por esta versión de yrs", no por formato — insumo para R16.
- **R6 — Coste de minutos de CI de la matriz de release**: severidad **media** (restricción operativa viva).
  Mitigación: `release.yml` es `workflow_dispatch`-only (no corre en push/PR); la validación es **un** dry-run
  deliberado; `ci.yml` por-PR no gana coste (el stub pack-smoke solo apunta a release.yml). Si falla: se quema
  presupuesto — por eso NO se cablea a `pull_request`.

## Tasks

1. Sync main, branch `charter/07-release-ready` (**ya basada en `chore/fu-011`** → el PR pliega FU-011). Flip
   `declared` → `in-progress` al empezar a ejecutar.
2. Re-evaluar **Constitution Check** (per-Charter): **P-VI** (pack-smoke por RID = "soportado"), **P-I/P-II**
   (binario release sin test-hooks, verificado), **P-III** (determinism cross-RID/cross-impl), **P-IV** (gate
   dual-engine ya activo). Sin violaciones esperadas.
3. `/speckit-implement` acotado a **T055–T059** + **T060 en dry-run**; marcar por tarea (`[X]` T055–T059; `[~]`
   T060 autorado).
4. **AILOG** (`risk_level: medium`, `review_required: true`). **AIDEC** si emerge decisión sustantiva (gating del
   publish; forma del corpus determinism-yjs).
5. **Batch Ledger** en el AILOG (multi-batch probable, L): `straymark charter batch-complete CHARTER-07 <N>`.
6. **Verificación local COMPLETA** (bloque Local checks) + **un** dry-run de `release.yml` verde ANTES de cerrar.
7. `straymark charter drift CHARTER-07` antes de commit; drifts → completarlos o documentarlos como
   `R<N+1> (new, not in Charter)` en el AILOG. **Nota**: el parser no reconoce `.csproj`/`.sln`/`.yml` (issue
   straymark #354) → esos aparecerán como FP de scope expansion; documentarlo, no es drift real.
8. Commit + push + abrir PR contra `main`; validar con **un** `workflow_dispatch` dry-run (no en cada PR).

## Charter Closure

Charter que **NO cierra M3** (lo deja release-ready; el publish + repo público es acto operador-gated posterior,
y el Polish es CHARTER-08). **No requiere auditoría externa multi-modelo obligatoria** (esa es para cierres de
hito). **Recomendación fuerte**: correr una auditoría externa **antes del paso gated** (el operador la lanza como
parte de apretar el botón de publish), dado que el binario empaquetado cruza P-I/P-II y el publish es
irreversible. Al cerrar:

1. Actualización atómica del Charter (format v4) si el drift check reveló divergencias, en el **mismo PR**.
2. `straymark charter drift CHARTER-07 --range origin/main..HEAD` → limpio o documentado (incl. los FP de
   `.csproj`/`.sln`/`.yml` del parser, issue #354).
3. `straymark charter close CHARTER-07` (telemetría, status `closed`, `closed_at`). No borrar este archivo.
4. Confirmar el estado: **release-ready verde** (dry-run de `release.yml` pasó). **M3 sigue abierto** hasta que
   el operador dispare el publish (`dry_run=false` + API key + repo público + tag) y se cierre **CHARTER-08**
   (Polish). Dejar el runbook del paso gated en `CONTRIBUTING.md`/`GOVERNANCE.md` o el AILOG.

## Closing notes

Ejecución (T055–T059 + T060 dry-run) documentada en `AILOG-2026-07-13-003`. **Verificación local verde** (build
0 warnings, 124 tests, pack local 6 `.nupkg` con los 2 RIDs de Linux, pack-smoke linux-x64, harness
determinism-yjs, YAML válido). **Pendiente para cierre**: **1 dry-run de `release.yml`** que valide win-x64/
osx-arm64/linux-arm64 (gasto de CI, decisión del operador — ver AILOG §Risk R2/R4). El charter permanece
`in-progress` hasta ese dry-run.

Expansiones de scope respecto a §Files (documentadas, intencionales):

- **`build/Weft.Native.targets`** (New, no declarado) — targets MSBuild compartido que empaqueta el cdylib por
  RID; extraído para no duplicar la lógica entre `Weft.Core.csproj` y `Weft.Loro.csproj` (que sí estaban
  declarados). Ref: `AILOG-2026-07-13-003 §Actions #1`.
- **`Directory.Build.props`** (Change, no declarado) — `IsPackable=false` para tests/samples/load-test, para que
  `dotnet pack Weft.sln` produzca solo los 6 paquetes de librería. Ref: idem.
- **`.straymark/follow-ups-backlog.md`** (Change, no declarado) — registro de **FU-012** (paridad determinism-yjs
  gated en client-ids deterministas), hand-add + `recount`.
- **Drift parser (issue #354)**: `.csproj`/`.sln`/`.yml` declarados aparecen como falso positivo de "scope
  expansion" (el parser no reconoce esas extensiones); no es drift real (`Weft.Core.csproj`, `Weft.Loro.csproj`,
  `release.yml`, `ci.yml` **sí** están declarados en §Files).
- **Granularidad directorio-vs-archivo**: §Files declara `tests/determinism-yjs/`, `tests/pack-smoke/` y
  `docs/api/` como **directorios**; el drift lista sus archivos individuales como "no declarados". No es drift —
  el contenido de esos dirs es exactamente lo declarado (ver `AILOG-2026-07-13-003 §Modified Files`).
- **`AIDEC-*.md` declarado pero no creado**: §Files lo marcaba condicional ("New si emerge decisión sustantiva").
  Las decisiones (strip vía `--config`, publish gateado, zig pinneado, determinism no-bloqueante) se
  documentaron en el **AILOG §Decisions**, sin ameritar un AIDEC aparte. No es trabajo faltante.
