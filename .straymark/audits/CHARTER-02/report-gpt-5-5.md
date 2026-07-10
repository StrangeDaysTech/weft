---
audit_role: auditor
auditor: gpt-5-5
charter_id: CHARTER-02-versioning-dual-engine
git_range: "origin/main..HEAD"
prompt_used: .straymark/audits/CHARTER-02/audit-prompt.md
audited_at: 2026-07-10
findings_total: 2
findings_by_category:
  hallucination: 0
  implementation_gap: 2
  real_debt: 0
  false_positive: 0
evidence_citations: 54
audit_quality: high
---

# Audit: CHARTER-02-versioning-dual-engine by gpt-5-5

## Executive summary

The core content-addressed versioning path is substantially implemented: `VersionId`, blob stores, `TextDiff`, `VersionStore`, the shared versioning suite, Loro adapter, determinism and dual-engine CI jobs are all present and traceable to the Charter scope.

The audit found two implementation gaps inside the Charter scope. First, the Loro native-versioning/probe/header surface declared by T032/T033 is not implemented, even though the task list marks it complete. Second, the Charter explicitly required an AIDEC for new implementation decisions, but no AIDEC document exists in the repository. The closure assessment is Partial until those gaps are either implemented or the Charter/spec are updated to make the deferral explicit.

## Compilation and test verification

`git diff --check origin/main..HEAD` completed with no output.

I did not run `dotnet test`, `cargo test`, ASan, or fuzz commands in this auditor-side session because the audit prompt's read-only rule permits only the final report to be written, while those commands normally create or update build artifacts. Static verification read the CI job definitions, source, tests, and task/spec documents.

## Task-by-task traceability

### T022 - VersionId

- **File(s)**: `src/Weft.Versioning/VersionId.cs:10`
- **Status**: Implemented
- **Verification**:
  - Implementation read: Yes.
  - Flow traced: `VersionId.FromBlob` computes SHA-256 at `src/Weft.Versioning/VersionId.cs:17`; parse validates 64 hex chars at `src/Weft.Versioning/VersionId.cs:31`; lowercase output is produced at `src/Weft.Versioning/VersionId.cs:53`.
  - Tests found: covered indirectly by `tests/Weft.Versioning.Tests/VersioningSuiteBase.cs:38` and `tests/Weft.Determinism.Tests/DeterminismTests.cs:104`.
- **Findings**: None.

### T023 - IBlobStore and InMemoryBlobStore

- **File(s)**: `src/Weft.Versioning/Blobs/IBlobStore.cs:8`, `src/Weft.Versioning/Blobs/InMemoryBlobStore.cs:6`
- **Status**: Implemented
- **Verification**:
  - Implementation read: Yes.
  - Flow traced: interface exposes put/get/exists at `src/Weft.Versioning/Blobs/IBlobStore.cs:12`; in-memory storage uses `ConcurrentDictionary` at `src/Weft.Versioning/Blobs/InMemoryBlobStore.cs:8`; idempotent put uses `TryAdd` at `src/Weft.Versioning/Blobs/InMemoryBlobStore.cs:17`.
  - Tests found: dedup checked by `tests/Weft.Versioning.Tests/VersioningSuiteBase.cs:31`; blob count checked at `tests/Weft.Versioning.Tests/VersioningSuiteBase.cs:42`.
- **Findings**: None.

### T024 - FileSystemBlobStore

- **File(s)**: `src/Weft.Versioning/Blobs/FileSystemBlobStore.cs:8`
- **Status**: Implemented
- **Verification**:
  - Implementation read: Yes.
  - Flow traced: sharding path uses `aa/bb/hash` at `src/Weft.Versioning/Blobs/FileSystemBlobStore.cs:20`; temporary write occurs at `src/Weft.Versioning/Blobs/FileSystemBlobStore.cs:36`; atomic rename uses `File.Move(... overwrite: false)` at `src/Weft.Versioning/Blobs/FileSystemBlobStore.cs:42`.
  - Tests found: no direct filesystem-store tests found; the `IBlobStore` behavior is covered through the in-memory store in `tests/Weft.Versioning.Tests/VersioningSuiteBase.cs:15`.
- **Findings**: None in closure scope, but direct filesystem-store coverage remains thinner than in-memory coverage.

