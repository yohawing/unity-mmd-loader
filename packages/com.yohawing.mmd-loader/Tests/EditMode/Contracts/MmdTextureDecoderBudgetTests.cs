#nullable enable

using System;
using System.IO;
using System.IO.Compression;
using Mmd.UnityIntegration;
using NUnit.Framework;
using UnityEngine;

namespace Mmd.Tests
{
    [TestFixture]
    [Timeout(5000)]
    public sealed class MmdTextureDecoderBudgetTests
    {
        [Test]
        public void OversizedPngDimensionsAreRejectedBeforeInflation()
        {
            byte[] png = CreatePng(
                MmdTextureDecodeBudget.DefaultMaxDimension + 1,
                1,
                new byte[] { 0, 1, 2, 3, 4 });

            ArgumentException error = Assert.Throws<ArgumentException>(() => MmdPngDecoder.Decode(png, "oversized"))!;
            Assert.That(error.Message, Does.Contain("dimensions").And.Contain("decode budget"));
        }

        [Test]
        public void PngChunkCountIsBounded()
        {
            MmdTextureDecodeBudget budget = TinyBudget(maxChunks: 3);
            byte[] png = CreatePng(1, 1, new byte[] { 0, 1, 2, 3, 4 }, ancillaryChunks: 2);

            ArgumentException error = Assert.Throws<ArgumentException>(() => MmdPngDecoder.Decode(png, "chunks", budget))!;
            Assert.That(error.Message, Does.Contain("chunk count").And.Contain("3"));
        }

        [TestCase(4, "ended before")]
        [TestCase(6, "exceeds the expected")]
        public void PngInflationMustMatchExpectedScanlineLength(int inflatedLength, string expectedMessage)
        {
            byte[] png = CreatePng(1, 1, new byte[inflatedLength]);

            ArgumentException error = Assert.Throws<ArgumentException>(() => MmdPngDecoder.Decode(png, "inflate"))!;
            Assert.That(error.Message, Does.Contain(expectedMessage));
        }

        [Test]
        public void ValidRgbaPngPreservesAlphaAndVerticalOrientation()
        {
            byte[] scanlines =
            {
                0, 255, 0, 0, 64,
                0, 0, 0, 255, 128
            };
            Texture2D texture = MmdPngDecoder.Decode(CreatePng(1, 2, scanlines), "valid");
            try
            {
                Color32[] pixels = texture.GetPixels32();
                Assert.That(pixels, Has.Length.EqualTo(2));
                Assert.That(pixels[0], Is.EqualTo(new Color32(0, 0, 255, 128)));
                Assert.That(pixels[1], Is.EqualTo(new Color32(255, 0, 0, 64)));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void BoundedFileReaderRejectsOversizedSparseFileBeforeReadingPayload()
        {
            string path = Path.Combine(Path.GetTempPath(), "mmd-texture-budget-" + Guid.NewGuid().ToString("N") + ".bin");
            try
            {
                using (FileStream stream = File.Create(path))
                {
                    stream.SetLength(33);
                }

                MmdTextureDecodeBudget budget = new(32, 16, 256, 1024, 8);
                ArgumentException error = Assert.Throws<ArgumentException>(() => budget.ReadFileBytes(path))!;
                Assert.That(error.Message, Does.Contain("33").And.Contain("32"));
            }
            finally
            {
                File.Delete(path);
            }
        }

        private static MmdTextureDecodeBudget TinyBudget(int maxChunks) => new(
            maxInputBytes: 4096,
            maxDimension: 16,
            maxPixelCount: 256,
            maxExpandedBytes: 1024,
            maxPngChunkCount: maxChunks);

        private static byte[] CreatePng(int width, int height, byte[] inflated, int ancillaryChunks = 0)
        {
            using var stream = new MemoryStream();
            stream.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, 0, 8);
            using (var header = new MemoryStream())
            {
                WriteInt32BigEndian(header, width);
                WriteInt32BigEndian(header, height);
                header.WriteByte(8);
                header.WriteByte(6);
                header.WriteByte(0);
                header.WriteByte(0);
                header.WriteByte(0);
                WriteChunk(stream, "IHDR", header.ToArray());
            }

            for (int i = 0; i < ancillaryChunks; i++)
            {
                WriteChunk(stream, "tEXt", Array.Empty<byte>());
            }

            WriteChunk(stream, "IDAT", CreateZlib(inflated));
            WriteChunk(stream, "IEND", Array.Empty<byte>());
            return stream.ToArray();
        }

        private static byte[] CreateZlib(byte[] data)
        {
            using var stream = new MemoryStream();
            stream.WriteByte(0x78);
            stream.WriteByte(0x01);
            using (var deflate = new DeflateStream(stream, System.IO.Compression.CompressionLevel.NoCompression, leaveOpen: true))
            {
                deflate.Write(data, 0, data.Length);
            }

            WriteInt32BigEndian(stream, unchecked((int)Adler32(data)));
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

        private static void WriteChunk(Stream stream, string type, byte[] data)
        {
            WriteInt32BigEndian(stream, data.Length);
            byte[] typeBytes = System.Text.Encoding.ASCII.GetBytes(type);
            stream.Write(typeBytes, 0, typeBytes.Length);
            stream.Write(data, 0, data.Length);
            WriteInt32BigEndian(stream, 0);
        }

        private static void WriteInt32BigEndian(Stream stream, int value)
        {
            stream.WriteByte((byte)(value >> 24));
            stream.WriteByte((byte)(value >> 16));
            stream.WriteByte((byte)(value >> 8));
            stream.WriteByte((byte)value);
        }
    }
}
