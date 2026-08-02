param(
    [string] $Unity = "C:\Program Files\Unity\Hub\Editor\6000.4.8f1\Editor\Unity.exe",
    [string] $ProjectPath = "F:\Develop\MMDDev\unity-mmd-loader\unity-mmd",
    [string] $PackageRoot = "F:\Develop\MMDDev\unity-mmd-loader\packages",
    [string] $ResultsFile = "F:\Develop\MMDDev\unity-mmd-loader\artifacts\native-unavailable-gate-results.xml",
    [string] $LogFile = "F:\Develop\MMDDev\unity-mmd-loader\artifacts\native-unavailable-gate.log",
    [ValidateSet("MissingDll", "MissingEntryPoint", "AbiMismatch", "InvalidBytes")]
    [string] $Mode = "MissingDll",
    [string] $ReplacementDll = ""
)

$ErrorActionPreference = "Stop"

. (Join-Path $PSScriptRoot "unity-process-environment.ps1")
. (Join-Path $PSScriptRoot "unity-project-guard.ps1")
. (Join-Path $PSScriptRoot "read-nunit-test-result.ps1")
Initialize-UnityProcessEnvironment

function Resolve-AbsolutePath {
    param([Parameter(Mandatory = $true)][string] $Path)

    if ([System.IO.Path]::IsPathRooted($Path) -or $Path -match "^[A-Za-z]:[\\/]|^\\\\") {
        return $Path
    }

    return [System.IO.Path]::GetFullPath((Join-Path (Get-Location) $Path))
}

function Copy-DirectoryWithRobocopy {
    param(
        [Parameter(Mandatory = $true)][string] $Source,
        [Parameter(Mandatory = $true)][string] $Destination,
        [string[]] $ExcludedFiles = @(),
        [string[]] $ExcludedDirectories = @()
    )

    $arguments = @(
        $Source,
        $Destination,
        "/E",
        "/XJ",
        "/NFL",
        "/NDL",
        "/NJH",
        "/NJS",
        "/NP"
    )
    if ($ExcludedFiles.Count -gt 0) {
        $arguments += "/XF"
        $arguments += $ExcludedFiles
    }
    if ($ExcludedDirectories.Count -gt 0) {
        $arguments += "/XD"
        $arguments += $ExcludedDirectories
    }

    & robocopy @arguments
    if ($LASTEXITCODE -gt 7) {
        throw "Native unavailable gate copy failed. source=$Source; destination=$Destination; robocopy=$LASTEXITCODE"
    }
}

function Remove-FileIfPresent {
    param([Parameter(Mandatory = $true)][string] $Path)

    if (Test-Path -LiteralPath $Path -PathType Leaf) {
        Remove-Item -LiteralPath $Path -Force -ErrorAction Stop
        if (Test-Path -LiteralPath $Path) {
            throw "Native unavailable gate failed. Stale file could not be removed: $Path"
        }
    }
}

function Remove-DirectoryWithRetry {
    param([Parameter(Mandatory = $true)][string] $Path)

    for ($attempt = 1; $attempt -le 5; $attempt++) {
        if (-not (Test-Path -LiteralPath $Path -PathType Container)) {
            return
        }

        try {
            [System.IO.Directory]::Delete($Path, $true)
        }
        catch {
            if ($attempt -eq 5) {
                throw "Native unavailable gate cleanup failed after $attempt attempts: $Path ($($_.Exception.Message))"
            }
            Start-Sleep -Seconds 1
        }
    }

    if (Test-Path -LiteralPath $Path) {
        throw "Native unavailable gate cleanup failed. Isolated project remains: $Path"
    }
}

$Unity = Resolve-AbsolutePath $Unity
$ProjectPath = Resolve-AbsolutePath $ProjectPath
$PackageRoot = Resolve-AbsolutePath $PackageRoot
$ResultsFile = Resolve-AbsolutePath $ResultsFile
$LogFile = Resolve-AbsolutePath $LogFile
$Mode = switch ($Mode.ToLowerInvariant()) {
    "missingdll" { "MissingDll" }
    "missingentrypoint" { "MissingEntryPoint" }
    "abimismatch" { "AbiMismatch" }
    "invalidbytes" { "InvalidBytes" }
    default { throw "Native unavailable gate failed. Unsupported mode: $Mode" }
}
if ($Mode -eq "InvalidBytes") {
    $ReplacementDll = Join-Path $PackageRoot "com.yohawing.mmd-loader\Runtime\Plugins\x86_64\mmd_runtime_ffi.dll"
}
elseif ($Mode -ne "MissingDll") {
    if ([string]::IsNullOrWhiteSpace($ReplacementDll)) {
        throw "Native unavailable gate failed. -ReplacementDll is required for mode=$Mode"
    }
    $ReplacementDll = Resolve-AbsolutePath $ReplacementDll
    if (-not (Test-Path -LiteralPath $ReplacementDll -PathType Leaf)) {
        throw "Native unavailable gate failed. Replacement DLL was not found: $ReplacementDll"
    }
}

