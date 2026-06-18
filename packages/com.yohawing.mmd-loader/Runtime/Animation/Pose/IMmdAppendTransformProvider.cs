#nullable enable

using Mmd.Motion;
using Mmd.Parser;

namespace Mmd.Pose
{
    public interface IMmdAppendTransformProvider
    {
        MmdSampledMotion ApplyAppendTransforms(MmdModelDefinition model, MmdSampledMotion sampledMotion);
    }
}