### T025 - TextDiff

- **File(s)**: `src/Weft.Versioning/TextDiff.cs:25`
- **Status**: Implemented
- **Verification**:
  - Implementation read: Yes.
  - Flow traced: `Compute` tokenizes both inputs at `src/Weft.Versioning/TextDiff.cs:36`, builds LCS at `src/Weft.Versioning/TextDiff.cs:38`, reconstructs deterministic segments at `src/Weft.Versioning/TextDiff.cs:44`, and merges contiguous operations at `src/Weft.Versioning/TextDiff.cs:66`.
  - Tests found: equal/empty/insert/delete/determinism cases in `tests/Weft.Versioning.Tests/TextDiffTests.cs:6`, `tests/Weft.Versioning.Tests/TextDiffTests.cs:14`, `tests/Weft.Versioning.Tests/TextDiffTests.cs:22`, and `tests/Weft.Versioning.Tests/TextDiffTests.cs:43`.
- **Findings**: None.

### T026 - VersionStore

- **File(s)**: `src/Weft.Versioning/VersionStore.cs:12`
- **Status**: Implemented
- **Verification**:
  - Implementation read: Yes.
  - Flow traced: publish exports and hashes state at `src/Weft.Versioning/VersionStore.cs:27`; checkout loads and verifies at `src/Weft.Versioning/VersionStore.cs:39`; diff, branch, merge, and merge-by-version are implemented at `src/Weft.Versioning/VersionStore.cs:46`, `src/Weft.Versioning/VersionStore.cs:55`, `src/Weft.Versioning/VersionStore.cs:59`, and `src/Weft.Versioning/VersionStore.cs:67`; integrity is checked at `src/Weft.Versioning/VersionStore.cs:81`.
  - Tests found: publish/checkout/diff/merge/compaction tests in `tests/Weft.Versioning.Tests/VersioningSuiteBase.cs:31`, `tests/Weft.Versioning.Tests/VersioningSuiteBase.cs:46`, `tests/Weft.Versioning.Tests/VersioningSuiteBase.cs:79`, `tests/Weft.Versioning.Tests/VersioningSuiteBase.cs:99`, and `tests/Weft.Versioning.Tests/VersioningSuiteBase.cs:130`.
- **Findings**: None.

### T027 - Engine-parametrized versioning suite for yrs

- **File(s)**: `tests/Weft.Versioning.Tests/VersioningSuiteBase.cs:11`, `tests/Weft.Versioning.Tests/YrsVersioningTests.cs:7`
- **Status**: Implemented
- **Verification**:
  - Implementation read: Yes.
  - Flow traced: the abstract suite constructs `VersionStore` over the selected engine at `tests/Weft.Versioning.Tests/VersioningSuiteBase.cs:15`; the yrs concrete suite binds `YrsEngine.Instance` at `tests/Weft.Versioning.Tests/YrsVersioningTests.cs:9`.
  - Tests found: the suite includes six concrete test methods spanning the declared postconditions at `tests/Weft.Versioning.Tests/VersioningSuiteBase.cs:31`, `tests/Weft.Versioning.Tests/VersioningSuiteBase.cs:46`, `tests/Weft.Versioning.Tests/VersioningSuiteBase.cs:62`, `tests/Weft.Versioning.Tests/VersioningSuiteBase.cs:79`, `tests/Weft.Versioning.Tests/VersioningSuiteBase.cs:99`, and `tests/Weft.Versioning.Tests/VersioningSuiteBase.cs:130`.
- **Findings**: None.

### T028 - TextDiff tests

- **File(s)**: `tests/Weft.Versioning.Tests/TextDiffTests.cs:4`
- **Status**: Implemented
- **Verification**:
  - Implementation read: Yes.
  - Flow traced: tests cover no changes, empty fields, insert/delete, reconstruction, and determinism at `tests/Weft.Versioning.Tests/TextDiffTests.cs:6`, `tests/Weft.Versioning.Tests/TextDiffTests.cs:14`, `tests/Weft.Versioning.Tests/TextDiffTests.cs:22`, `tests/Weft.Versioning.Tests/TextDiffTests.cs:34`, and `tests/Weft.Versioning.Tests/TextDiffTests.cs:43`.
  - Tests found: this is the test artifact.
- **Findings**: None.

