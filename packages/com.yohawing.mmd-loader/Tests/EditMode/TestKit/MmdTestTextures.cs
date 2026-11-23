#nullable enable

using System.IO;
using UnityEngine;

namespace Mmd.Tests
{
    /// <summary>
    /// Texture helpers shared by EditMode tests that write PNG fixtures or inspect bound materials.
    /// </summary>
    internal static class MmdTestTextures
    {
        internal static void WritePng(string path, Color color)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false);
            try
            {
                texture.SetPixels(new[] { color, color, color, color });
                texture.Apply();
                File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }

        internal static uint Adler32(byte[] data)
        {
            const uint Mod = 65521;
            uint a = 1;
            uint b = 0;
            foreach (byte value in data)
            {
                a = (a + value) % Mod;
                b = (b + a) % Mod;
            }

            return (b << 16) | a;
        }

        internal static Texture? ReadBoundDiffuseTexture(Material material)
        {
            if (material.HasProperty("_BaseMap"))
            {
                Texture texture = material.GetTexture("_BaseMap");
                if (texture != null)
                {
                    return texture;
                }
            }

            return material.HasProperty("_MainTex")
                ? material.GetTexture("_MainTex")
                : null;
        }
    }
}
