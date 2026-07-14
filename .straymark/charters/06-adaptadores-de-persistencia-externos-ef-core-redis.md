---
charter_id: CHARTER-06-adaptadores-de-persistencia-externos-ef-core-redis
status: closed
closed_at: 2026-07-13
effort_estimate: S
trigger: "M2 cerrado (CHARTER-05 en main, 3d67761/74c1c05): la `DocumentStoreContractSuite` compartida está verde y congelada contra InMemory+FileSystem, y `contracts/server-api.md` §Persistencia declara `Weft.Server.Persistence.EFCore`/`.Redis` como paquetes separados (research R8). T053/T054 son las 2 tareas restantes de US3, fuera del journey de aceptación de M2 — este Charter las entrega."
originating_spec: specs/001-weft-crdt-versioning/spec.md
work_verb: implement
design_provenance: new
---

# Charter: Adaptadores de persistencia externos — EF Core + Redis

> **Status (mirrored from frontmatter — source of truth is above):** closed. Effort: S.
>
> **Origin:** Derivado de `specs/001-weft-crdt-versioning/spec.md` (US3, FR-017, research R8). Entrega las
> dos tareas de persistencia externa (**T053, T054**) que quedaron **fuera del journey de aceptación de M2**:
> M2 ya cerró con InMemory + FileSystem. Este Charter **no cierra ningún hito** y **no requiere auditoría
> externa por cierre de hito**.

## Context

FR-017 exige que el estado durable del servidor viva tras un contrato mínimo de blobs **opacos**
(`IDocumentStore`: load / append-update / save-snapshot por `docId`), con adaptadores **intercambiables**.
CHARTER-04 congeló ese contrato, entregó los adaptadores in-proceso (`InMemoryDocumentStore`,
`FileSystemDocumentStore`) y —clave para este Charter— la **`DocumentStoreContractSuite` compartida**: la
misma suite corre **idéntica** contra cada adaptador y es la garantía de intercambiabilidad que exige el
escenario de aceptación de US3. CHARTER-05 cerró M2 cableando el relay end-to-end sobre esos stores.

Este Charter entrega los dos adaptadores **externos** que la spec difirió a paquetes separados (research R8:
separarlos evita arrastrar EF Core / Redis al relay básico `Weft.Server`): `Weft.Server.Persistence.EFCore`
y `Weft.Server.Persistence.Redis`. Es trabajo de **implementación** contra un contrato ya congelado — sin
diseño abierto, sin superficie nueva de API pública en `Weft.Server`. El criterio de aceptación es binario y
ya existe: **cada adaptador pasa la `DocumentStoreContractSuite` sin modificar su cuerpo abstracto**. Fuera
del journey de M2; ningún gate de M2/M3 depende de esto (M3/US4 sí empaquetará estos paquetes en el pack de
release, T055, pero eso es US4).

## Scope

**In scope (T053, T054):**

1. **Paquete `Weft.Server.Persistence.EFCore` (T053)**: `EFCoreDocumentStore : IDocumentStore` respaldado por
   un `DbContext` con una tabla de records por documento (`DocId`, `Seq` monotónico, `Kind` snapshot|update,
   `Payload` blob opaco). `LoadAsync` → snapshot (si existe) + updates en orden de `Seq`, enmarcados por
   `DocumentStateFraming.Frame`. `AppendUpdateAsync` → insert de un record `update` con el siguiente `Seq`.
   `SaveSnapshotAsync` → compaction transaccional: borra los records del doc e inserta el snapshot como `Seq`
   base. Provider-agnóstico (el paquete no fija el provider; el consumidor configura `DbContextOptions`); la
   contract suite lo ejercita sobre **SQLite** (real, relacional, cross-plataforma). Extensión DI
   `AddWeftEFCoreDocumentStore(...)`.
2. **Paquete `Weft.Server.Persistence.Redis` (T054)**: `RedisDocumentStore : IDocumentStore` sobre
   `StackExchange.Redis`. Clave por doc (prefijo + `docId` opaco, binary-safe): un string para el snapshot y
   una lista para los updates (`RPUSH` preserva orden). `LoadAsync` → `GET` snapshot + `LRANGE` updates,
   enmarcados por `DocumentStateFraming.Frame`. `AppendUpdateAsync` → `RPUSH` atómico. `SaveSnapshotAsync` →
   compaction **atómica** (transacción `MULTI/EXEC`: set snapshot + del lista de updates). Extensión DI
   `AddWeftRedisDocumentStore(...)`.
