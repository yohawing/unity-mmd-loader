#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Mmd.Parser;

namespace Mmd
{
    public enum MmdVmdImportSummaryStatus
    {
        NotParsed = 0,
        Passed = 1,
        Failed = 2
    }

    [Serializable]
    public readonly struct MmdVmdParseSummary
    {
        public MmdVmdParseSummary(
            string targetModelName,
            int maxFrame,
            int boneKeyframeCount,
            int morphKeyframeCount,
            int modelKeyframeCount,
            int constraintStateCount,
            int cameraKeyframeCount = 0,
            int lightKeyframeCount = 0,
            int selfShadowKeyframeCount = 0)
        {
            TargetModelName = targetModelName ?? string.Empty;
            MaxFrame = Math.Max(0, maxFrame);
            BoneKeyframeCount = Math.Max(0, boneKeyframeCount);
            MorphKeyframeCount = Math.Max(0, morphKeyframeCount);
            ModelKeyframeCount = Math.Max(0, modelKeyframeCount);
            ConstraintStateCount = Math.Max(0, constraintStateCount);
            CameraKeyframeCount = Math.Max(0, cameraKeyframeCount);
            LightKeyframeCount = Math.Max(0, lightKeyframeCount);
            SelfShadowKeyframeCount = Math.Max(0, selfShadowKeyframeCount);
        }

        public string TargetModelName { get; }

        public int MaxFrame { get; }

        public int BoneKeyframeCount { get; }

        public int MorphKeyframeCount { get; }

        public int ModelKeyframeCount { get; }

        public int ConstraintStateCount { get; }

        public int CameraKeyframeCount { get; }

        public int LightKeyframeCount { get; }

        public int SelfShadowKeyframeCount { get; }
    }

    public sealed class MmdVmdAsset : ScriptableObject
    {
        [SerializeField] private byte[] data = Array.Empty<byte>();
        [SerializeField] private string sourceId = string.Empty;
        [SerializeField] private string sourcePath = string.Empty;

        [SerializeField] private MmdVmdImportSummaryStatus importSummaryStatus = MmdVmdImportSummaryStatus.NotParsed;
        [SerializeField] private string targetModelName = string.Empty;
        [SerializeField] private int maxFrame;
        [SerializeField] private int boneKeyframeCount;
        [SerializeField] private int morphKeyframeCount;
        [SerializeField] private int modelKeyframeCount;
        [SerializeField] private int constraintStateCount;
        [SerializeField] private int cameraKeyframeCount;
        [SerializeField] private int lightKeyframeCount;
        [SerializeField] private int selfShadowKeyframeCount;
        [SerializeField] private string[] structuralDiagnostics = Array.Empty<string>();

        public string SourceId => sourceId;

        public string SourcePath => sourcePath;

        public int ByteLength => data.Length;

        public MmdVmdImportSummaryStatus ImportSummaryStatus => importSummaryStatus;

        public string TargetModelName => targetModelName;

        public int MaxFrame => maxFrame;

        public int BoneKeyframeCount => boneKeyframeCount;

        public int MorphKeyframeCount => morphKeyframeCount;

        public int ModelKeyframeCount => modelKeyframeCount;

        public int ConstraintStateCount => constraintStateCount;

        public int CameraKeyframeCount => cameraKeyframeCount;

        public int LightKeyframeCount => lightKeyframeCount;

        public int SelfShadowKeyframeCount => selfShadowKeyframeCount;

        public IReadOnlyList<string> StructuralDiagnostics => structuralDiagnostics;

        public void Initialize(byte[] bytes, string assetSourceId, string assetSourcePath, MmdVmdParseSummary? vmdParseSummary = null, IReadOnlyList<string>? importDiagnostics = null)
        {
            if (bytes == null || bytes.Length == 0)
            {
                throw new ArgumentException("VMD asset bytes are required.", nameof(bytes));
            }

            data = (byte[])bytes.Clone();
            sourceId = assetSourceId ?? string.Empty;
            sourcePath = assetSourcePath ?? string.Empty;
            ApplyVmdParseSummary(vmdParseSummary, importDiagnostics);
        }

        public byte[] GetBytesCopy()
        {
            return (byte[])data.Clone();
        }

        public MmdMotionDefinition LoadMotion(IMmdParser? parser = null)
        {
            if (data.Length == 0)
            {
                throw new InvalidOperationException("VMD asset has no imported bytes.");
            }

            parser ??= new NativeMmdParser();
            return parser.LoadMotion(data);
        }

        public MmdMotionDefinition CreateNativeClipMotionHeader()
        {
            if (data.Length == 0)
            {
                throw new InvalidOperationException("VMD asset has no imported bytes.");
            }

            return new MmdMotionDefinition
            {
                targetModelName = targetModelName ?? string.Empty,
                maxFrame = Math.Max(0, maxFrame),
                boneKeyframes = new List<MmdBoneKeyframeDefinition>(),
                morphKeyframes = new List<MmdMorphKeyframeDefinition>(),
                modelKeyframes = new List<MmdModelKeyframeDefinition>()
            };
        }

        private void ApplyVmdParseSummary(MmdVmdParseSummary? parseSummary, IReadOnlyList<string>? diagnostics)
        {
            structuralDiagnostics = diagnostics != null ? diagnostics.ToArray() : Array.Empty<string>();

            if (!parseSummary.HasValue)
            {
                importSummaryStatus = MmdVmdImportSummaryStatus.NotParsed;
                targetModelName = string.Empty;
                maxFrame = 0;
                boneKeyframeCount = 0;
                morphKeyframeCount = 0;
                modelKeyframeCount = 0;
                constraintStateCount = 0;
                cameraKeyframeCount = 0;
                lightKeyframeCount = 0;
                selfShadowKeyframeCount = 0;
                return;
            }

            MmdVmdParseSummary s = parseSummary.Value;
            targetModelName = s.TargetModelName;
            maxFrame = s.MaxFrame;
            boneKeyframeCount = s.BoneKeyframeCount;
            morphKeyframeCount = s.MorphKeyframeCount;
            modelKeyframeCount = s.ModelKeyframeCount;
            constraintStateCount = s.ConstraintStateCount;
            cameraKeyframeCount = s.CameraKeyframeCount;
            lightKeyframeCount = s.LightKeyframeCount;
            selfShadowKeyframeCount = s.SelfShadowKeyframeCount;

            bool isParseFailure = diagnostics != null &&
                diagnostics.Count > 0 &&
                (diagnostics[0].IndexOf("Failed to parse", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 diagnostics[0].IndexOf("Failed to load", StringComparison.OrdinalIgnoreCase) >= 0);

            importSummaryStatus = isParseFailure ? MmdVmdImportSummaryStatus.Failed : MmdVmdImportSummaryStatus.Passed;
        }
    }
}
