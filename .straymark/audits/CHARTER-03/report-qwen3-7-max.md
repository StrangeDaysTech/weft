---
audit_role: auditor
auditor: qwen3-7-max
charter_id: CHARTER-03-concurrencia-broker-actor-canal-y-ciclo-de-vida-a
git_range: "origin/main..HEAD"
prompt_used: audit-prompt.md
audited_at: 2026-07-11
findings_total: 5
findings_by_category:
  hallucination: 0
  implementation_gap: 0
  real_debt: 5
  false_positive: 0
evidence_citations: 41
audit_quality: high
---

# Audit: CHARTER-03-concurrencia-broker-actor-canal-y-ciclo-de-vida-a by qwen3-7-max

## Executive summary

The implementation of CHARTER-03 faithfully delivers the full scope declared in the Charter (T036–T042): `DocumentBrokerOptions`, `DocumentActor`, `DocumentBroker`, `DocumentSession`, 7 concurrency tests, a load-test harness, and a CI nightly job. All files exist, all API signatures match the frozen `core-api.md` contract, and the cross-boundary contract between `DocumentSession` and `ICrdtDoc` is correct. The build compiles with 0 warnings/0 errors and all 52 .NET tests pass (25 Core, 25 Versioning, 2 Determinism).

No hallucinations, no implementation gaps. Five `real_debt` findings were identified — two Medium severity (a `_state`/`_fault` race that can theoretically violate the "no OnEvicting on fault" contract guarantee, and an unprotected `UpdateApplied` event handler that can fault the actor), and three Low severity (dead code, missing test coverage for the event, and an O(n²) LINQ pattern in sweep). None are blockers for Charter closure; the two Medium findings should be captured in the follow-ups backlog for remediation before M2.

## Compilation and test verification

```
$ dotnet build Weft.sln -c Release
Compilación correcta.
    0 Advertencia(s)
    0 Errores

$ dotnet test tests/Weft.Core.Tests/
Pruebas totales: 25  Correcto: 25

$ dotnet test
Core: 25/25  Versioning: 25/25  Determinism: 2/2  →  52 total, all green
```

## Task-by-task traceability

### T036 — Define `DocumentBrokerOptions`

- **File(s)**: `src/Weft.Core/Concurrency/DocumentBrokerOptions.cs:1-49`
- **Status**: Implemented
- **Verification**:
  - Implementation read: Yes — full file (49 lines)
  - Flow traced: Options consumed by `DocumentBroker` constructor (`DocumentBroker.cs:44-46`) and `SweepOnceAsync` (`DocumentBroker.cs:218`), `ResolveSweepInterval` used in `SweepLoopAsync` (`DocumentBroker.cs:190`)
  - Tests found: No dedicated options test; exercised indirectly through `DocumentBrokerTests` eviction/LRU tests
- **Findings**: None. All three declared members present with correct defaults: `IdleEviction` = 5 min (`DocumentBrokerOptions.cs:14`), `MaxActiveDocuments` = 1024 (`DocumentBrokerOptions.cs:20`), `OnEvicting` (`DocumentBrokerOptions.cs:28`). Additive member `IdleSweepInterval` (`DocumentBrokerOptions.cs:34`) is permitted by `core-api.md:165` ("cambios aditivos permitidos"). `ResolveSweepInterval()` correctly clamps to [1s, 60s] (`DocumentBrokerOptions.cs:39-47`).

### T037 — Implement `DocumentActor` (internal)

- **File(s)**: `src/Weft.Core/Concurrency/DocumentActor.cs:1-259`
- **Status**: Implemented
- **Verification**:
  - Implementation read: Yes — full file (259 lines)
  - Flow traced: Constructor → `Channel.CreateUnbounded` (single-reader, `DocumentActor.cs:49-53`) → `Task.Run(RunAsync)` (`DocumentActor.cs:54`) → `RunAsync` drain loop (`DocumentActor.cs:117-149`) → `FinalizeAsync` (`DocumentActor.cs:153-174`)
  - Tests found: `DocumentBrokerTests.cs`, serialization test verifies single-reader guarantee via `PeakConcurrency == 1` assertion (`DocumentBrokerTests.cs:34`)