3. **Cobertura de contrato (reuso, sin tocar el cuerpo abstracto)**: `DocumentStoreContractSuite.cs` gana dos
   subclases `sealed` (`EFCoreDocumentStoreContractTests`, `RedisDocumentStoreContractTests`) siguiendo el
   patrón de las de InMemory/FileSystem; la clase abstracta **no se modifica**. EF Core corre sobre SQLite
   in-proc (siempre, en el job `test` existente). Redis usa `[SkippableFact]`-equivalente: corre real cuando
   hay Redis en `WEFT_TEST_REDIS`/`localhost:6379`, y **se salta** cuando no lo hay.
4. **Gate del adaptador Redis = validación local** (no CI): con Redis instalado en la máquina de desarrollo, la
   corrida local de `Weft.Server.Tests` ejercita el adaptador real y es el gate antes de push. **No se añade
   job de CI** (decisión de coste: el presupuesto de minutos de GitHub Actions está agotándose — ver §Closing
   notes / trigger). En CI el test de Redis se salta; EF Core/SQLite sí corre en CI. La reposición de la
   cobertura Redis en CI (job Linux-only con service container) queda como **follow-up** (`FU-011`), a activar
   cuando el presupuesto de CI lo permita — **no se pierde la intención de cobertura, se difiere explícitamente**.
5. **Solución + tasks**: `Weft.sln` gana los dos proyectos; `tasks.md` marca **T053/T054 `[X] — CHARTER-06`**.

**Out of scope:**

- **Empaquetado NuGet / pack de release** de estos adaptadores (layout `runtimes/`, cross-compile, pack-smoke)
  — es **T055–T057 (US4/M3)**, no US3. Aquí solo se crean los proyectos y su metadata de paquete (heredada de
  `Directory.Build.props`); el pack real vive en M3.
- **Job de CI para el adaptador Redis** (Linux-only con service container) — **diferido a `FU-011`** por coste
  de minutos de CI. La cobertura del adaptador Redis en este Charter es **local** (Redis instalado). No es una
  omisión silenciosa: el follow-up repone la cobertura en CI cuando el presupuesto lo permita.
- Providers EF Core concretos de producción (Postgres/SQL Server/MySQL) más allá de SQLite-para-test — el
  paquete es provider-agnóstico; elegir provider es responsabilidad del consumidor.
- `INativeVersioning` de Loro (**FU-006**) — mini-charter aparte; ningún gate depende.
- Endurecimiento de durabilidad del relay (persist-before-broadcast, **FU-010**) — opcional, registrado, sin
  relación con la implementación de los stores (vive en `WeftConnection`, no en `IDocumentStore`).
- Modificar el contrato `IDocumentStore` o el cuerpo abstracto de `DocumentStoreContractSuite` — **congelados**
  desde CHARTER-04; tocarlos sería drift de contrato, no un adaptador.

## Files to modify

<!-- Reconnaissance #210: IDocumentStore, DocumentStateFraming, DocumentStoreContractSuite (subclases
     InMemory/FileSystem en el mismo archivo), FileSystemDocumentStore (patrón de referencia: DocLock per-doc,
     Seq monotónico, Frame), Weft.Server.csproj (SEPARADO — no gana ref a EF/Redis), Weft.Server.Tests.csproj
     (TestHost + copia cdylib), ci.yml (job `test` matriz sin service containers), Directory.Build.props
     (metadata de paquete heredada) — todos leídos y verificados presentes. Los archivos de los dos paquetes
     nuevos NO existen (confirmado: src/ no tiene Weft.Server.Persistence.*). -->

| File | Change |
|---|---|
| `src/Weft.Server.Persistence.EFCore/Weft.Server.Persistence.EFCore.csproj` | New — paquete; `PackageReference` a `Microsoft.EntityFrameworkCore` **y `.Relational`** (ToTable/índices, ver §Closing notes); `ProjectReference` a `Weft.Server` (por `IDocumentStore`, `DocumentStateFraming`) |
| `src/Weft.Server.Persistence.EFCore/EFCoreDocumentStore.cs` | New — `IDocumentStore` sobre EF Core (T053) |
| `src/Weft.Server.Persistence.EFCore/WeftDocumentStoreContext.cs` | New — `DbContext` + entidad de record (`DocId`/`Seq`/`Kind`/`Payload`) |
| `src/Weft.Server.Persistence.EFCore/EFCoreServiceCollectionExtensions.cs` | New — `AddWeftEFCoreDocumentStore(...)` |
| `src/Weft.Server.Persistence.Redis/Weft.Server.Persistence.Redis.csproj` | New — paquete; `PackageReference` a `StackExchange.Redis`; `ProjectReference` a `Weft.Server` |
| `src/Weft.Server.Persistence.Redis/RedisDocumentStore.cs` | New — `IDocumentStore` sobre `StackExchange.Redis` (T054) |
| `src/Weft.Server.Persistence.Redis/RedisServiceCollectionExtensions.cs` | New — `AddWeftRedisDocumentStore(...)` |
| `tests/Weft.Server.Tests/DocumentStoreContractSuite.cs` | Change — 2 subclases `sealed` (EFCore/Redis) + `RedisConnectionFixture`; **`[Fact]→[SkippableFact]`** en los tests base (desviación intencional, ver §Closing notes) |
| `tests/Weft.Server.Tests/Weft.Server.Tests.csproj` | Change — `ProjectReference` a ambos paquetes; `Microsoft.EntityFrameworkCore.Sqlite`; `Xunit.SkippableFact`; **override `SQLitePCLRaw.bundle_e_sqlite3` 3.0.3** (NU1903, ver §Closing notes) |
| `Weft.sln` | Change — añadir los 2 proyectos nuevos |
| `specs/001-weft-crdt-versioning/tasks.md` | Change — marcar **T053, T054** `[X] — CHARTER-06` |
| `.straymark/07-ai-audit/agent-logs/AILOG-*.md` | New, `risk_level: low` (adaptadores managed puros contra contrato congelado; sin frontera nativa nueva, sin superficie de API pública nueva en `Weft.Server`) |

