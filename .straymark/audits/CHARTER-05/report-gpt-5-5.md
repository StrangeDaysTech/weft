---
audit_role: auditor
auditor: gpt-5-5
charter_id: CHARTER-05-weft-server-relay-end-to-end-cierra-m2-us3
git_range: "origin/main..HEAD"
prompt_used: audit-prompt.md
audited_at: 2026-07-13
findings_total: 3
findings_by_category:
  hallucination: 0
  implementation_gap: 1
  real_debt: 2
  false_positive: 0
evidence_citations: 59
audit_quality: high
---

# Audit: CHARTER-05-weft-server-relay-end-to-end-cierra-m2-us3 by gpt-5-5

## Executive summary

The implementation largely matches CHARTER-05 scope: the relay endpoint, hub/session wiring, project references, integration tests, sample server, and Tiptap/Yjs sample are present and line up with the declared T047-T052 work. The local server test suite and full test suite passed, and the Release solution build passed when run outside the sandbox after an opaque sandbox restore failure.

I found one High implementation gap: `ReadOnly` connections appear to be closed on any sync `Step2`, including the standard handshake response to the server's initial `SyncStep1`, so the declared read-only mode is not reliably usable with standard y-sync clients. I also found two Medium real-debt issues: update broadcast/application can get ahead of durable append on store failure, and awareness tracking has an unhandled zero-clock edge case.

## Compilation and test verification

`dotnet test tests/Weft.Server.Tests/ --no-restore`

```text
Passed!  - Failed:     0, Passed:    53, Skipped:     0, Total:    53, Duration: 375 ms - Weft.Server.Tests.dll (net10.0)
```

`dotnet test --no-restore`

```text
Passed!  - Failed:     0, Passed:    53, Skipped:     0, Total:    53, Duration: 406 ms - Weft.Server.Tests.dll (net10.0)
Passed!  - Failed:     0, Passed:     2, Skipped:     0, Total:     2, Duration: 48 ms - Weft.Determinism.Tests.dll (net10.0)
Passed!  - Failed:     0, Passed:    25, Skipped:     0, Total:    25, Duration: 52 ms - Weft.Versioning.Tests.dll (net10.0)
Passed!  - Failed:     0, Passed:    27, Skipped:     0, Total:    27, Duration: 1 s - Weft.Core.Tests.dll (net10.0)
```

`dotnet build Weft.sln -c Release`

