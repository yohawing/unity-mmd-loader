#nullable enable

using System;
using Mmd.Native;
using Mmd.Motion;
using Mmd.Parser;
using Mmd.UnityIntegration;
using NUnit.Framework;

namespace Mmd.Tests
{
    [TestFixture]
    public sealed class MmdNativeHandleLifetimeStressContractTests
    {
        private const string GateEnvironmentVariable = "MMD_NATIVE_HANDLE_LIFETIME_STRESS_GATE";
        private const int BindingIterations = 8;
        private const int FastRuntimeReplacementCount = 3;
        private const int SceneTrackIterations = 6;
        private const int ExpectedSceneTrackFrameCount = 2;
        private const string ModelFixture = "test_1bone_cube.pmx";
        private const string MotionFixture = "test_1bone_cube_motion.vmd";

        [Test]
        [Category("Stress")]
        public void PackagedNativePlaybackHandlesSurviveBoundedReplacementAndCleanupStress()
        {
            RequireNativeHandleLifetimeStressGate();

            byte[] pmxBytes = MmdTestFixtures.ReadFixtureAssetBytes(ModelFixture);
            byte[] vmdBytes = MmdTestFixtures.ReadFixtureAssetBytes(MotionFixture);
            var parser = new NativeMmdParser();
            MmdModelDefinition model = parser.LoadModel(pmxBytes);
            MmdMotionDefinition motion = parser.LoadMotion(vmdBytes);

            // Keep this gate bounded: the small packaged one-bone fixture pair is exercised through eight
            // owned bindings, with three in-place fast-session replacements plus one managed-session native
            // evaluation per binding before the final fast-session replacement and cleanup.
            for (int iteration = 0; iteration < BindingIterations; iteration++)
            {
                MmdUnityPlaybackBinding? binding = null;
                Exception? primaryException = null;
                try
                {
                    binding = MmdUnityPlaybackBinding.CreateSkinned(
                        model,
                        motion,
                        "native-handle-stress-model-" + iteration,
                        "native-handle-stress-motion-" + iteration);
                    binding.SetPhysicsMode(Mmd.Physics.MmdPhysicsMode.Off);

                    AssertFastRuntimeEnabled(
                        binding,
                        pmxBytes,
                        vmdBytes,
                        "initial fast-runtime session at binding iteration " + iteration);
                    Assert.DoesNotThrow(
                        () => binding.ApplyFrame(0, 30.0f),
                        "initial native evaluation at binding iteration " + iteration);

                    for (int replacement = 0; replacement < FastRuntimeReplacementCount; replacement++)
                    {
                        AssertFastRuntimeEnabled(
                            binding,
                            pmxBytes,
                            vmdBytes,
                            "fast-runtime replacement " + replacement + " at binding iteration " + iteration);
                        Assert.DoesNotThrow(
                            () => binding.ApplyFrame(replacement + 1, 30.0f),
                            "replacement native evaluation " + replacement + " at binding iteration " + iteration);
                    }

                    Assert.DoesNotThrow(
                        binding.DisableFastRuntime,
                        "DisableFastRuntime at binding iteration " + iteration);
                    Assert.That(binding.IsFastRuntimeEnabled, Is.False);
                    Assert.DoesNotThrow(
                        binding.DisableFastRuntime,
                        "repeated DisableFastRuntime at binding iteration " + iteration);

                    // With fast runtime disabled, ApplyFrame lazily creates the managed session's native
                    // playback handle. The final binding disposal must release this handle and any later fast
                    // replacement without throwing.
                    Assert.DoesNotThrow(
                        () => binding.ApplyFrame(5, 30.0f),
                        "managed-session native evaluation after DisableFastRuntime at binding iteration " + iteration);
                    AssertFastRuntimeEnabled(
                        binding,
                        pmxBytes,
                        vmdBytes,
                        "final fast-runtime session at binding iteration " + iteration);
                }
                catch (Exception ex)
                {
                    primaryException = ex;
                    throw;
                }
                finally
                {
                    if (binding != null)
                    {
                        DisposeTwiceWithoutMaskingFailure(
                            binding,
                            "binding iteration " + iteration,
                            primaryException);
                    }
                }
            }
        }