## Verification

### Local checks

> **Lección de CHARTER-01..05**: correr TODO localmente en verde ANTES de pushear.

```bash
# Build de toda la solución (incluye los 2 paquetes nuevos)
dotnet build Weft.sln -c Release

# Asegurar Redis activo en localhost:6379 (instalado en la máquina de desarrollo).
# Ejemplo Fedora: sudo dnf install redis && sudo systemctl start redis  (o `redis-server` en foreground).
redis-cli ping   # → PONG

# Contract suite completa: EF Core (SQLite in-proc) + Redis (real, contra el Redis local) — GATE de este Charter.
WEFT_TEST_REDIS=localhost:6379 dotnet test tests/Weft.Server.Tests/ --configuration Release

# Suite completa verde (M0/M1/M2 intactos)
dotnet test --configuration Release
```

> El test de Redis es `[SkippableFact]`: **corre** cuando `WEFT_TEST_REDIS`/`localhost:6379` responde (gate
> local antes de push), y **se salta** cuando no hay Redis (CI, o una máquina sin Redis). En CI no se ejecuta —
> su reposición está en `FU-011`.

### Production smoke (after deploy)

No aplica — librería sin despliegue. Los auditores externos deben saltar esta sección.

## Risks

- **R1 — Un adaptador no pasa la contract suite idéntica (orden / compaction / aislamiento divergentes)**:
  severidad **media**. Es exactamente lo que la intercambiabilidad de US3 (FR-017) prohíbe. Mitigación: reusar
  la **misma** `DocumentStoreContractSuite` vía subclases, sin tocar su cuerpo abstracto; cada adaptador enmarca
  con el mismo `DocumentStateFraming.Frame` que FileSystem. Si falla: el adaptador no es intercambiable → no se
  marca T053/T054 hasta verde.
- **R2 — SQLite: `SQLITE_BUSY` bajo los 250 appends concurrentes de la suite (`Concurrent_appends_are_all_persisted`)**:
  severidad **media**. SQLite serializa escritores; sin manejo, la escritura concurrente falla. Mitigación:
  serialización per-doc (semáforo, patrón `DocLock` de `FileSystemDocumentStore`) y/o `busy_timeout`; el `Seq`
  monotónico se asigna dentro de la sección crítica. El test de concurrencia de la suite es el gate.
- **R3 — Redis: no-atomicidad de snapshot+compaction (`SET` snapshot + `DEL` updates en dos comandos)**:
  severidad **media**. Una ventana entre ambos deja al `LoadAsync` concurrente viendo snapshot nuevo + updates
  viejos (o al revés) → estado incoherente. Mitigación: `SaveSnapshotAsync` usa una **transacción `MULTI/EXEC`**
  (set + del atómicos); `AppendUpdateAsync` es un `RPUSH` atómico de suyo. El test
  `Concurrent_loads_and_writes_do_not_fault` de la suite ejercita el solapamiento.
- **R4 — El adaptador Redis no se ejercita en CI (gate solo local)**: severidad **baja**. Por coste de minutos
  de CI no se añade job con service container; el test de Redis se salta en CI y el gate real es la corrida
  local con Redis instalado. Riesgo: una regresión del adaptador Redis introducida sin correr los tests
  localmente no la atrapa CI. Mitigación: (a) el adaptador es **.NET managed puro** (sin nativo por-plataforma),
  su comportamiento no depende del SO, así que la corrida local en Fedora es representativa; (b) la disciplina
  "todo verde local antes de push" de los Charters incluye la corrida con `WEFT_TEST_REDIS`; (c) **`FU-011`**
  repone el job de CI cuando el presupuesto lo permita. La brecha está **registrada, no silenciada**.
