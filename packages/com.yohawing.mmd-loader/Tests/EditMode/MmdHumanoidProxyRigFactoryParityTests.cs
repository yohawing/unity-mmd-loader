#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Mmd.Parser;
using Object = UnityEngine.Object;

namespace Mmd.Tests
{
    /// <summary>
    /// Parity of the model-definition overload of <see cref="MmdHumanoidProxyRigFactory"/>
    /// (used by tests) with the imported-hierarchy transform path (the only production
    /// path, reached through <c>CreateProxyRig(MmdPmxAsset)</c>).
    ///
    /// Every observable output is compared: readiness, ordered matches, proxy
    /// hierarchy (parent/child and sibling order), per-bone pose and diagnostics.
    /// The model overload delegates to the transform path, so no difference is allowed.
    /// </summary>
    public sealed class MmdHumanoidProxyRigFactoryParityTests
    {
        private const string FixtureDirectory = "Packages/com.yohawing.mmd-loader/Tests/Fixtures/Assets/";
        private const string ImportedHierarchyDiagnostic = "proxy-rig: input=ImportedHierarchy";
        private const string RemovedOriginsDiagnosticPrefix = "proxy-rig: origins found=";
        private const float PositionTolerance = 1e-4f;
        private const float RotationToleranceDegrees = 1e-3f;

        [Test]
        public void ModelPathMatchesImporterEquivalentTransformPathForSyntheticModels(
            [Values("sibling-torso", "serial-chain-without-origins", "missing-required")] string modelName,
            [Values("none", "mixed")] string overrideSet)
        {
            MmdModelDefinition model = CreateSyntheticModel(modelName);
            AssertModelPathMatchesImporterEquivalentTransformPath(
                model,
                CreateOverrides(overrideSet),
                modelName == "missing-required" ? MmdHumanoidMappingReadiness.MissingRequired : MmdHumanoidMappingReadiness.Ready,
                modelName + "/" + overrideSet);
        }

        [Test]
        public void ModelPathMatchesImporterEquivalentTransformPathForPmxFixtureModels(
            [Values("test_semi_basic_bone.pmx", "test_basic_bone.pmx", "test_1bone_cube.pmx")] string fixture,
            [Values("none", "mixed")] string overrideSet)
        {
            MmdPmxAsset asset = LoadFixture(fixture);
            MmdModelDefinition model = asset.LoadModel();
            AssertModelPathMatchesImporterEquivalentTransformPath(
                model,
                CreateOverrides(overrideSet),
                ExpectedFixtureReadiness(fixture),
                fixture + "/" + overrideSet);
        }

        [Test]
        public void ModelPathMatchesProductionPmxAssetPathScaledByImportScale(
            [Values("test_semi_basic_bone.pmx", "test_basic_bone.pmx", "test_1bone_cube.pmx")] string fixture,
            [Values("none", "mixed")] string overrideSet)
        {
            MmdPmxAsset asset = LoadFixture(fixture);
            MmdModelDefinition model = asset.LoadModel();
            IReadOnlyList<MmdHumanoidBoneMappingOverride>? overrides = CreateOverrides(overrideSet);
            string label = fixture + "/" + overrideSet;

            MmdHumanoidProxyRigResult? fromModel = null;
            MmdHumanoidProxyRigResult? fromAsset = null;
            try
            {
                fromModel = MmdHumanoidProxyRigFactory.CreateProxyRig(model, mappingOverrides: overrides);
                fromAsset = MmdHumanoidProxyRigFactory.CreateProxyRig(asset, mappingOverrides: overrides);

                // The production hierarchy is built with the asset import scale; the model
                // path always works in unscaled MMD units. Compare in proxy-root space.
                Assert.That(fromAsset.Readiness, Is.EqualTo(ExpectedFixtureReadiness(fixture)), label + " readiness pin");
                AssertProxyRigsEquivalent(fromModel, fromAsset, asset.ImportScale, label);
                AssertDiagnosticsIdentical(fromModel, fromAsset, label);
                AssertHipsIsAncestorOfSpineWhenMapped(fromAsset, label);
            }
            finally
            {
                DestroyProxy(fromModel);
                DestroyProxy(fromAsset);
            }
        }

