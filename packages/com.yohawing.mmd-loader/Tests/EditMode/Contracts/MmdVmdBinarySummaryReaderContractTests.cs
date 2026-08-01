#nullable enable

using System;
using System.IO;
using System.Text;
using NUnit.Framework;

namespace Mmd.Tests
{
    [TestFixture]
    public sealed class MmdVmdBinarySummaryReaderContractTests
    {
        private const int HeaderSize = 50;
        private const int BoneCountOffset = HeaderSize;
        private const int BoneRecordOffset = HeaderSize + sizeof(uint);
        private const int BoneRecordSize = 111;
        private const int MorphCountOffset = BoneRecordOffset + BoneRecordSize;
        private const int MorphRecordOffset = MorphCountOffset + sizeof(uint);

        [Test]
        public void ReadsValidSummaryAndAllowsOptionalTailAtEof()
        {
            byte[] bytes = CreateVmd(
                boneFrame: 10,
                morphFrame: 20,
                morphWeight: 0.75f,
                includeOptionalTail: false);

            MmdVmdParseSummary summary = MmdVmdBinarySummaryReader.Read(bytes);

            Assert.That(summary.TargetModelName, Is.EqualTo("summary-model"));
            Assert.That(summary.MaxFrame, Is.EqualTo(20));
            Assert.That(summary.BoneKeyframeCount, Is.EqualTo(1));
            Assert.That(summary.MorphKeyframeCount, Is.EqualTo(1));
            Assert.That(summary.ModelKeyframeCount, Is.EqualTo(0));
            Assert.That(summary.ConstraintStateCount, Is.EqualTo(0));
            Assert.That(summary.CameraKeyframeCount, Is.EqualTo(0));
            Assert.That(summary.LightKeyframeCount, Is.EqualTo(0));
            Assert.That(summary.SelfShadowKeyframeCount, Is.EqualTo(0));
        }

        [Test]
        public void TreatsMalformedOptionalCameraTailAsAbsentLikeNativeParser()
        {
            byte[] bytes = CreateVmd(morphCount: 0, includeOptionalTail: false);
            using (var stream = new MemoryStream())
            {
                stream.Write(bytes, 0, bytes.Length);
                using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
                {
                    writer.Write(300u); // camera count exceeds the remaining tail bytes
                    writer.Write(new byte[57]);
                }

                bytes = stream.ToArray();
            }

            MmdVmdParseSummary summary = MmdVmdBinarySummaryReader.Read(bytes);

            Assert.That(summary.MaxFrame, Is.EqualTo(10));
            Assert.That(summary.CameraKeyframeCount, Is.EqualTo(0));
            Assert.That(summary.LightKeyframeCount, Is.EqualTo(0));
            Assert.That(summary.SelfShadowKeyframeCount, Is.EqualTo(0));
            Assert.That(summary.ModelKeyframeCount, Is.EqualTo(0));
        }

        [Test]
        public void ReadsValidSummaryWithPropertySection()
        {
            byte[] bytes = CreateVmd(
                boneFrame: 10,
                morphFrame: 20,
                morphWeight: 0.75f,
                includeOptionalTail: true);

            MmdVmdParseSummary summary = MmdVmdBinarySummaryReader.Read(bytes);

            Assert.That(summary.MaxFrame, Is.EqualTo(30));
            Assert.That(summary.ModelKeyframeCount, Is.EqualTo(1));
            Assert.That(summary.ConstraintStateCount, Is.EqualTo(1));
        }

        [Test]
        public void RejectsTruncatedBoneInterpolationBlockWithLocation()
        {
            byte[] bytes = CreateVmd(morphCount: 0, includeOptionalTail: false);
            Array.Resize(ref bytes, BoneRecordOffset + BoneRecordSize - 1);

            InvalidDataException exception = AssertInvalid(bytes, "bone", "0");

            Assert.That(exception.Message, Does.Contain("offset=101"));
            Assert.That(exception.Message, Does.Contain("interpolation"));
        }

