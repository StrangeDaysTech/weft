---
charter_id: CHARTER-12-cerrar-declaracion-sin-cableado
status: in-progress
effort_estimate: L
trigger: "El operador decide vaciar el backlog de follow-ups ANTES del publish real (T060). El publish es irreversible: lo que quede mal documentado o sin cablear se congela en un paquete público. Los 4 follow-ups accionables (FU-017/018/019/020) resultaron ser la misma clase — el anti-patrón que CHARTER-11 destapó — y la investigación previa encontró que el repo afirma que existen verificaciones que NO existen."
originating_spec: specs/001-weft-crdt-versioning/spec.md
work_verb: implement
design_provenance: new
---

# Charter: Cerrar la clase «declaración de superficie sin cableado»

> **Status (mirrored from frontmatter — source of truth is above):** in-progress. Effort: L.
>
> **Origin:** Despacho del backlog de follow-ups antes de T060. Cierra FU-017, FU-018, FU-019 y FU-020,
> más los hallazgos adyacentes que la investigación destapó y que ningún follow-up cubría.

## Context

CHARTER-11 nombró el anti-patrón que gobierna este Charter: *«verificación fantasma»* — una declaración
que **pasa en verde sin verificar nada**, cuyo síntoma es idéntico al del éxito y que por eso sobrevive
indefinidamente. Su retrospectivo predijo que seguirían apareciendo mientras no existiera un guard
mecánico. La predicción se cumplió de inmediato: al investigar los 4 follow-ups accionables, los cuatro
resultaron ser la misma clase, y aparecieron tres instancias más que ningún follow-up había registrado.

El hallazgo que ordena el Charter: **el repo afirma en dos sitios que existe un test de CI que no
existe.** `src/Weft.Core/Yrs/NativeMethods.cs:8` («un test de CI lo valida») y
`native/weft-yrs-ffi/include/weft_ffi.h:4-6` («Un test de CI valida que las declaraciones
`[LibraryImport]` de Weft.Core coinciden con este header») describen una verificación que nunca se
implementó. No es una verificación fantasma en el sentido de CHARTER-11 —no hay job que pase en verde—:
es peor, es una **verificación imaginaria**. Y ya hizo daño: engañó a quien redactó FU-017, que pide
«replicar para Loro el test que yrs SÍ tiene». No hay nada que replicar.

Esto lo cambia todo respecto a lo declarado en el backlog: FU-017 no es un chore de coste S; es crear
el test **por primera vez, para ambos shims**, y borrar dos afirmaciones falsas. Y llega justo a tiempo:
CHARTER-14 (siguiente en la secuencia) va a tocar exactamente esa superficie y a subir el ABI del shim de
Loro a v3.

## Scope

**In scope:**

1. **FU-017 — test de paridad header↔binding, para AMBOS shims** (`weft_ffi.h` ↔ `Weft.Core` y
   `weft_loro_ffi.h` ↔ `Weft.Loro`): compara nombre, aridad, orden y tipos de parámetros, y retorno.
   Falla si el header declara algo que el binding no tiene, o al revés. Hoy las 15 firmas de Loro
   coinciden: el test no arregla una divergencia, **impide la próxima**.
2. **FU-017 (b)** — corregir los dos comentarios que afirman que ese test existe; pasarán a ser ciertos.
3. **FU-017 (c)** — completar el header de Loro: declarar `weft_loro_test_panic` (que `lib.rs:472`
   exporta bajo la feature) y marcar su ABI explícitamente, como hace el de yrs.
4. **Hueco de gate SC-009 (NUEVO)** — `release.yml:100` usa `grep -q weft_test_panic`, que **no caza**
   `weft_loro_test_panic`: el gate que impide que los test-hooks viajen en release **no cubre el cdylib
   de Loro**. Ampliarlo a ambos símbolos.
5. **Control positivo del gate SC-009 (NUEVO)** — hoy, si `nm`/`strings` faltasen en el runner, el script
   imprime `✓ sin weft_test_panic` y pasa en verde sin verificar nada (`release.yml:105` lo llama
   «verificación débil»). Es la clase de FU-020 **dentro del gate que debería protegernos**. Asertar que
   la herramienta existe y que detecta el símbolo en un control positivo.
6. **FU-018 — ~10 comentarios falsos u obsoletos** en `ci.yml`, `CONTRIBUTING.md:41` y `README.md`
   (inventario en `## Files to modify`). Patrón común: el comentario viejo sobrevive junto al nuevo que
   lo desmiente — varios se contradicen dentro del mismo archivo.
7. **FU-020 — guard BLOQUEANTE de filtros de test**: para cada `--filter X` documentado o usado en CI,
   `dotnet test --list-tests --filter X` debe devolver ≥1 resultado. Cubre el fantasma vivo de
   `ci.yml:227` (`FullyQualifiedName~RedisDocumentStoreContractTests`: si alguien renombra la clase, el
   job pasa verde con 0 tests).
