<!-- SPDX-License-Identifier: Apache-2.0 -->

# Security Policy

Weft is an open-source (Apache-2.0) project by
[Strange Days Tech](https://strangedays.tech/en). We take the security of the native FFI boundary
and the sync relay seriously.

## Reporting a vulnerability

**Please report vulnerabilities privately — do not open a public issue.**

- Preferred: use GitHub's [private vulnerability reporting](https://github.com/StrangeDaysTech/weft/security/advisories/new)
  ("Report a vulnerability" under the repository's **Security** tab).
- Alternatively, contact the Strange Days Tech security contact via
  [strangedays.tech](https://strangedays.tech/en).

Please include a description, affected version/RID, and a minimal reproduction if possible. We aim
to acknowledge reports promptly and will keep you informed as we investigate and remediate.

## Supported versions

Weft follows SemVer. Because a document's version identity is the `SHA-256` of its deterministic
export, an engine encoding change is **breaking** (it invalidates the citability of prior versions)
and bumps the major. Security fixes target the latest released major.

## How the boundary is hardened

- **Native memory** is verified with ASan/LSan in CI (0 leaks / 0 double-frees required on every
  accepted change).
- **The FFI boundary** is fuzzed with `cargo-fuzz`; no panic is allowed to cross the C boundary
  (`catch_unwind` at every shim entry point).
- **Untrusted network input** to the relay (`Weft.Server`) is bounded: a configurable message-size
  cap and per-connection resource limits before decoding (see `WeftServerOptions`).

## Caveat: feeding untrusted CRDT bytes directly

The relay already protects network ingestion. If instead you feed **untrusted** CRDT bytes
**directly** to the public API outside the relay — `weft_doc_load` / `apply_update` /
`export_since`, or their `Weft.Core` wrappers — replicate that defense: **impose an input-size cap
and a process memory limit** (e.g. cgroup/container).

Rationale: the `yrs` decoder can amplify memory (allocation-bomb) — a few bytes declaring a huge
length trigger a large reservation. `Update::decode` already uses fallible allocation
(`try_reserve` → recoverable error, not abort), so `apply_update` is hardened. On `glibc`
(overcommit) the practical effect is a virtual reservation and a **clean decode error**, not a
crash; a non-catchable `abort` only appears on hard memory-constrained hosts or eager allocators.
The canonical fix lives upstream (a `try_reserve` PR to `y-crdt`); a regression fuzz target tracks
the residual. See `GOVERNANCE.md` for the full note.
