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
            PlayerLoopSystem update = FindNode(patched, typeof(Update));
            Assert.That(FindType(update, collectionMarker) + 1, Is.EqualTo(FindType(
                update,
                typeof(Update.DirectorUpdate))));
            Assert.That(FindType(update, drainMarker), Is.EqualTo(FindType(
                update,
                typeof(Update.DirectorUpdate)) + 1));

            Assert.That(
                MmdTimelineWorkerBatchScheduler.TryPatchPlayerLoop(patched, out PlayerLoopSystem patchedAgain),
                Is.True);
            Assert.That(CountType(patchedAgain, collectionMarker), Is.EqualTo(1));
            Assert.That(CountType(patchedAgain, drainMarker), Is.EqualTo(1));
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

        private static int FindType(PlayerLoopSystem loop, Type type)
        {
            PlayerLoopSystem[] children = loop.subSystemList!;
            for (int i = 0; i < children.Length; i++)
            {
                if (children[i].type == type)
                {
                    return i;
                }
            }

            return -1;
        }

        private static PlayerLoopSystem FindNode(PlayerLoopSystem loop, Type type)
        {
            if (loop.type == type)
            {
                return loop;
            }

            PlayerLoopSystem[]? children = loop.subSystemList;
            if (children != null)
            {
                for (int i = 0; i < children.Length; i++)
                {
                    PlayerLoopSystem result = FindNode(children[i], type);
                    if (result.type == type)
                    {
                        return result;
                    }
                }
            }

            return default;
        }

        private static int CountType(PlayerLoopSystem loop, Type type)
        {
            int count = loop.type == type ? 1 : 0;
            PlayerLoopSystem[]? children = loop.subSystemList;
            if (children == null)
            {
                return count;
            }

            for (int i = 0; i < children.Length; i++)
            {
                count += CountType(children[i], type);
            }

            return count;
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
