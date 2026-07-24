[CmdletBinding()]
param(
    [string]$ProjectPath = (Join-Path $PSScriptRoot '..\tools\performance-unity'),
    [string]$OutputPath = (Join-Path $PSScriptRoot '..\artifacts\performance\performance-baseline.json'),
    [string]$PmxPath = (Join-Path $PSScriptRoot '..\packages\com.yohawing.mmd-loader\Tests\Fixtures\Assets\test_1bone_cube.pmx'),
    [string]$VmdPath = (Join-Path $PSScriptRoot '..\packages\com.yohawing.mmd-loader\Tests\Fixtures\Assets\test_1bone_cube_motion.vmd'),
    [string]$PhysicsPmxPath = (Join-Path $PSScriptRoot '..\packages\com.yohawing.mmd-loader\Tests\Fixtures\Assets\test_hair_physics.pmx'),
    [string]$UnityPath = 'C:\Program Files\Unity\Hub\Editor\6000.4.8f1\Editor\Unity.exe',
    [int]$WarmupFrames = 5,
    [int]$FrameCount = 120,
    [float]$FrameRate = 30.0,
    [string]$BaselinePath = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Write-SkipReport {
    param([string]$Reason)

    $outputDirectory = Split-Path -Parent ([IO.Path]::GetFullPath($OutputPath))
    New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null
    $report = [ordered]@{
        schemaVersion = 1
        schema = 'mmd-performance-baseline'
        status = 'SKIP'
        skipReason = $Reason
        generatedUtc = [DateTime]::UtcNow.ToString('O')
        fixtureSha256 = ''
        vmdFixtureSha256 = ''
        physicsFixtureSha256 = ''
        unityVersion = ''
        packageHead = ''
        mmdAnimRevision = ''
        mmdAnimAbi = ''
        backend = ''
        cpu = ''
        warmupFrames = $WarmupFrames
        measurementFrames = $FrameCount
        frameRate = $FrameRate
        deterministicResultChecksum = ''
        phases = @()
    }
    $report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $OutputPath -Encoding UTF8
}

$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$ProjectPath = [IO.Path]::GetFullPath($ProjectPath)
$OutputPath = [IO.Path]::GetFullPath($OutputPath)
$PmxPath = [IO.Path]::GetFullPath($PmxPath)
$VmdPath = [IO.Path]::GetFullPath($VmdPath)
$PhysicsPmxPath = [IO.Path]::GetFullPath($PhysicsPmxPath)
$UnityLogPath = [IO.Path]::ChangeExtension($OutputPath, '.unity.log')

$guardPath = Join-Path $PSScriptRoot 'unity-project-guard.ps1'
if (-not (Test-Path -LiteralPath $guardPath -PathType Leaf)) {
    Write-SkipReport "Unity project guard was not found: $guardPath"
    exit 2
}
. $guardPath

if ($WarmupFrames -lt 5) {
    Write-SkipReport 'WarmupFrames must be at least 5 for the P0 gate.'
    exit 2
}

if ($FrameCount -ne 120) {
    Write-SkipReport 'FrameCount must be exactly 120 for the P0 gate.'
    exit 2
}

if (-not (Test-Path -LiteralPath $ProjectPath -PathType Container)) {
    Write-SkipReport "Unity project was not found: $ProjectPath"
    exit 2
}

$missing = @(@($PmxPath, $VmdPath, $PhysicsPmxPath) | Where-Object { -not (Test-Path -LiteralPath $_ -PathType Leaf) })
if ($missing.Count -gt 0) {
    Write-SkipReport ('Tracked performance fixture missing: ' + ($missing -join ', '))
    exit 2
}

if (-not (Test-Path -LiteralPath $UnityPath -PathType Leaf)) {
    Write-SkipReport "Unity editor was not found: $UnityPath"
    exit 2
}

try {
    Assert-NoRunningUnityProject -ProjectPath $ProjectPath -OperationName 'MMD performance baseline' -TransientWaitSeconds 0
}
catch {
    Write-SkipReport $_.Exception.Message
    exit 2
}

$unityArguments = @(
    '-batchmode',
    '-quit',
    '-projectPath', $ProjectPath,
    '-logFile', $UnityLogPath,
    '-executeMethod', 'Mmd.Editor.MmdPerformanceBaselineCli.RunFromCommandLine',
    '-repoRoot', $repoRoot,
    '-out', $OutputPath,
    '-pmxPath', $PmxPath,
    '-vmdPath', $VmdPath,
    '-physicsPmxPath', $PhysicsPmxPath,
    '-warmupFrames', $WarmupFrames.ToString(),
    '-frameCount', $FrameCount.ToString(),
    '-frameRate', $FrameRate.ToString([Globalization.CultureInfo]::InvariantCulture)
)

if (-not [string]::IsNullOrWhiteSpace($BaselinePath)) {
    $BaselinePath = [IO.Path]::GetFullPath($BaselinePath)
    $unityArguments += @('-baseline', $BaselinePath)
}

Remove-Item -LiteralPath $OutputPath, $UnityLogPath -Force -ErrorAction SilentlyContinue

$unityProcess = Start-Process `
    -FilePath $UnityPath `
    -ArgumentList $unityArguments `
    -Wait `
    -PassThru `
    -WindowStyle Hidden
$unityExitCode = $unityProcess.ExitCode

if (-not (Test-Path -LiteralPath $OutputPath -PathType Leaf)) {
    Write-SkipReport "Unity did not produce a report (license or batchmode infrastructure unavailable; exit code $unityExitCode)."
    exit 2
}

$report = Get-Content -Raw -LiteralPath $OutputPath | ConvertFrom-Json
switch ([string]$report.status) {
    'PASS' { exit 0 }
    'SKIP' { exit 2 }
    'FAIL' { exit 1 }
    'ERROR' { exit 1 }
    default {
        exit 1
    }
}
