#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Yohawing.MmdUnity.Parser;
using Yohawing.MmdUnity.Physics;
using Yohawing.MmdUnity.Timeline;
using Yohawing.MmdUnity.UnityIntegration;
using Object = UnityEngine.Object;

namespace Yohawing.MmdUnity.Tests
{
    /// <summary>
    /// Regression coverage for the reported "after a scrub/seek, only the physics-pinned
    /// (揺れもの) bones stay frozen at the bind pose while the rest of the body keeps
    /// animating" bug. The existing Timeline hair test only seeds at frame 0 and never
    /// re-seeds, so it cannot catch a simulation that dies after a backward seek or after
    /// the controller's own forward Tick fights the Timeline clock.
    ///
    /// These tests use the real hair physics fixture with a rest-pose motion: gravity is
    /// the live-physics signal. After a reset+reseed the hair must sag again; if it stays
    /// pinned at the seeded (bind-relative) pose, the simulation has frozen.
    /// </summary>
    public sealed class MmdLivePhysicsScrubResumeRegressionTests
    {
        [Test]
        public void TimelineScrubSeekResumeKeepsHairPhysicsLive()
        {
            MmdPhysicsBackendAvailability availability = BulletMmdPhysicsBackend.ProbeAvailability();
            if (!availability.backendAvailable)
            {
                Assert.Ignore("Bullet physics backend is not available: " + availability.unsupportedReason);
            }

            MmdUnityPlaybackBinding? binding = null;
            try
            {
                // A moving mode-1 chain: the hair body's position is physics-driven, so if the live
                // simulation freezes after a scrub the hair stays at the post-seek (bind) pose while the
                // body animates away. test_hair_physics.pmx cannot be used here because its hair bind pose
                // already equals the gravity equilibrium, so it shows no measurable movement.
                MmdModelDefinition model = CreateMode1ChainModel();
                MmdMotionDefinition motion = CreateBoneTranslationMotion(model, "root", frames: 30, endTranslationX: 40.0f);
                binding = MmdUnityPlaybackBinding.CreateSkinned(model, motion, "mode1-chain.pmx", "translate-root");
                MmdUnityPlaybackController controller = binding.Instance.Root.AddComponent<MmdUnityPlaybackController>();
                controller.Configure(binding, 30.0f, playOnStart: false);
                controller.SetPhysicsMode(MmdPhysicsMode.Live);
                var behaviour = new MmdVmdTimelineBehaviour { FrameRate = 30.0f };

                // Forward play: the body translates and the hair follows.
                for (int frame = 0; frame <= 20; frame++)
                {
                    behaviour.EvaluateAtLocalTime(controller, frame / 30.0, runLivePhysics: true);
                }

                // Scrub/seek BACKWARD toward the start (model near the origin). In Play Mode this is a
                // runLivePhysics:true evaluation; it must place the rig at the bone-driven pose without
                // stepping physics.
                behaviour.EvaluateAtLocalTime(controller, 2 / 30.0, runLivePhysics: true);
                Vector3 anchorAtScrub = binding.Instance.BoneTransforms[0].position;
                Vector3 hairAtScrub = binding.Instance.BoneTransforms[1].position;

                // Resume forward: the body translates back out and the hair must follow it (re-seeded and
                // simulating), not stay frozen at the scrub/bind pose.
                for (int frame = 3; frame <= 22; frame++)
                {
                    behaviour.EvaluateAtLocalTime(controller, frame / 30.0, runLivePhysics: true);
                }

                float anchorMove = (binding.Instance.BoneTransforms[0].position - anchorAtScrub).magnitude;
                float hairMove = (binding.Instance.BoneTransforms[1].position - hairAtScrub).magnitude;
                Assert.That(anchorMove, Is.GreaterThan(5.0f),
                    "Sanity: the body must translate during the resume window.");
                Assert.That(
                    hairMove,
                    Is.GreaterThanOrEqualTo(anchorMove * 0.5f),
                    $"After a scrub/seek the hair physics froze: the body moved {anchorMove} on resume but the " +
                    $"hair moved only {hairMove}. The 揺れもの bone is pinned at the bind pose instead of following.");
            }
            finally
            {
                DestroyBinding(binding);
            }
        }

