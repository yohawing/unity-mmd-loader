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

        private static readonly List<MmdSelfShadowTarget> ActiveTargets = new();
        private static readonly MaterialPropertyBlock SharedPropertyBlock = new();

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
                    ApplyReceiverGateToRoot(BoundsRoot, false);
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

        public static void DisableAllReceiverGates()
        {
            for (int i = ActiveTargets.Count - 1; i >= 0; i--)
            {
                MmdSelfShadowTarget target = ActiveTargets[i];
                if (target == null)
                {
                    ActiveTargets.RemoveAt(i);
                    continue;
                }

                target.ApplyReceiverGate(false, force: true);
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
            ApplyReceiverGate(false, force: true);
            ActiveTargets.Remove(this);
            receiverGateRoot = null;
            receiverGateInitialized = false;
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
            bool shouldReceive = enabled && TryGetActiveProjectionState(out _);
            if (!force && receiverGateInitialized && receiverGateRoot == root && receiverGateEnabled == shouldReceive)
            {
                return;
            }

            if (receiverGateRoot != null && receiverGateRoot != root)
            {
                ApplyReceiverGateToRoot(receiverGateRoot, false);
            }

            ApplyReceiverGateToRoot(root, shouldReceive);
            receiverGateRoot = root;
            receiverGateEnabled = shouldReceive;
            receiverGateInitialized = true;
        }

        private static void ApplyReceiverGateToRoot(Transform root, bool enabled)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(includeInactive: true);
            float value = enabled ? 1.0f : 0.0f;
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                renderer.GetPropertyBlock(SharedPropertyBlock);
                SharedPropertyBlock.SetFloat(MmdSelfShadowReceiveId, value);
                renderer.SetPropertyBlock(SharedPropertyBlock);
                SharedPropertyBlock.Clear();
            }
        }
    }
}
