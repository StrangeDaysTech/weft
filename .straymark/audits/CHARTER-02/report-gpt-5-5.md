---
audit_role: auditor
auditor: gpt-5-5
charter_id: CHARTER-02-versioning-dual-engine
git_range: "origin/main..HEAD"
prompt_used: audit-prompt.md
audited_at: 2026-07-10
findings_total: 2
findings_by_category:
  hallucination: 0
  implementation_gap: 2
  real_debt: 0
  false_positive: 0
evidence_citations: 170
audit_quality: high
---

# Audit: CHARTER-02-versioning-dual-engine by gpt-5-5

## Executive summary

The content-addressed versioning path is substantially implemented. `VersionId`, blob stores, `TextDiff`, `VersionStore`, the shared versioning suite, Loro adapter, determinism CI, dual-engine CI, ASan coverage, sample wiring, and solution wiring are present and traceable to the Charter scope.

The audit found two implementation gaps. First, the Charter and task list declare Loro native-versioning probes, a C header, and `LoroNativeVersioning`, but the implementation intentionally returns `NativeVersioning => null` and no matching C#/header/probe surface exists. Second, the Charter requires an AIDEC for new implementation decisions, but only an AILOG was found. Closure is Partial until those gaps are implemented or explicitly reconciled in the Charter/spec.

## Compilation and test verification

Skipped. The audit prompt allows build/test commands when applicable, but also says the auditor's only permitted write is the report file. `dotnet test`, `cargo test`, and `cargo build` would write build artifacts under the repository, so this audit uses static tool evidence only. CI wiring for the relevant gates was read directly at `.github/workflows/ci.yml:148` and `.github/workflows/ci.yml:169`.

## Task-by-task traceability

### T022 - Implement `VersionId`

- **File(s)**: `src/Weft.Versioning/VersionId.cs:10`
- **Status**: Implemented
- **Verification**:
  - Implementation read: Yes
  - Flow traced: `VersionStore.PublishAsync` hashes `doc.ExportState()` via `VersionId.FromBlob` at `src/Weft.Versioning/VersionStore.cs:27` and `src/Weft.Versioning/VersionStore.cs:31`.
  - Tests found: shared suite publishes and compares ids at `tests/Weft.Versioning.Tests/VersioningSuiteBase.cs:31` and `tests/Weft.Versioning.Tests/VersioningSuiteBase.cs:63`; determinism tests hash reloads at `tests/Weft.Determinism.Tests/DeterminismTests.cs:99`.
- **Findings**: None. SHA-256 creation is at `src/Weft.Versioning/VersionId.cs:17`; parse validates 64 hex chars at `src/Weft.Versioning/VersionId.cs:31`; lowercase output is at `src/Weft.Versioning/VersionId.cs:53`.

### T023 - Define `IBlobStore` and `InMemoryBlobStore`

- **File(s)**: `src/Weft.Versioning/Blobs/IBlobStore.cs:8`, `src/Weft.Versioning/Blobs/InMemoryBlobStore.cs:6`
- **Status**: Implemented
- **Verification**:
  - Implementation read: Yes
  - Flow traced: `VersionStore` depends on `IBlobStore` at `src/Weft.Versioning/VersionStore.cs:15`, stores in `PublishAsync` at `src/Weft.Versioning/VersionStore.cs:32`, and reads in `LoadVerifiedAsync` at `src/Weft.Versioning/VersionStore.cs:76`.
  - Tests found: dedup count asserted at `tests/Weft.Versioning.Tests/VersioningSuiteBase.cs:41`; determinism dedup asserted at `tests/Weft.Determinism.Tests/DeterminismTests.cs:85`.
- **Findings**: None. `InMemoryBlobStore` uses `ConcurrentDictionary` at `src/Weft.Versioning/Blobs/InMemoryBlobStore.cs:8` and idempotent `TryAdd` at `src/Weft.Versioning/Blobs/InMemoryBlobStore.cs:17`.

