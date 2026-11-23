#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using Mmd.Editor;
using Mmd.Parser;
using Mmd.UnityIntegration;

namespace Mmd.Tests
{
    public sealed class MmdHumanoidClipConversionWriterTests
    {
        private const string FixtureVmdPath =
            "Packages/com.yohawing.mmd-loader/Tests/Fixtures/Assets/test_1bone_cube_motion.vmd";

        [Test]
        public void CreateInMemoryClipUsesImportedHumanoidAvatarAndBindings()
        {
            MmdPmxAsset pmxAsset = null!;
            MmdVmdAsset? vmdAsset = AssetDatabase.LoadAssetAtPath<MmdVmdAsset>(FixtureVmdPath);
            var ownedObjects = new List<UnityEngine.Object>();
            MmdHumanoidClipConversionWriterResult? result = null;

            try
            {
                CreateReadyFixturePmx(out pmxAsset, ownedObjects);
                ConfigureImportedHumanoidState(pmxAsset, ownedObjects);
                Assert.That(vmdAsset, Is.Not.Null);

                result = MmdHumanoidClipConversionWriter.CreateInMemoryClip(
                    pmxAsset,
                    vmdAsset!,
                    frameRate: 30.0f,
                    startFrame: 0,
                    endFrame: 2);

                Assert.That(result.Clip, Is.Not.Null, string.Join("\n", result.Diagnostics));
                Assert.That(result.PrerequisitesReady, Is.True);
                Assert.That(result.CanCreateClipNow, Is.True);
                Assert.That(
                    string.Join("\n", result.Diagnostics),
                    Does.Contain(MmdHumanoidClipConversionPlanner.ImportedPmxHumanoidMappingSource));
                Assert.That(
                    string.Join("\n", result.Diagnostics),
                    Does.Contain("native batch evaluation"));
                Assert.That(
                    string.Join("\n", result.Diagnostics),
                    Does.Contain("root-motion: wrote native evaluated"));
                AssertHumanoidClipHasMuscleBindings(result.Clip!);
                AssertRootMotionBindings(result.Clip!);
            }
            finally
            {
                if (result?.Clip != null)
                {
                    UnityEngine.Object.DestroyImmediate(result.Clip);
                }

                foreach (UnityEngine.Object obj in ownedObjects)
                {
                    if (obj != null)
                    {
                        UnityEngine.Object.DestroyImmediate(obj);
                    }
                }
            }
        }

        [Test]
        public void CreateInMemoryClipPreservesHumanoidBodyHeightAndRotationInRootCurves()
        {
            MmdPmxAsset pmxAsset = null!;
            MmdVmdAsset? vmdAsset = AssetDatabase.LoadAssetAtPath<MmdVmdAsset>(FixtureVmdPath);
            var ownedObjects = new List<UnityEngine.Object>();
            MmdHumanoidClipConversionWriterResult? result = null;

            try
            {
                CreateReadyFixturePmx(out pmxAsset, ownedObjects);
                ConfigureImportedHumanoidState(pmxAsset, ownedObjects);
                Assert.That(vmdAsset, Is.Not.Null);

                Mmd.UnityIntegration.MmdUnityPlaybackController controller = pmxAsset.ImportedRoot!
                    .GetComponent<Mmd.UnityIntegration.MmdUnityPlaybackController>();
                Assert.That(controller.HumanoidProxyRoot, Is.Not.Null);
                MmdHumanoidRetargetBinding hipsBinding = controller.HumanoidRetargetEntries
                    .Single(binding => binding.HumanBone == HumanBodyBones.Hips);
                hipsBinding.ProxyTransform!.localRotation *= Quaternion.AngleAxis(20.0f, Vector3.forward);

                var baselinePose = new HumanPose { muscles = new float[HumanTrait.MuscleCount] };
                using (var poseHandler = new HumanPoseHandler(
                           pmxAsset.ImportedAvatar!,
                           pmxAsset.ImportedRoot!.transform))
                {
                    poseHandler.GetHumanPose(ref baselinePose);
                }

                result = MmdHumanoidClipConversionWriter.CreateInMemoryClip(
                    pmxAsset,
                    vmdAsset!,
                    frameRate: 30.0f,
                    startFrame: 0,
                    endFrame: 2);

                Assert.That(result.Clip, Is.Not.Null, string.Join("\n", result.Diagnostics));
                Vector3 actualPosition = ReadRootPosition(result.Clip!, 0.0f);
                Quaternion actualRotation = ReadRootRotation(result.Clip!, 0.0f);
                Assert.That(baselinePose.bodyPosition.y, Is.GreaterThan(0.0f), "precondition: Humanoid body height");
                Assert.That(actualPosition.x, Is.EqualTo(baselinePose.bodyPosition.x).Within(0.0001f));
                Assert.That(actualPosition.y, Is.EqualTo(baselinePose.bodyPosition.y).Within(0.0001f));
                Assert.That(actualPosition.z, Is.EqualTo(baselinePose.bodyPosition.z).Within(0.0001f));
                Assert.That(
                    Mathf.Abs(Quaternion.Dot(actualRotation, baselinePose.bodyRotation)),
                    Is.EqualTo(1.0f).Within(0.0001f));
            }
            finally
            {
                if (result?.Clip != null)
                {
                    UnityEngine.Object.DestroyImmediate(result.Clip);
                }

                DestroyOwnedObjects(ownedObjects);
            }
        }

