---
id: AILOG-2026-07-13-003
title: "CHARTER-07: release-ready multiplataforma — NuGet nativo multi-RID (T055–T059 + T060 dry-run)"
status: accepted
created: 2026-07-13
agent: claude-opus-4-8
confidence: high
review_required: true
risk_level: medium
eu_ai_act_risk: not_applicable
nist_genai_risks: []
iso_42001_clause: []
lines_changed: 0
files_modified: []
observability_scope: none
tags: [release, packaging, nuget, multi-rid, cross-compile, zigbuild, pack-smoke, determinism, docs, us4, publish-gated]
related: [AILOG-2026-07-13-002, AILOG-2026-07-10-002]
originating_charter: CHARTER-07-release-ready-multiplataforma-nuget-nativo-multi
---

# AILOG: CHARTER-07 — release-ready multiplataforma (NuGet nativo multi-RID)

## Summary

Ejecución de **US4/M3** en modo **release-ready con publish gated** (effort L): T055–T059 entregados +
**T060 autorado en dry-run**. Weft queda instalable multiplataforma a un `workflow_dispatch` de distancia, SIN
ejecutar el paso irreversible (publish a NuGet.org / repo público), que es acto operador-gated aparte. El
packaging nativo (`runtimes/<rid>/native/`, patrón SkiaSharp) empaqueta los 2 shims por RID; `release.yml`
cross-compila los 4 RIDs (cargo-zigbuild + runners nativos), empaqueta, corre pack-smoke por RID y verifica la
ausencia de `weft_test_panic`; el `publish` va **gateado tras `inputs.dry_run` (default true)**. Verificación
local: build 0 warnings, **124 tests verdes**, pack local de los 6 `.nupkg` con los 2 RIDs de Linux, pack-smoke
linux-x64 verde (nativo resuelto desde el paquete), harness determinism-yjs verde. **Este Charter NO cierra M3.**

## Context

Trabajo bajo `.straymark/charters/07-*.md`, sobre US3 completa (CHARTER-06, main d494105). Decisiones de
alcance del operador al declarar: (a) release-ready publish-gated, (b) Polish (T061–T063) → CHARTER-08. El
binding ya resolvía `runtimes/<rid>/native/` (`NativeLibraryResolver`, R11); faltaba producir ese layout,
cross-compilar (R12), verificar (SC-007/SC-009), el gate determinism cross-impl (R13) y los docs (R16).

## Actions Performed

1. **T055 packaging**: `build/Weft.Native.targets` (targets MSBuild compartido, empaqueta `$(WeftNativeLib)`
   por RID bajo `runtimes/<rid>/native/`, cada RID condicionado a `Exists` → pack local incluye los presentes);
   importado por `Weft.Core.csproj` (`weft_yrs_ffi`) y `Weft.Loro.csproj` (`weft_loro_ffi`).
   `Directory.Build.props` marca `IsPackable=false` para tests/samples/load-test. Build de packaging con strip
   (`--config profile.release.strip="symbols"`) → yrs 936K/848K, loro 3.8M/3.4M (vs 41M), 12 exports C-ABI
   intactos. Pack local: 6 `.nupkg` + 6 `.snupkg`; `Weft.Core.nupkg` con linux-x64/arm64, `Weft.Server` sin
   nativo (correcto).
2. **T056/T060 `release.yml`**: `workflow_dispatch`-only (NO push/PR — coste de minutos). Jobs: `native`
   (matriz 4 RIDs; zigbuild para linux, runners nativos win/mac; strip; verifica ausencia de `weft_test_panic`),
   `pack` (stage de artefactos + `dotnet pack -p:Version`), `pack-smoke` (matriz linux-x64/win-x64/osx-arm64) +
   `pack-smoke-arm` (linux-arm64 vía QEMU), `determinism-yjs` (no-bloqueante), `publish` **gateado tras
   `inputs.dry_run == false`** (`nuget push` + tag + GitHub Release; `environment: release`).
3. **T057 pack-smoke**: `tests/pack-smoke/` (consumidor hello-Weft que referencia Weft como **paquetes** desde
   el feed local → ejercita el layout nativo). Smoke local linux-x64 verde: motor resuelto, VersionId SHA-256.
4. **T058 determinism-yjs**: `tests/determinism-yjs/` (Node+Yjs aplica el corpus compartido con client-ids fijos,
   emite el SHA-256 del export). **No-bloqueante**; la paridad con yrs está gated en client-ids deterministas →
   **FU-012**.
5. **T059 docs**: `README.md` (estado release-ready + Instalación + Quickstart de consumo), `docs/api/README.md`
   (overview por paquete), `CONTRIBUTING.md` (protocolo de bump del motor, R16), `GOVERNANCE.md`.
6. **ci.yml**: el stub `pack-smoke` apunta a `release.yml` (matriz real en dispatch, no por-PR).

## Modified Files

