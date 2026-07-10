# Contract — `Weft.Server` API pública y protocolo

**Paquete**: `Weft.Server` (namespace `Weft.Server`; adaptadores en `Weft.Server.Persistence.*`) · **Hito**: M2

Servidor relay WebSocket para ASP.NET Core, compatible con clientes del ecosistema Yjs
(`y-websocket`/`y-prosemirror`/Tiptap) sin adaptación. Racional en
[research.md](../research.md) R7, R8, R17.

## Registro en la app del consumidor

```csharp
// Program.cs del consumidor:
builder.Services.AddWeftServer(options => {
    options.Engine = YrsEngine.Instance;              // default
    options.Broker = new DocumentBrokerOptions { /* idle eviction, límites */ };
});
builder.Services.AddSingleton<IWeftAuthorizer, MyLmsAuthorizer>();     // OBLIGATORIO
builder.Services.AddSingleton<IDocumentStore, InMemoryDocumentStore>(); // o EFCore/Redis/FileSystem

app.MapWeft("/collab");   // WebSocket endpoint: /collab/{docId}
```

- `AddWeftServer` sin `IWeftAuthorizer` registrado → excepción al arrancar (falla explícita:
  la autorización nunca es opcional ni por-defecto-permisiva; SC-010).
- `MapWeft` acepta la ruta base; `{docId}` es el último segmento (URL-encoded).

## Autorización (hook del consumidor — FR-019)

```csharp
public enum WeftAccess { Deny, ReadOnly, ReadWrite }

/// <summary>Decisión de acceso en el handshake. Weft no conoce usuarios ni parsea tokens:
/// el consumidor decide con su propia identidad (JWT, cookies, headers...).</summary>
public interface IWeftAuthorizer
{
    ValueTask<WeftAccess> AuthorizeAsync(HttpContext context, string docId, CancellationToken ct);
}
```

**Semántica**: `Deny` → 403 antes del upgrade WebSocket (ningún byte de contenido viaja).
`ReadOnly` → el cliente recibe sync/updates/awareness; si envía un update de documento, la
conexión se cierra con código de política (1008). `ReadWrite` → flujo completo. La decisión es
por-conexión (re-evaluar en reconexión); revocación en vivo queda del lado del consumidor
(cerrar la conexión vía `IWeftServer` abajo).

## Protocolo de wire (y-protocols — FR-014/015/016)

Mensajes WebSocket binarios, encoding lib0 (varint). Compatibilidad: `y-websocket` v1/v2.

```text
Mensaje := <msgType:varint> <payload>
msgType 0 = SYNC:
    subtipo 0 · SyncStep1(stateVector)   — "esto conozco" (lo envía quien se conecta)
    subtipo 1 · SyncStep2(update)        — respuesta: delta que le falta al emisor del SV
    subtipo 2 · Update(update)           — update incremental en vivo
msgType 1 = AWARENESS: payload del protocolo y-awareness (estados efímeros por cliente)
```

**Comportamiento del relay (relay-only por defecto)**:
1. Al conectar: servidor envía `SyncStep1(sv_servidor)` y responde el `SyncStep1` del cliente
   con `SyncStep2` (delta) — sync incremental en ambas direcciones (FR-016, SC-004).
2. Cada `Update` entrante (de conexión `ReadWrite`) se aplica al doc del `DocumentBroker`
   (fuente del state-vector del servidor), se persiste vía `IDocumentStore` y se difunde a las
   demás conexiones del doc.
3. `AWARENESS` se difunde a los pares sin persistirse; al cerrar una conexión se difunde la
   retirada de su estado (FR-015).
4. Mensajes malformados → cierre de la conexión afectada (1002), sin impacto en los pares
   (edge case de spec).

## Persistencia (FR-017)

```csharp
/// <summary>Estado durable por documento. Blobs opacos; implementaciones thread-safe.</summary>
public interface IDocumentStore
{
    /// <summary>Estado completo persistido (null si el doc no existe).</summary>
    ValueTask<byte[]?> LoadAsync(string docId, CancellationToken ct = default);
    /// <summary>Añade un update incremental (durabilidad entre snapshots).</summary>
    ValueTask AppendUpdateAsync(string docId, ReadOnlyMemory<byte> update, CancellationToken ct = default);
    /// <summary>Snapshot consolidado; reemplaza estado + updates acumulados (compaction).</summary>
    ValueTask SaveSnapshotAsync(string docId, ReadOnlyMemory<byte> state, CancellationToken ct = default);
}
```

- Carga de doc = `LoadAsync` + re-aplicar updates pendientes (el store los devuelve como parte
  del estado o los gestiona internamente — contrato de implementación documentado por adaptador).
- Snapshot-compaction: al desalojar del broker (`OnEvicting`) y al publicar (abajo).
- Adaptadores: `InMemoryDocumentStore`, `FileSystemDocumentStore` (en `Weft.Server`);
  `Weft.Server.Persistence.EFCore` y `.Redis` como paquetes separados (research R8).
  La suite de contrato de `IDocumentStore` es compartida: todo adaptador la pasa idéntica.

## Publicación desde el servidor (FR-018)

```csharp
/// <summary>Servicio de operación del servidor (inyectable en la app del consumidor).</summary>
public interface IWeftServer
{
    /// <summary>Snapshot content-addressed de un doc activo: mismo contenido → mismo
    /// VersionId que produciría Weft.Versioning en local (SC de paridad).</summary>
    ValueTask<VersionId> PublishAsync(string docId, CancellationToken ct = default);

    ValueTask<int> GetConnectionCountAsync(string docId, CancellationToken ct = default);
    /// <summary>Cierra las conexiones de un doc (p. ej. tras revocación de acceso).</summary>
    ValueTask DisconnectAllAsync(string docId, CancellationToken ct = default);
}
```

`PublishAsync` usa el `VersionStore` (requiere `IBlobStore` registrado) y ejecuta dentro del
turno del actor del doc → el snapshot es consistente aunque haya tráfico concurrente.
La exposición HTTP de publicar (endpoint REST, permisos) es dominio del consumidor: Weft da
el servicio, no la ruta.

## Postcondiciones de contrato (base de Weft.Server.Tests)

1. Dos clientes simulados `ReadWrite` convergen a contenido idéntico tras ediciones cruzadas (SC-005).
2. Cliente que reconecta con SV previo recibe solo delta (bytes medidos ≪ estado completo, SC-004).
3. `Deny` → 0 bytes de contenido intercambiados; `ReadOnly` que escribe → cierre 1008 (SC-010).
4. Awareness visible entre pares y retirada al desconectar; nunca tocada por `IDocumentStore`.
5. `PublishAsync` produce el mismo `VersionId` que publicar el mismo contenido en local.
6. Kill del servidor y rearranque → estado recuperado desde `IDocumentStore` sin pérdida de
   updates confirmados (con cualquier adaptador).