        [Test]
        public void CreateHumanoidAnimationClipAssetWritesAnimationClipToAssetsPath()
        {
            const string outputDirectory = "Assets/HumanoidClipWriterTests";
            const string outputPath = outputDirectory + "/baked.anim";
            MmdPmxAsset pmxAsset = null!;
            MmdVmdAsset vmdAsset = AssetDatabase.LoadAssetAtPath<MmdVmdAsset>(FixtureVmdPath);
            var ownedObjects = new List<UnityEngine.Object>();
            MmdHumanoidClipConversionWriterResult? result = null;

            try
            {
                CreateReadyFixturePmx(out pmxAsset, ownedObjects);
                ConfigureImportedHumanoidState(pmxAsset, ownedObjects);
                AssetDatabase.DeleteAsset(outputDirectory);
                CreateFolderIfMissing(outputDirectory);

                result = MmdHumanoidClipConversionWriter.CreateHumanoidAnimationClipAsset(
                    pmxAsset, vmdAsset, 30.0f, 0, 2, outputPath);

                Assert.That(result.Clip, Is.Not.Null, string.Join("\n", result.Diagnostics));
                Assert.That(AssetDatabase.GetAssetPath(result.Clip), Is.EqualTo(outputPath));
                Assert.That(AssetDatabase.LoadAssetAtPath<AnimationClip>(outputPath), Is.SameAs(result.Clip));
                AssertRootMotionBindings(result.Clip!);
            }
            finally
            {
                AssetDatabase.DeleteAsset(outputDirectory);
                DestroyOwnedObjects(ownedObjects);
            }
        }

