#nullable enable

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Mmd.UnityIntegration;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.LowLevel;
using UnityEngine.PlayerLoop;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Mmd.Tests.Contracts
{
    public sealed class MmdStandaloneWorkerSchedulerTests
    {
        [Test]
        public void StandaloneBatchDispatchesEverySlotBeforeAnyCompletion()
        {
            using var barrier = new Barrier(3);
            var firstEvaluator = new BarrierEvaluator(barrier);
            var secondEvaluator = new BarrierEvaluator(barrier);
            var firstPool = new MmdMultiCharacterWorkerPool(new[] { firstEvaluator });
            var secondPool = new MmdMultiCharacterWorkerPool(new[] { secondEvaluator });
            Exception? dispatchError = null;
            var dispatchThread = new Thread(() =>
            {
                try
                {
                    MmdStandaloneWorkerScheduler.DispatchStandaloneBatchForTest(
                        new[] { firstPool, secondPool },
                        new[]
                        {
                            new MmdMultiCharacterWorkerRequest(1, 1.0f / 30.0f, 30.0f),
                            new MmdMultiCharacterWorkerRequest(2, 2.0f / 30.0f, 30.0f)
                        });
                }
                catch (Exception exception)
                {
                    dispatchError = exception;
                }
            });

            try
            {
                dispatchThread.Start();
                Assert.That(firstEvaluator.Entered.Wait(TimeSpan.FromSeconds(5)), Is.True,
                    "The first standalone worker did not begin evaluating.");
                Assert.That(secondEvaluator.Entered.Wait(TimeSpan.FromSeconds(5)), Is.True,
                    "The second standalone worker was not dispatched before completion.");
                Assert.That(
                    barrier.SignalAndWait(TimeSpan.FromSeconds(5)),
                    Is.True,
                    "Both standalone workers did not reach the shared evaluation barrier.");
                Assert.That(dispatchThread.Join(TimeSpan.FromSeconds(5)), Is.True,
                    "Standalone batch dispatch did not complete after the barrier opened.");
                Assert.That(dispatchError, Is.Null);
            }
            finally
            {
                // If an ordering regression blocks the first Complete before dispatching the
                // second slot, break the barrier so worker disposal cannot mask the assertion.
                if (!dispatchThread.Join(TimeSpan.Zero))
                {
                    _ = barrier.SignalAndWait(TimeSpan.FromMilliseconds(100));
                    dispatchThread.Join(TimeSpan.FromSeconds(5));
                }

                firstPool.Dispose();
                secondPool.Dispose();
            }
        }

        [Test]
        public void StandaloneWorkerFailureIsIsolatedAndFaultedControllerRollsBack()
        {
            var failedPool = new MmdMultiCharacterWorkerPool(new[] { new ThrowingEvaluator() });
            var healthyEvaluator = new SuccessfulEvaluator();
            var healthyPool = new MmdMultiCharacterWorkerPool(new[] { healthyEvaluator });
            var controllerObject = new GameObject("standalone-worker-failure-isolation");
            MmdUnityPlaybackController failedController =
                controllerObject.AddComponent<MmdUnityPlaybackController>();
            MmdUnityPlaybackController.MmdMultiCharacterClockState previousClock =
                failedController.AdvanceMultiCharacterClock(1.0f);
            int appliedHealthyResults = 0;
            try
            {
                LogAssert.Expect(
                    LogType.Warning,
                    "Standalone native worker playback faulted and was disabled for this controller: " +
                    "forced evaluator failure");
                IReadOnlyList<Exception?> errors = MmdStandaloneWorkerScheduler.DispatchStandaloneBatchForTest(
                    new[] { failedPool, healthyPool },
                    new[]
                    {
                        new MmdMultiCharacterWorkerRequest(1, 1.0f / 30.0f, 30.0f),
                        new MmdMultiCharacterWorkerRequest(1, 1.0f / 30.0f, 30.0f)
                    },
                    (index, error) =>
                    {
                        if (error == null)
                        {
                            Assert.That(index, Is.EqualTo(1));
                            _ = healthyPool.GetResult(0);
                            appliedHealthyResults++;
                            return;
                        }

                        Assert.That(index, Is.EqualTo(0));
                        failedController.HandleStandaloneWorkerFailure(previousClock, "forced evaluator failure");
                    });

                Assert.That(errors[0], Is.Not.Null);
                Assert.That(errors[1], Is.Null);
                Assert.That(healthyEvaluator.EvaluationCount, Is.EqualTo(1));
                Assert.That(appliedHealthyResults, Is.EqualTo(1),
                    "A failed slot must not prevent a healthy sibling from completing and applying.");
                Assert.That(failedController.CurrentFrame, Is.EqualTo(previousClock.currentFrame));
                Assert.That(failedController.IsStandaloneWorkerFaulted, Is.True);
            }
            finally
            {
                failedPool.Dispose();
                healthyPool.Dispose();
                Object.DestroyImmediate(controllerObject);
            }
        }

        [Test]
        public void PlayerLoopInsertionUsesExactUpdateAnchorAndIsIdempotent()
        {
            PlayerLoopSystem current = CreatePlayerLoop(includeAnchor: true);

            Assert.That(
                MmdStandaloneWorkerScheduler.TryPatchPlayerLoop(
                    current,
                    out PlayerLoopSystem patched),
                Is.True);

            PlayerLoopSystem update = MmdTestPlayerLoop.FindNode(patched, typeof(Update));
            Type marker = GetMarkerType();
            int markerIndex = MmdTestPlayerLoop.FindType(update, marker);
            int anchorIndex = MmdTestPlayerLoop.FindType(update, typeof(Update.ScriptRunBehaviourUpdate));
            Assert.That(markerIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(markerIndex + 1, Is.EqualTo(anchorIndex));
            Assert.That(update.subSystemList![markerIndex].updateDelegate, Is.Not.Null);

            Assert.That(
                MmdStandaloneWorkerScheduler.TryPatchPlayerLoop(
                    patched,
                    out PlayerLoopSystem patchedAgain),
                Is.True);
            Assert.That(MmdTestPlayerLoop.CountType(patchedAgain, marker), Is.EqualTo(1));
            Assert.That(patchedAgain.subSystemList![0].subSystemList, Has.Length.EqualTo(2));
        }

        [Test]
        public void PlayerLoopInsertionFailsClosedWhenUpdateAnchorIsMissing()
        {
            PlayerLoopSystem current = CreatePlayerLoop(includeAnchor: false);

            Assert.That(
                MmdStandaloneWorkerScheduler.TryPatchPlayerLoop(
                    current,
                    out PlayerLoopSystem patched),
                Is.False);
            Assert.That(patched.subSystemList, Is.SameAs(current.subSystemList));
        }

        [Test]
        public void PlayerLoopInsertionOnDefaultLoopAddsExactlyOneMarkerBeforeAnchor()
        {
            PlayerLoopSystem current = PlayerLoop.GetDefaultPlayerLoop();
            Type marker = GetMarkerType();
            int originalUpdateLength = MmdTestPlayerLoop.FindNode(current, typeof(Update)).subSystemList!.Length;

            Assert.That(
                MmdStandaloneWorkerScheduler.TryPatchPlayerLoop(current, out PlayerLoopSystem patched),
                Is.True);
            Assert.That(MmdTestPlayerLoop.CountType(patched, marker), Is.EqualTo(1));
            AssertSingleMarkerBeforeAnchor(patched, marker);
            Assert.That(
                MmdTestPlayerLoop.FindNode(patched, typeof(Update)).subSystemList,
                Has.Length.EqualTo(originalUpdateLength + 1));

            Assert.That(
                MmdStandaloneWorkerScheduler.TryPatchPlayerLoop(patched, out PlayerLoopSystem patchedAgain),
                Is.True);
            Assert.That(MmdTestPlayerLoop.CountType(patchedAgain, marker), Is.EqualTo(1));
            AssertSingleMarkerBeforeAnchor(patchedAgain, marker);
            Assert.That(
                MmdTestPlayerLoop.FindNode(patchedAgain, typeof(Update)).subSystemList,
                Has.Length.EqualTo(originalUpdateLength + 1));
        }

        [Test]
        public void EnsureInstalledPublishesOneMarkerAndRecoversAfterResetAndLoopReplacement()
        {
            RunWithIsolatedPlayerLoop(() =>
            {
                Type marker = GetMarkerType();
                Assert.That(MmdTestPlayerLoop.CountType(PlayerLoop.GetCurrentPlayerLoop(), marker), Is.Zero);

                Assert.That(MmdStandaloneWorkerScheduler.EnsureInstalled(), Is.True);
                Assert.That(MmdStandaloneWorkerScheduler.IsInstalled, Is.True);
                AssertSingleMarkerBeforeAnchor(PlayerLoop.GetCurrentPlayerLoop(), marker);

                // A second install must verify the published tree instead of inserting again.
                Assert.That(MmdStandaloneWorkerScheduler.EnsureInstalled(), Is.True);
                AssertSingleMarkerBeforeAnchor(PlayerLoop.GetCurrentPlayerLoop(), marker);

                // Domain-reload reset clears the flag while the published tree still holds the
                // marker; reinstalling must repair it in place without a duplicate.
                InvokeResetForDomainReload();
                Assert.That(MmdStandaloneWorkerScheduler.IsInstalled, Is.False);
                Assert.That(MmdStandaloneWorkerScheduler.EnsureInstalled(), Is.True);
                Assert.That(MmdStandaloneWorkerScheduler.IsInstalled, Is.True);
                AssertSingleMarkerBeforeAnchor(PlayerLoop.GetCurrentPlayerLoop(), marker);

                // Another system replaces the active loop without clearing our flag.
                PlayerLoop.SetPlayerLoop(PlayerLoop.GetDefaultPlayerLoop());
                Assert.That(MmdTestPlayerLoop.CountType(PlayerLoop.GetCurrentPlayerLoop(), marker), Is.Zero);
                Assert.That(MmdStandaloneWorkerScheduler.EnsureInstalled(), Is.True);
                AssertSingleMarkerBeforeAnchor(PlayerLoop.GetCurrentPlayerLoop(), marker);
            });
        }

        private static void AssertSingleMarkerBeforeAnchor(PlayerLoopSystem loop, Type marker)
        {
            Assert.That(MmdTestPlayerLoop.CountType(loop, marker), Is.EqualTo(1));
            PlayerLoopSystem update = MmdTestPlayerLoop.FindNode(loop, typeof(Update));
            int markerIndex = MmdTestPlayerLoop.FindType(update, marker);
            int anchorIndex = MmdTestPlayerLoop.FindType(update, typeof(Update.ScriptRunBehaviourUpdate));
            Assert.That(markerIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(markerIndex + 1, Is.EqualTo(anchorIndex));
            Assert.That(update.subSystemList![markerIndex].updateDelegate, Is.Not.Null);
        }

        // EditMode drives the editor's PlayerLoop, so every live install test restores the
        // original tree and installation flag. Starting from the default loop keeps the test
        // independent from markers a previous run may have left behind.
        private static void RunWithIsolatedPlayerLoop(Action body)
        {
            FieldInfo installedField = typeof(MmdStandaloneWorkerScheduler).GetField(
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

        private static void InvokeResetForDomainReload()
        {
            typeof(MmdStandaloneWorkerScheduler)
                .GetMethod("ResetForDomainReload", BindingFlags.NonPublic | BindingFlags.Static)!
                .Invoke(null, null);
        }

        private static PlayerLoopSystem CreatePlayerLoop(bool includeAnchor)
        {
            var updateChildren = new List<PlayerLoopSystem>();
            if (includeAnchor)
            {
                updateChildren.Add(new PlayerLoopSystem
                {
                    type = typeof(Update.ScriptRunBehaviourUpdate)
                });
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
                    }
                }
            };
        }

        private static Type GetMarkerType()
        {
            return typeof(MmdStandaloneWorkerScheduler).GetNestedType(
                "BoundaryMarker",
                BindingFlags.NonPublic)!;
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

            public void Evaluate(int frame, float time, float frameRate, MmdMultiCharacterWorkerResult result)
            {
                Entered.Set();
                barrier.SignalAndWait(TimeSpan.FromSeconds(5));
            }

            public void Dispose()
            {
                Entered.Dispose();
            }
        }

        private sealed class ThrowingEvaluator : MmdMultiCharacterWorkerPool.IEvaluator
        {
            public MmdMultiCharacterWorkerResult Initialize()
            {
                return new MmdMultiCharacterWorkerResult(
                    Array.Empty<float>(),
                    Array.Empty<float>(),
                    Array.Empty<byte>());
            }

            public void Evaluate(int frame, float time, float frameRate, MmdMultiCharacterWorkerResult result)
            {
                throw new InvalidOperationException("forced evaluator failure");
            }

            public void Dispose()
            {
            }
        }

        private sealed class SuccessfulEvaluator : MmdMultiCharacterWorkerPool.IEvaluator
        {
            internal int EvaluationCount { get; private set; }

            public MmdMultiCharacterWorkerResult Initialize()
            {
                return new MmdMultiCharacterWorkerResult(
                    Array.Empty<float>(),
                    Array.Empty<float>(),
                    Array.Empty<byte>());
            }

            public void Evaluate(int frame, float time, float frameRate, MmdMultiCharacterWorkerResult result)
            {
                EvaluationCount++;
            }

            public void Dispose()
            {
            }
        }
    }
}
