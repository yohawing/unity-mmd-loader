#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Mmd.Motion;
using Mmd.Native;
using Mmd.Parser;
using Mmd.Physics;
using Mmd.Rendering;

namespace Mmd
{
    [Serializable]
    public sealed class MmdEvaluatedFrame
    {
        public int frame;
        public float time;
        public List<MmdEvaluatedBonePose> bones = new();
        public List<MmdEvaluatedMorphWeight> morphs = new();
        public List<MmdMaterialDescriptor> materials = new();
    }

    [Serializable]
    public sealed class MmdEvaluatedBonePose
    {
        public int index;
        public string name = string.Empty;
        public float[] localPosition = Array.Empty<float>();
        public float[] localRotation = Array.Empty<float>();
        public float[] localScale = Array.Empty<float>();
        public float[] worldMatrix = Array.Empty<float>();
    }

    [Serializable]
    public sealed class MmdEvaluatedMorphWeight
    {
        public string name = string.Empty;
        public float weight;
    }

    public static class MmdRuntimeFrameEvaluator
    {
        public static MmdEvaluatedFrame EvaluatePhaseOneFrame(
            MmdModelDefinition model,
            MmdMotionDefinition motion,
            int frame,
            float time,
            IMmdPhysicsBackend? physicsBackend = null,
            IMmdIkSolver? ikSolver = null)
        {
            return EvaluateNativeFrame(model, motion, frame, time, includeMaterials: true);
        }

        public static MmdEvaluatedFrame EvaluatePhaseOnePlaybackFrame(
            MmdModelDefinition model,
            MmdMotionDefinition motion,
            int frame,
            float time,
            IMmdPhysicsBackend? physicsBackend = null,
            IMmdIkSolver? ikSolver = null)
        {
            return EvaluateNativeFrame(model, motion, frame, time, includeMaterials: false);
        }

        internal static MmdEvaluatedFrame EvaluateValidatedPhaseOnePlaybackFrame(
            MmdModelDefinition model,
            MmdMotionDefinition motion,
            int frame,
            float time,
            IMmdPhysicsBackend? physicsBackend = null,
            IMmdIkSolver? ikSolver = null)
        {
            ValidateFrame(frame);
            ValidateTime(time);
            return EvaluateNativeFrame(model, motion, frame, time, includeMaterials: false);
        }

        internal static MmdEvaluatedFrame EvaluateValidatedBeforePhysicsPlaybackFrame(
            MmdModelDefinition model,
            MmdMotionDefinition motion,
            int frame,
            float time,
            IMmdPhysicsBackend? physicsBackend = null,
            IMmdIkSolver? ikSolver = null)
        {
            // Native evaluation produces the animation-only result (no physics).
            // This is equivalent to the old "before physics" stage.
            ValidateFrame(frame);
            ValidateTime(time);
            return EvaluateNativeFrame(model, motion, frame, time, includeMaterials: false);
        }

        public static IReadOnlyList<MmdEvaluatedFrame> EvaluatePhaseOneFrames(
            MmdModelDefinition model,
            MmdMotionDefinition motion,
            IReadOnlyList<int> frames,
            float frameRate,
            IMmdPhysicsBackend? physicsBackend = null,
            IMmdIkSolver? ikSolver = null)
        {
            ValidateInputs(model, motion);
            if (frames == null)
                throw new ArgumentNullException(nameof(frames));
            if (frames.Count == 0)
                throw new ArgumentException("At least one frame is required.", nameof(frames));
            MmdPlaybackTime.ValidateFrameRate(frameRate);

            byte[] pmxBytes = RequireSourceBytes(model);
            byte[] vmdBytes = RequireSourceBytes(motion);

            using var session = MmdRuntimeFfiPlaybackSession.Create(pmxBytes, vmdBytes);
            float[] nativeWorldMatrices = new float[session.WorldMatrixFloatCount];
            float[] nativeMorphWeights = new float[session.MorphWeightCount];
            byte[] nativeIkEnabled = new byte[session.IkEnabledCount];

            var evaluatedFrames = new List<MmdEvaluatedFrame>(frames.Count);
            var seenFrames = new HashSet<int>(frames.Count);
            foreach (int frame in frames.OrderBy(value => value))
            {
                ValidateFrame(frame);
                if (!seenFrames.Add(frame))
                    throw new ArgumentException("Frame indices must be unique.", nameof(frames));

                session.EvaluateAndCopy(frame, nativeWorldMatrices, nativeMorphWeights, nativeIkEnabled);
                evaluatedFrames.Add(BuildFrameFromNative(
                    model, frame, MmdPlaybackTime.ToTime(frame, frameRate),
                    nativeWorldMatrices, nativeMorphWeights, includeMaterials: true));
            }

            return evaluatedFrames;
        }