```text
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

Note: the same Release build failed inside the sandbox during restore with no MSBuild error diagnostics. Rerunning the exact command outside sandbox/network restrictions succeeded.

## Task-by-task traceability

### T047 - Connection handler

- **File(s)**: `src/Weft.Server/WeftConnection.cs:23`, `src/Weft.Server/WeftConnection.cs:65`, `src/Weft.Server/WeftConnection.cs:110`, `src/Weft.Server/DocumentHub.cs:20`, `src/Weft.Server/Protocol/AwarenessProtocol.cs:27`
- **Status**: Partial
- **Verification**:
  - Implementation read: Yes
  - Flow traced: `MapWeft` -> `WeftServer.HandleConnectionAsync` -> `DocumentHub` -> `DocumentSession` -> `IDocumentStore`
  - Tests found: `tests/Weft.Server.Tests/RelayTests.cs`, 7 relay cases
- **Findings**: High finding H1 on `ReadOnly` sync handling; Medium findings M1 and M2 on durability ordering and awareness tracking.

### T048 - DI and endpoint

- **File(s)**: `src/Weft.Server/WeftServerExtensions.cs:21`, `src/Weft.Server/WeftServerExtensions.cs:44`, `src/Weft.Server/WeftServerOptions.cs:13`
- **Status**: Implemented
- **Verification**:
  - Implementation read: Yes
  - Flow traced: service registration -> authorizer probe -> route `/collab/{docId}` -> pre-upgrade `Deny` handling
  - Tests found: `tests/Weft.Server.Tests/RelayTests.cs:39`, `tests/Weft.Server.Tests/RelayTests.cs:345`
- **Findings**: None.

### T049 - `IWeftServer`

- **File(s)**: `src/Weft.Server/WeftServer.cs:14`, `src/Weft.Server/WeftServer.cs:79`, `src/Weft.Server/WeftServer.cs:160`, `src/Weft.Server/WeftServer.cs:181`, `src/Weft.Server/WeftServer.cs:189`
- **Status**: Implemented
- **Verification**:
  - Implementation read: Yes
  - Flow traced: service -> shared `DocumentBroker` -> `DocumentSession.ExecuteAsync` -> `VersionId.FromBlob` / `IBlobStore.PutAsync`
  - Tests found: `tests/Weft.Server.Tests/RelayTests.cs:415`
- **Findings**: None.

### Project references

- **File(s)**: `src/Weft.Server/Weft.Server.csproj:7`, `src/Weft.Server/Weft.Server.csproj:13`
- **Status**: Implemented
- **Verification**:
  - Implementation read: Yes
  - Flow traced: `Weft.Server` references `Weft.Core` and `Weft.Versioning`
  - Tests found: Release solution build includes `Weft.Server`
- **Findings**: None.

### T051 - Integration tests

- **File(s)**: `tests/Weft.Server.Tests/RelayTests.cs:301`, `tests/Weft.Server.Tests/RelayTests.cs:321`, `tests/Weft.Server.Tests/RelayTests.cs:345`, `tests/Weft.Server.Tests/RelayTests.cs:357`, `tests/Weft.Server.Tests/RelayTests.cs:371`, `tests/Weft.Server.Tests/RelayTests.cs:394`, `tests/Weft.Server.Tests/RelayTests.cs:415`
- **Status**: Implemented
- **Verification**:
  - Implementation read: Yes
  - Flow traced: `TestServer` -> WebSocket clients -> `SyncProtocol` -> server relay -> Yrs-backed client docs
  - Tests found: 7 relay tests, plus server suite passed 53/53
- **Findings**: The `ReadOnly` test does not prove the connection stays open through normal sync handshake before the attempted write; see H1.

### T052 - Samples and real-client validation

- **File(s)**: `samples/Weft.Sample.Server/Program.cs:10`, `samples/Weft.Sample.Server/Program.cs:17`, `samples/tiptap-client/src/main.js:3`, `samples/tiptap-client/src/main.js:15`, `samples/tiptap-client/wire-check.mjs:6`, `samples/tiptap-client/package.json:11`
- **Status**: Implemented
- **Verification**:
  - Implementation read: Yes
  - Flow traced: sample server `AddWeftServer` + `FileSystemDocumentStore` + `MapWeft` -> Tiptap/Yjs WebsocketProvider
  - Tests found: headless `wire-check.mjs`; AILOG records execution at `.straymark/07-ai-audit/agent-logs/AILOG-2026-07-13-001-charter-05-server-relay-end-to-end.md:117`
- **Findings**: None.

## Findings

### Critical (block Charter closure)

| # | Finding | File:Line | Category | Evidence | Suggested remediation |
|---|---------|-----------|----------|----------|----------------------|
| - | None | - | - | - | - |

### High (security or logic bugs)

| # | Finding | File:Line | Category | Evidence | Suggested remediation |
|---|---------|-----------|----------|----------|----------------------|
| H1 | `ReadOnly` clients can be closed during normal y-sync handshake, before any actual write. | `src/Weft.Server/WeftConnection.cs:137` | implementation_gap | The contract says `ReadOnly` clients receive sync/updates/awareness and close 1008 only if they send a document update (`specs/001-weft-crdt-versioning/contracts/server-api.md:40`). The server sends `SyncStep1` on connect (`src/Weft.Server/WeftConnection.cs:74`), and the test client answers any received `Step1` with `SyncStep2` (`tests/Weft.Server.Tests/RelayTests.cs:239`). But `WeftConnection` treats every non-`Step1` sync message, including `Step2`, as a write and closes if access is not `ReadWrite` (`src/Weft.Server/WeftConnection.cs:137`, `src/Weft.Server/WeftConnection.cs:140`). The read-only test only waits for 1008 after calling `EditAsync` (`tests/Weft.Server.Tests/RelayTests.cs:357`) and does not assert that the connection survived the handshake first. | Permit the standard handshake `SyncStep2` when it carries no client update, or otherwise distinguish handshake response from live document updates before enforcing the `ReadOnly` write close. Add a test that a read-only standard client connects, completes initial sync, receives server updates, then closes 1008 only on a non-empty client update. |

### Medium (inconsistencies, minor risks)

| # | Finding | File:Line | Category | Evidence | Suggested remediation |
|---|---------|-----------|----------|----------|----------------------|
| M1 | Document updates can be applied and broadcast before the durable append succeeds. | `src/Weft.Server/DocumentHub.cs:69` | real_debt | The Charter and contract require each incoming update to be applied, persisted, and relayed (`.straymark/charters/05-weft-server-relay-end-to-end-cierra-m2-us3.md:55`, `specs/001-weft-crdt-versioning/contracts/server-api.md:62`). `ApplyAndPersistAsync` applies through the session and only then awaits `_store.AppendUpdateAsync` (`src/Weft.Server/DocumentHub.cs:71`, `src/Weft.Server/DocumentHub.cs:72`). The actor raises `UpdateApplied` after a mutating operation (`src/Weft.Core/Concurrency/DocumentActor.cs:122`, `src/Weft.Core/Concurrency/DocumentActor.cs:133`), and `DocumentHub.OnUpdateApplied` broadcasts immediately (`src/Weft.Server/DocumentHub.cs:78`, `src/Weft.Server/DocumentHub.cs:85`). With `FileSystemDocumentStore`, append is a real async disk write that can fail (`src/Weft.Server/Persistence/FileSystemDocumentStore.cs:93`, `src/Weft.Server/Persistence/FileSystemDocumentStore.cs:107`). A failure leaves peers with an update that may not be durable until a later snapshot/eviction, weakening SC-006 under storage failure. | Make durability ordering explicit. Either persist before broadcasting/acknowledging, add a transactional actor operation that appends before notify, or document and test the accepted behavior for append failure and crash-before-snapshot. |
| M2 | Awareness tracking can throw for a first valid zero-clock client entry. | `src/Weft.Server/Protocol/AwarenessProtocol.cs:38` | real_debt | The local awareness parser documents `clock` as a varUint with no lower bound above zero (`src/Weft.Server/Protocol/AwarenessProtocol.cs:11`). For a new `clientId` with `clock == 0`, `tracked.GetValueOrDefault(clientId)` returns `0`, the ternary false branch indexes `tracked[clientId]`, and the key is absent (`src/Weft.Server/Protocol/AwarenessProtocol.cs:38`). The catch only handles `MalformedMessageException` (`src/Weft.Server/Protocol/AwarenessProtocol.cs:41`), so this escapes `TrackClients`. `WeftConnection` calls it directly in the awareness path (`src/Weft.Server/WeftConnection.cs:148`), and `RunAsync` catches cancellation/socket exceptions but not this general exception (`src/Weft.Server/WeftConnection.cs:79`). The only relay awareness test uses clock `1` (`tests/Weft.Server.Tests/RelayTests.cs:379`), so the edge is uncovered. | Replace the assignment with a `TryGetValue` branch that inserts missing clients regardless of clock, and add an awareness test with `clock == 0` for a new client. |

### Low (quality, naming, style improvements)

| # | Finding | File:Line | Category | Evidence | Suggested remediation |
|---|---------|-----------|----------|----------|----------------------|
| - | None | - | - | - | - |

## Out-of-scope notes

| Observation | Relevant Charter / area | Note |
|-------------|-------------------------|------|
| `contracts/server-api.md` in the Charter text resolves to `specs/001-weft-crdt-versioning/contracts/server-api.md` in this repo. | Prompt wording | I treated the spec-local contract file as the truth oracle, not as a missing root-level contract. |

## Charter closure assessment

Partial. The implementation covers the declared files and tasks, the AILOG documents the intentional file drift for `DocumentHub` and `AwarenessProtocol` (`.straymark/07-ai-audit/agent-logs/AILOG-2026-07-13-001-charter-05-server-relay-end-to-end.md:146`), task markers are checked off (`specs/001-weft-crdt-versioning/tasks.md:122`), the integration tests pass, and the Release solution build passes. However, H1 is inside the declared authorization/sync scope (`.straymark/charters/05-weft-server-relay-end-to-end-cierra-m2-us3.md:52`) and means the `ReadOnly` acceptance behavior is not fully delivered.

## Conclusion

CHARTER-05 is close, but I would not close it until the `ReadOnly` handshake behavior is fixed and covered by a test that proves a read-only standard client remains connected after initial sync. The two Medium findings should be captured as follow-ups or remediated in the same pass if closure requires hard durability semantics.
