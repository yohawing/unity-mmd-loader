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
        public List<MmdMorphDescriptorBuilder.MmdUvMorphDescriptor> uvMorphs = new();
        public List<MmdMorphDescriptorBuilder.MmdMaterialMorphDescriptor> materialMorphs = new();
        public List<MmdMorphDescriptorBuilder.MmdFlipMorphDescriptor> flipMorphs = new();
        public MmdTextureOrientationDescriptor textureOrientation = new();
        public int ikCount;
    }

    public static class MmdRenderingDescriptorBuilder
    {
        public static MmdRenderingDescriptor Build(MmdModelDefinition model)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            MmdModelValidator.ThrowIfInvalid(model);
            List<MmdMaterialDescriptor> materials = MmdMaterialDescriptorBuilder.Build(model).ToList();
            List<MmdMeshVertexDescriptor> vertices = MmdMeshDescriptorBuilder.BuildVertices(model).ToList();
            return new MmdRenderingDescriptor
            {
                vertices = vertices,
                indices = MmdMeshDescriptorBuilder.BuildIndices(model).ToList(),
                skinning = MmdSkinningDescriptorBuilder.Build(model).ToList(),
                materials = materials,
                submeshes = MmdSubmeshDescriptorBuilder.Build(materials).ToList(),
                urpMaterialBindings = MmdUrpMaterialBindingDescriptorBuilder.Build(materials).ToList(),
                vertexMorphs = MmdMorphDescriptorBuilder.BuildVertexMorphs(model).ToList(),
                groupMorphs = MmdMorphDescriptorBuilder.BuildGroupMorphs(model).ToList(),
                uvMorphs = MmdMorphDescriptorBuilder.BuildUvMorphs(model).ToList(),
                materialMorphs = MmdMorphDescriptorBuilder.BuildMaterialMorphs(model).ToList(),
                flipMorphs = MmdMorphDescriptorBuilder.BuildFlipMorphs(model).ToList(),
                textureOrientation = MmdTextureOrientationDescriptorBuilder.Build(vertices),
                ikCount = model.ik.Count
            };
        }
    }
}
