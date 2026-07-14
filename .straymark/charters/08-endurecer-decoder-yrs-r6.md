---
charter_id: CHARTER-08-endurecer-decoder-yrs-r6
status: in-progress
effort_estimate: M
trigger: "FU-014 (registrado 2026-07-14): el job `fuzz` de CHARTER-07 confirmó que el decoder de yrs amplifica memoria (R6) en la ruta CRUDA del FFI, NO capeada por FU-002 (relay). Investigación upstream (2026-07-14, ver [[yrs-decoder-r6-upstream]]): `Update::decode` YA usa `try_reserve`, pero `id_set.rs:91` (delete sets) y `state_vector.rs:120` (state vectors) siguen con `with_capacity` sin acotar — presente en 0.27.2 Y en la última 0.27.3. Decisión del operador: FU-014 = charter propio + colaboración upstream (PR de `try_reserve`)."
originating_spec: specs/001-weft-crdt-versioning/spec.md
work_verb: implement
design_provenance: new
---

# Charter: Endurecer el decoder de yrs contra amplificación de memoria (R6)

> **Status (mirrored from frontmatter — source of truth is above):** in-progress. Effort: M.
>
> **Origin:** Follow-up **FU-014**, sobre la spec 001 (constitución **P-I/P-II**: frontera nativa segura /
> memoria verificada). Endurece la ruta **directa del FFI** ante la amplificación R6 que FU-002 (cerrado)
> mitigó solo en la capa de relay. **Colaboración upstream decidida**: PR de `try_reserve` a `y-crdt`.

## Context

**R6** es una amplificación de memoria del decoder de yrs (allocation-bomb): un update/state-vector malformado
de pocos bytes declara una longitud gigante y yrs hace `with_capacity(N)` sin acotar N contra los bytes
restantes. No es un fallo del formato CRDT — es una brecha de robustez del decoder, clase conocida. Repro:
`Update::decode_v1(&[255,255,255,122])`.

La **investigación upstream (2026-07-14, ver `[[yrs-decoder-r6-upstream]]` en memoria)** precisó el estado real:
- Las **primitivas de bajo nivel** (`read_exact`/`read_buf`) **ya acotan** (zero-copy, error si `len > restante`).
- **`Update::decode` YA usa `try_reserve`** (asignación falible → error recuperable, no abort) — `update.rs:830,844`,
  introducido upstream en el commit `b234ef4e` (2023-12). **Confirmado en nuestra 0.27.2.** Por eso el fuzz ve
  `WEFT_ERR_DECODE` limpio en glibc, no un crash.
- **Residual** (sigue con `with_capacity` sin acotar, en 0.27.2 **y** en la última 0.27.3): `id_set.rs:91`
  (decode de **delete sets** — alcanzable desde `apply_update`, los updates llevan delete set) y
  `state_vector.rs:120` (decode de **state vectors** — alcanzable desde `weft_doc_export_since`).
  > **Actualización (2026-07-14, durante la implementación):** una **revisión de completitud** del crate `yrs`
  > (regla operativa: revisar más ancho que el cambio, para no enviar un fix que deja gemelos idénticos vivos)
  > encontró que la clase real son **5 sitios**, no 2: además de los dos de arriba, `any.rs:63` (`Any::Map`),
  > `any.rs:73` (`Any::Array`) y `sync/awareness.rs:560` (`AwarenessUpdate`, alcanzable desde presencia del
  > relay). El PR upstream (a) los cubre los cinco. Ver **AIDEC-2026-07-14-001** decisión 2.
- **Severidad práctica**: en glibc (overcommit) incluso el residual → reserva virtual, RSS acotado, error de
  decode limpio; el **abort** (`handle_alloc_error`, no capturable) solo en entornos memory-constrained duros
  (cgroup/contenedor con límite) o allocators eager (ASan). → robustez/calidad, no un hueco urgente.

Trabajo de **implementación** que tensa **P-I/P-II** (frontera nativa / memoria verificada) y **P-VI** (portabilidad
del comportamiento). La corrección **canónica** es upstream: extender el patrón `try_reserve` que yrs mismo ya
estableció (`b234ef4e`) a los dos sitios residuales — **termina su propia migración**, alta probabilidad de merge,
y beneficia a todo el ecosistema. Nuestro repo aporta la **prueba de regresión** (fuzz) y la **documentación** del
caveat de ingesta directa.

## Scope

**In scope (4 partes):**

