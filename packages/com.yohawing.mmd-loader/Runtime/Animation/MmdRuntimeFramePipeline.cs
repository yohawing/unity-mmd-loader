#nullable enable

using System;
using System.Collections.Generic;
using Mmd.Motion;
using Mmd.Parser;
using Mmd.Physics;
using Mmd.Pose;

namespace Mmd
{
    internal sealed class MmdRuntimeFrameEvaluation
    {
        public MmdSampledMotion SampledMotion { get; init; } = new();
        public Dictionary<int, float[]>? SampledWorldMatrices { get; init; }
        public MmdSampledMotion AppendedMotion { get; init; } = new();
        public Dictionary<int, float[]>? AppendedWorldMatrices { get; init; }
        public MmdSampledMotion IkMotion { get; init; } = new();
        public Dictionary<int, float[]>? IkWorldMatrices { get; init; }
        public MmdSampledMotion FinalMotion { get; init; } = new();
        public Dictionary<int, float[]> WorldMatrices { get; init; } = new();
    }

    internal static class MmdRuntimeFramePipeline
    {
        internal static bool IsSourceLess(MmdModelDefinition? model, MmdMotionDefinition? motion)
        {
            return model?.sourceBytes == null && motion?.sourceBytes == null;
        }

        public static MmdRuntimeFrameEvaluation Evaluate(
            MmdModelDefinition model,
            MmdMotionDefinition motion,
            int frame,
            IMmdPhysicsBackend physicsBackend,
            IMmdIkSolver? ikSolver = null)
        {
            return EvaluateWithOptions(
                model,
                motion,
                frame,
                physicsBackend,
                ikSolver,
                captureCheckpoints: true);
        }

        internal static MmdRuntimeFrameEvaluation EvaluateWithOptions(
            MmdModelDefinition model,
            MmdMotionDefinition motion,
            int frame,
            IMmdPhysicsBackend physicsBackend,
            IMmdIkSolver? ikSolver = null,
            bool captureCheckpoints = false,
            MmdTopologyPlan? topologyPlan = null,
            bool stopBeforePhysics = false)
        {
            if (!IsSourceLess(model, motion))
            {
                throw new NotSupportedException(
                    "Managed runtime frame pipeline is source-less-only: source-backed or mixed-source " +
                    "PMX/VMD inputs are unsupported; native evaluation is required.");
            }

            topologyPlan?.EnsureModel(model);
            ikSolver ??= new MmdIkSolver();

            MmdSampledMotion sampledMotion = VmdMotionSampler.Sample(motion, model, frame);

            Dictionary<int, float[]>? sampledWorldMatrices = captureCheckpoints
                ? EvaluateWorldMatrices(model, topologyPlan, sampledMotion) : null;

            MmdSampledMotion boneMorphedMotion = MmdBoneMorphEvaluator.ApplyBoneMorphs(model, sampledMotion);

            MmdSampledMotion appendedMotion = ApplyBeforePhysicsAppendTransforms(model, boneMorphedMotion, ikSolver);

            Dictionary<int, float[]>? appendedWorldMatrices = captureCheckpoints
                ? EvaluateWorldMatrices(model, topologyPlan, appendedMotion) : null;

            MmdSampledMotion ikMotion = ikSolver is MmdIkSolver mmdIkSolver
                ? topologyPlan != null
                    ? mmdIkSolver.SolveWithValidatedTopology(
                        model,
                        boneMorphedMotion,
                        appendedMotion,
                        MmdBoneEvaluationPass.BeforePhysics,
                        topologyPlan)
                    : mmdIkSolver.Solve(model, boneMorphedMotion, appendedMotion, MmdBoneEvaluationPass.BeforePhysics)
                    : ikSolver.Solve(model, appendedMotion);

            Dictionary<int, float[]>? ikWorldMatrices = captureCheckpoints
                ? EvaluateWorldMatrices(model, topologyPlan, ikMotion) : null;

            if (stopBeforePhysics)
            {
                return new MmdRuntimeFrameEvaluation
                {
                    SampledMotion = sampledMotion,
                    SampledWorldMatrices = sampledWorldMatrices,
                    AppendedMotion = appendedMotion,
                    AppendedWorldMatrices = appendedWorldMatrices,
                    IkMotion = ikMotion,
                    IkWorldMatrices = ikWorldMatrices,
                    FinalMotion = ikMotion,
                    WorldMatrices = ikWorldMatrices ?? EvaluateWorldMatrices(model, topologyPlan, ikMotion)
                };
            }

            physicsBackend.Step(frame, deltaTime: 0.0f);

            MmdSampledMotion finalMotion = ikMotion;
            Dictionary<int, float[]> finalWorldMatrices = captureCheckpoints
                ? ikWorldMatrices!
                : EvaluateWorldMatrices(model, topologyPlan, ikMotion);
            if (model.HasDeformAfterPhysicsBones)
            {
                MmdSampledMotion afterAppendMotion = MmdAppendTransformEvaluator.ApplyAppendTransforms(
                    model,
                    ikMotion,
                    MmdBoneEvaluationPass.AfterPhysics);

                MmdSampledMotion afterIkMotion = ikSolver is MmdIkSolver afterPassIkSolver
                    ? topologyPlan != null
                        ? afterPassIkSolver.SolveWithValidatedTopology(
                            model,
                            ikMotion,
                            afterAppendMotion,
                            MmdBoneEvaluationPass.AfterPhysics,
                            topologyPlan)
                        : afterPassIkSolver.Solve(model, ikMotion, afterAppendMotion, MmdBoneEvaluationPass.AfterPhysics)
                        : ikSolver.Solve(model, afterAppendMotion);
                finalMotion = MergeAfterPhysicsMotion(model, ikMotion, afterIkMotion);

                finalWorldMatrices = EvaluateWorldMatrices(model, topologyPlan, finalMotion);
            }

            return new MmdRuntimeFrameEvaluation
            {
                SampledMotion = sampledMotion,
                SampledWorldMatrices = sampledWorldMatrices,
                AppendedMotion = appendedMotion,
                AppendedWorldMatrices = appendedWorldMatrices,
                IkMotion = ikMotion,
                IkWorldMatrices = ikWorldMatrices,
                FinalMotion = finalMotion,
                WorldMatrices = finalWorldMatrices
            };
        }

