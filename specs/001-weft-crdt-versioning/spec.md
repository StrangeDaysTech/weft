# Feature Specification: Weft — Colaboración CRDT en tiempo real y versionado content-addressed para .NET

**Feature Branch**: `001-weft-crdt-versioning` _(trabajo en `main`; sin rama dedicada)_

**Created**: 2026-07-10

**Status**: Draft

**Input**: User description: "weft-design-brief.md — Brief de diseño de Weft (Strange Days Tech, v1.0): librería .NET Apache-2.0 de colaboración CRDT en tiempo real + versionado content-addressed de documentos sobre el core `yrs`, con servidor relay de sincronización y motor reemplazable (dual-path Loro)."

> **Nota de alcance del documento.** Weft es una **librería para desarrolladores**: su API pública y sus contratos SON el producto. Por eso esta spec nombra conceptos de contrato (abstracción de motor, content-addressing, protocolo de sync) que en una app de usuario final serían "detalle de implementación". Lo que sí queda fuera de los requisitos son las decisiones de implementación interna ya cerradas en el brief (motor `yrs`, shim C-ABI en Rust, P/Invoke) — se citan como contexto firme en **Assumptions** y no se re-litigan aquí.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Editar y versionar documentos desde .NET (Priority: P1)

Una desarrolladora .NET (p. ej. del LMS consumidor) crea un documento colaborativo, edita su texto por campos, y **publica versiones inmutables** identificadas por el hash de su contenido. Puede comparar dos versiones publicadas (diff legible), abrir una rama desde cualquier versión, fusionarla de vuelta con convergencia automática, y compactar el almacenamiento sin perder ninguna versión publicada.

**Why this priority**: Es el núcleo de valor de Weft (hito M0): sin documento editable ni versionado citable no existe el componente. Todo lo demás (concurrencia, sync, empaquetado) se construye encima.

**Independent Test**: Con solo la librería en un proyecto de consola: crear doc → editar → publicar v1 → editar → publicar v2 → `Diff(v1, v2)` muestra los cambios → `Branch(v1)` + ediciones divergentes + `Merge` converge → `Compact` conserva v1 y v2 recuperables por hash. Entrega valor sin servidor ni red.

**Acceptance Scenarios**:

1. **Given** un documento nuevo con texto editado, **When** la desarrolladora publica una versión, **Then** obtiene un identificador derivado del contenido (hash) y la versión puede recargarse después, byte a byte equivalente, usando solo ese identificador.
2. **Given** dos réplicas del mismo documento con el mismo contenido, **When** ambas publican, **Then** ambas obtienen exactamente el mismo identificador de versión (determinismo del content-addressing).
3. **Given** dos versiones publicadas vA y vB, **When** se solicita el diff, **Then** se obtiene una comparación de texto legible a nivel de palabras que refleja inserciones y eliminaciones.
4. **Given** una rama creada desde una versión publicada con ediciones propias, y ediciones paralelas en la línea principal, **When** se fusionan, **Then** el resultado converge de forma automática y determinista, sin pasos de resolución manual.
5. **Given** un historial con versiones publicadas y trabajo intermedio no publicado, **When** se compacta el almacenamiento, **Then** toda versión publicada sigue siendo recuperable por su hash y el espacio del historial no publicado se libera.

---

### User Story 2 - Operar muchos documentos concurrentes sin corrupción (Priority: P2)

Una aplicación consumidora abre y edita **muchos documentos a la vez** desde múltiples hilos/tareas. Weft garantiza que el acceso a cada documento se serializa internamente, gestiona el ciclo de vida de los documentos activos (registro, reutilización, desalojo por inactividad) y libera los recursos nativos de forma determinista.

**Why this priority**: (Hito M1) El motor subyacente no tolera acceso concurrente al mismo documento; sin esta capa, cualquier uso realista en servidor corrompe datos o filtra memoria. Es prerequisito del servidor de sync.

**Independent Test**: Prueba de carga local: N cientos de documentos, M tareas concurrentes editando cada uno al azar durante un período sostenido → todos los documentos terminan en estado consistente, la memoria del proceso se mantiene acotada y ningún recurso queda sin liberar.

**Acceptance Scenarios**:

