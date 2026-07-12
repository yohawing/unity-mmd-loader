<#
.SYNOPSIS
Validates the local MMD fixture manifest and writes a sanitized summary artifact.

.DESCRIPTION
This gate is an opt-in preflight for licensed local assets. It validates
data-local/fixtures.local.json without copying or recording absolute asset paths
into artifacts. By default, missing local assets are report-only so public gates
do not fail on machines without the local corpus. Use -RequireLocalAssets to
promote missing or out-of-root local references to failures.

.PARAMETER FixtureManifest
Path to the local fixture manifest. Defaults to data-local/fixtures.local.json.

.PARAMETER ReportDir
Directory for sanitized output. Defaults to artifacts/release-gate/local-assets-<timestamp>.

.PARAMETER RequireLocalAssets
Fail when the manifest is missing, references are missing, or local references
resolve outside the declared root.
#>
param(
    [string] $FixtureManifest = "",
    [string] $ReportDir = "",
    [switch] $RequireLocalAssets
)

$ErrorActionPreference = "Stop"

$root = Resolve-Path (Join-Path $PSScriptRoot "..")
$timestamp = "{0}-{1}" -f (Get-Date -Format "yyyyMMdd-HHmmss-fff"), $PID

if ([string]::IsNullOrWhiteSpace($FixtureManifest)) {
    $FixtureManifest = Join-Path $root "data-local\fixtures.local.json"
}
$FixtureManifest = [System.IO.Path]::GetFullPath($FixtureManifest)

if ([string]::IsNullOrWhiteSpace($ReportDir)) {
    $ReportDir = Join-Path $root "artifacts\release-gate\local-assets-$timestamp"
}
$ReportDir = [System.IO.Path]::GetFullPath($ReportDir)
New-Item -ItemType Directory -Force -Path $ReportDir | Out-Null

function ConvertTo-StableHash {
    param([string] $Value)

    if ([string]::IsNullOrEmpty($Value)) {
        return ""
    }

    $bytes = [System.Text.Encoding]::UTF8.GetBytes($Value)
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        return [System.BitConverter]::ToString($sha.ComputeHash($bytes)).Replace("-", "").ToLowerInvariant()
    }
    finally {
        $sha.Dispose()
    }
}

function Resolve-ManifestPath {
    param(
        [Parameter(Mandatory = $true)][string] $BasePath,
        [Parameter(Mandatory = $true)][string] $Path
    )

    if ([System.IO.Path]::IsPathRooted($Path)) {
        return [System.IO.Path]::GetFullPath($Path)
    }

    return [System.IO.Path]::GetFullPath((Join-Path $BasePath $Path))
}

