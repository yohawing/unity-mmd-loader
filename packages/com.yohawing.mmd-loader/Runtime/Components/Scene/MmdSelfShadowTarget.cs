#nullable enable

using System.Collections.Generic;
using Mmd.Motion;
using UnityEngine;

namespace Mmd.UnityIntegration
{
    /// <summary>
    /// Opt-in marker for an MMD character that should participate in the dedicated MMD self-shadow map.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public sealed class MmdSelfShadowTarget : MonoBehaviour
    {
        public static readonly int MmdSelfShadowReceiveId = Shader.PropertyToID("_MmdSelfShadowReceive");

        private static readonly List<MmdSelfShadowTarget> ActiveTargets = new();
        private static MaterialPropertyBlock? sharedPropertyBlock;
        private static bool receiverGateAvailable;

        [SerializeField]
        [Tooltip("Enables the dedicated MMD self-shadow render path for this character.")]
        private bool selfShadowEnabled = true;

        [SerializeField]
        [Tooltip("Optional root used for bounds collection. When empty, this GameObject is treated as the character root.")]
        private Transform? boundsRoot;

        [SerializeField]
        [Tooltip("Optional scene environment whose sampled VMD self-shadow state drives this target.")]
        private MmdSceneEnvironmentBinding? sceneEnvironment;

        private MmdSelfShadowProjectionPolicy projectionPolicy = MmdSelfShadowProjectionPolicy.Default;
        private Transform? receiverGateRoot;
        private bool receiverGateEnabled;
        private bool receiverGateInitialized;

        public bool SelfShadowEnabled
        {
            get => selfShadowEnabled;
            set
            {
                if (selfShadowEnabled == value)
                {
                    return;
                }

                selfShadowEnabled = value;
                if (isActiveAndEnabled)
                {
                    ApplyReceiverGate(value);
                }
            }
        }

        public Transform BoundsRoot
        {
            get => boundsRoot != null ? boundsRoot : transform;
            set
            {
                if (boundsRoot == value)
                {
                    return;
                }

                if (isActiveAndEnabled)
                {
                    ApplyReceiverGateToRoot(BoundsRoot, selfShadowReceive: false);
                }

                boundsRoot = value;
                receiverGateRoot = null;
                receiverGateInitialized = false;

                if (isActiveAndEnabled)
                {
                    ApplyReceiverGate(selfShadowEnabled);
                }
            }
        }

        internal Transform? ConfiguredBoundsRoot => boundsRoot;

        public MmdSelfShadowProjectionPolicy ProjectionPolicy
        {
            get => projectionPolicy;
            set => projectionPolicy = value;
        }

        public MmdSceneEnvironmentBinding? SceneEnvironment
        {
            get => sceneEnvironment;
            set
            {
                if (sceneEnvironment == value)
                {
                    return;
                }

                sceneEnvironment = value;
                if (isActiveAndEnabled)
                {
                    ApplyReceiverGate(selfShadowEnabled);
                }
            }
        }

        public static int ActiveTargetCount => ActiveTargets.Count;

        private static MaterialPropertyBlock SharedPropertyBlock => sharedPropertyBlock ??= new MaterialPropertyBlock();

        public static void DisableAllReceiverGates()
        {
            receiverGateAvailable = false;
            for (int i = ActiveTargets.Count - 1; i >= 0; i--)
            {
                MmdSelfShadowTarget target = ActiveTargets[i];
                if (target == null)
                {
                    ActiveTargets.RemoveAt(i);
                    continue;
                }

                target.ApplyReceiverGate(false, force: true);
                target.receiverGateRoot = null;
                target.receiverGateInitialized = false;
            }
        }

        internal static void SetReceiverGateAvailableForRendering(bool available)
        {
            if (!available)
            {
                DisableAllReceiverGates();
                return;
            }

            bool changed = !receiverGateAvailable;
            receiverGateAvailable = true;
            for (int i = ActiveTargets.Count - 1; i >= 0; i--)
            {
                MmdSelfShadowTarget target = ActiveTargets[i];
                if (target == null)
                {
                    ActiveTargets.RemoveAt(i);
                    continue;
                }

                if (target.isActiveAndEnabled)
                {
                    target.ApplyReceiverGate(target.selfShadowEnabled, force: changed);
                }
            }
        }

        internal static MmdSelfShadowTarget EnsureHiddenTarget(GameObject root, Transform? boundsRoot = null)
        {
            MmdSelfShadowTarget target = root.GetComponent<MmdSelfShadowTarget>();
            if (target == null)
            {
                target = root.AddComponent<MmdSelfShadowTarget>();
            }

            target.HideFromInspector();
            if (boundsRoot != null)
            {
                target.BoundsRoot = boundsRoot;
            }

            return target;
        }

        public static void CollectActiveTargets(List<MmdSelfShadowTarget> buffer)
        {
            buffer.Clear();
#if UNITY_EDITOR
            if (!Application.isPlaying && ActiveTargets.Count == 0)
            {
                MmdSelfShadowTarget[] sceneTargets =
                    Object.FindObjectsByType<MmdSelfShadowTarget>(FindObjectsInactive.Exclude);
                for (int i = 0; i < sceneTargets.Length; i++)
                {
                    if (!ActiveTargets.Contains(sceneTargets[i]))
                    {
                        ActiveTargets.Add(sceneTargets[i]);
                    }
                }
            }
#endif
            for (int i = ActiveTargets.Count - 1; i >= 0; i--)
            {
                MmdSelfShadowTarget target = ActiveTargets[i];
                if (target == null)
                {
                    ActiveTargets.RemoveAt(i);
                    continue;
                }

                if (target.isActiveAndEnabled && target.selfShadowEnabled)
                {
                    buffer.Add(target);
                }
            }
        }

        public MmdSelfShadowBoundsResult CollectBounds()
        {
            return MmdSelfShadowBoundsCollector.Collect(BoundsRoot, EffectiveProjectionPolicy);
        }

        public void RefreshReceiverGate()
        {
            if (isActiveAndEnabled)
            {
                ApplyReceiverGate(selfShadowEnabled);
            }
        }

        public MmdSceneSelfShadowDiagnosticStatus EvaluateSelfShadowDiagnosticStatus()
        {
            if (!isActiveAndEnabled || !selfShadowEnabled)
            {
                return MmdSceneSelfShadowDiagnosticStatus.NoCharacterRoots;
            }

            MmdSceneEnvironmentBinding? effectiveEnvironment =
                ResolveSceneEnvironment(out bool ambiguousEnvironment);
            if (ambiguousEnvironment)
            {
                return MmdSceneSelfShadowDiagnosticStatus.AmbiguousEnvironment;
            }

            if (effectiveEnvironment == null)
            {
                return MmdSceneSelfShadowDiagnosticStatus.NoSelfShadowState;
            }

            MmdSceneSelfShadowDiagnosticStatus environmentStatus =
                effectiveEnvironment.EvaluateSelfShadowDiagnosticStatus();
            if (environmentStatus != MmdSceneSelfShadowDiagnosticStatus.Active)
            {
                return environmentStatus;
            }

            if (!receiverGateAvailable)
            {
                return MmdSceneSelfShadowDiagnosticStatus.NoRendererFeature;
            }

            if (receiverGateInitialized && !receiverGateEnabled)
            {
                return MmdSceneSelfShadowDiagnosticStatus.ReceiverGateOff;
            }

            if (!CollectBounds().HasBounds)
            {
                return MmdSceneSelfShadowDiagnosticStatus.NoBounds;
            }

            return HasSelfShadowCasterPass(BoundsRoot)
                ? MmdSceneSelfShadowDiagnosticStatus.Active
                : MmdSceneSelfShadowDiagnosticStatus.NoCasterPass;
        }

        public bool TryGetActiveProjectionState(out MmdSelfShadowProjectionState projectionState)
        {
            MmdSceneEnvironmentBinding? effectiveEnvironment = ResolveSceneEnvironment();
            if (effectiveEnvironment != null)
            {
                effectiveEnvironment.EnsureSelfShadowDefaultState();
                if (!effectiveEnvironment.SelfShadowEnabled ||
                    effectiveEnvironment.LastSelfShadowApplyStatus != MmdSceneSelfShadowApplyStatus.Recorded)
                {
                    projectionState = MmdSelfShadowProjectionState.Inactive;
                    return false;
                }

                projectionState = effectiveEnvironment.SelfShadowProjectionPolicy.Evaluate(effectiveEnvironment.LastSelfShadowState);
                return projectionState.Active;
            }

            projectionState = MmdSelfShadowProjectionState.Inactive;
            return false;
        }

        public bool TryGetSelfShadowLightDirection(out Vector3 direction)
        {
            MmdSceneEnvironmentBinding? effectiveEnvironment = ResolveSceneEnvironmentForDirection();
            if (effectiveEnvironment != null &&
                effectiveEnvironment.TryGetSelfShadowUnityLightDirection(out direction))
            {
                return true;
            }

            direction = default;
            return false;
        }

        private MmdSelfShadowProjectionPolicy EffectiveProjectionPolicy
        {
            get
            {
                MmdSceneEnvironmentBinding? effectiveEnvironment = ResolveSceneEnvironment();
                return effectiveEnvironment != null ? effectiveEnvironment.SelfShadowProjectionPolicy : projectionPolicy;
            }
        }

        private void OnEnable()
        {
            HideFromInspector();
            if (!ActiveTargets.Contains(this))
            {
                ActiveTargets.Add(this);
            }

            ApplyReceiverGate(selfShadowEnabled);
        }

        private void OnDisable()
        {
            ApplyReceiverGateToRoot(BoundsRoot, selfShadowReceive: false);
            ActiveTargets.Remove(this);
            receiverGateRoot = null;
            receiverGateInitialized = false;
        }

        private void OnDestroy()
        {
            ApplyReceiverGateToRoot(BoundsRoot, selfShadowReceive: false);
        }

        private void OnTransformChildrenChanged()
        {
            if (isActiveAndEnabled)
            {
                ApplyReceiverGate(selfShadowEnabled, force: true);
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (isActiveAndEnabled)
            {
                ApplyReceiverGate(selfShadowEnabled, force: true);
            }
        }
#endif

        private void ApplyReceiverGate(bool enabled, bool force = false)
        {
            Transform root = BoundsRoot;
            bool shouldReceive = receiverGateAvailable && enabled && TryGetActiveProjectionState(out _);
            if (!force &&
                receiverGateInitialized &&
                receiverGateRoot == root &&
                receiverGateEnabled == shouldReceive)
            {
                return;
            }

            if (receiverGateRoot != null && receiverGateRoot != root)
            {
                ApplyReceiverGateToRoot(receiverGateRoot, selfShadowReceive: false);
            }

            ApplyReceiverGateToRoot(root, shouldReceive);
            receiverGateRoot = root;
            receiverGateEnabled = shouldReceive;
            receiverGateInitialized = true;
        }

        private static void ApplyReceiverGateToRoot(Transform root, bool selfShadowReceive)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(includeInactive: true);
            float selfShadowValue = selfShadowReceive ? 1.0f : 0.0f;
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                renderer.GetPropertyBlock(SharedPropertyBlock);
                SharedPropertyBlock.SetFloat(MmdSelfShadowReceiveId, selfShadowValue);
                renderer.SetPropertyBlock(SharedPropertyBlock);
                SharedPropertyBlock.Clear();
            }
        }

