<#
.SYNOPSIS
Builds a sanitized inventory and reduction-candidate report for the local MMD corpus.
#>
param(
    [string] $FixtureManifest = "",
    [string] $ReportDir = "",
    [string] $MmdAnimCli = "",
    [string] $ProposedManifestPath = "",
    [switch] $RequireLocalAssets
)

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$stamp = "{0}-{1}" -f (Get-Date -Format "yyyyMMdd-HHmmss-fff"), $PID
if ([string]::IsNullOrWhiteSpace($FixtureManifest)) {
    $FixtureManifest = Join-Path $repoRoot "data-local\fixtures.local.json"
}
if ([string]::IsNullOrWhiteSpace($ReportDir)) {
    $ReportDir = Join-Path $repoRoot "artifacts\release-gate\local-corpus-$stamp"
}
$FixtureManifest = [IO.Path]::GetFullPath($FixtureManifest)
$ReportDir = [IO.Path]::GetFullPath($ReportDir)
New-Item -ItemType Directory -Force $ReportDir | Out-Null
$reportPath = Join-Path $ReportDir "corpus-inventory.json"

if ([string]::IsNullOrWhiteSpace($MmdAnimCli)) {
    foreach ($candidate in @(
        (Join-Path $repoRoot "native\mmd-anim\target\release\mmd-anim.exe"),
        (Join-Path $repoRoot "native\mmd-anim\target\debug\mmd-anim.exe")
    )) {
        if (Test-Path -LiteralPath $candidate -PathType Leaf) { $MmdAnimCli = $candidate; break }
    }
}
if (-not [string]::IsNullOrWhiteSpace($MmdAnimCli)) { $MmdAnimCli = [IO.Path]::GetFullPath($MmdAnimCli) }

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

