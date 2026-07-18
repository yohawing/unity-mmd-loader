param(
    [string] $Unity = "C:\Program Files\Unity\Hub\Editor\6000.4.8f1\Editor\Unity.exe",
    [string] $ProjectPath = "F:\Develop\MMDDev\unity-mmd-loader\unity-mmd",
    [string] $PackagePath = "F:\Develop\MMDDev\unity-mmd-loader\packages\com.yohawing.mmd-loader",
    [string] $ArtifactsPath = "F:\Develop\MMDDev\unity-mmd-loader\artifacts\unity-toon-shader-sample"
)

$ErrorActionPreference = "Stop"

. (Join-Path $PSScriptRoot "unity-project-guard.ps1")

function Resolve-FullPath {
    param([Parameter(Mandatory = $true)][string] $Path)

    if ([System.IO.Path]::IsPathRooted($Path)) {
        return [System.IO.Path]::GetFullPath($Path)
    }

    return [System.IO.Path]::GetFullPath((Join-Path (Get-Location) $Path))
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
$compileScript = Join-Path $PSScriptRoot "unity-compile.ps1"

Assert-NoRunningUnityProject -ProjectPath $ProjectPath -OperationName "Unity Toon Shader adapter sample gate"

if ((Test-Path -LiteralPath $sampleImportPath) -or (Test-Path -LiteralPath $sampleImportMetaPath)) {
    if (-not (Test-Path -LiteralPath $sampleImportMarkerPath -PathType Leaf)) {
        throw @"
Unity Toon Shader adapter sample is already imported. The gate will not overwrite or delete a user-owned import.
path=$sampleImportPath
"@
    }

    Remove-Item -LiteralPath $sampleImportPath -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $sampleImportMetaPath, $sampleImportMarkerPath, ($sampleImportMarkerPath + ".meta") -Force -ErrorAction SilentlyContinue
}

$samplesPathExisted = Test-Path -LiteralPath $samplesPath -PathType Container
$packageSamplesPathExisted = Test-Path -LiteralPath $packageSamplesPath -PathType Container
$versionSamplesPathExisted = Test-Path -LiteralPath $versionSamplesPath -PathType Container

New-Item -ItemType Directory -Force -Path $versionSamplesPath, $ArtifactsPath | Out-Null
Remove-Item -LiteralPath $compileLog, $testLog, $testResults, $visualCanaryPath -Force -ErrorAction SilentlyContinue

try {
    [System.IO.File]::WriteAllText($sampleImportMarkerPath, "temporary import owned by verify-unity-toon-shader-sample.ps1")
    Copy-Item -LiteralPath $sampleSourcePath -Destination $sampleImportPath -Recurse

    & pwsh -NoProfile -File $compileScript `
        -Unity $Unity `
        -ProjectPath $ProjectPath `
        -LogFile $compileLog
    if ($LASTEXITCODE -ne 0) {
        throw "Unity Toon Shader adapter sample compile failed. exitCode=$LASTEXITCODE; log=$compileLog"
    }

    Assert-NoRunningUnityProject -ProjectPath $ProjectPath -OperationName "Unity Toon Shader adapter sample tests"

    & $Unity -batchmode -runTests `
        -projectPath $ProjectPath `
        -testPlatform EditMode `
        -testFilter "Mmd.Samples.UnityToonShader.Tests" `
        -testResults $testResults `
        -logFile $testLog
    $unityExitCode = if ($null -eq $LASTEXITCODE) { 0 } else { $LASTEXITCODE }

    if (-not (Test-Path -LiteralPath $testResults -PathType Leaf)) {
        throw "Unity Toon Shader adapter sample tests produced no results. exitCode=$unityExitCode; log=$testLog"
    }

    [xml] $resultsXml = Get-Content -LiteralPath $testResults -Raw
    $testRun = $resultsXml.SelectSingleNode("//test-run")
    if ($null -eq $testRun) {
        throw "Unity Toon Shader adapter sample results have no <test-run> root: $testResults"
    }

    $totalCount = 0
    $failedCount = 0
    $skippedCount = 0
    [void][int]::TryParse([string] $testRun.GetAttribute("total"), [ref] $totalCount)
    [void][int]::TryParse([string] $testRun.GetAttribute("failed"), [ref] $failedCount)
    [void][int]::TryParse([string] $testRun.GetAttribute("skipped"), [ref] $skippedCount)
    $runResult = [string] $testRun.GetAttribute("result")
    if ($unityExitCode -ne 0 -or $totalCount -lt 6 -or $failedCount -gt 0 -or $skippedCount -gt 0 -or $runResult -eq "Failed") {
        throw ("Unity Toon Shader adapter sample tests failed. exitCode={0}; result={1}; total={2}; passed={3}; failed={4}; skipped={5}; results={6}; log={7}" -f `
            $unityExitCode, $runResult, $totalCount, $testRun.GetAttribute("passed"), $failedCount, $testRun.GetAttribute("skipped"), $testResults, $testLog)
    }
    if (-not (Test-Path -LiteralPath $visualCanaryPath -PathType Leaf) -or (Get-Item -LiteralPath $visualCanaryPath).Length -eq 0) {
        throw "Unity Toon Shader adapter visual canary PNG was not generated: $visualCanaryPath"
    }

    Write-Host ("Unity Toon Shader adapter sample gate passed. total={0}; passed={1}; skipped={2}; results={3}; png={4}; log={5}" -f `
        $totalCount, $testRun.GetAttribute("passed"), $testRun.GetAttribute("skipped"), $testResults, $visualCanaryPath, $testLog)
}
finally {
    Assert-NoRunningUnityProject -ProjectPath $ProjectPath -OperationName "Unity Toon Shader adapter sample cleanup"
    Remove-Item -LiteralPath $sampleImportPath -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $sampleImportMetaPath, $sampleImportMarkerPath, ($sampleImportMarkerPath + ".meta") -Force -ErrorAction SilentlyContinue
    Remove-EmptyDirectoryCreatedByGate -Path $versionSamplesPath -ExistedBeforeGate $versionSamplesPathExisted
    Remove-EmptyDirectoryCreatedByGate -Path $packageSamplesPath -ExistedBeforeGate $packageSamplesPathExisted
    Remove-EmptyDirectoryCreatedByGate -Path $samplesPath -ExistedBeforeGate $samplesPathExisted
}
