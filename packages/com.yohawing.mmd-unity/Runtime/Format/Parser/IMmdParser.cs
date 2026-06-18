using System;

namespace Yohawing.MmdUnity.Parser
{
    public interface IMmdParser
    {
        MmdModelDefinition LoadModel(ReadOnlySpan<byte> data);

        MmdMotionDefinition LoadMotion(ReadOnlySpan<byte> data);
    }
}
