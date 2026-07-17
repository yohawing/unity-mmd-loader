#nullable enable

using System;
using UnityEngine;

namespace Mmd.UnityIntegration
{
    internal static class MmdTgaDecoder
    {
        public static Texture2D Decode(byte[] bytes, string textureName)
        {
            return Decode(bytes, textureName, MmdTextureDecodeBudget.Default);
        }

        internal static Texture2D Decode(byte[] bytes, string textureName, MmdTextureDecodeBudget budget)
        {
            if (bytes == null)
            {
                throw new ArgumentNullException(nameof(bytes));
            }

            if (bytes.Length < 18)
            {
                throw new ArgumentException("TGA data is too short.", nameof(bytes));
            }

            budget.ValidateInputLength(bytes.LongLength);

            int idLength = bytes[0];
            int colorMapType = bytes[1];
            int imageType = bytes[2];
            int width = ReadUInt16(bytes, 12);
            int height = ReadUInt16(bytes, 14);
            int bitsPerPixel = bytes[16];
            int descriptor = bytes[17];

            if (colorMapType != 0)
            {
                throw new NotSupportedException("Color-mapped TGA textures are not supported.");
            }

            if (imageType != 2 && imageType != 10)
            {
                throw new NotSupportedException($"TGA image type {imageType} is not supported.");
            }

            if (width <= 0 || height <= 0)
            {
                throw new ArgumentException("TGA width and height must be positive.", nameof(bytes));
            }

            if (bitsPerPixel != 24 && bitsPerPixel != 32)
            {
                throw new NotSupportedException($"TGA bit depth {bitsPerPixel} is not supported.");
            }

            int pixelCount = budget.ValidateImageAndGetPixelCount(width, height);

            int dataOffset = 18 + idLength;
            if (dataOffset > bytes.Length)
            {
                throw new ArgumentException("TGA image data offset exceeds the file length.", nameof(bytes));
            }

            var pixels = new Color32[pixelCount];
            bool topOrigin = (descriptor & 0x20) != 0;
            bool rightOrigin = (descriptor & 0x10) != 0;
            int bytesPerPixel = bitsPerPixel / 8;

            if (imageType == 2)
            {
                DecodeRaw(bytes, dataOffset, width, height, bytesPerPixel, topOrigin, rightOrigin, pixels);
            }
            else
            {
                DecodeRle(bytes, dataOffset, width, height, bytesPerPixel, topOrigin, rightOrigin, pixels);
            }

            var texture = new Texture2D(width, height, TextureFormat.RGBA32, mipChain: false)
            {
                name = textureName
            };
            texture.SetPixels32(pixels);
            texture.Apply(updateMipmaps: false, makeNoLongerReadable: false);
            return texture;
        }

        private static void DecodeRaw(
            byte[] bytes,
            int offset,
            int width,
            int height,
            int bytesPerPixel,
            bool topOrigin,
            bool rightOrigin,
            Color32[] pixels)
        {
            int cursor = offset;
            int pixelCount = checked(width * height);
            for (int sourceIndex = 0; sourceIndex < pixelCount; sourceIndex++)
            {
                if (cursor + bytesPerPixel > bytes.Length)
                {
                    throw new ArgumentException("TGA image data ended before all pixels were decoded.", nameof(bytes));
                }

                WritePixel(sourceIndex, ReadPixel(bytes, cursor, bytesPerPixel), width, height, topOrigin, rightOrigin, pixels);
                cursor += bytesPerPixel;
            }
        }

        private static void DecodeRle(
            byte[] bytes,
            int offset,
            int width,
            int height,
            int bytesPerPixel,
            bool topOrigin,
            bool rightOrigin,
            Color32[] pixels)
        {
            int cursor = offset;
            int sourceIndex = 0;
            int pixelCount = checked(width * height);
            while (sourceIndex < pixelCount)
            {
                if (cursor >= bytes.Length)
                {
                    throw new ArgumentException("TGA RLE packet header is missing.", nameof(bytes));
                }

                byte packetHeader = bytes[cursor++];
                int runLength = (packetHeader & 0x7f) + 1;
                bool isRlePacket = (packetHeader & 0x80) != 0;

                if (sourceIndex + runLength > pixelCount)
                {
                    throw new ArgumentException("TGA RLE packet exceeds the expected pixel count.", nameof(bytes));
                }

                if (isRlePacket)
                {
                    if (cursor + bytesPerPixel > bytes.Length)
                    {
                        throw new ArgumentException("TGA RLE pixel data is truncated.", nameof(bytes));
                    }

                    Color32 color = ReadPixel(bytes, cursor, bytesPerPixel);
                    cursor += bytesPerPixel;
                    for (int i = 0; i < runLength; i++)
                    {
                        WritePixel(sourceIndex++, color, width, height, topOrigin, rightOrigin, pixels);
                    }
                }
                else
                {
                    for (int i = 0; i < runLength; i++)
                    {
                        if (cursor + bytesPerPixel > bytes.Length)
                        {
                            throw new ArgumentException("TGA raw packet data is truncated.", nameof(bytes));
                        }

                        WritePixel(sourceIndex++, ReadPixel(bytes, cursor, bytesPerPixel), width, height, topOrigin, rightOrigin, pixels);
                        cursor += bytesPerPixel;
                    }
                }
            }
        }

        private static Color32 ReadPixel(byte[] bytes, int offset, int bytesPerPixel)
        {
            byte blue = bytes[offset];
            byte green = bytes[offset + 1];
            byte red = bytes[offset + 2];
            byte alpha = bytesPerPixel == 4 ? bytes[offset + 3] : (byte)255;
            return new Color32(red, green, blue, alpha);
        }

        private static void WritePixel(
            int sourceIndex,
            Color32 color,
            int width,
            int height,
            bool topOrigin,
            bool rightOrigin,
            Color32[] pixels)
        {
            int sourceX = sourceIndex % width;
            int sourceY = sourceIndex / width;
            int targetX = rightOrigin ? width - 1 - sourceX : sourceX;
            int targetY = topOrigin ? height - 1 - sourceY : sourceY;
            pixels[targetY * width + targetX] = color;
        }

        private static int ReadUInt16(byte[] bytes, int offset)
        {
            return bytes[offset] | (bytes[offset + 1] << 8);
        }
    }
}
