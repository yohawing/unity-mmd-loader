#nullable enable

using System;
using System.Collections.Generic;
using Mmd.Motion;
using Mmd.Native;
using Mmd.Parser;
using Mmd.Pose;
using UnityEngine;

namespace Mmd.UnityIntegration
{
    public sealed partial class MmdUnityPlaybackBinding
    {
        /// <summary>
        /// Opt-in fast runtime using the native mmd-runtime FFI library.
        /// Affects <see cref="ApplyFrame"/> in animation-only playback and the animation pose stage of Live physics playback.
        /// Returns true and clears <paramref name="reason"/> on success.
        /// Returns false with a diagnostic message in <paramref name="reason"/> when the native library is absent,
        /// ABI-incompatible, or the bone/morph counts do not match the managed model.
        /// </summary>
        public bool TryEnableFastRuntime(byte[] pmxBytes, byte[] vmdBytes, out string reason)
        {
            if (pmxBytes == null || pmxBytes.Length == 0)
            {
                throw new ArgumentException("PMX bytes are required.", nameof(pmxBytes));
            }

            if (vmdBytes == null || vmdBytes.Length == 0)
            {
                throw new ArgumentException("VMD bytes are required.", nameof(vmdBytes));
            }

            DisposeFastRuntime();
            if (model.HasDeformAfterPhysicsBones)
            {
                reason = "mmd-runtime fast playback does not support PMX deformAfterPhysics two-pass bone evaluation; managed playback remains active.";
                return false;
            }

            try
            {
                MmdRuntimeFfiPlaybackSession candidate = MmdRuntimeFfiPlaybackSession.Create(pmxBytes, vmdBytes);
                int candidateBoneCount = candidate.BoneCount;
                int candidateMorphCount = candidate.MorphCount;
                if (candidateBoneCount != model.bones.Count)
                {
                    candidate.Dispose();
                    reason = $"mmd-runtime bone count {candidateBoneCount} does not match managed model bone count {model.bones.Count}.";
                    return false;
                }

                if (candidateMorphCount != model.morphs.Count)
                {
                    candidate.Dispose();
                    reason = $"mmd-runtime morph count {candidateMorphCount} does not match managed model morph count {model.morphs.Count}.";
                    return false;
                }

                int expectedWorldMatrixFloatCount = model.bones.Count * 16;
                if (candidate.WorldMatrixFloatCount < expectedWorldMatrixFloatCount)
                {
                    candidate.Dispose();
                    reason = $"mmd-runtime world matrix float count {candidate.WorldMatrixFloatCount} is smaller than required {expectedWorldMatrixFloatCount}.";
                    return false;
                }

                if (candidate.MorphWeightCount != model.morphs.Count)
                {
                    candidate.Dispose();
                    reason = $"mmd-runtime morph weight count {candidate.MorphWeightCount} does not match managed model morph count {model.morphs.Count}.";
                    return false;
                }

                if (candidate.IkEnabledCount != model.ik.Count)
                {
                    candidate.Dispose();
                    reason = $"mmd-runtime IK enabled count {candidate.IkEnabledCount} does not match managed model IK count {model.ik.Count}.";
                    return false;
                }

                fastSession = candidate;
                fastWorldMatrices = new float[fastSession.WorldMatrixFloatCount];
                fastMorphWeights = new float[fastSession.MorphWeightCount];
                fastIkEnabled = new byte[fastSession.IkEnabledCount];
                fastLastAppliedMorphWeights = new float[fastSession.MorphWeightCount];
                fastMorphFrame = BuildFastMorphFrame(fastMorphWeights);
                fastMorphApplied = false;
                fastMorphCacheValid = false;
                reason = string.Empty;
                return true;
            }
            catch (DllNotFoundException ex)
            {
                DisposeFastRuntime();
                reason = ex.GetType().Name + ": " + ex.Message;
                return false;
            }
            catch (EntryPointNotFoundException ex)
            {
                DisposeFastRuntime();
                reason = ex.GetType().Name + ": " + ex.Message;
                return false;
            }
            catch (BadImageFormatException ex)
            {
                DisposeFastRuntime();
                reason = ex.GetType().Name + ": " + ex.Message;
                return false;
            }
            catch (InvalidOperationException ex)
            {
                DisposeFastRuntime();
                reason = ex.GetType().Name + ": " + ex.Message;
                return false;
            }
        }

