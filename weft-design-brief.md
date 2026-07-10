# Weft — Brief de diseño (handoff para la fase de diseño / Spec Kit `/specify` + `/plan`)

_Strange Days Tech · v1.0 · para arrancar el diseño del componente en Claude Code (Fable), previo a la implementación (StrayMark + Opus)._

> **Para quien diseña (Fable):** este documento es autocontenido. Contiene el **qué** y el **porqué** ya decididos (con evidencia experimental), y delimita el **espacio de diseño** que te toca resolver. Tu salida alimenta el `/specify` y `/plan` de GitHub Spec Kit. **No re-abras** las decisiones marcadas ✅ CERRADO salvo que encuentres una contradicción técnica dura (en cuyo caso, señálala explícitamente). Sí te toca diseñar la API pública, el modelo de módulos, los contratos y los detalles de las piezas abiertas.

---

## 0. Qué es Weft (en una frase)

Una librería **.NET, Apache-2.0**, que provee **colaboración CRDT en tiempo real + versionado content-addressed de documentos** sobre el core Rust `yrs`, mediante un **binding propio** y una **capa de versionado engine-agnóstica** — reutilizable como building block (no una app), diseñada a nuestros patrones.

## 1. Decisiones ya cerradas (contexto firme, no re-litigar)

| # | Decisión | Valor | Por qué (evidencia) |
|---|---|---|---|
| Core | Motor CRDT | **`yrs`** (adoptar, no reimplementar) | Continuidad: el formato Yjs tiene **múltiples implementaciones independientes** (yrs, y-octo, ygo, Yjs JS) → fork = elegir entre implementaciones. Madurez de editor. Fork-safety. |
| Binding | Enfoque | **Shim C-ABI Rust propio + P/Invoke `[LibraryImport]`** | Control de la superficie a nuestros patrones; aísla `yrs` (un bump toca solo el shim). csbindgen solo como acelerador de declaraciones; `yffi`/UniFFI directos descartados como base. |
| Versionado | Modelo | **Content-addressed** (`SHA-256` del export byte-determinista); diff/branch/merge/compact **en capa de dominio, engine-agnóstica** | Spike 03: la MISMA capa (~58 LOC) corrió idéntica sobre yrs y Loro con **6 primitivas del núcleo**. La tensión GC de Yjs **se evita** con blobs por versión + GC activo (nunca `skip_gc`). |
| Concurrencia | yrs no es thread-safe | **Serializar por documento** (actor/canal) | `yrs` no es `Send+Sync` (Loro sí). |
| Licencia | Del componente | **Apache-2.0** | Permisiva (adopción) + concesión de patentes. El consumidor puede ser AGPL. |
| Editor | Cliente recomendado | **Tiptap + y-prosemirror** → conecta a nuestro **servidor relay .NET** | Ecosistema maduro; schema-based (bloques tipados/gobernados). El framework JS del consumidor queda aparte. |
| Repo | Estrategia | **Repo propio** (`weft`), Spec-driven; consumo local durante desarrollo temprano | Frontera limpia; dirección de dependencia consumidor→Weft, nunca al revés. |
| Dual-path | Loro | **Capacidad opcional `INativeVersioning`** | Motor reemplazable; gatillo de reevaluación de Loro documentado, a bajo coste. |

## 2. `ICrdtEngine` — el contrato validado (punto de partida del diseño)

Validado en Spike 03 (la capa de versionado corrió idéntica sobre yrs y Loro usando solo estas primitivas). **Es un borrador: tu trabajo incluye pulirlo, tipar bien y decidir su forma idiomática final.**

