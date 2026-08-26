#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mmd.UnityIntegration
{
    /// <summary>
    /// Opt-in animation-only playback group. Native evaluation is pinned one character per
    /// long-lived worker; Unity object and transform mutation remains on the main thread.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MmdMultiCharacterPlaybackGroup : MonoBehaviour
    {
        private const int MaximumCharacterCount = 4;

        [SerializeField] private List<MmdUnityPlaybackController> controllers = new();

        private MmdUnityPlaybackController[]? resolvedControllers;
        private MmdUnityPlaybackController.MmdMultiCharacterClockState[]? previousClocks;
        private int[]? configurationRevisions;
        private MmdMultiCharacterWorkerPool? workerPool;
        private bool claimsHeld;
        private string lastFailureReason = string.Empty;

        public bool IsPlaybackActive => claimsHeld && isActiveAndEnabled;

        internal bool HasWorkerPool => workerPool != null;

        public string LastFailureReason => lastFailureReason;

        public IReadOnlyList<MmdUnityPlaybackController> Controllers => controllers;

        internal static void NotifyControllerUnavailable(MmdUnityPlaybackController controller)
        {
            if (controller?.MultiCharacterGroup is MmdMultiCharacterPlaybackGroup group)
            {
                group.HandleControllerUnavailable(controller);
            }
        }

        internal static void ReleaseForSerialPlayback(
            MmdUnityPlaybackController controller,
            string operation)
        {
            if (controller?.MultiCharacterGroup is MmdMultiCharacterPlaybackGroup group)
            {
                group.FailClosed(operation + " switched the controller back to serial playback.");
            }
        }

        private void OnEnable()
        {
            if (!claimsHeld)
            {
                TryClaimControllers();
            }
        }

        private void Update()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            if (!IsPlaybackActive)
            {
                return;
            }

            MmdUnityPlaybackController[] active = resolvedControllers!;
            if (!TryGetSharedFrame(active, out int frame, out float frameRate))
            {
                return;
            }

            if (!TryEnsureWorkerPool())
            {
                return;
            }

            MmdUnityPlaybackController.MmdMultiCharacterClockState[] clocks = previousClocks!;
            int advancedCount = 0;
            try
            {
                for (int i = 0; i < active.Length; i++)
                {
                    clocks[i] = active[i].AdvanceMultiCharacterClock(Time.deltaTime);
                    advancedCount++;
                }

                frame = active[0].CurrentFrame;
                for (int i = 1; i < active.Length; i++)
                {
                    if (active[i].CurrentFrame != frame)
                    {
                        RestoreClocks(active, clocks, advancedCount);
                        FailClosed("Controllers do not share one logical frame after clock advance.");
                        return;
                    }
                }

                workerPool!.Evaluate(frame, frameRate);
                for (int i = 0; i < active.Length; i++)
                {
                    if (!active[i].TryValidateMultiCharacterPreparedApply(
                            workerPool.GetResult(i),
                            out string applyReason))
                    {
                        RestoreClocks(active, clocks, advancedCount);
                        FailClosed(applyReason);
                        return;
                    }
                }

                for (int i = 0; i < active.Length; i++)
                {
                    active[i].ApplyPreparedMultiCharacterFrame(
                        frame,
                        frameRate,
                        workerPool.GetResult(i));
                }
            }
            catch (Exception exception)
            {
                RestoreClocks(active, clocks, advancedCount);
                FailClosed("Multi-character evaluation failed: " + exception.Message);
            }
        }

        private static void RestoreClocks(
            MmdUnityPlaybackController[] active,
            MmdUnityPlaybackController.MmdMultiCharacterClockState[] previousClocks,
            int count)
        {
            for (int i = 0; i < count; i++)
            {
                active[i].RestoreMultiCharacterClock(previousClocks[i]);
            }
        }

        private void OnDisable()
        {
            ReleaseGroup();
        }

        private void OnDestroy()
        {
            ReleaseGroup();
        }

        private void TryClaimControllers()
        {
            if (claimsHeld)
            {
                return;
            }

            MmdUnityPlaybackController[] candidate = ResolveControllers();
            if (candidate.Length < 2)
            {
                lastFailureReason = "At least two playback controllers are required.";
                return;
            }

            if (candidate.Length > MaximumCharacterCount)
            {
                lastFailureReason =
                    $"At most {MaximumCharacterCount} playback controllers are supported by this worker group.";
                return;
            }

            var ids = new HashSet<int>();
            for (int i = 0; i < candidate.Length; i++)
            {
                MmdUnityPlaybackController controller = candidate[i];
                if (controller == null || !ids.Add(controller.GetInstanceID()))
                {
                    lastFailureReason = "Controllers must be non-null and distinct.";
                    return;
                }

                if (controller.ModelAssetSource == null || controller.MotionAssetSource == null)
                {
                    lastFailureReason =
                        "Multi-character playback requires controller-owned PMX and VMD asset sources.";
                    return;
                }

                if (controller.PhysicsMode != Mmd.Physics.MmdPhysicsMode.Off)
                {
                    lastFailureReason = "Multi-character playback requires Physics Mode Off.";
                    return;
                }
            }

            for (int i = 0; i < candidate.Length; i++)
            {
                if (!candidate[i].TryClaimMultiCharacterGroup(this, out string reason))
                {
                    lastFailureReason = reason;
                    return;
                }
            }

            for (int i = 0; i < candidate.Length; i++)
            {
                candidate[i].AssignMultiCharacterGroup(this);
            }

            resolvedControllers = candidate;
            previousClocks = new MmdUnityPlaybackController.MmdMultiCharacterClockState[candidate.Length];
            claimsHeld = true;
            lastFailureReason = string.Empty;
        }

        private MmdUnityPlaybackController[] ResolveControllers()
        {
            if (controllers != null && controllers.Count > 0)
            {
                return controllers.ToArray();
            }

            return GetComponentsInChildren<MmdUnityPlaybackController>(includeInactive: true);
        }

        private bool TryEnsureWorkerPool()
        {
            MmdUnityPlaybackController[] active = resolvedControllers!;
            if (workerPool != null)
            {
                for (int i = 0; i < active.Length; i++)
                {
                    if (configurationRevisions![i] != active[i].ConfigurationRevision)
                    {
                        FailClosed("Playback configuration changed while the group was active.");
                        return false;
                    }
                }

                return true;
            }

            var evaluators = new List<MmdMultiCharacterWorkerPool.IEvaluator>(active.Length);
            MmdMultiCharacterWorkerPool? createdPool = null;
            bool ownershipTransferred = false;
            try
            {
                for (int i = 0; i < active.Length; i++)
                {
                    MmdUnityPlaybackController controller = active[i];
                    if (controller == null || !controller.isActiveAndEnabled ||
                        !controller.IsConfigured || controller.IsMultiCharacterTimelineDriven)
                    {
                        FailClosed(
                            "Multi-character playback requires configured standalone self-playback controllers.");
                        return false;
                    }

                    if (!controller.TryGetMultiCharacterSource(
                            out byte[] pmxBytes,
                            out byte[] vmdBytes,
                            out string reason))
                    {
                        FailClosed(reason);
                        return false;
                    }

                    if (!controller.TryPrepareMultiCharacterSource(
                            pmxBytes,
                            vmdBytes,
                            out reason))
                    {
                        FailClosed(reason);
                        return false;
                    }

                    evaluators.Add(new MmdNativeMultiCharacterWorker(
                        pmxBytes,
                        vmdBytes,
                        (uint)controller.IkMaxIterationsCap));
                }

                ownershipTransferred = true;
                createdPool = new MmdMultiCharacterWorkerPool(evaluators);
                var revisions = new int[active.Length];
                for (int i = 0; i < active.Length; i++)
                {
                    revisions[i] = active[i].ConfigurationRevision;
                }

                workerPool = createdPool;
                configurationRevisions = revisions;
                createdPool = null;
                return true;
            }
            catch (Exception exception)
            {
                // Once the pool owns evaluators, only the pool may dispose them so that
                // native sessions are released by their assigned worker exactly once.
                if (createdPool != null)
                {
                    createdPool.Dispose();
                }
                else if (!ownershipTransferred)
                {
                    for (int i = 0; i < evaluators.Count; i++)
                    {
                        evaluators[i].Dispose();
                    }
                }

                FailClosed("Multi-character worker setup failed: " + exception.Message);
                return false;
            }
        }

        private bool TryGetSharedFrame(
            MmdUnityPlaybackController[] active,
            out int frame,
            out float frameRate)
        {
            frame = active[0].CurrentFrame;
            frameRate = active[0].FrameRate;
            for (int i = 0; i < active.Length; i++)
            {
                MmdUnityPlaybackController controller = active[i];
                if (controller == null || !controller.isActiveAndEnabled)
                {
                    FailClosed("A multi-character controller was disabled or destroyed.");
                    return false;
                }

                if (!controller.IsPlaying)
                {
                    FailClosed("Multi-character playback requires every claimed controller to be playing.");
                    return false;
                }

                if (!controller.TryValidateMultiCharacterState(out string applyReason))
                {
                    FailClosed(applyReason);
                    return false;
                }

                if (controller.IsMultiCharacterTimelineDriven)
                {
                    FailClosed("Timeline playback is not supported by the multi-character group.");
                    return false;
                }

                if (controller.CurrentFrame != frame || controller.FrameRate != frameRate)
                {
                    FailClosed("Controllers do not share one logical frame.");
                    return false;
                }
            }

            return true;
        }

        private void FailClosed(string reason)
        {
            lastFailureReason = string.IsNullOrWhiteSpace(reason)
                ? "Multi-character playback was disabled due to an unsupported state."
                : reason;
            ReleaseGroup();
            if (enabled)
            {
                enabled = false;
            }
        }

        private void HandleControllerUnavailable(MmdUnityPlaybackController controller)
        {
            if (!claimsHeld || resolvedControllers == null)
            {
                return;
            }

            for (int i = 0; i < resolvedControllers.Length; i++)
            {
                if (ReferenceEquals(resolvedControllers[i], controller))
                {
                    FailClosed("A claimed controller was disabled or destroyed.");
                    return;
                }
            }
        }

        private void ReleaseGroup()
        {
            Exception? cleanupError = null;
            try
            {
                workerPool?.Dispose();
            }
            catch (Exception exception)
            {
                cleanupError = exception;
            }
            finally
            {
                workerPool = null;
                configurationRevisions = null;

                if (claimsHeld && resolvedControllers != null)
                {
                    for (int i = 0; i < resolvedControllers.Length; i++)
                    {
                        if (resolvedControllers[i] != null)
                        {
                            resolvedControllers[i].ReleaseMultiCharacterGroup(this);
                        }
                    }
                }

                claimsHeld = false;
                resolvedControllers = null;
                previousClocks = null;
            }

            if (cleanupError != null)
            {
                string prefix = string.IsNullOrWhiteSpace(lastFailureReason)
                    ? string.Empty
                    : lastFailureReason + " ";
                lastFailureReason = prefix +
                    "Worker cleanup failed: " + cleanupError.Message;
            }
        }
    }
}
