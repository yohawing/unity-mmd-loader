#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Mmd.Parser;
using Mmd.Rendering.Universal;
using Mmd.UnityIntegration;

namespace Mmd.Editor
{
    internal static partial class MmdAssetInspectorUtility
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
            string shaderPreset)
        {
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.TextField("Model Preset (Summary)", modelPreset);
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

            if (readiness.OutlineEligibleMaterialCount > 0 && !IsMmdOutlineFeaturePresent())
            {
                EditorGUILayout.HelpBox(
                    "MmdOutlineRendererFeature が URP Renderer Data に追加されていません。" +
                    "アウトラインは描画されません。\n" +
                    "追加方法: URP Renderer Data アセット → Add Renderer Feature → Mmd Outline Renderer Feature",
                    MessageType.Warning);
            }
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
                ? "MmdOutlineRendererFeature (LightMode=MmdOutline)"
                : "Shader preset summary only";
            return new MmdOutlineReadiness(
                asset.EdgeMaterialCount,
                runtimePath,
                "Back-face mesh-normal extrusion",
                MmdOutlineReadiness.NotClaimed);
        }

        internal static bool IsMmdOutlineFeaturePresent()
        {
            var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (pipeline == null)
                return false;

            var pipelineSo = new SerializedObject(pipeline);
            var rendererDataList = pipelineSo.FindProperty("m_RendererDataList");
            if (rendererDataList == null)
                return false;

            for (int i = 0; i < rendererDataList.arraySize; i++)
            {
                var rendererDataRef = rendererDataList.GetArrayElementAtIndex(i);
                if (rendererDataRef.objectReferenceValue == null)
                    continue;

                var rendererDataSo = new SerializedObject(rendererDataRef.objectReferenceValue);
                var features = rendererDataSo.FindProperty("m_RendererFeatures");
                if (features == null)
                    continue;

                for (int j = 0; j < features.arraySize; j++)
                {
                    if (features.GetArrayElementAtIndex(j).objectReferenceValue is MmdOutlineRendererFeature)
                        return true;
                }
            }

            return false;
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

    }
}