        [Test]
        public void ReimportedHumanoidClipDrivesFreshAvatarWithBodyHeightAndHipsRotation()
        {
            string outputDirectory = "Assets/HumanoidClipApplicationTests_" + Guid.NewGuid().ToString("N");
            string outputPath = outputDirectory + "/baked-humanoid.anim";
            MmdPmxAsset pmxAsset = null!;
            MmdVmdAsset vmdAsset = AssetDatabase.LoadAssetAtPath<MmdVmdAsset>(FixtureVmdPath);
            var ownedObjects = new List<UnityEngine.Object>();
            MmdHumanoidClipConversionWriterResult? result = null;
            AnimationClip? inMemoryClip = null;
            GameObject? referenceRoot = null;
            GameObject? playbackRoot = null;
            PlayableGraph referenceGraph = default;
            PlayableGraph playbackGraph = default;

            try
            {
                CreateReadyFixturePmx(out pmxAsset, ownedObjects);
                ConfigureImportedHumanoidState(pmxAsset, ownedObjects);
                CreateFolderIfMissing(outputDirectory);

                Mmd.UnityIntegration.MmdUnityPlaybackController controller = pmxAsset.ImportedRoot!
                    .GetComponent<Mmd.UnityIntegration.MmdUnityPlaybackController>();
                Assert.That(controller.HumanoidProxyRoot, Is.Not.Null);
                Transform sourceAvatarRoot = pmxAsset.ImportedRoot.transform;
                MmdHumanoidRetargetBinding sourceHipsBinding = controller.HumanoidRetargetEntries
                    .Single(binding => binding.HumanBone == HumanBodyBones.Hips);
                Assert.That(sourceHipsBinding.ProxyTransform, Is.Not.Null);

                string hipsPath = AnimationUtility.CalculateTransformPath(
                    sourceHipsBinding.ProxyTransform,
                    sourceAvatarRoot);
                referenceRoot = CreateFreshHumanoidPlaybackRoot(sourceAvatarRoot, "InMemoryHumanoidPlaybackRoot");
                playbackRoot = CreateFreshHumanoidPlaybackRoot(sourceAvatarRoot, "ReimportedHumanoidPlaybackRoot");
                Transform? referenceHips = referenceRoot.transform.Find(hipsPath);
                Transform? playbackHips = playbackRoot.transform.Find(hipsPath);
                Assert.That(referenceHips, Is.Not.Null, "reference clone must preserve the Avatar Hips path");
                Assert.That(playbackHips, Is.Not.Null, "fresh clone must preserve the Avatar Hips path");
                Quaternion bindHipsRotation = playbackHips!.rotation;

                sourceHipsBinding.ProxyTransform!.localRotation *=
                    Quaternion.AngleAxis(20.0f, Vector3.forward);
                result = MmdHumanoidClipConversionWriter.CreateHumanoidAnimationClipAsset(
                    pmxAsset,
                    vmdAsset,
                    frameRate: 30.0f,
                    startFrame: 0,
                    endFrame: 2,
                    outputPath);

                Assert.That(result.Clip, Is.Not.Null, string.Join("\n", result.Diagnostics));
                inMemoryClip = UnityEngine.Object.Instantiate(result.Clip);
                AssetDatabase.ImportAsset(outputPath, ImportAssetOptions.ForceUpdate);
                AnimationClip? reimported = AssetDatabase.LoadAssetAtPath<AnimationClip>(outputPath);
                Assert.That(reimported, Is.Not.Null);
                Assert.That(reimported!.humanMotion, Is.True);

                Avatar avatar = pmxAsset.ImportedAvatar!;
                Assert.That(avatar, Is.Not.Null);
                Assert.That(avatar.isValid && avatar.isHuman, Is.True);
                Animator referenceAnimator = ConfigureHumanoidPlaybackAnimator(referenceRoot, avatar);
                Animator playbackAnimator = ConfigureHumanoidPlaybackAnimator(playbackRoot, avatar);
                HumanPose baselinePose = ReadHumanPose(avatar, playbackRoot.transform);
                float baselineHipsHeight = playbackHips.position.y - playbackRoot.transform.position.y;
                Assert.That(baselinePose.bodyPosition.y, Is.GreaterThan(0.0f));
                Assert.That(baselineHipsHeight, Is.GreaterThan(0.0f));

                referenceGraph = CreateHumanoidPlaybackGraph(
                    "InMemoryHumanoidBake",
                    referenceAnimator,
                    inMemoryClip!,
                    out AnimationClipPlayable referencePlayable);
                playbackGraph = CreateHumanoidPlaybackGraph(
                    "ReimportedHumanoidBake",
                    playbackAnimator,
                    reimported,
                    out AnimationClipPlayable playbackPlayable);

                Vector3 expectedBodyPosition = ReadRootPosition(reimported, 0.0f);
                Assert.That(expectedBodyPosition.y, Is.GreaterThan(0.0f),
                    "persisted RootT must retain the baked Humanoid body height");

                foreach (double time in new[] { 0.0, 2.0 / 30.0 })
                {
                    referencePlayable.SetTime(time);
                    playbackPlayable.SetTime(time);
                    referenceGraph.Evaluate(0.0f);
                    playbackGraph.Evaluate(0.0f);

                    HumanPose referencePose = ReadHumanPose(avatar, referenceRoot.transform);
                    HumanPose appliedPose = ReadHumanPose(avatar, playbackRoot.transform);
                    Assert.That(Vector3.Distance(appliedPose.bodyPosition, referencePose.bodyPosition),
                        Is.LessThan(0.0001f), "body position round-trip parity at " + time);
                    Assert.That(Mathf.Abs(Quaternion.Dot(appliedPose.bodyRotation, referencePose.bodyRotation)),
                        Is.EqualTo(1.0f).Within(0.0001f), "body rotation round-trip parity at " + time);
                    Assert.That(Vector3.Distance(playbackRoot.transform.position, referenceRoot.transform.position),
                        Is.LessThan(0.0001f), "root position round-trip parity at " + time);
                    Assert.That(Quaternion.Angle(playbackRoot.transform.rotation, referenceRoot.transform.rotation),
                        Is.LessThan(0.01f), "root rotation round-trip parity at " + time);
                    Assert.That(Vector3.Distance(playbackHips.position, referenceHips!.position),
                        Is.LessThan(0.0001f), "Hips position round-trip parity at " + time);
                    Assert.That(Quaternion.Angle(playbackHips.rotation, referenceHips.rotation),
                        Is.LessThan(0.01f), "Hips rotation round-trip parity at " + time);

                    Assert.That(float.IsFinite(appliedPose.bodyPosition.y), Is.True);
                    Assert.That(appliedPose.bodyPosition.y, Is.GreaterThan(baselinePose.bodyPosition.y * 0.5f),
                        "reimported Humanoid clip must not collapse body/Hips height to Y=0");
                    float appliedHipsHeight = playbackHips.position.y - playbackRoot.transform.position.y;
                    Assert.That(appliedHipsHeight, Is.GreaterThan(baselineHipsHeight * 0.5f),
                        "fresh Avatar Hips must retain their bind-relative height after clip evaluation");
                    Assert.That(Quaternion.Angle(bindHipsRotation, playbackHips.rotation),
                        Is.GreaterThan(1.0f),
                        "reimported RootQ/muscle curves must visibly rotate the fresh Avatar Hips");
                }
            }
            finally
            {
                if (referenceGraph.IsValid()) referenceGraph.Destroy();
                if (playbackGraph.IsValid()) playbackGraph.Destroy();
                if (inMemoryClip != null) UnityEngine.Object.DestroyImmediate(inMemoryClip);
                if (referenceRoot != null) UnityEngine.Object.DestroyImmediate(referenceRoot);
                if (playbackRoot != null) UnityEngine.Object.DestroyImmediate(playbackRoot);
                AssetDatabase.DeleteAsset(outputDirectory);
                DestroyOwnedObjects(ownedObjects);
            }
        }

        [TestCase("../outside.anim")]
        [TestCase("Assets/not-animation.asset")]
        public void CreateHumanoidAnimationClipAssetRejectsInvalidOutputPath(string invalidOutputPath)
        {
            MmdPmxAsset pmxAsset = null!;
            MmdVmdAsset vmdAsset = AssetDatabase.LoadAssetAtPath<MmdVmdAsset>(FixtureVmdPath);
            var ownedObjects = new List<UnityEngine.Object>();

            try
            {
                CreateReadyFixturePmx(out pmxAsset, ownedObjects);
                ConfigureImportedHumanoidState(pmxAsset, ownedObjects);

                MmdHumanoidClipConversionWriterResult result =
                    MmdHumanoidClipConversionWriter.CreateHumanoidAnimationClipAsset(
                        pmxAsset, vmdAsset, 30.0f, 0, 2, invalidOutputPath);

                Assert.That(result.Clip, Is.Null);
                Assert.That(result.Diagnostics, Is.Not.Empty);
            }
            finally
            {
                DestroyOwnedObjects(ownedObjects);
            }
        }

