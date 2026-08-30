#nullable enable

using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using Mmd.Motion;
using Mmd.Native;
using Mmd.Parser;
using Mmd.Physics;
using UnityEngine;

namespace Mmd.UnityIntegration
{
    public sealed partial class MmdUnityPlaybackBinding
    {
        // Keep only digests for the bytes that built the active native session. The
        // caller-owned source arrays must not be retained: Timeline worker creation can
        // compare the provider source without making the binding a second source owner.
        private byte[]? fastRuntimePmxSourceDigest;
        private byte[]? fastRuntimeVmdSourceDigest;
        private long fastRuntimeSourceRevision;

        /// <summary>
        /// Opt-in fast runtime using the native mmd-runtime FFI library.
        /// Affects <see cref="ApplyFrame"/> in animation-only playback and the animation pose stage of Live physics playback.
        /// Returns true and clears <paramref name="reason"/> on success.
        /// Returns false with a diagnostic message in <paramref name="reason"/> when the native library is absent,
        /// ABI-incompatible, or the bone/morph counts do not match the managed model.
        /// </summary>
        public bool TryEnableFastRuntime(
            byte[] pmxBytes,
            byte[] vmdBytes,
            out string reason,
            bool abiAlreadyValidated = false)
        {
            return TryEnableFastRuntimeCore(
                pmxBytes,
                vmdBytes,
                sharedVmdContext: null,
                out reason,
                abiAlreadyValidated);
        }

        internal bool TryEnableFastRuntimeWithSharedVmdContext(
            byte[] pmxBytes,
            byte[] vmdBytes,
            MmdRuntimeFfiVmdContext sharedVmdContext,
            out string reason,
            bool abiAlreadyValidated = false)
        {
            if (sharedVmdContext == null)
            {
                throw new ArgumentNullException(nameof(sharedVmdContext));
            }

            return TryEnableFastRuntimeCore(
                pmxBytes,
                vmdBytes,
                sharedVmdContext,
                out reason,
                abiAlreadyValidated);
        }

