#nullable enable

using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Mmd.Tests
{
    public sealed class MmdHumanoidAppendTransformApplierTests
    {
        [TestCase(1.0f)]
        [TestCase(0.5f)]
        [TestCase(-1.0f)]
        public void ApplyInheritsParentRotationFromBindPose(float ratio)
        {
            GameObject root = new GameObject("append-root");
            try
            {
                Transform parent = CreateChild(root.transform, "parent");
                Transform target = CreateChild(root.transform, "target");
                Quaternion targetBind = Quaternion.Euler(10f, 20f, 30f);
                Quaternion parentBind = Quaternion.Euler(3f, 4f, 5f);
                target.localRotation = targetBind;
                parent.localRotation = parentBind;

                var entry = new MmdHumanoidAppendTransformBinding(
                    target,
                    targetMmdBoneIndex: 1,
                    parent,
                    appendParentMmdBoneIndex: 0,
                    appendRatio: ratio,
                    appendRotation: true,
                    appendTranslation: false,
                    appendLocal: true,
                    targetBind,
                    target.localPosition,
                    parentBind,
                    parent.localPosition,
                    evaluationOrder: 1);

                Quaternion parentDriven = parentBind * Quaternion.Euler(0f, 40f, 0f);
                parent.localRotation = parentDriven;

                MmdHumanoidAppendTransformApplier.Apply(new[] { entry });

                Quaternion parentDelta = Quaternion.Inverse(parentBind) * parentDriven;
                Quaternion expected = targetBind * Quaternion.SlerpUnclamped(Quaternion.identity, parentDelta, ratio);
                Assert.That(Quaternion.Angle(target.localRotation, expected), Is.LessThan(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ApplyInheritsParentTranslationFromBindPose()
        {
            GameObject root = new GameObject("append-translation-root");
            try
            {
                Transform parent = CreateChild(root.transform, "parent");
                Transform target = CreateChild(root.transform, "target");
                Vector3 parentBind = new Vector3(1f, 2f, 3f);
                Vector3 targetBind = new Vector3(10f, 20f, 30f);
                parent.localPosition = parentBind;
                target.localPosition = targetBind;

                var entry = new MmdHumanoidAppendTransformBinding(
                    target,
                    targetMmdBoneIndex: 1,
                    parent,
                    appendParentMmdBoneIndex: 0,
                    appendRatio: 0.25f,
                    appendRotation: false,
                    appendTranslation: true,
                    appendLocal: true,
                    target.localRotation,
                    targetBind,
                    parent.localRotation,
                    parentBind,
                    evaluationOrder: 1);

                parent.localPosition = parentBind + new Vector3(8f, -4f, 2f);

                MmdHumanoidAppendTransformApplier.Apply(new[] { entry });

                Assert.That(target.localPosition, Is.EqualTo(targetBind + new Vector3(2f, -1f, 0.5f)));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ApplyRecomputesFromBindPoseWithoutAccumulation()
        {
            GameObject root = new GameObject("append-nonaccum-root");
            try
            {
                Transform parent = CreateChild(root.transform, "parent");
                Transform target = CreateChild(root.transform, "target");
                Quaternion targetBind = Quaternion.Euler(5f, 0f, 0f);
                target.localRotation = targetBind;

                var entry = new MmdHumanoidAppendTransformBinding(
                    target,
                    targetMmdBoneIndex: 1,
                    parent,
                    appendParentMmdBoneIndex: 0,
                    appendRatio: 0.5f,
                    appendRotation: true,
                    appendTranslation: false,
                    appendLocal: true,
                    targetBind,
                    target.localPosition,
                    Quaternion.identity,
                    parent.localPosition,
                    evaluationOrder: 1);

                parent.localRotation = Quaternion.Euler(0f, 30f, 0f);
                MmdHumanoidAppendTransformApplier.Apply(new[] { entry });
                parent.localRotation = Quaternion.Euler(0f, 60f, 0f);
                MmdHumanoidAppendTransformApplier.Apply(new[] { entry });

                Quaternion expected = targetBind * Quaternion.SlerpUnclamped(
                    Quaternion.identity,
                    Quaternion.Euler(0f, 60f, 0f),
                    0.5f);
                Assert.That(Quaternion.Angle(target.localRotation, expected), Is.LessThan(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ApplyUsesEvaluationOrderForChainedAppendLocalFalse()
        {
            GameObject root = new GameObject("append-chain-root");
            try
            {
                Transform source = CreateChild(root.transform, "source");
                Transform middle = CreateChild(root.transform, "middle");
                Transform child = CreateChild(root.transform, "child");
                Quaternion sourceDriven = Quaternion.Euler(0f, 80f, 0f);

                var middleEntry = new MmdHumanoidAppendTransformBinding(
                    middle,
                    targetMmdBoneIndex: 1,
                    source,
                    appendParentMmdBoneIndex: 0,
                    appendRatio: 0.5f,
                    appendRotation: true,
                    appendTranslation: false,
                    appendLocal: true,
                    Quaternion.identity,
                    middle.localPosition,
                    Quaternion.identity,
                    source.localPosition,
                    evaluationOrder: 1);
                var childEntry = new MmdHumanoidAppendTransformBinding(
                    child,
                    targetMmdBoneIndex: 2,
                    middle,
                    appendParentMmdBoneIndex: 1,
                    appendRatio: 0.5f,
                    appendRotation: true,
                    appendTranslation: false,
                    appendLocal: false,
                    Quaternion.identity,
                    child.localPosition,
                    Quaternion.identity,
                    middle.localPosition,
                    evaluationOrder: 2);

                source.localRotation = sourceDriven;

                MmdHumanoidAppendTransformApplier.Apply(new[] { childEntry, middleEntry });

                Quaternion middleWeighted = Quaternion.SlerpUnclamped(Quaternion.identity, sourceDriven, 0.5f);
                Quaternion childExpected = Quaternion.SlerpUnclamped(Quaternion.identity, middleWeighted, 0.5f);
                Assert.That(Quaternion.Angle(middle.localRotation, middleWeighted), Is.LessThan(0.001f));
                Assert.That(Quaternion.Angle(child.localRotation, childExpected), Is.LessThan(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static Transform CreateChild(Transform parent, string name)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent, worldPositionStays: false);
            return child.transform;
        }
    }
}
