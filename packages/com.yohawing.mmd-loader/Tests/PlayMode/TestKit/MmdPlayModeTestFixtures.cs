#nullable enable

using Mmd.Parser;

namespace Mmd.Tests
{
    internal static class MmdPlayModeTestFixtures
    {
        internal const string MinimalTriangleModelName = "minimal-playmode-triangle";

        internal static MmdModelDefinition CreateMinimalTriangleModel(string modelName)
        {
            var model = new MmdModelDefinition
            {
                name = modelName
            };
            model.bones.Add(new MmdBoneDefinition
            {
                index = 0,
                name = "root",
                parentIndex = -1,
                transformOrder = 0,
                origin = new[] { 0.0f, 0.0f, 0.0f },
                isMovable = true,
                isRotatable = true
            });
            model.vertices.Add(CreateSyntheticVertex(0, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f));
            model.vertices.Add(CreateSyntheticVertex(1, 1.0f, 0.0f, 0.0f, 1.0f, 0.0f));
            model.vertices.Add(CreateSyntheticVertex(2, 0.0f, 1.0f, 0.0f, 0.0f, 1.0f));
            model.indices.AddRange(new[] { 0, 1, 2 });
            model.materials.Add(new MmdMaterialDefinition
            {
                index = 0,
                name = "triangle-material",
                vertexCount = 3
            });
            return model;
        }

        internal static MmdMotionDefinition CreateRootTranslationMotion(string targetModelName, int maxFrame = 10)
        {
            var motion = new MmdMotionDefinition
            {
                targetModelName = targetModelName,
                maxFrame = maxFrame
            };
            motion.boneKeyframes.Add(new MmdBoneKeyframeDefinition
            {
                boneName = "root",
                frame = 0,
                translation = new[] { 0.0f, 0.0f, 0.0f },
                rotation = new[] { 0.0f, 0.0f, 0.0f, 1.0f },
                interpolation = LinearBoneInterpolation()
            });
            motion.boneKeyframes.Add(new MmdBoneKeyframeDefinition
            {
                boneName = "root",
                frame = maxFrame,
                translation = new[] { 2.0f, 0.0f, 0.0f },
                rotation = new[] { 0.0f, 0.0f, 0.0f, 1.0f },
                interpolation = LinearBoneInterpolation()
            });
            return motion;
        }

        internal static MmdBoneInterpolationDefinition LinearBoneInterpolation()
        {
            byte[] linear = { 20, 20, 107, 107 };
            return new MmdBoneInterpolationDefinition
            {
                translationX = linear,
                translationY = linear,
                translationZ = linear,
                rotation = linear
            };
        }

        internal static MmdVertexDefinition CreateSyntheticVertex(
            int index,
            float x,
            float y,
            float z,
            float u,
            float v)
        {
            return new MmdVertexDefinition
            {
                index = index,
                position = new[] { x, y, z },
                normal = new[] { 0.0f, 0.0f, 1.0f },
                uv = new[] { u, v },
                boneIndices = new[] { 0 },
                boneWeights = new[] { 1.0f }
            };
        }
    }
}