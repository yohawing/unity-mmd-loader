#nullable enable

using System;
using UnityEngine;

namespace Yohawing.MmdUnity.UnityIntegration
{
    public static partial class MmdUnityModelFactory
    {
        public static MmdUnityModelInstance ApplyMaterialRemaps(
            MmdUnityModelInstance instance,
            Material[] remaps)
        {
            if (instance == null)
            {
                throw new ArgumentNullException(nameof(instance));
            }

            if (remaps == null || remaps.Length == 0)
            {
                return instance;
            }

            Material[] sceneMaterials = instance.Materials;
            Material[] resolved = (Material[])sceneMaterials.Clone();
            int count = Math.Min(resolved.Length, remaps.Length);
            bool changed = false;
            for (int i = 0; i < count; i++)
            {
                if (remaps[i] == null)
                {
                    continue;
                }

                resolved[i] = remaps[i];
                changed = true;
            }

            if (!changed)
            {
                return instance;
            }

            if (instance.SkinnedMeshRenderer != null)
            {
                instance.SkinnedMeshRenderer.sharedMaterials = resolved;
            }
            else if (instance.MeshRenderer != null)
            {
                instance.MeshRenderer.sharedMaterials = resolved;
            }

            return new MmdUnityModelInstance(
                instance.Root,
                instance.Mesh,
                resolved,
                instance.RenderingDescriptor,
                instance.BoneTransforms,
                instance.PhysicsBodies,
                instance.MeshRenderer,
                instance.SkinnedMeshRenderer,
                instance.SourceContext,
                instance.OwnedTextures,
                instance.TextureDiagnostics,
                instance.ShaderDiagnostics,
                instance.ImportScale);
        }
    }
}