        [Test]
        [Category("Stress")]
        public void PackagedNativeSceneTrackSamplersSurviveBoundedReplacementAndCleanupStress()
        {
            RequireNativeHandleLifetimeStressGate();

            byte[] sceneTrackBytes = MmdTestFixtures.BuildSceneTrackVmdBytes("native-handle-stress-scene");
            byte[] cameraTrackBytes = MmdTestFixtures.BuildCameraTrackVmdBytes("native_handle_camera");

            // Each iteration uses fresh source clones, while every track creates two samplers from the
            // same byte array. This covers source replacement and same-source sampler ownership without
            // turning the opt-in gate into an unbounded allocation test.
            for (int iteration = 0; iteration < SceneTrackIterations; iteration++)
            {
                byte[] sceneSource = (byte[])sceneTrackBytes.Clone();
                byte[] cameraSource = (byte[])cameraTrackBytes.Clone();
                string context = "scene-track iteration " + iteration;

                ExerciseTrackSamplers<NativeVmdLightTrackSampler, MmdLightState>(
                    sceneSource,
                    context + " light",
                    NativeVmdLightTrackSampler.TryCreate,
                    AssertLightSamplerSample);
                ExerciseTrackSamplers<NativeVmdSelfShadowTrackSampler, MmdSelfShadowState>(
                    sceneSource,
                    context + " self-shadow",
                    NativeVmdSelfShadowTrackSampler.TryCreate,
                    AssertSelfShadowSamplerSample);
                ExerciseTrackSamplers<NativeVmdCameraTrackSampler, MmdCameraState>(
                    cameraSource,
                    context + " camera",
                    NativeVmdCameraTrackSampler.TryCreate,
                    AssertCameraSamplerSample);
            }
        }

        private delegate bool TryCreateSampler<TSampler>(
            byte[] source,
            out TSampler? sampler,
            out string failureReason);

        private static void ExerciseTrackSamplers<TSampler, TState>(
            byte[] source,
            string context,
            TryCreateSampler<TSampler> tryCreate,
            Action<TSampler, string> assertSample)
            where TSampler : NativeVmdTrackSampler<TState>
        {
            TSampler? first = null;
            TSampler? second = null;
            Exception? primaryException = null;
            try
            {
                Assert.That(
                    tryCreate(source, out first, out string firstFailureReason),
                    Is.True,
                    context + " sampler creation failed: " + firstFailureReason);
                Assert.That(first, Is.Not.Null, context + " sampler is null after successful creation");

                Assert.That(
                    tryCreate(source, out second, out string secondFailureReason),
                    Is.True,
                    context + " second sampler creation failed: " + secondFailureReason);
                Assert.That(second, Is.Not.Null, context + " second sampler is null after successful creation");

                TSampler firstSampler = first!;
                TSampler secondSampler = second!;
                assertSample(firstSampler, context + " first sampler");
                assertSample(secondSampler, context + " second sampler");

                CloseSamplerAndAssertNoSample(
                    firstSampler,
                    () => firstSampler.TrySample(30.0f, out _),
                    context + " first sampler");
                first = null;
                CloseSamplerAndAssertNoSample(
                    secondSampler,
                    () => secondSampler.TrySample(30.0f, out _),
                    context + " second sampler");
                second = null;
            }
            catch (Exception exception)
            {
                primaryException = exception;
                throw;
            }
            finally
            {
                DisposeTwiceWithoutMaskingFailure(first, context + " first sampler", primaryException);
                DisposeTwiceWithoutMaskingFailure(second, context + " second sampler", primaryException);
            }
        }

        private static void AssertLightSamplerSample(NativeVmdLightTrackSampler sampler, string context)
        {
            Assert.That(sampler.FrameCount, Is.EqualTo(ExpectedSceneTrackFrameCount), context + " frame count");
            Assert.That(sampler.TrySample(30.0f, out MmdLightState state), Is.True, context + " sample");
            Assert.That(state.Color[0], Is.EqualTo(1.0f).Within(0.0001f), context + " color.r");
            Assert.That(state.Color[1], Is.EqualTo(0.5f).Within(0.0001f), context + " color.g");
            Assert.That(state.Direction[1], Is.EqualTo(-1.0f).Within(0.0001f), context + " direction.y");
        }

