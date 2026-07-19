<!-- SPDX-License-Identifier: Apache-2.0 -->

# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

Because a document's version identity is the `SHA-256` of its deterministic export, an engine
encoding change is **breaking** (it invalidates the citability of prior versions) and bumps the
major.

## [Unreleased]

First release candidate. Everything below is built and verified in CI, pending the operator-gated
publish to NuGet.org.

### Added

- **Core document & engine (`Weft.Core`).** A CRDT engine abstraction (`ICrdtEngine` / `ICrdtDoc`)
  over the Rust `yrs` core via an in-house C-ABI shim, with a deterministic native resource
  lifecycle (`SafeHandle`, explicit disposal, no GC touching native memory) and native panics
  translated to typed `WeftException`s.
- **Content-addressed versioning (`Weft.Versioning`).** Publish immutable versions identified by
  the `SHA-256` of the deterministic export; content-addressed blob store (in-memory, filesystem);
  word-level LCS text diff; branch/merge with automatic convergence; compaction that preserves all
  published versions. Engine-agnostic (no dependency on a concrete engine).
- **Concurrency & lifecycle at scale.** A document broker serializing all access per document
  (actor/channel, single-reader), with registration, reuse, and inactivity eviction.
- **Sync relay server (`Weft.Server`).** A WebSocket relay speaking the standard Yjs `y-sync`
  protocol — existing editor clients (Tiptap + `y-prosemirror`) connect without adaptation.
  Ephemeral awareness/presence; incremental reconnect via state vectors; a pluggable authorization
  extension point (`IWeftAuthorizer`); pluggable persistence adapters (in-memory, filesystem,
  EF Core relational, Redis) validated by a shared contract suite; persist-before-broadcast
  durability by default, with directory-fsync.
- **Replaceable engine (`Weft.Loro`).** A Loro adapter implementing the engine abstraction and its
  optional native versioning surface (`INativeVersioning`), kept compilable and exercised in CI as
  a continuous portability proof. Cross-engine deterministic seeding (`IDeterministicSeeding`).
- **Distribution.** NuGet packaging with native binaries for `linux-x64`, `linux-arm64`, `win-x64`,
  and `osx-arm64`, resolved automatically per platform; a release pipeline (symbols + SourceLink).
- **Quality gates in CI.** Multi-platform build/tests; ASan/LSan memory verification; `cargo-fuzz`
  fuzzing of the native boundary and convergence; an encoding-determinism gate cross-checked against
  the Yjs JS implementation.
- **Samples & docs.** `Weft.Sample.Versioning`, `Weft.Sample.Server`, and a `tiptap-client`
  (real editor + headless wire-compat check); architecture doc, per-package API overview, quickstart,
  and governance/contribution guides.

[Unreleased]: https://github.com/StrangeDaysTech/weft/commits/main