### T024 - Implement `FileSystemBlobStore`

- **File(s)**: `src/Weft.Versioning/Blobs/FileSystemBlobStore.cs:8`
- **Status**: Implemented
- **Verification**:
  - Implementation read: Yes
  - Flow traced: sharded path `aa/bb/hash` is constructed at `src/Weft.Versioning/Blobs/FileSystemBlobStore.cs:20`; writes go temp then rename at `src/Weft.Versioning/Blobs/FileSystemBlobStore.cs:36` and `src/Weft.Versioning/Blobs/FileSystemBlobStore.cs:42`; reads are at `src/Weft.Versioning/Blobs/FileSystemBlobStore.cs:51`.
  - Tests found: no direct filesystem blob test found in the opened test files; `IBlobStore` contract is exercised through `InMemoryBlobStore` in the shared suite.
- **Findings**: None for Charter closure. The implementation matches the declared sharding and atomic-write shape.

### T025 - Implement word-level LCS diff

- **File(s)**: `src/Weft.Versioning/TextDiff.cs:25`
- **Status**: Implemented
- **Verification**:
  - Implementation read: Yes
  - Flow traced: `VersionStore.DiffAsync` checks out two versions and calls `TextDiff.Compute` at `src/Weft.Versioning/VersionStore.cs:46` and `src/Weft.Versioning/VersionStore.cs:51`.
  - Tests found: no-change and empty-field cases at `tests/Weft.Versioning.Tests/TextDiffTests.cs:7` and `tests/Weft.Versioning.Tests/TextDiffTests.cs:15`; insert/delete and determinism cases at `tests/Weft.Versioning.Tests/TextDiffTests.cs:23` and `tests/Weft.Versioning.Tests/TextDiffTests.cs:43`.
- **Findings**: None. Tokenization splits whitespace/non-whitespace runs at `src/Weft.Versioning/TextDiff.cs:80`; LCS table construction is at `src/Weft.Versioning/TextDiff.cs:105`.

### T026 - Implement `VersionStore`

- **File(s)**: `src/Weft.Versioning/VersionStore.cs:12`
- **Status**: Implemented
- **Verification**:
  - Implementation read: Yes
  - Flow traced: publish at `src/Weft.Versioning/VersionStore.cs:27`, checkout at `src/Weft.Versioning/VersionStore.cs:39`, diff at `src/Weft.Versioning/VersionStore.cs:46`, branch at `src/Weft.Versioning/VersionStore.cs:55`, merge at `src/Weft.Versioning/VersionStore.cs:59`, and merge by version at `src/Weft.Versioning/VersionStore.cs:67`.
  - Tests found: round-trip at `tests/Weft.Versioning.Tests/VersioningSuiteBase.cs:47`, diff at `tests/Weft.Versioning.Tests/VersioningSuiteBase.cs:80`, merge at `tests/Weft.Versioning.Tests/VersioningSuiteBase.cs:100`.
- **Findings**: None. Integrity verification is present at `src/Weft.Versioning/VersionStore.cs:81` and throws `BlobIntegrityException` at `src/Weft.Versioning/VersionStore.cs:83`.

### T027 - Create engine-parametrized versioning suite for yrs

- **File(s)**: `tests/Weft.Versioning.Tests/VersioningSuiteBase.cs:11`, `tests/Weft.Versioning.Tests/YrsVersioningTests.cs:7`
- **Status**: Implemented
- **Verification**:
  - Implementation read: Yes
  - Flow traced: `YrsVersioningTests` supplies `YrsEngine.Instance` to the abstract shared suite at `tests/Weft.Versioning.Tests/YrsVersioningTests.cs:9`.
  - Tests found: postconditions 1-5 and 7 appear as facts at `tests/Weft.Versioning.Tests/VersioningSuiteBase.cs:31`, `tests/Weft.Versioning.Tests/VersioningSuiteBase.cs:47`, `tests/Weft.Versioning.Tests/VersioningSuiteBase.cs:63`, `tests/Weft.Versioning.Tests/VersioningSuiteBase.cs:80`, `tests/Weft.Versioning.Tests/VersioningSuiteBase.cs:100`, and `tests/Weft.Versioning.Tests/VersioningSuiteBase.cs:131`; postcondition 6 is enforced by running the same base class under both concrete engines at `tests/Weft.Versioning.Tests/LoroVersioningTests.cs:10`.