1. **Given** un mismo documento accedido desde múltiples hilos, **When** llegan operaciones concurrentes, **Then** todas se aplican serializadas en orden y el estado final es consistente (nunca acceso simultáneo al documento nativo).
2. **Given** cientos de documentos activos, **When** algunos dejan de usarse, **Then** son desalojados por inactividad liberando sus recursos, y pueden reabrirse después desde su estado persistido.
3. **Given** un documento ya liberado (disposed), **When** el código consumidor intenta usarlo, **Then** recibe un error predecible de la plataforma .NET — nunca un crash del proceso.

---

### User Story 3 - Colaboración en tiempo real entre clientes de editor (Priority: P3)

Dos o más personas editan el mismo documento desde clientes de editor compatibles con el protocolo Yjs (p. ej. Tiptap + y-prosemirror), conectados a un **servidor relay** provisto por Weft y alojado por el consumidor. Ven los cambios de las demás en tiempo real, ven quién está presente (awareness), y al reconectarse tras una caída solo reciben lo que les falta (sync incremental). El documento persiste en el almacenamiento que el consumidor elija, y **publicar** desde ese entorno produce una versión citable por hash. El consumidor inyecta su propia autenticación/autorización.

**Why this priority**: (Hito M2) Es el escenario colaborativo completo — el motivo de usar CRDTs — pero requiere P1 y P2 como base.

**Independent Test**: Levantar el servidor relay con un adaptador de persistencia; conectar dos clientes de editor de referencia; escribir desde ambos → ambos convergen en vivo; desconectar uno, seguir editando, reconectar → recibe solo el delta; publicar → existe una versión recuperable por hash; un cliente sin credenciales válidas es rechazado.

**Acceptance Scenarios**:

1. **Given** dos clientes conectados al mismo documento vía el servidor relay, **When** uno escribe, **Then** el otro ve el cambio en tiempo real y ambos convergen al mismo contenido.
2. **Given** varios clientes en el mismo documento, **When** entran, salen o mueven el cursor, **Then** todos ven la presencia actualizada de los demás (awareness efímera, no persistida).
3. **Given** un cliente que estuvo desconectado mientras el documento cambiaba, **When** se reconecta anunciando qué conoce (state vector), **Then** recibe únicamente el delta faltante, no el estado completo.
4. **Given** un documento con actividad, **When** el servidor lo persiste, **Then** el almacenamiento del consumidor guarda blobs opacos intercambiables entre adaptadores (en memoria, archivos, relacional, caché).
5. **Given** una publicación solicitada en el servidor, **When** se ejecuta, **Then** se produce un snapshot content-addressed idéntico al que produciría la capa de versionado local (mismo contenido → mismo hash).
6. **Given** un cliente sin autorización según la política inyectada por el consumidor, **When** intenta conectarse o unirse a un documento, **Then** es rechazado antes de recibir o enviar contenido.

---

### User Story 4 - Instalar y adoptar el componente multiplataforma (Priority: P4)

Un equipo externo agrega Weft a su solución .NET mediante un paquete NuGet e inmediatamente funciona en Linux, Windows y macOS (x64/arm64) sin pasos manuales para los binarios nativos. El repositorio público ofrece licencia Apache-2.0, documentación de arranque y reglas de gobernanza, y cada release pasó las verificaciones de calidad (memoria limpia, convergencia, determinismo).

**Why this priority**: (Hito M3) Convierte el componente en un building block adoptable OSS; sin distribución confiable el valor queda encerrado en el repo.

**Independent Test**: En una máquina limpia de cada plataforma soportada: instalar el paquete, compilar un "hello Weft" que crea/edita/publica un documento → funciona sin configuración nativa manual.

**Acceptance Scenarios**:

1. **Given** un proyecto .NET nuevo en cualquier plataforma soportada, **When** instala el paquete y ejecuta el ejemplo mínimo, **Then** el binario nativo correcto se resuelve automáticamente y el ejemplo corre.
2. **Given** un cambio propuesto al componente, **When** corre el pipeline de CI, **Then** el cambio solo se acepta si pasan: build y tests en todas las plataformas, verificación de memoria (0 fugas / 0 liberaciones dobles), fuzzing de la frontera nativa y de convergencia, y el test de determinismo del encoding.
3. **Given** el repositorio público, **When** una persona externa lo visita, **Then** encuentra licencia Apache-2.0, README con quickstart y pautas de contribución/gobernanza.

---

### User Story 5 - Sustituir el motor CRDT sin reescribir el versionado (Priority: P5)

Una mantenedora de Weft (o un consumidor avanzado) implementa la abstracción de motor con un motor CRDT alternativo (Loro). Toda la capa de versionado corre sin cambios sobre el nuevo motor, y si el motor ofrece capacidades nativas de versionado (diff semántico, fork/merge nativo, snapshot superficial), quedan expuestas como capacidad opcional sin volverse dependencia del núcleo.

