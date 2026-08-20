# Release runbook

This is the reusable release runbook for unity-mmd-loader. TODO.md owns the
active queue, while this file owns the release evidence and approval boundaries
and lists the required release gates directly. The version, tag, native
candidate, and checkout are operator inputs; this document does not prescribe a
particular release number.

## Operator inputs

Set these values before creating evidence. Replace every placeholder; do not
infer a version from an existing tag or silently follow a moving remote branch.

~~~powershell
$RepoRoot = "F:\Develop\MMDDev\unity-mmd-loader"
$ReleaseRef = "<release-branch-or-commit>"
$Version = "<package-semver>"
$Tag = "v$Version"
$ApprovedMergeCommit = "<40-hex-approved-main-merge>"
$Worktree = "F:\Develop\MMDDev\unity-mmd-loader-release-$Version"

# Repository facts used by the current gates. Change only when the repository
# or workflow changes and record the changed values in the evidence bundle.
$LtsEditorVersion = "6000.0.80f1"
$CurrentEditorVersion = "6000.4.8f1"
$LtsUnityExe = "C:\Program Files\Unity\Hub\Editor\$LtsEditorVersion\Editor\Unity.exe"
# Override this path when the selected current Unity executable is elsewhere.
$CurrentUnityExe = "C:\Program Files\Unity\Hub\Editor\$CurrentEditorVersion\Editor\Unity.exe"

# Explicit native candidate selected by the release owner.
$NativeCandidateSha = "<40-hex-mmd-anim-commit>"
$NativeAbiVersion = 3
~~~

packages/com.yohawing.mmd-loader/package.json must contain $Version; its
minimum supported Unity is 6000.0, while the current CI/editor compatibility
pass is Unity 6000.4.8f1. The current LTS compatibility script defaults to
6000.0.80f1 and invokes the current-editor visual gate. If either editor is
not installed at the documented path, pass explicit -Unity/-VisualUnity
paths or stop and obtain owner approval for the changed environment.

## Scope and release blockers

The public golden path is: import a redistributable PMX, place it in a Scene,
import a VMD onto an MMD VMD Timeline track, scrub in Edit Mode with physics
off, then play forward in Play Mode with Live physics. The Basic Playback sample
is the human-facing fixture. An explicit, prerequisite-gated Humanoid
AnimationClip bake remains an optional step when the model is Humanoid-ready.

Blockers are failures of that path, package metadata/native layout, or required
evidence:

- PMX import/placement, VMD import/Timeline binding, Edit Mode animation-only
  scrub, or Play Mode forward playback fails on the release fixture.
- The explicit Humanoid bake entry point is missing or no longer prerequisite
  gated.
- Default URP rendering cannot show the placed model with textures, material
  order, and outline smoke coverage.
- Package metadata, assembly layout, or the Windows native plugin prevents a
  clean consumer import.
- The native candidate bundle below is incomplete, ABI-incompatible, or lacks
  passing packaged-native parity evidence.
- Any required NUnit XML is missing, malformed, empty, internally inconsistent,
  below its minimum counts, failed, inconclusive, invalid, or contains a skip
  outside the exact reviewed allowlist. A skipped Unity job caused by a missing
  license is not release evidence.

The following remain non-blocking unless new evidence shows that they break the
golden path: SelfShadow visual polish, reference screenshot/SDEF/QDEF/sphere
parity, Physics Cache, weighted raw-VMD blending, optional Unity Toon Shader
polish, RuntimeVerification recents/seek polish, and macOS/Linux binaries.
The former raw-bone sampling API wait is resolved in the current native runtime;
do not carry it forward as a release blocker or a reason to defer this runbook.

The compatibility surface is checked by
`CompatibilitySurfaceContractTests` before merge. This test pins serialized
asset fields and types, selected public API signatures, diagnostics, assembly
definitions, and native entry-point names. A refactor that changes one of these
surfaces must include an explicit migration decision and updated consumer
evidence; passing only a compile check is insufficient.

## Candidate and evidence bundle

Native selection is explicit and immutable for a release candidate. Do not run a
"latest remote" command and do not adopt a tag merely because it is newer. The
release owner chooses $NativeCandidateSha, fetches that exact object, and
records all of the following together:

