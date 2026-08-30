#nullable enable

using System;
using System.Collections.Generic;
using Mmd.UnityIntegration;
using UnityEngine;
using UnityEngine.LowLevel;
using UnityEngine.PlayerLoop;

namespace Mmd.Timeline
{
    internal enum MmdTimelineWorkerQueueResult
    {
        NotCollecting = 0,
        Queued = 1,
        FallbackToSynchronous = 2,
        Rejected = 3
    }

    /// <summary>
    /// Collects normal Physics Off VMD Timeline evaluations across one PlayerLoop generation,
    /// dispatches their fixed workers without waiting, and applies every result immediately after
    /// the PlayerLoop DirectorUpdate phase has completed.
    /// </summary>
    internal static class MmdTimelineWorkerBatchScheduler
    {
        private sealed class CollectionBoundaryMarker
        {
        }

        private sealed class DrainBoundaryMarker
        {
        }

        private sealed class PendingEvaluation
        {
            internal PendingEvaluation(
                MmdUnityPlaybackController controller,
                MmdMultiCharacterWorkerPool pool,
                MmdMultiCharacterWorkerRequest request,
                int configurationRevision,
                long fastRuntimeSourceRevision,
                ulong generation)
            {
                Controller = controller;
                Pool = pool;
                Requests = new[] { request };
                Request = request;
                ConfigurationRevision = configurationRevision;
                FastRuntimeSourceRevision = fastRuntimeSourceRevision;
                Generation = generation;
            }

            internal MmdUnityPlaybackController Controller { get; }

            internal MmdMultiCharacterWorkerPool Pool { get; }

            internal MmdMultiCharacterWorkerRequest[] Requests { get; }

            internal MmdMultiCharacterWorkerRequest Request { get; }

            internal int ConfigurationRevision { get; }

            internal long FastRuntimeSourceRevision { get; }

            internal ulong Generation { get; }

            internal bool DispatchStarted { get; set; }

            internal bool Invalidated { get; set; }

            internal Exception? DispatchError { get; set; }

            internal string? RejectionReason { get; set; }
        }

        private static readonly List<PendingEvaluation> pending = new();
        private static bool collectionWindowActive;
        private static bool installed;
        private static ulong generation;

        internal static bool IsCollectionWindowActive => collectionWindowActive;

        internal static int PendingCount => pending.Count;

        internal static int LastCompletedBatchSize { get; private set; }

        internal static ulong LastCompletedGeneration { get; private set; }