        [Test]
        public void TimelineScrubBackwardReSeedsPhysicsAsSettleNotForwardStep()
        {
            // A backward scrub must not FORWARD-integrate physics (which explodes the chain). Instead it
            // resets and re-seeds the simulation, easing the 揺れもの into the scrubbed pose (saba
            // SyncPhysics) — a settle with deltaTime 0, not a forward step. This shows a physics-plausible
            // pose during the scrub (no snap to the straight bind pose) and re-seeds for resume.
            MmdPhysicsBackendAvailability availability = BulletMmdPhysicsBackend.ProbeAvailability();
            if (!availability.backendAvailable)
            {
                Assert.Ignore("Bullet physics backend is not available: " + availability.unsupportedReason);
            }

            MmdUnityPlaybackBinding? binding = null;
            try
            {
                MmdModelDefinition model = LoadHairPhysicsModel(out string pmxPath);
                binding = MmdUnityPlaybackBinding.CreateSkinned(
                    model, CreateRestPoseMotion(model), "test_hair_physics.pmx", "rest-pose", pmxPath);
                MmdUnityPlaybackController controller = binding.Instance.Root.AddComponent<MmdUnityPlaybackController>();
                controller.Configure(binding, 30.0f, playOnStart: false);
                controller.SetPhysicsMode(MmdPhysicsMode.Live);
                var behaviour = new MmdVmdTimelineBehaviour { FrameRate = 30.0f };

                // Forward play forward-steps physics (deltaTime > 0).
                for (int frame = 0; frame <= 10; frame++)
                {
                    behaviour.EvaluateAtLocalTime(controller, frame / 30.0, runLivePhysics: true);
                }
                Assert.That(binding.LastLivePhysicsDiagnostics, Is.Not.Null, "Forward play must run live physics.");
                Assert.That(binding.LastLivePhysicsDiagnostics!.deltaTime, Is.GreaterThan(0.0f),
                    "Steady forward play must forward-integrate physics (deltaTime > 0).");

                // Backward scrub re-seeds as a settle: physics is evaluated (diagnostics present) but with
                // deltaTime 0 (no forward integration).
                behaviour.EvaluateAtLocalTime(controller, 3 / 30.0, runLivePhysics: true);
                Assert.That(controller.CurrentFrame, Is.EqualTo(3));
                Assert.That(binding.LastLivePhysicsDiagnostics, Is.Not.Null,
                    "A backward scrub must re-seed the simulation (settle), not leave it cleared.");
                Assert.That(binding.LastLivePhysicsDiagnostics!.deltaTime, Is.EqualTo(0.0f),
                    "A backward scrub must settle (deltaTime 0), never forward-integrate physics.");

                // Resuming forward continues forward stepping from the scrub-settled state (deltaTime > 0).
                behaviour.EvaluateAtLocalTime(controller, 4 / 30.0, runLivePhysics: true);
                Assert.That(binding.LastLivePhysicsDiagnostics!.deltaTime, Is.GreaterThan(0.0f),
                    "Resuming forward after a scrub must forward-step from the settled state.");
            }
            finally
            {
                DestroyBinding(binding);
            }
        }

        [Test]
        public void TimelineLoopWrapToStartResetsPhysicsToInitialPlayState()
        {
            // When looping playback wraps from the end back to the start, the live simulation must restart
            // from the same clean state as the very first play (zero carried inertia / contacts), not carry
            // the disturbed end-of-loop pose into the next loop. A loop wrap is a backward time jump, so it
            // routes through the same backward-scrub reset path: soft reset (zero velocity, clean contact
            // pairs) + ease-in re-seed at frame 0 (saba SyncPhysics). This pins that loop restart == first play.
            MmdPhysicsBackendAvailability availability = BulletMmdPhysicsBackend.ProbeAvailability();
            if (!availability.backendAvailable)
            {
                Assert.Ignore("Bullet physics backend is not available: " + availability.unsupportedReason);
            }

            MmdUnityPlaybackBinding? binding = null;
            try
            {
                MmdModelDefinition model = CreateMode1ChainModel();
                MmdMotionDefinition motion = CreateBoneTranslationMotion(model, "root", frames: 30, endTranslationX: 40.0f);
                binding = MmdUnityPlaybackBinding.CreateSkinned(model, motion, "mode1-chain.pmx", "translate-root");
                MmdUnityPlaybackController controller = binding.Instance.Root.AddComponent<MmdUnityPlaybackController>();
                controller.Configure(binding, 30.0f, playOnStart: false);
                controller.SetPhysicsMode(MmdPhysicsMode.Live);
                var behaviour = new MmdVmdTimelineBehaviour { FrameRate = 30.0f };

                // First play, frame 0: seed the simulation from the initial pose.
                behaviour.EvaluateAtLocalTime(controller, 0.0, runLivePhysics: true);
                Vector3 hairAtFirstStart = binding.Instance.BoneTransforms[1].position;

                // Play forward to the end of the loop; the body translates and the hair is disturbed.
                for (int frame = 1; frame <= 30; frame++)
                {
                    behaviour.EvaluateAtLocalTime(controller, frame / 30.0, runLivePhysics: true);
                }
                Vector3 hairAtLoopEnd = binding.Instance.BoneTransforms[1].position;
                Assert.That((hairAtLoopEnd - hairAtFirstStart).magnitude, Is.GreaterThan(5.0f),
                    "Sanity: the hair must be displaced by the end of the loop (motion + physics).");

                // Loop wrap: time jumps back to the start. This must reset the simulation (settle, deltaTime 0)
                // and place the hair back at the initial-play position, not leave it at the disturbed end pose.
                behaviour.EvaluateAtLocalTime(controller, 0.0, runLivePhysics: true);
                Assert.That(controller.CurrentFrame, Is.EqualTo(0));
                Assert.That(binding.LastLivePhysicsDiagnostics, Is.Not.Null,
                    "Loop wrap must re-seed the simulation, not leave it cleared.");
                Assert.That(binding.LastLivePhysicsDiagnostics!.deltaTime, Is.EqualTo(0.0f),
                    "Loop wrap must reset+settle (deltaTime 0), never forward-integrate physics across the wrap.");

                Vector3 hairAtLoopRestart = binding.Instance.BoneTransforms[1].position;
                Assert.That(
                    (hairAtLoopRestart - hairAtFirstStart).magnitude,
                    Is.LessThan(0.5f),
                    $"Loop wrap did not reset the hair to the initial play state: first start {hairAtFirstStart}, " +
                    $"loop restart {hairAtLoopRestart}. The physics carried inertia/pose across the loop instead of resetting.");
            }
            finally
            {
                DestroyBinding(binding);
            }
        }