        [Test]
        public void GetDefaultOutputPathUsesAssetsDirectoryAndSourceIds()
        {
            MmdPmxAsset pmxAsset = null!;
            var ownedObjects = new List<UnityEngine.Object>();
            try
            {
                CreateReadyFixturePmx(out pmxAsset, ownedObjects);
                Assert.That(
                    MmdHumanoidClipConversionWriter.GetDefaultOutputPath(pmxAsset, null),
                    Is.EqualTo("Assets/ready-h6-writer-slice1_VMD.anim"));
            }
            finally
            {
                DestroyOwnedObjects(ownedObjects);
            }
        }

        [Test]
        public void CreateInMemoryClipUsesClipLocalTimeWhenStartFrameIsNonZero()
        {
            MmdPmxAsset pmxAsset = null!;
            MmdVmdAsset vmdAsset = AssetDatabase.LoadAssetAtPath<MmdVmdAsset>(FixtureVmdPath);
            var ownedObjects = new List<UnityEngine.Object>();
            MmdHumanoidClipConversionWriterResult? result = null;
            try
            {
                CreateReadyFixturePmx(out pmxAsset, ownedObjects);
                ConfigureImportedHumanoidState(pmxAsset, ownedObjects);
                result = MmdHumanoidClipConversionWriter.CreateInMemoryClip(
                    pmxAsset, vmdAsset, 30.0f, startFrame: 1, endFrame: 2);

                Assert.That(result.Clip, Is.Not.Null, string.Join("\n", result.Diagnostics));
                AnimationCurve rootCurve = AnimationUtility.GetEditorCurve(
                    result.Clip!,
                    EditorCurveBinding.FloatCurve(string.Empty, typeof(Animator), "RootT.x"));
                Assert.That(rootCurve.keys, Has.Length.EqualTo(2));
                Assert.That(rootCurve.keys[0].time, Is.Zero.Within(0.0001f));
                Assert.That(rootCurve.keys[1].time, Is.EqualTo(1.0f / 30.0f).Within(0.0001f));
                AnimationCurve verticalOffsetCurve = AnimationUtility.GetEditorCurve(
                    result.Clip!,
                    EditorCurveBinding.FloatCurve(
                        string.Empty,
                        typeof(MmdHumanoidRootMotionDriver),
                        "clipRootVerticalOffset"));
                Assert.That(verticalOffsetCurve.keys, Has.Length.EqualTo(2));
                Assert.That(verticalOffsetCurve.keys[0].time, Is.Zero.Within(0.0001f));
                Assert.That(verticalOffsetCurve.keys[1].time, Is.EqualTo(1.0f / 30.0f).Within(0.0001f));
            }
            finally
            {
                if (result?.Clip != null)
                {
                    UnityEngine.Object.DestroyImmediate(result.Clip);
                }
                DestroyOwnedObjects(ownedObjects);
            }
        }

        private static readonly string[] ReadySuccessDiagnostics =
        {
            "conversion-prerequisites: ready",
            "mapping-source: Imported PMX Humanoid settings.",
            "writer-status: CanCreateClipNow is true (in-memory writer in H6 slice 1).",
            "writer: enabled native batch evaluation for Humanoid IK sampling.",
            "writer: sampled evaluated MMD IK pose through native batch output.",
            "writer: wrote " + HumanTrait.MuscleCount + " Humanoid muscle curves.",
            "root-motion: wrote native evaluated dense RootT/RootQ curves from mapped Hips hierarchy.",
        };

        private static readonly string[] ReadyPlanDiagnostics =
        {
            "conversion-prerequisites: ready",
            "mapping-source: Imported PMX Humanoid settings.",
            "writer-status: CanCreateClipNow is true (in-memory writer in H6 slice 1).",
        };

        [Test]
        public void CreateInMemoryClipPinsDiagnosticsClipNameAndFrameRate()
        {
            MmdVmdAsset vmdAsset = AssetDatabase.LoadAssetAtPath<MmdVmdAsset>(FixtureVmdPath);
            var ownedObjects = new List<UnityEngine.Object>();
            try
            {
                CreateReadyFixturePmx(out MmdPmxAsset pmxAsset, ownedObjects);
                ConfigureImportedHumanoidState(pmxAsset, ownedObjects);
                foreach ((float rate, int start, int end) in new[] { (30.0f, 0, 2), (24.0f, 1, 3) })
                {
                    MmdHumanoidClipConversionWriterResult result =
                        MmdHumanoidClipConversionWriter.CreateInMemoryClip(pmxAsset, vmdAsset, rate, start, end);
                    try
                    {
                        Assert.That(result.Clip, Is.Not.Null, string.Join("\n", result.Diagnostics));
                        Assert.That(result.Diagnostics, Is.EqualTo(ReadySuccessDiagnostics));
                        Assert.That(
                            result.Clip!.name,
                            Is.EqualTo("H6_HumanoidProxyClip_ready-h6-writer-slice1_pmx_"
                                       + "Packages_com_yohawing_mmd-loader_Tests_Fixtures_Assets_test_1bone_cube_motion_vmd_"
                                       + start + "_" + end + "_" + (int)rate + "fps"));
                        Assert.That(result.Clip.frameRate, Is.EqualTo(rate));
                    }
                    finally
                    {
                        if (result.Clip != null) UnityEngine.Object.DestroyImmediate(result.Clip);
                    }
                }
            }
            finally
            {
                DestroyOwnedObjects(ownedObjects);
            }
        }

