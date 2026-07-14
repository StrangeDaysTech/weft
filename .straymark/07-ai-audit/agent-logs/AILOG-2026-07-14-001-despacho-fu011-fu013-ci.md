---
id: AILOG-2026-07-14-001
title: "Despacho de FU-011 + FU-013: cobertura Redis en CI + bump de GitHub Actions (Node 20)"
status: accepted
created: 2026-07-14
agent: claude-opus-4-8
confidence: high
review_required: false
risk_level: low
eu_ai_act_risk: not_applicable
nist_genai_risks: []
iso_42001_clause: []
lines_changed: 0
files_modified: []
observability_scope: none
tags: [ci, github-actions, redis, valkey, service-container, chore, follow-ups, node20]
related: [AILOG-2026-07-13-002, AILOG-2026-07-13-003]
originating_charter: none
---

# AILOG: despacho de FU-011 + FU-013 (CI)

## Summary

Despacho de dos follow-ups `chore` en un solo PR (no es un Charter — trabajo de una sesión sin cambios de
comportamiento de la librería). **FU-011**: reponer la cobertura del adaptador Redis en CI (diferida en
CHARTER-06 por presupuesto de minutos) con un job `server-adapters` Linux + service container `redis:7`.
**FU-013**: bump de GitHub Actions fuera de Node 20 (deprecado, detectado por las annotations del dry-run de
CHARTER-07). Verificación local: YAML válido; el test Redis filtrado corre **8/8 verde** contra Valkey.

## Actions Performed

1. **FU-013 — bump de actions**: la familia `actions/*` a **@v5** (consistente con las que `docs-validation.yml`
   ya usaba en v5, y off-Node 20): `checkout@v4→v5` (16), `setup-dotnet@v4→v5` (7), `setup-node@v4→v5` (2),
   `upload-artifact@v4→v5` (2), `download-artifact@v4→v5` (4); `docker/setup-qemu-action@v3→v4`. Se dejan
   `mlugg/setup-zig@v2` y `Swatinem/rust-cache@v2` (ya son la última major; `@v2` recoge parches Node-24), y las
   third-party de `cla.yml` (`contributor-assistant`, `changed-files`, `create-github-app-token`) intactas (no
   flageadas; `cla.yml` es sensible). `macos-latest` se deja (la migración a macOS 26 ya pasó [2026-06-15] y el
   dry-run pasó en él).
2. **FU-011 — job Redis en CI**: `ci.yml` gana `server-adapters` (ubuntu, `services: redis:7` con health-check),
   que corre `dotnet test tests/Weft.Server.Tests/ --filter "FullyQualifiedName~RedisDocumentStoreContractTests"`
   con `WEFT_TEST_REDIS=localhost:6379` → los 8 tests del adaptador Redis (que se saltan en el job `test` sin
   servidor) ahora se ejercitan de verdad. Linux-only porque los service containers de GH solo corren en Linux;
   el adaptador es .NET managed puro (sin binario nativo → sin build de cargo en este job, lean).

## Modified Files

**Modificados**: `.github/workflows/ci.yml` (bumps + job `server-adapters`), `.github/workflows/release.yml`
(bumps), `.straymark/follow-ups-backlog.md` (FU-011/FU-013 → `closed`). `cla.yml` y `docs-validation.yml` **NO**
cambiaron (ya estaban en `actions/*@v5` o usan third-party no flageadas).

## Risk

- **R1 (bajo) — bumps de `release.yml` no re-validados por dry-run**: los `actions/*` de `release.yml` (upload/
  download-artifact, checkout, setup-dotnet, setup-node) solo se ejercitan en un `workflow_dispatch`, no en el CI
  por-PR. Para no gastar otro dry-run (matriz cara), NO se re-lanzó; el próximo dispatch (dry-run o el publish
  real del operador) los ejercita. Riesgo real bajo: son bumps de versión de actions oficiales estables
  (v4→v5, backend de artefactos compartido v4+), y un fallo sería un fix trivial. El CI por-PR (ci.yml) sí valida
  los bumps de `ci.yml` incl. el nuevo job Redis.

## Verification

```bash
# YAML válido de los workflows tocados
python3 -c "import yaml; yaml.safe_load(open('.github/workflows/ci.yml')); yaml.safe_load(open('.github/workflows/release.yml'))"
# El job Redis: filtro selecciona exactamente los 8 tests del adaptador, verde contra Valkey local
WEFT_TEST_REDIS=localhost:6379 dotnet test tests/Weft.Server.Tests/ -c Release \
  --filter "FullyQualifiedName~RedisDocumentStoreContractTests"   # 8/8, 0 fallos, 0 omitidos
# Sin actions v4/v3 flageadas restantes
grep -rE 'actions/(checkout|setup-node|setup-dotnet|upload-artifact|download-artifact)@v4|setup-qemu-action@v3' .github/workflows/  # vacío
```

## Additional Notes

- FU-011 añade costo de CI **recurrente** por PR (un job ubuntu ~1-2 min con el service container) — decisión
  consciente del operador al despachar (aceptó reponer la cobertura). El adaptador Redis vuelve a estar cubierto
  en CI, no solo en el gate local.
- El job Redis es lean (sin build de cargo): la contract suite del adaptador opera sobre blobs opacos, no toca el
  motor nativo yrs.

## Approval

Chore de bajo riesgo (`risk_level: low`, `review_required: false`). Verificación local citada; el CI del PR valida
los bumps de `ci.yml` + el job Redis contra el service container.
