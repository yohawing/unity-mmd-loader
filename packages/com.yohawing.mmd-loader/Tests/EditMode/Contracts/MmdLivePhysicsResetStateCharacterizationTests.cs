#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using Mmd.Parser;
using Mmd.Physics;
using Mmd.UnityIntegration;
using NUnit.Framework;
using Object = UnityEngine.Object;

namespace Mmd.Tests.Contracts
{
    /// <summary>
    /// Safety net for consolidating the Live physics reset paths of <see cref="MmdUnityPlaybackBinding"/>.
    /// The expected values record the present behavior (golden); they are not a statement of the
    /// ideal behavior. Changing any expected cell requires an explicit reason.
    /// </summary>
    public sealed class MmdLivePhysicsResetStateCharacterizationTests
    {
        private const string PmxFileName = "test_hair_physics.pmx";
        private const string VmdFileName = "test_1bone_cube_motion.vmd";
        private const float FrameRate = 30.0f;
        private const string HumanoidFailClosedMessage =
            "Humanoid native host pose binding must be stepped through StepLivePhysicsFromHostPose.";

        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

        [Test]
        public void HumanoidNativeHostPoseBindingRejectsVmdLiveEntryPointsWithoutChangingState()
        {
            RequireBackend();
            MmdUnityPlaybackBinding? binding = null;
            try
            {
                binding = CreateBinding(enableFastRuntime: false);
                binding.LivePhysicsBodyDiagnosticsSampleInterval = 1;
                binding.SetPhysicsMode(MmdPhysicsMode.Live);
                Assert.That(
                    binding.TryEnableNativeHumanoidHostPose(new uint[] { 0u }, Array.Empty<uint>()),
                    Is.True,
                    binding.LastNativeHumanoidHostPoseFailureReason);
                Assert.That(binding.NativeHumanoidHostPoseEnabled, Is.True);

                MmdModelDefinition model = binding.ManagedModelDefinition;
                int boneCount = model.bones.Count;
                var rotations = new float[boneCount * 4];
                var scales = new float[boneCount * 3];
                for (int i = 0; i < boneCount; i++)
                {
                    rotations[i * 4 + 3] = 1.0f;
                    scales[i * 3] = 1.0f;
                    scales[i * 3 + 1] = 1.0f;
                    scales[i * 3 + 2] = 1.0f;
                }

                binding.StepLivePhysicsFromHostPose(
                    0,
                    0.0f,
                    true,
                    new float[boneCount * 3],
                    rotations,
                    scales,
                    new float[model.morphs.Count],
                    new byte[model.ik.Count],
                    hostPoseCaptureMs: null);
                MmdLivePhysicsFrameDiagnostics? diagnosticsBefore = binding.LastLivePhysicsDiagnostics;
                Assert.That(diagnosticsBefore, Is.Not.Null);
                Assert.That(diagnosticsBefore!.evaluationPath, Is.EqualTo("HumanoidNativeFinal"));
                Assert.That(diagnosticsBefore.hostPoseCapturePresent, Is.False,
                    "A host pose step without a caller-side capture measurement reports the phase as absent.");
                Assert.That(diagnosticsBefore.hostPoseCaptureMs, Is.Zero);

                // A non-negative forward frame makes a would-be backward scrub observable: if the guard ran
                // after the scrub detection, lastForwardPlaybackFrame would move to the requested frame.
                SetIntField(binding, "lastForwardPlaybackFrame", 7);
                int lastLiveFrameBefore = GetIntField(binding, "lastLiveFrame");
                object? afterMatricesBefore = GetField(binding, "livePhysicsAfterPhysicsWorldMatrices");
                Assert.That(afterMatricesBefore, Is.Not.Null);

                var applyFrame = Assert.Throws<InvalidOperationException>(() => binding.ApplyFrame(3, FrameRate));
                Assert.That(applyFrame!.Message, Is.EqualTo(HumanoidFailClosedMessage));
                AssertHumanoidStateUnchanged();

                var forward = Assert.Throws<InvalidOperationException>(
                    () => binding.ApplyLivePhysicsForwardFrame(3, FrameRate));
                Assert.That(forward!.Message, Is.EqualTo(HumanoidFailClosedMessage));
                AssertHumanoidStateUnchanged();

                void AssertHumanoidStateUnchanged()
                {
                    Assert.That(binding!.NativeHumanoidHostPoseEnabled, Is.True);
                    Assert.That(binding.HasLivePhysicsBackend, Is.True);
                    Assert.That(binding.PhysicsMode, Is.EqualTo(MmdPhysicsMode.Live));
                    Assert.That(GetIntField(binding, "lastForwardPlaybackFrame"), Is.EqualTo(7));
                    Assert.That(GetIntField(binding, "lastLiveFrame"), Is.EqualTo(lastLiveFrameBefore));
                    Assert.That(binding.LastLivePhysicsDiagnostics, Is.SameAs(diagnosticsBefore));
                    Assert.That(
                        GetField(binding, "livePhysicsAfterPhysicsWorldMatrices"),
                        Is.SameAs(afterMatricesBefore));
                }
            }
            finally
            {
                DestroyBinding(binding);
            }
        }