| Field | Required value/evidence |
| --- | --- |
| Native candidate SHA | Full 40-hex commit in native/mmd-anim. |
| Parent gitlink | The superproject native/mmd-anim gitlink at the candidate commit; it must equal the candidate SHA. |
| Packaged DLL hash | SHA-256 of packages/com.yohawing.mmd-loader/Runtime/Plugins/x86_64/mmd_runtime_ffi.dll produced from that checkout. |
| ABI | The accepted runtime ABI (3 today), confirmed by the package/native contract tests. |
| Parity evidence | Passing run-mmd-anim-cli-parity-report.ps1 report, results XML, log, and report hash. |
| Build metadata | Package version, superproject commit, requested and observed Unity editor ProductVersion values, OS/CPU, Rust/cargo version, and build feature flags. |

In the isolated checkout, the shape of the candidate operation is:

~~~powershell
git -C $Worktree submodule update --init --recursive native/mmd-anim
if ($LASTEXITCODE -ne 0) {
  throw "Submodule initialization failed; refusing to inspect candidate state"
}
git -C "$Worktree\native\mmd-anim" fetch --no-tags origin $NativeCandidateSha
if ($LASTEXITCODE -ne 0) {
  throw "Native candidate fetch failed; refusing checkout of stale state"
}
git -C "$Worktree\native\mmd-anim" checkout --detach $NativeCandidateSha
if ($LASTEXITCODE -ne 0) {
  throw "Native candidate checkout failed; refusing to inspect stale state"
}
$ParentTreeLine = git -C $Worktree ls-tree HEAD native/mmd-anim
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($ParentTreeLine)) {
  throw "Parent native gitlink lookup failed"
}
~~~

Update the parent gitlink in the release change and verify it before building.
The parent gitlink in HEAD and the checked-out submodule must both equal the
explicit candidate; fail closed before invoking the build helper:

~~~powershell
$ParentGitlink = ((git -C $Worktree ls-tree HEAD native/mmd-anim) -split '\s+')[2]
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($ParentGitlink)) {
  throw "Parent native gitlink lookup failed before build"
}
$SubmoduleHead = (git -C "$Worktree\native\mmd-anim" rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($SubmoduleHead)) {
  throw "Submodule HEAD lookup failed before build"
}
if ($ParentGitlink -ne $NativeCandidateSha -or $SubmoduleHead -ne $NativeCandidateSha) {
  throw "Native candidate pin mismatch before build: parent=$ParentGitlink submodule=$SubmoduleHead candidate=$NativeCandidateSha"
}
~~~

The maintainer-local scripts/build-mmd-runtime-ffi.ps1 is gitignored; it
initializes the already-pinned submodule, builds mmd-anim-ffi, and copies the
DLL into the package plugin directory. It does not choose a remote commit. The
script uses rtk cargo when rtk is installed and plain cargo otherwise; rtk is
optional for every runbook command.

After the build and parity gate, record the bundle under artifacts/ (never in
the public package by accident). For example, capture the values with the
equivalent of:

~~~powershell
$Dll = Join-Path $Worktree "packages/com.yohawing.mmd-loader/Runtime/Plugins/x86_64/mmd_runtime_ffi.dll"
$DllSha256 = (Get-FileHash -LiteralPath $Dll -Algorithm SHA256).Hash.ToLowerInvariant()
$SubmoduleHeadAfterBuild = (git -C "$Worktree\native\mmd-anim" rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($SubmoduleHeadAfterBuild)) {
  throw "Submodule HEAD lookup failed after build"
}
if ($SubmoduleHeadAfterBuild -ne $NativeCandidateSha) {
  throw "Native candidate changed during build: submodule=$SubmoduleHeadAfterBuild candidate=$NativeCandidateSha"
}
~~~

The evidence record must include $NativeCandidateSha, $ParentGitlink,
$DllSha256, $NativeAbiVersion, the parity report path/hash, and the metadata
listed above. A missing or mismatched field is a failed gate, not an inferred
value.

## Isolated worktree and checkout checks