### T029 - Determinism gate

- **File(s)**: `tests/Weft.Determinism.Tests/DeterminismTests.cs:14`
- **Status**: Implemented
- **Verification**:
  - Implementation read: Yes.
  - Flow traced: the deterministic corpus is fixed at `tests/Weft.Determinism.Tests/DeterminismTests.cs:19`; sync exports state vectors and updates at `tests/Weft.Determinism.Tests/DeterminismTests.cs:48`; converged replicas compare `VersionId` at `tests/Weft.Determinism.Tests/DeterminismTests.cs:69`; reload/re-export byte stability is checked at `tests/Weft.Determinism.Tests/DeterminismTests.cs:99`.
  - Tests found: two determinism tests at `tests/Weft.Determinism.Tests/DeterminismTests.cs:68` and `tests/Weft.Determinism.Tests/DeterminismTests.cs:98`.
- **Findings**: None.

### T030 - Versioning sample

- **File(s)**: `samples/Weft.Sample.Versioning/Program.cs:9`, `samples/Weft.Sample.Versioning/Weft.Sample.Versioning.csproj:1`
- **Status**: Implemented
- **Verification**:
  - Implementation read: Yes.
  - Flow traced: sample publishes v1/v2 at `samples/Weft.Sample.Versioning/Program.cs:17` and `samples/Weft.Sample.Versioning/Program.cs:24`, computes diff at `samples/Weft.Sample.Versioning/Program.cs:29`, checks out v1 at `samples/Weft.Sample.Versioning/Program.cs:39`, and branches/merges at `samples/Weft.Sample.Versioning/Program.cs:43`.
  - Tests found: no automated sample test; sample project references versioning at `samples/Weft.Sample.Versioning/Weft.Sample.Versioning.csproj:10` and copies the native yrs library at `samples/Weft.Sample.Versioning/Weft.Sample.Versioning.csproj:13`.
- **Findings**: None.

### T031 - Determinism and versioning CI wiring

- **File(s)**: `.github/workflows/ci.yml:20`, `.github/workflows/ci.yml:148`
- **Status**: Implemented
- **Verification**:
  - Implementation read: Yes.
  - Flow traced: main `test` job runs across ubuntu/windows/macos at `.github/workflows/ci.yml:20` and runs `dotnet test` at `.github/workflows/ci.yml:42`; separate blocking `determinism` job runs `dotnet test tests/Weft.Determinism.Tests/ --configuration Release` at `.github/workflows/ci.yml:148` and `.github/workflows/ci.yml:164`.
  - Tests found: determinism project source at `tests/Weft.Determinism.Tests/DeterminismTests.cs:14`; project reference at `tests/Weft.Determinism.Tests/Weft.Determinism.Tests.csproj:19`.
- **Findings**: None.

### T032 - weft-loro-ffi crate, probes, header, ASan/fuzz

- **File(s)**: `native/weft-loro-ffi/Cargo.toml:1`, `native/weft-loro-ffi/src/lib.rs:1`, `native/weft-loro-ffi/tests/mem_asan.rs:1`
- **Status**: Partial
- **Verification**:
  - Implementation read: Yes.
  - Flow traced: crate pins `loro = "=1.13.6"` at `native/weft-loro-ffi/Cargo.toml:15`; core lifecycle/text/state ABI functions are present at `native/weft-loro-ffi/src/lib.rs:83`, `native/weft-loro-ffi/src/lib.rs:133`, `native/weft-loro-ffi/src/lib.rs:218`, `native/weft-loro-ffi/src/lib.rs:240`, `native/weft-loro-ffi/src/lib.rs:259`, and `native/weft-loro-ffi/src/lib.rs:290`; memory/free and ABI-version functions are present at `native/weft-loro-ffi/src/lib.rs:313` and `native/weft-loro-ffi/src/lib.rs:325`; ASan-style tests exercise round-trip, convergence, error paths, stress, and panic catching at `native/weft-loro-ffi/tests/mem_asan.rs:59`, `native/weft-loro-ffi/tests/mem_asan.rs:84`, `native/weft-loro-ffi/tests/mem_asan.rs:112`, `native/weft-loro-ffi/tests/mem_asan.rs:148`, and `native/weft-loro-ffi/tests/mem_asan.rs:181`.
  - Tests found: Rust integration tests at `native/weft-loro-ffi/tests/mem_asan.rs:59`; fuzz targets at `native/weft-loro-ffi/fuzz/fuzz_targets/loro_doc_load.rs:14` and `native/weft-loro-ffi/fuzz/fuzz_targets/loro_apply_update.rs:13`.
