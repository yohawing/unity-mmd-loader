#nullable enable

using Mmd.UnityIntegration;

namespace Mmd.Tests
{
    /// <summary>
    /// Teardown helpers shared by EditMode tests that create playback bindings and PMX/VMD assets.
    /// </summary>
    internal static class MmdTestCleanup
    {
        internal static void DestroyBinding(MmdUnityPlaybackBinding? binding)
        {
            if (binding?.Instance?.Root != null)
            {
                UnityEngine.Object.DestroyImmediate(binding.Instance.Root);
            }

            binding?.Dispose();
        }

        internal static void DestroyAssets(MmdPmxAsset? pmx, MmdVmdAsset? vmd)
        {
            if (pmx != null) UnityEngine.Object.DestroyImmediate(pmx);
            if (vmd != null) UnityEngine.Object.DestroyImmediate(vmd);
        }
    }
}
