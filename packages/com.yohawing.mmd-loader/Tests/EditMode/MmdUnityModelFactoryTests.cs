#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Mmd.Parser;
using Mmd.Rendering;
using Mmd.UnityIntegration;

namespace Mmd.Tests
{
    public sealed partial class MmdUnityModelFactoryTests
    {























































        private static MmdModelDefinition CreateMinimalTriangleModel(bool includeTextureReferences)
        {
            var model = new MmdModelDefinition
            {
                name = "minimal-static-triangle"
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
            model.bones.Add(new MmdBoneDefinition
            {
                index = 1,
                name = "child",
                parentIndex = 0,
                transformOrder = 0,
                origin = new[] { 0.0f, 1.0f, 0.0f },
                isMovable = true,
                isRotatable = true
            });
            model.vertices.Add(CreateVertex(0, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f));
            model.vertices.Add(CreateVertex(1, 1.0f, 0.0f, 0.0f, 1.0f, 0.0f));
            model.vertices.Add(CreateVertex(2, 0.0f, 1.0f, 0.0f, 0.0f, 1.0f));
            model.indices.AddRange(new[] { 0, 1, 2 });
            model.materials.Add(new MmdMaterialDefinition
            {
                index = 0,
                name = "triangle-material",
                texture = includeTextureReferences ? "diffuse.png" : string.Empty,
                sphereTexture = includeTextureReferences ? "sphere.spa" : string.Empty,
                toonTexture = includeTextureReferences ? "toon.bmp" : string.Empty,
                vertexCount = 3
            });
            return model;
        }

        private static MmdModelDefinition CreateTextureUvMorphTriangleModel()
        {
            var model = new MmdModelDefinition
            {
                name = "texture-uv-morph-triangle"
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
            model.bones.Add(new MmdBoneDefinition
            {
                index = 1,
                name = "child",
                parentIndex = 0,
                transformOrder = 0,
                origin = new[] { 0.0f, 1.0f, 0.0f },
                isMovable = true,
                isRotatable = true
            });
            model.vertices.Add(CreateVertex(0, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f));
            model.vertices.Add(CreateVertex(1, 1.0f, 0.0f, 0.0f, 1.0f, 0.0f));
            model.vertices.Add(CreateVertex(2, 0.0f, 1.0f, 0.0f, 0.0f, 1.0f));
            model.indices.AddRange(new[] { 0, 1, 2 });
            model.materials.Add(new MmdMaterialDefinition
            {
                index = 0,
                name = "uv-morph-material",
                vertexCount = 3
            });
            // Texture UV morph that moves vertex 1 main UV by (0.25, 0.5).
            model.morphs.Add(new MmdMorphDefinition
            {
                index = 0,
                name = "uv-shift",
                type = "texture",
                panel = "other",
                uvOffsets =
                {
                    new MmdUvMorphOffsetDefinition
                    {
                        vertexIndex = 1,
                        positionDelta = new[] { 0.25f, 0.5f, 0.0f, 0.0f }
                    }
                }
            });
            return model;
        }

        private static MmdModelDefinition CreateSharedVertexTwoSubmeshMorphModel()
        {
            var model = new MmdModelDefinition
            {
                name = "shared-vertex-two-submesh"
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
            MmdVertexDefinition sharedSdefVertex = CreateVertex(0, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f);
            sharedSdefVertex.skinningMode = "sdef";
            sharedSdefVertex.boneIndices = new[] { 0, 0 };
            sharedSdefVertex.boneWeights = new[] { 0.6f, 0.4f };
            model.vertices.Add(sharedSdefVertex);
            model.vertices.Add(CreateVertex(1, 1.0f, 0.0f, 0.0f, 1.0f, 0.0f));
            model.vertices.Add(CreateVertex(2, 0.0f, 1.0f, 0.0f, 0.0f, 1.0f));
            model.vertices.Add(CreateVertex(3, -1.0f, 0.0f, 0.0f, 1.0f, 1.0f));
            model.indices.AddRange(new[] { 0, 1, 2, 0, 2, 3 });
            model.materials.Add(new MmdMaterialDefinition
            {
                index = 0,
                name = "first-submesh",
                vertexCount = 3
            });
            model.materials.Add(new MmdMaterialDefinition
            {
                index = 1,
                name = "second-submesh",
                vertexCount = 3
            });
            model.morphs.Add(new MmdMorphDefinition
            {
                index = 0,
                name = "shared-up",
                type = "vertex",
                panel = "eye",
                vertexOffsets =
                {
                    new MmdVertexMorphOffsetDefinition
                    {
                        vertexIndex = 0,
                        positionDelta = new[] { 0.0f, 1.0f, 0.0f }
                    }
                }
            });
            return model;
        }

        private static MmdModelDefinition CreateDuplicateNameVertexMorphModel()
        {
            MmdModelDefinition model = CreateMinimalTriangleModel(includeTextureReferences: false);
            model.name = "duplicate-name-vertex-morph";
            model.morphs.Add(new MmdMorphDefinition
            {
                index = 0,
                name = "duplicate",
                type = "vertex",
                panel = "eye",
                vertexOffsets =
                {
                    new MmdVertexMorphOffsetDefinition
                    {
                        vertexIndex = 1,
                        positionDelta = new[] { 0.0f, 1.0f, 0.0f }
                    }
                }
            });
            model.morphs.Add(new MmdMorphDefinition
            {
                index = 1,
                name = "duplicate",
                type = "vertex",
                panel = "eye",
                vertexOffsets =
                {
                    new MmdVertexMorphOffsetDefinition
                    {
                        vertexIndex = 2,
                        positionDelta = new[] { 0.0f, 2.0f, 0.0f }
                    }
                }
            });
            return model;
        }

        private static MmdModelDefinition CreateDuplicateOffsetVertexMorphModel()
        {
            MmdModelDefinition model = CreateMinimalTriangleModel(includeTextureReferences: false);
            model.name = "duplicate-offset-vertex-morph";
            model.morphs.Add(new MmdMorphDefinition
            {
                index = 0,
                name = "stacked",
                type = "vertex",
                panel = "eye",
                vertexOffsets =
                {
                    new MmdVertexMorphOffsetDefinition
                    {
                        vertexIndex = 1,
                        positionDelta = new[] { 0.0f, 0.25f, 0.0f }
                    },
                    new MmdVertexMorphOffsetDefinition
                    {
                        vertexIndex = 1,
                        positionDelta = new[] { 0.0f, 0.75f, 0.0f }
                    }
                }
            });
            return model;
        }

        private static MmdModelDefinition CreateSharedVertexTwoSubmeshTextureUvMorphModel()
        {
            var model = new MmdModelDefinition
            {
                name = "shared-vertex-uv-morph"
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
            model.vertices.Add(CreateVertex(0, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f));
            model.vertices.Add(CreateVertex(1, 1.0f, 0.0f, 0.0f, 1.0f, 0.0f));
            model.vertices.Add(CreateVertex(2, 0.0f, 1.0f, 0.0f, 0.0f, 1.0f));
            model.vertices.Add(CreateVertex(3, -1.0f, 0.0f, 0.0f, 1.0f, 1.0f));
            model.indices.AddRange(new[] { 0, 1, 2, 0, 2, 3 });
            model.materials.Add(new MmdMaterialDefinition
            {
                index = 0,
                name = "first-submesh",
                vertexCount = 3
            });
            model.materials.Add(new MmdMaterialDefinition
            {
                index = 1,
                name = "second-submesh",
                vertexCount = 3
            });
            // Texture UV morph targeting vertex 0 which spans both submeshes.
            model.morphs.Add(new MmdMorphDefinition
            {
                index = 0,
                name = "uv-shift",
                type = "texture",
                panel = "other",
                uvOffsets =
                {
                    new MmdUvMorphOffsetDefinition
                    {
                        vertexIndex = 0,
                        positionDelta = new[] { 0.25f, 0.5f, 0.0f, 0.0f }
                    }
                }
            });
            return model;
        }

        private static MmdModelDefinition CreateTwoTransparentTriangleModel()
        {
            var model = new MmdModelDefinition
            {
                name = "two-transparent-triangles"
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
            model.vertices.Add(CreateVertex(0, -0.5f, 0.5f, 0.0f, 0.0f, 0.0f));
            model.vertices.Add(CreateVertex(1, 0.5f, 0.5f, 0.0f, 1.0f, 0.0f));
            model.vertices.Add(CreateVertex(2, -0.5f, -0.5f, 0.0f, 0.0f, 1.0f));
            model.vertices.Add(CreateVertex(3, 0.5f, 0.5f, 0.0f, 1.0f, 0.0f));
            model.vertices.Add(CreateVertex(4, 0.5f, -0.5f, 0.0f, 1.0f, 1.0f));
            model.vertices.Add(CreateVertex(5, -0.5f, -0.5f, 0.0f, 0.0f, 1.0f));
            model.indices.AddRange(new[] { 0, 1, 2, 3, 4, 5 });
            model.materials.Add(new MmdMaterialDefinition
            {
                index = 0,
                name = "transparent-front-a",
                alpha = 0.45f,
                vertexCount = 3
            });
            model.materials.Add(new MmdMaterialDefinition
            {
                index = 1,
                name = "transparent-front-b",
                alpha = 0.45f,
                vertexCount = 3
            });
            return model;
        }

        private static MmdEvaluatedFrame CreateFrame(params MmdEvaluatedBonePose[] bones)
        {
            return new MmdEvaluatedFrame
            {
                frame = 5,
                time = 5.0f / 30.0f,
                bones = new List<MmdEvaluatedBonePose>(bones)
            };
        }

        private static float[] CreateColumnMajorWorldMatrix(Vector3 position, Quaternion rotation)
        {
            Matrix4x4 matrix = Matrix4x4.TRS(position, rotation, Vector3.one);
            return new[]
            {
                matrix[0, 0], matrix[1, 0], matrix[2, 0], matrix[3, 0],
                matrix[0, 1], matrix[1, 1], matrix[2, 1], matrix[3, 1],
                matrix[0, 2], matrix[1, 2], matrix[2, 2], matrix[3, 2],
                matrix[0, 3], matrix[1, 3], matrix[2, 3], matrix[3, 3]
            };
        }

        private static float[] Concatenate(float[] first, float[] second)
        {
            var combined = new float[first.Length + second.Length];
            Array.Copy(first, 0, combined, 0, first.Length);
            Array.Copy(second, 0, combined, first.Length, second.Length);
            return combined;
        }

        private static Quaternion ToUnityModelRotation(Quaternion rotation)
        {
            return new Quaternion(-rotation.x, rotation.y, -rotation.z, rotation.w);
        }

        private static MmdMotionDefinition CreateRootTranslationMotion()
        {
            var motion = new MmdMotionDefinition
            {
                targetModelName = "minimal-static-triangle",
                maxFrame = 10
            };
            motion.boneKeyframes.Add(new MmdBoneKeyframeDefinition
            {
                boneName = "root",
                frame = 0,
                translation = new[] { 0.0f, 0.0f, 0.0f },
                rotation = new[] { 0.0f, 0.0f, 0.0f, 1.0f },
                interpolation = LinearInterpolation()
            });
            motion.boneKeyframes.Add(new MmdBoneKeyframeDefinition
            {
                boneName = "root",
                frame = 10,
                translation = new[] { 2.0f, 0.0f, 0.0f },
                rotation = new[] { 0.0f, 0.0f, 0.0f, 1.0f },
                interpolation = LinearInterpolation()
            });
            return motion;
        }

        private static (MmdModelDefinition Model, MmdMotionDefinition Motion) LoadPlaybackFixturePair()
        {
            var parser = new NativeMmdParser();
            MmdModelDefinition model = parser.LoadModel(MmdTestFixtures.ReadFixtureAssetBytes("test_1bone_cube.pmx"));
            MmdMotionDefinition motion = parser.LoadMotion(MmdTestFixtures.ReadFixtureAssetBytes("test_1bone_cube_motion.vmd"));
            return (model, motion);
        }

        private static (MmdModelDefinition Model, MmdMotionDefinition Motion) LoadVertexMorphFixturePair()
        {
            var parser = new NativeMmdParser();
            MmdModelDefinition model = parser.LoadModel(MmdTestFixtures.ReadFixtureAssetBytes("test_vertex_morph.pmx"));
            MmdMotionDefinition motion = parser.LoadMotion(MmdTestFixtures.ReadFixtureAssetBytes("test_vertex_morph_motion.vmd"));
            return (model, motion);
        }

        private static MmdBoneInterpolationDefinition LinearInterpolation()
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

        private static MmdEvaluatedBonePose CreateBonePose(int index, string name, float x, float y, float z)
        {
            return new MmdEvaluatedBonePose
            {
                index = index,
                name = name,
                localPosition = new[] { x, y, z },
                localRotation = new[] { 0.0f, 0.0f, 0.0f, 1.0f },
                localScale = new[] { 1.0f, 1.0f, 1.0f },
                worldMatrix = new[]
                {
                    1.0f, 0.0f, 0.0f, x,
                    0.0f, 1.0f, 0.0f, y,
                    0.0f, 0.0f, 1.0f, z,
                    0.0f, 0.0f, 0.0f, 1.0f
                }
            };
        }

        private static MmdModelDefinition CreateTexturedQuadModel(string texture)
        {
            var model = new MmdModelDefinition
            {
                name = "viewport-texture-orientation-quad"
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
            model.vertices.Add(CreateVertex(0, -1.0f, 1.0f, 0.0f, 0.0f, 0.0f));
            model.vertices.Add(CreateVertex(1, 1.0f, 1.0f, 0.0f, 1.0f, 0.0f));
            model.vertices.Add(CreateVertex(2, 1.0f, -1.0f, 0.0f, 1.0f, 1.0f));
            model.vertices.Add(CreateVertex(3, -1.0f, -1.0f, 0.0f, 0.0f, 1.0f));
            model.indices.AddRange(new[] { 0, 1, 2, 0, 2, 3 });
            model.materials.Add(new MmdMaterialDefinition
            {
                index = 0,
                name = "viewport-texture-orientation-material",
                texture = texture,
                vertexCount = 6
            });
            return model;
        }


        private static void WritePng(string path, Color color)
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
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private static void WriteCutoutPng(string path)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false);
            try
            {
                texture.SetPixels(new[]
                {
                    new Color(1.0f, 1.0f, 1.0f, 1.0f),
                    new Color(1.0f, 1.0f, 1.0f, 0.0f),
                    new Color(1.0f, 1.0f, 1.0f, 1.0f),
                    new Color(1.0f, 1.0f, 1.0f, 0.0f)
                });
                texture.Apply();
                File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private static void WriteAtlasPaddingPng(string path)
        {
            var texture = new Texture2D(4, 4, TextureFormat.RGBA32, mipChain: false);
            try
            {
                var pixels = new Color[16];
                for (int y = 0; y < 4; y++)
                {
                    for (int x = 0; x < 4; x++)
                    {
                        bool inner = x >= 1 && x <= 2 && y >= 1 && y <= 2;
                        pixels[y * 4 + x] = inner
                            ? new Color(1.0f, 1.0f, 1.0f, 1.0f)
                            : new Color(1.0f, 1.0f, 1.0f, 0.0f);
                    }
                }

                texture.SetPixels(pixels);
                texture.Apply();
                File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private static void WriteVerticalOrientationPng(string path)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false);
            try
            {
                texture.SetPixels(new[]
                {
                    Color.blue, Color.blue,
                    Color.red, Color.red
                });
                texture.Apply();
                File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private static void WriteGrayscalePng(string path)
        {
            WriteMinimalPng(
                path,
                width: 2,
                height: 2,
                colorType: 0,
                bytesPerPixel: 1,
                pixelBytes: new byte[]
                {
                    0, 64,
                    128, 255
                });
        }

        private static void WriteRgbPng(string path)
        {
            WriteMinimalPng(
                path,
                width: 2,
                height: 2,
                colorType: 2,
                bytesPerPixel: 3,
                pixelBytes: new byte[]
                {
                    255, 0, 0,   0, 255, 0,
                    0, 0, 255,   128, 128, 128
                });
        }

        private static void WriteMinimalPng(
            string path,
            int width,
            int height,
            byte colorType,
            int bytesPerPixel,
            byte[] pixelBytes)
        {
            byte[] scanlines = new byte[(width * bytesPerPixel + 1) * height];
            int source = 0;
            int destination = 0;
            for (int y = 0; y < height; y++)
            {
                scanlines[destination++] = 0;
                int rowBytes = width * bytesPerPixel;
                Array.Copy(pixelBytes, source, scanlines, destination, rowBytes);
                source += rowBytes;
                destination += rowBytes;
            }

            using var stream = new MemoryStream();
            stream.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, 0, 8);

            using (var ihdr = new MemoryStream())
            {
                WriteInt32BigEndian(ihdr, width);
                WriteInt32BigEndian(ihdr, height);
                ihdr.WriteByte(8);
                ihdr.WriteByte(colorType);
                ihdr.WriteByte(0);
                ihdr.WriteByte(0);
                ihdr.WriteByte(0);
                WritePngChunk(stream, "IHDR", ihdr.ToArray());
            }

            WritePngChunk(stream, "IDAT", CreateZlibStream(scanlines));
            WritePngChunk(stream, "IEND", Array.Empty<byte>());
            File.WriteAllBytes(path, stream.ToArray());
        }

        private static byte[] CreateZlibStream(byte[] data)
        {
            using var stream = new MemoryStream();
            stream.WriteByte(0x78);
            stream.WriteByte(0x01);
            using (var deflate = new DeflateStream(stream, System.IO.Compression.CompressionLevel.NoCompression, leaveOpen: true))
            {
                deflate.Write(data, 0, data.Length);
            }

            uint adler = Adler32(data);
            WriteInt32BigEndian(stream, unchecked((int)adler));
            return stream.ToArray();
        }

        private static uint Adler32(byte[] data)
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

        private static void WritePngChunk(Stream stream, string type, byte[] data)
        {
            WriteInt32BigEndian(stream, data.Length);
            byte[] typeBytes = System.Text.Encoding.ASCII.GetBytes(type);
            stream.Write(typeBytes, 0, typeBytes.Length);
            stream.Write(data, 0, data.Length);
            WriteInt32BigEndian(stream, 0);
        }

        private static void WriteInt32BigEndian(Stream stream, int value)
        {
            stream.WriteByte((byte)((value >> 24) & 0xff));
            stream.WriteByte((byte)((value >> 16) & 0xff));
            stream.WriteByte((byte)((value >> 8) & 0xff));
            stream.WriteByte((byte)(value & 0xff));
        }

        private static void WriteJpg(string path, Color color)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false);
            try
            {
                texture.SetPixels(new[] { color, color, color, color });
                texture.Apply();
                File.WriteAllBytes(path, texture.EncodeToJPG());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private static void WriteBmp24(string path, int width, int height)
        {
            int rowStride = ((width * 3 + 3) / 4) * 4;
            int pixelBytes = rowStride * height;
            byte[] bytes = new byte[54 + pixelBytes];
            bytes[0] = (byte)'B';
            bytes[1] = (byte)'M';
            WriteInt32LittleEndian(bytes, 2, bytes.Length);
            WriteInt32LittleEndian(bytes, 10, 54);
            WriteInt32LittleEndian(bytes, 14, 40);
            WriteInt32LittleEndian(bytes, 18, width);
            WriteInt32LittleEndian(bytes, 22, height);
            bytes[26] = 1;
            bytes[28] = 24;
            WriteInt32LittleEndian(bytes, 34, pixelBytes);

            int cursor = 54;
            for (int y = 0; y < height; y++)
            {
                int rowStart = cursor;
                for (int x = 0; x < width; x++)
                {
                    bytes[cursor++] = 255;
                    bytes[cursor++] = 0;
                    bytes[cursor++] = 0;
                }

                cursor = rowStart + rowStride;
            }

            File.WriteAllBytes(path, bytes);
        }

        private static void WriteBmp8Indexed(string path, int width, int height)
        {
            int rowStride = ((width + 3) / 4) * 4;
            int paletteBytes = 256 * 4;
            int pixelOffset = 54 + paletteBytes;
            int pixelBytes = rowStride * height;
            byte[] bytes = new byte[pixelOffset + pixelBytes];
            bytes[0] = (byte)'B';
            bytes[1] = (byte)'M';
            WriteInt32LittleEndian(bytes, 2, bytes.Length);
            WriteInt32LittleEndian(bytes, 10, pixelOffset);
            WriteInt32LittleEndian(bytes, 14, 40);
            WriteInt32LittleEndian(bytes, 18, width);
            WriteInt32LittleEndian(bytes, 22, height);
            bytes[26] = 1;
            bytes[28] = 8;
            WriteInt32LittleEndian(bytes, 34, pixelBytes);
            WriteInt32LittleEndian(bytes, 46, 256);

            int paletteCursor = 54;
            for (int i = 0; i < 256; i++)
            {
                bytes[paletteCursor++] = (byte)i;
                bytes[paletteCursor++] = 0;
                bytes[paletteCursor++] = (byte)(255 - i);
                bytes[paletteCursor++] = 0;
            }

            int cursor = pixelOffset;
            for (int y = 0; y < height; y++)
            {
                int rowStart = cursor;
                for (int x = 0; x < width; x++)
                {
                    bytes[cursor++] = (byte)((x + y) & 0xff);
                }

                cursor = rowStart + rowStride;
            }

            File.WriteAllBytes(path, bytes);
        }

        private static void WriteInt32LittleEndian(byte[] bytes, int offset, int value)
        {
            bytes[offset] = (byte)(value & 0xff);
            bytes[offset + 1] = (byte)((value >> 8) & 0xff);
            bytes[offset + 2] = (byte)((value >> 16) & 0xff);
            bytes[offset + 3] = (byte)((value >> 24) & 0xff);
        }

        private static void WriteTga24(string path, int width, int height)
        {
            byte[] bytes = new byte[18 + width * height * 3];
            bytes[2] = 2;
            bytes[12] = (byte)(width & 0xff);
            bytes[13] = (byte)((width >> 8) & 0xff);
            bytes[14] = (byte)(height & 0xff);
            bytes[15] = (byte)((height >> 8) & 0xff);
            bytes[16] = 24;
            bytes[17] = 0x20;

            int cursor = 18;
            for (int i = 0; i < width * height; i++)
            {
                bytes[cursor++] = 0;
                bytes[cursor++] = 0;
                bytes[cursor++] = 255;
            }

            File.WriteAllBytes(path, bytes);
        }

        private static void WriteTga32Alpha(string path, int width, int height, byte alpha)
        {
            byte[] bytes = new byte[18 + width * height * 4];
            bytes[2] = 2;
            bytes[12] = (byte)(width & 0xff);
            bytes[13] = (byte)((width >> 8) & 0xff);
            bytes[14] = (byte)(height & 0xff);
            bytes[15] = (byte)((height >> 8) & 0xff);
            bytes[16] = 32;
            bytes[17] = 0x28;

            int cursor = 18;
            for (int i = 0; i < width * height; i++)
            {
                bytes[cursor++] = 255;
                bytes[cursor++] = 255;
                bytes[cursor++] = 255;
                bytes[cursor++] = alpha;
            }

            File.WriteAllBytes(path, bytes);
        }

        private static void WriteDdsDxt3(string path)
        {
            byte[] bytes = new byte[128 + 16];
            bytes[0] = (byte)'D';
            bytes[1] = (byte)'D';
            bytes[2] = (byte)'S';
            bytes[3] = (byte)' ';
            WriteInt32LittleEndian(bytes, 4, 124);
            WriteInt32LittleEndian(bytes, 8, 0x0002100f);
            WriteInt32LittleEndian(bytes, 12, 4);
            WriteInt32LittleEndian(bytes, 16, 4);
            WriteInt32LittleEndian(bytes, 20, 16);
            WriteInt32LittleEndian(bytes, 28, 1);
            WriteInt32LittleEndian(bytes, 76, 32);
            WriteInt32LittleEndian(bytes, 80, 0x00000004);
            bytes[84] = (byte)'D';
            bytes[85] = (byte)'X';
            bytes[86] = (byte)'T';
            bytes[87] = (byte)'3';
            WriteInt32LittleEndian(bytes, 108, 0x00001000);

            int cursor = 128;
            for (int i = 0; i < 8; i++)
            {
                bytes[cursor++] = 0xff;
            }

            WriteUInt16LittleEndian(bytes, cursor, 0xf800);
            WriteUInt16LittleEndian(bytes, cursor + 2, 0x001f);
            WriteInt32LittleEndian(bytes, cursor + 4, 0);
            File.WriteAllBytes(path, bytes);
        }

        private static void WriteUInt16LittleEndian(byte[] bytes, int offset, int value)
        {
            bytes[offset] = (byte)(value & 0xff);
            bytes[offset + 1] = (byte)((value >> 8) & 0xff);
        }

        private static Texture? ReadBoundDiffuseTexture(Material material)
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

        private static Texture? ReadMaterialTexture(Material material, string propertyName)
        {
            return material.HasProperty(propertyName)
                ? material.GetTexture(propertyName)
                : null;
        }

        private static float ReadMaterialAlpha(Material material)
        {
            if (material.HasProperty("_BaseColor"))
            {
                return material.GetColor("_BaseColor").a;
            }

            return material.HasProperty("_Color")
                ? material.GetColor("_Color").a
                : 1.0f;
        }

        private static float ReadMaterialFloat(Material material, string propertyName)
        {
            return material.HasProperty(propertyName)
                ? material.GetFloat(propertyName)
                : float.NaN;
        }

        private static Color ReadMaterialColor(Material material, string propertyName)
        {
            return material.HasProperty(propertyName)
                ? material.GetColor(propertyName)
                : Color.clear;
        }

        private static MmdVertexDefinition CreateVertex(
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




















        private static MmdModelDefinition CreateTwoMaterialTriangleModel()
        {
            var model = new MmdModelDefinition
            {
                name = "two-material-triangle"
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
            model.bones.Add(new MmdBoneDefinition
            {
                index = 1,
                name = "child",
                parentIndex = 0,
                transformOrder = 0,
                origin = new[] { 0.0f, 1.0f, 0.0f },
                isMovable = true,
                isRotatable = true
            });
            model.vertices.Add(CreateVertex(0, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f));
            model.vertices.Add(CreateVertex(1, 1.0f, 0.0f, 0.0f, 1.0f, 0.0f));
            model.vertices.Add(CreateVertex(2, 0.0f, 1.0f, 0.0f, 0.0f, 1.0f));
            model.vertices.Add(CreateVertex(3, -1.0f, 0.0f, 0.0f, 1.0f, 1.0f));
            model.indices.AddRange(new[] { 0, 1, 2, 0, 2, 3 });
            model.materials.Add(new MmdMaterialDefinition
            {
                index = 0,
                name = "material-a",
                alpha = 1.0f,
                diffuseColor = new[] { 0.8f, 0.2f, 0.6f },
                ambientColor = new[] { 0.1f, 0.3f, 0.5f },
                edgeColor = new[] { 0.0f, 0.0f, 0.0f, 1.0f },
                edgeSize = 1.0f,
                drawEdgeFlag = true,
                vertexCount = 3
            });
            model.materials.Add(new MmdMaterialDefinition
            {
                index = 1,
                name = "material-b",
                alpha = 0.8f,
                diffuseColor = new[] { 0.9f, 0.7f, 0.4f },
                ambientColor = new[] { 0.2f, 0.1f, 0.3f },
                edgeColor = new[] { 0.0f, 0.0f, 0.0f, 0.9f },
                edgeSize = 0.5f,
                drawEdgeFlag = true,
                vertexCount = 3
            });
            return model;
        }

        private static MmdModelDefinition CreateMaterialMorphAllMaterialAddModel()
        {
            var model = CreateTwoMaterialTriangleModel();
            model.morphs.Add(new MmdMorphDefinition
            {
                index = 0,
                name = "all-add",
                type = "material",
                panel = "other",
                materialOffsets =
                {
                    new MmdMaterialMorphOffsetDefinition
                    {
                        materialIndex = -1,
                        operation = "add",
                        diffuseColor = new[] { 0.0f, 0.5f, 0.0f },
                        diffuseOpacity = -0.3f,
                        ambientColor = new[] { 0.0f, 0.0f, 0.4f },
                        specularColor = new[] { 0.0f, 0.0f, 0.0f },
                        specularPower = 0.0f,
                        edgeColor = new[] { 0.5f, 0.0f, 0.0f },
                        edgeOpacity = 0.0f,
                        edgeSize = 2.0f,
                        diffuseTextureBlend = new[] { 0.0f, 0.0f, 0.0f, 0.0f },
                        sphereTextureBlend = new[] { 0.0f, 0.0f, 0.0f, 0.0f },
                        toonTextureBlend = new[] { 0.0f, 0.0f, 0.0f, 0.0f }
                    }
                }
            });
            return model;
        }

        private static MmdModelDefinition CreateMaterialMorphMultiplyAllMaterialModel()
        {
            var model = CreateTwoMaterialTriangleModel();
            model.morphs.Add(new MmdMorphDefinition
            {
                index = 0,
                name = "all-multiply",
                type = "material",
                panel = "other",
                materialOffsets =
                {
                    new MmdMaterialMorphOffsetDefinition
                    {
                        materialIndex = -1,
                        operation = "multiply",
                        diffuseColor = new[] { 0.5f, 1.5f, 0.75f },
                        diffuseOpacity = 0.5f,
                        ambientColor = new[] { 2.0f, 0.5f, 1.0f },
                        specularColor = new[] { 0.0f, 0.0f, 0.0f },
                        specularPower = 0.0f,
                        edgeColor = new[] { 0.5f, 0.5f, 0.5f },
                        edgeOpacity = 0.2f,
                        edgeSize = 2.0f,
                        diffuseTextureBlend = new[] { 0.0f, 0.0f, 0.0f, 0.0f },
                        sphereTextureBlend = new[] { 0.0f, 0.0f, 0.0f, 0.0f },
                        toonTextureBlend = new[] { 0.0f, 0.0f, 0.0f, 0.0f }
                    }
                }
            });
            return model;
        }

        private static MmdModelDefinition CreateGroupMaterialMorphModel()
        {
            var model = CreateMaterialMorphTriangleModel();
            // Add a group morph targeting material morph index 0 ("color-change") with weight 0.8.
            model.morphs.Add(new MmdMorphDefinition
            {
                index = model.morphs.Count,
                name = "mood-group",
                type = "group",
                panel = "other",
                groupOffsets =
                {
                    new MmdGroupMorphOffsetDefinition { morphIndex = 0, weight = 0.8f }
                }
            });
            return model;
        }


        private static MmdModelDefinition CreateGroupMorphTriangleModel()
        {
            var model = new MmdModelDefinition
            {
                name = "group-morph-triangle"
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
            model.vertices.Add(CreateVertex(0, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f));
            model.vertices.Add(CreateVertex(1, 1.0f, 0.0f, 0.0f, 1.0f, 0.0f));
            model.vertices.Add(CreateVertex(2, 0.0f, 1.0f, 0.0f, 0.0f, 1.0f));
            model.indices.AddRange(new[] { 0, 1, 2 });
            model.materials.Add(new MmdMaterialDefinition
            {
                index = 0,
                name = "group-morph-material",
                vertexCount = 3
            });
            // Vertex morph: "smile" moves vertex 1 up by 2.0.
            model.morphs.Add(new MmdMorphDefinition
            {
                index = 0,
                name = "smile",
                type = "vertex",
                panel = "eye",
                vertexOffsets =
                {
                    new MmdVertexMorphOffsetDefinition
                    {
                        vertexIndex = 1,
                        positionDelta = new[] { 0.0f, 2.0f, 0.0f }
                    }
                }
            });
            // Group morph: "happy-face" targets "smile" with coefficient 0.5.
            model.morphs.Add(new MmdMorphDefinition
            {
                index = 1,
                name = "happy-face",
                type = "group",
                panel = "other",
                groupOffsets =
                {
                    new MmdGroupMorphOffsetDefinition { morphIndex = 0, weight = 0.5f }
                }
            });
            return model;
        }

        private static MmdModelDefinition CreateSplitGroupMorphModel()
        {
            var model = CreateSharedVertexTwoSubmeshMorphModel();
            // Replace the direct vertex morph with a group morph that targets it.
            // The existing "shared-up" vertex morph (index 0) targets vertex 0 with delta (0,1,0).
            // Add a group morph "happy-face" targeting morphIndex 0 ("shared-up") with weight 1.0.
            model.morphs.Add(new MmdMorphDefinition
            {
                index = 1,
                name = "happy-face",
                type = "group",
                panel = "other",
                groupOffsets =
                {
                    new MmdGroupMorphOffsetDefinition { morphIndex = 0, weight = 1.0f }
                }
            });
            return model;
        }

        private static MmdModelDefinition CreateCycleGroupMorphModel()
        {
            var model = new MmdModelDefinition
            {
                name = "cycle-group-morph"
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
            model.vertices.Add(CreateVertex(0, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f));
            model.vertices.Add(CreateVertex(1, 1.0f, 0.0f, 0.0f, 1.0f, 0.0f));
            model.vertices.Add(CreateVertex(2, 0.0f, 1.0f, 0.0f, 0.0f, 1.0f));
            model.indices.AddRange(new[] { 0, 1, 2 });
            model.materials.Add(new MmdMaterialDefinition
            {
                index = 0,
                name = "cycle-material",
                vertexCount = 3
            });
            // Vertex morph "blink" (index 0) so the cycle test has a terminal target.
            model.morphs.Add(new MmdMorphDefinition
            {
                index = 0,
                name = "blink",
                type = "vertex",
                panel = "eye",
                vertexOffsets =
                {
                    new MmdVertexMorphOffsetDefinition
                    {
                        vertexIndex = 1,
                        positionDelta = new[] { 0.0f, 1.0f, 0.0f }
                    }
                }
            });
            // Group morph "loop-a" targets "loop-b" with weight 1.0.
            model.morphs.Add(new MmdMorphDefinition
            {
                index = 1,
                name = "loop-a",
                type = "group",
                panel = "other",
                groupOffsets =
                {
                    new MmdGroupMorphOffsetDefinition { morphIndex = 2, weight = 1.0f }
                }
            });
            // Group morph "loop-b" targets "loop-a" with weight 1.0 (creates cycle).
            model.morphs.Add(new MmdMorphDefinition
            {
                index = 2,
                name = "loop-b",
                type = "group",
                panel = "other",
                groupOffsets =
                {
                    new MmdGroupMorphOffsetDefinition { morphIndex = 1, weight = 1.0f }
                }
            });
            return model;
        }





        private static MmdModelDefinition CreateFlipMorphTriangleModel()
        {
            var model = new MmdModelDefinition
            {
                name = "flip-morph-triangle"
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
            model.vertices.Add(CreateVertex(0, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f));
            model.vertices.Add(CreateVertex(1, 1.0f, 0.0f, 0.0f, 1.0f, 0.0f));
            model.vertices.Add(CreateVertex(2, 0.0f, 1.0f, 0.0f, 0.0f, 1.0f));
            model.indices.AddRange(new[] { 0, 1, 2 });
            model.materials.Add(new MmdMaterialDefinition
            {
                index = 0,
                name = "flip-morph-material",
                vertexCount = 3
            });
            // Vertex morph: "smile" moves vertex 1 up by 2.0.
            model.morphs.Add(new MmdMorphDefinition
            {
                index = 0,
                name = "smile",
                type = "vertex",
                panel = "eye",
                vertexOffsets =
                {
                    new MmdVertexMorphOffsetDefinition
                    {
                        vertexIndex = 1,
                        positionDelta = new[] { 0.0f, 2.0f, 0.0f }
                    }
                }
            });
            // Flip morph: "flip-smile" targets "smile" with coefficient 0.5.
            model.morphs.Add(new MmdMorphDefinition
            {
                index = 1,
                name = "flip-smile",
                type = "flip",
                panel = "other",
                flipOffsets = new List<MmdFlipMorphOffsetDefinition>
                {
                    new MmdFlipMorphOffsetDefinition { morphIndex = 0, weight = 0.5f }
                }
            });
            return model;
        }

        private static MmdModelDefinition CreateFlipMaterialMorphModel()
        {
            var model = CreateMaterialMorphTriangleModel();
            // Add a flip morph targeting material morph index 0 ("color-change") with weight 0.8.
            model.morphs.Add(new MmdMorphDefinition
            {
                index = model.morphs.Count,
                name = "flip-color",
                type = "flip",
                panel = "other",
                flipOffsets = new List<MmdFlipMorphOffsetDefinition>
                {
                    new MmdFlipMorphOffsetDefinition { morphIndex = 0, weight = 0.8f }
                }
            });
            return model;
        }

        private static MmdModelDefinition CreateGroupFlipRecursiveModel()
        {
            var model = CreateFlipMorphTriangleModel();
            // Add a group morph "mood-group" targeting flip morph "flip-smile" (index 1) with weight 1.0.
            model.morphs.Add(new MmdMorphDefinition
            {
                index = model.morphs.Count,
                name = "mood-group",
                type = "group",
                panel = "other",
                groupOffsets =
                {
                    new MmdGroupMorphOffsetDefinition { morphIndex = 1, weight = 1.0f }
                }
            });
            return model;
        }

        private static MmdModelDefinition CreateCycleFlipMorphModel()
        {
            var model = new MmdModelDefinition
            {
                name = "cycle-flip-morph"
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
            model.vertices.Add(CreateVertex(0, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f));
            model.vertices.Add(CreateVertex(1, 1.0f, 0.0f, 0.0f, 1.0f, 0.0f));
            model.vertices.Add(CreateVertex(2, 0.0f, 1.0f, 0.0f, 0.0f, 1.0f));
            model.indices.AddRange(new[] { 0, 1, 2 });
            model.materials.Add(new MmdMaterialDefinition
            {
                index = 0,
                name = "cycle-flip-material",
                vertexCount = 3
            });
            // Vertex morph "blink" (index 0) so the cycle test has a terminal target.
            model.morphs.Add(new MmdMorphDefinition
            {
                index = 0,
                name = "blink",
                type = "vertex",
                panel = "eye",
                vertexOffsets =
                {
                    new MmdVertexMorphOffsetDefinition
                    {
                        vertexIndex = 1,
                        positionDelta = new[] { 0.0f, 1.0f, 0.0f }
                    }
                }
            });
            // Flip morph "loop-a" targets "loop-b" with weight 1.0.
            model.morphs.Add(new MmdMorphDefinition
            {
                index = 1,
                name = "loop-a",
                type = "flip",
                panel = "other",
                flipOffsets = new List<MmdFlipMorphOffsetDefinition>
                {
                    new MmdFlipMorphOffsetDefinition { morphIndex = 2, weight = 1.0f }
                }
            });
            // Flip morph "loop-b" targets "loop-a" with weight 1.0 (creates cycle).
            model.morphs.Add(new MmdMorphDefinition
            {
                index = 2,
                name = "loop-b",
                type = "flip",
                panel = "other",
                flipOffsets = new List<MmdFlipMorphOffsetDefinition>
                {
                    new MmdFlipMorphOffsetDefinition { morphIndex = 1, weight = 1.0f }
                }
            });
            return model;
        }







        private static SkinnedMeshRenderer RequireSkinnedRenderer(MmdUnityModelInstance instance)
        {
            Assert.That(instance.SkinnedMeshRenderer, Is.Not.Null);
            return instance.SkinnedMeshRenderer!;
        }

    }
}
