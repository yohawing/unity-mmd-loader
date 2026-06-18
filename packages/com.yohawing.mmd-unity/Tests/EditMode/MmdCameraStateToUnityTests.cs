using NUnit.Framework;
using UnityEngine;
using Yohawing.MmdUnity.Motion;
using Yohawing.MmdUnity.UnityIntegration;

namespace Yohawing.MmdUnity.Tests
{
    public sealed class MmdCameraStateToUnityTests
    {
        private static MmdCameraState State(
            float distance, float px, float py, float pz, float rx, float ry, float rz, float fov, bool perspective)
        {
            return new MmdCameraState(
                distance,
                new[] { px, py, pz },
                new[] { rx, ry, rz },
                fov,
                perspective);
        }

        [Test]
        public void ZeroRotationPlacesCameraInFrontOfTargetLookingAtIt()
        {
            // target origin, MMD distance -45 -> camera 45 units IN FRONT (+Z) looking back at origin.
            // (+Z is the discriminator that caught the earlier Z-flip: the camera must frame the
            // model's face, which the MMD->Unity map places facing +Z.)
            MmdUnityCameraPose pose = MmdCameraStateToUnity.Convert(State(-45f, 0, 0, 0, 0, 0, 0, 30f, true));

            Assert.That(pose.Position.x, Is.EqualTo(0f).Within(0.001f));
            Assert.That(pose.Position.y, Is.EqualTo(0f).Within(0.001f));
            Assert.That(pose.Position.z, Is.EqualTo(45f).Within(0.001f), "default camera sits in front (+Z), not behind");
            // Forward points from the camera toward the target (origin), i.e. toward -Z.
            Assert.That(Vector3.Dot(pose.Rotation * Vector3.forward, Vector3.back), Is.EqualTo(1f).Within(0.001f));
            Assert.That(pose.FieldOfView, Is.EqualTo(30f).Within(0.001f));
            Assert.That(pose.Perspective, Is.True);
        }

        [Test]
        public void TargetMapsWithBoneXZFlipConvention()
        {
            // Non-origin target, no rotation: eye = target + (0,0,distance) in MMD, then MmdToUnity.
            MmdUnityCameraPose pose = MmdCameraStateToUnity.Convert(State(-10f, 3, 5, 7, 0, 0, 0, 30f, true));

            // eyeMmd = (3,5,7) + (0,0,-10) = (3,5,-3) -> Unity (-3,5,3).
            Assert.That(pose.Position.x, Is.EqualTo(-3f).Within(0.001f));
            Assert.That(pose.Position.y, Is.EqualTo(5f).Within(0.001f));
            Assert.That(pose.Position.z, Is.EqualTo(3f).Within(0.001f));
        }

        [TestCase(-45f, 0f, 0f, 0f, 0f, 0f, 0f)]
        [TestCase(-30f, 2f, 10f, -4f, 0.3f, 0.5f, 0.1f)]
        [TestCase(-12.5f, -5f, 1f, 8f, -0.2f, 1.2f, 0.4f)]
        [TestCase(30f, 1f, 2f, 3f, 0.1f, -0.6f, 0f)]
        [TestCase(20f, 0f, 5f, 0f, 0.4f, 0.8f, 0.2f)] // positive distance with pan/tilt/roll
        public void CameraIsDistanceAwayFromTargetAndLooksAtIt(
            float distance, float px, float py, float pz, float rx, float ry, float rz)
        {
            MmdUnityCameraPose pose = MmdCameraStateToUnity.Convert(State(distance, px, py, pz, rx, ry, rz, 35f, true));

            // Target in Unity space uses the same (-x, y, -z) bone convention.
            Vector3 targetUnity = new Vector3(-px, py, -pz);

            Assert.That(Vector3.Distance(pose.Position, targetUnity), Is.EqualTo(Mathf.Abs(distance)).Within(0.01f),
                "Camera must sit |distance| away from the look-at target.");

            Vector3 forward = pose.Rotation * Vector3.forward;
            Vector3 toTarget = (targetUnity - pose.Position).normalized;
            Assert.That(Vector3.Dot(forward, toTarget), Is.EqualTo(1f).Within(0.001f),
                "Camera forward must point at the look-at target.");
        }

        [Test]
        public void FieldOfViewIsClampedToMinimum()
        {
            Assert.That(MmdCameraStateToUnity.Convert(State(-45f, 0, 0, 0, 0, 0, 0, 0.2f, true)).FieldOfView,
                Is.EqualTo(MmdCameraStateToUnity.DefaultMinFieldOfView).Within(0.001f));
            Assert.That(MmdCameraStateToUnity.Convert(State(-45f, 0, 0, 0, 0, 0, 0, 50f, true)).FieldOfView,
                Is.EqualTo(50f).Within(0.001f));
        }

