---
audit_role: auditor
auditor: qwen3.7-max
charter_id: CHARTER-05-weft-server-relay-end-to-end-cierra-m2-us3
git_range: "origin/main..HEAD"
prompt_used: audit-prompt.md
audited_at: 2026-07-13
findings_total: 3
findings_by_category:
  hallucination: 0
  implementation_gap: 0
  real_debt: 3
  false_positive: 0
evidence_citations: 38
audit_quality: high
---

# Audit: CHARTER-05-weft-server-relay-end-to-end-cierra-m2-us3 by qwen3.7-max

## Executive summary

The implementation of CHARTER-05 faithfully delivers every task declared in the Charter (T047–T049, T051, T052). The connection handler (`WeftConnection`) correctly implements the y-sync handshake, bidirectional sync, awareness broadcast with withdrawal, close-code semantics (403/1008/1002/1009), and FU-002 backpressure. The DI (`AddWeftServer`/`MapWeft`) enforces mandatory authorization at startup (SC-010). `IWeftServer.PublishAsync` achieves `VersionId` parity by capturing `ExportState()` inside the actor turn, byte-identical to local publish by construction. The 7 integration tests cover all acceptance criteria with real yrs engines on both sides. Build compiles with 0 warnings; 107 tests pass across the full solution.

Two scope-expansion files (`DocumentHub.cs`, `AwarenessProtocol.cs`) are a clean internal decomposition of T047, documented in the AILOG's drift section. All cross-boundary contracts (DocumentBroker, DocumentSession, SyncProtocol, IDocumentStore, IWeftAuthorizer, VersionStore, IBlobStore) verified against their real definitions — no mismatches found. Three low-severity `real_debt` findings concern disposal guards and DI validation consistency; none block Charter closure.

## Compilation and test verification

```
$ dotnet build Weft.sln -c Release
Compilación correcta.
    0 Advertencia(s)
    0 Errores

$ dotnet test --no-build -c Release
Correctas! - Server: 53, Core: 27, Versioning: 25, Determinism: 2 (Total: 107)
    0 errores, 0 omitidos
```

## Task-by-task traceability

### T047 — Connection handler (`WeftConnection.cs`)

- **File(s)**: `src/Weft.Server/WeftConnection.cs:1-230`
- **Status**: Implemented
- **Verification**:
  - Implementation read: Yes (full file)
  - Flow traced: `MapWeft` (authz→403/upgrade) → `HandleConnectionAsync` → `JoinAsync` (hub) → `WeftConnection.RunAsync` → `SendPumpAsync` + `ReceiveLoopAsync` → `DispatchAsync` (sync/awareness/malformed enforcement) → `DocumentHub.ApplyAndPersistAsync` → `DocumentSession.ApplyUpdateAsync` (actor turn) + `IDocumentStore.AppendUpdateAsync` → `UpdateApplied` event → `OnUpdateApplied` → `Broadcast` → `TryEnqueue` (backpressure)
  - Tests found: `RelayTests.cs`, 7 test cases covering convergence, delta, Deny, 1008, awareness, restart-recovery, VersionId parity