Release preparation starts in a clean, disposable worktree. Keep the normal
development checkout and any open Unity Editor untouched.

~~~powershell
git -C $RepoRoot worktree add --detach $Worktree $ReleaseRef
if ($LASTEXITCODE -ne 0) { throw "Isolated worktree creation failed" }
git -C $Worktree status --porcelain=v1       # must be empty before edits
if ($LASTEXITCODE -ne 0) { throw "Isolated worktree status check failed" }
git -C $Worktree submodule status --recursive
if ($LASTEXITCODE -ne 0) { throw "Isolated submodule status check failed" }

$Manifest = Get-Content (Join-Path $Worktree "packages/com.yohawing.mmd-loader/package.json") -Raw | ConvertFrom-Json
$InitialPackageVersion = $Manifest.version
Write-Host "Initial package version before release edits: $InitialPackageVersion"
if ($Tag -ne "v$Version") { throw "Tag must be v<package-semver>" }
~~~

The release PR may intentionally change the package manifest/changelog, native
gitlink, and packaged DLL. Before each machine gate, verify that no unrelated
tracked changes exist and that ignored output is confined to artifacts/,
data-local/, or other documented local-only paths. Do not reuse a worktree
with a stale Unity lock or an open Editor.

## Machine gates (CLI-only)

Run gates serially from the candidate worktree. Unity-backed commands must not
share a project concurrently. Keep logs, test XML, parity JSON, and visual
captures below artifacts/.

The following distinction is intentional:

- Tracked release helpers: scripts/unity-lts-compatibility-gate.ps1,
  scripts/run-visual-shading-tier.ps1,
  scripts/run-local-asset-fixture-gate.ps1, and
  scripts/run-mmd-anim-cli-parity-report.ps1.
- Maintainer-local/gitignored infrastructure: scripts/check-cli.ps1,
  scripts/check-cli.full.ps1, scripts/check-cli/, scripts/unity-compile.ps1,
  scripts/unity-editmode-tests.ps1, scripts/unity-playmode-tests.ps1, and
  scripts/build-mmd-runtime-ffi.ps1. Confirm these exist in the isolated
  maintainer checkout; do not add them to the public package as part of a docs
  change.

Because git worktree does not copy ignored files, provision those maintainer
helpers into the candidate worktree (or use a wrapper that sets its repository
root) before invoking them. Verify that each helper resolves the candidate
worktree, not the normal development checkout; otherwise its logs, package DLL,
and Unity project can come from the wrong commit.

After the release manifest/changelog/native gitlink/DLL changes are prepared,
but before the first machine gate, fail closed if package.json does not contain
the requested version:

~~~powershell
$ReleaseManifest = Get-Content (Join-Path $Worktree "packages/com.yohawing.mmd-loader/package.json") -Raw | ConvertFrom-Json
if ($ReleaseManifest.version -ne $Version) {
  throw "Release package version does not match Version: $($ReleaseManifest.version) vs $Version"
}
~~~

Validate the actual Unity binaries before any Unity-backed gate. Path existence
alone is insufficient: record the observed ProductName/ProductVersion and require
the expected editor version plus an underscore suffix.

~~~powershell
$LtsUnityInfo = (Get-Item -LiteralPath $LtsUnityExe -ErrorAction Stop).VersionInfo
$CurrentUnityInfo = (Get-Item -LiteralPath $CurrentUnityExe -ErrorAction Stop).VersionInfo
if ($LtsUnityInfo.ProductName -ne "Unity" -or
    -not $LtsUnityInfo.ProductVersion.StartsWith($LtsEditorVersion + "_", [StringComparison]::OrdinalIgnoreCase)) {
  throw "LTS Unity metadata mismatch: product=$($LtsUnityInfo.ProductName) version=$($LtsUnityInfo.ProductVersion)"
}
if ($CurrentUnityInfo.ProductName -ne "Unity" -or
    -not $CurrentUnityInfo.ProductVersion.StartsWith($CurrentEditorVersion + "_", [StringComparison]::OrdinalIgnoreCase)) {
  throw "Current Unity metadata mismatch: product=$($CurrentUnityInfo.ProductName) version=$($CurrentUnityInfo.ProductVersion)"
}
$ObservedLtsUnityProductVersion = $LtsUnityInfo.ProductVersion
$ObservedCurrentUnityProductVersion = $CurrentUnityInfo.ProductVersion
Write-Host "Unity evidence: lts=$ObservedLtsUnityProductVersion current=$ObservedCurrentUnityProductVersion"
~~~

