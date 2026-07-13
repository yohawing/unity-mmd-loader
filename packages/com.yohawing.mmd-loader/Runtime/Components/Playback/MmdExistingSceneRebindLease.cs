#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Mmd.UnityIntegration
{
    internal sealed class MmdExistingSceneRebindLease : IDisposable
    {
        private readonly SkinnedMeshRenderer renderer;
        private readonly Mesh? originalMesh;
        private readonly Material[] originalMaterials;
        private readonly Transform? originalRootBone;
        private readonly Bounds originalLocalBounds;
        private readonly Transform[] bones;
        private readonly Vector3[] originalBonePositions;
        private readonly Quaternion[] originalBoneRotations;
        private readonly Vector3[] originalBoneScales;
        private readonly float[] originalBlendShapeWeights;
        private Mesh? ownedMesh;
        private Material[] ownedMaterials = Array.Empty<Material>();
        private bool disposed;

        internal MmdExistingSceneRebindLease(GameObject root)
        {
            if (root == null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            renderer = MmdUnityModelFactory.ResolveExistingSkinnedMeshRenderer(root);
            originalMesh = renderer.sharedMesh;
            originalMaterials = renderer.sharedMaterials;
            originalRootBone = renderer.rootBone;
            originalLocalBounds = renderer.localBounds;
            bones = renderer.bones;
            originalBonePositions = new Vector3[bones.Length];
            originalBoneRotations = new Quaternion[bones.Length];
            originalBoneScales = new Vector3[bones.Length];
            for (int i = 0; i < bones.Length; i++)
            {
                Transform bone = bones[i];
                if (bone == null)
                {
                    continue;
                }

                originalBonePositions[i] = bone.localPosition;
                originalBoneRotations[i] = bone.localRotation;
                originalBoneScales[i] = bone.localScale;
            }

            originalBlendShapeWeights = new float[originalMesh?.blendShapeCount ?? 0];
            for (int i = 0; i < originalBlendShapeWeights.Length; i++)
            {
                originalBlendShapeWeights[i] = renderer.GetBlendShapeWeight(i);
            }
        }

        internal void AdoptFactoryResult(MmdUnityModelInstance instance)
        {
            if (instance == null)
            {
                throw new ArgumentNullException(nameof(instance));
            }

            AdoptGeneratedMesh(instance.Mesh);
            AdoptGeneratedMaterials(instance.Materials);
        }

        internal void AdoptGeneratedMesh(Mesh? mesh)
        {
            ownedMesh = ReferenceEquals(mesh, originalMesh) ? null : mesh;
        }

        internal void AdoptGeneratedMaterials(Material[] materials)
        {
            ownedMaterials = materials
                .Where(material => material != null && !ContainsReference(originalMaterials, material))
                .Distinct()
                .ToArray();
        }

        internal void Commit()
        {
            disposed = true;
            ownedMesh = null;
            ownedMaterials = Array.Empty<Material>();
        }

        internal void RollbackFactoryFailure()
        {
            Mesh? currentMesh = renderer.sharedMesh;
            if (ownedMesh == null && !ReferenceEquals(currentMesh, originalMesh))
            {
                ownedMesh = currentMesh;
            }

            ownedMaterials = ownedMaterials
                .Concat(renderer.sharedMaterials)
                .Where(material => material != null && !ContainsReference(originalMaterials, material))
                .Distinct()
                .ToArray();
            Dispose();
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            if (renderer != null)
            {
                renderer.sharedMesh = originalMesh;
                renderer.sharedMaterials = originalMaterials;
                renderer.rootBone = originalRootBone;
                renderer.localBounds = originalLocalBounds;
                RestoreBlendShapeWeights();
                RestoreBones();
            }

            DestroyOwnedResources();
        }

        private void RestoreBlendShapeWeights()
        {
            int count = Math.Min(originalMesh?.blendShapeCount ?? 0, originalBlendShapeWeights.Length);
            for (int i = 0; i < count; i++)
            {
                renderer.SetBlendShapeWeight(i, originalBlendShapeWeights[i]);
            }
        }

        private void RestoreBones()
        {
            for (int i = 0; i < bones.Length; i++)
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

        private void DestroyOwnedResources()
        {
            var destroyedIds = new HashSet<int>();
            foreach (Material material in ownedMaterials)
            {
                DestroyOnce(material, destroyedIds);
            }

            DestroyOnce(ownedMesh, destroyedIds);
            ownedMaterials = Array.Empty<Material>();
            ownedMesh = null;
        }

        private static bool ContainsReference(Material[] materials, Material candidate)
        {
            return materials.Any(material => ReferenceEquals(material, candidate));
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