        [Test]
        public void PhysicsResetWhileModelTranslatedPlacesDynamicBodyAtCurrentPoseNotOriginBind()
        {
            // Root-cause regression. On a reset (backward scrub / loop wrap / first seeded frame) the live
            // physics returned EVERY body to its origin-space descriptor (bind) transform instead of the
            // CURRENT animated bone pose, so a pure-dynamic (mode-1) 揺れ body snapped back toward the
            // origin-space bind while the model had translated away — the reported "揺れ骨が BindPose の場所に
            // 戻る" bug. saba PMXModel::ResetPhysics instead re-syncs every body to its current node pose.
            //
            // This MUST be checked with the reset happening while the model is translated FAR from the origin:
            // at the origin, origin-bind == current-pose and the bug is invisible (that is why the loop/scrub
            // tests that reset near the origin cannot catch it).
            //
            // The joint's linear DOFs are FREE (lower > upper => unlimited in btGeneric6DofSpring2Constraint),
            // so the joint does NOT drag the body toward the anchor — a rigid (linear-locked) joint would mask
            // the bug by hauling the origin-bind body home over the settle. With free linear DOFs gravity acts
            // only vertically, so the body's HORIZONTAL position is exactly where the RESET placed it: at the
            // origin-bind (pre-fix) or under the translated anchor (post-fix).
            MmdPhysicsBackendAvailability availability = BulletMmdPhysicsBackend.ProbeAvailability();
            if (!availability.backendAvailable)
            {
                Assert.Ignore("Bullet physics backend is not available: " + availability.unsupportedReason);
            }

            MmdUnityPlaybackBinding? binding = null;
            try
            {
                MmdModelDefinition model = CreateMode1ChainModelWithFreeLinearJoint();
                MmdMotionDefinition motion = CreateBoneTranslationMotion(model, "root", frames: 30, endTranslationX: 40.0f);
                binding = MmdUnityPlaybackBinding.CreateSkinned(model, motion, "mode1-chain-free.pmx", "translate-root");
                MmdUnityPlaybackController controller = binding.Instance.Root.AddComponent<MmdUnityPlaybackController>();
                controller.Configure(binding, 30.0f, playOnStart: false);
                controller.SetPhysicsMode(MmdPhysicsMode.Live);
                var behaviour = new MmdVmdTimelineBehaviour { FrameRate = 30.0f };

                // Forward play out to where the model is translated far from the origin.
                for (int frame = 0; frame <= 20; frame++)
                {
                    behaviour.EvaluateAtLocalTime(controller, frame / 30.0, runLivePhysics: true);
                }

                // Backward scrub: triggers the reset + re-seed while the model is still translated away.
                behaviour.EvaluateAtLocalTime(controller, 18 / 30.0, runLivePhysics: true);

                Vector3 anchor = binding.Instance.BoneTransforms[0].position; // bone-follow (static) anchor, at current pose
                Vector3 hair = binding.Instance.BoneTransforms[1].position;   // mode-1 dynamic body bone
                var anchorHoriz = new Vector2(anchor.x, anchor.z);
                var hairHoriz = new Vector2(hair.x, hair.z);
                Vector2 originHoriz = Vector2.zero;

                // Sanity: the model really did translate away from the origin (the chain is vertical, so the
                // anchor's horizontal separation from the origin is purely the root translation).
                Assert.That(Vector2.Distance(anchorHoriz, originHoriz), Is.GreaterThan(2.0f),
                    "Sanity: the anchor must be translated away from the origin at the scrubbed frame.");

                // The reset must place the dynamic body at the CURRENT pose (under the translated anchor), NOT
                // back at the origin-space bind. Horizontal position is immune to the vertical gravity settle,
                // so this isolates the reset placement. Pre-fix the body snaps to the origin-bind (hair
                // horizontal ~ origin, far from the translated anchor) and this fails.
                Assert.That(
                    Vector2.Distance(hairHoriz, anchorHoriz),
                    Is.LessThan(Vector2.Distance(hairHoriz, originHoriz)),
                    $"After a reset with the model translated away, the mode-1 hair body snapped back toward the " +
                    $"origin-space bind instead of staying at the current pose: anchor {anchorHoriz}, hair {hairHoriz}. " +
                    "Reset must re-sync bodies to the current bone pose (saba PMXModel::ResetPhysics).");
            }
            finally
            {
                DestroyBinding(binding);
            }
        }

