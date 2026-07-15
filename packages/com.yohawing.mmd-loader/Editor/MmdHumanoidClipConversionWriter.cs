#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Mmd.Parser;
using Mmd.Motion;
using Mmd.Pose;
using Mmd.UnityIntegration;

namespace Mmd.Editor
{
    public static class MmdHumanoidClipConversionWriter
    {
        public static MmdHumanoidClipConversionWriterResult CreateHumanoidAnimationClipAsset(
            MmdPmxAsset pmxAsset,
            MmdVmdAsset vmdAsset,
            MmdHumanoidSetupAsset? setupAsset,
            float frameRate,
            int startFrame = 0,
            int? endFrame = null,
            string? outputPath = null)
        {
            MmdHumanoidClipConversionWriterResult inMemoryResult =
                CreateInMemoryClip(pmxAsset, vmdAsset, setupAsset, frameRate, startFrame, endFrame);
            List<string> diagnostics = new(inMemoryResult.Diagnostics);

            if (inMemoryResult.Clip == null || !inMemoryResult.PrerequisitesReady || !inMemoryResult.CanCreateClipNow)
            {
                return new MmdHumanoidClipConversionWriterResult(null, inMemoryResult.Plan, diagnostics);
            }

            string fallbackPath = GetDefaultOutputPath(setupAsset, pmxAsset, vmdAsset);
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
            MmdHumanoidSetupAsset? setupAsset,
            MmdPmxAsset? pmxAsset,
            MmdVmdAsset? vmdAsset)
        {
            string directory = "Assets";
            if (setupAsset != null && AssetDatabase.Contains(setupAsset))
            {
                string? setupAssetPath = AssetDatabase.GetAssetPath(setupAsset);
                if (!string.IsNullOrWhiteSpace(setupAssetPath)
                    && setupAssetPath.StartsWith("Assets/", StringComparison.Ordinal))
                {
                    string? setupDirectory = Path.GetDirectoryName(setupAssetPath)?.Replace('\\', '/');
                    if (setupDirectory is { Length: > 0 } && !string.IsNullOrWhiteSpace(setupDirectory))
                    {
                        directory = setupDirectory;
                    }
                }
            }

            return directory + "/"
                   + "H6_HumanoidClip_"
                   + NormalizeIdentifier(pmxAsset?.SourceId ?? "pmx")
                   + "_"
                   + NormalizeIdentifier(vmdAsset?.SourceId ?? "vmd")
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
            MmdHumanoidSetupAsset? setupAsset,
            float frameRate,
            int startFrame = 0,
            int? endFrame = null)
        {
            List<string> diagnostics = new();

            MmdHumanoidClipConversionPlan plan =
                MmdHumanoidClipConversionPlanner.AnalyzePrerequisites(pmxAsset, vmdAsset, setupAsset);
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

            var mappedBones = new List<(HumanBodyBones HumanBone, Transform ProxyTransform, string SourceBoneName)>();
            GameObject? proxyRoot = null;
            Avatar? proxyAvatar = null;
            bool ownsProxyAvatar = false;
            try
            {
                var usedHumanBones = new HashSet<HumanBodyBones>();
                var boneKeyframesByName = BuildBoneKeyframesByName(motion.boneKeyframes);

                if (setupAsset != null)
                {
                    MmdHumanoidProxyRigResult proxyRigResult = MmdHumanoidProxyRigFactory.CreateProxyRig(pmxAsset);
                    diagnostics.AddRange(proxyRigResult.Diagnostics);
                    proxyRoot = proxyRigResult.ProxyRoot;
                    if (proxyRoot == null)
                    {
                        diagnostics.Add("validation: failed to create temporary proxy rig.");
                        return new MmdHumanoidClipConversionWriterResult(null, plan, diagnostics);
                    }

                    foreach (MmdSerializableBoneMappingEntry mappingEntry in setupAsset.MappingEntries)
                    {
                        if (mappingEntry == null || usedHumanBones.Contains(mappingEntry.HumanBone))
                        {
                            continue;
                        }

                        if (string.IsNullOrWhiteSpace(mappingEntry.MmdBoneName))
                        {
                            continue;
                        }

                        if (proxyRigResult.BoneMap.TryGetValue(mappingEntry.HumanBone, out Transform? proxyTransform))
                        {
                            usedHumanBones.Add(mappingEntry.HumanBone);
                            mappedBones.Add((mappingEntry.HumanBone, proxyTransform, mappingEntry.MmdBoneName));
                        }
                    }

                    MmdHumanoidAvatarBuildResult avatarResult =
                        MmdHumanoidProxyRigFactory.BuildAvatar(proxyRigResult);
                    diagnostics.AddRange(avatarResult.Diagnostics);
                    proxyAvatar = avatarResult.Avatar;
                    ownsProxyAvatar = proxyAvatar != null;
                }
                else
                {
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

                    Transform importedProxyRoot = controller.HumanoidProxyRoot;
                    proxyRoot = UnityEngine.Object.Instantiate(importedProxyRoot.gameObject);
                    proxyRoot.name = importedProxyRoot.gameObject.name + "_ClipBake";
                    foreach (MmdHumanoidRetargetBinding binding in controller.HumanoidRetargetEntries)
                    {
                        if (binding == null || !usedHumanBones.Add(binding.HumanBone))
                        {
                            continue;
                        }

                        string proxyPath = AnimationUtility.CalculateTransformPath(
                            binding.ProxyTransform,
                            importedProxyRoot);
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
                        mappedBones.Add((binding.HumanBone, clonedProxyTransform, sourceBoneName));
                    }

                    proxyAvatar = pmxAsset.ImportedAvatar;
                }

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

                AddMuscleCurvesToClip(
                    clip,
                    proxyRoot,
                    proxyAvatar,
                    mappedBones,
                    boneKeyframesByName,
                    startFrame,
                    effectiveEndFrame,
                    frameCount,
                    sampleFrameToTimeFactor,
                    diagnostics);

                AddRootMotionCurvesToClip(
                    clip,
                    pmxAsset,
                    motion,
                    mappedBones,
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
                if (ownsProxyAvatar && proxyAvatar != null)
                {
                    UnityEngine.Object.DestroyImmediate(proxyAvatar);
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
            if (!grouped.TryGetValue(sourceBoneName, out List<MmdBoneKeyframeDefinition>? keyframes))
            {
                return Array.Empty<MmdBoneKeyframeDefinition>();
            }

            return keyframes;
        }

        private static void AddMuscleCurvesToClip(
            AnimationClip clip,
            GameObject proxyRoot,
            Avatar proxyAvatar,
            IReadOnlyList<(HumanBodyBones HumanBone, Transform ProxyTransform, string SourceBoneName)> mappedBones,
            Dictionary<string, List<MmdBoneKeyframeDefinition>> boneKeyframesByName,
            int startFrame,
            int endFrame,
            int frameCount,
            float sampleFrameToTimeFactor,
            List<string> diagnostics)
        {
            int muscleCount = HumanTrait.MuscleCount;
            var muscleKeys = new Keyframe[muscleCount][];
            for (int muscleIndex = 0; muscleIndex < muscleCount; muscleIndex++)
            {
                muscleKeys[muscleIndex] = new Keyframe[frameCount];
            }

            var baseRotations = new Quaternion[mappedBones.Count];
            for (int boneIndex = 0; boneIndex < mappedBones.Count; boneIndex++)
            {
                baseRotations[boneIndex] = mappedBones[boneIndex].ProxyTransform.localRotation;
            }

            using (var poseHandler = new HumanPoseHandler(proxyAvatar, proxyRoot.transform))
            {
                var pose = new HumanPose { muscles = new float[muscleCount] };
                int sampleIndex = 0;
                for (int frame = startFrame; frame <= endFrame; frame++)
                {
                    float time = (frame - startFrame) * sampleFrameToTimeFactor;
                    for (int boneIndex = 0; boneIndex < mappedBones.Count; boneIndex++)
                    {
                        (_, Transform proxyTransform, string sourceBoneName) = mappedBones[boneIndex];
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

                    poseHandler.GetHumanPose(ref pose);
                    for (int muscleIndex = 0; muscleIndex < muscleCount; muscleIndex++)
                    {
                        muscleKeys[muscleIndex][sampleIndex] =
                            new Keyframe(time, pose.muscles[muscleIndex]);
                    }

                    sampleIndex++;
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

        private static void AddRootMotionCurvesToClip(
            AnimationClip clip,
            MmdPmxAsset pmxAsset,
            MmdMotionDefinition motion,
            IReadOnlyList<(HumanBodyBones HumanBone, Transform ProxyTransform, string SourceBoneName)> mappedBones,
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
            string failureDiagnostic = "root-motion: mapped Hips source is unavailable; wrote identity RootT/RootQ curves.";
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
                                            + "' is absent from the PMX model; wrote identity RootT/RootQ curves.";
                    }
                }
                catch (Exception ex)
                {
                    failureDiagnostic = "root-motion: PMX pose evaluation failed (" + ex.Message
                                        + "); wrote identity RootT/RootQ curves.";
                }
            }

            if (!evaluated)
            {
                FillIdentityRootMotionKeys(
                    startFrame,
                    endFrame,
                    sampleFrameToTimeFactor,
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
        }

        internal static bool TryBuildRootMotionKeys(
            MmdModelDefinition model,
            MmdMotionDefinition motion,
            int hipsBoneIndex,
            float importScale,
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

            Dictionary<int, float[]> bindWorldMatrices = MmdPoseEvaluator.EvaluateWorldMatrices(model, null);
            if (!bindWorldMatrices.TryGetValue(hipsBone.index, out float[]? bindHipsMatrix))
            {
                diagnostic = "root-motion: bind-pose Hips world matrix is unavailable.";
                return false;
            }

            Vector3 bindHipsPosition = ExtractPosition(bindHipsMatrix);
            Quaternion bindHipsRotation =
                MmdCoordinateSpace.MmdToUnityRotation(ExtractRotation(bindHipsMatrix));

            float scale = float.IsFinite(importScale) && importScale > 0.0f ? importScale : 1.0f;
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
                Vector3 rootPosition = MmdCoordinateSpace.MmdToUnityPosition(mmdPositionDelta) * scale;
                Quaternion currentHipsRotation =
                    MmdCoordinateSpace.MmdToUnityRotation(ExtractRotation(hipsMatrix));
                Quaternion rootRotation = currentHipsRotation * Quaternion.Inverse(bindHipsRotation);
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

        private static void FillIdentityRootMotionKeys(
            int startFrame,
            int endFrame,
            float sampleFrameToTimeFactor,
            Keyframe[][] positionKeys,
            Keyframe[][] rotationKeys)
        {
            int sampleIndex = 0;
            for (int frame = startFrame; frame <= endFrame; frame++)
            {
                float time = (frame - startFrame) * sampleFrameToTimeFactor;
                for (int i = 0; i < 3; i++)
                {
                    positionKeys[i][sampleIndex] = new Keyframe(time, 0.0f);
                    rotationKeys[i][sampleIndex] = new Keyframe(time, 0.0f);
                }
                rotationKeys[3][sampleIndex] = new Keyframe(time, 1.0f);
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
