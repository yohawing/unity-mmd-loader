#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using Mmd.Native;
using Mmd.Parser;
using Mmd.Physics;
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
        public void SingleEvaluatorInitializesEvaluatesAndDisposesOnOwnerThread()
        {
            var evaluators = CreateEvaluators(1);
            using (var pool = new MmdMultiCharacterWorkerPool(evaluators))
            {
                int[] workerIds = pool.WorkerManagedThreadIds;
                Assert.That(workerIds, Has.Length.EqualTo(1));
                Assert.That(workerIds[0], Is.EqualTo(evaluators[0].InitializeThreadId));

                pool.Evaluate(12, 30.0f);
                Assert.That(pool.GetResult(0).WorldMatrices[0], Is.EqualTo(12.0f));
            }

            Assert.That(evaluators[0].DisposedThreadId, Is.EqualTo(evaluators[0].InitializeThreadId));
        }

        [Test]
        public void OneToFourWorkersOverlapWithoutOverlappingOneWorkerState()
        {
            foreach (int count in new[] { 1, 2, 3, 4 })
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
        public void BatchRequestsDeliverDistinctAbsoluteFrameTimeAndRateValues()
        {
            using var overlap = new Barrier(3);
            var evaluators = CreateEvaluators(3, overlap);
            var requests = new[]
            {
                new MmdMultiCharacterWorkerRequest(3, 0.25f, 12.0f),
                new MmdMultiCharacterWorkerRequest(17, 1.5f, 24.0f),
                new MmdMultiCharacterWorkerRequest(5, 0.125f, 40.0f)
            };

            using (var pool = new MmdMultiCharacterWorkerPool(evaluators))
            {
                pool.BeginEvaluate(requests);
                pool.CompleteEvaluate();

                for (int i = 0; i < requests.Length; i++)
                {
                    Assert.That(evaluators[i].LastFrame, Is.EqualTo(requests[i].Frame));
                    Assert.That(evaluators[i].LastTime, Is.EqualTo(requests[i].Time));
                    Assert.That(evaluators[i].LastFrameRate, Is.EqualTo(requests[i].FrameRate));
                    Assert.That(pool.GetResult(i).WorldMatrices[0], Is.EqualTo(requests[i].Frame));
                }
            }

            Assert.That(Maximum(evaluators, evaluator => evaluator.ObservedConcurrentWorkers), Is.EqualTo(3));
        }

        [Test]
        public void BatchRequestValidationFailsBeforeDispatchAndLeavesNoEvaluationInFlight()
        {
            var evaluators = CreateEvaluators(2);
            using var pool = new MmdMultiCharacterWorkerPool(evaluators);

            Assert.Throws<ArgumentException>(() => pool.BeginEvaluate(new[]
            {
                new MmdMultiCharacterWorkerRequest(1, 1.0f / 30.0f, 30.0f)
            }));
            Assert.That(evaluators[0].EvaluationCount, Is.EqualTo(0));
            Assert.That(evaluators[1].EvaluationCount, Is.EqualTo(0));
            Assert.Throws<InvalidOperationException>(() => pool.CompleteEvaluate());

            var invalidRequests = new[]
            {
                new MmdMultiCharacterWorkerRequest(1, float.NaN, 30.0f),
                new MmdMultiCharacterWorkerRequest(1, 1.0f / 30.0f, float.PositiveInfinity)
            };
            Assert.Throws<ArgumentOutOfRangeException>(() => pool.BeginEvaluate(invalidRequests));
            Assert.That(evaluators[0].EvaluationCount, Is.EqualTo(0));
            Assert.That(evaluators[1].EvaluationCount, Is.EqualTo(0));
            Assert.Throws<InvalidOperationException>(() => pool.CompleteEvaluate());

            Assert.DoesNotThrow(() => pool.Evaluate(2, 30.0f));
            Assert.That(evaluators[0].EvaluationCount, Is.EqualTo(1));
            Assert.That(evaluators[1].EvaluationCount, Is.EqualTo(1));
        }

        [Test]
        public void BeginAndCompleteRejectOverlapAndProtectInFlightResults()
        {
            var evaluators = CreateEvaluators(2);
            using var entered = new ManualResetEventSlim(false);
            evaluators[0].Entered = entered;
            using var pool = new MmdMultiCharacterWorkerPool(evaluators);

            pool.BeginEvaluate(1, 30.0f);
            Assert.That(entered.Wait(TimeSpan.FromSeconds(5)), Is.True);
            Assert.Throws<InvalidOperationException>(() => pool.BeginEvaluate(2, 30.0f));
            Assert.Throws<InvalidOperationException>(() => pool.GetResult(0));

            evaluators[0].Release = true;
            pool.CompleteEvaluate();
            Assert.That(pool.GetResult(0).WorldMatrices[0], Is.EqualTo(1.0f));
            Assert.Throws<InvalidOperationException>(() => pool.CompleteEvaluate());
        }

        [Test]
        public void EvaluationExceptionIsReturnedAfterAllWorkersReachBarrier()
        {
            using var overlap = new Barrier(3);
            var evaluators = CreateEvaluators(3, overlap);
            evaluators[1].ThrowOnEvaluate = true;
            using var pool = new MmdMultiCharacterWorkerPool(evaluators);

            pool.BeginEvaluate(7, 30.0f);
            Assert.Throws<InvalidOperationException>(() => pool.CompleteEvaluate());
            Assert.That(evaluators[0].EvaluationCount, Is.EqualTo(1));
            Assert.That(evaluators[1].EvaluationCount, Is.EqualTo(1));
            Assert.That(evaluators[2].EvaluationCount, Is.EqualTo(1));
        }

        [Test]
        public void ConcurrentBeginOnOnePoolFailsClosed()
        {
            var evaluators = CreateEvaluators(2);
            using var pool = new MmdMultiCharacterWorkerPool(evaluators);
            using var entered = new ManualResetEventSlim(false);
            evaluators[0].Entered = entered;

            pool.BeginEvaluate(1, 30.0f);
            Assert.That(entered.Wait(TimeSpan.FromSeconds(5)), Is.True);
            Exception? beginError = null;
            var beginThread = new Thread(() =>
            {
                try
                {
                    pool.BeginEvaluate(2, 30.0f);
                }
                catch (Exception exception)
                {
                    beginError = exception;
                }
            });
            beginThread.Start();
            Assert.That(beginThread.Join(TimeSpan.FromSeconds(5)), Is.True);
            Assert.That(beginError, Is.TypeOf<InvalidOperationException>());
            evaluators[0].Release = true;
            Assert.DoesNotThrow(() => pool.CompleteEvaluate());
        }

        [Test]
        public void DisposeWaitsForInFlightEvaluationBeforeStoppingWorkers()
        {
            var evaluators = CreateEvaluators(2);
            using var entered = new ManualResetEventSlim(false);
            using var disposeCompleted = new ManualResetEventSlim(false);
            evaluators[0].Entered = entered;
            var pool = new MmdMultiCharacterWorkerPool(evaluators);
            Exception? disposeError = null;
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

            pool.BeginEvaluate(1, 30.0f);
            Assert.That(entered.Wait(TimeSpan.FromSeconds(5)), Is.True);
            disposeThread.Start();
            Assert.That(disposeCompleted.Wait(TimeSpan.FromMilliseconds(100)), Is.False);
            evaluators[0].Release = true;
            Assert.That(disposeThread.Join(TimeSpan.FromSeconds(5)), Is.True);
            Assert.That(disposeError, Is.Null);
            Assert.That(evaluators[0].EvaluationCount, Is.EqualTo(1));
            Assert.That(evaluators[1].EvaluationCount, Is.EqualTo(1));
            Assert.That(evaluators[0].DisposedThreadId, Is.EqualTo(evaluators[0].InitializeThreadId));
            Assert.That(evaluators[1].DisposedThreadId, Is.EqualTo(evaluators[1].InitializeThreadId));
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
        public void MultiCharacterClockAdvanceAtThirtyFpsKeepsEveryFrameFor120Steps()
        {
            var root = new GameObject("multi-character-clock-thirty-fps");
            try
            {
                var controller = root.AddComponent<MmdUnityPlaybackController>();
                const float deltaTime = 1.0f / 30.0f;

                for (int iteration = 0; iteration < 120; iteration++)
                {
                    int expectedFrame = iteration + 1;
                    controller.AdvanceMultiCharacterClock(deltaTime);
                    Assert.That(
                        controller.CurrentFrame,
                        Is.EqualTo(expectedFrame),
                        $"Clock frame advanced incorrectly at iteration={iteration}.");
                }
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

        [Test]
        public void LiveWorkersMatchSerialPhysicsAcrossForwardAndSameFrameTransitions()
        {
            MmdPhysicsBackendAvailability availability = MmdAnimPhysicsBackend.ProbeAvailability();
            if (!availability.backendAvailable)
            {
                Assert.Ignore("Bullet physics backend is not available: " + availability.unsupportedReason);
            }

            string pmxPath = ResolveFixture("test_hair_physics.pmx");
            string vmdPath = ResolveFixture("test_1bone_cube_motion.vmd");
            byte[] pmxBytes = File.ReadAllBytes(pmxPath);
            byte[] vmdBytes = File.ReadAllBytes(vmdPath);
            MmdModelDefinition model = new NativeMmdParser().LoadModel(pmxBytes);
            model.physics.joints.RemoveAll(joint =>
                joint.rigidbodyAIndex < 0 && joint.rigidbodyBIndex < 0);

            using MmdRuntimeFfiPlaybackSession serialSession =
                MmdRuntimeFfiPlaybackSession.Create(pmxBytes, vmdBytes);
            var serialBefore = new float[serialSession.WorldMatrixFloatCount];
            var serialMorph = new float[serialSession.MorphWeightCount];
            var serialIk = new byte[serialSession.IkEnabledCount];
            serialSession.EvaluateAndCopy(0, serialBefore, serialMorph, serialIk);
            Assert.That(
                MmdAnimPhysicsBackend.TryCreateForPlaybackSession(
                    pmxBytes,
                    serialSession,
                    model.name ?? string.Empty,
                    string.Empty,
                    out MmdAnimPhysicsBackend? serialBackend,
                    out string reason),
                Is.True,
                reason);
            using (serialBackend!)
            {
                serialBackend.InitializeWorld(model);
                serialBackend.Reset();
                var expectedShapeTypes = new string[model.physics.rigidbodies.Count];
                for (int i = 0; i < expectedShapeTypes.Length; i++)
                {
                    expectedShapeTypes[i] = serialBackend.GetRigidbodyShapeType(i);
                }
                var evaluators = new List<MmdMultiCharacterWorkerPool.IEvaluator>
                {
                    new MmdNativeLivePhysicsMultiCharacterWorker(
                        (byte[])pmxBytes.Clone(),
                        (byte[])vmdBytes.Clone(),
                        model,
                        0),
                    new MmdNativeLivePhysicsMultiCharacterWorker(
                        (byte[])pmxBytes.Clone(),
                        (byte[])vmdBytes.Clone(),
                        model,
                        0)
                };
                using var pool = new MmdMultiCharacterWorkerPool(evaluators);

                var serialAfter = new float[serialSession.WorldMatrixFloatCount];
                var serialRigidbodyPositions = new float[model.physics.rigidbodies.Count * 3];
                var serialRigidbodyRotations = new float[model.physics.rigidbodies.Count * 4];
                int serialLastFrame = -1;
                float serialLastTime = 0.0f;
                foreach (int frame in new[] { 0, 1, 2, 3 })
                {
                    float time = frame / 30.0f;
                    MmdPhysicsHostStepDiagnostics serialDiagnostics;
                    pool.Evaluate(frame, 30.0f);
                    StepSerialLivePhysics(
                        serialBackend,
                        serialSession,
                        frame,
                        time,
                        ref serialLastFrame,
                        ref serialLastTime,
                        serialBefore,
                        serialMorph,
                        serialIk,
                        serialAfter,
                        serialRigidbodyPositions,
                        serialRigidbodyRotations,
                        out serialDiagnostics);
                    AssertLiveWorkerResults(
                        pool,
                        serialBefore,
                        serialMorph,
                        serialIk,
                        serialAfter,
                        serialRigidbodyPositions,
                        serialRigidbodyRotations,
                        serialDiagnostics,
                        expectedShapeTypes,
                        frame);
                }

                MmdMultiCharacterWorkerResult sameFrameResult = pool.GetResult(0);
                var sameFrameAfter = (float[])sameFrameResult.AfterPhysicsWorldMatrices.Clone();
                MmdPhysicsHostStepDiagnostics sameFrameDiagnostics = sameFrameResult.LastPhysicsDiagnostics!.Value;
                float sameFrameTime = sameFrameResult.EvaluatedTime;
                float sameFrameDeltaTime = sameFrameResult.DeltaTime;
                bool sameFrameWasSeed = sameFrameResult.WasSeed;
                pool.Evaluate(3, 30.0f);
                MmdMultiCharacterWorkerResult repeatedFrameResult = pool.GetResult(0);
                AssertFloatBitsEqual(sameFrameAfter, repeatedFrameResult.AfterPhysicsWorldMatrices, "same-frame after-physics matrix", 3, 0);
                Assert.That(repeatedFrameResult.EvaluatedFrame, Is.EqualTo(sameFrameResult.EvaluatedFrame));
                Assert.That(BitConverter.SingleToInt32Bits(repeatedFrameResult.EvaluatedTime), Is.EqualTo(BitConverter.SingleToInt32Bits(sameFrameTime)));
                Assert.That(repeatedFrameResult.WasSeed, Is.EqualTo(sameFrameWasSeed));
                Assert.That(BitConverter.SingleToInt32Bits(repeatedFrameResult.DeltaTime), Is.EqualTo(BitConverter.SingleToInt32Bits(sameFrameDeltaTime)));
                Assert.That(repeatedFrameResult.LastPhysicsDiagnostics, Is.EqualTo(sameFrameDiagnostics));
                Assert.That(repeatedFrameResult.LivePhysicsBackendName, Is.EqualTo("mmd-anim-bullet-native"));
                Assert.That(repeatedFrameResult.SkippedWorldAnchorJointCount, Is.EqualTo(serialBackend.SkippedWorldAnchorJointCount));
                CollectionAssert.AreEqual(expectedShapeTypes, repeatedFrameResult.RigidbodyShapeTypes);
                Assert.Throws<InvalidOperationException>(() => pool.Evaluate(2, 30.0f));

            }
        }

        private static void StepSerialLivePhysics(
            MmdAnimPhysicsBackend backend,
            MmdRuntimeFfiPlaybackSession session,
            int frame,
            float time,
            ref int lastFrame,
            ref float lastTime,
            float[] before,
            float[] morph,
            byte[] ik,
            float[] after,
            float[] rigidbodyPositions,
            float[] rigidbodyRotations,
            out MmdPhysicsHostStepDiagnostics diagnostics)
        {
            bool seed = lastFrame < 0;
            backend.StepPlaybackFrame(
                session,
                frame,
                seed,
                seed ? 0.0f : time - lastTime,
                before,
                morph,
                ik,
                after,
                0,
                out diagnostics);
            var position = new float[3];
            var rotation = new float[4];
            for (int body = 0; body < rigidbodyPositions.Length / 3; body++)
            {
                backend.CopyRigidbodyTransform(body, position, rotation);
                Array.Copy(position, 0, rigidbodyPositions, body * 3, 3);
                Array.Copy(rotation, 0, rigidbodyRotations, body * 4, 4);
            }
            lastFrame = frame;
            lastTime = time;
        }

        private static void AssertLiveWorkerResults(
            MmdMultiCharacterWorkerPool pool,
            float[] expectedBefore,
            float[] expectedMorph,
            byte[] expectedIk,
            float[] expectedAfter,
            float[] expectedRigidbodyPositions,
            float[] expectedRigidbodyRotations,
            MmdPhysicsHostStepDiagnostics expectedDiagnostics,
            string[] expectedShapeTypes,
            int frame)
        {
            for (int worker = 0; worker < 2; worker++)
            {
                MmdMultiCharacterWorkerResult actual = pool.GetResult(worker);
                AssertFloatBitsEqual(expectedBefore, actual.BeforePhysicsWorldMatrices, "live before-physics matrix", frame, worker);
                AssertFloatBitsEqual(expectedMorph, actual.MorphWeights, "live morph", frame, worker);
                CollectionAssert.AreEqual(expectedIk, actual.IkEnabled, $"live IK mismatch at frame={frame}, worker={worker}");
                AssertFloatBitsEqual(expectedAfter, actual.AfterPhysicsWorldMatrices, "live after-physics matrix", frame, worker);
                AssertFloatBitsEqual(expectedRigidbodyPositions, actual.RigidbodyPositions, "live rigidbody position", frame, worker);
                AssertFloatBitsEqual(expectedRigidbodyRotations, actual.RigidbodyRotations, "live rigidbody rotation", frame, worker);
                Assert.That(actual.HasLivePhysicsEvaluation, Is.True);
                Assert.That(actual.EvaluatedFrame, Is.EqualTo(frame));
                Assert.That(
                    BitConverter.SingleToInt32Bits(actual.EvaluatedTime),
                    Is.EqualTo(BitConverter.SingleToInt32Bits(frame / 30.0f)));
                Assert.That(actual.WasSeed, Is.EqualTo(frame == 0));
                float expectedDeltaTime = frame == 0 ? 0.0f : 1.0f / 30.0f;
                Assert.That(
                    BitConverter.SingleToInt32Bits(actual.DeltaTime),
                    Is.EqualTo(BitConverter.SingleToInt32Bits(expectedDeltaTime)));
                Assert.That(actual.LivePhysicsBackendName, Is.EqualTo("mmd-anim-bullet-native"));
                CollectionAssert.AreEqual(expectedShapeTypes, actual.RigidbodyShapeTypes);
                Assert.That(actual.LastPhysicsDiagnostics.HasValue, Is.True);
                MmdPhysicsHostStepDiagnostics diagnostics = actual.LastPhysicsDiagnostics!.Value;
                Assert.That(diagnostics.reportPresent, Is.EqualTo(expectedDiagnostics.reportPresent));
                Assert.That(diagnostics.nativeRigidbodyCount, Is.EqualTo(expectedDiagnostics.nativeRigidbodyCount));
                Assert.That(diagnostics.nativeBoneCount, Is.EqualTo(expectedDiagnostics.nativeBoneCount));
                Assert.That(diagnostics.nativeSubstepCount, Is.EqualTo(expectedDiagnostics.nativeSubstepCount));
                Assert.That(diagnostics.nativeKinematicRigidbodiesFed, Is.EqualTo(expectedDiagnostics.nativeKinematicRigidbodiesFed));
                Assert.That(diagnostics.nativeBonesWrittenBack, Is.EqualTo(expectedDiagnostics.nativeBonesWrittenBack));
            }
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

            internal int LastFrame { get; private set; } = -1;

            internal float LastTime { get; private set; }

            internal float LastFrameRate { get; private set; }

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

            public void Evaluate(int frame, float time, float frameRate, MmdMultiCharacterWorkerResult result)
            {
                LastFrame = frame;
                LastTime = time;
                LastFrameRate = frameRate;
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