        [Test]
        public void SeededWhileTranslatedFirstForwardStepDoesNotExplodeMode1Body()
        {
            // Root-cause regression for the FAST/active live-physics seed path. When the simulation is
            // (re-)seeded while the model is translated FAR from the origin, the seed teleports every body
            // to the current pose via SetRigidbodyTransform — which sets the world + motion-state transform
            // but NOT the interpolation transform. The prior native Reset() had left the interpolation
            // transform at the ORIGIN-bind. So on the FIRST forward Step, Bullet's saveKinematicState computes
            // the kinematic (bone-following) anchor body's velocity as (currentPose - originBind)/dt — a huge
            // spurious velocity that is imparted through the 6-DoF joint into the pure-dynamic (mode-1) hair
            // body and flings it apart (the "揺れ骨が BindPose に残って崩れる" explosion).
            //
            // The fix re-aligns the native interpolation transform with the current pose and zeroes velocity
            // at seed time (saba PMXModel::ResetPhysics -> MMDRigidBody::Reset), so the first forward step
            // computes no spurious kinematic velocity. This test seeds while translated, then takes a SINGLE
            // forward step and asserts the mode-1 body does not jump far beyond the anchor's own per-frame
            // motion. Pre-fix the spurious kinematic velocity flings the hair body far and this FAILS; post-fix
            // the hair tracks the anchor and it PASSES.
            MmdPhysicsBackendAvailability availability = BulletMmdPhysicsBackend.ProbeAvailability();
            if (!availability.backendAvailable)
            {
                Assert.Ignore("Bullet physics backend is not available: " + availability.unsupportedReason);
            }

            MmdUnityPlaybackBinding? binding = null;
            try
            {
                MmdModelDefinition model = CreateMode1ChainModel();
                MmdMotionDefinition motion = CreateBoneTranslationMotion(model, "root", frames: 30, endTranslationX: 40.0f);
                binding = MmdUnityPlaybackBinding.CreateSkinned(model, motion, "mode1-chain.pmx", "translate-root");
                MmdUnityPlaybackController controller = binding.Instance.Root.AddComponent<MmdUnityPlaybackController>();
                controller.Configure(binding, 30.0f, playOnStart: false);
                controller.SetPhysicsMode(MmdPhysicsMode.Live);
                var behaviour = new MmdVmdTimelineBehaviour { FrameRate = 30.0f };

                // Forward play out to where the model is translated far from the origin.
                for (int frame = 0; frame <= 20; frame++)
                {
                    behaviour.EvaluateAtLocalTime(controller, frame / 30.0, runLivePhysics: true);
                }

                // Backward scrub to frame 18 (still translated): this soft-resets and RE-SEEDS the simulation
                // while the model is far from the origin. The re-seed is a settle (deltaTime 0).
                behaviour.EvaluateAtLocalTime(controller, 18 / 30.0, runLivePhysics: true);
                Assert.That(binding.LastLivePhysicsDiagnostics, Is.Not.Null, "The backward scrub must re-seed the simulation.");
                Assert.That(binding.LastLivePhysicsDiagnostics!.deltaTime, Is.EqualTo(0.0f),
                    "The re-seed must be a settle (deltaTime 0), not a forward integration.");

                Vector3 anchorAfterSeed = binding.Instance.BoneTransforms[0].position;
                Vector3 hairAfterSeed = binding.Instance.BoneTransforms[1].position;

                // Sanity: the model is genuinely translated away from the origin at the seed frame, so the
                // origin-bind interpolation transform (pre-fix) is far from the current pose.
                Assert.That(anchorAfterSeed.magnitude, Is.GreaterThan(10.0f),
                    "Sanity: the anchor must be translated far from the origin at the re-seed frame.");

                // ONE forward step (frame 18 -> 19). This is the first forward Step after the re-seed, where the
                // spurious kinematic velocity (if any) is realized.
                behaviour.EvaluateAtLocalTime(controller, 19 / 30.0, runLivePhysics: true);
                Assert.That(binding.LastLivePhysicsDiagnostics!.deltaTime, Is.GreaterThan(0.0f),
                    "The frame after the re-seed must forward-step physics (deltaTime > 0).");

                Vector3 anchorAfterStep = binding.Instance.BoneTransforms[0].position;
                Vector3 hairAfterStep = binding.Instance.BoneTransforms[1].position;
                float anchorStepMove = (anchorAfterStep - anchorAfterSeed).magnitude;
                float hairStepMove = (hairAfterStep - hairAfterSeed).magnitude;

                // The anchor (bone-driven) moves by exactly its per-frame motion translation (~40/30 ≈ 1.33).
                // A non-exploding mode-1 body tracks that plus a little gravity sag — bounded to a few units.
                // The spurious (currentPose - originBind)/dt velocity (pre-fix) is on the order of the whole
                // translation distance per second and flings the hair body many times farther in one step.
                Assert.That(anchorStepMove, Is.GreaterThan(0.5f).And.LessThan(5.0f),
                    "Sanity: the anchor must advance by roughly its per-frame motion translation on the forward step.");
                Assert.That(
                    hairStepMove,
                    Is.LessThan(anchorStepMove + 3.0f),
                    $"The first forward Step after a translated re-seed flung the mode-1 hair body: anchor moved " +
                    $"{anchorStepMove} but the hair jumped {hairStepMove}. The seed left the native interpolation " +
                    $"transform at the origin-bind, so saveKinematicState computed a spurious kinematic velocity " +
                    $"(currentPose - originBind)/dt that exploded the chain. The seed must re-sync interpolation " +
                    $"and zero velocity (saba PMXModel::ResetPhysics).");
            }
            finally
            {
                DestroyBinding(binding);
            }
        }