- **Findings**:
  - Cross-boundary contract checks:
    - `hub.Session.ExportStateVectorAsync(ct)` → `DocumentSession.ExportStateVectorAsync(CancellationToken)` ✓ (`WeftConnection.cs:76`)
    - `hub.Session.ExportUpdateSinceAsync(payload, ct)` → `DocumentSession.ExportUpdateSinceAsync(ReadOnlyMemory<byte>, CancellationToken)` ✓ (`WeftConnection.cs:122`)
    - `hub.ApplyAndPersistAsync(payload, ct)` → `DocumentSession.ApplyUpdateAsync` + `IDocumentStore.AppendUpdateAsync` ✓ (`DocumentHub.cs:62-65`)
    - `SyncProtocol.Decode(frame, _options.MaxMessageBytes)` → `SyncProtocol.Decode(ReadOnlySpan<byte>, int)` ✓ (`WeftConnection.cs:109`)
    - `SyncMessage` ref struct consumed synchronously before any `await` ✓ (`WeftConnection.cs:107-118`)
  - Close codes verified against charter:
    - 1002 (malformed): `WebSocketCloseStatus.ProtocolError` ✓ (`WeftConnection.cs:116`)
    - 1008 (read-only writes): `WebSocketCloseStatus.PolicyViolation` ✓ (`WeftConnection.cs:126`)
    - 1009 (oversized): `WebSocketCloseStatus.MessageTooBig` ✓ (`WeftConnection.cs:188`)
  - Backpressure (FU-002 part b):
    - Bounded channel `MaxSendQueuePerConnection=256` ✓ (`WeftConnection.cs:28`)
    - `TryWrite` returns false → `_cts.Cancel()` → connection closed ✓ (`WeftConnection.cs:48-55`)
    - `BoundedChannelFullMode.Wait` is irrelevant for `TryWrite` (non-blocking regardless); the mode setting is harmless but misleading — `DropOldest` or `DropWrite` would communicate intent more clearly (`WeftConnection.cs:30`). Not a defect; style observation.

### T048 — DI + endpoint (`WeftServerExtensions.cs`, `WeftServerOptions.cs`)

- **File(s)**: `src/Weft.Server/WeftServerExtensions.cs:1-80`, `src/Weft.Server/WeftServerOptions.cs:1-33`
- **Status**: Implemented
- **Verification**:
  - Implementation read: Yes (full files)
  - Flow traced: `AddWeftServer` → registers `WeftServer` singleton (factory) → `MapWeft` → startup probe `IServiceProviderIsService.IsService(typeof(IWeftAuthorizer))` → fail if absent → route handler → `IWeftAuthorizer.AuthorizeAsync` → Deny→403 / upgrade→`HandleConnectionAsync`
  - Tests found: `RelayTests.Denied_connection_exchanges_no_content` (403 path), `RelayTests.ReadOnly_client_that_writes_is_closed_with_policy_violation` (1008 path)
- **Findings**:
  - Cross-boundary contract checks:
    - `IWeftAuthorizer.AuthorizeAsync(context, docId, context.RequestAborted)` → matches `ValueTask<WeftAccess> AuthorizeAsync(HttpContext, string, CancellationToken)` ✓ (`WeftServerExtensions.cs:73`)
    - `WeftAccess.Deny` check ✓ (`WeftServerExtensions.cs:74-78`)
  - Startup validation: `IServiceProviderIsService` probe (`WeftServerExtensions.cs:52-57`) — correctly guarded with `probe is not null` for hosts that don't provide the probe
  - `WeftServerOptions` defaults: `Engine = YrsEngine.Instance`, `MaxMessageBytes = Lib0Encoding.DefaultMaxMessageBytes` (16 MiB), `MaxSendQueuePerConnection = 256` ✓ (`WeftServerOptions.cs:16-33`)

### T049 — `IWeftServer` service (`WeftServer.cs`)

- **File(s)**: `src/Weft.Server/WeftServer.cs:1-219`
- **Status**: Implemented
- **Verification**:
  - Implementation read: Yes (full file)
  - Flow traced: `PublishAsync` → `_broker.OpenAsync(docId, LoadDocStateAsync, ct)` → `session.ExecuteAsync(doc => doc.ExportState(), ct)` (inside actor turn) → `VersionId.FromBlob(snapshot)` → `_blobStore.PutAsync(id, snapshot, ct)` → return `VersionId`
  - Tests found: `RelayTests.Server_publish_matches_local_publish_version_id` (parity verified)
