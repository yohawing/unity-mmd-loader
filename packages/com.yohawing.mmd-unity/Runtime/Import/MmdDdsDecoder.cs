#nullable enable

using System;
using UnityEngine;

namespace Yohawing.MmdUnity.UnityIntegration
{
    internal static class MmdDdsDecoder
    {
        private const int HeaderSize = 128;

        public static Texture2D Decode(byte[] bytes, string textureName)
        {
            if (bytes == null)
            {
                throw new ArgumentNullException(nameof(bytes));
            }

            if (bytes.Length < HeaderSize
                || bytes[0] != (byte)'D'
                || bytes[1] != (byte)'D'
                || bytes[2] != (byte)'S'
                || bytes[3] != (byte)' ')
            {
                throw new ArgumentException("DDS data must start with a DDS magic header.", nameof(bytes));
            }

            int headerByteSize = ReadInt32(bytes, 4);
            int pixelFormatByteSize = ReadInt32(bytes, 76);
            if (headerByteSize != 124 || pixelFormatByteSize != 32)
            {
                throw new NotSupportedException("Only standard DDS headers are supported.");
            }

            int height = ReadInt32(bytes, 12);
            int width = ReadInt32(bytes, 16);
            if (width <= 0 || height <= 0)
            {
                throw new ArgumentException("DDS width and height must be positive.", nameof(bytes));
            }

            string fourCc = FourCc(bytes, 84);
            Color32[] pixels = fourCc switch
            {
                "DXT1" => DecodeDxt(bytes, HeaderSize, width, height, DxtFormat.Dxt1),
                "DXT3" => DecodeDxt(bytes, HeaderSize, width, height, DxtFormat.Dxt3),
                "DXT5" => DecodeDxt(bytes, HeaderSize, width, height, DxtFormat.Dxt5),
                _ => throw new NotSupportedException($"DDS FourCC '{fourCc}' is not supported.")
            };

            var texture = new Texture2D(width, height, TextureFormat.RGBA32, mipChain: true)
            {
                name = textureName
            };
            texture.SetPixels32(pixels);
            texture.Apply(updateMipmaps: true, makeNoLongerReadable: false);
            return texture;
        }

        private static Color32[] DecodeDxt(byte[] bytes, int offset, int width, int height, DxtFormat format)
        {
            int blockBytes = format == DxtFormat.Dxt1 ? 8 : 16;
            int blockCountX = Math.Max(1, (width + 3) / 4);
            int blockCountY = Math.Max(1, (height + 3) / 4);
            int requiredBytes = offset + blockCountX * blockCountY * blockBytes;
            if (requiredBytes > bytes.Length)
            {
                throw new ArgumentException("DDS pixel data is truncated.", nameof(bytes));
            }

            var pixels = new Color32[checked(width * height)];
            int cursor = offset;
            for (int blockY = 0; blockY < blockCountY; blockY++)
            {
                for (int blockX = 0; blockX < blockCountX; blockX++)
                {
                    DecodeBlock(bytes, cursor, blockX * 4, blockY * 4, width, height, format, pixels);
                    cursor += blockBytes;
                }
            }

            return pixels;
        }

        private static void DecodeBlock(
            byte[] bytes,
            int offset,
            int targetX,
            int targetY,
            int width,
            int height,
            DxtFormat format,
            Color32[] pixels)
        {
            byte[] alpha = format switch
            {
                DxtFormat.Dxt3 => DecodeDxt3Alpha(bytes, offset),
                DxtFormat.Dxt5 => DecodeDxt5Alpha(bytes, offset),
                _ => OpaqueAlpha()
            };
            int colorOffset = format == DxtFormat.Dxt1 ? offset : offset + 8;
            Color32[] colors = DecodeColorPalette(bytes, colorOffset, format == DxtFormat.Dxt1);
            uint colorBits = ReadUInt32(bytes, colorOffset + 4);

            for (int y = 0; y < 4; y++)
            {
                for (int x = 0; x < 4; x++)
                {
                    int imageX = targetX + x;
                    int imageY = targetY + y;
                    if (imageX >= width || imageY >= height)
                    {
                        continue;
                    }

                    int blockPixel = y * 4 + x;
                    int colorIndex = (int)((colorBits >> (2 * blockPixel)) & 0x3);
                    Color32 color = colors[colorIndex];
                    color.a = alpha[blockPixel];
                    pixels[(height - 1 - imageY) * width + imageX] = color;
                }
            }
        }