        [Test]
        public void TimelineDriveSuppressesControllerSelfTickToAvoidDoubleDriving()
        {
            // Guard logic: Update() runs before the PlayableDirector's ProcessFrame, so a Timeline-
            // driven controller sees the previous frame's drive (delta == 1) and must skip self-Tick.
            const int driven = 1000;
            Assert.That(MmdUnityPlaybackController.ShouldSuppressSelfTick(driven, driven), Is.True);
            Assert.That(MmdUnityPlaybackController.ShouldSuppressSelfTick(driven, driven + 1), Is.True);
            Assert.That(MmdUnityPlaybackController.ShouldSuppressSelfTick(driven, driven + 2), Is.False,
                "Once the Timeline stops driving, the controller resumes self-Tick (standalone playback).");
            Assert.That(MmdUnityPlaybackController.ShouldSuppressSelfTick(int.MinValue / 2, driven), Is.False,
                "A controller never driven by a Timeline must self-Tick (standalone playback).");

            // A Timeline evaluation records the current Unity frame so Update() can suppress its Tick.
            MmdUnityPlaybackBinding? binding = null;
            try
            {
                MmdModelDefinition model = LoadHairPhysicsModel(out string pmxPath);
                binding = MmdUnityPlaybackBinding.CreateSkinned(
                    model, CreateRestPoseMotion(model), "test_hair_physics.pmx", "rest-pose", pmxPath);
                MmdUnityPlaybackController controller = binding.Instance.Root.AddComponent<MmdUnityPlaybackController>();
                controller.Configure(binding, 30.0f, playOnStart: false);
                controller.SetPhysicsMode(MmdPhysicsMode.Live);
                var behaviour = new MmdVmdTimelineBehaviour { FrameRate = 30.0f };

                behaviour.EvaluateAtLocalTime(controller, 5 / 30.0, runLivePhysics: true);
                Assert.That(controller.LastTimelineDriveFrameCount, Is.EqualTo(Time.frameCount),
                    "Forward Timeline evaluation must record the drive frame so Update() suppresses self-Tick.");

                behaviour.EvaluateAtLocalTime(controller, 6 / 30.0, runLivePhysics: false);
                Assert.That(controller.LastTimelineDriveFrameCount, Is.EqualTo(Time.frameCount),
                    "Animation-only (scrub) Timeline evaluation must also record the drive frame.");
            }
            finally
            {
                DestroyBinding(binding);
            }
        }

