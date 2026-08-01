#nullable enable

using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace Mmd
{
    /// <summary>
    /// Reads only the fixed-width VMD records needed by the import summary.
    /// This intentionally does not materialize bone, morph, camera, or property DTOs.
    /// Full body playback is delegated to mmd-anim's model-aware native clip builder.
    /// </summary>
    public static class MmdVmdBinarySummaryReader
    {
        private const int HeaderSize = 50;
        private const int BoneRecordSize = 111;
        private const int MorphRecordSize = 23;
        private const int CameraRecordSize = 61;
        private const int LightRecordSize = 28;
        private const int SelfShadowRecordSize = 9;
        private const int PropertyMinimumRecordSize = 9;
        private const int BoneInterpolationOffset = 47;
        private const int BoneInterpolationSize = 64;
        private const string MagicPrefix = "Vocaloid Motion Data 0002\0";

        public static MmdVmdParseSummary Read(byte[] data)
        {
            if (data == null || data.Length == 0)
            {
                throw Invalid("header", "input", 0, "VMD bytes are empty");
            }

            var reader = new Reader(data);
            reader.Require(HeaderSize, "header", "header", "VMD header");
            reader.RequireMagic();

            string modelName = DecodeFixedString(data, 30, 20);
            int boneCount = reader.ReadCount("bone");
            int maxFrame = 0;
            for (int i = 0; i < boneCount; i++)
            {
                maxFrame = Math.Max(maxFrame, reader.ReadFrameAtCurrent(15, "bone", i, "frame"));
                reader.ValidateFiniteSingleAtCurrent(19, "bone", i, "translation.x");
                reader.ValidateFiniteSingleAtCurrent(23, "bone", i, "translation.y");
                reader.ValidateFiniteSingleAtCurrent(27, "bone", i, "translation.z");
                reader.ValidateFiniteSingleAtCurrent(31, "bone", i, "rotation.x");
                reader.ValidateFiniteSingleAtCurrent(35, "bone", i, "rotation.y");
                reader.ValidateFiniteSingleAtCurrent(39, "bone", i, "rotation.z");
                reader.ValidateFiniteSingleAtCurrent(43, "bone", i, "rotation.w");
                // mmd-anim v0.3.3 reads this as [u8; 64] and clamps control
                // points above 127 while decoding. Keep the raw byte range
                // accepted here; only the fixed block length is structural.
                reader.RequireAt(BoneInterpolationOffset, BoneInterpolationSize, "bone", i, "interpolation");
                reader.Skip(BoneRecordSize, "bone", i, "record");
            }

            int morphCount = reader.ReadOptionalCount("morph");
            for (int i = 0; i < morphCount; i++)
            {
                maxFrame = Math.Max(maxFrame, reader.ReadFrameAtCurrent(15, "morph", i, "frame"));
                reader.ValidateFiniteSingleAtCurrent(19, "morph", i, "weight");
                reader.Skip(MorphRecordSize, "morph", i, "record");
            }

            if (!reader.TryReadOptionalCount("camera", CameraRecordSize, out int cameraCount))
            {
                return CreateSummary(
                    modelName,
                    maxFrame,
                    boneCount,
                    morphCount,
                    cameraKeyframeCount: 0,
                    lightKeyframeCount: 0,
                    selfShadowKeyframeCount: 0,
                    propertyCount: 0,
                    constraintStateCount: 0);
            }

            for (int i = 0; i < cameraCount; i++)
            {
                maxFrame = Math.Max(maxFrame, reader.ReadFrameAtCurrent(0, "camera", i, "frame"));
                reader.ValidateFiniteSingleAtCurrent(4, "camera", i, "distance");
                reader.ValidateFiniteSingleAtCurrent(8, "camera", i, "position.x");
                reader.ValidateFiniteSingleAtCurrent(12, "camera", i, "position.y");
                reader.ValidateFiniteSingleAtCurrent(16, "camera", i, "position.z");
                reader.ValidateFiniteSingleAtCurrent(20, "camera", i, "rotation.x");
                reader.ValidateFiniteSingleAtCurrent(24, "camera", i, "rotation.y");
                reader.ValidateFiniteSingleAtCurrent(28, "camera", i, "rotation.z");
                reader.Skip(CameraRecordSize, "camera", i, "record");
            }

            if (!reader.TryReadOptionalCount("light", LightRecordSize, out int lightCount))
            {
                return CreateSummary(
                    modelName,
                    maxFrame,
                    boneCount,
                    morphCount,
                    cameraCount,
                    lightKeyframeCount: 0,
                    selfShadowKeyframeCount: 0,
                    propertyCount: 0,
                    constraintStateCount: 0);
            }

            for (int i = 0; i < lightCount; i++)
            {
                maxFrame = Math.Max(maxFrame, reader.ReadFrameAtCurrent(0, "light", i, "frame"));
                reader.ValidateFiniteSingleAtCurrent(4, "light", i, "color.r");
                reader.ValidateFiniteSingleAtCurrent(8, "light", i, "color.g");
                reader.ValidateFiniteSingleAtCurrent(12, "light", i, "color.b");
                reader.ValidateFiniteSingleAtCurrent(16, "light", i, "direction.x");
                reader.ValidateFiniteSingleAtCurrent(20, "light", i, "direction.y");
                reader.ValidateFiniteSingleAtCurrent(24, "light", i, "direction.z");
                reader.Skip(LightRecordSize, "light", i, "record");
            }

            if (!reader.TryReadOptionalCount("self-shadow", SelfShadowRecordSize, out int selfShadowCount))
            {
                return CreateSummary(
                    modelName,
                    maxFrame,
                    boneCount,
                    morphCount,
                    cameraCount,
                    lightCount,
                    selfShadowKeyframeCount: 0,
                    propertyCount: 0,
                    constraintStateCount: 0);
            }

            for (int i = 0; i < selfShadowCount; i++)
            {
                maxFrame = Math.Max(maxFrame, reader.ReadFrameAtCurrent(0, "self-shadow", i, "frame"));
                reader.ValidateFiniteSingleAtCurrent(5, "self-shadow", i, "distance");
                reader.Skip(SelfShadowRecordSize, "self-shadow", i, "record");
            }

            int propertyCount = reader.ReadOptionalCount("property");
            int constraintStateCount = 0;
            for (int i = 0; i < propertyCount; i++)
            {
                reader.Require(
                    PropertyMinimumRecordSize,
                    "property",
                    i.ToString(CultureInfo.InvariantCulture),
                    "record");
                maxFrame = Math.Max(maxFrame, reader.ReadFrameAtCurrent(0, "property", i, "frame"));
                reader.Skip(5, "property", i, "header");
                int ikCount = reader.ReadCount("property IK", i.ToString(CultureInfo.InvariantCulture));
                if (ikCount > int.MaxValue - constraintStateCount)
                {
                    throw Invalid(
                        "property",
                        i.ToString(CultureInfo.InvariantCulture),
                        reader.Offset,
                        "constraint state count is out of range");
                }

                constraintStateCount += ikCount;
                reader.SkipRecords(ikCount, 21, "property IK", i, "records");
            }

            return CreateSummary(
                modelName,
                maxFrame,
                boneCount,
                morphCount,
                cameraCount,
                lightCount,
                selfShadowCount,
                propertyCount,
                constraintStateCount);
        }

        private static MmdVmdParseSummary CreateSummary(
            string modelName,
            int maxFrame,
            int boneCount,
            int morphCount,
            int cameraKeyframeCount,
            int lightKeyframeCount,
            int selfShadowKeyframeCount,
            int propertyCount,
            int constraintStateCount)
        {
            return new MmdVmdParseSummary(
                modelName,
                maxFrame,
                boneCount,
                morphCount,
                propertyCount,
                constraintStateCount,
                cameraKeyframeCount,
                lightKeyframeCount,
                selfShadowKeyframeCount);
        }

        private static string DecodeFixedString(byte[] data, int offset, int byteCount)
        {
            int end = offset;
            int limit = checked(offset + byteCount);
            while (end < limit && data[end] != 0)
            {
                end++;
            }

            try
            {
                return Encoding.GetEncoding(932).GetString(data, offset, end - offset).TrimEnd(' ', '\0');
            }
            catch (ArgumentException)
            {
                return Encoding.UTF8.GetString(data, offset, end - offset).TrimEnd(' ', '\0');
            }
        }

        private static InvalidDataException Invalid(string section, string index, int offset, string detail)
        {
            return new InvalidDataException(string.Format(
                CultureInfo.InvariantCulture,
                "VMD section={0} index={1} offset={2}: {3}.",
                section,
                index,
                offset,
                detail));
        }

        private ref struct Reader
        {
            private readonly byte[] data;
            private int offset;

            public Reader(byte[] data)
            {
                this.data = data;
                offset = 0;
            }

            public int Offset => offset;

            public void Require(int count, string section, string index, string field)
            {
                if (count < 0 || offset < 0 || offset > data.Length || count > data.Length - offset)
                {
                    throw Invalid(section, index, offset, string.Format(
                        CultureInfo.InvariantCulture,
                        "{0} is truncated (requires {1} bytes)",
                        field,
                        count));
                }
            }

            public void RequireMagic()
            {
                byte[] prefix = Encoding.ASCII.GetBytes(MagicPrefix);
                Require(prefix.Length, "header", "magic", "magic");
                for (int i = 0; i < prefix.Length; i++)
                {
                    if (data[i] != prefix[i])
                    {
                        throw Invalid("header", "magic", i, "magic is invalid");
                    }
                }

                offset = HeaderSize;
            }

            public int ReadCount(string label)
            {
                return ReadCount(label, "count");
            }

            public int ReadCount(string section, string index)
            {
                int countOffset = offset;
                uint value = ReadUInt32(section, index, "count");
                if (value > int.MaxValue)
                {
                    throw Invalid(section, index, countOffset, string.Format(
                        CultureInfo.InvariantCulture,
                        "count is out of range: {0}",
                        value));
                }

                return (int)value;
            }

            public int ReadOptionalCount(string label)
            {
                if (offset == data.Length)
                {
                    return 0;
                }

                return ReadCount(label);
            }

            public bool TryReadOptionalCount(string section, int recordSize, out int count)
            {
                count = 0;
                if (offset == data.Length)
                {
                    return false;
                }

                uint rawCount = ReadUInt32(section, "count", "count");
                long recordBytes = (long)rawCount * recordSize;
                if (rawCount > int.MaxValue || recordBytes > data.Length - offset)
                {
                    offset = data.Length;
                    return false;
                }

                count = (int)rawCount;
                return true;
            }

            public int ReadFrameAtCurrent(int relativeOffset, string section, int index, string field)
            {
                string recordIndex = index.ToString(CultureInfo.InvariantCulture);
                RequireAt(relativeOffset, sizeof(uint), section, recordIndex, field);
                int valueOffset = offset + relativeOffset;
                uint value = ReadUInt32At(valueOffset);
                if (value > int.MaxValue)
                {
                    throw Invalid(section, recordIndex, valueOffset, string.Format(
                        CultureInfo.InvariantCulture,
                        "{0} is out of range: {1}",
                        field,
                        value));
                }

                return (int)value;
            }

            public void ValidateFiniteSingleAtCurrent(int relativeOffset, string section, int index, string field)
            {
                string recordIndex = index.ToString(CultureInfo.InvariantCulture);
                RequireAt(relativeOffset, sizeof(float), section, recordIndex, field);
                int valueOffset = offset + relativeOffset;
                float value = BitConverter.ToSingle(data, valueOffset);
                if (float.IsNaN(value) || float.IsInfinity(value))
                {
                    throw Invalid(section, recordIndex, valueOffset, field + " must be finite");
                }
            }

            public void RequireAt(int relativeOffset, int count, string section, int index, string field)
            {
                RequireAt(
                    relativeOffset,
                    count,
                    section,
                    index.ToString(CultureInfo.InvariantCulture),
                    field);
            }

            public void RequireAt(int relativeOffset, int count, string section, string index, string field)
            {
                int available = offset <= data.Length ? data.Length - offset : 0;
                if (relativeOffset < 0 || count < 0 || relativeOffset > available || count > available - relativeOffset)
                {
                    int errorOffset = offset;
                    if (relativeOffset >= 0 && relativeOffset <= available)
                    {
                        errorOffset += relativeOffset;
                    }

                    throw Invalid(section, index, errorOffset, string.Format(
                        CultureInfo.InvariantCulture,
                        "{0} is truncated (requires {1} bytes)",
                        field,
                        count));
                }
            }

            public void Skip(int count, string section, int index, string field)
            {
                Require(count, section, index.ToString(CultureInfo.InvariantCulture), field);
                offset += count;
            }

            public void SkipRecords(int count, int recordSize, string section, int index, string field)
            {
                long bytes = (long)count * recordSize;
                if (count < 0 || recordSize < 0 || bytes > int.MaxValue)
                {
                    throw Invalid(
                        section,
                        index.ToString(CultureInfo.InvariantCulture),
                        offset,
                        field + " size is out of range");
                }

                Require((int)bytes, section, index.ToString(CultureInfo.InvariantCulture), field);
                offset += (int)bytes;
            }

            private uint ReadUInt32(string section, string index, string field)
            {
                Require(sizeof(uint), section, index, field);
                uint value = ReadUInt32At(offset);
                offset += sizeof(uint);
                return value;
            }

            private uint ReadUInt32At(int position)
            {
                return (uint)(data[position]
                    | (data[position + 1] << 8)
                    | (data[position + 2] << 16)
                    | (data[position + 3] << 24));
            }
        }
    }
}