1. **(a) PR upstream a `y-crdt`**: mantener el fork **`StrangeDaysTech/y-crdt`**; PR contra `y-crdt/y-crdt` que
   endurece la clase completa de allocation-bomb por prefijo de longitud, con tests upstream que ejerciten el
   input adversarial. Deliverable: **PR abierto** (link en el AILOG/telemetría). Ejecuta la colaboración upstream
   decidida. **Entregado: [#639](https://github.com/y-crdt/y-crdt/pull/639)** — **5 sitios** (ampliado de 2 por la
   revisión de completitud, ver §Context y AIDEC-2026-07-14-001): `try_reserve(len)?` en `state_vector.rs:120`,
   `any.rs:63` (`Any::Map`), `any.rs:73` (`Any::Array`) y `sync/awareness.rs:560` (`AwarenessUpdate`) →
   variante `Error::NotEnoughMemory` existente; grow-on-push en `id_set.rs:91` (`SmallVec`; su `try_reserve` da
   otro tipo de error → evita ampliar el enum público). 5 tests de regresión, suite del fork verde (377+34).
2. **(b) Doc del caveat**: nota de seguridad en `GOVERNANCE.md` §Seguridad (+ pointer breve en `README.md`):
   la ruta **directa** del FFI (`weft_doc_load`/`apply_update`/`export_since`) ante bytes CRDT **no confiables**
   debe protegerse con un cap de tamaño + límite de memoria del proceso, como hace el relay (FU-002); la ruta
   `apply_update` ya está `try_reserve`-endurecida upstream. Ship-now, no espera al merge.
3. **(c) Fuzz de regresión**: nuevo target `cargo-fuzz` `native/weft-yrs-ffi/fuzz/fuzz_targets/export_since.rs`
   (+ `[[bin]]` en `fuzz/Cargo.toml`) que alimenta bytes arbitrarios como **state vector** a
   `weft_doc_export_since` → ejercita la ruta residual `state_vector::decode`; cableado al job `fuzz` (informativo)
   de `ci.yml`. Documenta/rastrea el residual y **prueba** el fix cuando se adopte. (`apply_update.rs` ya cubre la
   ruta delete-set vía update.)
4. **(d) Adopción del fix**: bump de yrs a la versión que incluya el fix (protocolo **R16**) — **diferido a un
   follow-up nuevo (FU-015)** que dispara cuando upstream mergee + publique. **NO bloquea el cierre de este Charter.**

**Out of scope:**

- **El merge upstream y el bump de adopción (d)**: fuera de nuestro control (timeline de revisión de y-crdt). El
  cierre de este Charter depende SOLO de nuestros entregables — ver §Charter Closure. FU-015 cubre la adopción.
- Tocar las primitivas lib0 de bajo nivel (`read.rs`) — ya acotan; no son el problema.
- FU-012 (client-id determinista, CHARTER-09), FU-006 (Loro nativo, CHARTER-10), FU-010 (durabilidad relay) —
  charters/follow-ups aparte.
- Reescribir el decoder de yrs de nuestro lado (fork mantenido, no vendorizado): el fix vive upstream + bump.

## Files to modify

