param(
    [string] $Unity = "C:\Program Files\Unity\Hub\Editor\6000.4.8f1\Editor\Unity.exe",
    [string] $ArtifactsRoot = "",
    [string] $ProjectPath = "",
    [switch] $SkipPerturbationProof,
    # When set, bypasses the default A/B/A determinism-vs-golden gate entirely and instead
    # runs exactly one EditMode -testFilter pass (no perturbation proof) using the same
    # bootstrap-project / capture-artifact plumbing. Used to reuse this script's Unity
    # batchmode invocation for other Explicit capture tests (e.g. the S1c visual review
    # delta cases) without touching the default gate behavior below.
    [string] $TestFilter = "",
    [string] $RunName = "custom",
    # A cold batchmode process's very first
    # MmdEditorRenderingDiagnostics.RenderGeneratedPmxVisualCase call returns an invalid
    # Material.FindPass("ForwardLit") result (observed 2026-07-21 while wiring up the S1c
    # batch review: the very first capture in a fresh Editor process reports
    # selectedMaterialPassValid=false even though the same call succeeds a moment later in
    # the same process) -- a Unity cold-domain quirk unrelated to any shader/test change.
    # When set, this runs WarmupTestFilter joined with TestFilter via NUnit's "|" OR
    # selector so the warm-up test absorbs that first-call failure in-process before the
    # real target test runs. Comma-joining does NOT work here (Unity's -testFilter treats a
    # comma-joined string as one literal name and matches zero tests); "|" is required.
    [string] $WarmupTestFilter = ""
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

    # Use absolute paths for the file: dependencies (not a relative path from the bootstrap
    # project's Packages folder): [IO.Path]::GetRelativePath is unavailable under Windows
    # PowerShell 5.1's .NET Framework runtime, and an absolute file: URI works identically
    # for Unity's package resolver without that compatibility risk.
    $loaderPath = (Join-Path $repoRoot "packages\com.yohawing.mmd-loader").Replace('\', '/')
    $devToolsPath = (Join-Path $repoRoot "packages\com.yohawing.mmd-loader.devtools").Replace('\', '/')
    # The devtools EditMode test assembly (Mmd.DevTools.EditModeTests) transitively references
    # Mmd.Editor -> Mmd.Rendering.Universal (needs URP), and directly references Unity.Timeline
    # (needs com.unity.timeline). Runtime code also uses UnityEngine.Collider, which only
    # resolves when the built-in Physics module package is present. A manifest with only the
    # two file: packages + test-framework fails to compile ("error CS1069: ... Collider ...
    # Enable the built in package 'Physics'", plus missing Unity.Timeline/URP assembly refs).
    # Mirror the built-in module + package set from the known-working local consumer project
    # (unity-mmd/Packages/manifest.json), minus its git-URL package (io.github.hatayama.
    # uloopmcp), which is what originally broke offline/batchmode package resolution here
    # ("Failed to resolve packages: The \"path\" argument must be of type string.").
    $manifest = [ordered]@{
        dependencies = [ordered]@{
            "com.unity.test-framework" = "1.6.0"
            "com.unity.render-pipelines.universal" = "17.4.0"
            "com.unity.timeline" = "1.8.12"
            "com.yohawing.mmd-loader" = "file:$loaderPath"
            "com.yohawing.mmd-loader.devtools" = "file:$devToolsPath"
            "com.unity.modules.accessibility" = "1.0.0"
            "com.unity.modules.adaptiveperformance" = "1.0.0"
            "com.unity.modules.ai" = "1.0.0"
            "com.unity.modules.androidjni" = "1.0.0"
            "com.unity.modules.animation" = "1.0.0"
            "com.unity.modules.assetbundle" = "1.0.0"
            "com.unity.modules.audio" = "1.0.0"
            "com.unity.modules.cloth" = "1.0.0"
            "com.unity.modules.director" = "1.0.0"
            "com.unity.modules.imageconversion" = "1.0.0"
            "com.unity.modules.imgui" = "1.0.0"
            "com.unity.modules.jsonserialize" = "1.0.0"
            "com.unity.modules.particlesystem" = "1.0.0"
            "com.unity.modules.physics" = "1.0.0"
            "com.unity.modules.physics2d" = "1.0.0"
            "com.unity.modules.screencapture" = "1.0.0"
            "com.unity.modules.terrain" = "1.0.0"
            "com.unity.modules.terrainphysics" = "1.0.0"
            "com.unity.modules.tilemap" = "1.0.0"
            "com.unity.modules.ui" = "1.0.0"
            "com.unity.modules.uielements" = "1.0.0"
            "com.unity.modules.umbra" = "1.0.0"
            "com.unity.modules.unityanalytics" = "1.0.0"
            "com.unity.modules.unitywebrequest" = "1.0.0"
            "com.unity.modules.unitywebrequestassetbundle" = "1.0.0"
            "com.unity.modules.unitywebrequestaudio" = "1.0.0"
            "com.unity.modules.unitywebrequesttexture" = "1.0.0"
            "com.unity.modules.unitywebrequestwww" = "1.0.0"
            "com.unity.modules.vectorgraphics" = "1.0.0"
            "com.unity.modules.vehicles" = "1.0.0"
            "com.unity.modules.video" = "1.0.0"
            "com.unity.modules.vr" = "1.0.0"
            "com.unity.modules.wind" = "1.0.0"
            "com.unity.modules.xr" = "1.0.0"
        }
        testables = @("com.yohawing.mmd-loader.devtools")
    }
    [IO.File]::WriteAllText((Join-Path $ProjectPath "Packages\manifest.json"), ($manifest | ConvertTo-Json -Depth 5))

    # A plain `-createProject` project has the URP *package* on disk as soon as it's in the
    # manifest, but Graphics Settings never gets a Universal Render Pipeline *asset* assigned
    # (ProjectSettings/GraphicsSettings.asset keeps m_CustomRenderPipeline: {fileID: 0}, i.e.
    # Built-in Render Pipeline stays active). URP-only shaders like "MMD Basic URP Toon" have
    # no Built-in subshader and render as the pink/magenta error shader as a result -- this is
    # what produced a solid-magenta captured PNG instead of a toon-shaded box with an outline.
    #
    # Fix it with a throwaway Editor script dropped straight into the bootstrap project itself
    # (Assets/Editor, which lives entirely under gitignored artifacts/ -- not the tracked
    # devtools package, keeping this fix scoped to bootstrap generation only). It creates a
    # UniversalRenderPipelineAsset + UniversalRendererData and assigns them as the project's
    # default render pipeline, then this single -executeMethod invocation also resolves the
    # manifest's newly added packages (URP, Timeline) and compiles scripts for the first time.
    #
    # It also attaches Mmd.Rendering.Universal.MmdOutlineRendererFeature to the renderer data.
    # unity-mmd's tracked golden-capture environment (Assets/Settings/PC_Renderer.asset) has
    # this feature in its m_RendererFeatures list; a from-scratch UniversalRendererData has an
    # empty feature list. The MMD outline is drawn entirely by this renderer feature's pass
    # (not by a shader-level "Outline" LightMode pass), so without it outlinePixelCount is 0
    # regardless of whether the shader/material/color-space are otherwise correct.
    $bootstrapEditorDir = Join-Path $ProjectPath "Assets\Editor"
    New-Item -ItemType Directory -Force -Path $bootstrapEditorDir | Out-Null
    $bootstrapScript = @'
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace VisualShadingTierBootstrap
{
    public static class EnsureUniversalRenderPipeline
    {
        public static void Run()
        {
            if (GraphicsSettings.defaultRenderPipeline is UniversalRenderPipelineAsset)
            {
                return;
            }

            const string settingsDirectory = "Assets/Settings";
            if (!AssetDatabase.IsValidFolder(settingsDirectory))
            {
                AssetDatabase.CreateFolder("Assets", "Settings");
            }

            var rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
            AssetDatabase.CreateAsset(rendererData, settingsDirectory + "/VisualTierRenderer.asset");

            // The golden capture's renderer (unity-mmd/Assets/Settings/PC_Renderer.asset) has
            // Mmd.Rendering.Universal.MmdOutlineRendererFeature in its feature list -- that is
            // what actually draws the MMD outline pass. Mirror it here so the outline gate has
            // something to measure.
            var outlineFeature = ScriptableObject.CreateInstance<Mmd.Rendering.Universal.MmdOutlineRendererFeature>();
            outlineFeature.name = "MmdOutlineRendererFeature";
            AssetDatabase.AddObjectToAsset(outlineFeature, rendererData);
            rendererData.rendererFeatures.Add(outlineFeature);
            EditorUtility.SetDirty(rendererData);

            UniversalRenderPipelineAsset pipelineAsset = UniversalRenderPipelineAsset.Create(rendererData);
            AssetDatabase.CreateAsset(pipelineAsset, settingsDirectory + "/VisualTierRenderPipelineAsset.asset");

            GraphicsSettings.defaultRenderPipeline = pipelineAsset;
            QualitySettings.renderPipeline = pipelineAsset;

            EditorUtility.SetDirty(pipelineAsset);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (!(GraphicsSettings.defaultRenderPipeline is UniversalRenderPipelineAsset))
            {
                Debug.LogError("EnsureUniversalRenderPipeline: failed to assign a UniversalRenderPipelineAsset as the default render pipeline.");
            }
        }
    }
}
'@
    [IO.File]::WriteAllText((Join-Path $bootstrapEditorDir "VisualShadingTierBootstrap.cs"), $bootstrapScript)

    $rpBootstrapLog = Join-Path $resultsRoot "bootstrap-urp.log"
    Invoke-UnityProcess -Arguments @(
        "-batchmode", "-quit", "-projectPath", $ProjectPath,
        "-executeMethod", "VisualShadingTierBootstrap.EnsureUniversalRenderPipeline.Run",
        "-logFile", $rpBootstrapLog
    ) | Out-Null
    $rpAsset = Join-Path $ProjectPath "Assets\Settings\VisualTierRenderPipelineAsset.asset"
    if (-not (Wait-ForFileToSettle -Path $rpAsset)) {
        throw "Failed to assign a Universal Render Pipeline asset in the bootstrap project. log=$rpBootstrapLog"
    }
}

$testName = "Mmd.Tests.MmdGeneratedPmxVisualParityTests.ToonRampOpaqueOutline_IsDeterministicAndMatchesGolden"
if (-not [string]::IsNullOrEmpty($TestFilter)) {
    $testName = $TestFilter
    if (-not [string]::IsNullOrEmpty($WarmupTestFilter)) {
        $testName = "$WarmupTestFilter|$TestFilter"
    }
}
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
    [xml] $xml = Get-Content -LiteralPath $results -Raw
    $testRun = $xml.SelectSingleNode("//test-run")
    if ($null -eq $testRun) {
        throw "$Name results have no test-run root. results=$results"
    }
    $failed = 0
    $passed = 0
    [void][int]::TryParse([string] $testRun.GetAttribute("failed"), [ref] $failed)
    [void][int]::TryParse([string] $testRun.GetAttribute("passed"), [ref] $passed)

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
    if (-not [string]::IsNullOrEmpty($TestFilter)) {
        Invoke-VisualTierRun -Name $RunName -Perturb $false -ExpectFailure $false
    }
    else {
        Invoke-VisualTierRun -Name "green-before" -Perturb $false -ExpectFailure $false
        if (-not $SkipPerturbationProof) {
            Invoke-VisualTierRun -Name "red-perturbed" -Perturb $true -ExpectFailure $true
            Invoke-VisualTierRun -Name "green-after" -Perturb $false -ExpectFailure $false
        }
    }
}
finally {
    Remove-Item Env:YMU_VISUAL_TIER_PERTURB -ErrorAction SilentlyContinue
    Remove-Item Env:YMU_VISUAL_PARITY_ARTIFACTS -ErrorAction SilentlyContinue
}

Write-Host "Visual shading tier passed. artifacts=$runRoot"
