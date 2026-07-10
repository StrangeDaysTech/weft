# Research — Weft (Phase 0)

**Feature**: [spec.md](./spec.md) · **Plan**: [plan.md](./plan.md) · **Fecha**: 2026-07-10

La investigación de fondo ya ocurrió en tres spikes con código ejecutado y medido
(`docs/spikes/`). Este documento consolida cada decisión técnica en formato
Decision/Rationale/Alternatives y resuelve los puntos que el brief dejó abiertos al diseño.
No quedan `NEEDS CLARIFICATION`.

---

## R1. Enfoque de binding: shim C-ABI propio + `[LibraryImport]` manual

- **Decision**: shim Rust propio (`weft-yrs-ffi`, cdylib) que expone una C-ABI diseñada por
  nosotros; declaraciones P/Invoke `[LibraryImport]` escritas a mano en `Weft.Core`.
- **Rationale**: control total de la superficie en ambos lados; expone exactamente lo que la
  arquitectura necesita; aísla `yrs` (un bump toca solo `lib.rs`); neutraliza el riesgo de
  reachability de tipos rich-text (Spike 01 §9: XML alcanzable porque el shim usa la API Rust
  de `yrs`, no la C-ABI de `yffi`). Superficie medida en spike: 9 funciones ≈ 205 LOC Rust.
- **Alternatives considered**: bindear `yffi` directo (hereda superficie y ownership de un
  tercero; descartado como base — decisión cerrada del brief); csbindgen (genera `[DllImport]`
  clásico con punteros crudos, no genera la capa segura — queda solo como acelerador opcional
  de declaraciones tras un bump); UniFFI (overhead de runtime y patrones ajenos).

## R2. Ciclo de vida de handles: `SafeHandle` + patrón `HandleLease`

- **Decision**: `DocHandle : SafeHandleZeroOrMinusOneIsInvalid` para `*mut Doc`;
  como `[LibraryImport]` no marshala `SafeHandle` (SYSLIB1051), las llamadas usan un helper
  interno `HandleLease` (`DangerousAddRef`/`DangerousRelease` en `try/finally`) que garantiza
  que el handle no se libera durante una llamada nativa en curso.
- **Rationale**: resuelve fuga (finalizer de respaldo), double-free (el runtime garantiza un
  solo `ReleaseHandle`) y use-after-free (ref-count durante la llamada). Fricción SYSLIB1051
  ya identificada y resuelta en Spike 01 §2 y notas.
- **Alternatives considered**: `IntPtr` crudo + disciplina manual (frágil, viola P-I);
  `[DllImport]` clásico que sí marshala SafeHandle (pierde los generadores source-gen y el
  marshalling moderno de spans).

## R3. Contrato de ownership de buffers

- **Decision**: (a) buffers **devueltos** por el shim se asignan como `Box<[u8]>` y se
  liberan SOLO con `weft_buf_free(ptr, len)`; el lado .NET copia una vez a `byte[]` y libera
  inmediatamente (patrón `TakeOwnedBuffer`). (b) Buffers **pasados** al shim
  (`ReadOnlySpan<byte>` pinned por el marshaller) son prestados: el shim no toma posesión.
  (c) `*mut Doc` se libera solo con `weft_doc_free`. Contrato documentado en el header
  `include/weft_ffi.h` y en [contracts/ffi-abi.md](./contracts/ffi-abi.md).
- **Rationale**: verificado con ASan/LSan en los tres spikes (0 fugas, 0 double-free);
  entrada zero-copy, salida con una copia inevitable (la memoria es de Rust). Constitución P-I.
- **Alternatives considered**: `Marshal.FreeHGlobal`/GC gestionando memoria nativa (UB,
  prohibido por P-I); callbacks de allocator compartido (complejidad sin beneficio a esta escala).

## R4. Manejo de errores a través de la frontera

- **Decision**: toda función del shim devuelve status `i32` (`0` = OK, negativos = error:
  `NULL_ARG`, `DECODE`, `APPLY`, `UTF8`, `OUT_OF_BOUNDS`, `PANIC`); cada cuerpo va envuelto en
  `catch_unwind`. En .NET, un helper central traduce códigos a la jerarquía:
  `WeftException` (base) ← `CorruptUpdateException` (decode), `WeftEngineException`
  (apply/panic, con `ErrorCode`), más `ArgumentException`/`ArgumentOutOfRangeException` y
  `ObjectDisposedException` idiomáticas generadas en la capa C# antes de cruzar la frontera.