8. **Comando roto + evidencia falsa (NUEVO)** — `quickstart.md:37` (`dotnet test tests/A tests/B` →
   MSB1008, verificado) y la evidencia de US1 en `checklists/requirements.md:45`, que afirma haber
   ejecutado ese comando con resultado 58/58. Corregir ambos.
9. **FU-019 — footgun de pack local**: documentar en `CONTRIBUTING.md` que compilar con `--target
   <triple> --features test-hooks` y empaquetar en local metería el símbolo de test en el `.nupkg` (el
   gate solo existe en CI).

**Out of scope:**

- **FU-010** → CHARTER-13 (durabilidad del relay). **FU-016** → CHARTER-14 (siembra cross-engine).
- **FU-015** — bloqueado por terceros: y-crdt#639 sigue abierto. No accionable.
- **Refactor del `pack-smoke` por-PR para que valide de verdad** — el `echo` de `ci.yml:229` es una
  decisión de coste consciente de CHARTER-07 (la matriz cross-compile es cara), ya documentada con
  honestidad en CHARTER-11. Este Charter no la revierte; solo puede reutilizar ese job como contenedor
  del guard, que es coste ≈ 0.
- **Guards para las otras sub-clases del anti-patrón** (env-vars, instrumentos, rutas HTML) — el patrón
  las sugiere, pero Weft no tiene esa superficie. Solo se cablea la sub-clase que este repo sí sufre.

## Files to modify

| File | Change |
|---|---|
| `tests/Weft.Versioning.Tests/HeaderBindingParityTests.cs` | New — paridad header↔binding para ambos shims (el proyecto ya referencia `Weft.Core` y `Weft.Loro`; evita tocar `Weft.sln`) |
| `src/Weft.Core/Yrs/NativeMethods.cs` | Corregir el comentario `:8` que afirma que existe un test de CI (pasará a ser cierto) |
| `native/weft-yrs-ffi/include/weft_ffi.h` | Corregir `:4-6` (misma afirmación falsa + csbindgen aspiracional) |
| `native/weft-loro-ffi/include/weft_loro_ffi.h` | Declarar `weft_loro_test_panic` bajo guard de feature; marcar ABI explícitamente |
| `.github/workflows/release.yml` | Gate SC-009: cazar **ambos** símbolos + control positivo (que `nm`/`strings` existan y detecten) |
| `.github/scripts/check-test-filters.sh` | New — guard de FU-020: cada `--filter` documentado casa con ≥1 test |
| `.github/workflows/ci.yml` | ~8 comentarios falsos/obsoletos + job bloqueante `test-filters` que corre el guard |
| `CONTRIBUTING.md` | `:41` paridad Yjs marcada «no-bloqueante» (es bloqueante) + footgun de pack local (FU-019) |
| `README.md` | `NOTICE` inexistente, `native/weft-ffi/` (layout muerto), ruta de `weft-design-brief.md` |
| `specs/001-weft-crdt-versioning/quickstart.md` | `:37` comando roto (MSB1008) |
| `specs/001-weft-crdt-versioning/checklists/requirements.md` | `:45` evidencia falsa de US1 |
| `.straymark/follow-ups-backlog.md` | Cierre de FU-017/018/019/020 (vía AILOG + `drift --apply`) |
| `.straymark/07-ai-audit/agent-logs/AILOG-2026-07-16-001-charter-12-cerrar-declaracion-sin-cableado.md` | New, `risk_level: low` |

## Verification

### Local checks

```bash
cargo build --release --features test-hooks --manifest-path native/Cargo.toml
dotnet build Weft.sln -c Release

# El test nuevo: paridad header↔binding en ambos shims
dotnet test tests/Weft.Versioning.Tests -c Release --filter "FullyQualifiedName~HeaderBindingParity"

# Suite completa (hoy 132/132; este Charter la sube)
dotnet test Weft.sln -c Release
cargo test --features test-hooks --manifest-path native/Cargo.toml

# El guard de FU-020 debe ejecutar >0 tests para cada filtro documentado
dotnet test tests/Weft.Core.Tests -c Release --filter Category=Concurrency
dotnet test tests/Weft.Server.Tests -c Release --filter "FullyQualifiedName~RedisDocumentStoreContractTests"

# Gate de memoria (el header de Loro cambia; el shim no debe regresar)
RUSTFLAGS="-Zsanitizer=address" cargo +nightly test --features test-hooks \
  --target x86_64-unknown-linux-gnu --manifest-path native/Cargo.toml

straymark validate --include-charters
```

### Production smoke (after deploy)

No aplica: Weft es una librería. El gate SC-009 corregido se ejercita en el **dry-run** de `release.yml`
(`workflow_dispatch`), no en un despliegue:

```bash
gh workflow run release.yml -f dry_run=true    # valida el gate ampliado sobre los 4 RIDs
```

