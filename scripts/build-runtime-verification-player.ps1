param(
    [string] $Unity = "C:\Program Files\Unity\Hub\Editor\6000.4.8f1\Editor\Unity.exe",
    [string] $ProjectPath = "F:\Develop\MMDDev\unity-mmd-loader\unity-mmd",
    [string] $SamplePath = "F:\Develop\MMDDev\unity-mmd-loader\packages\com.yohawing.mmd-loader\Samples~\RuntimeVerification",
    [string] $ProjectSamplePath = "F:\Develop\MMDDev\unity-mmd-loader\unity-mmd\Assets\RuntimeVerification",
    [string] $ArtifactsPath = "F:\Develop\MMDDev\unity-mmd-loader\artifacts\runtime-verification",
    [string] $OutputPath = "",
    [switch] $Development
)

$ErrorActionPreference = "Stop"

function Resolve-AbsolutePath {
    param([Parameter(Mandatory = $true)][string] $Path)

    if ([System.IO.Path]::IsPathRooted($Path)) {
        return [System.IO.Path]::GetFullPath($Path)
    }

    return [System.IO.Path]::GetFullPath((Join-Path (Get-Location) $Path))
}

$ProjectPath = Resolve-AbsolutePath $ProjectPath
$SamplePath = Resolve-AbsolutePath $SamplePath
$ProjectSamplePath = Resolve-AbsolutePath $ProjectSamplePath
$ArtifactsPath = Resolve-AbsolutePath $ArtifactsPath
if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path $ArtifactsPath "MmdRuntimeVerification.exe"
}
$OutputPath = Resolve-AbsolutePath $OutputPath

if (-not (Test-Path -LiteralPath $Unity)) {
    throw "Unity executable was not found: $Unity"
}

if (-not (Test-Path -LiteralPath $ProjectPath)) {
    throw "Unity project was not found: $ProjectPath"
}

if (-not (Test-Path -LiteralPath $SamplePath)) {
    throw "RuntimeVerification sample was not found: $SamplePath"
}

New-Item -ItemType Directory -Force -Path $ArtifactsPath | Out-Null
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $OutputPath) | Out-Null
if (Test-Path -LiteralPath $ProjectSamplePath) {
    Remove-Item -LiteralPath $ProjectSamplePath -Recurse -Force
}

New-Item -ItemType Directory -Force -Path (Split-Path -Parent $ProjectSamplePath) | Out-Null
Copy-Item -LiteralPath $SamplePath -Destination $ProjectSamplePath -Recurse -Force

$logFile = Join-Path $ArtifactsPath "build-player.log"
Remove-Item -LiteralPath $logFile -Force -ErrorAction SilentlyContinue

$unityArgs = @(
    "-batchmode",
    "-quit",
    "-projectPath", $ProjectPath,
    "-logFile", $logFile,
    "-executeMethod", "Mmd.Samples.RuntimeVerification.Editor.MmdRuntimeVerificationBuildCommand.BuildFromCommandLine",
    "--scene-path", "Assets/RuntimeVerification/RuntimeVerification.unity",
    "--output", $OutputPath
)

if ($Development) {
    $unityArgs += "--development"
}

& $Unity @unityArgs
$exitCode = if ($null -eq $LASTEXITCODE) { 0 } else { $LASTEXITCODE }
if ($exitCode -ne 0) {
    throw "Runtime verification player build failed with exit code $exitCode. log=$logFile"
}

Write-Host ("Runtime verification player built. output={0}; log={1}" -f $OutputPath, $logFile)
