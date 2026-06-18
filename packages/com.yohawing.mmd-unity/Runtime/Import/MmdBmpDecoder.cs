#nullable enable

using System;
using UnityEngine;

namespace Yohawing.MmdUnity.UnityIntegration
{
    internal static class MmdBmpDecoder
    {
        public static Texture2D Decode(byte[] bytes, string textureName)
        {
            if (bytes == null)
            {
                throw new ArgumentNullException(nameof(bytes));
            }

            if (bytes.Length < 54 || bytes[0] != (byte)'B' || bytes[1] != (byte)'M')
            {
                throw new ArgumentException("BMP data must start with a BITMAPFILEHEADER.", nameof(bytes));
            }

            int pixelOffset = ReadInt32(bytes, 10);
            int dibHeaderSize = ReadInt32(bytes, 14);
            if (dibHeaderSize < 40)
            {
                throw new NotSupportedException($"BMP DIB header size {dibHeaderSize} is not supported.");
            }

            int width = ReadInt32(bytes, 18);
            int signedHeight = ReadInt32(bytes, 22);
            int planes = ReadUInt16(bytes, 26);
            int bitsPerPixel = ReadUInt16(bytes, 28);
            int compression = ReadInt32(bytes, 30);

            if (planes != 1)
            {
                throw new ArgumentException("BMP planes must be 1.", nameof(bytes));
            }

            if (width <= 0 || signedHeight == 0)
            {
                throw new ArgumentException("BMP width and height must be non-zero.", nameof(bytes));
            }

            if (compression != 0)
            {
                throw new NotSupportedException($"BMP compression mode {compression} is not supported.");
            }

            if (bitsPerPixel != 8 && bitsPerPixel != 24 && bitsPerPixel != 32)
            {
                throw new NotSupportedException($"BMP bit depth {bitsPerPixel} is not supported.");
            }

            int height = Math.Abs(signedHeight);
            bool topDown = signedHeight < 0;
            int bytesPerPixel = bitsPerPixel / 8;
            int rowStride = ((width * bytesPerPixel + 3) / 4) * 4;
            int requiredBytes = pixelOffset + rowStride * height;
            if (pixelOffset < 0 || requiredBytes > bytes.Length)
            {
                throw new ArgumentException("BMP pixel data is truncated.", nameof(bytes));
            }

            Color32[]? palette = null;
            if (bitsPerPixel == 8)
            {
                int colorsUsed = ReadInt32(bytes, 46);
                int paletteEntryCount = colorsUsed > 0 ? colorsUsed : 256;
                int paletteOffset = 14 + dibHeaderSize;
                int paletteBytes = checked(paletteEntryCount * 4);
                if (paletteOffset < 0 || paletteOffset + paletteBytes > bytes.Length || paletteOffset + paletteBytes > pixelOffset)
                {
                    throw new ArgumentException("BMP palette data is truncated.", nameof(bytes));
                }

                palette = new Color32[paletteEntryCount];
                for (int i = 0; i < paletteEntryCount; i++)
                {
                    int offset = paletteOffset + i * 4;
                    palette[i] = new Color32(bytes[offset + 2], bytes[offset + 1], bytes[offset], 255);
                }
            }

            var pixels = new Color32[checked(width * height)];
            for (int sourceY = 0; sourceY < height; sourceY++)
            {
                int targetY = topDown ? height - 1 - sourceY : sourceY;
                int rowOffset = pixelOffset + sourceY * rowStride;
                for (int x = 0; x < width; x++)
                {
                    int offset = rowOffset + x * bytesPerPixel;
                    if (bitsPerPixel == 8)
                    {
                        int paletteIndex = bytes[offset];
                        if (palette == null || paletteIndex >= palette.Length)
                        {
                            throw new ArgumentException("BMP pixel references a missing palette entry.", nameof(bytes));
                        }

                        pixels[targetY * width + x] = palette[paletteIndex];
                    }
                    else
                    {
                        byte blue = bytes[offset];
                        byte green = bytes[offset + 1];
                        byte red = bytes[offset + 2];
                        byte alpha = bytesPerPixel == 4 ? bytes[offset + 3] : (byte)255;
                        pixels[targetY * width + x] = new Color32(red, green, blue, alpha);
                    }
                }
            }

            var texture = new Texture2D(width, height, TextureFormat.RGBA32, mipChain: true)
            {
                name = textureName
            };
            texture.SetPixels32(pixels);
            texture.Apply(updateMipmaps: true, makeNoLongerReadable: false);
            return texture;
        }

        private static int ReadInt32(byte[] bytes, int offset)
        {
            return bytes[offset]
                | (bytes[offset + 1] << 8)
                | (bytes[offset + 2] << 16)
                | (bytes[offset + 3] << 24);
        }

        private static int ReadUInt16(byte[] bytes, int offset)
        {
            return bytes[offset] | (bytes[offset + 1] << 8);
        }
    }
}
