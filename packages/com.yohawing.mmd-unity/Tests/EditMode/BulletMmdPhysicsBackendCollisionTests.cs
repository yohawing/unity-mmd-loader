#nullable enable

using NUnit.Framework;
using Yohawing.MmdUnity.Parser;
using Yohawing.MmdUnity.Physics;

namespace Yohawing.MmdUnity.Tests
{
    /// <summary>
    /// Backend-level regression tests for MMD rigid body collision behavior. These
    /// exercise <see cref="BulletMmdPhysicsBackend"/> directly (no Unity scene) with
    /// PMX-faithful collision masks (a set bit means "collides with that group", which
    /// is how MikuMikuDance / saba store the field). They guard two bugs that made
    /// colliders ineffective:
    ///   * the collision mask was inverted in native, so a "collides with all" body
    ///     (0xffff) became "collides with nothing" (0) and dynamic bodies fell through;
    ///   * bone-following (mass 0) bodies were plain static objects rather than
    ///     kinematic, so they could not push or drag dynamic bodies.
    /// </summary>
    [TestFixture]
    public sealed class BulletMmdPhysicsBackendCollisionTests
    {
        private const int CollidesWithAllGroups = 0xffff;

        [Test]
        public void DynamicBodyRestsOnStaticFloorWhenMaskCollidesWithAllGroups()
        {
            MmdPhysicsBackendAvailability availability = BulletMmdPhysicsBackend.ProbeAvailability();
            if (!availability.backendAvailable)
            {
                Assert.Ignore("Bullet physics backend is not available: " + availability.unsupportedReason);
            }

            const float floorTopY = 1.0f; // box half-extent y = 1 at origin -> top surface at y = 1
            const float sphereRadius = 1.0f;
            const float sphereStartY = 10.0f;

            var model = new MmdModelDefinition { name = "floor-and-falling-sphere" };
            model.physics.rigidbodies.Add(MakeBody(
                index: 0, name: "floor", shape: "box",
                size: new[] { 20.0f, 1.0f, 20.0f },
                position: new[] { 0.0f, 0.0f, 0.0f },
                mass: 0.0f, physicsKind: "static",
                group: 0, mask: CollidesWithAllGroups));
            model.physics.rigidbodies.Add(MakeBody(
                index: 1, name: "sphere", shape: "sphere",
                size: new[] { sphereRadius, sphereRadius, sphereRadius },
                position: new[] { 0.0f, sphereStartY, 0.0f },
                mass: 1.0f, physicsKind: "dynamic",
                group: 0, mask: CollidesWithAllGroups));

            using var backend = new BulletMmdPhysicsBackend("collision-floor.pmx");
            backend.InitializeWorld(model);
            backend.Reset();

            for (int frame = 1; frame <= 180; frame++)
            {
                backend.Step(frame, 1.0f / 60.0f);
            }

            float sphereY = backend.GetRigidbodyTransform(1).position[1];

            Assert.That(
                sphereY,
                Is.GreaterThan(floorTopY),
                "Dynamic body fell through the floor: the collision mask is not being honored (a 0xffff 'collides with all' body must collide).");
            Assert.That(
                sphereY,
                Is.LessThan(sphereStartY - 1.0f),
                "Dynamic body did not fall under gravity, so the rest-on-floor result is not meaningful.");
        }