```csharp
// Núcleo (obligatorio para cualquier motor)
interface ICrdtEngine {
    string Name { get; }
    ICrdtDoc NewDoc();                            // documento vacío
    ICrdtDoc LoadDoc(ReadOnlySpan<byte> blob);    // reconstruir desde blob exportado
    INativeVersioning? NativeVersioning { get; }  // capacidad opcional (null si no la hay)
}

interface ICrdtDoc : IDisposable {
    void   InsertText(string field, uint index, string text);
    void   DeleteText(string field, uint index, uint len);
    string ReadText(string field);
    byte[] ExportState();                          // blob determinista -> content-addressing (SHA-256)
    void   Import(ReadOnlySpan<byte> update);      // merge de otra versión/rama
}

// Extensiones útiles observadas (sync incremental / diff eficiente)
byte[] StateVector();                              // resumen "qué conozco"
byte[] ExportUpdateSince(ReadOnlySpan<byte> sv);   // delta desde una versión (medido: 523 B -> 29 B)

// Capacidad opcional (Loro sí; yrs no la necesita)
interface INativeVersioning {
    string NativeDiffProbe(ICrdtDoc doc, string field);        // Loro: DiffCalculator (diff semántico)
    string NativeBranchMergeProbe(ICrdtDoc doc, string field); // Loro: fork_at + merge
    byte[] ShallowSnapshot(ICrdtDoc doc);                      // Loro: snapshot con GC de historia
}
```

Sobre estas primitivas se construyen (en capa de dominio, portable): `PublishVersion` (→ `SHA-256(ExportState)`), `Diff(vA,vB)` (reconstruir ambos + text-diff), `Branch(v)`/`Merge`, `Compact` (conservar blobs publicados, GC activo).

## 3. Espacio de diseño que te toca resolver (el "qué diseñar")

Diseña, y produce spec/plan para:

1. **API pública de `Weft.Core`** — `ICrdtEngine`/`ICrdtDoc` idiomáticos; ciclo de vida (`SafeHandle`/`IAsyncDisposable`); manejo de errores (códigos `i32` del shim → excepciones .NET; `catch_unwind` en Rust para que un panic no cruce la frontera C); contrato de ownership de buffers (los que devuelve el shim se liberan con la FFI, nunca el GC).
2. **`Weft.Versioning`** — modelo de "documento con versiones publicadas content-addressed"; `PublishVersion`/`Diff`/`Branch`/`Merge`/`Compact`; almacenamiento de blobs por hash. Diseña el **diff de texto** (LCS de palabras basta para v1); marca como **diferido** el **diff estructural rich-text** (tree-diff sobre el modelo de documento de ProseMirror — cuantificar cuando el editor lo exija).
3. **`Weft.Server`** — servidor **relay** WebSocket (ASP.NET Core) que implementa el **protocolo de sync de Yjs** (relay-only por defecto); **awareness/presencia**; **sync incremental** (state-vector + delta); **adaptadores de persistencia** (EF Core / Redis / Blob) que guardan blobs opacos; **snapshot content-addressed al publicar**; y un **hook de auth/authz** (el consumidor inyecta su Identity/JWT y su política — no lo implementa Weft).
4. **`Weft.Loro`** — adaptador opcional que implementa `ICrdtEngine` + `INativeVersioning` sobre Loro (dual-path). Mantenerlo compilable como prueba de portabilidad.
5. **Empaquetado y CI** — cross-compilación del `cdylib` por **RID** (`linux-x64/arm64`, `win-x64`, `osx-arm64`) con `cross`/`cargo-zigbuild`; **NuGet nativo** (patrón SkiaSharp/YDotNet); CI con **gates**: build, tests, ASan/LSan, fuzzing en la frontera FFI y en la convergencia CRDT, y **test de determinismo** del encoding (idealmente cross-implementación contra Yjs JS).
6. **Modelo de concurrencia** — actor/canal ligero por documento (registro, pooling, desalojo por inactividad); nunca acceso concurrente al mismo doc `yrs`.

## 4. Estándares de calidad (no negociables — heredados de los spikes)

