# Spikes de elección de core CRDT — StrangeDaysTech

Trilogía de experimentos técnicos (spikes) que fundamenta, con **código que corre y mediciones**,
la elección del motor CRDT para el versionado colaborativo del LMS. El código es **desechable**; lo
que persiste son los hallazgos, las matrices y los veredictos.

> **Principio de dependencias:** adoptar la "rueda profunda" (motor CRDT maduro en Rust) y construir
> nosotros la capa de encaje (.NET + versionado), con el motor abstraído tras `ICrdtEngine`.

| Spike | Pregunta | Veredicto |
|---|---|---|
| **[spike01/](./spike01/)** | ¿Es sólido el fundamento .NET → shim Rust propio → **yrs** (P/Invoke)? | 🟢 **VERDE** — cimiento validado |
| **[spike02/](./spike02/)** | ¿Ofrece **Loro** una ventaja decisiva sobre yrs como core? | 🟢 **Mantener yrs** · dual-path con gatillo |
| **[spike03/](./spike03/)** | ¿Cuánto cuesta construir el **versionado** sobre yrs vs primitivas nativas de Loro? | 🟢 **yrs CIERRA** |

**Conclusión de la trilogía:** se confirma **yrs** como core y se procede al brief de construcción de
la capa .NET de versionado, con el motor abstraído tras `ICrdtEngine` (validado en Spike 03) para que
Loro siga intercambiable si su gatillo estratégico se activa (ver Spike 02).

## Estructura

Cada spike es autocontenido y reproducible (shim Rust `cdylib` + consumidor .NET + hallazgos):

```
spike01/   fundamento del binding yrs (shim C-ABI propio + P/Invoke, content-addressing, memoria)
spike02/   comparación yrs vs Loro (paridad + diferenciadores + research estratégico)
spike03/   plomería de versionado (diff/branch/compactación sobre yrs vs Loro nativo) + ICrdtEngine
```

Entorno común: Rust (stable 1.96 / nightly 1.99 para ASan) · .NET SDK 10 (`net10.0`) ·
`yrs =0.27.2` · `loro =1.13.6`. Ver el README de cada spike para reproducir.
