#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.LowLevel;
using UnityEngine.PlayerLoop;
using UnityEngine.TestTools;
using Mmd.Timeline;
using Mmd.UnityIntegration;

namespace Mmd.Tests
{
    public sealed class MmdTimelineWorkerBatchSchedulerTests
    {
        [Test]
        public void PlayerLoopInsertionUsesExactDirectorUpdateAnchorsAndIsIdempotent()
        {
            PlayerLoopSystem current = CreatePlayerLoop(
                includeDirectorUpdate: true,
                includePartialDrainMarker: false,
                typeof(PreLateUpdate.DirectorUpdateAnimationBegin),
                typeof(PreLateUpdate.DirectorDeferredEvaluate),
                typeof(PreLateUpdate.ScriptRunBehaviourLateUpdate));

            Assert.That(
                MmdTimelineWorkerBatchScheduler.TryPatchPlayerLoop(current, out PlayerLoopSystem patched),
                Is.True);

            Type collectionMarker = GetMarkerType("CollectionBoundaryMarker");
            Type drainMarker = GetMarkerType("DrainBoundaryMarker");
            PlayerLoopSystem update = MmdTestPlayerLoop.FindNode(patched, typeof(Update));
            Assert.That(MmdTestPlayerLoop.FindType(update, collectionMarker) + 1, Is.EqualTo(MmdTestPlayerLoop.FindType(
                update,
                typeof(Update.DirectorUpdate))));
            Assert.That(MmdTestPlayerLoop.FindType(update, drainMarker), Is.EqualTo(MmdTestPlayerLoop.FindType(
                update,
                typeof(Update.DirectorUpdate)) + 1));

            Assert.That(
                MmdTimelineWorkerBatchScheduler.TryPatchPlayerLoop(patched, out PlayerLoopSystem patchedAgain),
                Is.True);
            Assert.That(MmdTestPlayerLoop.CountType(patchedAgain, collectionMarker), Is.EqualTo(1));
            Assert.That(MmdTestPlayerLoop.CountType(patchedAgain, drainMarker), Is.EqualTo(1));
            Assert.That(patchedAgain.subSystemList, Has.Length.EqualTo(patched.subSystemList!.Length));
        }

        [Test]
        public void PlayerLoopInsertionFailsClosedWhenAnExactAnchorIsMissing()
        {
            PlayerLoopSystem current = CreatePlayerLoop(
                includeDirectorUpdate: false,
                includePartialDrainMarker: false,
                typeof(PreLateUpdate.DirectorDeferredEvaluate));

            Assert.That(
                MmdTimelineWorkerBatchScheduler.TryPatchPlayerLoop(current, out PlayerLoopSystem patched),
                Is.False);
            Assert.That(patched.subSystemList, Is.SameAs(current.subSystemList));
        }

        [Test]
        public void PlayerLoopInsertionFailsClosedWhenBoundaryMarkerIsPartial()
        {
            PlayerLoopSystem current = CreatePlayerLoop(
                includeDirectorUpdate: true,
                includePartialDrainMarker: true,
                typeof(PreLateUpdate.DirectorDeferredEvaluate),
                typeof(PreLateUpdate.ScriptRunBehaviourLateUpdate));

            Assert.That(
                MmdTimelineWorkerBatchScheduler.TryPatchPlayerLoop(current, out PlayerLoopSystem patched),
                Is.False);
            Assert.That(patched.subSystemList, Is.SameAs(current.subSystemList));
        }

        [Test]
        public void PlayerLoopInsertionOnDefaultLoopAddsExactlyOneMarkerPairAroundAnchor()
        {
            PlayerLoopSystem current = PlayerLoop.GetDefaultPlayerLoop();
            Type collectionMarker = GetMarkerType("CollectionBoundaryMarker");
            Type drainMarker = GetMarkerType("DrainBoundaryMarker");
            int originalUpdateLength = MmdTestPlayerLoop.FindNode(current, typeof(Update)).subSystemList!.Length;

            Assert.That(
                MmdTimelineWorkerBatchScheduler.TryPatchPlayerLoop(current, out PlayerLoopSystem patched),
                Is.True);
            AssertSingleMarkerPairAroundAnchor(patched, collectionMarker, drainMarker);
            Assert.That(
                MmdTestPlayerLoop.FindNode(patched, typeof(Update)).subSystemList,
                Has.Length.EqualTo(originalUpdateLength + 2));

            Assert.That(
                MmdTimelineWorkerBatchScheduler.TryPatchPlayerLoop(patched, out PlayerLoopSystem patchedAgain),
                Is.True);
            AssertSingleMarkerPairAroundAnchor(patchedAgain, collectionMarker, drainMarker);
            Assert.That(
                MmdTestPlayerLoop.FindNode(patchedAgain, typeof(Update)).subSystemList,
                Has.Length.EqualTo(originalUpdateLength + 2));
        }