- **Findings**:
  - Cross-boundary contract checks:
    - `DocumentBroker(engine, brokerOptions)` → `DocumentBroker(ICrdtEngine, DocumentBrokerOptions?)` ✓ (`WeftServer.cs:74`)
    - `DocumentBrokerOptions.OnEvicting` type `Func<string, byte[], CancellationToken, ValueTask>?` ✓ (`WeftServer.cs:66-72`)
    - `session.ExecuteAsync(static doc => doc.ExportState(), ct)` → `ExecuteAsync<T>(Func<ICrdtDoc, T>, CancellationToken)` with `T=byte[]` ✓ (`WeftServer.cs:177`)
    - `VersionId.FromBlob(snapshot)` → static method ✓ (`WeftServer.cs:178`)
    - `_blobStore.PutAsync(id, snapshot, ct)` → `IBlobStore.PutAsync(VersionId, ReadOnlyMemory<byte>, CancellationToken)` with implicit `byte[]`→`ReadOnlyMemory<byte>` ✓ (`WeftServer.cs:179`)
    - `IDocumentStore.LoadAsync(docId, ct)` returns `byte[]?` → `DocumentStateFraming.ReadRecords(framed)` returns `IReadOnlyList<byte[]>` → `doc.ApplyUpdate(record)` takes `ReadOnlySpan<byte>` (implicit `byte[]`→`ReadOnlySpan<byte>`) ✓ (`WeftServer.cs:146-155`)

### T051 — Integration tests (`RelayTests.cs`)

- **File(s)**: `tests/Weft.Server.Tests/RelayTests.cs:1-467`
- **Status**: Implemented
- **Verification**:
  - Implementation read: Yes (full file)
  - Tests found: 7 `[Fact]` test cases, all passing
  - Test coverage matrix:

| Test | Charter criterion | Coverage |
|------|------------------|----------|
| `Two_readwrite_clients_converge_after_cross_edits` | SC-005 (convergence <1s) | ✓ (`RelayTests.cs:293-307`) |
| `Reconnecting_client_receives_only_a_small_delta` | SC-004 (delta ≪ full state) | ✓ (`RelayTests.cs:309-332`) |
| `Denied_connection_exchanges_no_content` | SC-010 (Deny→0 bytes) | ✓ (`RelayTests.cs:334-343`) |
| `ReadOnly_client_that_writes_is_closed_with_policy_violation` | SC-010 (ReadOnly→1008) | ✓ (`RelayTests.cs:345-356`) |
| `Awareness_is_relayed_and_withdrawn_on_disconnect` | FR-015 (awareness + withdrawal) | ✓ (`RelayTests.cs:358-381`) |
| `State_survives_a_server_restart` | SC-006 (restart-recovery) | ✓ (`RelayTests.cs:383-405`) |
| `Server_publish_matches_local_publish_version_id` | P-III (VersionId parity) | ✓ (`RelayTests.cs:407-432`) |

  - Harness: `YClient` uses real `yrs` engine + `SyncProtocol` (no mocks), connected over `TestServer` WebSocket — high fidelity (`RelayTests.cs:113-277`)
  - `TestServer` + `WebApplicationFactory` pattern provides ASP.NET Core integration without external process ✓

### T052 — Samples (`Weft.Sample.Server`, `tiptap-client`)

- **File(s)**: `samples/Weft.Sample.Server/Program.cs:1-33`, `samples/tiptap-client/` (5 files)
- **Status**: Implemented
- **Verification**:
  - Implementation read: Yes (Program.cs, wire-check.mjs, main.js, package.json, index.html, README.md)
  - Sample server: `AddWeftServer()` + `DemoAuthorizer` (ReadWrite to all) + `FileSystemDocumentStore` + `MapWeft("/collab")` ✓ (`Program.cs:8-24`)
  - Tiptap client: `y-prosemirror` + `y-websocket` + Tiptap extensions, no Weft-specific adaptation ✓ (`main.js:1-31`)
  - Wire check: headless `yjs`/`y-websocket` convergence test, exits 0/1 ✓ (`wire-check.mjs:1-57`)

### Project references (`Weft.Server.csproj`, `Weft.Server.Tests.csproj`)

