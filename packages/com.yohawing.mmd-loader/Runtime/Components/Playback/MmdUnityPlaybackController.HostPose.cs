#nullable enable

using System;
using Mmd;
using Mmd.Native;
using Mmd.Parser;
using Mmd.Physics;
using UnityEngine;

namespace Mmd.UnityIntegration
{
    public sealed partial class MmdUnityPlaybackController
    {
        private MmdRuntimeFfiHostPoseSession? humanoidHostPoseSession;
        private MmdPmxAsset? humanoidHostPoseModelAsset;
        private MmdModelDefinition? humanoidHostPoseModel;
        private byte[]? humanoidHostPoseSourceBytes;
        private Transform[]? humanoidHostPoseBones;
        private Vector3[]? humanoidHostPoseBindLocalPositions;
        private Transform? humanoidHostPoseRoot;
        private float humanoidHostPoseImportScale;
        private float[]? humanoidHostPosePositions;
        private float[]? humanoidHostPoseRotations;
        private float[]? humanoidHostPoseScales;
        private float[]? humanoidHostPoseMorphs;
        private byte[]? humanoidHostPoseIk;
        private float[]? humanoidHostPoseWorldMatrices;
        private bool humanoidHostPoseFailureLatched;
        private MmdPmxAsset? humanoidHostPoseFailureModelAsset;

        private bool TryApplyHumanoidNativeHostPose(MmdHumanoidRetargeterResult result)
        {
            if (humanoidHostPoseFailureLatched && binding == null &&
                !ReferenceEquals(humanoidHostPoseFailureModelAsset, modelAsset))
            {
                ResetHumanoidHostPoseFailureLatch();
            }

            if (humanoidHostPoseFailureLatched ||
                physicsMode != MmdPhysicsMode.Off ||
                (binding == null && modelAsset == null) ||
                result == null || !result.AllSucceeded)
            {
                return false;
            }

            try
            {
                EnsureHumanoidHostPoseSession();
                if (humanoidHostPoseSession == null || humanoidHostPoseModel == null ||
                    humanoidHostPoseBones == null || humanoidHostPoseRoot == null)
                {
                    return false;
                }

                CaptureHumanoidHostPose();
                humanoidHostPoseSession.EvaluateAndCopy(
                    humanoidHostPosePositions!,
                    humanoidHostPoseRotations!,
                    humanoidHostPoseScales!,
                    humanoidHostPoseMorphs!,
                    humanoidHostPoseIk!,
                    humanoidHostPoseWorldMatrices!);
                ApplyHumanoidHostPoseWorldMatrices();
                return true;
            }
            catch (Exception exception) when (IsNativeHostPoseFailure(exception))
            {
                DisposeHumanoidHostPoseSession();
                humanoidHostPoseFailureLatched = true;
                humanoidHostPoseFailureModelAsset = modelAsset;
                return false;
            }
        }

        private void EnsureHumanoidHostPoseSession()
        {
            if (humanoidHostPoseSession != null)
            {
                if (binding != null || ReferenceEquals(humanoidHostPoseModelAsset, modelAsset))
                {
                    return;
                }

                DisposeHumanoidHostPoseSession();
            }

            MmdModelDefinition model;
            byte[] sourceBytes;
            Transform[] bones;
            Transform root;
            float importScale;
            if (binding != null)
            {
                model = binding.NativeHostPoseModel;
                sourceBytes = binding.NativeHostPoseModelSourceBytes
                    ?? throw new InvalidOperationException("Humanoid native host pose requires PMX source bytes.");
                MmdUnityModelInstance instance = binding.PlaybackInstance;
                bones = instance.BoneTransforms;
                root = instance.Root.transform;
                importScale = instance.ImportScale;
            }
            else
            {
                if (modelAsset == null)
                {
                    return;
                }

                if (!ReferenceEquals(humanoidHostPoseModelAsset, modelAsset))
                {
                    model = modelAsset.LoadModel(new NativeMmdParser());
                    sourceBytes = model.sourceBytes ?? modelAsset.GetBytesCopy();
                    SkinnedMeshRenderer renderer = MmdUnityModelFactory.ResolveExistingSkinnedMeshRenderer(gameObject);
                    bones = renderer.bones;
                    root = gameObject.transform;
                    importScale = NormalizeHostPoseImportScale(modelAsset.ImportScale);
                    humanoidHostPoseModelAsset = modelAsset;
                    humanoidHostPoseModel = model;
                    humanoidHostPoseSourceBytes = sourceBytes;
                    humanoidHostPoseBones = bones;
                    humanoidHostPoseBindLocalPositions = BuildHostPoseBindLocalPositions(model, importScale);
                    humanoidHostPoseRoot = root;
                    humanoidHostPoseImportScale = importScale;
                }

                model = humanoidHostPoseModel
                    ?? throw new InvalidOperationException("Humanoid native host pose model was not initialized.");
                sourceBytes = humanoidHostPoseSourceBytes
                    ?? throw new InvalidOperationException("Humanoid native host pose source was not initialized.");
                bones = humanoidHostPoseBones
                    ?? throw new InvalidOperationException("Humanoid native host pose bones were not initialized.");
                root = humanoidHostPoseRoot
                    ?? throw new InvalidOperationException("Humanoid native host pose root was not initialized.");
                importScale = humanoidHostPoseImportScale;
            }

            if (model.bones == null || model.bones.Count == 0 || bones.Length != model.bones.Count)
            {
                throw new InvalidOperationException("Humanoid native host pose bone mapping does not match the PMX model.");
            }

            humanoidHostPoseSession = MmdRuntimeFfiHostPoseSession.Create(sourceBytes);
            if (humanoidHostPoseSession.BoneCount != model.bones.Count)
            {
                throw new InvalidOperationException("Humanoid native host pose session bone count does not match the PMX model.");
            }

            humanoidHostPoseModel = model;
            humanoidHostPoseSourceBytes = sourceBytes;
            humanoidHostPoseBones = bones;
            humanoidHostPoseRoot = root;
            humanoidHostPoseImportScale = importScale;
        }

