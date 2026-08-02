#nullable enable
#pragma warning disable CS0649

using System;
using Mmd;
using Mmd.Native;

namespace Mmd.Parser
{
    public sealed partial class NativeMmdParser : IMmdParser
    {
        private readonly Func<byte[], string> parsePmxNonGeometryJson;
        private readonly Func<byte[], PmxModelSourceGeometry> createPmxGeometry;

        public NativeMmdParser()
            : this(MmdParserFfiMethods.ParsePmxNonGeometryJson, CreatePmxGeometryFromNativeBuffers)
        {
        }

        internal NativeMmdParser(
            Func<byte[], string> parsePmxNonGeometryJson,
            Func<byte[], PmxModelSourceGeometry> createPmxGeometry)
        {
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

            // Complete and release the native geometry handle before materializing the
            // non-geometry JSON object graph. The handle owns a parsed PMX model, so keeping
            // it alive beside the snapshot needlessly raises peak memory for large PMX.
            PmxModelSourceGeometry geometry = createPmxGeometry(bytes);
            PmxModelSourceSnapshot snapshot = UnityEngine.JsonUtility.FromJson<PmxModelSourceSnapshot>(json)
                ?? new PmxModelSourceSnapshot();
            snapshot.geometry = geometry;
            MmdModelDefinition model = BuildModelDefinition(snapshot);
            model.sourceBytes = bytes;
            return model;
        }

        public MmdMotionDefinition LoadMotion(ReadOnlySpan<byte> data)
        {
            MmdParserInput.RequireNonEmpty(data, nameof(data));
            byte[] bytes = data.ToArray();
            using var context = MmdRuntimeFfiVmdContext.Create(bytes);
            MmdVmdParseSummary summary = MmdVmdNativeSummaryAdapter.Read(context);
            return MmdNativeMotionReadbackConverter.BuildRaw(
                summary,
                context.GetRawBoneKeyframes(),
                context.GetRawMorphKeyframes(),
                context.GetCameraKeyframes(),
                context.GetLightKeyframes(),
                context.GetSelfShadowKeyframes(),
                context.GetPropertyKeyframes(),
                context.GetPropertyIkEntries(),
                bytes);
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
