#nullable enable

using UnityEngine;

namespace Mmd.UnityIntegration
{
    public sealed partial class MmdUnityPlaybackController
    {
        private void ApplyEditableRigLayer(string executionStage)
        {
            if (binding == null)
            {
                LastEditableRigDiagnostics = MmdEditableRigLayerDiagnostics.NotFound(executionStage);
                return;
            }

            MmdEditableRigLayer? layer = ResolveEditableRigLayer();
            LastEditableRigDiagnostics = layer == null
                ? MmdEditableRigLayerDiagnostics.NotFound(executionStage)
                : layer.ApplyAfterRuntimePose(binding.Instance, executionStage);
        }

        private MmdEditableRigLayer? ResolveEditableRigLayer()
        {
            MmdEditableRigLayer? layer = GetComponent<MmdEditableRigLayer>();
            if (layer != null || binding == null || binding.Instance.Root == gameObject)
            {
                return layer;
            }

            return binding.Instance.Root.GetComponent<MmdEditableRigLayer>();
        }
    }
}