**Why this priority**: Protege la inversión: la abstracción validada en los spikes solo sigue siendo real si un segundo motor la ejercita continuamente. Es barata de mantener (adaptador compilable en CI) y habilita el gatillo de reevaluación de Loro.

**Independent Test**: Compilar el adaptador Loro y correr la suite de la capa de versionado (publicar/diff/branch/merge/compact) contra ambos motores → misma suite verde en los dos.

**Acceptance Scenarios**:

1. **Given** la suite de pruebas de la capa de versionado, **When** se ejecuta contra el motor principal y contra el adaptador alternativo, **Then** ambos pasan la misma suite sin cambios en la capa de versionado.
2. **Given** un motor con capacidades nativas de versionado, **When** el consumidor consulta la capacidad opcional, **Then** puede usarla; y **Given** un motor sin ella, **Then** la consulta indica su ausencia sin romper ningún flujo del núcleo.

---

### Edge Cases

- **Ediciones concurrentes en la misma región de texto**: todas las réplicas convergen al mismo resultado determinista (semántica CRDT); nunca se pide resolución manual ni se pierde una edición aceptada.
- **Fallo interno del motor nativo (panic)**: se traduce a una excepción .NET manejable; el fallo nunca cruza la frontera nativa como crash del proceso ni deja memoria sin liberar.
- **Blob corrupto o hash que no corresponde al contenido**: la carga se rechaza con un error claro que identifica la versión afectada; nunca se materializa un documento a partir de datos que no verifican.
- **Uso de un documento después de liberarlo**: error predecible e idiomático de .NET; jamás un fallo del proceso.
- **Desconexión a mitad de una sesión de sync**: al reconectar, el intercambio de state vectors garantiza que solo viaja el delta faltante y que no se duplican ni pierden cambios.
- **Compactación con ramas y versiones publicadas**: ninguna versión publicada se vuelve irrecuperable; solo el historial intermedio no publicado es recolectable.
- **Merge de ramas largamente divergentes**: converge automáticamente; el resultado es idéntico sin importar el orden de fusión (conmutatividad observable).
- **Cliente que envía datos malformados al servidor**: la conexión se rechaza o cierra de forma controlada sin afectar a los demás clientes del documento.
- **Regresión de determinismo en el encoding** (p. ej. tras actualizar el motor): el gate de determinismo en CI la detecta antes de cualquier release; nunca se publica un release que rompa la estabilidad de los hashes.

## Requirements *(mandatory)*

### Functional Requirements

**Núcleo — documento y motor (`Weft.Core`)**

- **FR-001**: El sistema MUST exponer una abstracción de motor CRDT que permita: crear un documento vacío, reconstruir un documento desde un blob exportado, y consultar si el motor ofrece capacidades nativas de versionado (capacidad opcional, ausente sin error).
- **FR-002**: Los documentos MUST soportar operaciones de texto por campo nombrado: insertar texto en una posición, eliminar un rango y leer el contenido completo del campo.
- **FR-003**: Los documentos MUST exportar su estado como blob autocontenido y **byte-determinista**: el mismo contenido lógico produce exactamente los mismos bytes, en cualquier réplica y plataforma (base del content-addressing).
- **FR-004**: Los documentos MUST importar updates o estados de otras réplicas, fusionándolos con convergencia (dos réplicas que intercambian sus cambios terminan en estado idéntico).
- **FR-005**: Los documentos MUST producir un resumen de lo que conocen (state vector) y un delta con solo los cambios posteriores a un state vector dado (base del sync incremental).
- **FR-006**: El ciclo de vida de los recursos nativos MUST ser determinista: liberación explícita e idiomática en .NET, seguro ante liberación doble o uso tras liberar; los buffers provenientes de la capa nativa se devuelven siempre a esa capa (nunca los libera el recolector de .NET).
- **FR-007**: Todo error o fallo interno del motor nativo MUST traducirse a excepciones .NET idiomáticas y tipificadas; ningún fallo nativo puede tumbar el proceso ni filtrar memoria.

**Versionado (`Weft.Versioning`)**