        [Test]
        public void KinematicFloorDragsRestingDynamicBodyHorizontally()
        {
            MmdPhysicsBackendAvailability availability = BulletMmdPhysicsBackend.ProbeAvailability();
            if (!availability.backendAvailable)
            {
                Assert.Ignore("Bullet physics backend is not available: " + availability.unsupportedReason);
            }

            // Floor top surface at y = 0 (half-extent y = 1, centered at y = -1).
            var model = new MmdModelDefinition { name = "kinematic-drag-floor" };
            model.physics.rigidbodies.Add(MakeBody(
                index: 0, name: "floor", shape: "box",
                size: new[] { 50.0f, 1.0f, 50.0f },
                position: new[] { 0.0f, -1.0f, 0.0f },
                mass: 0.0f, physicsKind: "static",
                group: 0, mask: CollidesWithAllGroups,
                friction: 1.0f));
            model.physics.rigidbodies.Add(MakeBody(
                index: 1, name: "block", shape: "box",
                size: new[] { 1.0f, 1.0f, 1.0f },
                position: new[] { 0.0f, 3.0f, 0.0f },
                mass: 1.0f, physicsKind: "dynamic",
                group: 0, mask: CollidesWithAllGroups,
                friction: 1.0f, linearDamping: 0.0f, angularDamping: 0.0f));

            using var backend = new BulletMmdPhysicsBackend("kinematic-drag.pmx");
            backend.InitializeWorld(model);
            backend.Reset();

            // Phase 1: let the block fall and settle on the stationary floor.
            for (int frame = 1; frame <= 60; frame++)
            {
                backend.Step(frame, 1.0f / 60.0f);
            }

            float restingX = backend.GetRigidbodyTransform(1).position[0];

            // Phase 2: translate the floor in +X every frame. A kinematic floor carries
            // its surface velocity, so friction drags the resting block along; a plain
            // static (teleported) floor reports zero velocity and cannot drag it.
            var identity = new[] { 0.0f, 0.0f, 0.0f, 1.0f };
            float floorX = 0.0f;
            for (int frame = 61; frame <= 120; frame++)
            {
                floorX += 0.25f;
                backend.SetRigidbodyTransform(0, new[] { floorX, -1.0f, 0.0f }, identity);
                backend.Step(frame, 1.0f / 60.0f);
            }

            float draggedX = backend.GetRigidbodyTransform(1).position[0];
            float blockY = backend.GetRigidbodyTransform(1).position[1];

            Assert.That(
                blockY,
                Is.GreaterThan(-1.0f),
                "Block fell through the kinematic floor.");
            Assert.That(
                draggedX - restingX,
                Is.GreaterThan(1.0f),
                "Kinematic floor did not drag the resting body: bone-following (mass 0) bodies must be kinematic so they impart velocity to dynamic bodies.");
        }

