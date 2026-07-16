# Arquitectura de Weft

> Cómo está construido Weft y por qué. Documento de orientación para quien va a consumir la
> librería, integrarla o contribuir a ella.
>
> **Alcance**: este doc explica la **forma** del sistema y el **contrato de memoria** de la
> frontera nativa. No duplica lo que ya vive en otro sitio: la API por paquete está en
> [`docs/api/README.md`](api/README.md), los contratos formales en
> [`specs/001-weft-crdt-versioning/contracts/`](../specs/001-weft-crdt-versioning/contracts/), y el
> *porqué* de cada decisión técnica en
> [`research.md`](../specs/001-weft-crdt-versioning/research.md) (R1–R17), al que se enlaza en vez
> de reexplicarlo.

## Qué es Weft

Weft es un **building block**, no una aplicación: da colaboración CRDT en tiempo real y versionado
content-addressed a aplicaciones .NET. El trabajo CRDT real lo hace [`yrs`](https://github.com/y-crdt/y-crdt)
(el core Rust de Yjs); Weft aporta un binding seguro, un modelo de versionado que `yrs` no tiene, un
relay compatible con el ecosistema Yjs, y la disciplina de memoria/determinismo que hace que todo eso
sea sostenible desde .NET.

Dos consecuencias de diseño que conviene tener claras desde el principio:

- **Weft no decide tu autenticación, tu almacenamiento ni tu retención.** El relay delega la
  autorización en un `IWeftAuthorizer` tuyo (R17), la persistencia en un `IDocumentStore` tuyo (R8), y
  las versiones publicadas son inmutables sin `delete` en v1 — la política de retención es dominio
  del consumidor.
- **La interoperabilidad con Yjs es un requisito, no un accidente.** El wire es el protocolo y-sync
  sobre lib0 (R7) y el encoding es byte-idéntico al de Yjs JS, verificado por un gate bloqueante.
  Un cliente Tiptap/y-websocket existente habla con Weft sin adaptadores.

## Mapa de módulos

Seis paquetes. Todas las dependencias apuntan **hacia el core**; ninguna al revés.

```text
                    ┌───────────────┐
                    │   Weft.Core   │  binding yrs + abstracciones + broker
                    └───────┬───────┘
            ┌───────────────┼───────────────┐
            │               │               │
    ┌───────▼──────┐ ┌──────▼──────┐ ┌──────▼─────┐
    │Weft.Versioning│ │  Weft.Loro  │ │            │
    └───────┬───────┘ └─────────────┘ │            │
            │                          │            │
            └──────────┬───────────────┘            │
                ┌──────▼──────┐                     │
                │ Weft.Server │─────────────────────┘
                └──────┬──────┘
            ┌──────────┴──────────┐
    ┌───────▼────────┐   ┌────────▼───────┐
    │ …Persistence   │   │ …Persistence   │
    │    .Redis      │   │    .EFCore     │
    └────────────────┘   └────────────────┘
```

| Paquete | Responsabilidad | Depende de |
|---|---|---|
| **Weft.Core** | Binding seguro a `yrs` vía shim C-ABI propio; abstracciones (`ICrdtEngine`, `ICrdtDoc`); concurrencia serializada (`DocumentBroker`) | — |
| **Weft.Versioning** | Versionado content-addressed **engine-agnóstico**: `VersionStore`, `VersionId`, `IBlobStore`, diff por palabras | Weft.Core |
| **Weft.Server** | Relay WebSocket y-sync para ASP.NET Core: `AddWeftServer`/`MapWeft`, awareness, backpressure | Weft.Core, Weft.Versioning |
| **Weft.Loro** | Adaptador dual-path sobre Loro. Existe para mantener honesta la abstracción (P-IV) | Weft.Core |
| **…Persistence.Redis** / **…Persistence.EFCore** | Implementaciones de `IDocumentStore` | Weft.Server |

La regla que sostiene el grafo: **`Weft.Versioning` no puede referenciar tipos de `yrs` ni de Loro.**
Sólo habla con las abstracciones. Es lo que hace que el versionado funcione igual sobre ambos motores,
y lo verifica un gate (`dual-engine`) que corre la misma suite sobre los dos.

## La frontera FFI

Es la parte del sistema donde un error no da una excepción sino corrupción silenciosa, así que es la
que más disciplina lleva.

### Por qué un shim propio

Weft no llama a `yrs` directamente ni usa un binding de terceros: mantiene su propio shim C-ABI en
Rust (`native/weft-yrs-ffi`), y .NET habla con él vía `[LibraryImport]` (R1). El shim es la única
superficie que cruza; `yrs` nunca se expone. Eso permite fijar la semántica que .NET necesita
—índices en UTF-16, errores tipados, un contrato de memoria explícito— en vez de heredar la de Rust.

Un detalle no obvio: **los índices son unidades de código UTF-16**, no bytes UTF-8 (el default de
`yrs`). Es lo que hace que `doc.InsertText("t", 5, …)` signifique lo mismo desde C# que desde Yjs JS,
y coincida con `string.Length` de .NET.

Las versiones de los motores están **fijadas exactamente** (`yrs = "=0.27.2"`, `loro = "=1.13.6"`): un
bump es un acto deliberado con protocolo propio (R16), porque puede cambiar el encoding y por tanto
la identidad de las versiones ya publicadas.

### Contrato de ownership de memoria

**Ésta es la parte que hay que leer si vas a tocar el shim.** La regla de oro:

> El GC de .NET **nunca** toca memoria nativa. Todo buffer que el shim entrega se libera **sólo** con
> `weft_buf_free`, exactamente una vez, con el mismo `(ptr, len)` que se recibió.

Tres clases de memoria cruzan la frontera, con reglas distintas:

| Qué | Quién lo asigna | Quién lo libera | Regla |
|---|---|---|---|
| **Handle de documento** (`WeftDoc*`) | El shim (`weft_doc_new` / `weft_doc_load`) | El llamador, con `weft_doc_free`, **exactamente una vez** | La idempotencia **no** está garantizada: liberar dos veces es UB. Del lado C# lo envuelve un `SafeHandle`, así que no lo haces a mano |
| **Buffers de salida** (`out_ptr` + `out_len`) | El shim (`Box<[u8]>` + `mem::forget`) | El llamador, con `weft_buf_free(ptr, len)` | `len` debe ser el que el shim devolvió: reconstruye el `Box` desde `(ptr, len)`. Un `len` distinto corrompe el allocator |
| **Buffers de entrada** | El llamador | El llamador | Están **prestados**: el shim no toma posesión ni retiene el puntero más allá de la llamada |

Dos postcondiciones que ahorran depuración:

- **En error, los out-params no se escriben.** Si el código de retorno no es `WEFT_OK`, no hay nada
  que liberar.
- **En éxito, un resultado vacío puede tener `out_ptr` válido con `out_len == 0`.** Hay que liberarlo
  igual: «vacío» no es «nulo».

Del lado .NET esto se concentra en **un punto por motor**: `YrsDoc.TakeOwnedBuffer` y
`LoroDoc.TakeOwnedBuffer` (que llama a `weft_loro_buf_free` — cada shim libera con el suyo, nunca
cruzados). Ambos copian a memoria gestionada y liberan en un `finally`. Si auditas fugas, son los dos
sitios por los que empezar; el resto del código gestionado nunca ve un puntero nativo.

Los handles usan `SafeHandleZeroOrMinusOneIsInvalid`, que resuelve de una vez fuga, double-free y
use-after-free (R2). Hay una fricción conocida: `[LibraryImport]` no marshala `SafeHandle`
(SYSLIB1051), así que los P/Invoke declaran `nint` crudo y las llamadas prestan el puntero con un
`HandleLease` (`DangerousAddRef`/`DangerousRelease`). Es deliberado y está documentado, no un descuido.

### Ningún panic cruza la frontera

Un panic de Rust desenrollando a través de una frontera C es UB. Por eso **toda** entrada del shim que
ejecuta código del motor envuelve su cuerpo en un helper `guard()` con `catch_unwind`: un panic se
convierte en `WEFT_ERR_PANIC` (-127), nunca en un desenrollado que cruza. `weft_doc_free` y
`weft_buf_free` usan `catch_unwind` directo por la misma razón. (La única excepción es
`weft_abi_version`, que devuelve una constante y no puede entrar en pánico.)

Esta garantía se verifica end-to-end, no se asume: el shim exporta `weft_test_panic` bajo la feature
`test-hooks`, y la suite comprueba que un panic real se contiene y el proceso sigue vivo (SC-009). El
símbolo **nunca viaja en release**: el pipeline compila sin la feature, y el job `native` de
`release.yml` verifica con `nm` que no está exportado en los cdylibs antes de que lleguen al pack.

**Lo que `catch_unwind` no puede contener**: un fallo de asignación aborta el proceso vía
`handle_alloc_error`, que no es un panic. Es la raíz de R6 — ver [Límites conocidos](#límites-conocidos).

### Errores

Códigos `i32` en la frontera, mapeados a la jerarquía `WeftException` del lado gestionado. La
traducción es total: ningún código se filtra a la API pública como número.

| Código | Valor | Excepción .NET |
|---|---|---|
| `WEFT_OK` | 0 | — |
| `WEFT_ERR_NULL_ARG` | -1 | `WeftException` |
| `WEFT_ERR_DECODE` | -2 | `CorruptUpdateException` |
| `WEFT_ERR_APPLY` | -3 | `WeftEngineException(Apply)` |
| `WEFT_ERR_UTF8` | -4 | `WeftEngineException(Utf8)` |
| `WEFT_ERR_OUT_OF_BOUNDS` | -5 | `ArgumentOutOfRangeException` |
| `WEFT_ERR_PANIC` | -127 | `WeftEngineException(Panic)` |

### Carga del binario y verificación de ABI

El resolver se registra solo (`[ModuleInitializer]`) y busca el binario en
`runtimes/<rid>/native/` → `runtimes/<portable-rid>/native/` → directorio base, con fallback a
`NATIVE_DLL_SEARCH_DIRECTORIES`. Es el layout estándar de NuGet multi-RID (patrón SkiaSharp, R11),
así que `dotnet add package Weft.Core` resuelve el nativo sin que hagas nada.

Al cargar, **verifica la ABI antes de usar la librería**: llama a `weft_abi_version` y, si el símbolo
falta o la versión no es la esperada (hoy **2**), libera la librería y lanza una excepción explícita.
Esto convierte un desajuste binario —que si no sería corrupción silenciosa o un crash sin contexto—
en un error legible en el primer uso. La ABI subió a 2 al añadirse `weft_doc_new_with_client_id`
(client-ids deterministas, necesarios para el gate de paridad con Yjs).

La superficie son **12 funciones** de datos (crear doc, crear doc con client-id fijo, cargar, liberar;
insertar/borrar/leer texto; exportar estado/state-vector/delta; aplicar update; liberar buffer), más
`weft_abi_version`. El contrato formal está en
[`contracts/ffi-abi.md`](../specs/001-weft-crdt-versioning/contracts/ffi-abi.md).

### El shim de Loro

`native/weft-loro-ffi` es simétrico, con prefijo `weft_loro_*`, su propio `weft_loro_buf_free` y su
propio `weft_loro_abi_version`. Mismas reglas de ownership y de `catch_unwind`.

Expone además tres *probes* que `yrs` no tiene (`shallow_snapshot`, `native_diff_probe`,
`native_branch_merge_probe`), superficie de `INativeVersioning`. **Son demostrativos**: su salida no
es byte-determinista entre réplicas, **no** alimentan `VersionId` y ningún gate depende de ellos.
Existen para probar que la abstracción admite capacidades específicas de motor sin que el dominio se
entere; el content-addressing sigue viniendo de `ExportState()` en ambos motores.

## Flujo de sync

```text
cliente ──WebSocket──> MapWeft ──> IWeftAuthorizer ──> WeftServer ──> DocumentHub
                                        │                                  │
                                  Deny → 403                        DocumentSession
                              (antes del upgrade)                          │
                                                                    DocumentActor
                                                                   (canal 1-reader)
                                                                           │
                                                                       ICrdtDoc
```

El recorrido de un update:

1. **Endpoint**. `MapWeft` expone `{pattern}/{docId}`. Si falta un `IWeftAuthorizer` o un
   `IDocumentStore` registrado, **falla al arrancar**, no en la primera petición. Un `Deny` responde
   **403 antes del upgrade** a WebSocket: cero bytes de contenido para quien no tiene acceso.
2. **Handshake**. El **servidor** manda su `SyncStep1` primero; el cliente responde con el suyo y el
   servidor contesta con `SyncStep2(delta)`. Incremental en ambas direcciones desde el primer byte:
   por eso una reconexión transfiere un delta y no el estado completo (SC-004; medido: 479 B → 26 B,
   94,6 % menos).
3. **Aplicar y persistir**. El update se aplica dentro del turno del actor y se persiste (`AppendUpdateAsync`).
4. **Difundir**. El delta se difunde a **todas** las conexiones del documento, incluido el emisor. El
   eco es un no-op CRDT idempotente, y es deliberado: rastrear el origen dentro del turno del actor
   costaría más que dejar que el CRDT haga su trabajo.
5. **Cerrar**. Cada desconexión emite la retirada de awareness de ese cliente, para que los demás
   dejen de verlo al instante (FR-015). Cuando además era el **último**, se consolida un snapshot
   (compaction) y se libera la sesión.

Cosas del protocolo que conviene saber:

- **Awareness (presencia) nunca se persiste.** Es efímera por definición; se difunde a los pares y ya.
- **Un cliente `ReadOnly` que manda `SyncStep2` se ignora sin cerrar la conexión** — cerrarla rompería
  el handshake y-websocket estándar. Pero si manda un `Update` se cierra con **1008** (PolicyViolation).
  La distinción es intencional.
- **Backpressure en dos ejes** (FU-002): tamaño de mensaje (16 MiB por defecto → cierre **1009**) y
  cola de envío por conexión (256 → se cierra el consumidor lento). Un cliente lento no puede hacer
  crecer la memoria del servidor sin límite.
- Mensaje malformado → cierre **1002** (ProtocolError). El cap de tamaño se aplica **antes** de parsear.

### Concurrencia

`ICrdtDoc` **no es thread-safe**, y no se pretende que lo sea. La serialización vive un nivel arriba:

- Un **`DocumentActor` por documento**, con un canal de un solo lector (R6). Todas las operaciones de
  un documento pasan por su turno; no hay locks en el camino caliente.
- El **broker** gestiona el ciclo de vida: desalojo por inactividad, LRU bajo presión de memoria, y
  recarga desde lo persistido al reabrir. `MaxActiveDocuments` es un límite **suave**: nunca desaloja
  un documento con sesiones vivas.
- La carrera interesante —desalojo en vuelo mientras alguien reabre el mismo documento— se resuelve
  esperando a que la persistencia termine antes de recargar (SC-006). Está cubierta por la prueba de
  carga, que fuerza cientos de miles de desalojos.

Fuera del broker, serializar es responsabilidad del dueño del documento (P-V).

## Modelo de versionado

Ortogonal al sync: puedes versionar sin servidor, y servir sin versionar.

- **`VersionId` = SHA-256 del export determinista.** No hay contador ni reloj: la identidad *es* el
  contenido (R10). Dos réplicas convergidas publican el mismo id, byte a byte. Esto sólo funciona
  porque el export es determinista — de ahí que el determinismo sea un principio constitucional con
  gate propio, y no un detalle de implementación.
- **`IBlobStore`** guarda blobs por hash: `Put` es idempotente y la deduplicación sale gratis. **No hay
  `delete` en v1**: las versiones publicadas son inmutables y la retención la decide el consumidor.
- **`VersionStore`** publica, hace checkout, diff (LCS por palabras, R9), branch y merge. Al leer,
  **re-hashea y compara** antes de devolver: un blob corrupto da `BlobIntegrityException`, no un
  documento silenciosamente equivocado.
- **Los merges cross-engine se rechazan** comparando `EngineName`: el formato de update de yrs y el de
  Loro no son intercambiables, y fallar temprano y claro es mejor que fallar dentro del FFI.

Una nota sobre citabilidad: **nunca se usa `skip_gc`** en el motor. La capacidad de citar una versión
antigua no viene de retener basura en el documento vivo, sino de que cada versión publicada es un blob
inmutable direccionado por contenido.

### Paridad servidor ↔ local

`IWeftServer.PublishAsync` ejecuta el export **dentro del turno del actor**, lo que garantiza que
publicar desde el servidor da el mismo `VersionId` que publicar en local sobre el mismo estado. Sin
esa garantía, «la versión v1 del documento» significaría cosas distintas según quién la publicó.

## Persistencia (eje distinto)

`IDocumentStore` trata el estado como **blobs opacos** (R8): snapshot consolidado + updates
acumulados. La capa de persistencia no sabe de CRDTs, y eso es a propósito — es lo que permite que
haya adaptadores de Redis, EF Core, filesystem y memoria intercambiables, todos validados por la
**misma** suite de contrato.

La recuperación tolera solapamiento entre snapshot y updates porque aplicar un update es idempotente:
en el peor caso se aplica dos veces lo mismo y converge igual.

## Gates

Los gates no son CI decorativo: son la constitución hecha ejecutable. Corren por PR en
`ci.yml` salvo donde se indique.

| Gate | Qué protege | Bloquea | Principio |
|---|---|---|---|
| `test-{linux,win,mac}` | Build + suite en los 3 SO | Sí | P-VI |
| `asan` | 0 fugas / 0 double-free en ambos shims (nightly + ASan/LSan) | Sí | P-II |
| `determinism` | Export byte-determinista cross-RID **y paridad byte-idéntica con Yjs JS** | Sí | P-III |
| `dual-engine` | La misma suite de versionado verde sobre yrs **y** Loro | Sí | P-IV |
| `fuzz` | La frontera FFI ante bytes arbitrarios | **Parcial** | P-I/P-II |
| `pack-smoke` | Instalar el paquete y correr hello-Weft por RID; símbolo de test ausente | **No** (ver abajo) | P-VI |

Dos matices que importan si vas a fiarte de estos gates:

- **`fuzz` bloquea a medias, y es deliberado.** Si los targets no *compilan*, el job se pone rojo. Si
  un target *encuentra un crash*, sólo emite un `::warning`. La razón es R6 (ver abajo): hoy los
  targets reproducen un fallo conocido de `yrs` aguas abajo del shim, y bloquear merges por él
  paralizaría el repo sin arreglar nada.
- **El `pack-smoke` real no corre por PR.** El job de `ci.yml` es un marcador; la matriz
  cross-compile de verdad —y la verificación de que `weft_test_panic` no está exportado— vive en
  `release.yml`, que es `workflow_dispatch` únicamente, porque la matriz es cara. En la práctica el
  empaquetado se valida en el dry-run del release, no en cada PR.

## Límites conocidos

Vale más decirlos que descubrirlos en producción:

- **R6 — amplificación de memoria del decoder de `yrs`.** Un update malformado de pocos bytes puede
  declarar una longitud enorme y hacer que `yrs` reserve sin cota; la asignación falla y el proceso
  aborta (`handle_alloc_error`, **no** capturable por `catch_unwind`). El shim es correcto —contiene
  panics, sin UB—; el fallo está aguas abajo. El fix está enviado upstream
  ([y-crdt#639](https://github.com/y-crdt/y-crdt/pull/639), aprobado) y se adoptará vía bump.
  **Mientras tanto**: el relay ya se protege con cap de tamaño y límite de memoria; si usas la **ruta
  directa** del FFI (`LoadDoc`/`ApplyUpdate`) con bytes **no confiables**, protégela igual. Ver
  [`GOVERNANCE.md`](../GOVERNANCE.md) §Seguridad.
- **Sólo texto por campo nombrado en v1.** Sin mapas, arrays ni tipos anidados todavía.
- **Los probes nativos de Loro no son content-addressing** (ver arriba). Si tu código los usa como si
  lo fueran, converge a resultados no deterministas.

## Decisiones

El *porqué* de cada elección vive en
[`research.md`](../specs/001-weft-crdt-versioning/research.md), no aquí. Índice rápido:

| | Decisión | | Decisión |
|---|---|---|---|
| **R1** | Shim propio + `[LibraryImport]` | **R10** | Content-addressing SHA-256 |
| **R2** | `SafeHandle` / `HandleLease` | **R11** | NuGet multi-RID (patrón SkiaSharp) |
| **R3** | Ownership de buffers | **R12** | Cross-compile con cargo-zigbuild |
| **R4** | Errores tipados | **R13** | Gate de determinismo vs Yjs |
| **R5** | Índices `int` validados | **R14** | Fuzzing de la frontera |
| **R6** | Actor + Channels | **R15** | Dual-engine como prueba viva |
| **R7** | Protocolo y-protocols | **R16** | Pinning y protocolo de bump |
| **R8** | `IDocumentStore` opaco | **R17** | Authz delegada al consumidor |
| **R9** | Diff LCS por palabras | | |

Los principios que gobiernan todo esto están en la
[constitución](../.specify/memory/constitution.md) (P-I…P-VI); son vinculantes, no aspiracionales.

## Por dónde seguir

- **Consumir la librería**: [`README.md`](../README.md) (quickstart) → [`docs/api/README.md`](api/README.md)
- **Contribuir**: [`CONTRIBUTING.md`](../CONTRIBUTING.md), incluido el protocolo de bump del motor
- **Validar end-to-end**: [`quickstart.md`](../specs/001-weft-crdt-versioning/quickstart.md)
- **Evidencia experimental** que fundó estas decisiones: [`docs/spikes/`](spikes/)
