param(
    [string] $Unity = "C:\Program Files\Unity\Hub\Editor\6000.4.8f1\Editor\Unity.exe",
    [string] $ArtifactsRoot = "",
    [string] $ProjectPath = "",
    [switch] $SkipPerturbationProof
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrEmpty($ArtifactsRoot)) {
    $ArtifactsRoot = Join-Path $repoRoot "artifacts\visual-shading-tier"
}
$runId = Get-Date -Format "yyyyMMdd-HHmmss"
$runRoot = Join-Path $ArtifactsRoot $runId
$resultsRoot = Join-Path $runRoot "results"
$captureRoot = Join-Path $runRoot "captures"
New-Item -ItemType Directory -Force -Path $runRoot, $resultsRoot, $captureRoot | Out-Null

if (-not (Test-Path -LiteralPath $Unity)) {
    throw "Unity executable not found: $Unity"
}

$localConsumerProject = Join-Path $repoRoot "unity-mmd"
if ([string]::IsNullOrEmpty($ProjectPath) -and (Test-Path -LiteralPath (Join-Path $localConsumerProject "Packages\manifest.json"))) {
    $ProjectPath = $localConsumerProject
}
elseif ([string]::IsNullOrEmpty($ProjectPath)) {
    $ProjectPath = Join-Path $runRoot "project"
}
$ProjectPath = [IO.Path]::GetFullPath($ProjectPath)

if (-not (Test-Path -LiteralPath (Join-Path $ProjectPath "Packages\manifest.json"))) {
    $bootstrapLog = Join-Path $resultsRoot "bootstrap.log"
    & $Unity -batchmode -quit -createProject $ProjectPath -logFile $bootstrapLog
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath (Join-Path $ProjectPath "Packages\manifest.json"))) {
        throw "Unity project bootstrap failed. log=$bootstrapLog"
    }

    $projectPackagesPath = Join-Path $ProjectPath "Packages"
    $loaderPath = [IO.Path]::GetRelativePath($projectPackagesPath, (Join-Path $repoRoot "packages\com.yohawing.mmd-loader")).Replace('\', '/')
    $devToolsPath = [IO.Path]::GetRelativePath($projectPackagesPath, (Join-Path $repoRoot "packages\com.yohawing.mmd-loader.devtools")).Replace('\', '/')
    $manifest = [ordered]@{
        dependencies = [ordered]@{
            "com.unity.test-framework" = "1.6.0"
            "com.yohawing.mmd-loader" = "file:$loaderPath"
            "com.yohawing.mmd-loader.devtools" = "file:$devToolsPath"
        }
        testables = @("com.yohawing.mmd-loader.devtools")
    }
    $manifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $ProjectPath "Packages\manifest.json") -Encoding utf8
}

$testName = "Mmd.Tests.MmdGeneratedPmxVisualParityTests.ToonRampOpaqueOutline_IsDeterministicAndMatchesGolden"
function Invoke-VisualTierRun {
    param(
        [Parameter(Mandatory = $true)][string] $Name,
        [Parameter(Mandatory = $true)][bool] $Perturb,
        [Parameter(Mandatory = $true)][bool] $ExpectFailure
    )
    $results = Join-Path $resultsRoot "$Name.xml"
    $log = Join-Path $resultsRoot "$Name.log"
    $env:YMU_VISUAL_PARITY_ARTIFACTS = Join-Path $captureRoot $Name
    $env:YMU_VISUAL_TIER_PERTURB = if ($Perturb) { "1" } else { "0" }
    Remove-Item Env:YMU_VISUAL_TIER_OPT_OUT -ErrorAction SilentlyContinue
    & $Unity -batchmode -quit -runTests -projectPath $ProjectPath -testPlatform EditMode `
        -testFilter $testName -testResults $results -logFile $log
    if (-not (Test-Path -LiteralPath $results)) {
        throw "$Name did not produce test results. log=$log"
    }
    [xml] $xml = Get-Content -LiteralPath $results -Raw
    $testRun = $xml.SelectSingleNode("//test-run")
    if ($null -eq $testRun) {
        throw "$Name results have no test-run root. results=$results"
    }
    $failed = [int]$testRun.GetAttribute("failed")
    if ($ExpectFailure -and $failed -eq 0) {
        throw "$Name was expected to fail after shader-output perturbation, but stayed green."
    }
    if (-not $ExpectFailure -and $failed -ne 0) {
        throw "$Name failed. results=$results log=$log"
    }
}

try {
    Invoke-VisualTierRun -Name "green-before" -Perturb $false -ExpectFailure $false
    if (-not $SkipPerturbationProof) {
        Invoke-VisualTierRun -Name "red-perturbed" -Perturb $true -ExpectFailure $true
        Invoke-VisualTierRun -Name "green-after" -Perturb $false -ExpectFailure $false
    }
}
finally {
    Remove-Item Env:YMU_VISUAL_TIER_PERTURB -ErrorAction SilentlyContinue
    Remove-Item Env:YMU_VISUAL_PARITY_ARTIFACTS -ErrorAction SilentlyContinue
}

Write-Host "Visual shading tier passed. artifacts=$runRoot"
