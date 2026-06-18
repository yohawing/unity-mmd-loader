#nullable enable

using Yohawing.MmdUnity.Motion;
using Yohawing.MmdUnity.Parser;

namespace Yohawing.MmdUnity.Pose
{
    public interface IMmdAppendTransformProvider
    {
        MmdSampledMotion ApplyAppendTransforms(MmdModelDefinition model, MmdSampledMotion sampledMotion);
    }
}
