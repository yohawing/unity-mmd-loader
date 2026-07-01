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
    public sealed class MmdSelfShadowTarget : MonoBehaviour
    {
        public static readonly int MmdSelfShadowReceiveId = Shader.PropertyToID("_MmdSelfShadowReceive");
        public static readonly int MmdReceiveShadowsId = Shader.PropertyToID("_MmdReceiveShadows");
        public static readonly int MmdSuppressStandardShadowsId = Shader.PropertyToID("_MmdSuppressStandardShadows");

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
        private bool receiverGateSuppressesStandard;
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
                    ApplyReceiverGateToRoot(BoundsRoot, selfShadowReceive: false, suppressStandardShadows: false);
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
                    target.ApplyReceiverGate(target.selfShadowEnabled, force: true);
                }
            }
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

        public bool TryGetActiveProjectionState(out MmdSelfShadowProjectionState projectionState)
        {
            if (sceneEnvironment != null)
            {
                if (!sceneEnvironment.SelfShadowEnabled ||
                    sceneEnvironment.LastSelfShadowApplyStatus != MmdSceneSelfShadowApplyStatus.Recorded)
                {
                    projectionState = MmdSelfShadowProjectionState.Inactive;
                    return false;
                }

                projectionState = sceneEnvironment.SelfShadowProjectionPolicy.Evaluate(sceneEnvironment.LastSelfShadowState);
                return projectionState.Active;
            }

            projectionState = EffectiveProjectionPolicy.Evaluate(new MmdSelfShadowState(1, 1.0f));
            return projectionState.Active;
        }

        public bool TryGetSelfShadowLightDirection(out Vector3 direction)
        {
            if (sceneEnvironment != null &&
                sceneEnvironment.TryGetLastUnityLightDirection(out direction))
            {
                return true;
            }

            direction = default;
            return false;
        }

        private MmdSelfShadowProjectionPolicy EffectiveProjectionPolicy =>
            sceneEnvironment != null ? sceneEnvironment.SelfShadowProjectionPolicy : projectionPolicy;

        private void OnEnable()
        {
            if (!ActiveTargets.Contains(this))
            {
                ActiveTargets.Add(this);
            }

            ApplyReceiverGate(selfShadowEnabled);
        }

        private void OnDisable()
        {
            ApplyReceiverGateToRoot(BoundsRoot, selfShadowReceive: false, suppressStandardShadows: false);
            ActiveTargets.Remove(this);
            receiverGateRoot = null;
            receiverGateInitialized = false;
        }

        private void OnDestroy()
        {
            ApplyReceiverGateToRoot(BoundsRoot, selfShadowReceive: false, suppressStandardShadows: false);
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
            bool suppressStandard = shouldReceive;
            if (!force &&
                receiverGateInitialized &&
                receiverGateRoot == root &&
                receiverGateEnabled == shouldReceive &&
                receiverGateSuppressesStandard == suppressStandard)
            {
                return;
            }

            if (receiverGateRoot != null && receiverGateRoot != root)
            {
                ApplyReceiverGateToRoot(receiverGateRoot, selfShadowReceive: false, suppressStandardShadows: false);
            }

            ApplyReceiverGateToRoot(root, shouldReceive, suppressStandard);
            receiverGateRoot = root;
            receiverGateEnabled = shouldReceive;
            receiverGateSuppressesStandard = suppressStandard;
            receiverGateInitialized = true;
        }

        private static void ApplyReceiverGateToRoot(
            Transform root,
            bool selfShadowReceive,
            bool suppressStandardShadows)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(includeInactive: true);
            float selfShadowValue = selfShadowReceive ? 1.0f : 0.0f;
            float suppressStandardValue = suppressStandardShadows ? 1.0f : 0.0f;
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                renderer.GetPropertyBlock(SharedPropertyBlock);
                SharedPropertyBlock.SetFloat(MmdSelfShadowReceiveId, selfShadowValue);
                SharedPropertyBlock.SetFloat(MmdSuppressStandardShadowsId, suppressStandardValue);
                renderer.SetPropertyBlock(SharedPropertyBlock);
                SharedPropertyBlock.Clear();
            }
        }
    }
}
