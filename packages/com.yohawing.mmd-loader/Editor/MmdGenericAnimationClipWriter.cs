#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Mmd.Physics;
using Mmd.UnityIntegration;

namespace Mmd.Editor
{
    public static class MmdGenericAnimationClipWriter
    {
        public static MmdGenericAnimationClipWriterResult CreateInMemoryClip(
            MmdPmxAsset pmxAsset,
            MmdVmdAsset vmdAsset,
            float frameRate,
            int startFrame = 0,
            int? endFrame = null)
        {
            var diagnostics = new List<string>();
            if (pmxAsset == null)
            {
                diagnostics.Add("validation: pmxAsset is required.");
                return new MmdGenericAnimationClipWriterResult(null, diagnostics, MmdPhysicsMode.Off);
            }

            if (vmdAsset == null)
            {
                diagnostics.Add("validation: vmdAsset is required.");
                return new MmdGenericAnimationClipWriterResult(null, diagnostics, MmdPhysicsMode.Off);
            }

            if (!float.IsFinite(frameRate) || frameRate <= 0.0f)
            {
                diagnostics.Add("validation: frameRate must be finite and > 0.");
                return new MmdGenericAnimationClipWriterResult(null, diagnostics, MmdPhysicsMode.Off);
            }

            if (startFrame < 0)
            {
                diagnostics.Add("validation: startFrame must be >= 0.");
                return new MmdGenericAnimationClipWriterResult(null, diagnostics, MmdPhysicsMode.Off);
            }

            MmdUnityPlaybackBinding? binding = null;
            try
            {
                binding = MmdUnityPlaybackBinding.CreateSkinned(pmxAsset, vmdAsset);
                int effectiveEndFrame = endFrame ?? binding.MotionMaxFrame;
                if (effectiveEndFrame < startFrame)
                {
                    diagnostics.Add("validation: endFrame must be >= startFrame.");
                    return new MmdGenericAnimationClipWriterResult(null, diagnostics, MmdPhysicsMode.Off);
                }

                if (effectiveEndFrame > binding.MotionMaxFrame)
                {
                    diagnostics.Add("validation: endFrame must be <= motion max frame " + binding.MotionMaxFrame + ".");
                    return new MmdGenericAnimationClipWriterResult(null, diagnostics, MmdPhysicsMode.Off);
                }

                if (startFrame > binding.MotionMaxFrame)
                {
                    diagnostics.Add("validation: startFrame must be <= motion max frame " + binding.MotionMaxFrame + ".");
                    return new MmdGenericAnimationClipWriterResult(null, diagnostics, MmdPhysicsMode.Off);
                }

                binding.SetPhysicsMode(MmdPhysicsMode.Off);
                if (binding.TryEnableFastRuntime(pmxAsset.GetBytesCopy(), vmdAsset.GetBytesCopy(), out string fastRuntimeReason))
                {
                    diagnostics.Add("writer: enabled persistent native evaluation for Generic bake.");
                }
                else
                {
                    diagnostics.Add("writer: persistent native evaluation unavailable; using managed fallback: "
                                    + fastRuntimeReason);
                }

                AnimationClip clip = BakeDenseClip(
                    binding,
                    pmxAsset,
                    vmdAsset,
                    frameRate,
                    startFrame,
                    effectiveEndFrame,
                    out int compactedCurveCount);
                diagnostics.Add("writer: baked Generic transform and vertex morph curves with physics off; compacted "
                                + compactedCurveCount + " constant curves to endpoint keys.");
                return new MmdGenericAnimationClipWriterResult(clip, diagnostics, binding.PhysicsMode);
            }
            catch (Exception ex)
            {
                diagnostics.Add("writer: failed to build Generic AnimationClip: " + ex.Message);
                return new MmdGenericAnimationClipWriterResult(null, diagnostics, MmdPhysicsMode.Off);
            }
            finally
            {
                if (binding != null)
                {
                    DestroyTemporaryBinding(binding);
                }
            }
        }

