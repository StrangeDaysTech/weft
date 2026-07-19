---
charter_id: CHARTER-13-siembra-cross-engine
status: closed
closed_at: 2026-07-19
effort_estimate: S
trigger: "El operador decide implementar FU-016 (diferido en CHARTER-09) como parte del vaciado del backlog antes del publish (T060). Segundo de tres Charters de ese vaciado, tras CHARTER-12. La investigación previa resolvió las dos incógnitas que hacían de FU-016 un coste M: la API real de set_peer_id de loro 1.13.6 y el default de record_timestamp — ambas verificadas en las fuentes pinneadas, lo que baja el coste a S."
originating_spec: specs/001-weft-crdt-versioning/spec.md
work_verb: implement
design_provenance: new
---

# Charter: Siembra de réplica cross-engine — IDeterministicSeeding + peer_id de Loro

> **Status (mirrored from frontmatter — source of truth is above):** closed. Effort: S.
>
> **Origin:** FU-016. CHARTER-09 expuso la siembra de client-id como capacidad **concreta de
> `YrsEngine`** (`CreateDoc(ulong)`), no en `ICrdtEngine`, porque el gate `determinism-yjs` es
> yrs↔Yjs. Este Charter la promueve a capacidad cross-engine y añade el equivalente de Loro.

## Context

Un `LoroDoc` fresco recibe un `peer_id` **aleatorio** (`loro-internal/src/state.rs`: `DefaultRandom`),
así que hoy Loro **no tiene golden de determinismo**: dos ejecuciones producen exports distintos. yrs
sí lo tiene desde CHARTER-09, vía `weft_doc_new_with_client_id` + el gate de paridad contra Yjs.
Sembrar el `peer_id` de Loro cierra esa asimetría — pero la promoción tiene un crux de diseño y un
espejismo de gate que la investigación resolvió antes de escribir código.

**El crux (resuelto): capacidad opcional, no método en `ICrdtEngine`.** El argumento «un motor futuro
podría no tener id sembrable» es débil (casi todo CRDT op-based lo tiene). El argumento decisivo es la
**asimetría de dominios**, verificada en las fuentes de las versiones pinneadas:

| Motor | Dominio válido del id | Fuente |
|---|---|---|
| yrs 0.27.2 | `< 2^53` (encoding de 53 bits) | `weft-yrs-ffi/src/lib.rs:57` |
| loro 1.13.6 | `u64` completo **excepto `u64::MAX`** (reservado → `LoroError::InvalidPeerID`) | `loro-internal/src/loro.rs:184` |

Un `ICrdtEngine.CreateDoc(ulong)` único **no puede enunciar un contrato uniforme sobre su dominio
válido**: el llamador tendría que ramificar por `engine.Name` para saber si `2^60` es legal — la
abstracción se filtra justo donde P-IV existe para protegerla. La solución es el patrón ya establecido
en el repo (`ICrdtEngine.NativeVersioning → INativeVersioning?`): una interfaz opcional
`IDeterministicSeeding` que hace del dominio parte del contrato (`MaxReplicaIdExclusive`).

**El espejismo de gate (descartado): «Loro↔referencia» NO es realizable.** FU-016 pedía un gate de
paridad de Loro contra una referencia. No existe una implementación independiente de Loro:
`loro-crdt` de npm es un build wasm del **mismo** core Rust, así que compararlo con nuestro `loro`
nativo es compararlo consigo mismo — tautológico. El gate yrs↔Yjs es significativo porque Yjs y yrs
son implementaciones genuinamente independientes de un formato; Loro no tiene contraparte. El gate
**realizable y útil** es distinto: *Loro es determinista consigo mismo, cross-run y cross-RID, con
`peer_id` fijo* (clase R13(a), no R13(b)). Lo habilitan dos hechos verificados: el `peer_id` aleatorio
por defecto (de ahí que hoy no haya golden) y `record_timestamp = false` por defecto
(`loro-internal/src/configure.rs:33` — si fuera `true`, el gate sería imposible con o sin `peer_id`).

## Scope

**In scope:**

1. **`IDeterministicSeeding`** (nuevo) en `Weft.Core.Abstractions`: `ulong MaxReplicaIdExclusive` +
   `ICrdtDoc CreateDoc(ulong replicaId)`. Expuesta como `ICrdtEngine.DeterministicSeeding` (`null` si el
   motor no la soporta — hoy ambos la soportan). Nombre `replicaId`, no `clientId`/`peer_id` (dialectos
   de motor).
2. **`YrsDeterministicSeeding`** (nuevo): delega en `YrsEngine.CreateDoc(ulong)` ya existente;
   `MaxReplicaIdExclusive = 1UL << 53`. El método concreto se conserva (no romper CHARTER-09).