        [Test]
        public void CreateInMemoryClipNameUsesInvariantFrameRateUnderNonInvariantCulture()
        {
            MmdVmdAsset vmdAsset = AssetDatabase.LoadAssetAtPath<MmdVmdAsset>(FixtureVmdPath);
            var ownedObjects = new List<UnityEngine.Object>();
            System.Globalization.CultureInfo originalCulture = System.Globalization.CultureInfo.CurrentCulture;
            try
            {
                CreateReadyFixturePmx(out MmdPmxAsset pmxAsset, ownedObjects);
                ConfigureImportedHumanoidState(pmxAsset, ownedObjects);
                System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("de-DE");
                foreach ((float rate, string rateText) in new[] { (30.0f, "30"), (29.97f, "29.97") })
                {
                    MmdHumanoidClipConversionWriterResult result =
                        MmdHumanoidClipConversionWriter.CreateInMemoryClip(pmxAsset, vmdAsset, rate, 0, 2);
                    try
                    {
                        Assert.That(result.Clip, Is.Not.Null, string.Join("\n", result.Diagnostics));
                        Assert.That(
                            result.Clip!.name,
                            Is.EqualTo("H6_HumanoidProxyClip_ready-h6-writer-slice1_pmx_"
                                       + "Packages_com_yohawing_mmd-loader_Tests_Fixtures_Assets_test_1bone_cube_motion_vmd_"
                                       + "0_2_" + rateText + "fps"));
                    }
                    finally
                    {
                        if (result.Clip != null) UnityEngine.Object.DestroyImmediate(result.Clip);
                    }
                }
            }
            finally
            {
                System.Globalization.CultureInfo.CurrentCulture = originalCulture;
                DestroyOwnedObjects(ownedObjects);
            }
        }

        [Test]
        public void HumanoidWrittenKeyEstimateMatchesDenseKeysWrittenByWriter()
        {
            MmdVmdAsset vmdAsset = AssetDatabase.LoadAssetAtPath<MmdVmdAsset>(FixtureVmdPath);
            var ownedObjects = new List<UnityEngine.Object>();
            try
            {
                CreateReadyFixturePmx(out MmdPmxAsset pmxAsset, ownedObjects);
                ConfigureImportedHumanoidState(pmxAsset, ownedObjects);
                const int startFrame = 0;
                const int endFrame = 2;
                MmdHumanoidClipConversionWriterResult result =
                    MmdHumanoidClipConversionWriter.CreateInMemoryClip(pmxAsset, vmdAsset, 30.0f, startFrame, endFrame);
                try
                {
                    Assert.That(result.Clip, Is.Not.Null, string.Join("\n", result.Diagnostics));
                    long writtenKeyCount = AnimationUtility.GetCurveBindings(result.Clip!)
                        .Sum(binding => (long)AnimationUtility.GetEditorCurve(result.Clip!, binding).length);
                    long frameCount = endFrame - startFrame + 1;

                    Assert.That(
                        MmdAnimationClipBakeBudget.EstimateHumanoidWrittenKeyCount(frameCount, HumanTrait.MuscleCount),
                        Is.EqualTo(writtenKeyCount));
                    Assert.That(writtenKeyCount, Is.EqualTo((HumanTrait.MuscleCount + 8L) * frameCount));
                }
                finally
                {
                    if (result.Clip != null) UnityEngine.Object.DestroyImmediate(result.Clip);
                }
            }
            finally
            {
                DestroyOwnedObjects(ownedObjects);
            }
        }

        [Test]
        public void CreateInMemoryClipNullInputsReportPlannerDiagnostics()
        {
            MmdVmdAsset vmdAsset = AssetDatabase.LoadAssetAtPath<MmdVmdAsset>(FixtureVmdPath);
            var ownedObjects = new List<UnityEngine.Object>();
            try
            {
                CreateReadyFixturePmx(out MmdPmxAsset pmxAsset, ownedObjects);
                ConfigureImportedHumanoidState(pmxAsset, ownedObjects);
                const string notReady = "writer: prerequisites are not ready; cannot create in-memory clip.";
                AssertNoClip(
                    MmdHumanoidClipConversionWriter.CreateInMemoryClip(null!, vmdAsset, 30.0f),
                    "pmx asset is null",
                    notReady);
                AssertNoClip(
                    MmdHumanoidClipConversionWriter.CreateInMemoryClip(pmxAsset, null!, 30.0f),
                    "vmd asset is null",
                    notReady);
                AssertNoClip(
                    MmdHumanoidClipConversionWriter.CreateInMemoryClip(null!, null!, float.NaN, -1, -5),
                    "pmx asset is null",
                    "vmd asset is null",
                    notReady);
            }
            finally
            {
                DestroyOwnedObjects(ownedObjects);
            }
        }