        [Test]
        public void RejectsTruncatedMorphRecordWithLocation()
        {
            byte[] bytes = CreateVmd(includeOptionalTail: false);
            Array.Resize(ref bytes, MorphRecordOffset + 22);

            InvalidDataException exception = AssertInvalid(bytes, "morph", "0");

            Assert.That(exception.Message, Does.Contain("offset=188"));
            Assert.That(exception.Message, Does.Contain("weight"));
        }

        [Test]
        public void RejectsCountOutsideManagedSummaryRange()
        {
            byte[] bytes = CreateVmd(boneCount: 0, morphCount: 0, includeOptionalTail: false);
            WriteUInt32(bytes, BoneCountOffset, uint.MaxValue);

            InvalidDataException exception = AssertInvalid(bytes, "bone", "count");

            Assert.That(exception.Message, Does.Contain("offset=50"));
            Assert.That(exception.Message, Does.Contain("out of range"));
        }

        [Test]
        public void RejectsFrameOutsideManagedSummaryRange()
        {
            byte[] bytes = CreateVmd(morphCount: 0, includeOptionalTail: false);
            WriteUInt32(bytes, BoneRecordOffset + 15, uint.MaxValue);

            InvalidDataException exception = AssertInvalid(bytes, "bone", "0");

            Assert.That(exception.Message, Does.Contain("offset=69"));
            Assert.That(exception.Message, Does.Contain("frame"));
            Assert.That(exception.Message, Does.Contain("out of range"));
        }

        [Test]
        public void RejectsNonFiniteBoneAndMorphValues()
        {
            byte[] boneBytes = CreateVmd(morphCount: 0, includeOptionalTail: false);
            WriteUInt32(boneBytes, BoneRecordOffset + 19, 0x7FC00000u);
            InvalidDataException boneException = AssertInvalid(boneBytes, "bone", "0");
            Assert.That(boneException.Message, Does.Contain("translation.x"));

            byte[] morphBytes = CreateVmd(includeOptionalTail: false);
            WriteUInt32(morphBytes, MorphRecordOffset + 19, 0x7F800000u);
            InvalidDataException morphException = AssertInvalid(morphBytes, "morph", "0");
            Assert.That(morphException.Message, Does.Contain("weight"));
        }

        [Test]
        public void AcceptsRawInterpolationBytesAbove127AsNativeParserDoes()
        {
            var interpolation = new byte[64];
            for (int i = 0; i < interpolation.Length; i++)
            {
                interpolation[i] = byte.MaxValue;
            }

            byte[] bytes = CreateVmd(
                morphCount: 0,
                interpolation: interpolation,
                includeOptionalTail: false);

            MmdVmdParseSummary summary = MmdVmdBinarySummaryReader.Read(bytes);

            Assert.That(summary.BoneKeyframeCount, Is.EqualTo(1));
        }

        [Test]
        public void RejectsHeaderWithoutNativeTerminator()
        {
            byte[] bytes = CreateVmd(morphCount: 0, includeOptionalTail: false);
            bytes[25] = 1;

            AssertInvalid(bytes, "header", "magic");
        }

        [Test]
        public void RejectsEmptyBoneName()
        {
            byte[] bytes = CreateVmd(morphCount: 0, includeOptionalTail: false);
            Array.Clear(bytes, BoneRecordOffset, 15);

            AssertInvalid(bytes, "bone", "0");
        }

        [Test]
        public void RejectsEmptyMorphName()
        {
            byte[] bytes = CreateVmd(boneCount: 0, includeOptionalTail: false);
            Array.Clear(bytes, HeaderSize + sizeof(uint) + sizeof(uint), 15);

            AssertInvalid(bytes, "morph", "0");
        }

        [Test]
        public void RejectsEmptyPropertyIkName()
        {
            byte[] bytes = CreateVmd(boneCount: 0, morphCount: 0, includeOptionalTail: true);
            Array.Clear(bytes, 83, 20);

            InvalidDataException exception = AssertInvalid(bytes, "property IK", "0[0]");
            Assert.That(exception.Message, Does.Contain("boneName"));
        }