- **Findings**:
  1. **`_state`/`_fault` race in `FinalizeAsync`** — see Finding F1 below
  2. **`_persistOnEnd` dead code** — see Finding F3 below
  3. **`UpdateApplied` handler exception can fault actor** — see Finding F2 below

### T038 — Implement `DocumentBroker`

- **File(s)**: `src/Weft.Core/Concurrency/DocumentBroker.cs:1-316`
- **Status**: Implemented
- **Verification**:
  - Implementation read: Yes — full file (316 lines)
  - Flow traced: `OpenAsync` (`DocumentBroker.cs:57-146`) → lock on `_gate` → check `_evicting`/`_actors`/`_loading` → `LoadAndRegisterAsync` (`DocumentBroker.cs:148-169`) → session attachment (`DocumentBroker.cs:134-143`). `SweepOnceAsync` (`DocumentBroker.cs:207-258`) → idle + LRU eviction → `EvictActorAsync` (`DocumentBroker.cs:260-275`). `DisposeAsync` (`DocumentBroker.cs:278-316`) → cancel sweeper → wait in-flight evictions → evict all.
  - Tests found: `DocumentBrokerTests.cs` — eviction test (`DocumentBrokerTests.cs:62-96`), LRU test (`DocumentBrokerTests.cs:101-122`), dispose tests (`DocumentBrokerTests.cs:155-168`)
- **Findings**:
  1. **LINQ O(n²) in sweep LRU selection** — see Finding F5 below

### T039 — Implement `DocumentSession`

- **File(s)**: `src/Weft.Core/Concurrency/DocumentSession.cs:1-123`
- **Status**: Implemented
- **Verification**:
  - Implementation read: Yes — full file (123 lines)
  - Flow traced: Each method validates args synchronously (`DocumentSession.cs:33-36` for InsertTextAsync) → calls `_actor.EnqueueAsync` with lambda → `EnqueueAsync` creates `WorkItem<T>` and writes to channel (`DocumentActor.cs:88-94`). Cross-boundary contract verified: all 7 `ICrdtDoc` methods called match `ICrdtDoc.cs:7-37` signatures exactly. Defensive copies for `ReadOnlyMemory<byte>` parameters at `DocumentSession.cs:72` and `DocumentSession.cs:80`.
  - Tests found: FIFO test (`DocumentBrokerTests.cs:42-57`), dispose test (`DocumentBrokerTests.cs:155-161`)
- **Findings**:
  1. **Missing test for `UpdateApplied` event** — see Finding F4 below

### T040 — Concurrency tests `DocumentBrokerTests`

- **File(s)**: `tests/Weft.Core.Tests/DocumentBrokerTests.cs:1-268`
- **Status**: Implemented
- **Verification**:
  - Implementation read: Yes — full file (268 lines), 7 test cases
  - Tests found:
    1. `Operations_on_same_document_never_run_concurrently` (`DocumentBrokerTests.cs:17-36`) — 50 writers × 20 ops, asserts `PeakConcurrency == 1`
    2. `Operations_from_a_session_apply_in_FIFO_order` (`DocumentBrokerTests.cs:42-57`) — 10 sequential inserts, asserts order
    3. `Idle_document_is_evicted_persisted_and_can_be_reopened` (`DocumentBrokerTests.cs:62-96`) — idle eviction + reopen from persisted state
    4. `Over_capacity_evicts_least_recently_used_without_sessions` (`DocumentBrokerTests.cs:101-122`) — LRU eviction
    5. `Faulted_actor_propagates_causal_exception` (`DocumentBrokerTests.cs:127-150`) — causal exception propagation to pending + future ops
    6. `Using_a_disposed_session_throws_ObjectDisposedException` (`DocumentBrokerTests.cs:155-161`)
    7. `Operations_after_broker_dispose_fail_predictably` (`DocumentBrokerTests.cs:163-172`)
  - Tracking test engine (`DocumentBrokerTests.cs:181-268`): `TrackingDoc` with `Guarded()` method that uses `Interlocked.Increment`/`Decrement` to detect concurrent access + `SpinWait(100)` to widen the race window. Sound methodology.
- **Findings**: None. Test coverage matches the Charter's T040 description exactly.

### T041 — Load test harness `Weft.LoadTest`

