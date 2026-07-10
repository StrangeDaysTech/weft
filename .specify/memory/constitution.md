<!--
Sync Impact Report — 2026-07-10
- Version change: (plantilla sin ratificar) → 1.0.0 (ratificación inicial)
- Modified principles: n/a (creación inicial; 6 principios derivados del §4 del brief de diseño
  y de la evidencia de los spikes 01–03 en docs/spikes/)
- Added sections: Core Principles (I–VI), Restricciones adicionales (decisiones cerradas),
  Flujo de desarrollo, Governance
- Removed sections: ninguna (se llenaron todos los slots de la plantilla)
- Templates:
  ✅ .specify/templates/plan-template.md — el gate "Constitution Check" es genérico y se
     instancia en /speckit-plan contra estos principios; sin cambios necesarios
  ✅ .specify/templates/spec-template.md — sin secciones obligatorias nuevas; sin cambios
  ✅ .specify/templates/tasks-template.md — los tests son "OPTIONAL - only if requested";
     compatible: la spec 001 los exige explícitamente (FR-023), así que las tandas de tasks
     deben incluirlos — el principio II y VI lo refuerzan
- Follow-up TODOs: ninguno (sin placeholders diferidos)
-->

# Constitución de Weft

## Core Principles

### I. Frontera nativa segura (FFI propia)

Toda interacción con motores CRDT nativos pasa por un shim C-ABI propio; nunca se
bindea directamente la superficie C de un tercero. Reglas no negociables:

- La capa segura .NET usa `SafeHandle` (o equivalente) para todo recurso nativo:
  fuga, double-free y use-after-free deben ser imposibles desde la API pública.
- El contrato de ownership de cada buffer es explícito y documentado en ambos lados:
  los buffers devueltos por el shim se liberan SOLO con la función de liberación de la
  FFI; el GC de .NET jamás toca memoria nativa. Los buffers pasados al shim son
  prestados; el shim no toma posesión.
- Ningún panic cruza la frontera C (`catch_unwind` obligatorio en cada entrada del
  shim); los errores viajan como códigos y se traducen a excepciones .NET idiomáticas
  y tipificadas.

Racional: validado en Spike 01 (9 funciones, 0 fugas en 2000 iteraciones bajo
sanitizers); el shim propio neutralizó el riesgo de reachability que descalificó a
las alternativas (`yffi` directo, ports puros).

### II. Memoria verificada como gate de CI (NON-NEGOTIABLE)

Cada cambio aceptado corre la suite bajo AddressSanitizer + LeakSanitizer (o
herramientas equivalentes en la plataforma de CI) con resultado exigido de 0 fugas y
0 liberaciones dobles. Un cambio que degrade esto no se fusiona, sin excepciones.
Los spikes cerraron con memoria limpia; ese es el estándar permanente.

### III. Determinismo verificable (content-addressing)

La identidad de una versión es `SHA-256` de su export byte-determinista: el mismo
contenido lógico DEBE producir los mismos bytes en cualquier réplica y plataforma.

- Un test de determinismo del encoding actúa como gate de CI en cada cambio y en
  cada bump del motor (idealmente contrastado cross-implementación contra Yjs JS).
- El determinismo del export es una observación experimental, no una garantía
  documentada del motor (salvedad viva): el gate existe para detectar regresiones
  antes de cualquier release. Si se empiezan a hashear updates incrementales o
  snapshots parciales, su determinismo se revalida antes de usarse como identidad.
- La recolección interna del motor permanece siempre activa (nunca `skip_gc`); la
  citabilidad se logra con blobs content-addressed por versión publicada, no
  desactivando GC (medido en Spike 03: sin GC el estado crece 7.3× sin cota).

### IV. Abstracción de motor viva y dependencias aisladas

Toda la capa de versionado se implementa exclusivamente contra la abstracción de
motor (`ICrdtEngine` y compañía); ninguna referencia a un motor concreto fuera de su
adaptador.

- El adaptador alternativo (Loro) se mantiene compilable y ejercitado en CI: la misma
  suite de versionado DEBE pasar sobre ambos motores. Una abstracción que solo tiene
  una implementación ejercitada se considera rota.
- El motor principal (`yrs`) se fija por versión exacta (version-pinning); un bump se
  hace deliberadamente: regenerar el shim, re-correr sanitizers, determinismo y
  convergencia. El shim aísla el bump: un cambio de `yrs` toca solo el shim, nunca la
  superficie C estable ni el C#.
