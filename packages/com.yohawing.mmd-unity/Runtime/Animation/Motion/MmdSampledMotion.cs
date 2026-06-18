using System.Collections.Generic;

namespace Yohawing.MmdUnity.Motion
{
    public sealed class MmdSampledMotion
    {
        public Dictionary<string, MmdBonePoseSample> Bones { get; } = new();

        public Dictionary<string, float> Morphs { get; } = new();

        public Dictionary<string, bool> IkStates { get; } = new();
    }
}
