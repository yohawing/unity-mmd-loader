#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Mmd.UnityIntegration
{
    [AddComponentMenu("")]
    [DisallowMultipleComponent]
    public sealed class MmdTransientRuntimeInstanceMarker : MonoBehaviour
    {
        [Serializable]
        private struct BorrowedRendererState
        {
            internal BorrowedRendererState(Renderer renderer, bool wasEnabled)
            {
                this.renderer = renderer;
                this.wasEnabled = wasEnabled;
            }

            [SerializeField] internal Renderer? renderer;
            [SerializeField] internal bool wasEnabled;
        }

        [SerializeField, HideInInspector] private int schemaVersion = 1;
        [SerializeField, HideInInspector] private MmdUnityPlaybackController? owner;
        [SerializeField, HideInInspector] private Mesh? ownedMesh;
        [SerializeField, HideInInspector] private Material[] ownedMaterials = Array.Empty<Material>();
        [SerializeField, HideInInspector] private Texture2D[] ownedTextures = Array.Empty<Texture2D>();
        [SerializeField, HideInInspector] private BorrowedRendererState[] borrowedRendererStates = Array.Empty<BorrowedRendererState>();

        internal MmdUnityPlaybackController? Owner => owner;

        internal bool IsSafeTransientSibling =>
            schemaVersion == 1 &&
            (owner == null || owner.gameObject != gameObject) &&
            GetComponent<MmdUnityPlaybackController>() == null &&
            GetComponent<MmdRuntimeImporterComponent>() == null;

        internal void Initialize(MmdUnityPlaybackController instanceOwner, MmdUnityModelInstance instance)
        {
            if (instanceOwner == null)
            {
                throw new ArgumentNullException(nameof(instanceOwner));
            }

            if (instance == null)
            {
                throw new ArgumentNullException(nameof(instance));
            }

            if (instance.Root != gameObject)
            {
                throw new InvalidOperationException("The transient marker must be attached to its generated instance root.");
            }

            schemaVersion = 1;
            owner = instanceOwner;
            ownedMesh = instance.Mesh;
            ownedMaterials = instance.Materials.Where(material => material != null).Distinct().ToArray();
            ownedTextures = instance.OwnedTextures.Where(texture => texture != null).Distinct().ToArray();
        }

        internal void CaptureAndDisableBorrowedRenderer(Renderer renderer)
        {
            if (renderer == null)
            {
                throw new ArgumentNullException(nameof(renderer));
            }

            if (!renderer.enabled || borrowedRendererStates.Any(state => state.renderer == renderer))
            {
                return;
            }

            Array.Resize(ref borrowedRendererStates, borrowedRendererStates.Length + 1);
            borrowedRendererStates[borrowedRendererStates.Length - 1] = new BorrowedRendererState(renderer, wasEnabled: true);
            renderer.enabled = false;
        }

        internal void RestoreBorrowedRendererStates()
        {
            BorrowedRendererState[] states = borrowedRendererStates;
            borrowedRendererStates = Array.Empty<BorrowedRendererState>();
            foreach (BorrowedRendererState state in states)
            {
                if (state.renderer != null)
                {
                    state.renderer.enabled = state.wasEnabled;
                }
            }
        }

        internal bool DestroyOwnedObjectsAndRoot()
        {
            if (!IsSafeTransientSibling)
            {
                return false;
            }

            GameObject root = gameObject;
            Mesh? mesh = ownedMesh;
            Material[] materials = ownedMaterials;
            Texture2D[] textures = ownedTextures;
            var destroyedIds = new HashSet<int>();

            RestoreBorrowedRendererStates();

            SkinnedMeshRenderer? skinned = root.GetComponentInChildren<SkinnedMeshRenderer>(includeInactive: true);
            if (skinned != null)
            {
                skinned.sharedMesh = null;
                skinned.sharedMaterials = Array.Empty<Material>();
            }

            MeshFilter? meshFilter = root.GetComponentInChildren<MeshFilter>(includeInactive: true);
            if (meshFilter != null)
            {
                meshFilter.sharedMesh = null;
            }

            MeshRenderer? meshRenderer = root.GetComponentInChildren<MeshRenderer>(includeInactive: true);
            if (meshRenderer != null)
            {
                meshRenderer.sharedMaterials = Array.Empty<Material>();
            }

            DestroyObject(root, destroyedIds);
            DestroyObject(mesh, destroyedIds);
            foreach (Material material in materials)
            {
                DestroyObject(material, destroyedIds);
            }

            foreach (Texture2D texture in textures)
            {
                DestroyObject(texture, destroyedIds);
            }

            return true;
        }

        private void OnDestroy()
        {
            RestoreBorrowedRendererStates();
        }

        private static void DestroyObject(Object? value, HashSet<int> destroyedIds)
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
