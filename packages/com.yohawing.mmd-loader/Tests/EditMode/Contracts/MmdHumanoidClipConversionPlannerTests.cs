#nullable enable

using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Mmd.Editor;
using Mmd.Parser;
using Mmd.UnityIntegration;

namespace Mmd.Tests
{
    public sealed class MmdHumanoidClipConversionPlannerTests
    {
        private const string FixturePmxPath =
            "Packages/com.yohawing.mmd-loader/Tests/Fixtures/Assets/test_1bone_cube.pmx";

        private const string FixtureVmdPath =
            "Packages/com.yohawing.mmd-loader/Tests/Fixtures/Assets/test_1bone_cube_motion.vmd";

        private static readonly string[] RequiredBoneNames =
        {
            "下半身", "上半身", "首", "頭",
            "左足", "左ひざ", "左足首", "右足", "右ひざ", "右足首",
            "左腕", "左ひじ", "左手首", "右腕", "右ひじ", "右手首",
        };

        [Test]
        public void AnalyzePrerequisitesReturnsFailureForNullInputs()
        {
            MmdHumanoidClipConversionPlan result =
                MmdHumanoidClipConversionPlanner.AnalyzePrerequisites(null, null);

            Assert.That(result.PrerequisitesReady, Is.False);
            Assert.That(result.CanCreateClipNow, Is.False);
            Assert.That(result.Readiness, Is.EqualTo(MmdHumanoidClipConversionPlanner.NotReadyReadiness));
            Assert.That(string.Join("\n", result.Diagnostics), Does.Contain("pmx asset is null"));
            Assert.That(string.Join("\n", result.Diagnostics), Does.Contain("vmd asset is null"));
        }

        [Test]
        public void AnalyzePrerequisitesRejectsGenericImportedPmx()
        {
            MmdPmxAsset pmxAsset = AssetDatabase.LoadAssetAtPath<MmdPmxAsset>(FixturePmxPath);
            MmdVmdAsset vmdAsset = AssetDatabase.LoadAssetAtPath<MmdVmdAsset>(FixtureVmdPath);

            Assert.That(pmxAsset, Is.Not.Null);
            Assert.That(vmdAsset, Is.Not.Null);
            MmdHumanoidClipConversionPlan result =
                MmdHumanoidClipConversionPlanner.AnalyzePrerequisites(pmxAsset, vmdAsset);

            Assert.That(result.PrerequisitesReady, Is.False);
            Assert.That(result.CanCreateClipNow, Is.False);
            Assert.That(string.Join("\n", result.Diagnostics), Does.Contain("expected Humanoid"));
        }

        [Test]
        public void AnalyzePrerequisitesReturnsReadyForImportedHumanoidState()
        {
            MmdPmxAsset pmxAsset = null!;
            MmdVmdAsset vmdAsset = AssetDatabase.LoadAssetAtPath<MmdVmdAsset>(FixtureVmdPath);
            var ownedObjects = new List<UnityEngine.Object>();
            try
            {
                CreateReadyImportedHumanoidPmx(out pmxAsset, ownedObjects);
                MmdHumanoidClipConversionPlan result =
                    MmdHumanoidClipConversionPlanner.AnalyzePrerequisites(pmxAsset, vmdAsset);

                Assert.That(result.PrerequisitesReady, Is.True, string.Join("\n", result.Diagnostics));
                Assert.That(result.CanCreateClipNow, Is.True);
                Assert.That(result.Readiness, Is.EqualTo(MmdHumanoidClipConversionPlanner.ReadyReadiness));
                Assert.That(result.PmxSourceId, Is.EqualTo(pmxAsset.SourceId));
                Assert.That(result.VmdSourceId, Is.EqualTo(vmdAsset.SourceId));
                Assert.That(result.PmxBoneCount, Is.EqualTo(RequiredBoneNames.Length));
                Assert.That(result.VmdBoneKeyframeCount, Is.GreaterThan(0));
                Assert.That(string.Join("\n", result.Diagnostics),
                    Does.Contain(MmdHumanoidClipConversionPlanner.ImportedPmxHumanoidMappingSource));
            }
            finally
            {
                DestroyOwnedObjects(ownedObjects);
            }
        }