        [Test]
        public void ForwardPlaybackMode1ChainFollowsMovingModelInsteadOfStayingAtBindPose()
        {
            // The core reported bug: while the body animates, the 揺れもの (physics) bones stay frozen
            // at the bind pose instead of following the moving body, which then tears the mesh apart.
            // A synthetic PMX mode-1 (pure dynamic) hair chain is used because the position of a mode-1
            // body comes entirely from the Bullet simulation in model space — if it is not dragged along
            // with the model, its bone stays at the world-space bind position while the body moves away.
            MmdPhysicsBackendAvailability availability = BulletMmdPhysicsBackend.ProbeAvailability();
            if (!availability.backendAvailable)
            {
                Assert.Ignore("Bullet physics backend is not available: " + availability.unsupportedReason);
            }

            MmdUnityPlaybackBinding? binding = null;
            try
            {
                MmdModelDefinition model = CreateMode1ChainModel();
                MmdMotionDefinition motion = CreateBoneTranslationMotion(model, "root", frames: 30, endTranslationX: 40.0f);
                binding = MmdUnityPlaybackBinding.CreateSkinned(model, motion, "mode1-chain.pmx", "translate-root");
                MmdUnityPlaybackController controller = binding.Instance.Root.AddComponent<MmdUnityPlaybackController>();
                controller.Configure(binding, 30.0f, playOnStart: false);
                controller.SetPhysicsMode(MmdPhysicsMode.Live);
                var behaviour = new MmdVmdTimelineBehaviour { FrameRate = 30.0f };

                behaviour.EvaluateAtLocalTime(controller, 0.0, runLivePhysics: true);
                Vector3 anchorStart = binding.Instance.BoneTransforms[0].position;
                Vector3 hairStart = binding.Instance.BoneTransforms[1].position;

                for (int frame = 1; frame <= 30; frame++)
                {
                    behaviour.EvaluateAtLocalTime(controller, frame / 30.0, runLivePhysics: true);
                }

                float anchorDisplacement = (binding.Instance.BoneTransforms[0].position - anchorStart).magnitude;
                float hairDisplacement = (binding.Instance.BoneTransforms[1].position - hairStart).magnitude;

                Assert.That(anchorDisplacement, Is.GreaterThan(5.0f),
                    "Sanity: the bone-follow anchor must move with the translating model.");
                Assert.That(
                    hairDisplacement,
                    Is.GreaterThanOrEqualTo(anchorDisplacement * 0.5f),
                    $"The mode-1 hair bone stayed near the bind pose while the body moved: anchor moved " +
                    $"{anchorDisplacement} but hair moved only {hairDisplacement}. Live physics is not following " +
                    "the model (the reported '揺れ骨がBindPoseに残る' bug).");
            }
            finally
            {
                DestroyBinding(binding);
            }
        }

        [Test]
        public void ForwardPlaybackWithMovingModelKeepsHairAttachedToBody()
        {
            MmdPhysicsBackendAvailability availability = BulletMmdPhysicsBackend.ProbeAvailability();
            if (!availability.backendAvailable)
            {
                Assert.Ignore("Bullet physics backend is not available: " + availability.unsupportedReason);
            }

            MmdUnityPlaybackBinding? binding = null;
            try
            {
                MmdModelDefinition model = LoadHairPhysicsModel(out string pmxPath);

                // Anchor = a bone driven by a bone-follow (static) body; it moves rigidly with the
                // animated skeleton. Hair = a bone driven by a PMX mode-1 (pure "dynamic") body whose
                // position comes entirely from the Bullet simulation (model space). If the simulation
                // does not get dragged along when the body translates, the mode-1 hair bone stays at
                // its model-space bind position and visibly lags/freezes while the body moves away.
                int anchorBoneIndex = FirstBodyBoneIndex(model, binding: null, kind => string.Equals(kind, "static", StringComparison.Ordinal));
                int hairBoneIndex = FirstBodyBoneIndex(model, binding: null, kind => string.Equals(kind, "dynamic", StringComparison.Ordinal));
                Assert.That(anchorBoneIndex, Is.GreaterThanOrEqualTo(0), "test_hair_physics.pmx must have a static (bone-follow) body");
                if (hairBoneIndex < 0)
                {
                    // This fixture only has static + PMX mode-2 (dynamic-orientation) bodies, whose
                    // bone position is pinned to the (hierarchically animated) bone every frame, so a
                    // world-position follow check cannot distinguish a frozen simulation. The position-
                    // driven mode-1 drag is covered at the backend layer by
                    // BulletMmdPhysicsBackendCollisionTests.KinematicAnchorDragsJointedDynamicBodyWhenTranslated.
                    Assert.Inconclusive("test_hair_physics.pmx has no PMX mode-1 (pure dynamic) body to measure world-position follow.");
                }

                string rootBoneName = model.bones[0].name;
                MmdMotionDefinition motion = CreateBoneTranslationMotion(model, rootBoneName, frames: 30, endTranslationX: 30.0f);

                binding = MmdUnityPlaybackBinding.CreateSkinned(
                    model, motion, "test_hair_physics.pmx", "translate-root", pmxPath);
                MmdUnityPlaybackController controller = binding.Instance.Root.AddComponent<MmdUnityPlaybackController>();
                controller.Configure(binding, 30.0f, playOnStart: false);
                controller.SetPhysicsMode(MmdPhysicsMode.Live);
                var behaviour = new MmdVmdTimelineBehaviour { FrameRate = 30.0f };

                Assert.That(anchorBoneIndex, Is.LessThan(binding.Instance.BoneTransforms.Length));
                Assert.That(hairBoneIndex, Is.LessThan(binding.Instance.BoneTransforms.Length));

                behaviour.EvaluateAtLocalTime(controller, 0.0, runLivePhysics: true);
                Vector3 anchorStart = binding.Instance.BoneTransforms[anchorBoneIndex].position;
                Vector3 hairStart = binding.Instance.BoneTransforms[hairBoneIndex].position;

                for (int frame = 1; frame <= 30; frame++)
                {
                    behaviour.EvaluateAtLocalTime(controller, frame / 30.0, runLivePhysics: true);
                }

                Vector3 anchorEnd = binding.Instance.BoneTransforms[anchorBoneIndex].position;
                Vector3 hairEnd = binding.Instance.BoneTransforms[hairBoneIndex].position;
                float anchorDisplacement = (anchorEnd - anchorStart).magnitude;
                float hairDisplacement = (hairEnd - hairStart).magnitude;

                if (anchorDisplacement < 1.0f)
                {
                    Assert.Inconclusive(
                        $"The model did not translate (anchor bone moved only {anchorDisplacement}); bone 0 " +
                        $"'{rootBoneName}' may not be movable, so the moving-body follow cannot be measured.");
                }

                Assert.That(
                    hairDisplacement,
                    Is.GreaterThanOrEqualTo(anchorDisplacement * 0.5f),
                    $"The mode-1 hair bone did not follow the moving body: the anchor moved {anchorDisplacement} " +
                    $"but the hair moved only {hairDisplacement}. The 揺れもの is frozen near the bind pose while " +
                    "the body animates away (live physics is not being dragged along with the model).");
            }
            finally
            {
                DestroyBinding(binding);
            }
        }

