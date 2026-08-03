#nullable enable

using NUnit.Framework;
using System.Collections.Generic;
using Mmd.Parser;
using Mmd.Rendering;
using Mmd.UnityIntegration;
using UnityEngine;

namespace Mmd.Tests
{
    [TestFixture]
    public sealed class MmdUnityPlaybackWorksetTests
    {
        [Test]
        public void BoneWorksetKeepsSkinningAncestorsAndOmitsUnreferencedRoot()
        {
            MmdModelDefinition model = CreateFourBoneModel();
            MmdUnityModelInstance? instance = null;
            try
            {
                instance = MmdUnityModelFactory.CreateSkinnedModel(model);

                int[] result = MmdUnityPlaybackWorkset.BuildBoneIndices(model, instance);

                Assert.That(result, Is.EqualTo(new[] { 0, 1, 2 }));
            }
            finally
            {
                MmdTestInstanceScope.DestroyInstance(instance);
            }
        }

        [Test]
        public void AfterPhysicsWorksetContainsOnlyAfterPhysicsBones()
        {
            MmdModelDefinition model = CreateFourBoneModel();
            model.bones[2].deformAfterPhysics = true;
            model.bones[3].deformAfterPhysics = true;

            int[] result = MmdUnityPlaybackWorkset.BuildAfterPhysicsBoneIndices(model);

            Assert.That(result, Is.EqualTo(new[] { 2, 3 }));
        }

        [Test]
        public void MorphWorksetKeepsRenderableAndFlipDependenciesOnly()
        {
            MmdModelDefinition model = MmdTestFixtures.CreateMinimalTriangleModel("morph-workset");
            model.morphs.Add(CreateMorph(0, "unused"));
            model.morphs.Add(CreateMorph(1, "flip-source", "flip"));
            model.morphs.Add(CreateMorph(2, "face"));

            var descriptor = new MmdRenderingDescriptor();
            descriptor.vertexMorphs.Add(new MmdVertexMorphDescriptor
            {
                morphIndex = 2,
                morphName = "face"
            });
            descriptor.flipMorphs.Add(new MmdMorphDescriptorBuilder.MmdFlipMorphDescriptor
            {
                morphIndex = 1,
                morphName = "flip-source",
                offsets = new List<MmdMorphDescriptorBuilder.MmdFlipMorphOffsetDescriptor>
                {
                    new MmdMorphDescriptorBuilder.MmdFlipMorphOffsetDescriptor
                    {
                        targetMorphIndex = 2,
                        targetMorphName = "face",
                        weight = 1.0f,
                        finiteWeight = true
                    }
                }
            });

            int[] result = MmdUnityPlaybackWorkset.BuildMorphIndices(model, descriptor);

            Assert.That(result, Is.EqualTo(new[] { 1, 2 }));
        }

        [Test]
        public void SparseWorldMatrixApplyLeavesUnselectedBoneUntouched()
        {
            MmdModelDefinition model = CreateFourBoneModel();
            MmdUnityModelInstance? instance = null;
            try
            {
                instance = MmdUnityModelFactory.CreateSkinnedModel(model);
                Transform untouched = instance.BoneTransforms[3];
                untouched.position = new Vector3(9.0f, 8.0f, 7.0f);
                Vector3 untouchedBefore = untouched.position;
                float[] worldMatrices = CreateIdentityWorldMatrices(instance.BoneTransforms.Length);
                int selectedOffset = 2 * 16;
                worldMatrices[selectedOffset + 12] = 3.0f;
                worldMatrices[selectedOffset + 13] = 4.0f;
                worldMatrices[selectedOffset + 14] = 5.0f;

                MmdUnityWorldMatrixFrameApplier.ApplyColumnMajorWorldMatrices(
                    instance,
                    worldMatrices,
                    new[] { 2 });

                Assert.That(untouched.position, Is.EqualTo(untouchedBefore));
                Assert.That(instance.BoneTransforms[2].position, Is.EqualTo(new Vector3(-3.0f, 4.0f, -5.0f)));
            }
            finally
            {
                MmdTestInstanceScope.DestroyInstance(instance);
            }
        }

        private static MmdModelDefinition CreateFourBoneModel()
        {
            MmdModelDefinition model = MmdTestFixtures.CreateMinimalTriangleModel("playback-workset");
            model.bones.Add(new MmdBoneDefinition
            {
                index = 1,
                name = "middle",
                parentIndex = 0,
                transformOrder = 1,
                origin = new[] { 0.0f, 1.0f, 0.0f },
                isMovable = true,
                isRotatable = true
            });
            model.bones.Add(new MmdBoneDefinition
            {
                index = 2,
                name = "skinned-child",
                parentIndex = 1,
                transformOrder = 2,
                origin = new[] { 0.0f, 2.0f, 0.0f },
                isMovable = true,
                isRotatable = true
            });
            model.bones.Add(new MmdBoneDefinition
            {
                index = 3,
                name = "unreferenced-root",
                parentIndex = -1,
                transformOrder = 3,
                origin = new[] { 10.0f, 0.0f, 0.0f },
                isMovable = true,
                isRotatable = true
            });
            foreach (MmdVertexDefinition vertex in model.vertices)
            {
                vertex.boneIndices = new[] { 2 };
                vertex.boneWeights = new[] { 1.0f };
            }

            return model;
        }

        private static MmdMorphDefinition CreateMorph(int index, string name, string type = "vertex")
        {
            return new MmdMorphDefinition
            {
                index = index,
                name = name,
                type = type,
                panel = "face"
            };
        }

        private static float[] CreateIdentityWorldMatrices(int boneCount)
        {
            var result = new float[boneCount * 16];
            for (int boneIndex = 0; boneIndex < boneCount; boneIndex++)
            {
                int offset = boneIndex * 16;
                result[offset] = 1.0f;
                result[offset + 5] = 1.0f;
                result[offset + 10] = 1.0f;
                result[offset + 15] = 1.0f;
            }

            return result;
        }
    }
}
