[CmdletBinding()]
param(
    [string] $EditorVersion = "6000.0.80f1",
    [string] $Unity = "C:\Program Files\Unity\Hub\Editor\6000.0.80f1\Editor\Unity.exe",
    [string] $UnityCli = (Join-Path $env:LOCALAPPDATA "Unity\bin\unity.exe"),
    [string] $ProjectPath = "",
    [string] $Filter = "Mmd.Tests.MmdSelfShadowTargetTests;Mmd.Tests.MmdHumanoidProxyRigFactoryTests;Mmd.Tests.MmdAssetImporterTests"
)

$ErrorActionPreference = "Stop"

. (Join-Path $PSScriptRoot "unity-process-environment.ps1")
Initialize-UnityProcessEnvironment
. (Join-Path $PSScriptRoot "unity-project-guard.ps1")
. (Join-Path $PSScriptRoot "read-nunit-test-result.ps1")

$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$packageRoot = Join-Path $repoRoot "packages\com.yohawing.mmd-loader"
if ([string]::IsNullOrWhiteSpace($ProjectPath)) {
    $ProjectPath = Join-Path $repoRoot ("artifacts\compat-unity-lts\consumer-{0}" -f $EditorVersion)
}
$ProjectPath = [System.IO.Path]::GetFullPath($ProjectPath)

if (-not (Test-Path -LiteralPath $Unity -PathType Leaf)) {
    throw "Unity LTS compatibility gate failed. Editor not found: $Unity"
}
if (-not (Test-Path -LiteralPath (Join-Path $packageRoot "package.json") -PathType Leaf)) {
    throw "Unity LTS compatibility gate failed. Package not found: $packageRoot"
}

if (-not (Test-Path -LiteralPath $ProjectPath -PathType Container)) {
    if (-not (Test-Path -LiteralPath $UnityCli -PathType Leaf)) {
        throw "Unity LTS compatibility gate failed. Unity CLI not found: $UnityCli"
    }

    $projectParent = Split-Path -Parent $ProjectPath
    $projectName = Split-Path -Leaf $ProjectPath
    New-Item -ItemType Directory -Path $projectParent -Force | Out-Null

    & $UnityCli projects create $projectName `
        --path $projectParent `
        --editor-version $EditorVersion `
        --template com.unity.template.urp-blank `
        --non-interactive `
        --json
    if ($LASTEXITCODE -ne 0) {
        throw "Unity LTS compatibility gate failed while creating the clean consumer project. exitCode=$LASTEXITCODE"
    }
}

$projectVersionPath = Join-Path $ProjectPath "ProjectSettings\ProjectVersion.txt"
if (-not (Test-Path -LiteralPath $projectVersionPath -PathType Leaf)) {
    throw "Unity LTS compatibility gate failed. ProjectVersion.txt is missing: $projectVersionPath"
}
$projectVersionText = Get-Content -LiteralPath $projectVersionPath -Raw
if ($projectVersionText -notmatch ("m_EditorVersion:\s*" + [regex]::Escape($EditorVersion))) {
    throw "Unity LTS compatibility gate refused a project created by another Editor. expected=$EditorVersion; file=$projectVersionPath"
}

Assert-NoRunningUnityProject -ProjectPath $ProjectPath -OperationName "Unity LTS compatibility gate"

$manifestPath = Join-Path $ProjectPath "Packages\manifest.json"
$lockPath = Join-Path $ProjectPath "Packages\packages-lock.json"
$manifestOriginalBytes = [System.IO.File]::ReadAllBytes($manifestPath)
$lockExisted = Test-Path -LiteralPath $lockPath -PathType Leaf
$lockOriginalBytes = if ($lockExisted) { [System.IO.File]::ReadAllBytes($lockPath) } else { $null }
$artifactRoot = Join-Path $repoRoot "artifacts\compat-unity-lts"
New-Item -ItemType Directory -Path $artifactRoot -Force | Out-Null
$testLog = Join-Path $artifactRoot ("editmode-{0}.log" -f $EditorVersion)
$testResults = Join-Path $artifactRoot ("editmode-{0}.xml" -f $EditorVersion)
$testSummary = $null

try {
    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    $localReference = "file:" + ($packageRoot -replace "\\", "/")
    $packageDependency = $manifest.dependencies.PSObject.Properties["com.yohawing.mmd-loader"]
    if ($null -eq $packageDependency) {
        $manifest.dependencies | Add-Member -NotePropertyName "com.yohawing.mmd-loader" -NotePropertyValue $localReference
    }
    else {
        $packageDependency.Value = $localReference
    }

    $testablesProperty = $manifest.PSObject.Properties["testables"]
    $testables = if ($null -eq $testablesProperty) { @() } else { @($testablesProperty.Value) }
    if ($testables -notcontains "com.yohawing.mmd-loader") {
        $testables += "com.yohawing.mmd-loader"
    }
    if ($null -eq $testablesProperty) {
        $manifest | Add-Member -NotePropertyName "testables" -NotePropertyValue $testables
    }
    else {
        $manifest.testables = $testables
    }
    $manifestJson = $manifest | ConvertTo-Json -Depth 100
    $utf8WithoutBom = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($manifestPath, $manifestJson, $utf8WithoutBom)

    Remove-Item -LiteralPath $testLog, $testResults -Force -ErrorAction SilentlyContinue
    $testArguments = @(
        "-batchmode",
        "-runTests",
        "-projectPath", $ProjectPath,
        "-testPlatform", "EditMode",
        "-testResults", $testResults,
        "-logFile", $testLog,
        "-testFilter", $Filter
    )
    $unityProcess = Start-Process `
        -FilePath $Unity `
        -ArgumentList $testArguments `
        -WindowStyle Hidden `
        -Wait `
        -PassThru
    $testExitCode = $unityProcess.ExitCode

    try {
        $testSummary = Read-NUnitTestRunSummary -Path $testResults -Context "Unity LTS compatibility gate"
    }
    catch {
        throw "Unity LTS compatibility gate failed. exitCode=$testExitCode; results=$testResults; log=$testLog; $($_.Exception.Message)"
    }

    if ($testExitCode -ne 0 -or $testSummary.Total -le 0 -or $testSummary.Failed -gt 0 -or $testSummary.HasFailedResult) {
        throw ("Unity LTS compatibility gate failed. exitCode={0}; result={1}; failed={2}; passed={3}; skipped={4}; total={5}; results={6}; log={7}" -f `
            $testExitCode, $testSummary.Result, $testSummary.Failed, $testSummary.Passed, $testSummary.Skipped, $testSummary.Total, $testResults, $testLog)
    }

}
finally {
    [System.IO.File]::WriteAllBytes($manifestPath, $manifestOriginalBytes)
    if ($lockExisted) {
        [System.IO.File]::WriteAllBytes($lockPath, $lockOriginalBytes)
    }
    else {
        Remove-Item -LiteralPath $lockPath -Force -ErrorAction SilentlyContinue
    }
}

Write-Host ("Unity LTS compatibility gate passed. editor={0}; result={1}; passed={2}; skipped={3}; total={4}; project={5}; results={6}" -f `
    $EditorVersion, $testSummary.Result, $testSummary.Passed, $testSummary.Skipped, $testSummary.Total, $ProjectPath, $testResults)
