# Weft — contexto para agentes

Librería .NET (Apache-2.0): colaboración CRDT en tiempo real + versionado content-addressed
sobre el core Rust `yrs`, vía shim C-ABI propio. Building block reutilizable, no una app.

## Flujo de trabajo

Spec-driven con GitHub Spec Kit. Feature activa: `specs/001-weft-crdt-versioning/`
(spec → plan → contratos → tasks). La constitución (`.specify/memory/constitution.md`, v1.0.0)
es vinculante: 6 principios (FFI segura, memoria verificada en CI, determinismo, abstracción
de motor viva, concurrencia serializada por doc, portabilidad por RID). Las decisiones
✅ CERRADO del brief (`weft-design-brief.md`) no se re-litigan. Evidencia experimental en
`docs/spikes/`.

## Stack activo

- **C# 13 / .NET 10** (`net10.0`): `Weft.Core` (binding + abstracciones + DocumentBroker),
  `Weft.Versioning` (engine-agnóstico), `Weft.Server` (relay y-sync, ASP.NET Core),
  `Weft.Loro` (dual-path).
- **Rust stable** (pinned): `native/weft-yrs-ffi` y `native/weft-loro-ffi` (cdylib);
  `yrs = "=0.27.2"`, `loro = "=1.13.6"` exactos. Nightly solo para ASan/LSan en CI.
- **Tests**: xUnit, CsCheck (convergencia), cargo test/fuzz, suite dual-engine.

## Reglas duras al escribir código

- Ningún panic cruza la frontera C (`catch_unwind` en cada entrada del shim).
- Buffers del shim se liberan SOLO con `weft_buf_free`; el GC jamás toca memoria nativa.
- `ICrdtDoc` no es thread-safe: acceso serializado (fuera del broker, responsabilidad del
  dueño; el broker usa actor/canal single-reader).
- Nunca `skip_gc` en el motor; la citabilidad viene de blobs content-addressed por versión.
- `Weft.Versioning` no puede referenciar tipos de yrs/Loro — solo las abstracciones.
- API pública con índices `int` validados; errores nativos → jerarquía `WeftException`.

Contratos de referencia: `specs/001-weft-crdt-versioning/contracts/` (core-api, ffi-abi,
versioning-api, server-api).

## Auditoría externa (StrayMark)

Generar el prompt de auditoría externa (`straymark charter audit --prepare` / `/straymark-audit-prompt`)
SOLO con el estado estable: **CI del PR en verde** (ni en curso ni rojo), sin trabajos en vuelo que
puedan cambiar el árbol, y working tree limpio y pusheado. El prompt embebe el diff `origin/main..HEAD`
y el código al momento de generarlo; si algo pendiente lo altera después (p. ej. un gate que falla y
fuerza un fix), el prompt queda obsoleto y hay que detener y regenerar las auditorías. Secuencia:
PR → **CI verde** → `--prepare` → lanzar auditores. (Origen: CHARTER-02 — el prompt se generó con el CI
corriendo, el CI destapó R7, y hubo que regenerar.)

<!-- straymark:begin -->
> **Read and follow the rules in [STRAYMARK.md](STRAYMARK.md).**
> That file contains all StrayMark documentation governance rules for this project.
<!-- straymark:end -->
