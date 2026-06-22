#nullable enable

// Simple MMD-space CCD solver. Reference-specific parity patches should stay
// outside this default path until backed by a focused MMD-exported oracle.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Mmd.Parser;
using Mmd.Pose;

namespace Mmd.Motion
{
    public sealed class MmdIkSolver : IMmdIkSolver, IMmdAppendTransformProvider
    {
        private const int MaxIkLoopCount = 16;
        private const float Tolerance = 1e-4f;
        private const float ConvergedDistanceImprovementTolerance = 1e-5f;
        private const float HalfPi = MathF.PI * 0.5f;
        private const float RotationOrderThreshold = 88.0f * MathF.PI / 180.0f;
        private static readonly HashSet<int> EmptyChangedBoneIndices = new HashSet<int>(capacity: 0);
        public string Name => "MMD";

        private enum EulerRotationOrder
        {
            Yxz,
            Zyx,
            Xzy
        }

        public MmdSampledMotion ApplyAppendTransforms(MmdModelDefinition model, MmdSampledMotion sampledMotion)
        {
            return MmdAppendTransformEvaluator.ApplyAppendTransforms(model, sampledMotion);
        }

        public MmdSampledMotion ApplyAppendTransforms(
            MmdModelDefinition model,
            MmdSampledMotion sampledMotion,
            MmdBoneEvaluationPass pass)
        {
            return MmdAppendTransformEvaluator.ApplyAppendTransforms(model, sampledMotion, pass);
        }

        public MmdSampledMotion Solve(MmdModelDefinition model, MmdSampledMotion? sampledMotion)
        {
            return SolveCore(model, sampledMotion, pass: null);
        }

        public MmdSampledMotion Solve(
            MmdModelDefinition model,
            MmdSampledMotion? sampledMotion,
            MmdBoneEvaluationPass pass)
        {
            return SolveCore(model, sampledMotion, pass);
        }

        private MmdSampledMotion SolveCore(
            MmdModelDefinition model,
            MmdSampledMotion? sampledMotion,
            MmdBoneEvaluationPass? pass)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            MmdModelValidator.ThrowIfInvalid(model);
            MmdSampledMotion result = CopySampledMotion(sampledMotion);
            MmdBoneDefinition?[] indexedBones = BuildIndexedBones(model);
            IReadOnlyList<int>[] indexedChildren = BuildIndexedChildren(indexedBones);
            float[][] translations = CaptureLocalTranslations(model, indexedBones, result);
            float[][] baseRotations = CaptureIndexedRotations(indexedBones, result);
            var ikRotations = new float[indexedBones.Length][];
            for (int i = 0; i < indexedBones.Length; i++)
            {
                ikRotations[i] = CopyQuaternion(MmdBonePoseSample.Identity.Rotation);
            }
            float[][] rotations = CaptureEffectiveRotations(indexedBones, baseRotations, ikRotations);
            float[] worldMatrices = ComposeWorldMatrices(indexedBones, translations, rotations);

            var chainChangedBoneIndices = new HashSet<int>(indexedBones.Length);
            foreach (MmdIkDefinition ik in model.ik)
            {
                if (pass.HasValue && !ShouldEvaluateIkInPass(model, ik, pass.Value))
                {
                    continue;
                }

                chainChangedBoneIndices.Clear();
                SolveChain(model, result, indexedBones, indexedChildren, translations, baseRotations, ikRotations, rotations, worldMatrices, ik, chainChangedBoneIndices);
            }

            return result;
        }

        public MmdSampledMotion Solve(
            MmdModelDefinition model,
            MmdSampledMotion preAppendMotion,
            MmdSampledMotion appendedMotion)
        {
            return SolveCore(model, preAppendMotion, appendedMotion, pass: null);
        }

        public MmdSampledMotion Solve(
            MmdModelDefinition model,
            MmdSampledMotion preAppendMotion,
            MmdSampledMotion appendedMotion,
            MmdBoneEvaluationPass pass)
        {
            return SolveCore(model, preAppendMotion, appendedMotion, pass);
        }

