#nullable enable

using Mmd.Parser;

namespace Mmd.UnityIntegration
{
    public sealed partial class MmdUnityPlaybackBinding
    {
        internal MmdModelDefinition NativeHostPoseModel => model;

        internal byte[]? NativeHostPoseModelSourceBytes => model.sourceBytes;
    }
}