        [Test]
        public void SemiBasicFixtureExercisesSiblingTorsoReparenting()
        {
            // Guard that the parity cases above are not a straight-chain false green:
            // in this fixture 上半身 and 下半身 are siblings under センター.
            MmdModelDefinition model = LoadFixture("test_semi_basic_bone.pmx").LoadModel();
            MmdBoneDefinition spine = model.bones.Single(bone => bone.name == "上半身");
            MmdBoneDefinition hips = model.bones.Single(bone => bone.name == "下半身");
            Assert.That(spine.parentIndex, Is.EqualTo(hips.parentIndex));
            Assert.That(spine.parentIndex, Is.Not.EqualTo(hips.index));
        }

        [Test]
        public void EmptyModelWithoutOverridesMatchesTransformPath()
        {
            AssertModelPathMatchesImporterEquivalentTransformPath(
                new MmdModelDefinition(),
                null,
                MmdHumanoidMappingReadiness.NoBones,
                "empty/none");
        }

        [Test]
        public void EmptyModelWithOverridesReportsOverridesLikeTransformPath()
        {
            // Before unification the model path returned before applying overrides when the
            // model had no bones; both paths now apply (and report) them first.
            var model = new MmdModelDefinition();
            var overrides = new[] { new MmdHumanoidBoneMappingOverride("missing-head", HumanBodyBones.Head) };

            MmdHumanoidProxyRigResult fromModel = MmdHumanoidProxyRigFactory.CreateProxyRig(model, mappingOverrides: overrides);
            Transform[] bones = BuildImporterEquivalentHierarchy(model, out GameObject hierarchyRoot);
            try
            {
                MmdHumanoidProxyRigResult fromTransforms = MmdHumanoidProxyRigFactory.CreateProxyRigFromBoneTransforms(
                    bones,
                    hierarchyRoot.transform,
                    mappingOverrides: overrides);

                Assert.That(fromModel.Readiness, Is.EqualTo(MmdHumanoidMappingReadiness.NoBones));
                Assert.That(fromTransforms.Readiness, Is.EqualTo(MmdHumanoidMappingReadiness.NoBones));
                Assert.That(fromModel.ProxyRoot, Is.Null);
                Assert.That(fromTransforms.ProxyRoot, Is.Null);
                Assert.That(fromModel.Matches, Is.Empty);
                Assert.That(fromTransforms.Matches, Is.Empty);
                var expectedDiagnostics = new[]
                {
                    ImportedHierarchyDiagnostic,
                    "no-bones: PMX model has no bones.",
                    "manual-override: ignored missing MMD bone 'missing-head' for Head",
                    "manual-overrides: applied=0 ignored=1",
                };
                Assert.That(fromModel.Diagnostics, Is.EqualTo(expectedDiagnostics));
                Assert.That(fromTransforms.Diagnostics, Is.EqualTo(expectedDiagnostics));
            }
            finally
            {
                Object.DestroyImmediate(hierarchyRoot);
            }
        }

        [Test]
        public void UnsortedBoneListUsesImporterIndexOrderLikeTransformPath()
        {
            // Before unification the model path collected matches in model.bones list order;
            // it now follows the importer hierarchy, which is ordered by bone index.
            MmdModelDefinition sorted = CreateSyntheticModel("sibling-torso");
            var model = new MmdModelDefinition();
            for (int i = sorted.bones.Count - 1; i >= 0; i--)
            {
                model.bones.Add(sorted.bones[i]);
            }

            MmdHumanoidProxyRigResult? fromModel = null;
            MmdHumanoidProxyRigResult? fromTransforms = null;
            GameObject? hierarchyRoot = null;
            try
            {
                fromModel = MmdHumanoidProxyRigFactory.CreateProxyRig(model);
                Transform[] bones = BuildImporterEquivalentHierarchy(model, out hierarchyRoot);
                fromTransforms = MmdHumanoidProxyRigFactory.CreateProxyRigFromBoneTransforms(bones, hierarchyRoot.transform);

                Assert.That(fromModel.Matches.Select(match => match.MmdBoneIndex),
                    Is.Ordered.Ascending, "model path follows importer index order");
                Assert.That(fromModel.Readiness, Is.EqualTo(MmdHumanoidMappingReadiness.Ready));
                AssertProxyRigsEquivalent(fromModel, fromTransforms, 1f, "unsorted");
                AssertDiagnosticsIdentical(fromModel, fromTransforms, "unsorted");
            }
            finally
            {
                DestroyProxy(fromModel);
                DestroyProxy(fromTransforms);
                if (hierarchyRoot != null)
                {
                    Object.DestroyImmediate(hierarchyRoot);
                }
            }
        }