        private void CaptureHumanoidHostPose()
        {
            MmdModelDefinition model = humanoidHostPoseModel!;
            Transform[] bones = humanoidHostPoseBones!;
            int boneCount = model.bones.Count;
            MmdUnityModelInstance? instance = binding?.PlaybackInstance;
            float scale = NormalizeHostPoseImportScale(humanoidHostPoseImportScale);
            humanoidHostPosePositions = EnsureFloatBuffer(humanoidHostPosePositions, checked(boneCount * 3));
            humanoidHostPoseRotations = EnsureFloatBuffer(humanoidHostPoseRotations, checked(boneCount * 4));
            humanoidHostPoseScales = EnsureFloatBuffer(humanoidHostPoseScales, checked(boneCount * 3));
            humanoidHostPoseMorphs = EnsureFloatBuffer(humanoidHostPoseMorphs, model.morphs.Count);
            humanoidHostPoseIk = EnsureByteBuffer(humanoidHostPoseIk, model.ik.Count);
            humanoidHostPoseWorldMatrices = EnsureFloatBuffer(
                humanoidHostPoseWorldMatrices,
                checked(boneCount * 16));
            Array.Clear(humanoidHostPoseMorphs, 0, humanoidHostPoseMorphs.Length);
            for (int ikIndex = 0; ikIndex < humanoidHostPoseIk.Length; ikIndex++)
            {
                humanoidHostPoseIk[ikIndex] = 1;
            }
            for (int index = 0; index < boneCount; index++)
            {
                Transform bone = bones[index];
                Vector3 bindPosition = instance != null
                    ? instance.BindLocalPositions[index]
                    : humanoidHostPoseBindLocalPositions![index];
                Quaternion bindRotation = instance != null
                    ? instance.BindLocalRotations[index]
                    : Quaternion.identity;
                Vector3 position = MmdCoordinateSpace.UnityToMmdPosition(bone.localPosition - bindPosition) / scale;
                Quaternion rotation = MmdCoordinateSpace.UnityToMmdRotation(
                    Quaternion.Inverse(bindRotation) * bone.localRotation);
                int positionOffset = index * 3;
                humanoidHostPosePositions[positionOffset] = position.x;
                humanoidHostPosePositions[positionOffset + 1] = position.y;
                humanoidHostPosePositions[positionOffset + 2] = position.z;
                int rotationOffset = index * 4;
                humanoidHostPoseRotations[rotationOffset] = rotation.x;
                humanoidHostPoseRotations[rotationOffset + 1] = rotation.y;
                humanoidHostPoseRotations[rotationOffset + 2] = rotation.z;
                humanoidHostPoseRotations[rotationOffset + 3] = rotation.w;
                int scaleOffset = index * 3;
                Vector3 localScale = bone.localScale;
                humanoidHostPoseScales[scaleOffset] = localScale.x;
                humanoidHostPoseScales[scaleOffset + 1] = localScale.y;
                humanoidHostPoseScales[scaleOffset + 2] = localScale.z;
            }
            // Native morph expansion owns bone/material/vertex morph application. These are
            // pre-morph weights; this first Humanoid bridge has no managed morph authoring input.
        }

        private void ApplyHumanoidHostPoseWorldMatrices()
        {
            MmdUnityWorldMatrixFrameApplier.ApplyColumnMajorWorldMatrices(
                humanoidHostPoseRoot!,
                humanoidHostPoseBones!,
                humanoidHostPoseImportScale,
                humanoidHostPoseWorldMatrices!);
        }

