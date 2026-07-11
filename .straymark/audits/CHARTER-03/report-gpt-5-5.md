---
audit_role: auditor
auditor: gpt-5-5
charter_id: CHARTER-03-concurrencia-broker-actor-canal-y-ciclo-de-vida-a
git_range: "origin/main..HEAD"
prompt_used: audit-prompt.md
audited_at: 2026-07-11
findings_total: 4
findings_by_category:
  hallucination: 0
  implementation_gap: 1
  real_debt: 3
  false_positive: 0
evidence_citations: 95
audit_quality: high
---

# Auditoría: CHARTER-03-concurrencia-broker-actor-canal-y-ciclo-de-vida-a por gpt-5-5

## Executive summary

The implementation covers the declared M1 surface: options, actor, broker, session facade, unit tests, load-test project, CI nightly job, solution wiring, task markers, and a high-risk AILOG are all present and were read against the Charter scope. The core single-reader serialization design is implemented by an unbounded channel with `SingleReader = true` in `src/Weft.Core/Concurrency/DocumentActor.cs:46`, operations are funneled through `DocumentSession` methods such as `src/Weft.Core/Concurrency/DocumentSession.cs:32`, and the load harness exercises many documents through `tests/Weft.LoadTest/Program.cs:71`.

The audit found no hallucinated APIs, but it found four lifecycle defects/gaps. The most material issue is that `DocumentBroker.DisposeAsync` does not wait for in-flight `OpenAsync` loads, so a document created after disposal starts can be released asynchronously after `DisposeAsync` returns, violating the Charter's deterministic release requirement in `.straymark/charters/03-concurrencia-broker-actor-canal-y-ciclo-de-vida-a.md:40`.

## Compilation and test verification

Skipped. The audit prompt requires read-only behavior and forbids commands with repository mutation side effects. `dotnet test`/`dotnet run` would write build outputs, so this audit is based on static source, contract, and test inspection.

## Task-by-task traceability

### T036 — DocumentBrokerOptions

- **File(s)**: `src/Weft.Core/Concurrency/DocumentBrokerOptions.cs:8`, `src/Weft.Core/Concurrency/DocumentBrokerOptions.cs:14`, `src/Weft.Core/Concurrency/DocumentBrokerOptions.cs:20`, `src/Weft.Core/Concurrency/DocumentBrokerOptions.cs:28`
- **Status**: Implemented
- **Verification**:
  - Implementation read: Yes
  - Flow traced: options -> `DocumentBroker` constructor at `src/Weft.Core/Concurrency/DocumentBroker.cs:28` -> sweeper interval at `src/Weft.Core/Concurrency/DocumentBroker.cs:170` -> actor eviction hook at `src/Weft.Core/Concurrency/DocumentBroker.cs:152`
  - Tests found: `tests/Weft.Core.Tests/DocumentBrokerTests.cs`, 2 lifecycle cases read at `tests/Weft.Core.Tests/DocumentBrokerTests.cs:59` and `tests/Weft.Core.Tests/DocumentBrokerTests.cs:95`
- **Findings**: F1, F4.

### T037 — DocumentActor

- **File(s)**: `src/Weft.Core/Concurrency/DocumentActor.cs:27`, `src/Weft.Core/Concurrency/DocumentActor.cs:46`, `src/Weft.Core/Concurrency/DocumentActor.cs:95`, `src/Weft.Core/Concurrency/DocumentActor.cs:154`
- **Status**: Partial
- **Verification**:
  - Implementation read: Yes
  - Flow traced: session enqueue -> `DocumentActor.EnqueueAsync` at `src/Weft.Core/Concurrency/DocumentActor.cs:80` -> run loop at `src/Weft.Core/Concurrency/DocumentActor.cs:109` -> finalization at `src/Weft.Core/Concurrency/DocumentActor.cs:154`
  - Tests found: `tests/Weft.Core.Tests/DocumentBrokerTests.cs`, serialization and fault cases read at `tests/Weft.Core.Tests/DocumentBrokerTests.cs:17` and `tests/Weft.Core.Tests/DocumentBrokerTests.cs:122`
- **Findings**: F1, F3.