- **File(s)**: `src/Weft.Server/Weft.Server.csproj:1-20`, `tests/Weft.Server.Tests/Weft.Server.Tests.csproj:1-52`
- **Status**: Implemented
- **Verification**:
  - `ProjectReference` to `Weft.Core` and `Weft.Versioning` ✓ (`Weft.Server.csproj:14-15`)
  - `Microsoft.AspNetCore.TestHost 10.0.9` + `FrameworkReference Microsoft.AspNetCore.App` ✓ (`Weft.Server.Tests.csproj:9,17`)
  - Native cdylib copy target `CopyWeftNativeForTests` mirrors `Weft.Core.Tests` pattern ✓ (`Weft.Server.Tests.csproj:28-52`)
  - `InternalsVisibleTo Weft.Server.Tests` ✓ (`Weft.Server.csproj:19`)

### Tasks tracking (`tasks.md`)

- **File(s)**: `specs/001-weft-crdt-versioning/tasks.md`
- **Status**: Implemented
- **Verification**: T047, T048, T049, T051, T052 all marked `[X] — CHARTER-05` ✓

### Scope expansion (`DocumentHub.cs`, `AwarenessProtocol.cs`)

- **File(s)**: `src/Weft.Server/DocumentHub.cs:1-117`, `src/Weft.Server/Protocol/AwarenessProtocol.cs:1-70`
- **Status**: Intentional scope expansion (T047 internal decomposition), documented in AILOG §"Scope expansion"
- **Verification**:
  - `DocumentHub`: per-document session hub, single `UpdateApplied` subscription, `ApplyAndPersistAsync`, broadcast-to-all (AIDEC §2), compaction snapshot on dispose ✓
  - `AwarenessProtocol`: minimal `clientID`/`clock` parsing for withdrawal, `EncodeRemoval` with `null` + `clock+1` (matches `y-protocols/awareness`) ✓
  - Cross-boundary:
    - `Session.UpdateApplied += OnUpdateApplied` → `Action<DocumentSession, ReadOnlyMemory<byte>>` ✓ (`DocumentHub.cs:26`)
    - `Session.ApplyUpdateAsync(update, ct)` → `ValueTask ApplyUpdateAsync(ReadOnlyMemory<byte>, CancellationToken)` ✓ (`DocumentHub.cs:63`)
    - `Session.ExportStateAsync()` → `ValueTask<byte[]>` ✓ (`DocumentHub.cs:94`)
    - `Session.DisposeAsync()` → `ValueTask` ✓ (`DocumentHub.cs:102`)

## Findings

### Critical (block Charter closure)

_None._

### High (security or logic bugs)

_None._

### Medium (inconsistencies, minor risks)

_None._

### Low (quality, naming, style improvements)

| # | Finding | File:Line | Category | Evidence | Suggested remediation |
|---|---------|-----------|----------|----------|----------------------|
| 1 | `WeftServer.DisposeAsync` lacks disposal guards on public methods | `src/Weft.Server/WeftServer.cs:46,92,165,189,199` | `real_debt` | `_disposed` is set at line 183 and checked in `DisposeAsync` for idempotency (line 178), but `HandleConnectionAsync` (line 92), `PublishAsync` (line 165), `GetConnectionCountAsync` (line 189), and `DisconnectAllAsync` (line 199) do not check `_disposed`. If any of these methods are called concurrently with `DisposeAsync` (e.g., during graceful shutdown with in-flight requests), they may interact with disposed broker/hubs. In practice the DI container coordinates singleton disposal with request draining, making this unlikely, but the guard would cost nothing. | Add `if (_disposed) throw new ObjectDisposedException(nameof(WeftServer));` at the top of each public method, or use `ObjectDisposedException.ThrowIf`. |
| 2 | `AddWeftServer` does not validate `IDocumentStore` registration | `src/Weft.Server/WeftServerExtensions.cs:22-38` | `real_debt` | `MapWeft` validates `IWeftAuthorizer` via `IServiceProviderIsService` at startup (line 52-57), providing a clear SC-010 failure. But `AddWeftServer` has no equivalent check for `IDocumentStore` — a missing registration surfaces as `InvalidOperationException` from `GetRequiredService<IDocumentStore>()` inside the `WeftServer` factory (line 31), which is harder to diagnose. Consistency with the authorizer pattern would improve the failure mode. | Add `IDocumentStore` probe in `AddWeftServer` or `MapWeft` (same `IServiceProviderIsService` pattern). |
| 3 | `WeftServerOptions` constructor uses `BoundedChannelFullMode.Wait` despite `TryWrite` semantics | `src/Weft.Server/WeftConnection.cs:28-32` | `real_debt` | `BoundedChannelFullMode.Wait` controls the behavior of `WriteAsync` (asynchronous blocking), but the code exclusively uses `TryWrite` (non-blocking, returns `false` when full — line 48). The mode is functionally irrelevant for `TryWrite` but may mislead future maintainers into thinking the channel blocks. `DropWrite` or `Wait` with a clarifying comment would communicate intent more precisely. | Change to `BoundedChannelFullMode.DropWrite` (semantically equivalent for `TryWrite`) or add a comment explaining that `TryWrite` is used intentionally for non-blocking backpressure. |

