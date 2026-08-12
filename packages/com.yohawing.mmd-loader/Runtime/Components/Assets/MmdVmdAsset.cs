#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using Mmd.Native;
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
        // Kept for serialized assets created before importer version 2. New imported assets
        // store the raw source in the Unity-native TextAsset subasset below instead.
        [SerializeField] private byte[] data = Array.Empty<byte>();
        [SerializeField] private TextAsset? rawSource;
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

        [NonSerialized] private MmdRuntimeFfiVmdContext? nativeVmdContext;
        [NonSerialized] private byte[]? nativeVmdContextSource;
        [NonSerialized] private object? nativeVmdContextGate;
        [NonSerialized] private Task<MmdRuntimeFfiVmdContext>? nativeVmdContextPreloadTask;
        [NonSerialized] private byte[]? nativeVmdContextPreloadSource;
        [NonSerialized] private TextAsset? sourceReadbackAsset;
        [NonSerialized] private byte[]? sourceReadback;

        // Test-only seam for deterministic shared-context failure propagation coverage. Production
        // code leaves this null and uses the real native context creation below.
        internal static Func<byte[], string>? NativeVmdContextFailureReasonOverrideForTests { get; set; }

        public string SourceId => sourceId;

        public string SourcePath => sourcePath;

        public int ByteLength => rawSource != null
            ? checked((int)rawSource.dataSize)
            : data?.Length ?? 0;

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

            InitializeCore(
                (byte[])bytes.Clone(),
                assetSourceId,
                assetSourcePath,
                null,
                vmdParseSummary,
                importDiagnostics);
        }

        /// <summary>
        /// Initializes an imported asset with the importer-owned read buffer.
        /// The importer must not mutate <paramref name="bytes"/> after this call.
        /// </summary>
        internal void InitializeImported(
            byte[] bytes,
            string assetSourceId,
            string assetSourcePath,
            MmdVmdParseSummary? vmdParseSummary = null,
            IReadOnlyList<string>? importDiagnostics = null)
        {
            if (bytes == null || bytes.Length == 0)
            {
                throw new ArgumentException("VMD asset bytes are required.", nameof(bytes));
            }

            InitializeCore(
                bytes,
                assetSourceId,
                assetSourcePath,
                null,
                vmdParseSummary,
                importDiagnostics);
        }

        /// <summary>
        /// Initializes an imported asset backed by a Unity-native raw source subasset. The
        /// importer-owned byte buffer is used only for validation and summary parsing; it is
        /// intentionally not serialized into the MmdVmdAsset.
        /// </summary>
        internal void InitializeImported(
            byte[] bytes,
            string assetSourceId,
            string assetSourcePath,
            TextAsset importedRawSource,
            MmdVmdParseSummary? vmdParseSummary = null,
            IReadOnlyList<string>? importDiagnostics = null)
        {
            if (bytes == null || bytes.Length == 0)
            {
                throw new ArgumentException("VMD asset bytes are required.", nameof(bytes));
            }

            if (importedRawSource == null)
            {
                throw new ArgumentNullException(nameof(importedRawSource));
            }

            if (importedRawSource.dataSize != bytes.LongLength)
            {
                throw new ArgumentException("The imported VMD raw source does not match the source buffer.", nameof(importedRawSource));
            }

            InitializeCore(
                Array.Empty<byte>(),
                assetSourceId,
                assetSourcePath,
                importedRawSource,
                vmdParseSummary,
                importDiagnostics);
        }

        private void InitializeCore(
            byte[] bytes,
            string assetSourceId,
            string assetSourcePath,
            TextAsset? importedRawSource,
            MmdVmdParseSummary? vmdParseSummary,
            IReadOnlyList<string>? importDiagnostics)
        {
            DisposeNativeVmdContext();
            sourceReadbackAsset = null;
            sourceReadback = null;
            data = bytes;
            rawSource = importedRawSource;
            sourceId = assetSourceId ?? string.Empty;
            sourcePath = assetSourcePath ?? string.Empty;
            ApplyVmdParseSummary(vmdParseSummary, importDiagnostics);
        }

        public byte[] GetBytesCopy()
        {
            return (byte[])ReadSourceBytes().Clone();
        }

        public MmdMotionDefinition LoadMotion(IMmdParser? parser = null)
        {
            byte[] sourceBytes = ReadSourceBytes();
            if (sourceBytes.Length == 0)
            {
                throw new InvalidOperationException("VMD asset has no imported bytes.");
            }

            parser ??= new NativeMmdParser();
            return parser.LoadMotion(sourceBytes);
        }

        /// <summary>
        /// Gets one asset-owned native VMD parse for clips created from this source. Native clips
        /// retain independent ownership, while parsed VMD records are shared across bindings.
        /// </summary>
        internal bool TryGetOrCreateNativeVmdContext(
            out MmdRuntimeFfiVmdContext? context,
            out string reason)
        {
            ValidateNativeClipHeaderSource();
            byte[] sourceBytes = ReadSourceBytes();
            reason = string.Empty;
            Task<MmdRuntimeFfiVmdContext> preloadTask;
            lock (NativeVmdContextGate)
            {
                if (nativeVmdContext != null && ReferenceEquals(nativeVmdContextSource, sourceBytes))
                {
                    context = nativeVmdContext;
                    return true;
                }

                preloadTask = GetOrStartNativeVmdContextPreloadLocked(sourceBytes);
            }

            try
            {
                MmdRuntimeFfiVmdContext created = preloadTask.GetAwaiter().GetResult();
                lock (NativeVmdContextGate)
                {
                    if (ReferenceEquals(nativeVmdContextPreloadSource, sourceBytes))
                    {
                        nativeVmdContext = created;
                        nativeVmdContextSource = sourceBytes;
                        context = created;
                        return true;
                    }
                }

                created.Dispose();
                context = null;
                reason = "VMD source changed while its native playback context was being prepared.";
                return false;
            }
            catch (Exception exception) when (
                exception is MmdRuntimeUnsupportedException ||
                exception is MmdRuntimeNativeUnavailableException)
            {
                context = null;
                reason = exception.Message;
                return false;
            }
            catch (NativeVmdContextPreloadException exception)
            {
                context = null;
                reason = exception.Message;
                return false;
            }
        }

        internal Task BeginNativePlaybackPreload()
        {
            ValidateNativeClipHeaderSource();
            byte[] sourceBytes = ReadSourceBytes();
            lock (NativeVmdContextGate)
            {
                if (nativeVmdContext != null && ReferenceEquals(nativeVmdContextSource, sourceBytes))
                {
                    return Task.CompletedTask;
                }

                return GetOrStartNativeVmdContextPreloadLocked(sourceBytes);
            }
        }

        private object NativeVmdContextGate => nativeVmdContextGate ??= new object();

        private Task<MmdRuntimeFfiVmdContext> GetOrStartNativeVmdContextPreloadLocked(byte[] sourceBytes)
        {
            if (ReferenceEquals(nativeVmdContextPreloadSource, sourceBytes) &&
                nativeVmdContextPreloadTask != null)
            {
                return nativeVmdContextPreloadTask;
            }

            Func<byte[], string>? failureOverrideForTests = NativeVmdContextFailureReasonOverrideForTests;
            nativeVmdContextPreloadSource = sourceBytes;
            nativeVmdContextPreloadTask = Task.Run(() =>
            {
                if (failureOverrideForTests != null)
                {
                    throw new NativeVmdContextPreloadException(failureOverrideForTests(sourceBytes));
                }

                return MmdRuntimeFfiVmdContext.Create(sourceBytes);
            });
            _ = nativeVmdContextPreloadTask.ContinueWith(
                completed => _ = completed.Exception,
                TaskContinuationOptions.OnlyOnFaulted);
            return nativeVmdContextPreloadTask;
        }

        private sealed class NativeVmdContextPreloadException : Exception
        {
            internal NativeVmdContextPreloadException(string message)
                : base(message)
            {
            }
        }

        public MmdMotionDefinition CreateNativeClipMotionHeader()
        {
            ValidateNativeClipHeaderSource();

            MmdVmdParseSummary summary = GetNativeClipSummary();
            byte[] sourceBytes = ReadSourceBytes();
            return CreateNativeClipMotionHeaderCore(sourceBytes, summary, rawSource != null);
        }

        public static MmdMotionDefinition CreateNativeClipMotionHeader(
            byte[] vmdBytes,
            MmdVmdParseSummary summary)
        {
            if (vmdBytes == null || vmdBytes.Length == 0)
            {
                throw new ArgumentException("VMD bytes are required.", nameof(vmdBytes));
            }

            return CreateNativeClipMotionHeaderCore(vmdBytes, summary, shareSourceBytes: false);
        }

        private static MmdMotionDefinition CreateNativeClipMotionHeaderCore(
            byte[] vmdBytes,
            MmdVmdParseSummary summary,
            bool shareSourceBytes)
        {
            return new MmdMotionDefinition
            {
                targetModelName = summary.TargetModelName,
                maxFrame = summary.MaxFrame,
                boneKeyframes = new List<MmdBoneKeyframeDefinition>(),
                morphKeyframes = new List<MmdMorphKeyframeDefinition>(),
                modelKeyframes = new List<MmdModelKeyframeDefinition>(),
                cameraKeyframeCount = summary.CameraKeyframeCount,
                lightKeyframeCount = summary.LightKeyframeCount,
                selfShadowKeyframeCount = summary.SelfShadowKeyframeCount,
                // Imported raw-source headers share the cached asset-owned buffer to avoid
                // cloning the full VMD during the first Timeline evaluation.
                sourceBytes = shareSourceBytes ? vmdBytes : (byte[])vmdBytes.Clone()
            };
        }

        private MmdVmdParseSummary GetNativeClipSummary()
        {
            return importSummaryStatus == MmdVmdImportSummaryStatus.NotParsed
                ? MmdVmdNativeSummaryAdapter.Read(ReadSourceBytes())
                : new MmdVmdParseSummary(
                    targetModelName,
                    maxFrame,
                    boneKeyframeCount,
                    morphKeyframeCount,
                    modelKeyframeCount,
                    constraintStateCount,
                    cameraKeyframeCount,
                    lightKeyframeCount,
                    selfShadowKeyframeCount);
        }

        private void ValidateNativeClipHeaderSource()
        {
            if (ByteLength == 0)
            {
                throw new InvalidOperationException("VMD asset has no imported bytes.");
            }

            if (structuralDiagnostics != null && structuralDiagnostics.Length > 0)
            {
                throw new InvalidOperationException(structuralDiagnostics[0]);
            }

            if (importSummaryStatus == MmdVmdImportSummaryStatus.Failed)
            {
                throw new InvalidOperationException("VMD import summary is marked as failed.");
            }
        }

        private byte[] ReadSourceBytes()
        {
            if (rawSource == null)
            {
                return data ?? Array.Empty<byte>();
            }

            if (!ReferenceEquals(sourceReadbackAsset, rawSource) || sourceReadback == null)
            {
                if (sourceReadbackAsset != null && !ReferenceEquals(sourceReadbackAsset, rawSource))
                {
                    DisposeNativeVmdContext();
                }

                sourceReadbackAsset = rawSource;
                sourceReadback = rawSource.bytes;
            }

            return sourceReadback;
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

        private void OnDisable()
        {
            DisposeNativeVmdContext();
        }

        private void DisposeNativeVmdContext()
        {
            lock (NativeVmdContextGate)
            {
                MmdRuntimeFfiVmdContext? context = nativeVmdContext;
                if (context == null && nativeVmdContextPreloadTask != null)
                {
                    try
                    {
                        context = nativeVmdContextPreloadTask.GetAwaiter().GetResult();
                    }
                    catch
                    {
                        // A failed creation owns no native handle. Its diagnostic was already
                        // observed by the preload continuation or the playback caller.
                        nativeVmdContextPreloadTask = null;
                        nativeVmdContextPreloadSource = null;
                        return;
                    }
                }

                // Dispose deliberately happens before clearing the references. If native cleanup
                // is unavailable, the context retains its handle and a later lifecycle call can retry.
                context?.Dispose();
                nativeVmdContext = null;
                nativeVmdContextSource = null;
                nativeVmdContextPreloadTask = null;
                nativeVmdContextPreloadSource = null;
            }
        }
    }
}