        [Test]
        public void PerspectiveFlagIsPreserved()
        {
            Assert.That(MmdCameraStateToUnity.Convert(State(-45f, 0, 0, 0, 0, 0, 0, 30f, false)).Perspective, Is.False);
            Assert.That(MmdCameraStateToUnity.Convert(State(-45f, 0, 0, 0, 0, 0, 0, 30f, true)).Perspective, Is.True);
        }

        [Test]
        public void NonFiniteDistanceAndFovDoNotProduceNonFinitePose()
        {
            MmdUnityCameraPose pose = MmdCameraStateToUnity.Convert(
                State(float.NaN, 0, 0, 0, 0, 0, 0, float.PositiveInfinity, true));

            Assert.That(
                float.IsFinite(pose.Position.x) && float.IsFinite(pose.Position.y) && float.IsFinite(pose.Position.z),
                Is.True);
            Assert.That(float.IsFinite(pose.FieldOfView), Is.True);
            Assert.That(pose.FieldOfView, Is.EqualTo(MmdCameraStateToUnity.DefaultMinFieldOfView).Within(0.001f));
        }

        // The four golden tests below PIN the current conversion convention (Euler YXZ order +
        // MMD->Unity 180°-Y map + look-back flip) so an accidental change to the rotation order or
        // multiply side is caught as a regression. They assert observed output, not correctness vs
        // MMD; the absolute pan/tilt/roll sign is confirmed visually in the Timeline slice (3c).
        private static void AssertPose(MmdUnityCameraPose pose, Vector3 expectedPosition, Quaternion expectedRotation)
        {
            Assert.That(pose.Position.x, Is.EqualTo(expectedPosition.x).Within(0.01f), "position.x");
            Assert.That(pose.Position.y, Is.EqualTo(expectedPosition.y).Within(0.01f), "position.y");
            Assert.That(pose.Position.z, Is.EqualTo(expectedPosition.z).Within(0.01f), "position.z");
            Assert.That(Quaternion.Angle(pose.Rotation, expectedRotation), Is.LessThan(0.05f), "rotation");
        }

        [Test]
        public void GoldenYawNinetyDegrees()
        {
            // +90° MMD yaw swings the camera around the target onto Unity -X, still looking at it.
            AssertPose(
                MmdCameraStateToUnity.Convert(State(-45f, 0, 0, 0, 0, Mathf.PI / 2f, 0, 30f, true)),
                new Vector3(-45f, 0f, 0f),
                new Quaternion(0f, 0.70710677f, 0f, 0.70710677f));
        }

        [Test]
        public void GoldenPitchFortyFiveDegrees()
        {
            // +45° MMD pitch swings the camera below the target and tilts it up at it.
            AssertPose(
                MmdCameraStateToUnity.Convert(State(-45f, 0, 0, 0, Mathf.PI / 4f, 0, 0, 30f, true)),
                new Vector3(0f, -31.81981f, 31.8198f),
                new Quaternion(0f, 0.9238795f, 0.3826835f, 0f));
        }

        [Test]
        public void GoldenRollThirtyDegrees()
        {
            // Roll keeps the camera in front (+Z) and only rolls it about its view axis.
            AssertPose(
                MmdCameraStateToUnity.Convert(State(-45f, 0, 0, 0, 0, 0, Mathf.PI / 6f, 30f, true)),
                new Vector3(0f, 0f, 45f),
                new Quaternion(-0.258819f, 0.9659258f, 0f, 0f));
        }

        [Test]
        public void GoldenPositiveDistanceLooksFromBehind()
        {
            // distance > 0 places the camera on Unity -Z (behind), still looking at the target.
            AssertPose(
                MmdCameraStateToUnity.Convert(State(30f, 0, 0, 0, 0, 0, 0, 30f, true)),
                new Vector3(0f, 0f, -30f),
                new Quaternion(0f, 0f, 0f, 1f));
        }

        [Test]
        public void ZeroDistanceKeepsCameraAtTarget()
        {
            MmdUnityCameraPose pose = MmdCameraStateToUnity.Convert(State(0f, 1, 2, 3, 0, 0, 0, 30f, true));

            // eye == target, mapped with (-x, y, -z).
            Assert.That(pose.Position.x, Is.EqualTo(-1f).Within(0.001f));
            Assert.That(pose.Position.y, Is.EqualTo(2f).Within(0.001f));
            Assert.That(pose.Position.z, Is.EqualTo(-3f).Within(0.001f));
        }
    }
}
