---
id: AILOG-2026-07-10-003
title: "Despacho de follow-ups ready tras M0 (FU-007/008/009)"
status: accepted
created: 2026-07-10
agent: claude-opus-4-8
confidence: high
review_required: true
reviewed_by: Jose Villaseñor Montfort
reviewed_at: 2026-07-11
review_outcome: approved
risk_level: low
eu_ai_act_risk: not_applicable
nist_genai_risks: []
iso_42001_clause: []
lines_changed: 46
files_modified: []
observability_scope: none
tags: [versionado, crdt, dual-engine, deuda-tecnica, chore]
related: [AILOG-2026-07-10-002]
originating_charter: none
---

# AILOG: despacho de follow-ups `ready` tras M0 (FU-007/008/009)

## Summary

Limpieza de deuda menor post-M0: se despachan los tres follow-ups del bucket `ready` derivados de la
auditoría externa de CHARTER-02 (G3/G4/G5). No es un Charter — trabajo `chore` aislado, sin cambios de
comportamiento observable y sin dependencia de fases futuras. Ningún gate constitucional afectado. Los
dos follow-ups restantes (FU-002, FU-006) siguen `charter-triggered` (M2 y mini-charter Loro).

## Context

Tras cerrar M0 (CHARTER-01 + CHARTER-02) y el triaje del backlog (PR #6), el bucket `ready` quedaba con
FU-007 (XS), FU-008 (S) y FU-009 (S). Origen: `.straymark/audits/CHARTER-02/review.md` §4 y
`AILOG-2026-07-10-002` §Follow-ups.

## Actions Performed

1. **FU-007 (G3)**: `InMemoryBlobStore` pasa a `ConcurrentDictionary<VersionId, byte[]>`; se elimina el
   `id.ToString()` (hex de 64 chars) por operación en Put/Get/Exists. Habilitado porque `VersionId` ya
   es `IEquatable` con `GetHashCode`/`==` por valor.
2. **FU-008 (G4)**: nuevo miembro `string EngineName { get; }` en la abstracción `ICrdtDoc`
   (engine-agnóstico, cumple P-IV), implementado por `YrsDoc` (`"yrs"`) y `LoroDoc` (`"loro"`) contra
   una constante `EngineName` compartida con `ICrdtEngine.Name` en cada motor. Guards en
   `VersionStore.Merge` (target vs branch) y `VersionStore.MergeAsync` (target vs motor del almacén):
   una mezcla cross-engine lanza ahora `ArgumentException` clara **antes** del FFI, en vez de la
   `CorruptUpdateException` opaca del decoder nativo.
3. **FU-009 (G5)**: `FileSystemBlobStoreTests` — cobertura directa (5 casos: round-trip, id ausente,
   layout de sharding `aa/bb/hash`, idempotencia por content-addressing, ausencia de temporales `.tmp-*`),
   con directorio temporal aislado por instancia.
4. **Gobernanza**: FU-007/008/009 marcados `status: closed` en el backlog + `straymark followups recount`
   (2 open / 7 closed+superseded; bucket `ready` a cero).

## Modified Files

| File | Change Description |
|------|--------------------|
| `src/Weft.Versioning/Blobs/InMemoryBlobStore.cs` | Key `VersionId` directo (FU-007) |
| `src/Weft.Core/Abstractions/ICrdtDoc.cs` | Nuevo miembro `EngineName` (FU-008) |
| `src/Weft.Core/Yrs/YrsDoc.cs`, `Yrs/YrsEngine.cs` | `EngineName` + constante compartida (FU-008) |
| `src/Weft.Loro/LoroDoc.cs`, `LoroEngine.cs` | `EngineName` + constante compartida (FU-008) |
| `src/Weft.Versioning/VersionStore.cs` | Guards cross-engine en Merge/MergeAsync (FU-008) |
| `tests/Weft.Versioning.Tests/FileSystemBlobStoreTests.cs` | Cobertura directa del store en disco (FU-009) |
| `tests/Weft.Versioning.Tests/CrossEngineMergeGuardTests.cs` | Verificación del guard (FU-008) |
| `.straymark/follow-ups-backlog.md` | Cierre FU-007/008/009 + recount |

## Decisions Made

- **`EngineName` en `ICrdtDoc`, no un tipo nuevo**: la abstracción ya es la frontera engine-agnóstica;
  añadir la propiedad respeta P-IV (`Weft.Versioning` sigue sin referenciar tipos de yrs/Loro) y da al
  guard una comparación barata por string. Constante `internal const` por motor como fuente única de
  verdad compartida con `Name`.
- **Guard también en `MergeAsync`**: no solo el `Merge(doc,doc)` puede cruzar motores; un target de otro
  motor que el del almacén también fallaría opaco al aplicar el blob decodificado.

## Impact

- **Functionality**: sin cambio de comportamiento en paths existentes; solo un error más claro para un
  mal uso (mezcla cross-engine) que ningún path actual dispara.
- **Security/Memory**: sin cambios en la frontera FFI ni en ownership.
- **Performance**: FU-007 elimina una asignación de string de 64 chars por operación del store en memoria.

## Verification

- [x] `dotnet build Weft.sln` — 0 warnings / 0 errores (el nuevo miembro de interfaz obliga a ambas impls)
- [x] **45 tests .NET** verdes (Core 18, Versioning 25, Determinism 2); 8 nuevos (5 FileSystem + 3 guard)
- [x] Guard cross-engine verificado: `Merge(yrsDoc, loroDoc)` → `ArgumentException` (no `CorruptUpdateException`)
- [x] Backlog reconciliado con `straymark followups recount` (bucket `ready` = 0)
- [x] Revisión humana del operador — aprobada 2026-07-11 (ver §Approval)
- [x] CI del PR #7 en verde (11/11 checks)

## Additional Notes

Trabajo `chore` sin auditoría externa (no aplica a limpieza de deuda menor). No introduce follow-ups
nuevos; cierra los tres accionables de M0. FU-002 (hardening decoder, trigger M2) y FU-006 (superficie
`INativeVersioning` de Loro, mini-charter) permanecen diferidos a sus triggers.

## Approval

**Approved**: 2026-07-11 by `Jose Villaseñor Montfort`.
