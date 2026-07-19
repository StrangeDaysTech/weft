<!-- SPDX-License-Identifier: Apache-2.0 -->

# Weft governance

Weft is an open-source (Apache-2.0) project by [Strange Days Tech](https://strangedays.tech/en).

## Model

- **Maintainer(s)**: the Strange Days Tech team administers the repository, reviews PRs, and cuts releases.
- **Technical decisions**: made by the maintainer based on the project **constitution**
  (`.specify/memory/constitution.md`, binding) and recorded as **Charters** and
  **AILOG/AIDEC/ADR** documents of [StrayMark](https://github.com/StrangeDaysTech/straymark) — the why
  of each decision is part of the repository, not oral folklore.
- **Scope**: Weft is a reusable *building block*, not an application. Requests that push domain logic
  into the library are declined by design (see [`README.md`](./README.md) §"What Weft is not").

## How a change is decided

1. Discussion in an issue (for substantive or contract changes: FFI, `IDocumentStore`, sync protocol).
2. A StrayMark **Charter** declares the scope ex-ante (what's in, what's out, risks) before implementing.
3. Implementation in a PR with the **6 constitutional gates** green (see [`CONTRIBUTING.md`](./CONTRIBUTING.md)).
4. Milestone closes require a **multi-model external audit** (StrayMark) before merging.

## Versioning and releases

- **SemVer**. A document's version identity is the `SHA-256` of its deterministic export: **an engine
  encoding change is breaking** (it invalidates the citability of prior versions) and bumps the
  *major*. See the engine bump protocol in
  [`CONTRIBUTING.md`](./CONTRIBUTING.md#engine-bump-protocol-yrs--loro--research-r16).
- Packages are published to NuGet.org with symbols + SourceLink from the release pipeline
  (`.github/workflows/release.yml`), after green cross-compile + *pack-smoke* multi-RID.
- **Supported RIDs** v1: `linux-x64`, `linux-arm64`, `win-x64`, `osx-arm64`. "Supported" = exercised in
  CI (P-VI).

## Security

Report vulnerabilities privately (not in a public issue) to the security contact of
[Strange Days Tech](https://strangedays.tech/en). See [`SECURITY.md`](./SECURITY.md) for the reporting
process. Native memory is verified with ASan/LSan in CI (P-II) and the FFI boundary is fuzzed
(`cargo-fuzz`); the relay's untrusted network input has size/resource limits.

### Direct ingestion of untrusted CRDT bytes (R6 caveat)

The **relay** (`Weft.Server`) already protects network ingestion: a configurable message-size cap and
per-connection resource limits before decoding (see `WeftServerOptions`). If instead you feed
**untrusted** CRDT bytes **directly** to the public API outside the relay — `weft_doc_load` /
`apply_update` / `export_since`, or their wrappers in `Weft.Core` — replicate that defense: **impose an
input-size cap and a process memory limit** (e.g. cgroup/container).

Rationale: the `yrs` decoder can amplify memory (allocation-bomb) — a few bytes declaring a huge length
trigger a large reservation. `Update::decode` already uses fallible allocation (`try_reserve` →
recoverable error, not abort), so `apply_update` is hardened upstream; two residual sites remain with
unbounded `with_capacity` (decode of *delete sets* and of *state vectors*, the latter reachable via
`export_since`). On `glibc` (overcommit) the practical effect is a virtual reservation and a **clean
decode error**, not a crash; the non-catchable `abort` only appears on hard memory-constrained hosts or
eager allocators. The canonical fix lives upstream (the `try_reserve` PR to `y-crdt`); a regression fuzz
target tracks the residual.

## License

[Apache-2.0](./LICENSE) — permissive, with an explicit patent grant. Reciprocal with the MIT engines it
builds on (`yrs`, Loro).