        private static Dictionary<int, float[]> EvaluateWorldMatrices(
            MmdModelDefinition model,
            MmdTopologyPlan? topologyPlan,
            MmdSampledMotion motion)
        {
            return topologyPlan != null
                ? MmdPoseEvaluator.EvaluateWorldMatrices(topologyPlan, motion)
                : MmdPoseEvaluator.EvaluateWorldMatrices(model, motion);
        }

        private static MmdSampledMotion ApplyBeforePhysicsAppendTransforms(
            MmdModelDefinition model,
            MmdSampledMotion boneMorphedMotion,
            IMmdIkSolver ikSolver)
        {
            if (ikSolver is MmdIkSolver appendAwareIkSolver)
            {
                return appendAwareIkSolver.ApplyAppendTransforms(model, boneMorphedMotion, MmdBoneEvaluationPass.BeforePhysics);
            }

            if (!model.HasDeformAfterPhysicsBones && ikSolver is IMmdAppendTransformProvider appendTransformProvider)
            {
                return appendTransformProvider.ApplyAppendTransforms(model, boneMorphedMotion);
            }

            return MmdAppendTransformEvaluator.ApplyAppendTransforms(
                model,
                boneMorphedMotion,
                MmdBoneEvaluationPass.BeforePhysics);
        }

        private static MmdSampledMotion MergeAfterPhysicsMotion(
            MmdModelDefinition model,
            MmdSampledMotion beforePhysicsMotion,
            MmdSampledMotion afterPhysicsMotion)
        {
            var result = CopyMotion(beforePhysicsMotion);
            IReadOnlyList<MmdBoneDefinition> bones = model.bones != null
                ? model.bones
                : System.Array.Empty<MmdBoneDefinition>();
            for (int i = 0; i < bones.Count; i++)
            {
                MmdBoneDefinition bone = bones[i];
                if (!bone.deformAfterPhysics)
                {
                    continue;
                }

                if (afterPhysicsMotion.Bones.TryGetValue(bone.name, out MmdBonePoseSample pose))
                {
                    result.Bones[bone.name] = pose;
                }
            }

            return result;
        }

        private static MmdSampledMotion CopyMotion(MmdSampledMotion source)
        {
            var result = new MmdSampledMotion();
            foreach (KeyValuePair<string, MmdBonePoseSample> bone in source.Bones)
            {
                result.Bones[bone.Key] = bone.Value;
            }

            foreach (KeyValuePair<string, float> morph in source.Morphs)
            {
                result.Morphs[morph.Key] = morph.Value;
            }

            foreach (KeyValuePair<string, bool> ikState in source.IkStates)
            {
                result.IkStates[ikState.Key] = ikState.Value;
            }

            return result;
        }
    }
}
