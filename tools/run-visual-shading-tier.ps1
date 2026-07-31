param(
    [string] $Unity = "C:\Program Files\Unity\Hub\Editor\6000.4.8f1\Editor\Unity.exe",
    [string] $ArtifactsRoot = "",
    [string] $ProjectPath = "",
    [switch] $SkipPerturbationProof
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
. (Join-Path $repoRoot "scripts\unity-process-environment.ps1")
Initialize-UnityProcessEnvironment
. (Join-Path $repoRoot "scripts\read-nunit-test-result.ps1")

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

# `& $Unity ...` (the PowerShell call operator) returns control before the spawned
# Unity.exe process has actually exited in this environment: $LASTEXITCODE comes back
# $null/stale and Packages\manifest.json / test-results.xml are not yet on disk
# immediately after the call "returns", even though the run completes successfully
# moments later. That also lets the next invocation race the previous one's still-open
# project lock ("another Unity instance is running with this project open"). Use
# Start-Process -Wait, which reliably blocks on the actual child process handle, and
# still poll for the expected artifact as a belt-and-suspenders check.
function Invoke-UnityProcess {
    param(
        [Parameter(Mandatory = $true)][string[]] $Arguments
    )
    $proc = Start-Process -FilePath $Unity -ArgumentList $Arguments -Wait -PassThru -NoNewWindow
    return $proc.ExitCode
}

function Wait-ForFileToSettle {
    param(
        [Parameter(Mandatory = $true)][string] $Path,
        [int] $TimeoutSeconds = 180
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
                return $true
            }
        }
        else {
            $stableCount = 0
            $lastLength = $item.Length
            $lastWriteTimeUtc = $item.LastWriteTimeUtc
        }

        Start-Sleep -Milliseconds 250
    }

    return (Test-Path -LiteralPath $Path)
}

# Never default to the repo's local "unity-mmd" consumer project: it may be open in the
# Unity Editor (project lock conflict) and its manifest carries git-URL package
# dependencies that fail to resolve in offline/batchmode ("The path argument must be of
# type string. Received undefined."). Always bootstrap an isolated throwaway project
# unless the caller explicitly passes -ProjectPath.
if ([string]::IsNullOrEmpty($ProjectPath)) {
    $ProjectPath = Join-Path $runRoot "project"
}
$ProjectPath = [IO.Path]::GetFullPath($ProjectPath)

