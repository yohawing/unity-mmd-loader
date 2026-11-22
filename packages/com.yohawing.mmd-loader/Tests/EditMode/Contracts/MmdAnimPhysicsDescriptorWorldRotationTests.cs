#nullable enable

using Mmd.Native;
using Mmd.Parser;
using Mmd.Physics;
using Mmd.UnityIntegration;
using NUnit.Framework;
using UnityEngine;

namespace Mmd.Tests
{
    /// <summary>
    /// The descriptor-world fallback (native world reports 0 rigidbodies while the managed model has
    /// some) builds rigidbody descriptors in managed code. Its Euler→quaternion order must match the
    /// PMX convention used by the native PMX path (glam <c>EulerRot::ZYX</c>, i.e. qz·qy·qx) and by the
    /// Unity-side physics transforms.
    /// </summary>
    [TestFixture]
    public sealed class MmdAnimPhysicsDescriptorWorldRotationTests
    {
        [Test]
        public void DescriptorWorldBodyFromBoneRotationUsesZyxEulerOrder()
        {
            const float x = 0.3f;
            const float y = 0.5f;
            const float z = 0.7f;
            var model = new MmdModelDefinition();
            model.bones.Add(new MmdBoneDefinition
            {
                index = 0,
                name = "root",
                parentIndex = -1,
                origin = new[] { 0.0f, 1.0f, 0.0f }
            });
            var body = new MmdRigidbodyDefinition
            {
                index = 0,
                name = "body",
                boneIndex = 0,
                boneName = "root",
                shapeType = "box",
                size = new[] { 1.0f, 2.0f, 3.0f },
                position = new[] { 0.5f, 2.0f, -0.25f },
                rotation = new[] { x, y, z },
                mass = 1.0f,
                group = 0,
                mask = 0xffff,
                physicsKind = "dynamic"
            };
            model.physics.rigidbodies.Add(body);

            MmdRuntimeFfiMethods.PhysicsRigidbodyDescriptor descriptor =
                MmdAnimPhysicsBackend.CreateRigidbodyDescriptor(model, body);

            Quaternion expected =
                Quaternion.AngleAxis(z * Mathf.Rad2Deg, Vector3.forward)
                * Quaternion.AngleAxis(y * Mathf.Rad2Deg, Vector3.up)
                * Quaternion.AngleAxis(x * Mathf.Rad2Deg, Vector3.right);
            var actual = new Quaternion(
                descriptor.bodyFromBoneRotationX,
                descriptor.bodyFromBoneRotationY,
                descriptor.bodyFromBoneRotationZ,
                descriptor.bodyFromBoneRotationW);

            AssertSameRotation(actual, expected);

            Quaternion inverse = new Quaternion(
                descriptor.boneFromBodyRotationX,
                descriptor.boneFromBodyRotationY,
                descriptor.boneFromBodyRotationZ,
                descriptor.boneFromBodyRotationW);
            AssertSameRotation(inverse, Quaternion.Inverse(expected));

            Vector3 bodyFromBonePosition = new Vector3(0.5f, 1.0f, -0.25f);
            Vector3 expectedBoneFromBodyPosition = -(Quaternion.Inverse(expected) * bodyFromBonePosition);
            Assert.That(descriptor.boneFromBodyPositionX, Is.EqualTo(expectedBoneFromBodyPosition.x).Within(1.0e-5f));
            Assert.That(descriptor.boneFromBodyPositionY, Is.EqualTo(expectedBoneFromBodyPosition.y).Within(1.0e-5f));
            Assert.That(descriptor.boneFromBodyPositionZ, Is.EqualTo(expectedBoneFromBodyPosition.z).Within(1.0e-5f));
        }

        [TestCase(0.3f, 0.5f, 0.7f)]
        [TestCase(-1.2f, 0.4f, 2.9f)]
        [TestCase(3.0f, -1.5f, -0.25f)]
        [TestCase(0.0f, 0.0f, 0.0f)]
        public void SharedEulerHelperMatchesUnityZyxComposition(float x, float y, float z)
        {
            Quaternion expected =
                Quaternion.AngleAxis(z * Mathf.Rad2Deg, Vector3.forward)
                * Quaternion.AngleAxis(y * Mathf.Rad2Deg, Vector3.up)
                * Quaternion.AngleAxis(x * Mathf.Rad2Deg, Vector3.right);

            AssertSameRotation(MmdCoordinateSpace.MmdEulerToRotation(new[] { x, y, z }), expected);
            Assert.That(MmdCoordinateSpace.MmdEulerToRotation(null), Is.EqualTo(Quaternion.identity));
            Assert.That(MmdCoordinateSpace.MmdEulerToRotation(new[] { x, y }), Is.EqualTo(Quaternion.identity));
        }

        private static void AssertSameRotation(Quaternion actual, Quaternion expected)
        {
            // q and -q are the same rotation.
            float dot = Mathf.Abs(Quaternion.Dot(actual, expected));
            Assert.That(dot, Is.EqualTo(1.0f).Within(1.0e-5f), $"actual={actual} expected={expected}");
        }
    }
}