        private static int FirstBodyBoneIndex(MmdModelDefinition model, MmdUnityPlaybackBinding? binding, Func<string, bool> kindMatches)
        {
            foreach (MmdRigidbodyDefinition body in model.physics.rigidbodies)
            {
                if (body.boneIndex < 0)
                {
                    continue;
                }

                if (binding != null && body.boneIndex >= binding.Instance.BoneTransforms.Length)
                {
                    continue;
                }

                if (kindMatches(body.physicsKind ?? string.Empty))
                {
                    return body.boneIndex;
                }
            }

            return -1;
        }

        private static MmdMotionDefinition CreateBoneTranslationMotion(
            MmdModelDefinition model, string boneName, int frames, float endTranslationX)
        {
            byte[] linear = { 20, 20, 107, 107 };
            MmdBoneInterpolationDefinition Interp() => new MmdBoneInterpolationDefinition
            {
                translationX = linear, translationY = linear, translationZ = linear, rotation = linear
            };

            return new MmdMotionDefinition
            {
                targetModelName = model.name,
                maxFrame = frames,
                boneKeyframes = new List<MmdBoneKeyframeDefinition>
                {
                    new MmdBoneKeyframeDefinition
                    {
                        boneName = boneName, frame = 0,
                        translation = new[] { 0.0f, 0.0f, 0.0f },
                        rotation = new[] { 0.0f, 0.0f, 0.0f, 1.0f },
                        interpolation = Interp()
                    },
                    new MmdBoneKeyframeDefinition
                    {
                        boneName = boneName, frame = frames,
                        translation = new[] { endTranslationX, 0.0f, 0.0f },
                        rotation = new[] { 0.0f, 0.0f, 0.0f, 1.0f },
                        interpolation = Interp()
                    }
                },
                morphKeyframes = new List<MmdMorphKeyframeDefinition>(),
                modelKeyframes = new List<MmdModelKeyframeDefinition>()
            };
        }