        [Test]
        public void EnsureInstalledPublishesOneMarkerPairAndRecoversAfterResetAndLoopReplacement()
        {
            RunWithIsolatedPlayerLoop(() =>
            {
                Type collectionMarker = GetMarkerType("CollectionBoundaryMarker");
                Type drainMarker = GetMarkerType("DrainBoundaryMarker");
                Assert.That(MmdTestPlayerLoop.CountType(PlayerLoop.GetCurrentPlayerLoop(), collectionMarker), Is.Zero);
                Assert.That(MmdTestPlayerLoop.CountType(PlayerLoop.GetCurrentPlayerLoop(), drainMarker), Is.Zero);

                Assert.That(EnsureInstalled(), Is.True);
                Assert.That(IsInstalled(), Is.True);
                AssertSingleMarkerPairAroundAnchor(PlayerLoop.GetCurrentPlayerLoop(), collectionMarker, drainMarker);

                // A second install re-publishes the verified tree instead of inserting again.
                Assert.That(EnsureInstalled(), Is.True);
                AssertSingleMarkerPairAroundAnchor(PlayerLoop.GetCurrentPlayerLoop(), collectionMarker, drainMarker);

                // Domain-reload reset clears the flag while the published tree still holds the
                // markers; reinstalling must repair them in place without duplicates.
                InvokeResetForDomainReload();
                Assert.That(IsInstalled(), Is.False);
                Assert.That(EnsureInstalled(), Is.True);
                Assert.That(IsInstalled(), Is.True);
                AssertSingleMarkerPairAroundAnchor(PlayerLoop.GetCurrentPlayerLoop(), collectionMarker, drainMarker);

                // Another system replaces the active loop without clearing our flag.
                PlayerLoop.SetPlayerLoop(PlayerLoop.GetDefaultPlayerLoop());
                Assert.That(MmdTestPlayerLoop.CountType(PlayerLoop.GetCurrentPlayerLoop(), collectionMarker), Is.Zero);
                Assert.That(EnsureInstalled(), Is.True);
                AssertSingleMarkerPairAroundAnchor(PlayerLoop.GetCurrentPlayerLoop(), collectionMarker, drainMarker);
            });
        }

        [Test]
        public void TimelineWorkerDispatchesEveryControllerBeforeAnyCompletion()
        {
            var barrier = new Barrier(3);
            var firstEvaluator = new BarrierEvaluator(barrier);
            var secondEvaluator = new BarrierEvaluator(barrier);
            var firstPool = new MmdMultiCharacterWorkerPool(new[] { firstEvaluator });
            var secondPool = new MmdMultiCharacterWorkerPool(new[] { secondEvaluator });
            GameObject firstObject = new GameObject("timeline-worker-batch-first");
            GameObject secondObject = new GameObject("timeline-worker-batch-second");
            MmdUnityPlaybackController firstController = firstObject.AddComponent<MmdUnityPlaybackController>();
            MmdUnityPlaybackController secondController = secondObject.AddComponent<MmdUnityPlaybackController>();

            try
            {
                SetCollectionWindowForTest(active: true, generationValue: 1UL);
                MmdTimelineWorkerQueueResult firstEnqueueResult =
                    MmdTimelineWorkerBatchScheduler.TryEnqueue(
                        firstController,
                        firstPool,
                        new MmdMultiCharacterWorkerRequest(1, 1.0f / 30.0f, 30.0f),
                        firstController.ConfigurationRevision,
                        out string firstReason);
                Assert.That(
                    firstEvaluator.Entered.Wait(TimeSpan.FromSeconds(5)),
                    Is.True,
                    "The first worker did not begin evaluating.");
                Assert.That(firstEnqueueResult, Is.EqualTo(MmdTimelineWorkerQueueResult.Queued), firstReason);

                MmdTimelineWorkerQueueResult secondEnqueueResult =
                    MmdTimelineWorkerBatchScheduler.TryEnqueue(
                        secondController,
                        secondPool,
                        new MmdMultiCharacterWorkerRequest(2, 2.0f / 30.0f, 30.0f),
                        secondController.ConfigurationRevision,
                        out string secondReason);
                Assert.That(secondEnqueueResult, Is.EqualTo(MmdTimelineWorkerQueueResult.Queued), secondReason);
                Assert.That(
                    secondEvaluator.Entered.Wait(TimeSpan.FromSeconds(5)),
                    Is.True,
                    "The second worker did not begin evaluating after dispatch.");

                Assert.That(
                    barrier.SignalAndWait(TimeSpan.FromSeconds(5)),
                    Is.True,
                    "Both workers did not reach the shared evaluation barrier.");
                InvokeDrainBoundaryForTest();
            }
            finally
            {
                SetCollectionWindowForTest(active: false, generationValue: 1UL);
                firstPool.Dispose();
                secondPool.Dispose();
                UnityEngine.Object.DestroyImmediate(firstObject);
                UnityEngine.Object.DestroyImmediate(secondObject);
                barrier.Dispose();
            }
        }