        private static Color32[] DecodeColorPalette(byte[] bytes, int offset, bool allowDxt1TransparentColor)
        {
            ushort color0 = ReadUInt16(bytes, offset);
            ushort color1 = ReadUInt16(bytes, offset + 2);
            Color32 c0 = DecodeRgb565(color0);
            Color32 c1 = DecodeRgb565(color1);
            var colors = new Color32[4];
            colors[0] = c0;
            colors[1] = c1;

            if (color0 > color1 || !allowDxt1TransparentColor)
            {
                colors[2] = Lerp(c0, c1, 2, 1, 3);
                colors[3] = Lerp(c0, c1, 1, 2, 3);
            }
            else
            {
                colors[2] = Lerp(c0, c1, 1, 1, 2);
                colors[3] = new Color32(0, 0, 0, 0);
            }

            return colors;
        }

        private static byte[] DecodeDxt3Alpha(byte[] bytes, int offset)
        {
            var alpha = new byte[16];
            for (int i = 0; i < 16; i++)
            {
                int sourceByte = bytes[offset + i / 2];
                int nibble = (i & 1) == 0 ? sourceByte & 0x0f : sourceByte >> 4;
                alpha[i] = (byte)(nibble * 17);
            }

            return alpha;
        }

        private static byte[] DecodeDxt5Alpha(byte[] bytes, int offset)
        {
            var palette = new byte[8];
            palette[0] = bytes[offset];
            palette[1] = bytes[offset + 1];
            if (palette[0] > palette[1])
            {
                for (int i = 1; i <= 6; i++)
                {
                    palette[i + 1] = (byte)(((7 - i) * palette[0] + i * palette[1] + 3) / 7);
                }
            }
            else
            {
                for (int i = 1; i <= 4; i++)
                {
                    palette[i + 1] = (byte)(((5 - i) * palette[0] + i * palette[1] + 2) / 5);
                }

                palette[6] = 0;
                palette[7] = 255;
            }

            ulong indices = 0;
            for (int i = 0; i < 6; i++)
            {
                indices |= (ulong)bytes[offset + 2 + i] << (8 * i);
            }

            var alpha = new byte[16];
            for (int i = 0; i < 16; i++)
            {
                alpha[i] = palette[(indices >> (3 * i)) & 0x7];
            }

            return alpha;
        }

        private static byte[] OpaqueAlpha()
        {
            var alpha = new byte[16];
            for (int i = 0; i < alpha.Length; i++)
            {
                alpha[i] = 255;
            }

            return alpha;
        }

        private static Color32 DecodeRgb565(ushort value)
        {
            byte r = (byte)(((value >> 11) & 0x1f) * 255 / 31);
            byte g = (byte)(((value >> 5) & 0x3f) * 255 / 63);
            byte b = (byte)((value & 0x1f) * 255 / 31);
            return new Color32(r, g, b, 255);
        }

        private static Color32 Lerp(Color32 a, Color32 b, int weightA, int weightB, int divisor)
        {
            return new Color32(
                (byte)((a.r * weightA + b.r * weightB) / divisor),
                (byte)((a.g * weightA + b.g * weightB) / divisor),
                (byte)((a.b * weightA + b.b * weightB) / divisor),
                255);
        }

        private static string FourCc(byte[] bytes, int offset)
        {
            return new string(new[]
            {
                (char)bytes[offset],
                (char)bytes[offset + 1],
                (char)bytes[offset + 2],
                (char)bytes[offset + 3]
            });
        }

        private static int ReadInt32(byte[] bytes, int offset)
        {
            return bytes[offset]
                | (bytes[offset + 1] << 8)
                | (bytes[offset + 2] << 16)
                | (bytes[offset + 3] << 24);
        }

        private static uint ReadUInt32(byte[] bytes, int offset)
        {
            return (uint)ReadInt32(bytes, offset);
        }

        private static ushort ReadUInt16(byte[] bytes, int offset)
        {
            return (ushort)(bytes[offset] | (bytes[offset + 1] << 8));
        }

        private enum DxtFormat
        {
            Dxt1,
            Dxt3,
            Dxt5
        }
    }
}