- **FR-008**: El sistema MUST permitir publicar una versión inmutable de un documento, identificada por el hash SHA-256 de su export determinista; el mismo contenido publicado produce el mismo identificador siempre.
- **FR-009**: El sistema MUST almacenar y recuperar blobs de versión por su hash (almacén content-addressed), con deduplicación natural por contenido.
- **FR-010**: El sistema MUST producir un diff legible entre dos versiones publicadas de un campo de texto, a nivel de palabras (LCS). El diff estructural de rich-text (árbol del modelo de documento del editor) queda explícitamente **diferido** (ver Assumptions).
- **FR-011**: El sistema MUST permitir crear una rama a partir de cualquier versión publicada y fusionar ramas con convergencia automática, publicando el resultado como nueva versión.
- **FR-012**: El sistema MUST ofrecer compactación que conserva todos los blobs de versiones publicadas y libera el historial intermedio no publicado; la recolección interna del motor permanece siempre activa.
- **FR-013**: La capa de versionado MUST implementarse exclusivamente contra la abstracción de motor (FR-001–FR-005), sin ninguna dependencia de un motor concreto (engine-agnóstica y verificable con más de un motor).

**Servidor de sincronización (`Weft.Server`)**

- **FR-014**: El sistema MUST proveer un servidor relay de sincronización sobre WebSocket que hable el protocolo de sync estándar del ecosistema Yjs, de modo que clientes de editor existentes se conecten sin adaptación; por defecto el servidor solo retransmite (relay-only), sin materializar el documento.
- **FR-015**: El servidor MUST difundir awareness/presencia efímera (quién está en el documento, cursores, estado) entre los clientes de un mismo documento, sin persistirla.
- **FR-016**: El servidor MUST soportar sync incremental en (re)conexión: intercambio de state vectors y transferencia solo del delta faltante.
- **FR-017**: El servidor MUST persistir el estado de los documentos mediante adaptadores de persistencia intercambiables que tratan los blobs como opacos; cambiar de adaptador no cambia el comportamiento observable (verificado por una suite de contrato compartida que todo adaptador debe pasar). v1 incluye: en memoria, sistema de archivos, relacional (EF Core) y caché (Redis). El adaptador de almacenamiento de objetos (S3/Azure Blob) queda **diferido**; la suite de contrato hace su incorporación trivial.
- **FR-018**: El servidor MUST poder producir, al publicar, un snapshot content-addressed equivalente al de la capa de versionado local (mismo contenido → mismo hash).
- **FR-019**: El servidor MUST exponer un punto de extensión de autenticación/autorización donde el consumidor inyecta su identidad y su política (p. ej. validación de tokens propia); Weft MUST NOT implementar gestión de identidad propia, y sin autorización válida un cliente no recibe ni envía contenido.

**Concurrencia y ciclo de vida a escala**

- **FR-020**: El sistema MUST serializar todo acceso a un mismo documento (un solo flujo de operaciones por documento, patrón actor/canal) y gestionar el conjunto de documentos activos: registro/lookup, reutilización y desalojo por inactividad con liberación de recursos; el acceso concurrente directo al documento nativo MUST ser imposible desde la API pública.

**Motor reemplazable (dual-path, `Weft.Loro`)**

- **FR-021**: El sistema MUST incluir un adaptador alternativo (Loro) que implementa la abstracción de motor y su capacidad nativa opcional de versionado; el adaptador MUST mantenerse compilable y ejercitado en CI como prueba continua de portabilidad de la abstracción.

**Distribución y calidad (empaquetado, CI)**

- **FR-022**: El componente MUST distribuirse como paquete NuGet que incluye los binarios nativos para `linux-x64`, `linux-arm64`, `win-x64` y `osx-arm64`, con resolución automática por plataforma (sin pasos manuales del consumidor).
- **FR-023**: El pipeline de CI MUST actuar como gate de calidad en cada cambio: build y tests multiplataforma; verificación automática de memoria (0 fugas, 0 liberaciones dobles); fuzzing de la frontera nativa y de la convergencia CRDT; y test de determinismo del encoding (idealmente contrastado con otra implementación del formato).
- **FR-024**: El repositorio MUST publicarse bajo licencia Apache-2.0 con README (quickstart), documentación de la API pública y pautas de contribución/gobernanza.

### Key Entities

