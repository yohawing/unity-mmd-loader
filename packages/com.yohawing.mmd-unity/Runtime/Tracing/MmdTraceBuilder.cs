#nullable enable

using System.Collections.Generic;
using System.Linq;
using Yohawing.MmdUnity.Motion;
using Yohawing.MmdUnity.Parser;
using Yohawing.MmdUnity.Pose;

namespace Yohawing.MmdUnity.Tracing
{
    public static class MmdTraceBuilder
    {
        public static MmdTraceFrame BuildMotionSamplingFrame(
            MmdModelDefinition? model,
            MmdSampledMotion? sampledMotion,
            Dictionary<int, float[]>? worldMatrices,
            int frame,
            float time)
        {
            MmdTraceFrame traceFrame = BuildFrame(
                model,
                sampledMotion,
                worldMatrices,
                frame,
                time,
                MmdTraceCheckpoints.AfterMotionSampling);
            return traceFrame;
        }

        public static MmdTraceFrame BuildAppendTransformFrame(
            MmdModelDefinition? model,
            MmdSampledMotion? sampledMotion,
            Dictionary<int, float[]>? worldMatrices,
            int frame,
            float time)
        {
            return BuildFrame(
                model,
                sampledMotion,
                worldMatrices,
                frame,
                time,
                MmdTraceCheckpoints.AfterAppendTransform);
        }

        public static MmdTraceFrame BuildIkFrame(
            MmdModelDefinition? model,
            MmdSampledMotion? sampledMotion,
            Dictionary<int, float[]>? worldMatrices,
            int frame,
            float time)
        {
            MmdTraceFrame traceFrame = BuildFrame(
                model,
                sampledMotion,
                worldMatrices,
                frame,
                time,
                MmdTraceCheckpoints.AfterIk);

            if (model != null)
            {
                foreach (MmdIkDefinition ik in IkOrEmpty(model).OrderBy(ik => ik.boneIndex))
                {
                    string ikName = ResolveBoneName(model, ik.boneIndex);
                    traceFrame.ik.Add(new MmdTraceIk
                    {
                        name = ikName,
                        enabled = IsIkEnabled(sampledMotion, ikName),
                        target = ikName,
                        effector = ResolveBoneName(model, ik.targetBoneIndex),
                        chain = ResolveIkChain(model, ik)
                    });
                }
            }

            return traceFrame;
        }

        private static bool IsIkEnabled(MmdSampledMotion? sampledMotion, string ikName)
        {
            return sampledMotion == null
                || !sampledMotion.IkStates.TryGetValue(ikName, out bool enabled)
                || enabled;
        }

        public static MmdTraceFrame BuildMorphEvaluationFrame(
            MmdModelDefinition? model,
            MmdSampledMotion? sampledMotion,
            Dictionary<int, float[]>? worldMatrices,
            int frame,
            float time)
        {
            return BuildFrame(
                model,
                sampledMotion,
                worldMatrices,
                frame,
                time,
                MmdTraceCheckpoints.AfterMorphEvaluation);
        }

        public static MmdTraceFrame BuildFinalWorldFrame(
            MmdModelDefinition? model,
            MmdSampledMotion? sampledMotion,
            Dictionary<int, float[]>? worldMatrices,
            int frame,
            float time)
        {
            return BuildFrame(
                model,
                sampledMotion,
                worldMatrices,
                frame,
                time,
                MmdTraceCheckpoints.FinalWorldUpdate);
        }

        private static MmdTraceFrame BuildFrame(
            MmdModelDefinition? model,
            MmdSampledMotion? sampledMotion,
            Dictionary<int, float[]>? worldMatrices,
            int frame,
            float time,
            string checkpoint)
        {
            var traceFrame = new MmdTraceFrame
            {
                frame = frame,
                time = time,
                checkpoint = checkpoint
            };

            if (model == null)
            {
                return traceFrame;
            }

            foreach (MmdBoneDefinition bone in BonesOrEmpty(model).OrderBy(bone => bone.index))
            {
                MmdBonePoseSample sample = sampledMotion != null && sampledMotion.Bones.TryGetValue(bone.name, out MmdBonePoseSample found)
                    ? found
                    : MmdBonePoseSample.Identity;

                traceFrame.bones.Add(new MmdTraceBone
                {
                    name = StableBoneName(bone),
                    localPosition = MmdPoseEvaluator.GetLocalTranslation(model, bone, sample),
                    localRotation = sample.Rotation,
                    localScale = new[] { 1.0f, 1.0f, 1.0f },
                    worldMatrix = worldMatrices != null && worldMatrices.TryGetValue(bone.index, out float[]? matrix)
                        ? matrix
                        : IdentityMatrix()
                });
            }

            if (sampledMotion != null)
            {
                foreach (KeyValuePair<string, float> morph in sampledMotion.Morphs.OrderBy(pair => pair.Key, System.StringComparer.Ordinal))
                {
                    traceFrame.morphs.Add(new MmdTraceMorph
                    {
                        name = string.IsNullOrWhiteSpace(morph.Key) ? "<unnamed-morph>" : morph.Key,
                        weight = morph.Value
                    });
                }
            }

            return traceFrame;
        }

        private static float[] IdentityMatrix()
        {
            return new[]
            {
                1.0f, 0.0f, 0.0f, 0.0f,
                0.0f, 1.0f, 0.0f, 0.0f,
                0.0f, 0.0f, 1.0f, 0.0f,
                0.0f, 0.0f, 0.0f, 1.0f
            };
        }

        private static string ResolveBoneName(MmdModelDefinition model, int boneIndex)
        {
            IReadOnlyList<MmdBoneDefinition> bones = BonesOrEmpty(model);
            for (int i = 0; i < bones.Count; i++)
            {
                if (bones[i].index == boneIndex)
                {
                    return StableBoneName(bones[i]);
                }
            }

            return boneIndex.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        private static string StableBoneName(MmdBoneDefinition bone)
        {
            return string.IsNullOrWhiteSpace(bone.name)
                ? bone.index.ToString(System.Globalization.CultureInfo.InvariantCulture)
                : bone.name;
        }

        private static List<string> ResolveIkChain(MmdModelDefinition model, MmdIkDefinition ik)
        {
            var chain = new List<string>();
            IReadOnlyList<MmdIkLinkDefinition> links = ik.links != null ? ik.links : System.Array.Empty<MmdIkLinkDefinition>();
            for (int i = 0; i < links.Count; i++)
            {
                chain.Add(ResolveBoneName(model, links[i].boneIndex));
            }

            return chain;
        }

        private static IReadOnlyList<MmdBoneDefinition> BonesOrEmpty(MmdModelDefinition model)
        {
            return model.bones != null ? model.bones : System.Array.Empty<MmdBoneDefinition>();
        }

        private static IReadOnlyList<MmdIkDefinition> IkOrEmpty(MmdModelDefinition model)
        {
            return model.ik != null ? model.ik : System.Array.Empty<MmdIkDefinition>();
        }
    }
}
