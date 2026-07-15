<!-- SPDX-License-Identifier: Apache-2.0 -->

# Gobernanza de Weft

Weft es un proyecto open-source (Apache-2.0) de [Strange Days Tech](https://strangedays.tech/es).

## Modelo

- **Mantenedor(es)**: el equipo de Strange Days Tech administra el repositorio, revisa PRs y corta releases.
- **Decisiones técnicas**: se toman por el mantenedor con base en la **constitución** del proyecto
  (`.specify/memory/constitution.md`, vinculante) y quedan registradas como **Charters** y documentos
  **AILOG/AIDEC/ADR** de [StrayMark](https://github.com/StrangeDaysTech/straymark) — el porqué de cada
  decisión es parte del repositorio, no folclore oral.
- **Alcance**: Weft es un *building block* reutilizable, no una aplicación. Las peticiones que empujen lógica
  de dominio hacia la librería se declinan por diseño (ver [`README.md`](./README.md) §"Lo que no es Weft").

## Cómo se decide un cambio

1. Discusión en un issue (para cambios sustantivos o de contrato: FFI, `IDocumentStore`, protocolo de sync).
2. Un **Charter** de StrayMark declara el alcance ex-ante (qué entra, qué no, riesgos) antes de implementar.
3. Implementación en un PR con los **6 gates** de la constitución en verde (ver [`CONTRIBUTING.md`](./CONTRIBUTING.md)).
4. Cierres de hito requieren **auditoría externa multi-modelo** (StrayMark) antes de mergear.

## Versionado y releases

- **SemVer**. La identidad de versión de un documento es el `SHA-256` de su export determinista: **un cambio
  de encoding del motor es breaking** (rompe la citabilidad de versiones previas) y sube el *major*. Ver el
  protocolo de bump del motor en [`CONTRIBUTING.md`](./CONTRIBUTING.md#protocolo-de-bump-del-motor-yrs--loro--research-r16).
- Los paquetes se publican a NuGet.org con símbolos + SourceLink desde el pipeline de release
  (`.github/workflows/release.yml`), tras verde en cross-compile + *pack-smoke* multi-RID.
- **RIDs soportados** v1: `linux-x64`, `linux-arm64`, `win-x64`, `osx-arm64`. "Soportado" = ejercitado en CI
  (P-VI).

## Seguridad

Reporta vulnerabilidades de forma privada (no en un issue público) al contacto de seguridad de
[Strange Days Tech](https://strangedays.tech/es). La memoria nativa se verifica con ASan/LSan en CI (P-II) y la
frontera FFI se fuzzea (`cargo-fuzz`); el input de red no confiable del relay tiene límites de tamaño/recursos.

### Ingesta directa de bytes CRDT no confiables (caveat R6)

El **relay** (`Weft.Server`) ya protege la ingesta de red: cap configurable de tamaño de mensaje y límites de
recursos por conexión antes de decodificar (ver `WeftServerOptions`). Si en cambio alimentas bytes CRDT **no
confiables directamente** a la API pública fuera del relay — `weft_doc_load` / `apply_update` / `export_since`,
o sus envoltorios en `Weft.Core` — replica esa defensa: **impón un cap de tamaño de entrada y un límite de
memoria del proceso** (p. ej. cgroup/contenedor).

Motivo: el decoder de `yrs` puede amplificar memoria (allocation-bomb) — pocos bytes que declaran una longitud
gigante disparan una reserva grande. `Update::decode` ya usa asignación falible (`try_reserve` → error
recuperable, no abort), por lo que `apply_update` está endurecido upstream; quedan dos sitios residuales con
`with_capacity` sin acotar (decode de *delete sets* y de *state vectors*, este último alcanzable vía
`export_since`). En `glibc` (overcommit) el efecto práctico es una reserva virtual y un **error de decode limpio**,
no un crash; el `abort` no capturable solo aparece en hosts memory-constrained duros o allocators eager. El fix
canónico vive upstream (PR de `try_reserve` a `y-crdt`); un target de fuzz de regresión rastrea el residual.

## Licencia

[Apache-2.0](./LICENSE) — permisiva, con concesión explícita de patentes. Recíproca con los motores MIT sobre
los que se apoya (`yrs`, Loro).