        [Test]
        public void KinematicAnchorDragsJointedDynamicBodyWhenTranslated()
        {
            MmdPhysicsBackendAvailability availability = BulletMmdPhysicsBackend.ProbeAvailability();
            if (!availability.backendAvailable)
            {
                Assert.Ignore("Bullet physics backend is not available: " + availability.unsupportedReason);
            }

            // Models a 揺れもの chain root: a bone-following (mass 0) anchor with a mode-1
            // dynamic body hanging from it through a ball joint (linear locked at the pivot,
            // angular free). When the anchor translates with the model, the joint must drag
            // the dynamic body along. If it does not, the dynamic body stays at its bind-pose
            // model-space position while the body animates away — the reported "hair freezes
            // at the bind pose" symptom for position-driven (mode-1) physics bones.
            var model = new MmdModelDefinition { name = "kinematic-anchor-jointed-chain" };
            model.physics.rigidbodies.Add(MakeBody(
                index: 0, name: "anchor", shape: "box",
                size: new[] { 0.5f, 0.5f, 0.5f },
                position: new[] { 0.0f, 10.0f, 0.0f },
                mass: 0.0f, physicsKind: "static",
                group: 0, mask: CollidesWithAllGroups));
            model.physics.rigidbodies.Add(MakeBody(
                index: 1, name: "hair", shape: "sphere",
                size: new[] { 0.5f, 0.5f, 0.5f },
                position: new[] { 0.0f, 8.0f, 0.0f },
                mass: 1.0f, physicsKind: "dynamic",
                group: 0, mask: CollidesWithAllGroups,
                linearDamping: 0.0f, angularDamping: 0.0f));
            model.physics.joints.Add(new MmdJointDefinition
            {
                index = 0,
                name = "anchor-hair",
                rigidbodyAIndex = 0,
                rigidbodyBIndex = 1,
                position = new[] { 0.0f, 9.0f, 0.0f },
                rotation = new[] { 0.0f, 0.0f, 0.0f },
                linearLowerLimit = new[] { 0.0f, 0.0f, 0.0f },
                linearUpperLimit = new[] { 0.0f, 0.0f, 0.0f },
                angularLowerLimit = new[] { -3.1415927f, -3.1415927f, -3.1415927f },
                angularUpperLimit = new[] { 3.1415927f, 3.1415927f, 3.1415927f },
                linearSpring = new[] { 0.0f, 0.0f, 0.0f },
                angularSpring = new[] { 0.0f, 0.0f, 0.0f }
            });

            using var backend = new BulletMmdPhysicsBackend("kinematic-anchor-chain.pmx");
            backend.InitializeWorld(model);
            backend.Reset();

            // Phase 1: let the dynamic body settle hanging from the stationary anchor.
            for (int frame = 1; frame <= 60; frame++)
            {
                backend.Step(frame, 1.0f / 60.0f);
            }

            float restX = backend.GetRigidbodyTransform(1).position[0];

            // Phase 2: translate the kinematic anchor +X every frame (the model moves).
            var identity = new[] { 0.0f, 0.0f, 0.0f, 1.0f };
            float anchorX = 0.0f;
            for (int frame = 61; frame <= 140; frame++)
            {
                anchorX += 0.25f;
                backend.SetRigidbodyTransform(0, new[] { anchorX, 10.0f, 0.0f }, identity);
                backend.Step(frame, 1.0f / 60.0f);
            }

            float draggedX = backend.GetRigidbodyTransform(1).position[0];
            float draggedY = backend.GetRigidbodyTransform(1).position[1];

            Assert.That(
                draggedY,
                Is.GreaterThan(0.0f),
                "Dynamic body fell away from the anchor: the joint is not holding.");
            Assert.That(
                draggedX - restX,
                Is.GreaterThan(5.0f),
                "The jointed dynamic body did not follow the translating kinematic anchor: a moving " +
                "bone-following body must drag its 揺れもの chain along. If it stays put, the physics bone " +
                "freezes at the bind pose while the body animates away.");
        }

        [Test]
        public void DynamicBodyTeleportedIntoDeepPenetrationRecoversWithoutExploding()
        {
            // Characterizes the scrub/seek reseed: the binding teleports physics bodies to the
            // (straight) bone pose, which at an animated frame can sit deep inside a now-working
            // body collider. The first forward step must recover gently, not fling the body away.
            MmdPhysicsBackendAvailability availability = BulletMmdPhysicsBackend.ProbeAvailability();
            if (!availability.backendAvailable)
            {
                Assert.Ignore("Bullet physics backend is not available: " + availability.unsupportedReason);
            }

            var model = new MmdModelDefinition { name = "deep-penetration-recovery" };
            model.physics.rigidbodies.Add(MakeBody(
                index: 0, name: "torso", shape: "box",
                size: new[] { 5.0f, 5.0f, 5.0f },
                position: new[] { 0.0f, 0.0f, 0.0f },
                mass: 0.0f, physicsKind: "static",
                group: 0, mask: CollidesWithAllGroups));
            model.physics.rigidbodies.Add(MakeBody(
                index: 1, name: "hair", shape: "sphere",
                size: new[] { 1.0f, 1.0f, 1.0f },
                position: new[] { 0.0f, 8.0f, 0.0f },
                mass: 1.0f, physicsKind: "dynamic",
                group: 0, mask: CollidesWithAllGroups,
                linearDamping: 0.0f, angularDamping: 0.0f));

            using var backend = new BulletMmdPhysicsBackend("deep-penetration.pmx");
            backend.InitializeWorld(model);
            backend.Reset();

            for (int frame = 1; frame <= 60; frame++)
            {
                backend.Step(frame, 1.0f / 60.0f);
            }

            // Teleport the sphere to a realistic, off-centre moderate penetration: 2 units into the
            // top face of the box (box top at y=5, sphere radius 1 => bottom at y=3 => 2 deep). This
            // has an unambiguous up normal, unlike a body spawned at the exact box centre.
            backend.SetRigidbodyTransform(1, new[] { 0.0f, 4.0f, 0.0f }, new[] { 0.0f, 0.0f, 0.0f, 1.0f });

            float maxDistance = 0.0f;
            for (int frame = 61; frame <= 120; frame++)
            {
                backend.Step(frame, 1.0f / 60.0f);
                float[] p = backend.GetRigidbodyTransform(1).position;
                maxDistance = System.Math.Max(maxDistance, (float)System.Math.Sqrt(p[0] * p[0] + p[1] * p[1] + p[2] * p[2]));
            }

            Assert.That(
                maxDistance,
                Is.LessThan(15.0f),
                "A dynamic body teleported into deep penetration was flung far away (explosion) instead of " +
                "recovering gently — this is the 'scrub reseed makes the physics collapse' mechanism.");
        }