- **Findings**: Medium M1: the declared header and native probe functions are missing.

### T033 - Weft.Loro adapter and native versioning

- **File(s)**: `src/Weft.Loro/LoroEngine.cs:8`, `src/Weft.Loro/LoroDoc.cs:8`, `src/Weft.Loro/Interop/NativeMethods.cs:6`
- **Status**: Partial
- **Verification**:
  - Implementation read: Yes.
  - Flow traced: `LoroEngine` creates and loads docs at `src/Weft.Loro/LoroEngine.cs:27` and `src/Weft.Loro/LoroEngine.cs:30`; `LoroDoc` maps text operations to FFI at `src/Weft.Loro/LoroDoc.cs:26`, `src/Weft.Loro/LoroDoc.cs:39`, and `src/Weft.Loro/LoroDoc.cs:51`; export/import paths call FFI at `src/Weft.Loro/LoroDoc.cs:62`, `src/Weft.Loro/LoroDoc.cs:70`, `src/Weft.Loro/LoroDoc.cs:78`, and `src/Weft.Loro/LoroDoc.cs:87`; P/Invoke bindings list only core functions at `src/Weft.Loro/Interop/NativeMethods.cs:10`.
  - Tests found: the shared suite binds Loro at `tests/Weft.Versioning.Tests/LoroVersioningTests.cs:10`; CI runs the suite at `.github/workflows/ci.yml:185`.
- **Findings**: Medium M1: `LoroNativeVersioning` is absent and `LoroEngine.NativeVersioning` returns null.

### T034 - Loro dual-engine suite and blocking CI gate

- **File(s)**: `tests/Weft.Versioning.Tests/LoroVersioningTests.cs:10`, `.github/workflows/ci.yml:169`
- **Status**: Implemented for the content-addressed versioning path
- **Verification**:
  - Implementation read: Yes.
  - Flow traced: Loro test class inherits the same `VersioningSuiteBase` at `tests/Weft.Versioning.Tests/LoroVersioningTests.cs:10`; CI `dual-engine` job builds shims at `.github/workflows/ci.yml:182` and runs `dotnet test tests/Weft.Versioning.Tests/ --configuration Release` at `.github/workflows/ci.yml:185`.
  - Tests found: shared suite at `tests/Weft.Versioning.Tests/VersioningSuiteBase.cs:11`.
- **Findings**: None beyond M1 for the omitted native probes.

### T035 - ASan matrix covers Loro

- **File(s)**: `.github/workflows/ci.yml:49`, `native/Cargo.toml:3`
- **Status**: Implemented
- **Verification**:
  - Implementation read: Yes.
  - Flow traced: Rust workspace includes both shims at `native/Cargo.toml:5`; ASan job runs workspace-wide cargo test without `-p` filtering at `.github/workflows/ci.yml:62` and `.github/workflows/ci.yml:68`.
  - Tests found: Loro memory tests at `native/weft-loro-ffi/tests/mem_asan.rs:1`.
- **Findings**: None.

### Charter documentation tasks

- **File(s)**: `.straymark/charters/02-versioning-dual-engine.md:120`, `.straymark/07-ai-audit/agent-logs/AILOG-2026-07-10-002-charter-02-versionado-content-addressed-dual-engin.md:1`
- **Status**: Partial
- **Verification**:
  - Implementation read: Yes.
  - Flow traced: the Charter requires AILOG plus AIDEC for new implementation decisions at `.straymark/charters/02-versioning-dual-engine.md:125`; an AILOG exists and records decisions at `.straymark/07-ai-audit/agent-logs/AILOG-2026-07-10-002-charter-02-versionado-content-addressed-dual-engin.md:63`; no `AIDEC-*.md` files were found under `.straymark/07-ai-audit/decisions/`.
  - Tests found: not applicable.
