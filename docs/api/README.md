<!-- SPDX-License-Identifier: Apache-2.0 -->

# Weft — overview de API por paquete

Weft se distribuye como varios paquetes NuGet en capas. Referencia solo los que necesites; las dependencias
van **hacia el core**, nunca al revés.

| Paquete | Depende de | Para qué |
|---|---|---|
| **Weft.Core** | — (trae el motor nativo `yrs`) | Binding seguro + abstracciones + broker de concurrencia |
| **Weft.Versioning** | Weft.Core | Versionado content-addressed (publish/diff/branch/merge) |
| **Weft.Server** | Weft.Core, Weft.Versioning | Relay WebSocket y-sync para ASP.NET Core |
| **Weft.Server.Persistence.EFCore** | Weft.Server | Adaptador `IDocumentStore` sobre EF Core |
| **Weft.Server.Persistence.Redis** | Weft.Server | Adaptador `IDocumentStore` sobre Redis/Valkey |
| **Weft.Loro** | Weft.Core | Motor CRDT alternativo (Loro) tras la misma abstracción |

## Weft.Core

El binding y las abstracciones. Trae el shim nativo `weft_yrs_ffi` (motor `yrs`) empaquetado por RID.

- **`ICrdtEngine`** (`YrsEngine.Instance`) — fábrica de documentos: `CreateDoc()`, `LoadDoc(blob)`.
- **`ICrdtDoc`** — documento CRDT: `InsertText`/`DeleteText`/`GetText`, `ExportState`/`ExportStateVector`/
  `ExportUpdateSince`/`ApplyUpdate`. **No es thread-safe**: acceso serializado (o vía el broker).
- **Concurrencia** (`Weft.Concurrency`): `DocumentBroker`/`DocumentSession` — actor por documento
  (single-reader) para acceso concurrente seguro a escala.
- **Errores**: jerarquía `WeftException` (`CorruptUpdateException`, `WeftEngineException`) + `WeftErrorCode`.
- Ciclo de vida nativo: `SafeHandle` + contrato de ownership; el GC jamás toca memoria nativa.

## Weft.Versioning

Versionado inmutable content-addressed, **engine-agnóstico** (no referencia tipos de `yrs`/Loro).

- **`VersionStore(engine, IBlobStore)`** — `PublishAsync(doc)` → `VersionId` (`SHA-256` del export
  determinista), `CheckoutAsync`/`BranchAsync`/`DiffAsync`, `Merge`.
- **`IBlobStore`** — `InMemoryBlobStore`, `FileSystemBlobStore` (blobs por versión, citables).
- **`TextDiff`** — diff por palabras (segmentos `Equal`/`Insert`/`Delete`).

## Weft.Server

Relay WebSocket compatible con clientes Yjs estándar (`y-websocket`/`y-prosemirror`/Tiptap), sin adaptación.

- **DI**: `AddWeftServer(options)` (falla al arrancar sin `IWeftAuthorizer`) + `MapWeft(path)` → `path/{docId}`.
- **`IWeftAuthorizer`** — hook de acceso del consumidor (`Deny`/`ReadOnly`/`ReadWrite`); Weft nunca decide identidad.
- **`IDocumentStore`** — estado durable de blobs **opacos** (`LoadAsync`/`AppendUpdateAsync`/`SaveSnapshotAsync`);
  implementaciones `InMemory`/`FileSystem` incluidas, `EFCore`/`Redis` en paquetes aparte.
- **`IWeftServer`** — `PublishAsync` (paridad de `VersionId` server↔local), `GetConnectionCountAsync`,
  `DisconnectAllAsync`.

## Weft.Server.Persistence.EFCore / .Redis

Adaptadores `IDocumentStore` externos, intercambiables (pasan la misma contract suite). EF Core es
provider-agnóstico (SQLite/PostgreSQL/SQL Server); Redis es compatible con Valkey. Registro DI:
`AddWeftEFCoreDocumentStore(...)` / `AddWeftRedisDocumentStore(...)`.

## Weft.Loro

Motor CRDT alternativo (Loro) tras la misma `ICrdtEngine`/`ICrdtDoc` — prueba viva de que la abstracción es
reemplazable (P-IV). Ofrece además la capacidad opcional `INativeVersioning` para versionado nativo.

---

> Documentación de referencia por símbolo: los paquetes incluyen sus comentarios XML (IntelliSense) y
> SourceLink (navegación al fuente). Contratos formales en `specs/001-weft-crdt-versioning/contracts/`.