- **Findings**: None.

### T028 - Unit tests for `TextDiff`

- **File(s)**: `tests/Weft.Versioning.Tests/TextDiffTests.cs:4`
- **Status**: Implemented
- **Verification**:
  - Implementation read: Yes
  - Flow traced: tests call `TextDiff.Compute` directly at `tests/Weft.Versioning.Tests/TextDiffTests.cs:9`, `tests/Weft.Versioning.Tests/TextDiffTests.cs:25`, and `tests/Weft.Versioning.Tests/TextDiffTests.cs:46`.
  - Tests found: equal, empty, insert/delete, reconstruct-new-text, and determinism cases at `tests/Weft.Versioning.Tests/TextDiffTests.cs:7`, `tests/Weft.Versioning.Tests/TextDiffTests.cs:15`, `tests/Weft.Versioning.Tests/TextDiffTests.cs:23`, `tests/Weft.Versioning.Tests/TextDiffTests.cs:35`, and `tests/Weft.Versioning.Tests/TextDiffTests.cs:43`.
- **Findings**: None.

### T029 - Create determinism gate

- **File(s)**: `tests/Weft.Determinism.Tests/DeterminismTests.cs:14`
- **Status**: Implemented
- **Verification**:
  - Implementation read: Yes
  - Flow traced: the fixed corpus is at `tests/Weft.Determinism.Tests/DeterminismTests.cs:19`; all replicas sync via state vectors and updates at `tests/Weft.Determinism.Tests/DeterminismTests.cs:48`; version ids are compared at `tests/Weft.Determinism.Tests/DeterminismTests.cs:79`.
  - Tests found: converged replicas share ids at `tests/Weft.Determinism.Tests/DeterminismTests.cs:69`; reload/re-export stability is at `tests/Weft.Determinism.Tests/DeterminismTests.cs:99`.
- **Findings**: None. CI runs the determinism gate at `.github/workflows/ci.yml:148` and `.github/workflows/ci.yml:165`.

### T030 - Create runnable versioning sample

- **File(s)**: `samples/Weft.Sample.Versioning/Program.cs:6`, `samples/Weft.Sample.Versioning/Weft.Sample.Versioning.csproj:1`
- **Status**: Implemented
- **Verification**:
  - Implementation read: Yes
  - Flow traced: sample creates `VersionStore` at `samples/Weft.Sample.Versioning/Program.cs:10`, publishes at `samples/Weft.Sample.Versioning/Program.cs:17` and `samples/Weft.Sample.Versioning/Program.cs:24`, diffs at `samples/Weft.Sample.Versioning/Program.cs:29`, checkouts at `samples/Weft.Sample.Versioning/Program.cs:39`, and branches/merges at `samples/Weft.Sample.Versioning/Program.cs:43`.
  - Tests found: no automated sample test found; the project references `Weft.Versioning` at `samples/Weft.Sample.Versioning/Weft.Sample.Versioning.csproj:10` and copies the yrs native library at `samples/Weft.Sample.Versioning/Weft.Sample.Versioning.csproj:25`.
- **Findings**: None.

### T031 - Wire determinism and versioning CI

- **File(s)**: `.github/workflows/ci.yml:148`
- **Status**: Implemented
- **Verification**:
  - Implementation read: Yes
  - Flow traced: CI builds shims before determinism at `.github/workflows/ci.yml:161` and runs `dotnet test tests/Weft.Determinism.Tests/ --configuration Release` at `.github/workflows/ci.yml:165`; the multi-platform test job also runs `dotnet test --configuration Release` at `.github/workflows/ci.yml:43`.
  - Tests found: project-level determinism tests read at `tests/Weft.Determinism.Tests/DeterminismTests.cs:69` and `tests/Weft.Determinism.Tests/DeterminismTests.cs:99`.
