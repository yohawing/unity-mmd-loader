#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Mmd.Parser;
using Mmd.Motion;

namespace Mmd.Editor
{
    public static class MmdHumanoidClipConversionWriter
    {
        public static MmdHumanoidClipConversionWriterResult CreateHumanoidAnimationClipAsset(
            MmdPmxAsset pmxAsset,
            MmdVmdAsset vmdAsset,
            MmdHumanoidSetupAsset setupAsset,
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
                string setupAssetPath = AssetDatabase.GetAssetPath(setupAsset);
                if (!string.IsNullOrWhiteSpace(setupAssetPath)
                    && setupAssetPath.StartsWith("Assets/", StringComparison.Ordinal))
                {
                    string? setupDirectory = Path.GetDirectoryName(setupAssetPath)?.Replace('\\', '/');
                    if (!string.IsNullOrWhiteSpace(setupDirectory))
                    {
                        directory = setupDirectory;
                    }
                }
            }

            return directory + "/"
                   + "H6_HumanoidClip_"
                   + NormalizeIdentifier(pmxAsset?.SourceId)
                   + "_"
                   + NormalizeIdentifier(vmdAsset?.SourceId)
                   + ".anim";
        }

        public static bool TryNormalizeAndValidateOutputPath(
            string outputPath,
            List<string> diagnostics,
            out string normalizedOutputPath)
        {
            normalizedOutputPath = string.Empty;
            if (string.IsNullOrWhiteSpace(outputPath))
            {
                diagnostics.Add("validation: output path is required.");
                return false;
            }

            if (Path.IsPathRooted(outputPath))
            {
                diagnostics.Add("validation: output path must be project-relative, not rooted.");
                return false;
            }

            string path = outputPath.Replace('\\', '/');
            if (!path.StartsWith("Assets/", StringComparison.Ordinal))
            {
                diagnostics.Add("validation: output path must start with Assets/.");
                return false;
            }

            if (!path.EndsWith(".anim", StringComparison.OrdinalIgnoreCase))
            {
                diagnostics.Add("validation: output path must end with .anim.");
                return false;
            }

            string[] segments = path.Split('/');
            if (segments.Length == 0)
            {
                diagnostics.Add("validation: output path must not be empty.");
                return false;
            }

            foreach (string segment in segments)
            {
                if (string.IsNullOrWhiteSpace(segment))
                {
                    diagnostics.Add("validation: output path must not contain empty segments.");
                    return false;
                }

                if (segment == "." || segment == "..")
                {
                    diagnostics.Add("validation: output path must not contain '.' or '..' segments.");
                    return false;
                }
            }

            normalizedOutputPath = path;
            return true;
        }

        public static MmdHumanoidClipConversionWriterResult CreateInMemoryClip(
            MmdPmxAsset pmxAsset,
            MmdVmdAsset vmdAsset,
            MmdHumanoidSetupAsset setupAsset,
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

            MmdHumanoidProxyRigResult proxyRigResult = MmdHumanoidProxyRigFactory.CreateProxyRig(pmxAsset);
            if (proxyRigResult.ProxyRoot == null)
            {
                diagnostics.AddRange(proxyRigResult.Diagnostics);
                diagnostics.Add("validation: failed to create temporary proxy rig.");
                return new MmdHumanoidClipConversionWriterResult(null, plan, diagnostics);
            }

            var mappedBones = new List<(Transform ProxyTransform, string SourceBoneName)>();
            try
            {
                var usedHumanBones = new HashSet<HumanBodyBones>();
                var boneKeyframesByName = BuildBoneKeyframesByName(motion.boneKeyframes);
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
                        mappedBones.Add((proxyTransform, mappingEntry.MmdBoneName));
                    }
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

                foreach ((Transform proxyTransform, string sourceBoneName) in mappedBones)
                {
                    IReadOnlyList<MmdBoneKeyframeDefinition> keyframes = SelectBoneKeyframes(
                        boneKeyframesByName,
                        sourceBoneName);

                    string transformPath = AnimationUtility.CalculateTransformPath(
                        proxyTransform,
                        proxyRigResult.ProxyRoot.transform);
                    AddRotationCurvesToClip(
                        clip,
                        transformPath,
                        keyframes,
                        startFrame,
                        effectiveEndFrame,
                        frameCount,
                        sampleFrameToTimeFactor,
                        diagnostics,
                        sourceBoneName);
                }

                return new MmdHumanoidClipConversionWriterResult(clip, plan, diagnostics);
            }
            catch (Exception ex)
            {
                diagnostics.Add("writer: failed to build clip: " + ex.Message);
                return new MmdHumanoidClipConversionWriterResult(null, plan, diagnostics);
            }
            finally
            {
                if (proxyRigResult.ProxyRoot != null)
                {
                    UnityEngine.Object.DestroyImmediate(proxyRigResult.ProxyRoot);
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

        private static void AddRotationCurvesToClip(
            AnimationClip clip,
            string relativeTransformPath,
            IReadOnlyList<MmdBoneKeyframeDefinition> boneKeyframes,
            int startFrame,
            int endFrame,
            int frameCount,
            float sampleFrameToTimeFactor,
            List<string> diagnostics,
            string sourceBoneName)
        {
            var keyX = new Keyframe[frameCount];
            var keyY = new Keyframe[frameCount];
            var keyZ = new Keyframe[frameCount];
            var keyW = new Keyframe[frameCount];

            int sampleIndex = 0;
            for (int frame = startFrame; frame <= endFrame; frame++)
            {
                float time = (frame - startFrame) * sampleFrameToTimeFactor;

                float[] rotated = VmdBoneSampler.SampleSortedPose(
                    boneKeyframes,
                    sourceBoneName,
                    frame).Rotation;
                var rotation = new Quaternion(
                    -rotated[0],
                    rotated[1],
                    -rotated[2],
                    rotated[3]);

                keyX[sampleIndex] = new Keyframe(time, rotation.x);
                keyY[sampleIndex] = new Keyframe(time, rotation.y);
                keyZ[sampleIndex] = new Keyframe(time, rotation.z);
                keyW[sampleIndex] = new Keyframe(time, rotation.w);

                sampleIndex++;
            }

            var xBinding = EditorCurveBinding.FloatCurve(
                relativeTransformPath,
                typeof(Transform),
                "m_LocalRotation.x");
            var yBinding = EditorCurveBinding.FloatCurve(
                relativeTransformPath,
                typeof(Transform),
                "m_LocalRotation.y");
            var zBinding = EditorCurveBinding.FloatCurve(
                relativeTransformPath,
                typeof(Transform),
                "m_LocalRotation.z");
            var wBinding = EditorCurveBinding.FloatCurve(
                relativeTransformPath,
                typeof(Transform),
                "m_LocalRotation.w");

            AnimationUtility.SetEditorCurve(clip, xBinding, new AnimationCurve(keyX));
            AnimationUtility.SetEditorCurve(clip, yBinding, new AnimationCurve(keyY));
            AnimationUtility.SetEditorCurve(clip, zBinding, new AnimationCurve(keyZ));
            AnimationUtility.SetEditorCurve(clip, wBinding, new AnimationCurve(keyW));

            diagnostics.Add("writer: wrote rotation curves for " + sourceBoneName);
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
