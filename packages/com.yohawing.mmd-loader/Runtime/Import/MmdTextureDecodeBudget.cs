#nullable enable

using System;
using System.IO;

namespace Mmd.UnityIntegration
{
    internal readonly struct MmdTextureDecodeBudget
    {
        internal const long DefaultMaxExpandedBytes = 256L * 1024 * 1024;
        internal const long DefaultMaxInputBytes = DefaultMaxExpandedBytes + (1L * 1024 * 1024);
        internal const int DefaultMaxDimension = 16_384;
        internal const long DefaultMaxPixelCount = 67_108_864;
        internal const int DefaultMaxPngChunkCount = 4_096;

        internal static readonly MmdTextureDecodeBudget Default = new(
            DefaultMaxInputBytes,
            DefaultMaxDimension,
            DefaultMaxPixelCount,
            DefaultMaxExpandedBytes,
            DefaultMaxPngChunkCount);

        internal MmdTextureDecodeBudget(
            long maxInputBytes,
            int maxDimension,
            long maxPixelCount,
            long maxExpandedBytes,
            int maxPngChunkCount)
        {
            if (maxInputBytes <= 0) throw new ArgumentOutOfRangeException(nameof(maxInputBytes));
            if (maxDimension <= 0) throw new ArgumentOutOfRangeException(nameof(maxDimension));
            if (maxPixelCount <= 0) throw new ArgumentOutOfRangeException(nameof(maxPixelCount));
            if (maxExpandedBytes <= 0) throw new ArgumentOutOfRangeException(nameof(maxExpandedBytes));
            if (maxPngChunkCount <= 0) throw new ArgumentOutOfRangeException(nameof(maxPngChunkCount));
            MaxInputBytes = maxInputBytes;
            MaxDimension = maxDimension;
            MaxPixelCount = maxPixelCount;
            MaxExpandedBytes = maxExpandedBytes;
            MaxPngChunkCount = maxPngChunkCount;
        }

        internal long MaxInputBytes { get; }
        internal int MaxDimension { get; }
        internal long MaxPixelCount { get; }
        internal long MaxExpandedBytes { get; }
        internal int MaxPngChunkCount { get; }

        internal void ValidateInputLength(long length)
        {
            if (length < 0 || length > MaxInputBytes)
            {
                throw new ArgumentException($"Texture input length {length} exceeds the decode budget {MaxInputBytes} bytes.");
            }
        }

        internal int ValidateImageAndGetPngInflatedLength(int width, int height, int sourceBytesPerPixel)
        {
            if (width <= 0 || height <= 0)
            {
                throw new ArgumentException("Texture width and height must be positive.");
            }

            if (width > MaxDimension || height > MaxDimension)
            {
                throw new ArgumentException($"Texture dimensions {width}x{height} exceed the decode budget {MaxDimension}.");
            }

            long pixels = checked((long)width * height);
            if (pixels > MaxPixelCount)
            {
                throw new ArgumentException($"Texture pixel count {pixels} exceeds the decode budget {MaxPixelCount}.");
            }

            long rgbaBytes = checked(pixels * 4L);
            long rowBytes = checked((long)width * sourceBytesPerPixel);
            long inflatedBytes = checked((rowBytes + 1L) * height);
            if (rgbaBytes > MaxExpandedBytes || inflatedBytes > MaxExpandedBytes)
            {
                throw new ArgumentException($"Texture expanded data exceeds the decode budget {MaxExpandedBytes} bytes.");
            }

            if (inflatedBytes > int.MaxValue)
            {
                throw new ArgumentException("Texture expanded data exceeds the managed array limit.");
            }

            return (int)inflatedBytes;
        }

        internal byte[] ReadFileBytes(string path)
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            ValidateInputLength(stream.Length);
            int length = checked((int)stream.Length);
            var bytes = new byte[length];
            int offset = 0;
            while (offset < bytes.Length)
            {
                int read = stream.Read(bytes, offset, bytes.Length - offset);
                if (read == 0)
                {
                    throw new IOException("Texture file ended before its declared length was read.");
                }

                offset += read;
            }

            if (stream.ReadByte() != -1)
            {
                throw new ArgumentException("Texture file grew beyond the decode budget while it was being read.");
            }

            return bytes;
        }
    }
}
