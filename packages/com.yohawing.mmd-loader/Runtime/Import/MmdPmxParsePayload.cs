#nullable enable

using Mmd.Parser;
using Mmd;

namespace Mmd.UnityIntegration
{
    /// <summary>
    /// Editor-independent typed result of the PMX byte parse step.
    /// Contains the core MmdModelDefinition and the derived MmdPmxParseSummary.
    /// This moves only the pure parse + summary construction out of MmdPmxScriptedImporter
    /// while leaving all Editor asset import context, texture binding, avatar, and sub-asset
    /// registration in the importer.
    /// </summary>
    public readonly struct MmdPmxParsePayload
    {
        public MmdModelDefinition Model { get; }
        public MmdPmxParseSummary ParseSummary { get; }

        public MmdPmxParsePayload(MmdModelDefinition model, MmdPmxParseSummary parseSummary)
        {
            Model = model ?? throw new System.ArgumentNullException(nameof(model));
            ParseSummary = parseSummary;
        }

        /// <summary>
        /// Validates bytes (null/empty), calls NativeMmdParser().LoadModel(bytes),
        /// computes MmdPmxParseSummary.FromModel(model), and returns the combined
        /// payload. This is the Editor-independent surface for byte-to-(model+summary).
        /// </summary>
        public static MmdPmxParsePayload FromBytes(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
            {
                throw new System.ArgumentException("PMX asset bytes are required.", nameof(bytes));
            }

            MmdModelDefinition model = new NativeMmdParser().LoadModel(bytes);
            MmdPmxParseSummary parseSummary = MmdPmxParseSummary.FromModel(model);
            return new MmdPmxParsePayload(model, parseSummary);
        }
    }
}
