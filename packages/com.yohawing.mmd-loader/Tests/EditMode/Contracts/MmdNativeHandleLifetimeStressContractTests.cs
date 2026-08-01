#nullable enable

using System;
using Mmd.Native;
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
        private const string ModelFixture = "test_1bone_cube.pmx";
        private const string MotionFixture = "test_1bone_cube_motion.vmd";

        [Test]
        [Category("Stress")]
        public void PackagedNativePlaybackHandlesSurviveBoundedReplacementAndCleanupStress()
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
                        DisposeBindingWithoutMaskingFailure(binding, iteration, primaryException);
                    }
                }
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

        private static void DisposeBindingWithoutMaskingFailure(
            MmdUnityPlaybackBinding binding,
            int iteration,
            Exception? primaryException)
        {
            Exception? cleanupFailure = null;
            try
            {
                binding.Dispose();
            }
            catch (Exception ex)
            {
                cleanupFailure = ex;
            }

            try
            {
                binding.Dispose();
            }
            catch (Exception ex)
            {
                cleanupFailure ??= ex;
            }

            if (cleanupFailure != null && primaryException == null)
            {
                Assert.Fail(
                    "Binding cleanup failed at binding iteration " + iteration + ": " + cleanupFailure);
            }
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
