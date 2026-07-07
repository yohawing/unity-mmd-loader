#nullable enable

using System;
using System.Linq;
using Mmd.UnityIntegration;
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
            if (Instance.Root != null)
            {
                UnityEngine.Object.DestroyImmediate(Instance.Root);
            }

            if (Instance.Mesh != null)
            {
                UnityEngine.Object.DestroyImmediate(Instance.Mesh);
            }

            if (Instance.Materials == null)
            {
                return;
            }

            foreach (Material material in Instance.Materials.Where(material => material != null).Distinct())
            {
                UnityEngine.Object.DestroyImmediate(material);
            }

            foreach (Texture2D texture in Instance.OwnedTextures.Where(texture => texture != null).Distinct())
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }
    }
}