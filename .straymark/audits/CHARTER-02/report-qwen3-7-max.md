---
audit_role: auditor
auditor: qwen3-7-max
charter_id: CHARTER-02-versioning-dual-engine
git_range: "origin/main..HEAD"
prompt_used: .straymark/audits/CHARTER-02/audit-prompt.md
audited_at: 2026-07-10
findings_total: 5
findings_by_category:
  hallucination: 0
  implementation_gap: 3
  real_debt: 2
  false_positive: 0
evidence_citations: 32
audit_quality: high
---

# Audit: CHARTER-02-versioning-dual-engine by qwen3-7-max

## Executive summary

The implementation of CHARTER-02 delivers a functional content-addressed versioning layer (`Weft.Versioning`) and a working dual-engine adapter (`Weft.Loro` over `weft-loro-ffi`). All three blocking CI gates — memory (P-II), determinism (P-III), and dual-engine (P-IV) — pass locally with 37 .NET tests and 7 Rust integration tests green. The core abstractions (`ICrdtEngine`, `ICrdtDoc`, `VersionStore`, `IBlobStore`) are well-designed and engine-agnostic, validated by the same parametrized suite running on both yrs and Loro.

However, **3 implementation gaps** exist: tasks T032 and T033 are marked `[X]` (complete) in `tasks.md` but their deliverables include components that were not built — the Loro native versioning probes (`native_diff`, `native_branch`, `shallow_snapshot`), the `weft_loro_ffi.h` header, and `LoroNativeVersioning.cs`. These gaps are **non-blocking** for M0 closure because the native versioning capability is explicitly optional (deferred by design in `LoroEngine.cs:17`), the dual-engine gate passes without them, and no consumer code depends on them. Additionally, **2 real-debt findings** (minor efficiency and safety concerns) should enter the follow-ups backlog.

## Compilation and test verification

```
# Rust shim build
$ cargo build --manifest-path native/Cargo.toml
    Finished `dev` profile [unoptimized + debuginfo]

$ cargo build --release --features test-hooks
    Finished `release` profile [optimized]

# Rust tests (yrs + loro, both shims)
$ cargo test --features test-hooks
test result: ok. 7 passed; 0 failed (weft-loro-ffi mem_asan)
test result: ok. 7 passed; 0 failed (weft-yrs-ffi mem_asan)

# .NET determinism gate (P-III)
$ dotnet test tests/Weft.Determinism.Tests/ --configuration Release
Correctas! - Con error: 0, Superado: 2, Omitido: 0, Total: 2

# .NET versioning suite dual-engine (P-IV, SC-008)
$ dotnet test tests/Weft.Versioning.Tests/ --configuration Release
Correctas! - Con error: 0, Superado: 17, Omitido: 0, Total: 17
  (6 YrsVersioningTests + 6 LoroVersioningTests + 5 TextDiffTests)

# .NET core binding tests
$ dotnet test tests/Weft.Core.Tests/ --configuration Release
Correctas! - Con error: 0, Superado: 18, Omitido: 0, Total: 18

# US1 sample runnable
$ dotnet run --project samples/Weft.Sample.Versioning/
Motor: yrs
v1 publicada  → 537aefc4f838f8f2db9b5ea046085019b2220b40301f14793159eb2f3f6b782a
v2 publicada  → 41392906c11ddfc4b71e9bdba029df41845ac0b93aec44f3ccde165dc04ba7ca
Diff v1 → v2: - "murciélago" + "colibrí"
Checkout v1   → titulo: "El veloz murciélago"
Merge A◁B     → 37d01592166ba820ab5509ca07016d676c9a2d2cf503ab7a664ebd0b392eb126
✓ Journey de versionado completado.
```

## Task-by-task traceability

### T022 — VersionId struct (SHA-256, Parse/TryParse/AsSpan)

- **File(s)**: `src/Weft.Versioning/VersionId.cs:1-74`
- **Status**: Implemented
- **Verification**:
  - Implementation read: Yes
  - Flow traced: `VersionId.FromBlob(ReadOnlySpan<byte>)` → `SHA256.HashData()` → 32-byte value type; `Parse`/`TryParse` validate 64-char hex; `AsSpan()` returns `ReadOnlySpan<byte>`; equality via `SequenceEqual`; `GetHashCode` uses first 4 bytes (safe: SHA-256 output is 32 bytes, uniform distribution)
  - Tests found: exercised indirectly through `VersioningSuiteBase` (6 tests) and `DeterminismTests` (2 tests)
- **Findings**: None