        // Minimal two-bone model with a bone-follow (static) anchor on the root bone and a PMX mode-1
        // (pure dynamic) hair body on a child bone, joined by a ball joint. Used to verify that the
        // mode-1 hair (whose position is entirely physics-driven) follows the body when the root bone
        // animates, instead of staying at the bind pose.
        private static MmdModelDefinition CreateMode1ChainModel()
        {
            var model = new MmdModelDefinition { name = "mode1-chain" };
            model.bones.Add(new MmdBoneDefinition
            {
                index = 0, name = "root", parentIndex = -1, transformOrder = 0,
                origin = new[] { 0.0f, 0.0f, 0.0f }, isMovable = true, isRotatable = true
            });
            model.bones.Add(new MmdBoneDefinition
            {
                index = 1, name = "hair", parentIndex = 0, transformOrder = 1,
                origin = new[] { 0.0f, -2.0f, 0.0f }, isMovable = true, isRotatable = true
            });
            model.vertices.Add(CreateVertex(0, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0));
            model.vertices.Add(CreateVertex(1, 1.0f, 0.0f, 0.0f, 1.0f, 0.0f, 0));
            model.vertices.Add(CreateVertex(2, 0.0f, -2.0f, 0.0f, 0.0f, 1.0f, 1));
            model.indices.AddRange(new[] { 0, 1, 2 });
            model.materials.Add(new MmdMaterialDefinition { index = 0, name = "chain-material", vertexCount = 3 });

            model.physics.rigidbodies.Add(new MmdRigidbodyDefinition
            {
                index = 0, name = "anchor", boneIndex = 0, boneName = "root", shapeType = "sphere",
                size = new[] { 0.3f, 0.3f, 0.3f }, position = new[] { 0.0f, 0.0f, 0.0f },
                rotation = new[] { 0.0f, 0.0f, 0.0f }, mass = 0.0f, linearDamping = 0.0f, angularDamping = 0.0f,
                friction = 0.5f, restitution = 0.0f, group = 0, mask = 0xffff, physicsKind = "static"
            });
            model.physics.rigidbodies.Add(new MmdRigidbodyDefinition
            {
                index = 1, name = "hairBody", boneIndex = 1, boneName = "hair", shapeType = "sphere",
                size = new[] { 0.3f, 0.3f, 0.3f }, position = new[] { 0.0f, -2.0f, 0.0f },
                rotation = new[] { 0.0f, 0.0f, 0.0f }, mass = 1.0f, linearDamping = 0.0f, angularDamping = 0.0f,
                friction = 0.5f, restitution = 0.0f, group = 0, mask = 0xffff, physicsKind = "dynamic"
            });
            model.physics.joints.Add(new MmdJointDefinition
            {
                index = 0, name = "anchor-hair", rigidbodyAIndex = 0, rigidbodyBIndex = 1,
                position = new[] { 0.0f, -1.0f, 0.0f }, rotation = new[] { 0.0f, 0.0f, 0.0f },
                linearLowerLimit = new[] { 0.0f, 0.0f, 0.0f }, linearUpperLimit = new[] { 0.0f, 0.0f, 0.0f },
                angularLowerLimit = new[] { -3.1415927f, -3.1415927f, -3.1415927f },
                angularUpperLimit = new[] { 3.1415927f, 3.1415927f, 3.1415927f },
                linearSpring = new[] { 0.0f, 0.0f, 0.0f }, angularSpring = new[] { 0.0f, 0.0f, 0.0f }
            });
            return model;
        }

        // CreateMode1ChainModel with the joint's linear DOFs FREED (lower > upper => unlimited in
        // btGeneric6DofSpring2Constraint). The pure-dynamic hair body is then NOT linearly dragged toward
        // the anchor by the joint, so its horizontal position reflects only where the reset PLACED it —
        // exposing the origin-bind vs current-pose reset target that a rigid (linear-locked) joint masks.
        private static MmdModelDefinition CreateMode1ChainModelWithFreeLinearJoint()
        {
            MmdModelDefinition model = CreateMode1ChainModel();
            MmdJointDefinition joint = model.physics.joints[0];
            joint.linearLowerLimit = new[] { 1.0f, 1.0f, 1.0f };
            joint.linearUpperLimit = new[] { -1.0f, -1.0f, -1.0f };
            return model;
        }

        private static MmdVertexDefinition CreateVertex(int index, float x, float y, float z, float u, float v, int bone)
        {
            return new MmdVertexDefinition
            {
                index = index,
                position = new[] { x, y, z },
                normal = new[] { 0.0f, 0.0f, 1.0f },
                uv = new[] { u, v },
                boneIndices = new[] { bone },
                boneWeights = new[] { 1.0f }
            };
        }

        private static MmdModelDefinition LoadHairPhysicsModel(out string pmxPath)
        {
            pmxPath = ResolvePackageFixture("test_hair_physics.pmx");
            var parser = new NativeMmdParser();
            MmdModelDefinition model = parser.LoadModel(File.ReadAllBytes(pmxPath));
            Assert.That(model.physics.rigidbodies.Count, Is.GreaterThan(0),
                "test_hair_physics.pmx must contain rigidbody definitions");
            // Pure world-anchor joints (both endpoints -1) are rejected by the descriptor validator.
            model.physics.joints.RemoveAll(j => j.rigidbodyAIndex < 0 && j.rigidbodyBIndex < 0);
            return model;
        }

        private static MmdMotionDefinition CreateRestPoseMotion(MmdModelDefinition model)
        {
            return new MmdMotionDefinition
            {
                targetModelName = model.name,
                maxFrame = 0,
                boneKeyframes = new List<MmdBoneKeyframeDefinition>(),
                morphKeyframes = new List<MmdMorphKeyframeDefinition>(),
                modelKeyframes = new List<MmdModelKeyframeDefinition>()
            };
        }

        private static void DestroyBinding(MmdUnityPlaybackBinding? binding)
        {
            if (binding?.Instance?.Root != null)
            {
                Object.DestroyImmediate(binding.Instance.Root);
            }

            binding?.Dispose();
        }

        private static string ResolvePackageFixture(string fileName)
        {
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            string packageRoot = Path.GetFullPath(Path.Combine(projectRoot, "..", "packages", "com.yohawing.mmd-unity"));
            return Path.Combine(packageRoot, "Tests", "Fixtures", "Assets", fileName);
        }
    }
}