        public void DisableFastRuntime()
        {
            DisposeFastRuntime();
        }

        internal bool HasFastRuntimeBatch => fastSession != null;

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

        private void DisposeFastRuntime()
        {
            fastSession?.Dispose();
            fastSession = null;
            fastWorldMatrices = null;
            fastMorphWeights = null;
            fastIkEnabled = null;
            fastMorphFrame = null;
            fastLastAppliedMorphWeights = null;
            fastMorphApplied = false;
            fastMorphCacheValid = false;
            fastSnapshot = null;
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
            fastSession!.EvaluateAndCopy(frame, fastWorldMatrices!, fastMorphWeights!, fastIkEnabled!);
            MmdUnityWorldMatrixFrameApplier.ApplyColumnMajorWorldMatrices(playbackInstance, fastWorldMatrices!);
            ApplyFastMorphWeights();
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

        private MmdPlaybackSnapshot ApplyFastTime(float time, float frameRate)
        {
            int frame = MmdPlaybackTime.ToFrame(time, frameRate);
            fastSession!.EvaluateAndCopy(frame, fastWorldMatrices!, fastMorphWeights!, fastIkEnabled!);
            MmdUnityWorldMatrixFrameApplier.ApplyColumnMajorWorldMatrices(playbackInstance, fastWorldMatrices!);
            ApplyFastMorphWeights();
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

        private void ApplyFastMorphWeights()
        {
            bool hasNonZero = HasAnyNonZeroMorphWeight(fastMorphWeights!);
            if (fastMorphCacheValid && !hasNonZero && !fastMorphApplied)
            {
                return;
            }

            if (fastMorphCacheValid && hasNonZero && MorphWeightsEqual(fastMorphWeights!, fastLastAppliedMorphWeights!))
            {
                return;
            }

            RefreshFastMorphFrame(fastMorphWeights!);
            // The native mmd-runtime (RuntimeInstance::expand_group_morphs) has already expanded group
            // morph weights into their member morphs, while leaving each group morph's own weight in the
            // array. Re-running group resolution here would distribute that residual group weight a SECOND
            // time and over-drive (roughly double) the member blend shapes. Flip morphs are NOT expanded by
            // the native runtime, so the applier still resolves those.
            MmdUnityFrameApplier.ApplyMorphs(playbackInstance, fastMorphFrame!, groupMorphsResolvedExternally: true);
            Array.Copy(fastMorphWeights!, fastLastAppliedMorphWeights!, fastMorphWeights!.Length);
            fastMorphApplied = hasNonZero;
            fastMorphCacheValid = true;
        }

        private void RefreshFastMorphFrame(float[] weights)
        {
            List<MmdEvaluatedMorphWeight> morphList = fastMorphFrame!.morphs;
            for (int i = 0; i < morphList.Count; i++)
            {
                morphList[i].weight = i < weights.Length ? weights[i] : 0.0f;
            }
        }

        private MmdEvaluatedFrame BuildFastMorphFrame(float[] weights)
        {
            var morphList = new List<MmdEvaluatedMorphWeight>(model.morphs.Count);
            for (int i = 0; i < model.morphs.Count; i++)
            {
                morphList.Add(new MmdEvaluatedMorphWeight
                {
                    name = string.IsNullOrWhiteSpace(model.morphs[i].name)
                        ? model.morphs[i].index.ToString(System.Globalization.CultureInfo.InvariantCulture)
                        : model.morphs[i].name,
                    weight = i < weights.Length ? weights[i] : 0.0f
                });
            }

            return new MmdEvaluatedFrame { morphs = morphList };
        }

        private MmdEvaluatedFrame BuildFastLivePhysicsFrame(int frame, float time)
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

            return new MmdEvaluatedFrame
            {
                frame = frame,
                time = time,
                bones = bones,
                morphs = BuildFastMorphFrame(fastMorphWeights!).morphs
            };
        }

        private static bool HasAnyNonZeroMorphWeight(float[] weights)
        {
            for (int i = 0; i < weights.Length; i++)
            {
                if (weights[i] != 0.0f)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool MorphWeightsEqual(float[] a, float[] b)
        {
            if (a.Length != b.Length)
            {
                return false;
            }

            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i])
                {
                    return false;
                }
            }

            return true;
        }
    }
}
