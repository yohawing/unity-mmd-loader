#nullable enable
#pragma warning disable CS0649

using System;

namespace Mmd.Parser
{
    public sealed partial class NativeMmdParser : IMmdParser
    {
        private readonly Func<byte[], string> parseVmdJson;
        private readonly Func<byte[], string> parsePmxNonGeometryJson;
        private readonly Func<byte[], PmxModelSourceGeometry> createPmxGeometry;

        public NativeMmdParser()
            : this(MmdParserFfiMethods.ParseVmdJson,
                   MmdParserFfiMethods.ParsePmxNonGeometryJson, CreatePmxGeometryFromNativeBuffers)
        {
        }

        internal NativeMmdParser(
            Func<byte[], string> parseVmdJson)
            : this(parseVmdJson,
                   MmdParserFfiMethods.ParsePmxNonGeometryJson, CreatePmxGeometryFromNativeBuffers)
        {
        }

        internal NativeMmdParser(
            Func<byte[], string> parseVmdJson,
            Func<byte[], string> parsePmxNonGeometryJson,
            Func<byte[], PmxModelSourceGeometry> createPmxGeometry)
        {
            this.parseVmdJson = parseVmdJson ?? throw new ArgumentNullException(nameof(parseVmdJson));
            this.parsePmxNonGeometryJson = parsePmxNonGeometryJson ?? throw new ArgumentNullException(nameof(parsePmxNonGeometryJson));
            this.createPmxGeometry = createPmxGeometry ?? throw new ArgumentNullException(nameof(createPmxGeometry));
        }

        public MmdModelDefinition LoadModel(ReadOnlySpan<byte> data)
        {
            MmdParserInput.RequireNonEmpty(data, nameof(data));
            byte[] bytes = data.ToArray();
            string json = parsePmxNonGeometryJson(bytes);
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new InvalidOperationException("mmd-runtime PMX non-geometry JSON parser returned empty JSON.");
            }

            PmxModelSourceSnapshot snapshot = UnityEngine.JsonUtility.FromJson<PmxModelSourceSnapshot>(json)
                ?? new PmxModelSourceSnapshot();
            snapshot.geometry = createPmxGeometry(bytes);
            MmdModelDefinition model = BuildModelDefinition(snapshot);
            model.sourceBytes = bytes;
            return model;
        }

        public MmdMotionDefinition LoadMotion(ReadOnlySpan<byte> data)
        {
            MmdParserInput.RequireNonEmpty(data, nameof(data));
            string json = parseVmdJson(data.ToArray());
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new InvalidOperationException("mmd-runtime VMD JSON parser returned empty JSON.");
            }

            VmdParsedAnimationJson? parsed = UnityEngine.JsonUtility.FromJson<VmdParsedAnimationJson>(json);
            MmdMotionDefinition motion = BuildMotionDefinition(CreateMotionSnapshot(parsed));
            motion.sourceBytes = data.ToArray();
            return motion;
        }

        private static int CountToArrayLength(int value, string label)
        {
            if (value < 0)
            {
                throw new InvalidOperationException(label + " is out of range: " + value);
            }

            return value;
        }

        private static int CheckedUIntToInt(uint value, string label)
        {
            if (value > int.MaxValue)
            {
                throw new InvalidOperationException(label + " is out of range: " + value);
            }

            return (int)value;
        }

        private static int UIntCountToInt(uint value)
        {
            return value > int.MaxValue ? int.MaxValue : (int)value;
        }

        private static float GetFloat(float[]? values, int index, float fallback)
        {
            return values != null && index >= 0 && index < values.Length ? values[index] : fallback;
        }

        private static float[] CopyVec3(float[]? values)
        {
            return new[] { GetFloat(values, 0, 0.0f), GetFloat(values, 1, 0.0f), GetFloat(values, 2, 0.0f) };
        }

        private static float[] CopyVec4(float[]? values)
        {
            return new[] { GetFloat(values, 0, 0.0f), GetFloat(values, 1, 0.0f), GetFloat(values, 2, 0.0f), GetFloat(values, 3, 0.0f) };
        }
    }
}