        [Test]
        public void DynamicBodyTeleportedAwayFromLockedJointRecoversWithoutExploding()
        {
            // Characterizes a reseed that lands a jointed body off its constraint: the locked joint
            // must pull it back without an overshoot/explosion.
            MmdPhysicsBackendAvailability availability = BulletMmdPhysicsBackend.ProbeAvailability();
            if (!availability.backendAvailable)
            {
                Assert.Ignore("Bullet physics backend is not available: " + availability.unsupportedReason);
            }

            var model = new MmdModelDefinition { name = "joint-violation-recovery" };
            model.physics.rigidbodies.Add(MakeBody(
                index: 0, name: "anchor", shape: "box",
                size: new[] { 0.5f, 0.5f, 0.5f },
                position: new[] { 0.0f, 10.0f, 0.0f },
                mass: 0.0f, physicsKind: "static",
                group: 0, mask: CollidesWithAllGroups));
            model.physics.rigidbodies.Add(MakeBody(
                index: 1, name: "hair", shape: "sphere",
                size: new[] { 0.5f, 0.5f, 0.5f },
                position: new[] { 0.0f, 8.0f, 0.0f },
                mass: 1.0f, physicsKind: "dynamic",
                group: 0, mask: CollidesWithAllGroups,
                linearDamping: 0.0f, angularDamping: 0.0f));
            model.physics.joints.Add(new MmdJointDefinition
            {
                index = 0,
                name = "anchor-hair",
                rigidbodyAIndex = 0,
                rigidbodyBIndex = 1,
                position = new[] { 0.0f, 9.0f, 0.0f },
                rotation = new[] { 0.0f, 0.0f, 0.0f },
                linearLowerLimit = new[] { 0.0f, 0.0f, 0.0f },
                linearUpperLimit = new[] { 0.0f, 0.0f, 0.0f },
                angularLowerLimit = new[] { -3.1415927f, -3.1415927f, -3.1415927f },
                angularUpperLimit = new[] { 3.1415927f, 3.1415927f, 3.1415927f },
                linearSpring = new[] { 0.0f, 0.0f, 0.0f },
                angularSpring = new[] { 0.0f, 0.0f, 0.0f }
            });

            using var backend = new BulletMmdPhysicsBackend("joint-violation.pmx");
            backend.InitializeWorld(model);
            backend.Reset();

            for (int frame = 1; frame <= 60; frame++)
            {
                backend.Step(frame, 1.0f / 60.0f);
            }

            // Teleport the jointed body well away from its locked constraint position.
            backend.SetRigidbodyTransform(1, new[] { 6.0f, 8.0f, 0.0f }, new[] { 0.0f, 0.0f, 0.0f, 1.0f });

            float maxDistanceFromAnchor = 0.0f;
            for (int frame = 61; frame <= 120; frame++)
            {
                backend.Step(frame, 1.0f / 60.0f);
                float[] p = backend.GetRigidbodyTransform(1).position;
                float dx = p[0] - 0.0f;
                float dy = p[1] - 10.0f;
                float dz = p[2] - 0.0f;
                maxDistanceFromAnchor = System.Math.Max(maxDistanceFromAnchor, (float)System.Math.Sqrt(dx * dx + dy * dy + dz * dz));
            }

            Assert.That(
                maxDistanceFromAnchor,
                Is.LessThan(20.0f),
                "A jointed body teleported off its constraint overshot/exploded instead of settling back " +
                "to the joint length.");
        }