        [Test]
        public void DuplicateControllerRequestIsRejectedWithDiagnostic()
        {
            var evaluator = new ImmediateEvaluator();
            var pool = new MmdMultiCharacterWorkerPool(new[] { evaluator });
            GameObject controllerObject = new GameObject("timeline-worker-duplicate");
            MmdUnityPlaybackController controller = controllerObject.AddComponent<MmdUnityPlaybackController>();
            try
            {
                SetCollectionWindowForTest(active: true, generationValue: 1UL);
                Assert.That(
                    MmdTimelineWorkerBatchScheduler.TryEnqueue(
                        controller,
                        pool,
                        new MmdMultiCharacterWorkerRequest(1, 1.0f / 30.0f, 30.0f),
                        controller.ConfigurationRevision,
                        out string firstReason),
                    Is.EqualTo(MmdTimelineWorkerQueueResult.Queued),
                    firstReason);
                Assert.That(
                    MmdTimelineWorkerBatchScheduler.TryEnqueue(
                        controller,
                        pool,
                        new MmdMultiCharacterWorkerRequest(2, 2.0f / 30.0f, 30.0f),
                        controller.ConfigurationRevision,
                        out string duplicateReason),
                    Is.EqualTo(MmdTimelineWorkerQueueResult.Rejected));
                Assert.That(duplicateReason, Does.Contain("duplicate Timeline worker requests"));
                Assert.That(
                    MmdUnityPlaybackController.ShouldSuppressSelfTick(
                        controller.LastTimelineDriveFrameCount,
                        Time.frameCount),
                    Is.True,
                    "Rejected Timeline ownership must suppress standalone self-drive for this frame.");

                LogAssert.Expect(
                    LogType.Warning,
                    "Timeline worker batch was rejected: A controller received duplicate Timeline worker requests " +
                    "in one PlayerLoop generation.");
                InvokeDrainBoundaryForTest();
            }
            finally
            {
                SetCollectionWindowForTest(active: false, generationValue: 1UL);
                pool.Dispose();
                UnityEngine.Object.DestroyImmediate(controllerObject);
            }
        }