function Test-PathUnderRoot {
    param(
        [Parameter(Mandatory = $true)][string] $Candidate,
        [Parameter(Mandatory = $true)][string] $RootPath
    )

    $candidateFull = [System.IO.Path]::GetFullPath($Candidate).TrimEnd('\', '/')
    $rootFull = [System.IO.Path]::GetFullPath($RootPath).TrimEnd('\', '/')
    return $candidateFull.Equals($rootFull, [System.StringComparison]::OrdinalIgnoreCase) -or
        $candidateFull.StartsWith($rootFull + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase) -or
        $candidateFull.StartsWith($rootFull + [System.IO.Path]::AltDirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase)
}

function Add-Reference {
    param(
        [System.Collections.Generic.List[object]] $References,
        [string] $Kind,
        [string] $Key,
        [string] $PathValue,
        [string] $BasePath,
        [bool] $Required
    )

    if ([string]::IsNullOrWhiteSpace($PathValue)) {
        return
    }

    $resolved = Resolve-ManifestPath -BasePath $BasePath -Path $PathValue
    $exists = Test-Path -LiteralPath $resolved
    $underRoot = Test-PathUnderRoot -Candidate $resolved -RootPath $BasePath
    $isRooted = [System.IO.Path]::IsPathRooted($PathValue)

    $References.Add([pscustomobject]@{
        kind      = $Kind
        key       = $Key
        exists    = $exists
        underRoot = $underRoot
        rooted    = $isRooted
        required  = $Required
        pathHash  = ConvertTo-StableHash $resolved
        extension = [System.IO.Path]::GetExtension($resolved).TrimStart('.').ToLowerInvariant()
    }) | Out-Null
}

function Add-MissingReference {
    param(
        [System.Collections.Generic.List[object]] $References,
        [string] $Kind,
        [string] $Key,
        [bool] $Required
    )

    $References.Add([pscustomobject]@{
        kind      = $Kind
        key       = $Key
        exists    = $false
        underRoot = $true
        rooted    = $false
        required  = $Required
        pathHash  = ""
        extension = ""
    }) | Out-Null
}

function Add-ManifestKeyReference {
    param(
        [System.Collections.Generic.List[object]] $References,
        [object] $ByExtension,
        [string] $CaseKey,
        [string] $Kind,
        [string] $Extension,
        [string] $ReferenceKey,
        [string] $BasePath,
        [bool] $Required
    )

    if ([string]::IsNullOrWhiteSpace($Extension) -or [string]::IsNullOrWhiteSpace($ReferenceKey)) {
        if ($Required) {
            Add-MissingReference -References $References -Kind $Kind -Key $CaseKey -Required $true
        }
        return
    }

    $extensionMap = Get-PropertyValue $ByExtension $Extension
    if ($null -eq $extensionMap) {
        Add-MissingReference -References $References -Kind $Kind -Key ("$CaseKey/$Extension.$ReferenceKey") -Required $Required
        return
    }

    $pathValue = [string] (Get-PropertyValue $extensionMap $ReferenceKey)
    if ([string]::IsNullOrWhiteSpace($pathValue)) {
        Add-MissingReference -References $References -Kind $Kind -Key ("$CaseKey/$Extension.$ReferenceKey") -Required $Required
        return
    }

    Add-Reference -References $References `
        -Kind $Kind `
        -Key ("$CaseKey/$Extension.$ReferenceKey") `
        -PathValue $pathValue `
        -BasePath $BasePath `
        -Required $Required
}

function Get-PropertyValue {
    param(
        [object] $Object,
        [string] $Name
    )

    if ($null -eq $Object) {
        return $null
    }

    $property = $Object.PSObject.Properties[$Name]
    if ($null -eq $property) {
        return $null
    }

    return $property.Value
}

function Get-ArrayValue {
    param(
        [object] $Object,
        [string] $Name
    )

    $value = Get-PropertyValue $Object $Name
    if ($null -eq $value) {
        return @()
    }

    return @($value)
}

$summaryPath = Join-Path $ReportDir "summary.json"

if (-not (Test-Path -LiteralPath $FixtureManifest)) {
    $summary = [pscustomobject]@{
        schemaVersion       = 1
        timestamp           = $timestamp
        status              = if ($RequireLocalAssets) { "failed" } else { "skipped" }
        requireLocalAssets  = [bool] $RequireLocalAssets
        manifestPresent     = $false
        manifestHash        = ""
        declaredRootHash    = ""
        totalReferences     = 0
        existingReferences  = 0
        missingReferences   = 0
        outsideRootRefs     = 0
        rootedReferences    = 0
        playbackCases       = 0
        skippedCases        = 0
        referencesByKind    = @{}
        missing             = @()
        outsideRoot         = @()
    }
    $summary | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $summaryPath -Encoding UTF8

    if ($RequireLocalAssets) {
        Write-Host "FAIL local-assets: fixture manifest is missing; summary=$summaryPath"
        exit 1
    }

    Write-Host "SKIP local-assets: fixture manifest is missing; summary=$summaryPath"
    exit 0
}

$manifest = Get-Content -LiteralPath $FixtureManifest -Raw -Encoding UTF8 | ConvertFrom-Json
$manifestDirectory = Split-Path -Parent $FixtureManifest
$declaredRoot = Get-PropertyValue $manifest "basePath"
if ([string]::IsNullOrWhiteSpace($declaredRoot)) {
    $declaredRoot = Get-PropertyValue $manifest "root"
}
if ([string]::IsNullOrWhiteSpace($declaredRoot)) {
    $declaredRoot = "."
}
$basePath = Resolve-ManifestPath -BasePath $manifestDirectory -Path $declaredRoot

$references = [System.Collections.Generic.List[object]]::new()

$paths = Get-PropertyValue $manifest "paths"
$releaseSmoke = Get-PropertyValue $paths "releaseSmoke"
$byExtension = Get-PropertyValue $releaseSmoke "byExtension"
if ($null -ne $byExtension) {
    foreach ($extensionGroup in $byExtension.PSObject.Properties) {
        foreach ($entry in $extensionGroup.Value.PSObject.Properties) {
            Add-Reference -References $references `
                -Kind $extensionGroup.Name `
                -Key $entry.Name `
                -PathValue ([string] $entry.Value) `
                -BasePath $basePath `
                -Required $false
        }
    }
}