### T038 — DocumentBroker

- **File(s)**: `src/Weft.Core/Concurrency/DocumentBroker.cs:15`, `src/Weft.Core/Concurrency/DocumentBroker.cs:47`, `src/Weft.Core/Concurrency/DocumentBroker.cs:191`, `src/Weft.Core/Concurrency/DocumentBroker.cs:263`
- **Status**: Partial
- **Verification**:
  - Implementation read: Yes
  - Flow traced: `OpenAsync` single-flight load at `src/Weft.Core/Concurrency/DocumentBroker.cs:87` -> load/register at `src/Weft.Core/Concurrency/DocumentBroker.cs:143` -> eviction tracking at `src/Weft.Core/Concurrency/DocumentBroker.cs:228` -> dispose drain at `src/Weft.Core/Concurrency/DocumentBroker.cs:284`
  - Tests found: `tests/Weft.Core.Tests/DocumentBrokerTests.cs`, eviction and dispose cases read at `tests/Weft.Core.Tests/DocumentBrokerTests.cs:59` and `tests/Weft.Core.Tests/DocumentBrokerTests.cs:163`
- **Findings**: F1, F2, F4.

### T039 — DocumentSession

- **File(s)**: `src/Weft.Core/Concurrency/DocumentSession.cs:10`, `src/Weft.Core/Concurrency/DocumentSession.cs:32`, `src/Weft.Core/Concurrency/DocumentSession.cs:84`, `src/Weft.Core/Concurrency/DocumentSession.cs:97`, `src/Weft.Core/Concurrency/DocumentSession.cs:112`
- **Status**: Implemented with event-isolation debt
- **Verification**:
  - Implementation read: Yes
  - Flow traced: public facade -> actor enqueue at `src/Weft.Core/Concurrency/DocumentSession.cs:38` -> actor mutation and notification at `src/Weft.Core/Concurrency/DocumentActor.cs:126` and `src/Weft.Core/Concurrency/DocumentActor.cs:194`
  - Tests found: `tests/Weft.Core.Tests/DocumentBrokerTests.cs`, FIFO and disposed-session cases read at `tests/Weft.Core.Tests/DocumentBrokerTests.cs:40` and `tests/Weft.Core.Tests/DocumentBrokerTests.cs:152`
- **Findings**: F3.

### T040 — DocumentBrokerTests

- **File(s)**: `tests/Weft.Core.Tests/DocumentBrokerTests.cs:13`, `tests/Weft.Core.Tests/DocumentBrokerTests.cs:17`, `tests/Weft.Core.Tests/DocumentBrokerTests.cs:59`, `tests/Weft.Core.Tests/DocumentBrokerTests.cs:122`, `tests/Weft.Core.Tests/DocumentBrokerTests.cs:163`
- **Status**: Partial
- **Verification**:
  - Implementation read: Yes
  - Flow traced: tests cover basic serialization, FIFO, successful persistence/reopen, fault propagation, and dispose-after-open.
  - Tests found: this is the test file; 7 cases read.
- **Findings**: F2 is not covered because the dispose test only disposes after `OpenAsync` completed at `tests/Weft.Core.Tests/DocumentBrokerTests.cs:167`; F1 is not covered because the successful eviction test uses a non-throwing hook at `tests/Weft.Core.Tests/DocumentBrokerTests.cs:68`; F3 is not covered because no test subscribes `UpdateApplied`.

### T041 — Weft.LoadTest

- **File(s)**: `tests/Weft.LoadTest/Program.cs:11`, `tests/Weft.LoadTest/Program.cs:25`, `tests/Weft.LoadTest/Program.cs:71`, `tests/Weft.LoadTest/Program.cs:117`, `tests/Weft.LoadTest/Program.cs:140`
- **Status**: Implemented
- **Verification**:
  - Implementation read: Yes
  - Flow traced: load-test options -> concurrent workers -> consistency scan -> memory assertion.
  - Tests found: executable harness rather than xUnit; final pass/fail gate read at `tests/Weft.LoadTest/Program.cs:152`
- **Findings**: The harness does not exercise failing persistence hooks, dispose during in-flight load, or `UpdateApplied` handlers; those are unit-level gaps rather than a load harness failure.