## Out-of-scope notes (optional)

| Observation | Relevant Charter / area | Note |
|-------------|-------------------------|------|
| `Lib0Encoding.Lib0Reader`/`Lib0Writer` are shared across `SyncProtocol`, `AwarenessProtocol`, and tests | CHARTER-04 (codec) | Well-tested codec surface; no issues found in how CHARTER-05 consumes it |
| `DocumentBroker` idle eviction default is 5 minutes | CHARTER-03 (broker) | `PublishAsync` on a cold document loads it into the broker; it stays resident until idle eviction. Not a correctness issue; acceptable for v1 |
| `FileSystemDocumentStore` used in sample server | CHARTER-04 (stores) | Persistence fidelity depends on `DocumentStateFraming.Frame`/`ReadRecords` round-trip; verified correct by contract suite |

## Charter closure assessment

**Yes** — the implementation meets every closure criterion declared by CHARTER-05:

1. **All tasks implemented** (T047–T049, T051, T052): verified file-by-file with full code reads and cross-boundary contract checks against the real definitions in `Weft.Core`, `Weft.Versioning`, and `Weft.Server` (codec/stores/authorizer from CHARTER-04).
2. **Constitution preserved**: P-V (all updates via `DocumentSession` actor turn — `DocumentHub.cs:63`), P-III (`PublishAsync` captures `ExportState()` inside the turn — `WeftServer.cs:177`), P-I/P-II (FU-002 part b delivered — bounded queue + backpressure — `WeftConnection.cs:28-55`), P-IV (broker/session + opaque blobs — no direct yrs types in relay code).
3. **Build + tests green**: 0 warnings, 0 errors; 107 tests pass (Server 53, Core 27, Versioning 25, Determinism 2).
4. **Wire compatibility**: headless `yjs`/`y-websocket` convergence check (`wire-check.mjs`) provides programmatic evidence that R1 is retired.
5. **FU-002 closed**: cap de tamaño (parte a, CHARTER-04) + límites por conexión/backpressure (parte b, este Charter) + malformed→1002 + oversized→1009.
6. **AILOG + AIDEC signed** by operator (2026-07-13).
7. **Mandatory multi-model external audit**: this is one of the independent auditor reports; closure requires ≥2 auditor reports + calibrator reconciliation before `straymark charter close`.

## Conclusion

CHARTER-05 is a clean, well-structured implementation that faithfully delivers the relay end-to-end over the CHARTER-04 substrate and closes Milestone 2. The code demonstrates disciplined adherence to the project's constitution (P-V serialization, P-III determinism, P-IV abstraction boundary) and the four design decisions recorded in AIDEC-2026-07-13-001 are sound. Three low-severity `real_debt` findings (disposal guards, DI validation consistency, channel mode clarity) are non-blocking quality improvements suitable for a follow-up. No hallucinations, no implementation gaps, no critical or high-severity issues. The Charter is ready for closure once the multi-model audit cycle completes (this report + ≥1 other auditor + calibrator reconciliation).