        [TestCase(0.0f, 0, 2, "validation: frameRate must be finite and > 0.")]
        [TestCase(-30.0f, 0, 2, "validation: frameRate must be finite and > 0.")]
        [TestCase(float.NaN, 0, 2, "validation: frameRate must be finite and > 0.")]
        [TestCase(float.PositiveInfinity, -1, 2, "validation: frameRate must be finite and > 0.")]
        [TestCase(30.0f, -1, 2, "validation: startFrame must be >= 0.")]
        [TestCase(30.0f, 5, 4, "validation: endFrame must be >= startFrame.")]
        [TestCase(30.0f, 50, 50, "validation: startFrame must be <= motion.maxFrame 49.")]
        [TestCase(30.0f, 0, 50, "validation: endFrame 50 must be <= motion.maxFrame 49.")]
        public void CreateInMemoryClipInvalidSamplingInputsReportExactDiagnostics(
            float rate,
            int start,
            int end,
            string expected)
        {
            MmdVmdAsset vmdAsset = AssetDatabase.LoadAssetAtPath<MmdVmdAsset>(FixtureVmdPath);
            var ownedObjects = new List<UnityEngine.Object>();
            try
            {
                CreateReadyFixturePmx(out MmdPmxAsset pmxAsset, ownedObjects);
                ConfigureImportedHumanoidState(pmxAsset, ownedObjects);
                AssertNoClip(
                    MmdHumanoidClipConversionWriter.CreateInMemoryClip(pmxAsset, vmdAsset, rate, start, end),
                    ReadyPlanDiagnostics.Append(expected).ToArray());
            }
            finally
            {
                DestroyOwnedObjects(ownedObjects);
            }
        }

        [Test]
        public void CreateInMemoryClipStartFrameBeyondMotionWithDefaultEndReportsEndBeforeStart()
        {
            MmdVmdAsset vmdAsset = AssetDatabase.LoadAssetAtPath<MmdVmdAsset>(FixtureVmdPath);
            var ownedObjects = new List<UnityEngine.Object>();
            try
            {
                CreateReadyFixturePmx(out MmdPmxAsset pmxAsset, ownedObjects);
                ConfigureImportedHumanoidState(pmxAsset, ownedObjects);
                AssertNoClip(
                    MmdHumanoidClipConversionWriter.CreateInMemoryClip(pmxAsset, vmdAsset, 30.0f, 60),
                    ReadyPlanDiagnostics.Append("validation: endFrame must be >= startFrame.").ToArray());
            }
            finally
            {
                DestroyOwnedObjects(ownedObjects);
            }
        }

        [Test]
        public void CreateInMemoryClipBudgetOverflowReportsExactDiagnostics()
        {
            byte[] vmdBytes = MmdGenericAnimationClipWriterCharacterizationTests.BuildTwoKeyVmd("センター", 40_000);
            MmdVmdAsset vmdAsset = ScriptableObject.CreateInstance<MmdVmdAsset>();
            vmdAsset.Initialize(vmdBytes, "budget.vmd", "Assets/budget.vmd", MmdVmdNativeSummaryAdapter.Read(vmdBytes));
            var ownedObjects = new List<UnityEngine.Object> { vmdAsset };
            try
            {
                CreateReadyFixturePmx(out MmdPmxAsset pmxAsset, ownedObjects);
                ConfigureImportedHumanoidState(pmxAsset, ownedObjects);
                AssertNoClip(
                    MmdHumanoidClipConversionWriter.CreateInMemoryClip(pmxAsset, vmdAsset, 30.0f),
                    ReadyPlanDiagnostics.Append(
                        "validation: requested Frame Range exceeds the safe dense bake budget of 4,000,000 key "
                        + "equivalents. Narrow Frame Range and try again.").ToArray());
            }
            finally
            {
                DestroyOwnedObjects(ownedObjects);
            }
        }

        [TestCase("", "validation: output path is required.")]
        [TestCase("C:/outside.anim", "validation: output path must be project-relative, not rooted.")]
        [TestCase("Packages/outside.anim", "validation: output path must start with Assets/.")]
        [TestCase("Assets/clip.asset", "validation: output path must end with .anim.")]
        [TestCase("Assets//clip.anim", "validation: output path must not contain empty segments.")]
        [TestCase("Assets/../clip.anim", "validation: output path must not contain '.' or '..' segments.")]
        public void CreateHumanoidAnimationClipAssetInvalidPathReportsBakeDiagnosticsThenPathError(
            string outputPath,
            string expected)
        {
            MmdVmdAsset vmdAsset = AssetDatabase.LoadAssetAtPath<MmdVmdAsset>(FixtureVmdPath);
            var ownedObjects = new List<UnityEngine.Object>();
            try
            {
                CreateReadyFixturePmx(out MmdPmxAsset pmxAsset, ownedObjects);
                ConfigureImportedHumanoidState(pmxAsset, ownedObjects);
                AssertNoClip(
                    MmdHumanoidClipConversionWriter.CreateHumanoidAnimationClipAsset(
                        pmxAsset, vmdAsset, 30.0f, 0, 2, outputPath),
                    ReadySuccessDiagnostics.Append(expected).ToArray());
            }
            finally
            {
                DestroyOwnedObjects(ownedObjects);
            }
        }

