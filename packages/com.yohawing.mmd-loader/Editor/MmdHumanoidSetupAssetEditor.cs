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
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.TextField("Status", conversionPlan.Readiness);
            }

            if (!conversionPlan.PrerequisitesReady)
            {
                string compact = MmdAssetInspectorUtility.FormatCompactHumanoidClipConversionIssues(conversionPlan);
                if (!string.IsNullOrEmpty(compact))
                {
                    EditorGUILayout.HelpBox(compact, MessageType.Info);
                }
            }

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

}
