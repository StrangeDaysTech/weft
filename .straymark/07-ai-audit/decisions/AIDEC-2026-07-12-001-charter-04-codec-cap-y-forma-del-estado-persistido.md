---
id: AIDEC-2026-07-12-001
title: "CHARTER-04: forma del cap de tamaño (FU-002 a) y forma del estado que devuelve IDocumentStore.LoadAsync"
status: draft
created: 2026-07-12
agent: claude-opus-4-8
confidence: high
review_required: true
reviewed_by: ""
reviewed_at: ""
review_outcome: pending
risk_level: medium
eu_ai_act_risk: not_applicable
nist_genai_risks: []
iso_42001_clause: []
tags: [server, y-sync, lib0, dos, ffi-boundary, persistence, document-store]
related: [AILOG-2026-07-12-001]
---

# AIDEC: cap de tamaño del códec (FU-002 a) y forma del estado persistido

> Registra las dos decisiones de implementación sustantivas de CHARTER-04 sobre el contrato congelado
> `contracts/server-api.md` (exigido por el Charter §Tasks). Ambas quedan fijas para CHARTER-05/06.

## Context

CHARTER-04 construye el substrato sin red del relay `Weft.Server`: el códec lib0/y-sync y la persistencia
`IDocumentStore` de blobs opacos. El contrato de API v1 (`server-api.md`) fija las **firmas** (`Decode`,
`LoadAsync`/`AppendUpdateAsync`/`SaveSnapshotAsync`) pero deja abierta la **forma concreta** de dos puntos que
tienen consecuencias de seguridad y de intercambiabilidad. Este AIDEC las cierra.

---

## Decisión 1 — Forma del cap de tamaño de mensaje (FU-002 parte a)

### Problem

US3 es el primer punto donde el motor recibe bytes de red **no confiables**. Un update malformado puede
amplificar memoria (pocos bytes → asignación gigante en el decoder de yrs → posible abort), tensando P-I
("ningún panic/abort cruza la frontera") y P-II ("memoria acotada"). ¿Dónde y cómo se rechaza el input
peligroso **antes** de llegar al decoder?

### Alternatives Considered

- **A1 — Delegar la validación al decoder de yrs (o a un bump de versión con validación de longitud).**
  Pros: menos código propio. Contras: mueve la frontera de confianza *dentro* del motor nativo, justo lo que
  P-I/P-II quieren evitar; la amplificación ocurre antes de que podamos rechazarla; acopla la mitigación a una
  versión futura de `yrs`. **Rechazada.**
- **A2 — Solo un cap de frame completo (rechazar frames > N bytes).** Pros: simple. Contras: insuficiente —
  un frame *pequeño* (7 bytes) puede declarar un `VarUint8Array` de 4 GiB y disparar la asignación al decodar.
  El cap de frame no cubre el prefijo de longitud mentiroso. **Insuficiente por sí solo.**
- **A3 (elegida) — Dos guardas estructurales en la capa de framing, antes del decoder.**
  (a) Rechazo del frame completo por encima de `maxMessageBytes` (configurable, default 16 MiB) **antes** de
  parsear. (b) `Lib0Reader.ReadVarUint8Array` nunca asigna ni avanza según una longitud declarada mayor que
  los bytes que realmente quedan en el frame; además `ReadVarUint` rechaza varints truncados o que excedan 32
  bits. Ambas fallan con `MalformedMessageException` (traducible a cierre 1002 en CHARTER-05), jamás con abort.

### Rationale

La amplificación de memoria vive en el prefijo de longitud, no solo en el tamaño del frame; por eso (b) es
imprescindible y (a) sola no basta. Poner las dos guardas en la capa de framing —propia, en C# gestionado—
mantiene la frontera de confianza **fuera** del motor nativo (P-I) y acota la memoria por construcción (P-II).
El cap es configurable porque los documentos grandes legítimos se consolidan por snapshot y los updates en
vivo son pequeños. Test dedicado del prefijo mentiroso (`Decode_rejects_lying_length_prefix_without_allocating`)
y del cap de frame.

### Consequences

FU-002 queda **parcialmente** mitigado: la parte a (cap en el framing) se entrega aquí; la parte b (límites de
recursos por conexión, backpressure, path malformed→1002 en el handler real) es CHARTER-05. **FU-002 sigue
`open`** hasta la parte b.

---

## Decisión 2 — Forma del estado que devuelve `IDocumentStore.LoadAsync`

### Problem

`server-api.md` fija `ValueTask<byte[]?> LoadAsync(...)` — "estado completo persistido, null si el doc no
existe" — pero los blobs son **opacos** (P-IV: el store no conoce tipos de yrs). El estado durable de un doc es
un snapshot más los updates acumulados desde él. ¿Qué byte[] devuelve `LoadAsync`, si el store no puede
fusionar updates de yrs?

### Alternatives Considered

- **B1 — Devolver un único blob fusionado.** Requeriría fusionar snapshot + updates, imposible sin lógica de
  yrs en el store (viola P-IV). **Rechazada.**
- **B2 — Concatenar snapshot + updates crudos sin framing.** El resultado no es un update de yrs válido ni
  separable; el relay no puede saber dónde termina cada record. **Rechazada.**
- **B3 (elegida) — Secuencia plana de records enmarcada con `VarUint8Array` lib0.** `LoadAsync` devuelve el
  snapshot (si existe) seguido de los updates en orden de append, cada uno como un `VarUint8Array`; `null` si
  no hay nada persistido. El relay (CHARTER-05) lee los records (`DocumentStateFraming.ReadRecords`) y aplica
  cada uno a un doc fresco de yrs. La lógica del contenedor vive en un único punto compartido
  (`DocumentStateFraming`), reutilizando el códec lib0 en vez de inventar un segundo formato de longitud.

### Rationale

Todos los records —snapshot y updates— son operaciones `apply_update` de yrs, y aplicar un update ya integrado
es un no-op CRDT idempotente. Por eso una secuencia plana (sin distinguir snapshot de update) es suficiente y
hace la recuperación tolerante a solapes (p. ej. tras un fallo entre "escribir snapshot" y "borrar updates" en
el FileSystem store). El store no interpreta bytes de yrs → P-IV preservado. El formato es byte-preciso, así
que la contract suite lo verifica con blobs opacos aleatorios y todo adaptador futuro (EFCore/Redis, CHARTER-06)
lo hereda sin cambiarlo.

### Consequences

- `SaveSnapshotAsync` hace compaction: reemplaza el snapshot y descarta los updates (ya incorporados).
- El pitfall de conversión `byte[]`/`ReadOnlyMemory<byte>?` → vacío-no-nulo (ver AILOG §Additional Notes) es
  específico de esta forma; la firma de `Frame` se fijó a `byte[]?` para evitarlo. La dual-suite lo destapó
  antes de main.
- CHARTER-05 depende de `ReadRecords` para reconstruir el doc; CHARTER-06 depende del formato para pasar la
  contract suite intacta.
