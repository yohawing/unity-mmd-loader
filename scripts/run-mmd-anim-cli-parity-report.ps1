param(
    [string] $Unity = "C:\Program Files\Unity\Hub\Editor\6000.4.8f1\Editor\Unity.exe",
    [string] $ProjectPath = "F:\Develop\MMDDev\unity-mmd-loader\unity-mmd",
    [string] $ResultsFile = "F:\Develop\MMDDev\unity-mmd-loader\artifacts\parity\mmd-anim-cli-parity-results.xml",
    [string] $LogFile = "F:\Develop\MMDDev\unity-mmd-loader\artifacts\parity\mmd-anim-cli-parity.log"
)

$ErrorActionPreference = "Stop"

. (Join-Path $PSScriptRoot "unity-project-guard.ps1")

function Resolve-OutputPath {
    param([Parameter(Mandatory = $true)][string] $Path)

    if ([System.IO.Path]::IsPathRooted($Path)) {
        return [System.IO.Path]::GetFullPath($Path)
    }

    return [System.IO.Path]::GetFullPath((Join-Path (Get-Location) $Path))
}

$ResultsFile = Resolve-OutputPath $ResultsFile
$LogFile = Resolve-OutputPath $LogFile

Assert-NoRunningUnityProject -ProjectPath $ProjectPath -OperationName "mmd-anim CLI parity report"

New-Item -ItemType Directory -Force -Path (Split-Path -Parent $ResultsFile) | Out-Null
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $LogFile) | Out-Null
Remove-Item -LiteralPath $ResultsFile, $LogFile -Force -ErrorAction SilentlyContinue

$previousParityFlag = $env:YMU_MMD_ANIM_CLI_PARITY
$env:YMU_MMD_ANIM_CLI_PARITY = "1"
try {
    & $Unity -batchmode -runTests `
        -projectPath $ProjectPath `
        -testPlatform EditMode `
        -testFilter "Mmd.Tests.Contracts.MmdNativeParityTests.MmdAnimCliParityReportComparesCliWithPackagedNativeRuntime" `
        -testResults $ResultsFile `
        -logFile $LogFile
    $unityExitCode = if ($null -eq $LASTEXITCODE) { 0 } else { $LASTEXITCODE }
}
finally {
    if ($null -eq $previousParityFlag) {
        Remove-Item Env:\YMU_MMD_ANIM_CLI_PARITY -ErrorAction SilentlyContinue
    }
    else {
        $env:YMU_MMD_ANIM_CLI_PARITY = $previousParityFlag
    }
}

function Wait-ForFile {
    param(
        [Parameter(Mandatory = $true)][string] $Path,
        [int] $TimeoutSeconds = 120
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        if (Test-Path -LiteralPath $Path) {
            return
        }
        Start-Sleep -Milliseconds 250
    }
}

function Wait-ForFileToSettle {
    param(
        [Parameter(Mandatory = $true)][string] $Path,
        [int] $TimeoutSeconds = 30
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastLength = -1
    $lastWriteTimeUtc = [datetime]::MinValue
    $stableCount = 0
    while ((Get-Date) -lt $deadline) {
        $item = Get-Item -LiteralPath $Path -ErrorAction SilentlyContinue
        if ($null -eq $item) {
            Start-Sleep -Milliseconds 250
            continue
        }

        if ($item.Length -eq $lastLength -and $item.LastWriteTimeUtc -eq $lastWriteTimeUtc) {
            $stableCount++
            if ($stableCount -ge 4) {
                return
            }
        }
        else {
            $stableCount = 0
            $lastLength = $item.Length
            $lastWriteTimeUtc = $item.LastWriteTimeUtc
        }

        Start-Sleep -Milliseconds 250
    }
}

Wait-ForFile -Path $LogFile
Wait-ForFile -Path $ResultsFile
Wait-ForFileToSettle -Path $LogFile
Wait-ForFileToSettle -Path $ResultsFile

if (-not (Test-Path -LiteralPath $LogFile)) {
    throw ("mmd-anim CLI parity report failed. exitCode={0}; log was not created: {1}" -f $unityExitCode, $LogFile)
}

if (-not (Test-Path -LiteralPath $ResultsFile)) {
    throw ("mmd-anim CLI parity report failed. exitCode={0}; results file was not created: {1}; log={2}" -f $unityExitCode, $ResultsFile, $LogFile)
}

[xml] $resultsXml = Get-Content -LiteralPath $ResultsFile -Raw
$testRun = $resultsXml.SelectSingleNode("//test-run")
if ($null -eq $testRun) {
    throw ("mmd-anim CLI parity report failed. results XML has no <test-run> root: {0}; log={1}" -f $ResultsFile, $LogFile)
}

$failedCount = 0
[void][int]::TryParse([string] $testRun.GetAttribute("failed"), [ref] $failedCount)
$runResult = [string] $testRun.GetAttribute("result")
if ($unityExitCode -ne 0 -or $failedCount -gt 0 -or $runResult -eq "Failed") {
    throw ("mmd-anim CLI parity report failed. exitCode={0}; result={1}; failed={2}; passed={3}; skipped={4}; total={5}; results={6}; log={7}" -f `
        $unityExitCode, $runResult, $testRun.GetAttribute("failed"), $testRun.GetAttribute("passed"), $testRun.GetAttribute("skipped"), $testRun.GetAttribute("total"), $ResultsFile, $LogFile)
}

$reportPath = Join-Path (Split-Path -Parent $ResultsFile) "mmd-anim-cli-parity-report.json"
if (-not (Test-Path -LiteralPath $reportPath)) {
    throw ("mmd-anim CLI parity report test passed but report was not created: {0}" -f $reportPath)
}

Write-Host ("mmd-anim CLI parity report passed. report={0}; results={1}; log={2}" -f $reportPath, $ResultsFile, $LogFile)
