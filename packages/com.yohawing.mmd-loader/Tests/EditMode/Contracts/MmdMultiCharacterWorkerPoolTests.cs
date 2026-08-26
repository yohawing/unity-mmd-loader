#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using Mmd.Native;
using Mmd.UnityIntegration;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Mmd.Tests.Contracts
{
    public sealed class MmdMultiCharacterWorkerPoolTests
    {
        [Test]
        public void WorkersAreLongLivedAndEvaluatorDisposalRunsOnOwnerThread()
        {
            var evaluators = CreateEvaluators(2);
            using (var pool = new MmdMultiCharacterWorkerPool(evaluators))
            {
                int[] workerIds = pool.WorkerManagedThreadIds;
                Assert.That(workerIds, Is.Unique);
                Assert.That(workerIds[0], Is.EqualTo(evaluators[0].InitializeThreadId));
                Assert.That(workerIds[1], Is.EqualTo(evaluators[1].InitializeThreadId));

                pool.Evaluate(12, 30.0f);
                Assert.That(pool.GetResult(0).WorldMatrices[0], Is.EqualTo(12.0f));
                Assert.That(pool.GetResult(1).WorldMatrices[0], Is.EqualTo(12.0f));
            }

            Assert.That(evaluators[0].DisposedThreadId, Is.EqualTo(evaluators[0].InitializeThreadId));
            Assert.That(evaluators[1].DisposedThreadId, Is.EqualTo(evaluators[1].InitializeThreadId));
        }

        [Test]
        public void TwoAndFourWorkersOverlapWithoutOverlappingOneWorkerState()
        {
            foreach (int count in new[] { 2, 4 })
            {
                using var overlap = new Barrier(count);
                var evaluators = CreateEvaluators(count, overlap);
                using (var pool = new MmdMultiCharacterWorkerPool(evaluators))
                {
                    pool.Evaluate(3, 30.0f);
                }

                Assert.That(Maximum(evaluators, evaluator => evaluator.MaximumConcurrency), Is.EqualTo(1));
                Assert.That(Maximum(evaluators, evaluator => evaluator.ObservedConcurrentWorkers), Is.EqualTo(count));
            }
        }

        [Test]
        public void EvaluationExceptionIsReturnedAfterAllWorkersReachBarrier()
        {
            using var overlap = new Barrier(3);
            var evaluators = CreateEvaluators(3, overlap);
            evaluators[1].ThrowOnEvaluate = true;
            using var pool = new MmdMultiCharacterWorkerPool(evaluators);

            Assert.Throws<InvalidOperationException>(() => pool.Evaluate(7, 30.0f));
            Assert.That(evaluators[0].EvaluationCount, Is.EqualTo(1));
            Assert.That(evaluators[1].EvaluationCount, Is.EqualTo(1));
            Assert.That(evaluators[2].EvaluationCount, Is.EqualTo(1));
        }

        [Test]
        public void ConcurrentEvaluationOnOnePoolFailsClosed()
        {
            var evaluators = CreateEvaluators(2);
            using var pool = new MmdMultiCharacterWorkerPool(evaluators);
            Exception? backgroundError = null;
            using var entered = new ManualResetEventSlim(false);
            evaluators[0].Entered = entered;

            var thread = new Thread(() =>
            {
                try
                {
                    pool.Evaluate(1, 30.0f);
                }
                catch (Exception exception)
                {
                    backgroundError = exception;
                }
            });
            thread.Start();
            Assert.That(entered.Wait(TimeSpan.FromSeconds(5)), Is.True);
            Assert.Throws<InvalidOperationException>(() => pool.Evaluate(2, 30.0f));
            evaluators[0].Release = true;
            Assert.That(thread.Join(TimeSpan.FromSeconds(5)), Is.True);
            Assert.That(backgroundError, Is.Null);
        }

        [Test]
        public void DisposeWaitsForInFlightEvaluationBeforeStoppingWorkers()
        {
            var evaluators = CreateEvaluators(2);
            using var entered = new ManualResetEventSlim(false);
            using var disposeCompleted = new ManualResetEventSlim(false);
            evaluators[0].Entered = entered;
            var pool = new MmdMultiCharacterWorkerPool(evaluators);
            Exception? evaluationError = null;
            Exception? disposeError = null;

            var evaluationThread = new Thread(() =>
            {
                try
                {
                    pool.Evaluate(1, 30.0f);
                }
                catch (Exception exception)
                {
                    evaluationError = exception;
                }
            });
            var disposeThread = new Thread(() =>
            {
                try
                {
                    pool.Dispose();
                }
                catch (Exception exception)
                {
                    disposeError = exception;
                }
                finally
                {
                    disposeCompleted.Set();
                }
            });

            evaluationThread.Start();
            Assert.That(entered.Wait(TimeSpan.FromSeconds(5)), Is.True);
            disposeThread.Start();
            Assert.That(disposeCompleted.Wait(TimeSpan.FromMilliseconds(100)), Is.False);
            evaluators[0].Release = true;
            Assert.That(evaluationThread.Join(TimeSpan.FromSeconds(5)), Is.True);
            Assert.That(disposeThread.Join(TimeSpan.FromSeconds(5)), Is.True);
            Assert.That(evaluationError, Is.Null);
            Assert.That(disposeError, Is.Null);
        }

        [Test]
        public void CleanupFailureReturnsToCallerAfterEveryWorkerStops()
        {
            var evaluators = CreateEvaluators(2);
            evaluators[0].ThrowOnDispose = true;
            var pool = new MmdMultiCharacterWorkerPool(evaluators);

            Assert.Throws<InvalidOperationException>(() => pool.Dispose());
            Assert.That(evaluators[0].DisposedThreadId, Is.EqualTo(evaluators[0].InitializeThreadId));
            Assert.That(evaluators[1].DisposedThreadId, Is.EqualTo(evaluators[1].InitializeThreadId));
        }

        [Test]
        public void GroupDoesNotClaimControllersFromAwake()
        {
            Assert.That(
                typeof(MmdMultiCharacterPlaybackGroup).GetMethod(
                    "Awake",
                    BindingFlags.Instance | BindingFlags.NonPublic),
                Is.Null);
        }

        [Test]
        public void GroupWithoutTwoControllersFailsClosed()
        {
            var root = new GameObject("empty-multi-character-group");
            try
            {
                var group = root.AddComponent<MmdMultiCharacterPlaybackGroup>();
                typeof(MmdMultiCharacterPlaybackGroup)
                    .GetMethod("TryClaimControllers", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(group, null);
                Assert.That(group.IsPlaybackActive, Is.False);
                Assert.That(group.LastFailureReason, Does.Contain("At least two"));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ControllerCannotBeClaimedBySecondGroup()
        {
            var controllerRoot = new GameObject("claimed-controller");
            var firstGroupRoot = new GameObject("first-group");
            var secondGroupRoot = new GameObject("second-group");
            try
            {
                var controller = controllerRoot.AddComponent<MmdUnityPlaybackController>();
                var firstGroup = firstGroupRoot.AddComponent<MmdMultiCharacterPlaybackGroup>();
                var secondGroup = secondGroupRoot.AddComponent<MmdMultiCharacterPlaybackGroup>();
                controller.AssignMultiCharacterGroup(firstGroup);

                Assert.That(
                    controller.TryClaimMultiCharacterGroup(secondGroup, out string reason),
                    Is.False);
                Assert.That(reason, Does.Contain("another group"));
                Assert.That(controller.IsMultiCharacterClaimed, Is.True);
                controller.ReleaseMultiCharacterGroup(firstGroup);
                Assert.That(controller.IsMultiCharacterClaimed, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(controllerRoot);
                Object.DestroyImmediate(firstGroupRoot);
                Object.DestroyImmediate(secondGroupRoot);
            }
        }

        [Test]
        public void MultiCharacterClockAdvanceCanBeRolledBackWithoutLosingFractionalState()
        {
            var root = new GameObject("multi-character-clock");
            try
            {
                var controller = root.AddComponent<MmdUnityPlaybackController>();
                MmdUnityPlaybackController.MmdMultiCharacterClockState previous =
                    controller.AdvanceMultiCharacterClock(1.0f / 30.0f);
                Assert.That(controller.CurrentFrame, Is.EqualTo(1));

                controller.RestoreMultiCharacterClock(previous);
                Assert.That(controller.CurrentFrame, Is.EqualTo(0));
                Assert.That(previous.playbackFrame, Is.EqualTo(0.0f));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void GroupWithInsufficientSourceConfigurationFailsClosed()
        {
            var root = new GameObject("source-gated-group");
            var first = new GameObject("first-controller");
            var second = new GameObject("second-controller");
            first.transform.SetParent(root.transform);
            second.transform.SetParent(root.transform);
            try
            {
                first.AddComponent<MmdUnityPlaybackController>();
                second.AddComponent<MmdUnityPlaybackController>();
                var group = root.AddComponent<MmdMultiCharacterPlaybackGroup>();
                typeof(MmdMultiCharacterPlaybackGroup)
                    .GetMethod("TryClaimControllers", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(group, null);

                Assert.That(group.IsPlaybackActive, Is.False);
                Assert.That(group.LastFailureReason, Does.Contain("controller-owned PMX and VMD"));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void HumanoidInputIsRejectedByMultiCharacterStateGate()
        {
            var root = new GameObject("humanoid-gated-controller");
            try
            {
                var controller = root.AddComponent<MmdUnityPlaybackController>();
                typeof(MmdUnityPlaybackController)
                    .GetField("proxyRoot", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .SetValue(controller, root.transform);
                typeof(MmdUnityPlaybackController)
                    .GetField("humanoidRetargetEntries", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .SetValue(controller, new List<MmdHumanoidRetargetBinding>
                    {
                        new(HumanBodyBones.Hips, 0, root.transform, root.transform)
                    });

                Assert.That(
                    controller.TryValidateMultiCharacterState(out string reason),
                    Is.False);
                Assert.That(reason, Does.Contain("Humanoid retarget"));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void NativeWorkersMatchSerialMorphOutputAcrossRepeatAndReverseFrames()
        {
            AssertNativeWorkerParity(
                ResolveFixture("test_vertex_morph.pmx"),
                ResolveFixture("test_vertex_morph_motion.vmd"),
                new[] { 0, 10, 5, 10, 0 });
        }

        [Test]
        public void NativeWorkersMatchSerialIkOutput()
        {
            AssertNativeWorkerParity(
                ResolveFixture("test_semi_basic_bone.pmx"),
                ResolveFixture("test_1bone_cube_motion.vmd"),
                new[] { 0, 12, 0 });
        }

        private static void AssertNativeWorkerParity(
            string pmxPath,
            string vmdPath,
            IReadOnlyList<int> frames)
        {
            byte[] pmxBytes = File.ReadAllBytes(pmxPath);
            byte[] vmdBytes = File.ReadAllBytes(vmdPath);
            using MmdRuntimeFfiPlaybackSession serial =
                MmdRuntimeFfiPlaybackSession.Create(pmxBytes, vmdBytes);
            var evaluators = new List<MmdMultiCharacterWorkerPool.IEvaluator>
            {
                new MmdNativeMultiCharacterWorker((byte[])pmxBytes.Clone(), (byte[])vmdBytes.Clone(), 0),
                new MmdNativeMultiCharacterWorker((byte[])pmxBytes.Clone(), (byte[])vmdBytes.Clone(), 0)
            };
            using var pool = new MmdMultiCharacterWorkerPool(evaluators);
            var worldMatrices = new float[serial.WorldMatrixFloatCount];
            var morphWeights = new float[serial.MorphWeightCount];
            var ikEnabled = new byte[serial.IkEnabledCount];

            foreach (int frame in frames)
            {
                serial.EvaluateAndCopy(frame, worldMatrices, morphWeights, ikEnabled, 0);
                pool.Evaluate(frame, 30.0f);
                for (int worker = 0; worker < 2; worker++)
                {
                    MmdMultiCharacterWorkerResult actual = pool.GetResult(worker);
                    AssertFloatBitsEqual(worldMatrices, actual.WorldMatrices, "world matrix", frame, worker);
                    AssertFloatBitsEqual(morphWeights, actual.MorphWeights, "morph", frame, worker);
                    CollectionAssert.AreEqual(
                        ikEnabled,
                        actual.IkEnabled,
                        $"IK enabled mismatch at frame={frame}, worker={worker}");
                }
            }
        }

        private static void AssertFloatBitsEqual(
            float[] expected,
            float[] actual,
            string label,
            int frame,
            int worker)
        {
            Assert.That(actual.Length, Is.EqualTo(expected.Length));
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.That(
                    BitConverter.SingleToInt32Bits(actual[i]),
                    Is.EqualTo(BitConverter.SingleToInt32Bits(expected[i])),
                    $"{label} mismatch at frame={frame}, worker={worker}, index={i}");
            }
        }

        private static string ResolveFixture(string fileName)
        {
            return Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "..",
                "Packages",
                "com.yohawing.mmd-loader",
                "Tests",
                "Fixtures",
                "Assets",
                fileName));
        }

        private static List<TestEvaluator> CreateEvaluators(
            int count,
            Barrier? overlap = null)
        {
            var probe = new ConcurrencyProbe();
            var result = new List<TestEvaluator>(count);
            for (int i = 0; i < count; i++)
            {
                result.Add(new TestEvaluator(overlap, probe));
            }

            return result;
        }

        private static int Maximum(
            IReadOnlyList<TestEvaluator> evaluators,
            Func<TestEvaluator, int> selector)
        {
            int result = 0;
            for (int i = 0; i < evaluators.Count; i++)
            {
                result = Math.Max(result, selector(evaluators[i]));
            }

            return result;
        }

        private sealed class TestEvaluator : MmdMultiCharacterWorkerPool.IEvaluator
        {
            private readonly Barrier? overlap;
            private readonly ConcurrencyProbe probe;
            private int currentConcurrency;
            private int evaluationCount;

            internal TestEvaluator(Barrier? overlap, ConcurrencyProbe probe)
            {
                this.overlap = overlap;
                this.probe = probe;
            }

            internal int InitializeThreadId { get; private set; }

            internal int DisposedThreadId { get; private set; }

            internal int MaximumConcurrency { get; private set; }

            internal int ObservedConcurrentWorkers => probe.Maximum;

            internal int EvaluationCount => Volatile.Read(ref evaluationCount);

            internal bool ThrowOnEvaluate { get; set; }

            internal bool ThrowOnDispose { get; set; }

            internal ManualResetEventSlim? Entered { get; set; }

            private int release;

            internal bool Release
            {
                get => Volatile.Read(ref release) != 0;
                set => Volatile.Write(ref release, value ? 1 : 0);
            }

            public MmdMultiCharacterWorkerResult Initialize()
            {
                InitializeThreadId = Thread.CurrentThread.ManagedThreadId;
                return new MmdMultiCharacterWorkerResult(
                    new float[1],
                    new float[1],
                    Array.Empty<byte>());
            }

            public void Evaluate(int frame, float time, MmdMultiCharacterWorkerResult result)
            {
                _ = time;
                Interlocked.Increment(ref evaluationCount);
                int current = Interlocked.Increment(ref currentConcurrency);
                MaximumConcurrency = Math.Max(MaximumConcurrency, current);
                probe.Enter();

                Entered?.Set();
                while (!Release && Entered != null)
                {
                    Thread.Yield();
                }

                overlap?.SignalAndWait(TimeSpan.FromSeconds(5));
                if (ThrowOnEvaluate)
                {
                    probe.Exit();
                    Interlocked.Decrement(ref currentConcurrency);
                    throw new InvalidOperationException("test evaluator failure");
                }

                result.WorldMatrices[0] = frame;
                probe.Exit();
                Interlocked.Decrement(ref currentConcurrency);
            }

            public void Dispose()
            {
                DisposedThreadId = Thread.CurrentThread.ManagedThreadId;
                if (ThrowOnDispose)
                {
                    throw new InvalidOperationException("test evaluator cleanup failure");
                }
            }
        }

        private sealed class ConcurrencyProbe
        {
            private int current;

            private int maximum;

            internal int Maximum => Volatile.Read(ref maximum);

            internal void Enter()
            {
                int value = Interlocked.Increment(ref current);
                int previous;
                do
                {
                    previous = Volatile.Read(ref maximum);
                    if (value <= previous)
                    {
                        break;
                    }
                }
                while (Interlocked.CompareExchange(ref maximum, value, previous) != previous);
            }

            internal void Exit()
            {
                Interlocked.Decrement(ref current);
            }
        }
    }
}
