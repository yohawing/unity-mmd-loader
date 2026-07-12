<#
.SYNOPSIS
Runs the RuntimeVerification player against a fixture manifest and evaluates the JSON report for pass/fail.

.DESCRIPTION
Orchestrates the headless RuntimeVerification gate:
1. Optionally builds the standalone player (skip with -SkipBuild if already built).
2. Runs the player with the given fixture manifest.
3. Reads the JSON report and determines pass/fail.

Exit code 0 = all cases passed or skipped. Non-zero = at least one failure.

.PARAMETER FixtureManifest
Path to the fixture manifest JSON. Defaults to the smoke manifest in artifacts/.

.PARAMETER Drive
Playback drive mode: timeline or controller. Defaults to timeline.

.PARAMETER Duration
Playback duration in seconds per case. Defaults to 1.0 so timeline playback advances.

.PARAMETER FrameRate
Frame rate for playback sampling. Defaults to 30.

.PARAMETER SkipBuild
Skip building the player (use existing build in artifacts/runtime-verification/).

.PARAMETER PlayerPath
Path to the built player executable. Defaults to artifacts/runtime-verification/MmdRuntimeVerification.exe.

.PARAMETER ReportDir
Directory for gate reports. Defaults to artifacts/release-gate/<timestamp>/.

.EXAMPLE
.\scripts\run-runtime-verification-gate.ps1

Builds the player, runs the smoke manifest, evaluates the report.

.EXAMPLE
.\scripts\run-runtime-verification-gate.ps1 -SkipBuild -FixtureManifest data-local\my-fixtures.json

Runs a custom manifest against an already-built player.
#>
param(
    [string] $FixtureManifest = "",
    [string] $Drive = "timeline",
    [float] $Duration = 1.0,
    [float] $FrameRate = 30.0,
    [switch] $SkipBuild,
    [string] $PlayerPath = "",
    [string] $ReportDir = ""
)

$ErrorActionPreference = "Stop"

function ConvertTo-ProcessArgument {
    param([Parameter(Mandatory = $true)][string] $Argument)

    if ($Argument -notmatch '[\s"]') {
        return $Argument
    }

    return '"' + ($Argument -replace '"', '\"') + '"'
}

function ConvertTo-ProcessArgumentList {
    param([Parameter(Mandatory = $true)][string[]] $Arguments)

    return (($Arguments | ForEach-Object { ConvertTo-ProcessArgument $_ }) -join ' ')
}

$root = Resolve-Path (Join-Path $PSScriptRoot "..")
$artifactsBase = Join-Path $root "artifacts"
$rvArtifacts = Join-Path $artifactsBase "runtime-verification"

if ([string]::IsNullOrWhiteSpace($PlayerPath)) {
    $PlayerPath = Join-Path $rvArtifacts "MmdRuntimeVerification.exe"
}
$PlayerPath = [System.IO.Path]::GetFullPath($PlayerPath)

if ([string]::IsNullOrWhiteSpace($FixtureManifest)) {
    $FixtureManifest = Join-Path $root "packages\com.yohawing.mmd-loader\Samples~\RuntimeVerification\Manifests\golden-path.json"
}
$FixtureManifest = [System.IO.Path]::GetFullPath($FixtureManifest)

$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
if ([string]::IsNullOrWhiteSpace($ReportDir)) {
    $ReportDir = Join-Path $artifactsBase "release-gate\$timestamp"
}
$ReportDir = [System.IO.Path]::GetFullPath($ReportDir)

New-Item -ItemType Directory -Force -Path $ReportDir | Out-Null