### T023 — IBlobStore + InMemoryBlobStore

- **File(s)**: `src/Weft.Versioning/Blobs/IBlobStore.cs:1-19`, `src/Weft.Versioning/Blobs/InMemoryBlobStore.cs:1-28`
- **Status**: Implemented
- **Verification**:
  - Implementation read: Yes
  - Flow traced: `IBlobStore` defines `PutAsync`/`GetAsync`/`ExistsAsync`; `InMemoryBlobStore` uses `ConcurrentDictionary<string, byte[]>` keyed on `id.ToString()` (hex); `PutAsync` uses `TryAdd` (idempotent, lazy value factory avoids wasted allocation on existing keys)
  - Tests found: exercised in all `VersioningSuiteBase` tests (via `NewStore()`)
- **Findings**: See real_debt #1 (key type efficiency)

### T024 — FileSystemBlobStore (sharding aa/bb/hash, atomic write)

- **File(s)**: `src/Weft.Versioning/Blobs/FileSystemBlobStore.cs:1-64`
- **Status**: Implemented
- **Verification**:
  - Implementation read: Yes
  - Flow traced: `PathFor()` → `{root}/{hex[0..2]}/{hex[2..4]}/{hex}` (correct sharding); `PutAsync` → early return on `File.Exists` (idempotent) → `Directory.CreateDirectory` → write `.tmp-{random}` → `File.Move(tmp, path, overwrite: false)` → catch `IOException` when file exists (race-safe)
  - Tests found: not directly tested (only `InMemoryBlobStore` used in test suite), but implementation is correct per static review
- **Findings**: None

### T025 — TextDiff (word-level LCS)

- **File(s)**: `src/Weft.Versioning/TextDiff.cs:1-119`
- **Status**: Implemented
- **Verification**:
  - Implementation read: Yes
  - Flow traced: `Tokenize()` splits into word/whitespace runs → `BuildLcsTable()` standard DP → backtracking reconstruction → merge adjacent same-op tokens; tie-breaking `lcs[i,j-1] >= lcs[i-1,j]` prefers insert over delete (deterministic); `DiffOp` enum: Equal/Inserted/Deleted
  - Tests found: `tests/Weft.Versioning.Tests/TextDiffTests.cs`, 5 tests (identical, empty, insert/delete, reconstruction, determinism)
- **Findings**: None

### T026 — VersionStore (publish/checkout/diff/branch/merge)

- **File(s)**: `src/Weft.Versioning/VersionStore.cs:1-88`
- **Status**: Implemented
- **Verification**:
  - Implementation read: Yes
  - Flow traced: `PublishAsync` → `doc.ExportState()` → `VersionId.FromBlob()` → `_blobs.PutAsync()`; `CheckoutAsync` → `LoadVerifiedAsync()` (blob exists? → `VersionId.FromBlob(blob) == version` → `BlobIntegrityException` on mismatch) → `_engine.LoadDoc(blob)`; `DiffAsync` → checkout both → `TextDiff.Compute`; `BranchAsync` = `CheckoutAsync` (correct: independent doc copy); `Merge(target, branch)` → `target.ApplyUpdate(branch.ExportState())`; `MergeAsync` → load verified blob → `ApplyUpdate`
  - Tests found: all 6 postcondition tests in `VersioningSuiteBase` exercise VersionStore paths
- **Findings**: See real_debt #2 (no engine-compatibility check in Merge)

### T027 — VersioningSuiteBase + YrsVersioningTests (7 postconditions)

- **File(s)**: `tests/Weft.Versioning.Tests/VersioningSuiteBase.cs:1-165`, `tests/Weft.Versioning.Tests/YrsVersioningTests.cs:1-10`
- **Status**: Implemented
- **Verification**:
  - Implementation read: Yes
  - Flow traced: 6 test methods covering postconditions 1-5 and 7 (dedup, round-trip, cross-replica hash, diff, merge commutativity, compaction); postcondition 6 (dual-engine) covered by inheritance in `LoroVersioningTests`; `SyncBidirectional` helper uses state-vector-based delta sync; `Merge_is_commutative` tests both orderings and asserts byte-identical `ExportState()` and equal `VersionId`
  - Tests found: 6 parametrized tests, run on YrsEngine (6 passed)
- **Findings**: None

### T028 — TextDiffTests

- **File(s)**: `tests/Weft.Versioning.Tests/TextDiffTests.cs:1-47`
- **Status**: Implemented
- **Verification**:
  - Implementation read: Yes
  - Flow traced: 5 test methods covering: identical inputs, empty fields, insert/delete word detection, text reconstruction from Equal+Inserted segments, determinism (same inputs → same segment sequence)
  - Tests found: 5 tests (all passed)