- **Findings**: None.

### T032 - Create `weft-loro-ffi` crate with core ABI, probes, header, and mem/ASan tests

- **File(s)**: `native/weft-loro-ffi/Cargo.toml:1`, `native/weft-loro-ffi/src/lib.rs:1`, `native/weft-loro-ffi/tests/mem_asan.rs:1`
- **Status**: Partial
- **Verification**:
  - Implementation read: Yes
  - Flow traced: core lifecycle/text/state functions exist at `native/weft-loro-ffi/src/lib.rs:84`, `native/weft-loro-ffi/src/lib.rs:97`, `native/weft-loro-ffi/src/lib.rs:133`, `native/weft-loro-ffi/src/lib.rs:163`, `native/weft-loro-ffi/src/lib.rs:192`, `native/weft-loro-ffi/src/lib.rs:221`, `native/weft-loro-ffi/src/lib.rs:243`, `native/weft-loro-ffi/src/lib.rs:262`, and `native/weft-loro-ffi/src/lib.rs:293`.
  - Tests found: round-trip, incremental sync, typed errors, stress loop, and panic hook tests are at `native/weft-loro-ffi/tests/mem_asan.rs:60`, `native/weft-loro-ffi/tests/mem_asan.rs:85`, `native/weft-loro-ffi/tests/mem_asan.rs:113`, `native/weft-loro-ffi/tests/mem_asan.rs:149`, and `native/weft-loro-ffi/tests/mem_asan.rs:183`.
- **Findings**: M1. The declared native probe/header portion is absent: the task declares probes and header at `specs/001-weft-crdt-versioning/tasks.md:85`, while the actual Rust exports stop at core ABI/diagnostic/test hook functions such as `native/weft-loro-ffi/src/lib.rs:329` and `native/weft-loro-ffi/src/lib.rs:338`; `find native/weft-loro-ffi -maxdepth 3 -type f` returned no `include/weft_loro_ffi.h`.

### T033 - Implement `Weft.Loro` adapter and `LoroNativeVersioning`

- **File(s)**: `src/Weft.Loro/LoroEngine.cs:8`, `src/Weft.Loro/LoroDoc.cs:8`, `src/Weft.Loro/Interop/NativeMethods.cs:6`
- **Status**: Partial
- **Verification**:
  - Implementation read: Yes
  - Flow traced: `LoroEngine.CreateDoc` and `LoadDoc` delegate to `LoroDoc` at `src/Weft.Loro/LoroEngine.cs:27` and `src/Weft.Loro/LoroEngine.cs:30`; `LoroDoc` calls native lifecycle/text/state APIs at `src/Weft.Loro/LoroDoc.cs:16`, `src/Weft.Loro/LoroDoc.cs:22`, `src/Weft.Loro/LoroDoc.cs:36`, `src/Weft.Loro/LoroDoc.cs:48`, `src/Weft.Loro/LoroDoc.cs:58`, `src/Weft.Loro/LoroDoc.cs:66`, `src/Weft.Loro/LoroDoc.cs:74`, `src/Weft.Loro/LoroDoc.cs:82`, and `src/Weft.Loro/LoroDoc.cs:91`; P/Invoke methods are declared at `src/Weft.Loro/Interop/NativeMethods.cs:10`.
  - Tests found: Loro runs the shared versioning suite at `tests/Weft.Versioning.Tests/LoroVersioningTests.cs:10`.
- **Findings**: M1. The `ICrdtEngine` adapter exists, but `LoroNativeVersioning` does not: `LoroEngine.NativeVersioning` intentionally returns null at `src/Weft.Loro/LoroEngine.cs:24`, while the Charter declares `LoroNativeVersioning` at `.straymark/charters/02-versioning-dual-engine.md:37` and the task marks it complete at `specs/001-weft-crdt-versioning/tasks.md:86`.

