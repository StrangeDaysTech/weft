---
id: AILOG-2026-07-13-002
title: "CHARTER-06: adaptadores de persistencia externos — EF Core + Redis (T053, T054)"
status: accepted
created: 2026-07-13
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
tags: [server, persistence, efcore, redis, valkey, document-store, contract-suite, adapters, us3]
related: [AILOG-2026-07-13-001, AILOG-2026-07-12-001]
originating_charter: CHARTER-06-adaptadores-de-persistencia-externos-ef-core-redis
---

# AILOG: CHARTER-06 — adaptadores de persistencia externos (EF Core + Redis)

## Summary

Ejecución de **T053/T054** (effort S): las dos últimas tareas de US3, fuera del journey de aceptación de M2
(ya cerrado). Se crean dos paquetes de persistencia externos que implementan el contrato **congelado**
`IDocumentStore` (blobs opacos, P-IV): `Weft.Server.Persistence.EFCore` (EF Core, relacional, provider-agnóstico)
y `Weft.Server.Persistence.Redis` (StackExchange.Redis; compatible con Valkey). Ambos **reusan la
`DocumentStoreContractSuite` compartida** vía subclases — la garantía de intercambiabilidad de stores que exige
US3. Verificación local: build Release 0 warnings, **124 tests verdes** (Server 70 incl. EF Core/SQLite y Redis
reales contra Valkey en `localhost:6379`; Core 27, Versioning 25, Determinism 2), y el path de skip confirmado
(sin Redis → 8 tests omitidos, 0 fallos). **Sin cambios en CI** por presupuesto de minutos (ver §Decisions y R4
del Charter); la cobertura Redis en CI se difiere a FU-011.

## Context

Trabajo bajo `.straymark/charters/06-adaptadores-de-persistencia-externos-ef-core-redis.md`, sobre el substrato
de CHARTER-04 (contrato `IDocumentStore` + `DocumentStateFraming` + contract suite) y CHARTER-05 (relay que
consume los stores). Research R8 difirió EF Core/Redis a paquetes separados para no arrastrar sus dependencias
al relay básico `Weft.Server`; este Charter los entrega. Ningún gate de M2/M3 depende de esto; no cierra hito y
no requiere auditoría externa multi-modelo.

## Actions Performed

1. **T053 — paquete `Weft.Server.Persistence.EFCore`**: `EFCoreDocumentStore : IDocumentStore` sobre un
   `DbContext` con una tabla de records (`DocId`/`Seq`/`Kind`/`Payload`). `Load` → snapshot (Kind=Snapshot) +
   updates (Kind=Update, orden `Seq`) enmarcados por `DocumentStateFraming.Frame`. `Append` → insert con
   `Seq = max+1`. `SaveSnapshot` → compaction en **transacción** (`ExecuteDelete` + insert snapshot Seq 0).
   Serialización **por documento** con `SemaphoreSlim` (patrón `DocLock` de FileSystem): asigna `Seq` sin
   carreras y evita `SQLITE_BUSY` bajo appends concurrentes. Provider-agnóstico (referencia
   `Microsoft.EntityFrameworkCore` + `.Relational`; el consumidor elige el provider). DI:
   `AddWeftEFCoreDocumentStore(configureContext)`.
2. **T054 — paquete `Weft.Server.Persistence.Redis`**: `RedisDocumentStore : IDocumentStore` sobre
   `StackExchange.Redis`. Claves derivadas por SHA-256 hex del `docId` opaco (evita colisión de separadores),
   sufijos `:s` (snapshot, string) y `:u` (updates, lista). `Load` → `GET` + `LRANGE` enmarcados. `Append` →
   `RPUSH` atómico. `SaveSnapshot` → compaction **atómica** (`MULTI/EXEC`: `SET` snapshot + `DEL` updates). DI:
   `AddWeftRedisDocumentStore(configuration)`.
3. **Cobertura de contrato**: `DocumentStoreContractSuite` gana subclases `EFCoreDocumentStoreContractTests`
   (SQLite en archivo temporal) y `RedisDocumentStoreContractTests` (Valkey real vía `RedisConnectionFixture`,
   db 15, prefijo único por test). La suite ejercita orden, compaction, aislamiento, blob de 4 MiB, 250 appends
   concurrentes y load/write concurrentes — idéntica para los 4 adaptadores.
4. **Solución + tasks**: `Weft.sln` gana los 2 proyectos; `tasks.md` marca T053/T054 `[X] — CHARTER-06`.

## Modified Files

**Nuevos (paquete EF Core):** `src/Weft.Server.Persistence.EFCore/{Weft.Server.Persistence.EFCore.csproj,
WeftDocumentStoreContext.cs, EFCoreDocumentStore.cs, EFCoreServiceCollectionExtensions.cs}`.

**Nuevos (paquete Redis):** `src/Weft.Server.Persistence.Redis/{Weft.Server.Persistence.Redis.csproj,
RedisDocumentStore.cs, RedisServiceCollectionExtensions.cs}`.