        private void RestoreHumanoidHostPoseInputIfAvailable()
        {
            if (humanoidHostPoseModel == null || humanoidHostPoseBones == null ||
                humanoidHostPosePositions == null || humanoidHostPoseRotations == null ||
                humanoidHostPoseScales == null)
            {
                return;
            }

            MmdUnityModelInstance? instance = binding?.PlaybackInstance;
            float scale = NormalizeHostPoseImportScale(humanoidHostPoseImportScale);
            for (int index = 0; index < humanoidHostPoseBones.Length; index++)
            {
                Vector3 bindPosition = instance != null
                    ? instance.BindLocalPositions[index]
                    : humanoidHostPoseBindLocalPositions![index];
                Quaternion bindRotation = instance != null
                    ? instance.BindLocalRotations[index]
                    : Quaternion.identity;
                int positionOffset = index * 3;
                humanoidHostPoseBones[index].localPosition = bindPosition +
                    MmdCoordinateSpace.MmdToUnityPosition(new Vector3(
                        humanoidHostPosePositions[positionOffset],
                        humanoidHostPosePositions[positionOffset + 1],
                        humanoidHostPosePositions[positionOffset + 2])) * scale;
                int rotationOffset = index * 4;
                humanoidHostPoseBones[index].localRotation = bindRotation *
                    MmdCoordinateSpace.MmdToUnityRotation(new Quaternion(
                        humanoidHostPoseRotations[rotationOffset],
                        humanoidHostPoseRotations[rotationOffset + 1],
                        humanoidHostPoseRotations[rotationOffset + 2],
                        humanoidHostPoseRotations[rotationOffset + 3]));
                int scaleOffset = index * 3;
                humanoidHostPoseBones[index].localScale = new Vector3(
                    humanoidHostPoseScales[scaleOffset],
                    humanoidHostPoseScales[scaleOffset + 1],
                    humanoidHostPoseScales[scaleOffset + 2]);
            }
        }

        private static Vector3[] BuildHostPoseBindLocalPositions(
            MmdModelDefinition model,
            float importScale)
        {
            var origins = new Vector3[model.bones.Count];
            foreach (MmdBoneDefinition bone in model.bones)
            {
                if (bone.index < 0 || bone.index >= origins.Length)
                {
                    throw new InvalidOperationException("Humanoid native host pose PMX bone index is invalid: " + bone.index);
                }

                origins[bone.index] = ToHostPoseVector(bone.origin);
            }

            var bindPositions = new Vector3[origins.Length];
            foreach (MmdBoneDefinition bone in model.bones)
            {
                Vector3 localOrigin = origins[bone.index];
                if (bone.parentIndex >= 0)
                {
                    if (bone.parentIndex >= origins.Length)
                    {
                        throw new InvalidOperationException(
                            "Humanoid native host pose PMX parent bone index is invalid: " + bone.parentIndex);
                    }

                    localOrigin -= origins[bone.parentIndex];
                }

                bindPositions[bone.index] = MmdCoordinateSpace.MmdToUnityPosition(localOrigin) * importScale;
            }

            return bindPositions;
        }

        private static Vector3 ToHostPoseVector(float[] values)
        {
            return values != null && values.Length >= 3
                ? new Vector3(values[0], values[1], values[2])
                : Vector3.zero;
        }

        private static float[] EnsureFloatBuffer(float[]? buffer, int length)
        {
            return buffer != null && buffer.Length == length ? buffer : new float[length];
        }

        private static byte[] EnsureByteBuffer(byte[]? buffer, int length)
        {
            return buffer != null && buffer.Length == length ? buffer : new byte[length];
        }

        private static float NormalizeHostPoseImportScale(float value)
        {
            return float.IsFinite(value) && value > 0.0f ? value : 1.0f;
        }

        private static bool IsNativeHostPoseFailure(Exception exception)
        {
            return exception is MmdRuntimeNativeUnavailableException ||
                   exception is MmdRuntimeUnsupportedException ||
                   exception is ArgumentException ||
                   exception is InvalidOperationException ||
                   exception is MissingComponentException ||
                   exception is DllNotFoundException ||
                   exception is EntryPointNotFoundException ||
                   exception is BadImageFormatException;
        }

        private void DisposeHumanoidHostPoseSession()
        {
            RestoreHumanoidHostPoseInputIfAvailable();
            humanoidHostPoseSession?.Dispose();
            humanoidHostPoseSession = null;
            humanoidHostPoseModelAsset = null;
            humanoidHostPoseModel = null;
            humanoidHostPoseSourceBytes = null;
            humanoidHostPoseBones = null;
            humanoidHostPoseBindLocalPositions = null;
            humanoidHostPoseRoot = null;
            humanoidHostPosePositions = null;
            humanoidHostPoseRotations = null;
            humanoidHostPoseScales = null;
            humanoidHostPoseMorphs = null;
            humanoidHostPoseIk = null;
            humanoidHostPoseWorldMatrices = null;
        }

        private void ResetHumanoidHostPoseFailureLatch()
        {
            humanoidHostPoseFailureLatched = false;
            humanoidHostPoseFailureModelAsset = null;
        }
    }
}
