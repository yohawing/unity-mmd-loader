#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Mmd.Native;
using Mmd.Parser;
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
            float time)
        {
            return EvaluateNativeFrame(model, motion, frame, time, includeMaterials: true);
        }

        public static MmdEvaluatedFrame EvaluatePhaseOnePlaybackFrame(
            MmdModelDefinition model,
            MmdMotionDefinition motion,
            int frame,
            float time)
        {
            return EvaluateNativeFrame(model, motion, frame, time, includeMaterials: false);
        }

        public static IReadOnlyList<MmdEvaluatedFrame> EvaluatePhaseOneFrames(
            MmdModelDefinition model,
            MmdMotionDefinition motion,
            IReadOnlyList<int> frames,
            float frameRate)
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

        internal static MmdEvaluatedFrame BuildFrameFromNative(
            MmdModelDefinition model,
            int frame,
            float time,
            float[] nativeWorldMatrices,
            float[] nativeMorphWeights,
            bool includeMaterials)
        {
            var orderedBones = new List<MmdBoneDefinition>(model.bones);
            orderedBones.Sort((left, right) => left.index.CompareTo(right.index));
            var destination = new MmdEvaluatedFrame { bones = new List<MmdEvaluatedBonePose>(orderedBones.Count) };
            var morphEntries = new MmdEvaluatedMorphWeight[model.morphs.Count];
            var activeMorphOrder = new List<int>();
            for (int i = 0; i < model.morphs.Count && i < nativeMorphWeights.Length; i++)
            {
                if (nativeMorphWeights[i] == 0.0f)
                    continue;
                morphEntries[i] = new MmdEvaluatedMorphWeight { name = model.morphs[i].name };
                activeMorphOrder.Add(i);
            }
            activeMorphOrder.Sort((left, right) => StringComparer.Ordinal.Compare(
                morphEntries[left].name, morphEntries[right].name));
            return BuildFrameFromNativeInPlace(
                model,
                frame,
                time,
                nativeWorldMatrices,
                nativeMorphWeights,
                destination,
                new float[checked(orderedBones.Count * 16)],
                new float[16],
                orderedBones,
                morphEntries,
                activeMorphOrder.ToArray(),
                includeMaterials);
        }

        internal static MmdEvaluatedFrame BuildFrameFromNativeInPlace(
            MmdModelDefinition model,
            int frame,
            float time,
            float[] nativeWorldMatrices,
            float[] nativeMorphWeights,
            MmdEvaluatedFrame destination,
            float[] rowMajorAll,
            float[] localMatrixScratch,
            IReadOnlyList<MmdBoneDefinition> orderedBones,
            MmdEvaluatedMorphWeight[] morphEntries,
            int[] morphOrder,
            bool includeMaterials)
        {
            int boneCount = orderedBones.Count;
            for (int i = 0; i < boneCount; i++)
                TransposeMatrix4x4(nativeWorldMatrices, i * 16, rowMajorAll, i * 16);

            EnsureBoneEntries(destination, orderedBones);
            for (int i = 0; i < boneCount; i++)
            {
                MmdBoneDefinition bone = orderedBones[i];
                MmdEvaluatedBonePose pose = destination.bones[i];
                int boneIdx = bone.index;
                int offset = boneIdx * 16;
                pose.index = boneIdx;
                pose.name = string.IsNullOrWhiteSpace(bone.name) ? boneIdx.ToString() : bone.name;
                Array.Copy(rowMajorAll, offset, pose.worldMatrix, 0, 16);
                pose.localScale[0] = 1.0f;
                pose.localScale[1] = 1.0f;
                pose.localScale[2] = 1.0f;

                if (bone.parentIndex >= 0 && bone.parentIndex < boneCount)
                {
                    MultiplyInverseRigidRowMajorInto(
                        rowMajorAll,
                        bone.parentIndex * 16,
                        rowMajorAll,
                        offset,
                        localMatrixScratch,
                        0);
                    FindBoneOriginInto(orderedBones, bone.parentIndex, out float parentX, out float parentY, out float parentZ);
                    GetSafeOriginInto(bone.origin, out float boneX, out float boneY, out float boneZ);
                    float restX = boneX - parentX;
                    float restY = boneY - parentY;
                    float restZ = boneZ - parentZ;
                    pose.localPosition[0] = localMatrixScratch[3] - restX;
                    pose.localPosition[1] = localMatrixScratch[7] - restY;
                    pose.localPosition[2] = localMatrixScratch[11] - restZ;
                    ExtractQuaternionRowMajorInto(localMatrixScratch, 0, pose.localRotation, 0);
                }
                else
                {
                    GetSafeOriginInto(bone.origin, out float boneX, out float boneY, out float boneZ);
                    pose.localPosition[0] = pose.worldMatrix[3] - boneX;
                    pose.localPosition[1] = pose.worldMatrix[7] - boneY;
                    pose.localPosition[2] = pose.worldMatrix[11] - boneZ;
                    ExtractQuaternionRowMajorInto(pose.worldMatrix, 0, pose.localRotation, 0);
                }
            }

            destination.morphs.Clear();
            for (int i = 0; i < morphOrder.Length; i++)
            {
                int morphIndex = morphOrder[i];
                MmdEvaluatedMorphWeight morph = morphEntries[morphIndex];
                morph.weight = morphIndex < nativeMorphWeights.Length ? nativeMorphWeights[morphIndex] : 0.0f;
                if (morph.weight != 0.0f)
                    destination.morphs.Add(morph);
            }
            destination.frame = frame;
            destination.time = time;
            destination.materials.Clear();
            if (includeMaterials)
                destination.materials.AddRange(MmdMaterialDescriptorBuilder.Build(model));
            return destination;
        }

        private static void EnsureBoneEntries(MmdEvaluatedFrame destination, IReadOnlyList<MmdBoneDefinition> orderedBones)
        {
            if (destination.bones.Count == orderedBones.Count)
                return;
            destination.bones.Clear();
            for (int i = 0; i < orderedBones.Count; i++)
            {
                destination.bones.Add(new MmdEvaluatedBonePose
                {
                    index = orderedBones[i].index,
                    name = orderedBones[i].name,
                    localPosition = new float[3],
                    localRotation = new float[4],
                    localScale = new[] { 1.0f, 1.0f, 1.0f },
                    worldMatrix = new float[16]
                });
            }
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

        private static void MultiplyInverseRigidRowMajorInto(
            float[] a,
            int ao,
            float[] b,
            int bo,
            float[] destination,
            int destinationOffset)
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

            destination[destinationOffset] = a00 * b00 + a10 * b10 + a20 * b20;
            destination[destinationOffset + 1] = a00 * b01 + a10 * b11 + a20 * b21;
            destination[destinationOffset + 2] = a00 * b02 + a10 * b12 + a20 * b22;
            destination[destinationOffset + 3] = a00 * dx + a10 * dy + a20 * dz;
            destination[destinationOffset + 4] = a01 * b00 + a11 * b10 + a21 * b20;
            destination[destinationOffset + 5] = a01 * b01 + a11 * b11 + a21 * b21;
            destination[destinationOffset + 6] = a01 * b02 + a11 * b12 + a21 * b22;
            destination[destinationOffset + 7] = a01 * dx + a11 * dy + a21 * dz;
            destination[destinationOffset + 8] = a02 * b00 + a12 * b10 + a22 * b20;
            destination[destinationOffset + 9] = a02 * b01 + a12 * b11 + a22 * b21;
            destination[destinationOffset + 10] = a02 * b02 + a12 * b12 + a22 * b22;
            destination[destinationOffset + 11] = a02 * dx + a12 * dy + a22 * dz;
            destination[destinationOffset + 12] = 0f;
            destination[destinationOffset + 13] = 0f;
            destination[destinationOffset + 14] = 0f;
            destination[destinationOffset + 15] = 1f;
        }

        private static void ExtractQuaternionRowMajorInto(
            float[] m,
            int o,
            float[] destination,
            int destinationOffset)
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

            destination[destinationOffset] = x;
            destination[destinationOffset + 1] = y;
            destination[destinationOffset + 2] = z;
            destination[destinationOffset + 3] = w;
        }

        private static void FindBoneOriginInto(
            IReadOnlyList<MmdBoneDefinition> orderedBones,
            int boneIndex,
            out float x,
            out float y,
            out float z)
        {
            if (boneIndex >= 0 && boneIndex < orderedBones.Count && orderedBones[boneIndex].index == boneIndex)
            {
                GetSafeOriginInto(orderedBones[boneIndex].origin, out x, out y, out z);
                return;
            }
            for (int i = 0; i < orderedBones.Count; i++)
            {
                if (orderedBones[i].index == boneIndex)
                {
                    GetSafeOriginInto(orderedBones[i].origin, out x, out y, out z);
                    return;
                }
            }
            x = 0f;
            y = 0f;
            z = 0f;
        }

        private static void GetSafeOriginInto(
            float[]? origin,
            out float x,
            out float y,
            out float z)
        {
            if (origin != null && origin.Length >= 3)
            {
                x = origin[0];
                y = origin[1];
                z = origin[2];
                return;
            }
            x = 0f;
            y = 0f;
            z = 0f;
        }
    }
}