        [Test]
        public void GapAndDuplicateBoneIndicesUseImporterListPosition()
        {
            // Before unification the model path reported MmdBoneDefinition.index as
            // MmdBoneIndex. It now reports the position in the index-ordered bone list, like
            // the transform path, so a gap index (999) no longer leaks into matches.
            // Remaining known difference, for duplicate indices only (never produced by the
            // parser): the importer keeps only the last bone per index for every list slot
            // of that index, so 上半身 is not visible to the transform path, while the model
            // overload keeps each bone as its own slot.
            var gap = new MmdModelDefinition();
            gap.bones.Add(new MmdBoneDefinition { index = 0, name = "下半身", parentIndex = -1 });
            gap.bones.Add(new MmdBoneDefinition { index = 1, name = "上半身", parentIndex = 0 });
            gap.bones.Add(new MmdBoneDefinition { index = 2, name = "首", parentIndex = 1 });
            gap.bones.Add(new MmdBoneDefinition { index = 999, name = "頭", parentIndex = 2 });

            var duplicate = new MmdModelDefinition();
            duplicate.bones.Add(new MmdBoneDefinition { index = 0, name = "下半身", parentIndex = -1 });
            duplicate.bones.Add(new MmdBoneDefinition { index = 1, name = "上半身", parentIndex = 0 });
            duplicate.bones.Add(new MmdBoneDefinition { index = 1, name = "右腕", parentIndex = 0 });
            duplicate.bones.Add(new MmdBoneDefinition { index = 2, name = "首", parentIndex = 1 });

            AssertMatchIndices(gap, "gap",
                expectedModelPath: new[] { "Hips:0", "Spine:1", "Neck:2", "Head:3" },
                expectedTransformPath: new[] { "Hips:0", "Spine:1", "Neck:2", "Head:3" });
            AssertMatchIndices(duplicate, "duplicate",
                expectedModelPath: new[] { "Hips:0", "Spine:1", "RightUpperArm:2", "Neck:3" },
                expectedTransformPath: new[] { "Hips:0", "RightUpperArm:1", "Neck:3" });
        }

        private static void AssertMatchIndices(
            MmdModelDefinition model,
            string label,
            string[] expectedModelPath,
            string[] expectedTransformPath)
        {
            MmdHumanoidProxyRigResult? fromModel = null;
            MmdHumanoidProxyRigResult? fromTransforms = null;
            GameObject? hierarchyRoot = null;
            try
            {
                fromModel = MmdHumanoidProxyRigFactory.CreateProxyRig(model);
                Transform[] bones = BuildImporterEquivalentHierarchy(model, out hierarchyRoot);
                fromTransforms = MmdHumanoidProxyRigFactory.CreateProxyRigFromBoneTransforms(bones, hierarchyRoot.transform);

                Assert.That(DescribeMatchIndices(fromModel), Is.EqualTo(expectedModelPath), label + " model path");
                Assert.That(DescribeMatchIndices(fromTransforms), Is.EqualTo(expectedTransformPath), label + " transform path");
            }
            finally
            {
                DestroyProxy(fromModel);
                DestroyProxy(fromTransforms);
                if (hierarchyRoot != null)
                {
                    Object.DestroyImmediate(hierarchyRoot);
                }
            }
        }

        private static string[] DescribeMatchIndices(MmdHumanoidProxyRigResult result)
        {
            return result.Matches
                .Select(match => match.HumanBone + ":" + match.MmdBoneIndex.ToString(CultureInfo.InvariantCulture))
                .ToArray();
        }