        [Test]
        public void LargeDeltaTimeStepIsClampedAndDoesNotOverSimulate()
        {
            // A frame hitch, a Timeline seek, or the per-frame world recreate during a backward scrub
            // can hand the backend a very large deltaTime. The substep count must be capped (saba uses
            // a fixed max of 10) so Bullet drops the excess time instead of integrating seconds of
            // gravity/inertia in a single Step — which is what flings the 揺れもの apart on resume.
            MmdPhysicsBackendAvailability availability = BulletMmdPhysicsBackend.ProbeAvailability();
            if (!availability.backendAvailable)
            {
                Assert.Ignore("Bullet physics backend is not available: " + availability.unsupportedReason);
            }

            Assert.That(BulletMmdPhysicsBackend.EstimateMaxSubSteps(2.0f), Is.LessThanOrEqualTo(BulletMmdPhysicsBackend.MaxSubStepsLimit));
            Assert.That(BulletMmdPhysicsBackend.MaxSubStepsLimit, Is.LessThanOrEqualTo(16),
                "The substep cap must stay small (saba uses 10) so large deltaTime is clamped, not fully simulated.");

            var model = new MmdModelDefinition { name = "large-delta-clamp" };
            model.physics.rigidbodies.Add(MakeBody(
                index: 0, name: "free", shape: "sphere",
                size: new[] { 1.0f, 1.0f, 1.0f },
                position: new[] { 0.0f, 0.0f, 0.0f },
                mass: 1.0f, physicsKind: "dynamic",
                group: 0, mask: CollidesWithAllGroups,
                linearDamping: 0.0f, angularDamping: 0.0f));

            using var backend = new BulletMmdPhysicsBackend("large-delta.pmx");
            backend.InitializeWorld(model);
            backend.Reset();

            // One absurdly large step (2 seconds). Free fall under gravity (-98) for the full 2s would
            // drop ~196 units; a properly clamped step integrates at most MaxSubStepsLimit/60 seconds.
            backend.Step(1, 2.0f);

            float y = backend.GetRigidbodyTransform(0).position[1];
            Assert.That(
                y,
                Is.GreaterThan(-20.0f),
                "A single huge deltaTime step integrated far too much free-fall: the substep count is not " +
                "clamped, so a frame hitch / seek would let physics explode on resume. y=" + y);
        }

        private static MmdRigidbodyDefinition MakeBody(
            int index,
            string name,
            string shape,
            float[] size,
            float[] position,
            float mass,
            string physicsKind,
            int group,
            int mask,
            float friction = 0.5f,
            float linearDamping = 0.0f,
            float angularDamping = 0.0f)
        {
            return new MmdRigidbodyDefinition
            {
                index = index,
                name = name,
                boneIndex = -1,
                boneName = string.Empty,
                shapeType = shape,
                size = size,
                position = position,
                rotation = new[] { 0.0f, 0.0f, 0.0f },
                mass = mass,
                linearDamping = linearDamping,
                angularDamping = angularDamping,
                friction = friction,
                restitution = 0.0f,
                group = group,
                mask = mask,
                physicsKind = physicsKind
            };
        }
    }
}
