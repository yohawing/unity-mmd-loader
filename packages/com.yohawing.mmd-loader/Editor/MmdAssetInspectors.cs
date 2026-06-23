#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Mmd.Parser;
using Mmd.UnityIntegration;

namespace Mmd.Editor
{
    [CustomEditor(typeof(MmdPmxAsset))]
    public sealed class MmdPmxAssetEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            if (target is not MmdPmxAsset asset)
            {
                return;
            }

            MmdAssetInspectorUtility.DrawSummary("PMX Asset", asset.SourceId, asset.SourcePath, asset.ByteLength);
            MmdAssetInspectorUtility.DrawImportScaleSummary(asset.ImportScale);
            MmdAssetInspectorUtility.DrawImportSettingsSummary(
                asset.ModelPreset,
                asset.MeshGenerationMode,
                asset.MaterialTexturePolicy,
                asset.ShaderPreset);
            MmdAssetInspectorUtility.DrawModelSummary(asset);
            MmdAssetInspectorUtility.DrawParseSummary(asset);
            MmdAssetInspectorUtility.DrawMaterialSummary(asset);
            MmdAssetInspectorUtility.DrawOutlineSummary(asset);
            MmdAssetInspectorUtility.DrawHierarchyReadinessSummary(asset);
            MmdAssetInspectorUtility.DrawPhysicsSummary(asset);
            MmdAssetInspectorUtility.DrawHumanoidSummary(asset);
            MmdAssetInspectorUtility.DrawAnimationTimelineSummary(asset);

