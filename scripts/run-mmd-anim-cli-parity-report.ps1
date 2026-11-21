param(
    [string] $Unity = "",
    [string] $ProjectPath = "F:\Develop\MMDDev\unity-mmd-loader\unity-mmd",
    [string] $ResultsFile = "F:\Develop\MMDDev\unity-mmd-loader\artifacts\parity\mmd-anim-cli-parity-results.xml",
    [string] $LogFile = "F:\Develop\MMDDev\unity-mmd-loader\artifacts\parity\mmd-anim-cli-parity.log"
)

$ErrorActionPreference = "Stop"

. (Join-Path $PSScriptRoot "unity-project-guard.ps1")
. (Join-Path $PSScriptRoot "unity-batch-common.ps1")
. (Join-Path $PSScriptRoot "read-nunit-test-result.ps1")

$Unity = Resolve-UnityExecutable $Unity
$ResultsFile = Resolve-OutputPath $ResultsFile
$LogFile = Resolve-OutputPath $LogFile

Assert-NoRunningUnityProject -ProjectPath $ProjectPath -OperationName "mmd-anim CLI parity report"

New-Item -ItemType Directory -Force -Path (Split-Path -Parent $ResultsFile) | Out-Null
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $LogFile) | Out-Null
Remove-Item -LiteralPath $ResultsFile, $LogFile -Force -ErrorAction SilentlyContinue

$previousParityFlag = $env:YMU_MMD_ANIM_CLI_PARITY
$env:YMU_MMD_ANIM_CLI_PARITY = "1"
try {
    Clear-StaleUnityProjectLocks -ProjectPath $ProjectPath
    $unityExitCode = Invoke-UnityBatch -Unity $Unity -Arguments @(
        "-batchmode",
        "-runTests",
        "-projectPath", $ProjectPath,
        "-testPlatform", "EditMode",
        "-testFilter", "Mmd.Tests.Contracts.MmdNativeParityTests.MmdAnimCliParityReportComparesCliWithPackagedNativeRuntime",
        "-testResults", $ResultsFile,
        "-logFile", $LogFile
    )
}
finally {
    if ($null -eq $previousParityFlag) {
        Remove-Item Env:\YMU_MMD_ANIM_CLI_PARITY -ErrorAction SilentlyContinue
    }
    else {
        $env:YMU_MMD_ANIM_CLI_PARITY = $previousParityFlag
    }
}

Wait-ForFile -Path $LogFile | Out-Null
Wait-ForFile -Path $ResultsFile | Out-Null
Wait-ForFileToSettle -Path $LogFile | Out-Null
Wait-ForFileToSettle -Path $ResultsFile | Out-Null

if (-not (Test-Path -LiteralPath $LogFile)) {
    throw ("mmd-anim CLI parity report failed. exitCode={0}; log was not created: {1}" -f $unityExitCode, $LogFile)
}

try {
    $testSummary = Read-NUnitTestRunSummary -Path $ResultsFile -Context "mmd-anim CLI parity report"
}
catch {
    throw "mmd-anim CLI parity report failed. exitCode=$unityExitCode; results=$ResultsFile; log=$LogFile; $($_.Exception.Message)"
}

if ($unityExitCode -ne 0 -or $testSummary.Failed -gt 0 -or $testSummary.HasFailedResult) {
    throw ("mmd-anim CLI parity report failed. exitCode={0}; result={1}; failed={2}; passed={3}; skipped={4}; total={5}; results={6}; log={7}" -f `
        $unityExitCode, $testSummary.Result, $testSummary.Failed, $testSummary.Passed, $testSummary.Skipped, $testSummary.Total, $ResultsFile, $LogFile)
}

$reportPath = Join-Path (Split-Path -Parent $ResultsFile) "mmd-anim-cli-parity-report.json"
if (-not (Test-Path -LiteralPath $reportPath)) {
    throw ("mmd-anim CLI parity report test passed but report was not created: {0}" -f $reportPath)
}

Write-Host ("mmd-anim CLI parity report passed. report={0}; results={1}; log={2}" -f $reportPath, $ResultsFile, $LogFile)
