#nullable enable

namespace Mmd.Physics
{
    public sealed class NullMmdPhysicsBackend : IMmdPhysicsBackend
    {
        public string Name => "Null";

        public bool IsDeterministic => true;

        public void Reset()
        {
        }

        public void Step(int frame, float deltaTime)
        {
        }
    }
}