- FFI: shim propio; `SafeHandle`; contrato de ownership explícito; `catch_unwind`; errores → excepciones idiomáticas.
- Memoria: **ASan + LeakSanitizer en CI** (los spikes cerraron con 0 fugas / 0 double-free).
- Version-pinning: `yrs` fijado; el shim lo aísla; re-generar/re-testear en cada bump.
- Determinismo: el content-addressing depende del export byte-determinista → **test de determinismo como gate** (salvedad viva: confirmar si es garantía documentada de `yrs`, no solo observada).
- Portabilidad: binarios nativos por RID probados (Linux/Win/Mac, x64/arm64).
- Abstracción viva: todo el versionado corre contra `ICrdtEngine`; el adaptador Loro se mantiene compilable.

## 5. Hitos (para el `/plan` y las tandas de `/tasks`)

- **M0 · Componente base a grado producción:** shim `yrs` estable + `Weft.Core` (`ICrdtEngine`) + `Weft.Versioning` (publish/diff/branch/merge/compact). _Salida:_ API estable mínima, tests verdes, memoria limpia en CI.
- **M1 · Concurrencia y ciclo de vida a escala:** actor/canal por doc; gestión de muchos docs. _Salida:_ prueba de carga sin corrupción/fugas.
- **M2 · Sync incremental + servidor relay:** WebSocket, protocolo Yjs, awareness, persistencia, snapshot al publicar, hook de auth. _Salida:_ dos+ clientes Tiptap/Yjs sincronizando vía el servidor .NET; publicación produce versión citable por hash.
- **M3 · Empaquetado, CI y primer release OSS:** multi-RID, NuGet nativo, gates de CI, release Apache-2.0 (README, docs, gobernanza). _Salida:_ paquete instalable multiplataforma + repo público.

**Diferido / watch:** diff estructural rich-text; blame por rango (yrs no expone `PermanentUserData` → capa de dominio); motor-en-servidor opcional para búsqueda/indexado dentro del doc.

## 6. Referencias (para el diseño)

- **Evidencia previa (copiar a `docs/` del repo):** los tres spikes y sus salidas (fundamento del binding; comparación de motores; plomería de versionado) y el borrador de `ICrdtEngine` — provienen del proyecto LMS que consumirá Weft.
- **yrs / Yjs:** https://github.com/y-crdt/y-crdt · header C FFI de referencia (yffi): https://github.com/y-crdt/y-crdt/blob/main/tests-ffi/include/libyrs.h · Document Updates: https://docs.yjs.dev/api/document-updates
- **Editor:** Tiptap https://tiptap.dev · y-prosemirror https://github.com/yjs/y-prosemirror
- **Empaquetado nativo .NET:** https://learn.microsoft.com/en-us/nuget/create-packages/native-files-in-net-packages · precedente SkiaSharp https://github.com/mono/skiasharp · YDotNet (referencia de P/Invoke sobre yffi) https://github.com/y-crdt/ydotnet
- **Dual-path (Loro):** https://github.com/loro-dev/loro · loro-ffi https://github.com/loro-dev/loro-ffi

---

## Cómo usar este brief con GitHub Spec Kit

1. `git init` en `/home/montfort/StrangeDaysTech/weft`; soltar estos artefactos semilla (`LICENSE`, `README.md`, `.gitignore`, este brief en `docs/weft-design-brief.md`) + copiar los `docs/` de los spikes.
2. Inicializar Spec Kit para Claude Code (`specify init` / `--ai claude`).
3. **Fable** ejecuta `/specify` (tomando este brief + los spikes como fuente) y `/plan` (arquitectura + módulos + contratos).
4. `/tasks` desglosa en tandas; **StrayMark + Opus** ejecutan `/implement` por hito (M0 primero).
5. Los artefactos de cada fase (spec/plan/tasks Markdown) son la fuente de verdad; se versionan en `.specify/`.

_El diseño se refina; las decisiones ✅ CERRADO y la evidencia de los spikes son firmes. Lo que sigue es diseñar bien la API y los contratos, luego construir por hitos con estándares._