- **Rationale**: un panic cruzando la frontera C es UB — `catch_unwind` es obligatorio
  (constitución P-I); códigos planos mantienen la ABI trivial y estable. Probado en Spike 01 §6
  (bytes corruptos → `SDT_ERR_DECODE` → excepción .NET).
- **Alternatives considered**: estructuras de error con mensaje por la ABI (complica ownership
  de strings; el código basta y el detalle se enriquece en C#); errno-style thread-local (frágil
  con async/actores).

## R5. Índices en la API pública .NET: `int`

- **Decision**: la API pública usa `int index/length` (idiomático .NET) con validación
  `>= 0`; la conversión a `u32`/`usize` del shim se hace en la capa interna, con chequeo de
  rango explícito antes de cruzar.
- **Rationale**: `uint` no es CLS-friendly y rompe la ergonomía (`text.Length` es `int`);
  el borrador del brief usaba `uint` pero delega explícitamente la forma idiomática final al
  diseño. Documentos de texto > 2 GiB por campo están fuera del dominio.
- **Alternatives considered**: `uint` (fiel al shim pero anti-idiomático); `long` (falsa
  promesa de rango que el motor no honra).

## R6. Concurrencia: actor por documento con `System.Threading.Channels`

- **Decision**: `DocumentBroker` mantiene un registro `docId → DocumentActor`; cada actor es
  un `Channel<WorkItem>` unbounded-single-reader con un bucle consumidor único que es el ÚNICO
  código que toca el `ICrdtDoc` nativo. El consumidor usa `DocumentSession` (API async) que
  encola trabajo y devuelve `Task`/`ValueTask`. Desalojo por inactividad configurable
  (`IdleEviction`, con guardado previo vía hook) y `IAsyncDisposable` en broker y sesión.
- **Rationale**: `yrs` no es `Send+Sync` (decisión cerrada + P-V); Channels es BCL puro (sin
  dependencia externa), single-reader garantiza serialización sin locks por operación, y el
  patrón escala a "cientos de docs activos" (cada actor inactivo no consume hilo: el bucle
  espera en `ReadAsync`).
- **Alternatives considered**: `lock` por documento (bloquea hilos del pool bajo carga y no
  modela cola justa; suficiente en spikes, insuficiente a escala M1); Akka.NET/Orleans
  (dependencia pesada para un building block; Orleans queda como patrón del consumidor si
  quiere distribución); un hilo dedicado por doc (no escala a cientos).

## R7. Protocolo de sincronización: y-protocols (sync v1 + awareness)

- **Decision**: `Weft.Server` implementa el framing estándar de y-protocols sobre WebSocket
  binario: mensajes lib0-varint con tipo `0 = sync` (subtipos `SyncStep1(sv)`,
  `SyncStep2(update)`, `Update(update)`) y `1 = awareness`. Relay-only por defecto: el servidor
  reenvía updates a los pares del mismo doc y persiste el update acumulado sin materializar el
  documento; materializa solo para snapshot/publicación.
- **Rationale**: compatibilidad sin adaptación con `y-websocket`/`y-prosemirror`/Tiptap
  (FR-014; decisión cerrada de editor); relay-only minimiza CPU y evita motor-en-servidor
  (diferido explícito). El encoding lib0 (varint) es pequeño y estable, documentado en
  https://docs.yjs.dev/api/document-updates y en el código de `y-protocols`.
- **Alternatives considered**: protocolo propio (rompe el ecosistema de clientes; sin
  beneficio); GraphQL/SSE (no aptos para tráfico binario bidireccional de baja latencia);
  materializar el doc en servidor siempre (coste innecesario; queda como opción diferida para
  búsqueda/indexado).

## R8. Persistencia del servidor: `IDocumentStore` de blobs opacos

- **Decision**: contrato mínimo `IDocumentStore` (load/append-or-save/delete por `docId`,
  blobs opacos) con estrategia de snapshot-compaction: el servidor persiste updates
  incrementales y consolida a un blob compacto (export con GC) al desalojar/publicar.
  Adaptadores: InMemory (en `Weft.Server`), FileSystem (v1), EF Core y Redis como paquetes
  separados `Weft.Server.Persistence.EFCore` / `.Redis`.