- **R5 — Arrastre de EF Core / Redis al relay básico (`Weft.Server`)**: severidad **baja**. Meterlos como
  dependencia de `Weft.Server` obligaría a todo consumidor del relay a cargarlos (contra research R8).
  Mitigación: **paquetes separados**; `Weft.Server.csproj` **no** gana `ProjectReference` a los adaptadores —
  la dependencia va al revés (adaptador → `Weft.Server`), y solo el proyecto de tests referencia ambos.

## Tasks

1. Sync main, branch `charter/06-persistence-adapters`. Flip `declared` → `in-progress` al empezar a ejecutar.
2. Re-evaluar **Constitution Check** (per-Charter): **P-IV** (blobs opacos; los adaptadores nunca interpretan
   bytes de yrs/Loro — hablan a `IDocumentStore`, no al motor) y **P-V** (thread-safety per-doc del store). Sin
   violaciones esperadas; sin frontera nativa nueva (P-I/P-II no aplican — código managed puro).
3. `/speckit-implement` acotado a **T053, T054**; marcar `[X] — CHARTER-06` por tarea.
4. **AILOG** (`risk_level: low`, `review_required: false`). **AIDEC** si emerge una decisión sustantiva (p. ej.
   estrategia de serialización SQLite; forma exacta de la transacción Redis de compaction).
5. **Verificación local COMPLETA** (bloque Local checks íntegro, incl. la corrida opcional con Redis efímero)
   ANTES de push.
6. `straymark charter drift CHARTER-06` antes de commit; drifts → completarlos o documentarlos como
   `R<N+1> (new, not in Charter)` en el AILOG.
7. Commit + push + abrir PR contra `main`; **CI verde** (sin job Redis nuevo — coste de minutos; ver `FU-011`).

## Charter Closure

Charter que **no cierra hito** (M2 ya cerrado; M3/US4 es el siguiente) y **no requiere auditoría externa
multi-modelo** (esa es obligatoria solo en cierres de hito, como CHARTER-02/03/05). Al cerrar:

1. Actualización atómica del Charter (format v4) si el drift check reveló divergencias, en el **mismo PR**
   (editar `## Files to modify` y/o añadir `## Closing notes`).
2. `straymark charter drift CHARTER-06 --range origin/main..HEAD` → limpio o documentado en el AILOG.
3. `straymark charter close CHARTER-06` (telemetría, status `closed`, `closed_at`). No borrar este archivo.
4. Confirmar que **US3 queda 100 % entregada** (T043–T054 todas `[X]`); el siguiente frente es **M3/US4**
   (release NuGet multi-RID, T055–T060), donde estos paquetes entran al pack.

## Closing notes

Cerrado **2026-07-13** vía **PR** (implementación + AILOG en el mismo PR). Entrega **T053/T054**; con ello
**US3 queda 100 % completa** (T043–T054). No cierra hito (M2 ya cerrado). Referencia: `AILOG-2026-07-13-002`.
Verificación local: build Release 0 warnings, **124 tests verdes** con Valkey (`localhost:6379`); skip graceful
confirmado sin Redis (8 omitidos, 0 fallos).

Tres desviaciones intencionales respecto a la declaración, remediadas atómicamente (mismo PR):

- **`[Fact]→[SkippableFact]` en el cuerpo abstracto de `DocumentStoreContractSuite`** — la declaración decía
  "cuerpo abstracto intacto; solo añade subclases". Para que la subclase Redis se **omita** (no falle) cuando el
  backend no está, el skip debe dispararse desde `CreateStore()` (primera línea de cada test), lo que exige que
  los tests base sean `[SkippableFact]`. Generalización mínima y benigna: para los adaptadores in-proceso el
  comportamiento es idéntico a `[Fact]`. Se añadió además `RedisConnectionFixture` en el mismo archivo. Ref:
  `AILOG-2026-07-13-002 §Decisions #1`.
- **`Microsoft.EntityFrameworkCore.Relational`** añadido al paquete EF Core — la declaración solo citaba
  `Microsoft.EntityFrameworkCore`. `ToTable`/índices/`ExecuteDelete`/transacciones son API relacional; el
  adaptador es relacional (aunque provider-agnóstico). Ref: `AILOG-2026-07-13-002 §Actions #1`.
- **Override `SQLitePCLRaw.bundle_e_sqlite3` 3.0.3** en el csproj de test — `Microsoft.EntityFrameworkCore.Sqlite`
  10.0.9 arrastra `SQLitePCLRaw.lib.e_sqlite3` 2.1.11, con NU1903 (GHSA-2m69-gcr7-jv3q, alta) que
  `TreatWarningsAsErrors` volvió error de restore. Bump al bundle parcheado (SQLite ≥ 3.50); dependencia solo de
  test. Registrado como `R6 (new, not in Charter)`. Ref: `AILOG-2026-07-13-002 §Risk`.