function Test-UnderRoot([string] $Candidate, [string] $RootPath) {
    $candidateFull = [IO.Path]::GetFullPath($Candidate).TrimEnd('\', '/')
    $rootFull = [IO.Path]::GetFullPath($RootPath).TrimEnd('\', '/')
    return $candidateFull.Equals($rootFull, [StringComparison]::OrdinalIgnoreCase) -or
        $candidateFull.StartsWith($rootFull + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -or
        $candidateFull.StartsWith($rootFull + [IO.Path]::AltDirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)
}

function Get-Category([string] $ExtensionGroup, [System.Collections.Generic.HashSet[string]] $CharacterKeys,
    [System.Collections.Generic.HashSet[string]] $BackgroundKeys, [System.Collections.Generic.HashSet[string]] $BodyKeys,
    [System.Collections.Generic.HashSet[string]] $CameraKeys, [string] $Key) {
    if ($BackgroundKeys.Contains("$ExtensionGroup/$Key") -or $ExtensionGroup -in @("backgroundPmx", "backgroundPmd")) { return "backgroundModel" }
    if ($CharacterKeys.Contains("$ExtensionGroup/$Key")) { return "characterModel" }
    if ($ExtensionGroup -in @("pmx", "pmd")) { return "unknownModel" }
    if ($CameraKeys.Contains("$ExtensionGroup/$Key") -or $ExtensionGroup -eq "cameraVmd") { return "cameraMotion" }
    if ($BodyKeys.Contains("$ExtensionGroup/$Key") -or $ExtensionGroup -eq "vmd") { return "bodyMotion" }
    if ($ExtensionGroup -in @("mp3", "wav", "ogg")) { return "audio" }
    if ($ExtensionGroup -eq "vpd") { return "pose" }
    return "other"
}

function Get-VmdMetadata([string] $AssetPath) {
    if ([string]::IsNullOrWhiteSpace($MmdAnimCli) -or -not (Test-Path -LiteralPath $MmdAnimCli -PathType Leaf)) {
        return [ordered]@{ status = "unavailable" }
    }
    try {
        $summary = (& $MmdAnimCli inspect $AssetPath 2>$null | Out-String).Trim()
        if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($summary)) { return [ordered]@{ status = "failed" } }
        $match = [regex]::Match($summary, '^VMD parser: boneFrames=(?<bone>\d+) morphFrames=(?<morph>\d+) cameraFrames=(?<camera>\d+) lightFrames=(?<light>\d+) selfShadowFrames=(?<shadow>\d+) propertyFrames=(?<property>\d+) maxFrame=(?<max>\d+)$')
        if (-not $match.Success) { return [ordered]@{ status = "failed" } }
        $counts = [ordered]@{
            bones = [int64]$match.Groups['bone'].Value
            morphs = [int64]$match.Groups['morph'].Value
            cameras = [int64]$match.Groups['camera'].Value
            lights = [int64]$match.Groups['light'].Value
            properties = [int64]$match.Groups['property'].Value
            selfShadows = [int64]$match.Groups['shadow'].Value
        }
        $channels = [Collections.Generic.List[string]]::new()
        foreach ($channel in @(
            @{ Name = "bone"; Value = [int64]$counts.bones },
            @{ Name = "morph"; Value = [int64]$counts.morphs },
            @{ Name = "camera"; Value = [int64]$counts.cameras },
            @{ Name = "light"; Value = [int64]$counts.lights },
            @{ Name = "property"; Value = [int64]$counts.properties },
            @{ Name = "selfShadow"; Value = [int64]$counts.selfShadows }
        )) { if ($channel.Value -gt 0) { $channels.Add($channel.Name) } }
        return [ordered]@{
            status = "parsed"
            frameRange = [ordered]@{ min = 0; max = [int64]$match.Groups['max'].Value }
            channels = @($channels)
            frameCounts = [ordered]@{
                bone = [int64]$counts.bones; morph = [int64]$counts.morphs; camera = [int64]$counts.cameras
                light = [int64]$counts.lights; property = [int64]$counts.properties; selfShadow = [int64]$counts.selfShadows
            }
        }
    }
    catch { return [ordered]@{ status = "failed" } }
}

if (-not (Test-Path -LiteralPath $FixtureManifest)) {
    $status = if ($RequireLocalAssets) { "failed" } else { "skipped" }
    [ordered]@{ schemaVersion = 1; status = $status; manifestPresent = $false } |
        ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $reportPath -Encoding UTF8
    Write-Host "$($status.ToUpperInvariant()) local-corpus: manifest missing; report=$reportPath"
    if ($RequireLocalAssets) { exit 1 } else { exit 0 }
}

$manifest = Get-Content -LiteralPath $FixtureManifest -Raw -Encoding UTF8 | ConvertFrom-Json
$manifestDir = Split-Path -Parent $FixtureManifest
$declaredRoot = [string](Get-Value $manifest "basePath")
if ([string]::IsNullOrWhiteSpace($declaredRoot)) { $declaredRoot = [string](Get-Value $manifest "root") }
if ([string]::IsNullOrWhiteSpace($declaredRoot)) { $declaredRoot = "." }
$basePath = Resolve-AssetPath $manifestDir $declaredRoot
$byExtension = $manifest.paths.releaseSmoke.byExtension
$cases = @($manifest.paths.playbackSmoke.cases)

$characterKeys = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
$backgroundKeys = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
$bodyKeys = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
$cameraKeys = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
$caseUses = @{}
$bundleById = @{}
$unresolved = [Collections.Generic.List[object]]::new()

function Add-CaseUse([string] $Group, [string] $Key, [string] $CaseName, [string] $Role) {
    if ([string]::IsNullOrWhiteSpace($Key)) { return }
    $id = "$Group/$Key"
    if (-not $caseUses.ContainsKey($id)) { $caseUses[$id] = [Collections.Generic.List[string]]::new() }
    $caseUses[$id].Add((Get-StableHash "$CaseName/$Role"))
}

foreach ($case in $cases) {
    $caseName = [string](Get-Value $case "name")
    foreach ($spec in @(
        @{ Field = "model"; DefaultGroup = "pmx"; Set = $characterKeys; Role = "model" },
        @{ Field = "motion"; DefaultGroup = "vmd"; Set = $bodyKeys; Role = "motion" },
        @{ Field = "camera"; DefaultGroup = "cameraVmd"; Set = $cameraKeys; Role = "camera" },
        @{ Field = "background"; DefaultGroup = "backgroundPmx"; Set = $backgroundKeys; Role = "background" },
        @{ Field = "audio"; DefaultGroup = "mp3"; Set = $null; Role = "audio" }
    )) {
        $reference = Get-Value $case $spec.Field
        if ($null -eq $reference) { continue }
        $key = [string](Get-Value $reference "key")
        $group = [string](Get-Value $reference "extension")
        if ([string]::IsNullOrWhiteSpace($group)) { $group = $spec.DefaultGroup }
        $map = Get-Value $byExtension $group
        $pathValue = [string](Get-Value $map $key)
        if ([string]::IsNullOrWhiteSpace($pathValue)) {
            $unresolved.Add([ordered]@{ caseHash = Get-StableHash $caseName; role = $spec.Role; group = $group; key = $key })
            continue
        }
        if ($null -ne $spec.Set) { [void]$spec.Set.Add("$group/$key") }
        Add-CaseUse $group $key $caseName $spec.Role
    }
}

$records = [Collections.Generic.List[object]]::new()
foreach ($groupProperty in $byExtension.PSObject.Properties) {
    $group = $groupProperty.Name
    foreach ($entry in $groupProperty.Value.PSObject.Properties) {
        $key = $entry.Name
        $resolved = Resolve-AssetPath $basePath ([string]$entry.Value)
        $bundleById["$group/$key"] = Get-StableHash ([IO.Path]::GetDirectoryName($resolved).ToLowerInvariant())
        $exists = Test-Path -LiteralPath $resolved -PathType Leaf
        $underRoot = Test-UnderRoot $resolved $basePath
        $sha256 = ""
        $length = 0L
        if ($exists) {
            $file = Get-Item -LiteralPath $resolved
            $length = $file.Length
            $sha256 = (Get-FileHash -LiteralPath $resolved -Algorithm SHA256).Hash.ToLowerInvariant()
        }
        $metadata = if ($exists -and [IO.Path]::GetExtension($resolved).Equals(".vmd", [StringComparison]::OrdinalIgnoreCase)) {
            Get-VmdMetadata $resolved
        } else { [ordered]@{ status = "notApplicable" } }
        $uses = if ($caseUses.ContainsKey("$group/$key")) { @($caseUses["$group/$key"] | Sort-Object -Unique) } else { @() }
        $records.Add([pscustomobject][ordered]@{
            id = "$group/$key"
            group = $group
            key = $key
            category = Get-Category $group $characterKeys $backgroundKeys $bodyKeys $cameraKeys $key
            format = [IO.Path]::GetExtension($resolved).TrimStart('.').ToLowerInvariant()
            exists = $exists
            underRoot = $underRoot
            sha256 = $sha256
            byteLength = $length
            referenceCount = $uses.Count
            caseCoverage = $uses
            metadata = $metadata
        })
    }
}

foreach ($case in $cases) {
    $caseName = [string](Get-Value $case "name")
    $oraclePath = [string](Get-Value $case "oracle")
    if ([string]::IsNullOrWhiteSpace($oraclePath)) { continue }
    $resolved = Resolve-AssetPath $basePath $oraclePath
    $exists = Test-Path -LiteralPath $resolved -PathType Leaf
    $underRoot = Test-UnderRoot $resolved $basePath
    $records.Add([pscustomobject][ordered]@{
        id = "oracle/$(Get-StableHash $caseName)"
        group = "oracle"
        key = Get-StableHash $caseName
        category = "oracle"
        format = [IO.Path]::GetExtension($resolved).TrimStart('.').ToLowerInvariant()
        exists = $exists
        underRoot = $underRoot
        sha256 = if ($exists) { (Get-FileHash -LiteralPath $resolved -Algorithm SHA256).Hash.ToLowerInvariant() } else { "" }
        byteLength = if ($exists) { (Get-Item -LiteralPath $resolved).Length } else { 0L }
        referenceCount = 1
        caseCoverage = @(Get-StableHash "$caseName/oracle")
        metadata = [ordered]@{ status = "notApplicable" }
    })
}

$exactGroups = @($records | Where-Object { $_.exists } | Group-Object sha256 | Where-Object Count -gt 1 | ForEach-Object {
    $ordered = @($_.Group | Sort-Object @{Expression = "referenceCount"; Descending = $true}, id)
    [ordered]@{
        sha256 = $_.Name
        retainId = $ordered[0].id
        removeCandidateIds = @($ordered | Select-Object -Skip 1 -ExpandProperty id)
        reason = "exactSha256Duplicate"
    }
})

# Same category/format/size is a deliberately conservative near-duplicate signal.
$nearGroups = @($records | Where-Object { $_.exists -and $_.byteLength -gt 0 } |
    Group-Object category, format, byteLength | Where-Object Count -gt 1 | ForEach-Object {
        $hashes = @($_.Group.sha256 | Sort-Object -Unique)
        if ($hashes.Count -le 1) { return }
        $ordered = @($_.Group | Sort-Object @{Expression = "referenceCount"; Descending = $true}, id)
        [ordered]@{
            category = $ordered[0].category
            format = $ordered[0].format
            byteLength = $ordered[0].byteLength
            retainId = $ordered[0].id
            reviewCandidateIds = @($ordered | Select-Object -Skip 1 -ExpandProperty id)
            reason = "sameCategoryFormatAndExactByteLength"
        }
    })

$exactRemove = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($group in $exactGroups) { foreach ($id in $group.removeCandidateIds) { [void]$exactRemove.Add($id) } }

$bodyVariantGroups = @($records | Where-Object {
    $_.category -eq "bodyMotion" -and $_.exists -and $_.metadata.status -eq "parsed" -and -not $exactRemove.Contains($_.id)
} | Group-Object {
    $c = $_.metadata.frameCounts
    "max=$($_.metadata.frameRange.max);bone=$($c.bone);morph=$($c.morph);camera=$($c.camera);light=$($c.light);property=$($c.property);shadow=$($c.selfShadow)"
} | Where-Object Count -gt 1 | ForEach-Object {
    $ordered = @($_.Group | Sort-Object @{ Expression = "referenceCount"; Descending = $true }, id)
    [ordered]@{
        signatureHash = Get-StableHash $_.Name
        retainId = $ordered[0].id
        removeCandidateIds = @($ordered | Select-Object -Skip 1 -ExpandProperty id)
        reason = "sameParserChannelCountsAndFrameRange"
    }
})
$variantRemove = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($group in $bodyVariantGroups) {
    foreach ($id in $group.removeCandidateIds) {
        $record = $records | Where-Object id -eq $id | Select-Object -First 1
        if ($null -ne $record -and $record.referenceCount -eq 0) { [void]$variantRemove.Add($id) }
    }
}

$bundleGroups = @($records | Where-Object {
    $_.category -eq "bodyMotion" -and $_.exists -and -not $exactRemove.Contains($_.id) -and -not $variantRemove.Contains($_.id)
} | Group-Object { $bundleById[$_.id] } | Where-Object Count -gt 1 | ForEach-Object {
    $referenced = @($_.Group | Where-Object { $_.referenceCount -gt 0 } | Sort-Object id)
    $unreferenced = @($_.Group | Where-Object { $_.referenceCount -eq 0 } | Sort-Object id)
    if ($unreferenced.Count -le 1) { return }
    [ordered]@{
        bundleHash = $_.Name
        retainIds = @($referenced.id) + @($unreferenced[0].id)
        removeCandidateIds = @($unreferenced | Select-Object -Skip 1 -ExpandProperty id)
        reason = "sameBundleDirectoryUnreferencedVariant"
    }
})
$bundleRemove = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($group in $bundleGroups) { foreach ($id in $group.removeCandidateIds) { [void]$bundleRemove.Add($id) } }

$proposedRemove = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($set in @($exactRemove, $variantRemove, $bundleRemove)) {
    foreach ($id in $set) {
        $record = $records | Where-Object id -eq $id | Select-Object -First 1
        if ($null -ne $record -and $record.referenceCount -eq 0) { [void]$proposedRemove.Add($id) }
    }
}
$proposedManifestHash = ""
if (-not [string]::IsNullOrWhiteSpace($ProposedManifestPath)) {
    $ProposedManifestPath = [IO.Path]::GetFullPath($ProposedManifestPath)
    $dataLocalRoot = [IO.Path]::GetFullPath((Join-Path $repoRoot "data-local")).TrimEnd('\', '/')
    if (-not (Test-UnderRoot $ProposedManifestPath $dataLocalRoot)) {
        throw "ProposedManifestPath must stay under data-local because it contains licensed local paths."
    }
    $proposed = ($manifest | ConvertTo-Json -Depth 100 | ConvertFrom-Json)
    foreach ($id in $proposedRemove) {
        $parts = $id.Split('/', 2)
        if ($parts.Count -ne 2) { continue }
        $map = Get-Value $proposed.paths.releaseSmoke.byExtension $parts[0]
        if ($null -ne $map) { [void]$map.PSObject.Properties.Remove($parts[1]) }
    }
    New-Item -ItemType Directory -Force (Split-Path -Parent $ProposedManifestPath) | Out-Null
    $proposed | ConvertTo-Json -Depth 100 | Set-Content -LiteralPath $ProposedManifestPath -Encoding UTF8
    $proposedManifestHash = (Get-FileHash -LiteralPath $ProposedManifestPath -Algorithm SHA256).Hash.ToLowerInvariant()
}
$counts = [ordered]@{}
foreach ($category in @("characterModel", "backgroundModel", "unknownModel", "bodyMotion", "cameraMotion", "audio", "oracle", "pose", "other")) {
    $items = @($records | Where-Object category -eq $category)
    $counts[$category] = [ordered]@{
        before = $items.Count
        proposedAfterExactDuplicates = @($items | Where-Object { -not $exactRemove.Contains($_.id) }).Count
        proposedAfterVariantReduction = @($items | Where-Object { -not $exactRemove.Contains($_.id) -and -not $variantRemove.Contains($_.id) }).Count
        proposedAfterBundleReview = @($items | Where-Object { -not $exactRemove.Contains($_.id) -and -not $variantRemove.Contains($_.id) -and -not $bundleRemove.Contains($_.id) }).Count
    }
}
$modelBefore = $counts.characterModel.before + $counts.backgroundModel.before + $counts.unknownModel.before
$modelAfter = $counts.characterModel.proposedAfterExactDuplicates + $counts.backgroundModel.proposedAfterExactDuplicates + $counts.unknownModel.proposedAfterExactDuplicates
$bodyAfterVariants = $counts.bodyMotion.proposedAfterBundleReview

$missing = @($records | Where-Object { -not $_.exists })
$outside = @($records | Where-Object { -not $_.underRoot })
$status = if ($RequireLocalAssets -and ($missing.Count -gt 0 -or $outside.Count -gt 0 -or $unresolved.Count -gt 0)) { "failed" }
elseif ($missing.Count -gt 0 -or $outside.Count -gt 0 -or $unresolved.Count -gt 0) { "report-only" } else { "passed" }

$report = [ordered]@{
    schemaVersion = 1
    status = $status
    manifestHash = (Get-FileHash -LiteralPath $FixtureManifest -Algorithm SHA256).Hash.ToLowerInvariant()
    declaredRootHash = Get-StableHash $basePath
    playbackCaseCount = $cases.Count
    allPlaybackCaseReferencesResolved = ($unresolved.Count -eq 0)
    countsByCategory = $counts
    thresholds = [ordered]@{
        pmxPmdModelBefore = $modelBefore
        pmxPmdModelProposedAfter = $modelAfter
        pmxPmdModelAtMost100 = ($modelAfter -le 100)
        bodyMotionBefore = $counts.bodyMotion.before
        bodyMotionProposedAfter = $bodyAfterVariants
        bodyMotionAtMost100 = ($bodyAfterVariants -le 100)
    }
    exactDuplicateGroups = $exactGroups
    nearDuplicateReviewGroups = $nearGroups
    bodyMotionVariantGroups = $bodyVariantGroups
    bodyMotionBundleReviewGroups = $bundleGroups
    proposedManifest = [ordered]@{
        written = (-not [string]::IsNullOrWhiteSpace($ProposedManifestPath))
        hash = $proposedManifestHash
        removedReferenceCount = $proposedRemove.Count
    }
    metadataProbe = [ordered]@{
        available = (-not [string]::IsNullOrWhiteSpace($MmdAnimCli) -and (Test-Path -LiteralPath $MmdAnimCli -PathType Leaf))
        parsed = @($records | Where-Object { $_.metadata.status -eq "parsed" }).Count
        failed = @($records | Where-Object { $_.metadata.status -eq "failed" }).Count
        unavailable = @($records | Where-Object { $_.metadata.status -eq "unavailable" }).Count
    }
    unresolvedCaseReferences = @($unresolved)
    missing = @($missing | Select-Object id, category, format)
    outsideRoot = @($outside | Select-Object id, category, format)
    assets = @($records)
    limitations = @(
        "near-duplicate byte-length candidates are review-only; parser-signature candidates require human confirmation before manifest removal",
        "only SHA256 matches are unconditional duplicate-removal candidates"
    )
}
$report | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $reportPath -Encoding UTF8
Write-Host ("{0} local-corpus: assets={1}, exactGroups={2}, nearGroups={3}, unresolved={4}, report={5}" -f $status.ToUpperInvariant(), $records.Count, $exactGroups.Count, $nearGroups.Count, $unresolved.Count, $reportPath)
if ($status -eq "failed") { exit 1 }
exit 0
