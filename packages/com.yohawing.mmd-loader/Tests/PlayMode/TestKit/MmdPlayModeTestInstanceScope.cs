#nullable enable

using System.Linq;
using Mmd.UnityIntegration;
using UnityEngine;

namespace Mmd.Tests
{
    internal static class MmdPlayModeTestInstanceScope
    {
        internal static void DestroyInstance(MmdUnityModelInstance? instance)
        {
            if (instance == null)
            {
                return;
            }

            if (instance.Root != null)
            {
                Object.Destroy(instance.Root);
            }

            if (instance.Mesh != null)
            {
                Object.Destroy(instance.Mesh);
            }

            if (instance.Materials == null)
            {
                return;
            }

            foreach (Material material in instance.Materials.Where(material => material != null).Distinct())
            {
                Object.Destroy(material);
            }

            foreach (Texture2D texture in instance.OwnedTextures.Where(texture => texture != null).Distinct())
            {
                Object.Destroy(texture);
            }
        }
    }
}