        private MmdSampledMotion SolveCore(
            MmdModelDefinition model,
            MmdSampledMotion preAppendMotion,
            MmdSampledMotion appendedMotion,
            MmdBoneEvaluationPass? pass)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            MmdModelValidator.ThrowIfInvalid(model);
            MmdSampledMotion result = CopySampledMotion(appendedMotion);
            HashSet<int> sourceBoneIndices = SolveChains(model, result, model.ik, pass);
            return pass.HasValue
                ? MmdAppendTransformEvaluator.ReapplyAppendTransformsForSources(model, preAppendMotion, result, sourceBoneIndices, pass.Value)
                : MmdAppendTransformEvaluator.ReapplyAppendTransformsForSources(model, preAppendMotion, result, sourceBoneIndices);
        }

        public MmdSampledMotion SolveWithBreakdown(
            MmdModelDefinition model,
            MmdSampledMotion preAppendMotion,
            MmdSampledMotion appendedMotion,
            MmdIkSolveBreakdownAccumulator breakdown)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            if (breakdown == null)
            {
                throw new ArgumentNullException(nameof(breakdown));
            }

            MmdModelValidator.ThrowIfInvalid(model);
            MmdSampledMotion result = CopySampledMotion(appendedMotion);
            HashSet<int> sourceBoneIndices = SolveChains(model, result, model.ik, breakdown);
            return MmdAppendTransformEvaluator.ReapplyAppendTransformsForSources(model, preAppendMotion, result, sourceBoneIndices);
        }

        private static HashSet<int> SolveChains(
            MmdModelDefinition model,
            MmdSampledMotion result,
            IReadOnlyList<MmdIkDefinition> chains)
        {
            return SolveChains(model, result, chains, pass: null, breakdown: null);
        }

        private static HashSet<int> SolveChains(
            MmdModelDefinition model,
            MmdSampledMotion result,
            IReadOnlyList<MmdIkDefinition> chains,
            MmdBoneEvaluationPass? pass)
        {
            return SolveChains(model, result, chains, pass, breakdown: null);
        }

        private static HashSet<int> SolveChains(
            MmdModelDefinition model,
            MmdSampledMotion result,
            IReadOnlyList<MmdIkDefinition> chains,
            MmdIkSolveBreakdownAccumulator? breakdown)
        {
            return SolveChains(model, result, chains, pass: null, breakdown);
        }

        private static HashSet<int> SolveChains(
            MmdModelDefinition model,
            MmdSampledMotion result,
            IReadOnlyList<MmdIkDefinition> chains,
            MmdBoneEvaluationPass? pass,
            MmdIkSolveBreakdownAccumulator? breakdown)
        {
            long setupStarted = breakdown != null ? Stopwatch.GetTimestamp() : 0;
            MmdBoneDefinition?[] indexedBones = BuildIndexedBones(model);
            IReadOnlyList<int>[] indexedChildren = BuildIndexedChildren(indexedBones);
            float[][] translations = CaptureLocalTranslations(model, indexedBones, result);
            float[][] baseRotations = CaptureIndexedRotations(indexedBones, result);
            var ikRotations = new float[indexedBones.Length][];
            for (int i = 0; i < indexedBones.Length; i++)
            {
                ikRotations[i] = CopyQuaternion(MmdBonePoseSample.Identity.Rotation);
            }
            float[][] rotations = CaptureEffectiveRotations(indexedBones, baseRotations, ikRotations);
            float[] worldMatrices = ComposeWorldMatrices(indexedBones, translations, rotations);
            var changedBoneIndices = new HashSet<int>(indexedBones.Length);
            var chainChangedBoneIndices = new HashSet<int>(indexedBones.Length);
            if (breakdown != null)
            {
                breakdown.ChainCount = chains.Count;
                breakdown.SetupMs += ToMilliseconds(Stopwatch.GetTimestamp() - setupStarted);
            }

            foreach (MmdIkDefinition ik in chains)
            {
                if (pass.HasValue && !ShouldEvaluateIkInPass(model, ik, pass.Value))
                {
                    continue;
                }

                chainChangedBoneIndices.Clear();
                changedBoneIndices.UnionWith(SolveChain(
                    model,
                    result,
                    indexedBones,
                    indexedChildren,
                    translations,
                    baseRotations,
                    ikRotations,
                    rotations,
                    worldMatrices,
                    ik,
                    chainChangedBoneIndices,
                    breakdown));
            }

            return changedBoneIndices;
        }

        private static bool ShouldEvaluateIkInPass(MmdModelDefinition model, MmdIkDefinition ik, MmdBoneEvaluationPass pass)
        {
            MmdBoneDefinition? bone = FindBone(model, ik.boneIndex);
            if (bone == null)
            {
                return false;
            }

            return pass == MmdBoneEvaluationPass.AfterPhysics
                ? bone.deformAfterPhysics
                : !bone.deformAfterPhysics;
        }

        private static HashSet<int> SolveChain(
            MmdModelDefinition model,
            MmdSampledMotion result,
            IReadOnlyList<MmdBoneDefinition?> indexedBones,
            IReadOnlyList<int>[] indexedChildren,
            float[][] translations,
            float[][] baseRotations,
            float[][] ikRotations,
            float[][] rotations,
            float[] worldMatrices,
            MmdIkDefinition ik,
            HashSet<int> changedBoneIndices,
            MmdIkSolveBreakdownAccumulator? breakdown = null)
        {
            MmdBoneDefinition? goal = FindBone(model, ik.boneIndex);
            MmdBoneDefinition? effector = FindBone(model, ik.targetBoneIndex);
            if (goal == null || effector == null || ik.links.Count == 0)
            {
                if (breakdown != null)
                {
                    breakdown.InvalidChainCount++;
                }

                return EmptyChangedBoneIndices;
            }

            if (!IsIkEnabled(result, goal.name))
            {
                if (breakdown != null)
                {
                    breakdown.DisabledChainCount++;
                }

                return EmptyChangedBoneIndices;
            }

            int iterationCount = Math.Min(Math.Max(ik.iterationCount, 0), MaxIkLoopCount);
            MmdIkChainBreakdownAccumulator? chainBreakdown = null;
            if (breakdown != null)
            {
                breakdown.EnabledChainCount++;
                if (iterationCount == 0)
                {
                    breakdown.ZeroIterationChainCount++;
                }

                chainBreakdown = breakdown.BeginChain(
                    goal.name,
                    goal.index,
                    effector.index,
                    ik.links.Count,
                    ik.iterationCount,
                    iterationCount);
            }

            float limitAngle = ik.angleLimit > 0.0f ? ik.angleLimit : float.PositiveInfinity;
            int linkCount = ik.links.Count;
            float[][] previousEulerAngles = new float[linkCount][];
            for (int linkIndex = 0; linkIndex < linkCount; linkIndex++)
            {
                previousEulerAngles[linkIndex] = new[] { 0.0f, 0.0f, 0.0f };
            }

            float bestDistance = float.PositiveInfinity;
            float[][] savedLinkRotations = new float[linkCount][];
            for (int linkIndex = 0; linkIndex < linkCount; linkIndex++)
            {
                int boneIndex = ik.links[linkIndex].boneIndex;
                savedLinkRotations[linkIndex] = boneIndex >= 0 && boneIndex < ikRotations.Length
                    ? CopyQuaternion(ikRotations[boneIndex])
                    : CopyQuaternion(MmdBonePoseSample.Identity.Rotation);
            }

            long chainStarted = chainBreakdown != null ? Stopwatch.GetTimestamp() : 0;

            for (int iteration = 0; iteration < iterationCount; iteration++)
            {
                if (chainBreakdown != null)
                {
                    chainBreakdown.iterationsRun++;
                }

                float[] goalPosition = MatrixTranslation(worldMatrices, goal.index);
                if (Distance(MatrixTranslation(worldMatrices, effector.index), goalPosition) <= Tolerance)
                {
                    if (chainBreakdown != null)
                    {
                        chainBreakdown.earlyExitReason = "goal-reached";
                    }

                    break;
                }

                for (int linkIndex = 0; linkIndex < ik.links.Count; linkIndex++)
                {
                    if (chainBreakdown != null)
                    {
                        chainBreakdown.linkVisitCount++;
                    }

                    MmdIkLinkDefinition link = ik.links[linkIndex];
                    MmdBoneDefinition? linkBone = FindBone(model, link.boneIndex);
                    if (linkBone == null || link.boneIndex == ik.targetBoneIndex)
                    {
                        continue;
                    }

                    float[] linkPosition = MatrixTranslation(worldMatrices, linkBone.index);
                    float[] effectorPosition = MatrixTranslation(worldMatrices, effector.index);
                    long ccdStarted = chainBreakdown != null ? Stopwatch.GetTimestamp() : 0;
                    bool hasDelta = TryComputeCcdDelta(
                            goalPosition,
                            effectorPosition,
                            linkPosition,
                            worldMatrices,
                            linkBone.index,
                            linkBone,
                            link,
                            indexedBones,
                            iteration < (iterationCount >> 1),
                            limitAngle,
                            out float[] delta);
                    if (chainBreakdown != null)
                    {
                        double ccdElapsedMs = ToMilliseconds(Stopwatch.GetTimestamp() - ccdStarted);
                        chainBreakdown.ccdStepMs += ccdElapsedMs;
                        breakdown!.CcdStepMs += ccdElapsedMs;
                    }

                    if (!hasDelta)
                    {
                        continue;
                    }

                    if (chainBreakdown != null)
                    {
                        chainBreakdown.ccdStepCount++;
                    }

                    float[] nextRotation = MmdQuaternionMath.Multiply(
                        MmdQuaternionMath.Multiply(ikRotations[linkBone.index], baseRotations[linkBone.index]),
                        delta);
                    if (link.hasLimit)
                    {
                        long clampStarted = chainBreakdown != null ? Stopwatch.GetTimestamp() : 0;
                        nextRotation = ClampToLinkLimit(
                            nextRotation,
                            link,
                            previousEulerAngles[linkIndex],
                            iteration < (iterationCount >> 1));
                        if (chainBreakdown != null)
                        {
                            double clampElapsedMs = ToMilliseconds(Stopwatch.GetTimestamp() - clampStarted);
                            chainBreakdown.limitClampCount++;
                            chainBreakdown.limitClampMs += clampElapsedMs;
                            breakdown!.LimitClampMs += clampElapsedMs;
                        }
                    }

                    ikRotations[linkBone.index] = MmdQuaternionMath.Multiply(nextRotation, Conjugate(baseRotations[linkBone.index]));
                    ApplyEffectiveRotation(result, rotations, linkBone, baseRotations, ikRotations);
                    changedBoneIndices.Add(linkBone.index);
                    if (chainBreakdown != null)
                    {
                        chainBreakdown.linkAdjustmentCount++;
                    }

                    long worldUpdateStarted = chainBreakdown != null ? Stopwatch.GetTimestamp() : 0;
                    ComposeWorldMatricesFromBoneInto(linkBone.index, indexedBones, indexedChildren, translations, rotations, worldMatrices);
                    if (chainBreakdown != null)
                    {
                        double worldUpdateElapsedMs = ToMilliseconds(Stopwatch.GetTimestamp() - worldUpdateStarted);
                        chainBreakdown.worldUpdateCount++;
                        chainBreakdown.worldUpdateMs += worldUpdateElapsedMs;
                        breakdown!.WorldUpdateMs += worldUpdateElapsedMs;
                    }
                }

                float currentDistance = Distance(
                    MatrixTranslation(worldMatrices, effector.index),
                    MatrixTranslation(worldMatrices, goal.index));
                if (currentDistance < bestDistance)
                {
                    float improvement = bestDistance - currentDistance;
                    bestDistance = currentDistance;
                    for (int linkIndex = 0; linkIndex < ik.links.Count; linkIndex++)
                    {
                        int boneIndex = ik.links[linkIndex].boneIndex;
                        if (boneIndex >= 0 && boneIndex < ikRotations.Length)
                        {
                            savedLinkRotations[linkIndex] = CopyQuaternion(ikRotations[boneIndex]);
                        }
                    }

                    if (improvement <= ConvergedDistanceImprovementTolerance)
                    {
                        if (chainBreakdown != null)
                        {
                            chainBreakdown.earlyExitReason = "converged";
                        }

                        break;
                    }
                }
                else
                {
                    RestoreLinkRotations(model, result, baseRotations, ikRotations, rotations, ik, savedLinkRotations);
                    long fullRebuildStarted = chainBreakdown != null ? Stopwatch.GetTimestamp() : 0;
                    ComposeWorldMatricesInto(indexedBones, translations, rotations, worldMatrices);
                    if (chainBreakdown != null)
                    {
                        double fullRebuildElapsedMs = ToMilliseconds(Stopwatch.GetTimestamp() - fullRebuildStarted);
                        chainBreakdown.rollbackCount++;
                        chainBreakdown.fullWorldRebuildCount++;
                        chainBreakdown.fullWorldRebuildMs += fullRebuildElapsedMs;
                        breakdown!.FullWorldRebuildMs += fullRebuildElapsedMs;
                        chainBreakdown.earlyExitReason = "rollback";
                    }

                    break;
                }
            }

            if (chainBreakdown != null)
            {
                double chainElapsedMs = ToMilliseconds(Stopwatch.GetTimestamp() - chainStarted);
                chainBreakdown.chainSolveMs += chainElapsedMs;
                breakdown!.ChainSolveMs += chainElapsedMs;
            }

            return changedBoneIndices;
        }

        private static double ToMilliseconds(long ticks)
        {
            return ticks * 1000.0 / Stopwatch.Frequency;
        }

        private static bool IsIkEnabled(MmdSampledMotion result, string ikName)
        {
            return !result.IkStates.TryGetValue(ikName, out bool enabled) || enabled;
        }

        private static MmdSampledMotion CopySampledMotion(MmdSampledMotion? sampledMotion)
        {
            var result = new MmdSampledMotion();
            if (sampledMotion == null)
            {
                return result;
            }

            var keys = new List<string>(sampledMotion.Bones.Keys);
            keys.Sort(StringComparer.Ordinal);
            foreach (string key in keys)
            {
                result.Bones[key] = sampledMotion.Bones[key];
            }

            keys.Clear();
            foreach (string key in sampledMotion.Morphs.Keys)
            {
                keys.Add(key);
            }
            keys.Sort(StringComparer.Ordinal);
            foreach (string key in keys)
            {
                result.Morphs[key] = sampledMotion.Morphs[key];
            }

            keys.Clear();
            foreach (string key in sampledMotion.IkStates.Keys)
            {
                keys.Add(key);
            }
            keys.Sort(StringComparer.Ordinal);
            foreach (string key in keys)
            {
                result.IkStates[key] = sampledMotion.IkStates[key];
            }

            return result;
        }

        private static bool TryGetSingleLimitAxis(MmdIkLinkDefinition link, out int axis)
        {
            axis = -1;
            if (link.minimumAngle.Length < 3 || link.maximumAngle.Length < 3)
            {
                return false;
            }

            for (int index = 0; index < 3; index++)
            {
                bool active = MathF.Abs(link.minimumAngle[index]) > 1e-6f || MathF.Abs(link.maximumAngle[index]) > 1e-6f;
                if (!active)
                {
                    continue;
                }

                if (axis >= 0)
                {
                    axis = -1;
                    return false;
                }

                axis = index;
            }

            return axis >= 0;
        }

        private static void RestoreLinkRotations(
            MmdModelDefinition model,
            MmdSampledMotion result,
            IReadOnlyList<float[]> baseRotations,
            IList<float[]> ikRotations,
            IList<float[]> rotations,
            MmdIkDefinition ik,
            IReadOnlyList<float[]> savedLinkRotations)
        {
            for (int linkIndex = 0; linkIndex < ik.links.Count; linkIndex++)
            {
                MmdBoneDefinition? linkBone = FindBone(model, ik.links[linkIndex].boneIndex);
                if (linkBone == null || linkBone.index < 0 || linkBone.index >= rotations.Count)
                {
                    continue;
                }

                ikRotations[linkBone.index] = CopyQuaternion(savedLinkRotations[linkIndex]);
                ApplyEffectiveRotation(result, rotations, linkBone, baseRotations, ikRotations);
            }
        }

        private static bool TryComputeCcdDelta(
            float[] goalPosition,
            float[] effectorPosition,
            float[] linkPosition,
            float[] worldMatrices,
            int linkBoneIndex,
            MmdBoneDefinition linkBone,
            MmdIkLinkDefinition link,
            IReadOnlyList<MmdBoneDefinition?> indexedBones,
            bool useAxis,
            float limitAngle,
            out float[] delta)
        {
            float[] effectorVector = Subtract(effectorPosition, linkPosition);
            float[] goalVector = Subtract(goalPosition, linkPosition);
            if (Length(effectorVector) < 1e-5f || Length(goalVector) < 1e-5f)
            {
                delta = MmdBonePoseSample.Identity.Rotation;
                return false;
            }

            float[] worldToEffector = Normalize(effectorVector);
            float[] worldToGoal = Normalize(goalVector);
            float[] toEffector = Normalize(TransformDirectionByInverseMatrix(effectorVector, worldMatrices, linkBoneIndex));
            float[] toGoal = Normalize(TransformDirectionByInverseMatrix(goalVector, worldMatrices, linkBoneIndex));
            if (Length(toEffector) < 1e-5f || Length(toGoal) < 1e-5f)
            {
                delta = MmdBonePoseSample.Identity.Rotation;
                return false;
            }

            float dot = Math.Clamp(Dot(toEffector, toGoal), -1.0f, 1.0f);
            float angle = MathF.Acos(dot);
            if (angle < 1e-5f)
            {
                delta = MmdBonePoseSample.Identity.Rotation;
                return false;
            }

            angle = Math.Min(angle, limitAngle);
            float[] localCross = Cross(toEffector, toGoal);
            float[] worldCross = Cross(worldToEffector, worldToGoal);
            float[] axis = ResolveCcdAxis(linkBone, link, localCross, worldCross, worldMatrices, indexedBones, useAxis);
            if (Length(axis) < 1e-5f)
            {
                delta = MmdBonePoseSample.Identity.Rotation;
                return false;
            }

            delta = AxisAngleQuaternion(axis, angle);
            return true;
        }

        private static float[] ResolveCcdAxis(
            MmdBoneDefinition linkBone,
            MmdIkLinkDefinition link,
            float[] localCross,
            float[] worldCross,
            float[] worldMatrices,
            IReadOnlyList<MmdBoneDefinition?> indexedBones,
            bool useAxis)
        {
            if (linkBone.fixedAxis && linkBone.fixedAxisVector.Length >= 3)
            {
                return Normalize(new[] { linkBone.fixedAxisVector[0], linkBone.fixedAxisVector[1], linkBone.fixedAxisVector[2] });
            }

            if (link.hasLimit && useAxis && TryGetSingleLimitAxis(link, out int limitAxis))
            {
                float[] parentAxis = ParentWorldAxis(linkBone, worldMatrices, indexedBones, limitAxis);
                float sign = Dot(worldCross, parentAxis) >= 0.0f ? 1.0f : -1.0f;
                return Scale(AxisVector(limitAxis), sign);
            }

            return Normalize(localCross);
        }

        private static float[] ClampToLinkLimit(float[] rotation, MmdIkLinkDefinition link, float[] previousEuler, bool useAxis)
        {
            if (link.minimumAngle.Length < 3 || link.maximumAngle.Length < 3)
            {
                return rotation;
            }

            EulerRotationOrder order = ResolveRotationOrder(link);
            float[] euler = DecomposeEuler(QuaternionToRotation3(rotation), order);
            float[] clamped =
            {
                LimitAngle(euler[0], link.minimumAngle[0], link.maximumAngle[0], useAxis),
                LimitAngle(euler[1], link.minimumAngle[1], link.maximumAngle[1], useAxis),
                LimitAngle(euler[2], link.minimumAngle[2], link.maximumAngle[2], useAxis)
            };
            previousEuler[0] = clamped[0];
            previousEuler[1] = clamped[1];
            previousEuler[2] = clamped[2];
            return EulerToQuaternion(clamped[0], clamped[1], clamped[2], order);
        }

        private static MmdBoneDefinition?[] BuildIndexedBones(MmdModelDefinition model)
        {
            int maxIndex = -1;
            for (int i = 0; i < model.bones.Count; i++)
            {
                if (model.bones[i].index > maxIndex)
                {
                    maxIndex = model.bones[i].index;
                }
            }

            var bones = new MmdBoneDefinition?[Math.Max(maxIndex + 1, 0)];
            foreach (MmdBoneDefinition bone in model.bones)
            {
                if (bone.index >= 0 && bone.index < bones.Length)
                {
                    bones[bone.index] = bone;
                }
            }

            return bones;
        }

        private static IReadOnlyList<int>[] BuildIndexedChildren(IReadOnlyList<MmdBoneDefinition?> indexedBones)
        {
            var children = new IReadOnlyList<int>[indexedBones.Count];
            var mutableChildren = new List<int>[indexedBones.Count];
            for (int index = 0; index < indexedBones.Count; index++)
            {
                mutableChildren[index] = new List<int>();
            }

            for (int index = 0; index < indexedBones.Count; index++)
            {
                int parentIndex = indexedBones[index]?.parentIndex ?? -1;
                if (parentIndex >= 0 && parentIndex < indexedBones.Count)
                {
                    mutableChildren[parentIndex].Add(index);
                }
            }

            for (int index = 0; index < indexedBones.Count; index++)
            {
                children[index] = mutableChildren[index];
            }

            return children;
        }

        private static float[][] CaptureLocalTranslations(
            MmdModelDefinition model,
            IReadOnlyList<MmdBoneDefinition?> indexedBones,
            MmdSampledMotion result)
        {
            var translations = new float[indexedBones.Count][];
            for (int i = 0; i < indexedBones.Count; i++)
            {
                translations[i] = new[] { 0.0f, 0.0f, 0.0f };
            }

            for (int index = 0; index < indexedBones.Count; index++)
            {
                MmdBoneDefinition? bone = indexedBones[index];
                if (bone == null)
                {
                    continue;
                }

                MmdBonePoseSample pose = result.Bones.TryGetValue(bone.name, out MmdBonePoseSample found)
                    ? found
                    : MmdBonePoseSample.Identity;
                translations[index] = MmdPoseEvaluator.GetLocalTranslation(model, bone, pose);
            }

            return translations;
        }

        private static float[][] CaptureIndexedRotations(
            IReadOnlyList<MmdBoneDefinition?> indexedBones,
            MmdSampledMotion result)
        {
            var rotations = new float[indexedBones.Count][];
            for (int i = 0; i < indexedBones.Count; i++)
            {
                rotations[i] = CopyQuaternion(MmdBonePoseSample.Identity.Rotation);
            }

            for (int index = 0; index < indexedBones.Count; index++)
            {
                MmdBoneDefinition? bone = indexedBones[index];
                if (bone == null)
                {
                    continue;
                }

                rotations[index] = result.Bones.TryGetValue(bone.name, out MmdBonePoseSample pose)
                    ? CopyQuaternion(pose.Rotation)
                    : CopyQuaternion(MmdBonePoseSample.Identity.Rotation);
            }

            return rotations;
        }

        private static float[][] CaptureEffectiveRotations(
            IReadOnlyList<MmdBoneDefinition?> indexedBones,
            IReadOnlyList<float[]> baseRotations,
            IReadOnlyList<float[]> ikRotations)
        {
            var rotations = new float[indexedBones.Count][];
            for (int i = 0; i < indexedBones.Count; i++)
            {
                rotations[i] = CopyQuaternion(MmdBonePoseSample.Identity.Rotation);
            }

            for (int index = 0; index < indexedBones.Count; index++)
            {
                if (indexedBones[index] != null)
                {
                    rotations[index] = MmdQuaternionMath.Multiply(ikRotations[index], baseRotations[index]);
                }
            }

            return rotations;
        }

        private static void ApplyEffectiveRotation(
            MmdSampledMotion result,
            IList<float[]> rotations,
            MmdBoneDefinition bone,
            IReadOnlyList<float[]> baseRotations,
            IList<float[]> ikRotations)
        {
            rotations[bone.index] = MmdQuaternionMath.Multiply(ikRotations[bone.index], baseRotations[bone.index]);
            WritePoseRotation(result, bone, rotations[bone.index]);
        }

        private static void WritePoseRotation(MmdSampledMotion result, MmdBoneDefinition bone, float[] rotation)
        {
            MmdBonePoseSample current = result.Bones.TryGetValue(bone.name, out MmdBonePoseSample pose)
                ? pose
                : MmdBonePoseSample.Identity;
            result.Bones[bone.name] = new MmdBonePoseSample(current.Translation, rotation);
        }

        private static MmdBoneDefinition? FindBone(MmdModelDefinition model, int index)
        {
            for (int i = 0; i < model.bones.Count; i++)
            {
                if (model.bones[i].index == index)
                {
                    return model.bones[i];
                }
            }

            return null;
        }

        private static float[] MatrixTranslation(float[] matrices, int index)
        {
            int offset = index * 16;
            if (index < 0 || offset + 14 >= matrices.Length)
            {
                return new[] { 0.0f, 0.0f, 0.0f };
            }

            return new[] { matrices[offset + 12], matrices[offset + 13], matrices[offset + 14] };
        }

        private static float[] ParentWorldAxis(
            MmdBoneDefinition linkBone,
            float[] matrices,
            IReadOnlyList<MmdBoneDefinition?> indexedBones,
            int axisIndex)
        {
            int parentIndex = linkBone.parentIndex;
            if (parentIndex < 0 || parentIndex >= indexedBones.Count || indexedBones[parentIndex] == null)
            {
                return AxisVector(axisIndex);
            }

            int offset = parentIndex * 16;
            int columnOffset = offset + axisIndex * 4;
            if (columnOffset + 2 >= matrices.Length)
            {
                return AxisVector(axisIndex);
            }

            return Normalize(new[] { matrices[columnOffset], matrices[columnOffset + 1], matrices[columnOffset + 2] });
        }

        private static float[] TransformDirectionByInverseMatrix(float[] vector, float[] matrices, int boneIndex)
        {
            int offset = boneIndex * 16;
            if (boneIndex < 0 || offset + 10 >= matrices.Length)
            {
                return new[] { 0.0f, 0.0f, 0.0f };
            }

            return new[]
            {
                vector[0] * matrices[offset] + vector[1] * matrices[offset + 1] + vector[2] * matrices[offset + 2],
                vector[0] * matrices[offset + 4] + vector[1] * matrices[offset + 5] + vector[2] * matrices[offset + 6],
                vector[0] * matrices[offset + 8] + vector[1] * matrices[offset + 9] + vector[2] * matrices[offset + 10]
            };
        }

        private static float[] QuaternionToRotation3(float[] rotation)
        {
            float[] q = NormalizeQuaternion(rotation);
            float x = q[0];
            float y = q[1];
            float z = q[2];
            float w = q[3];
            float x2 = x + x;
            float y2 = y + y;
            float z2 = z + z;
            float xx = x * x2;
            float xy = x * y2;
            float xz = x * z2;
            float yy = y * y2;
            float yz = y * z2;
            float zz = z * z2;
            float wx = w * x2;
            float wy = w * y2;
            float wz = w * z2;

            return new[]
            {
                1.0f - (yy + zz),
                xy + wz,
                xz - wy,
                xy - wz,
                1.0f - (xx + zz),
                yz + wx,
                xz + wy,
                yz - wx,
                1.0f - (xx + yy)
            };
        }

        private static EulerRotationOrder ResolveRotationOrder(MmdIkLinkDefinition link)
        {
            float minX = Math.Min(link.minimumAngle[0], link.maximumAngle[0]);
            float maxX = Math.Max(link.minimumAngle[0], link.maximumAngle[0]);
            float minY = Math.Min(link.minimumAngle[1], link.maximumAngle[1]);
            float maxY = Math.Max(link.minimumAngle[1], link.maximumAngle[1]);
            if (-HalfPi < minX && maxX < HalfPi)
            {
                return EulerRotationOrder.Yxz;
            }

            if (-HalfPi < minY && maxY < HalfPi)
            {
                return EulerRotationOrder.Zyx;
            }

            return EulerRotationOrder.Xzy;
        }

        private static float[] DecomposeEuler(float[] matrix, EulerRotationOrder order)
        {
            return order switch
            {
                EulerRotationOrder.Yxz => DecomposeEulerYxz(matrix),
                EulerRotationOrder.Zyx => DecomposeEulerZyx(matrix),
                _ => DecomposeEulerXzy(matrix)
            };
        }

        private static float[] DecomposeEulerYxz(float[] matrix)
        {
            float x = MathF.Asin(Math.Clamp(-matrix[7], -1.0f, 1.0f));
            x = ClampRotationOrderSingularity(x);
            float inverseCosX = InverseCos(x);
            return new[]
            {
                x,
                MathF.Atan2(matrix[6] * inverseCosX, matrix[8] * inverseCosX),
                MathF.Atan2(matrix[1] * inverseCosX, matrix[4] * inverseCosX)
            };
        }

        private static float[] DecomposeEulerZyx(float[] matrix)
        {
            float y = MathF.Asin(Math.Clamp(-matrix[2], -1.0f, 1.0f));
            y = ClampRotationOrderSingularity(y);
            float inverseCosY = InverseCos(y);
            return new[]
            {
                MathF.Atan2(matrix[5] * inverseCosY, matrix[8] * inverseCosY),
                y,
                MathF.Atan2(matrix[1] * inverseCosY, matrix[0] * inverseCosY)
            };
        }

        private static float[] DecomposeEulerXzy(float[] matrix)
        {
            float z = MathF.Asin(Math.Clamp(-matrix[3], -1.0f, 1.0f));
            z = ClampRotationOrderSingularity(z);
            float inverseCosZ = InverseCos(z);
            return new[]
            {
                MathF.Atan2(matrix[5] * inverseCosZ, matrix[4] * inverseCosZ),
                MathF.Atan2(matrix[6] * inverseCosZ, matrix[0] * inverseCosZ),
                z
            };
        }

        private static float ClampRotationOrderSingularity(float value)
        {
            if (MathF.Abs(value) <= RotationOrderThreshold)
            {
                return value;
            }

            return value < 0.0f ? -RotationOrderThreshold : RotationOrderThreshold;
        }

        private static float InverseCos(float angle)
        {
            float cos = MathF.Cos(angle);
            return cos == 0.0f ? 1.0f : 1.0f / cos;
        }

        private static float LimitAngle(float angle, float minimum, float maximum, bool useAxis)
        {
            float min = Math.Min(minimum, maximum);
            float max = Math.Max(minimum, maximum);
            if (angle < min)
            {
                float diff = 2.0f * min - angle;
                return diff <= max && useAxis ? diff : min;
            }

            if (angle > max)
            {
                float diff = 2.0f * max - angle;
                return diff >= min && useAxis ? diff : max;
            }

            return angle;
        }

        private static float[] EulerToQuaternion(float x, float y, float z, EulerRotationOrder order)
        {
            float[] qx = AxisAngleQuaternion(AxisVector(0), x);
            float[] qy = AxisAngleQuaternion(AxisVector(1), y);
            float[] qz = AxisAngleQuaternion(AxisVector(2), z);
            return order switch
            {
                EulerRotationOrder.Yxz => MmdQuaternionMath.Multiply(MmdQuaternionMath.Multiply(qy, qx), qz),
                EulerRotationOrder.Zyx => MmdQuaternionMath.Multiply(MmdQuaternionMath.Multiply(qz, qy), qx),
                _ => MmdQuaternionMath.Multiply(MmdQuaternionMath.Multiply(qx, qz), qy)
            };
        }

        private static float[] DecomposeEulerXyz(float[] matrix, float[] previous)
        {
            float sy = -matrix[2];
            float[] result;
            if (1.0f - MathF.Abs(sy) < 1e-6f)
            {
                result = new[] { 0.0f, MathF.Asin(sy), 0.0f };
                float sx = MathF.Sin(previous[0]);
                float sz = MathF.Sin(previous[2]);
                if (MathF.Abs(sx) < MathF.Abs(sz))
                {
                    float cx = MathF.Cos(previous[0]);
                    if (cx > 0.0f)
                    {
                        result[0] = 0.0f;
                        result[2] = MathF.Asin(-matrix[3]);
                    }
                    else
                    {
                        result[0] = MathF.PI;
                        result[2] = MathF.Asin(matrix[3]);
                    }
                }
                else
                {
                    float cz = MathF.Cos(previous[2]);
                    if (cz > 0.0f)
                    {
                        result[2] = 0.0f;
                        result[0] = MathF.Asin(-matrix[7]);
                    }
                    else
                    {
                        result[2] = MathF.PI;
                        result[0] = MathF.Asin(matrix[7]);
                    }
                }
            }
            else
            {
                result = new[]
                {
                    MathF.Atan2(matrix[5], matrix[8]),
                    MathF.Asin(sy),
                    MathF.Atan2(matrix[1], matrix[0])
                };
            }

            return ClosestEuler(result, previous);
        }

        private static float[] ClosestEuler(float[] euler, float[] previous)
        {
            float pi = MathF.PI;
            float[][] candidates =
            {
                euler,
                new[] { euler[0] + pi, pi - euler[1], euler[2] + pi },
                new[] { euler[0] + pi, pi - euler[1], euler[2] - pi },
                new[] { euler[0] + pi, -pi - euler[1], euler[2] + pi },
                new[] { euler[0] + pi, -pi - euler[1], euler[2] - pi },
                new[] { euler[0] - pi, pi - euler[1], euler[2] + pi },
                new[] { euler[0] - pi, pi - euler[1], euler[2] - pi },
                new[] { euler[0] - pi, -pi - euler[1], euler[2] + pi },
                new[] { euler[0] - pi, -pi - euler[1], euler[2] - pi }
            };
            float[] best = candidates[0];
            float bestScore = MathF.Abs(DiffAngle(candidates[0][0], previous[0]))
                + MathF.Abs(DiffAngle(candidates[0][1], previous[1]))
                + MathF.Abs(DiffAngle(candidates[0][2], previous[2]));
            for (int i = 1; i < candidates.Length; i++)
            {
                float score = MathF.Abs(DiffAngle(candidates[i][0], previous[0]))
                    + MathF.Abs(DiffAngle(candidates[i][1], previous[1]))
                    + MathF.Abs(DiffAngle(candidates[i][2], previous[2]));
                if (score < bestScore)
                {
                    bestScore = score;
                    best = candidates[i];
                }
            }

            return best;
        }

        private static float[] EulerXyzToQuaternion(float x, float y, float z)
        {
            float c1 = MathF.Cos(x * 0.5f);
            float c2 = MathF.Cos(y * 0.5f);
            float c3 = MathF.Cos(z * 0.5f);
            float s1 = MathF.Sin(x * 0.5f);
            float s2 = MathF.Sin(y * 0.5f);
            float s3 = MathF.Sin(z * 0.5f);

            return NormalizeQuaternion(new[]
            {
                s1 * c2 * c3 + c1 * s2 * s3,
                c1 * s2 * c3 - s1 * c2 * s3,
                c1 * c2 * s3 + s1 * s2 * c3,
                c1 * c2 * c3 - s1 * s2 * s3
            });
        }

        private static float DiffAngle(float left, float right)
        {
            float diff = NormalizeAngle(left) - NormalizeAngle(right);
            if (diff > MathF.PI)
            {
                return diff - MathF.PI * 2.0f;
            }

            if (diff < -MathF.PI)
            {
                return diff + MathF.PI * 2.0f;
            }

            return diff;
        }

        private static float NormalizeAngle(float angle)
        {
            float result = angle;
            while (result >= MathF.PI * 2.0f)
            {
                result -= MathF.PI * 2.0f;
            }

            while (result < 0.0f)
            {
                result += MathF.PI * 2.0f;
            }

            return result;
        }

        private static float[] AxisAngleQuaternion(float[] axis, float angle)
        {
            float[] normalizedAxis = Normalize(axis);
            float half = angle * 0.5f;
            float scale = MathF.Sin(half);
            return NormalizeQuaternion(new[]
            {
                normalizedAxis[0] * scale,
                normalizedAxis[1] * scale,
                normalizedAxis[2] * scale,
                MathF.Cos(half)
            });
        }

        private static float[] AxisVector(int axisIndex)
        {
            return axisIndex switch
            {
                0 => new[] { 1.0f, 0.0f, 0.0f },
                1 => new[] { 0.0f, 1.0f, 0.0f },
                _ => new[] { 0.0f, 0.0f, 1.0f }
            };
        }

        private static float[] ComposeWorldMatrices(
            IReadOnlyList<MmdBoneDefinition?> indexedBones,
            IReadOnlyList<float[]> translations,
            IReadOnlyList<float[]> rotations)
        {
            float[] matrices = new float[indexedBones.Count * 16];
            ComposeWorldMatricesInto(indexedBones, translations, rotations, matrices);
            return matrices;
        }

        private static void ComposeWorldMatricesInto(
            IReadOnlyList<MmdBoneDefinition?> indexedBones,
            IReadOnlyList<float[]> translations,
            IReadOnlyList<float[]> rotations,
            float[] matrices)
        {
            var local = new float[16];
            if (!IsParentBeforeChildOrdered(indexedBones))
            {
                var states = new byte[indexedBones.Count];
                for (int index = 0; index < indexedBones.Count; index++)
                {
                    ComposeWorldMatrix(index, indexedBones, translations, rotations, matrices, states);
                }

                return;
            }

            for (int index = 0; index < indexedBones.Count; index++)
            {
                MmdBoneDefinition? bone = indexedBones[index];
                ComposeColumnMajorMatrixInto(
                    bone == null ? new[] { 0.0f, 0.0f, 0.0f } : translations[index],
                    bone == null ? MmdBonePoseSample.Identity.Rotation : rotations[index],
                    local);

                int targetOffset = index * 16;
                int parentIndex = bone?.parentIndex ?? -1;
                if (parentIndex >= 0 && parentIndex < indexedBones.Count)
                {
                    MultiplyColumnMajorMatricesInto(matrices, parentIndex * 16, local, 0, matrices, targetOffset);
                }
                else
                {
                    Array.Copy(local, 0, matrices, targetOffset, 16);
                }
            }
        }

        private static void ComposeWorldMatricesFromBoneInto(
            int rootIndex,
            IReadOnlyList<MmdBoneDefinition?> indexedBones,
            IReadOnlyList<int>[] indexedChildren,
            IReadOnlyList<float[]> translations,
            IReadOnlyList<float[]> rotations,
            float[] matrices)
        {
            if (rootIndex < 0 || rootIndex >= indexedBones.Count)
            {
                return;
            }

            ComposeSingleWorldMatrixInto(rootIndex, indexedBones, translations, rotations, matrices);
            foreach (int childIndex in indexedChildren[rootIndex])
            {
                ComposeWorldMatricesFromBoneInto(childIndex, indexedBones, indexedChildren, translations, rotations, matrices);
            }
        }

        private static void ComposeSingleWorldMatrixInto(
            int index,
            IReadOnlyList<MmdBoneDefinition?> indexedBones,
            IReadOnlyList<float[]> translations,
            IReadOnlyList<float[]> rotations,
            float[] matrices)
        {
            MmdBoneDefinition? bone = indexedBones[index];
            var local = new float[16];
            ComposeColumnMajorMatrixInto(
                bone == null ? new[] { 0.0f, 0.0f, 0.0f } : translations[index],
                bone == null ? MmdBonePoseSample.Identity.Rotation : rotations[index],
                local);

            int targetOffset = index * 16;
            int parentIndex = bone?.parentIndex ?? -1;
            if (parentIndex >= 0 && parentIndex < indexedBones.Count)
            {
                MultiplyColumnMajorMatricesInto(matrices, parentIndex * 16, local, 0, matrices, targetOffset);
            }
            else
            {
                Array.Copy(local, 0, matrices, targetOffset, 16);
            }
        }

        private static bool IsParentBeforeChildOrdered(IReadOnlyList<MmdBoneDefinition?> indexedBones)
        {
            for (int index = 0; index < indexedBones.Count; index++)
            {
                int parentIndex = indexedBones[index]?.parentIndex ?? -1;
                if (parentIndex >= index)
                {
                    return false;
                }
            }

            return true;
        }

        private static void ComposeWorldMatrix(
            int index,
            IReadOnlyList<MmdBoneDefinition?> indexedBones,
            IReadOnlyList<float[]> translations,
            IReadOnlyList<float[]> rotations,
            float[] matrices,
            byte[] states)
        {
            if (index < 0 || index >= indexedBones.Count || states[index] == 2)
            {
                return;
            }

            if (states[index] == 1)
            {
                throw new InvalidOperationException($"Bone parent cycle detected at index {index}.");
            }

            states[index] = 1;
            MmdBoneDefinition? bone = indexedBones[index];
            int parentIndex = bone?.parentIndex ?? -1;
            if (parentIndex >= 0 && parentIndex < indexedBones.Count)
            {
                ComposeWorldMatrix(parentIndex, indexedBones, translations, rotations, matrices, states);
            }

            var localMatrix = new float[16];
            ComposeColumnMajorMatrixInto(
                bone == null ? new[] { 0.0f, 0.0f, 0.0f } : translations[index],
                bone == null ? MmdBonePoseSample.Identity.Rotation : rotations[index],
                localMatrix);

            int targetOffset = index * 16;
            if (parentIndex >= 0 && parentIndex < indexedBones.Count)
            {
                MultiplyColumnMajorMatricesInto(matrices, parentIndex * 16, localMatrix, 0, matrices, targetOffset);
            }
            else
            {
                Array.Copy(localMatrix, 0, matrices, targetOffset, 16);
            }

            states[index] = 2;
        }

        private static void ComposeColumnMajorMatrixInto(float[] translation, float[] rotation, float[] target)
        {
            float[] q = NormalizeQuaternion(rotation);
            float x = q[0];
            float y = q[1];
            float z = q[2];
            float w = q[3];
            float x2 = x + x;
            float y2 = y + y;
            float z2 = z + z;
            float xx = x * x2;
            float xy = x * y2;
            float xz = x * z2;
            float yy = y * y2;
            float yz = y * z2;
            float zz = z * z2;
            float wx = w * x2;
            float wy = w * y2;
            float wz = w * z2;
            target[0] = 1.0f - (yy + zz);
            target[1] = xy + wz;
            target[2] = xz - wy;
            target[3] = 0.0f;
            target[4] = xy - wz;
            target[5] = 1.0f - (xx + zz);
            target[6] = yz + wx;
            target[7] = 0.0f;
            target[8] = xz + wy;
            target[9] = yz - wx;
            target[10] = 1.0f - (xx + yy);
            target[11] = 0.0f;
            target[12] = translation[0];
            target[13] = translation[1];
            target[14] = translation[2];
            target[15] = 1.0f;
        }

        private static void MultiplyColumnMajorMatricesInto(
            float[] left,
            int leftOffset,
            float[] right,
            int rightOffset,
            float[] target,
            int targetOffset)
        {
            var result = new float[16];
            for (int column = 0; column < 4; column++)
            {
                for (int row = 0; row < 4; row++)
                {
                    result[column * 4 + row] =
                        left[leftOffset + row] * right[rightOffset + column * 4]
                        + left[leftOffset + 4 + row] * right[rightOffset + column * 4 + 1]
                        + left[leftOffset + 8 + row] * right[rightOffset + column * 4 + 2]
                        + left[leftOffset + 12 + row] * right[rightOffset + column * 4 + 3];
                }
            }

            Array.Copy(result, 0, target, targetOffset, 16);
        }

        private static float[] NormalizeQuaternion(float[] rotation)
        {
            float length = MathF.Sqrt(
                rotation[0] * rotation[0]
                + rotation[1] * rotation[1]
                + rotation[2] * rotation[2]
                + rotation[3] * rotation[3]);
            if (length <= 0.000001f || !IsFinite(length))
            {
                return MmdBonePoseSample.Identity.Rotation;
            }

            float inverse = 1.0f / length;
            return new[] { rotation[0] * inverse, rotation[1] * inverse, rotation[2] * inverse, rotation[3] * inverse };
        }

        private static float[] Conjugate(float[] rotation)
        {
            return new[]
            {
                rotation.Length > 0 ? -rotation[0] : 0.0f,
                rotation.Length > 1 ? -rotation[1] : 0.0f,
                rotation.Length > 2 ? -rotation[2] : 0.0f,
                rotation.Length > 3 ? rotation[3] : 1.0f
            };
        }

        private static float[] CopyQuaternion(float[] rotation)
        {
            return new[]
            {
                rotation.Length > 0 ? rotation[0] : 0.0f,
                rotation.Length > 1 ? rotation[1] : 0.0f,
                rotation.Length > 2 ? rotation[2] : 0.0f,
                rotation.Length > 3 ? rotation[3] : 1.0f
            };
        }

        private static float Distance(float[] left, float[] right)
        {
            return Length(Subtract(left, right));
        }

        private static float Length(float[] value)
        {
            return MathF.Sqrt(Dot(value, value));
        }

        private static float Dot(float[] left, float[] right)
        {
            return left[0] * right[0] + left[1] * right[1] + left[2] * right[2];
        }

        private static float[] Cross(float[] left, float[] right)
        {
            return new[]
            {
                left[1] * right[2] - left[2] * right[1],
                left[2] * right[0] - left[0] * right[2],
                left[0] * right[1] - left[1] * right[0]
            };
        }

        private static float[] Subtract(float[] left, float[] right)
        {
            return new[] { left[0] - right[0], left[1] - right[1], left[2] - right[2] };
        }

        private static float[] Scale(float[] value, float scale)
        {
            return new[] { value[0] * scale, value[1] * scale, value[2] * scale };
        }

        private static float[] Normalize(float[] value)
        {
            float length = Length(value);
            if (length <= 0.000001f || !IsFinite(length))
            {
                return new[] { 0.0f, 0.0f, 0.0f };
            }

            float inverse = 1.0f / length;
            return new[] { value[0] * inverse, value[1] * inverse, value[2] * inverse };
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