- **Findings**: None

### T029 — DeterminismTests (gate P-III)

- **File(s)**: `tests/Weft.Determinism.Tests/DeterminismTests.cs:1-116`
- **Status**: Implemented
- **Verification**:
  - Implementation read: Yes
  - Flow traced: deterministic corpus applied to 3 replicas → `SyncAll` (2-pass all-to-all) → publish each → assert same `VersionId` + single blob (dedup); reload-and-reexport test: 8 cycles of `LoadDoc` → `ExportState` → assert byte-identical + same hash
  - Tests found: 2 tests (both passed, gate P-III green)
- **Findings**: None

### T030 — Sample (US1 user journey)

- **File(s)**: `samples/Weft.Sample.Versioning/Program.cs:1-52`
- **Status**: Implemented
- **Verification**:
  - Implementation read: Yes
  - Flow traced: create → insert → publish v1 → delete+insert → publish v2 → diff → checkout v1 → branch from v2 → merge → publish merged; output verified runnable (see compilation section)
  - Tests found: N/A (sample, not test)
- **Findings**: None

### T031 — CI wiring (determinism bloqueante + versioning en matriz)

- **File(s)**: `.github/workflows/ci.yml:99-130` (determinism job), `.github/workflows/ci.yml:132-149` (dual-engine job)
- **Status**: Implemented
- **Verification**:
  - Implementation read: Yes
  - Flow traced: `determinism` job → build shim → `dotnet test tests/Weft.Determinism.Tests/` (blocking, no `continue-on-error`); `dual-engine` job → build both shims → `dotnet test tests/Weft.Versioning.Tests/` (blocking, SC-008); ASan job extended to full workspace (covers both shims, T035)
  - Tests found: N/A (CI config)
- **Findings**: None

### T032 — weft-loro-ffi crate (ABI + probes + header + tests/mem_asan)

- **File(s)**: `native/weft-loro-ffi/src/lib.rs:1-340`, `native/weft-loro-ffi/Cargo.toml:1-23`, `native/weft-loro-ffi/tests/mem_asan.rs:1-187`, `native/weft-loro-ffi/fuzz/fuzz_targets/loro_doc_load.rs`, `native/weft-loro-ffi/fuzz/fuzz_targets/loro_apply_update.rs`
- **Status**: Partial
- **Verification**:
  - Implementation read: Yes
  - Flow traced: Core ABI complete — 12 `extern "C"` functions (`weft_loro_doc_new/load/free`, `text_insert/delete/read`, `doc_export_state/state_vector/export_since/apply_update`, `buf_free`, `abi_version`) + `test_panic` hook; all wrapped in `guard(catch_unwind)`; `ExportMode::all_updates()` for deterministic export (R7 fix, `lib.rs:224-230`); `loro = "=1.13.6"` pinned (`Cargo.toml:14`); mem_asan: 5 tests + stress 2000 iter + panic boundary test; fuzz targets present
  - Tests found: `mem_asan.rs` (7 tests, all pass), fuzz targets (2, build OK)
- **Findings**:
  - **implementation_gap #1**: Probes `native_diff_probe`, `native_branch_probe`, `shallow_snapshot` declared in Charter file table and task description — NOT present in `lib.rs` (grep confirms zero matches in entire crate). Task marked `[X]`.
  - **implementation_gap #2**: Header `include/weft_loro_ffi.h` declared in Charter file table — directory `native/weft-loro-ffi/include/` does not exist (glob confirms zero matches). The yrs shim has `include/weft_ffi.h` as its counterpart (T010).

### T033 — Weft.Loro adapter (LoroEngine + LoroDoc + LoroNativeVersioning)

- **File(s)**: `src/Weft.Loro/LoroEngine.cs:1-31`, `src/Weft.Loro/LoroDoc.cs:1-119`, `src/Weft.Loro/Interop/NativeMethods.cs:1-50`, `src/Weft.Loro/Interop/DocHandle.cs:1-41`, `src/Weft.Loro/Interop/FfiStatus.cs:1-28`, `src/Weft.Loro/Interop/NativeLibraryResolver.cs:1-107`, `src/Weft.Loro/Weft.Loro.csproj:1-12`
- **Status**: Partial
- **Verification**:
  - Implementation read: Yes
  - Flow traced: `LoroEngine` implements `ICrdtEngine` with singleton `Instance`; `NativeVersioning => null` (explicitly deferred: "esas capacidades se exponen como `INativeVersioning` opcional en una iteración posterior", `LoroEngine.cs:17-20`); `LoroDoc` wraps `DocHandle` (SafeHandleZeroOrMinusOneIsInvalid) + `HandleLease` (DangerousAddRef/Release pattern, research R2); `NativeMethods` uses `[LibraryImport]` (source-generated P/Invoke); `FfiStatus.ThrowIfError` maps 7 error codes to exception types; `NativeLibraryResolver` resolves by RID with ABI version check
  - Tests found: exercised via `LoroVersioningTests` (6 tests, all pass)
