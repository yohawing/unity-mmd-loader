#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Mmd.Parser;

namespace Mmd.Rendering
{
    [Serializable]
    public sealed class MmdRenderingDescriptor
    {
        public List<MmdMeshVertexDescriptor> vertices = new();
        public List<int> indices = new();
        public List<MmdSkinningDescriptor> skinning = new();
        public List<MmdMaterialDescriptor> materials = new();
        public List<MmdSubmeshDescriptor> submeshes = new();
        public List<MmdUrpMaterialBindingDescriptor> urpMaterialBindings = new();
        public List<MmdVertexMorphDescriptor> vertexMorphs = new();
        public List<MmdGroupMorphDescriptor> groupMorphs = new();
        public List<MmdUvMorphDescriptor> uvMorphs = new();
        public List<MmdMaterialMorphDescriptor> materialMorphs = new();
        public List<MmdFlipMorphDescriptor> flipMorphs = new();
        public MmdTextureOrientationDescriptor textureOrientation = new();
        public int ikCount;
    }

    public static class MmdRenderingDescriptorBuilder
    {
        public static MmdRenderingDescriptor Build(MmdModelDefinition model)
        {
            return Build(model, MmdMaterialPreset.MmdToon);
        }

        public static MmdRenderingDescriptor Build(MmdModelDefinition model, MmdMaterialPreset preset)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            // Validate once; the sub-builders below run on the validated model.
            MmdModelValidator.ThrowIfInvalid(model);
            List<MmdMaterialDescriptor> materials = MmdMaterialDescriptorBuilder.BuildFromValidated(model).ToList();
            List<MmdMeshVertexDescriptor> vertices = MmdMeshDescriptorBuilder.BuildVerticesFromValidated(model).ToList();
            return new MmdRenderingDescriptor
            {
                vertices = vertices,
                indices = MmdMeshDescriptorBuilder.BuildIndicesFromValidated(model).ToList(),
                skinning = MmdSkinningDescriptorBuilder.BuildFromValidated(model).ToList(),
                materials = materials,
                submeshes = MmdSubmeshDescriptorBuilder.Build(materials).ToList(),
                urpMaterialBindings = MmdUrpMaterialBindingDescriptorBuilder.Build(materials, preset).ToList(),
                vertexMorphs = MmdMorphDescriptorBuilder.BuildVertexMorphsFromValidated(model).ToList(),
                groupMorphs = MmdMorphDescriptorBuilder.BuildGroupMorphsFromValidated(model).ToList(),
                uvMorphs = MmdMorphDescriptorBuilder.BuildUvMorphsFromValidated(model).ToList(),
                materialMorphs = MmdMorphDescriptorBuilder.BuildMaterialMorphsFromValidated(model).ToList(),
                flipMorphs = MmdMorphDescriptorBuilder.BuildFlipMorphsFromValidated(model).ToList(),
                textureOrientation = MmdTextureOrientationDescriptorBuilder.Build(vertices),
                ikCount = model.ik.Count
            };
        }
    }
}