Include both observed ProductVersion values in the evidence metadata alongside
the requested editor versions.

1. Build the selected native candidate with the maintainer-local build helper
   (add -PhysicsBulletNative only when that feature is part of the candidate):

   ~~~powershell
   & (Join-Path $Worktree "scripts/build-mmd-runtime-ffi.ps1")
   if (-not $?) { throw "Native build helper failed" }
   ~~~

   Re-run the parent-gitlink and submodule-head assertions above immediately
   after the helper. A helper that resets the submodule to another gitlink is a
   failed candidate, even when the DLL build itself succeeds.

2. Run the default release gate. -Tier full is explicit; -Tier fast is a
   docs/non-Unity near-no-op and is not sufficient for a release candidate.

   ~~~powershell
   & (Join-Path $Worktree "scripts/check-cli.ps1") -Tier full
   if (-not $?) { throw "Default release gate failed" }
   ~~~

3. Run the LTS compatibility floor and retain its visual artifacts. The
   tracked LTS helper compiles/tests on $LtsEditorVersion, then invokes
   run-visual-shading-tier.ps1 with $CurrentEditorVersion. That visual gate
   requires a green capture, a perturbation red proof, and a green-after capture
   (unless an owner-approved diagnostic exception is recorded).

   ~~~powershell
   & (Join-Path $Worktree "scripts/unity-lts-compatibility-gate.ps1") -EditorVersion $LtsEditorVersion -Unity $LtsUnityExe -VisualUnity $CurrentUnityExe
   if (-not $?) { throw "Unity LTS compatibility/visual gate failed" }
   ~~~

4. Run the packaged-native parity report with every path explicit. The tracked
   parity helper has a maintainer checkout-fixed default
   (F:\Develop\MMDDev\unity-mmd-loader\unity-mmd); do not rely on that
   default when the candidate lives in another worktree. Point -ProjectPath
   at the candidate checkout's provisioned consumer and keep results/logs in
   that checkout's artifacts/parity/.

   ~~~powershell
   $ParityProject = Join-Path $Worktree "unity-mmd"
   & (Join-Path $Worktree "scripts/run-mmd-anim-cli-parity-report.ps1") -Unity $CurrentUnityExe -ProjectPath $ParityProject -ResultsFile (Join-Path $Worktree "artifacts/parity/mmd-anim-cli-parity-results.xml") -LogFile (Join-Path $Worktree "artifacts/parity/mmd-anim-cli-parity.log")
   if (-not $?) { throw "Packaged-native parity report failed" }
   ~~~

   Hash the generated report after it exists and store this value in the same
   candidate bundle as the DLL hash and native pin:

   ~~~powershell
   $ParityReport = Join-Path $Worktree "artifacts/parity/mmd-anim-cli-parity-report.json"
   if (-not (Test-Path -LiteralPath $ParityReport -PathType Leaf)) {
     throw "Parity report was not generated: $ParityReport"
   }
   $ParityReportSha256 = (Get-FileHash -LiteralPath $ParityReport -Algorithm SHA256).Hash.ToLowerInvariant()
   ~~~

   The report must compare the CLI and the packaged DLL from the same candidate
   bundle. If the checkout has no consumer project, stop and provision one or
   obtain owner direction; silently falling back to another checkout breaks
   candidate provenance.

5. Run the licensed local-asset preflight when its corpus is in release scope:

   ~~~powershell
   & (Join-Path $Worktree "scripts/run-local-asset-fixture-gate.ps1")
   if (-not $?) { throw "Local-asset preflight failed" }
   # Add -RequireLocalAssets only when the local corpus is an explicit hard gate.
   ~~~

   The default is report-only when data-local/fixtures.local.json or licensed
   files are absent. Never copy licensed paths into public artifacts.