- **Rationale**: FR-017 exige adaptadores intercambiables con blobs opacos; separar paquetes
  evita arrastrar EF/Redis al relay básico; append+compact equilibra durabilidad y tamaño.
- **Alternatives considered**: exigir transaccionalidad rica al store (sobre-especifica;
  los blobs opacos bastan); persistir cada keystroke como fila (ruido; el debounce/append
  acotado lo resuelve).

## R9. Diff de texto: LCS a nivel de palabras en la capa de dominio

- **Decision**: `TextDiff` engine-agnóstico: reconstruir ambas versiones desde sus blobs,
  tokenizar por palabras/espacios y computar LCS → segmentos `Equal/Insert/Delete`.
- **Rationale**: Spike 03 §1 — ~30 LOC, correcto incluso tras merges concurrentes porque
  compara estados materializados, no ops; suficiente para v1 (el brief lo fija). El diff
  estructural rich-text (tree-diff ProseMirror) queda **diferido** y se cuantificará cuando el
  editor lo exija.
- **Alternatives considered**: diff nativo del motor (Loro lo tiene, yrs no — usarlo rompería
  la portabilidad P-IV; queda accesible vía `INativeVersioning` como probe); diff por
  caracteres (ruido visual); Myers general por líneas (los campos son texto corrido, palabras
  funcionan mejor).

## R10. Content-addressing: SHA-256 del export completo, calculado en .NET

- **Decision**: `VersionId = SHA-256(ExportState())` calculado con `SHA256.HashData` en la
  capa de dominio (no en el shim). Identidad solo sobre **estado completo** exportado.
- **Rationale**: Spike 01 §10 — dos docs convergidos producen blobs byte-a-byte idénticos
  (yrs ordena el update v1 determinísticamente por client-id), así que el hash es un id de
  versión citable cross-nodo sin forma canónica adicional. Hashear en .NET mantiene el shim
  mínimo y el hashing engine-agnóstico.
- **Caveat vivo (P-III)**: determinismo observado, no garantizado documentalmente por `yrs` →
  gate de CI permanente; si se hashearan updates incrementales o snapshots parciales,
  revalidar su determinismo antes de usarlos como identidad.
- **Alternatives considered**: hash en el shim (duplica lógica por motor); forma canónica
  propia (innecesaria mientras el export sea determinista; sería el plan B si el gate rompe).

## R11. Empaquetado nativo: NuGet multi-RID patrón SkiaSharp

- **Decision**: los cdylib van en `runtimes/<rid>/native/` dentro del paquete `Weft.Core`
  (y `Weft.Loro` para su shim); resolución vía `NativeLibrary.SetDllImportResolver` desde
  `AppContext.BaseDirectory`/deps del RID activo. RIDs v1: `linux-x64`, `linux-arm64`,
  `win-x64`, `osx-arm64`.
- **Rationale**: layout estándar NuGet probado en Spike 01 §8 (mismo árbol funciona en dev y
  empaquetado); precedentes SkiaSharp/YDotNet. Un solo paquete con los 4 RIDs (~1.1 MB por
  binario stripped) es aceptable en v1; dividir en `Weft.Core.runtime.<rid>` es optimización
  diferida no-breaking.
- **Alternatives considered**: paquetes runtime separados por RID (más piezas de release sin
  necesidad aún); binarios descargados post-install (fricción y riesgo supply-chain).

## R12. Cross-compilación: `cargo-zigbuild` primero, `cross` como fallback

- **Decision**: CI compila los 4 RIDs con `cargo-zigbuild` (glibc target fijado para
  linux) y usa `cross` (contenedores) como fallback si un target lo requiere; macOS/Windows
  compilan en runners nativos de GitHub Actions cuando sea más simple (osx-arm64, win-x64).
- **Rationale**: zigbuild da cross-linking hermético y control de versión mínima de glibc;
  runners nativos eliminan fricción de firma/toolchain en mac/win. El brief admite ambas
  herramientas.
- **Alternatives considered**: solo runners nativos (sin linux-arm64 barato); toolchains GNU
  manuales (frágil).

