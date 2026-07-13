#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mmd.UnityIntegration
{
    internal sealed class MmdEditableRigLookAtDiagnostics
    {
        public int solvedTargetCount;
        public int skippedTargetCount;
    }

    internal sealed class MmdEditableRigSpaceSwitchDiagnostics
    {
        public int solvedTargetCount;
        public int skippedTargetCount;
    }

    internal sealed class MmdEditableRigContactCorrectionDiagnostics
    {
        public int solvedTargetCount;
        public int skippedTargetCount;
    }

    internal sealed class MmdEditableRigManualIkDiagnostics
    {
        public int solvedTargetCount;
        public int skippedTargetCount;
        public float worstDistance;
        public string skippedReasons = string.Empty;
    }

}
