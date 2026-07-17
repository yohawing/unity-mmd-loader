#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Mmd;
using Mmd.Rendering;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Mmd.UnityIntegration
{
    internal sealed class MmdBorrowedSceneMutationLease : IDisposable
    {
        private readonly MmdUnityModelInstance sourceInstance;
        private readonly MmdMaterialOverrideAsset? materialOverride;
        private readonly Material[]? materialRemaps;
        private SkinnedMeshRenderer? renderer;
        private Mesh? originalMesh;
        private Material[] originalMaterials = Array.Empty<Material>();
        private Transform? originalRootBone;
        private Bounds originalLocalBounds;
        private float[] originalBlendShapeWeights = Array.Empty<float>();
        private Vector3[] originalBonePositions = Array.Empty<Vector3>();
        private Quaternion[] originalBoneRotations = Array.Empty<Quaternion>();
        private Vector3[] originalBoneScales = Array.Empty<Vector3>();
        private bool[] originalPhysicsBodyHasTransforms = Array.Empty<bool>();
        private Vector3[] originalPhysicsBodyPositions = Array.Empty<Vector3>();
        private Quaternion[] originalPhysicsBodyRotations = Array.Empty<Quaternion>();
        private Mesh? workingMesh;
        private Material[] workingMaterials = Array.Empty<Material>();
        private MmdUnityModelInstance? workingInstance;
        private bool active;
        private bool disposed;

        internal MmdBorrowedSceneMutationLease(
            MmdUnityModelInstance sourceInstance,
            MmdMaterialOverrideAsset? materialOverride = null,
            Material[]? materialRemaps = null)
        {
            this.sourceInstance = sourceInstance ?? throw new ArgumentNullException(nameof(sourceInstance));
            this.materialOverride = materialOverride;
            this.materialRemaps = materialRemaps;
        }

        internal bool IsActive => active;

        internal MmdUnityModelInstance Activate()
        {
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(MmdBorrowedSceneMutationLease));
            }

            if (active)
            {
                return workingInstance!;
            }

            SkinnedMeshRenderer sourceRenderer = sourceInstance.SkinnedMeshRenderer
                ?? throw new InvalidOperationException("Borrowed MMD playback requires a SkinnedMeshRenderer.");
            Mesh sourceMesh = sourceRenderer.sharedMesh
                ?? throw new InvalidOperationException("Borrowed MMD playback requires an assigned Mesh.");
            Transform[] bones = sourceInstance.BoneTransforms;
            ValidateBones(bones);

            MmdRenderingDescriptor descriptor = CloneRenderingDescriptor(sourceInstance.RenderingDescriptor);
            bool[] excludedSlots = BuildMaterialOverrideExclusionSlots(materialRemaps, sourceRenderer.sharedMaterials.Length);
            MmdMaterialOverrideApplier.ApplyToRenderingDescriptor(materialOverride, descriptor, excludedSlots);

            Mesh? createdMesh = null;
            Material[] createdMaterials = Array.Empty<Material>();
            bool capturedOriginalState = false;
            try
            {
                createdMesh = Object.Instantiate(sourceMesh);
                createdMesh.name = sourceMesh.name + " Playback";
                createdMaterials = CloneMaterials(sourceRenderer.sharedMaterials);
                MmdMaterialOverrideApplier.Apply(materialOverride, createdMaterials, excludedSlots);

                var candidate = new MmdUnityModelInstance(
                    sourceInstance.Root,
                    createdMesh,
                    createdMaterials,
                    descriptor,
                    bones,
                    sourceInstance.PhysicsBodies,
                    meshRenderer: null,
                    sourceRenderer,
                    sourceInstance.SourceContext,
                    Array.Empty<Texture2D>(),
                    sourceInstance.TextureDiagnostics,
                    sourceInstance.ShaderDiagnostics,
                    sourceInstance.ImportScale);
                CopyBindPose(sourceInstance, candidate);

                CaptureOriginalState(sourceRenderer, sourceMesh, bones);
                capturedOriginalState = true;
                sourceRenderer.sharedMesh = createdMesh;
                sourceRenderer.sharedMaterials = createdMaterials;
                renderer = sourceRenderer;
                workingMesh = createdMesh;
                workingMaterials = createdMaterials;
                workingInstance = candidate;
                active = true;
                return candidate;
            }
            catch
            {
                if (capturedOriginalState)
                {
                    RestoreOriginalState(sourceRenderer);
                }

                DestroyCreatedResources(createdMesh, createdMaterials);
                throw;
            }
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            if (!active)
            {
                return;
            }

            active = false;
            if (renderer != null)
            {
                RestoreOriginalState(renderer);
            }

            DestroyCreatedResources(workingMesh, workingMaterials);
            workingMesh = null;
            workingMaterials = Array.Empty<Material>();
            workingInstance = null;
        }

        private void CaptureOriginalState(SkinnedMeshRenderer sourceRenderer, Mesh sourceMesh, Transform[] bones)
        {
            originalMesh = sourceMesh;
            originalMaterials = sourceRenderer.sharedMaterials;
            originalRootBone = sourceRenderer.rootBone;
            originalLocalBounds = sourceRenderer.localBounds;
            originalBlendShapeWeights = new float[sourceMesh.blendShapeCount];
            for (int i = 0; i < originalBlendShapeWeights.Length; i++)
            {
                originalBlendShapeWeights[i] = sourceRenderer.GetBlendShapeWeight(i);
            }

            originalBonePositions = new Vector3[bones.Length];
            originalBoneRotations = new Quaternion[bones.Length];
            originalBoneScales = new Vector3[bones.Length];
            for (int i = 0; i < bones.Length; i++)
            {
                originalBonePositions[i] = bones[i].localPosition;
                originalBoneRotations[i] = bones[i].localRotation;
                originalBoneScales[i] = bones[i].localScale;
            }

            MmdUnityPhysicsBody[] physicsBodies = sourceInstance.PhysicsBodies;
            originalPhysicsBodyHasTransforms = new bool[physicsBodies.Length];
            originalPhysicsBodyPositions = new Vector3[physicsBodies.Length];
            originalPhysicsBodyRotations = new Quaternion[physicsBodies.Length];
            for (int i = 0; i < physicsBodies.Length; i++)
            {
                MmdUnityPhysicsBody body = physicsBodies[i];
                if (body == null)
                {
                    continue;
                }

                originalPhysicsBodyHasTransforms[i] = body.HasNativeTransform;
                originalPhysicsBodyPositions[i] = body.NativePosition;
                originalPhysicsBodyRotations[i] = body.NativeRotation;
            }
        }

        private void RestoreOriginalState(SkinnedMeshRenderer sourceRenderer)
        {
            sourceRenderer.sharedMesh = originalMesh;
            sourceRenderer.sharedMaterials = originalMaterials;
            sourceRenderer.rootBone = originalRootBone;
            sourceRenderer.localBounds = originalLocalBounds;
            RestoreBlendShapeWeights(sourceRenderer, originalMesh, originalBlendShapeWeights);
            RestoreBoneTransforms(sourceInstance.BoneTransforms);
            RestorePhysicsBodyState(sourceInstance.PhysicsBodies);
        }

        private void RestorePhysicsBodyState(MmdUnityPhysicsBody[] physicsBodies)
        {
            int count = Math.Min(physicsBodies.Length, originalPhysicsBodyHasTransforms.Length);
            for (int i = 0; i < count; i++)
            {
                MmdUnityPhysicsBody body = physicsBodies[i];
                if (body == null)
                {
                    continue;
                }

                body.RestoreNativeTransform(
                    originalPhysicsBodyHasTransforms[i],
                    originalPhysicsBodyPositions[i],
                    originalPhysicsBodyRotations[i]);
            }
        }

        private void RestoreBoneTransforms(Transform[] bones)
        {
            int count = Math.Min(bones.Length, originalBonePositions.Length);
            for (int i = 0; i < count; i++)
            {
                Transform bone = bones[i];
                if (bone == null)
                {
                    continue;
                }

                bone.localPosition = originalBonePositions[i];
                bone.localRotation = originalBoneRotations[i];
                bone.localScale = originalBoneScales[i];
            }
        }

        private static void RestoreBlendShapeWeights(
            SkinnedMeshRenderer sourceRenderer,
            Mesh? sourceMesh,
            float[] weights)
        {
            if (sourceMesh == null)
            {
                return;
            }

            int count = Math.Min(sourceMesh.blendShapeCount, weights.Length);
            for (int i = 0; i < count; i++)
            {
                sourceRenderer.SetBlendShapeWeight(i, weights[i]);
            }
        }

        private static void ValidateBones(Transform[] bones)
        {
            for (int i = 0; i < bones.Length; i++)
            {
                if (bones[i] == null)
                {
                    throw new InvalidOperationException($"Borrowed MMD scene bone at index {i} is missing.");
                }
            }
        }

        private static Material[] CloneMaterials(Material[] materials)
        {
            var clones = new Material[materials.Length];
            try
            {
                for (int i = 0; i < materials.Length; i++)
                {
                    Material source = materials[i];
                    if (source != null)
                    {
                        clones[i] = new Material(source) { name = source.name + " Playback" };
                    }
                }

                return clones;
            }
            catch
            {
                DestroyCreatedResources(null, clones);
                throw;
            }
        }

        private static MmdRenderingDescriptor CloneRenderingDescriptor(MmdRenderingDescriptor source)
        {
            string json = JsonUtility.ToJson(source);
            MmdRenderingDescriptor? clone = JsonUtility.FromJson<MmdRenderingDescriptor>(json);
            return clone ?? throw new InvalidOperationException("Failed to clone the borrowed MMD rendering descriptor.");
        }

        private static void CopyBindPose(MmdUnityModelInstance source, MmdUnityModelInstance destination)
        {
            if (source.BindLocalPositions.Length != destination.BindLocalPositions.Length ||
                source.BindLocalRotations.Length != destination.BindLocalRotations.Length)
            {
                throw new InvalidOperationException("Borrowed MMD bind pose does not match the playback bone mapping.");
            }

            Array.Copy(source.BindLocalPositions, destination.BindLocalPositions, source.BindLocalPositions.Length);
            Array.Copy(source.BindLocalRotations, destination.BindLocalRotations, source.BindLocalRotations.Length);
        }

        private static bool[] BuildMaterialOverrideExclusionSlots(Material[]? remaps, int materialSlotCount)
        {
            var excluded = new bool[Math.Max(0, materialSlotCount)];
            if (remaps == null)
            {
                return excluded;
            }

            int count = Math.Min(excluded.Length, remaps.Length);
            for (int i = 0; i < count; i++)
            {
                excluded[i] = remaps[i] != null;
            }

            return excluded;
        }

        private static void DestroyCreatedResources(Mesh? mesh, Material[] materials)
        {
            var destroyedIds = new HashSet<int>();
            foreach (Material material in materials.Where(material => material != null))
            {
                DestroyOnce(material, destroyedIds);
            }

            DestroyOnce(mesh, destroyedIds);
        }

        private static void DestroyOnce(Object? value, HashSet<int> destroyedIds)
        {
            if (value == null || !destroyedIds.Add(value.GetInstanceID()))
            {
                return;
            }

            if (Application.isPlaying)
            {
                Object.Destroy(value);
            }
            else
            {
                Object.DestroyImmediate(value);
            }
        }
    }
}