- **File(s)**: `tests/Weft.LoadTest/Program.cs:1-169`, `tests/Weft.LoadTest/Weft.LoadTest.csproj:1-37`
- **Status**: Implemented
- **Verification**:
  - Implementation read: Yes — both files in full
  - Flow traced: Configurable via args (`Program.cs:11-14`) → `DocumentBroker` with aggressive idle (30ms eviction, 10ms sweep, `Program.cs:25-32`) → worker tasks open sessions, insert up to per-doc cap, then read (`Program.cs:70-101`) → consistency check reopens each doc and compares length to confirmed inserts (`Program.cs:108-120`) → exit code 0/1 (`Program.cs:148-154`)
  - Tests found: Load test itself IS the test; exit code is the assertion
  - `.csproj` correctly references `Weft.Core` and copies native lib (`Weft.LoadTest.csproj:13-36`). Server GC enabled (`Weft.LoadTest.csproj:8`).
- **Findings**: None.

### T042 — CI nightly job `load-test`

- **File(s)**: `.github/workflows/ci.yml:197-225`
- **Status**: Implemented
- **Verification**:
  - Implementation read: Yes
  - Job gated by `if: github.event_name == 'schedule' || github.event_name == 'workflow_dispatch'` (`ci.yml:203`) — correctly excludes PRs
  - Schedule: `cron: "0 6 * * *"` (`ci.yml:7`) — nightly 06:00 UTC
  - Builds native shim + runs load test with `--docs 2000 --tasks 16 --seconds 60` (`ci.yml:224-225`)
  - `workflow_dispatch` trigger present (`ci.yml:8`)
  - `Weft.LoadTest` added to `Weft.sln` (verified in diff: solution GUID `{064C3FA7-...}`)
- **Findings**: None.

## Findings

### Critical (block Charter closure)

| # | Finding | File:Line | Category | Evidence | Suggested remediation |
|---|---------|-----------|----------|----------|----------------------|
| — | None | — | — | — | — |

### High (security or logic bugs)

| # | Finding | File:Line | Category | Evidence | Suggested remediation |
|---|---------|-----------|----------|----------|----------------------|
| — | None | — | — | — | — |

### Medium (inconsistencies, minor risks)

| # | Finding | File:Line | Category | Evidence | Suggested remediation |
|---|---------|-----------|----------|----------|----------------------|
| F1 | `_state`/`_fault` race can invoke `OnEvicting` on a faulted document | `DocumentActor.cs:108-109,140-141,155` | real_debt | `BeginEvictionAsync` reads `_state` (line 108) and writes `Idle` (line 109) from the caller thread. The `catch` in `RunAsync` writes `_state = Faulted` (line 141) from the actor thread. If `BeginEvictionAsync` reads `Active` just before the catch writes `Faulted`, and then writes `Idle` after, `FinalizeAsync` (line 155) sees `_state != Faulted` → persists potentially corrupt state via `OnEvicting`. This violates the contract guarantee at `core-api.md:158` ("el doc se desaloja sin invocar OnEvicting"). Severity calibration: the race window is narrow (volatile writes near-instant on x86) and requires concurrent eviction of a faulting actor; most realistic during `DisposeAsync` which evicts all actors. Not a blocker but a real defect in the R4 mitigation. | Check `_fault is null` in `FinalizeAsync` instead of (or in addition to) `_state != Faulted`. The `_fault` field is authoritative — if non-null, the document is in an unknown state regardless of `_state`. |
| F2 | `UpdateApplied` handler exception can fault the actor | `DocumentActor.cs:181-191` | real_debt | `NotifySessions` invokes `s.RaiseUpdateApplied(mem)` (line 188) without try/catch. If a handler throws, the exception propagates into `RunAsync`'s try block (line 129), is caught by the outer catch (line 138), sets `_fault` and `_state = Faulted` — killing the document for all sessions. The XML-doc at `DocumentSession.cs:29` warns "no debe bloquear esperando otra operación" but does not warn about exception behavior. Severity calibration: no M2 consumer exists yet, so no handler will throw today. But when M2 adds a relay handler, this becomes a single-point-of-failure: one buggy handler kills the document for all sessions. | Wrap `RaiseUpdateApplied` in try/catch inside `NotifySessions` and log/swallow handler exceptions. Alternatively, document in the XML-doc that a throwing handler will fault the actor (defense-by-contract). Recommend the try/catch approach — it matches the principle that event handlers should not kill their publisher. |