**Modificados:** `tests/Weft.Server.Tests/DocumentStoreContractSuite.cs` (subclases + `[Fact]→[SkippableFact]`,
ver §Decisions), `tests/Weft.Server.Tests/Weft.Server.Tests.csproj` (refs a ambos paquetes + `Sqlite` +
`Xunit.SkippableFact` + override `SQLitePCLRaw.bundle_e_sqlite3`), `Weft.sln`,
`specs/001-weft-crdt-versioning/tasks.md`.

## Decisions Made

1. **`[Fact]→[SkippableFact]` en el cuerpo abstracto de la contract suite** (desviación intencional de la
   declaración, que decía "cuerpo abstracto intacto"). Razón: para que la subclase Redis se **omita** (no falle)
   cuando el backend no está, el skip debe dispararse desde `CreateStore()` (primera línea de cada test), lo que
   exige que los tests sean `[SkippableFact]`. Es una generalización mínima y benigna: para los adaptadores
   in-proceso (InMemory/FileSystem/EFCore) el comportamiento es idéntico a `[Fact]`. Documentada en §Closing
   notes del Charter (atomic-update).
2. **Serialización por-documento en EF Core** (no confiar en el orden de autoincrement ni en el manejo de
   `SQLITE_BUSY` del provider): `SemaphoreSlim` por doc, igual que `FileSystemDocumentStore`. Correcto y
   provider-agnóstico; la contract suite (250 appends concurrentes al mismo doc) es el gate.
3. **Compaction atómica**: EF Core vía transacción explícita; Redis vía `MULTI/EXEC`. Cierra R3 del Charter
   (ventana de incoherencia snapshot+updates).
4. **Sin job de CI para Redis** (R4 del Charter): decisión de coste (minutos de GitHub Actions ~agotados). El
   gate del adaptador Redis es local (Valkey). Cobertura de CI diferida a **FU-011** — brecha registrada, no
   silenciada.

## Risk

- **R6 (new, not in Charter) — NU1903: `SQLitePCLRaw.lib.e_sqlite3` 2.1.11 vulnerable (GHSA-2m69-gcr7-jv3q,
  alta)**, arrastrado transitivamente por `Microsoft.EntityFrameworkCore.Sqlite` 10.0.9 en el **proyecto de
  test**. `TreatWarningsAsErrors` + NuGetAudit lo convirtió en error de restore. Mitigación: override explícito
  a `SQLitePCLRaw.bundle_e_sqlite3` 3.0.3 (SQLite ≥ 3.50, parcheado) en `Weft.Server.Tests.csproj`. Dependencia
  **solo de test** (SQLite in-proc con datos de confianza); ningún paquete publicable la toca. Verificado: restore
  y suite verdes tras el bump.

## Impact

- **API pública nueva**: `Weft.Server.Persistence.EFCore` (`EFCoreDocumentStore`, `WeftDocumentStoreContext`,
  `AddWeftEFCoreDocumentStore`) y `Weft.Server.Persistence.Redis` (`RedisDocumentStore`, `AddWeftRedisDocumentStore`).
  `Weft.Server` **no** gana dependencias a EF/Redis (research R8 preservado: la ref va adaptador → server).
- **US3 completa**: T043–T054 todas `[X]`. Siguiente frente: M3/US4 (T055–T060), donde estos paquetes entran al
  pack NuGet multi-RID.

## Verification

```bash
dotnet build Weft.sln -c Release            # 0 warnings, 0 errores
WEFT_TEST_REDIS=localhost:6379 dotnet test Weft.sln -c Release --no-build
# Server 70, Core 27, Versioning 25, Determinism 2 → 124 verdes, 0 fallos, 0 omitidos (con Valkey)
WEFT_TEST_REDIS=localhost:6399 dotnet test tests/Weft.Server.Tests/ -c Release --no-build
# 62 verdes, 8 omitidos (Redis no disponible → skip graceful), 0 fallos
```

## Additional Notes

- **Drift check** (`straymark charter drift CHARTER-06 --range origin/main..HEAD`): reporta 4 archivos como
  "scope expansion" (`Weft.sln`, los 2 `.csproj` de los paquetes, el `.csproj` de test). **Falso positivo**:
  los 4 **sí** están declarados en §Files to modify del Charter; el parser de drift reconoció los 6 `.cs` + 2
  `.md` (Declared: 8) pero no las extensiones `.csproj`/`.sln`. Sin drift real: todos los archivos modificados
  son intencionales y están declarados. (Limitación del parser candidata a adopter-feedback en StrayMark.)
- Valkey (fork Linux Foundation de Redis, wire-compatible) instalado por el operador en Fedora; el adaptador no
  distingue Redis de Valkey. Sin `requirepass` en local (gate sin credenciales).
- FU-011 (reponer la cobertura Redis en CI) se materializará en el registro vía este AILOG §Risk/§Decisions +
  `straymark followups drift --apply` cuando el presupuesto de CI lo permita.

## Approval

Pendiente de confirmación del operador (`risk_level: low`, `review_required: false`). Verificación local completa
citada arriba; concordancia AILOG↔código autoverificada.