        [Test]
        public void LivePhysicsResetOperationsLeaveRecordedObservableState()
        {
            RequireBackend();

            // Every row starts from a fast-runtime Live binding stepped forward over frames 0..5, then runs
            // one operation, records the observable state, and finally steps forward to frame 6.
            var rows = new (string name, Action<MmdUnityPlaybackBinding> operation)[]
            {
                ("none", _ => { }),
                ("forward-same-frame-5", b => b.ApplyLivePhysicsForwardFrame(5, FrameRate)),
                ("set-mode-live-off-live", b =>
                {
                    b.SetPhysicsMode(MmdPhysicsMode.Off);
                    b.SetPhysicsMode(MmdPhysicsMode.Live);
                }),
                ("reset-live-physics", b => b.ResetLivePhysics()),
                ("reset-for-drive-source", b => b.ResetLivePhysicsForDriveSource()),
                ("timeline-preview-time-10", b => b.ApplyTimelinePreviewTime(10.0f / FrameRate, FrameRate)),
                ("backward-scrub-to-2", b => b.ApplyLivePhysicsForwardFrame(2, FrameRate)),
                ("soft-reset-simulation", b => InvokePrivate(b, "SoftResetLivePhysicsSimulation")),
                ("soft-reset-then-forward-3", b =>
                {
                    InvokePrivate(b, "SoftResetLivePhysicsSimulation");
                    b.ApplyLivePhysicsForwardFrame(3, FrameRate);
                }),
                ("timeline-detach", b =>
                {
                    MmdTimelineLivePhysicsTransfer? transfer = b.DetachTimelineLivePhysicsBackend();
                    Assert.That(transfer, Is.Not.Null, "detach must hand over the native backend");
                    transfer!.Dispose();
                }),
                ("timeline-detach-attach", b =>
                {
                    MmdTimelineLivePhysicsTransfer? transfer = b.DetachTimelineLivePhysicsBackend();
                    Assert.That(transfer, Is.Not.Null, "detach must hand over the native backend");
                    try
                    {
                        Assert.That(b.TryAttachTimelineLivePhysicsBackend(transfer!), Is.True);
                    }
                    finally
                    {
                        transfer!.Dispose();
                    }
                }),
            };

            // Columns: backend = HasLivePhysicsBackend; diag = LastLivePhysicsDiagnostics (null or its deltaTime);
            // reuse5 = CanReuseLivePhysicsSeed(5); live/forward = lastLiveFrame/lastForwardPlaybackFrame;
            // after/meta = after-physics matrix buffer and body metadata cache (kept = same instance as
            // before the operation); bodyDiag = body diagnostics length@frame; timing = LastDetailedApplyTiming;
            // next6 = the following ApplyLivePhysicsForwardFrame(6) (dt 0 means it was a seed step).
            var expected = new[]
            {
                "none | backend=True diag=dt=0.0333333351 reuse5=True live=5 forward=5 after=kept meta=kept bodyDiag=16@5 timing=set | next6 dt=0.0333333351 frame=6 forward=6",
                "forward-same-frame-5 | backend=True diag=dt=0.0333333351 reuse5=True live=5 forward=5 after=kept meta=kept bodyDiag=16@5 timing=set | next6 dt=0.0333333351 frame=6 forward=6",
                "set-mode-live-off-live | backend=False diag=null reuse5=False live=-1 forward=-1 after=null meta=kept bodyDiag=0@-1 timing=null | next6 dt=0 frame=6 forward=6",
                "reset-live-physics | backend=False diag=null reuse5=False live=-1 forward=-1 after=null meta=kept bodyDiag=0@-1 timing=null | next6 dt=0 frame=6 forward=6",
                "reset-for-drive-source | backend=True diag=null reuse5=False live=-1 forward=-1 after=kept meta=kept bodyDiag=0@-1 timing=null | next6 dt=0 frame=6 forward=6",
                "timeline-preview-time-10 | backend=True diag=null reuse5=False live=-1 forward=-1 after=kept meta=kept bodyDiag=0@-1 timing=null | next6 dt=0 frame=6 forward=6",
                "backward-scrub-to-2 | backend=True diag=dt=0 reuse5=False live=2 forward=2 after=kept meta=kept bodyDiag=16@2 timing=set | next6 dt=0.13333334 frame=6 forward=6",
                "soft-reset-simulation | backend=True diag=null reuse5=False live=-1 forward=5 after=kept meta=kept bodyDiag=0@-1 timing=null | next6 dt=0 frame=6 forward=6",
                "soft-reset-then-forward-3 | backend=True diag=dt=0 reuse5=False live=3 forward=3 after=kept meta=kept bodyDiag=16@3 timing=set | next6 dt=0.1 frame=6 forward=6",
                "timeline-detach | backend=False diag=null reuse5=False live=-1 forward=-1 after=null meta=null bodyDiag=0@-1 timing=null | next6 dt=0 frame=6 forward=6",
                "timeline-detach-attach | backend=True diag=null reuse5=False live=-1 forward=-1 after=null meta=null bodyDiag=0@-1 timing=null | next6 dt=0 frame=6 forward=6",
            };

            var actual = new List<string>();
            foreach ((string name, Action<MmdUnityPlaybackBinding> operation) in rows)
            {
                actual.Add(name + " | " + RunRow(operation));
            }

            Assert.That(
                string.Join("\n", actual),
                Is.EqualTo(string.Join("\n", expected)),
                "Live physics reset state table changed. Actual:\n" + string.Join("\n", actual));
        }