if (-not (Test-Path -LiteralPath (Join-Path $ProjectPath "Packages\manifest.json"))) {
    $bootstrapLog = Join-Path $resultsRoot "bootstrap.log"
    Invoke-UnityProcess -Arguments @("-batchmode", "-quit", "-createProject", $ProjectPath, "-logFile", $bootstrapLog) | Out-Null
    $bootstrapManifest = Join-Path $ProjectPath "Packages\manifest.json"
    if (-not (Wait-ForFileToSettle -Path $bootstrapManifest)) {
        throw "Unity project bootstrap failed (manifest.json never appeared). log=$bootstrapLog"
    }

    # Disable async shader compilation for this throwaway project. A fresh bootstrap project
    # has no ShaderCache, so Editor async shader compilation lets the *first* capture in the
    # determinism pair (firstPng in MmdGeneratedPmxVisualParityTests) render before the
    # "MMD Basic URP Toon" variant has finished compiling -- observed as a byte-for-byte
    # determinism-assertion failure where determinism-a.png comes back as a near-empty
    # background-only image (~20KB) while determinism-b.png (rendered moments later, once the
    # variant is warm) is the correct toon-shaded capture (~110KB). unity-mmd's long-lived
    # project never showed this because its ShaderCache/Library is already warm. Force
    # synchronous compilation so every capture -- including the very first one after a cold
    # project bootstrap -- blocks until its shader variants are ready.
    $editorSettingsPath = Join-Path $ProjectPath "ProjectSettings\EditorSettings.asset"
    if (Wait-ForFileToSettle -Path $editorSettingsPath) {
        $editorSettingsText = Get-Content -LiteralPath $editorSettingsPath -Raw
        $patchedEditorSettingsText = $editorSettingsText -replace 'm_AsyncShaderCompilation: 1', 'm_AsyncShaderCompilation: 0'
        # [IO.File]::WriteAllText writes BOM-less UTF-8 on every PowerShell host; Set-Content
        # -Encoding utf8 emits a BOM under Windows PowerShell 5.1, and Unity's package
        # resolver rejects BOM-prefixed JSON ("Non-whitespace before {"), so never use it here.
        [IO.File]::WriteAllText($editorSettingsPath, $patchedEditorSettingsText)
    }
    else {
        throw "Unity project bootstrap failed (EditorSettings.asset never appeared). log=$bootstrapLog"
    }

    # A plain `-createProject` project defaults to Gamma color space (m_ActiveColorSpace: 0).
    # The tracked golden capture was recorded against unity-mmd, whose project template chose
    # Linear (m_ActiveColorSpace: 1) -- Unity's standard for URP projects. Rendering the same
    # scene in Gamma space visibly desaturates/brightens everything (observed: the golden's
    # deep-orange toon-shaded box came back as pale yellow), because the whole lighting pipeline
    # (light intensity falloff, ambient, the shader's own gamma/linear-aware paths) assumes
    # Linear. This has to be set before the project is ever opened for real: changing color
    # space later in an already-imported project just re-imports everything, it does not
    # retroactively fix a capture taken under the wrong space.
    $projectSettingsPath = Join-Path $ProjectPath "ProjectSettings\ProjectSettings.asset"
    if (Wait-ForFileToSettle -Path $projectSettingsPath) {
        $projectSettingsText = Get-Content -LiteralPath $projectSettingsPath -Raw
        $patchedProjectSettingsText = $projectSettingsText -replace 'm_ActiveColorSpace: 0', 'm_ActiveColorSpace: 1'
        [IO.File]::WriteAllText($projectSettingsPath, $patchedProjectSettingsText)
    }
    else {
        throw "Unity project bootstrap failed (ProjectSettings.asset never appeared). log=$bootstrapLog"
    }

    # Preserve the module set selected by this Unity version and add only the packages this
    # gate owns. Package dependencies (URP and Timeline) stay authoritative in package.json;
    # duplicating their versions here made the gate drift from the package under test.
    $loaderPath = (Join-Path $repoRoot "packages\com.yohawing.mmd-loader").Replace('\', '/')
    $devToolsPath = (Join-Path $repoRoot "packages\com.yohawing.mmd-loader.devtools").Replace('\', '/')
    $loaderPackage = Get-Content -LiteralPath (Join-Path $repoRoot "packages\com.yohawing.mmd-loader\package.json") -Raw | ConvertFrom-Json
    $editorManifestPath = Join-Path (Split-Path -Parent $Unity) "Data\Resources\PackageManager\Editor\manifest.json"
    $editorManifest = Get-Content -LiteralPath $editorManifestPath -Raw | ConvertFrom-Json
    $manifest = Get-Content -LiteralPath $bootstrapManifest -Raw | ConvertFrom-Json
    $manifest.dependencies | Add-Member -NotePropertyName "com.unity.test-framework" -NotePropertyValue "1.6.0" -Force
    foreach ($dependencyName in @("com.unity.render-pipelines.universal", "com.unity.timeline")) {
        $dependencyVersion = $loaderPackage.dependencies.$dependencyName
        if ([string]::IsNullOrEmpty($dependencyVersion)) {
            throw "Loader package does not declare required visual-tier dependency: $dependencyName"
        }
        # package.json declares the oldest supported line. A newer Editor can require its
        # bundled package line (Unity 6000.4 requires URP 17.4, for example), so use the
        # Editor's own manifest when it publishes an exact compatible version.
        $editorDependency = $editorManifest.packages.$dependencyName
        if ($null -ne $editorDependency -and -not [string]::IsNullOrEmpty($editorDependency.version)) {
            $dependencyVersion = $editorDependency.version
        }
        $manifest.dependencies | Add-Member -NotePropertyName $dependencyName -NotePropertyValue $dependencyVersion -Force
    }
    $manifest.dependencies | Add-Member -NotePropertyName "com.yohawing.mmd-loader" -NotePropertyValue "file:$loaderPath" -Force
    $manifest.dependencies | Add-Member -NotePropertyName "com.yohawing.mmd-loader.devtools" -NotePropertyValue "file:$devToolsPath" -Force
    # `-createProject` emits no built-in module entries in batch mode. The package uses
    # modular UnityEngine APIs across runtime/editor assemblies, so retain the known module
    # surface explicitly; unlike registry package versions these module versions are fixed by
    # the Editor and do not duplicate package.json policy.
    $requiredUnityModules = @(
        "accessibility", "adaptiveperformance", "ai", "androidjni", "animation",
        "assetbundle", "audio", "cloth", "director", "imageconversion", "imgui",
        "jsonserialize", "particlesystem", "physics", "physics2d", "screencapture",
        "terrain", "terrainphysics", "tilemap", "ui", "uielements", "umbra",
        "unityanalytics", "unitywebrequest", "unitywebrequestassetbundle",
        "unitywebrequestaudio", "unitywebrequesttexture", "unitywebrequestwww",
        "vectorgraphics", "vehicles", "video", "vr", "wind", "xr"
    )
    foreach ($moduleName in $requiredUnityModules) {
        $manifest.dependencies | Add-Member -NotePropertyName "com.unity.modules.$moduleName" -NotePropertyValue "1.0.0" -Force
    }
    $manifest | Add-Member -NotePropertyName testables -NotePropertyValue @("com.yohawing.mmd-loader.devtools") -Force
    [IO.File]::WriteAllText($bootstrapManifest, ($manifest | ConvertTo-Json -Depth 5))

    # A plain `-createProject` project has the URP *package* on disk as soon as it's in the
    # manifest, but Graphics Settings never gets a Universal Render Pipeline *asset* assigned
    # (ProjectSettings/GraphicsSettings.asset keeps m_CustomRenderPipeline: {fileID: 0}, i.e.
    # Built-in Render Pipeline stays active). URP-only shaders like "MMD Basic URP Toon" have
    # no Built-in subshader and render as the pink/magenta error shader as a result -- this is
    # what produced a solid-magenta captured PNG instead of a toon-shaded box with an outline.
    #
    # The first empty-project import can terminate during package resolution before local
    # packages are compiled. Seed the tracked bootstrap source into Assets/Editor so the next
    # launch can both resolve packages and configure URP; do not generate C# inside PowerShell.
    $bootstrapEditorDir = Join-Path $ProjectPath "Assets\Editor"
    New-Item -ItemType Directory -Force -Path $bootstrapEditorDir | Out-Null
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot "VisualShadingTierBootstrap.cs") -Destination $bootstrapEditorDir -Force

    $rpBootstrapLog = Join-Path $resultsRoot "bootstrap-urp.log"
    Invoke-UnityProcess -Arguments @(
        "-batchmode", "-quit", "-projectPath", $ProjectPath,
        "-executeMethod", "VisualShadingTierBootstrap.EnsureUniversalRenderPipeline",
        "-logFile", $rpBootstrapLog
    ) | Out-Null
    $rpAsset = Join-Path $ProjectPath "Assets\Settings\VisualTierRenderPipelineAsset.asset"
    if (-not (Wait-ForFileToSettle -Path $rpAsset)) {
        throw "Failed to assign a Universal Render Pipeline asset in the bootstrap project. log=$rpBootstrapLog"
    }
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
    # NOTE: do not pass -quit alongside -runTests. Unity Test Framework's own -runTests
    # flow quits the Editor once the run finishes and reports its result via the test
    # results XML / exit code; an explicit -quit races that and can close the Editor
    # right after the post-compile domain reload, before the test runner ever executes
    # (observed: clean "Exiting batchmode successfully" with no testResults XML written
    # and no capture artifacts).
    Invoke-UnityProcess -Arguments @(
        "-batchmode", "-runTests", "-projectPath", $ProjectPath, "-testPlatform", "EditMode",
        "-testFilter", $testName, "-testResults", $results, "-logFile", $log
    ) | Out-Null
    if (-not (Wait-ForFileToSettle -Path $results)) {
        throw "$Name did not produce test results. log=$log"
    }
    $testSummary = Read-NUnitTestRunSummary -Path $results -Context "$Name results"
    $failed = $testSummary.Failed
    $passed = $testSummary.Passed

    # A clean exit with an empty/no-op test-run (0 matched, 0 passed, 0 failed) is not a
    # green result -- it means -testFilter matched nothing and the gate never ran. Positively
    # require at least one test case to have executed on either side of the A/B/A proof.
    if ($passed -eq 0 -and $failed -eq 0) {
        throw "$Name matched zero test cases (passed=0, failed=0) -- the gate did not actually run. results=$results log=$log"
    }
    if ($ExpectFailure) {
        if ($failed -eq 0) {
            throw "$Name was expected to fail after shader-output perturbation, but stayed green. results=$results"
        }
    }
    else {
        if ($failed -ne 0) {
            throw "$Name failed. results=$results log=$log"
        }
        if ($passed -eq 0) {
            throw "$Name reported passed=0 with failed=0; refusing to treat as green. results=$results"
        }
        $captureDir = Join-Path $captureRoot $Name
        $capturedPngs = @()
        if (Test-Path -LiteralPath $captureDir) {
            $capturedPngs = @(Get-ChildItem -LiteralPath $captureDir -Filter "*.png" -Recurse -ErrorAction SilentlyContinue)
        }
        if ($capturedPngs.Count -eq 0) {
            throw "$Name passed but produced no capture PNGs under $captureDir. Machine-green without artifacts is not accepted."
        }
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
