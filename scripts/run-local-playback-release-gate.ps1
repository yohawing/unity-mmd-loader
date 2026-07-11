<#
.SYNOPSIS
Runs every local playback case at 0/25/50/75/end with physics-off random access.

.DESCRIPTION
Raw player reports and logs contain licensed local paths and stay temporarily under
data-local. Only a sanitized aggregate report is written under artifacts.
#>
param(
    [string] $FixtureManifest = "",
    [string] $PlayerPath = "",
    [string] $ReportDir = "",
    [int] $CaseTimeoutSeconds = 120
)

$ErrorActionPreference = "Stop"
$root = Resolve-Path (Join-Path $PSScriptRoot "..")
$stamp = "{0}-{1}" -f (Get-Date -Format "yyyyMMdd-HHmmss-fff"), $PID
if ([string]::IsNullOrWhiteSpace($FixtureManifest)) { $FixtureManifest = Join-Path $root "data-local\fixtures.local.proposed.json" }
if ([string]::IsNullOrWhiteSpace($PlayerPath)) { $PlayerPath = Join-Path $root "artifacts\runtime-verification\MmdRuntimeVerification.exe" }
if ([string]::IsNullOrWhiteSpace($ReportDir)) { $ReportDir = Join-Path $root "artifacts\release-gate\local-playback-$stamp" }
$FixtureManifest = [IO.Path]::GetFullPath($FixtureManifest)
$PlayerPath = [IO.Path]::GetFullPath($PlayerPath)
$ReportDir = [IO.Path]::GetFullPath($ReportDir)
New-Item -ItemType Directory -Force $ReportDir | Out-Null
$reportPath = Join-Path $ReportDir "summary.json"
$tempRoot = [IO.Path]::GetFullPath((Join-Path $root "data-local\.local-playback-gate-$stamp"))

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

function Test-Finite([object] $Value) {
    $number = [double]$Value
    return -not [double]::IsNaN($number) -and -not [double]::IsInfinity($number)
}

function Sanitize-Reason([string] $Reason, [string[]] $SensitivePaths) {
    if ([string]::IsNullOrWhiteSpace($Reason)) { return "" }
    $sanitized = $Reason
    foreach ($path in ($SensitivePaths | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Sort-Object Length -Descending)) {
        $sanitized = $sanitized.Replace($path, "<asset>", [StringComparison]::OrdinalIgnoreCase)
    }
    return $sanitized
}

if (-not (Test-Path -LiteralPath $FixtureManifest -PathType Leaf)) { throw "Fixture manifest was not found." }
if (-not (Test-Path -LiteralPath $PlayerPath -PathType Leaf)) { throw "RuntimeVerification player was not found." }
if ($CaseTimeoutSeconds -le 0) { throw "CaseTimeoutSeconds must be positive." }

$manifest = Get-Content -LiteralPath $FixtureManifest -Raw -Encoding UTF8 | ConvertFrom-Json
$basePathValue = [string](Get-Value $manifest "basePath")
if ([string]::IsNullOrWhiteSpace($basePathValue)) { $basePathValue = [string](Get-Value $manifest "root") }
if ([string]::IsNullOrWhiteSpace($basePathValue)) { $basePathValue = "." }
$basePath = Resolve-AssetPath (Split-Path -Parent $FixtureManifest) $basePathValue
$byExtension = $manifest.paths.releaseSmoke.byExtension
$cases = @($manifest.paths.playbackSmoke.cases)

$inventoryDir = Join-Path $ReportDir "inventory"
& (Join-Path $PSScriptRoot "run-local-asset-corpus-inventory.ps1") -FixtureManifest $FixtureManifest -ReportDir $inventoryDir
if ($LASTEXITCODE -ne 0) { throw "Corpus inventory failed before playback." }
$inventory = Get-Content -LiteralPath (Join-Path $inventoryDir "corpus-inventory.json") -Raw -Encoding UTF8 | ConvertFrom-Json
$inventoryById = @{}
foreach ($asset in @($inventory.assets)) { $inventoryById[$asset.id] = $asset }

