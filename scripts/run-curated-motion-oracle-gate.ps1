<#
.SYNOPSIS
Compares provenance-verified PMX/VMD cases with local MMD-space oracles.

.DESCRIPTION
The player uses native, physics-off random access and samples the native MMD
model-space matrices before Unity conversion. Raw reports and oracle paths
remain under data-local; the artifact contains hashes and numeric deltas only.
This command never writes or updates baselines. Local oracles without source
hashes are report-only because matching paths do not prove source freshness.
#>
param(
    [string] $FixtureManifest = "",
    [string] $PlayerPath = "",
    [string] $ReportDir = "",
    [int] $CaseTimeoutSeconds = 180
)

$ErrorActionPreference = "Stop"
$root = Resolve-Path (Join-Path $PSScriptRoot "..")
$stamp = "{0}-{1}" -f (Get-Date -Format "yyyyMMdd-HHmmss-fff"), $PID
if ([string]::IsNullOrWhiteSpace($FixtureManifest)) { $FixtureManifest = Join-Path $root "data-local\fixtures.local.proposed.json" }
if ([string]::IsNullOrWhiteSpace($PlayerPath)) { $PlayerPath = Join-Path $root "artifacts\runtime-verification\MmdRuntimeVerification.exe" }
if ([string]::IsNullOrWhiteSpace($ReportDir)) { $ReportDir = Join-Path $root "artifacts\release-gate\curated-oracle-$stamp" }
$FixtureManifest = [IO.Path]::GetFullPath($FixtureManifest)
$PlayerPath = [IO.Path]::GetFullPath($PlayerPath)
$ReportDir = [IO.Path]::GetFullPath($ReportDir)
New-Item -ItemType Directory -Force $ReportDir | Out-Null
$reportPath = Join-Path $ReportDir "summary.json"
$tempRoot = [IO.Path]::GetFullPath((Join-Path $root "data-local\.curated-oracle-gate-$stamp"))
$curatedNames = @(
    "repo-test-1bone-cube-native-nanoem",
    "tda-miku-togenrenka",
    "sour-miku-rabbithole"
)
$minimumTrustedCases = 3
$repoFixtureTrustedCases = @("repo-test-1bone-cube-native-nanoem")

function Get-Value([object] $Object, [string] $Name) {
    if ($null -eq $Object) { return $null }
    $property = $Object.PSObject.Properties[$Name]
    if ($null -eq $property) { return $null }
    return $property.Value
}

function Get-StableHash([string] $Value) {
    $bytes = [Text.Encoding]::UTF8.GetBytes($Value)
    $sha = [Security.Cryptography.SHA256]::Create()
    try { return ([BitConverter]::ToString($sha.ComputeHash($bytes))).Replace("-", "").ToLowerInvariant() }
    finally { $sha.Dispose() }
}

function Resolve-AssetPath([string] $BasePath, [string] $PathValue) {
    if ([IO.Path]::IsPathRooted($PathValue)) { return [IO.Path]::GetFullPath($PathValue) }
    return [IO.Path]::GetFullPath((Join-Path $BasePath $PathValue))
}

function ConvertTo-ProcessArgument([string] $Argument) {
    if ($Argument -notmatch '[\s"]') { return $Argument }
    return '"' + ($Argument -replace '"', '\"') + '"'
}

function Add-Worst([hashtable] $Worst, [double] $Delta, [int] $Frame, [string] $TargetHash, [string] $Component) {
    if ($Delta -le [double]$Worst.delta) { return }
    $Worst.delta = $Delta
    $Worst.frame = $Frame
    $Worst.targetHash = $TargetHash
    $Worst.component = $Component
}

if (-not (Test-Path -LiteralPath $FixtureManifest -PathType Leaf)) { throw "Fixture manifest was not found." }
if (-not (Test-Path -LiteralPath $PlayerPath -PathType Leaf)) { throw "RuntimeVerification player was not found." }
if ($CaseTimeoutSeconds -le 0) { throw "CaseTimeoutSeconds must be positive." }

