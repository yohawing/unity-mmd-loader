#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.LowLevel;
using UnityEngine.PlayerLoop;

namespace Mmd.UnityIntegration
{
    /// <summary>
    /// Drives eligible standalone controllers before the normal MonoBehaviour Update phase.
    /// Every controller dispatches its long-lived native worker before any worker is waited on;
    /// this is the automatic multi-character batch boundary and does not require a scene object.
    /// </summary>
    internal static class MmdStandaloneWorkerScheduler
    {
        private sealed class BoundaryMarker
        {
        }

        private sealed class PendingEvaluation
        {
            internal PendingEvaluation(
                MmdUnityPlaybackController controller,
                MmdMultiCharacterWorkerPool pool,
                MmdMultiCharacterWorkerRequest request,
                MmdUnityPlaybackController.MmdMultiCharacterClockState previousClock)
            {
                Controller = controller;
                Pool = pool;
                Request = request;
                PreviousClock = previousClock;
            }

            internal MmdUnityPlaybackController Controller { get; }

            internal MmdMultiCharacterWorkerPool Pool { get; }

            internal MmdMultiCharacterWorkerRequest Request { get; }

            internal MmdUnityPlaybackController.MmdMultiCharacterClockState PreviousClock { get; }

            internal bool DispatchStarted { get; set; }

            internal Exception? Error { get; set; }
        }

        private static readonly List<MmdUnityPlaybackController> registered = new();
        private static readonly List<PendingEvaluation> pending = new();
        private static bool installed;
        private static bool callbackActive;
        private static ulong generation;

        internal static bool IsInstalled => installed;

        internal static bool IsCallbackActive => callbackActive;

        internal static int RegisteredCount => registered.Count;

        internal static int LastBatchSize { get; private set; }

        internal static ulong LastGeneration { get; private set; }