        [Test]
        public void AnalyzePrerequisitesUsesPassedVmdImportCacheWithoutParsingBytes()
        {
            MmdPmxAsset pmxAsset = null!;
            var vmdAsset = ScriptableObject.CreateInstance<MmdVmdAsset>();
            var ownedObjects = new List<UnityEngine.Object> { vmdAsset };
            try
            {
                CreateReadyImportedHumanoidPmx(out pmxAsset, ownedObjects);
                vmdAsset.Initialize(
                    new byte[] { 0x00, 0xDE, 0xAD },
                    "injected-cache.vmd",
                    "injected-cache.vmd",
                    new MmdVmdParseSummary("cache", 240, 120, 15, 4, 8),
                    Array.Empty<string>());

                MmdHumanoidClipConversionPlan result =
                    MmdHumanoidClipConversionPlanner.AnalyzePrerequisites(pmxAsset, vmdAsset);

                Assert.That(result.PrerequisitesReady, Is.True, string.Join("\n", result.Diagnostics));
                Assert.That(result.VmdMaxFrame, Is.EqualTo(240));
                Assert.That(result.VmdBoneKeyframeCount, Is.EqualTo(120));
                Assert.That(result.VmdMorphKeyframeCount, Is.EqualTo(15));
                Assert.That(result.VmdModelKeyframeCount, Is.EqualTo(4));
            }
            finally
            {
                DestroyOwnedObjects(ownedObjects);
            }
        }

        [Test]
        public void AnalyzePrerequisitesRejectsFailedOrStructurallyInvalidVmdCache()
        {
            MmdPmxAsset pmxAsset = null!;
            var failed = ScriptableObject.CreateInstance<MmdVmdAsset>();
            var structural = ScriptableObject.CreateInstance<MmdVmdAsset>();
            var ownedObjects = new List<UnityEngine.Object> { failed, structural };
            try
            {
                CreateReadyImportedHumanoidPmx(out pmxAsset, ownedObjects);
                failed.Initialize(
                    new byte[] { 0x01 }, "failed.vmd", "failed.vmd",
                    new MmdVmdParseSummary("failed", 10, 1, 0, 0, 0),
                    new[] { "Failed to parse VMD during import" });
                structural.Initialize(
                    new byte[] { 0x02 }, "structural.vmd", "structural.vmd",
                    new MmdVmdParseSummary("structural", 30, 5, 1, 0, 1),
                    new[] { "structural: invalid interpolation" });

                MmdHumanoidClipConversionPlan failedResult =
                    MmdHumanoidClipConversionPlanner.AnalyzePrerequisites(pmxAsset, failed);
                MmdHumanoidClipConversionPlan structuralResult =
                    MmdHumanoidClipConversionPlanner.AnalyzePrerequisites(pmxAsset, structural);

                Assert.That(failedResult.PrerequisitesReady, Is.False);
                Assert.That(structuralResult.PrerequisitesReady, Is.False);
                Assert.That(string.Join("\n", failedResult.Diagnostics), Does.Contain("vmd validation failed"));
                Assert.That(string.Join("\n", structuralResult.Diagnostics),
                    Does.Contain("cached structural diagnostics present"));
            }
            finally
            {
                DestroyOwnedObjects(ownedObjects);
            }
        }

        [Test]
        public void AnalyzePrerequisitesRejectsMissingVmdImportCache()
        {
            MmdPmxAsset pmxAsset = null!;
            var vmdAsset = ScriptableObject.CreateInstance<MmdVmdAsset>();
            var ownedObjects = new List<UnityEngine.Object> { vmdAsset };
            try
            {
                CreateReadyImportedHumanoidPmx(out pmxAsset, ownedObjects);
                Assert.That(vmdAsset.ImportSummaryStatus, Is.EqualTo(MmdVmdImportSummaryStatus.NotParsed));

                MmdHumanoidClipConversionPlan result =
                    MmdHumanoidClipConversionPlanner.AnalyzePrerequisites(pmxAsset, vmdAsset);

                Assert.That(result.PrerequisitesReady, Is.False);
                Assert.That(result.CanCreateClipNow, Is.False);
                Assert.That(string.Join("\n", result.Diagnostics), Does.Contain("NotParsed"));
            }
            finally
            {
                DestroyOwnedObjects(ownedObjects);
            }
        }