- **Findings**: Medium M2: required AIDEC is missing.

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
| M1 | T032/T033 are marked complete, but the declared Loro native-versioning/probe/header surface is absent. | `.straymark/charters/02-versioning-dual-engine.md:35` | implementation_gap | The Charter declares Loro probes and `LoroNativeVersioning` at `.straymark/charters/02-versioning-dual-engine.md:35` and `.straymark/charters/02-versioning-dual-engine.md:37`; tasks mark probes/header and `LoroNativeVersioning` complete at `specs/001-weft-crdt-versioning/tasks.md:85` and `specs/001-weft-crdt-versioning/tasks.md:86`; quickstart expects `LoroEngine.NativeVersioning != null` at `specs/001-weft-crdt-versioning/quickstart.md:86`; the actual engine returns null at `src/Weft.Loro/LoroEngine.cs:24`, P/Invoke lists no probe imports at `src/Weft.Loro/Interop/NativeMethods.cs:10`, and Rust exports stop at core ABI/test hooks (`native/weft-loro-ffi/src/lib.rs:325`, `native/weft-loro-ffi/src/lib.rs:333`). | Either implement `LoroNativeVersioning`, C ABI probes, and the declared header, or update the Charter/spec/task list to explicitly defer this scope before closure. |
| M2 | The Charter required an AIDEC for new implementation decisions, but no AIDEC exists. | `.straymark/charters/02-versioning-dual-engine.md:125` | implementation_gap | The Charter requires an AIDEC for tokenization, sharding, and Loro capability mapping decisions at `.straymark/charters/02-versioning-dual-engine.md:125`; the AILOG records those decisions in prose at `.straymark/07-ai-audit/agent-logs/AILOG-2026-07-10-002-charter-02-versionado-content-addressed-dual-engin.md:63`, including `NativeVersioning = null` at `.straymark/07-ai-audit/agent-logs/AILOG-2026-07-10-002-charter-02-versionado-content-addressed-dual-engin.md:69`; no `AIDEC-*.md` file was found under `.straymark/07-ai-audit/decisions/`. | Create the required AIDEC or amend the Charter with an explicit rationale for why the decisions were downgraded to AILOG-only documentation. |

### Low (quality, naming, style improvements)

| # | Finding | File:Line | Category | Evidence | Suggested remediation |
|---|---------|-----------|----------|----------|----------------------|

## Out-of-scope notes

| Observation | Relevant Charter / area | Note |
|-------------|-------------------------|------|
| The yrs UTF-16 indexing fix is outside this Charter's declared files but documented as a scope expansion. | CHARTER-01 regression / CHARTER-02 AILOG | The AILOG explains the latent bug and correction at `.straymark/07-ai-audit/agent-logs/AILOG-2026-07-10-002-charter-02-versionado-content-addressed-dual-engin.md:93`; the regression tests are in `tests/Weft.Core.Tests/Utf16IndexingTests.cs:11`. |

## Charter closure assessment

Partial. The core content-addressed versioning implementation and blocking P-III/P-IV CI gates are present: `VersionStore` publishes and verifies content-addressed blobs at `src/Weft.Versioning/VersionStore.cs:27` and `src/Weft.Versioning/VersionStore.cs:81`; Loro inherits the same versioning suite at `tests/Weft.Versioning.Tests/LoroVersioningTests.cs:10`; CI has blocking `determinism` and `dual-engine` jobs at `.github/workflows/ci.yml:148` and `.github/workflows/ci.yml:169`.

However, strict closure is not met as written because the Charter and tasks declare native Loro probes, `LoroNativeVersioning`, a header, and an AIDEC. The implementation deliberately returns `NativeVersioning => null` at `src/Weft.Loro/LoroEngine.cs:24`, and the AILOG records that deferral at `.straymark/07-ai-audit/agent-logs/AILOG-2026-07-10-002-charter-02-versionado-content-addressed-dual-engin.md:69`; that is a reasonable product decision, but it conflicts with the current Charter/spec until the scope is reconciled. The Charter also still requires external audit consolidation before close at `.straymark/charters/02-versioning-dual-engine.md:133`.

## Conclusion

The implementation is strong for the engine-agnostic versioning path and the dual-engine gate that actually exercises `VersionStore`. The remaining work is scope reconciliation: either implement the native Loro probe surface and create the missing AIDEC, or explicitly defer those items in the Charter/spec so closure matches the real delivered surface.