        public static MmdGenericAnimationClipWriterResult CreateAnimationClipAsset(
            MmdPmxAsset pmxAsset,
            MmdVmdAsset vmdAsset,
            float frameRate,
            int startFrame = 0,
            int? endFrame = null,
            string? outputPath = null)
        {
            MmdGenericAnimationClipWriterResult result = CreateInMemoryClip(
                pmxAsset, vmdAsset, frameRate, startFrame, endFrame);
            if (result.Clip == null)
            {
                return result;
            }

            string requestedPath = outputPath ?? GetDefaultOutputPath(pmxAsset, vmdAsset);
            if (!MmdAssetPathUtility.TryValidateProjectRelativeOutputPath(
                    requestedPath, ".anim", out string normalizedPath, out MmdOutputPathError error))
            {
                UnityEngine.Object.DestroyImmediate(result.Clip);
                var diagnostics = new List<string>(result.Diagnostics)
                {
                    "validation: invalid output path (" + error + ")."
                };
                return new MmdGenericAnimationClipWriterResult(null, diagnostics, result.PhysicsMode);
            }

            try
            {
                string? directory = Path.GetDirectoryName(normalizedPath)?.Replace('\\', '/');
                if (!string.IsNullOrWhiteSpace(directory) && !AssetDatabase.IsValidFolder(directory))
                {
                    string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                    Directory.CreateDirectory(Path.Combine(projectRoot, directory));
                    AssetDatabase.Refresh();
                }

                string uniquePath = AssetDatabase.GenerateUniqueAssetPath(normalizedPath);
                AssetDatabase.CreateAsset(result.Clip, uniquePath);
                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(uniquePath, ImportAssetOptions.ForceUpdate);
                AnimationClip? saved = AssetDatabase.LoadAssetAtPath<AnimationClip>(uniquePath);
                return new MmdGenericAnimationClipWriterResult(saved, result.Diagnostics, result.PhysicsMode, uniquePath);
            }
            catch (Exception ex)
            {
                if (!AssetDatabase.Contains(result.Clip) && !EditorUtility.IsPersistent(result.Clip))
                {
                    UnityEngine.Object.DestroyImmediate(result.Clip);
                }

                var diagnostics = new List<string>(result.Diagnostics)
                {
                    "writer: failed to persist Generic AnimationClip: " + ex.Message
                };
                return new MmdGenericAnimationClipWriterResult(null, diagnostics, result.PhysicsMode);
            }
        }

        public static string GetDefaultOutputPath(MmdPmxAsset? pmxAsset, MmdVmdAsset? vmdAsset)
        {
            return "Assets/MmdGenericClip_" + NormalizeIdentifier(pmxAsset?.SourceId ?? "pmx") + "_"
                   + NormalizeIdentifier(vmdAsset?.SourceId ?? "vmd") + ".anim";
        }

        private static AnimationClip BakeDenseClip(
            MmdUnityPlaybackBinding binding,
            MmdPmxAsset pmxAsset,
            MmdVmdAsset vmdAsset,
            float frameRate,
            int startFrame,
            int endFrame,
            out int compactedCurveCount)
        {
            MmdUnityModelInstance instance = binding.Instance;
            int frameCount = endFrame - startFrame + 1;
            var clip = new AnimationClip
            {
                name = "MmdGenericClip_" + NormalizeIdentifier(pmxAsset.SourceId) + "_"
                       + NormalizeIdentifier(vmdAsset.SourceId) + "_" + startFrame + "_" + endFrame,
                frameRate = frameRate
            };

            try
            {
            compactedCurveCount = 0;
            string[] bonePaths = CalculateUniqueBonePaths(instance.BoneTransforms, instance.Root.transform);
            var positionKeys = new Keyframe[instance.BoneTransforms.Length, 3][];
            var rotationKeys = new Keyframe[instance.BoneTransforms.Length, 4][];
            for (int bone = 0; bone < instance.BoneTransforms.Length; bone++)
            {
                for (int axis = 0; axis < 3; axis++) positionKeys[bone, axis] = new Keyframe[frameCount];
                for (int axis = 0; axis < 4; axis++) rotationKeys[bone, axis] = new Keyframe[frameCount];
            }

            IReadOnlyList<MmdUnityVertexMorphBlendShapeBinding> morphs = instance.VertexMorphBlendShapes;
            var morphKeys = new Keyframe[morphs.Count][];
            for (int morph = 0; morph < morphs.Count; morph++) morphKeys[morph] = new Keyframe[frameCount];

            for (int frame = startFrame, sample = 0; frame <= endFrame; frame++, sample++)
            {
                binding.ApplyFrame(frame, frameRate);
                float time = (frame - startFrame) / frameRate;
                for (int bone = 0; bone < instance.BoneTransforms.Length; bone++)
                {
                    Vector3 p = instance.BoneTransforms[bone].localPosition;
                    Quaternion q = instance.BoneTransforms[bone].localRotation;
                    positionKeys[bone, 0][sample] = new Keyframe(time, p.x);
                    positionKeys[bone, 1][sample] = new Keyframe(time, p.y);
                    positionKeys[bone, 2][sample] = new Keyframe(time, p.z);
                    rotationKeys[bone, 0][sample] = new Keyframe(time, q.x);
                    rotationKeys[bone, 1][sample] = new Keyframe(time, q.y);
                    rotationKeys[bone, 2][sample] = new Keyframe(time, q.z);
                    rotationKeys[bone, 3][sample] = new Keyframe(time, q.w);
                }

                if (instance.SkinnedMeshRenderer != null)
                {
                    for (int morph = 0; morph < morphs.Count; morph++)
                    {
                        float weight = instance.SkinnedMeshRenderer.GetBlendShapeWeight(morphs[morph].BlendShapeIndex);
                        morphKeys[morph][sample] = new Keyframe(time, weight);
                    }
                }
            }

            string[] positionProperties = { "m_LocalPosition.x", "m_LocalPosition.y", "m_LocalPosition.z" };
            string[] rotationProperties = { "m_LocalRotation.x", "m_LocalRotation.y", "m_LocalRotation.z", "m_LocalRotation.w" };
            var curveBindings = new List<EditorCurveBinding>(instance.BoneTransforms.Length * 7 + morphs.Count);
            var curves = new List<AnimationCurve>(curveBindings.Capacity);
            for (int bone = 0; bone < instance.BoneTransforms.Length; bone++)
            {
                string path = bonePaths[bone];
                for (int axis = 0; axis < 3; axis++)
                {
                    curveBindings.Add(EditorCurveBinding.FloatCurve(path, typeof(Transform), positionProperties[axis]));
                    curves.Add(CreateCompactedCurve(positionKeys[bone, axis], ref compactedCurveCount));
                }
                for (int axis = 0; axis < 4; axis++)
                {
                    curveBindings.Add(EditorCurveBinding.FloatCurve(path, typeof(Transform), rotationProperties[axis]));
                    curves.Add(CreateCompactedCurve(rotationKeys[bone, axis], ref compactedCurveCount));
                }
            }

            if (instance.SkinnedMeshRenderer != null)
            {
                string rendererPath = AnimationUtility.CalculateTransformPath(instance.SkinnedMeshRenderer.transform, instance.Root.transform);
                for (int morph = 0; morph < morphs.Count; morph++)
                {
                    curveBindings.Add(EditorCurveBinding.FloatCurve(
                        rendererPath,
                        typeof(SkinnedMeshRenderer),
                        "blendShape." + morphs[morph].BlendShapeName));
                    curves.Add(CreateCompactedCurve(morphKeys[morph], ref compactedCurveCount));
                }
            }

            AnimationUtility.SetEditorCurves(clip, curveBindings.ToArray(), curves.ToArray());
            clip.EnsureQuaternionContinuity();
            return clip;
            }
            catch
            {
                UnityEngine.Object.DestroyImmediate(clip);
                throw;
            }
        }

