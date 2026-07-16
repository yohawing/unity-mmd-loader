#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Mmd.Parser;
using Mmd.Motion;
using Mmd.Physics;
using Mmd.Pose;
using Mmd.UnityIntegration;

namespace Mmd.Editor
{
    public static class MmdHumanoidClipConversionWriter
    {
        public static MmdHumanoidClipConversionWriterResult CreateHumanoidAnimationClipAsset(
            MmdPmxAsset pmxAsset,
            MmdVmdAsset vmdAsset,
            float frameRate,
            int startFrame = 0,
            int? endFrame = null,
            string? outputPath = null)
        {
            MmdHumanoidClipConversionWriterResult inMemoryResult =
                CreateInMemoryClip(pmxAsset, vmdAsset, frameRate, startFrame, endFrame);
            List<string> diagnostics = new(inMemoryResult.Diagnostics);

            if (inMemoryResult.Clip == null || !inMemoryResult.PrerequisitesReady || !inMemoryResult.CanCreateClipNow)
            {
                return new MmdHumanoidClipConversionWriterResult(null, inMemoryResult.Plan, diagnostics);
            }

            string fallbackPath = GetDefaultOutputPath(pmxAsset, vmdAsset);
            string requestedPath = outputPath == null ? fallbackPath : outputPath;
            if (!TryNormalizeAndValidateOutputPath(requestedPath, diagnostics, out string normalizedOutputPath))
            {
                UnityEngine.Object.DestroyImmediate(inMemoryResult.Clip);
                return new MmdHumanoidClipConversionWriterResult(null, inMemoryResult.Plan, diagnostics);
            }

            try
            {
                string? outputDirectory = Path.GetDirectoryName(normalizedOutputPath)?.Replace('\\', '/');
                if (!string.IsNullOrWhiteSpace(outputDirectory) && !AssetDatabase.IsValidFolder(outputDirectory))
                {
                    Directory.CreateDirectory(Path.Combine(ProjectRoot, outputDirectory));
                    AssetDatabase.Refresh();
                }

                string uniqueAssetPath = AssetDatabase.GenerateUniqueAssetPath(normalizedOutputPath);
                AssetDatabase.CreateAsset(inMemoryResult.Clip, uniqueAssetPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(uniqueAssetPath, ImportAssetOptions.ForceUpdate);
                AnimationClip? savedClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(uniqueAssetPath);
                if (savedClip == null)
                {
                    diagnostics.Add(
                        "validation: saved clip could not be loaded from AssetDatabase: "
                        + uniqueAssetPath + ".");
                }

                return new MmdHumanoidClipConversionWriterResult(savedClip, inMemoryResult.Plan, diagnostics);
            }
            catch (Exception ex)
            {
                if (inMemoryResult.Clip != null && !AssetDatabase.Contains(inMemoryResult.Clip))
                {
                    UnityEngine.Object.DestroyImmediate(inMemoryResult.Clip);
                }

                diagnostics.Add("writer: failed to persist animation clip: " + ex.Message);
                return new MmdHumanoidClipConversionWriterResult(null, inMemoryResult.Plan, diagnostics);
            }
        }

        public static string GetDefaultOutputPath(
            MmdPmxAsset? pmxAsset,
            MmdVmdAsset? vmdAsset)
        {
            return "Assets/"
                   + GetSourceStem(pmxAsset?.SourceId, "PMX")
                   + "_"
                   + GetSourceStem(vmdAsset?.SourceId, "VMD")
                   + ".anim";
        }

        public static bool TryNormalizeAndValidateOutputPath(
            string outputPath,
            List<string> diagnostics,
            out string normalizedOutputPath)
        {
            if (MmdAssetPathUtility.TryValidateProjectRelativeOutputPath(
                    outputPath,
                    ".anim",
                    out normalizedOutputPath,
                    out MmdOutputPathError error))
            {
                return true;
            }

            switch (error)
            {
                case MmdOutputPathError.Empty:
                    diagnostics.Add("validation: output path is required.");
                    break;
                case MmdOutputPathError.Rooted:
                    diagnostics.Add("validation: output path must be project-relative, not rooted.");
                    break;
                case MmdOutputPathError.NotUnderAssets:
                    diagnostics.Add("validation: output path must start with Assets/.");
                    break;
                case MmdOutputPathError.WrongExtension:
                    diagnostics.Add("validation: output path must end with .anim.");
                    break;
                case MmdOutputPathError.EmptyOrDotSegment:
                    string[] segments = outputPath.Replace('\\', '/').Split('/');
                    diagnostics.Add(Array.Exists(segments, string.IsNullOrWhiteSpace)
                        ? "validation: output path must not contain empty segments."
                        : "validation: output path must not contain '.' or '..' segments.");
                    break;
                case MmdOutputPathError.EscapesAssets:
                    diagnostics.Add("validation: output path must stay inside the Unity Assets directory.");
                    break;
                default:
                    diagnostics.Add("validation: output path must not be empty.");
                    break;
            }

            return false;
        }

        public static MmdHumanoidClipConversionWriterResult CreateInMemoryClip(
            MmdPmxAsset pmxAsset,
            MmdVmdAsset vmdAsset,
            float frameRate,
            int startFrame = 0,
            int? endFrame = null)
        {
            List<string> diagnostics = new();

            MmdHumanoidClipConversionPlan plan =
                MmdHumanoidClipConversionPlanner.AnalyzePrerequisites(pmxAsset, vmdAsset);
            diagnostics.AddRange(plan.Diagnostics);

            if (!plan.PrerequisitesReady || !plan.CanCreateClipNow)
            {
                diagnostics.Add("writer: prerequisites are not ready; cannot create in-memory clip.");
                return new MmdHumanoidClipConversionWriterResult(null, plan, diagnostics);
            }

            if (float.IsNaN(frameRate) || float.IsInfinity(frameRate) || frameRate <= 0.0f)
            {
                diagnostics.Add("validation: frameRate must be finite and > 0.");
                return new MmdHumanoidClipConversionWriterResult(null, plan, diagnostics);
            }

            if (startFrame < 0)
            {
                diagnostics.Add("validation: startFrame must be >= 0.");
                return new MmdHumanoidClipConversionWriterResult(null, plan, diagnostics);
            }

            MmdMotionDefinition motion;
            try
            {
                motion = vmdAsset.LoadMotion();
                MmdMotionValidator.ThrowIfInvalid(motion);
            }
            catch (Exception ex)
            {
                diagnostics.Add("validation: failed to load VMD motion: " + ex.Message);
                return new MmdHumanoidClipConversionWriterResult(null, plan, diagnostics);
            }

            int effectiveEndFrame = endFrame ?? motion.maxFrame;
            if (effectiveEndFrame < startFrame)
            {
                diagnostics.Add("validation: endFrame must be >= startFrame.");
                return new MmdHumanoidClipConversionWriterResult(null, plan, diagnostics);
            }

            if (startFrame > motion.maxFrame)
            {
                diagnostics.Add(
                    "validation: startFrame must be <= motion.maxFrame "
                    + motion.maxFrame + ".");
                return new MmdHumanoidClipConversionWriterResult(null, plan, diagnostics);
            }

            if (effectiveEndFrame > motion.maxFrame)
            {
                diagnostics.Add(
                    "validation: endFrame " + effectiveEndFrame
                    + " must be <= motion.maxFrame " + motion.maxFrame + ".");
                return new MmdHumanoidClipConversionWriterResult(null, plan, diagnostics);
            }

            var mappedBones = new List<(HumanBodyBones HumanBone, Transform ProxyTransform, string SourceBoneName, MmdHumanoidRetargetBinding Binding)>();
            GameObject? proxyRoot = null;
            Avatar? proxyAvatar = null;
            MmdUnityPlaybackBinding? evaluatedBinding = null;
            try
            {
                var usedHumanBones = new HashSet<HumanBodyBones>();
                var boneKeyframesByName = BuildBoneKeyframesByName(motion.boneKeyframes);

                var importedStateDiagnostics = new List<string>();
                if (!MmdHumanoidClipConversionPlanner.TryResolveImportedHumanoidState(
                        pmxAsset,
                        importedStateDiagnostics,
                        out Mmd.UnityIntegration.MmdUnityPlaybackController? controller,
                        out Transform[] nativeBones)
                    || controller == null
                    || controller.HumanoidProxyRoot == null)
                {
                    diagnostics.AddRange(importedStateDiagnostics);
                    diagnostics.Add("validation: imported PMX Humanoid mapping became unavailable before writing.");
                    return new MmdHumanoidClipConversionWriterResult(null, plan, diagnostics);
                }

                Transform importedAvatarRoot = controller.transform;
                Transform importedProxyRoot = controller.HumanoidProxyRoot;
                proxyRoot = new GameObject(importedAvatarRoot.gameObject.name + "_ClipBake");
                GameObject clonedProxyRoot = UnityEngine.Object.Instantiate(
                    importedProxyRoot.gameObject,
                    proxyRoot.transform,
                    worldPositionStays: false);
                clonedProxyRoot.name = importedProxyRoot.gameObject.name;
                foreach (MmdHumanoidRetargetBinding binding in controller.HumanoidRetargetEntries)
                {
                    if (binding == null || !usedHumanBones.Add(binding.HumanBone))
                    {
                        continue;
                    }

                    string proxyPath = AnimationUtility.CalculateTransformPath(
                        binding.ProxyTransform,
                        importedAvatarRoot);
                    Transform? clonedProxyTransform = string.IsNullOrEmpty(proxyPath)
                        ? proxyRoot.transform
                        : proxyRoot.transform.Find(proxyPath);
                    if (clonedProxyTransform == null)
                    {
                        diagnostics.Add(
                            "validation: imported proxy mapping path could not be resolved for "
                            + binding.HumanBone + ".");
                        return new MmdHumanoidClipConversionWriterResult(null, plan, diagnostics);
                    }

                    string sourceBoneName = nativeBones[binding.MmdBoneIndex].name;
                    mappedBones.Add((binding.HumanBone, clonedProxyTransform, sourceBoneName, binding));
                }

                proxyAvatar = pmxAsset.ImportedAvatar;

                if (mappedBones.Count == 0)
                {
                    diagnostics.Add("validation: no mapped proxy bones available for writing.");
                    return new MmdHumanoidClipConversionWriterResult(null, plan, diagnostics);
                }

                var clip = new AnimationClip();
                clip.name = "H6_HumanoidProxyClip_"
                            + NormalizeIdentifier(pmxAsset.SourceId) + "_"
                            + NormalizeIdentifier(vmdAsset.SourceId) + "_"
                            + startFrame + "_"
                            + effectiveEndFrame
                            + "_" + frameRate + "fps";
                clip.frameRate = frameRate;

                int frameCount = effectiveEndFrame - startFrame + 1;
                float sampleFrameToTimeFactor = 1.0f / frameRate;

                if (proxyAvatar == null || !proxyAvatar.isValid || !proxyAvatar.isHuman)
                {
                    UnityEngine.Object.DestroyImmediate(clip);
                    diagnostics.Add("validation: temporary proxy Avatar is not a valid Humanoid Avatar.");
                    return new MmdHumanoidClipConversionWriterResult(null, plan, diagnostics);
                }

                try
                {
                    evaluatedBinding = MmdUnityPlaybackBinding.CreateSkinned(pmxAsset, vmdAsset);
                    evaluatedBinding.SetPhysicsMode(MmdPhysicsMode.Off);
                    if (evaluatedBinding.TryEnableFastRuntime(
                            pmxAsset.GetBytesCopy(),
                            vmdAsset.GetBytesCopy(),
                            out string fastRuntimeReason))
                    {
                        diagnostics.Add("writer: enabled native batch evaluation for Humanoid IK sampling.");
                    }
                    else
                    {
                        diagnostics.Add("writer: native batch evaluation unavailable; using managed IK sampling: "
                                        + fastRuntimeReason);
                    }
                }
                catch (Exception ex)
                {
                    diagnostics.Add("writer: evaluated MMD pose unavailable; using direct VMD bone fallback: " + ex.Message);
                    evaluatedBinding = null;
                }

                AddMuscleCurvesToClip(
                    clip,
                    proxyRoot,
                    proxyAvatar,
                    mappedBones,
                    evaluatedBinding,
                    boneKeyframesByName,
                    startFrame,
                    effectiveEndFrame,
                    frameCount,
                    sampleFrameToTimeFactor,
                    diagnostics,
                    out Vector3[] bodyPositions,
                    out Quaternion[] bodyRotations);

                float humanScale = ResolveHumanScale(proxyAvatar);

                AddRootMotionCurvesToClip(
                    clip,
                    pmxAsset,
                    motion,
                    mappedBones,
                    humanScale,
                    bodyPositions,
                    bodyRotations,
                    startFrame,
                    effectiveEndFrame,
                    frameCount,
                    sampleFrameToTimeFactor,
                    diagnostics);

                return new MmdHumanoidClipConversionWriterResult(clip, plan, diagnostics);
            }
            catch (Exception ex)
            {
                diagnostics.Add("writer: failed to build clip: " + ex.Message);
                return new MmdHumanoidClipConversionWriterResult(null, plan, diagnostics);
            }
            finally
            {
                if (evaluatedBinding != null)
                {
                    evaluatedBinding.Dispose();
                }
                if (proxyRoot != null)
                {
                    UnityEngine.Object.DestroyImmediate(proxyRoot);
                }
            }
        }

        private static Dictionary<string, List<MmdBoneKeyframeDefinition>> BuildBoneKeyframesByName(
            IReadOnlyList<MmdBoneKeyframeDefinition>? boneKeyframes)
        {
            var grouped = new Dictionary<string, List<MmdBoneKeyframeDefinition>>(StringComparer.Ordinal);
            if (boneKeyframes == null)
            {
                return grouped;
            }

            foreach (MmdBoneKeyframeDefinition keyframe in boneKeyframes)
            {
                if (keyframe == null || string.IsNullOrWhiteSpace(keyframe.boneName))
                {
                    continue;
                }

                if (!grouped.TryGetValue(keyframe.boneName, out List<MmdBoneKeyframeDefinition>? list))
                {
                    list = new List<MmdBoneKeyframeDefinition>();
                    grouped.Add(keyframe.boneName, list);
                }
                list.Add(keyframe);
            }

            foreach (List<MmdBoneKeyframeDefinition> list in grouped.Values)
            {
                list.Sort((left, right) => left.frame.CompareTo(right.frame));
            }
            return grouped;
        }

        private static IReadOnlyList<MmdBoneKeyframeDefinition> SelectBoneKeyframes(
            Dictionary<string, List<MmdBoneKeyframeDefinition>> grouped,
            string sourceBoneName)
        {
            return grouped.TryGetValue(sourceBoneName, out List<MmdBoneKeyframeDefinition>? keyframes)
                ? keyframes
                : Array.Empty<MmdBoneKeyframeDefinition>();
        }

        private static void AddMuscleCurvesToClip(
            AnimationClip clip,
            GameObject proxyRoot,
            Avatar proxyAvatar,
            IReadOnlyList<(HumanBodyBones HumanBone, Transform ProxyTransform, string SourceBoneName, MmdHumanoidRetargetBinding Binding)> mappedBones,
            MmdUnityPlaybackBinding? evaluatedBinding,
            Dictionary<string, List<MmdBoneKeyframeDefinition>> boneKeyframesByName,
            int startFrame,
            int endFrame,
            int frameCount,
            float sampleFrameToTimeFactor,
            List<string> diagnostics,
            out Vector3[] bodyPositions,
            out Quaternion[] bodyRotations)
        {
            int muscleCount = HumanTrait.MuscleCount;
            var muscleKeys = new Keyframe[muscleCount][];
            for (int muscleIndex = 0; muscleIndex < muscleCount; muscleIndex++)
            {
                muscleKeys[muscleIndex] = new Keyframe[frameCount];
            }
            bodyPositions = new Vector3[frameCount];
            bodyRotations = new Quaternion[frameCount];
            var baseRotations = new Quaternion[mappedBones.Count];
            for (int boneIndex = 0; boneIndex < mappedBones.Count; boneIndex++)
            {
                baseRotations[boneIndex] = mappedBones[boneIndex].ProxyTransform.localRotation;
            }

            using (var poseHandler = new HumanPoseHandler(proxyAvatar, proxyRoot.transform))
            {
                var pose = new HumanPose { muscles = new float[muscleCount] };
                bool usedNativeBatch = evaluatedBinding != null &&
                    TryFillMuscleKeysFromNativeBatch(
                        evaluatedBinding,
                        mappedBones,
                        poseHandler,
                        ref pose,
                        startFrame,
                        frameCount,
                        sampleFrameToTimeFactor,
                        muscleKeys,
                        bodyPositions,
                        bodyRotations);
                if (usedNativeBatch)
                {
                    diagnostics.Add("writer: sampled evaluated MMD IK pose through native batch output.");
                }
                else
                {
                    int sampleIndex = 0;
                    for (int frame = startFrame; frame <= endFrame; frame++)
                    {
                        float time = (frame - startFrame) * sampleFrameToTimeFactor;
                        evaluatedBinding?.ApplyFrame(frame, 1.0f / sampleFrameToTimeFactor);
                        for (int boneIndex = 0; boneIndex < mappedBones.Count; boneIndex++)
                        {
                            (_, Transform proxyTransform, string sourceBoneName, MmdHumanoidRetargetBinding binding) = mappedBones[boneIndex];
                            if (evaluatedBinding != null)
                            {
                                Transform evaluatedNative = evaluatedBinding.Instance.BoneTransforms[binding.MmdBoneIndex];
                                ApplyEvaluatedLocalPoseToProxy(
                                    proxyTransform,
                                    binding,
                                    evaluatedNative.localRotation);
                            }
                            else
                            {
                                IReadOnlyList<MmdBoneKeyframeDefinition> keyframes = SelectBoneKeyframes(
                                    boneKeyframesByName,
                                    sourceBoneName);
                                float[] rotated = VmdBoneSampler.SampleSortedPose(
                                    keyframes,
                                    sourceBoneName,
                                    frame).Rotation;
                                var rotationDelta = new Quaternion(
                                    -rotated[0],
                                    rotated[1],
                                    -rotated[2],
                                    rotated[3]);
                                proxyTransform.localRotation = baseRotations[boneIndex] * rotationDelta;
                            }
                        }

                        poseHandler.GetHumanPose(ref pose);
                        bodyPositions[sampleIndex] = pose.bodyPosition;
                        bodyRotations[sampleIndex] = pose.bodyRotation;
                        for (int muscleIndex = 0; muscleIndex < muscleCount; muscleIndex++)
                        {
                            muscleKeys[muscleIndex][sampleIndex] =
                                new Keyframe(time, pose.muscles[muscleIndex]);
                        }
                        sampleIndex++;
                    }
                }
            }

            for (int muscleIndex = 0; muscleIndex < muscleCount; muscleIndex++)
            {
                string muscleName = HumanTrait.MuscleName[muscleIndex];
                var binding = EditorCurveBinding.FloatCurve(string.Empty, typeof(Animator), muscleName);
                AnimationUtility.SetEditorCurve(clip, binding, new AnimationCurve(muscleKeys[muscleIndex]));
            }

            diagnostics.Add("writer: wrote " + muscleCount + " Humanoid muscle curves.");
        }

        private static bool TryFillMuscleKeysFromNativeBatch(
            MmdUnityPlaybackBinding binding,
            IReadOnlyList<(HumanBodyBones HumanBone, Transform ProxyTransform, string SourceBoneName, MmdHumanoidRetargetBinding Binding)> mappedBones,
            HumanPoseHandler poseHandler,
            ref HumanPose pose,
            int startFrame,
            int frameCount,
            float sampleFrameToTimeFactor,
            Keyframe[][] muscleKeys,
            Vector3[] bodyPositions,
            Quaternion[] bodyRotations)
        {
            MmdUnityModelInstance instance = binding.Instance;
            if (!MmdGenericAnimationClipWriter.CanUseNativeBatch(
                    binding,
                    instance,
                    out int[] parentBoneIndices,
                    out Vector3[] staticParentPositions,
                    out Quaternion[] staticParentRotations,
                    requireMorphCompatibility: false))
            {
                return false;
            }

            const int ChunkFrameCount = 256;
            int worldFloatsPerFrame = binding.FastRuntimeWorldMatrixFloatCount;
            int morphFloatsPerFrame = binding.FastRuntimeMorphWeightCount;
            int bufferFrameCount = Math.Min(frameCount, ChunkFrameCount);
            var worldMatrices = new float[checked(worldFloatsPerFrame * bufferFrameCount)];
            var morphWeights = new float[checked(morphFloatsPerFrame * bufferFrameCount)];
            float importScale = float.IsFinite(instance.ImportScale) && instance.ImportScale > 0.0f
                ? instance.ImportScale
                : 1.0f;

            for (int chunkStart = 0; chunkStart < frameCount; chunkStart += ChunkFrameCount)
            {
                int chunkCount = Math.Min(ChunkFrameCount, frameCount - chunkStart);
                binding.EvaluateFastRuntimeBatch(
                    startFrame + chunkStart,
                    1.0f,
                    chunkCount,
                    workerCount: 0,
                    worldMatrices,
                    morphWeights);

                for (int chunkSample = 0; chunkSample < chunkCount; chunkSample++)
                {
                    int sampleIndex = chunkStart + chunkSample;
                    float time = sampleIndex * sampleFrameToTimeFactor;
                    ReadOnlySpan<float> frameWorld = worldMatrices.AsSpan(
                        chunkSample * worldFloatsPerFrame,
                        worldFloatsPerFrame);
                    for (int boneIndex = 0; boneIndex < mappedBones.Count; boneIndex++)
                    {
                        (_, Transform proxyTransform, _, MmdHumanoidRetargetBinding retargetBinding) = mappedBones[boneIndex];
                        ReadNativeLocalPose(
                            frameWorld,
                            retargetBinding.MmdBoneIndex,
                            parentBoneIndices,
                            staticParentPositions,
                            staticParentRotations,
                            importScale,
                            out _,
                            out Quaternion localRotation);

                        ApplyEvaluatedLocalPoseToProxy(
                            proxyTransform,
                            retargetBinding,
                            localRotation);
                    }

                    poseHandler.GetHumanPose(ref pose);
                    bodyPositions[sampleIndex] = pose.bodyPosition;
                    bodyRotations[sampleIndex] = pose.bodyRotation;
                    for (int muscleIndex = 0; muscleIndex < muscleKeys.Length; muscleIndex++)
                    {
                        muscleKeys[muscleIndex][sampleIndex] = new Keyframe(
                            time,
                            pose.muscles[muscleIndex]);
                    }
                }
            }

            return true;
        }

        private static void ReadNativeLocalPose(
            ReadOnlySpan<float> frameWorld,
            int boneIndex,
            int[] parentBoneIndices,
            Vector3[] staticParentPositions,
            Quaternion[] staticParentRotations,
            float importScale,
            out Vector3 localPosition,
            out Quaternion localRotation)
        {
            MmdGenericAnimationClipWriter.ReadWorldPose(
                frameWorld,
                boneIndex,
                out Vector3 worldPosition,
                out Quaternion worldRotation);
            int parentBone = parentBoneIndices[boneIndex];
            if (parentBone >= 0)
            {
                MmdGenericAnimationClipWriter.ReadWorldPose(
                    frameWorld,
                    parentBone,
                    out Vector3 parentPosition,
                    out Quaternion parentRotation);
                Quaternion inverseParent = Quaternion.Inverse(parentRotation);
                localPosition = inverseParent * (worldPosition - parentPosition) * importScale;
                localRotation = inverseParent * worldRotation;
                return;
            }

            Quaternion inverseStaticParent = Quaternion.Inverse(staticParentRotations[boneIndex]);
            localPosition = inverseStaticParent * (
                worldPosition * importScale - staticParentPositions[boneIndex]);
            localRotation = inverseStaticParent * worldRotation;
        }

        private static void ApplyEvaluatedLocalPoseToProxy(
            Transform proxyTransform,
            MmdHumanoidRetargetBinding binding,
            Quaternion evaluatedLocalRotation)
        {
            proxyTransform.localRotation =
                binding.ProxyBindLocalRotation *
                Quaternion.Inverse(binding.NativeBindLocalRotation) *
                evaluatedLocalRotation;
        }

        private static float ResolveHumanScale(Avatar proxyAvatar)
        {
            var scaleHost = new GameObject("MmdHumanoidScaleProbe");
            try
            {
                Animator animator = scaleHost.AddComponent<Animator>();
                animator.avatar = proxyAvatar;
                float humanScale = animator.humanScale;
                return float.IsFinite(humanScale) && humanScale > 0.0f ? humanScale : 1.0f;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(scaleHost);
            }
        }

        private static void AddRootMotionCurvesToClip(
            AnimationClip clip,
            MmdPmxAsset pmxAsset,
            MmdMotionDefinition motion,
            IReadOnlyList<(HumanBodyBones HumanBone, Transform ProxyTransform, string SourceBoneName, MmdHumanoidRetargetBinding Binding)> mappedBones,
            float humanScale,
            Vector3[] bodyPositions,
            Quaternion[] bodyRotations,
            int startFrame,
            int endFrame,
            int frameCount,
            float sampleFrameToTimeFactor,
            List<string> diagnostics)
        {
            var positionKeys = new[]
            {
                new Keyframe[frameCount],
                new Keyframe[frameCount],
                new Keyframe[frameCount],
            };
            var rotationKeys = new[]
            {
                new Keyframe[frameCount],
                new Keyframe[frameCount],
                new Keyframe[frameCount],
                new Keyframe[frameCount],
            };

            bool evaluated = false;
            string failureDiagnostic = "root-motion: mapped Hips source is unavailable; wrote baseline RootT/RootQ curves.";
            string? hipsSourceName = null;
            for (int i = 0; i < mappedBones.Count; i++)
            {
                if (mappedBones[i].HumanBone == HumanBodyBones.Hips)
                {
                    hipsSourceName = mappedBones[i].SourceBoneName;
                    break;
                }
            }

            if (!string.IsNullOrWhiteSpace(hipsSourceName))
            {
                try
                {
                    MmdModelDefinition model = pmxAsset.LoadModel();
                    MmdBoneDefinition? hipsBone = FindBoneByName(model, hipsSourceName!);
                    if (hipsBone != null)
                    {
                        evaluated = TryBuildRootMotionKeys(
                            model,
                            motion,
                            hipsBone.index,
                            pmxAsset.ImportScale,
                            humanScale,
                            bodyPositions,
                            bodyRotations,
                            startFrame,
                            endFrame,
                            sampleFrameToTimeFactor,
                            positionKeys,
                            rotationKeys,
                            out failureDiagnostic);
                    }
                    else
                    {
                        failureDiagnostic = "root-motion: mapped Hips bone '" + hipsSourceName
                                            + "' is absent from the PMX model; wrote baseline RootT/RootQ curves.";
                    }
                }
                catch (Exception ex)
                {
                    failureDiagnostic = "root-motion: PMX pose evaluation failed (" + ex.Message
                                        + "); wrote baseline RootT/RootQ curves.";
                }
            }

            if (!evaluated)
            {
                FillBodyPoseRootMotionKeys(
                    startFrame,
                    endFrame,
                    sampleFrameToTimeFactor,
                    bodyPositions,
                    bodyRotations,
                    positionKeys,
                    rotationKeys);
                diagnostics.Add(failureDiagnostic);
            }
            else
            {
                diagnostics.Add("root-motion: wrote evaluated dense RootT/RootQ curves from mapped Hips hierarchy.");
            }

            string[] positionProperties = { "RootT.x", "RootT.y", "RootT.z" };
            string[] rotationProperties = { "RootQ.x", "RootQ.y", "RootQ.z", "RootQ.w" };
            for (int i = 0; i < positionProperties.Length; i++)
            {
                AnimationUtility.SetEditorCurve(
                    clip,
                    EditorCurveBinding.FloatCurve(string.Empty, typeof(Animator), positionProperties[i]),
                    new AnimationCurve(positionKeys[i]));
            }

            for (int i = 0; i < rotationProperties.Length; i++)
            {
                AnimationUtility.SetEditorCurve(
                    clip,
                    EditorCurveBinding.FloatCurve(string.Empty, typeof(Animator), rotationProperties[i]),
                    new AnimationCurve(rotationKeys[i]));
            }

            var verticalOffsetKeys = new Keyframe[frameCount];
            float bindBodyPositionY = bodyPositions[0].y;
            for (int i = 0; i < frameCount; i++)
            {
                verticalOffsetKeys[i] = new Keyframe(
                    positionKeys[1][i].time,
                    positionKeys[1][i].value - bindBodyPositionY);
            }
            AnimationUtility.SetEditorCurve(
                clip,
                EditorCurveBinding.FloatCurve(
                    string.Empty,
                    typeof(MmdHumanoidRootMotionDriver),
                    "clipRootVerticalOffset"),
                new AnimationCurve(verticalOffsetKeys));
        }

        internal static bool TryBuildRootMotionKeys(
            MmdModelDefinition model,
            MmdMotionDefinition motion,
            int hipsBoneIndex,
            float importScale,
            float humanScale,
            Vector3[] bodyPositions,
            Quaternion[] bodyRotations,
            int startFrame,
            int endFrame,
            float sampleFrameToTimeFactor,
            Keyframe[][] positionKeys,
            Keyframe[][] rotationKeys,
            out string diagnostic)
        {
            MmdBoneDefinition? hipsBone = FindBoneByIndex(model, hipsBoneIndex);
            if (hipsBone == null)
            {
                diagnostic = "root-motion: mapped Hips index is absent from the PMX model.";
                return false;
            }
            int frameCount = endFrame - startFrame + 1;
            if (bodyPositions.Length != frameCount || bodyRotations.Length != frameCount)
            {
                diagnostic = "root-motion: sampled Humanoid body pose count does not match the requested frame range.";
                return false;
            }

            Dictionary<int, float[]> bindWorldMatrices = MmdPoseEvaluator.EvaluateWorldMatrices(model, null);
            if (!bindWorldMatrices.TryGetValue(hipsBone.index, out float[]? bindHipsMatrix))
            {
                diagnostic = "root-motion: bind-pose Hips world matrix is unavailable.";
                return false;
            }

            Vector3 bindHipsPosition = ExtractPosition(bindHipsMatrix);
            MmdBoneDefinition? hipsParent = FindBoneByIndex(model, hipsBone.parentIndex);
            Quaternion bindAncestorRotation = Quaternion.identity;
            if (hipsParent != null)
            {
                if (!bindWorldMatrices.TryGetValue(hipsParent.index, out float[]? bindParentMatrix))
                {
                    diagnostic = "root-motion: bind-pose Hips ancestor world matrix is unavailable.";
                    return false;
                }
                bindAncestorRotation = MmdCoordinateSpace.MmdToUnityRotation(ExtractRotation(bindParentMatrix));
            }

            float scale = float.IsFinite(importScale) && importScale > 0.0f ? importScale : 1.0f;
            float normalizedHumanScale = float.IsFinite(humanScale) && humanScale > 0.0f ? humanScale : 1.0f;
            Quaternion previousRotation = Quaternion.identity;
            int sampleIndex = 0;
            for (int frame = startFrame; frame <= endFrame; frame++)
            {
                float time = (frame - startFrame) * sampleFrameToTimeFactor;
                MmdSampledMotion sampledMotion = VmdMotionSampler.Sample(motion, frame);
                Dictionary<int, float[]> worldMatrices = MmdPoseEvaluator.EvaluateWorldMatrices(model, sampledMotion);
                if (!worldMatrices.TryGetValue(hipsBone.index, out float[]? hipsMatrix))
                {
                    diagnostic = "root-motion: sampled Hips world matrix is unavailable at frame " + frame + ".";
                    return false;
                }

                Vector3 mmdPositionDelta = ExtractPosition(hipsMatrix) - bindHipsPosition;
                Vector3 rootPosition = bodyPositions[sampleIndex]
                                       + MmdCoordinateSpace.MmdToUnityPosition(mmdPositionDelta)
                                       * (scale / normalizedHumanScale);
                Quaternion ancestorRotationDelta = Quaternion.identity;
                if (hipsParent != null)
                {
                    if (!worldMatrices.TryGetValue(hipsParent.index, out float[]? parentMatrix))
                    {
                        diagnostic = "root-motion: sampled Hips ancestor world matrix is unavailable at frame " + frame + ".";
                        return false;
                    }
                    Quaternion currentAncestorRotation =
                        MmdCoordinateSpace.MmdToUnityRotation(ExtractRotation(parentMatrix));
                    ancestorRotationDelta = currentAncestorRotation * Quaternion.Inverse(bindAncestorRotation);
                }
                Quaternion rootRotation = ancestorRotationDelta * bodyRotations[sampleIndex];
                rootRotation.Normalize();

                if (sampleIndex > 0 && Quaternion.Dot(previousRotation, rootRotation) < 0.0f)
                {
                    rootRotation = new Quaternion(
                        -rootRotation.x,
                        -rootRotation.y,
                        -rootRotation.z,
                        -rootRotation.w);
                }
                previousRotation = rootRotation;

                positionKeys[0][sampleIndex] = new Keyframe(time, rootPosition.x);
                positionKeys[1][sampleIndex] = new Keyframe(time, rootPosition.y);
                positionKeys[2][sampleIndex] = new Keyframe(time, rootPosition.z);
                rotationKeys[0][sampleIndex] = new Keyframe(time, rootRotation.x);
                rotationKeys[1][sampleIndex] = new Keyframe(time, rootRotation.y);
                rotationKeys[2][sampleIndex] = new Keyframe(time, rootRotation.z);
                rotationKeys[3][sampleIndex] = new Keyframe(time, rootRotation.w);
                sampleIndex++;
            }

            diagnostic = string.Empty;
            return true;
        }

        private static void FillBodyPoseRootMotionKeys(
            int startFrame,
            int endFrame,
            float sampleFrameToTimeFactor,
            Vector3[] bodyPositions,
            Quaternion[] bodyRotations,
            Keyframe[][] positionKeys,
            Keyframe[][] rotationKeys)
        {
            int sampleIndex = 0;
            for (int frame = startFrame; frame <= endFrame; frame++)
            {
                float time = (frame - startFrame) * sampleFrameToTimeFactor;
                Quaternion bodyRotation = bodyRotations[sampleIndex];
                bodyRotation.Normalize();
                positionKeys[0][sampleIndex] = new Keyframe(time, bodyPositions[sampleIndex].x);
                positionKeys[1][sampleIndex] = new Keyframe(time, bodyPositions[sampleIndex].y);
                positionKeys[2][sampleIndex] = new Keyframe(time, bodyPositions[sampleIndex].z);
                rotationKeys[0][sampleIndex] = new Keyframe(time, bodyRotation.x);
                rotationKeys[1][sampleIndex] = new Keyframe(time, bodyRotation.y);
                rotationKeys[2][sampleIndex] = new Keyframe(time, bodyRotation.z);
                rotationKeys[3][sampleIndex] = new Keyframe(time, bodyRotation.w);
                sampleIndex++;
            }
        }

        private static MmdBoneDefinition? FindBoneByName(MmdModelDefinition model, string boneName)
        {
            for (int i = 0; i < model.bones.Count; i++)
            {
                MmdBoneDefinition bone = model.bones[i];
                if (bone != null && string.Equals(bone.name, boneName, StringComparison.Ordinal))
                {
                    return bone;
                }
            }

            return null;
        }

        private static MmdBoneDefinition? FindBoneByIndex(MmdModelDefinition model, int boneIndex)
        {
            for (int i = 0; i < model.bones.Count; i++)
            {
                MmdBoneDefinition bone = model.bones[i];
                if (bone != null && bone.index == boneIndex)
                {
                    return bone;
                }
            }

            return null;
        }

        private static Vector3 ExtractPosition(float[] matrix)
        {
            return new Vector3(matrix[3], matrix[7], matrix[11]);
        }

        private static Quaternion ExtractRotation(float[] matrix)
        {
            Vector3 forward = new(matrix[2], matrix[6], matrix[10]);
            Vector3 up = new(matrix[1], matrix[5], matrix[9]);
            return forward.sqrMagnitude > 0.0f && up.sqrMagnitude > 0.0f
                ? Quaternion.LookRotation(forward.normalized, up.normalized)
                : Quaternion.identity;
        }

        private static string NormalizeIdentifier(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "asset";
            }

            return value.Replace('/', '_')
                .Replace('\\', '_')
                .Replace(':', '_')
                .Replace('.', '_');
        }

        private static string GetSourceStem(string? sourceId, string fallback)
        {
            string fileName = string.IsNullOrWhiteSpace(sourceId)
                ? fallback
                : Path.GetFileNameWithoutExtension(sourceId!.Replace('\\', '/')) ?? fallback;
            return NormalizeIdentifier(fileName);
        }

        private static string ProjectRoot => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
    }

    public sealed class MmdHumanoidClipConversionWriterResult
    {
        public MmdHumanoidClipConversionWriterResult(
            AnimationClip? clip,
            MmdHumanoidClipConversionPlan plan,
            IReadOnlyList<string> diagnostics)
        {
            Clip = clip;
            Plan = plan;
            Diagnostics = diagnostics != null ? new List<string>(diagnostics).AsReadOnly() : Array.Empty<string>();
        }

        public AnimationClip? Clip { get; }

        public MmdHumanoidClipConversionPlan Plan { get; }

        public bool PrerequisitesReady => Plan.PrerequisitesReady;

        public bool CanCreateClipNow => Plan.CanCreateClipNow;

        public IReadOnlyList<string> Diagnostics { get; }
    }
}