        private static void CreateReadyImportedHumanoidPmx(
            out MmdPmxAsset pmxAsset,
            List<UnityEngine.Object> ownedObjects)
        {
            var hierarchyRoot = new GameObject("PlannerReadyPmxRoot");
            var modelObject = new GameObject("Model");
            modelObject.transform.SetParent(hierarchyRoot.transform, false);
            SkinnedMeshRenderer renderer = modelObject.AddComponent<SkinnedMeshRenderer>();
            var mesh = new Mesh
            {
                vertices = new[] { new Vector3(-0.5f, 0, 0), new Vector3(0.5f, 0, 0), new Vector3(0, 1, 0) },
                triangles = new[] { 0, 1, 2 },
                bindposes = new Matrix4x4[RequiredBoneNames.Length],
            };
            renderer.sharedMesh = mesh;
            var bones = new Transform[RequiredBoneNames.Length];
            for (int i = 0; i < bones.Length; i++)
            {
                var bone = new GameObject(RequiredBoneNames[i]);
                bone.transform.SetParent(modelObject.transform, false);
                bones[i] = bone.transform;
            }
            renderer.bones = bones;

            pmxAsset = ScriptableObject.CreateInstance<MmdPmxAsset>();
            pmxAsset.Initialize(
                new byte[] { 0x10, 0x20 }, "planner-ready.pmx", "Assets/planner-ready.pmx",
                importedMeshAsset: mesh,
                importedRootAsset: hierarchyRoot,
                hierarchyReadinessValue: MmdImportReadiness.Ready,
                rendererReadinessValue: MmdImportReadiness.Ready,
                boneBindingReadinessValue: MmdImportReadiness.Ready,
                parseSummary: new MmdPmxParseSummary(
                    "planner-ready",
                    vertexCount: 3,
                    indexCount: 3,
                    boneCount: RequiredBoneNames.Length,
                    morphCount: 0,
                    materialCount: 1,
                    diffuseTextureReferenceCount: 0,
                    sphereTextureReferenceCount: 0,
                    toonTextureReferenceCount: 0,
                    transparentMaterialCount: 0,
                    edgeMaterialCount: 0,
                    ikCount: 0,
                    rigidbodyCount: 0,
                    jointCount: 0,
                    boundsMin: new Vector3(-0.5f, 0, 0),
                    boundsMax: new Vector3(0.5f, 1, 0),
                    materialSummaries: Array.Empty<MmdPmxMaterialSummary>()));

            MmdHumanoidProxyRigResult proxyRig = MmdHumanoidProxyRigFactory.CreateProxyRig(pmxAsset);
            Assert.That(proxyRig.ProxyRoot, Is.Not.Null, string.Join("\n", proxyRig.Diagnostics));
            MmdHumanoidAvatarBuildResult avatarResult = MmdHumanoidProxyRigFactory.BuildAvatar(proxyRig);
            Assert.That(avatarResult.Avatar, Is.Not.Null, string.Join("\n", avatarResult.Diagnostics));
            var bindings = new List<MmdHumanoidRetargetBinding>();
            foreach (MmdHumanoidBoneMappingMatch match in proxyRig.Matches)
            {
                proxyRig.BoneMap.TryGetValue(match.HumanBone, out Transform proxyTransform);
                bindings.Add(new MmdHumanoidRetargetBinding(
                    match.HumanBone, match.MmdBoneIndex, proxyTransform, bones[match.MmdBoneIndex]));
            }
            proxyRig.ProxyRoot!.transform.SetParent(hierarchyRoot.transform, false);
            MmdUnityPlaybackController controller = hierarchyRoot.AddComponent<MmdUnityPlaybackController>();
            controller.ConfigureHumanoidRetarget(
                proxyRig.ProxyRoot.transform, bindings, Array.Empty<MmdHumanoidAppendTransformBinding>());
            pmxAsset.ApplyHumanoidAvatarImportSummary(
                "Humanoid", avatarResult.Avatar, MmdHumanoidMappingReadiness.Ready, "test ready");

            ownedObjects.Add(pmxAsset);
            ownedObjects.Add(hierarchyRoot);
            ownedObjects.Add(mesh);
            ownedObjects.Add(proxyRig.ProxyRoot);
            ownedObjects.Add(avatarResult.Avatar!);
        }

        private static void DestroyOwnedObjects(IEnumerable<UnityEngine.Object> objects)
        {
            foreach (UnityEngine.Object obj in objects)
            {
                if (obj != null)
                {
                    UnityEngine.Object.DestroyImmediate(obj);
                }
            }
        }
    }
}