### T034 - Activate dual-engine theory and blocking CI gate

- **File(s)**: `tests/Weft.Versioning.Tests/LoroVersioningTests.cs:10`, `.github/workflows/ci.yml:169`
- **Status**: Implemented for the shared versioning suite
- **Verification**:
  - Implementation read: Yes
  - Flow traced: `LoroVersioningTests` supplies `LoroEngine.Instance` at `tests/Weft.Versioning.Tests/LoroVersioningTests.cs:12`; CI builds both shims at `.github/workflows/ci.yml:182` and runs the versioning suite at `.github/workflows/ci.yml:185`.
  - Tests found: the same `VersioningSuiteBase` facts read under T027 run for Loro through inheritance at `tests/Weft.Versioning.Tests/LoroVersioningTests.cs:10`.
- **Findings**: None for the shared suite. The native-probe part is covered by M1 under T032/T033.

### T035 - Extend ASan CI to `weft-loro-ffi`

- **File(s)**: `.github/workflows/ci.yml:49`, `native/Cargo.toml:3`
- **Status**: Implemented
- **Verification**:
  - Implementation read: Yes
  - Flow traced: the Rust workspace includes both `weft-yrs-ffi` and `weft-loro-ffi` at `native/Cargo.toml:5`; ASan runs `cargo +nightly test --features test-hooks` over the workspace from `native` at `.github/workflows/ci.yml:63` and `.github/workflows/ci.yml:69`.
  - Tests found: Loro mem/ASan tests are in `native/weft-loro-ffi/tests/mem_asan.rs:60`, `native/weft-loro-ffi/tests/mem_asan.rs:85`, `native/weft-loro-ffi/tests/mem_asan.rs:113`, and `native/weft-loro-ffi/tests/mem_asan.rs:149`.
- **Findings**: None.

### Charter governance tasks

- **File(s)**: `.straymark/charters/02-versioning-dual-engine.md:120`, `.straymark/07-ai-audit/agent-logs/AILOG-2026-07-10-002-charter-02-versionado-content-addressed-dual-engin.md:1`
- **Status**: Partial
- **Verification**:
  - Implementation read: Yes
  - Flow traced: Charter requires AILOG plus AIDEC for implementation decisions at `.straymark/charters/02-versioning-dual-engine.md:125`; AILOG exists and records decisions at `.straymark/07-ai-audit/agent-logs/AILOG-2026-07-10-002-charter-02-versionado-content-addressed-dual-engin.md:63`.
  - Tests found: not applicable.
- **Findings**: M2. No `AIDEC-*.md` file was found under `.straymark/07-ai-audit/decisions/` during repository search.

## Findings

### Critical (block Charter closure)

| # | Finding | File:Line | Category | Evidence | Suggested remediation |
|---|---------|-----------|----------|----------|----------------------|

### High (security or logic bugs)

| # | Finding | File:Line | Category | Evidence | Suggested remediation |
|---|---------|-----------|----------|----------|----------------------|

### Medium (inconsistencies, minor risks)

