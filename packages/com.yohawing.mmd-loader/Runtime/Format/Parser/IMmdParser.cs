using System;

namespace Mmd.Parser
{
    public interface IMmdParser
    {
        MmdModelDefinition LoadModel(ReadOnlySpan<byte> data);

        MmdMotionDefinition LoadMotion(ReadOnlySpan<byte> data);
    }
}
