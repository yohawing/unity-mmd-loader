#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Mmd.Physics;
using Mmd.UnityIntegration;

namespace Mmd.Editor
{
    public readonly struct MmdGenericAnimationClipParityTolerance
    {
        public MmdGenericAnimationClipParityTolerance(
            float localPositionDistance,
            float worldPositionDistance,
            float localRotationAngle,
            float worldRotationAngle,
            float blendShapeWeightDelta)
        {
            LocalPositionDistance = Validate(localPositionDistance, nameof(localPositionDistance));
            WorldPositionDistance = Validate(worldPositionDistance, nameof(worldPositionDistance));
            LocalRotationAngle = Validate(localRotationAngle, nameof(localRotationAngle));
            WorldRotationAngle = Validate(worldRotationAngle, nameof(worldRotationAngle));
            BlendShapeWeightDelta = Validate(blendShapeWeightDelta, nameof(blendShapeWeightDelta));
        }

        public float LocalPositionDistance { get; }
        public float WorldPositionDistance { get; }
        public float LocalRotationAngle { get; }
        public float WorldRotationAngle { get; }
        public float BlendShapeWeightDelta { get; }

        private static float Validate(float value, string name)
        {
            if (!float.IsFinite(value) || value < 0.0f) throw new ArgumentOutOfRangeException(name);
            return value;
        }
    }

    public sealed class MmdGenericAnimationClipParityFrameResult
    {
        internal MmdGenericAnimationClipParityFrameResult(
            int frame, int boneSampleCount, int blendShapeSampleCount,
            float localPosition, float worldPosition, float localRotation, float worldRotation, float blendShape)
        {
            Frame = frame;
            BoneSampleCount = boneSampleCount;
            BlendShapeSampleCount = blendShapeSampleCount;
            MaxLocalPositionDistance = localPosition;
            MaxWorldPositionDistance = worldPosition;
            MaxLocalRotationAngle = localRotation;
            MaxWorldRotationAngle = worldRotation;
            MaxBlendShapeWeightDelta = blendShape;
        }

        public int Frame { get; }
        public int BoneSampleCount { get; }
        public int BlendShapeSampleCount { get; }
        public float MaxLocalPositionDistance { get; }
        public float MaxWorldPositionDistance { get; }
        public float MaxLocalRotationAngle { get; }
        public float MaxWorldRotationAngle { get; }
        public float MaxBlendShapeWeightDelta { get; }
    }

    public sealed class MmdGenericAnimationClipParityReport
    {
        internal MmdGenericAnimationClipParityReport(
            IReadOnlyList<MmdGenericAnimationClipParityFrameResult> frames,
            IReadOnlyList<string> diagnostics,
            MmdGenericAnimationClipParityTolerance tolerance)
        {
            Frames = new List<MmdGenericAnimationClipParityFrameResult>(frames).AsReadOnly();
            Diagnostics = new List<string>(diagnostics).AsReadOnly();
            Tolerance = tolerance;
            SampleCount = Frames.Count;
            BoneSampleCount = Frames.Sum(frame => frame.BoneSampleCount);
            BlendShapeSampleCount = Frames.Sum(frame => frame.BlendShapeSampleCount);
            MaxLocalPositionDistance = Frames.Count == 0 ? 0 : Frames.Max(frame => frame.MaxLocalPositionDistance);
            MaxWorldPositionDistance = Frames.Count == 0 ? 0 : Frames.Max(frame => frame.MaxWorldPositionDistance);
            MaxLocalRotationAngle = Frames.Count == 0 ? 0 : Frames.Max(frame => frame.MaxLocalRotationAngle);
            MaxWorldRotationAngle = Frames.Count == 0 ? 0 : Frames.Max(frame => frame.MaxWorldRotationAngle);
            MaxBlendShapeWeightDelta = Frames.Count == 0 ? 0 : Frames.Max(frame => frame.MaxBlendShapeWeightDelta);
            Passed = Diagnostics.Count == 0 && Frames.Count > 0
                     && MaxLocalPositionDistance <= tolerance.LocalPositionDistance
                     && MaxWorldPositionDistance <= tolerance.WorldPositionDistance
                     && MaxLocalRotationAngle <= tolerance.LocalRotationAngle
                     && MaxWorldRotationAngle <= tolerance.WorldRotationAngle
                     && MaxBlendShapeWeightDelta <= tolerance.BlendShapeWeightDelta;
        }