Unity launchers initialize ALLUSERSPROFILE and ProgramData when missing.
Keep that environment setup; clearing UPM caches does not repair an environment
failure such as "The path argument must be of type string. Received undefined".

The release-only job in .github/workflows/unity-ci.yml is a separate CI signal:
the develop -> main pull request (or a manual release-gate dispatch) checks the
native gitlink against the remote main ref, builds native artifacts on its
matrix, and uses Unity 6000.4.8f1 for package tests when a license is available.
It does not select a candidate for this runbook. If that moving-ref check reports
a mismatch with the explicitly selected candidate, stop for owner direction
instead of silently adopting a newer remote commit.

The release evidence summary intentionally fails closed when `UNITY_LICENSE` is
absent, when the macOS/Windows native matrix is not successful, or when either
Unity test job is skipped or lacks exactly one strict NUnit XML result. Its
EditMode skip policy is an explicit name list (plus the reviewed UTS parameter
prefix); PlayMode has no allowlisted skips. New skip names, inconclusive tests,
zero-test XML, and hidden/malformed results must fail until reviewed.

## Public-surface and manual visual checks

Before requesting merge approval, compare the same $Version and release
scope across README.md, docs/README.ja.md, docs/HOW_TO_USE.md,
packages/com.yohawing.mmd-loader/Documentation~/README.md,
packages/com.yohawing.mmd-loader/CHANGELOG.md, and
packages/com.yohawing.mmd-loader/package.json. Sample names/paths,
Unity/URP floor, Windows native support, physics boundaries, Humanoid bake
limits, and known limitations must agree. The manifest currently declares
Basic Playback, Humanoid Playback, and Unity Toon Shader Adapter; any
sample change requires the package docs and sample-contract tests to change in
the same release.

Perform one human pass through Basic Playback in a clean consumer:

1. Import the sample through Package Manager and drag its PMX into a Scene.
2. Put its VMD on an MMD VMD Timeline track and bind the placed playback object.
3. Scrub in Edit Mode and confirm physics remains off and deterministic.
4. Enter Play Mode and confirm forward playback advances with Live physics.
5. On a Humanoid-ready model, confirm the explicit Clip-bake action is present
   and prerequisite-gated.

Review SelfShadow setup against docs/HOW_TO_USE.md. Treat the visual gate's
captures as machine evidence plus a human review signal; a diagnostic or
reference pass is not by itself a product Golden approval.

## Approval and publication stops

Keep these owner decisions separate. A merged PR, a tag, and a GitHub Release
are different states. The runbook records the stop; it does not grant approval.

1. **Merge approval:** an independent owner reviews the candidate evidence
   bundle, public-surface diff, machine gates, and Basic Playback visual pass.
   Stop if any evidence is stale, missing, or from a different candidate.
2. **Merge verification:** after the develop -> main merge, set
   $ApprovedMergeCommit to the exact approved merge SHA. Do not move the normal
   checkout to tag; verify the remote main ref directly:

   ~~~powershell
   git fetch origin refs/heads/main:refs/remotes/origin/main --tags
   if ($LASTEXITCODE -ne 0) { throw "origin/main refresh failed; refusing to read a stale ref" }
   $OriginMain = (git rev-parse origin/main).Trim()
   if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($OriginMain)) {
     throw "origin/main lookup failed"
   }
   if ($OriginMain -ne $ApprovedMergeCommit) {
     throw "origin/main is not the approved merge: origin/main=$OriginMain approved=$ApprovedMergeCommit"
   }
   $ApprovedManifestJson = git show ($ApprovedMergeCommit + ":packages/com.yohawing.mmd-loader/package.json") | Out-String
   if ($LASTEXITCODE -ne 0) { throw "Approved merge package manifest lookup failed" }
   $ApprovedManifest = $ApprovedManifestJson | ConvertFrom-Json
   if ($ApprovedManifest.version -ne $Version) {
     throw "Approved merge package version does not match Version: $($ApprovedManifest.version) vs $Version"
   }
   ~~~

