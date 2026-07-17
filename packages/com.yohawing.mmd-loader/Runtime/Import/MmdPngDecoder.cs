#nullable enable

using System;
using System.IO;
using System.IO.Compression;
using UnityEngine;

namespace Mmd.UnityIntegration
{
    internal static class MmdPngDecoder
    {
        private static readonly byte[] Signature = { 137, 80, 78, 71, 13, 10, 26, 10 };

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

            budget.ValidateInputLength(bytes.LongLength);

            if (bytes.Length < Signature.Length || !HasSignature(bytes))
            {
                throw new ArgumentException("PNG signature is missing.", nameof(bytes));
            }

            int width = 0;
            int height = 0;
            byte bitDepth = 0;
            byte colorType = 0;
            using var idat = new MemoryStream();
            int cursor = Signature.Length;
            int chunkCount = 0;
            bool sawHeader = false;
            bool sawImageData = false;
            bool sawEnd = false;
            while (cursor + 12 <= bytes.Length)
            {
                chunkCount++;
                if (chunkCount > budget.MaxPngChunkCount)
                {
                    throw new ArgumentException($"PNG chunk count exceeds the decode budget {budget.MaxPngChunkCount}.", nameof(bytes));
                }

                int length = ReadInt32BigEndian(bytes, cursor);
                cursor += 4;
                string chunkType = System.Text.Encoding.ASCII.GetString(bytes, cursor, 4);
                cursor += 4;
                if (length < 0 || length > bytes.Length - cursor - 4)
                {
                    throw new ArgumentException("PNG chunk length exceeds the file length.", nameof(bytes));
                }

                if (chunkType == "IHDR")
                {
                    if (sawHeader || sawImageData || chunkCount != 1 || length != 13)
                    {
                        throw new ArgumentException("PNG must contain one 13-byte IHDR as its first chunk.", nameof(bytes));
                    }

                    width = ReadInt32BigEndian(bytes, cursor);
                    height = ReadInt32BigEndian(bytes, cursor + 4);
                    bitDepth = bytes[cursor + 8];
                    colorType = bytes[cursor + 9];
                    if (bytes[cursor + 10] != 0 || bytes[cursor + 11] != 0 || bytes[cursor + 12] != 0)
                    {
                        throw new NotSupportedException("PNG compression, filter, and interlace methods must be zero.");
                    }

                    sawHeader = true;
                }
                else if (chunkType == "IDAT")
                {
                    if (!sawHeader || sawEnd)
                    {
                        throw new ArgumentException("PNG IDAT must follow IHDR and precede IEND.", nameof(bytes));
                    }

                    long combinedLength = checked(idat.Length + length);
                    budget.ValidateInputLength(combinedLength);
                    idat.Write(bytes, cursor, length);
                    sawImageData = true;
                }
                else if (chunkType == "IEND")
                {
                    if (!sawHeader || !sawImageData || length != 0)
                    {
                        throw new ArgumentException("PNG IEND must be empty and follow image data.", nameof(bytes));
                    }

                    sawEnd = true;
                    break;
                }

                cursor += length + 4;
            }

            if (!sawHeader || !sawImageData || !sawEnd)
            {
                throw new ArgumentException("PNG is missing required IHDR, IDAT, or IEND chunks.", nameof(bytes));
            }

            if (width <= 0 || height <= 0)
            {
                throw new ArgumentException("PNG width and height must be positive.", nameof(bytes));
            }

            if (bitDepth != 8 || (colorType != 0 && colorType != 2 && colorType != 6))
            {
                throw new NotSupportedException($"PNG color type {colorType} with bit depth {bitDepth} is not supported.");
            }

            int bytesPerPixel = GetBytesPerPixel(colorType);
            int expected = budget.ValidateImageAndGetPngInflatedLength(width, height, bytesPerPixel);
            int stride = checked(width * bytesPerPixel);
            byte[] inflated = InflateZlib(idat.ToArray(), expected);