        private static void AssertSingleMarkerPairAroundAnchor(
            PlayerLoopSystem loop,
            Type collectionMarker,
            Type drainMarker)
        {
            Assert.That(MmdTestPlayerLoop.CountType(loop, collectionMarker), Is.EqualTo(1));
            Assert.That(MmdTestPlayerLoop.CountType(loop, drainMarker), Is.EqualTo(1));
            PlayerLoopSystem update = MmdTestPlayerLoop.FindNode(loop, typeof(Update));
            int collectionIndex = MmdTestPlayerLoop.FindType(update, collectionMarker);
            int drainIndex = MmdTestPlayerLoop.FindType(update, drainMarker);
            int anchorIndex = MmdTestPlayerLoop.FindType(update, typeof(Update.DirectorUpdate));
            Assert.That(anchorIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(collectionIndex + 1, Is.EqualTo(anchorIndex));
            Assert.That(drainIndex, Is.EqualTo(anchorIndex + 1));
            Assert.That(update.subSystemList![collectionIndex].updateDelegate, Is.Not.Null);
            Assert.That(update.subSystemList![drainIndex].updateDelegate, Is.Not.Null);
        }

        // EditMode drives the editor's PlayerLoop, so every live install test restores the
        // original tree and installation flag. Starting from the default loop keeps the test
        // independent from markers a previous run may have left behind.
        private static void RunWithIsolatedPlayerLoop(Action body)
        {
            FieldInfo installedField = typeof(MmdTimelineWorkerBatchScheduler).GetField(
                "installed",
                BindingFlags.NonPublic | BindingFlags.Static)!;
            PlayerLoopSystem original = PlayerLoop.GetCurrentPlayerLoop();
            object originalInstalled = installedField.GetValue(null)!;
            try
            {
                PlayerLoop.SetPlayerLoop(PlayerLoop.GetDefaultPlayerLoop());
                installedField.SetValue(null, false);
                body();
            }
            finally
            {
                PlayerLoop.SetPlayerLoop(original);
                installedField.SetValue(null, originalInstalled);
            }
        }

        private static bool EnsureInstalled()
        {
            return MmdTimelineWorkerBatchScheduler.EnsureInstalled();
        }

        private static bool IsInstalled()
        {
            return (bool)typeof(MmdTimelineWorkerBatchScheduler)
                .GetField("installed", BindingFlags.NonPublic | BindingFlags.Static)!
                .GetValue(null)!;
        }

        private static void InvokeResetForDomainReload()
        {
            typeof(MmdTimelineWorkerBatchScheduler)
                .GetMethod("ResetForDomainReload", BindingFlags.NonPublic | BindingFlags.Static)!
                .Invoke(null, null);
        }

        private static PlayerLoopSystem CreatePlayerLoop(
            bool includeDirectorUpdate,
            bool includePartialDrainMarker = false,
            params Type[] preLateChildTypes)
        {
            var updateChildren = new List<PlayerLoopSystem>();
            if (includePartialDrainMarker)
            {
                updateChildren.Add(new PlayerLoopSystem { type = GetMarkerType("DrainBoundaryMarker") });
            }

            if (includeDirectorUpdate)
            {
                updateChildren.Add(new PlayerLoopSystem { type = typeof(Update.DirectorUpdate) });
            }

            var preLateChildren = new PlayerLoopSystem[preLateChildTypes.Length];
            for (int i = 0; i < preLateChildTypes.Length; i++)
            {
                preLateChildren[i] = new PlayerLoopSystem { type = preLateChildTypes[i] };
            }

            return new PlayerLoopSystem
            {
                type = typeof(PlayerLoopSystem),
                subSystemList = new[]
                {
                    new PlayerLoopSystem
                    {
                        type = typeof(Update),
                        subSystemList = updateChildren.ToArray()
                    },
                    new PlayerLoopSystem
                    {
                        type = typeof(PreLateUpdate),
                        subSystemList = preLateChildren
                    }
                }
            };
        }

        private static Type GetMarkerType(string name)
        {
            return typeof(MmdTimelineWorkerBatchScheduler).GetNestedType(
                name,
                BindingFlags.NonPublic)!;
        }

        private static void SetCollectionWindowForTest(bool active, ulong generationValue)
        {
            Type schedulerType = typeof(MmdTimelineWorkerBatchScheduler);
            if (active)
            {
                ClearPendingForTest(schedulerType);
            }

            schedulerType.GetField("collectionWindowActive", BindingFlags.NonPublic | BindingFlags.Static)!
                .SetValue(null, active);
            schedulerType.GetField("generation", BindingFlags.NonPublic | BindingFlags.Static)!
                .SetValue(null, generationValue);
        }

        private static void ClearPendingForTest(Type schedulerType)
        {
            object pending = schedulerType.GetField("pending", BindingFlags.NonPublic | BindingFlags.Static)!
                .GetValue(null)!;
            ((IList)pending).Clear();
        }

        private static void InvokeDrainBoundaryForTest()
        {
            typeof(MmdTimelineWorkerBatchScheduler)
                .GetMethod("DrainCollectionBoundary", BindingFlags.NonPublic | BindingFlags.Static)!
                .Invoke(null, null);
        }

        private sealed class BarrierEvaluator : MmdMultiCharacterWorkerPool.IEvaluator
        {
            private readonly Barrier barrier;

            internal BarrierEvaluator(Barrier barrier)
            {
                this.barrier = barrier;
            }

            internal ManualResetEventSlim Entered { get; } = new(false);

            public MmdMultiCharacterWorkerResult Initialize()
            {
                return new MmdMultiCharacterWorkerResult(
                    Array.Empty<float>(),
                    Array.Empty<float>(),
                    Array.Empty<byte>());
            }

            public void Evaluate(
                int frame,
                float time,
                float frameRate,
                MmdMultiCharacterWorkerResult result)
            {
                Entered.Set();
                barrier.SignalAndWait(TimeSpan.FromSeconds(5));
            }

            public void Dispose()
            {
                Entered.Dispose();
            }
        }

        private sealed class ImmediateEvaluator : MmdMultiCharacterWorkerPool.IEvaluator
        {
            public MmdMultiCharacterWorkerResult Initialize()
            {
                return new MmdMultiCharacterWorkerResult(
                    Array.Empty<float>(),
                    Array.Empty<float>(),
                    Array.Empty<byte>());
            }

            public void Evaluate(
                int frame,
                float time,
                float frameRate,
                MmdMultiCharacterWorkerResult result)
            {
            }

            public void Dispose()
            {
            }
        }
    }
}