### T042 — CI load-test job

- **File(s)**: `.github/workflows/ci.yml:3`, `.github/workflows/ci.yml:198`, `.github/workflows/ci.yml:203`, `.github/workflows/ci.yml:222`
- **Status**: Implemented
- **Verification**:
  - Implementation read: Yes
  - Flow traced: schedule/manual triggers -> `load-test` job conditional -> Rust shim build -> `dotnet run` load harness.
  - Tests found: CI invokes `tests/Weft.LoadTest` directly at `.github/workflows/ci.yml:224`
- **Findings**: None.

## Findings

### Critical (block Charter closure)

| # | Finding | File:Line | Category | Evidence | Suggested remediation |
|---|---------|-----------|----------|----------|----------------------|
| - | None. | - | - | - | - |

### High (security or logic bugs)

| # | Finding | File:Line | Category | Evidence | Suggested remediation |
|---|---------|-----------|----------|----------|----------------------|
| F1 | Failed `OnEvicting` is swallowed after the actor has already been removed from the broker, so acknowledged edits can be discarded silently on eviction. | `src/Weft.Core/Concurrency/DocumentActor.cs:161` | real_debt | The contract says eviction drains, invokes `OnEvicting`, then releases the doc at `specs/001-weft-crdt-versioning/contracts/core-api.md:156`, and the Charter defines the hook as pre-eviction persistence at `.straymark/charters/03-concurrencia-broker-actor-canal-y-ciclo-de-vida-a.md:35`. The broker removes the actor before starting eviction at `src/Weft.Core/Concurrency/DocumentBroker.cs:228`, while `FinalizeAsync` catches and ignores any hook exception at `src/Weft.Core/Concurrency/DocumentActor.cs:166`; `EvictActorAsync` also swallows actor eviction failures at `src/Weft.Core/Concurrency/DocumentBroker.cs:249`. The success test uses a hook that cannot fail at `tests/Weft.Core.Tests/DocumentBrokerTests.cs:68`. | Decide the contract for persistence failure. Prefer keeping the actor registered or marking eviction failed until persistence succeeds; at minimum surface the hook exception and prevent reopening from stale loader state as if eviction succeeded. |
| F2 | `DisposeAsync` does not wait for in-flight document loads, so deterministic release is not guaranteed when disposal races with `OpenAsync`. | `src/Weft.Core/Concurrency/DocumentBroker.cs:263` | implementation_gap | The Charter requires `DisposeAsync` to drain and release all documents exactly once at `.straymark/charters/03-concurrencia-broker-actor-canal-y-ciclo-de-vida-a.md:40`, and the contract repeats this at `specs/001-weft-crdt-versioning/contracts/core-api.md:160`. `OpenAsync` records in-flight loads in `_loading` at `src/Weft.Core/Concurrency/DocumentBroker.cs:87`, but `DisposeAsync` only waits `_evicting` and drains `_actors` at `src/Weft.Core/Concurrency/DocumentBroker.cs:284`; it never waits `_loading`. If a loader resumes after `_disposed`, `LoadAndRegisterAsync` creates an actor at `src/Weft.Core/Concurrency/DocumentBroker.cs:150`, calls `actor.BeginEvictionAsync()` fire-and-forget at `src/Weft.Core/Concurrency/DocumentBroker.cs:158`, then throws. The existing dispose test opens the session before disposal at `tests/Weft.Core.Tests/DocumentBrokerTests.cs:167`, so it does not cover this race. | Track and await `_loading` during disposal, or make disposal cancel/await all load tasks and synchronously await the actor eviction path for any actor created during shutdown before `DisposeAsync` returns. |

### Medium (inconsistencies, minor risks)