            var pixels = new Color32[checked(width * height)];
            var previous = new byte[stride];
            var current = new byte[stride];
            int offset = 0;
            for (int y = 0; y < height; y++)
            {
                byte filter = inflated[offset++];
                Array.Copy(inflated, offset, current, 0, stride);
                offset += stride;
                Unfilter(current, previous, filter, bytesPerPixel);
                int targetY = height - 1 - y;
                for (int x = 0; x < width; x++)
                {
                    pixels[targetY * width + x] = DecodePixel(current, x, colorType);
                }

                (previous, current) = (current, previous);
            }

            var texture = new Texture2D(width, height, TextureFormat.RGBA32, mipChain: false)
            {
                name = textureName
            };
            texture.SetPixels32(pixels);
            texture.Apply(updateMipmaps: false, makeNoLongerReadable: false);
            return texture;
        }

        private static int GetBytesPerPixel(byte colorType)
        {
            return colorType switch
            {
                0 => 1,
                2 => 3,
                6 => 4,
                _ => throw new NotSupportedException($"PNG color type {colorType} is not supported.")
            };
        }

        private static Color32 DecodePixel(byte[] row, int x, byte colorType)
        {
            int source = x * GetBytesPerPixel(colorType);
            return colorType switch
            {
                0 => new Color32(row[source], row[source], row[source], 255),
                2 => new Color32(row[source], row[source + 1], row[source + 2], 255),
                6 => new Color32(row[source], row[source + 1], row[source + 2], row[source + 3]),
                _ => throw new NotSupportedException($"PNG color type {colorType} is not supported.")
            };
        }

        private static bool HasSignature(byte[] bytes)
        {
            for (int i = 0; i < Signature.Length; i++)
            {
                if (bytes[i] != Signature[i])
                {
                    return false;
                }
            }

            return true;
        }

        private static byte[] InflateZlib(byte[] bytes, int expectedLength)
        {
            if (bytes.Length < 6)
            {
                throw new ArgumentException("PNG zlib stream is too short.", nameof(bytes));
            }

            using var input = new MemoryStream(bytes, 2, bytes.Length - 6);
            using var deflate = new DeflateStream(input, CompressionMode.Decompress);
            var output = new byte[expectedLength];
            int offset = 0;
            while (offset < output.Length)
            {
                int read = deflate.Read(output, offset, output.Length - offset);
                if (read == 0)
                {
                    throw new ArgumentException("PNG image data ended before all rows were decoded.", nameof(bytes));
                }

                offset += read;
            }

            if (deflate.ReadByte() != -1)
            {
                throw new ArgumentException("PNG image data exceeds the expected row budget.", nameof(bytes));
            }

            return output;
        }

        private static void Unfilter(byte[] current, byte[] previous, byte filter, int bytesPerPixel)
        {
            for (int i = 0; i < current.Length; i++)
            {
                int left = i >= bytesPerPixel ? current[i - bytesPerPixel] : 0;
                int up = previous[i];
                int upLeft = i >= bytesPerPixel ? previous[i - bytesPerPixel] : 0;
                int predictor = filter switch
                {
                    0 => 0,
                    1 => left,
                    2 => up,
                    3 => (left + up) >> 1,
                    4 => Paeth(left, up, upLeft),
                    _ => throw new NotSupportedException($"PNG filter {filter} is not supported.")
                };
                current[i] = unchecked((byte)(current[i] + predictor));
            }
        }

        private static int Paeth(int left, int up, int upLeft)
        {
            int p = left + up - upLeft;
            int pa = Math.Abs(p - left);
            int pb = Math.Abs(p - up);
            int pc = Math.Abs(p - upLeft);
            if (pa <= pb && pa <= pc)
            {
                return left;
            }

            return pb <= pc ? up : upLeft;
        }

        private static int ReadInt32BigEndian(byte[] bytes, int offset)
        {
            return (bytes[offset] << 24) |
                (bytes[offset + 1] << 16) |
                (bytes[offset + 2] << 8) |
                bytes[offset + 3];
        }
    }
}