if (-not (Test-Path -LiteralPath $Unity -PathType Leaf)) {
    throw "Native unavailable gate failed. Unity editor was not found: $Unity"
}
if (-not (Test-Path -LiteralPath $ProjectPath -PathType Container)) {
    throw "Native unavailable gate failed. Unity project was not found: $ProjectPath"
}
if (-not (Test-Path -LiteralPath $PackageRoot -PathType Container)) {
    throw "Native unavailable gate failed. package root was not found: $PackageRoot"
}
Assert-NoRunningUnityProject -ProjectPath $ProjectPath -OperationName "native unavailable gate ($Mode)"

$resultsDirectory = Split-Path -Parent $ResultsFile
$logDirectory = Split-Path -Parent $LogFile
New-Item -ItemType Directory -Force -Path $resultsDirectory, $logDirectory | Out-Null

$gateRoot = Join-Path $resultsDirectory ("native-unavailable-gate-" + [Guid]::NewGuid().ToString("N"))
$gateProject = Join-Path $gateRoot "unity-mmd"
$gatePackages = Join-Path $gateRoot "packages"
$previousGateMode = $env:MMD_NATIVE_PHYSICAL_GATE_MODE
$replacementFingerprint = $null
if ($Mode -ne "MissingDll") {
    $replacementFile = Get-Item -LiteralPath $ReplacementDll
    $replacementFingerprint = Get-FileHash -LiteralPath $ReplacementDll -Algorithm SHA256
    Write-Host ("Native unavailable gate replacement DLL: mode={0}; path={1}; bytes={2}; sha256={3}" -f `
        $Mode, $ReplacementDll, $replacementFile.Length, $replacementFingerprint.Hash)
}

try {
    New-Item -ItemType Directory -Force -Path `
        (Join-Path $gateProject "Assets"),
        (Join-Path $gateProject "Packages"),
        (Join-Path $gateProject "ProjectSettings"),
        $gatePackages | Out-Null

    Copy-DirectoryWithRobocopy `
        (Join-Path $ProjectPath "Packages") `
        (Join-Path $gateProject "Packages")
    Copy-DirectoryWithRobocopy `
        (Join-Path $ProjectPath "ProjectSettings") `
        (Join-Path $gateProject "ProjectSettings")
    Copy-DirectoryWithRobocopy `
        -Source (Join-Path $PackageRoot "com.yohawing.mmd-loader") `
        -Destination (Join-Path $gatePackages "com.yohawing.mmd-loader") `
        -ExcludedFiles @("mmd_runtime_ffi.dll") `
        -ExcludedDirectories @(
            (Join-Path $PackageRoot "com.yohawing.mmd-loader\Tests\Fixtures\Assets"))

    $devtoolsSource = Join-Path $PackageRoot "com.yohawing.mmd-loader.devtools"
    if (Test-Path -LiteralPath $devtoolsSource -PathType Container) {
        Copy-DirectoryWithRobocopy `
            $devtoolsSource `
            (Join-Path $gatePackages "com.yohawing.mmd-loader.devtools")
    }

    $gateDllPath = Join-Path $gatePackages "com.yohawing.mmd-loader\Runtime\Plugins\x86_64\mmd_runtime_ffi.dll"
    if ($Mode -eq "MissingDll") {
        if (Test-Path -LiteralPath $gateDllPath) {
            throw "Native unavailable gate setup failed. The copied package still contains the native DLL: $gateDllPath"
        }
    }
    elseif ($Mode -eq "InvalidBytes") {
        Copy-Item -LiteralPath $ReplacementDll -Destination $gateDllPath -Force
        if (-not (Test-Path -LiteralPath $gateDllPath -PathType Leaf)) {
            throw "Native unavailable gate setup failed. Packaged DLL was not copied for invalid-bytes mode: $gateDllPath"
        }
    }
    else {
        Copy-Item -LiteralPath $ReplacementDll -Destination $gateDllPath -Force
        if (-not (Test-Path -LiteralPath $gateDllPath -PathType Leaf)) {
            throw "Native unavailable gate setup failed. Replacement DLL was not copied: $gateDllPath"
        }
    }
    if (-not (Test-Path -LiteralPath $gateProject -PathType Container) -or
        -not (Test-Path -LiteralPath (Join-Path $gateProject "ProjectSettings\ProjectVersion.txt") -PathType Leaf)) {
        throw "Native unavailable gate setup failed. The isolated Unity project is incomplete: $gateProject"
    }

    Remove-FileIfPresent -Path $ResultsFile
    Remove-FileIfPresent -Path $LogFile
    $env:MMD_NATIVE_PHYSICAL_GATE_MODE = $Mode
    $expectedTestName = switch ($Mode) {
        "MissingDll" {
            "Mmd.Tests.MmdRuntimeNativeUnavailableBoundaryContractTests.PhysicalMissingNativeDllProbeClassifiesUnavailableRuntime"
        }
        "MissingEntryPoint" {
            "Mmd.Tests.MmdRuntimeNativeUnavailableBoundaryContractTests.PhysicalMissingNativeEntryPointProbeClassifiesUnavailableRuntime"
        }
        "AbiMismatch" {
            "Mmd.Tests.MmdRuntimeNativeUnavailableBoundaryContractTests.PhysicalAbiMismatchProbeClassifiesUnsupportedRuntime"
        }
        "InvalidBytes" {
            "Mmd.Tests.MmdRuntimeNativeUnavailableBoundaryContractTests.PhysicalInvalidNativeBytesProbeReportsNativeLastError"
        }
    }
    $gateProjectForUnity = Resolve-Path -LiteralPath $gateProject -Relative
    $unityArguments = @(
        "-batchmode",
        "-runTests",
        "-projectPath", $gateProjectForUnity,
        "-testPlatform", "EditMode",
        "-testResults", $ResultsFile,
        "-logFile", $LogFile,
        "-testFilter", $expectedTestName
    )
    Write-Host ("Native unavailable gate mode={0}; isolated project: {1}" -f $Mode, $gateProject)
    $unityExitCode = 1
    for ($attempt = 1; $attempt -le 2; $attempt++) {
        if ($attempt -gt 1) {
            Remove-Item -LiteralPath `
                (Join-Path $gateProject "Library\ArtifactDB-lock"),
                (Join-Path $gateProject "Library\SourceAssetDB-lock") `
                -Force -ErrorAction SilentlyContinue
        }

        $unityProcess = Start-Process `
            -FilePath $Unity `
            -ArgumentList $unityArguments `
            -WorkingDirectory (Get-Location).Path `
            -PassThru `
            -Wait `
            -WindowStyle Hidden
        $unityExitCode = $unityProcess.ExitCode
        if (Test-Path -LiteralPath $ResultsFile -PathType Leaf) {
            break
        }
    }

    if (-not (Test-Path -LiteralPath $ResultsFile -PathType Leaf)) {
        throw "Native unavailable gate failed. Unity did not create test results. exitCode=$unityExitCode; log=$LogFile"
    }

    $summary = Read-NUnitTestRunSummary -Path $ResultsFile -Context "Native unavailable gate"
    [xml] $resultsXml = Get-Content -LiteralPath $ResultsFile -Raw
    $testRun = $resultsXml.SelectSingleNode("/test-run")
    $inconclusiveAttribute = $testRun.Attributes["inconclusive"]
    $inconclusiveCount = 0
    if ($null -eq $inconclusiveAttribute -or
        [string]::IsNullOrWhiteSpace([string] $inconclusiveAttribute.Value) -or
        -not [int]::TryParse([string] $inconclusiveAttribute.Value, [ref] $inconclusiveCount) -or
        $inconclusiveCount -lt 0) {
        throw "Native unavailable gate XML has an invalid <inconclusive> count: $ResultsFile"
    }

    $physicalCase = $resultsXml.SelectSingleNode(
        "//test-case[@fullname='$expectedTestName']")
    $physicalCaseResult = if ($null -eq $physicalCase) { "<missing>" } else { [string] $physicalCase.GetAttribute("result") }
    if ($unityExitCode -ne 0 -or
        $summary.Result -ne "Passed" -or
        $summary.Total -ne 1 -or
        $summary.Passed -ne 1 -or
        $summary.Failed -ne 0 -or
        $inconclusiveCount -ne 0 -or
        $summary.Skipped -ne 0 -or
        $physicalCaseResult -ne "Passed") {
        throw (("Native unavailable gate failed. mode={0}; result={1}; total={2}; passed={3}; failed={4}; " +
            "inconclusive={5}; skipped={6}; physicalCase={7}; unityExitCode={8}; results={9}; log={10}") -f `
            $Mode,
            $summary.Result, $summary.Total, $summary.Passed, $summary.Failed, $inconclusiveCount, $summary.Skipped,
            $physicalCaseResult, $unityExitCode, $ResultsFile, $LogFile
        )
    }
}
finally {
    if ($null -eq $previousGateMode) {
        Remove-Item Env:MMD_NATIVE_PHYSICAL_GATE_MODE -ErrorAction SilentlyContinue
    }
    else {
        $env:MMD_NATIVE_PHYSICAL_GATE_MODE = $previousGateMode
    }

    if (Test-Path -LiteralPath $gateRoot -PathType Container) {
        Remove-DirectoryWithRetry -Path $gateRoot
    }
}

Write-Host ("Native unavailable gate passed. mode={0}; results={1}; log={2}; isolatedCopy=cleaned" -f $Mode, $ResultsFile, $LogFile)