        private static AnimationCurve CreateCompactedCurve(Keyframe[] denseKeys, ref int compactedCurveCount)
        {
            if (denseKeys.Length <= 1)
            {
                return new AnimationCurve(denseKeys);
            }

            float firstValue = denseKeys[0].value;
            for (int index = 1; index < denseKeys.Length; index++)
            {
                if (!denseKeys[index].value.Equals(firstValue))
                {
                    return new AnimationCurve(denseKeys);
                }
            }

            compactedCurveCount++;
            return new AnimationCurve(
                denseKeys[0],
                denseKeys[denseKeys.Length - 1]);
        }

        internal static string[] CalculateUniqueBonePaths(IReadOnlyList<Transform> boneTransforms, Transform root)
        {
            var paths = new string[boneTransforms.Count];
            var owners = new Dictionary<string, Transform>(StringComparer.Ordinal);
            for (int bone = 0; bone < boneTransforms.Count; bone++)
            {
                Transform transform = boneTransforms[bone];
                string path = AnimationUtility.CalculateTransformPath(transform, root);
                if (!owners.TryAdd(path, transform))
                {
                    throw new InvalidOperationException(
                        "Generic AnimationClip cannot represent duplicate transform path '" + path
                        + "' for bones '" + owners[path].name + "' and '" + transform.name + "'.");
                }

                paths[bone] = path;
            }

            return paths;
        }

        private static void DestroyTemporaryBinding(MmdUnityPlaybackBinding binding)
        {
            MmdUnityModelInstance instance = binding.Instance;
            binding.Dispose();
            if (instance.Root != null) UnityEngine.Object.DestroyImmediate(instance.Root);
            if (instance.Mesh != null) UnityEngine.Object.DestroyImmediate(instance.Mesh);
            var destroyedMaterials = new HashSet<Material>();
            foreach (Material material in instance.Materials)
                if (material != null && destroyedMaterials.Add(material)) UnityEngine.Object.DestroyImmediate(material);
            var destroyedTextures = new HashSet<Texture2D>();
            foreach (Texture2D texture in instance.OwnedTextures)
                if (texture != null && destroyedTextures.Add(texture)) UnityEngine.Object.DestroyImmediate(texture);
        }

        private static string NormalizeIdentifier(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? "asset"
                : value.Replace('/', '_').Replace('\\', '_').Replace(':', '_').Replace('.', '_');
        }
    }

    public sealed class MmdGenericAnimationClipWriterResult
    {
        internal MmdGenericAnimationClipWriterResult(
            AnimationClip? clip,
            IReadOnlyList<string> diagnostics,
            MmdPhysicsMode physicsMode,
            string assetPath = "")
        {
            Clip = clip;
            Diagnostics = new List<string>(diagnostics).AsReadOnly();
            PhysicsMode = physicsMode;
            AssetPath = assetPath;
        }

        public AnimationClip? Clip { get; }
        public IReadOnlyList<string> Diagnostics { get; }
        public MmdPhysicsMode PhysicsMode { get; }
        public string AssetPath { get; }
    }
}
