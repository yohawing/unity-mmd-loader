#nullable enable

using System;
using System.Collections.Generic;
using System.Reflection;
using Mmd.Parser;
using Mmd.UnityIntegration;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Mmd.Tests
{
    /// <summary>
    /// Pins the exception type, text and ordering of the three PMX bone topology checks (Humanoid
    /// native-array retarget, Humanoid host pose setup and HostRig world-matrix apply). They share one
    /// invariant (contiguous indices, parent inside the list) but each caller owns its own wording.
    /// </summary>
    [TestFixture]
    public sealed class MmdBoneTopologyValidationCharacterizationTests
    {
        private readonly List<Object> createdObjects = new();

        [TearDown]
        public void TearDown()
        {
            foreach (Object created in createdObjects)
            {
                if (created != null)
                {
                    Object.DestroyImmediate(created);
                }
            }

            createdObjects.Clear();
        }

        // ── Humanoid native-array retarget (ArgumentException) ──────────

        [Test]
        public void RetargetRejectsNonContiguousBoneIndex()
        {
            MmdModelDefinition model = CreateTwoBoneModel();
            model.bones[1].index = 5;

            ArgumentException exception = Assert.Throws<ArgumentException>(() => Retarget(model, CreateHierarchy().bones));

            Assert.That(exception.GetType(), Is.EqualTo(typeof(ArgumentException)));
            Assert.That(exception.ParamName, Is.EqualTo("model"));
            Assert.That(exception.Message, Does.StartWith("Model bone indices must be a unique contiguous mapping."));
        }

        [Test]
        public void RetargetRejectsNullBoneDefinitionAsNonContiguous()
        {
            MmdModelDefinition model = CreateTwoBoneModel();
            model.bones[1] = null!;

            ArgumentException exception = Assert.Throws<ArgumentException>(() => Retarget(model, CreateHierarchy().bones));

            Assert.That(exception.ParamName, Is.EqualTo("model"));
            Assert.That(exception.Message, Does.StartWith("Model bone indices must be a unique contiguous mapping."));
        }

        [TestCase(7)]
        [TestCase(2)]
        [TestCase(-2)]
        public void RetargetRejectsParentOutsideModel(int parentIndex)
        {
            MmdModelDefinition model = CreateTwoBoneModel();
            model.bones[1].parentIndex = parentIndex;

            ArgumentException exception = Assert.Throws<ArgumentException>(() => Retarget(model, CreateHierarchy().bones));

            Assert.That(exception.GetType(), Is.EqualTo(typeof(ArgumentException)));
            Assert.That(exception.ParamName, Is.EqualTo("model"));
            Assert.That(exception.Message, Does.StartWith($"Model parent index {parentIndex} is invalid for bone 1."));
        }

        [Test]
        public void RetargetReportsEarlierNullTransformBeforeLaterBadParent()
        {
            MmdModelDefinition model = CreateTwoBoneModel();
            model.bones[1].parentIndex = 7;
            Transform[] bones = CreateHierarchy().bones;
            bones[0] = null!;

            ArgumentException exception = Assert.Throws<ArgumentException>(() => Retarget(model, bones));

            Assert.That(exception.ParamName, Is.EqualTo("nativeTransforms"));
            Assert.That(exception.Message, Does.StartWith("Native transform 0 is null."));
        }

        // ── Humanoid host pose topology (InvalidOperationException) ─────

        [Test]
        public void HostPoseTopologyRejectsEmptyModel()
        {
            var model = new MmdModelDefinition();

            InvalidOperationException exception = InvokeHostPoseTopology(model);

            Assert.That(exception.Message, Is.EqualTo("Humanoid native host pose PMX bone topology is empty."));
        }

        [Test]
        public void HostPoseTopologyRejectsNonContiguousBoneIndex()
        {
            MmdModelDefinition model = CreateTwoBoneModel();
            model.bones[1].index = 5;

            InvalidOperationException exception = InvokeHostPoseTopology(model);

            Assert.That(
                exception.Message,
                Is.EqualTo("Humanoid native host pose PMX bone topology has a non-contiguous index at 1."));
        }

        [TestCase(7)]
        [TestCase(-2)]
        public void HostPoseTopologyRejectsParentOutsideModel(int parentIndex)
        {
            MmdModelDefinition model = CreateTwoBoneModel();
            model.bones[1].parentIndex = parentIndex;

            InvalidOperationException exception = InvokeHostPoseTopology(model);

            Assert.That(
                exception.Message,
                Is.EqualTo("Humanoid native host pose PMX parent index is invalid for bone 1: " + parentIndex + "."));
        }

        [Test]
        public void HostPoseTopologyRejectsParentCycle()
        {
            MmdModelDefinition model = CreateTwoBoneModel();
            model.bones[0].parentIndex = 1;

            InvalidOperationException exception = InvokeHostPoseTopology(model);

            Assert.That(
                exception.Message,
                Does.StartWith("Humanoid native host pose PMX bone topology contains a parent cycle at "));
        }

        [Test]
        public void HostPoseTopologyAcceptsValidModel()
        {
            Assert.DoesNotThrow(() => InvokeHostPoseTopologyRaw(CreateTwoBoneModel()));
        }

        // ── HostRig world-matrix apply (InvalidOperationException) ──────

        [Test]
        public void HostRigApplyRejectsEmptyModel()
        {
            (Transform root, _) = CreateHierarchy();

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
                MmdUnityWorldMatrixFrameApplier.ApplyHostRigColumnMajorWorldMatrices(
                    root,
                    Array.Empty<Transform>(),
                    1.0f,
                    new List<MmdBoneDefinition>(),
                    new float[32],
                    new HashSet<Transform>()));

            Assert.That(exception.Message, Is.EqualTo("HostRig PMX bone topology is empty."));
        }

        [Test]
        public void HostRigApplyRejectsNonContiguousBoneIndex()
        {
            MmdModelDefinition model = CreateTwoBoneModel();
            model.bones[1].index = 5;
            (Transform root, Transform[] bones) = CreateHierarchy();

            InvalidOperationException exception = ApplyHostRig(root, bones, model);

            Assert.That(exception.Message, Is.EqualTo("HostRig PMX bone topology has a non-contiguous index at 1."));
        }

        [TestCase(7)]
        [TestCase(-2)]
        public void HostRigApplyRejectsParentOutsideModel(int parentIndex)
        {
            MmdModelDefinition model = CreateTwoBoneModel();
            model.bones[1].parentIndex = parentIndex;
            (Transform root, Transform[] bones) = CreateHierarchy();

            InvalidOperationException exception = ApplyHostRig(root, bones, model);

            Assert.That(
                exception.Message,
                Is.EqualTo("HostRig PMX parent index is invalid for bone 1: " + parentIndex + "."));
        }

        [Test]
        public void HostRigApplyReportsEarlierMissingTransformBeforeLaterBadParent()
        {
            MmdModelDefinition model = CreateTwoBoneModel();
            model.bones[1].parentIndex = 7;
            (Transform root, Transform[] bones) = CreateHierarchy();
            bones[0] = null!;

            InvalidOperationException exception = ApplyHostRig(root, bones, model);

            Assert.That(exception.Message, Is.EqualTo("HostRig Unity bone transform is missing at index 0."));
        }

        [Test]
        public void HostRigApplyStillValidatesUnityParentEveryCall()
        {
            MmdModelDefinition model = CreateTwoBoneModel();
            (Transform root, Transform[] bones) = CreateHierarchy();
            var boneTransformSet = new HashSet<Transform>();
            float[] matrices = CreateIdentityMatrices(2);
            MmdUnityWorldMatrixFrameApplier.ApplyHostRigColumnMajorWorldMatrices(
                root, bones, 1.0f, model.bones, matrices, boneTransformSet);

            bones[1].SetParent(root, worldPositionStays: false);

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
                MmdUnityWorldMatrixFrameApplier.ApplyHostRigColumnMajorWorldMatrices(
                    root, bones, 1.0f, model.bones, matrices, boneTransformSet));
            Assert.That(exception.Message, Is.EqualTo("HostRig Unity parent does not match PMX parent for bone 1."));
        }

        // ── Helpers ─────────────────────────────────────────────────────

        private static MmdModelDefinition CreateTwoBoneModel()
        {
            var model = new MmdModelDefinition();
            model.bones.Add(new MmdBoneDefinition
            {
                index = 0,
                name = "root",
                parentIndex = -1,
                origin = new[] { 0.0f, 0.0f, 0.0f }
            });
            model.bones.Add(new MmdBoneDefinition
            {
                index = 1,
                name = "child",
                parentIndex = 0,
                origin = new[] { 0.0f, 1.0f, 0.0f }
            });
            return model;
        }

        private (Transform root, Transform[] bones) CreateHierarchy()
        {
            var root = new GameObject("TopologyRoot");
            createdObjects.Add(root);
            var bone0 = new GameObject("root");
            bone0.transform.SetParent(root.transform, worldPositionStays: false);
            var bone1 = new GameObject("child");
            bone1.transform.SetParent(bone0.transform, worldPositionStays: false);
            return (root.transform, new[] { bone0.transform, bone1.transform });
        }

        private static void Retarget(MmdModelDefinition model, Transform[] nativeTransforms)
        {
            int boneCount = model.bones.Count;
            MmdHumanoidRetargeter.RetargetPoseToNativeArrays(
                Array.Empty<MmdHumanoidRetargetBinding>(),
                model,
                nativeTransforms,
                importScale: 1.0f,
                new float[boneCount * 3],
                new float[boneCount * 4],
                new float[boneCount * 3]);
        }

        private static InvalidOperationException InvokeHostPoseTopology(MmdModelDefinition model)
        {
            TargetInvocationException wrapper = Assert.Throws<TargetInvocationException>(
                () => InvokeHostPoseTopologyRaw(model));
            Assert.That(wrapper.InnerException, Is.Not.Null);
            Assert.That(wrapper.InnerException!.GetType(), Is.EqualTo(typeof(InvalidOperationException)));
            return (InvalidOperationException)wrapper.InnerException;
        }

        private static void InvokeHostPoseTopologyRaw(MmdModelDefinition model)
        {
            MethodInfo method = typeof(MmdUnityPlaybackController).GetMethod(
                "ValidateHostPoseTopology",
                BindingFlags.NonPublic | BindingFlags.Static)!;
            Assert.That(method, Is.Not.Null);
            method.Invoke(null, new object[] { model });
        }

        private static InvalidOperationException ApplyHostRig(
            Transform root,
            Transform[] bones,
            MmdModelDefinition model)
        {
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
                MmdUnityWorldMatrixFrameApplier.ApplyHostRigColumnMajorWorldMatrices(
                    root,
                    bones,
                    1.0f,
                    model.bones,
                    CreateIdentityMatrices(model.bones.Count),
                    new HashSet<Transform>()));
            Assert.That(exception.GetType(), Is.EqualTo(typeof(InvalidOperationException)));
            return exception;
        }

        private static float[] CreateIdentityMatrices(int count)
        {
            var matrices = new float[count * 16];
            for (int i = 0; i < count; i++)
            {
                int offset = i * 16;
                matrices[offset] = 1.0f;
                matrices[offset + 5] = 1.0f;
                matrices[offset + 10] = 1.0f;
                matrices[offset + 15] = 1.0f;
            }

            return matrices;
        }
    }
}
