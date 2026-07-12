#nullable enable

using System;
using System.Linq;
using Mmd.UnityIntegration;
using UnityEditor;
using UnityEngine;

namespace Mmd.Tests
{
    internal sealed class MmdTestInstanceScope : IDisposable
    {
        public MmdUnityModelInstance Instance { get; }

        public MmdTestInstanceScope(MmdUnityModelInstance instance)
        {
            Instance = instance ?? throw new ArgumentNullException(nameof(instance));
        }

        public void Dispose()
        {
            DestroyInstance(Instance);
        }

        internal static void DestroyInstance(MmdUnityModelInstance? instance)
        {
            if (instance == null)
            {
                return;
            }

            if (instance.Root != null)
            {
                UnityEngine.Object.DestroyImmediate(instance.Root);
            }

            if (instance.Mesh != null)
            {
                UnityEngine.Object.DestroyImmediate(instance.Mesh);
            }

            if (instance.Materials != null)
            {
                foreach (Material material in instance.Materials.Where(material => material != null).Distinct())
                {
                    UnityEngine.Object.DestroyImmediate(material);
                }
            }

            foreach (Texture2D texture in instance.OwnedTextures.Where(texture => texture != null).Distinct())
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        internal static void DestroyImporterCacheInstance(MmdUnityModelInstance? instance)
        {
            if (instance == null)
            {
                return;
            }

            if (instance.Root != null)
            {
                UnityEngine.Object.DestroyImmediate(instance.Root);
            }

            if (instance.Mesh != null && !AssetDatabase.Contains(instance.Mesh))
            {
                UnityEngine.Object.DestroyImmediate(instance.Mesh);
            }

            foreach (Material material in instance.Materials)
            {
                if (material != null && !AssetDatabase.Contains(material))
                {
                    UnityEngine.Object.DestroyImmediate(material);
                }
            }

            foreach (Texture2D texture in instance.OwnedTextures)
            {
                if (texture != null && !AssetDatabase.Contains(texture))
                {
                    UnityEngine.Object.DestroyImmediate(texture);
                }
            }
        }
    }
}