        public IReadOnlyList<MmdGenericAnimationClipParityFrameResult> Frames { get; }
        public IReadOnlyList<string> Diagnostics { get; }
        public MmdGenericAnimationClipParityTolerance Tolerance { get; }
        public int SampleCount { get; }
        public int BoneSampleCount { get; }
        public int BlendShapeSampleCount { get; }
        public float MaxLocalPositionDistance { get; }
        public float MaxWorldPositionDistance { get; }
        public float MaxLocalRotationAngle { get; }
        public float MaxWorldRotationAngle { get; }
        public float MaxBlendShapeWeightDelta { get; }
        public bool Passed { get; }
    }

    public static class MmdGenericAnimationClipParityVerifier
    {
        public static MmdGenericAnimationClipParityReport Verify(
            MmdPmxAsset pmxAsset,
            MmdVmdAsset vmdAsset,
            AnimationClip clip,
            float frameRate,
            int startFrame,
            int endFrame,
            IReadOnlyList<int> frames,
            MmdGenericAnimationClipParityTolerance tolerance)
        {
            var diagnostics = new List<string>();
            var results = new List<MmdGenericAnimationClipParityFrameResult>();
            if (pmxAsset == null) diagnostics.Add("validation: pmxAsset is required.");
            if (vmdAsset == null) diagnostics.Add("validation: vmdAsset is required.");
            if (clip == null) diagnostics.Add("validation: clip is required.");
            if (!float.IsFinite(frameRate) || frameRate <= 0) diagnostics.Add("validation: frameRate must be finite and > 0.");
            if (startFrame < 0) diagnostics.Add("validation: startFrame must be >= 0.");
            if (endFrame < startFrame) diagnostics.Add("validation: endFrame must be >= startFrame.");
            if (frames == null || frames.Count == 0) diagnostics.Add("validation: at least one fixed frame is required.");
            if (diagnostics.Count > 0) return new MmdGenericAnimationClipParityReport(results, diagnostics, tolerance);

            MmdUnityPlaybackBinding? nativeBinding = null;
            MmdUnityPlaybackBinding? clipBinding = null;
            bool startedAnimationMode = false;
            try
            {
                nativeBinding = MmdUnityPlaybackBinding.CreateSkinned(pmxAsset, vmdAsset);
                clipBinding = MmdUnityPlaybackBinding.CreateSkinned(pmxAsset, vmdAsset);
                nativeBinding.SetPhysicsMode(MmdPhysicsMode.Off);
                clipBinding.SetPhysicsMode(MmdPhysicsMode.Off);
                if (endFrame > nativeBinding.MotionMaxFrame)
                    diagnostics.Add("validation: endFrame exceeds motion max frame " + nativeBinding.MotionMaxFrame + ".");
                if (startFrame > nativeBinding.MotionMaxFrame)
                    diagnostics.Add("validation: startFrame exceeds motion max frame " + nativeBinding.MotionMaxFrame + ".");
                foreach (int frame in frames)
                    if (frame < startFrame || frame > endFrame)
                        diagnostics.Add("validation: fixed frame " + frame + " is outside the requested frame range.");

                Dictionary<string, Transform> nativeBones = MapBones(nativeBinding.Instance);
                Dictionary<string, Transform> clipBones = MapBones(clipBinding.Instance);
                ValidateCurves(clip, nativeBinding.Instance, nativeBones.Keys, diagnostics);
                if (diagnostics.Count > 0) return new MmdGenericAnimationClipParityReport(results, diagnostics, tolerance);

                if (!AnimationMode.InAnimationMode())
                {
                    AnimationMode.StartAnimationMode();
                    startedAnimationMode = true;
                }

                foreach (int frame in frames)
                {
                    nativeBinding.ApplyFrame(frame, frameRate);
                    AnimationMode.SampleAnimationClip(clipBinding.Instance.Root, clip, (frame - startFrame) / frameRate);
                    float maxLocalPosition = 0, maxWorldPosition = 0, maxLocalRotation = 0, maxWorldRotation = 0;
                    foreach ((string path, Transform nativeBone) in nativeBones)
                    {
                        Transform clipBone = clipBones[path];
                        maxLocalPosition = Mathf.Max(maxLocalPosition, Vector3.Distance(nativeBone.localPosition, clipBone.localPosition));
                        maxWorldPosition = Mathf.Max(maxWorldPosition, Vector3.Distance(nativeBone.position, clipBone.position));
                        maxLocalRotation = Mathf.Max(maxLocalRotation, Quaternion.Angle(nativeBone.localRotation, clipBone.localRotation));
                        maxWorldRotation = Mathf.Max(maxWorldRotation, Quaternion.Angle(nativeBone.rotation, clipBone.rotation));
                    }

                    int morphCount = nativeBinding.Instance.VertexMorphBlendShapes.Count;
                    float maxMorph = 0;
                    for (int i = 0; i < morphCount; i++)
                    {
                        MmdUnityVertexMorphBlendShapeBinding morph = nativeBinding.Instance.VertexMorphBlendShapes[i];
                        float nativeWeight = nativeBinding.Instance.SkinnedMeshRenderer!.GetBlendShapeWeight(morph.BlendShapeIndex);
                        float clipWeight = clipBinding.Instance.SkinnedMeshRenderer!.GetBlendShapeWeight(morph.BlendShapeIndex);
                        maxMorph = Mathf.Max(maxMorph, Mathf.Abs(nativeWeight - clipWeight));
                    }

                    results.Add(new MmdGenericAnimationClipParityFrameResult(
                        frame, nativeBones.Count, morphCount,
                        maxLocalPosition, maxWorldPosition, maxLocalRotation, maxWorldRotation, maxMorph));
                }
            }
            catch (Exception ex)
            {
                diagnostics.Add("verification: " + ex.GetType().Name + ": " + ex.Message);
            }
            finally
            {
                if (startedAnimationMode && AnimationMode.InAnimationMode())
                    AnimationMode.StopAnimationMode();
                DestroyBinding(nativeBinding);
                DestroyBinding(clipBinding);
            }

            return new MmdGenericAnimationClipParityReport(results, diagnostics, tolerance);
        }

