<!-- SPDX-License-Identifier: Apache-2.0 -->

# Contributing to Weft

Thanks for your interest. Weft is a .NET library (Apache-2.0) for real-time CRDT collaboration and
content-addressed versioning over the Rust `yrs` core, via an in-house C-ABI shim.

## CLA

Every contribution requires signing the **CLA** (Contributor License Agreement) — a bot requests it
automatically on your first PR. See [`CLA.md`](./CLA.md).

## Toolchain

- **.NET SDK 10** (`net10.0`, C# 13).
- **Rust stable** (pinned in `native/rust-toolchain.toml`); `nightly` only for ASan/LSan (CI installs it).
- To cross-compile the native binaries locally (Linux RIDs): `cargo-zigbuild` + `zig` 0.15.x.
- Optional: **Node.js** for the cross-implementation determinism gate (`tests/determinism-yjs/`) and the
  Tiptap sample client.

## Build and test

```bash
# Native shim (with test-hooks for the panic-safety suite, SC-009)
cd native && cargo build --release --features test-hooks && cargo test --features test-hooks && cd ..

# Full .NET solution
dotnet build Weft.sln -c Release
dotnet test  Weft.sln -c Release        # the Redis test is skipped without WEFT_TEST_REDIS (local Valkey/Redis)
```

> **Do not pack (`dotnet pack`) from a tree built with `--features test-hooks`.** The pack reads the
> cdylib from `native/target/<triple>/release/`; if you built it with the feature (e.g. after
> `cargo build --release --target <triple> --features test-hooks`), the `.nupkg` would include the
> `weft_test_panic`/`weft_loro_test_panic` symbol. The SC-009 gate that verifies its absence only runs
> in the `release.yml` pipeline, **not** in a local pack. To publish, build the native binary
> **without** the feature (FU-019).

## Gates (constitution)

The project constitution (`.specify/memory/constitution.md`) fixes 6 **binding** principles, each with
its CI gate. A PR is not merged without them green:

| Principle | Gate |
| --- | --- |
| **P-I** Safe FFI | no panic crosses the C boundary (`catch_unwind` at every entry point) |
| **P-II** Verified memory | ASan/LSan over the Rust tests of both shims — 0 leaks / 0 double-free |
| **P-III** Determinism | reproducible encoding cross-RID + byte-identical cross-impl parity vs Yjs (**blocking** since CHARTER-09) |
| **P-IV** Replaceable engine | the versioning suite runs identically over `yrs` **and** Loro (dual-engine) |
| **P-V** Per-doc concurrency | serialized access to `ICrdtDoc`; the broker uses a single-reader actor/channel |
| **P-VI** Portability by RID | *pack-smoke* of the package on every supported RID — "supported" = exercised |

Hard rules when writing code: shim buffers freed only with `weft_buf_free` (the GC never touches native
memory); never `skip_gc`; `Weft.Versioning` does not reference `yrs`/Loro types (only the abstractions);
public API with validated `int` indices and native errors → `WeftException` hierarchy.

## Engine bump protocol (yrs / Loro) — research R16

Engine versions are **pinned exactly** (`yrs = "=0.27.2"`, `loro = "=1.13.6"`) with a versioned
`Cargo.lock` — `yrs`'s names and signatures change between minors, and the determinism gate (P-III)
requires reproducibility. To bump an engine:

1. **Dedicated branch**; update the exact pin in `native/<crate>/Cargo.toml` + `Cargo.lock`.
2. **Adjust the shim** (`native/<crate>/src/lib.rs`) to the engine's API changes. The shim isolates the
   bump: the **in-house C-ABI and the C# do not change** (that's its whole point).
3. **Run the full gates**: sanitizers (P-II), determinism cross-RID **and cross-implementation** (P-III
   — a bump can change the encoding and break the citability of prior versions), convergence, and
   dual-engine (P-IV).
4. **Merge only when green.** An encoding change is *breaking* for content-addressing → it's treated as
   such in the package's SemVer versioning.

## Workflow

Spec-driven with [GitHub Spec Kit](https://github.com/github/spec-kit) (spec → plan → tasks → implement)
and documentation governance with [StrayMark](https://github.com/StrangeDaysTech/straymark) (Charters +
AILOG/AIDEC). See [`GOVERNANCE.md`](./GOVERNANCE.md). The ✅ CLOSED decisions of the brief
(`weft-design-brief.md`) are not re-litigated.

## Reporting bugs / proposing changes

Open an issue with a minimal repro. For substantive changes, discuss the design in an issue before the
PR — contract changes (FFI, `IDocumentStore`, sync protocol) require prior agreement.