        [Test]
        public void HumanoidBakesDoNotLeakTemporaryObjects()
        {
            MmdVmdAsset vmdAsset = AssetDatabase.LoadAssetAtPath<MmdVmdAsset>(FixtureVmdPath);
            byte[] budgetBytes = MmdGenericAnimationClipWriterCharacterizationTests.BuildTwoKeyVmd("センター", 40_000);
            MmdVmdAsset budgetVmd = ScriptableObject.CreateInstance<MmdVmdAsset>();
            budgetVmd.Initialize(budgetBytes, "budget.vmd", "Assets/budget.vmd", MmdVmdNativeSummaryAdapter.Read(budgetBytes));
            var ownedObjects = new List<UnityEngine.Object> { budgetVmd };
            try
            {
                CreateReadyFixturePmx(out MmdPmxAsset pmxAsset, ownedObjects);
                ConfigureImportedHumanoidState(pmxAsset, ownedObjects);

                void BakeAll()
                {
                    DestroyResultClip(MmdHumanoidClipConversionWriter.CreateInMemoryClip(pmxAsset, vmdAsset, 30.0f, 0, 2));
                    DestroyResultClip(MmdHumanoidClipConversionWriter.CreateInMemoryClip(pmxAsset, vmdAsset, 30.0f, 0, 50));
                    DestroyResultClip(MmdHumanoidClipConversionWriter.CreateInMemoryClip(pmxAsset, budgetVmd, 30.0f));
                    DestroyResultClip(MmdHumanoidClipConversionWriter.CreateHumanoidAnimationClipAsset(
                        pmxAsset, vmdAsset, 30.0f, 0, 2, "Assets/clip.asset"));
                }

                BakeAll();
                string before = MmdGenericAnimationClipWriterCharacterizationTests.CountObjects();
                BakeAll();
                Assert.That(MmdGenericAnimationClipWriterCharacterizationTests.CountObjects(), Is.EqualTo(before));
            }
            finally
            {
                DestroyOwnedObjects(ownedObjects);
            }
        }

        private static void AssertNoClip(MmdHumanoidClipConversionWriterResult result, params string[] expected)
        {
            try
            {
                Assert.That(result.Clip, Is.Null, string.Join("\n", result.Diagnostics));
                Assert.That(result.Diagnostics, Is.EqualTo(expected), string.Join("\n", result.Diagnostics));
            }
            finally
            {
                DestroyResultClip(result);
            }
        }

        private static void DestroyResultClip(MmdHumanoidClipConversionWriterResult result)
        {
            if (result.Clip != null && !AssetDatabase.Contains(result.Clip))
            {
                UnityEngine.Object.DestroyImmediate(result.Clip);
            }
        }

        private static void CreateReadyFixturePmx(
            out MmdPmxAsset pmxAsset,
            List<UnityEngine.Object> ownedObjects)
        {
            byte[] modelBytes = MmdTestFixtures.ReadFixtureAssetBytes("test_basic_bone.pmx");
            MmdModelDefinition model = new NativeMmdParser().LoadModel(modelBytes);
            MmdUnityModelInstance generatedAssets = MmdUnityModelFactory.CreateSkinnedModel(model);

            pmxAsset = ScriptableObject.CreateInstance<MmdPmxAsset>();
            pmxAsset.Initialize(
                modelBytes,
                "ready-h6-writer-slice1.pmx",
                System.IO.Path.Combine("Assets", "ready-h6-writer-slice1.pmx"),
                importedMeshAsset: generatedAssets.Mesh,
                importedMaterialAssets: generatedAssets.Materials,
                importedRootAsset: generatedAssets.Root,
                hierarchyReadinessValue: MmdImportReadiness.Ready,
                rendererReadinessValue: MmdImportReadiness.Ready,
                boneBindingReadinessValue: MmdImportReadiness.Ready,
                parseSummary: MmdPmxParseSummary.FromModel(model));

            ownedObjects.Add(pmxAsset);
            ownedObjects.Add(generatedAssets.Root);
            // The renderer only references these; destroying the root does not destroy them.
            ownedObjects.Add(generatedAssets.Mesh);
            ownedObjects.AddRange(generatedAssets.Materials);
            ownedObjects.AddRange(generatedAssets.OwnedTextures);
        }

        private static void ConfigureImportedHumanoidState(
            MmdPmxAsset pmxAsset,
            List<UnityEngine.Object> ownedObjects)
        {
            MmdHumanoidProxyRigResult proxyRig = MmdHumanoidProxyRigFactory.CreateProxyRig(pmxAsset);
            Assert.That(proxyRig.ProxyRoot, Is.Not.Null, string.Join("\n", proxyRig.Diagnostics));
            Assert.That(proxyRig.Readiness, Is.EqualTo(MmdHumanoidMappingReadiness.Ready));

            GameObject importedRoot = pmxAsset.ImportedRoot!;
            proxyRig.ProxyRoot!.transform.SetParent(importedRoot.transform, worldPositionStays: false);
            MmdHumanoidAvatarBuildResult avatarResult = MmdHumanoidProxyRigFactory.BuildAvatar(
                proxyRig,
                avatarRoot: importedRoot);
            Assert.That(avatarResult.Avatar, Is.Not.Null, string.Join("\n", avatarResult.Diagnostics));
            Assert.That(avatarResult.Avatar!.isValid, Is.True);
            Assert.That(avatarResult.Avatar.isHuman, Is.True);

            Transform[] nativeBones = importedRoot
                .GetComponentInChildren<SkinnedMeshRenderer>(includeInactive: true)!
                .bones;
            var bindings = new List<MmdHumanoidRetargetBinding>();
            foreach (MmdHumanoidBoneMappingMatch match in proxyRig.Matches)
            {
                proxyRig.BoneMap.TryGetValue(match.HumanBone, out Transform proxyTransform);
                bindings.Add(new MmdHumanoidRetargetBinding(
                    match.HumanBone,
                    match.MmdBoneIndex,
                    proxyTransform,
                    nativeBones[match.MmdBoneIndex]));
            }

            Mmd.UnityIntegration.MmdUnityPlaybackController controller =
                importedRoot.AddComponent<Mmd.UnityIntegration.MmdUnityPlaybackController>();
            controller.ConfigureHumanoidRetarget(
                proxyRig.ProxyRoot.transform,
                bindings,
                Array.Empty<MmdHumanoidAppendTransformBinding>());
            pmxAsset.ApplyHumanoidAvatarImportSummary(
                "Humanoid",
                avatarResult.Avatar,
                MmdHumanoidMappingReadiness.Ready,
                "test: imported Humanoid ready");

            ownedObjects.Add(proxyRig.ProxyRoot);
            ownedObjects.Add(avatarResult.Avatar);
        }