        internal static int LastDrainFrameCount { get; private set; } = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetForDomainReload()
        {
            collectionWindowActive = false;
            installed = false;
            generation = 0;
            pending.Clear();
            LastCompletedBatchSize = 0;
            LastCompletedGeneration = 0;
            LastDrainFrameCount = -1;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InstallAtRuntime()
        {
            _ = EnsureInstalled();
        }

        internal static bool EnsureInstalled()
        {
            PlayerLoopSystem current = PlayerLoop.GetCurrentPlayerLoop();
            if (installed && HasInstalledBoundaryPair(current))
            {
                // Re-publish the current tree as well. Unity's test runner and a few host
                // integrations replace the active loop without clearing our installation flag;
                // publishing the verified tree makes the marker delegates active again.
                PlayerLoop.SetPlayerLoop(current);
                return true;
            }

            installed = false;
            if (!TryPatchPlayerLoop(current, out PlayerLoopSystem patched))
            {
                // Keep the synchronous Timeline path when a Unity version or host has no exact
                // anchors. No partial PlayerLoop mutation is published.
                return false;
            }

            PlayerLoop.SetPlayerLoop(patched);
            installed = true;
            return true;
        }

        private static bool HasInstalledBoundaryPair(PlayerLoopSystem current)
        {
            if (current.type == typeof(Update))
            {
                PlayerLoopSystem[]? children = current.subSystemList;
                if (children == null)
                {
                    return false;
                }

                int collectionMarkerIndex = FindType<CollectionBoundaryMarker>(children);
                int drainMarkerIndex = FindType<DrainBoundaryMarker>(children);
                int collectionAnchorIndex = FindType<Update.DirectorUpdate>(children);
                return collectionMarkerIndex >= 0 &&
                    drainMarkerIndex >= 0 &&
                    children[collectionMarkerIndex].updateDelegate != null &&
                    children[drainMarkerIndex].updateDelegate != null &&
                    collectionMarkerIndex + 1 == collectionAnchorIndex &&
                    drainMarkerIndex == collectionAnchorIndex + 1;
            }

            PlayerLoopSystem[]? currentChildren = current.subSystemList;
            if (currentChildren == null)
            {
                return false;
            }

            for (int i = 0; i < currentChildren.Length; i++)
            {
                if (HasInstalledBoundaryPair(currentChildren[i]))
                {
                    return true;
                }
            }

            return false;
        }

        internal static MmdTimelineWorkerQueueResult TryEnqueue(
            MmdUnityPlaybackController controller,
            MmdMultiCharacterWorkerPool pool,
            MmdMultiCharacterWorkerRequest request,
            int configurationRevision,
            out string reason)
        {
            MmdTimelineWorkerQueueResult result = TryEnqueueCore(
                controller,
                pool,
                request,
                configurationRevision,
                out reason);
            if (result == MmdTimelineWorkerQueueResult.Queued ||
                result == MmdTimelineWorkerQueueResult.Rejected)
            {
                // A rejected request still means Timeline owned this controller for the current
                // Director evaluation. Keep standalone playback from immediately double-driving
                // the controller while the rejection is diagnosed at the drain boundary.
                controller.MarkTimelineDriveForWorker();
            }

            return result;
        }

        private static MmdTimelineWorkerQueueResult TryEnqueueCore(
            MmdUnityPlaybackController controller,
            MmdMultiCharacterWorkerPool pool,
            MmdMultiCharacterWorkerRequest request,
            int configurationRevision,
            out string reason)
        {
            if (controller == null)
            {
                throw new ArgumentNullException(nameof(controller));
            }

            if (pool == null)
            {
                throw new ArgumentNullException(nameof(pool));
            }

            reason = string.Empty;
            if (!collectionWindowActive)
            {
                return MmdTimelineWorkerQueueResult.NotCollecting;
            }

            try
            {
                request.Validate();
            }
            catch (Exception exception)
            {
                reason = exception.GetType().Name + ": " + exception.Message;
                return MmdTimelineWorkerQueueResult.Rejected;
            }

            for (int i = 0; i < pending.Count; i++)
            {
                PendingEvaluation existing = pending[i];
                if (existing.Controller != controller || existing.Generation != generation)
                {
                    continue;
                }

                // A controller may only have one winner in a collection generation. Invalidate
                // the first request as well so an accidental duplicate cannot partially apply.
                existing.Invalidated = true;
                reason = "A controller received duplicate Timeline worker requests in one PlayerLoop generation.";
                existing.RejectionReason = reason;
                return MmdTimelineWorkerQueueResult.Rejected;
            }

            var entry = new PendingEvaluation(
                controller,
                pool,
                request,
                configurationRevision,
                controller.TimelineWorkerFastRuntimeSourceRevision,
                generation);
            pending.Add(entry);
            try
            {
                pool.BeginEvaluate(entry.Requests);
                entry.DispatchStarted = true;
                return MmdTimelineWorkerQueueResult.Queued;
            }
            catch (Exception exception)
            {
                entry.Invalidated = true;
                entry.DispatchError = exception;
                reason = exception.GetType().Name + ": " + exception.Message;
                entry.RejectionReason = reason;
                return MmdTimelineWorkerQueueResult.Rejected;
            }
        }

        /// <summary>
        /// Pure PlayerLoop tree transformation used by runtime installation and contract tests.
        /// </summary>
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

        private static bool TryPatchUpdate(
            PlayerLoopSystem[]? current,
            out PlayerLoopSystem[] patched)
        {
            patched = Array.Empty<PlayerLoopSystem>();
            if (current == null)
            {
                return false;
            }

            int collectionMarkerIndex = FindType<CollectionBoundaryMarker>(current);
            int drainMarkerIndex = FindType<DrainBoundaryMarker>(current);
            int collectionAnchorIndex = FindType<Update.DirectorUpdate>(current);
            if (collectionAnchorIndex < 0)
            {
                return false;
            }

            if (collectionMarkerIndex >= 0 || drainMarkerIndex >= 0)
            {
                if (collectionMarkerIndex < 0 || drainMarkerIndex < 0 ||
                    collectionMarkerIndex + 1 != collectionAnchorIndex ||
                    drainMarkerIndex != collectionAnchorIndex + 1)
                {
                    return false;
                }

                var repairedChildren = (PlayerLoopSystem[])current.Clone();
                repairedChildren[collectionMarkerIndex].updateDelegate = BeginCollectionBoundary;
                repairedChildren[drainMarkerIndex].updateDelegate = DrainCollectionBoundary;
                patched = repairedChildren;
                return true;
            }

            var children = new List<PlayerLoopSystem>(current);
            children.Insert(
                collectionAnchorIndex,
                new PlayerLoopSystem
                {
                    type = typeof(CollectionBoundaryMarker),
                    updateDelegate = BeginCollectionBoundary
                });
            children.Insert(
                collectionAnchorIndex + 2,
                new PlayerLoopSystem
                {
                    type = typeof(DrainBoundaryMarker),
                    updateDelegate = DrainCollectionBoundary
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

        private static void BeginCollectionBoundary()
        {
            if (!Application.isPlaying)
            {
                collectionWindowActive = false;
                pending.Clear();
                return;
            }

            generation++;
            if (generation == 0)
            {
                generation = 1;
            }

            pending.Clear();
            LastCompletedBatchSize = 0;
            collectionWindowActive = true;
        }

        private static void DrainCollectionBoundary()
        {
            if (!collectionWindowActive)
            {
                return;
            }

            LastCompletedBatchSize = pending.Count;
            LastCompletedGeneration = generation;
            LastDrainFrameCount = Time.frameCount;
            try
            {
                bool allWorkersCompleted = true;
                for (int i = 0; i < pending.Count; i++)
                {
                    PendingEvaluation entry = pending[i];
                    if (!entry.DispatchStarted)
                    {
                        allWorkersCompleted = false;
                        continue;
                    }

                    try
                    {
                        entry.Pool.CompleteEvaluate();
                    }
                    catch (Exception exception)
                    {
                        entry.DispatchError ??= exception;
                        allWorkersCompleted = false;
                    }
                }

                bool allResultsValid = allWorkersCompleted;
                for (int i = 0; i < pending.Count; i++)
                {
                    PendingEvaluation entry = pending[i];
                    if (entry.Invalidated || entry.DispatchError != null ||
                        entry.Generation != generation)
                    {
                        allResultsValid = false;
                        continue;
                    }

                    if (!entry.Controller.TryValidateTimelineWorkerApply(
                            entry.Request.Frame,
                            entry.Request.Time,
                        entry.Request.FrameRate,
                        entry.ConfigurationRevision,
                        entry.FastRuntimeSourceRevision,
                        entry.Pool,
                        entry.Pool.GetResult(0),
                            out string reason))
                    {
                        entry.DispatchError = new InvalidOperationException(reason);
                        allResultsValid = false;
                    }
                }

                // Validation is intentionally a separate complete pass. No controller receives a
                // Unity mutation until every result in this generation has passed.
                if (!allResultsValid)
                {
                    LogRejectedBatch();
                    return;
                }

                for (int i = 0; i < pending.Count; i++)
                {
                    PendingEvaluation entry = pending[i];
                    try
                    {
                        entry.Controller.ApplyPreparedMultiCharacterFrame(
                            entry.Request.Frame,
                            entry.Request.Time,
                            entry.Request.FrameRate,
                            entry.Pool.GetResult(0));
                    }
                    catch (Exception exception)
                    {
                        // This should be unreachable after the validation pass, but never let one
                        // controller prevent the barrier from being closed for the next frame.
                        Debug.LogError(
                            "Timeline worker result application failed: " + exception,
                            entry.Controller);
                    }
                }
            }
            finally
            {
                pending.Clear();
                collectionWindowActive = false;
            }
        }

        private static void LogRejectedBatch()
        {
            for (int i = 0; i < pending.Count; i++)
            {
                PendingEvaluation entry = pending[i];
                if (entry.DispatchError != null)
                {
                    Debug.LogWarning(
                        "Timeline worker batch was rejected: " + entry.DispatchError.Message,
                        entry.Controller);
                }
                else if (!string.IsNullOrWhiteSpace(entry.RejectionReason))
                {
                    Debug.LogWarning(
                        "Timeline worker batch was rejected: " + entry.RejectionReason,
                        entry.Controller);
                }
            }
        }
    }
}