3. **Shim de Loro**: `weft_loro_doc_new_with_peer_id(uint64 peer_id, out doc)` con guard de `u64::MAX`
   en la frontera (espejo del guard de rango de yrs), `catch_unwind`. **ABI v2 → v3.**
4. **`LoroDeterministicSeeding`** (nuevo): `MaxReplicaIdExclusive = ulong.MaxValue`; binding +
   `LoroDoc.Create(ulong)`; `NativeLibraryResolver.ExpectedAbiVersion = 3`.
5. **Gate de auto-determinismo de Loro**: `[Theory]` en `DeterminismTests` que aplica el corpus
   compartido con `peer_id` fijo y asierta contra `golden-loro.json` (nuevo). Documentado como
   **testigo de regresión, no prueba de paridad** — caza un cambio de encoding al bumpear `loro` (R16).
6. **Test de paridad header↔binding**: el de CHARTER-12 (`HeaderBindingParityTests`) valida
   automáticamente la nueva función y el bump de ABI. **No hay que escribirlo — ya existe, por eso este
   Charter va después del 12.** Verificar que sigue verde.

**Out of scope:**

- **Cablear la siembra en el relay/broker.** Sembrar `peer_id` es un **footgun en producción**: Loro
  advierte que reusar el mismo PeerID entre escritores concurrentes corrompe el documento. La capacidad
  es para uso determinista de test/corpus. `DocumentBroker`/`WeftServer` siguen usando `CreateDoc()`.
- **Un gate «Loro↔referencia»** — no realizable (ver Context). Se implementa el de auto-determinismo.
- **FU-010** (durabilidad del relay) → CHARTER-14. **FU-015** — bloqueado por upstream.

## Files to modify

| File | Change |
|---|---|
| `src/Weft.Core/Abstractions/IDeterministicSeeding.cs` | New — la capacidad opcional |
| `src/Weft.Core/Abstractions/ICrdtEngine.cs` | `+ IDeterministicSeeding? DeterministicSeeding` |
| `src/Weft.Core/Yrs/YrsEngine.cs` | `DeterministicSeeding => YrsDeterministicSeeding.Instance`; actualizar el XML doc de `CreateDoc(ulong)` (dice «se difiere a un follow-up») |
| `src/Weft.Core/Yrs/YrsDeterministicSeeding.cs` | New — `MaxReplicaIdExclusive = 1UL << 53` |
| `native/weft-loro-ffi/src/lib.rs` | `weft_loro_doc_new_with_peer_id` + guard `u64::MAX` + `WEFT_ABI_VERSION` 2→3 |
| `native/weft-loro-ffi/include/weft_loro_ffi.h` | Declarar la fn nueva; ABI v2→v3 en el comentario |
| `native/weft-loro-ffi/tests/mem_asan.rs` | `assert_eq!(weft_loro_abi_version(), 3)` + loop ASan/LSan sobre la fn nueva |
| `src/Weft.Loro/Interop/NativeMethods.cs` | `weft_loro_doc_new_with_peer_id` |
| `src/Weft.Loro/Interop/NativeLibraryResolver.cs` | `ExpectedAbiVersion = 3` |
| `src/Weft.Loro/LoroDoc.cs` | `internal static LoroDoc Create(ulong peerId)` |
| `src/Weft.Loro/LoroEngine.cs` | `DeterministicSeeding => LoroDeterministicSeeding.Instance` |
| `src/Weft.Loro/LoroDeterministicSeeding.cs` | New — `MaxReplicaIdExclusive = ulong.MaxValue` |
| `tests/Weft.Core.Tests/DocumentBrokerTests.cs` | `TrackingEngine` (impl de `ICrdtEngine` en tests) gana `DeterministicSeeding => null` |
| `tests/Weft.Versioning.Tests/DeterministicSeedingTests.cs` | New — forma de la capacidad + guard de rango en ambos motores |
| `tests/Weft.Determinism.Tests/DeterminismTests.cs` | Gate de auto-determinismo de Loro |
| `tests/Weft.Determinism.Tests/Weft.Determinism.Tests.csproj` | ProjectReference a `Weft.Loro` (el gate de Loro lo necesita) |
| `tests/determinism-yjs/golden-loro.json` | New — golden del corpus sembrado (testigo de regresión) |
| `.straymark/follow-ups-backlog.md` | Cierre de FU-016 con su premisa corregida |
| `.straymark/07-ai-audit/agent-logs/AILOG-2026-07-16-002-charter-13-siembra-cross-engine.md` | New, `risk_level: low` |

## Verification

### Local checks