        private static string RunRow(Action<MmdUnityPlaybackBinding> operation)
        {
            MmdUnityPlaybackBinding? binding = null;
            try
            {
                binding = CreateBinding(enableFastRuntime: true);
                binding.LivePhysicsBodyDiagnosticsSampleInterval = 1;
                binding.DetailedApplyTimingEnabled = true;
                binding.SetPhysicsMode(MmdPhysicsMode.Live);
                for (int frame = 0; frame <= 5; frame++)
                {
                    binding.ApplyLivePhysicsForwardFrame(frame, FrameRate);
                }

                object? afterMatricesBefore = GetField(binding, "livePhysicsAfterPhysicsWorldMatrices");
                object? metadataBefore = GetField(binding, "livePhysicsBodyMetadata");
                Assert.That(afterMatricesBefore, Is.Not.Null);
                Assert.That(metadataBefore, Is.Not.Null);

                operation(binding);

                object? afterMatrices = GetField(binding, "livePhysicsAfterPhysicsWorldMatrices");
                object? metadata = GetField(binding, "livePhysicsBodyMetadata");
                var bodyDiagnostics = (Array)GetField(binding, "lastLivePhysicsBodyDiagnostics")!;
                string state = string.Format(
                    CultureInfo.InvariantCulture,
                    "backend={0} diag={1} reuse5={2} live={3} forward={4} after={5} meta={6} bodyDiag={7}@{8} timing={9}",
                    binding.HasLivePhysicsBackend,
                    binding.LastLivePhysicsDiagnostics == null
                        ? "null"
                        : "dt=" + binding.LastLivePhysicsDiagnostics.deltaTime.ToString("R", CultureInfo.InvariantCulture),
                    binding.CanReuseLivePhysicsSeed(5),
                    GetIntField(binding, "lastLiveFrame"),
                    GetIntField(binding, "lastForwardPlaybackFrame"),
                    DescribeReference(afterMatrices, afterMatricesBefore),
                    DescribeReference(metadata, metadataBefore),
                    bodyDiagnostics.Length,
                    GetIntField(binding, "lastLivePhysicsBodyDiagnosticsFrame"),
                    binding.LastDetailedApplyTiming == null ? "null" : "set");

                binding.ApplyLivePhysicsForwardFrame(6, FrameRate);
                MmdLivePhysicsFrameDiagnostics next = binding.LastLivePhysicsDiagnostics!;
                return state + string.Format(
                    CultureInfo.InvariantCulture,
                    " | next6 dt={0} frame={1} forward={2}",
                    next.deltaTime.ToString("R", CultureInfo.InvariantCulture),
                    next.frame,
                    GetIntField(binding, "lastForwardPlaybackFrame"));
            }
            finally
            {
                DestroyBinding(binding);
            }
        }