        [Test]
        public void RejectsNonFiniteCameraValue()
        {
            byte[] bytes = CreateVmd(morphCount: 0, includeOptionalTail: false);
            using (var stream = new MemoryStream())
            {
                stream.Write(bytes, 0, bytes.Length);
                using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
                {
                    writer.Write(1u); // camera count
                    writer.Write(0u); // frame
                    writer.Write(float.NaN); // distance
                    for (int i = 0; i < 6; i++)
                    {
                        writer.Write(0.0f);
                    }

                    writer.Write(new byte[24]); // interpolation
                    writer.Write(0u); // view angle
                    writer.Write((byte)0); // perspective
                    writer.Write(0u); // light count
                    writer.Write(0u); // self-shadow count
                    writer.Write(0u); // property count
                }

                bytes = stream.ToArray();
            }

            InvalidDataException exception = AssertInvalid(bytes, "camera", "0");

            Assert.That(exception.Message, Does.Contain("distance"));
            Assert.That(exception.Message, Does.Contain("finite"));
        }

        private static InvalidDataException AssertInvalid(byte[] bytes, string section, string index)
        {
            InvalidDataException first = Assert.Throws<InvalidDataException>(
                () => MmdVmdBinarySummaryReader.Read(bytes))!;
            InvalidDataException second = Assert.Throws<InvalidDataException>(
                () => MmdVmdBinarySummaryReader.Read(bytes))!;

            Assert.That(first.Message, Is.EqualTo(second.Message));
            Assert.That(first.Message, Does.Contain("section=" + section));
            Assert.That(first.Message, Does.Contain("index=" + index));
            Assert.That(first.Message, Does.Contain("offset="));
            return first;
        }

        private static byte[] CreateVmd(
            int boneCount = 1,
            int morphCount = 1,
            uint boneFrame = 10,
            uint morphFrame = 20,
            float morphWeight = 0.5f,
            byte[]? interpolation = null,
            bool includeOptionalTail = true)
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            WriteFixedAscii(writer, "Vocaloid Motion Data 0002", 30);
            WriteFixedAscii(writer, "summary-model", 20);
            writer.Write((uint)boneCount);

            byte[] boneInterpolation = interpolation ?? CreateLinearInterpolation();
            for (int i = 0; i < boneCount; i++)
            {
                WriteFixedAscii(writer, "center", 15);
                writer.Write(boneFrame);
                writer.Write(1.0f);
                writer.Write(2.0f);
                writer.Write(3.0f);
                writer.Write(0.0f);
                writer.Write(0.0f);
                writer.Write(0.0f);
                writer.Write(1.0f);
                writer.Write(boneInterpolation);
            }

            writer.Write((uint)morphCount);
            for (int i = 0; i < morphCount; i++)
            {
                WriteFixedAscii(writer, "smile", 15);
                writer.Write(morphFrame);
                writer.Write(morphWeight);
            }

            if (includeOptionalTail)
            {
                writer.Write(0u); // camera count
                writer.Write(0u); // light count
                writer.Write(0u); // self-shadow count
                writer.Write(1u); // property count
                writer.Write(30u);
                writer.Write((byte)1);
                writer.Write(1u); // IK count
                WriteFixedAscii(writer, "center_ik", 20);
                writer.Write((byte)1);
            }

            return stream.ToArray();
        }

        private static byte[] CreateLinearInterpolation()
        {
            var interpolation = new byte[64];
            for (int channel = 0; channel < 4; channel++)
            {
                interpolation[channel] = 20;
                interpolation[channel + 4] = 20;
                interpolation[channel + 8] = 107;
                interpolation[channel + 12] = 107;
            }

            return interpolation;
        }

        private static void WriteFixedAscii(BinaryWriter writer, string value, int length)
        {
            var bytes = new byte[length];
            byte[] encoded = Encoding.ASCII.GetBytes(value);
            Buffer.BlockCopy(encoded, 0, bytes, 0, Math.Min(encoded.Length, bytes.Length));
            writer.Write(bytes);
        }

        private static void WriteUInt32(byte[] data, int offset, uint value)
        {
            data[offset] = (byte)value;
            data[offset + 1] = (byte)(value >> 8);
            data[offset + 2] = (byte)(value >> 16);
            data[offset + 3] = (byte)(value >> 24);
        }
    }
}
