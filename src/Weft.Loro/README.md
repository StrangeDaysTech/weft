# Weft.Loro

A dual-path engine adapter that runs Weft over the [Loro](https://github.com/loro-dev/loro) CRDT
engine (via the `weft-loro-ffi` shim) behind the same `ICrdtEngine` / `ICrdtDoc` abstractions — the
living proof that Weft's engine is replaceable (constitution principle P-IV).

Part of **[Weft](https://github.com/StrangeDaysTech/weft)** — real-time CRDT collaboration and
content-addressed document versioning for .NET.

## Install

```bash
dotnet add package Weft.Loro   # brings in Weft.Core
```

The native Loro shim ships in the package and is resolved per RID automatically.

## What it provides

- **`LoroEngine`** — an `ICrdtEngine` backed by Loro; the entire `Weft.Versioning` layer runs over it
  unchanged.
- **Optional `INativeVersioning`** — exposes Loro's native versioning capabilities (shallow snapshot,
  native diff/branch-merge probes) without making them a dependency of the core.

> Note: the redistributed Loro binary statically links three MPL-2.0 crates (`im`, `bitmaps`,
> `sized-chunks`). MPL-2.0 is compatible with proprietary use; see the repository's
> `THIRD-PARTY-NOTICES.md`. The default `yrs` engine (`Weft.Core`) is fully permissive.

## Links

- Repository & docs: <https://github.com/StrangeDaysTech/weft>
- Architecture: <https://github.com/StrangeDaysTech/weft/blob/main/docs/architecture.md>

Licensed under Apache-2.0.