$playbackSmoke = Get-PropertyValue $paths "playbackSmoke"
$cases = Get-ArrayValue $playbackSmoke "cases"
foreach ($case in $cases) {
    $name = [string] (Get-PropertyValue $case "name")
    $caseKey = if ([string]::IsNullOrWhiteSpace($name)) { "unnamed" } else { $name }
    $model = Get-PropertyValue $case "model"
    $motion = Get-PropertyValue $case "motion"
    $camera = Get-PropertyValue $case "camera"
    $audio = Get-PropertyValue $case "audio"
    $background = Get-PropertyValue $case "background"
    $modelExtension = [string] (Get-PropertyValue $model "extension")
    if ([string]::IsNullOrWhiteSpace($modelExtension)) {
        $modelExtension = "pmx"
    }

    if ($modelExtension -eq "pmx") {
        Add-ManifestKeyReference -References $references `
            -ByExtension $byExtension `
            -CaseKey $caseKey `
            -Kind "caseModel" `
            -Extension "pmx" `
            -ReferenceKey ([string] (Get-PropertyValue $model "key")) `
            -BasePath $basePath `
            -Required $true
        Add-ManifestKeyReference -References $references `
            -ByExtension $byExtension `
            -CaseKey $caseKey `
            -Kind "caseMotion" `
            -Extension "vmd" `
            -ReferenceKey ([string] (Get-PropertyValue $motion "key")) `
            -BasePath $basePath `
            -Required $true
        Add-ManifestKeyReference -References $references `
            -ByExtension $byExtension `
            -CaseKey $caseKey `
            -Kind "caseCamera" `
            -Extension "cameraVmd" `
            -ReferenceKey ([string] (Get-PropertyValue $camera "key")) `
            -BasePath $basePath `
            -Required $false
        Add-ManifestKeyReference -References $references `
            -ByExtension $byExtension `
            -CaseKey $caseKey `
            -Kind "caseAudio" `
            -Extension ([string] (Get-PropertyValue $audio "extension")) `
            -ReferenceKey ([string] (Get-PropertyValue $audio "key")) `
            -BasePath $basePath `
            -Required $false
        Add-ManifestKeyReference -References $references `
            -ByExtension $byExtension `
            -CaseKey $caseKey `
            -Kind "caseBackground" `
            -Extension ([string] (Get-PropertyValue $background "extension")) `
            -ReferenceKey ([string] (Get-PropertyValue $background "key")) `
            -BasePath $basePath `
            -Required $false
    }

    foreach ($field in @("oracle")) {
        $value = [string] (Get-PropertyValue $case $field)
        Add-Reference -References $references `
            -Kind $field `
            -Key $caseKey `
            -PathValue $value `
            -BasePath $basePath `
            -Required $false
    }
}

$legacyFixtures = Get-ArrayValue $manifest "fixtures"
foreach ($fixture in $legacyFixtures) {
    $id = [string] (Get-PropertyValue $fixture "id")
    $fixtureKey = if ([string]::IsNullOrWhiteSpace($id)) { "unnamed" } else { $id }
    foreach ($field in @("model", "motion", "camera", "audio", "background")) {
        $value = [string] (Get-PropertyValue $fixture $field)
        Add-Reference -References $references `
            -Kind $field `
            -Key $fixtureKey `
            -PathValue $value `
            -BasePath $basePath `
            -Required ($field -eq "model" -or $field -eq "motion")
    }
}

$missing = @($references | Where-Object { -not $_.exists })
$outsideRoot = @($references | Where-Object { -not $_.underRoot })
$rooted = @($references | Where-Object { $_.rooted })
$skippedCases = @($cases | Where-Object {
    $skipReason = [string] (Get-PropertyValue $_ "skipReason")
    -not [string]::IsNullOrWhiteSpace($skipReason)
}).Count

$referencesByKind = @{}
foreach ($group in ($references | Group-Object kind)) {
    $referencesByKind[$group.Name] = $group.Count
}

$status = "passed"
if ($RequireLocalAssets -and ($missing.Count -gt 0 -or $outsideRoot.Count -gt 0)) {
    $status = "failed"
}
elseif ($missing.Count -gt 0 -or $outsideRoot.Count -gt 0) {
    $status = "report-only"
}

$summary = [pscustomobject]@{
    schemaVersion       = 1
    timestamp           = $timestamp
    status              = $status
    requireLocalAssets  = [bool] $RequireLocalAssets
    manifestPresent     = $true
    manifestHash        = (Get-FileHash -LiteralPath $FixtureManifest -Algorithm SHA256).Hash
    declaredRootHash    = ConvertTo-StableHash $basePath
    totalReferences     = $references.Count
    existingReferences  = @($references | Where-Object { $_.exists }).Count
    missingReferences   = $missing.Count
    outsideRootRefs     = $outsideRoot.Count
    rootedReferences    = $rooted.Count
    playbackCases       = $cases.Count
    skippedCases        = $skippedCases
    referencesByKind    = $referencesByKind
    missing             = @($missing | Select-Object kind, key, pathHash, extension)
    outsideRoot         = @($outsideRoot | Select-Object kind, key, pathHash, extension)
}

$summary | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $summaryPath -Encoding UTF8

Write-Host ("{0} local-assets: refs={1}, existing={2}, missing={3}, outsideRoot={4}, playbackCases={5}, skippedCases={6}, summary={7}" -f `
    $status.ToUpperInvariant(),
    $summary.totalReferences,
    $summary.existingReferences,
    $summary.missingReferences,
    $summary.outsideRootRefs,
    $summary.playbackCases,
    $summary.skippedCases,
    $summaryPath)

if ($status -eq "failed") {
    exit 1
}

exit 0