| # | Finding | File:Line | Category | Evidence | Suggested remediation |
|---|---------|-----------|----------|----------|----------------------|
| F3 | A user `UpdateApplied` handler can fault the whole actor after a successful mutation, coupling event-consumer failures to document lifecycle. | `src/Weft.Core/Concurrency/DocumentActor.cs:194` | real_debt | `DocumentSession.UpdateApplied` is public API at `src/Weft.Core/Concurrency/DocumentSession.cs:29` and the Charter delivers it as M2 relay surface at `.straymark/charters/03-concurrencia-broker-actor-canal-y-ciclo-de-vida-a.md:43`. The actor invokes `NotifySessions(delta)` inside the same `try` block as document mutation at `src/Weft.Core/Concurrency/DocumentActor.cs:121`; `NotifySessions` calls `s.RaiseUpdateApplied(mem)` synchronously at `src/Weft.Core/Concurrency/DocumentActor.cs:206`; `RaiseUpdateApplied` directly invokes subscriber delegates at `src/Weft.Core/Concurrency/DocumentSession.cs:106`. Any thrown handler exception is caught by the actor's broad catch at `src/Weft.Core/Concurrency/DocumentActor.cs:138`, setting `_state = Faulted` at `src/Weft.Core/Concurrency/DocumentActor.cs:143` even though the CRDT operation itself already completed. | Isolate event dispatch from actor faulting: catch/log subscriber exceptions, dispatch outside the actor turn, or define an explicit failure policy that cannot transform a listener bug into a document fault. Add a regression test with a throwing `UpdateApplied` handler. |
| F4 | Cancellation from the first `OpenAsync` caller is shared through the single-flight load task, so one caller's cancellation can fail other concurrent opens for the same document. | `src/Weft.Core/Concurrency/DocumentBroker.cs:93` | real_debt | `OpenAsync` stores one `Task<DocumentActor>` per `docId` in `_loading` at `src/Weft.Core/Concurrency/DocumentBroker.cs:87`. The task is created with the initiating caller's `ct` at `src/Weft.Core/Concurrency/DocumentBroker.cs:93` and that token is passed to the loader at `src/Weft.Core/Concurrency/DocumentBroker.cs:150`. Other callers reuse the same pending task at `src/Weft.Core/Concurrency/DocumentBroker.cs:87` and await it at `src/Weft.Core/Concurrency/DocumentBroker.cs:109`, so if the first caller cancels, unrelated waiters observe the same canceled/failed task. The API exposes per-call cancellation at `specs/001-weft-crdt-versioning/contracts/core-api.md:113`, which implies cancellation should not poison the shared load for callers that did not cancel. | Decouple shared load lifetime from individual caller cancellation. Use an internal broker load token, and apply each caller's token only while awaiting the shared task; if a load fails due to caller cancellation, remove it and let non-canceled waiters retry. |

### Low (quality, naming, style improvements)

| # | Finding | File:Line | Category | Evidence | Suggested remediation |
|---|---------|-----------|----------|----------|----------------------|
| - | None. | - | - | - | - |

## Out-of-scope notes

| Observation | Relevant Charter / area | Note |
|-------------|-------------------------|------|
| The decoder hardening for untrusted network input remains explicitly out of scope. | M2 / FU-002 | The Charter excludes network decoder hardening at `.straymark/charters/03-concurrencia-broker-actor-canal-y-ciclo-de-vida-a.md:61`; this audit did not treat it as a CHARTER-03 defect. |

## Charter closure assessment

Partial. The task files exist and the main happy-path implementation matches the declared M1 scope: task markers are checked at `specs/001-weft-crdt-versioning/tasks.md:100`, the broker/session/actor files are present at `.straymark/charters/03-concurrencia-broker-actor-canal-y-ciclo-de-vida-a.md:73`, and the CI nightly load-test job is defined at `.github/workflows/ci.yml:203`. However, the closure criterion requires external audit findings classified as real debt to be remediated before close at `.straymark/charters/03-concurrencia-broker-actor-canal-y-ciclo-de-vida-a.md:155`. F1 and F2 directly affect the Charter's lifecycle and deterministic-release guarantees, so this implementation should not be closed until those paths are fixed or explicitly accepted with a follow-up and calibrated closure decision.

## Conclusion

The Charter is substantially implemented, but not clean for closure. The actor/channel serialization path is present, yet eviction and shutdown error paths still have data-loss and deterministic-release gaps that the existing tests do not exercise. Remediate F1/F2 first, then add targeted regression tests for persistence failure, dispose-during-load, and throwing `UpdateApplied` subscribers.