        private bool TryEnableFastRuntimeCore(
            byte[] pmxBytes,
            byte[] vmdBytes,
            MmdRuntimeFfiVmdContext? sharedVmdContext,
            out string reason,
            bool abiAlreadyValidated)
        {
            if (pmxBytes == null || pmxBytes.Length == 0)
            {
                throw new ArgumentException("PMX bytes are required.", nameof(pmxBytes));
            }

            if (vmdBytes == null || vmdBytes.Length == 0)
            {
                throw new ArgumentException("VMD bytes are required.", nameof(vmdBytes));
            }

            // A failed replacement must not leave the old session active for a new model/motion pair.
            DisposeFastRuntime();
            MmdRuntimeFfiPlaybackSession? candidate = null;
            reason = string.Empty;
            try
            {
                MmdRuntimeFfiPlaybackSession created = sharedVmdContext == null
                    ? MmdRuntimeFfiPlaybackSession.Create(pmxBytes, vmdBytes, abiAlreadyValidated)
                    : MmdRuntimeFfiPlaybackSession.CreateFromVmdContext(
                        pmxBytes,
                        sharedVmdContext,
                        abiAlreadyValidated);
                candidate = created;
                created.SetIkCompatibilityProfile(ikCompatibilityProfile);
                int candidateBoneCount = created.BoneCount;
                int candidateMorphCount = created.MorphCount;
                if (candidateBoneCount != model.bones.Count)
                {
                    reason = $"mmd-runtime bone count {candidateBoneCount} does not match managed model bone count {model.bones.Count}.";
                    return false;
                }

                if (candidateMorphCount != model.morphs.Count)
                {
                    reason = $"mmd-runtime morph count {candidateMorphCount} does not match managed model morph count {model.morphs.Count}.";
                    return false;
                }

                int expectedWorldMatrixFloatCount = model.bones.Count * 16;
                if (created.WorldMatrixFloatCount < expectedWorldMatrixFloatCount)
                {
                    reason = $"mmd-runtime world matrix float count {created.WorldMatrixFloatCount} is smaller than required {expectedWorldMatrixFloatCount}.";
                    return false;
                }

                if (created.MorphWeightCount != model.morphs.Count)
                {
                    reason = $"mmd-runtime morph weight count {created.MorphWeightCount} does not match managed model morph count {model.morphs.Count}.";
                    return false;
                }

                if (created.IkEnabledCount != model.ik.Count)
                {
                    reason = $"mmd-runtime IK enabled count {created.IkEnabledCount} does not match managed model IK count {model.ik.Count}.";
                    return false;
                }

                float[] worldMatrices = new float[created.WorldMatrixFloatCount];
                float[] morphWeights = new float[created.MorphWeightCount];
                byte[] ikEnabled = new byte[created.IkEnabledCount];
                float[] lastAppliedMorphWeights = new float[fastMorphIndices.Length];
                MmdEvaluatedFrame? morphFrame = BuildFastMorphFrame(morphWeights);
                byte[] pmxSourceDigest = ComputeFastRuntimeSourceDigest(pmxBytes);
                byte[] vmdSourceDigest = ComputeFastRuntimeSourceDigest(vmdBytes);

                fastSession = created;
                fastWorldMatrices = worldMatrices;
                fastMorphWeights = morphWeights;
                fastIkEnabled = ikEnabled;
                fastRuntimePmxSourceDigest = pmxSourceDigest;
                fastRuntimeVmdSourceDigest = vmdSourceDigest;
                fastLastAppliedMorphWeights = lastAppliedMorphWeights;
                fastMorphFrame = morphFrame;
                fastMorphApplied = false;
                fastMorphCacheValid = false;
                candidate = null;
                reason = string.Empty;
                return true;
            }
            catch (DllNotFoundException ex)
            {
                reason = ex.GetType().Name + ": " + ex.Message;
                return false;
            }
            catch (EntryPointNotFoundException ex)
            {
                reason = ex.GetType().Name + ": " + ex.Message;
                return false;
            }
            catch (BadImageFormatException ex)
            {
                reason = ex.GetType().Name + ": " + ex.Message;
                return false;
            }
            catch (MmdRuntimeNativeUnavailableException ex)
            {
                reason = ex.GetType().Name + ": " + ex.Message;
                return false;
            }
            catch (InvalidOperationException ex)
            {
                reason = ex.GetType().Name + ": " + ex.Message;
                return false;
            }
            finally
            {
                if (candidate != null)
                {
                    try
                    {
                        candidate.Dispose();
                    }
                    catch (Exception)
                    {
                        reason = string.IsNullOrEmpty(reason)
                            ? "native runtime cleanup failed while releasing a candidate session."
                            : reason + " Native runtime cleanup also failed while releasing the candidate session.";
                    }
                }
            }
        }

        public void DisableFastRuntime()
        {
            DisposeFastRuntime();
        }

        internal bool HasFastRuntimeBatch => fastSession != null;

        internal long FastRuntimeSourceRevision => fastRuntimeSourceRevision;