        private static void AssertModelPathMatchesImporterEquivalentTransformPath(
            MmdModelDefinition model,
            IReadOnlyList<MmdHumanoidBoneMappingOverride>? overrides,
            string expectedReadiness,
            string label)
        {
            MmdHumanoidProxyRigResult? fromModel = null;
            MmdHumanoidProxyRigResult? fromTransforms = null;
            GameObject? hierarchyRoot = null;
            try
            {
                fromModel = MmdHumanoidProxyRigFactory.CreateProxyRig(model, mappingOverrides: overrides);
                Transform[] bones = BuildImporterEquivalentHierarchy(model, out hierarchyRoot);
                fromTransforms = MmdHumanoidProxyRigFactory.CreateProxyRigFromBoneTransforms(
                    bones,
                    hierarchyRoot.transform,
                    mappingOverrides: overrides);

                Assert.That(fromTransforms.Readiness, Is.EqualTo(expectedReadiness), label + " readiness pin");
                AssertProxyRigsEquivalent(fromModel, fromTransforms, 1f, label);
                AssertDiagnosticsIdentical(fromModel, fromTransforms, label);
                AssertHipsIsAncestorOfSpineWhenMapped(fromTransforms, label);
            }
            finally
            {
                DestroyProxy(fromModel);
                DestroyProxy(fromTransforms);
                if (hierarchyRoot != null)
                {
                    Object.DestroyImmediate(hierarchyRoot);
                }
            }
        }

        private static void AssertProxyRigsEquivalent(
            MmdHumanoidProxyRigResult fromModel,
            MmdHumanoidProxyRigResult fromTransforms,
            float transformPositionScale,
            string label)
        {
            Assert.That(fromTransforms.Readiness, Is.EqualTo(fromModel.Readiness), label + " readiness");
            Assert.That(DescribeMatches(fromTransforms.Matches), Is.EqualTo(DescribeMatches(fromModel.Matches)),
                label + " ordered matches");
            Assert.That(fromTransforms.ProxyRoot != null, Is.EqualTo(fromModel.ProxyRoot != null), label + " proxy root presence");
            if (fromModel.ProxyRoot == null || fromTransforms.ProxyRoot == null)
            {
                return;
            }

            Assert.That(fromTransforms.ProxyRoot.name, Is.EqualTo(fromModel.ProxyRoot.name), label + " root name");
            Assert.That(fromTransforms.ProxyRoot.hideFlags, Is.EqualTo(fromModel.ProxyRoot.hideFlags), label + " root hideFlags");
            Assert.That(fromTransforms.ProxyRoot.activeSelf, Is.EqualTo(fromModel.ProxyRoot.activeSelf), label + " root active");
            Assert.That(DescribeHierarchy(fromTransforms.ProxyRoot.transform),
                Is.EqualTo(DescribeHierarchy(fromModel.ProxyRoot.transform)), label + " hierarchy");
            AssertBonePosesEquivalent(fromModel, fromTransforms, transformPositionScale, label);
        }

        private static void AssertBonePosesEquivalent(
            MmdHumanoidProxyRigResult fromModel,
            MmdHumanoidProxyRigResult fromTransforms,
            float transformPositionScale,
            string label)
        {
            Transform modelRoot = fromModel.ProxyRoot!.transform;
            Transform transformRoot = fromTransforms.ProxyRoot!.transform;
            Assert.That(fromTransforms.BoneMap.Keys.OrderBy(bone => bone),
                Is.EqualTo(fromModel.BoneMap.Keys.OrderBy(bone => bone)), label + " bone map keys");

            foreach (KeyValuePair<HumanBodyBones, Transform> entry in fromModel.BoneMap)
            {
                Transform expected = entry.Value;
                Transform actual = fromTransforms.BoneMap[entry.Key];
                string boneLabel = label + " " + entry.Key;

                Assert.That(actual.name, Is.EqualTo(expected.name), boneLabel + " name");
                Assert.That(actual.gameObject.hideFlags, Is.EqualTo(expected.gameObject.hideFlags), boneLabel + " hideFlags");
                Assert.That(actual.hideFlags, Is.EqualTo(expected.hideFlags), boneLabel + " transform hideFlags");

                Vector3 expectedPosition = modelRoot.InverseTransformPoint(expected.position) * transformPositionScale;
                Vector3 actualPosition = transformRoot.InverseTransformPoint(actual.position);
                float tolerance = PositionTolerance * Mathf.Max(1f, expectedPosition.magnitude);
                Assert.That(Vector3.Distance(actualPosition, expectedPosition), Is.LessThan(tolerance),
                    boneLabel + " position expected " + Format(expectedPosition) + " actual " + Format(actualPosition));

                Quaternion expectedRotation = Quaternion.Inverse(modelRoot.rotation) * expected.rotation;
                Quaternion actualRotation = Quaternion.Inverse(transformRoot.rotation) * actual.rotation;
                Assert.That(Quaternion.Angle(actualRotation, expectedRotation), Is.LessThan(RotationToleranceDegrees),
                    boneLabel + " rotation");

                Assert.That(Vector3.Distance(actual.localScale, expected.localScale), Is.LessThan(PositionTolerance),
                    boneLabel + " localScale");
            }
        }