        private static string DescribeReference(object? current, object? before)
        {
            if (current == null)
            {
                return "null";
            }

            return ReferenceEquals(current, before) ? "kept" : "new";
        }

        private static MmdUnityPlaybackBinding CreateBinding(bool enableFastRuntime)
        {
            string pmxPath = MmdTestFixtures.FixtureAssetPath(PmxFileName);
            byte[] pmxBytes = File.ReadAllBytes(pmxPath);
            byte[] vmdBytes = File.ReadAllBytes(MmdTestFixtures.FixtureAssetPath(VmdFileName));
            var parser = new NativeMmdParser();
            MmdModelDefinition model = parser.LoadModel(pmxBytes);
            // Pure world-anchor joints (both endpoints -1) are rejected by the descriptor validator.
            model.physics.joints.RemoveAll(joint => joint.rigidbodyAIndex < 0 && joint.rigidbodyBIndex < 0);
            MmdMotionDefinition motion = parser.LoadMotion(vmdBytes);
            MmdUnityPlaybackBinding binding = MmdUnityPlaybackBinding.CreateSkinned(
                model,
                motion,
                PmxFileName,
                VmdFileName,
                pmxPath);
            if (enableFastRuntime)
            {
                bool enabled = binding.TryEnableFastRuntime(pmxBytes, vmdBytes, out string reason);
                if (!enabled)
                {
                    DestroyBinding(binding);
                }

                Assert.That(enabled, Is.True, reason);
            }

            return binding;
        }

        private static void RequireBackend()
        {
            MmdPhysicsBackendAvailability availability = MmdAnimPhysicsBackend.ProbeAvailability();
            Assert.That(availability.backendAvailable, Is.True,
                "Bullet physics backend is required: " + availability.unsupportedReason);
        }

        private static object? GetField(MmdUnityPlaybackBinding binding, string name)
        {
            FieldInfo field = typeof(MmdUnityPlaybackBinding).GetField(name, PrivateInstance)
                ?? throw new MissingFieldException(nameof(MmdUnityPlaybackBinding), name);
            return field.GetValue(binding);
        }

        private static int GetIntField(MmdUnityPlaybackBinding binding, string name)
        {
            return (int)GetField(binding, name)!;
        }

        private static void SetIntField(MmdUnityPlaybackBinding binding, string name, int value)
        {
            FieldInfo field = typeof(MmdUnityPlaybackBinding).GetField(name, PrivateInstance)
                ?? throw new MissingFieldException(nameof(MmdUnityPlaybackBinding), name);
            field.SetValue(binding, value);
        }

        private static void InvokePrivate(MmdUnityPlaybackBinding binding, string name)
        {
            MethodInfo method = typeof(MmdUnityPlaybackBinding).GetMethod(name, PrivateInstance)
                ?? throw new MissingMethodException(nameof(MmdUnityPlaybackBinding), name);
            method.Invoke(binding, null);
        }

        private static void DestroyBinding(MmdUnityPlaybackBinding? binding)
        {
            if (binding?.Instance?.Root != null)
            {
                Object.DestroyImmediate(binding.Instance.Root);
            }

            binding?.Dispose();
        }
    }
}
