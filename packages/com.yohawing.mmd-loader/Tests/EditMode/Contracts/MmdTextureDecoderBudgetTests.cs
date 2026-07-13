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

        [Test]
        public void BmpMinimumSignedHeightIsRejectedBeforeAbsoluteValueOverflow()
        {
            byte[] bmp = CreateBmpHeader(width: 1, height: int.MinValue, bitsPerPixel: 24);
            ArgumentException error = Assert.Throws<ArgumentException>(() => MmdBmpDecoder.Decode(bmp, "bmp-min-height"))!;
            Assert.That(error.Message, Does.Contain("width and height"));
        }

        [Test]
        public void DdsHugeDimensionsAreRejectedBeforeBlockArithmeticOrAllocation()
        {
            byte[] dds = CreateDdsHeader(width: int.MaxValue, height: 1);
            ArgumentException error = Assert.Throws<ArgumentException>(() => MmdDdsDecoder.Decode(dds, "dds-huge"))!;
            Assert.That(error.Message, Does.Contain("dimensions").And.Contain("decode budget"));
        }

        [Test]
        public void TgaHugeDimensionsAreRejectedBeforePixelAllocation()
        {
            byte[] tga = CreateTgaHeader(width: ushort.MaxValue, height: ushort.MaxValue, bitsPerPixel: 32);
            ArgumentException error = Assert.Throws<ArgumentException>(() => MmdTgaDecoder.Decode(tga, "tga-huge"))!;
            Assert.That(error.Message, Does.Contain("dimensions").And.Contain("decode budget"));
        }

        [Test]
        public void CustomDecodersRejectTruncatedPayloadsAfterBudgetValidation()
        {
            Assert.That(
                Assert.Throws<ArgumentException>(() => MmdBmpDecoder.Decode(CreateBmpHeader(1, 1, 24), "bmp"))!.Message,
                Does.Contain("truncated"));
            Assert.That(
                Assert.Throws<ArgumentException>(() => MmdDdsDecoder.Decode(CreateDdsHeader(4, 4), "dds"))!.Message,
                Does.Contain("truncated"));
            Assert.That(
                Assert.Throws<ArgumentException>(() => MmdTgaDecoder.Decode(CreateTgaHeader(1, 1, 24), "tga"))!.Message,
                Does.Contain("ended before"));
        }

        [Test]
        public void OversizedJpegFrameIsRejectedBeforeUnityImageDecode()
        {
            byte[] jpeg = CreateJpegFrameHeader(MmdTextureDecodeBudget.DefaultMaxDimension + 1, 1);
            ArgumentException error = Assert.Throws<ArgumentException>(() =>
                MmdJpegHeaderValidator.Validate(jpeg, MmdTextureDecodeBudget.Default))!;
            Assert.That(error.Message, Does.Contain("dimensions").And.Contain("decode budget"));
        }

        [Test]
        public void ValidJpegStillLoadsAfterHeaderPreflight()
        {
            var source = new Texture2D(2, 3, TextureFormat.RGBA32, mipChain: false);
            Texture2D? decoded = null;
            try
            {
                source.SetPixels(new[]
                {
                    Color.red, Color.green,
                    Color.blue, Color.white,
                    Color.black, Color.yellow
                });
                source.Apply();
                decoded = MmdRuntimeTextureResolver.DecodeTextureBytes(source.EncodeToJPG(), ".jpg", "jpeg-valid");
                Assert.That(decoded, Is.Not.Null);
                Assert.That(decoded!.width, Is.EqualTo(2));
                Assert.That(decoded.height, Is.EqualTo(3));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(source);
                if (decoded != null) UnityEngine.Object.DestroyImmediate(decoded);
            }
        }

        [Test]
        public void UnknownExtensionDoesNotFallThroughToUnityImageDecoder()
        {
            var source = new Texture2D(1, 1, TextureFormat.RGBA32, mipChain: false);
            try
            {
                source.SetPixel(0, 0, Color.white);
                source.Apply();
                Assert.That(MmdRuntimeTextureResolver.DecodeTextureBytes(source.EncodeToPNG(), ".gif", "unknown"), Is.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(source);
            }
        }

        [TestCase(16, 6, 0, "bit depth 16")]
        [TestCase(8, 3, 0, "color type 3")]
        [TestCase(8, 6, 1, "interlace")]
        public void UnsupportedPngModesAreRejectedBeforeInflation(
            byte bitDepth,
            byte colorType,
            byte interlace,
            string expectedMessage)
        {
            byte[] png = CreatePng(1, 1, new byte[] { 0, 1, 2, 3, 4 }, bitDepth: bitDepth, colorType: colorType, interlace: interlace);
            NotSupportedException error = Assert.Throws<NotSupportedException>(() => MmdPngDecoder.Decode(png, "unsupported-png"))!;
            Assert.That(error.Message, Does.Contain(expectedMessage));
        }

        [Test]
        public void MalformedPngChunkLengthAndMissingEndAreRejected()
        {
            byte[] invalidLength = new byte[20];
            new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }.CopyTo(invalidLength, 0);
            invalidLength[8] = 0xff;
            invalidLength[9] = 0xff;
            invalidLength[10] = 0xff;
            invalidLength[11] = 0xff;
            invalidLength[12] = (byte)'I';
            invalidLength[13] = (byte)'H';
            invalidLength[14] = (byte)'D';
            invalidLength[15] = (byte)'R';
            Assert.That(
                Assert.Throws<ArgumentException>(() => MmdPngDecoder.Decode(invalidLength, "bad-length"))!.Message,
                Does.Contain("chunk length"));

            byte[] complete = CreatePng(1, 1, new byte[] { 0, 1, 2, 3, 4 });
            Array.Resize(ref complete, complete.Length - 12);
            Assert.That(
                Assert.Throws<ArgumentException>(() => MmdPngDecoder.Decode(complete, "missing-end"))!.Message,
                Does.Contain("missing required"));
        }

        [Test]
        public void IndexedBmpWithoutPaletteIsRejected()
        {
            byte[] bmp = CreateBmpHeader(1, 1, 8);
            Array.Resize(ref bmp, 58);
            Assert.That(
                Assert.Throws<ArgumentException>(() => MmdBmpDecoder.Decode(bmp, "indexed-bmp"))!.Message,
                Does.Contain("palette data is truncated"));
        }

        [TestCase("DXT1")]
        [TestCase("DXT3")]
        [TestCase("DXT5")]
        public void DdsCompressedFormatsRejectTruncatedBlocks(string fourCc)
        {
            byte[] dds = CreateDdsHeader(4, 4, fourCc);
            Assert.That(
                Assert.Throws<ArgumentException>(() => MmdDdsDecoder.Decode(dds, "dds-truncated"))!.Message,
                Does.Contain("truncated"));
        }

        [Test]
        public void TgaRlePacketsRejectRunOverflowAndTruncation()
        {
            byte[] overflow = CreateTgaHeader(1, 1, 24);
            overflow[2] = 10;
            Array.Resize(ref overflow, 22);
            overflow[18] = 0x81;
            Assert.That(
                Assert.Throws<ArgumentException>(() => MmdTgaDecoder.Decode(overflow, "tga-overflow"))!.Message,
                Does.Contain("exceeds the expected pixel count"));

            byte[] truncated = CreateTgaHeader(1, 1, 24);
            truncated[2] = 10;
            Array.Resize(ref truncated, 19);
            truncated[18] = 0x80;
            Assert.That(
                Assert.Throws<ArgumentException>(() => MmdTgaDecoder.Decode(truncated, "tga-truncated"))!.Message,
                Does.Contain("pixel data is truncated"));
        }

        [Test]
        public void RuntimeDecodeEntryContainsAdversarialDecoderFailures()
        {
            byte[] pngBomb = CreatePng(1, 1, new byte[1024]);
            byte[] tgaOverflow = CreateTgaHeader(1, 1, 24);
            tgaOverflow[2] = 10;
            Array.Resize(ref tgaOverflow, 22);
            tgaOverflow[18] = 0x81;

            Assert.That(MmdRuntimeTextureResolver.DecodeTextureBytes(pngBomb, ".png", "png-bomb"), Is.Null);
            Assert.That(MmdRuntimeTextureResolver.DecodeTextureBytes(CreateBmpHeader(1, 1, 8), ".bmp", "bmp-palette"), Is.Null);
            Assert.That(MmdRuntimeTextureResolver.DecodeTextureBytes(CreateDdsHeader(4, 4, "DXT5"), ".dds", "dds-short"), Is.Null);
            Assert.That(MmdRuntimeTextureResolver.DecodeTextureBytes(tgaOverflow, ".tga", "tga-overflow"), Is.Null);
        }

        private static MmdTextureDecodeBudget TinyBudget(int maxChunks) => new(
            maxInputBytes: 4096,
            maxDimension: 16,
            maxPixelCount: 256,
            maxExpandedBytes: 1024,
            maxPngChunkCount: maxChunks);

        private static byte[] CreatePng(
            int width,
            int height,
            byte[] inflated,
            int ancillaryChunks = 0,
            byte bitDepth = 8,
            byte colorType = 6,
            byte interlace = 0)
        {
            using var stream = new MemoryStream();
            stream.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, 0, 8);
            using (var header = new MemoryStream())
            {
                WriteInt32BigEndian(header, width);
                WriteInt32BigEndian(header, height);
                header.WriteByte(bitDepth);
                header.WriteByte(colorType);
                header.WriteByte(0);
                header.WriteByte(0);
                header.WriteByte(interlace);
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

        private static byte[] CreateBmpHeader(int width, int height, ushort bitsPerPixel)
        {
            var bytes = new byte[54];
            bytes[0] = (byte)'B';
            bytes[1] = (byte)'M';
            WriteInt32LittleEndian(bytes, 10, 54);
            WriteInt32LittleEndian(bytes, 14, 40);
            WriteInt32LittleEndian(bytes, 18, width);
            WriteInt32LittleEndian(bytes, 22, height);
            bytes[26] = 1;
            bytes[28] = (byte)bitsPerPixel;
            bytes[29] = (byte)(bitsPerPixel >> 8);
            return bytes;
        }

        private static byte[] CreateDdsHeader(int width, int height, string fourCc = "DXT1")
        {
            var bytes = new byte[128];
            bytes[0] = (byte)'D';
            bytes[1] = (byte)'D';
            bytes[2] = (byte)'S';
            bytes[3] = (byte)' ';
            WriteInt32LittleEndian(bytes, 4, 124);
            WriteInt32LittleEndian(bytes, 12, height);
            WriteInt32LittleEndian(bytes, 16, width);
            WriteInt32LittleEndian(bytes, 76, 32);
            bytes[84] = (byte)fourCc[0];
            bytes[85] = (byte)fourCc[1];
            bytes[86] = (byte)fourCc[2];
            bytes[87] = (byte)fourCc[3];
            return bytes;
        }

        private static byte[] CreateTgaHeader(ushort width, ushort height, byte bitsPerPixel)
        {
            var bytes = new byte[18];
            bytes[2] = 2;
            bytes[12] = (byte)width;
            bytes[13] = (byte)(width >> 8);
            bytes[14] = (byte)height;
            bytes[15] = (byte)(height >> 8);
            bytes[16] = bitsPerPixel;
            return bytes;
        }

        private static byte[] CreateJpegFrameHeader(int width, int height)
        {
            using var stream = new MemoryStream();
            stream.WriteByte(0xff);
            stream.WriteByte(0xd8);
            stream.WriteByte(0xff);
            stream.WriteByte(0xc0);
            stream.WriteByte(0x00);
            stream.WriteByte(0x11);
            stream.WriteByte(8);
            stream.WriteByte((byte)(height >> 8));
            stream.WriteByte((byte)height);
            stream.WriteByte((byte)(width >> 8));
            stream.WriteByte((byte)width);
            stream.WriteByte(3);
            for (int component = 1; component <= 3; component++)
            {
                stream.WriteByte((byte)component);
                stream.WriteByte(0x11);
                stream.WriteByte(0);
            }
            stream.WriteByte(0xff);
            stream.WriteByte(0xd9);
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

        private static void WriteInt32LittleEndian(byte[] bytes, int offset, int value)
        {
            bytes[offset] = (byte)value;
            bytes[offset + 1] = (byte)(value >> 8);
            bytes[offset + 2] = (byte)(value >> 16);
            bytes[offset + 3] = (byte)(value >> 24);
        }
    }
}