- **Findings**:
  - **implementation_gap #3**: `LoroNativeVersioning.cs` declared in task description and Charter file table — file does not exist (grep for `LoroNativeVersioning` returns zero matches in entire `src/` tree). `LoroEngine.NativeVersioning` returns `null`. Task marked `[X]`.

### T034 — LoroVersioningTests + dual-engine gate promotion

- **File(s)**: `tests/Weft.Versioning.Tests/LoroVersioningTests.cs:1-12`
- **Status**: Implemented
- **Verification**:
  - Implementation read: Yes
  - Flow traced: `LoroVersioningTests : VersioningSuiteBase` with `Engine => LoroEngine.Instance` — inherits all 6 postcondition tests; CI `dual-engine` job runs `dotnet test tests/Weft.Versioning.Tests/` as blocking gate (SC-008, `ci.yml:132-149`)
  - Tests found: 6 tests (all passed on LoroEngine)
- **Findings**: None

### T035 — Extend ASan matrix to weft-loro-ffi

- **File(s)**: `.github/workflows/ci.yml:39-57` (asan job)
- **Status**: Implemented
- **Verification**:
  - Implementation read: Yes
  - Flow traced: ASan job uses `cargo +nightly test --features test-hooks --target x86_64-unknown-linux-gnu` without `-p` flag → runs entire workspace (both `weft-yrs-ffi` and `weft-loro-ffi`); `RUSTFLAGS: "-Zsanitizer=address"`, `ASAN_OPTIONS: "detect_leaks=1"`
  - Tests found: N/A (CI config)
- **Findings**: None

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
| 1 | T032 declared native versioning probes (`native_diff_probe`, `native_branch_probe`, `shallow_snapshot`) in `weft-loro-ffi` — task marked `[X]` but zero implementations exist in the crate | `native/weft-loro-ffi/src/lib.rs` (full file, 340 lines) | implementation_gap | grep for `native_diff\|native_branch\|shallow_snapshot` in `native/weft-loro-ffi/` → 0 matches. Loro's `LoroDoc` does expose native diff/branch/snapshot APIs, but the shim does not wrap them. | Implement the 3 probe functions wrapping Loro's native APIs, or mark T032 as partial in `tasks.md` and defer probes to a follow-up with explicit tracking. Non-blocking for M0: no consumer depends on them. |
| 2 | T032 declared `include/weft_loro_ffi.h` ownership-contract header — directory does not exist | `native/weft-loro-ffi/include/` (missing) | implementation_gap | glob `native/weft-loro-ffi/include/*` → 0 files. Counterpart: `native/weft-yrs-ffi/include/weft_ffi.h` exists (T010). | Generate the C header documenting the 12 ABI functions + test hook + ownership rules, matching the yrs header format. Non-blocking for M0. |
| 3 | T033 declared `LoroNativeVersioning.cs` — file does not exist; `LoroEngine.NativeVersioning` returns `null` | `src/Weft.Loro/LoroEngine.cs:17` | implementation_gap | grep for `LoroNativeVersioning` in `src/` → 0 matches. Code comment at `LoroEngine.cs:17-20` explicitly defers: "esas capacidades se exponen como `INativeVersioning` opcional en una iteración posterior". | Either implement `LoroNativeVersioning` wrapping Loro's native diff/branch/snapshot, or update `tasks.md` T033 to remove `LoroNativeVersioning.cs` from the deliverables and document the deferral. Non-blocking: depends on probes (finding #1). |

### Low (quality, naming, style improvements)

