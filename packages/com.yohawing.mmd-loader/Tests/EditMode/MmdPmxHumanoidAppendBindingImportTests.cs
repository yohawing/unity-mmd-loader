#nullable enable

using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Mmd.Parser;
using Mmd.UnityIntegration;

namespace Mmd.Tests
{
    public sealed class MmdPmxHumanoidAppendBindingImportTests
    {
        [Test]
        public void BuildRuntimeAppendTransformBindingsUsesIrAppendDataAndSkinnedBones()
        {
            var model = new MmdModelDefinition();
            model.bones.Add(new MmdBoneDefinition
            {
                index = 0,
                name = "左足",
                parentIndex = -1,
                origin = new[] { 0f, 0f, 0f }
            });
            model.bones.Add(new MmdBoneDefinition
            {
                index = 1,
                name = "左足D",
                parentIndex = 2,
                appendParentIndex = 0,
                appendRatio = 1.0f,
                appendRotation = true,
                appendTranslation = false,
                appendLocal = false,
                origin = new[] { 0f, 0f, 0f }
            });
            model.bones.Add(new MmdBoneDefinition
            {
                index = 2,
                name = "腰キャンセル左",
                parentIndex = -1,
                origin = new[] { 0f, 0f, 0f }
            });

            GameObject root = new GameObject("imported-root");
            Mesh? mesh = null;
            try
            {
                Transform leftLeg = CreateChild(root.transform, "左足");
                Transform leftLegD = CreateChild(root.transform, "左足D");
                Transform cancel = CreateChild(root.transform, "腰キャンセル左");
                leftLeg.localRotation = Quaternion.Euler(1f, 2f, 3f);
                leftLegD.localRotation = Quaternion.Euler(4f, 5f, 6f);
                leftLeg.localPosition = new Vector3(1f, 2f, 3f);
                leftLegD.localPosition = new Vector3(4f, 5f, 6f);

                GameObject rendererObject = new GameObject("renderer");
                rendererObject.transform.SetParent(root.transform, worldPositionStays: false);
                SkinnedMeshRenderer smr = rendererObject.AddComponent<SkinnedMeshRenderer>();
                mesh = new Mesh();
                mesh.vertices = new Vector3[3];
                mesh.bindposes = new Matrix4x4[3];
                smr.sharedMesh = mesh;
                smr.bones = new[] { leftLeg, leftLegD, cancel };

                IReadOnlyList<MmdHumanoidAppendTransformBinding> bindings =
                    MmdPmxHumanoidAvatarImportBuilder.BuildRuntimeAppendTransformBindings(model, root);

                Assert.That(bindings, Has.Count.EqualTo(1));
                MmdHumanoidAppendTransformBinding binding = bindings[0];
                Assert.That(binding.TargetTransform, Is.SameAs(leftLegD));
                Assert.That(binding.TargetMmdBoneIndex, Is.EqualTo(1));
                Assert.That(binding.AppendParentTransform, Is.SameAs(leftLeg));
                Assert.That(binding.AppendParentMmdBoneIndex, Is.EqualTo(0));
                Assert.That(binding.AppendRatio, Is.EqualTo(1.0f));
                Assert.That(binding.AppendRotation, Is.True);
                Assert.That(binding.AppendTranslation, Is.False);
                Assert.That(binding.AppendLocal, Is.False);
                Assert.That(binding.TargetBindLocalRotation, Is.EqualTo(leftLegD.localRotation));
                Assert.That(binding.TargetBindLocalPosition, Is.EqualTo(leftLegD.localPosition));
                Assert.That(binding.AppendParentBindLocalRotation, Is.EqualTo(leftLeg.localRotation));
                Assert.That(binding.AppendParentBindLocalPosition, Is.EqualTo(leftLeg.localPosition));
                Assert.That(binding.EvaluationOrder, Is.EqualTo(1));
            }
            finally
            {
                if (mesh != null)
                {
                    Object.DestroyImmediate(mesh);
                }

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