```bash
cargo build --release --features test-hooks --manifest-path native/Cargo.toml
dotnet build Weft.sln -c Release

# El bump de ABI: el resolver del binding debe aceptar v3; el test de paridad valida la fn nueva
dotnet test tests/Weft.Versioning.Tests -c Release --filter "FullyQualifiedName~HeaderBindingParity"

# Suite completa + gate de auto-determinismo de Loro
dotnet test Weft.sln -c Release
cargo test --features test-hooks --manifest-path native/Cargo.toml

# El golden de Loro debe ser estable cross-run: correr dos veces y comparar
dotnet test tests/Weft.Determinism.Tests -c Release --filter "FullyQualifiedName~Loro"
dotnet test tests/Weft.Determinism.Tests -c Release --filter "FullyQualifiedName~Loro"

# P-II: la fn nueva bajo ASan/LSan
RUSTFLAGS="-Zsanitizer=address" cargo +nightly test --features test-hooks \
  --target x86_64-unknown-linux-gnu --manifest-path native/Cargo.toml

straymark validate --include-charters
```

## Risks

- **R1 — El gate «Loro↔referencia» del follow-up no es construible**: certeza alta. Planificar contra
  él produciría un deliverable fantasma.
  Mitigación: se implementa el gate de **auto-determinismo** (cross-run/cross-RID con peer_id fijo),
  documentado explícitamente como testigo de regresión, no paridad. La entrada de FU-016 en el registro
  se cierra con esta corrección anotada.
- **R2 — El golden de Loro se lee como «prueba de paridad»**: probabilidad media, severidad media. Un
  doc futuro podría citarlo como equivalente al gate yrs↔Yjs, que es cualitativamente más fuerte.
  Mitigación: el `_comment` de `golden-loro.json` y el doc-comment del test dicen literalmente «testigo
  de regresión, NO paridad» y citan la razón (no hay implementación Loro independiente). Exagerar el
  alcance de una verificación es la clase que CHARTER-12 acaba de cerrar.
- **R3 — `record_timestamp` cambia de default en un loro futuro** → el auto-determinismo muere en
  silencio: probabilidad baja, severidad alta. Hoy es `false` (verificado); si pasara a `true`, el
  oplog llevaría wall-clock y el golden divergiría cross-run.
  Mitigación: (a) el gate lo caza al bumpear (el golden cambiaría), que es el R16 funcionando; (b)
  evaluar asertar el config explícitamente en el shim en vez de confiar en el default — decisión a
  tomar al implementar, documentada en el AILOG.
- **R4 — Sembrar `peer_id` es un footgun si se cablea en producción**: probabilidad baja (out of scope),
  severidad alta. Reusar un PeerID entre escritores concurrentes corrompe el documento (aviso de Loro).
  Mitigación: el XML doc de `IDeterministicSeeding` lo dice; el relay/broker **no** se tocan; ningún
  test lo cablea en un escenario multi-cliente.
- **R5 — ABI v3 vs. un `.so` cacheado v2** hace fallar los tests locales por el resolver: probabilidad
  media, severidad baja.
  Mitigación: el `NativeLibraryResolver` rechaza con excepción clara (es su trabajo); el flujo de
  verificación recompila el nativo primero. Se anota en el AILOG.
- **R6 — Promover a `ICrdtEngine.DeterministicSeeding` tensa P-IV** si se hace mal: probabilidad baja
  con el patrón de capacidad opcional.
  Mitigación: se replica exactamente `NativeVersioning → INativeVersioning?` (null si no se soporta),
  que es el patrón bendecido del repo para capacidades de motor. `LoroNativeVersioningTests` ya asierta
  `Assert.Null(YrsEngine.Instance.NativeVersioning)`; el test análogo asierta la forma de la nueva.

## Tasks

1. Sync main (con CHARTER-12), branch `charter/14-siembra-cross-engine` (número de Charter 13).
2. Shim de Loro: `weft_loro_doc_new_with_peer_id` + guard + ABI v3 + header + `mem_asan.rs`.
3. `IDeterministicSeeding` + `ICrdtEngine` + `YrsDeterministicSeeding` + `LoroDeterministicSeeding` +
   binding + `LoroDoc.Create(ulong)` + resolver v3.
4. Tests de la capacidad + guard de rango + gate de auto-determinismo de Loro (`golden-loro.json`).
5. Verificar que `HeaderBindingParityTests` valida la fn nueva sin tocarlo (es el pago del orden 12→13).
6. AILOG (`risk_level: low`) + cerrar FU-016 + `recount`.
7. Verificación local limpia (incl. golden estable en 2 corridas) + `charter drift`.
8. Commit + push + PR.

## Charter Closure

1. **Atomic update (format v4)** si el drift reporta deriva no capturada.
2. **Post-merge drift check** `--range origin/main..HEAD`.
3. **Status** `in-progress` → `closed` + `closed_at`.
4. Al cerrar, `straymark followups status` debe bajar a 2 open (FU-010, FU-015).
5. **No borrar** este archivo.