## Risks

- **R1 — El test de paridad es un parser de C frágil y se vuelve el problema que venía a resolver**:
  probabilidad media, severidad media. Parsear C de verdad es AST y no lo hay; un regex ingenuo dará
  falsos positivos con macros, `#ifdef`, comentarios o continuaciones de línea.
  Mitigación: el parser se acota deliberadamente al subconjunto que estos dos headers usan (declaraciones
  `TYPE name(args);` de una superficie de 15 funciones escrita por nosotros), **falla ruidosamente si
  encuentra algo que no entiende** en vez de ignorarlo en silencio (ignorar = fantasma nuevo), y se
  verifica con un caso negativo: un header sintético con una firma divergente debe hacer fallar el test.
  Si el parser no puede ser honesto, mejor un test que compare **nombres exportados** (superficie) que
  uno que finja entender tipos.
- **R2 — Falsa sensación de seguridad: el test pasa pero la paridad real es semántica, no sintáctica**:
  probabilidad media, severidad media. Que `size_t` ↔ `nuint` coincidan de nombre no prueba que el
  marshalling sea correcto.
  Mitigación: el doc-comment del test declara explícitamente **qué NO cubre** (semántica, convención de
  llamada, ownership). La garantía real de esas propiedades sigue siendo ASan + los tests de
  round-trip. Un test cuyo alcance se exagera es exactamente la clase que este Charter cierra.
- **R3 — El guard de filtros no caza la clase entera, solo `--filter`**: probabilidad alta, severidad
  baja. Un `dotnet run --project X` que no existe, o un `npm run Y` inexistente, seguirían sin guard.
  Mitigación: acotar la promesa por escrito — el guard cubre **filtros de test**, que es donde el
  fantasma pasa en verde. Los comandos rotos fallan en **rojo** (como `quickstart.md:37`), que es
  molesto pero no engañoso, y por tanto de otra clase. No se anuncia como más de lo que es.
- **R4 — Corregir ~10 comentarios introduce afirmaciones nuevas igual de falsas**: probabilidad media,
  severidad alta. Es literalmente lo que pasó en CHARTER-11 (heredé `continue-on-error` de un comentario
  en vez de leer el YAML).
  Mitigación: cada comentario corregido se ancla a `archivo:línea` **leído en HEAD** —incluido el YAML,
  que fue justo donde falló la regla la vez anterior— y el conjunto pasa un pase adversarial contra el
  código antes del commit, como el que cazó las 5 afirmaciones falsas del doc de arquitectura.
- **R5 — El control positivo del gate SC-009 es difícil de montar sin un binario con el símbolo**:
  probabilidad media, severidad baja. Compilar un cdylib con `test-hooks` solo para probar el gate
  duplicaría la matriz.
  Mitigación: el control positivo no necesita el cdylib real — basta verificar que la herramienta existe
  (`command -v nm`) y que detecta un símbolo conocido **presente** en el mismo binario (p. ej.
  `weft_abi_version`). Si no detecta lo que sí está, el gate está roto y debe fallar.
- **R6 — Tocar el header de Loro colisiona con CHARTER-14**, que sube su ABI a v3: probabilidad alta,
  severidad baja.
  Mitigación: es la razón del orden 12 → 14. Este Charter **no** toca el valor del ABI; solo declara el
  símbolo de test y documenta la versión vigente (v2). CHARTER-14 la sube y el test de paridad, ya
  existente, valida ese cambio — que es exactamente para lo que se construye ahora.

## Tasks

1. Sync main, branch `charter/12-cerrar-declaracion-sin-cableado`.
2. FU-017: test de paridad (con su caso negativo, R1) + corregir los 2 comentarios + completar el header
   de Loro.
3. Gate SC-009: ambos símbolos + control positivo.
4. FU-020: guard bloqueante de filtros.
5. FU-018: los ~10 comentarios, anclados a HEAD (R4).
6. Comando roto de `quickstart.md:37` + evidencia falsa de US1 en `requirements.md:45`.
7. FU-019: footgun de pack local en `CONTRIBUTING.md`.
8. Pase adversarial de las correcciones contra el código (R4).
9. AILOG (`risk_level: low`) + `followups drift --apply`.
10. Verificación local limpia + `straymark charter drift CHARTER-12 --range origin/main..HEAD`.
11. Commit + push + PR.

## Charter Closure

1. **Atomic update (format v4)**: si el drift check reporta deriva no capturada en el AILOG, editar
   `## Files to modify` y/o añadir `## Closing notes` **en este mismo PR**.
2. **Post-merge drift check**: `straymark charter drift CHARTER-12 --range origin/main..HEAD`.
3. **Status frontmatter** `in-progress` → `closed` + `closed_at`.
4. Al cerrar, `straymark followups status` debe bajar de 7 open a 3 (FU-010, FU-015, FU-016).
5. **No borrar** este archivo.