        /// <summary>
        /// Confirms that the active native session was created from the exact provider bytes.
        /// This is intentionally internal: arbitrary public fast-runtime replacement remains
        /// supported, while Timeline worker reuse must fail closed for a different source.
        /// </summary>
        internal bool TryMatchFastRuntimeSources(
            byte[] pmxBytes,
            byte[] vmdBytes,
            out string reason)
        {
            if (pmxBytes == null || pmxBytes.Length == 0)
            {
                reason = "Provider PMX source bytes are empty.";
                return false;
            }

            if (vmdBytes == null || vmdBytes.Length == 0)
            {
                reason = "Provider VMD source bytes are empty.";
                return false;
            }

            if (fastSession == null ||
                fastRuntimePmxSourceDigest == null ||
                fastRuntimeVmdSourceDigest == null)
            {
                reason = "The active fast-runtime binding has no tracked source identity.";
                return false;
            }

            if (!FastRuntimeSourceDigestEquals(fastRuntimePmxSourceDigest, pmxBytes))
            {
                reason = "The active fast-runtime PMX source differs from the provider source.";
                return false;
            }

            if (!FastRuntimeSourceDigestEquals(fastRuntimeVmdSourceDigest, vmdBytes))
            {
                reason = "The active fast-runtime VMD source differs from the provider source.";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        internal int FastRuntimeWorldMatrixFloatCount => fastSession?.WorldMatrixFloatCount ?? 0;

        internal int FastRuntimeMorphWeightCount => fastSession?.MorphWeightCount ?? 0;

        internal void EvaluateFastRuntimeBatch(
            float startFrame,
            float frameStep,
            int frameCount,
            uint workerCount,
            float[] worldMatrices,
            float[] morphWeights)
        {
            if (fastSession == null)
            {
                throw new InvalidOperationException("mmd-runtime fast playback session is not enabled.");
            }

            fastSession.EvaluateBatch(
                startFrame,
                frameStep,
                frameCount,
                workerCount,
                worldMatrices,
                morphWeights);
        }

        internal bool TryValidatePreparedFastFrame(
            float[] worldMatrices,
            float[] morphWeights,
            out string reason)
        {
            try
            {
                ValidatePreparedFastFrame(worldMatrices, morphWeights);
            }
            catch (Exception exception)
            {
                reason = exception.GetType().Name + ": " + exception.Message;
                return false;
            }

            reason = string.Empty;
            return true;
        }

        private void DisposeFastRuntime()
        {
            fastRuntimeSourceRevision++;
            fastRuntimePmxSourceDigest = null;
            fastRuntimeVmdSourceDigest = null;
            if (physicsMode == MmdPhysicsMode.Live && !nativeHumanoidHostPoseEnabled)
            {
                ResetLivePhysicsState();
            }
            fastSession?.Dispose();
            fastSession = null;
            fastWorldMatrices = null;
            fastMorphWeights = null;
            fastIkEnabled = null;
            fastMorphFrame = null;
            fastLastAppliedMorphWeights = null;
            fastLivePhysicsFrame = null;
            fastMorphApplied = false;
            fastMorphCacheValid = false;
            fastSnapshot = null;
        }

        private static byte[] ComputeFastRuntimeSourceDigest(byte[] sourceBytes)
        {
            using SHA256 sha256 = SHA256.Create();
            return sha256.ComputeHash(sourceBytes);
        }

        private static bool FastRuntimeSourceDigestEquals(byte[] expectedDigest, byte[] sourceBytes)
        {
            byte[] actualDigest = ComputeFastRuntimeSourceDigest(sourceBytes);
            if (expectedDigest.Length != actualDigest.Length)
            {
                return false;
            }

            for (int index = 0; index < expectedDigest.Length; index++)
            {
                if (expectedDigest[index] != actualDigest[index])
                {
                    return false;
                }
            }

            return true;
        }

        private void InvalidateFastMorphCache()
        {
            if (fastSession == null)
            {
                return;
            }

            fastMorphApplied = false;
            fastMorphCacheValid = false;
        }

        private MmdPlaybackSnapshot ApplyFastFrame(int frame, float frameRate)
        {
            float time = MmdPlaybackTime.ToTime(frame, frameRate);
            return ApplyFastCore(frame, time, frame);
        }

        private MmdPlaybackSnapshot ApplyFastTime(float time, float frameRate)
        {
            int frame = MmdPlaybackTime.ToFrame(time, frameRate);
            return ApplyFastCore(frame, time, time * frameRate);
        }

        private MmdPlaybackSnapshot ApplyFastCore(
            int frame,
            float time,
            float evaluationFrame)
        {
            MmdUnityFrameApplier.ValidateSupportedMorphPlayback(playbackInstance);
            fastSession!.EvaluateAndCopy(
                evaluationFrame,
                fastWorldMatrices!,
                fastMorphWeights!,
                fastIkEnabled!,
                (uint)ikMaxIterationsCap);
            return ApplyPreparedFastCore(frame, time, fastWorldMatrices!, fastMorphWeights!);
        }

        /// <summary>
        /// Applies a frame evaluated by a worker-owned native session. The caller must be on
        /// Unity's main thread; this method only applies managed Unity state and never evaluates
        /// the binding's native session.
        /// </summary>
        internal MmdPlaybackSnapshot ApplyPreparedFastFrame(
            int frame,
            float frameRate,
            float[] worldMatrices,
            float[] morphWeights)
        {
            return ApplyPreparedFastFrame(
                frame,
                frameRate,
                MmdPlaybackTime.ToTime(frame, frameRate),
                worldMatrices,
                morphWeights);
        }

        internal MmdPlaybackSnapshot ApplyPreparedFastFrame(
            int frame,
            float frameRate,
            float sourceTime,
            float[] worldMatrices,
            float[] morphWeights)
        {
            MmdPlaybackTime.ValidateFrame(frame);
            MmdPlaybackTime.ValidateFrameRate(frameRate);
            MmdPlaybackTime.ValidateTime(sourceTime);
            EnsureBorrowedMutationActive();
            ValidatePreparedFastFrame(worldMatrices, morphWeights);

            return ApplyPreparedFastCore(frame, sourceTime, worldMatrices, morphWeights);
        }

        private void ValidatePreparedFastFrame(
            float[] worldMatrices,
            float[] morphWeights)
        {
            if (physicsMode != MmdPhysicsMode.Off)
            {
                throw new InvalidOperationException(
                    "Prepared multi-character frames require Physics Mode Off.");
            }

            if (fastSession == null)
            {
                throw new InvalidOperationException(
                    "Prepared multi-character frames require an enabled fast runtime binding.");
            }

            if (worldMatrices == null || worldMatrices.Length < fastSession.WorldMatrixFloatCount)
            {
                throw new ArgumentException(
                    "Prepared world matrix buffer is smaller than the configured runtime output.",
                    nameof(worldMatrices));
            }

            if (morphWeights == null || morphWeights.Length < fastSession.MorphWeightCount)
            {
                throw new ArgumentException(
                    "Prepared morph weight buffer is smaller than the configured runtime output.",
                    nameof(morphWeights));
            }

            MmdUnityFrameApplier.ValidateSupportedMorphPlayback(playbackInstance);
            MmdUnityWorldMatrixFrameApplier.ValidateColumnMajorWorldMatrices(
                playbackInstance,
                worldMatrices,
                fastPoseBoneIndices);
        }

        private MmdPlaybackSnapshot ApplyPreparedFastCore(
            int frame,
            float time,
            float[] worldMatrices,
            float[] morphWeights)
        {
            MmdUnityWorldMatrixFrameApplier.ApplyColumnMajorWorldMatrices(
                playbackInstance,
                worldMatrices,
                fastPoseBoneIndices);
            ApplyFastMorphWeights(morphWeights);
            // Lightweight snapshot: no managed session.EvaluateFrame call.
            // fastMorphFrame is reused in-place; frame/time are updated each call.
            // bones is empty because world matrices are applied directly to Unity transforms.
            // morphs reflects the last-applied fast weights (mutated on subsequent calls).
            // rendering is the active playback descriptor reference.
            // See runtime-session contract "fast-runtime binding snapshot mode".
            fastMorphFrame!.frame = frame;
            fastMorphFrame.time = time;
            fastSnapshot ??= new MmdPlaybackSnapshot
            {
                model = modelId,
                motion = motionId,
                frame = fastMorphFrame,
                rendering = playbackInstance.RenderingDescriptor
            };
            return fastSnapshot;
        }

        private void ApplyFastMorphWeights(float[] morphWeights)
        {
            bool hasNonZero = HasAnyNonZeroMorphWeight(morphWeights);
            if (fastMorphCacheValid && !hasNonZero && !fastMorphApplied)
            {
                return;
            }

            if (fastMorphCacheValid && hasNonZero && MorphWeightsEqual(morphWeights, fastLastAppliedMorphWeights!))
            {
                return;
            }

            RefreshFastMorphFrame(morphWeights);
            // The native mmd-runtime (RuntimeInstance::expand_group_morphs) has already expanded group
            // morph weights into their member morphs, while leaving each group morph's own weight in the
            // array. Re-running group resolution here would distribute that residual group weight a SECOND
            // time and over-drive (roughly double) the member blend shapes. Flip morphs are NOT expanded by
            // the native runtime, so the applier still resolves those.
            MmdUnityFrameApplier.ApplyMorphs(playbackInstance, fastMorphFrame!, groupMorphsResolvedExternally: true);
            for (int i = 0; i < fastMorphIndices.Length; i++)
            {
                fastLastAppliedMorphWeights![i] = morphWeights[fastMorphIndices[i]];
            }
            fastMorphApplied = hasNonZero;
            fastMorphCacheValid = true;
        }

        private void RefreshFastMorphFrame(float[] weights)
        {
            List<MmdEvaluatedMorphWeight> morphList = fastMorphFrame!.morphs;
            for (int i = 0; i < morphList.Count; i++)
            {
                int morphIndex = fastMorphIndices[i];
                morphList[i].weight = morphIndex < weights.Length ? weights[morphIndex] : 0.0f;
            }
        }

        private MmdEvaluatedFrame BuildFastMorphFrame(float[] weights)
        {
            var morphList = new List<MmdEvaluatedMorphWeight>(fastMorphIndices.Length);
            for (int i = 0; i < fastMorphIndices.Length; i++)
            {
                int morphIndex = fastMorphIndices[i];
                morphList.Add(new MmdEvaluatedMorphWeight
                {
                    name = string.IsNullOrWhiteSpace(model.morphs[morphIndex].name)
                        ? model.morphs[morphIndex].index.ToString(System.Globalization.CultureInfo.InvariantCulture)
                        : model.morphs[morphIndex].name,
                    weight = morphIndex < weights.Length ? weights[morphIndex] : 0.0f
                });
            }

            return new MmdEvaluatedFrame { morphs = morphList };
        }

        private MmdEvaluatedFrame BuildFastLivePhysicsFrame(int frame, float time)
        {
            if (fastLivePhysicsFrame == null)
            {
                var bones = new List<MmdEvaluatedBonePose>(model.bones.Count);
                foreach (MmdBoneDefinition bone in model.bones)
                {
                    bones.Add(new MmdEvaluatedBonePose
                    {
                        index = bone.index,
                        name = string.IsNullOrWhiteSpace(bone.name)
                            ? bone.index.ToString(System.Globalization.CultureInfo.InvariantCulture)
                            : bone.name,
                        localPosition = new[] { 0.0f, 0.0f, 0.0f },
                        localRotation = new[] { 0.0f, 0.0f, 0.0f, 1.0f },
                        localScale = new[] { 1.0f, 1.0f, 1.0f },
                        worldMatrix = new[]
                        {
                            1.0f, 0.0f, 0.0f, 0.0f,
                            0.0f, 1.0f, 0.0f, 0.0f,
                            0.0f, 0.0f, 1.0f, 0.0f,
                            0.0f, 0.0f, 0.0f, 1.0f
                        }
                    });
                }

                fastLivePhysicsFrame = new MmdEvaluatedFrame
                {
                    bones = bones,
                    morphs = fastMorphFrame!.morphs
                };
            }

            fastLivePhysicsFrame.frame = frame;
            fastLivePhysicsFrame.time = time;
            return fastLivePhysicsFrame;
        }

        private bool HasAnyNonZeroMorphWeight(float[] weights)
        {
            for (int i = 0; i < fastMorphIndices.Length; i++)
            {
                int morphIndex = fastMorphIndices[i];
                if (morphIndex < weights.Length && weights[morphIndex] != 0.0f)
                {
                    return true;
                }
            }

            return false;
        }

        private bool MorphWeightsEqual(float[] weights, float[] lastAppliedWeights)
        {
            if (lastAppliedWeights.Length != fastMorphIndices.Length)
            {
                return false;
            }

            for (int i = 0; i < fastMorphIndices.Length; i++)
            {
                int morphIndex = fastMorphIndices[i];
                if (morphIndex >= weights.Length || weights[morphIndex] != lastAppliedWeights[i])
                {
                    return false;
                }
            }

            return true;
        }
    }
}