# --- Step 1: Build (optional) ---
if (-not $SkipBuild) {
    Write-Host "[gate] Building RuntimeVerification player..."
    $buildScript = Join-Path $PSScriptRoot "build-runtime-verification-player.ps1"
    & powershell -NoProfile -ExecutionPolicy Bypass -File $buildScript `
        -ProjectPath (Join-Path $root "unity-mmd") `
        -SamplePath (Join-Path $root "packages\com.yohawing.mmd-loader\Samples~\RuntimeVerification") `
        -ProjectSamplePath (Join-Path $root "unity-mmd\Assets\RuntimeVerification") `
        -ArtifactsPath $rvArtifacts `
        -OutputPath $PlayerPath
    $buildExit = if ($null -eq $LASTEXITCODE) { 0 } else { $LASTEXITCODE }
    if ($buildExit -ne 0) {
        Write-Host "FAIL gate: player build failed with exit code $buildExit"
        exit $buildExit
    }
    Write-Host "[gate] Player build succeeded."
}

if (-not (Test-Path -LiteralPath $PlayerPath)) {
    Write-Host "FAIL gate: player not found at $PlayerPath. Run without -SkipBuild or build first."
    exit 1
}

if (-not (Test-Path -LiteralPath $FixtureManifest)) {
    Write-Host "FAIL gate: fixture manifest not found at $FixtureManifest"
    exit 1
}

# --- Step 2: Run player ---
$reportPath = Join-Path $ReportDir "report.json"
$playerLogPath = Join-Path $ReportDir "player.log"
$screenshotDir = Join-Path $ReportDir "screenshots"

Write-Host "[gate] Running RuntimeVerification player..."
Write-Host "[gate]   manifest    = $FixtureManifest"
Write-Host "[gate]   drive       = $Drive"
Write-Host "[gate]   duration    = $Duration"
Write-Host "[gate]   report      = $reportPath"
Write-Host "[gate]   screenshots = $screenshotDir"

Remove-Item -LiteralPath $reportPath, $playerLogPath -Force -ErrorAction SilentlyContinue

$playerArgs = @(
    "-batchmode",
    "--fixture-manifest", $FixtureManifest,
    "--drive", $Drive,
    "--screenshot-dir", $screenshotDir,
    "--duration", $Duration.ToString([System.Globalization.CultureInfo]::InvariantCulture),
    "--frame-rate", $FrameRate.ToString([System.Globalization.CultureInfo]::InvariantCulture),
    "--out", $reportPath,
    "-logFile", $playerLogPath
)

$playerProcess = Start-Process -FilePath $PlayerPath -ArgumentList (ConvertTo-ProcessArgumentList $playerArgs) -Wait -PassThru -WindowStyle Hidden
$playerExit = $playerProcess.ExitCode

# --- Step 3: Read and evaluate report ---
if (-not (Test-Path -LiteralPath $reportPath)) {
    Write-Host "FAIL gate: report was not generated at $reportPath (player exit=$playerExit)"
    Write-Host "[gate] Check player log: $playerLogPath"
    exit 1
}

$report = Get-Content $reportPath -Raw -Encoding UTF8 | ConvertFrom-Json

$totalCases = $report.caseResults.Count
$passedCases = @($report.caseResults | Where-Object { $_.status -eq "passed" }).Count
$failedCases = @($report.caseResults | Where-Object { $_.status -eq "failed" }).Count
$skippedCases = @($report.caseResults | Where-Object { $_.status -eq "skipped" }).Count
$otherCases = $totalCases - $passedCases - $failedCases - $skippedCases

# Write summary JSON
$smokeFailed = @($report.caseResults | Where-Object { $_.visualSmoke.smokeStatus -eq "failed" }).Count
$visualSmokeRequiredCases = @($report.caseResults | Where-Object {
    $_.status -ne "skipped" -and
    $_.expectedFeatures -contains "visual-smoke"
}).Count
$visualSmokePassedCases = @($report.caseResults | Where-Object {
    $_.status -ne "skipped" -and
    $_.expectedFeatures -contains "visual-smoke" -and $_.visualSmoke.smokeStatus -eq "passed"
}).Count
$visualSmokeNotPassedCases = @($report.caseResults | Where-Object {
    $_.status -ne "skipped" -and
    $_.expectedFeatures -contains "visual-smoke" -and $_.visualSmoke.smokeStatus -ne "passed"
}).Count
$visualSmokeBlankCases = @($report.caseResults | Where-Object { $_.visualSmoke.isBlank }).Count
$visualSmokeAllBlackCases = @($report.caseResults | Where-Object { $_.visualSmoke.isAllBlack }).Count
$visualSmokeAllWhiteCases = @($report.caseResults | Where-Object { $_.visualSmoke.isAllWhite }).Count
$visualSmokeMissingContentCases = @($report.caseResults | Where-Object {
    $_.status -ne "skipped" -and
    $_.expectedFeatures -contains "visual-smoke" -and
    $_.visualSmoke.PSObject.Properties['hasContentBounds'] -and -not $_.visualSmoke.hasContentBounds
}).Count
$visualSmokeTouchesEdgeCases = @($report.caseResults | Where-Object {
    $_.status -ne "skipped" -and
    $_.expectedFeatures -contains "visual-smoke" -and
    $_.visualSmoke.PSObject.Properties['touchesImageEdge'] -and $_.visualSmoke.touchesImageEdge
}).Count
$visualSmokeMissingOutlineCases = @($report.caseResults | Where-Object {
    $_.status -ne "skipped" -and
    $_.expectedFeatures -contains "visual-smoke" -and
    $_.visualSmoke.PSObject.Properties['outlinePixelCount'] -and
    $_.visualSmoke.outlinePixelCount -le 0
}).Count
$outlinePixelRequiredCases = @($report.caseResults | Where-Object {
    $_.status -ne "skipped" -and
    $_.expectedFeatures -contains "outline-pixel"
}).Count
$outlinePixelMissingRequiredCases = @($report.caseResults | Where-Object {
    $_.status -ne "skipped" -and
    $_.expectedFeatures -contains "outline-pixel" -and
    (-not $_.visualSmoke.PSObject.Properties['outlinePixelCount'] -or $_.visualSmoke.outlinePixelCount -le 0)
}).Count

$packageJsonPath = Join-Path $root "packages\com.yohawing.mmd-loader\package.json"
$packageVersion = ""
if (Test-Path -LiteralPath $packageJsonPath) {
    $packageVersion = (Get-Content $packageJsonPath -Raw -Encoding UTF8 | ConvertFrom-Json).version
}

$nativeDllPath = Join-Path $root "packages\com.yohawing.mmd-loader\Runtime\Plugins\x86_64\mmd_runtime_ffi.dll"
$nativeDllHash = ""
if (Test-Path -LiteralPath $nativeDllPath) {
    $nativeDllHash = (Get-FileHash $nativeDllPath -Algorithm SHA256).Hash
}

$fixtureManifestHash = (Get-FileHash $FixtureManifest -Algorithm SHA256).Hash

$summary = @{
    timestamp           = $timestamp
    fixtureManifest     = $FixtureManifest
    fixtureManifestHash = $fixtureManifestHash
    drive               = $Drive
    playerPath          = $PlayerPath
    playerExitCode      = $playerExit
    reportStatus        = $report.status
    unityVersion        = $report.unityVersion
    packageVersion      = $packageVersion
    nativeDllHash       = $nativeDllHash
    totalCases          = $totalCases
    passedCases         = $passedCases
    failedCases         = $failedCases
    skippedCases        = $skippedCases
    smokeFailed         = $smokeFailed
    visualSmokeRequiredCases       = $visualSmokeRequiredCases
    visualSmokePassedCases         = $visualSmokePassedCases
    visualSmokeNotPassedCases      = $visualSmokeNotPassedCases
    visualSmokeBlankCases          = $visualSmokeBlankCases
    visualSmokeAllBlackCases       = $visualSmokeAllBlackCases
    visualSmokeAllWhiteCases       = $visualSmokeAllWhiteCases
    visualSmokeMissingContentCases = $visualSmokeMissingContentCases
    visualSmokeTouchesEdgeCases    = $visualSmokeTouchesEdgeCases
    visualSmokeMissingOutlineCases = $visualSmokeMissingOutlineCases
    outlinePixelRequiredCases      = $outlinePixelRequiredCases
    outlinePixelMissingRequiredCases = $outlinePixelMissingRequiredCases
    screenshotDir       = $screenshotDir
    durationSeconds     = $report.durationSeconds
    executedCommand     = "$PlayerPath $($playerArgs -join ' ')"
}
$summaryPath = Join-Path $ReportDir "summary.json"
$summary | ConvertTo-Json -Depth 4 | Set-Content $summaryPath -Encoding UTF8

# Print results
Write-Host ""
Write-Host "=== RuntimeVerification Gate ==="
Write-Host "  Status:   $($report.status)"
Write-Host "  Cases:    $totalCases total, $passedCases passed, $failedCases failed, $skippedCases skipped"
Write-Host "  Visual:   $visualSmokePassedCases/$visualSmokeRequiredCases required passed, $visualSmokeNotPassedCases not passed, $visualSmokeMissingContentCases missing content, $outlinePixelMissingRequiredCases/$outlinePixelRequiredCases required outline missing, $visualSmokeTouchesEdgeCases touch edge"
Write-Host "  Duration: $([Math]::Round($report.durationSeconds, 2))s"
Write-Host "  Report:   $reportPath"
Write-Host "  Summary:  $summaryPath"

if ($failedCases -gt 0) {
    Write-Host ""
    Write-Host "  Failed cases:"
    foreach ($case in $report.caseResults) {
        if ($case.status -eq "failed") {
            $reason = if ([string]::IsNullOrWhiteSpace($case.exception)) { "unknown" } else { $case.exception.Split("`n")[0] }
            Write-Host ("    - {0}: {1}" -f $case.name, $reason)
        }
    }
}

if ($skippedCases -gt 0) {
    Write-Host ""
    Write-Host "  Skipped cases:"
    foreach ($case in $report.caseResults) {
        if ($case.status -eq "skipped") {
            $reason = if ($case.PSObject.Properties['skipReason'] -and -not [string]::IsNullOrWhiteSpace($case.skipReason)) { $case.skipReason } else { "no reason" }
            Write-Host ("    - {0}: {1}" -f $case.name, $reason)
        }
    }
}

Write-Host ""

if ($playerExit -eq 0 -and $report.status -eq "passed" -and $visualSmokeNotPassedCases -eq 0 -and $outlinePixelMissingRequiredCases -eq 0) {
    Write-Host "PASS gate: RuntimeVerification ($passedCases passed, $skippedCases skipped)"
    exit 0
}
else {
    Write-Host "FAIL gate: RuntimeVerification ($failedCases failed, $passedCases passed, $skippedCases skipped)"
    exit 1
}