$results = [Collections.Generic.List[object]]::new()
New-Item -ItemType Directory -Force $tempRoot | Out-Null
try {
    for ($caseIndex = 0; $caseIndex -lt $cases.Count; $caseIndex++) {
        $case = $cases[$caseIndex]
        $caseName = [string](Get-Value $case "name")
        $caseHash = Get-StableHash $caseName
        $model = Get-Value $case "model"
        $motion = Get-Value $case "motion"
        $modelGroup = [string](Get-Value $model "extension")
        if ([string]::IsNullOrWhiteSpace($modelGroup)) { $modelGroup = "pmx" }
        $modelKey = [string](Get-Value $model "key")
        $motionKey = [string](Get-Value $motion "key")
        if (-not [string]::Equals($modelGroup, "pmx", [StringComparison]::OrdinalIgnoreCase)) {
            $results.Add([ordered]@{
                caseHash = $caseHash
                status = "skipped"
                skipReason = "unsupported-model-format"
                modelFormat = $modelGroup.ToLowerInvariant()
            })
            continue
        }

        $pmxValue = [string](Get-Value (Get-Value $byExtension $modelGroup) $modelKey)
        $vmdValue = [string](Get-Value (Get-Value $byExtension "vmd") $motionKey)
        $pmxPath = Resolve-AssetPath $basePath $pmxValue
        $vmdPath = Resolve-AssetPath $basePath $vmdValue
        $motionInventory = $inventoryById["vmd/$motionKey"]
        $maxFrame = if ($null -ne $motionInventory -and $motionInventory.metadata.status -eq "parsed") { [int]$motionInventory.metadata.frameRange.max } else { 0 }
        $frames = @(
            0,
            [int][Math]::Round($maxFrame * 0.25, [MidpointRounding]::AwayFromZero),
            [int][Math]::Round($maxFrame * 0.5, [MidpointRounding]::AwayFromZero),
            [int][Math]::Round($maxFrame * 0.75, [MidpointRounding]::AwayFromZero),
            $maxFrame
        ) | Sort-Object -Unique
        $caseTemp = Join-Path $tempRoot $caseIndex
        New-Item -ItemType Directory -Force $caseTemp | Out-Null
        $rawReport = Join-Path $caseTemp "report.json"
        $rawLog = Join-Path $caseTemp "player.log"
        $args = @(
            "-batchmode", "--pmx", $pmxPath, "--vmd", $vmdPath,
            "--drive", "controller", "--sample-mode", "random-access",
            "--sample-frames", ($frames -join ','), "--dump-bones",
            "--duration", "0", "--out", $rawReport, "-logFile", $rawLog
        )
        $argumentLine = ($args | ForEach-Object { ConvertTo-ProcessArgument $_ }) -join ' '
        $process = Start-Process -FilePath $PlayerPath -ArgumentList $argumentLine -PassThru -WindowStyle Hidden
        $timedOut = -not $process.WaitForExit($CaseTimeoutSeconds * 1000)
        if ($timedOut) { Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue }

        $checks = [ordered]@{
            completedWithoutTimeout = -not $timedOut
            reportGenerated = Test-Path -LiteralPath $rawReport -PathType Leaf
            parsePassed = $false
            playbackPassed = $false
            hierarchyAndMeshCreated = $false
            configured = $false
            requestedFramesSampled = $false
            finiteBoneMatrices = $false
            consoleErrorZero = $false
        }
        $native = [ordered]@{ requested = $true; enabled = $false; reason = "report-not-generated" }
        $observedFrames = @()
        $modelCounts = [ordered]@{ vertices = 0; indices = 0; bones = 0; materials = 0 }
        $motionCounts = [ordered]@{ maxFrame = $maxFrame; bones = 0; morphs = 0; cameras = 0; lights = 0; selfShadows = 0 }
        $consoleErrors = -1
        $failureReasons = [Collections.Generic.List[string]]::new()
        if ($checks.reportGenerated) {
            $raw = Get-Content -LiteralPath $rawReport -Raw -Encoding UTF8 | ConvertFrom-Json
            $caseResult = @($raw.caseResults)[0]
            $checks.parsePassed = $caseResult.parseStatus -eq "passed"
            $checks.playbackPassed = $caseResult.playbackStatus -eq "passed"
            $checks.hierarchyAndMeshCreated = $caseResult.model.vertexCount -gt 0 -and $caseResult.model.indexCount -gt 0 -and $caseResult.model.boneCount -gt 0
            $checks.configured = [bool]$caseResult.playback.configured
            $observedFrames = @($caseResult.sampledFrames | ForEach-Object { [int]$_.frame })
            $checks.requestedFramesSampled = (@($observedFrames) -join ',') -eq (@($frames) -join ',')
            $finite = @($caseResult.sampledFrames).Count -gt 0
            foreach ($sample in @($caseResult.sampledFrames)) {
                if (-not $sample.configured -or $null -eq $sample.bones -or @($sample.bones).Count -eq 0) { $finite = $false; break }
                foreach ($bone in @($sample.bones)) {
                    if (@($bone.worldMatrix).Count -ne 16) { $finite = $false; break }
                    foreach ($value in @($bone.worldMatrix)) { if (-not (Test-Finite $value)) { $finite = $false; break } }
                    if (-not $finite) { break }
                }
                if (-not $finite) { break }
            }
            $checks.finiteBoneMatrices = $finite
            $consoleErrors = [int]$caseResult.consoleErrorCount
            $checks.consoleErrorZero = $consoleErrors -eq 0
            $native.enabled = [bool]$caseResult.playback.fastRuntimeEnabled
            $native.reason = Sanitize-Reason ([string]$caseResult.playback.fastRuntimeReason) @($pmxPath, $vmdPath, $basePath)
            $modelCounts = [ordered]@{ vertices = [int]$caseResult.model.vertexCount; indices = [int]$caseResult.model.indexCount; bones = [int]$caseResult.model.boneCount; materials = [int]$caseResult.model.materialCount }
            $motionCounts = [ordered]@{ maxFrame = [int]$caseResult.motion.maxFrame; bones = [int]$caseResult.motion.boneKeyframeCount; morphs = [int]$caseResult.motion.morphKeyframeCount; cameras = [int]$caseResult.motion.cameraKeyframeCount; lights = [int]$caseResult.motion.lightKeyframeCount; selfShadows = [int]$caseResult.motion.selfShadowKeyframeCount }
            if (-not [string]::IsNullOrWhiteSpace($caseResult.exception) -and -not ($caseResult.exception -like "Visual smoke failed:*")) {
                $failureReasons.Add((Sanitize-Reason ([string]$caseResult.exception) @($pmxPath, $vmdPath, $basePath)))
            }
        }
        else {
            $failureReason = if ($timedOut) { "case-timeout" } else { "report-not-generated" }
            $failureReasons.Add($failureReason)
        }

        $optionalReferences = [ordered]@{}
        foreach ($field in @("camera", "background", "audio")) {
            $reference = Get-Value $case $field
            if ($null -eq $reference) { $optionalReferences[$field] = "not-declared"; continue }
            $group = [string](Get-Value $reference "extension")
            if ([string]::IsNullOrWhiteSpace($group)) { $group = if ($field -eq "camera") { "cameraVmd" } else { "" } }
            $value = [string](Get-Value (Get-Value $byExtension $group) ([string](Get-Value $reference "key")))
            $optionalReferences[$field] = if (-not [string]::IsNullOrWhiteSpace($value) -and (Test-Path -LiteralPath (Resolve-AssetPath $basePath $value) -PathType Leaf)) { "resolved" } else { "missing" }
        }
        $passed = -not ($checks.Values -contains $false) -and $optionalReferences.Values -notcontains "missing"
        $results.Add([ordered]@{
            caseHash = $caseHash; status = if ($passed) { "passed" } else { "failed" }
            requestedFrames = @($frames); observedFrames = @($observedFrames); checks = $checks
            model = $modelCounts; motion = $motionCounts; native = $native
            optionalReferences = $optionalReferences; consoleErrorCount = $consoleErrors
            failureReasons = @($failureReasons)
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
$skipped = @($results | Where-Object status -eq "skipped").Count
$summary = [ordered]@{
    schemaVersion = 1; status = if ($failed -eq 0) { "passed" } else { "failed" }
    manifestHash = (Get-FileHash -LiteralPath $FixtureManifest -Algorithm SHA256).Hash.ToLowerInvariant()
    playerHash = (Get-FileHash -LiteralPath $PlayerPath -Algorithm SHA256).Hash.ToLowerInvariant()
    sampleMode = "random-access"; physicsMode = "off"; caseCount = $results.Count
    passedCases = $results.Count - $failed - $skipped; failedCases = $failed; skippedCases = $skipped; cases = @($results)
}
$summary | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $reportPath -Encoding UTF8
Write-Host ("{0} local-playback: cases={1}, passed={2}, failed={3}, skipped={4}, report={5}" -f $summary.status.ToUpperInvariant(), $summary.caseCount, $summary.passedCases, $summary.failedCases, $summary.skippedCases, $reportPath)
if ($failed -gt 0) { exit 1 }
exit 0
