#nullable enable

using System;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.Playables;
using Mmd;
using Mmd.Physics;
using Mmd.UnityIntegration;

namespace Mmd.Timeline
{
    public enum MmdVmdTimelineLoopPolicy
    {
        None = 0
    }

    [Serializable]
    public sealed class MmdVmdTimelineBehaviour : PlayableBehaviour
    {
#if UNITY_EDITOR
        public static event Action<double>? ProcessFrameEvaluated;
#endif

        public MmdUnityPlaybackController? Controller { get; set; }

        public MmdVmdAsset? MotionAsset { get; set; }

        public string ModelSourceId { get; set; } = string.Empty;

        public string MotionSourceId { get; set; } = string.Empty;

        public float FrameRate { get; set; } = 30.0f;

        public float StartOffsetSeconds { get; set; }

        public MmdVmdTimelineLoopPolicy LoopPolicy { get; set; }

        public bool PhysicsOffByDefault => true;

        /// <summary>
        /// Applies this clip's full pose at the playable local time after single-winner arbitration.
        /// Does not scale by weight.
        /// </summary>
        internal void ApplyTimelineEvaluation(Playable playable, object playerData)
        {
            MmdUnityPlaybackController? target = playerData as MmdUnityPlaybackController ?? Controller;
            if (target == null)
            {
                return;
            }

            if (!target.IsConfigured &&
                MotionAsset == null &&
                string.IsNullOrWhiteSpace(target.MotionSourceId))
            {
                return;
            }

            if (!target.IsConfigured &&
                MotionAsset != null &&
                !target.HasModelSource)
            {
                return;
            }

#if UNITY_EDITOR
            ProcessFrameEvaluated?.Invoke(playable.GetTime());
#endif

            // Live physics steps forward during Play Mode Timeline evaluation; outside Play Mode
            // (Editor preview / Timeline-window scrubbing) it falls back to animation-only random access,
            // which also resets Live physics so re-entering Play Mode re-seeds from the current pose.
            //
            // The discriminator is Application.isPlaying ONLY. Verified via Editor.log instrumentation
            // that, within Play Mode, FrameData reports identical signals for forward playback and for a
            // programmatic director.Evaluate()/seek:
            //   eval=Playback, seekOccurred=true, effectivePlayState=Playing  (for BOTH)
            // so none of evaluationType / seekOccurred / effectivePlayState can distinguish play from
            // scrub inside Play Mode. Scrubbing is an Editor (non-playing) operation, so the Play Mode
            // boundary is the contract. Held/paused frames are a no-op because ApplyLivePhysicsForwardFrame
            // returns the cached snapshot when the frame does not advance.
            bool runLivePhysics = Application.isPlaying;
            if (runLivePhysics && TryQueueTimelineWorkerEvaluation(target, playable.GetTime()))
            {
                return;
            }

            EvaluateAtLocalTime(target, playable.GetTime(), runLivePhysics);
        }

        private bool TryQueueTimelineWorkerEvaluation(
            MmdUnityPlaybackController target,
            double localTime)
        {
            if (!MmdTimelineWorkerBatchScheduler.IsCollectionWindowActive ||
                target.PhysicsMode != MmdPhysicsMode.Off)
            {
                return false;
            }

            if (double.IsNaN(localTime) || double.IsInfinity(localTime) || localTime < 0.0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(localTime),
                    "Timeline local time must be a non-negative finite value.");
            }

            MmdPlaybackTime.ValidateFrameRate(FrameRate);
            MmdPlaybackTime.ValidateTime(StartOffsetSeconds);
            if (MotionAsset != null && string.IsNullOrWhiteSpace(MotionSourceId))
            {
                MotionSourceId = string.IsNullOrWhiteSpace(MotionAsset.SourceId) ? MotionAsset.name : MotionAsset.SourceId;
            }

