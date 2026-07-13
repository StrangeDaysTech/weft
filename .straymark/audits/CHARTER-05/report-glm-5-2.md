---
audit_role: auditor
auditor: glm-5-2
charter_id: CHARTER-05-weft-server-relay-end-to-end-cierra-m2-us3
git_range: "origin/main..HEAD"
prompt_used: audit-prompt.md
audited_at: 2026-07-13
findings_total: 4
findings_by_category:
  hallucination: 0
  implementation_gap: 0
  real_debt: 3
  false_positive: 1
evidence_citations: 34
audit_quality: high
---

# Audit: CHARTER-05-weft-server-relay-end-to-end-cierra-m2-us3 by glm-5-2

## Executive summary

The implementation delivers the relay end-to-end that CHARTER-05 declared: a connection handler (`WeftConnection`) that does the y-sync handshake, relays updates via `DocumentSession` (actor turn, P-V), persists via `IDocumentStore`, broadcasts awareness, and enforces close codes (1008/1002/1009); DI extensions (`AddWeftServer`/`MapWeft`) that fail at startup without `IWeftAuthorizer` (SC-010); the `IWeftServer` service with `PublishAsync` parity (P-III); 7 integration tests; and samples with a headless wire-compat check using real `yjs`/`y-websocket`. Build is clean (0 warnings, 0 errors) and all 107 tests pass. The wire-compat risk (R1) is retired by the headless check.