- **Documento colaborativo**: unidad de colaboración; contiene campos de texto nombrados; tiene estado vivo (editable, sincronizable) y recursos nativos con ciclo de vida explícito.
- **Versión publicada**: instantánea inmutable de un documento, identificada por el hash de su contenido exportado; citable, recuperable y comparable; forma el historial duradero.
- **Blob de versión**: representación opaca y autocontenida del estado exportado; lo que se almacena, persiste y transporta; su hash es la identidad de la versión.
- **Rama**: línea de trabajo derivada de una versión publicada; editable de forma independiente y fusionable de vuelta con convergencia.
- **State vector / Delta**: resumen compacto de "qué conoce" una réplica y el paquete de cambios que le falta; base del sync incremental.
- **Sesión de colaboración**: conexión de un cliente a un documento vía el servidor relay; porta identidad/autorización del consumidor y presencia efímera (awareness).
- **Motor CRDT (abstracción)**: contrato que provee documentos y sus primitivas (crear, cargar, exportar, importar, state vector/delta); punto de sustitución entre motores; puede declarar capacidades nativas opcionales de versionado.
- **Almacén de blobs**: repositorio content-addressed (hash → blob) detrás del versionado y de los adaptadores de persistencia.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Dos réplicas que aplican ediciones concurrentes arbitrarias y luego intercambian cambios convergen a un estado idéntico en el 100 % de las corridas de las pruebas de convergencia/fuzzing de CI.
- **SC-002**: Publicar el mismo contenido produce el mismo identificador de versión en todas las plataformas soportadas, en el 100 % de las corridas del gate de determinismo.
- **SC-003**: La suite completa de pruebas corre con 0 fugas de memoria y 0 liberaciones dobles bajo la verificación automática de memoria de CI, en cada cambio aceptado.
- **SC-004**: En el escenario de referencia de reconexión, el sync incremental transfiere ≥ 90 % menos datos que reenviar el estado completo (referencia medida en los spikes: 523 B → 29 B).
- **SC-005**: Dos o más clientes de editor conectados al servidor relay ven los cambios de los demás en menos de 1 segundo en red local, y terminan con contenido idéntico al finalizar la sesión.
- **SC-006**: Una prueba de carga sostenida con cientos de documentos activos y acceso concurrente termina sin corrupción de ningún documento y con uso de memoria acotado (sin crecimiento monótono).
- **SC-007**: En una máquina limpia de cada plataforma soportada, instalar el paquete y ejecutar el ejemplo mínimo funciona al primer intento, sin configuración manual de binarios nativos.
- **SC-008**: La misma suite de la capa de versionado pasa en verde sobre los dos motores (principal y alternativo) en CI, en cada cambio aceptado.
- **SC-009**: Ante fallos inyectados del motor nativo, el 100 % de los casos de prueba de fallo resultan en una excepción manejable con el proceso consumidor intacto.
- **SC-010**: Un cliente sin autorización válida nunca recibe ni envía contenido de documento (0 casos de fuga en las pruebas de la política de acceso).

## Assumptions

- **Decisiones cerradas del brief se toman como firmes y no se re-litigan**: motor CRDT `yrs` (adoptado, no reimplementado); binding mediante shim C-ABI propio en Rust con P/Invoke; versionado content-addressed (SHA-256 del export determinista) en capa de dominio engine-agnóstica; serialización por documento (patrón actor); licencia Apache-2.0; cliente de editor recomendado Tiptap + y-prosemirror; repo propio con dirección de dependencia consumidor→Weft; dual-path Loro como capacidad opcional. Ninguna contradicción técnica dura fue detectada al elaborar esta spec.
- **Determinismo del export**: está observado experimentalmente (spikes), no confirmado como garantía documentada del motor; el gate de determinismo en CI (FR-023) existe precisamente para cubrir ese riesgo de forma continua (salvedad viva del brief).
- **Diferidos explícitos (fuera de alcance v1)**: diff estructural de rich-text (tree-diff sobre el modelo del editor — se cuantificará cuando el editor lo exija); blame por rango de texto; motor-en-servidor opcional para búsqueda/indexado dentro del documento; adaptador de persistencia sobre almacenamiento de objetos (S3/Azure Blob).
- **v1 se centra en texto colaborativo por campos**; otros tipos compartidos (mapas, listas, XML del editor) se incorporarán cuando el cliente de editor los exija, ampliando el contrato del motor sin romperlo.
- **El consumidor aporta**: identidad y política de autorización (FR-019), el frontend de editor, y su modelo de contenido de dominio; Weft es un building block, no una aplicación.
- **Consumo local durante el desarrollo temprano** (referencia de proyecto o feed local) hasta el primer release empaquetado (M3).
- **Evidencia previa**: los tres spikes del proyecto consumidor (fundamento del binding, comparación de motores, plomería de versionado) se copiarán a `docs/` del repo como referencia de diseño.
