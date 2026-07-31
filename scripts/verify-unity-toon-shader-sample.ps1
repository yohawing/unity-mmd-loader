param(
    [string] $Unity = "C:\Program Files\Unity\Hub\Editor\6000.4.8f1\Editor\Unity.exe",
    [string] $ProjectPath = "F:\Develop\MMDDev\unity-mmd-loader\unity-mmd",
    [string] $PackagePath = "F:\Develop\MMDDev\unity-mmd-loader\packages\com.yohawing.mmd-loader",
    [string] $ArtifactsPath = "F:\Develop\MMDDev\unity-mmd-loader\artifacts\unity-toon-shader-sample"
)

$ErrorActionPreference = "Stop"

. (Join-Path $PSScriptRoot "unity-project-guard.ps1")
. (Join-Path $PSScriptRoot "unity-process-environment.ps1")
Initialize-UnityProcessEnvironment
. (Join-Path $PSScriptRoot "read-nunit-test-result.ps1")

function Resolve-FullPath {
    param([Parameter(Mandatory = $true)][string] $Path)

    if ([System.IO.Path]::IsPathRooted($Path)) {
        return [System.IO.Path]::GetFullPath($Path)
    }

    return [System.IO.Path]::GetFullPath((Join-Path (Get-Location) $Path))
}