        // Contract seam used by EditMode tests. The production callback below performs the same
        // two-phase boundary with per-controller error isolation; this small pure seam lets tests
        // prove the ordering with real worker slots and a barrier without depending on PlayerLoop
        // timing or thread IDs.
        internal static IReadOnlyList<Exception?> DispatchStandaloneBatchForTest(
            IReadOnlyList<MmdMultiCharacterWorkerPool> pools,
            IReadOnlyList<MmdMultiCharacterWorkerRequest> requests,
            Action<int, Exception?>? completionObserver = null)
        {
            if (pools == null)
            {
                throw new ArgumentNullException(nameof(pools));
            }

            if (requests == null)
            {
                throw new ArgumentNullException(nameof(requests));
            }

            if (pools.Count != requests.Count)
            {
                throw new ArgumentException("Exactly one request is required for each worker slot.", nameof(requests));
            }

            var errors = new Exception?[pools.Count];
            var dispatchStarted = new bool[pools.Count];
            for (int i = 0; i < pools.Count; i++)
            {
                try
                {
                    pools[i].BeginEvaluate(requests[i]);
                    dispatchStarted[i] = true;
                }
                catch (Exception exception)
                {
                    errors[i] = exception;
                }
            }

            for (int i = 0; i < pools.Count; i++)
            {
                if (!dispatchStarted[i])
                {
                    continue;
                }

                try
                {
                    pools[i].CompleteEvaluate();
                }
                catch (Exception exception)
                {
                    errors[i] = exception;
                }
            }

            // Test-only seam: observe the same post-barrier outcome pass used by the runtime
            // callback, after every worker has reached CompleteEvaluate. The observer is useful
            // for proving that a faulted slot can roll back/latch without suppressing a sibling.
            for (int i = 0; i < errors.Length; i++)
            {
                completionObserver?.Invoke(i, errors[i]);
            }

            return errors;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetForDomainReload()
        {
            registered.Clear();
            pending.Clear();
            installed = false;
            callbackActive = false;
            generation = 0;
            LastBatchSize = 0;
            LastGeneration = 0;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InstallAtRuntime()
        {
            _ = EnsureInstalled();
        }

        internal static void Register(MmdUnityPlaybackController controller)
        {
            if (controller == null)
            {
                return;
            }

            if (!registered.Contains(controller))
            {
                registered.Add(controller);
            }

            _ = EnsureInstalled();
        }

        internal static void Unregister(MmdUnityPlaybackController controller)
        {
            if (controller == null)
            {
                return;
            }

            registered.Remove(controller);
        }

        internal static bool EnsureInstalled()
        {
            PlayerLoopSystem current = PlayerLoop.GetCurrentPlayerLoop();
            if (installed && HasInstalledBoundary(current))
            {
                return true;
            }

            installed = false;
            if (!TryPatchPlayerLoop(current, out PlayerLoopSystem patched))
            {
                // Keep controller.Update as the serial fail-safe when the exact Unity anchor is
                // unavailable. No partially patched tree is published.
                return false;
            }

            PlayerLoop.SetPlayerLoop(patched);
            installed = true;
            return true;
        }

        internal static bool TryPatchPlayerLoop(
            PlayerLoopSystem current,
            out PlayerLoopSystem patched)
        {
            if (current.type == typeof(Update))
            {
                patched = current;
                if (!TryPatchUpdate(current.subSystemList, out PlayerLoopSystem[] children))
                {
                    return false;
                }

                patched.subSystemList = children;
                return true;
            }

            PlayerLoopSystem[]? currentChildren = current.subSystemList;
            if (currentChildren != null)
            {
                for (int i = 0; i < currentChildren.Length; i++)
                {
                    if (!TryPatchPlayerLoop(currentChildren[i], out PlayerLoopSystem childPatched))
                    {
                        continue;
                    }

                    var children = (PlayerLoopSystem[])currentChildren.Clone();
                    children[i] = childPatched;
                    patched = current;
                    patched.subSystemList = children;
                    return true;
                }
            }

            patched = current;
            return false;
        }

        private static bool HasInstalledBoundary(PlayerLoopSystem current)
        {
            if (current.type == typeof(Update))
            {
                PlayerLoopSystem[]? children = current.subSystemList;
                if (children == null)
                {
                    return false;
                }

                int markerIndex = FindType<BoundaryMarker>(children);
                int anchorIndex = FindType<Update.ScriptRunBehaviourUpdate>(children);
                return markerIndex >= 0 && anchorIndex >= 0 &&
                    markerIndex + 1 == anchorIndex &&
                    children[markerIndex].updateDelegate != null;
            }

            PlayerLoopSystem[]? currentChildren = current.subSystemList;
            if (currentChildren == null)
            {
                return false;
            }

            for (int i = 0; i < currentChildren.Length; i++)
            {
                if (HasInstalledBoundary(currentChildren[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryPatchUpdate(
            PlayerLoopSystem[]? current,
            out PlayerLoopSystem[] patched)
        {
            patched = Array.Empty<PlayerLoopSystem>();
            if (current == null)
            {
                return false;
            }

            int markerIndex = FindType<BoundaryMarker>(current);
            int anchorIndex = FindType<Update.ScriptRunBehaviourUpdate>(current);
            if (anchorIndex < 0)
            {
                return false;
            }

            if (markerIndex >= 0)
            {
                if (markerIndex + 1 != anchorIndex)
                {
                    return false;
                }

                var repaired = (PlayerLoopSystem[])current.Clone();
                repaired[markerIndex].updateDelegate = DriveRegisteredControllers;
                patched = repaired;
                return true;
            }

            var children = new List<PlayerLoopSystem>(current);
            children.Insert(
                anchorIndex,
                new PlayerLoopSystem
                {
                    type = typeof(BoundaryMarker),
                    updateDelegate = DriveRegisteredControllers
                });
            patched = children.ToArray();
            return true;
        }

        private static int FindType<T>(IReadOnlyList<PlayerLoopSystem> systems)
        {
            Type type = typeof(T);
            for (int i = 0; i < systems.Count; i++)
            {
                if (systems[i].type == type)
                {
                    return i;
                }
            }

            return -1;
        }

        private static void DriveRegisteredControllers()
        {
            if (!Application.isPlaying || callbackActive)
            {
                return;
            }

            callbackActive = true;
            generation++;
            if (generation == 0)
            {
                generation = 1;
            }

            pending.Clear();
            LastBatchSize = 0;
            LastGeneration = generation;
            MmdUnityPlaybackController[] snapshot = registered.ToArray();
            try
            {
                // Prepare, advance, and dispatch every eligible controller before waiting on any
                // worker. A controller that cannot use the native path is simply left for its
                // normal serial Update; it does not poison other controllers in the snapshot.
                for (int i = 0; i < snapshot.Length; i++)
                {
                    MmdUnityPlaybackController controller = snapshot[i];
                    if (controller == null || !controller.isActiveAndEnabled)
                    {
                        continue;
                    }

                    MmdMultiCharacterWorkerPool pool;
                    MmdMultiCharacterWorkerRequest request;
                    MmdUnityPlaybackController.MmdMultiCharacterClockState previousClock;
                    try
                    {
                        if (!controller.TryPrepareStandaloneWorkerEvaluation(
                                Time.deltaTime,
                                out pool,
                                out request,
                                out previousClock,
                                out _))
                        {
                            continue;
                        }
                    }
                    catch (Exception exception)
                    {
                        // Unexpected preparation failures are isolated too. No request has been
                        // dispatched yet, so the remaining controllers can still form a batch.
                        controller.HandleStandaloneWorkerPreparationFailure(exception.Message);
                        continue;
                    }

                    var entry = new PendingEvaluation(controller, pool, request, previousClock);
                    pending.Add(entry);
                    try
                    {
                        pool.BeginEvaluate(request);
                        entry.DispatchStarted = true;
                    }
                    catch (Exception exception)
                    {
                        entry.Error = exception;
                    }
                }

                LastBatchSize = pending.Count;

                // Complete every dispatched worker even if one worker failed. This keeps each
                // controller's lifecycle isolated while preserving the dispatch-before-complete
                // batch boundary for the successful controllers.
                for (int i = 0; i < pending.Count; i++)
                {
                    PendingEvaluation entry = pending[i];
                    if (!entry.DispatchStarted)
                    {
                        continue;
                    }

                    try
                    {
                        entry.Pool.CompleteEvaluate();
                    }
                    catch (Exception exception)
                    {
                        entry.Error = exception;
                    }
                }

                for (int i = 0; i < pending.Count; i++)
                {
                    PendingEvaluation entry = pending[i];
                    if (entry.Error != null || !entry.DispatchStarted)
                    {
                        entry.Controller.HandleStandaloneWorkerFailure(
                            entry.PreviousClock,
                            entry.Error?.Message ?? "Standalone worker dispatch failed.");
                        continue;
                    }

                    try
                    {
                        MmdMultiCharacterWorkerResult result = entry.Pool.GetResult(0);
                        if (!entry.Controller.TryValidateStandaloneWorkerApply(
                                entry.Request,
                                entry.Pool,
                                result,
                                out string reason))
                        {
                            entry.Controller.HandleStandaloneWorkerFailure(entry.PreviousClock, reason);
                            continue;
                        }

                        entry.Controller.ApplyPreparedMultiCharacterFrame(
                            entry.Request.Frame,
                            entry.Request.Time,
                            entry.Request.FrameRate,
                            result);
                        if (entry.Controller.isActiveAndEnabled)
                        {
                            entry.Controller.MarkStandaloneWorkerDriven();
                        }
                    }
                    catch (Exception exception)
                    {
                        entry.Controller.HandleStandaloneWorkerFailure(
                            entry.PreviousClock,
                            exception.Message);
                    }
                }
            }
            finally
            {
                pending.Clear();
                callbackActive = false;
            }
        }
    }
}
