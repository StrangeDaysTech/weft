---
id: AIDEC-2026-07-14-001
title: "CHARTER-08: forma del guard anti-amplificación en el decoder de yrs y alcance del PR upstream (2 → 5 sitios)"
status: accepted
created: 2026-07-14
agent: claude-opus-4-8
confidence: high
review_required: true
reviewed_by: Jose Villaseñor Montfort
reviewed_at: 2026-07-14
review_outcome: approved
risk_level: medium
eu_ai_act_risk: not_applicable
nist_genai_risks: []
iso_42001_clause: []
tags: [ffi-boundary, dos, yrs, upstream-pr, try-reserve, decoder, memory-amplification]
related: [AILOG-2026-07-14-002, yrs-decoder-r6-upstream]
originating_charter: CHARTER-08-endurecer-decoder-yrs-r6
---

# AIDEC: forma del guard anti-amplificación (R6) y alcance del PR upstream

> Registra las dos decisiones sustantivas de CHARTER-08 sobre el PR upstream a `y-crdt/y-crdt`
> ([#639](https://github.com/y-crdt/y-crdt/pull/639)). El Charter §Tasks anticipó la primera como
> candidata a AIDEC ("forma del guard: `try_reserve` vs bound-vs-remaining"); la segunda emergió de
> una revisión de completitud durante la implementación.

## Context

**R6** es una amplificación de memoria del decoder de yrs (allocation-bomb): un `Decode` que hace
`with_capacity(len)` con un `len` leído del prefijo de longitud —controlado por el atacante— reserva
cientos de MB con pocos bytes de input sin respaldo. Bajo glibc (overcommit) es una reserva virtual +
error de decode limpio; bajo un límite duro de memoria (cgroup/contenedor) o allocator eager, es un
`abort` (`handle_alloc_error`, no capturable por `catch_unwind`). yrs mismo ya estableció el patrón de
mitigación en `Update::decode` (`try_reserve`, commit `b234ef4e`). El Charter mandató extenderlo a los
sitios residuales.

---

## Decisión 1 — Forma del guard en cada sitio (`try_reserve` vs alternativa)

### Problem

¿Cómo se acota la reserva sin cambiar el comportamiento para input válido ni el API público del enum
`read::Error`? El enum ya tiene `NotEnoughMemory(#[from] std::collections::TryReserveError)` — la
variante que `Update::decode` alimenta vía `try_reserve`.

### Alternatives Considered

- **A1 — `try_reserve` uniforme en los cinco sitios.** Encaja perfecto para los que usan `HashMap`/`Vec`
  std (`std::collections::TryReserveError` → `?` → `NotEnoughMemory`). Pero `IdRanges::decode` usa un
  `SmallVec`, y `SmallVec::try_reserve` devuelve el error propio de smallvec (`CollectionAllocErr`), **no**
  el `std::TryReserveError`. Enchufarlo por `?` exigiría añadir una variante nueva al enum público `Error`
  (o un `From<CollectionAllocErr>`) — un cambio **breaking** para consumidores que hagan `match` exhaustivo.
  Rechazada **para el sitio SmallVec** por ese blast radius; adoptada para los otros cuatro.
- **A2 — bound-vs-remaining (rechazar si `len` > bytes restantes del decoder).** El trait `Decoder` no
  expone de forma barata/portátil los bytes restantes en todas sus implementaciones (V1/V2), así que el
  guard no vive limpio en esta capa. Rechazada.
- **A3 (elegida para SmallVec) — no pre-asignar: crecer en `push`.** `SmallVec::new()` sin
  `with_capacity`; cada `Range::decode` empujado consume bytes reales del decoder, así que el crecimiento
  queda acotado por la longitud real del input. Elimina la bomba sin ampliar el enum y conserva el buffer
  inline para el caso común pequeño (que es justo para lo que existe el SmallVec en `IdRanges`).

### Rationale

**Solución mixta, por tipo de colección:** `try_reserve` (variante `NotEnoughMemory` existente, idéntico
a `Update::decode`) en los cuatro sitios `HashMap`/`Vec` — `state_vector.rs`, `any.rs` (Map + Array),
`sync/awareness.rs` — y grow-on-push en el único sitio `SmallVec` (`id_set.rs`). El criterio: limitar el
alcance del cambio a corregir R6 respetando las convenciones locales, sin introducir un cambio breaking
del API por simetría cosmética. El PR **ofrece explícitamente** al maintainer cambiar el SmallVec a
`try_reserve` + `From<CollectionAllocErr>` si prefiere la simetría (la decisión de ampliar su API es suya).

### Consequences

- Sin cambio de comportamiento para input válido; solo cambia el modo de fallo del prefijo mentiroso
  (error de decode recuperable en vez de reserva ilimitada / abort).
- Cinco tests de regresión upstream (uno por ruta) con el input adversarial `[255,255,255,122]`.
- La adopción en Weft (bump de yrs) es **FU-015**, disparada al mergear+publicar upstream — no bloquea el
  cierre de CHARTER-08.

---

## Decisión 2 — Alcance del PR: los 5 sitios de la clase, no los 2 del Charter

### Problem

El Charter §Scope (basado en la investigación upstream previa, ver `[[yrs-decoder-r6-upstream]]`) nombró
**dos** sitios residuales: `id_set.rs:91` y `state_vector.rs:120`. Al implementar, una revisión de
completitud del crate `yrs` (motivada por la regla operativa: revisar más ancho que el cambio, para no
enviar un fix que deja hermanos idénticos vivos — analogía con un patch al kernel) barrió **todos** los
`with_capacity`/`reserve` del crate y los clasificó por origen del `len`.

### Alternatives Considered

- **B1 — Enviar solo los 2 sitios del Charter.** Disciplinado con el scope declarado, pero envía un fix
  de clase **incompleto**: deja 3 gemelos idénticos vivos. Rechazada.
- **B2 (elegida) — Enviar los 5 sitios de la clase completa.** La barrida encontró 3 sitios más con el
  patrón exacto (longitud no confiable → `with_capacity` eager): `any.rs:63` (`Any::Map`), `any.rs:73`
  (`Any::Array`) y `sync/awareness.rs:560` (`AwarenessUpdate`, alcanzable desde mensajes de presencia del
  relay). Descartados correctamente los `with_capacity` alimentados por longitud **local** (`self.len(txn)`,
  `blocks.len()`, el Serializer `serde/ser.rs`, constantes, buffers de encoder). Es el mismo one-liner por
  sitio; arreglar la clase de una es lo más honesto y mergeable.

### Rationale

"Limitar el cambio a lo que reportamos" aplica a la **clase** reportada (allocation-bomb por prefijo de
longitud), no a una enumeración incompleta de instancias. Fijar 2 de 5 y dejar 3 idénticos sería
precisamente el error que la revisión ancha busca evitar. Corroboración de alcanzabilidad: el fuzz
`apply_update` ya ejercita `any.rs` (los updates llevan contenido `Any`); la presencia es tráfico no
confiable del relay.

### Consequences

- **Expande el scope declarado de CHARTER-08** de 2 a 5 sitios. Reconciliado en el propio Charter (§Context/
  §Scope, actualización atómica format v4 en el mismo PR de cierre) y documentado aquí + en AILOG.
- El entregable (c) de Weft (fuzz `export_since` de regresión) sigue cubriendo la ruta `state_vector`
  residual local; los 5 tests upstream cubren la clase completa en el lado de yrs.
- No cambia el resto del Charter: (b) doc de caveat y (d) FU-015 quedan igual.

## Approval

**Approved**: 2026-07-14 por `Jose Villaseñor Montfort`, en revisión interactiva. El operador revisó el
diff final de los 5 sitios + los 5 tests antes del envío, aprobó explícitamente la expansión 2→5 (decisión
2), la forma mixta del guard (decisión 1), la identidad del commit (sin trailer de coautoría; disclosure
honesto de uso de IA con responsabilidad humana en el cuerpo del PR) y dio el visto bueno para abrir el PR.
Compañero de AILOG-2026-07-14-002. PR upstream: y-crdt/y-crdt#639.