        /// <summary>
        /// Before unification the model path omitted "proxy-rig: input=ImportedHierarchy"
        /// and appended "proxy-rig: origins found=N fallback-zero=M". Both paths now emit
        /// the transform path's diagnostics, line for line.
        /// </summary>
        private static void AssertDiagnosticsIdentical(
            MmdHumanoidProxyRigResult fromModel,
            MmdHumanoidProxyRigResult fromTransforms,
            string label)
        {
            Assert.That(fromModel.Diagnostics, Has.Some.EqualTo(ImportedHierarchyDiagnostic), label + " input marker");
            Assert.That(fromModel.Diagnostics.Any(line => line.StartsWith(RemovedOriginsDiagnosticPrefix, StringComparison.Ordinal)),
                Is.False, label + " removed model-only diagnostic");
            Assert.That(fromTransforms.Diagnostics, Is.EqualTo(fromModel.Diagnostics), label + " diagnostics");
        }

        private static void AssertHipsIsAncestorOfSpineWhenMapped(MmdHumanoidProxyRigResult result, string label)
        {
            if (result.BoneMap == null
                || !result.BoneMap.TryGetValue(HumanBodyBones.Hips, out Transform hips)
                || !result.BoneMap.TryGetValue(HumanBodyBones.Spine, out Transform spine))
            {
                return;
            }

            Assert.That(spine.parent, Is.EqualTo(hips), label + " Spine must be reparented under Hips");
        }

        private static string[] DescribeMatches(IReadOnlyList<MmdHumanoidBoneMappingMatch> matches)
        {
            return matches
                .Select(match => match.HumanBone + ":" + match.MmdBoneIndex.ToString(CultureInfo.InvariantCulture)
                                 + ":" + match.MmdBoneName + ":" + (match.Required ? "required" : "optional"))
                .ToArray();
        }

        private static string[] DescribeHierarchy(Transform root)
        {
            var lines = new List<string>();
            AppendHierarchy(root, 0, lines);
            return lines.ToArray();
        }

