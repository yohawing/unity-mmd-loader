#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

namespace Mmd.Rendering
{
    [Serializable]
    public sealed class MmdSubmeshDescriptor
    {
        public int submeshIndex;
        public int materialIndex;
        public int indexStart;
        public int indexCount;
    }

    public static class MmdSubmeshDescriptorBuilder
    {
        public static IReadOnlyList<MmdSubmeshDescriptor> Build(IReadOnlyList<MmdMaterialDescriptor> materials)
        {
            if (materials == null)
            {
                throw new ArgumentNullException(nameof(materials));
            }

            var submeshes = new List<MmdSubmeshDescriptor>(materials.Count);
            foreach (MmdMaterialDescriptor material in materials.OrderBy(material => material.materialIndex))
            {
                submeshes.Add(new MmdSubmeshDescriptor
                {
                    submeshIndex = submeshes.Count,
                    materialIndex = material.materialIndex,
                    indexStart = material.vertexStart,
                    indexCount = material.vertexCount
                });
            }

            return submeshes;
        }
    }
}