        private void HideFromInspector()
        {
            hideFlags |= HideFlags.HideInInspector;
        }

        private MmdSceneEnvironmentBinding? ResolveSceneEnvironment()
        {
            return ResolveSceneEnvironment(out _);
        }

        private MmdSceneEnvironmentBinding? ResolveSceneEnvironment(out bool ambiguous)
        {
            ambiguous = false;
            if (sceneEnvironment != null)
            {
                return sceneEnvironment;
            }

            MmdSceneEnvironmentBinding? resolvedEnvironment = null;
            MmdSceneEnvironmentBinding? fallbackEnvironment = null;
            bool hasAmbiguousFallback = false;
            MmdSceneEnvironmentBinding[] environments =
                Object.FindObjectsByType<MmdSceneEnvironmentBinding>(FindObjectsInactive.Exclude);
            for (int i = 0; i < environments.Length; i++)
            {
                MmdSceneEnvironmentBinding environment = environments[i];
                if (environment == null)
                {
                    continue;
                }

                if (environment.LastSelfShadowApplyStatus == MmdSceneSelfShadowApplyStatus.Recorded ||
                    environment.LastSelfShadowApplyStatus == MmdSceneSelfShadowApplyStatus.Disabled)
                {
                    if (resolvedEnvironment != null)
                    {
                        ambiguous = true;
                        return null;
                    }

                    resolvedEnvironment = environment;
                    continue;
                }

                if (fallbackEnvironment != null)
                {
                    hasAmbiguousFallback = true;
                    continue;
                }

                fallbackEnvironment = environment;
            }

            if (resolvedEnvironment != null)
            {
                return resolvedEnvironment;
            }

            if (hasAmbiguousFallback)
            {
                ambiguous = true;
                return null;
            }

            return fallbackEnvironment;
        }

        private static bool HasSelfShadowCasterPass(Transform root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(includeInactive: false);
            for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
            {
                Renderer renderer = renderers[rendererIndex];
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
                {
                    continue;
                }

                Material[] materials = renderer.sharedMaterials;
                int submeshCount = renderer is SkinnedMeshRenderer skinned && skinned.sharedMesh != null
                    ? skinned.sharedMesh.subMeshCount
                    : materials.Length;
                for (int submesh = 0; submesh < materials.Length && submesh < submeshCount; submesh++)
                {
                    Material material = materials[submesh];
                    if (material != null && material.FindPass("MmdSelfShadowCaster") >= 0)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private MmdSceneEnvironmentBinding? ResolveSceneEnvironmentForDirection()
        {
            MmdSceneEnvironmentBinding? effectiveEnvironment = ResolveSceneEnvironment();
            return effectiveEnvironment != null && effectiveEnvironment.TryGetSelfShadowUnityLightDirection(out _)
                ? effectiveEnvironment
                : null;
        }
    }
}