### Low (quality, naming, style improvements)

| # | Finding | File:Line | Category | Evidence | Suggested remediation |
|---|---------|-----------|----------|----------|----------------------|
| F3 | `_persistOnEnd` field is dead code | `DocumentActor.cs:38,159` | real_debt | `volatile bool _persistOnEnd` is declared and initialized to `true` (line 38), checked in `FinalizeAsync` (line 159: `if (_persistOnEnd && _onEvicting is not null)`), but never set to `false` anywhere in the codebase (confirmed via grep: only 2 references). It is either vestigial from development or a placeholder for a future "skip persistence on broker dispose" feature that was not implemented. | Remove the field and the check, or implement the intended behavior (e.g., set `_persistOnEnd = false` during `DisposeAsync` to skip persistence on shutdown). |
| F4 | Missing test for `UpdateApplied` event | `DocumentBrokerTests.cs` (absent) | real_debt | The `UpdateApplied` event is a public API surface declared in T039 (`DocumentSession.cs:29-32`) and frozen in `core-api.md:140`. The implementation computes deltas (`DocumentActor.cs:124-132`) and notifies sessions (`DocumentActor.cs:181-191`), but no test verifies the event fires with correct deltas, fires for both own and imported updates, or stops firing after session disposal. T040's task description did not require it, so this is not an implementation gap — but the missing coverage leaves the delta computation path unverified. | Add a test that opens two sessions on the same doc, subscribes `UpdateApplied` on one, inserts from the other, and asserts the delta is non-empty and applicable. |
| F5 | LINQ O(n²) in sweep LRU selection | `DocumentBroker.cs:232-238` | real_debt | `toEvict.Contains(a)` inside the LINQ `Where` clause performs a linear scan of `List<DocumentActor>` for each actor in `_actors.Values`. With N actors and M already-marked-for-eviction, this is O(N×M). For the default `MaxActiveDocuments = 1024`, this is negligible, but at higher scales (thousands of actors) it becomes material. | Replace `toEvict.Contains(a)` with a `HashSet<DocumentActor>` lookup (O(1) per check) built from the eviction list before the LINQ query. |

## Out-of-scope notes (optional)

| Observation | Relevant Charter / area | Note |
|-------------|-------------------------|------|
| `DocumentBrokerOptions.IdleSweepInterval` is not in `core-api.md` | core-api.md contract / future Charters | Additive member, permitted by the contract's evolution rules (`core-api.md:165`). Not a finding, but worth noting for future contract reviews. |
| AILOG emergent risks R6/R7/R8 | CHARTER-03 AILOG | All three were discovered and fixed during implementation. The `_evicting`-await mechanism (R7 fix) at `DocumentBroker.cs:73-78,116-118` is well-designed and verified by the load test (~433k evictions, 0 inconsistencies). |
| Drift parser false positive (Weft.sln, .csproj) | `straymark charter drift` tooling | Documented in AILOG. Parser issue, not scope expansion. Same pattern as CHARTER-01. |

## Charter closure assessment

**Partial** — the implementation is complete and correct for closure, but the Charter's own closure criterion (§Charter Closure) requires:

1. ✅ Drift check (documented in AILOG, no real expansion)
2. ✅ External audit (this report + pending second auditor)
3. ⏳ `straymark charter close` (not yet executed — pending audit consolidation)

The code is ready for closure once the audit cycle completes and any `real_debt` findings are triaged into the follow-ups backlog. No blockers exist in the implementation.

## Conclusion

CHARTER-03 delivers a solid, well-tested concurrency layer that faithfully implements the actor/channel pattern for per-document serialization. The five `real_debt` findings are all non-blocking — the two Medium-severity items (F1: `_state`/`_fault` race, F2: unprotected event handler) should be captured as follow-ups and addressed before M2 introduces consumers of `UpdateApplied`. The load test and CI gate provide strong ongoing validation for SC-006. Recommended next step: run the second auditor, consolidate with `/straymark-audit-review`, and proceed to Charter closure.
