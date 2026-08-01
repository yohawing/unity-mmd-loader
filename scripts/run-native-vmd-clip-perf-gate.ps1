param(
    [string] $Unity = "C:\Program Files\Unity\Hub\Editor\6000.4.8f1\Editor\Unity.exe",
    [string] $ProjectPath = "F:\Develop\MMDDev\unity-mmd-loader\unity-mmd",
    [string] $ResultsFile = "F:\Develop\MMDDev\unity-mmd-loader\artifacts\native-vmd-clip-performance-results.xml",
    [string] $LogFile = "F:\Develop\MMDDev\unity-mmd-loader\artifacts\native-vmd-clip-performance.log"
)

$ErrorActionPreference = "Stop"

. (Join-Path $PSScriptRoot "unity-process-environment.ps1")
. (Join-Path $PSScriptRoot "unity-project-guard.ps1")
. (Join-Path $PSScriptRoot "read-nunit-test-result.ps1")
Initialize-UnityProcessEnvironment

$ProjectPath = [System.IO.Path]::GetFullPath($ProjectPath)
$ResultsFile = [System.IO.Path]::GetFullPath($ResultsFile)
$LogFile = [System.IO.Path]::GetFullPath($LogFile)
if (-not (Test-Path -LiteralPath $Unity -PathType Leaf)) {
    throw "Native VMD clip performance gate failed. Unity editor was not found: $Unity"
}
if (-not (Test-Path -LiteralPath $ProjectPath -PathType Container)) {
    throw "Native VMD clip performance gate failed. Unity project was not found: $ProjectPath"
}
Assert-NoRunningUnityProject -ProjectPath $ProjectPath -OperationName "native VMD clip performance gate"
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $ResultsFile) | Out-Null
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $LogFile) | Out-Null
Remove-Item -LiteralPath $ResultsFile, $LogFile -Force -ErrorAction SilentlyContinue

$previousGate = $env:MMD_VMD_PERF_GATE
$env:MMD_VMD_PERF_GATE = "1"
$exitCode = 0

function ConvertTo-WindowsCommandLineArgument {
    param([Parameter(Mandatory = $true)][string] $Value)

    $builder = [System.Text.StringBuilder]::new()
    [void]$builder.Append('"')
    $backslashCount = 0
    foreach ($character in $Value.ToCharArray()) {
        if ($character -eq '\') {
            $backslashCount++
            continue
        }

        if ($character -eq '"') {
            if ($backslashCount -gt 0) {
                [void]$builder.Append((('\' * (2 * $backslashCount)) -join ''))
                $backslashCount = 0
            }
            [void]$builder.Append('\')
            [void]$builder.Append('"')
            continue
        }

        if ($backslashCount -gt 0) {
            [void]$builder.Append((('\' * $backslashCount) -join ''))
            $backslashCount = 0
        }
        [void]$builder.Append($character)
    }

    if ($backslashCount -gt 0) {
        [void]$builder.Append((('\' * (2 * $backslashCount)) -join ''))
    }

    [void]$builder.Append('"')
    return $builder.ToString()
}

try {
    $arguments = @(
        "-batchmode",
        "-runTests",
        "-projectPath", $ProjectPath,
        "-testPlatform", "EditMode",
        "-testResults", $ResultsFile,
        "-logFile", $LogFile,
        "-testFilter", "Mmd.Tests.MmdVmdNativeClipPerformanceTests"
    )
    $startInfo = [System.Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $Unity
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.Arguments = ($arguments | ForEach-Object {
        ConvertTo-WindowsCommandLineArgument ([string]$_)
    }) -join ' '
    $unityProcess = [System.Diagnostics.Process]::new()
    $unityProcess.StartInfo = $startInfo
    [void]$unityProcess.Start()
    $unityProcess.WaitForExit()
    $exitCode = $unityProcess.ExitCode
    $unityProcess.Dispose()
}
finally {
    if ($null -eq $previousGate) {
        Remove-Item Env:MMD_VMD_PERF_GATE -ErrorAction SilentlyContinue
    }
    else {
        $env:MMD_VMD_PERF_GATE = $previousGate
    }
}

if ($exitCode -ne 0) {
    throw "Native VMD clip performance gate failed. Unity exitCode=$exitCode; results=$ResultsFile; log=$LogFile"
}
$summary = Read-NUnitTestRunSummary -Path $ResultsFile -Context "Native VMD clip performance gate"
$resultsXml = [xml](Get-Content -LiteralPath $ResultsFile -Raw)
$performanceCase = $resultsXml.SelectSingleNode(
    "//test-case[@fullname='Mmd.Tests.MmdVmdNativeClipPerformanceTests.GeneratedDenseVmdNativeClipBuildHasP95Under100Milliseconds']")
if ($summary.Total -le 0 -or
    $summary.Result -ne "Passed" -or
    $summary.Failed -gt 0 -or
    $summary.Skipped -gt 0 -or
    $summary.HasFailedResult -or
    $null -eq $performanceCase -or
    $performanceCase.GetAttribute("result") -ne "Passed") {
    throw ("Native VMD clip performance gate failed. result={0}; failed={1}; passed={2}; skipped={3}; total={4}; results={5}; log={6}" -f `
        $summary.Result, $summary.Failed, $summary.Passed, $summary.Skipped, $summary.Total, $ResultsFile, $LogFile)
}

Write-Host ("Native VMD clip performance gate passed. result={0}; passed={1}; skipped={2}; total={3}; results={4}; log={5}" -f `
    $summary.Result, $summary.Passed, $summary.Skipped, $summary.Total, $ResultsFile, $LogFile)
exit $exitCode