            using (new EditorGUI.DisabledScope(asset.ByteLength == 0))
            {
                if (GUILayout.Button("Load PMX Into Scene"))
                {
                    MmdEditorPmxLoader.LoadPmxIntoScene(asset);
                }

                if (GUILayout.Button("Create Prefab from PMX"))
                {
                    MmdPmxPrefabExporter.CreatePrefabWithFeedback(asset);
                }

                if (GUILayout.Button("Create Humanoid Setup Asset"))
                {
                    MmdHumanoidSetupAsset setup = MmdHumanoidSetupAssetBuilder.CreateHumanoidSetupAsset(
                        asset,
                        MmdHumanoidSetupAssetBuilder.GetDefaultSetupAssetPath(asset));
                    Selection.activeObject = setup;
                    EditorGUIUtility.PingObject(setup);
                }
            }
        }
    }

    [CustomEditor(typeof(MmdVmdAsset))]
    public sealed class MmdVmdAssetEditor : UnityEditor.Editor
    {
        private IReadOnlyList<string>? lastStructuralDiagnostics;

        // Non-persistent preview references for Humanoid Clip Readiness (per Inspector lifetime).
        // Selection/inspector display must not trigger VMD parse; planner now uses import cache only.
        private MmdPmxAsset? previewPmxAsset;
        private MmdHumanoidSetupAsset? previewSetupAsset;

        public override void OnInspectorGUI()
        {
            if (target is not MmdVmdAsset asset)
            {
                return;
            }

            MmdAssetInspectorUtility.DrawSummary("VMD Asset", asset.SourceId, asset.SourcePath, asset.ByteLength);

            if (asset.ByteLength > 0)
            {
                EditorGUILayout.Space();
                MmdAssetInspectorUtility.DrawVmdMotionSummary(asset);

                EditorGUILayout.Space();
                // Initialize from import-time cached structural diagnostics.
                // Do NOT call LoadMotion / Refresh on mere asset selection or inspector open.
                if (lastStructuralDiagnostics == null)
                {
                    lastStructuralDiagnostics = asset.StructuralDiagnostics;
                }

                MmdAssetInspectorUtility.DrawVmdStructuralDiagnostics(lastStructuralDiagnostics);

                EditorGUILayout.Space();
                MmdAssetInspectorUtility.DrawVmdTimelineReadiness(asset);

                EditorGUILayout.Space();
                MmdAssetInspectorUtility.DrawVmdHumanoidClipReadinessSection(
                    asset,
                    ref previewPmxAsset,
                    ref previewSetupAsset);

                EditorGUILayout.Space();
                using (new EditorGUI.DisabledScope(false))
                {
                    if (GUILayout.Button("Run VMD Diagnostics"))
                    {
                        RefreshDiagnostics(asset, repaint: true);
                    }
                }
            }
            else
            {
                EditorGUILayout.HelpBox("VMD asset has no imported bytes. Reimport the source .vmd file.", MessageType.Warning);
            }
        }

        private void RefreshDiagnostics(MmdVmdAsset asset, bool repaint)
        {
            // Only path that performs full LoadMotion for structural revalidation.
            // Called exclusively from the "Run VMD Diagnostics" button.
            try
            {
                MmdMotionDefinition motion = asset.LoadMotion();
                lastStructuralDiagnostics = MmdMotionValidator.ValidateStructuralMotion(motion);
            }
            catch (System.Exception ex)
            {
                lastStructuralDiagnostics = new[]
                {
                    "Failed to load or validate VMD motion: " + ex.Message
                };
            }

            if (repaint)
            {
                Repaint();
            }
        }
    }

    [CustomEditor(typeof(MmdHumanoidSetupAsset))]
    public sealed class MmdHumanoidSetupAssetEditor : UnityEditor.Editor
    {
        private bool showMappingEntries;
        private MmdVmdAsset? vmdAsset;
        private float clipFrameRate = 30.0f;
        private int clipStartFrame;
        private int clipEndFrame = -1;

        public override void OnInspectorGUI()
        {
            if (target is not MmdHumanoidSetupAsset asset)
            {
                return;
            }

            // EditMode tests call this directly without an InspectorWindow GUI event.
            if (Event.current == null)
            {
                return;
            }

            EditorGUILayout.LabelField("Humanoid Setup Asset", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.ObjectField("PMX Asset", asset.PmxAsset, typeof(MmdPmxAsset), allowSceneObjects: false);
                EditorGUILayout.EnumPopup("Setup Preset", asset.SetupPreset);
                EditorGUILayout.IntField("PMX Bones", asset.PmxBoneCount);
                EditorGUILayout.TextField("Mapping Readiness", asset.MappingReadiness);
                EditorGUILayout.IntField("Required Bones", asset.RequiredMappedBoneCount);
                EditorGUILayout.IntField("Optional Bones", asset.OptionalMappedBoneCount);
                EditorGUILayout.IntField("Missing Required", asset.MissingRequiredBoneCount);
                EditorGUILayout.IntField("Ambiguous Mappings", asset.AmbiguousMappingCount);
                EditorGUILayout.IntField("Ignored Helpers", asset.IgnoredHelperBoneCount);
                EditorGUILayout.TextField("Native Playback Impact", asset.NativePlaybackImpact);
                foreach (string diagnostic in asset.MappingDiagnostics)
                {
                    EditorGUILayout.TextField("Mapping Diagnostic", diagnostic);
                }
            }

            // ── Read-only mapping entries foldout ──
            IReadOnlyList<MmdSerializableBoneMappingEntry> entries = asset.MappingEntries;
            showMappingEntries = EditorGUILayout.Foldout(
                showMappingEntries,
                $"Mapping Entries ({entries.Count})",
                EditorStyles.foldout);
            if (showMappingEntries && entries.Count > 0)
            {
                using (new EditorGUI.DisabledScope(true))
                using (new EditorGUILayout.VerticalScope(GUI.skin.box))
                {
                    // Compact column header
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("HumanBone", EditorStyles.miniBoldLabel, GUILayout.Width(140));
                    EditorGUILayout.LabelField("MMD Bone", EditorStyles.miniBoldLabel, GUILayout.Width(100));
                    EditorGUILayout.LabelField("#", EditorStyles.miniBoldLabel, GUILayout.Width(24));
                    EditorGUILayout.LabelField("Req", EditorStyles.miniBoldLabel, GUILayout.Width(30));
                    EditorGUILayout.LabelField("Category", EditorStyles.miniBoldLabel, GUILayout.Width(70));
                    EditorGUILayout.EndHorizontal();

                    foreach (MmdSerializableBoneMappingEntry entry in entries)
                    {
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.EnumPopup(entry.HumanBone, GUILayout.Width(140));
                        EditorGUILayout.TextField(entry.MmdBoneName, GUILayout.Width(100));
                        EditorGUILayout.IntField(entry.MmdBoneIndex, GUILayout.Width(24));
                        EditorGUILayout.Toggle(entry.Required, GUILayout.Width(30));
                        EditorGUILayout.TextField(entry.Category, GUILayout.Width(70));
                        EditorGUILayout.EndHorizontal();
                    }
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Humanoid Clip Conversion", EditorStyles.boldLabel);
            vmdAsset = (MmdVmdAsset?)EditorGUILayout.ObjectField("VMD Asset", vmdAsset, typeof(MmdVmdAsset), false);
            clipFrameRate = EditorGUILayout.FloatField("Frame Rate", clipFrameRate);
            clipStartFrame = EditorGUILayout.IntField("Start Frame", clipStartFrame);
            clipEndFrame = EditorGUILayout.IntField("End Frame", clipEndFrame);

            bool hasValidInputs = asset.PmxAsset != null && vmdAsset != null;
            MmdHumanoidClipConversionPlan conversionPlan = MmdHumanoidClipConversionPlanner.AnalyzePrerequisites(
                asset.PmxAsset,
                vmdAsset,
                asset);
            bool canCreateHumanoidClip = hasValidInputs && conversionPlan.CanCreateClipNow;

            using (new EditorGUI.DisabledScope(!canCreateHumanoidClip))
            {
                if (GUILayout.Button("Create Humanoid AnimationClip Asset"))
                {
                    int? effectiveEndFrame = clipEndFrame < 0 ? null : clipEndFrame;
                    MmdHumanoidClipConversionWriterResult result =
                        MmdHumanoidClipConversionWriter.CreateHumanoidAnimationClipAsset(
                            asset.PmxAsset!,
                            vmdAsset!,
                            asset,
                            clipFrameRate,
                            clipStartFrame,
                            effectiveEndFrame);

                    if (result.Clip == null)
                    {
                        LogConversionFailureDiagnostics(result);
                    }
                    else
                    {
                        Selection.activeObject = result.Clip;
                        EditorGUIUtility.PingObject(result.Clip);
                        Debug.Log("[MmdHumanoidSetupAsset] Created Humanoid AnimationClip: "
                                  + AssetDatabase.GetAssetPath(result.Clip));
                    }
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Proxy Rig & Avatar", EditorStyles.boldLabel);
            if (asset.MappingReadiness == MmdHumanoidSetupAsset.ReadyReadiness ||
                asset.RequiredMappedBoneCount > 0)
            {
                if (GUILayout.Button("Create Hidden Proxy Rig"))
                {
                    CreateHiddenProxyRig(asset);
                }

                if (GUILayout.Button("Create Proxy Rig and Build Avatar"))
                {
                    CreateHiddenProxyRigAndBuildAvatar(asset);
                }
            }
            else
            {
                using (new EditorGUI.DisabledScope(true))
                {
                    GUILayout.Button("Create Hidden Proxy Rig (requires mapped bones)");
                    GUILayout.Button("Build Avatar (requires proxy rig with mapped bones)");
                }
            }

            EditorGUILayout.HelpBox(
                "'Create Hidden Proxy Rig' creates only the Transform hierarchy. " +
                "'Build Avatar' additionally creates a Unity Avatar via AvatarBuilder " +
                "but does NOT assign it to an Animator. Diagnostics are logged to Console.",
                MessageType.Info);
        }

        private static void LogConversionFailureDiagnostics(MmdHumanoidClipConversionWriterResult result)
        {
            Debug.LogWarning("[MmdHumanoidSetupAsset] Failed to create Humanoid AnimationClip asset.");
            foreach (string diagnostic in result.Diagnostics)
            {
                Debug.LogWarning("[MmdHumanoidSetupAsset] " + diagnostic);
            }
        }

        private static void CreateHiddenProxyRig(MmdHumanoidSetupAsset asset)
        {
            if (asset.PmxAsset == null)
                return;

            try
            {
                MmdHumanoidProxyRigResult result = MmdHumanoidProxyRigFactory.CreateProxyRig(asset.PmxAsset);

                if (result.ProxyRoot != null)
                {
                    Undo.RegisterCreatedObjectUndo(result.ProxyRoot, "Create Hidden Proxy Rig");
                    Selection.activeGameObject = result.ProxyRoot;
                    Debug.Log("[MmdHumanoidSetupAsset] Created hidden proxy rig with " +
                              result.BoneMap.Count + " bone transforms, readiness=" +
                              result.Readiness);
                }
                else
                {
                    Debug.LogWarning("[MmdHumanoidSetupAsset] Failed to create proxy rig: " +
                                     string.Join("; ", result.Diagnostics));
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError("[MmdHumanoidSetupAsset] Failed to create proxy rig: " + ex.Message);
            }
        }

        private static void CreateHiddenProxyRigAndBuildAvatar(MmdHumanoidSetupAsset asset)
        {
            if (asset.PmxAsset == null)
            {
                Debug.LogError("[MmdHumanoidSetupAsset] Cannot build Avatar: PMX asset is null.");
                return;
            }

            GameObject? proxyRoot = null;
            try
            {
                MmdHumanoidProxyRigResult proxyResult = MmdHumanoidProxyRigFactory.CreateProxyRig(asset.PmxAsset);

                if (proxyResult.ProxyRoot == null)
                {
                    Debug.LogWarning("[MmdHumanoidSetupAsset] Cannot build Avatar: " +
                                     "proxy rig creation failed. readiness=" + proxyResult.Readiness);
                    foreach (string d in proxyResult.Diagnostics)
                        Debug.Log("[MmdHumanoidSetupAsset]   " + d);
                    return;
                }

                proxyRoot = proxyResult.ProxyRoot;
                Undo.RegisterCreatedObjectUndo(proxyRoot, "Create Proxy Rig and Build Avatar");
                Selection.activeGameObject = proxyRoot;

                MmdHumanoidAvatarBuildResult avatarResult = MmdHumanoidProxyRigFactory.BuildAvatar(proxyResult);

                // Log diagnostics to Console.
                Debug.Log("[MmdHumanoidSetupAsset] Avatar build complete. " +
                          "Avatar=" + (avatarResult.Avatar != null ? avatarResult.Avatar.name : "null") +
                          " isValid=" + (avatarResult.Avatar?.isValid ?? false) +
                          " isHuman=" + (avatarResult.Avatar?.isHuman ?? false));
                foreach (string d in avatarResult.Diagnostics)
                    Debug.Log("[MmdHumanoidSetupAsset]   " + d);

                // Do NOT assign to any Animator component.
            }
            catch (System.Exception ex)
            {
                Debug.LogError("[MmdHumanoidSetupAsset] Failed to build Avatar: " +
                               ex.GetType().Name + ": " + ex.Message);
                if (ex.InnerException != null)
                    Debug.LogError("[MmdHumanoidSetupAsset] Inner: " + ex.InnerException.Message);
            }
        }
    }

    internal static class MmdAssetInspectorUtility
    {
        public static void DrawSummary(string title, string sourceId, string sourcePath, int byteLength)
        {
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.TextField("Source Id", sourceId);
                EditorGUILayout.TextField("Source Path", sourcePath);
                EditorGUILayout.IntField("Bytes", byteLength);
            }
        }

        public static void DrawImportScaleSummary(float importScale)
        {
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.FloatField("Import Scale (Summary)", importScale);
            }
        }

        public static void DrawImportSettingsSummary(
            string modelPreset,
            string meshGenerationMode,
            string materialTexturePolicy,
            string shaderPreset)
        {
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.TextField("Model Preset (Summary)", modelPreset);
                EditorGUILayout.TextField("Mesh Generation (Summary)", meshGenerationMode);
                EditorGUILayout.TextField("Texture Policy (Summary)", materialTexturePolicy);
                EditorGUILayout.TextField("Shader Preset (Summary)", shaderPreset);
            }
        }

        public static void DrawModelSummary(MmdPmxAsset asset)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Model Summary", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.TextField("Model Name", asset.ModelName);
                if (!string.IsNullOrWhiteSpace(asset.ModelEnglishName))
                {
                    EditorGUILayout.TextField("English Name", asset.ModelEnglishName);
                }

                if (!string.IsNullOrWhiteSpace(asset.ModelComment))
                {
                    EditorGUILayout.LabelField("Comment");
                    EditorGUILayout.TextArea(asset.ModelComment, GUILayout.MinHeight(40));
                }

                if (!string.IsNullOrWhiteSpace(asset.ModelEnglishComment))
                {
                    EditorGUILayout.LabelField("English Comment");
                    EditorGUILayout.TextArea(asset.ModelEnglishComment, GUILayout.MinHeight(40));
                }

                EditorGUILayout.Vector3Field("MMD Bounds Min", asset.BoundsMin);
                EditorGUILayout.Vector3Field("MMD Bounds Max", asset.BoundsMax);
                EditorGUILayout.Vector3Field("MMD Bounds Size", asset.BoundsSize);
                EditorGUILayout.TextField("Unity Conversion", "[-x, y, -z] at instantiation");
            }

            EditorGUILayout.HelpBox(
                "Bounds are cached in unscaled MMD model space. Import Scale and MMD-to-Unity basis conversion are applied only at Unity instantiation/export boundaries.",
                MessageType.Info);
        }

        public static void DrawParseSummary(MmdPmxAsset asset)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Import Diagnostics Summary", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.EnumPopup("Status", asset.ImportSummaryStatus);
                EditorGUILayout.IntField("Vertices", asset.VertexCount);
                EditorGUILayout.IntField("Indices", asset.IndexCount);
                EditorGUILayout.IntField("Bones", asset.BoneCount);
                EditorGUILayout.IntField("Morphs", asset.MorphCount);
                EditorGUILayout.IntField("Materials", asset.MaterialCount);
                EditorGUILayout.IntField("IK", asset.IkCount);
                EditorGUILayout.IntField("Rigidbodies", asset.RigidbodyCount);
                EditorGUILayout.IntField("Joints", asset.JointCount);
            }
        }

        public static void DrawMaterialSummary(MmdPmxAsset asset)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Material Reference Summary", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.IntField("Diffuse Texture Refs", asset.DiffuseTextureReferenceCount);
                EditorGUILayout.IntField("Sphere Texture Refs", asset.SphereTextureReferenceCount);
                EditorGUILayout.IntField("Toon Texture Refs", asset.ToonTextureReferenceCount);
                EditorGUILayout.IntField("Resolved Project Texture Refs", asset.ResolvedProjectTextureReferenceCount);
                EditorGUILayout.IntField("Missing Project Texture Refs", asset.MissingProjectTextureReferenceCount);
                if (asset.MissingProjectTextureReferenceCount > 0)
                {
                    EditorGUILayout.TextField("First Missing Texture", asset.MissingProjectTextureReferenceSample);
                }

                EditorGUILayout.IntField("Transparent Materials", asset.TransparentMaterialCount);
                EditorGUILayout.IntField("Edge Materials", asset.EdgeMaterialCount);
            }
        }

        public static void DrawOutlineSummary(MmdPmxAsset asset)
        {
            MmdOutlineReadiness readiness = GetOutlineReadiness(asset);
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Outline Readiness", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.IntField("Outline-Eligible Materials", readiness.OutlineEligibleMaterialCount);
                EditorGUILayout.TextField("Runtime Path", readiness.RuntimePath);
                EditorGUILayout.TextField("Release Mode", readiness.ReleaseMode);
                EditorGUILayout.TextField("Final Visual Parity", readiness.FinalVisualParity);
            }

            EditorGUILayout.HelpBox(
                "Outline readiness is a cached PMX material summary. It does not generate outline proxy meshes, update visual baselines, or claim rayMMD-compatible parity.",
                MessageType.Info);
        }

        internal static MmdOutlineReadiness GetOutlineReadiness(MmdPmxAsset? asset)
        {
            if (asset == null || asset.EdgeMaterialCount <= 0)
            {
                return new MmdOutlineReadiness(
                    outlineEligibleMaterialCount: 0,
                    runtimePath: "No PMX draw-edge materials",
                    releaseMode: "Not needed",
                    finalVisualParity: MmdOutlineReadiness.NotClaimed);
            }

            string runtimePath = string.Equals(asset.ShaderPreset, "MmdBasicUrpToon", System.StringComparison.Ordinal)
                ? "MmdBasicUrpToon outline pass"
                : "Shader preset summary only";
            return new MmdOutlineReadiness(
                asset.EdgeMaterialCount,
                runtimePath,
                "Back-face mesh-normal extrusion",
                MmdOutlineReadiness.NotClaimed);
        }

        public static void DrawHierarchyReadinessSummary(MmdPmxAsset asset)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Hierarchy Readiness", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.EnumPopup("Hierarchy", asset.HierarchyReadiness);
                EditorGUILayout.EnumPopup("Renderer", asset.RendererReadiness);
                EditorGUILayout.EnumPopup("Bone Binding", asset.BoneBindingReadiness);
                EditorGUILayout.TextField("Hierarchy Diagnostic", asset.HierarchyReadinessDiagnostic);
                EditorGUILayout.TextField("Renderer Diagnostic", asset.RendererReadinessDiagnostic);
                EditorGUILayout.TextField("Bone Binding Diagnostic", asset.BoneBindingReadinessDiagnostic);
            }
        }

        public static void DrawPhysicsSummary(MmdPmxAsset asset)
        {
            MmdScaleAwarePhysicsReadiness readiness = GetScaleAwarePhysicsReadiness(asset);
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Physics Summary", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.IntField("Rigidbodies", asset.RigidbodyCount);
                EditorGUILayout.IntField("Joints", asset.JointCount);
                EditorGUILayout.TextField("PMX Physics Descriptors", asset.RigidbodyCount > 0 ? "Present" : "None");
                EditorGUILayout.FloatField("Import Scale", readiness.ImportScale);
                EditorGUILayout.TextField("Gravity Policy", readiness.GravityPolicy);
                EditorGUILayout.TextField("Backend Readback Space", readiness.BackendReadbackSpace);
                EditorGUILayout.TextField("Scale-Aware Handoff", readiness.ScaleAwareHandoffReadiness);
                EditorGUILayout.TextField("Required Smoke", readiness.RequiredSmoke);
            }

            EditorGUILayout.HelpBox(
                "Import Scale is stored as import metadata. Runtime playback and live physics apply scale at Unity/MMD boundaries while cached PMX summaries stay in MMD space.",
                MessageType.Info);
        }

        internal static MmdScaleAwarePhysicsReadiness GetScaleAwarePhysicsReadiness(MmdPmxAsset? asset)
        {
            if (asset == null)
            {
                return new MmdScaleAwarePhysicsReadiness(
                    importScale: MmdPmxAsset.DefaultImportScale,
                    hasPhysicsDescriptors: false,
                    gravityPolicy: "unavailable-no-pmx-asset",
                    backendReadbackSpace: MmdScaleAwarePhysicsReadiness.MmdSpaceReadback,
                    scaleAwareHandoffReadiness: MmdScaleAwarePhysicsReadiness.ScaleAwareHandoffReady,
                    requiredSmoke: MmdScaleAwarePhysicsReadiness.ScaleAwareSmokeCovered);
            }

            bool hasPhysicsDescriptors = asset.RigidbodyCount > 0 || asset.JointCount > 0;

            return new MmdScaleAwarePhysicsReadiness(
                asset.ImportScale,
                hasPhysicsDescriptors,
                "scale-aware-mmd-gravity-98",
                MmdScaleAwarePhysicsReadiness.MmdSpaceReadback,
                MmdScaleAwarePhysicsReadiness.ScaleAwareHandoffReady,
                MmdScaleAwarePhysicsReadiness.ScaleAwareSmokeCovered);
        }

        public static void DrawHumanoidSummary(MmdPmxAsset asset)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Humanoid Summary", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.IntField("PMX Bones", asset.BoneCount);
                EditorGUILayout.TextField("Mapping Readiness", "Not evaluated");
                EditorGUILayout.TextField("Humanoid Setup Asset", "Explicit asset workflow");
                EditorGUILayout.TextField("Native Playback Impact", "None");
            }

            EditorGUILayout.HelpBox(
                "Create Humanoid Setup Asset stores PMX source and H1 readiness metadata only. It does not create Avatar, proxy rig, or mapping assets.",
                MessageType.Info);
        }

        public static void DrawAnimationTimelineSummary(MmdPmxAsset asset)
        {
            MmdAnimationTimelineReadiness readiness = GetAnimationTimelineReadiness(asset);
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Animation / Timeline", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.TextField("Timeline Binding Target", readiness.TimelineBindingTarget);
                EditorGUILayout.TextField("VMD Drop Readiness", readiness.VmdDropReadiness);
                EditorGUILayout.TextField("Playback Source", readiness.PlaybackSource);
            }

            EditorGUILayout.HelpBox(
                "PMX import does not auto-create scene playback objects. Use Load PMX Into Scene to create a controller root that can receive VMD clips or playback sources.",
                MessageType.Info);
        }

        internal static MmdAnimationTimelineReadiness GetAnimationTimelineReadiness(MmdPmxAsset? asset)
        {
            if (asset == null || asset.ByteLength <= 0)
            {
                return new MmdAnimationTimelineReadiness(
                    timelineBindingTarget: "Unavailable",
                    vmdDropReadiness: "Unavailable",
                    playbackSource: "Scene component or Timeline clip, not PMX import side effect");
            }

            return new MmdAnimationTimelineReadiness(
                timelineBindingTarget: "Ready after Load PMX Into Scene",
                vmdDropReadiness: "Requires generated scene controller",
                playbackSource: "Scene component or Timeline clip, not PMX import side effect");
        }

        public static void DrawVmdMotionSummary(MmdVmdAsset asset)
        {
            EditorGUILayout.LabelField("VMD Motion Summary", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(true))
            {
                try
                {
                    MmdVmdMotionSummary summary = GetVmdMotionSummary(asset);
                    EditorGUILayout.TextField("Target Model Name", summary.TargetModelName);
                    EditorGUILayout.IntField("Max Frame", summary.MaxFrame);
                    EditorGUILayout.IntField("Bone Keyframes", summary.BoneKeyframeCount);
                    EditorGUILayout.IntField("Morph Keyframes", summary.MorphKeyframeCount);
                    EditorGUILayout.IntField("Model Keyframes", summary.ModelKeyframeCount);
                    EditorGUILayout.IntField("Constraint States", summary.ConstraintStateCount);
                    EditorGUILayout.IntField("Camera Keyframes", summary.CameraKeyframeCount);
                    EditorGUILayout.IntField("Light Keyframes", summary.LightKeyframeCount);
                    EditorGUILayout.IntField("Self-Shadow Keyframes", summary.SelfShadowKeyframeCount);
                }
                catch (System.Exception ex)
                {
                    EditorGUILayout.HelpBox(
                        "Failed to get VMD motion summary: " + ex.Message,
                        MessageType.Error);
                }
            }
        }

        internal static MmdVmdMotionSummary GetVmdMotionSummary(MmdVmdAsset asset)
        {
            if (asset == null)
            {
                throw new ArgumentNullException(nameof(asset));
            }

            if (asset.ByteLength == 0)
            {
                throw new InvalidOperationException("VMD asset has no imported bytes.");
            }

            // Read exclusively from import-time cached summary. Never call LoadMotion()
            // here: selection or summary display must not trigger full VMD parse.
            return new MmdVmdMotionSummary(
                asset.TargetModelName,
                asset.MaxFrame,
                asset.BoneKeyframeCount,
                asset.MorphKeyframeCount,
                asset.ModelKeyframeCount,
                asset.ConstraintStateCount,
                asset.CameraKeyframeCount,
                asset.LightKeyframeCount,
                asset.SelfShadowKeyframeCount);
        }

        public static void DrawVmdStructuralDiagnostics(IReadOnlyList<string>? diagnostics)
        {
            EditorGUILayout.LabelField("Structural Validation", EditorStyles.boldLabel);
            if (diagnostics == null)
            {
                EditorGUILayout.HelpBox(
                    "Run VMD Diagnostics to check structural motion validity.",
                    MessageType.Info);
                return;
            }

            if (diagnostics.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "No structural issues found. VMD motion is valid.",
                    MessageType.Info);
                return;
            }

            using (new EditorGUI.DisabledScope(true))
            {
                for (int i = 0; i < diagnostics.Count; i++)
                {
                    EditorGUILayout.TextField("Issue " + (i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture), diagnostics[i]);
                }
            }

            EditorGUILayout.HelpBox(
                diagnostics.Count == 1
                    ? "1 structural issue found."
                    : diagnostics.Count.ToString(System.Globalization.CultureInfo.InvariantCulture) + " structural issues found.",
                MessageType.Warning);
        }

        internal static IReadOnlyList<string> GetVmdStructuralDiagnostics(MmdVmdAsset asset)
        {
            // Explicit revalidation path (calls LoadMotion + Validate). Intended for "Run VMD Diagnostics"
            // and focused tests that deliberately exercise full parse. Selection / summary display
            // paths must not invoke this.
            MmdMotionDefinition motion = asset.LoadMotion();
            return MmdMotionValidator.ValidateStructuralMotion(motion);
        }

        public static void DrawVmdTimelineReadiness(MmdVmdAsset asset)
        {
            if (asset == null)
            {
                return;
            }

            MmdVmdTimelineReadiness readiness = GetVmdTimelineReadiness(asset);
            EditorGUILayout.LabelField("Timeline Readiness", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.IntField("Max Frame (Duration)", readiness.MaxFrame);
                EditorGUILayout.TextField("Scene Motion", readiness.SceneMotionStatus);
                EditorGUILayout.TextField("Clip Creation", readiness.ClipCreationRequirement);
            }
            // Compact diagnostic rows only. No long normal-state HelpBox per UI contract.
            // Raw camera/light/self-shadow counts are already shown in VMD Motion Summary above.
        }

        internal static MmdVmdTimelineReadiness GetVmdTimelineReadiness(MmdVmdAsset? asset)
        {
            if (asset == null || asset.ByteLength <= 0)
            {
                return new MmdVmdTimelineReadiness(
                    maxFrame: 0,
                    cameraKeyframeCount: 0,
                    lightKeyframeCount: 0,
                    selfShadowKeyframeCount: 0,
                    clipDurationSource: "Unavailable",
                    sceneMotionStatus: "No VMD asset",
                    clipCreationRequirement: "PMX model source and Timeline are required for VMD Clip creation.");
            }

            // Read EXCLUSIVELY from import-time cached summary on MmdVmdAsset.
            // Never call LoadMotion(), parser, or any full parse path here.
            // This guarantees Inspector selection / readiness display does not parse bytes.
            int cam = asset.CameraKeyframeCount;
            int lit = asset.LightKeyframeCount;
            int shd = asset.SelfShadowKeyframeCount;
            int mf = asset.MaxFrame;

            string sceneStatus = (cam > 0 || lit > 0)
                ? string.Format(System.Globalization.CultureInfo.InvariantCulture,
                    "Present (camera:{0}, light:{1}, selfShadow:{2})", cam, lit, shd)
                : shd > 0
                    ? string.Format(System.Globalization.CultureInfo.InvariantCulture,
                        "None (camera/light only; selfShadow:{0} deferred)", shd)
                    : "None (model motion only)";

            string durationSrc = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "Cached VMD MaxFrame ({0})", mf);

            return new MmdVmdTimelineReadiness(
                mf,
                cam,
                lit,
                shd,
                durationSrc,
                sceneStatus,
                "PMX model source and Timeline are required for VMD Clip creation.");
        }

        // --- Humanoid Clip Readiness preview (VMD Inspector, cache-only, non-persistent) ---

        public static void DrawVmdHumanoidClipReadinessSection(
            MmdVmdAsset vmdAsset,
            ref MmdPmxAsset? previewPmx,
            ref MmdHumanoidSetupAsset? previewSetup)
        {
            if (vmdAsset == null)
            {
                return;
            }

            EditorGUILayout.LabelField("Humanoid Clip Readiness (preview)", EditorStyles.boldLabel);

            previewPmx = (MmdPmxAsset?)EditorGUILayout.ObjectField(
                "PMX Asset",
                previewPmx,
                typeof(MmdPmxAsset),
                allowSceneObjects: false);

            previewSetup = (MmdHumanoidSetupAsset?)EditorGUILayout.ObjectField(
                "Humanoid Setup Asset",
                previewSetup,
                typeof(MmdHumanoidSetupAsset),
                allowSceneObjects: false);

            MmdHumanoidClipConversionPlan plan = ComputeHumanoidClipReadinessForVmd(vmdAsset, previewPmx, previewSetup);

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.TextField("Status", plan.Readiness);
            }

            // Short actionable diagnostics only for not-ready states. No long normal-state HelpBoxes.
            if (!plan.PrerequisitesReady)
            {
                string compact = FormatCompactVmdHumanoidIssues(plan);
                if (!string.IsNullOrEmpty(compact))
                {
                    EditorGUILayout.HelpBox(compact, MessageType.Info);
                }
            }
        }

        internal static MmdHumanoidClipConversionPlan ComputeHumanoidClipReadinessForVmd(
            MmdVmdAsset vmdAsset,
            MmdPmxAsset? pmxAsset,
            MmdHumanoidSetupAsset? setupAsset)
        {
            // Delegates to planner. Planner AnalyzePrerequisites uses VMD import cache only (no LoadMotion).
            // This helper is testable without IMGUI and without triggering full VMD parse.
            if (vmdAsset == null)
            {
                // Planner handles null vmd; return its not-ready plan for consistency.
                return MmdHumanoidClipConversionPlanner.AnalyzePrerequisites(pmxAsset, null, setupAsset);
            }
            return MmdHumanoidClipConversionPlanner.AnalyzePrerequisites(pmxAsset, vmdAsset, setupAsset);
        }

        internal static string FormatCompactVmdHumanoidIssues(MmdHumanoidClipConversionPlan plan)
        {
            if (plan == null || plan.PrerequisitesReady)
            {
                return string.Empty;
            }
            var diags = plan.Diagnostics;
            if (diags == null || diags.Count == 0)
            {
                return "Humanoid Clip prerequisites not ready.";
            }
            // Keep compact: surface first 1-2 actionable items (PMX missing, setup mismatch, VMD cache fail, hierarchy not ready, etc.).
            int take = System.Math.Min(2, diags.Count);
            return string.Join(" | ", diags.Take(take));
        }
    }

    internal readonly struct MmdAnimationTimelineReadiness
    {
        public MmdAnimationTimelineReadiness(
            string timelineBindingTarget,
            string vmdDropReadiness,
            string playbackSource)
        {
            TimelineBindingTarget = timelineBindingTarget;
            VmdDropReadiness = vmdDropReadiness;
            PlaybackSource = playbackSource;
        }

        public string TimelineBindingTarget { get; }

        public string VmdDropReadiness { get; }

        public string PlaybackSource { get; }
    }

    internal readonly struct MmdOutlineReadiness
    {
        public const string NotClaimed = "Not claimed";

        public MmdOutlineReadiness(
            int outlineEligibleMaterialCount,
            string runtimePath,
            string releaseMode,
            string finalVisualParity)
        {
            OutlineEligibleMaterialCount = System.Math.Max(0, outlineEligibleMaterialCount);
            RuntimePath = string.IsNullOrWhiteSpace(runtimePath) ? "Unknown" : runtimePath;
            ReleaseMode = string.IsNullOrWhiteSpace(releaseMode) ? "Unknown" : releaseMode;
            FinalVisualParity = string.IsNullOrWhiteSpace(finalVisualParity) ? NotClaimed : finalVisualParity;
        }

        public int OutlineEligibleMaterialCount { get; }

        public string RuntimePath { get; }

        public string ReleaseMode { get; }

        public string FinalVisualParity { get; }
    }

    internal readonly struct MmdScaleAwarePhysicsReadiness
    {
        public const string MmdSpaceReadback = "mmd-space-scale-aware";
        public const string ScaleAwareHandoffReady = "scale-aware-runtime-handoff-ready";
        public const string ScaleAwareSmokeCovered = "scale-aware-live-physics-smoke-covered";

        public MmdScaleAwarePhysicsReadiness(
            float importScale,
            bool hasPhysicsDescriptors,
            string gravityPolicy,
            string backendReadbackSpace,
            string scaleAwareHandoffReadiness,
            string requiredSmoke)
        {
            ImportScale = importScale;
            HasPhysicsDescriptors = hasPhysicsDescriptors;
            GravityPolicy = gravityPolicy ?? string.Empty;
            BackendReadbackSpace = backendReadbackSpace ?? string.Empty;
            ScaleAwareHandoffReadiness = scaleAwareHandoffReadiness ?? string.Empty;
            RequiredSmoke = requiredSmoke ?? string.Empty;
        }

        public float ImportScale { get; }

        public bool HasPhysicsDescriptors { get; }

        public string GravityPolicy { get; }

        public string BackendReadbackSpace { get; }

        public string ScaleAwareHandoffReadiness { get; }

        public string RequiredSmoke { get; }
    }

    internal readonly struct MmdVmdMotionSummary
    {
        public MmdVmdMotionSummary(
            string targetModelName,
            int maxFrame,
            int boneKeyframeCount,
            int morphKeyframeCount,
            int modelKeyframeCount,
            int constraintStateCount,
            int cameraKeyframeCount = 0,
            int lightKeyframeCount = 0,
            int selfShadowKeyframeCount = 0)
        {
            TargetModelName = targetModelName ?? string.Empty;
            MaxFrame = maxFrame;
            BoneKeyframeCount = boneKeyframeCount;
            MorphKeyframeCount = morphKeyframeCount;
            ModelKeyframeCount = modelKeyframeCount;
            ConstraintStateCount = constraintStateCount;
            CameraKeyframeCount = Math.Max(0, cameraKeyframeCount);
            LightKeyframeCount = Math.Max(0, lightKeyframeCount);
            SelfShadowKeyframeCount = Math.Max(0, selfShadowKeyframeCount);
        }

        public string TargetModelName { get; }

        public int MaxFrame { get; }

        public int BoneKeyframeCount { get; }

        public int MorphKeyframeCount { get; }

        public int ModelKeyframeCount { get; }

        public int ConstraintStateCount { get; }

        public int CameraKeyframeCount { get; }

        public int LightKeyframeCount { get; }

        public int SelfShadowKeyframeCount { get; }
    }

    /// <summary>
    /// Small immutable diagnostic shape for VMD Timeline readiness preview.
    /// Duration (MaxFrame) and scene-motion presence (camera/light/self-shadow counts)
    /// are derived exclusively from MmdVmdAsset cached import summary.
    /// No LoadMotion or parse occurs through this helper (see GetVmdTimelineReadiness).
    /// </summary>
    internal readonly struct MmdVmdTimelineReadiness
    {
        public MmdVmdTimelineReadiness(
            int maxFrame,
            int cameraKeyframeCount,
            int lightKeyframeCount,
            int selfShadowKeyframeCount,
            string clipDurationSource,
            string sceneMotionStatus,
            string clipCreationRequirement)
        {
            MaxFrame = Math.Max(0, maxFrame);
            CameraKeyframeCount = Math.Max(0, cameraKeyframeCount);
            LightKeyframeCount = Math.Max(0, lightKeyframeCount);
            SelfShadowKeyframeCount = Math.Max(0, selfShadowKeyframeCount);
            HasSceneMotion = (CameraKeyframeCount > 0) || (LightKeyframeCount > 0);
            ClipDurationSource = string.IsNullOrWhiteSpace(clipDurationSource) ? "Unavailable" : clipDurationSource;
            SceneMotionStatus = string.IsNullOrWhiteSpace(sceneMotionStatus) ? "None" : sceneMotionStatus;
            ClipCreationRequirement = string.IsNullOrWhiteSpace(clipCreationRequirement)
                ? "PMX model source and Timeline are required for VMD Clip creation."
                : clipCreationRequirement;
        }

        public int MaxFrame { get; }

        public int CameraKeyframeCount { get; }

        public int LightKeyframeCount { get; }

        public int SelfShadowKeyframeCount { get; }

        public bool HasSceneMotion { get; }

        public string ClipDurationSource { get; }

        public string SceneMotionStatus { get; }

        public string ClipCreationRequirement { get; }
    }
}