<!-- Reconnaissance #210: fuzz de weft-yrs-ffi verificado (Cargo.toml con [[bin]] doc_load/apply_update,
     fuzz_targets/*.rs usan el shim: weft_doc_new + weft_doc_export_since existe desde CHARTER-01); GOVERNANCE.md
     §Seguridad y README.md existen (CHARTER-07); sitios upstream id_set.rs:91 / state_vector.rs:120 confirmados
     por GitHub API en 0.27.2 y 0.27.3. El PR (a) es EXTERNO al repo (fork StrangeDaysTech/y-crdt) → no es un
     archivo de este repo. -->

| File | Change |
|---|---|
| `native/weft-yrs-ffi/fuzz/fuzz_targets/export_since.rs` | New — fuzz target de la ruta residual `state_vector::decode` vía `weft_doc_export_since` (T-c) |
| `native/weft-yrs-ffi/fuzz/Cargo.toml` | Change — registrar el `[[bin]] export_since` (T-c) |
| `.github/workflows/ci.yml` | Change — añadir el target `export_since` al job `fuzz` (informativo) (T-c) |
| `GOVERNANCE.md` | Change — nota de seguridad: ingesta directa no confiable → cap + límite de memoria (T-b) |
| `README.md` | Change — pointer breve al caveat de seguridad (T-b) |
| `.straymark/follow-ups-backlog.md` | Change — FU-014 → `closed`; registrar **FU-015** (adopción vía bump R16) |
| `.straymark/07-ai-audit/agent-logs/AILOG-*.md` | New, `risk_level: medium` (frontera nativa P-I/P-II; input adversarial; PR upstream) |
| `.straymark/07-ai-audit/decisions/AIDEC-*.md` | New si emerge decisión sustantiva (forma del guard: `try_reserve` vs bound-vs-remaining) |

**Externo (no en este repo):** PR a `y-crdt/y-crdt` desde el fork `StrangeDaysTech/y-crdt` — los dos sitios
upstream de yrs (id_set y state_vector, líneas confirmadas en el §Context) + tests upstream. Link en el
AILOG/telemetría.

## Verification

### Local checks

```bash
# El fuzz de regresión compila y corre; el shim NO hace panic-through / UB (mismo invariante que doc_load).
cd native/weft-yrs-ffi && cargo +nightly fuzz build export_since
cargo +nightly fuzz run -s none export_since -- -max_total_time=60 -rss_limit_mb=0 -max_len=8192   # informativo (R6)

# Suite completa intacta + ASan sin fugas (P-II) sobre la suite determinista.
cd ../.. && dotnet test Weft.sln -c Release

# El PR upstream: en el fork, la suite de yrs pasa con el fix + el nuevo test adversarial.
#   (cd <fork>/y-crdt && cargo test -p yrs)   # se corre en el fork, no en este repo.
```

### Production smoke (after deploy)

No aplica — librería sin despliegue. Los auditores externos deben saltar esta sección.

## Risks

- **R1 — El merge upstream no llega (o tarda / se rechaza)**: severidad **media**. El fix canónico vive en y-crdt,
  cuyo timeline no controlamos. Mitigación: el **cierre de este Charter NO depende del merge** — cierra con
  nuestros entregables (fuzz + doc + PR **enviado**); la adopción es **FU-015** (dispara al publicarse). Si el PR
  se rechaza: reevaluar un guard de nuestro lado (pre-validación en el shim) como plan B, registrado como riesgo
  emergente. Nada regresiona mientras tanto (comportamiento actual = error limpio en glibc).
- **R2 — El fuzz de regresión no ejercita realmente el residual**: severidad **media**. Si el target no alcanza
  `state_vector::decode`, no prueba nada. Mitigación: `weft_doc_export_since` decodifica el SV vía
  `StateVector::decode` (verificado, CHARTER-01); el target alimenta el SV crudo. Validar que un input tipo
  `[255,255,255,122]` dispara la ruta (RSS medido / error de decode).
- **R3 — Sobre-estimar la severidad y alarmar innecesariamente en la doc**: severidad **baja**. En glibc es error
  limpio, no crash. Mitigación: la nota de doc calibra honesto (afecta hosts memory-constrained; `apply_update`
  ya endurecido; el relay ya capea). No es un CVE nuestro.
- **R4 — Divergir del fork upstream**: severidad **baja**. Mantener `StrangeDaysTech/y-crdt` como fork implica
  rebase periódico. Mitigación: el fork es solo para el PR (no vendorizamos); tras el merge, volvemos a consumir
  yrs de crates.io vía el bump. Documentar el propósito del fork en su README.

## Tasks

1. Sync main, branch `charter/08-yrs-decoder-hardening` (**ya sobre `chore/fu-014-register`** → pliega el registro
   de FU-014). Flip `declared` → `in-progress` al empezar.
2. Re-evaluar **Constitution Check**: **P-I/P-II** (frontera nativa / memoria — el fix es upstream + nuestra prueba
   de regresión), **P-VI** (comportamiento portable). Sin violaciones esperadas.
3. **(c)** Escribir `export_since.rs` + registrar en `fuzz/Cargo.toml` + cablear a `ci.yml`; validar que ejercita
   el residual localmente.
4. **(a)** Crear/actualizar el fork `StrangeDaysTech/y-crdt`; rama con el fix (`try_reserve` en `id_set.rs:91` +
   `state_vector.rs:120`) + tests upstream; abrir PR contra `y-crdt/y-crdt`. Guardar el link.
5. **(b)** Nota de seguridad en `GOVERNANCE.md` + pointer en `README.md`.
6. **(d)** Registrar **FU-015** (adopción vía bump R16) en el backlog; NO ejecutarlo aquí.
7. **AILOG** (`risk_level: medium`, `review_required: true`) con el link del PR upstream. **AIDEC** si la forma del
   guard amerita decisión.
8. Cerrar FU-014 en el backlog + `recount`. Verificación local completa.
9. `straymark charter drift CHARTER-08` (los `.rs`/`.toml`/`.yml`/`.md` pueden dar FP del parser #354 — documentar).
   Commit + push + PR contra `main`; CI verde.

## Charter Closure

Charter con un **entregable externo** (PR upstream) cuyo merge NO controlamos → su cierre depende **solo de
nuestros entregables**. **No cierra hito** (no requiere auditoría externa multi-modelo). Al cerrar:

1. Confirmar entregados: **(c)** fuzz de regresión verde/informativo, **(b)** doc, **(a)** PR upstream **abierto**
   (link en telemetría), **(d)** FU-015 registrado. El merge + bump (adopción) es **FU-015**, fuera de scope.
2. Actualización atómica del Charter (format v4) si el drift reveló divergencias, mismo PR.
3. `straymark charter drift CHARTER-08 --range origin/main..HEAD` → limpio o documentado (incl. FP del parser #354).
4. `straymark charter close CHARTER-08` (telemetría con el link del PR upstream). No borrar este archivo.
5. Confirmar el backlog: **FU-014 `closed`**, **FU-015 `open`** (adopción). Siguiente en la secuencia:
   **CHARTER-09 (FU-012, client-id determinista)** → **CHARTER-10 (FU-006, Loro nativo)**; FU-010 diferido.