        private static void AppendHierarchy(Transform parent, int depth, List<string> lines)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                lines.Add(depth.ToString(CultureInfo.InvariantCulture) + ":" + parent.name + ">" + child.name
                          + "#" + i.ToString(CultureInfo.InvariantCulture));
                AppendHierarchy(child, depth + 1, lines);
            }
        }

        /// <summary>
        /// Mirrors MmdUnityModelFactory.BuildBoneTransforms at import scale 1: bones are
        /// ordered by index, parented by parentIndex, positioned at their converted MMD
        /// origin, with identity rotation and unit scale. A missing origin falls back to
        /// zero (the model path's behavior) instead of failing like the importer.
        /// </summary>
        private static Transform[] BuildImporterEquivalentHierarchy(MmdModelDefinition model, out GameObject root)
        {
            root = new GameObject("ImporterEquivalentHierarchy");
            List<MmdBoneDefinition> ordered = model.bones.OrderBy(bone => bone.index).ToList();
            var transformsByIndex = new Dictionary<int, Transform>(ordered.Count);
            var boneByIndex = new Dictionary<int, MmdBoneDefinition>(ordered.Count);
            foreach (MmdBoneDefinition bone in ordered)
            {
                string name = string.IsNullOrWhiteSpace(bone.name) ? "bone_" + bone.index : bone.name;
                // Parent every bone under root immediately so a duplicate index (whose earlier
                // transform is replaced in the map) is still destroyed with root.
                Transform created = new GameObject(name).transform;
                created.SetParent(root.transform, worldPositionStays: false);
                transformsByIndex[bone.index] = created;
                boneByIndex[bone.index] = bone;
            }

            foreach (MmdBoneDefinition bone in ordered)
            {
                Transform boneTransform = transformsByIndex[bone.index];
                Transform parent = root.transform;
                Vector3 parentPosition = Vector3.zero;
                if (bone.parentIndex >= 0 && transformsByIndex.TryGetValue(bone.parentIndex, out Transform parentBone))
                {
                    parent = parentBone;
                    parentPosition = MmdHumanoidProxyRigFactory.MmdOriginToUnityPosition(boneByIndex[bone.parentIndex].origin);
                }

                boneTransform.SetParent(parent, worldPositionStays: false);
                boneTransform.localPosition = MmdHumanoidProxyRigFactory.MmdOriginToUnityPosition(bone.origin) - parentPosition;
                boneTransform.localRotation = Quaternion.identity;
                boneTransform.localScale = Vector3.one;
            }

            return ordered.Select(bone => transformsByIndex[bone.index]).ToArray();
        }

        private static IReadOnlyList<MmdHumanoidBoneMappingOverride>? CreateOverrides(string overrideSet)
        {
            switch (overrideSet)
            {
                case "none":
                    return null;
                case "mixed":
                    return new[]
                    {
                        // Same as automatic where present, otherwise ignored as missing.
                        new MmdHumanoidBoneMappingOverride("上半身2", HumanBodyBones.Chest),
                        // Replaces the automatic LeftHand source.
                        new MmdHumanoidBoneMappingOverride("左手先", HumanBodyBones.LeftHand),
                        // Missing source bone.
                        new MmdHumanoidBoneMappingOverride("missing-jaw-bone", HumanBodyBones.Jaw),
                        // Steals the automatic LeftEye source, suppressing it as a duplicate.
                        new MmdHumanoidBoneMappingOverride("左目", HumanBodyBones.RightEye),
                    };
                default:
                    throw new ArgumentOutOfRangeException(nameof(overrideSet), overrideSet, null);
            }
        }

        private static MmdModelDefinition CreateSyntheticModel(string modelName)
        {
            var model = new MmdModelDefinition();
            switch (modelName)
            {
                case "sibling-torso":
                    // Real-model topology: 上半身 and 下半身 are siblings under 腰, plus helper
                    // bones (center/groove/waist, IK, twist) that must not enter the proxy.
                    AddBone(model, "全ての親", -1, 0f, 0f, 0f);
                    AddBone(model, "センター", 0, 0f, 9f, 0f);
                    AddBone(model, "グルーブ", 1, 0f, 9.2f, 0f);
                    AddBone(model, "腰", 2, 0f, 9.4f, 0.3f);
                    AddBone(model, "下半身", 3, 0f, 9.5f, 0.2f);
                    AddBone(model, "上半身", 3, 0f, 11.8f, 0.1f);
                    AddBone(model, "上半身2", 5, 0f, 13.3f, -0.1f);
                    AddBone(model, "首", 6, 0f, 15.3f, 0.2f);
                    AddBone(model, "頭", 7, 0f, 16.8f, 0.1f);
                    AddBone(model, "左目", 8, 0.3f, 17.2f, -0.6f);
                    AddBone(model, "右目", 8, -0.3f, 17.2f, -0.6f);
                    AddBone(model, "左足", 4, 0.8f, 8.8f, 0.1f);
                    AddBone(model, "左ひざ", 11, 0.9f, 4.8f, -0.2f);
                    AddBone(model, "左足首", 12, 1.0f, 0.8f, 0.5f);
                    AddBone(model, "左つま先", 13, 1.0f, 0.1f, -1.0f);
                    AddBone(model, "右足", 4, -0.8f, 8.8f, 0.1f);
                    AddBone(model, "右ひざ", 15, -0.9f, 4.8f, -0.2f);
                    AddBone(model, "右足首", 16, -1.0f, 0.8f, 0.5f);
                    AddBone(model, "右つま先", 17, -1.0f, 0.1f, -1.0f);
                    AddBone(model, "左肩", 6, 0.3f, 14.4f, 0.2f);
                    AddBone(model, "左腕", 19, 1.6f, 14.0f, 0.3f);
                    AddBone(model, "左腕捩", 20, 2.6f, 12.9f, 0.3f);
                    AddBone(model, "左ひじ", 20, 3.5f, 11.9f, 0.4f);
                    AddBone(model, "左手首", 22, 5.0f, 10.4f, 0.2f);
                    AddBone(model, "左手先", 23, 5.6f, 9.8f, 0.2f);
                    AddBone(model, "左人指１", 23, 5.6f, 9.9f, -0.2f);
                    AddBone(model, "左人指２", 25, 5.9f, 9.6f, -0.2f);
                    AddBone(model, "右肩", 6, -0.3f, 14.4f, 0.2f);
                    AddBone(model, "右腕", 27, -1.6f, 14.0f, 0.3f);
                    AddBone(model, "右ひじ", 28, -3.5f, 11.9f, 0.4f);
                    AddBone(model, "右手首", 29, -5.0f, 10.4f, 0.2f);
                    AddBone(model, "左足ＩＫ", 0, 1.0f, 0.8f, 0.5f);
                    AddBone(model, "右足ＩＫ", 0, -1.0f, 0.8f, 0.5f);
                    break;
                case "serial-chain-without-origins":
                    // All origins missing: both paths must fall back to zero positions.
                    string[] names =
                    {
                        "下半身", "上半身", "首", "頭",
                        "左足", "左ひざ", "左足首", "右足", "右ひざ", "右足首",
                        "左腕", "左ひじ", "左手首", "右腕", "右ひじ", "右手首",
                    };
                    for (int i = 0; i < names.Length; i++)
                    {
                        model.bones.Add(new MmdBoneDefinition { index = i, name = names[i], parentIndex = i - 1 });
                    }

                    break;
                case "missing-required":
                    AddBone(model, "センター", -1, 0f, 9f, 0f);
                    AddBone(model, "下半身", 0, 0f, 9.5f, 0f);
                    AddBone(model, "上半身", 0, 0f, 11.8f, 0f);
                    AddBone(model, "首", 2, 0f, 15.3f, 0f);
                    AddBone(model, "左腕", 2, 1.6f, 14.0f, 0f);
                    AddBone(model, "左ひじ", 4, 3.5f, 11.9f, 0f);
                    AddBone(model, "左手先", 5, 5.6f, 9.8f, 0f);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(modelName), modelName, null);
            }

            return model;
        }

        private static void AddBone(MmdModelDefinition model, string name, int parentIndex, float x, float y, float z)
        {
            model.bones.Add(new MmdBoneDefinition
            {
                index = model.bones.Count,
                name = name,
                parentIndex = parentIndex,
                origin = new[] { x, y, z },
            });
        }

        private static string ExpectedFixtureReadiness(string fixture)
        {
            return fixture == "test_1bone_cube.pmx"
                ? MmdHumanoidMappingReadiness.MissingRequired
                : MmdHumanoidMappingReadiness.Ready;
        }

        private static MmdPmxAsset LoadFixture(string fixture)
        {
            MmdPmxAsset? asset = AssetDatabase.LoadAssetAtPath<MmdPmxAsset>(FixtureDirectory + fixture);
            Assert.That(asset, Is.Not.Null, fixture);
            Assert.That(asset!.ImportedRoot, Is.Not.Null, fixture + " ImportedRoot");
            return asset;
        }

        private static void DestroyProxy(MmdHumanoidProxyRigResult? result)
        {
            if (result?.ProxyRoot != null)
            {
                Object.DestroyImmediate(result.ProxyRoot);
            }
        }

        private static string Format(Vector3 value)
        {
            return value.ToString("F5", CultureInfo.InvariantCulture);
        }
    }
}