$manifest = Get-Content -LiteralPath $FixtureManifest -Raw -Encoding UTF8 | ConvertFrom-Json
$basePathValue = [string](Get-Value $manifest "basePath")
if ([string]::IsNullOrWhiteSpace($basePathValue)) { $basePathValue = "." }
$basePath = Resolve-AssetPath (Split-Path -Parent $FixtureManifest) $basePathValue
$byExtension = $manifest.paths.releaseSmoke.byExtension
$manifestCases = @($manifest.paths.playbackSmoke.cases)
$results = [Collections.Generic.List[object]]::new()
$reportOnlyCases = [Collections.Generic.List[object]]::new()
foreach ($case in $manifestCases) {
    if ([string]::IsNullOrWhiteSpace([string]$case.oracle)) { continue }
    $reason = ""
    if (-not [string]::IsNullOrWhiteSpace([string]$case.skipReason)) {
        $reason = "known-mismatch"
    }
    elseif (-not [string]::Equals([string]$case.model.extension, "pmx", [StringComparison]::OrdinalIgnoreCase)) {
        $reason = "unsupported-model-format"
    }
    if (-not [string]::IsNullOrWhiteSpace($reason)) {
        $reportOnlyCases.Add([ordered]@{ caseHash = Get-StableHash ([string]$case.name); reason = $reason })
    }
}
New-Item -ItemType Directory -Force $tempRoot | Out-Null

