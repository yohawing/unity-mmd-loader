#nullable enable

namespace Mmd.Physics
{
    public interface IMmdPhysicsBackend
    {
        string Name { get; }

        bool IsDeterministic { get; }

        void Reset();

        void Step(int frame, float deltaTime);
    }
}
