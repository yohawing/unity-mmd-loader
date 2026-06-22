#nullable enable

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Mmd.Native
{
    internal static class MmdRuntimeFfiMethods
    {
        internal const string LibraryName = "mmd_runtime_ffi";
        internal const uint ExpectedAbiVersion = 1;

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_abi_version", CallingConvention = CallingConvention.Cdecl)]
        internal static extern uint AbiVersion();

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_model_create_from_pmx_bytes", CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr ModelCreateFromPmxBytes(byte[] data, IntPtr len);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_model_bone_count", CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr ModelBoneCount(IntPtr model);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_model_morph_count", CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr ModelMorphCount(IntPtr model);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_model_ik_count", CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr ModelIkCount(IntPtr model);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_model_free", CallingConvention = CallingConvention.Cdecl)]
        internal static extern void ModelFree(IntPtr model);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_clip_create_from_vmd_bytes_for_model", CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr ClipCreateFromVmdBytesForModel(IntPtr model, byte[] data, IntPtr len);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_clip_frame_range", CallingConvention = CallingConvention.Cdecl)]
        internal static extern byte ClipFrameRange(IntPtr clip, out uint firstFrame, out uint lastFrame);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_clip_free", CallingConvention = CallingConvention.Cdecl)]
        internal static extern void ClipFree(IntPtr clip);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_instance_create_for_model", CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr InstanceCreateForModel(IntPtr model);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_instance_free", CallingConvention = CallingConvention.Cdecl)]
        internal static extern void InstanceFree(IntPtr instance);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_instance_evaluate_clip_frame", CallingConvention = CallingConvention.Cdecl)]
        internal static extern byte InstanceEvaluateClipFrame(IntPtr instance, IntPtr clip, float frame);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_instance_evaluate_clip_frame_without_ik", CallingConvention = CallingConvention.Cdecl)]
        internal static extern byte InstanceEvaluateClipFrameWithoutIk(IntPtr instance, IntPtr clip, float frame);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_instance_world_matrix_f32_len", CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr InstanceWorldMatrixF32Len(IntPtr instance);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_instance_copy_world_matrices", CallingConvention = CallingConvention.Cdecl)]
        internal static extern byte InstanceCopyWorldMatrices(IntPtr instance, [Out] float[] outF32, IntPtr outF32Len);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_instance_morph_weight_len", CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr InstanceMorphWeightLen(IntPtr instance);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_instance_copy_morph_weights", CallingConvention = CallingConvention.Cdecl)]
        internal static extern byte InstanceCopyMorphWeights(IntPtr instance, [Out] float[] outF32, IntPtr outF32Len);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_instance_ik_enabled_len", CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr InstanceIkEnabledLen(IntPtr instance);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_instance_copy_ik_enabled", CallingConvention = CallingConvention.Cdecl)]
        internal static extern byte InstanceCopyIkEnabled(IntPtr instance, [Out] byte[] outU8, IntPtr outU8Len);

        internal static uint ValidateAbiVersion()
        {
            uint abiVersion = AbiVersion();
            if (abiVersion != ExpectedAbiVersion)
            {
                throw new InvalidOperationException(
                    $"mmd-runtime ABI version {abiVersion} is not supported. Expected {ExpectedAbiVersion}.");
            }

            return abiVersion;
        }

    }

    internal sealed class MmdRuntimeFfiPlaybackSession : IDisposable
    {
        private readonly IntPtr model;
        private readonly IntPtr clip;
        private readonly IntPtr instance;
        private bool disposed;

        private MmdRuntimeFfiPlaybackSession(IntPtr model, IntPtr clip, IntPtr instance)
        {
            this.model = model;
            this.clip = clip;
            this.instance = instance;
            AbiVersion = MmdRuntimeFfiMethods.ExpectedAbiVersion;
            BoneCount = MmdRuntimeFfiSmoke.CheckedIntPtrToInt(MmdRuntimeFfiMethods.ModelBoneCount(model), "bone count");
            MorphCount = MmdRuntimeFfiSmoke.CheckedIntPtrToInt(MmdRuntimeFfiMethods.ModelMorphCount(model), "morph count");
            IkCount = MmdRuntimeFfiSmoke.CheckedIntPtrToInt(MmdRuntimeFfiMethods.ModelIkCount(model), "IK count");
            WorldMatrixFloatCount = MmdRuntimeFfiSmoke.CheckedIntPtrToInt(MmdRuntimeFfiMethods.InstanceWorldMatrixF32Len(instance), "world matrix float count");
            MorphWeightCount = MmdRuntimeFfiSmoke.CheckedIntPtrToInt(MmdRuntimeFfiMethods.InstanceMorphWeightLen(instance), "morph weight count");
            IkEnabledCount = MmdRuntimeFfiSmoke.CheckedIntPtrToInt(MmdRuntimeFfiMethods.InstanceIkEnabledLen(instance), "IK enabled count");
        }

        public uint AbiVersion { get; }
        public int BoneCount { get; }
        public int MorphCount { get; }
        public int IkCount { get; }
        public int WorldMatrixFloatCount { get; }
        public int MorphWeightCount { get; }
        public int IkEnabledCount { get; }

        public static MmdRuntimeFfiPlaybackSession Create(byte[] pmxBytes, byte[] vmdBytes)
        {
            if (pmxBytes == null || pmxBytes.Length == 0)
            {
                throw new ArgumentException("PMX bytes are required.", nameof(pmxBytes));
            }

            if (vmdBytes == null || vmdBytes.Length == 0)
            {
                throw new ArgumentException("VMD bytes are required.", nameof(vmdBytes));
            }

            IntPtr model = IntPtr.Zero;
            IntPtr clip = IntPtr.Zero;
            IntPtr instance = IntPtr.Zero;
            try
            {
                MmdRuntimeFfiMethods.ValidateAbiVersion();
                model = MmdRuntimeFfiMethods.ModelCreateFromPmxBytes(pmxBytes, new IntPtr(pmxBytes.Length));
                if (model == IntPtr.Zero)
                {
                    throw new InvalidOperationException("mmd-runtime PMX import returned a null model.");
                }

                clip = MmdRuntimeFfiMethods.ClipCreateFromVmdBytesForModel(model, vmdBytes, new IntPtr(vmdBytes.Length));
                if (clip == IntPtr.Zero)
                {
                    throw new InvalidOperationException("mmd-runtime VMD import returned a null clip.");
                }

                instance = MmdRuntimeFfiMethods.InstanceCreateForModel(model);
                if (instance == IntPtr.Zero)
                {
                    throw new InvalidOperationException("mmd-runtime instance creation returned null.");
                }

                MmdRuntimeFfiPlaybackSession session = new MmdRuntimeFfiPlaybackSession(model, clip, instance);
                model = IntPtr.Zero;
                clip = IntPtr.Zero;
                instance = IntPtr.Zero;
                return session;
            }
            finally
            {
                if (instance != IntPtr.Zero)
                {
                    MmdRuntimeFfiMethods.InstanceFree(instance);
                }

                if (clip != IntPtr.Zero)
                {
                    MmdRuntimeFfiMethods.ClipFree(clip);
                }

                if (model != IntPtr.Zero)
                {
                    MmdRuntimeFfiMethods.ModelFree(model);
                }
            }
        }

        public void EvaluateAndCopy(float frame, float[] worldMatrices, float[] morphWeights, byte[] ikEnabled)
        {
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(MmdRuntimeFfiPlaybackSession));
            }

            MmdRuntimeFfiSmoke.EvaluateAndCopy(instance, clip, frame, worldMatrices, morphWeights, ikEnabled);
        }

        public void EvaluateWithoutIkAndCopy(float frame, float[] worldMatrices, float[] morphWeights, byte[] ikEnabled)
        {
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(MmdRuntimeFfiPlaybackSession));
            }

            MmdRuntimeFfiSmoke.EvaluateWithoutIkAndCopy(instance, clip, frame, worldMatrices, morphWeights, ikEnabled);
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            MmdRuntimeFfiMethods.InstanceFree(instance);
            MmdRuntimeFfiMethods.ClipFree(clip);
            MmdRuntimeFfiMethods.ModelFree(model);
            disposed = true;
        }
    }

    internal static class MmdRuntimeFfiSmoke
    {
        public static MmdRuntimeFfiSmokeReport Evaluate(byte[] pmxBytes, byte[] vmdBytes, float frame)
        {
            if (pmxBytes == null || pmxBytes.Length == 0)
            {
                throw new ArgumentException("PMX bytes are required.", nameof(pmxBytes));
            }

            if (vmdBytes == null || vmdBytes.Length == 0)
            {
                throw new ArgumentException("VMD bytes are required.", nameof(vmdBytes));
            }

            if (float.IsNaN(frame) || float.IsInfinity(frame) || frame < 0.0f)
            {
                throw new ArgumentOutOfRangeException(nameof(frame), "Frame must be a non-negative finite value.");
            }

            IntPtr model = IntPtr.Zero;
            IntPtr clip = IntPtr.Zero;
            IntPtr instance = IntPtr.Zero;
            try
            {
                uint abiVersion = MmdRuntimeFfiMethods.ValidateAbiVersion();
                model = MmdRuntimeFfiMethods.ModelCreateFromPmxBytes(pmxBytes, new IntPtr(pmxBytes.Length));
                if (model == IntPtr.Zero)
                {
                    throw new InvalidOperationException("mmd-runtime PMX import returned a null model.");
                }

                clip = MmdRuntimeFfiMethods.ClipCreateFromVmdBytesForModel(model, vmdBytes, new IntPtr(vmdBytes.Length));
                if (clip == IntPtr.Zero)
                {
                    throw new InvalidOperationException("mmd-runtime VMD import returned a null clip.");
                }

                instance = MmdRuntimeFfiMethods.InstanceCreateForModel(model);
                if (instance == IntPtr.Zero)
                {
                    throw new InvalidOperationException("mmd-runtime instance creation returned null.");
                }

                if (MmdRuntimeFfiMethods.InstanceEvaluateClipFrame(instance, clip, frame) == 0)
                {
                    throw new InvalidOperationException("mmd-runtime clip frame evaluation returned false.");
                }

                int worldMatrixFloatCount = CheckedIntPtrToInt(MmdRuntimeFfiMethods.InstanceWorldMatrixF32Len(instance), "world matrix float count");
                float[] worldMatrices = new float[worldMatrixFloatCount];
                if (worldMatrices.Length > 0 &&
                    MmdRuntimeFfiMethods.InstanceCopyWorldMatrices(instance, worldMatrices, new IntPtr(worldMatrices.Length)) == 0)
                {
                    throw new InvalidOperationException("mmd-runtime world matrix copy returned false.");
                }

                int morphWeightCount = CheckedIntPtrToInt(MmdRuntimeFfiMethods.InstanceMorphWeightLen(instance), "morph weight count");
                float[] morphWeights = new float[morphWeightCount];
                if (morphWeights.Length > 0 &&
                    MmdRuntimeFfiMethods.InstanceCopyMorphWeights(instance, morphWeights, new IntPtr(morphWeights.Length)) == 0)
                {
                    throw new InvalidOperationException("mmd-runtime morph weight copy returned false.");
                }

                int ikEnabledCount = CheckedIntPtrToInt(MmdRuntimeFfiMethods.InstanceIkEnabledLen(instance), "IK enabled count");
                byte[] ikEnabled = new byte[ikEnabledCount];
                if (ikEnabled.Length > 0 &&
                    MmdRuntimeFfiMethods.InstanceCopyIkEnabled(instance, ikEnabled, new IntPtr(ikEnabled.Length)) == 0)
                {
                    throw new InvalidOperationException("mmd-runtime IK enabled copy returned false.");
                }

                uint firstFrame = 0;
                uint lastFrame = 0;
                bool hasClipRange = MmdRuntimeFfiMethods.ClipFrameRange(clip, out firstFrame, out lastFrame) != 0;
                int boneCount = CheckedIntPtrToInt(MmdRuntimeFfiMethods.ModelBoneCount(model), "bone count");

                return new MmdRuntimeFfiSmokeReport
                {
                    available = true,
                    libraryName = MmdRuntimeFfiMethods.LibraryName,
                    abiVersion = abiVersion,
                    frame = frame,
                    boneCount = boneCount,
                    morphCount = CheckedIntPtrToInt(MmdRuntimeFfiMethods.ModelMorphCount(model), "morph count"),
                    ikCount = CheckedIntPtrToInt(MmdRuntimeFfiMethods.ModelIkCount(model), "IK count"),
                    worldMatrixFloatCount = worldMatrixFloatCount,
                    worldMatrixBoneCount = worldMatrixFloatCount / 16,
                    morphWeightCount = morphWeightCount,
                    ikEnabledCount = ikEnabledCount,
                    nonZeroMorphWeightCount = CountNonZero(morphWeights),
                    enabledIkCount = CountEnabled(ikEnabled),
                    worldMatrixChecksum = Checksum(worldMatrices),
                    morphWeightChecksum = Checksum(morphWeights),
                    ikEnabledChecksum = Checksum(ikEnabled),
                    clipRangeAvailable = hasClipRange,
                    clipFirstFrame = hasClipRange ? firstFrame : 0,
                    clipLastFrame = hasClipRange ? lastFrame : 0,
                    unsupportedReason = string.Empty
                };
            }
            finally
            {
                if (instance != IntPtr.Zero)
                {
                    MmdRuntimeFfiMethods.InstanceFree(instance);
                }

                if (clip != IntPtr.Zero)
                {
                    MmdRuntimeFfiMethods.ClipFree(clip);
                }

                if (model != IntPtr.Zero)
                {
                    MmdRuntimeFfiMethods.ModelFree(model);
                }
            }
        }

        public static MmdRuntimeFfiBenchmarkReport Benchmark(byte[] pmxBytes, byte[] vmdBytes, int startFrame, int frameCount, int repetitions)
        {
            if (pmxBytes == null || pmxBytes.Length == 0)
            {
                throw new ArgumentException("PMX bytes are required.", nameof(pmxBytes));
            }

            if (vmdBytes == null || vmdBytes.Length == 0)
            {
                throw new ArgumentException("VMD bytes are required.", nameof(vmdBytes));
            }

            if (startFrame < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(startFrame), "Start frame must be non-negative.");
            }

            if (frameCount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(frameCount), "Frame count must be positive.");
            }

            if (repetitions <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(repetitions), "Repetitions must be positive.");
            }

            IntPtr model = IntPtr.Zero;
            IntPtr clip = IntPtr.Zero;
            IntPtr instance = IntPtr.Zero;
            try
            {
                uint abiVersion = MmdRuntimeFfiMethods.ValidateAbiVersion();
                model = MmdRuntimeFfiMethods.ModelCreateFromPmxBytes(pmxBytes, new IntPtr(pmxBytes.Length));
                if (model == IntPtr.Zero)
                {
                    throw new InvalidOperationException("mmd-runtime PMX import returned a null model.");
                }

                clip = MmdRuntimeFfiMethods.ClipCreateFromVmdBytesForModel(model, vmdBytes, new IntPtr(vmdBytes.Length));
                if (clip == IntPtr.Zero)
                {
                    throw new InvalidOperationException("mmd-runtime VMD import returned a null clip.");
                }

                instance = MmdRuntimeFfiMethods.InstanceCreateForModel(model);
                if (instance == IntPtr.Zero)
                {
                    throw new InvalidOperationException("mmd-runtime instance creation returned null.");
                }

                int worldMatrixFloatCount = CheckedIntPtrToInt(MmdRuntimeFfiMethods.InstanceWorldMatrixF32Len(instance), "world matrix float count");
                int morphWeightCount = CheckedIntPtrToInt(MmdRuntimeFfiMethods.InstanceMorphWeightLen(instance), "morph weight count");
                int ikEnabledCount = CheckedIntPtrToInt(MmdRuntimeFfiMethods.InstanceIkEnabledLen(instance), "IK enabled count");
                float[] worldMatrices = new float[worldMatrixFloatCount];
                float[] morphWeights = new float[morphWeightCount];
                byte[] ikEnabled = new byte[ikEnabledCount];

                EvaluateAndCopy(instance, clip, startFrame, worldMatrices, morphWeights, ikEnabled);
                GC.Collect();

                long totalFrames = (long)frameCount * repetitions;
                Stopwatch stopwatch = Stopwatch.StartNew();
                for (int repetition = 0; repetition < repetitions; repetition++)
                {
                    for (int frameOffset = 0; frameOffset < frameCount; frameOffset++)
                    {
                        EvaluateAndCopy(instance, clip, startFrame + frameOffset, worldMatrices, morphWeights, ikEnabled);
                    }
                }

                stopwatch.Stop();
                double elapsedMs = stopwatch.Elapsed.TotalMilliseconds;
                uint firstFrame = 0;
                uint lastFrame = 0;
                bool hasClipRange = MmdRuntimeFfiMethods.ClipFrameRange(clip, out firstFrame, out lastFrame) != 0;

                return new MmdRuntimeFfiBenchmarkReport
                {
                    available = true,
                    libraryName = MmdRuntimeFfiMethods.LibraryName,
                    abiVersion = abiVersion,
                    startFrame = startFrame,
                    frameCount = frameCount,
                    repetitions = repetitions,
                    measuredFrames = totalFrames,
                    elapsedMs = elapsedMs,
                    measuredFps = elapsedMs > 0.0 ? totalFrames * 1000.0 / elapsedMs : 0.0,
                    averageFrameMs = elapsedMs / totalFrames,
                    boneCount = CheckedIntPtrToInt(MmdRuntimeFfiMethods.ModelBoneCount(model), "bone count"),
                    morphCount = CheckedIntPtrToInt(MmdRuntimeFfiMethods.ModelMorphCount(model), "morph count"),
                    ikCount = CheckedIntPtrToInt(MmdRuntimeFfiMethods.ModelIkCount(model), "IK count"),
                    worldMatrixFloatCount = worldMatrixFloatCount,
                    worldMatrixBoneCount = worldMatrixFloatCount / 16,
                    morphWeightCount = morphWeightCount,
                    ikEnabledCount = ikEnabledCount,
                    nonZeroMorphWeightCount = CountNonZero(morphWeights),
                    enabledIkCount = CountEnabled(ikEnabled),
                    worldMatrixChecksum = Checksum(worldMatrices),
                    morphWeightChecksum = Checksum(morphWeights),
                    ikEnabledChecksum = Checksum(ikEnabled),
                    clipRangeAvailable = hasClipRange,
                    clipFirstFrame = hasClipRange ? firstFrame : 0,
                    clipLastFrame = hasClipRange ? lastFrame : 0,
                    unsupportedReason = string.Empty
                };
            }
            finally
            {
                if (instance != IntPtr.Zero)
                {
                    MmdRuntimeFfiMethods.InstanceFree(instance);
                }

                if (clip != IntPtr.Zero)
                {
                    MmdRuntimeFfiMethods.ClipFree(clip);
                }

                if (model != IntPtr.Zero)
                {
                    MmdRuntimeFfiMethods.ModelFree(model);
                }
            }
        }

        public static MmdRuntimeFfiSmokeReport Unavailable(Exception exception)
        {
            return new MmdRuntimeFfiSmokeReport
            {
                available = false,
                libraryName = MmdRuntimeFfiMethods.LibraryName,
                unsupportedReason = exception.GetType().Name + ": " + exception.Message
            };
        }

        public static MmdRuntimeFfiBenchmarkReport BenchmarkUnavailable(Exception exception)
        {
            return new MmdRuntimeFfiBenchmarkReport
            {
                available = false,
                libraryName = MmdRuntimeFfiMethods.LibraryName,
                unsupportedReason = exception.GetType().Name + ": " + exception.Message
            };
        }

        internal static void EvaluateAndCopy(
            IntPtr instance,
            IntPtr clip,
            float frame,
            float[] worldMatrices,
            float[] morphWeights,
            byte[] ikEnabled)
        {
            if (MmdRuntimeFfiMethods.InstanceEvaluateClipFrame(instance, clip, frame) == 0)
            {
                throw new InvalidOperationException("mmd-runtime clip frame evaluation returned false.");
            }

            if (worldMatrices.Length > 0 &&
                MmdRuntimeFfiMethods.InstanceCopyWorldMatrices(instance, worldMatrices, new IntPtr(worldMatrices.Length)) == 0)
            {
                throw new InvalidOperationException("mmd-runtime world matrix copy returned false.");
            }

            if (morphWeights.Length > 0 &&
                MmdRuntimeFfiMethods.InstanceCopyMorphWeights(instance, morphWeights, new IntPtr(morphWeights.Length)) == 0)
            {
                throw new InvalidOperationException("mmd-runtime morph weight copy returned false.");
            }

            if (ikEnabled.Length > 0 &&
                MmdRuntimeFfiMethods.InstanceCopyIkEnabled(instance, ikEnabled, new IntPtr(ikEnabled.Length)) == 0)
            {
                throw new InvalidOperationException("mmd-runtime IK enabled copy returned false.");
            }
        }

        internal static void EvaluateWithoutIkAndCopy(
            IntPtr instance,
            IntPtr clip,
            float frame,
            float[] worldMatrices,
            float[] morphWeights,
            byte[] ikEnabled)
        {
            if (MmdRuntimeFfiMethods.InstanceEvaluateClipFrameWithoutIk(instance, clip, frame) == 0)
            {
                throw new InvalidOperationException("mmd-runtime clip frame without IK evaluation returned false.");
            }

            if (worldMatrices.Length > 0 &&
                MmdRuntimeFfiMethods.InstanceCopyWorldMatrices(instance, worldMatrices, new IntPtr(worldMatrices.Length)) == 0)
            {
                throw new InvalidOperationException("mmd-runtime world matrix copy returned false.");
            }

            if (morphWeights.Length > 0 &&
                MmdRuntimeFfiMethods.InstanceCopyMorphWeights(instance, morphWeights, new IntPtr(morphWeights.Length)) == 0)
            {
                throw new InvalidOperationException("mmd-runtime morph weight copy returned false.");
            }

            if (ikEnabled.Length > 0 &&
                MmdRuntimeFfiMethods.InstanceCopyIkEnabled(instance, ikEnabled, new IntPtr(ikEnabled.Length)) == 0)
            {
                throw new InvalidOperationException("mmd-runtime IK enabled copy returned false.");
            }
        }

        internal static int CheckedIntPtrToInt(IntPtr value, string label)
        {
            long raw = value.ToInt64();
            if (raw < 0 || raw > int.MaxValue)
            {
                throw new InvalidOperationException($"mmd-runtime {label} is out of range: {raw}");
            }

            return (int)raw;
        }

        private static int CountNonZero(float[] values)
        {
            int count = 0;
            for (int i = 0; i < values.Length; i++)
            {
                if (values[i] != 0.0f)
                {
                    count++;
                }
            }

            return count;
        }

        private static int CountEnabled(byte[] values)
        {
            int count = 0;
            for (int i = 0; i < values.Length; i++)
            {
                if (values[i] != 0)
                {
                    count++;
                }
            }

            return count;
        }

        private static string Checksum(float[] values)
        {
            unchecked
            {
                uint hash = 2166136261u;
                for (int i = 0; i < values.Length; i++)
                {
                    byte[] bytes = BitConverter.GetBytes(values[i]);
                    for (int byteIndex = 0; byteIndex < bytes.Length; byteIndex++)
                    {
                        hash = (hash ^ bytes[byteIndex]) * 16777619u;
                    }
                }

                return hash.ToString("x8", System.Globalization.CultureInfo.InvariantCulture);
            }
        }

        private static string Checksum(byte[] values)
        {
            unchecked
            {
                uint hash = 2166136261u;
                for (int i = 0; i < values.Length; i++)
                {
                    hash = (hash ^ values[i]) * 16777619u;
                }

                return hash.ToString("x8", System.Globalization.CultureInfo.InvariantCulture);
            }
        }
    }

    [Serializable]
    internal sealed class MmdRuntimeFfiSmokeReport
    {
        public bool available;
        public string libraryName = string.Empty;
        public uint abiVersion;
        public float frame;
        public int boneCount;
        public int morphCount;
        public int ikCount;
        public int worldMatrixFloatCount;
        public int worldMatrixBoneCount;
        public int morphWeightCount;
        public int ikEnabledCount;
        public int nonZeroMorphWeightCount;
        public int enabledIkCount;
        public string worldMatrixChecksum = string.Empty;
        public string morphWeightChecksum = string.Empty;
        public string ikEnabledChecksum = string.Empty;
        public bool clipRangeAvailable;
        public uint clipFirstFrame;
        public uint clipLastFrame;
        public string unsupportedReason = string.Empty;
    }

    [Serializable]
    internal sealed class MmdRuntimeFfiBenchmarkReport
    {
        public bool available;
        public string libraryName = string.Empty;
        public uint abiVersion;
        public int startFrame;
        public int frameCount;
        public int repetitions;
        public long measuredFrames;
        public double elapsedMs;
        public double measuredFps;
        public double averageFrameMs;
        public int boneCount;
        public int morphCount;
        public int ikCount;
        public int worldMatrixFloatCount;
        public int worldMatrixBoneCount;
        public int morphWeightCount;
        public int ikEnabledCount;
        public int nonZeroMorphWeightCount;
        public int enabledIkCount;
        public string worldMatrixChecksum = string.Empty;
        public string morphWeightChecksum = string.Empty;
        public string ikEnabledChecksum = string.Empty;
        public bool clipRangeAvailable;
        public uint clipFirstFrame;
        public uint clipLastFrame;
        public string unsupportedReason = string.Empty;
    }
}