        private static MmdEvaluatedFrame EvaluateNativeFrame(
            MmdModelDefinition model,
            MmdMotionDefinition motion,
            int frame,
            float time,
            bool includeMaterials)
        {
            ValidateInputs(model, motion);
            ValidateFrame(frame);
            ValidateTime(time);

            byte[] pmxBytes = RequireSourceBytes(model);
            byte[] vmdBytes = RequireSourceBytes(motion);

            using var session = MmdRuntimeFfiPlaybackSession.Create(pmxBytes, vmdBytes);
            float[] nativeWorldMatrices = new float[session.WorldMatrixFloatCount];
            float[] nativeMorphWeights = new float[session.MorphWeightCount];
            byte[] nativeIkEnabled = new byte[session.IkEnabledCount];

            session.EvaluateAndCopy(frame, nativeWorldMatrices, nativeMorphWeights, nativeIkEnabled);
            return BuildFrameFromNative(model, frame, time, nativeWorldMatrices, nativeMorphWeights, includeMaterials);
        }

        private static MmdEvaluatedFrame BuildFrameFromNative(
            MmdModelDefinition model,
            int frame,
            float time,
            float[] nativeWorldMatrices,
            float[] nativeMorphWeights,
            bool includeMaterials)
        {
            var orderedBones = new List<MmdBoneDefinition>(model.bones);
            orderedBones.Sort((left, right) => left.index.CompareTo(right.index));
            int boneCount = orderedBones.Count;

            var rowMajorAll = new float[boneCount * 16];
            for (int i = 0; i < boneCount; i++)
                TransposeMatrix4x4(nativeWorldMatrices, i * 16, rowMajorAll, i * 16);

            var bones = new List<MmdEvaluatedBonePose>(boneCount);
            foreach (MmdBoneDefinition bone in orderedBones)
            {
                int boneIdx = bone.index;
                int offset = boneIdx * 16;
                float[] worldMatrix = new float[16];
                Array.Copy(rowMajorAll, offset, worldMatrix, 0, 16);

                float[] localPosition;
                float[] localRotation;

                if (bone.parentIndex >= 0 && bone.parentIndex < boneCount)
                {
                    float[] localMatrix = MultiplyInverseRigidRowMajor(
                        rowMajorAll, bone.parentIndex * 16,
                        rowMajorAll, offset);

                    float[] parentOrigin = FindBoneOrigin(orderedBones, bone.parentIndex);
                    float[] boneOrigin = SafeOrigin(bone.origin);
                    float restX = boneOrigin[0] - parentOrigin[0];
                    float restY = boneOrigin[1] - parentOrigin[1];
                    float restZ = boneOrigin[2] - parentOrigin[2];

                    localPosition = new[]
                    {
                        localMatrix[3] - restX,
                        localMatrix[7] - restY,
                        localMatrix[11] - restZ
                    };
                    localRotation = ExtractQuaternionRowMajor(localMatrix, 0);
                }
                else
                {
                    float[] boneOrigin = SafeOrigin(bone.origin);
                    localPosition = new[]
                    {
                        worldMatrix[3] - boneOrigin[0],
                        worldMatrix[7] - boneOrigin[1],
                        worldMatrix[11] - boneOrigin[2]
                    };
                    localRotation = ExtractQuaternionRowMajor(worldMatrix, 0);
                }

                bones.Add(new MmdEvaluatedBonePose
                {
                    index = boneIdx,
                    name = string.IsNullOrWhiteSpace(bone.name) ? boneIdx.ToString() : bone.name,
                    localPosition = localPosition,
                    localRotation = localRotation,
                    localScale = new[] { 1.0f, 1.0f, 1.0f },
                    worldMatrix = worldMatrix
                });
            }

            var morphs = new List<MmdEvaluatedMorphWeight>();
            for (int i = 0; i < model.morphs.Count && i < nativeMorphWeights.Length; i++)
            {
                if (nativeMorphWeights[i] != 0.0f)
                {
                    morphs.Add(new MmdEvaluatedMorphWeight
                    {
                        name = model.morphs[i].name,
                        weight = nativeMorphWeights[i]
                    });
                }
            }
            morphs.Sort((a, b) => StringComparer.Ordinal.Compare(a.name, b.name));

            return new MmdEvaluatedFrame
            {
                frame = frame,
                time = time,
                bones = bones,
                morphs = morphs,
                materials = includeMaterials ? MmdMaterialDescriptorBuilder.Build(model).ToList() : new List<MmdMaterialDescriptor>()
            };
        }

        private static void ValidateInputs(MmdModelDefinition model, MmdMotionDefinition motion)
        {
            if (model == null)
                throw new ArgumentNullException(nameof(model));
            if (motion == null)
                throw new ArgumentNullException(nameof(motion));
            MmdModelValidator.ThrowIfInvalid(model);
            MmdMotionValidator.ThrowIfInvalid(motion);
        }