try {
    foreach ($curatedName in $curatedNames) {
        $case = $manifestCases | Where-Object name -eq $curatedName | Select-Object -First 1
        if ($null -eq $case) { throw "Curated case is missing from the manifest: $curatedName" }
        if (-not [string]::IsNullOrWhiteSpace([string]$case.skipReason)) { throw "Curated case is marked as a known mismatch: $curatedName" }
        if (-not [string]::Equals([string]$case.model.extension, "pmx", [StringComparison]::OrdinalIgnoreCase)) { throw "Curated case must use PMX: $curatedName" }
        $oraclePath = [string]$case.oracle
        if ([string]::IsNullOrWhiteSpace($oraclePath) -or -not (Test-Path -LiteralPath $oraclePath -PathType Leaf)) { throw "Curated oracle is missing: $curatedName" }
        $oracle = Get-Content -LiteralPath $oraclePath -Raw -Encoding UTF8 | ConvertFrom-Json
        if (-not $oracle.authoritative -or $oracle.coordinateSpace -ne "mmd-world" -or $oracle.matrixOrder -ne "column-major") {
            throw "Curated oracle contract is not authoritative MMD column-major: $curatedName"
        }

        $modelKey = [string]$case.model.key
        $motionKey = [string]$case.motion.key
        $pmxPath = Resolve-AssetPath $basePath ([string](Get-Value $byExtension.pmx $modelKey))
        $vmdPath = Resolve-AssetPath $basePath ([string](Get-Value $byExtension.vmd $motionKey))
        $sourceMatches = [IO.Path]::GetFullPath([string]$oracle.sourcePaths.model).Equals($pmxPath, [StringComparison]::OrdinalIgnoreCase) -and
            [IO.Path]::GetFullPath([string]$oracle.sourcePaths.motion).Equals($vmdPath, [StringComparison]::OrdinalIgnoreCase)
        if (-not $sourceMatches) { throw "Curated oracle source paths do not match the manifest case: $curatedName" }

        $sourceHashes = Get-Value $oracle "sourceHashes"
        $expectedModelHash = [string](Get-Value $sourceHashes "modelSha256")
        $expectedMotionHash = [string](Get-Value $sourceHashes "motionSha256")
        $hasVerifiedHashes = -not [string]::IsNullOrWhiteSpace($expectedModelHash) -and
            -not [string]::IsNullOrWhiteSpace($expectedMotionHash) -and
            $expectedModelHash.Equals((Get-FileHash -LiteralPath $pmxPath -Algorithm SHA256).Hash, [StringComparison]::OrdinalIgnoreCase) -and
            $expectedMotionHash.Equals((Get-FileHash -LiteralPath $vmdPath -Algorithm SHA256).Hash, [StringComparison]::OrdinalIgnoreCase)
        $isRepoFixture = $repoFixtureTrustedCases -contains $curatedName
        if (-not $hasVerifiedHashes -and -not $isRepoFixture) {
            $reportOnlyCases.Add([ordered]@{ caseHash = Get-StableHash $curatedName; reason = "unverified-source-freshness" })
            continue
        }

        $frames = @($oracle.frames | ForEach-Object { [int]$_.frame } | Sort-Object -Unique)
        $caseHash = Get-StableHash $curatedName
        $caseTemp = Join-Path $tempRoot $caseHash.Substring(0, 12)
        New-Item -ItemType Directory -Force $caseTemp | Out-Null
        $rawReport = Join-Path $caseTemp "report.json"
        $rawLog = Join-Path $caseTemp "player.log"
        $args = @(
            "-batchmode", "--pmx", $pmxPath, "--vmd", $vmdPath,
            "--drive", "controller", "--sample-mode", "random-access",
            "--sample-frames", ($frames -join ','), "--dump-bones", "--dump-morphs",
            "--fast-runtime", "on", "--duration", "0", "--out", $rawReport, "-logFile", $rawLog
        )
        $argumentLine = ($args | ForEach-Object { ConvertTo-ProcessArgument $_ }) -join ' '
        $process = Start-Process -FilePath $PlayerPath -ArgumentList $argumentLine -PassThru -WindowStyle Hidden
        $timedOut = -not $process.WaitForExit($CaseTimeoutSeconds * 1000)
        if ($timedOut) { Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue }
        if ($timedOut -or -not (Test-Path -LiteralPath $rawReport -PathType Leaf)) {
            $results.Add([ordered]@{ caseHash = $caseHash; status = "failed"; reason = if ($timedOut) { "timeout" } else { "report-not-generated" } })
            continue
        }

        $raw = Get-Content -LiteralPath $rawReport -Raw -Encoding UTF8 | ConvertFrom-Json
        $runtime = @($raw.caseResults)[0]
        if ($runtime.parseStatus -ne "passed" -or $runtime.playbackStatus -ne "passed" -or $runtime.consoleErrorCount -ne 0) {
            $results.Add([ordered]@{ caseHash = $caseHash; status = "failed"; reason = "runtime-case-failed" })
            continue
        }
        if (-not $runtime.playback.fastRuntimeEnabled) {
            $results.Add([ordered]@{ caseHash = $caseHash; status = "failed"; reason = "native-runtime-unavailable" })
            continue
        }

        $oracleBonesByName = @{}
        foreach ($bone in @($oracle.model.bones)) { $oracleBonesByName[[string]$bone.name] = $bone }
        $oracleMorphs = @($oracle.model.morphs)
        $watchBones = @($case.watchBones)
        $positionWorst = @{ delta = 0.0; frame = -1; targetHash = ""; component = "" }
        $rotationWorst = @{ delta = 0.0; frame = -1; targetHash = ""; component = "" }
        $morphWorst = @{ delta = 0.0; frame = -1; targetHash = ""; component = "weight" }
        $positionMismatchCount = 0
        $rotationMismatchCount = 0
        $morphMismatchCount = 0
        $comparedBoneComponents = 0
        $comparedMorphWeights = 0
        $matrixEpsilon = if ($null -ne $case.matrixEpsilon) { [double]$case.matrixEpsilon } else { 0.0001 }
        $morphEpsilon = if ($null -ne $case.morphEpsilon) { [double]$case.morphEpsilon } else { 0.0001 }
        $positionRuntimeRowMajorIndices = @(3, 7, 11)
        $positionRuntimeColumnMajorIndices = @(12, 13, 14)
        $positionOracleIndices = @(12, 13, 14)
        $positionLabels = @("x", "y", "z")
        $rotationRuntimeIndices = @(0, 1, 2, 4, 5, 6, 8, 9, 10)
        $rotationOracleIndices = @(0, 4, 8, 1, 5, 9, 2, 6, 10)
        $rotationLabels = @("m00", "m01", "m02", "m10", "m11", "m12", "m20", "m21", "m22")

        foreach ($sample in @($runtime.sampledFrames)) {
            $frame = [int]$sample.frame
            $oracleFrame = @($oracle.frames | Where-Object { [int]$_.frame -eq $frame })[0]
            $stageName = if ([string]::IsNullOrWhiteSpace([string]$case.stage)) { "physics" } else { [string]$case.stage }
            $stage = Get-Value $oracleFrame.stages $stageName
            $oracleMatrices = @($stage.worldMatricesColumnMajor)
            $runtimeColumnMajor = [string]::Equals([string]$sample.matrixLayout, "column-major", [StringComparison]::OrdinalIgnoreCase)
            $runtimeBonesByName = @{}
            foreach ($bone in @($sample.bones)) { $runtimeBonesByName[[string]$bone.name] = $bone }

            foreach ($boneName in $watchBones) {
                $oracleBone = $oracleBonesByName[[string]$boneName]
                $runtimeBone = $runtimeBonesByName[[string]$boneName]
                if ($null -eq $oracleBone -or $null -eq $runtimeBone) {
                    $positionMismatchCount++
                    $rotationMismatchCount++
                    continue
                }
                $offset = [int]$oracleBone.index * 16
                $targetHash = Get-StableHash ([string]$boneName)
                for ($i = 0; $i -lt $positionRuntimeRowMajorIndices.Count; $i++) {
                    $runtimeIndex = if ($runtimeColumnMajor) { $positionRuntimeColumnMajorIndices[$i] } else { $positionRuntimeRowMajorIndices[$i] }
                    $oracleIndex = $positionOracleIndices[$i]
                    $delta = [Math]::Abs([double]$runtimeBone.worldMatrix[$runtimeIndex] - [double]$oracleMatrices[$offset + $oracleIndex])
                    $comparedBoneComponents++
                    if ($delta -gt $matrixEpsilon) { $positionMismatchCount++ }
                    Add-Worst $positionWorst $delta $frame $targetHash ("position." + $positionLabels[$i])
                }
                for ($i = 0; $i -lt $rotationRuntimeIndices.Count; $i++) {
                    $runtimeIndex = $rotationRuntimeIndices[$i]
                    $oracleIndex = if ($runtimeColumnMajor) { $rotationRuntimeIndices[$i] } else { $rotationOracleIndices[$i] }
                    $delta = [Math]::Abs([double]$runtimeBone.worldMatrix[$runtimeIndex] - [double]$oracleMatrices[$offset + $oracleIndex])
                    $comparedBoneComponents++
                    if ($delta -gt $matrixEpsilon) { $rotationMismatchCount++ }
                    Add-Worst $rotationWorst $delta $frame $targetHash ("rotation." + $rotationLabels[$i])
                }
            }

            $runtimeMorphsByName = @{}
            foreach ($morph in @($sample.morphs)) { $runtimeMorphsByName[[string]$morph.name] = [double]$morph.weight }
            $oracleWeights = @($stage.morphWeights)
            foreach ($oracleMorph in $oracleMorphs) {
                $name = [string]$oracleMorph.name
                $runtimeWeight = if ($runtimeMorphsByName.ContainsKey($name)) { [double]$runtimeMorphsByName[$name] } else { 0.0 }
                $oracleWeight = [double]$oracleWeights[[int]$oracleMorph.index]
                $delta = [Math]::Abs($runtimeWeight - $oracleWeight)
                $comparedMorphWeights++
                if ($delta -gt $morphEpsilon) { $morphMismatchCount++ }
                Add-Worst $morphWorst $delta $frame (Get-StableHash $name) "weight"
            }
        }

        $mismatchCount = $positionMismatchCount + $rotationMismatchCount + $morphMismatchCount
        $results.Add([ordered]@{
            caseHash = $caseHash
            status = if ($mismatchCount -eq 0) { "passed" } else { "failed" }
            frames = $frames
            watchBoneCount = $watchBones.Count
            morphCount = $oracleMorphs.Count
            matrixEpsilon = $matrixEpsilon
            morphEpsilon = $morphEpsilon
            comparedBoneComponents = $comparedBoneComponents
            comparedMorphWeights = $comparedMorphWeights
            positionMismatchCount = $positionMismatchCount
            rotationMismatchCount = $rotationMismatchCount
            morphMismatchCount = $morphMismatchCount
            mismatchCount = $mismatchCount
            worstPosition = $positionWorst
            worstRotation = $rotationWorst
            worstMorph = $morphWorst
        })
    }
}
finally {
    $dataLocal = [IO.Path]::GetFullPath((Join-Path $root "data-local")).TrimEnd('\', '/')
    if ($tempRoot.StartsWith($dataLocal + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        Remove-Item -LiteralPath $tempRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
}

$failed = @($results | Where-Object status -eq "failed").Count
$trustedCaseShortfall = $results.Count -lt $minimumTrustedCases
$summary = [ordered]@{
    schemaVersion = 1
    status = if ($failed -gt 0) { "failed" } elseif ($trustedCaseShortfall) { "incomplete" } else { "passed" }
    baselineUpdateMode = "explicit-only"
    baselineUpdated = $false
    manifestHash = (Get-FileHash -LiteralPath $FixtureManifest -Algorithm SHA256).Hash.ToLowerInvariant()
    playerHash = (Get-FileHash -LiteralPath $PlayerPath -Algorithm SHA256).Hash.ToLowerInvariant()
    caseCount = $results.Count
    minimumTrustedCases = $minimumTrustedCases
    passedCases = $results.Count - $failed
    failedCases = $failed
    cases = @($results)
    reportOnlyCaseCount = $reportOnlyCases.Count
    reportOnlyCases = @($reportOnlyCases)
}
$summary | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $reportPath -Encoding UTF8
Write-Host ("{0} curated-oracle: cases={1}, passed={2}, failed={3}, report={4}" -f $summary.status.ToUpperInvariant(), $summary.caseCount, $summary.passedCases, $summary.failedCases, $reportPath)
if ($failed -gt 0 -or $trustedCaseShortfall) { exit 1 }
exit 0
