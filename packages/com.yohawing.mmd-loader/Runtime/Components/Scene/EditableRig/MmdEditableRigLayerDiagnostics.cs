#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mmd.UnityIntegration
{
    [Serializable]
    public sealed class MmdEditableRigLayerDiagnostics
    {
        public bool layerFound;
        public bool componentEnabled;
        public bool editableRigEnabled;
        public float layerWeight;
        public string executionStage = string.Empty;
        public string transformState = string.Empty;
        public string noOpReason = string.Empty;
        public int correctedBoneCount;
        public int solvedManualIkTargetCount;
        public int skippedManualIkTargetCount;
        public float worstManualIkDistance;
        public string manualIkSkippedReasons = string.Empty;
        public int solvedLookAtTargetCount;
        public int skippedLookAtTargetCount;
        public int solvedSpaceSwitchTargetCount;
        public int skippedSpaceSwitchTargetCount;
        public int solvedContactCorrectionTargetCount;
        public int skippedContactCorrectionTargetCount;
        public float maxLayerDelta;
        public float meanLayerDelta;

        public static MmdEditableRigLayerDiagnostics NotFound(string executionStage)
        {
            return new MmdEditableRigLayerDiagnostics
            {
                layerFound = false,
                componentEnabled = false,
                editableRigEnabled = false,
                layerWeight = 0.0f,
                executionStage = executionStage,
                transformState = "native-only",
                noOpReason = "layer-missing",
                correctedBoneCount = 0,
                solvedManualIkTargetCount = 0,
                skippedManualIkTargetCount = 0,
                worstManualIkDistance = 0.0f,
                manualIkSkippedReasons = string.Empty,
                solvedLookAtTargetCount = 0,
                skippedLookAtTargetCount = 0,
                solvedSpaceSwitchTargetCount = 0,
                skippedSpaceSwitchTargetCount = 0,
                solvedContactCorrectionTargetCount = 0,
                skippedContactCorrectionTargetCount = 0,
                maxLayerDelta = 0.0f,
                meanLayerDelta = 0.0f
            };
        }
    }

    public enum MmdEditableRigSpaceSwitchSourceSpace
    {
        Local = 0,
        World = 1,
        Parent = 2
    }

}