| # | Finding | File:Line | Category | Evidence | Suggested remediation |
|---|---------|-----------|----------|----------|----------------------|
| M1 | T032/T033 are marked complete, but the declared Loro native-versioning/probe/header surface is absent. | `.straymark/charters/02-versioning-dual-engine.md:35` | implementation_gap | The Charter declares Loro probes and `LoroNativeVersioning` at `.straymark/charters/02-versioning-dual-engine.md:35` and `.straymark/charters/02-versioning-dual-engine.md:37`; tasks mark probes/header and `LoroNativeVersioning` complete at `specs/001-weft-crdt-versioning/tasks.md:85` and `specs/001-weft-crdt-versioning/tasks.md:86`; quickstart expects `LoroEngine.NativeVersioning != null` at `specs/001-weft-crdt-versioning/quickstart.md:87`; the actual engine returns null at `src/Weft.Loro/LoroEngine.cs:24`; P/Invoke declares only core ABI functions at `src/Weft.Loro/Interop/NativeMethods.cs:10`; Rust exports stop at ABI version/test hook functions at `native/weft-loro-ffi/src/lib.rs:329` and `native/weft-loro-ffi/src/lib.rs:338`; repository search found no `src/Weft.Loro/LoroNativeVersioning.cs` or `native/weft-loro-ffi/include/weft_loro_ffi.h`. | Either implement `LoroNativeVersioning`, the C ABI probes, and the header, or update the Charter/spec/tasks/quickstart to explicitly defer that surface before closure. |
| M2 | The Charter required an AIDEC for new implementation decisions, but no AIDEC exists. | `.straymark/charters/02-versioning-dual-engine.md:125` | implementation_gap | The Charter requires an AIDEC for tokenization, sharding, and Loro capability mapping decisions at `.straymark/charters/02-versioning-dual-engine.md:125`; the AILOG records implementation decisions in prose at `.straymark/07-ai-audit/agent-logs/AILOG-2026-07-10-002-charter-02-versionado-content-addressed-dual-engin.md:63`, including the decision to set `LoroEngine.NativeVersioning = null` at `.straymark/07-ai-audit/agent-logs/AILOG-2026-07-10-002-charter-02-versionado-content-addressed-dual-engin.md:68`; repository search found no `AIDEC-*.md` under `.straymark/07-ai-audit/decisions/`. | Create the required AIDEC, or amend the Charter with an explicit rationale for downgrading these implementation decisions to AILOG-only documentation. |

### Low (quality, naming, style improvements)

| # | Finding | File:Line | Category | Evidence | Suggested remediation |
|---|---------|-----------|----------|----------|----------------------|

## Out-of-scope notes

| Observation | Relevant Charter / area | Note |
|-------------|-------------------------|------|
| The yrs UTF-16 indexing correction is outside this Charter's declared files but documented as a scope expansion. | CHARTER-01 regression / CHARTER-02 AILOG | The AILOG explains the latent bug and correction at `.straymark/07-ai-audit/agent-logs/AILOG-2026-07-10-002-charter-02-versionado-content-addressed-dual-engin.md:92`; this audit did not treat it as a CHARTER-02 defect. |

## Charter closure assessment

Partial. The core content-addressed versioning implementation and blocking P-III/P-IV gates are present: `VersionStore` publishes and verifies content-addressed blobs at `src/Weft.Versioning/VersionStore.cs:27` and `src/Weft.Versioning/VersionStore.cs:81`; Loro inherits the same versioning suite at `tests/Weft.Versioning.Tests/LoroVersioningTests.cs:10`; CI has blocking `determinism` and `dual-engine` jobs at `.github/workflows/ci.yml:148` and `.github/workflows/ci.yml:169`.

Strict closure is not met as written because the Charter and tasks still declare native Loro probes, `LoroNativeVersioning`, a header, and an AIDEC. The implementation deliberately returns `NativeVersioning => null` at `src/Weft.Loro/LoroEngine.cs:24`, and the AILOG records that deferral at `.straymark/07-ai-audit/agent-logs/AILOG-2026-07-10-002-charter-02-versionado-content-addressed-dual-engin.md:68`; that may be a valid product decision, but it conflicts with the current Charter/spec/task list until reconciled. The Charter also requires multi-model audit consolidation before close at `.straymark/charters/02-versioning-dual-engine.md:140`.

## Conclusion

CHARTER-02 is strong for the engine-agnostic versioning path and the dual-engine gate that actually exercises `VersionStore`. The remaining work is scope reconciliation: either implement the native Loro probe/header/`LoroNativeVersioning` surface and add the required AIDEC, or explicitly defer those items in the governing Charter/spec artifacts so closure matches the delivered surface.
