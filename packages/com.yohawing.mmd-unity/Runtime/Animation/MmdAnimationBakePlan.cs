#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Yohawing.MmdUnity.Physics;

namespace Yohawing.MmdUnity
{
    [Serializable]
    public sealed class MmdAnimationBakeSummary
    {
        public string model = string.Empty;
        public string motion = string.Empty;
        public string physicsMode = "off";
        public string outputPath = string.Empty;
        public string provenanceStatus = "explicit-user-operation-pending-source-review";
        public string writePolicy = "animationclip-asset-write-explicit-user-operation-only";
        public float frameRate;
        public int startFrame;
        public int endFrame;
        public int bakedFrameCount;
        public int boneCount;
        public int positionCurveCount;
        public int rotationCurveCount;
        public int totalCurveCount;
        public List<int> frames = new();
        public List<string> boneNames = new();
        public List<MmdAnimationBakeBoneCurve> boneCurves = new();
    }

    [Serializable]
    public sealed class MmdAnimationBakeBoneCurve
    {
        public int boneIndex;
        public string boneName = string.Empty;
        public List<MmdAnimationBakePositionKey> positionKeys = new();
        public List<MmdAnimationBakeRotationKey> rotationKeys = new();
    }

    [Serializable]
    public sealed class MmdAnimationBakePositionKey
    {
        public int frame;
        public float time;
        public float x;
        public float y;
        public float z;
    }

    [Serializable]
    public sealed class MmdAnimationBakeRotationKey
    {
        public int frame;
        public float time;
        public float x;
        public float y;
        public float z;
        public float w;
    }

    public static class MmdAnimationBakePlanner
    {
        public static MmdAnimationBakeSummary BuildTransformBakeSummary(
            MmdRuntimeSession session,
            int startFrame,
            int endFrame,
            float frameRate,
            string outputPath = "Assets/MmdUnity/BakedAnimations/animation-bake-plan.anim")
        {
            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            MmdPlaybackTime.ValidateFrame(startFrame);
            MmdPlaybackTime.ValidateFrame(endFrame);
            MmdPlaybackTime.ValidateFrameRate(frameRate);
            ValidateOutputPath(outputPath);
            if (endFrame < startFrame)
            {
                throw new ArgumentOutOfRangeException(nameof(endFrame), "End frame must be greater than or equal to start frame.");
            }

            int[] frames = Enumerable.Range(startFrame, checked(endFrame - startFrame + 1)).ToArray();
            IReadOnlyList<MmdPlaybackSnapshot> snapshots = session.BuildSnapshots(frames, frameRate, new NullMmdPhysicsBackend());
            MmdPlaybackSnapshot first = snapshots[0];
            var orderedBones = first.frame.bones
                .OrderBy(bone => bone.index)
                .ToList();
            var boneNames = orderedBones.Select(bone => bone.name).ToList();
            List<MmdAnimationBakeBoneCurve> boneCurves = orderedBones
                .Select(bone => BuildBoneCurve(snapshots, bone.index, bone.name))
                .ToList();
            int boneCount = boneNames.Count;
            return new MmdAnimationBakeSummary
            {
                model = first.model,
                motion = first.motion,
                physicsMode = "off",
                outputPath = outputPath,
                provenanceStatus = "explicit-user-operation-pending-source-review",
                writePolicy = "animationclip-asset-write-explicit-user-operation-only",
                frameRate = frameRate,
                startFrame = startFrame,
                endFrame = endFrame,
                bakedFrameCount = snapshots.Count,
                boneCount = boneCount,
                positionCurveCount = checked(boneCount * 3),
                rotationCurveCount = checked(boneCount * 4),
                totalCurveCount = checked(boneCount * 7),
                frames = snapshots.Select(snapshot => snapshot.frame.frame).ToList(),
                boneNames = boneNames,
                boneCurves = boneCurves
            };
        }

        private static void ValidateOutputPath(string outputPath)
        {
            if (string.IsNullOrWhiteSpace(outputPath))
            {
                throw new ArgumentException("AnimationClip bake output path is required.", nameof(outputPath));
            }

            string normalized = outputPath.Replace('\\', '/');
            if (!normalized.StartsWith("Assets/", StringComparison.Ordinal) ||
                normalized.EndsWith("/", StringComparison.Ordinal) ||
                normalized.Contains("/../", StringComparison.Ordinal) ||
                !normalized.EndsWith(".anim", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("AnimationClip bake output path must be an AssetDatabase .anim path.", nameof(outputPath));
            }
        }

        private static MmdAnimationBakeBoneCurve BuildBoneCurve(
            IReadOnlyList<MmdPlaybackSnapshot> snapshots,
            int boneIndex,
            string boneName)
        {
            var positionKeys = new List<MmdAnimationBakePositionKey>(snapshots.Count);
            var rotationKeys = new List<MmdAnimationBakeRotationKey>(snapshots.Count);
            foreach (MmdPlaybackSnapshot snapshot in snapshots)
            {
                MmdEvaluatedBonePose bone = snapshot.frame.bones.First(item => item.index == boneIndex);
                if (bone.localPosition == null || bone.localPosition.Length != 3)
                {
                    throw new InvalidOperationException($"Bone localPosition must have 3 values: {boneName}");
                }

                if (bone.localRotation == null || bone.localRotation.Length != 4)
                {
                    throw new InvalidOperationException($"Bone localRotation must have 4 values: {boneName}");
                }

                positionKeys.Add(new MmdAnimationBakePositionKey
                {
                    frame = snapshot.frame.frame,
                    time = snapshot.frame.time,
                    x = bone.localPosition[0],
                    y = bone.localPosition[1],
                    z = bone.localPosition[2]
                });
                rotationKeys.Add(new MmdAnimationBakeRotationKey
                {
                    frame = snapshot.frame.frame,
                    time = snapshot.frame.time,
                    x = bone.localRotation[0],
                    y = bone.localRotation[1],
                    z = bone.localRotation[2],
                    w = bone.localRotation[3]
                });
            }

            return new MmdAnimationBakeBoneCurve
            {
                boneIndex = boneIndex,
                boneName = boneName,
                positionKeys = positionKeys,
                rotationKeys = rotationKeys
            };
        }
    }
}
