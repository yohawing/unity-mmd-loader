#nullable enable

using Mmd.Parser;

namespace Mmd.Motion
{
    public interface IMmdIkSolver
    {
        string Name { get; }

        MmdSampledMotion Solve(MmdModelDefinition model, MmdSampledMotion? sampledMotion);
    }
}