**Nuevos**: `build/Weft.Native.targets`, `.github/workflows/release.yml`, `tests/pack-smoke/{Program.cs,
Weft.PackSmoke.csproj,nuget.config}`, `tests/determinism-yjs/{apply.mjs,corpus.json,package.json,README.md}`,
`CONTRIBUTING.md`, `GOVERNANCE.md`, `docs/api/README.md`.

**Modificados**: `src/Weft.Core/Weft.Core.csproj`, `src/Weft.Loro/Weft.Loro.csproj`, `Directory.Build.props`,
`.github/workflows/ci.yml`, `README.md`, `specs/001-weft-crdt-versioning/tasks.md`,
`.straymark/follow-ups-backlog.md` (FU-012).

## Decisions Made

1. **Strip vía `--config`, no en Cargo.toml**: el perfil release mantiene `debug = 1` (símbolos para
   sanitizers/inspección, decisión previa); el build de packaging aplica `profile.release.strip="symbols"` por
   línea de comandos (arch-correcto, sin tocar el perfil compartido). Mismo comando local y en `release.yml`.
2. **Publish gateado tras `dry_run` (default true)**: cierra R1 (publish irreversible). El paso irreversible no
   se ejecuta en este Charter; `release.yml` queda listo para que el operador lo dispare con `dry_run=false`.
3. **zig 0.15.2 fijado local y en CI** (`mlugg/setup-zig`): paridad local↔CI del cross-compile de los RIDs Linux.
4. **determinism-yjs no-bloqueante** (R13 (b)): la paridad byte-idéntica con yrs requiere client-ids fijos que
   el FFI/binding no exponen hoy → FU-012; el harness corre e informa mientras tanto.

## Risk

- **R2/R4 del Charter — matriz multi-RID NO validada en CI aún**: severidad **media**. La verificación local solo
  cubre **2 de 4 RIDs** (linux-x64 nativo + linux-arm64 vía zig): pack + pack-smoke linux-x64 verdes,
  cross-compile de ambos shims a arm64 verde. **win-x64, osx-arm64 y el pack-smoke arm (QEMU) NO están
  validados** — su gate es **un `workflow_dispatch` dry-run de `release.yml`**, que es un gasto deliberado de
  CI (matriz win 2×/mac 10× + QEMU) y queda **pendiente de la decisión del operador** (presupuesto de minutos).
  Hasta ese dry-run verde, "release-ready" está probado solo en Linux. Documentado, no oculto.

## Impact

- **US4 entregado** salvo el trigger de publish (operador-gated). **M3 sigue abierto** hasta (a) 1 dry-run verde
  de `release.yml`, (b) el publish real del operador, (c) CHARTER-08 (Polish, T061–T063).
- API pública sin cambios; solo packaging + infra + docs. Los 6 `.nupkg` incluyen símbolos + SourceLink.

## Verification

```bash
dotnet build Weft.sln -c Release                                  # 0 warnings
WEFT_TEST_REDIS=localhost:6379 dotnet test Weft.sln -c Release    # 124 verdes, 0 fallos
# packaging local (2 RIDs Linux, con zig): cross-compile stripped + pack + pack-smoke
(cd native && cargo zigbuild --release --config 'profile.release.strip="symbols"' \
   --target x86_64-unknown-linux-gnu --target aarch64-unknown-linux-gnu -p weft-yrs-ffi -p weft-loro-ffi)
dotnet pack Weft.sln -c Release -o artifacts/nupkg               # 6 nupkg + 6 snupkg
unzip -l artifacts/nupkg/Weft.Core.1.0.0.nupkg | grep runtimes   # linux-x64 + linux-arm64
(cd tests/pack-smoke && dotnet run -c Release)                    # ✓ nativo resuelto, edit→publish
(cd tests/determinism-yjs && npm install && npm test)             # ✓ harness verde (informativo)
python3 -c "import yaml; yaml.safe_load(open('.github/workflows/release.yml'))"  # YAML válido
```

**PENDIENTE (gasto de CI, decisión del operador)**: `gh workflow run release.yml -f dry_run=true` → valida
win-x64/osx-arm64/linux-arm64(QEMU) + pack-smoke matrix. Sin él, la matriz no-Linux no está probada.

## Additional Notes

- **zig + cargo-zigbuild instalados local** (sesión previa): zig 0.15.2 (tarball verificado por SHA-256, en
  `~/.local`), cargo-zigbuild 0.23.0. Permiten validar los 2 RIDs de Linux localmente.
- **Drift check**: `.csproj`/`.sln`/`.yml` declarados aparecerán como FP de scope expansion (el parser no los
  reconoce — issue straymark #354, ya comentado). Sin drift real.

## Approval

Pendiente de confirmación del operador (`risk_level: medium`, `review_required: true`). Verificación local
completa citada; el dry-run de la matriz multi-RID es el gasto de CI que el operador decide.