The most material finding is that `ReadOnly` enforcement closes connections for `SyncStep2` sent during the standard y-websocket handshake — the server sends `SyncStep1` first, the client must respond with `SyncStep2`, and the server closes 1008 for that message. This makes `ReadOnly` mode incompatible with standard `y-websocket` clients. The test passes but for the wrong reason (the handshake `SyncStep2` triggers 1008, not the explicit `EditAsync` `Update`). Two additional `real_debt` findings concern the apply-before-persist ordering (persistence outside the actor turn, contradicting the Charter Context's stated invariant) and a shutdown race between `DisposeAsync` and `LeaveAsync`.

## Compilation and test verification

```
$ dotnet build Weft.sln -c Release
Compilación correcta.
    0 Advertencia(s)
    0 Errores
Tiempo transcurrido 00:00:02.08

$ dotnet test Weft.sln -c Release --no-build
Correctas! - Con error:     0, Superado:    25, Omitido:     0, Total:    25 — Weft.Versioning.Tests
Correctas! - Con error:     0, Superado:     2, Omitido:     0, Total:     2 — Weft.Determinism.Tests
Correctas! - Con error:     0, Superado:    53, Omitido:     0, Total:    53 — Weft.Server.Tests
Correctas! - Con error:     0, Superado:    27, Omitido:     0, Total:    27 — Weft.Core.Tests
```

107 tests pass (Server 53, Core 27, Versioning 25, Determinism 2), matching the AILOG's claim.

## Task-by-task traceability

### T047 — Connection handler (`WeftConnection.cs`)

- **File(s)**: `src/Weft.Server/WeftConnection.cs:1-230`, `src/Weft.Server/DocumentHub.cs:1-117`, `src/Weft.Server/Protocol/AwarenessProtocol.cs:1-70`
- **Status**: Implemented (with findings)
- **Verification**:
  - Implementation read: Yes — all three files read in full.
  - Flow traced: `MapWeft` (endpoint) → `WeftServer.HandleConnectionAsync` (`WeftServer.cs:83`) → `WeftConnection.RunAsync` → `ReceiveLoopAsync` → `DispatchAsync` → `DocumentHub.ApplyAndPersistAsync` → `Session.ApplyUpdateAsync` (actor turn) → `OnUpdateApplied` → `Broadcast` → `TryEnqueue` (send pump).
  - Tests found: `RelayTests.cs` — 7 test cases (convergence, delta, Deny, ReadOnly→1008, awareness, restart-recovery, publish parity).
- **Findings**:
  - **ReadOnly closes for SyncStep2 during handshake** — see Finding M-1 below.
  - `Deny`→403 is correctly in `MapWeft` before upgrade (`WeftServerExtensions.cs:71`), not in `WeftConnection`. The AILOG documents this attribution correction.
  - Sync bidirectional: server sends `SyncStep1(serverSv)` first (`WeftConnection.cs:57`), responds to client's `SyncStep1` with `SyncStep2(delta)` (`WeftConnection.cs:103-105`). ✅
  - Relay+persist: `Update`/`Step2` from `ReadWrite` → `hub.ApplyAndPersistAsync` (`WeftConnection.cs:116`). See Finding M-2 for ordering concern.
  - Awareness: tracked via `AwarenessProtocol.TrackClients` and broadcast to peers excluding origin (`WeftConnection.cs:119`). Withdrawal via `WeftServer.LeaveAsync` (`WeftServer.cs:93`). ✅
  - Close codes: malformed→1002 (`WeftConnection.cs:93`), ReadOnly→1008 (`WeftConnection.cs:110`), oversized→1009 (`WeftConnection.cs:183`). ✅
  - FU-002 part b: bounded send queue with backpressure close (`WeftConnection.cs:39`, `WeftConnection.cs:64-67`). Receive-side cap via `MaxMessageBytes` (`WeftConnection.cs:181`). ✅

### T048 — DI + endpoint (`WeftServerExtensions.cs` + `WeftServerOptions.cs`)

- **File(s)**: `src/Weft.Server/WeftServerExtensions.cs:1-80`, `src/Weft.Server/WeftServerOptions.cs:1-33`
- **Status**: Implemented
- **Verification**:
  - Implementation read: Yes.
  - Flow traced: `AddWeftServer` registers `WeftServer` singleton + `IWeftServer` (`WeftServerExtensions.cs:30-33`); `MapWeft` validates `IWeftAuthorizer` via `IServiceProviderIsService` (`WeftServerExtensions.cs:51`), maps `path/{docId}` (`WeftServerExtensions.cs:60`).
  - Tests found: `RelayTests.cs` — `BuildHostAsync` exercises `AddWeftServer` + `MapWeft`; the `Deny` test verifies the 403 path; the `ReadOnly` test verifies the 1008 path.
- **Findings**: None. The fail-at-startup check (`WeftServerExtensions.cs:50-57`) correctly throws `InvalidOperationException` if `IWeftAuthorizer` is not registered. The `Deny`→403 before upgrade is correct (`WeftServerExtensions.cs:71`). `WeftServerOptions` provides `Engine` (default `YrsEngine.Instance`), `Broker` (`DocumentBrokerOptions`), `MaxMessageBytes` (16 MiB), `MaxSendQueuePerConnection` (256). All match the contract.

### T049 — `IWeftServer` service (`WeftServer.cs`)

- **File(s)**: `src/Weft.Server/WeftServer.cs:1-219`
- **Status**: Implemented
- **Verification**:
  - Implementation read: Yes.
  - Flow traced: `PublishAsync` (`WeftServer.cs:141`) → `_broker.OpenAsync(docId, LoadDocStateAsync, ct)` → `session.ExecuteAsync(doc => doc.ExportState(), ct)` (within actor turn) → `VersionId.FromBlob(snapshot)` → `_blobStore.PutAsync(id, snapshot, ct)`.
  - Cross-referenced against `VersionStore.PublishAsync` (`VersionStore.cs:24-30`): both call `ExportState()` → `VersionId.FromBlob()` → `PutAsync()`. Parity holds by construction — `ExportState()` is the same deterministic operation (P-III), and `VersionId.FromBlob` uses `SHA256.HashData` (`VersionId.cs:17`). ✅
  - `GetConnectionCountAsync` returns hub connection count or 0 (`WeftServer.cs:167`). ✅
  - `DisconnectAllAsync` calls `hub.DisconnectAll()` which cancels each connection's CTS (`WeftServer.cs:175`). ✅
  - Tests found: `Server_publish_matches_local_publish_version_id` — verifies `localId == serverId`. `State_survives_a_server_restart` — verifies recovery from store. ✅
- **Findings**: See Finding L-1 for the shutdown race in `DisposeAsync`.

### T051 — Integration tests (`RelayTests.cs`)

- **File(s)**: `tests/Weft.Server.Tests/RelayTests.cs:1-467`
- **Status**: Implemented
- **Verification**:
  - Implementation read: Yes — all 7 tests read.
  - Tests cover: convergence after cross edits (SC-005), delta vs full state with bytes measured (SC-004), Deny→0 bytes, ReadOnly→1008 (SC-010), awareness relay+withdrawal, restart-recovery (SC-006), publish parity (P-III). All pass.
- **Findings**:
  - The `ReadOnly_client_that_writes_is_closed_with_policy_violation` test passes but for the wrong reason — see Finding M-1 evidence.
  - The `State_survives_a_server_restart` test only covers clean shutdown (`DisposeAsync` consolidates a snapshot). It does not test a mid-operation crash. See Finding M-2.

### T052 — Samples + wire-compat validation

- **File(s)**: `samples/Weft.Sample.Server/Program.cs:1-33`, `samples/Weft.Sample.Server/Weft.Sample.Server.csproj:1-33`, `samples/tiptap-client/wire-check.mjs:1-57`, `samples/tiptap-client/src/main.js:1-31`
- **Status**: Implemented
- **Verification**:
  - Implementation read: Yes — all sample files read.
  - Sample server: `Program.cs` registers `AddWeftServer` + `DemoAuthorizer` (ReadWrite to all) + `FileSystemDocumentStore`. Correct — `DemoAuthorizer` comment says "NO usar en producción". ✅
  - Wire-check: `wire-check.mjs` creates two real `Y.Doc` instances, connects via `y-websocket`, edits in both, checks convergence. This proves yrs↔Yjs binary compatibility. ✅
  - Tiptap client: `main.js` uses `Tiptap` + `y-prosemirror` + `y-websocket` against the relay. Provided for manual browser validation. ✅
- **Findings**: None. The headless wire-check is a stronger gate than a browser test for wire compat — it's reproducible and doesn't require human interaction. The AILOG documents this substitution clearly.

### Project references (`.csproj`)

- **File(s)**: `src/Weft.Server/Weft.Server.csproj:1-20`, `tests/Weft.Server.Tests/Weft.Server.Tests.csproj`, `Weft.sln`
- **Status**: Implemented
- **Verification**: `Weft.Server.csproj` adds `ProjectReference` to `Weft.Core` and `Weft.Versioning` (`Weft.Server.csproj:13-16`). Tests add `Microsoft.AspNetCore.TestHost` and `FrameworkReference Microsoft.AspNetCore.App`. Solution adds `Weft.Sample.Server`. ✅
- **Findings**: See Finding L-2 (false positive — `TestHost` vs declared `Mvc.Testing`).

## Findings

### Critical (block Charter closure)

| # | Finding | File:Line | Category | Evidence | Suggested remediation |
|---|---------|-----------|----------|----------|----------------------|
| — | (none) | — | — | — | — |

### High (security or logic bugs)

| # | Finding | File:Line | Category | Evidence | Suggested remediation |
|---|---------|-----------|----------|----------|----------------------|
| — | (none) | — | — | — | — |

### Medium (inconsistencies, minor risks)

| # | Finding | File:Line | Category | Evidence | Suggested remediation |
|---|---------|-----------|----------|----------|----------------------|
| M-1 | ReadOnly mode closes connections for `SyncStep2` sent during the standard y-websocket handshake — making ReadOnly incompatible with standard `y-websocket` clients | `src/Weft.Server/WeftConnection.cs:57` (server sends `SyncStep1` first), `src/Weft.Server/WeftConnection.cs:108-116` (`Step2`/`Update` → 1008 for non-ReadWrite) | real_debt | The server always sends `SyncStep1(serverSv)` first (`WeftConnection.cs:57`). In the standard y-websocket protocol (`y-protocols/sync.js` `readSyncStep1`), the client MUST respond to `SyncStep1` with `SyncStep2(encodeStateAsUpdate(doc, sv))` — even if the delta is empty. The server's `DispatchAsync` treats `SyncStep2` identically to `Update` for access control (`WeftConnection.cs:108`: `case MessageType.Sync: // Step2 o Update`), so a `ReadOnly` client's `SyncStep2` triggers 1008 at `WeftConnection.cs:110-112`. The test `ReadOnly_client_that_writes_is_closed_with_policy_violation` (`RelayTests.cs:229-240`) passes because the `YClient`'s `Dispatch` method sends `SyncStep2` in response to the server's `SyncStep1` (`RelayTests.cs:139-141`), which triggers 1008 before — or independently of — the `EditAsync(0, "nope")` `Update` (`RelayTests.cs:236`). The test does not verify which message triggered the close. The contract (`contracts/server-api.md` §Autorización) says `ReadOnly` should "recibe sync/updates/awareness" — the client receives the initial `SyncStep2` from the server but is then disconnected and cannot receive subsequent updates. Calibration: Medium — does not trigger under the `DemoAuthorizer` (ReadWrite to all, `Program.cs:19-21`) or the wire-check (ReadWrite), but is a clear code path for any consumer who configures `ReadOnly` access with standard y-websocket clients. | Either (a) skip sending `SyncStep1` to `ReadOnly` connections in `RunAsync` (they don't need to send their delta — only receive), or (b) allow `SyncStep2` from `ReadOnly` but don't apply it (or check if the delta is empty), only close for `Update` messages. Option (a) is cleanest: `ReadOnly` clients should only receive, not be asked for their delta. |
| M-2 | Apply-before-persist ordering: `AppendUpdateAsync` executes outside the actor turn, contradicting the Charter Context's stated invariant | `src/Weft.Server/DocumentHub.cs:72-76` | real_debt | The Charter Context states: "PublishAsync y AppendUpdate/SaveSnapshot ejecutan dentro del turno del actor del doc" (`charters/05-*.md` §Context). The implementation in `DocumentHub.ApplyAndPersistAsync` calls `await Session.ApplyUpdateAsync(update, ct)` (enqueued to actor; returns after the actor processes it, which fires `UpdateApplied` → `Broadcast` to peers) and THEN `await _store.AppendUpdateAsync(DocId, update, ct)` outside the actor turn (`DocumentHub.cs:74-75`). This means: (1) updates are broadcast to peers before being persisted to the store; (2) two concurrent `ApplyAndPersistAsync` calls can interleave as applyA→applyB→persistB→persistA, so the store order doesn't match the apply order. CRDT commutativity makes the store-order mismatch harmless for convergence (`DocumentStateFraming.ReadRecords` applies records in order, and CRDT updates are commutative). But a crash between `ApplyUpdateAsync` and `AppendUpdateAsync` loses the update from the store — the update is in peers' docs but not durable. CRDT self-healing (re-sync on reconnection) recovers it. The restart-recovery test (`RelayTests.cs:209-227`) only tests clean shutdown (`DisposeAsync` consolidates a snapshot at `DocumentHub.cs:105-108`), not a mid-operation crash. Calibration: Medium — self-healing recovers, clean shutdown consolidates, and the library is single-node without deployment. But the Charter's stated invariant is not implemented. | Move `AppendUpdateAsync` inside the actor turn (e.g., via `DocumentSession.ExecuteAsync` that applies + persists atomically), or document the apply-before-persist ordering as a conscious decision in the AILOG/AIDEC (like AIDEC §1 did for `PublishAsync`'s `PutAsync` outside the turn). Add a crash-recovery test that kills the server mid-operation. |

### Low (quality, naming, style improvements)

| # | Finding | File:Line | Category | Evidence | Suggested remediation |
|---|---------|-----------|----------|----------|----------------------|
| L-1 | Shutdown race: `WeftServer.DisposeAsync` disposes `_hubGate` without awaiting in-flight `LeaveAsync` completions | `src/Weft.Server/WeftServer.cs:192-201` (`DisposeAsync`), `src/Weft.Server/WeftServer.cs:96-107` (`LeaveAsync` acquires `_hubGate`) | real_debt | `DisposeAsync` calls `hub.DisconnectAll()` (`WeftServer.cs:195` — cancels connections' CTSs) but does not wait for the connections' `HandleConnectionAsync` finally blocks to call `LeaveAsync` before disposing `_hubGate` at `WeftServer.cs:201`. A `LeaveAsync` still running when `_hubGate` is disposed would throw `ObjectDisposedException` at `_hubGate.WaitAsync()` (`WeftServer.cs:96`). The `State_survives_a_server_restart` test doesn't manifest this because clients are disposed before the server (`RelayTests.cs:211-222`). Calibration: Low — only during abrupt shutdown with active connections; the exception is in a background task's finally block; the library has no deployment scenario. | Track in-flight `HandleConnectionAsync` tasks and await them in `DisposeAsync` before disposing `_hubGate`, or catch `ObjectDisposedException` in `LeaveAsync`'s `_hubGate.WaitAsync()`. |
| L-2 | Charter declares `Microsoft.AspNetCore.Mvc.Testing` (`WebApplicationFactory`) but implementation uses `Microsoft.AspNetCore.TestHost` | `charters/05-*.md` §Files to modify, `tests/Weft.Server.Tests/Weft.Server.Tests.csproj:9` | false_positive | The Charter's §Files to modify says: "`Microsoft.AspNetCore.Mvc.Testing` (WebApplicationFactory) para los tests de integración". The implementation uses `Microsoft.AspNetCore.TestHost` (`Weft.Server.Tests.csproj:9`) with `TestServer` and `HostBuilder` (`RelayTests.cs:26-44`). On closer inspection, this is a documented substitution — the AILOG's Modified Files table lists "TestHost + FrameworkReference" without mentioning `Mvc.Testing`. `TestHost` is lighter than `Mvc.Testing` and fully sufficient for the integration tests (no `Program` class partial-declaration requirement). The functional requirement (integration tests with a real in-process server) is met. | No action needed — the substitution is reasonable and documented. If the Charter is updated at close (format v4), align the §Files to modify entry. |

## Out-of-scope notes (optional)

| Observation | Relevant Charter / area | Note |
|-------------|-------------------------|------|
| `DemoAuthorizer` grants `ReadWrite` to all connections (`Program.cs:29-33`) | CHARTER-05 (T052 sample) | Sample-only; the comment correctly says "NO usar en producción". A real consumer must implement `IWeftAuthorizer` with their own identity. |
| The `YClient` simulated test client (`RelayTests.cs:77-172`) uses real yrs on both sides but doesn't exercise the full `y-websocket` client handshake (it sends `SyncStep1` on connect but doesn't send `SyncStep2` proactively — only in response to the server's `SyncStep1`). | CHARTER-05 (T051) | The wire-check.mjs headless test uses real `y-websocket` and is the actual compat gate. The `YClient` is a lighter harness for logic tests. |
| `DocumentHub.Broadcast` (`DocumentHub.cs:53-62`) has no try/catch around `TryEnqueue`, but `TryEnqueue` (`WeftConnection.cs:60-67`) is non-throwing — `TryWrite` returns false and `_cts.Cancel()` doesn't throw. The AIDEC's mention of "try/catch" per handler is conceptual isolation, not literal code. | CHARTER-05 (AIDEC §2) | Not a defect — isolation is maintained by `TryEnqueue`'s non-throwing contract. |

## Charter closure assessment

Does the implementation meet the closure criterion declared by `CHARTER-05-weft-server-relay-end-to-end-cierra-m2-us3`?

**Partial** — The Charter's closure criterion is: "Corte que cierra el hito M2 y completa el journey de aceptación de US3, además de retirar el riesgo de compat del wire y cerrar FU-002."

- **Wire compat (R1)**: Retired. The headless wire-check (`wire-check.mjs`) with real `yjs`/`y-websocket` proves yrs↔Yjs binary compatibility. The Tiptap sample is provided for manual browser validation. ✅
- **FU-002 (part b)**: Delivered. Bounded send queue with backpressure close (`WeftConnection.cs:39`, `WeftConnection.cs:64-67`), receive-side cap (`WeftConnection.cs:181`), malformed→1002, oversized→1009. ✅
- **M2 tasks (T047–T049, T051, T052)**: All implemented, all marked `[X] — CHARTER-05` in `tasks.md`. Build clean, 107 tests pass. ✅
- **ReadOnly mode (SC-010)**: The `Deny`→403 and fail-at-startup enforcement is correct. But `ReadOnly` mode is broken with standard y-websocket clients (Finding M-1) — the contract says `ReadOnly` should "recibe sync/updates/awareness" but the connection is closed during the handshake. This is a real_debt that should be remediated before or shortly after close. ⚠️
- **Persistence invariant**: The Charter Context states `AppendUpdate` executes within the actor turn, but the implementation puts it outside (Finding M-2). CRDT self-healing mitigates the impact, but the stated invariant is not met. ⚠️

The Charter can close if M-1 and M-2 are captured in the follow-ups backlog (`real_debt` → `straymark followups` registry) and remediated in a post-close amendment or a follow-up. They do not block the wire-compat gate or the core relay functionality, which work correctly for `ReadWrite` connections.

## Conclusion

CHARTER-05 delivers a working y-sync relay with correct P-V (actor turn for applies), P-III (publish parity), and FU-002 (backpressure) compliance, verified by 107 passing tests and a headless wire-compat check with real `yjs`/`y-websocket`. The most material finding is that `ReadOnly` enforcement is too aggressive — it closes connections for `SyncStep2` sent during the standard protocol handshake, making `ReadOnly` incompatible with standard y-websocket clients (M-1, Medium). The apply-before-persist ordering puts `AppendUpdate` outside the actor turn, contradicting the Charter Context's stated invariant, though CRDT self-healing mitigates the impact (M-2, Medium). Neither finding blocks the wire-compat gate or `ReadWrite` relay functionality. Recommended next step: remediate M-1 (skip `SyncStep1` for `ReadOnly` connections) and capture M-1/M-2 in the follow-ups backlog before closing the Charter.