        private static Dictionary<string, Transform> MapBones(MmdUnityModelInstance instance)
        {
            return instance.BoneTransforms.ToDictionary(
                bone => AnimationUtility.CalculateTransformPath(bone, instance.Root.transform),
                bone => bone,
                StringComparer.Ordinal);
        }

        private static void ValidateCurves(
            AnimationClip clip,
            MmdUnityModelInstance instance,
            IEnumerable<string> bonePaths,
            List<string> diagnostics)
        {
            var bindings = new HashSet<string>(AnimationUtility.GetCurveBindings(clip).Select(Key), StringComparer.Ordinal);
            string[] positionProperties =
            {
                "m_LocalPosition.x", "m_LocalPosition.y", "m_LocalPosition.z"
            };
            string[] quaternionProperties =
            {
                "m_LocalRotation.x", "m_LocalRotation.y", "m_LocalRotation.z", "m_LocalRotation.w"
            };
            string[] eulerProperties =
            {
                "localEulerAnglesRaw.x", "localEulerAnglesRaw.y", "localEulerAnglesRaw.z"
            };
            foreach (string path in bonePaths)
            {
                foreach (string property in positionProperties)
                    if (!bindings.Contains(Key(EditorCurveBinding.FloatCurve(path, typeof(Transform), property))))
                        diagnostics.Add("binding: missing Transform curve " + path + "|" + property + ".");

                bool hasQuaternion = HasAllTransformCurves(bindings, path, quaternionProperties);
                bool hasEuler = HasAllTransformCurves(bindings, path, eulerProperties);
                if (!hasQuaternion && !hasEuler)
                {
                    diagnostics.Add(
                        "binding: missing Transform rotation curves " + path
                        + " (requires m_LocalRotation.* or localEulerAnglesRaw.*).");
                }
            }

            if (instance.SkinnedMeshRenderer == null && instance.VertexMorphBlendShapes.Count > 0)
            {
                diagnostics.Add("binding: vertex morphs require a SkinnedMeshRenderer.");
                return;
            }

            if (instance.SkinnedMeshRenderer != null)
            {
                string path = AnimationUtility.CalculateTransformPath(instance.SkinnedMeshRenderer.transform, instance.Root.transform);
                foreach (MmdUnityVertexMorphBlendShapeBinding morph in instance.VertexMorphBlendShapes)
                    if (!bindings.Contains(Key(EditorCurveBinding.FloatCurve(path, typeof(SkinnedMeshRenderer), "blendShape." + morph.BlendShapeName))))
                        diagnostics.Add("binding: missing BlendShape curve " + path + "|" + morph.BlendShapeName + ".");
            }
        }

        private static bool HasAllTransformCurves(
            HashSet<string> bindings,
            string path,
            IEnumerable<string> properties)
        {
            return properties.All(property =>
                bindings.Contains(Key(EditorCurveBinding.FloatCurve(path, typeof(Transform), property))));
        }

        private static string Key(EditorCurveBinding binding) => binding.path + "|" + binding.type.FullName + "|" + binding.propertyName;

        private static void DestroyBinding(MmdUnityPlaybackBinding? binding)
        {
            if (binding == null) return;
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
    }
}