        private static void ValidateFrame(int frame) => MmdPlaybackTime.ValidateFrame(frame);
        private static void ValidateTime(float time) => MmdPlaybackTime.ValidateTime(time);

        private static byte[] RequireSourceBytes(MmdModelDefinition model)
        {
            return model.sourceBytes
                ?? throw new InvalidOperationException(
                    "Model sourceBytes are required for native evaluation. Use NativeMmdParser to load models.");
        }

        private static byte[] RequireSourceBytes(MmdMotionDefinition motion)
        {
            return motion.sourceBytes
                ?? throw new InvalidOperationException(
                    "Motion sourceBytes are required for native evaluation. Use NativeMmdParser to load motions.");
        }

        private static void TransposeMatrix4x4(float[] src, int so, float[] dst, int doff)
        {
            for (int r = 0; r < 4; r++)
            for (int c = 0; c < 4; c++)
                dst[doff + r * 4 + c] = src[so + c * 4 + r];
        }

        private static float[] MultiplyInverseRigidRowMajor(float[] a, int ao, float[] b, int bo)
        {
            float a00 = a[ao], a01 = a[ao + 1], a02 = a[ao + 2], atx = a[ao + 3];
            float a10 = a[ao + 4], a11 = a[ao + 5], a12 = a[ao + 6], aty = a[ao + 7];
            float a20 = a[ao + 8], a21 = a[ao + 9], a22 = a[ao + 10], atz = a[ao + 11];

            float dx = b[bo + 3] - atx;
            float dy = b[bo + 7] - aty;
            float dz = b[bo + 11] - atz;

            float b00 = b[bo], b01 = b[bo + 1], b02 = b[bo + 2];
            float b10 = b[bo + 4], b11 = b[bo + 5], b12 = b[bo + 6];
            float b20 = b[bo + 8], b21 = b[bo + 9], b22 = b[bo + 10];

            return new[]
            {
                a00 * b00 + a10 * b10 + a20 * b20, a00 * b01 + a10 * b11 + a20 * b21, a00 * b02 + a10 * b12 + a20 * b22, a00 * dx + a10 * dy + a20 * dz,
                a01 * b00 + a11 * b10 + a21 * b20, a01 * b01 + a11 * b11 + a21 * b21, a01 * b02 + a11 * b12 + a21 * b22, a01 * dx + a11 * dy + a21 * dz,
                a02 * b00 + a12 * b10 + a22 * b20, a02 * b01 + a12 * b11 + a22 * b21, a02 * b02 + a12 * b12 + a22 * b22, a02 * dx + a12 * dy + a22 * dz,
                0f, 0f, 0f, 1f
            };
        }

        private static float[] ExtractQuaternionRowMajor(float[] m, int o)
        {
            float m00 = m[o], m01 = m[o + 1], m02 = m[o + 2];
            float m10 = m[o + 4], m11 = m[o + 5], m12 = m[o + 6];
            float m20 = m[o + 8], m21 = m[o + 9], m22 = m[o + 10];

            float trace = m00 + m11 + m22;
            float x, y, z, w;

            if (trace > 0f)
            {
                float s = MathF.Sqrt(trace + 1f) * 2f;
                w = 0.25f * s;
                x = (m21 - m12) / s;
                y = (m02 - m20) / s;
                z = (m10 - m01) / s;
            }
            else if (m00 > m11 && m00 > m22)
            {
                float s = MathF.Sqrt(1f + m00 - m11 - m22) * 2f;
                w = (m21 - m12) / s;
                x = 0.25f * s;
                y = (m01 + m10) / s;
                z = (m02 + m20) / s;
            }
            else if (m11 > m22)
            {
                float s = MathF.Sqrt(1f + m11 - m00 - m22) * 2f;
                w = (m02 - m20) / s;
                x = (m01 + m10) / s;
                y = 0.25f * s;
                z = (m12 + m21) / s;
            }
            else
            {
                float s = MathF.Sqrt(1f + m22 - m00 - m11) * 2f;
                w = (m10 - m01) / s;
                x = (m02 + m20) / s;
                y = (m12 + m21) / s;
                z = 0.25f * s;
            }

            return new[] { x, y, z, w };
        }

        private static float[] FindBoneOrigin(List<MmdBoneDefinition> orderedBones, int boneIndex)
        {
            if (boneIndex >= 0 && boneIndex < orderedBones.Count && orderedBones[boneIndex].index == boneIndex)
                return SafeOrigin(orderedBones[boneIndex].origin);
            for (int i = 0; i < orderedBones.Count; i++)
            {
                if (orderedBones[i].index == boneIndex)
                    return SafeOrigin(orderedBones[i].origin);
            }
            return new[] { 0f, 0f, 0f };
        }

        private static float[] SafeOrigin(float[]? origin)
        {
            return origin != null && origin.Length >= 3 ? origin : new[] { 0f, 0f, 0f };
        }
    }
}