function Get-GateContentManifest {
    param(
        [Parameter(Mandatory = $true)][string] $Path,
        [Parameter(Mandatory = $true)][string] $SampleMetaPath,
        [switch] $LegacyRootMetaHash
    )

    $entries = @()
    if (Test-Path -LiteralPath $Path -PathType Container) {
        $entries += @(
            Get-ChildItem -LiteralPath $Path -Recurse -Directory |
                ForEach-Object {
                    $relativePath = [System.IO.Path]::GetRelativePath($Path, $_.FullName).Replace('\', '/')
                    "D|$relativePath"
                }
        )
        $entries += @(
            Get-ChildItem -LiteralPath $Path -Recurse -File |
                ForEach-Object {
                    $relativePath = [System.IO.Path]::GetRelativePath($Path, $_.FullName).Replace('\', '/')
                    $hash = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash
                    "F|{0}|{1}|{2}" -f $relativePath, $_.Length, $hash
                }
        )
    }

    if (Test-Path -LiteralPath $SampleMetaPath -PathType Leaf) {
        if ($LegacyRootMetaHash) {
            $meta = Get-Item -LiteralPath $SampleMetaPath
            $metaHash = (Get-FileHash -LiteralPath $SampleMetaPath -Algorithm SHA256).Hash
            $entries += "R|{0}|{1}" -f $meta.Length, $metaHash
            return @($entries | Sort-Object)
        }

        $metaLines = @(Get-Content -LiteralPath $SampleMetaPath | ForEach-Object { $_.Trim() } | Where-Object { $_ -ne "" })
        $guidLine = @($metaLines | Where-Object { $_ -match '^guid: [0-9a-fA-F]{32}$' })
        $defaultFolderMetaLines = @(
            "fileFormatVersion: 2", "folderAsset: yes", "DefaultImporter:",
            "externalObjects: {}", "userData:", "assetBundleName:", "assetBundleVariant:"
        )
        $nonGuidLines = @($metaLines | Where-Object { $_ -notmatch '^guid: [0-9a-fA-F]{32}$' })
        $isInitialStub = $nonGuidLines.Count -eq 1 -and $nonGuidLines[0] -eq "fileFormatVersion: 2"
        $isExpandedDefault = $nonGuidLines.Count -eq $defaultFolderMetaLines.Count -and
            @(Compare-Object -ReferenceObject $defaultFolderMetaLines -DifferenceObject $nonGuidLines).Count -eq 0
        if ($guidLine.Count -eq 1 -and ($isInitialStub -or $isExpandedDefault)) {
            # Unity may expand the initial two-line folder .meta into its equivalent default
            # importer form between compile and EditMode. Compare that representation by GUID,
            # while any non-default importer/user setting still falls back to an exact hash.
            $entries += "R|default-folder|{0}" -f $guidLine[0].Substring(6).ToLowerInvariant()
        }
        else {
            $meta = Get-Item -LiteralPath $SampleMetaPath
            $metaHash = (Get-FileHash -LiteralPath $SampleMetaPath -Algorithm SHA256).Hash
            $entries += "R|exact|{0}|{1}" -f $meta.Length, $metaHash
        }
    }

    return @($entries | Sort-Object)
}

function Write-GateOwnershipMarker {
    param(
        [Parameter(Mandatory = $true)][string] $SamplePath,
        [Parameter(Mandatory = $true)][string] $SampleMetaPath,
        [Parameter(Mandatory = $true)][string] $MarkerPath
    )

    $marker = [ordered]@{
        schemaVersion = 3
        entries = @(Get-GateContentManifest -Path $SamplePath -SampleMetaPath $SampleMetaPath)
    }
    [System.IO.File]::WriteAllText(
        $MarkerPath,
        ($marker | ConvertTo-Json -Depth 3),
        [System.Text.UTF8Encoding]::new($false))
}

function Remove-GateOwnedSampleIfUnchanged {
    param(
        [Parameter(Mandatory = $true)][string] $SamplePath,
        [Parameter(Mandatory = $true)][string] $SampleMetaPath,
        [Parameter(Mandatory = $true)][string] $MarkerPath
    )

    if (-not (Test-Path -LiteralPath $MarkerPath -PathType Leaf)) {
        throw "Gate ownership marker is missing; preserving the imported sample: $SamplePath"
    }

    try {
        $marker = Get-Content -LiteralPath $MarkerPath -Raw | ConvertFrom-Json
    }
    catch {
        throw "Gate ownership marker is invalid; preserving the imported sample: $MarkerPath"
    }
    $schemaVersion = [int] $marker.schemaVersion
    if ($schemaVersion -ne 2 -and $schemaVersion -ne 3) {
        throw "Gate ownership marker schema is unsupported; preserving the imported sample: $MarkerPath"
    }

    $expected = @($marker.entries | ForEach-Object { [string] $_ })
    $actual = @(Get-GateContentManifest `
        -Path $SamplePath `
        -SampleMetaPath $SampleMetaPath `
        -LegacyRootMetaHash:($schemaVersion -eq 2))
    if (@(Compare-Object -ReferenceObject $expected -DifferenceObject $actual).Count -ne 0) {
        throw "Gate-owned sample content changed; preserving it for manual recovery: $SamplePath"
    }

    Remove-Item -LiteralPath $SamplePath -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $SampleMetaPath, $MarkerPath, ($MarkerPath + ".meta") -Force -ErrorAction SilentlyContinue
}

function Remove-EmptyDirectoryCreatedByGate {
    param(
        [Parameter(Mandatory = $true)][string] $Path,
        [Parameter(Mandatory = $true)][bool] $ExistedBeforeGate
    )

    if ($ExistedBeforeGate -or -not (Test-Path -LiteralPath $Path -PathType Container)) {
        return
    }
    if (@(Get-ChildItem -LiteralPath $Path -Force).Count -ne 0) {
        return
    }

    Remove-Item -LiteralPath $Path -Force
    Remove-Item -LiteralPath ($Path + ".meta") -Force -ErrorAction SilentlyContinue
}

$ProjectPath = Resolve-FullPath $ProjectPath
$PackagePath = Resolve-FullPath $PackagePath
$ArtifactsPath = Resolve-FullPath $ArtifactsPath
$repositoryPath = Resolve-FullPath (Join-Path $PSScriptRoot "..")
$packageJsonPath = Join-Path $PackagePath "package.json"
$sampleSourcePath = Join-Path $PackagePath "Samples~\UnityToonShaderAdapter"

if (-not (Test-Path -LiteralPath $Unity -PathType Leaf)) {
    throw "Unity executable was not found: $Unity"
}
if (-not (Test-Path -LiteralPath $ProjectPath -PathType Container)) {
    throw "Unity project was not found: $ProjectPath"
}
if (-not (Test-Path -LiteralPath $packageJsonPath -PathType Leaf)) {
    throw "Package manifest was not found: $packageJsonPath"
}
if (-not (Test-Path -LiteralPath $sampleSourcePath -PathType Container)) {
    throw "Unity Toon Shader adapter sample was not found: $sampleSourcePath"
}

$package = Get-Content -LiteralPath $packageJsonPath -Raw | ConvertFrom-Json
$packageVersion = [string] $package.version
if ([string]::IsNullOrWhiteSpace($packageVersion)) {
    throw "Package version is missing from: $packageJsonPath"
}

$samplesPath = Join-Path $ProjectPath "Assets\Samples"
$packageSamplesPath = Join-Path $samplesPath "MMD Loader"
$versionSamplesPath = Join-Path $packageSamplesPath $packageVersion
$sampleImportPath = Join-Path $versionSamplesPath "Unity Toon Shader Adapter"
$sampleImportMetaPath = $sampleImportPath + ".meta"
$sampleImportMarkerPath = $sampleImportPath + ".gate-owned"
$compileLog = Join-Path $ArtifactsPath "compile.log"
$testLog = Join-Path $ArtifactsPath "editmode.log"
$testResults = Join-Path $ArtifactsPath "editmode-results.xml"
$visualCanaryPath = Join-Path $repositoryPath "artifacts\visual\uts-adapter-canary.png"
$generatedPmxVisualPath = Join-Path $repositoryPath "artifacts\visual\uts-adapter-generated-pmx"
$compileScript = Join-Path $PSScriptRoot "unity-compile.ps1"

Assert-NoRunningUnityProject -ProjectPath $ProjectPath -OperationName "Unity Toon Shader adapter sample gate"

if ((Test-Path -LiteralPath $sampleImportPath) -or (Test-Path -LiteralPath $sampleImportMetaPath)) {
    if (-not (Test-Path -LiteralPath $sampleImportMarkerPath -PathType Leaf)) {
        throw @"
Unity Toon Shader adapter sample is already imported. The gate will not overwrite or delete a user-owned import.
path=$sampleImportPath
"@
    }

    Remove-GateOwnedSampleIfUnchanged `
        -SamplePath $sampleImportPath `
        -SampleMetaPath $sampleImportMetaPath `
        -MarkerPath $sampleImportMarkerPath
}

$samplesPathExisted = Test-Path -LiteralPath $samplesPath -PathType Container
$packageSamplesPathExisted = Test-Path -LiteralPath $packageSamplesPath -PathType Container
$versionSamplesPathExisted = Test-Path -LiteralPath $versionSamplesPath -PathType Container

New-Item -ItemType Directory -Force -Path $versionSamplesPath, $ArtifactsPath | Out-Null
Remove-Item -LiteralPath $compileLog, $testLog, $testResults, $visualCanaryPath -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath $generatedPmxVisualPath -Recurse -Force -ErrorAction SilentlyContinue

$markerCreated = $false
try {
    Copy-Item -LiteralPath $sampleSourcePath -Destination $sampleImportPath -Recurse
    Write-GateOwnershipMarker `
        -SamplePath $sampleImportPath `
        -SampleMetaPath $sampleImportMetaPath `
        -MarkerPath $sampleImportMarkerPath
    $markerCreated = $true

    & pwsh -NoProfile -File $compileScript `
        -Unity $Unity `
        -ProjectPath $ProjectPath `
        -LogFile $compileLog
    if ($LASTEXITCODE -ne 0) {
        throw "Unity Toon Shader adapter sample compile failed. exitCode=$LASTEXITCODE; log=$compileLog"
    }
    Write-GateOwnershipMarker `
        -SamplePath $sampleImportPath `
        -SampleMetaPath $sampleImportMetaPath `
        -MarkerPath $sampleImportMarkerPath

    Assert-NoRunningUnityProject -ProjectPath $ProjectPath -OperationName "Unity Toon Shader adapter sample tests"

    $testArguments = @(
        "-batchmode",
        "-runTests",
        "-projectPath", $ProjectPath,
        "-testPlatform", "EditMode",
        "-testFilter", "Mmd.Samples.UnityToonShader.Tests",
        "-testResults", $testResults,
        "-logFile", $testLog
    )
    $testStartInfo = [System.Diagnostics.ProcessStartInfo]::new()
    $testStartInfo.FileName = $Unity
    $testStartInfo.UseShellExecute = $false
    $testStartInfo.CreateNoWindow = $true
    foreach ($argument in $testArguments) {
        [void] $testStartInfo.ArgumentList.Add($argument)
    }
    $testProcess = [System.Diagnostics.Process]::Start($testStartInfo)
    $testProcess.WaitForExit()
    $unityExitCode = $testProcess.ExitCode

    try {
        $testSummary = Read-NUnitTestRunSummary -Path $testResults -Context "Unity Toon Shader adapter sample tests"
    }
    catch {
        throw "Unity Toon Shader adapter sample tests failed. exitCode=$unityExitCode; results=$testResults; log=$testLog; $($_.Exception.Message)"
    }

    if ($unityExitCode -ne 0 -or $testSummary.Total -lt 6 -or $testSummary.Failed -gt 0 -or $testSummary.Skipped -gt 0 -or $testSummary.HasFailedResult) {
        throw ("Unity Toon Shader adapter sample tests failed. exitCode={0}; result={1}; total={2}; passed={3}; failed={4}; skipped={5}; results={6}; log={7}" -f `
            $unityExitCode, $testSummary.Result, $testSummary.Total, $testSummary.Passed, $testSummary.Failed, $testSummary.Skipped, $testResults, $testLog)
    }
    if (-not (Test-Path -LiteralPath $visualCanaryPath -PathType Leaf) -or (Get-Item -LiteralPath $visualCanaryPath).Length -eq 0) {
        throw "Unity Toon Shader adapter visual canary PNG was not generated: $visualCanaryPath"
    }

    $generatedPmxCaptures = @(Get-ChildItem -LiteralPath $generatedPmxVisualPath -File -ErrorAction SilentlyContinue |
        Where-Object Name -Match '-(legacy|uts)\.png$')
    if ($generatedPmxCaptures.Count -ne 6) {
        throw "Generated PMX UTS visual evidence must contain exactly 6 Legacy/UTS PNGs: $generatedPmxVisualPath"
    }
    foreach ($capture in $generatedPmxCaptures) {
        if ($capture.Length -eq 0) {
            throw "Generated PMX UTS visual evidence is empty: $($capture.FullName)"
        }
    }

    Write-Host ("Unity Toon Shader adapter sample gate passed. total={0}; passed={1}; skipped={2}; results={3}; png={4}; generatedPmx={5}; log={6}" -f `
        $testSummary.Total, $testSummary.Passed, $testSummary.Skipped, $testResults, $visualCanaryPath, $generatedPmxVisualPath, $testLog)
}
finally {
    Assert-NoRunningUnityProject -ProjectPath $ProjectPath -OperationName "Unity Toon Shader adapter sample cleanup"
    if ($markerCreated -and (Test-Path -LiteralPath $sampleImportMarkerPath -PathType Leaf)) {
        Remove-GateOwnedSampleIfUnchanged `
            -SamplePath $sampleImportPath `
            -SampleMetaPath $sampleImportMetaPath `
            -MarkerPath $sampleImportMarkerPath
    }
    Remove-EmptyDirectoryCreatedByGate -Path $versionSamplesPath -ExistedBeforeGate $versionSamplesPathExisted
    Remove-EmptyDirectoryCreatedByGate -Path $packageSamplesPath -ExistedBeforeGate $packageSamplesPathExisted
    Remove-EmptyDirectoryCreatedByGate -Path $samplesPath -ExistedBeforeGate $samplesPathExisted
}