3. **Tag approval:** an owner confirms the approved merge commit and $Tag before
   the tag is created. Do not replace an existing tag or tag the current
   checkout implicitly.

   ~~~powershell
   git tag -a $Tag -m "Release $Tag" $ApprovedMergeCommit
   if ($LASTEXITCODE -ne 0) { throw "Annotated tag creation failed" }
   git push origin $Tag
   if ($LASTEXITCODE -ne 0) { throw "Annotated tag push failed" }
   ~~~

4. **Publish approval:** an owner confirms the tag resolves to the approved
   merge commit before creating the GitHub Release. Use --verify-tag; do not
   create a tag implicitly. Verify the remote annotated tag's peeled commit
   before invoking the release publication:

   ~~~powershell
   $PeeledTagRef = "refs/tags/$Tag^{}"
   $RemotePeeledTagLines = @(git ls-remote --tags origin $PeeledTagRef)
   if ($LASTEXITCODE -ne 0 -or $RemotePeeledTagLines.Count -ne 1) {
     throw "Annotated remote tag is missing or could not be resolved: $PeeledTagRef"
   }
   $RemotePeeledTagCommit = ($RemotePeeledTagLines[0] -split '\s+')[0]
   if ($RemotePeeledTagCommit -ne $ApprovedMergeCommit) {
     throw "Remote tag does not resolve to approved merge: remote=$RemotePeeledTagCommit approved=$ApprovedMergeCommit"
   }
   gh release create $Tag --repo yohawing/unity-mmd-loader --verify-tag --title $Tag --generate-notes
   if ($LASTEXITCODE -ne 0) { throw "GitHub Release creation failed" }
   ~~~

5. **Public verification:** parse the GitHub Release response and fail closed
   unless the tag, draft state, prerelease state, tag commit, and approved
   package version all match:

   ~~~powershell
   $ReleaseInfoJson = gh release view $Tag --repo yohawing/unity-mmd-loader --json tagName,isDraft,isPrerelease,publishedAt,url | Out-String
   if ($LASTEXITCODE -ne 0) { throw "GitHub Release lookup failed" }
   $ReleaseInfo = $ReleaseInfoJson | ConvertFrom-Json
   if ($ReleaseInfo.tagName -ne $Tag -or [bool]$ReleaseInfo.isDraft -or [bool]$ReleaseInfo.isPrerelease) {
     throw "Public release metadata mismatch: tag=$($ReleaseInfo.tagName) draft=$($ReleaseInfo.isDraft) prerelease=$($ReleaseInfo.isPrerelease)"
   }
   $PublishedRemoteTagLines = @(git ls-remote --tags origin $PeeledTagRef)
   if ($LASTEXITCODE -ne 0 -or $PublishedRemoteTagLines.Count -ne 1) {
     throw "Published annotated tag is missing or could not be resolved: $PeeledTagRef"
   }
   $PublishedRemoteTagCommit = ($PublishedRemoteTagLines[0] -split '\s+')[0]
   if ($PublishedRemoteTagCommit -ne $ApprovedMergeCommit) {
     throw "Published remote tag moved from approved merge: remote=$PublishedRemoteTagCommit approved=$ApprovedMergeCommit"
   }
   $TagCommit = (git rev-list -n 1 $Tag).Trim()
   if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($TagCommit)) {
     throw "Local tag lookup failed"
   }
   if ($TagCommit -ne $ApprovedMergeCommit) {
     throw "Tag does not resolve to approved merge: tag=$TagCommit approved=$ApprovedMergeCommit"
   }
   $PublishedManifestJson = git show ($ApprovedMergeCommit + ":packages/com.yohawing.mmd-loader/package.json") | Out-String
   if ($LASTEXITCODE -ne 0) { throw "Published package manifest lookup failed" }
   $PublishedManifest = $PublishedManifestJson | ConvertFrom-Json
   if ($PublishedManifest.version -ne $Version) {
     throw "Published package version mismatch: $($PublishedManifest.version) vs $Version"
   }
   ~~~

If a tag or release already exists, stop and inspect it; normal publication
does not replace or republish existing objects. UPM registry publication, if
introduced later, is a separate owner-approved boundary.
