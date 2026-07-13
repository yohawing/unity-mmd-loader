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
    [CustomEditor(typeof(MmdSceneEnvironmentBinding))]
    public sealed class MmdSceneEnvironmentBindingEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            if (target is not MmdSceneEnvironmentBinding binding)
            {
                return;
            }

            if (!binding.SelfShadowEnabled)
            {
                return;
            }

            MmdSelfShadowRendererSetupReadiness readiness =
                MmdAssetInspectorUtility.EvaluateMmdSelfShadowRendererSetupForCurrentPipeline(binding.TargetCamera);
            string warning = MmdAssetInspectorUtility.GetSelfShadowRendererSetupWarning(binding, readiness);
            if (string.IsNullOrEmpty(warning))
            {
                return;
            }

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(warning, MessageType.Warning);
        }
    }

}
