#nullable enable

using System;
using System.Collections.Generic;
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
        private uint[]? humanoidHostPoseDrivenBones;
        private uint[]? humanoidHostPoseGoalBones;
        private byte[]? humanoidHostPoseIkEligibility;
        private bool humanoidHostPoseFailureLatched;
        private MmdPmxAsset? humanoidHostPoseFailureModelAsset;
        private string humanoidHostPoseFailureReason = string.Empty;
        private uint humanoidHostPoseInputFingerprint;

        /// <summary>
        /// Reports the most recent native HostRig failure. An empty value means no failure has
        /// been recorded since the last Humanoid retarget configuration change.
        /// </summary>
        public string LastHumanoidNativeHostPoseFailureReason => humanoidHostPoseFailureReason;

        private bool TryApplyHumanoidNativeHostPose(
            bool copyLocalPositions,
            out MmdHumanoidRetargeterResult? nativeResult)
        {
            nativeResult = null;
            if (humanoidHostPoseFailureLatched && binding == null &&
                !ReferenceEquals(humanoidHostPoseFailureModelAsset, modelAsset))
            {
                ResetHumanoidHostPoseFailureLatch();
            }

            if (humanoidHostPoseFailureLatched ||
                physicsMode != MmdPhysicsMode.Off ||
                (binding == null && modelAsset == null))
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

                PrepareHumanoidHostPoseArrays();
                nativeResult = MmdHumanoidRetargeter.RetargetPoseToNativeArrays(
                    humanoidRetargetEntries,
                    humanoidHostPoseModel,
                    humanoidHostPoseBones,
                    humanoidHostPoseImportScale,
                    humanoidHostPosePositions!,
                    humanoidHostPoseRotations!,
                    humanoidHostPoseScales!,
                    copyLocalPositions);
                if (!nativeResult.AllSucceeded)
                {
                    humanoidHostPoseFailureReason = nativeResult.Diagnostics.Count > 0
                        ? nativeResult.Diagnostics[0]
                        : "Humanoid native HostRig retarget validation failed.";
                    ResetHumanoidHostPoseDisplayToBindRest();
                    return false;
                }

                humanoidHostPoseSession.EvaluateAndCopy(
                    humanoidHostPosePositions!,
                    humanoidHostPoseRotations!,
                    humanoidHostPoseScales!,
                    humanoidHostPoseMorphs!,
                    humanoidHostPoseIk!,
                    humanoidHostPoseWorldMatrices!);
                ApplyHumanoidHostPoseWorldMatrices();
                humanoidHostPoseFailureReason = string.Empty;
                return true;
            }
            catch (Exception exception) when (IsNativeHostPoseFailure(exception))
            {
                ResetHumanoidHostPoseDisplayToBindRest();
                RecordHumanoidHostPoseFailure(exception, restoreInput: false);
                return false;
            }
        }

        /// <summary>
        /// Captures the retargeted (pre-append) Unity pose after the Live native binding has been
        /// prepared. The live physics binding evaluates and applies the native final world matrices
        /// later in the frame; this capture deliberately does not mutate Unity transforms.
        /// </summary>
        private bool TryCaptureHumanoidNativeLivePose(MmdHumanoidRetargeterResult result)
        {
            if (humanoidHostPoseFailureLatched ||
                physicsMode != MmdPhysicsMode.Live ||
                humanoidHostPoseModel == null ||
                humanoidHostPoseBones == null ||
                humanoidHostPoseRoot == null ||
                result == null || result.CopiedBoneCount <= 0)
            {
                return false;
            }

            try
            {
                // Keep a clean pre-append input snapshot so the next frame can restore helper
                // bones that the previous native evaluation owned.
                CaptureHumanoidHostPose();
                return true;
            }
            catch (Exception exception) when (IsNativeHostPoseFailure(exception))
            {
                RecordHumanoidHostPoseFailure(exception);
                return false;
            }
        }

        private bool TryPrepareHumanoidNativeLivePhysics()
        {
            if (humanoidHostPoseFailureLatched || physicsMode != MmdPhysicsMode.Live)
            {
                return false;
            }

            try
            {
                EnsureHumanoidHostPoseSession(createNativeSession: false);
                if (humanoidHostPoseModel == null ||
                    humanoidHostPoseBones == null || humanoidHostPoseRoot == null)
                {
                    return false;
                }

                return TryPrepareHumanoidNativeLivePhysicsBinding();
            }
            catch (Exception exception) when (IsNativeHostPoseFailure(exception))
            {
                RecordHumanoidHostPoseFailure(exception);
                return false;
            }
        }

        private void EnsureHumanoidHostPoseSession(bool createNativeSession = true)
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

            humanoidHostPoseModel = model;
            humanoidHostPoseSourceBytes = sourceBytes;
            humanoidHostPoseBones = bones;
            humanoidHostPoseRoot = root;
            humanoidHostPoseImportScale = importScale;
            if (!createNativeSession)
            {
                return;
            }

            if (humanoidHostPoseDrivenBones == null ||
                humanoidHostPoseGoalBones == null ||
                humanoidHostPoseIkEligibility == null)
            {
                ConfigureHumanoidHostPoseRig(model, bones);
            }

            humanoidHostPoseSession = MmdRuntimeFfiHostPoseSession.CreateWithRig(
                sourceBytes,
                humanoidHostPoseDrivenBones!,
                humanoidHostPoseGoalBones!);
            if (humanoidHostPoseSession.BoneCount != model.bones.Count)
            {
                throw new InvalidOperationException("Humanoid native host pose session bone count does not match the PMX model.");
            }

        }

        internal readonly struct HumanoidHostPoseRigSelection
        {
            internal HumanoidHostPoseRigSelection(
                uint[] drivenBones,
                uint[] goalBones,
                byte[] ikEligibility)
            {
                DrivenBones = drivenBones;
                GoalBones = goalBones;
                IkEligibility = ikEligibility;
            }

            internal uint[] DrivenBones { get; }
            internal uint[] GoalBones { get; }
            internal byte[] IkEligibility { get; }
        }

        private void ConfigureHumanoidHostPoseRig(
            MmdModelDefinition model,
            Transform[] bones)
        {
            HumanoidHostPoseRigSelection selection = BuildHumanoidHostPoseRigSelection(
                model,
                bones,
                humanoidRetargetEntries);
            humanoidHostPoseDrivenBones = selection.DrivenBones;
            humanoidHostPoseGoalBones = selection.GoalBones;
            humanoidHostPoseIkEligibility = selection.IkEligibility;
        }

        internal static HumanoidHostPoseRigSelection BuildHumanoidHostPoseRigSelection(
            MmdModelDefinition model,
            Transform[] bones,
            IReadOnlyList<MmdHumanoidRetargetBinding> entries)
        {
            ValidateHostPoseTopology(model);
            if (bones.Length != model.bones.Count)
            {
                throw new InvalidOperationException(
                    "Humanoid native host pose bone mapping does not match the PMX model.");
            }

            var driven = new List<int>();
            var drivenSet = new HashSet<int>();
            for (int entryIndex = 0; entryIndex < entries.Count; entryIndex++)
            {
                MmdHumanoidRetargetBinding? entry = entries[entryIndex];
                if (entry == null || entry.ProxyTransform == null)
                {
                    continue;
                }

                if (IsActualHostPoseTarget(entry.NativeTransform, bones, entry.MmdBoneIndex) &&
                    drivenSet.Add(entry.MmdBoneIndex))
                {
                    driven.Add(entry.MmdBoneIndex);
                }

                if (entry.CopyLocalPosition &&
                    IsActualHostPoseTarget(
                        entry.TranslationTargetTransform,
                        bones,
                        entry.TranslationTargetMmdBoneIndex) &&
                    drivenSet.Add(entry.TranslationTargetMmdBoneIndex))
                {
                    driven.Add(entry.TranslationTargetMmdBoneIndex);
                }
            }

            var goals = new List<uint>();
            var goalSet = new HashSet<int>();
            var ikEligibility = new byte[model.ik.Count];
            for (int ikIndex = 0; ikIndex < model.ik.Count; ikIndex++)
            {
                MmdIkDefinition? ik = model.ik[ikIndex];
                if (ik == null)
                {
                    throw new InvalidOperationException(
                        "Humanoid native host pose PMX IK definition is null at index " + ikIndex + ".");
                }

                bool eligible = IsStrictDescendantOfDriven(ik.boneIndex, drivenSet, model) &&
                                IsIkControllerConflictFree(ik.boneIndex, drivenSet, model);
                if (eligible)
                {
                    if (goalSet.Add(ik.boneIndex))
                    {
                        goals.Add((uint)ik.boneIndex);
                    }

                    ikEligibility[ikIndex] = 1;
                }
            }

            return new HumanoidHostPoseRigSelection(
                ToNativeBoneIndices(driven),
                goals.ToArray(),
                ikEligibility);
        }

        private static uint[] ToNativeBoneIndices(List<int> indices)
        {
            var result = new uint[indices.Count];
            for (int index = 0; index < indices.Count; index++)
            {
                result[index] = checked((uint)indices[index]);
            }

            return result;
        }

        private static bool IsActualHostPoseTarget(
            Transform? target,
            IReadOnlyList<Transform> bones,
            int index)
        {
            return index >= 0 && index < bones.Count &&
                   target != null && ReferenceEquals(target, bones[index]);
        }

        private static bool IsStrictDescendantOfDriven(
            int controllerIndex,
            HashSet<int> driven,
            MmdModelDefinition model)
        {
            int current = model.bones[controllerIndex].parentIndex;
            while (current >= 0)
            {
                if (driven.Contains(current))
                {
                    return true;
                }

                current = model.bones[current].parentIndex;
            }

            return false;
        }

        private static bool IsIkControllerConflictFree(
            int controllerIndex,
            HashSet<int> driven,
            MmdModelDefinition model)
        {
            for (int chainIndex = 0; chainIndex < model.ik.Count; chainIndex++)
            {
                MmdIkDefinition chain = model.ik[chainIndex];
                if (chain.boneIndex != controllerIndex)
                {
                    continue;
                }

                for (int linkIndex = 0; linkIndex < chain.links.Count; linkIndex++)
                {
                    int linkBoneIndex = chain.links[linkIndex].boneIndex;
                    foreach (int drivenBoneIndex in driven)
                    {
                        int current = drivenBoneIndex;
                        while (current >= 0)
                        {
                            if (current == linkBoneIndex)
                            {
                                return false;
                            }

                            current = model.bones[current].parentIndex;
                        }
                    }
                }
            }

            return true;
        }

        private static void ValidateHostPoseTopology(MmdModelDefinition model)
        {
            if (model.bones == null || model.bones.Count == 0)
            {
                throw new InvalidOperationException(
                    "Humanoid native host pose PMX bone topology is empty.");
            }

            for (int index = 0; index < model.bones.Count; index++)
            {
                MmdBoneDefinition? bone = model.bones[index];
                if (bone == null || bone.index != index)
                {
                    throw new InvalidOperationException(
                        "Humanoid native host pose PMX bone topology has a non-contiguous index at " + index + ".");
                }

                if (bone.parentIndex < -1 || bone.parentIndex >= model.bones.Count)
                {
                    throw new InvalidOperationException(
                        "Humanoid native host pose PMX parent index is invalid for bone " + index + ": " +
                        bone.parentIndex + ".");
                }
            }

            for (int startIndex = 0; startIndex < model.bones.Count; startIndex++)
            {
                int current = startIndex;
                int hopCount = 0;
                while (current >= 0)
                {
                    if (++hopCount > model.bones.Count)
                    {
                        throw new InvalidOperationException(
                            "Humanoid native host pose PMX bone topology contains a parent cycle at " + current + ".");
                    }

                    current = model.bones[current].parentIndex;
                }
            }

            if (model.ik == null)
            {
                throw new InvalidOperationException(
                    "Humanoid native host pose PMX IK topology is null.");
            }

            for (int ikIndex = 0; ikIndex < model.ik.Count; ikIndex++)
            {
                MmdIkDefinition? ik = model.ik[ikIndex];
                if (ik == null || ik.boneIndex < 0 || ik.boneIndex >= model.bones.Count ||
                    ik.targetBoneIndex < 0 || ik.targetBoneIndex >= model.bones.Count)
                {
                    throw new InvalidOperationException(
                        "Humanoid native host pose PMX IK topology is invalid at index " + ikIndex + ".");
                }

                if (ik.links == null)
                {
                    throw new InvalidOperationException(
                        "Humanoid native host pose PMX IK links are null at index " + ikIndex + ".");
                }

                for (int linkIndex = 0; linkIndex < ik.links.Count; linkIndex++)
                {
                    MmdIkLinkDefinition? link = ik.links[linkIndex];
                    if (link == null || link.boneIndex < 0 || link.boneIndex >= model.bones.Count)
                    {
                        throw new InvalidOperationException(
                            "Humanoid native host pose PMX IK link topology is invalid at " +
                            ikIndex + "[" + linkIndex + "].");
                    }
                }
            }
        }

        private void CaptureHumanoidHostPose()
        {
            MmdModelDefinition model = humanoidHostPoseModel!;
            Transform[] bones = humanoidHostPoseBones!;
            int boneCount = model.bones.Count;
            MmdUnityModelInstance? instance = binding?.PlaybackInstance ?? humanoidPhysicsBinding?.PlaybackInstance;
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

            humanoidHostPoseInputFingerprint = ComputeHumanoidHostPoseInputFingerprint(
                humanoidHostPosePositions,
                humanoidHostPoseRotations,
                humanoidHostPoseScales);
            // Native morph expansion owns bone/material/vertex morph application. These are
            // pre-morph weights; this first Humanoid bridge has no managed morph authoring input.
        }

        private void PrepareHumanoidHostPoseArrays()
        {
            MmdModelDefinition model = humanoidHostPoseModel!;
            int boneCount = model.bones.Count;
            humanoidHostPosePositions = EnsureFloatBuffer(
                humanoidHostPosePositions,
                checked(boneCount * 3));
            humanoidHostPoseRotations = EnsureFloatBuffer(
                humanoidHostPoseRotations,
                checked(boneCount * 4));
            humanoidHostPoseScales = EnsureFloatBuffer(
                humanoidHostPoseScales,
                checked(boneCount * 3));
            humanoidHostPoseMorphs = EnsureFloatBuffer(
                humanoidHostPoseMorphs,
                model.morphs.Count);
            humanoidHostPoseIk = EnsureByteBuffer(
                humanoidHostPoseIk,
                model.ik.Count);
            humanoidHostPoseWorldMatrices = EnsureFloatBuffer(
                humanoidHostPoseWorldMatrices,
                checked(boneCount * 16));
            Array.Clear(humanoidHostPoseMorphs, 0, humanoidHostPoseMorphs.Length);
            Array.Clear(humanoidHostPoseWorldMatrices, 0, humanoidHostPoseWorldMatrices.Length);
            Array.Clear(humanoidHostPoseIk, 0, humanoidHostPoseIk.Length);
            if (humanoidHostPoseIkEligibility != null)
            {
                Array.Copy(
                    humanoidHostPoseIkEligibility,
                    humanoidHostPoseIk,
                    humanoidHostPoseIkEligibility.Length);
            }
        }

        private static uint ComputeHumanoidHostPoseInputFingerprint(
            float[] positions,
            float[] rotations,
            float[] scales)
        {
            unchecked
            {
                uint hash = 2166136261u;
                MixHumanoidHostPoseFingerprint(ref hash, positions);
                MixHumanoidHostPoseFingerprint(ref hash, rotations);
                MixHumanoidHostPoseFingerprint(ref hash, scales);
                return hash;
            }
        }

        private static void MixHumanoidHostPoseFingerprint(ref uint hash, float[] values)
        {
            for (int i = 0; i < values.Length; i++)
            {
                hash ^= (uint)values[i].GetHashCode();
                hash *= 16777619u;
            }
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

            MmdUnityModelInstance? instance = binding?.PlaybackInstance ?? humanoidPhysicsBinding?.PlaybackInstance;
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

        private void ResetHumanoidHostPoseDisplayToBindRest()
        {
            if (humanoidHostPoseModel == null || humanoidHostPoseModel.bones == null ||
                humanoidHostPoseBones == null)
            {
                return;
            }

            int boneCount = humanoidHostPoseModel.bones.Count;
            if (boneCount <= 0 || humanoidHostPoseBones.Length != boneCount)
            {
                return;
            }

            MmdUnityModelInstance? instance = binding?.PlaybackInstance ?? humanoidPhysicsBinding?.PlaybackInstance;
            if (instance != null &&
                (instance.BindLocalPositions == null || instance.BindLocalRotations == null ||
                 instance.BindLocalPositions.Length != boneCount ||
                 instance.BindLocalRotations.Length != boneCount))
            {
                return;
            }

            if (instance == null &&
                (humanoidHostPoseBindLocalPositions == null ||
                 humanoidHostPoseBindLocalPositions.Length != boneCount))
            {
                return;
            }

            for (int index = 0; index < boneCount; index++)
            {
                if (humanoidHostPoseBones[index] == null)
                {
                    return;
                }

                Vector3 bindPosition = instance != null
                    ? instance.BindLocalPositions[index]
                    : humanoidHostPoseBindLocalPositions![index];
                Quaternion bindRotation = instance != null
                    ? instance.BindLocalRotations[index]
                    : Quaternion.identity;
                if (!float.IsFinite(bindPosition.x) || !float.IsFinite(bindPosition.y) ||
                    !float.IsFinite(bindPosition.z) || !float.IsFinite(bindRotation.x) ||
                    !float.IsFinite(bindRotation.y) || !float.IsFinite(bindRotation.z) ||
                    !float.IsFinite(bindRotation.w))
                {
                    return;
                }
            }

            for (int index = 0; index < boneCount; index++)
            {
                Transform bone = humanoidHostPoseBones[index];
                Vector3 bindPosition = instance != null
                    ? instance.BindLocalPositions[index]
                    : humanoidHostPoseBindLocalPositions![index];
                Quaternion bindRotation = instance != null
                    ? instance.BindLocalRotations[index]
                    : Quaternion.identity;
                bone.localPosition = bindPosition;
                bone.localRotation = bindRotation;
                bone.localScale = Vector3.one;
            }
        }

        private static Vector3[] BuildHostPoseBindLocalPositions(
            MmdModelDefinition model,
            float importScale)
        {
            ValidateHostPoseTopology(model);
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

        private void DisposeHumanoidHostPoseSession(bool restoreInput = true)
        {
            if (restoreInput)
            {
                RestoreHumanoidHostPoseInputIfAvailable();
            }

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
            humanoidHostPoseDrivenBones = null;
            humanoidHostPoseGoalBones = null;
            humanoidHostPoseIkEligibility = null;
        }

        private void ResetHumanoidHostPoseFailureLatch()
        {
            humanoidHostPoseFailureLatched = false;
            humanoidHostPoseFailureModelAsset = null;
            humanoidHostPoseFailureReason = string.Empty;
        }

        private void RecordHumanoidHostPoseFailure(Exception exception, bool restoreInput = true)
        {
            humanoidHostPoseFailureReason = exception.GetType().Name + ": " + exception.Message;
            DisposeHumanoidHostPoseSession(restoreInput);
            humanoidHostPoseFailureLatched = true;
            humanoidHostPoseFailureModelAsset = modelAsset;
        }
    }
}