        private static void AssertHumanoidClipHasMuscleBindings(AnimationClip clip)
        {
            EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(clip);
            Assert.That(clip.humanMotion, Is.True);
            Assert.That(bindings.Any(b => b.type == typeof(Animator) && string.IsNullOrEmpty(b.path)), Is.True);
            Assert.That(bindings.Any(b => b.propertyName == "m_LocalRotation.x"), Is.False);
        }

        private static void AssertRootMotionBindings(AnimationClip clip)
        {
            EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(clip);
            string[] properties =
            {
                "RootT.x", "RootT.y", "RootT.z",
                "RootQ.x", "RootQ.y", "RootQ.z", "RootQ.w",
            };
            foreach (string property in properties)
            {
                Assert.That(
                    bindings.Any(binding => binding.type == typeof(Animator)
                                            && string.IsNullOrEmpty(binding.path)
                                            && binding.propertyName == property),
                    Is.True,
                    property);
            }
            Assert.That(
                bindings.Any(binding => binding.type == typeof(MmdHumanoidRootMotionDriver)
                                        && string.IsNullOrEmpty(binding.path)
                                        && binding.propertyName == "clipRootVerticalOffset"),
                Is.True,
                "clipRootVerticalOffset");
        }

        private static GameObject CreateFreshHumanoidPlaybackRoot(Transform sourceRoot, string name)
        {
            GameObject clone = UnityEngine.Object.Instantiate(sourceRoot.gameObject);
            clone.name = name;
            clone.transform.SetParent(null, worldPositionStays: false);
            clone.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            clone.SetActive(true);
            return clone;
        }

        private static Animator ConfigureHumanoidPlaybackAnimator(GameObject root, Avatar avatar)
        {
            Animator animator = root.AddComponent<Animator>();
            animator.avatar = avatar;
            animator.applyRootMotion = true;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            return animator;
        }

        private static PlayableGraph CreateHumanoidPlaybackGraph(
            string name,
            Animator animator,
            AnimationClip clip,
            out AnimationClipPlayable playable)
        {
            PlayableGraph graph = PlayableGraph.Create(name);
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            playable = AnimationClipPlayable.Create(graph, clip);
            AnimationPlayableOutput output = AnimationPlayableOutput.Create(graph, name, animator);
            output.SetSourcePlayable(playable);
            graph.Play();
            return graph;
        }

        private static HumanPose ReadHumanPose(Avatar avatar, Transform root)
        {
            var pose = new HumanPose { muscles = new float[HumanTrait.MuscleCount] };
            using (var poseHandler = new HumanPoseHandler(avatar, root))
            {
                poseHandler.GetHumanPose(ref pose);
            }
            return pose;
        }

        private static Vector3 ReadRootPosition(AnimationClip clip, float time)
        {
            return new Vector3(
                ReadRootCurve(clip, "RootT.x").Evaluate(time),
                ReadRootCurve(clip, "RootT.y").Evaluate(time),
                ReadRootCurve(clip, "RootT.z").Evaluate(time));
        }

        private static Quaternion ReadRootRotation(AnimationClip clip, float time)
        {
            var rotation = new Quaternion(
                ReadRootCurve(clip, "RootQ.x").Evaluate(time),
                ReadRootCurve(clip, "RootQ.y").Evaluate(time),
                ReadRootCurve(clip, "RootQ.z").Evaluate(time),
                ReadRootCurve(clip, "RootQ.w").Evaluate(time));
            rotation.Normalize();
            return rotation;
        }

        private static AnimationCurve ReadRootCurve(AnimationClip clip, string propertyName)
        {
            AnimationCurve curve = AnimationUtility.GetEditorCurve(
                clip,
                EditorCurveBinding.FloatCurve(string.Empty, typeof(Animator), propertyName));
            Assert.That(curve, Is.Not.Null, propertyName);
            return curve;
        }

        private static void DestroyOwnedObjects(IEnumerable<UnityEngine.Object> ownedObjects)
        {
            foreach (UnityEngine.Object obj in ownedObjects)
            {
                if (obj != null)
                {
                    UnityEngine.Object.DestroyImmediate(obj);
                }
            }
        }

        private static void CreateFolderIfMissing(string folderPath)
        {
            if (!AssetDatabase.IsValidFolder(folderPath))
            {
                string? parent = Path.GetDirectoryName(folderPath.Replace('/', '\\'))?.Replace('\\', '/');
                string folderName = Path.GetFileName(folderPath);
                if (!string.IsNullOrWhiteSpace(parent) && !AssetDatabase.IsValidFolder(parent))
                {
                    CreateFolderIfMissing(parent!);
                }

                AssetDatabase.CreateFolder(
                    string.IsNullOrWhiteSpace(parent) ? "Assets" : parent,
                    folderName);
            }
        }
    }
}