- Capacidades que solo un motor ofrece se exponen como capacidad opcional
  (p. ej. `INativeVersioning`), jamás como dependencia del núcleo.

Racional: Spike 03 — la misma capa de dominio (~58 LOC) corrió idéntica sobre yrs y
Loro con 6 primitivas; esa portabilidad es el seguro de la decisión de motor.

### V. Concurrencia serializada por documento

El motor no es thread-safe y eso se asume como invariante de diseño: todo acceso a un
mismo documento se serializa (patrón actor/canal, un flujo de operaciones por
documento). El acceso concurrente directo al documento nativo DEBE ser imposible
desde la API pública. El ciclo de vida a escala (registro, pooling, desalojo por
inactividad) libera recursos de forma determinista y se valida con pruebas de carga
sin corrupción ni crecimiento no acotado de memoria.

### VI. Portabilidad probada por plataforma

El componente se distribuye como paquete NuGet con binarios nativos por RID
(`linux-x64`, `linux-arm64`, `win-x64`, `osx-arm64`) y resolución automática. "Soportado"
significa probado: cada RID publicado tiene build y prueba de humo en CI. No se
declara soporte de una plataforma que la CI no ejercita.

## Restricciones adicionales (decisiones cerradas)

Las decisiones ✅ CERRADO del brief de diseño (`weft-design-brief.md`) son contexto
firme: motor `yrs` adoptado (no reimplementado ni forkeado); shim C-ABI propio en Rust
con P/Invoke `[LibraryImport]`; versionado content-addressed en capa de dominio
engine-agnóstica; serialización por documento; licencia Apache-2.0; cliente de editor
recomendado Tiptap + y-prosemirror contra el servidor relay .NET; repo propio con
dirección de dependencia consumidor→Weft (nunca al revés); dual-path Loro como
capacidad opcional.

Estas decisiones NO se re-litigan en specs, planes ni PRs. La única vía para
reabrirlas es documentar una contradicción técnica dura (con evidencia reproducible)
y tramitarla como enmienda a esta constitución. La evidencia experimental que las
sustenta vive en `docs/spikes/` y se preserva en el repo.

## Flujo de desarrollo

- **Spec-driven**: el trabajo fluye por GitHub Spec Kit — spec → plan → tasks →
  implement. Los artefactos en `specs/<feature>/` son la fuente de verdad y se
  versionan; el código no se adelanta a su spec/plan aprobados.
- **Construcción por hitos** (M0 base → M1 concurrencia → M2 sync/servidor → M3
  empaquetado/release), cada hito con salida verificable definida antes de empezar.
- **Gates de CI en cada cambio**: build y tests multiplataforma; sanitizers de
  memoria (Principio II); test de determinismo (Principio III); suite de versionado
  sobre ambos motores (Principio IV); fuzzing de la frontera FFI y de convergencia
  CRDT. Un gate rojo bloquea el merge.
- **Código desechable no entra al repo**: de los experimentos persisten hallazgos y
  contratos (en `docs/`), no su código.

## Governance

- Esta constitución prevalece sobre cualquier otra práctica del repo. El gate
  "Constitution Check" de cada plan (`/speckit-plan`) y la revisión de cada PR
  verifican cumplimiento; toda complejidad que viole un principio debe justificarse
  en el plan (Complexity Tracking) o rechazarse.
- **Enmiendas**: por PR que modifique este archivo, con racional explícito y, si
  reabre una decisión cerrada, evidencia técnica reproducible. La enmienda debe
  propagar cambios a las plantillas dependientes (`.specify/templates/*`) en el mismo
  PR.
- **Versionado semántico de la constitución**: MAJOR = eliminación o redefinición
  incompatible de principios/gobernanza; MINOR = principio o sección nueva, o guía
  materialmente ampliada; PATCH = clarificaciones y redacción sin cambio semántico.
- **Revisión de cumplimiento**: al cerrar cada hito (M0–M3) se revisa que los gates
  sigan activos y que la evidencia (sanitizers, determinismo, dual-engine) siga
  verde; los desvíos se corrigen antes de iniciar el siguiente hito.

**Version**: 1.0.0 | **Ratified**: 2026-07-10 | **Last Amended**: 2026-07-10
