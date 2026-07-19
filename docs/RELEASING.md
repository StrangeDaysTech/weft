<!-- SPDX-License-Identifier: Apache-2.0 -->

# Releasing Weft

How a maintainer cuts a NuGet release. Publishing is a **deliberate, irreversible, operator-gated
act**: NuGet packages cannot be un-published (only unlisted), and a public repo stays indexed.

The pipeline lives in [`.github/workflows/release.yml`](../.github/workflows/release.yml) — it is
`workflow_dispatch`-only, and the publish job is gated behind `dry_run=false` plus the `release`
environment. Authentication uses **NuGet Trusted Publishing** (OIDC), so there is no long-lived API
key to manage.

## Prerequisites

**One-time (already done — listed for the record):**

- NuGet Trusted Publishing policy `Active` on nuget.org: package owner = the *StrangeDaysTech*
  organization, repository owner = `StrangeDaysTech`, repository = `weft`, workflow = `release.yml`,
  environment = `release`.
- GitHub `release` environment configured with:
  - Secret **`NUGET_USER = StrangeDaysTech`** (the nuget.org **org** username — must match the policy's
    package owner; *not* a personal account).
  - **Required reviewers** (a maintainer) — pauses the publish job for manual approval.
  - Deployment branch restricted to `main`.

**Every release:**

- `main` is green in CI (including the `third-party-notices` license gate).
- A SemVer version number is chosen. An engine encoding change is breaking and bumps the major
  (see [`CONTRIBUTING.md`](../CONTRIBUTING.md#engine-bump-protocol-yrs--loro--research-r16)).

## Steps

1. **Final dry-run** — builds, packs, and pack-smokes across all four RIDs without publishing. Wait for
   green. (The dry-run does *not* exercise the OIDC login; that only runs in the real publish.)

   ```bash
   gh workflow run release.yml -f dry_run=true -f version=X.Y.Z
   ```

2. **First release only:** make the repository **public** (Settings → General → Danger Zone → Change
   visibility). Needed so SourceLink / symbols resolve for consumers.

3. **Publish.**

   ```bash
   gh workflow run release.yml -f dry_run=false -f version=X.Y.Z
   ```

4. **Approve the gate.** GitHub pauses the `publish` job for the `release` environment's required
   reviewers — approve it in the Actions tab.

5. The pipeline then runs automatically: `NuGet/login` (OIDC → short-lived 1 h API key) →
   `dotnet nuget push` of the six packages → `git tag vX.Y.Z` + a GitHub Release with generated notes.

## Post-publish verification

- The six packages appear on nuget.org under **StrangeDaysTech**: `Weft.Core`, `Weft.Versioning`,
  `Weft.Server`, `Weft.Loro`, `Weft.Server.Persistence.EFCore`, `Weft.Server.Persistence.Redis`.
- The GitHub Release `vX.Y.Z` exists with the `.nupkg` assets attached.
- **First release only:** reserve the `Weft.*` ID prefix on nuget.org now that the IDs exist.

## Troubleshooting

- **OIDC login fails** — confirm `NUGET_USER = StrangeDaysTech` (not a personal account) and that the
  Trusted Publishing policy is still `Active`. The push runs *after* login, so a failed login publishes
  nothing partial.
- **A package version already exists** — `--skip-duplicate` skips it; re-runs are safe and never
  re-publish.
- **Private-repo policy pending** — a policy on a private repo may start "temporarily active (7 days)"
  until the first successful publish binds it to GitHub's immutable IDs.