            double sourceTime = localTime + StartOffsetSeconds;
            if (sourceTime > float.MaxValue)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(localTime),
                    "Timeline local time is too large for playback evaluation.");
            }

            if (!TryConfigureTimelineTarget(target, setupTiming: null))
            {
                throw new InvalidOperationException(
                    "Timeline target playback controller is not configured and has no provider-owned PMX/VMD source.");
            }

            if (!target.TryGetOrCreateTimelineWorkerPool(
                    out MmdMultiCharacterWorkerPool pool,
                    out _))
            {
                // Native fast runtime or controller compatibility may be unavailable. Preserve
                // the existing synchronous path in that case.
                return false;
            }

            float sourceTimeValue = (float)sourceTime;
            var request = new MmdMultiCharacterWorkerRequest(
                MmdPlaybackTime.ToFrame(sourceTimeValue, FrameRate),
                sourceTimeValue,
                FrameRate);
            MmdTimelineWorkerQueueResult queueResult = MmdTimelineWorkerBatchScheduler.TryEnqueue(
                target,
                pool,
                request,
                target.ConfigurationRevision,
                out _);
            return queueResult == MmdTimelineWorkerQueueResult.Queued ||
                   queueResult == MmdTimelineWorkerQueueResult.Rejected;
        }

        internal bool TryPrepareTimelinePlayback(object playerData)
        {
            MmdUnityPlaybackController? target = playerData as MmdUnityPlaybackController ?? Controller;
            if (target == null)
            {
                return false;
            }

            MmdMultiCharacterPlaybackGroup.ReleaseForSerialPlayback(
                target,
                nameof(TryPrepareTimelinePlayback));

            var timing = new MmdTimelineSetupTimingSummary();
            target.LastTimelineSetupTiming = timing;
            Stopwatch totalWatch = Stopwatch.StartNew();
            try
            {
                MmdPlaybackTime.ValidateFrameRate(FrameRate);
                if (!TryConfigureTimelineTarget(target, timing))
                {
                    return false;
                }

                timing.configured = true;
                Stopwatch prewarmWatch = Stopwatch.StartNew();
                timing.livePhysicsPrewarmed = target.PrewarmTimelineLivePhysics();
                timing.livePhysicsPrewarmMs = prewarmWatch.Elapsed.TotalMilliseconds;
                Stopwatch seedWatch = Stopwatch.StartNew();
                target.PrepareTimelineSeed(
                    (float)StartOffsetSeconds,
                    FrameRate,
                    runLivePhysics: Application.isPlaying);
                timing.initialSeedMs = seedWatch.Elapsed.TotalMilliseconds;
                timing.succeeded = true;
                return true;
            }
            finally
            {
                timing.totalMs = totalWatch.Elapsed.TotalMilliseconds;
            }
        }

        public MmdPlaybackSnapshot EvaluateAtLocalTime(MmdUnityPlaybackController target, double localTime)
        {
            return EvaluateAtLocalTime(target, localTime, runLivePhysics: false);
        }

        public MmdPlaybackSnapshot EvaluateAtLocalTime(
            MmdUnityPlaybackController target,
            double localTime,
            bool runLivePhysics)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            MmdMultiCharacterPlaybackGroup.ReleaseForSerialPlayback(
                target,
                nameof(EvaluateAtLocalTime));

            if (double.IsNaN(localTime) || double.IsInfinity(localTime) || localTime < 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(localTime), "Timeline local time must be a non-negative finite value.");
            }

            MmdPlaybackTime.ValidateFrameRate(FrameRate);
            MmdPlaybackTime.ValidateTime(StartOffsetSeconds);
            if (MotionAsset != null && string.IsNullOrWhiteSpace(MotionSourceId))
            {
                MotionSourceId = string.IsNullOrWhiteSpace(MotionAsset.SourceId) ? MotionAsset.name : MotionAsset.SourceId;
            }

            double sourceTime = localTime + StartOffsetSeconds;
            if (sourceTime > float.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(localTime), "Timeline local time is too large for playback evaluation.");
            }

            if (!TryConfigureTimelineTarget(target, setupTiming: null))
            {
                throw new InvalidOperationException("Timeline target playback controller is not configured and has no provider-owned PMX/VMD source.");
            }

            // Forward Play Mode playback steps Live physics so 揺れもの simulate during Timeline play.
            // Random-access (scrub/seek/editor preview) instead uses ApplyTimelineTime, which evaluates
            // animation-only while retaining the warmed native physics backend.
            if (runLivePhysics && target.PhysicsMode == MmdPhysicsMode.Live)
            {
                return target.ApplyTimelineLivePhysicsForward((float)sourceTime, FrameRate);
            }

            return target.ApplyTimelineTime((float)sourceTime, FrameRate);
        }

        private bool TryConfigureTimelineTarget(
            MmdUnityPlaybackController target,
            MmdTimelineSetupTimingSummary? setupTiming)
        {
            if (MotionAsset != null && !target.IsConfiguredForMotionAsset(MotionAsset))
            {
                target.ConfigureMotionFromProviderModelSourceForTimeline(
                    MotionAsset,
                    FrameRate,
                    startFrame: 0,
                    playOnStart: false,
                    setupTiming: setupTiming);
            }
            else if (!target.IsConfigured &&
                !target.ConfigureFromPlaybackSourceIfAvailableForTimeline(setupTiming))
            {
                return false;
            }

            return true;
        }
    }
}