## R13. Gate de determinismo: matriz cross-RID + cross-implementación vs Yjs JS

- **Decision**: `Weft.Determinism.Tests` genera corpus de documentos (secuencias de
  ediciones/merges deterministas con client-ids fijos), exporta y hashea: (a) el mismo corpus
  debe producir hashes idénticos en todos los RIDs de la matriz de CI; (b) un job Node aplica
  el mismo corpus con Yjs JS y compara los blobs/hashes (cross-implementación, "idealmente"
  del brief → lo adoptamos como job no-bloqueante primero, promovible a gate al estabilizarse).
- **Rationale**: P-III exige detectar regresiones de determinismo en cada cambio y bump;
  cross-implementación distingue "determinista por accidente de esta versión de yrs" de
  "determinista por formato".
- **Alternatives considered**: solo same-binary determinism (no detecta divergencia entre
  implementaciones); snapshot de hashes dorados en repo (frágil ante cambios legítimos de
  corpus; se usa solo como testigo secundario).

## R14. Fuzzing: `cargo-fuzz` en la frontera + property-based en convergencia

- **Decision**: (a) `cargo-fuzz` targets sobre el shim: `weft_doc_load`/`apply_update` con
  bytes arbitrarios (nunca panic ni UB, solo códigos de error); (b) CsCheck en .NET generando
  secuencias aleatorias de operaciones concurrentes en N réplicas con intercambio de updates →
  propiedad: convergencia byte-idéntica y hash igual.
- **Rationale**: FR-023; los dos riesgos distintos (memoria/UB en la frontera, semántica de
  convergencia) requieren herramientas distintas. ASan corre también bajo el fuzzing Rust.
- **Alternatives considered**: AFL externo (cargo-fuzz/libFuzzer integra mejor con el crate);
  fuzzing solo en .NET (no ejercita el parsing nativo directamente).

## R15. Estrategia dual-engine en tests

- **Decision**: `Weft.Versioning.Tests` define la suite como clase base abstracta
  parametrizada por `ICrdtEngine` (theory data: `YrsEngine`, `LoroEngine`); CI la corre
  completa contra ambos motores en cada cambio. `Weft.Loro` implementa además
  `INativeVersioning` (probes de diff nativo, fork/merge y shallow-snapshot).
- **Rationale**: constitución P-IV ("una abstracción con una sola implementación ejercitada
  se considera rota"); Spike 03 demostró la misma capa (~58 LOC) sobre ambos motores.
- **Alternatives considered**: compilar Loro sin correr tests (abstracción zombi); duplicar
  suites (divergen).

## R16. Version-pinning y protocolo de bump del motor

- **Decision**: `yrs = "=0.27.2"` y `loro = "=1.13.6"` exactos; `Cargo.lock` versionado.
  Protocolo de bump (documentado en CONTRIBUTING): (1) actualizar pin en rama dedicada,
  (2) ajustar `lib.rs` a cambios de API (csbindgen opcional para regenerar declaraciones),
  (3) correr gates completos (sanitizers, determinismo, convergencia, dual-engine),
  (4) merge solo en verde.
- **Rationale**: los nombres/firmas de `yrs` cambian entre minors (Spike 01 §7); el shim
  aísla el bump — la C-ABI propia y el C# no cambian. Constitución P-IV.
- **Alternatives considered**: rangos de versión (rompen reproducibilidad del gate de
  determinismo); vendorear yrs (coste de mantenimiento sin control real).

## R17. Identidad y autorización: hook del consumidor, nunca de Weft

- **Decision**: `IWeftAuthorizer` único punto de decisión: recibe el `HttpContext` del
  handshake WebSocket + `docId` y devuelve `Deny | ReadOnly | ReadWrite`. Weft no parsea
  tokens ni conoce usuarios; el consumidor registra su implementación en DI (JWT, cookies, lo
  que sea). Conexiones `ReadOnly` reciben updates pero sus updates entrantes se descartan con
  cierre de protocolo.
- **Rationale**: FR-019 y frontera del componente (el consumidor posee identidad); un enum de
  acceso mínimo cubre los casos sin acoplar políticas.
- **Alternatives considered**: middleware de auth propio (fuera de frontera); solo
  allow/deny sin ReadOnly (los revisores/lectores son caso real del LMS consumidor).
