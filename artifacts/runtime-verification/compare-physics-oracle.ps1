param(
    [string]$ManifestPath = "F:\Develop\MMDDev\GoldenOracle\manifests\physics-coarse.json",
    [string]$UnityReportDir = "artifacts/runtime-verification/physics-oracle/",
    [string]$OracleRoot = "F:\Develop\MMDDev\GoldenOracle",
    [string]$OutDir = "artifacts/physics-goldenoracle/"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Resolve-RelativePath {
    param(
        [string]$BasePath,
        [string]$Path
    )

    if ([string]::IsNullOrWhiteSpace($Path)) {
        return $null
    }

    if ([System.IO.Path]::IsPathRooted($Path)) {
        return [System.IO.Path]::GetFullPath($Path)
    }

    return [System.IO.Path]::GetFullPath((Join-Path $BasePath $Path))
}

function Get-JsonProperty {
    param(
        [object]$Object,
        [string]$Name
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

function ConvertTo-Array {
    param([object]$Value)

    if ($null -eq $Value) {
        return @()
    }

    return @($Value)
}

function Read-JsonFile {
    param([string]$Path)

    return Get-Content -Raw -LiteralPath $Path | ConvertFrom-Json
}

function Read-JsonLines {
    param([string]$Path)

    $records = @()
    foreach ($line in Get-Content -LiteralPath $Path) {
        if ([string]::IsNullOrWhiteSpace($line)) {
            continue
        }

        $records += ($line | ConvertFrom-Json)
    }

    return $records
}

function Get-CaseName {
    param([object]$Case)

    $name = Get-JsonProperty $Case "name"
    if ([string]::IsNullOrWhiteSpace([string]$name)) {
        throw "Manifest case is missing a non-empty name."
    }

    return [string]$name
}

function Get-Epsilon {
    param(
        [object]$Manifest,
        [object]$Case
    )

    $caseCompare = Get-JsonProperty $Case "compare"
    $caseEpsilon = Get-JsonProperty $caseCompare "epsilon"
    if ($null -ne $caseEpsilon) {
        return [double]$caseEpsilon
    }

    $defaults = Get-JsonProperty $Manifest "defaults"
    $defaultCompare = Get-JsonProperty $defaults "compare"
    $defaultEpsilon = Get-JsonProperty $defaultCompare "epsilon"
    if ($null -ne $defaultEpsilon) {
        return [double]$defaultEpsilon
    }

    return 0.003
}

function Resolve-OraclePath {
    param(
        [object]$Manifest,
        [object]$Case,
        [string]$ManifestDir,
        [string]$OracleRootPath
    )

    $oracle = Get-JsonProperty $Case "oracle"
    $oraclePath = Get-JsonProperty $oracle "path"
    if (-not [string]::IsNullOrWhiteSpace([string]$oraclePath)) {
        $manifestRelative = Resolve-RelativePath $ManifestDir ([string]$oraclePath)
        if (Test-Path -LiteralPath $manifestRelative) {
            return $manifestRelative
        }

        $rootRelative = Resolve-RelativePath $OracleRootPath ([string]$oraclePath)
        if (Test-Path -LiteralPath $rootRelative) {
            return $rootRelative
        }

        return $manifestRelative
    }

    $caseName = Get-CaseName $Case
    $defaults = Get-JsonProperty $Manifest "defaults"
    $outDir = Get-JsonProperty $defaults "outDir"
    if (-not [string]::IsNullOrWhiteSpace([string]$outDir)) {
        $resolvedOutDir = Resolve-RelativePath $ManifestDir ([string]$outDir)
        if (-not (Test-Path -LiteralPath $resolvedOutDir)) {
            $resolvedOutDir = Resolve-RelativePath $OracleRootPath ([string]$outDir)
        }

        return Join-Path $resolvedOutDir (Join-Path $caseName "oracle.actual.jsonl")
    }

    return Join-Path $OracleRootPath (Join-Path $caseName "oracle.actual.jsonl")
}

function Resolve-UnityReportPath {
    param(
        [object]$Case,
        [string]$UnityReportRoot
    )

    $caseName = Get-CaseName $Case
    $unityReport = Get-JsonProperty $Case "unityReport"
    $unityReportPath = Get-JsonProperty $unityReport "path"
    if (-not [string]::IsNullOrWhiteSpace([string]$unityReportPath)) {
        return Resolve-RelativePath $UnityReportRoot ([string]$unityReportPath)
    }

    $candidates = @(
        (Join-Path $UnityReportRoot ($caseName + ".json")),
        (Join-Path $UnityReportRoot (Join-Path $caseName "report.json")),
        (Join-Path $UnityReportRoot "report.json")
    )

    foreach ($candidate in $candidates) {
        if (Test-Path -LiteralPath $candidate) {
            return [System.IO.Path]::GetFullPath($candidate)
        }
    }

    return [System.IO.Path]::GetFullPath($candidates[0])
}

function Get-RecordFrame {
    param([object]$Record)

    foreach ($name in @("frame", "frameIndex", "f")) {
        $value = Get-JsonProperty $Record $name
        if ($null -ne $value) {
            return [int]$value
        }
    }

    throw "Oracle JSONL record is missing frame."
}

function Get-OracleFrameMap {
    param([object[]]$Records)

    $map = @{}
    foreach ($record in $Records) {
        $map[[int](Get-RecordFrame $record)] = $record
    }

    return $map
}

function Get-UnityCaseResult {
    param(
        [object]$Report,
        [string]$CaseName
    )

    $caseResults = ConvertTo-Array (Get-JsonProperty $Report "caseResults")
    foreach ($caseResult in $caseResults) {
        if ([string](Get-JsonProperty $caseResult "name") -eq $CaseName) {
            return $caseResult
        }
    }

    if ($caseResults.Count -gt 0) {
        return $caseResults[0]
    }

    throw "Unity report has no caseResults."
}

function Get-UnityFrameMap {
    param([object]$CaseResult)

    $map = @{}
    foreach ($frame in (ConvertTo-Array (Get-JsonProperty $CaseResult "sampledFrames"))) {
        $map[[int](Get-JsonProperty $frame "frame")] = $frame
    }

    return $map
}

function Get-BoneKey {
    param(
        [object]$Bone,
        [int]$FallbackIndex
    )

    $index = Get-JsonProperty $Bone "index"
    if ($null -ne $index) {
        return "i:" + [string]([int]$index)
    }

    $name = Get-JsonProperty $Bone "name"
    if (-not [string]::IsNullOrWhiteSpace([string]$name)) {
        return "n:" + [string]$name
    }

    return "o:" + [string]$FallbackIndex
}

function Get-BoneMap {
    param([object[]]$Bones)

    $map = @{}
    for ($i = 0; $i -lt $Bones.Count; $i++) {
        $bone = $Bones[$i]
        $map[(Get-BoneKey $bone $i)] = $bone
        $name = Get-JsonProperty $bone "name"
        if (-not [string]::IsNullOrWhiteSpace([string]$name)) {
            $map["n:" + [string]$name] = $bone
        }
    }

    return $map
}

function Get-Translation {
    param([object]$Bone)

    $matrix = ConvertTo-Array (Get-JsonProperty $Bone "worldMatrix")
    if ($matrix.Count -lt 12) {
        throw "Bone worldMatrix must contain at least 12 elements."
    }

    return @([double]$matrix[3], [double]$matrix[7], [double]$matrix[11])
}

function Compare-Case {
    param(
        [object]$Manifest,
        [object]$Case,
        [string]$ManifestDir,
        [string]$UnityReportRoot,
        [string]$OracleRootPath
    )

    $caseName = Get-CaseName $Case
    $epsilon = Get-Epsilon $Manifest $Case
    $oraclePath = Resolve-OraclePath $Manifest $Case $ManifestDir $OracleRootPath
    $unityReportPath = Resolve-UnityReportPath $Case $UnityReportRoot

    if (-not (Test-Path -LiteralPath $oraclePath)) {
        return [pscustomobject]@{
            name = $caseName
            status = "missing-oracle"
            oraclePath = $oraclePath
            unityReportPath = $unityReportPath
            epsilon = $epsilon
        }
    }

    if (-not (Test-Path -LiteralPath $unityReportPath)) {
        return [pscustomobject]@{
            name = $caseName
            status = "missing-unity-report"
            oraclePath = $oraclePath
            unityReportPath = $unityReportPath
            epsilon = $epsilon
        }
    }

    $oracleFrames = Get-OracleFrameMap (Read-JsonLines $oraclePath)
    $unityReport = Read-JsonFile $unityReportPath
    $unityCase = Get-UnityCaseResult $unityReport $caseName
    $unityFrames = Get-UnityFrameMap $unityCase
    $requestedFrames = ConvertTo-Array (Get-JsonProperty $Case "frames")
    if ($requestedFrames.Count -eq 0) {
        $requestedFrames = @($oracleFrames.Keys | Sort-Object)
    }

    $sumSquared = 0.0
    $maxDistance = 0.0
    $compared = 0
    $divergent = 0
    $axisFlipCount = 0
    $missingFrames = @()
    $missingBones = @()

    foreach ($frameValue in $requestedFrames) {
        $frame = [int]$frameValue
        if (-not $oracleFrames.ContainsKey($frame) -or -not $unityFrames.ContainsKey($frame)) {
            $missingFrames += $frame
            continue
        }

        $oracleRecord = $oracleFrames[$frame]
        $oracleModels = ConvertTo-Array (Get-JsonProperty $oracleRecord "models")
        if ($oracleModels.Count -eq 0) {
            $missingFrames += $frame
            continue
        }

        $oracleBones = ConvertTo-Array (Get-JsonProperty $oracleModels[0] "bones")
        $unityBones = ConvertTo-Array (Get-JsonProperty $unityFrames[$frame] "bones")
        $unityBoneMap = Get-BoneMap $unityBones

        for ($i = 0; $i -lt $oracleBones.Count; $i++) {
            $oracleBone = $oracleBones[$i]
            $key = Get-BoneKey $oracleBone $i
            if (-not $unityBoneMap.ContainsKey($key)) {
                $missingBones += ($frame.ToString() + ":" + $key)
                continue
            }

            $oracleTranslation = Get-Translation $oracleBone
            $unityTranslation = Get-Translation $unityBoneMap[$key]
            $dx = $unityTranslation[0] - $oracleTranslation[0]
            $dy = $unityTranslation[1] - $oracleTranslation[1]
            $dz = $unityTranslation[2] - $oracleTranslation[2]
            $distance = [Math]::Sqrt(($dx * $dx) + ($dy * $dy) + ($dz * $dz))

            $sumSquared += $distance * $distance
            $maxDistance = [Math]::Max($maxDistance, $distance)
            $compared++
            if ($distance -gt $epsilon) {
                $divergent++
            }

            for ($axis = 0; $axis -lt 3; $axis++) {
                $oracleAxis = $oracleTranslation[$axis]
                $unityAxis = $unityTranslation[$axis]
                if ([Math]::Abs($oracleAxis) -gt $epsilon -and
                    [Math]::Abs($unityAxis) -gt $epsilon -and
                    [Math]::Sign($oracleAxis) -ne [Math]::Sign($unityAxis)) {
                    $axisFlipCount++
                }
            }
        }
    }

    $rms = if ($compared -gt 0) { [Math]::Sqrt($sumSquared / $compared) } else { 0.0 }
    $ratio = if ($compared -gt 0) { [double]$divergent / [double]$compared } else { 0.0 }
    $status = if ($missingFrames.Count -gt 0 -or $missingBones.Count -gt 0) {
        "partial"
    } elseif ($divergent -gt 0 -or $axisFlipCount -gt 0) {
        "diverged"
    } else {
        "matched"
    }

    return [pscustomobject]@{
        name = $caseName
        status = $status
        oraclePath = $oraclePath
        unityReportPath = $unityReportPath
        epsilon = $epsilon
        comparedTranslations = $compared
        rmsTranslation = $rms
        maxTranslation = $maxDistance
        divergenceRatio = $ratio
        divergentTranslations = $divergent
        axisFlipDetected = ($axisFlipCount -gt 0)
        axisFlipCount = $axisFlipCount
        missingFrames = $missingFrames
        missingBones = $missingBones
    }
}

$manifestFullPath = [System.IO.Path]::GetFullPath($ManifestPath)
$manifestDir = Split-Path -Parent $manifestFullPath
$unityReportRoot = [System.IO.Path]::GetFullPath($UnityReportDir)
$oracleRootPath = [System.IO.Path]::GetFullPath($OracleRoot)
$outFullDir = [System.IO.Path]::GetFullPath($OutDir)

New-Item -ItemType Directory -Force -Path $outFullDir | Out-Null

$manifest = Read-JsonFile $manifestFullPath
$results = @()
foreach ($case in (ConvertTo-Array (Get-JsonProperty $manifest "cases"))) {
    $results += Compare-Case $manifest $case $manifestDir $unityReportRoot $oracleRootPath
}

$summary = [pscustomobject]@{
    schemaVersion = 1
    generatedAtUtc = [DateTime]::UtcNow.ToString("O")
    manifestPath = $manifestFullPath
    unityReportDir = $unityReportRoot
    oracleRoot = $oracleRootPath
    caseCount = $results.Count
    matchedCount = @($results | Where-Object { $_.status -eq "matched" }).Count
    divergedCount = @($results | Where-Object { $_.status -eq "diverged" }).Count
    partialCount = @($results | Where-Object { $_.status -eq "partial" }).Count
    missingCount = @($results | Where-Object { $_.status -like "missing-*" }).Count
    cases = $results
}

$summaryPath = Join-Path $outFullDir "summary.json"
$markdownPath = Join-Path $outFullDir "summary.md"
$summary | ConvertTo-Json -Depth 32 | Set-Content -LiteralPath $summaryPath -Encoding UTF8

$markdown = @()
$markdown += "# Physics GoldenOracle Comparison"
$markdown += ""
$markdown += "| Case | Status | RMS Translation | Max Translation | Divergence Ratio | Axis Flip | Compared |"
$markdown += "| --- | --- | ---: | ---: | ---: | --- | ---: |"
foreach ($result in $results) {
    $markdown += "| {0} | {1} | {2:N6} | {3:N6} | {4:P2} | {5} | {6} |" -f `
        $result.name,
        $result.status,
        [double](Get-JsonProperty $result "rmsTranslation"),
        [double](Get-JsonProperty $result "maxTranslation"),
        [double](Get-JsonProperty $result "divergenceRatio"),
        [string](Get-JsonProperty $result "axisFlipDetected"),
        [int](Get-JsonProperty $result "comparedTranslations")
}

$markdown | Set-Content -LiteralPath $markdownPath -Encoding UTF8

Write-Host "Wrote $summaryPath"
Write-Host "Wrote $markdownPath"