        private static void AssertSelfShadowSamplerSample(
            NativeVmdSelfShadowTrackSampler sampler,
            string context)
        {
            Assert.That(sampler.FrameCount, Is.EqualTo(ExpectedSceneTrackFrameCount), context + " frame count");
            Assert.That(sampler.TrySample(30.0f, out MmdSelfShadowState state), Is.True, context + " sample");
            Assert.That(state.Mode, Is.EqualTo((byte)2), context + " mode");
            Assert.That(state.Distance, Is.EqualTo(0.4f).Within(0.0001f), context + " distance");
        }

        private static void AssertCameraSamplerSample(NativeVmdCameraTrackSampler sampler, string context)
        {
            Assert.That(sampler.FrameCount, Is.EqualTo(ExpectedSceneTrackFrameCount), context + " frame count");
            Assert.That(sampler.TrySample(30.0f, out MmdCameraState state), Is.True, context + " sample");
            Assert.That(state.Distance, Is.EqualTo(-20.0f).Within(0.0001f), context + " distance");
            Assert.That(state.Position[0], Is.EqualTo(2.0f).Within(0.0001f), context + " position.x");
            Assert.That(state.Rotation[1], Is.EqualTo(0.2f).Within(0.0001f), context + " rotation.y");
            Assert.That(state.ViewAngle, Is.EqualTo(40.0f).Within(0.0001f), context + " view angle");
            Assert.That(state.Perspective, Is.False, context + " perspective");
        }

        private static void CloseSamplerAndAssertNoSample(
            IDisposable sampler,
            Func<bool> trySample,
            string context)
        {
            DisposeTwiceWithoutMaskingFailure(sampler, context, primaryException: null);
            Assert.That(trySample(), Is.False, context + " sample after Dispose");
        }

        private static void DisposeTwiceWithoutMaskingFailure(
            IDisposable? sampler,
            string context,
            Exception? primaryException)
        {
            if (sampler == null)
            {
                return;
            }

            Exception? cleanupFailure = null;
            try
            {
                sampler.Dispose();
            }
            catch (Exception exception)
            {
                cleanupFailure = exception;
            }

            try
            {
                sampler.Dispose();
            }
            catch (Exception exception)
            {
                cleanupFailure ??= exception;
            }

            if (cleanupFailure != null && primaryException == null)
            {
                Assert.Fail(context + " cleanup failed: " + cleanupFailure);
            }
        }

        private static void AssertFastRuntimeEnabled(
            MmdUnityPlaybackBinding binding,
            byte[] pmxBytes,
            byte[] vmdBytes,
            string context)
        {
            string reason = string.Empty;
            bool enabled = false;
            Assert.DoesNotThrow(
                () => enabled = binding.TryEnableFastRuntime(
                    (byte[])pmxBytes.Clone(),
                    (byte[])vmdBytes.Clone(),
                    out reason),
                context + " threw while creating or replacing the native session.");
            Assert.That(enabled, Is.True, context + " failed: " + reason);
            Assert.That(binding.IsFastRuntimeEnabled, Is.True, context + " did not retain a native session.");
        }

        private static void RequireNativeHandleLifetimeStressGate()
        {
            if (!string.Equals(
                    Environment.GetEnvironmentVariable(GateEnvironmentVariable),
                    "1",
                    StringComparison.Ordinal))
            {
                Assert.Ignore(
                    "Set " + GateEnvironmentVariable + "=1 to run the packaged native handle lifetime stress gate.");
            }

            IgnoreIfNativeRuntimeUnavailable();
        }

        private static void IgnoreIfNativeRuntimeUnavailable()
        {
#if !UNITY_EDITOR_WIN
            Assert.Fail("The packaged native handle lifetime gate is Windows Editor only.");
#else
            try
            {
                MmdRuntimeFfiMethods.ValidateAbiVersion();
            }
            catch (DllNotFoundException ex)
            {
                Assert.Fail("Packaged native runtime is unavailable: " + ex.Message);
            }
            catch (EntryPointNotFoundException ex)
            {
                Assert.Fail("Packaged native runtime ABI entry point is unavailable: " + ex.Message);
            }
            catch (BadImageFormatException ex)
            {
                Assert.Fail("Packaged native runtime binary is incompatible: " + ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                Assert.Fail("Packaged native runtime ABI is unsupported: " + ex.Message);
            }
#endif
        }
    }
}
