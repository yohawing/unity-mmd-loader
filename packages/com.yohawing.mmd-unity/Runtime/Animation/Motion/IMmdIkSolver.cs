#nullable enable

using Yohawing.MmdUnity.Parser;

namespace Yohawing.MmdUnity.Motion
{
    public interface IMmdIkSolver
    {
        string Name { get; }

        MmdSampledMotion Solve(MmdModelDefinition model, MmdSampledMotion? sampledMotion);
    }
}
