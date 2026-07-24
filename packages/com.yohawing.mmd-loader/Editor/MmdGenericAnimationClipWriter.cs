#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using UnityEditor;
using UnityEngine;
using Mmd.Native;
using Mmd.Physics;
using Mmd.UnityIntegration;

namespace Mmd.Editor
{
    public readonly struct MmdGenericAnimationClipBakeOptions
    {
        public MmdGenericAnimationClipBakeOptions(bool reduceKeys, bool highPrecision)
        {
            ReduceKeys = reduceKeys;
            HighPrecision = highPrecision;
        }

        public bool ReduceKeys { get; }
        public bool HighPrecision { get; }

        public static MmdGenericAnimationClipBakeOptions Default => new(true, false);
    }

    public static class MmdGenericAnimationClipWriter
    {
        public static MmdGenericAnimationClipWriterResult CreateInMemoryClip(
            MmdPmxAsset pmxAsset,
            MmdVmdAsset vmdAsset,
            float frameRate,
            int startFrame = 0,
            int? endFrame = null)
        {
            return CreateInMemoryClip(
                pmxAsset,
                vmdAsset,
                frameRate,
                startFrame,
                endFrame,
                MmdGenericAnimationClipBakeOptions.Default);
        }

        public static MmdGenericAnimationClipWriterResult CreateInMemoryClip(
            MmdPmxAsset pmxAsset,
            MmdVmdAsset vmdAsset,
            float frameRate,
            int startFrame,
            int? endFrame,
            MmdGenericAnimationClipBakeOptions options)
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
                    options,
                    out int compactedCurveCount,
                    out bool usedNativeSparse,
                    out string nativeSparseReason);
                if (usedNativeSparse)
                {
                    diagnostics.Add("writer: built AnimationClip exclusively from mmd-runtime generic sparse track descriptors and keys.");
                }
                else if (!options.ReduceKeys)
                {
                    diagnostics.Add("writer: key reduction disabled; baked dense curves for every sampled frame.");
                }
                else if (!string.IsNullOrEmpty(nativeSparseReason))
                {
                    diagnostics.Add("writer: native sparse reduction unavailable; using dense managed fallback: "
                                    + nativeSparseReason);
                }
                if (!usedNativeSparse)
                {
                    diagnostics.Add("writer: baked Generic transform and vertex morph curves with physics off; compacted "
                                    + compactedCurveCount + " constant curves to endpoint keys.");
                }
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
            return CreateAnimationClipAsset(
                pmxAsset,
                vmdAsset,
                frameRate,
                startFrame,
                endFrame,
                outputPath,
                MmdGenericAnimationClipBakeOptions.Default);
        }

        public static MmdGenericAnimationClipWriterResult CreateAnimationClipAsset(
            MmdPmxAsset pmxAsset,
            MmdVmdAsset vmdAsset,
            float frameRate,
            int startFrame,
            int? endFrame,
            string? outputPath,
            MmdGenericAnimationClipBakeOptions options)
        {
            MmdGenericAnimationClipWriterResult result = CreateInMemoryClip(
                pmxAsset, vmdAsset, frameRate, startFrame, endFrame, options);
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
            return "Assets/" + GetSourceStem(pmxAsset?.SourceId, "PMX") + "_"
                   + GetSourceStem(vmdAsset?.SourceId, "VMD") + ".anim";
        }

        private static AnimationClip BakeDenseClip(
            MmdUnityPlaybackBinding binding,
            MmdPmxAsset pmxAsset,
            MmdVmdAsset vmdAsset,
            float frameRate,
            int startFrame,
            int endFrame,
            MmdGenericAnimationClipBakeOptions options,
            out int compactedCurveCount,
            out bool usedNativeSparse,
            out string nativeSparseReason)
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
            usedNativeSparse = false;
            nativeSparseReason = string.Empty;
            string[] bonePaths = CalculateUniqueBonePaths(instance.BoneTransforms, instance.Root.transform);
            IReadOnlyList<MmdUnityVertexMorphBlendShapeBinding> morphs = instance.VertexMorphBlendShapes;
            if (options.ReduceKeys && TryBakeSparseNativeCurves(
                    binding,
                    instance,
                    pmxAsset,
                    vmdAsset,
                    frameRate,
                    startFrame,
                    frameCount,
                    bonePaths,
                    morphs,
                    clip,
                    options.HighPrecision,
                    out int sparseCurveCount,
                    out nativeSparseReason))
            {
                compactedCurveCount = sparseCurveCount;
                usedNativeSparse = true;
                return clip;
            }

            // Native sparse reduction owns a separate byte budget and may return a
            // small clip for ranges that would be unsafe to materialize as managed
            // Keyframe arrays. Apply this budget only at the dense fallback boundary.
            if (!MmdAnimationClipBakeBudget.TryValidateGeneric(
                    frameCount,
                    instance.BoneTransforms.Length,
                    morphs.Count,
                    out _,
                    out string budgetDiagnostic))
            {
                throw new InvalidOperationException(budgetDiagnostic);
            }

            var positionKeys = new Keyframe[instance.BoneTransforms.Length, 3][];
            var rotationKeys = new Keyframe[instance.BoneTransforms.Length, 4][];
            for (int bone = 0; bone < instance.BoneTransforms.Length; bone++)
            {
                for (int axis = 0; axis < 3; axis++) positionKeys[bone, axis] = new Keyframe[frameCount];
                for (int axis = 0; axis < 4; axis++) rotationKeys[bone, axis] = new Keyframe[frameCount];
            }

            var morphKeys = new Keyframe[morphs.Count][];
            for (int morph = 0; morph < morphs.Count; morph++) morphKeys[morph] = new Keyframe[frameCount];

            if (CanUseNativeBatch(
                    binding,
                    instance,
                    out int[] parentBoneIndices,
                    out Vector3[] staticParentPositions,
                    out Quaternion[] staticParentRotations))
            {
                try
                {
                    FillDenseKeysFromNativeBatch(
                        binding,
                        instance,
                        parentBoneIndices,
                        staticParentPositions,
                        staticParentRotations,
                        morphs,
                        frameRate,
                        startFrame,
                        frameCount,
                        positionKeys,
                        rotationKeys,
                        morphKeys);
                }
                catch (EntryPointNotFoundException)
                {
                    FillDenseKeysThroughUnityTransforms(
                        binding,
                        instance,
                        morphs,
                        frameRate,
                        startFrame,
                        endFrame,
                        positionKeys,
                        rotationKeys,
                        morphKeys);
                }
            }
            else
            {
                FillDenseKeysThroughUnityTransforms(
                    binding,
                    instance,
                    morphs,
                    frameRate,
                    startFrame,
                    endFrame,
                    positionKeys,
                    rotationKeys,
                    morphKeys);
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
                    curves.Add(CreateBakedCurve(
                        positionKeys[bone, axis], options.ReduceKeys, ref compactedCurveCount));
                }
                for (int axis = 0; axis < 4; axis++)
                {
                    curveBindings.Add(EditorCurveBinding.FloatCurve(path, typeof(Transform), rotationProperties[axis]));
                    curves.Add(CreateBakedCurve(
                        rotationKeys[bone, axis], options.ReduceKeys, ref compactedCurveCount));
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
                    curves.Add(CreateBakedCurve(
                        morphKeys[morph], options.ReduceKeys, ref compactedCurveCount));
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

        private static bool TryBakeSparseNativeCurves(
            MmdUnityPlaybackBinding binding,
            MmdUnityModelInstance instance,
            MmdPmxAsset pmxAsset,
            MmdVmdAsset vmdAsset,
            float frameRate,
            int startFrame,
            int frameCount,
            string[] bonePaths,
            IReadOnlyList<MmdUnityVertexMorphBlendShapeBinding> morphs,
            AnimationClip clip,
            bool highPrecision,
            out int sparseCurveCount,
            out string reason)
        {
            sparseCurveCount = 0;
            reason = string.Empty;
            if (!CanUseNativeBatch(binding, instance, out _, out _, out _))
            {
                reason = "native batch input is unavailable or the Unity hierarchy is unsupported.";
                return false;
            }

            try
            {
                MmdRuntimeReducedPose reducedPose;
                using (MmdRuntimeFfiPlaybackSession session = MmdRuntimeFfiPlaybackSession.Create(
                           pmxAsset.GetBytesCopy(), vmdAsset.GetBytesCopy()))
                {
                    if (session.BoneCount != bonePaths.Length)
                    {
                        throw new InvalidOperationException(
                            "native reduced pose bone count does not match the Unity bone mapping.");
                    }

                    reducedPose = session.ReduceBatch(
                        startFrame,
                        frameCount,
                        0,
                        MmdRuntimeFfiMethods.ReductionTolerances.ForUnityAnimationClip(
                            instance.ImportScale,
                            highPrecision));
                }

                using (reducedPose)
                {
                    MmdRuntimeFfiMethods.GenericCurveInfo info = reducedPose.GetGenericCurveInfo();
                    ValidateGenericCurveInfo(info, bonePaths.Length);
                    int trackCount = reducedPose.GetGenericCurveCount();
                    int boneTrackCount = MmdFfiMarshal.CheckedIntPtrToInt(
                        info.boneCount, "generic reduced pose bone count");
                    int morphTrackCount = MmdFfiMarshal.CheckedIntPtrToInt(
                        info.morphCount, "generic reduced pose morph count");
                    if (trackCount != checked(boneTrackCount + morphTrackCount))
                    {
                        throw new InvalidOperationException("native generic reduced pose track count is inconsistent with its metadata.");
                    }

                    var bindings = new List<EditorCurveBinding>(checked(boneTrackCount * 6 + morphTrackCount));
                    var curves = new List<AnimationCurve>(bindings.Capacity);
                    var morphBindings = new Dictionary<int, MmdUnityVertexMorphBlendShapeBinding>();
                    foreach (MmdUnityVertexMorphBlendShapeBinding morph in morphs)
                    {
                        if (!morphBindings.TryAdd(morph.MorphIndex, morph))
                        {
                            throw new InvalidOperationException(
                                "duplicate Unity blendShape mapping for native morph index " + morph.MorphIndex + ".");
                        }
                    }

                    string rendererPath = instance.SkinnedMeshRenderer == null
                        ? string.Empty
                        : AnimationUtility.CalculateTransformPath(
                            instance.SkinnedMeshRenderer.transform, instance.Root.transform);
                    string[] axes = { "x", "y", "z" };
                    for (int trackIndex = 0; trackIndex < trackCount; trackIndex++)
                    {
                        MmdRuntimeFfiMethods.GenericCurveDescriptor descriptor =
                            reducedPose.GetGenericCurveDescriptor(trackIndex);
                        ValidateGenericCurveDescriptor(descriptor);
                        int targetIndex = checked((int)descriptor.targetIndex);
                        MmdRuntimeFfiMethods.GenericCurveKey[] nativeKeys =
                            reducedPose.GetGenericCurveKeys(trackIndex);
                        int declaredKeyCount = MmdFfiMarshal.CheckedIntPtrToInt(
                            descriptor.keyCount, "generic reduced pose descriptor key count");
                        if (nativeKeys.Length != declaredKeyCount)
                        {
                            throw new InvalidOperationException("native generic reduced pose descriptor/key count mismatch.");
                        }

                        if (descriptor.kind == MmdRuntimeFfiMethods.GenericCurveBoneLocal)
                        {
                            if (targetIndex < 0 || targetIndex >= bonePaths.Length ||
                                descriptor.valueFlags != (MmdRuntimeFfiMethods.GenericValueTranslation |
                                                          MmdRuntimeFfiMethods.GenericValueQuaternion) ||
                                descriptor.rotationBasis != MmdRuntimeFfiMethods.GenericRotationBasisRuntimeQuaternion)
                            {
                                throw new InvalidOperationException("native generic reduced pose returned an invalid bone track descriptor.");
                            }

                            Vector3[] eulerDegrees = ConvertGenericRotationKeysToEulerDegrees(nativeKeys);
                            for (int axis = 0; axis < 3; axis++)
                            {
                                float valueScale = axis == 1 ? instance.ImportScale : -instance.ImportScale;
                                var unityKeys = new Keyframe[nativeKeys.Length];
                                for (int keyIndex = 0; keyIndex < nativeKeys.Length; keyIndex++)
                                {
                                    float inTangent = keyIndex == 0
                                        ? 0.0f
                                        : GetTranslationInTangent(nativeKeys[keyIndex], axis);
                                    float outTangent = keyIndex + 1 == nativeKeys.Length
                                        ? 0.0f
                                        : GetTranslationOutTangent(nativeKeys[keyIndex + 1], axis);
                                    unityKeys[keyIndex] = CreateGenericKeyframe(
                                        nativeKeys[keyIndex].frame,
                                        GetTranslation(nativeKeys[keyIndex], axis),
                                        inTangent,
                                        outTangent,
                                        frameRate,
                                        valueScale,
                                        1.0f);
                                }

                                bindings.Add(EditorCurveBinding.FloatCurve(
                                    bonePaths[targetIndex], typeof(Transform), "m_LocalPosition." + axes[axis]));
                                curves.Add(new AnimationCurve(unityKeys));
                            }

                            for (int axis = 0; axis < 3; axis++)
                            {
                                float valueScale = axis == 1 ? 1.0f : -1.0f;
                                var unityKeys = new Keyframe[nativeKeys.Length];
                                for (int keyIndex = 0; keyIndex < nativeKeys.Length; keyIndex++)
                                {
                                    float inTangent = keyIndex == 0
                                        ? 0.0f
                                        : GetRotationInTangent(nativeKeys[keyIndex], axis);
                                    float outTangent = keyIndex + 1 == nativeKeys.Length
                                        ? 0.0f
                                        : GetRotationOutTangent(nativeKeys[keyIndex + 1], axis);
                                    unityKeys[keyIndex] = CreateGenericKeyframe(
                                        nativeKeys[keyIndex].frame,
                                        eulerDegrees[keyIndex][axis],
                                        inTangent,
                                        outTangent,
                                        frameRate,
                                        valueScale,
                                        Mathf.Rad2Deg);
                                }

                                bindings.Add(EditorCurveBinding.FloatCurve(
                                    bonePaths[targetIndex], typeof(Transform), "localEulerAnglesRaw." + axes[axis]));
                                curves.Add(new AnimationCurve(unityKeys));
                            }
                        }
                        else if (descriptor.kind == MmdRuntimeFfiMethods.GenericCurveMorphWeight)
                        {
                            if (descriptor.valueFlags != MmdRuntimeFfiMethods.GenericValueScalar ||
                                descriptor.rotationBasis != MmdRuntimeFfiMethods.GenericRotationBasisNone)
                            {
                                throw new InvalidOperationException("native generic reduced pose returned an invalid morph track descriptor.");
                            }

                            if (instance.SkinnedMeshRenderer == null ||
                                !morphBindings.TryGetValue(targetIndex, out MmdUnityVertexMorphBlendShapeBinding morph))
                            {
                                continue;
                            }

                            var unityKeys = new Keyframe[nativeKeys.Length];
                            for (int keyIndex = 0; keyIndex < nativeKeys.Length; keyIndex++)
                            {
                                float inTangent = keyIndex == 0
                                    ? 0.0f
                                    : nativeKeys[keyIndex].segmentCurrentInScalar;
                                float outTangent = keyIndex + 1 == nativeKeys.Length
                                    ? 0.0f
                                    : nativeKeys[keyIndex + 1].segmentPrevOutScalar;
                                unityKeys[keyIndex] = CreateGenericKeyframe(
                                    nativeKeys[keyIndex].frame,
                                    nativeKeys[keyIndex].scalar,
                                    inTangent,
                                    outTangent,
                                    frameRate,
                                    100.0f,
                                    1.0f);
                            }

                            bindings.Add(EditorCurveBinding.FloatCurve(
                                rendererPath,
                                typeof(SkinnedMeshRenderer),
                                "blendShape." + morph.BlendShapeName));
                            curves.Add(new AnimationCurve(unityKeys));
                        }
                        else
                        {
                            throw new InvalidOperationException("native generic reduced pose returned an unknown track kind.");
                        }
                    }

                    AnimationUtility.SetEditorCurves(clip, bindings.ToArray(), curves.ToArray());
                    SetSparseEulerRotationOrderToXyz(clip);
                    sparseCurveCount = curves.Count;
                    return true;
                }
            }
            catch (Exception ex) when (IsSparseNativeFallbackException(ex))
            {
                reason = ex.GetType().Name + ": " + ex.Message;
                return false;
            }
        }

        internal static bool IsSparseNativeFallbackException(Exception exception)
        {
            return exception is DllNotFoundException or
                EntryPointNotFoundException or
                BadImageFormatException or
                MmdRuntimeUnsupportedException;
        }

        internal static Keyframe CreateGenericKeyframe(
            float frame,
            float value,
            float inTangentPerFrame,
            float outTangentPerFrame,
            float framesPerSecond,
            float valueScale,
            float tangentUnitScale)
        {
            if (!float.IsFinite(frame) || !float.IsFinite(value) ||
                !float.IsFinite(inTangentPerFrame) || !float.IsFinite(outTangentPerFrame) ||
                !float.IsFinite(framesPerSecond) || framesPerSecond <= 0.0f ||
                !float.IsFinite(valueScale) || valueScale == 0.0f ||
                !float.IsFinite(tangentUnitScale) || tangentUnitScale <= 0.0f)
            {
                throw new ArgumentOutOfRangeException(nameof(framesPerSecond));
            }

            return new Keyframe(
                frame / framesPerSecond,
                value * valueScale,
                inTangentPerFrame * framesPerSecond * tangentUnitScale * valueScale,
                outTangentPerFrame * framesPerSecond * tangentUnitScale * valueScale);
        }

        internal static Vector3[] ConvertGenericRotationKeysToEulerDegrees(
            MmdRuntimeFfiMethods.GenericCurveKey[] keys)
        {
            var result = new Vector3[keys.Length];
            double[]? previous = null;
            const double RadiansToDegrees = 180.0 / Math.PI;
            for (int index = 0; index < keys.Length; index++)
            {
                MmdRuntimeFfiMethods.GenericCurveKey key = keys[index];
                double x = key.rotationX;
                double y = key.rotationY;
                double z = key.rotationZ;
                double w = key.rotationW;
                double sinBeta = Math.Max(-1.0, Math.Min(1.0, 2.0 * (w * y - x * z)));
                double beta = Math.Asin(sinBeta);
                double alpha;
                double gamma;
                if (Math.Abs(Math.Cos(beta)) < 1.0e-6)
                {
                    alpha = Math.Atan2(2.0 * (x * y + w * z), 1.0 - 2.0 * (y * y + z * z));
                    gamma = 0.0;
                }
                else
                {
                    alpha = Math.Atan2(2.0 * (y * z + w * x), 1.0 - 2.0 * (x * x + y * y));
                    gamma = Math.Atan2(2.0 * (x * y + w * z), 1.0 - 2.0 * (y * y + z * z));
                }

                var current = new[]
                {
                    alpha * RadiansToDegrees,
                    beta * RadiansToDegrees,
                    gamma * RadiansToDegrees
                };
                if (previous != null)
                {
                    for (int axis = 0; axis < 3; axis++)
                    {
                        while (current[axis] - previous[axis] > 180.0)
                        {
                            current[axis] -= 360.0;
                        }

                        while (current[axis] - previous[axis] < -180.0)
                        {
                            current[axis] += 360.0;
                        }
                    }
                }

                result[index] = new Vector3((float)current[0], (float)current[1], (float)current[2]);
                previous = current;
            }

            return result;
        }

        private static void ValidateGenericCurveInfo(
            MmdRuntimeFfiMethods.GenericCurveInfo info,
            int expectedBoneCount)
        {
            if (info.structSize != Marshal.SizeOf<MmdRuntimeFfiMethods.GenericCurveInfo>() ||
                info.abiVersion != MmdRuntimeFfiMethods.GenericCurveAbiVersionV1 ||
                info.reductionTarget != MmdRuntimeFfiMethods.ReductionTargetDccCubic ||
                info.coordinateSystem != 0 || info.lengthUnit != 0 || info.angleUnit != 0 ||
                info.timeUnit != 0 || info.tangentUnit != 0 ||
                !float.IsFinite(info.startFrame) || !float.IsFinite(info.frameStep) || info.frameStep <= 0.0f ||
                MmdFfiMarshal.CheckedIntPtrToInt(info.boneCount, "generic reduced pose bone count") != expectedBoneCount)
            {
                throw new InvalidOperationException("native generic reduced pose metadata does not match the Unity adapter contract.");
            }
        }

        private static void ValidateGenericCurveDescriptor(
            MmdRuntimeFfiMethods.GenericCurveDescriptor descriptor)
        {
            if (descriptor.structSize != Marshal.SizeOf<MmdRuntimeFfiMethods.GenericCurveDescriptor>() ||
                descriptor.abiVersion != MmdRuntimeFfiMethods.GenericCurveAbiVersionV1 ||
                descriptor.interpolation != MmdRuntimeFfiMethods.ReductionTargetDccCubic)
            {
                throw new InvalidOperationException("native generic reduced pose descriptor does not match the Unity adapter contract.");
            }
        }

        private static float GetTranslation(MmdRuntimeFfiMethods.GenericCurveKey key, int axis)
        {
            return axis switch { 0 => key.translationX, 1 => key.translationY, _ => key.translationZ };
        }

        private static float GetTranslationInTangent(MmdRuntimeFfiMethods.GenericCurveKey key, int axis)
        {
            return axis switch
            {
                0 => key.segmentCurrentInTranslationX,
                1 => key.segmentCurrentInTranslationY,
                _ => key.segmentCurrentInTranslationZ
            };
        }

        private static float GetTranslationOutTangent(MmdRuntimeFfiMethods.GenericCurveKey key, int axis)
        {
            return axis switch
            {
                0 => key.segmentPrevOutTranslationX,
                1 => key.segmentPrevOutTranslationY,
                _ => key.segmentPrevOutTranslationZ
            };
        }

        private static float GetRotationInTangent(MmdRuntimeFfiMethods.GenericCurveKey key, int axis)
        {
            return axis switch
            {
                0 => key.segmentCurrentInRotationX,
                1 => key.segmentCurrentInRotationY,
                _ => key.segmentCurrentInRotationZ
            };
        }

        private static float GetRotationOutTangent(MmdRuntimeFfiMethods.GenericCurveKey key, int axis)
        {
            return axis switch
            {
                0 => key.segmentPrevOutRotationX,
                1 => key.segmentPrevOutRotationY,
                _ => key.segmentPrevOutRotationZ
            };
        }

        internal static void SetSparseEulerRotationOrderToXyz(AnimationClip clip)
        {
            if (clip == null)
            {
                throw new ArgumentNullException(nameof(clip));
            }

            const int XyzRotationOrder = 0;
            var serializedClip = new SerializedObject(clip);
            SetCurveArrayRotationOrder(serializedClip.FindProperty("m_EulerCurves"), XyzRotationOrder);
            SetCurveArrayRotationOrder(serializedClip.FindProperty("m_EulerEditorCurves"), XyzRotationOrder);

            SerializedProperty? editorCurves = serializedClip.FindProperty("m_EditorCurves");
            if (editorCurves != null && editorCurves.isArray)
            {
                for (int index = 0; index < editorCurves.arraySize; index++)
                {
                    SerializedProperty curve = editorCurves.GetArrayElementAtIndex(index);
                    SerializedProperty? attribute = curve.FindPropertyRelative("attribute");
                    if (attribute != null &&
                        attribute.stringValue.StartsWith("localEulerAnglesRaw.", StringComparison.Ordinal))
                    {
                        SetSerializedCurveRotationOrder(curve, XyzRotationOrder);
                    }
                }
            }

            serializedClip.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetCurveArrayRotationOrder(SerializedProperty? curves, int rotationOrder)
        {
            if (curves == null || !curves.isArray)
            {
                return;
            }

            for (int index = 0; index < curves.arraySize; index++)
            {
                SetSerializedCurveRotationOrder(curves.GetArrayElementAtIndex(index), rotationOrder);
            }
        }

        private static void SetSerializedCurveRotationOrder(SerializedProperty curve, int rotationOrder)
        {
            SerializedProperty? property = curve.FindPropertyRelative("curve.m_RotationOrder");
            if (property != null)
            {
                property.intValue = rotationOrder;
            }
        }

        internal static bool CanUseNativeBatch(
            MmdUnityPlaybackBinding binding,
            MmdUnityModelInstance instance,
            out int[] parentBoneIndices,
            out Vector3[] staticParentPositions,
            out Quaternion[] staticParentRotations,
            bool requireMorphCompatibility = true)
        {
            parentBoneIndices = Array.Empty<int>();
            staticParentPositions = Array.Empty<Vector3>();
            staticParentRotations = Array.Empty<Quaternion>();
            if (!binding.HasFastRuntimeBatch ||
                binding.FastRuntimeWorldMatrixFloatCount != instance.BoneTransforms.Length * 16 ||
                requireMorphCompatibility &&
                (binding.FastRuntimeMorphWeightCount <= 0 && instance.VertexMorphBlendShapes.Count > 0 ||
                 instance.RenderingDescriptor.flipMorphs.Count > 0))
            {
                return false;
            }

            var boneIndices = new Dictionary<Transform, int>(instance.BoneTransforms.Length);
            for (int bone = 0; bone < instance.BoneTransforms.Length; bone++)
            {
                boneIndices[instance.BoneTransforms[bone]] = bone;
            }

            parentBoneIndices = new int[instance.BoneTransforms.Length];
            staticParentPositions = new Vector3[instance.BoneTransforms.Length];
            staticParentRotations = new Quaternion[instance.BoneTransforms.Length];
            Transform root = instance.Root.transform;
            for (int bone = 0; bone < instance.BoneTransforms.Length; bone++)
            {
                Transform? parent = instance.BoneTransforms[bone].parent;
                if (parent != null && boneIndices.TryGetValue(parent, out int parentBone))
                {
                    parentBoneIndices[bone] = parentBone;
                }
                else if (parent != null && (parent == root || parent.IsChildOf(root)))
                {
                    parentBoneIndices[bone] = -1;
                    staticParentPositions[bone] = root.InverseTransformPoint(parent.position);
                    staticParentRotations[bone] = Quaternion.Inverse(root.rotation) * parent.rotation;
                }
                else
                {
                    parentBoneIndices = Array.Empty<int>();
                    staticParentPositions = Array.Empty<Vector3>();
                    staticParentRotations = Array.Empty<Quaternion>();
                    return false;
                }
            }

            return true;
        }

        private static void FillDenseKeysFromNativeBatch(
            MmdUnityPlaybackBinding binding,
            MmdUnityModelInstance instance,
            int[] parentBoneIndices,
            Vector3[] staticParentPositions,
            Quaternion[] staticParentRotations,
            IReadOnlyList<MmdUnityVertexMorphBlendShapeBinding> morphs,
            float frameRate,
            int startFrame,
            int frameCount,
            Keyframe[,][] positionKeys,
            Keyframe[,][] rotationKeys,
            Keyframe[][] morphKeys)
        {
            const int ChunkFrameCount = 256;
            int worldFloatsPerFrame = binding.FastRuntimeWorldMatrixFloatCount;
            int morphFloatsPerFrame = binding.FastRuntimeMorphWeightCount;
            var worldMatrices = new float[checked(worldFloatsPerFrame * Math.Min(frameCount, ChunkFrameCount))];
            var morphWeights = new float[checked(morphFloatsPerFrame * Math.Min(frameCount, ChunkFrameCount))];
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
                    int sample = chunkStart + chunkSample;
                    float time = sample / frameRate;
                    ReadOnlySpan<float> frameWorld = worldMatrices.AsSpan(
                        chunkSample * worldFloatsPerFrame,
                        worldFloatsPerFrame);
                    for (int bone = 0; bone < instance.BoneTransforms.Length; bone++)
                    {
                        ReadWorldPose(frameWorld, bone, out Vector3 worldPosition, out Quaternion worldRotation);
                        int parentBone = parentBoneIndices[bone];
                        Vector3 localPosition;
                        Quaternion localRotation;
                        if (parentBone >= 0)
                        {
                            ReadWorldPose(frameWorld, parentBone, out Vector3 parentPosition, out Quaternion parentRotation);
                            Quaternion inverseParent = Quaternion.Inverse(parentRotation);
                            localPosition = inverseParent * (worldPosition - parentPosition) * importScale;
                            localRotation = inverseParent * worldRotation;
                        }
                        else
                        {
                            Quaternion inverseParent = Quaternion.Inverse(staticParentRotations[bone]);
                            localPosition = inverseParent * (
                                worldPosition * importScale - staticParentPositions[bone]);
                            localRotation = inverseParent * worldRotation;
                        }

                        if (sample > 0)
                        {
                            Quaternion previous = new Quaternion(
                                rotationKeys[bone, 0][sample - 1].value,
                                rotationKeys[bone, 1][sample - 1].value,
                                rotationKeys[bone, 2][sample - 1].value,
                                rotationKeys[bone, 3][sample - 1].value);
                            if (Quaternion.Dot(previous, localRotation) < 0.0f)
                            {
                                localRotation = new Quaternion(
                                    -localRotation.x,
                                    -localRotation.y,
                                    -localRotation.z,
                                    -localRotation.w);
                            }
                        }

                        positionKeys[bone, 0][sample] = new Keyframe(time, localPosition.x);
                        positionKeys[bone, 1][sample] = new Keyframe(time, localPosition.y);
                        positionKeys[bone, 2][sample] = new Keyframe(time, localPosition.z);
                        rotationKeys[bone, 0][sample] = new Keyframe(time, localRotation.x);
                        rotationKeys[bone, 1][sample] = new Keyframe(time, localRotation.y);
                        rotationKeys[bone, 2][sample] = new Keyframe(time, localRotation.z);
                        rotationKeys[bone, 3][sample] = new Keyframe(time, localRotation.w);
                    }

                    int morphOffset = chunkSample * morphFloatsPerFrame;
                    for (int morph = 0; morph < morphs.Count; morph++)
                    {
                        morphKeys[morph][sample] = new Keyframe(
                            time,
                            morphWeights[morphOffset + morphs[morph].MorphIndex] * 100.0f);
                    }
                }
            }
        }

        internal static void ReadWorldPose(
            ReadOnlySpan<float> frameWorld,
            int bone,
            out Vector3 position,
            out Quaternion rotation)
        {
            int offset = bone * 16;
            position = MmdCoordinateSpace.MmdToUnityPosition(new Vector3(
                frameWorld[offset + 12],
                frameWorld[offset + 13],
                frameWorld[offset + 14]));
            Vector3 forward = new Vector3(
                frameWorld[offset + 8],
                frameWorld[offset + 9],
                frameWorld[offset + 10]);
            Vector3 up = new Vector3(
                frameWorld[offset + 4],
                frameWorld[offset + 5],
                frameWorld[offset + 6]);
            Quaternion mmdRotation = forward.sqrMagnitude > 0.0f && up.sqrMagnitude > 0.0f
                ? Quaternion.LookRotation(forward.normalized, up.normalized)
                : Quaternion.identity;
            rotation = MmdCoordinateSpace.MmdToUnityRotation(mmdRotation);
        }

        private static void FillDenseKeysThroughUnityTransforms(
            MmdUnityPlaybackBinding binding,
            MmdUnityModelInstance instance,
            IReadOnlyList<MmdUnityVertexMorphBlendShapeBinding> morphs,
            float frameRate,
            int startFrame,
            int endFrame,
            Keyframe[,][] positionKeys,
            Keyframe[,][] rotationKeys,
            Keyframe[][] morphKeys)
        {
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
        }

        private static AnimationCurve CreateBakedCurve(
            Keyframe[] denseKeys,
            bool reduceKeys,
            ref int compactedCurveCount)
        {
            if (!reduceKeys || denseKeys.Length <= 1)
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

        private static string GetSourceStem(string? sourceId, string fallback)
        {
            string fileName = string.IsNullOrWhiteSpace(sourceId)
                ? fallback
                : Path.GetFileNameWithoutExtension(sourceId!.Replace('\\', '/')) ?? fallback;
            return NormalizeIdentifier(fileName);
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