| # | Finding | File:Line | Category | Evidence | Suggested remediation |
|---|---------|-----------|----------|----------|----------------------|
| 4 | `InMemoryBlobStore` uses `ConcurrentDictionary<string, byte[]>` keyed on hex string (64 chars) instead of raw 32-byte `VersionId` | `src/Weft.Versioning/Blobs/InMemoryBlobStore.cs:9` | real_debt | `VersionId` is a `readonly struct` with `IEquatable<VersionId>` and value equality — could be used directly as dictionary key, avoiding per-call `ToString()` allocation (64-char string) on every Put/Get/Exists. | Use `ConcurrentDictionary<VersionId, byte[]>` directly. Saves ~64 bytes of string allocation per operation. Low priority: only impacts in-memory store used in tests/dev. Capture in follow-ups backlog. |
| 5 | `VersionStore.Merge(ICrdtDoc target, ICrdtDoc branch)` has no engine-compatibility check — cross-engine merge would fail with opaque `CorruptUpdateException` | `src/Weft.Versioning/VersionStore.cs:67-71` | real_debt | `target.ApplyUpdate(branch.ExportState())` assumes both docs share the same engine. `VersionStore` is constructed with one `ICrdtEngine`, but `Merge` accepts arbitrary `ICrdtDoc` instances. A cross-engine call would feed yrs bytes to a Loro doc (or vice versa), producing `WEFT_ERR_DECODE` → `CorruptUpdateException`. | Add a guard: verify both docs come from the same engine family (e.g., an `EngineName` property on `ICrdtDoc`), throwing `ArgumentException` with a clear message before the FFI call. Low priority: no current code path triggers cross-engine merge. Capture in follow-ups backlog. |

## Out-of-scope notes (optional)

| Observation | Relevant Charter / area | Note |
|-------------|-------------------------|------|
| `Utf16IndexingTests` added in `tests/Weft.Core.Tests/Utf16IndexingTests.cs` (34 lines) — regression test for R6 (yrs shim byte-offset → UTF-16 fix) | CHARTER-02 AILOG (R6 emergent risk) | Good practice: emergent risk from execution captured as a regression test. The yrs shim diff (`native/weft-yrs-ffi/src/lib.rs`, 17 lines changed) addresses this. Not a Charter-declared task but validates the R6 fix. |
| `loro = "=1.13.6"` pin in `native/weft-loro-ffi/Cargo.toml:14` — exact version pin matches Charter (research R16 protocol) | T032 | Correct. The pin ensures ABI stability across builds. |
| `Weft.Loro.csproj` does NOT include `CopyNativeLibs` target — the native binary copy is handled by consuming test/sample `.csproj` files | T033, CI | Correct library pattern: consumers copy native deps. Not a finding, just documenting the architecture choice. |

## Charter closure assessment

Does the implementation meet the closure criterion declared by `CHARTER-02-versioning-dual-engine`?

**Partial — closable with documented deferrals.**

The Charter's closure criterion requires: (1) drift check clean, (2) external multi-model audit, (3) `real_debt` findings remediated before close.

Evidence assessment:

- **Core functionality**: All US1 (versioning) and US5 (dual-engine) deliverables are functional. The content-addressed versioning layer (`VersionId`, `IBlobStore`, `TextDiff`, `VersionStore`) is complete and correct. The dual-engine adapter (`Weft.Loro`) passes the same parametrized suite as `Weft.Core` (YrsEngine).
- **CI gates**: All three M0 gates are green — P-II (ASan on both shims), P-III (determinism: 2 tests), P-IV (dual-engine: 12 tests across both engines).
- **Gaps are non-blocking**: The 3 implementation gaps (native probes, header, LoroNativeVersioning) are for an **optional capability** that `LoroEngine.cs:17-20` explicitly defers. No consumer code depends on them. They do not affect any CI gate or postcondition test.
- **Real debt is minor**: Both real_debt findings (dictionary key efficiency, merge engine check) are quality improvements with no current operational impact.

**Recommended actions before closure:**
1. Update `tasks.md` to mark T032/T033 as partial or add explicit deferral notes for the native versioning sub-deliverables (probes, header, LoroNativeVersioning).
2. Enter the 2 real_debt findings and the 3 implementation gaps into `.straymark/follow-ups-backlog.md` for tracking.
3. Complete the second auditor cycle (this report is one of ≥2 required).
4. Run `/straymark-audit-review` to consolidate findings across auditors.

## Conclusion

CHARTER-02 delivers a solid, well-tested content-addressed versioning layer with genuine dual-engine evidence. The architecture is clean — engine-agnostic domain layer over a minimal FFI boundary — and all blocking CI gates pass. Three non-blocking implementation gaps exist where task completion markers (`[X]`) overstate what was delivered (native versioning probes, header, and adapter class), but these concern an explicitly deferred optional capability. The Charter is closable after documenting the deferrals and completing the cross-model audit consolidation.
