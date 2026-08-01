param(
    [string] $Unity = "C:\Program Files\Unity\Hub\Editor\6000.4.8f1\Editor\Unity.exe",
    [string] $ProjectPath = "F:\Develop\MMDDev\unity-mmd-loader\unity-mmd",
    [string] $ResultsFile = "F:\Develop\MMDDev\unity-mmd-loader\artifacts\vmd-import-timeline-performance-results.xml",
    [string] $LogFile = "F:\Develop\MMDDev\unity-mmd-loader\artifacts\vmd-import-timeline-performance.log",
    [int] $MeasurementCount = 20,
    [int] $KeyframeCount = 300000
)

$ErrorActionPreference = "Stop"

. (Join-Path $PSScriptRoot "unity-process-environment.ps1")
. (Join-Path $PSScriptRoot "unity-project-guard.ps1")
. (Join-Path $PSScriptRoot "read-nunit-test-result.ps1")
Initialize-UnityProcessEnvironment

if ($MeasurementCount -lt 20) {
    throw "VMD import/Timeline performance gate failed. MeasurementCount must be at least 20 for a meaningful p95: $MeasurementCount"
}
if ($KeyframeCount -le 0) {
    throw "VMD import/Timeline performance gate failed. KeyframeCount must be positive: $KeyframeCount"
}

$ProjectPath = [System.IO.Path]::GetFullPath($ProjectPath)
$ResultsFile = [System.IO.Path]::GetFullPath($ResultsFile)
$LogFile = [System.IO.Path]::GetFullPath($LogFile)
if (-not (Test-Path -LiteralPath $Unity -PathType Leaf)) {
    throw "VMD import/Timeline performance gate failed. Unity editor was not found: $Unity"
}
if (-not (Test-Path -LiteralPath $ProjectPath -PathType Container)) {
    throw "VMD import/Timeline performance gate failed. Unity project was not found: $ProjectPath"
}
Assert-NoRunningUnityProject -ProjectPath $ProjectPath -OperationName "VMD import/Timeline performance gate"
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $ResultsFile) | Out-Null
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $LogFile) | Out-Null
Remove-Item -LiteralPath $ResultsFile, $LogFile -Force -ErrorAction SilentlyContinue

$previousGate = $env:MMD_VMD_PERF_GATE
$previousIterations = $env:MMD_VMD_TIMELINE_PERF_ITERATIONS
$previousKeyframes = $env:MMD_VMD_TIMELINE_PERF_KEYFRAMES
$env:MMD_VMD_PERF_GATE = "1"
$env:MMD_VMD_TIMELINE_PERF_ITERATIONS = [string]$MeasurementCount
$env:MMD_VMD_TIMELINE_PERF_KEYFRAMES = [string]$KeyframeCount

try {
    & (Join-Path $PSScriptRoot "unity-editmode-tests.ps1") `
        -Unity $Unity `
        -ProjectPath $ProjectPath `
        -ResultsFile $ResultsFile `
        -LogFile $LogFile `
        -Filter "Mmd.Tests.MmdVmdImportTimelinePerformanceTests"
}
finally {
    if ($null -eq $previousGate) {
        Remove-Item Env:MMD_VMD_PERF_GATE -ErrorAction SilentlyContinue
    }
    else {
        $env:MMD_VMD_PERF_GATE = $previousGate
    }
    if ($null -eq $previousIterations) {
        Remove-Item Env:MMD_VMD_TIMELINE_PERF_ITERATIONS -ErrorAction SilentlyContinue
    }
    else {
        $env:MMD_VMD_TIMELINE_PERF_ITERATIONS = $previousIterations
    }
    if ($null -eq $previousKeyframes) {
        Remove-Item Env:MMD_VMD_TIMELINE_PERF_KEYFRAMES -ErrorAction SilentlyContinue
    }
    else {
        $env:MMD_VMD_TIMELINE_PERF_KEYFRAMES = $previousKeyframes
    }
}

$summary = Read-NUnitTestRunSummary -Path $ResultsFile -Context "VMD import/Timeline performance gate"
$resultsXml = [xml](Get-Content -LiteralPath $ResultsFile -Raw)
$performanceCase = $resultsXml.SelectSingleNode(
    "//test-case[@fullname='Mmd.Tests.MmdVmdImportTimelinePerformanceTests.GeneratedVmdImportTimelineFirstEvaluateHasP95Under100Milliseconds']")
$inconclusiveCase = $resultsXml.SelectSingleNode("//test-case[@result='Inconclusive']")
$skippedCase = $resultsXml.SelectSingleNode("//test-case[@result='Skipped']")
if ($summary.Total -le 0 -or
    $summary.Result -ne "Passed" -or
    $summary.Failed -gt 0 -or
    $summary.Skipped -gt 0 -or
    $summary.HasFailedResult -or
    $null -eq $performanceCase -or
    $performanceCase.GetAttribute("result") -ne "Passed" -or
    $null -ne $inconclusiveCase -or
    $null -ne $skippedCase) {
    throw (("VMD import/Timeline performance gate failed. result={0}; failed={1}; passed={2}; skipped={3}; " +
        "inconclusive={4}; results={5}; log={6}") -f `
        $summary.Result, $summary.Failed, $summary.Passed, $summary.Skipped,
        ($null -ne $inconclusiveCase), $ResultsFile, $LogFile)
}

Write-Host (("VMD import/Timeline performance gate passed. result={0}; passed={1}; skipped={2}; total={3}; " +
    "results={4}; log={5}") -f `
    $summary.Result, $summary.Passed, $summary.Skipped, $summary.Total, $ResultsFile, $LogFile)
