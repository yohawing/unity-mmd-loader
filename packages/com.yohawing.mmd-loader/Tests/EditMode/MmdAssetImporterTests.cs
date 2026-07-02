#nullable enable

using System;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Mmd.Editor;
using Mmd.Parser;
using Mmd.Physics;
using Mmd.UnityIntegration;
using Object = UnityEngine.Object;

namespace Mmd.Tests
{
    public sealed class MmdAssetImporterTests
    {
        private const string TempDirectory = "Assets/__MmdAssetImporterTests";
        private const string TempPmxPath = TempDirectory + "/test_1bone_cube.pmx";
        private const string TempHumanoidPmxPath = TempDirectory + "/test_semi_basic_bone.pmx";
        private const string TempVmdPath = TempDirectory + "/test_1bone_cube_motion.vmd";
        private const string TempSetupPath = TempDirectory + "/test_1bone_cube_playback_setup.asset";
        private const string TempConfigPath = TempDirectory + "/test_playback_config.asset";
        private const string TempHumanoidSetupPath = TempDirectory + "/test_humanoid_setup.asset";
        private const string TempPrefabPath = TempDirectory + "/test_1bone_cube.prefab";
        private const string TempScenePath = TempDirectory + "/test_1bone_cube_scene.unity";
        private const string TempRemapMaterialPath = TempDirectory + "/remapped_body.mat";
        private const int TestOneBoneCubeVertexCount = 14;

        [SetUp]
        public void SetUp()
        {
            DeleteTempDirectory();
        }

        [TearDown]
        public void TearDown()
        {
            DeleteTempDirectory();
        }

        private static void DeleteTempDirectory()
        {
            AssetDatabase.DeleteAsset(TempDirectory);
            string fullTempDirectory = Path.Combine(ProjectRoot, TempDirectory);
            if (Directory.Exists(fullTempDirectory))
            {
                foreach (string path in Directory.EnumerateFileSystemEntries(fullTempDirectory, "*", SearchOption.AllDirectories))
                {
                    File.SetAttributes(path, FileAttributes.Normal);
                }

                Directory.Delete(fullTempDirectory, recursive: true);
            }

            string metaPath = fullTempDirectory + ".meta";
            if (File.Exists(metaPath))
            {
                File.SetAttributes(metaPath, FileAttributes.Normal);
                File.Delete(metaPath);
            }

            AssetDatabase.Refresh();
        }

        [Test]
        public void PmxAndVmdImportAsMmdAssetsAndParse()
        {
            CopyFixtureToAssetDatabase("test_1bone_cube.pmx", TempPmxPath);
            CopyFixtureToAssetDatabase("test_1bone_cube_motion.vmd", TempVmdPath);

            MmdPmxAsset pmxAsset = AssetDatabase.LoadAssetAtPath<MmdPmxAsset>(TempPmxPath);
            MmdVmdAsset vmdAsset = AssetDatabase.LoadAssetAtPath<MmdVmdAsset>(TempVmdPath);

            Assert.That(pmxAsset, Is.Not.Null);
            Assert.That(vmdAsset, Is.Not.Null);
            Assert.That(pmxAsset.SourceId, Is.EqualTo(TempPmxPath));
            Assert.That(vmdAsset.SourceId, Is.EqualTo(TempVmdPath));
            Assert.That(File.Exists(pmxAsset.SourcePath), Is.True);
            Assert.That(File.Exists(vmdAsset.SourcePath), Is.True);
            Assert.That(pmxAsset.ByteLength, Is.GreaterThan(0));
            Assert.That(vmdAsset.ByteLength, Is.GreaterThan(0));

            MmdModelDefinition model = pmxAsset.LoadModel();
            MmdMotionDefinition motion = vmdAsset.LoadMotion();
            Assert.That(model.bones, Has.Count.GreaterThan(0));
            Assert.That(motion.boneKeyframes, Has.Count.GreaterThan(0));
        }

        [Test]
        public void ImportedPmxAssetCarriesImportScaleSummaryFromImporter()
        {
            CopyFixtureToAssetDatabase("test_1bone_cube.pmx", TempPmxPath);

            MmdPmxAsset pmxAsset = AssetDatabase.LoadAssetAtPath<MmdPmxAsset>(TempPmxPath);

            Assert.That(pmxAsset.ImportScale, Is.EqualTo(0.1f).Within(0.0001f));

            // Default PMX import scale is 0.1, and runtime playback/live physics are scale-aware.
            MmdScaleAwarePhysicsReadiness readiness = MmdAssetInspectorUtility.GetScaleAwarePhysicsReadiness(pmxAsset);
            Assert.That(readiness.ImportScale, Is.EqualTo(0.1f).Within(0.0001f));
            Assert.That(readiness.HasPhysicsDescriptors, Is.False);
            Assert.That(readiness.GravityPolicy, Is.EqualTo("scale-aware-mmd-gravity-98"));
            Assert.That(readiness.BackendReadbackSpace, Is.EqualTo(MmdScaleAwarePhysicsReadiness.MmdSpaceReadback));
            Assert.That(readiness.ScaleAwareHandoffReadiness, Is.EqualTo(MmdScaleAwarePhysicsReadiness.ScaleAwareHandoffReady));
            Assert.That(readiness.RequiredSmoke, Is.EqualTo(MmdScaleAwarePhysicsReadiness.ScaleAwareSmokeCovered));
        }

        [Test]
        public void ImportedPmxAssetCarriesImportUiSettingsSummaryFromImporter()
        {
            CopyFixtureToAssetDatabase("test_1bone_cube.pmx", TempPmxPath);

            MmdPmxAsset pmxAsset = AssetDatabase.LoadAssetAtPath<MmdPmxAsset>(TempPmxPath);

            Assert.That(pmxAsset.ModelPreset, Is.EqualTo(nameof(MmdPmxModelPreset.Custom)));
            Assert.That(pmxAsset.MeshGenerationMode, Is.EqualTo(nameof(MmdPmxMeshGenerationMode.SingleMesh)));
            Assert.That(pmxAsset.MaterialTexturePolicy, Is.EqualTo(nameof(MmdPmxMaterialTexturePolicy.ResolveReferencesOnly)));
            Assert.That(pmxAsset.AnimationType, Is.EqualTo(nameof(MmdPmxAnimationType.Generic)));
            Assert.That(pmxAsset.ShaderPreset, Is.EqualTo(nameof(MmdPmxShaderPreset.MmdBasicUrpToon)));
            Assert.That(pmxAsset.ImportedAvatar, Is.Null);
            Assert.That(pmxAsset.HumanoidAvatarReadiness, Is.EqualTo("NotRequested"));
        }

        [Test]
        public void NoneAnimationTypeImportsWithoutAnimator()
        {
            CopyFixtureToAssetDatabase("test_1bone_cube.pmx", TempPmxPath);
            SetPmxImporterAnimationType(TempPmxPath, MmdPmxAnimationType.None);

            GameObject root = AssetDatabase.LoadAssetAtPath<GameObject>(TempPmxPath);
            MmdPmxAsset pmxAsset = AssetDatabase.LoadAssetAtPath<MmdPmxAsset>(TempPmxPath);

            Assert.That(root, Is.Not.Null);
            Assert.That(pmxAsset.AnimationType, Is.EqualTo(nameof(MmdPmxAnimationType.None)));
            Assert.That(root.GetComponent<Animator>(), Is.Null);
            MmdUnityPlaybackController noneController = root.GetComponent<MmdUnityPlaybackController>();
            Assert.That(noneController, Is.Not.Null);
            Assert.That(noneController.ModelAssetSource, Is.SameAs(pmxAsset));
            Assert.That(noneController.HasModelSource, Is.True);
            Assert.That(noneController.IsConfigured, Is.False);
            Assert.That(noneController.HumanoidRetargetEntries, Is.Empty);
            Assert.That(GetAvatarSubAssets(TempPmxPath), Is.Empty);
        }

        [Test]
        public void GenericAnimationTypeImportsRootAnimatorWithGenericAvatar()
        {
            CopyFixtureToAssetDatabase("test_1bone_cube.pmx", TempPmxPath);
            SetPmxImporterAnimationType(TempPmxPath, MmdPmxAnimationType.Generic);

            GameObject root = AssetDatabase.LoadAssetAtPath<GameObject>(TempPmxPath);
            MmdPmxAsset pmxAsset = AssetDatabase.LoadAssetAtPath<MmdPmxAsset>(TempPmxPath);

            Assert.That(root, Is.Not.Null);
            Assert.That(pmxAsset.AnimationType, Is.EqualTo(nameof(MmdPmxAnimationType.Generic)));
            Assert.That(pmxAsset.ImportedAvatar, Is.Null,
                "Generic Avatar belongs to the imported root Animator, not MmdPmxAsset.ImportedAvatar.");

            Animator animator = root.GetComponent<Animator>();
            Assert.That(animator, Is.Not.Null);
            Assert.That(animator.runtimeAnimatorController, Is.Null);
            Assert.That(animator.avatar, Is.Not.Null);
            Assert.That(animator.avatar.isValid, Is.True);
            Assert.That(animator.avatar.isHuman, Is.False);
            MmdUnityPlaybackController genericController = root.GetComponent<MmdUnityPlaybackController>();
            Assert.That(genericController, Is.Not.Null);
            Assert.That(genericController.ModelAssetSource, Is.SameAs(pmxAsset));
            Assert.That(genericController.HasModelSource, Is.True);
            Assert.That(genericController.IsConfigured, Is.False);
            Assert.That(genericController.HumanoidRetargetEntries, Is.Empty);

            System.Collections.Generic.List<Avatar> avatarSubAssets = GetAvatarSubAssets(TempPmxPath);
            Assert.That(avatarSubAssets, Has.Count.EqualTo(1));
            Assert.That(avatarSubAssets[0], Is.SameAs(animator.avatar));
        }

        [Test]
        public void PmxImporterReimportUpdatesImportedAssetSettingsSummary()
        {
            CopyFixtureToAssetDatabase("test_1bone_cube.pmx", TempPmxPath);
            var importer = AssetImporter.GetAtPath(TempPmxPath) as MmdPmxScriptedImporter;
            Assert.That(importer, Is.Not.Null);

            var serializedImporter = new SerializedObject(importer!);
            serializedImporter.FindProperty("importScale").floatValue = 2.5f;
            serializedImporter.FindProperty("modelPreset").enumValueIndex = (int)MmdPmxModelPreset.Stage;
            serializedImporter.FindProperty("meshGenerationMode").enumValueIndex = (int)MmdPmxMeshGenerationMode.SplitByMaterial;
            serializedImporter.FindProperty("materialTexturePolicy").enumValueIndex = (int)MmdPmxMaterialTexturePolicy.ResolveReferencesOnly;
            serializedImporter.FindProperty("animationType").enumValueIndex = (int)MmdPmxAnimationType.Humanoid;
            // This enum currently has one value; add a non-default round-trip assertion when a second preset exists.
            serializedImporter.FindProperty("shaderPreset").enumValueIndex = (int)MmdPmxShaderPreset.MmdBasicUrpToon;
            serializedImporter.ApplyModifiedPropertiesWithoutUndo();
            importer!.SaveAndReimport();

            MmdPmxAsset pmxAsset = AssetDatabase.LoadAssetAtPath<MmdPmxAsset>(TempPmxPath);

            Assert.That(pmxAsset.ImportScale, Is.EqualTo(2.5f).Within(0.0001f));
            Assert.That(pmxAsset.ModelPreset, Is.EqualTo(nameof(MmdPmxModelPreset.Stage)));
            Assert.That(pmxAsset.MeshGenerationMode, Is.EqualTo(nameof(MmdPmxMeshGenerationMode.SplitByMaterial)));
            Assert.That(pmxAsset.MaterialTexturePolicy, Is.EqualTo(nameof(MmdPmxMaterialTexturePolicy.ResolveReferencesOnly)));
            Assert.That(pmxAsset.AnimationType, Is.EqualTo(nameof(MmdPmxAnimationType.Humanoid)));
            Assert.That(pmxAsset.ShaderPreset, Is.EqualTo(nameof(MmdPmxShaderPreset.MmdBasicUrpToon)));
            Assert.That(pmxAsset.HumanoidAvatarReadiness, Is.Not.EqualTo("NotRequested"));
            Assert.That(pmxAsset.ImportSummaryStatus, Is.EqualTo(MmdPmxImportSummaryStatus.Passed));
            Assert.That(pmxAsset.VertexCount, Is.EqualTo(TestOneBoneCubeVertexCount));
            Assert.That(pmxAsset.MaterialCount, Is.EqualTo(1));

            MmdScaleAwarePhysicsReadiness readiness = MmdAssetInspectorUtility.GetScaleAwarePhysicsReadiness(pmxAsset);
            Assert.That(readiness.ImportScale, Is.EqualTo(2.5f).Within(0.0001f));
            Assert.That(pmxAsset.RigidbodyCount, Is.EqualTo(0));
            Assert.That(pmxAsset.JointCount, Is.EqualTo(0));
            Assert.That(readiness.HasPhysicsDescriptors, Is.False);
            Assert.That(readiness.GravityPolicy, Is.EqualTo("scale-aware-mmd-gravity-98"));
            Assert.That(readiness.BackendReadbackSpace, Is.EqualTo(MmdScaleAwarePhysicsReadiness.MmdSpaceReadback));
            Assert.That(readiness.ScaleAwareHandoffReadiness, Is.EqualTo(MmdScaleAwarePhysicsReadiness.ScaleAwareHandoffReady));
            Assert.That(readiness.RequiredSmoke, Is.EqualTo(MmdScaleAwarePhysicsReadiness.ScaleAwareSmokeCovered));

            // Prove imported asset ImportScale flows to scene instantiation (transient bones/physics) and cached mesh.
            MmdUnityModelInstance? loadInstance = null;
            try
            {
                loadInstance = MmdEditorPmxLoader.LoadPmxIntoScene(pmxAsset);
                Assert.That(loadInstance.ImportScale, Is.EqualTo(2.5f).Within(0.0001f));
                SkinnedMeshRenderer renderer = loadInstance.SkinnedMeshRenderer!;
                Assert.That(renderer.sharedMesh, Is.SameAs(pmxAsset.ImportedMesh));
            }
            finally
            {
                if (loadInstance?.Root != null)
                {
                    Object.DestroyImmediate(loadInstance.Root);
                }
            }
        }

        [Test]
        public void PmxImporterNormalizesInvalidImportScaleOnReimport()
        {
            CopyFixtureToAssetDatabase("test_1bone_cube.pmx", TempPmxPath);
            var importer = AssetImporter.GetAtPath(TempPmxPath) as MmdPmxScriptedImporter;
            Assert.That(importer, Is.Not.Null);

            var serializedImporter = new SerializedObject(importer!);
            serializedImporter.FindProperty("importScale").floatValue = -5.0f;
            serializedImporter.ApplyModifiedPropertiesWithoutUndo();
            importer!.SaveAndReimport();

            MmdPmxAsset pmxAsset = AssetDatabase.LoadAssetAtPath<MmdPmxAsset>(TempPmxPath);

            Assert.That(importer.ImportScale, Is.EqualTo(0.1f).Within(0.0001f));
            Assert.That(pmxAsset.ImportScale, Is.EqualTo(0.1f).Within(0.0001f));

            MmdScaleAwarePhysicsReadiness readiness = MmdAssetInspectorUtility.GetScaleAwarePhysicsReadiness(pmxAsset);
            Assert.That(readiness.ImportScale, Is.EqualTo(0.1f).Within(0.0001f));
            Assert.That(readiness.HasPhysicsDescriptors, Is.False);
            Assert.That(readiness.GravityPolicy, Is.EqualTo("scale-aware-mmd-gravity-98"));
            Assert.That(readiness.BackendReadbackSpace, Is.EqualTo(MmdScaleAwarePhysicsReadiness.MmdSpaceReadback));
            Assert.That(readiness.ScaleAwareHandoffReadiness, Is.EqualTo(MmdScaleAwarePhysicsReadiness.ScaleAwareHandoffReady));
            Assert.That(readiness.RequiredSmoke, Is.EqualTo(MmdScaleAwarePhysicsReadiness.ScaleAwareSmokeCovered));
        }

        [Test]
        public void HumanoidAnimationTypeImportsRootAnimatorWithReadyAvatar()
        {
            CopyFixtureToAssetDatabase("test_semi_basic_bone.pmx", TempHumanoidPmxPath);

            var importer = AssetImporter.GetAtPath(TempHumanoidPmxPath) as MmdPmxScriptedImporter;
            Assert.That(importer, Is.Not.Null);

            var serializedImporter = new SerializedObject(importer!);
            serializedImporter.FindProperty("animationType").enumValueIndex = (int)MmdPmxAnimationType.Humanoid;
            serializedImporter.ApplyModifiedPropertiesWithoutUndo();
            importer!.SaveAndReimport();

            MmdPmxAsset pmxAsset = AssetDatabase.LoadAssetAtPath<MmdPmxAsset>(TempHumanoidPmxPath);
            Assert.That(pmxAsset, Is.Not.Null);
            Assert.That(pmxAsset.AnimationType, Is.EqualTo(nameof(MmdPmxAnimationType.Humanoid)));
            Assert.That(pmxAsset.HumanoidAvatarReadiness, Is.EqualTo(MmdHumanoidSetupAsset.ReadyReadiness));
            Assert.That(pmxAsset.HumanoidAvatarDiagnostic, Is.Not.Empty);

            GameObject root = AssetDatabase.LoadAssetAtPath<GameObject>(TempHumanoidPmxPath);
            Assert.That(root, Is.Not.Null);
            Animator animator = root.GetComponent<Animator>();
            Assert.That(animator, Is.Not.Null);
            Assert.That(animator.runtimeAnimatorController, Is.Null);

            System.Collections.Generic.List<Avatar> avatarSubAssets = GetAvatarSubAssets(TempHumanoidPmxPath);

            Assert.That(pmxAsset.ImportedAvatar, Is.Not.Null,
                "Ready Humanoid import must expose the Avatar sub-asset from MmdPmxAsset.");
            Assert.That(avatarSubAssets, Has.Count.EqualTo(1));
            Assert.That(avatarSubAssets[0], Is.SameAs(pmxAsset.ImportedAvatar));
            Assert.That(pmxAsset.ImportedAvatar!.isValid, Is.True);
            Assert.That(pmxAsset.ImportedAvatar.isHuman, Is.True);
            Assert.That(animator.avatar, Is.SameAs(pmxAsset.ImportedAvatar));

            Transform? proxyRoot = root.transform.Find("MmdHumanoidProxyRig");
            Assert.That(proxyRoot, Is.Not.Null,
                "Humanoid import must persist the proxy rig under the imported hierarchy root for Animator binding.");
            Assert.That(proxyRoot!.parent, Is.SameAs(root.transform));
            Assert.That(proxyRoot.gameObject.hideFlags & HideFlags.HideInHierarchy, Is.EqualTo(HideFlags.None),
                "persisted proxy must be visible in hierarchy (not HideInHierarchy)");
            Assert.That(proxyRoot.GetComponentInChildren<SkinnedMeshRenderer>(includeInactive: true), Is.Null,
                "Slice 1 proxy rig must not add a second skinning renderer.");

            MmdUnityPlaybackController controller = root.GetComponent<MmdUnityPlaybackController>();
            Assert.That(controller, Is.Not.Null,
                "Humanoid import must add the playback controller on the imported root.");
            Assert.That(controller.ModelAssetSource, Is.SameAs(pmxAsset));
            Assert.That(controller.HasModelSource, Is.True);
            Assert.That(controller.IsConfigured, Is.False);
            Assert.That(controller.HumanoidProxyRoot, Is.SameAs(proxyRoot));
            Assert.That(controller.HumanoidRetargetEntries, Is.Not.Empty);
            Assert.That(controller.HumanoidAppendEntries, Is.Not.Null);
            foreach (MmdHumanoidRetargetBinding entry in controller.HumanoidRetargetEntries)
            {
                Assert.That(entry.ProxyTransform, Is.Not.Null, entry.HumanBone + " proxy transform");
                Assert.That(entry.NativeTransform, Is.Not.Null, entry.HumanBone + " native transform");
                Assert.That(entry.MmdBoneIndex, Is.GreaterThanOrEqualTo(0), entry.HumanBone + " MMD bone index");
                Assert.That(Quaternion.Angle(entry.ProxyBindLocalRotation, entry.ProxyTransform!.localRotation),
                    Is.LessThan(0.001f),
                    entry.HumanBone + " proxy bind rotation must be captured after Avatar T-pose.");
                Assert.That(Quaternion.Angle(entry.NativeBindLocalRotation, entry.NativeTransform!.localRotation),
                    Is.LessThan(0.001f),
                    entry.HumanBone + " native bind rotation must be captured from the imported hierarchy.");
            }

            foreach (MmdHumanoidAppendTransformBinding entry in controller.HumanoidAppendEntries)
            {
                Assert.That(entry.TargetTransform, Is.Not.Null, "append target transform");
                Assert.That(entry.AppendParentTransform, Is.Not.Null, "append parent transform");
            }

            MmdHumanoidRetargetBinding hipsEntry = default!;
            bool foundHipsEntry = false;
            foreach (MmdHumanoidRetargetBinding entry in controller.HumanoidRetargetEntries)
            {
                if (entry.HumanBone == HumanBodyBones.Hips)
                {
                    hipsEntry = entry;
                    foundHipsEntry = true;
                    break;
                }
            }

            Assert.That(foundHipsEntry, Is.True, "Humanoid import must include a Hips retarget binding.");
            Assert.That(hipsEntry.CopyLocalPosition, Is.True,
                "Hips binding must copy humanoid body translation to the native MMD move bone.");
            Assert.That(hipsEntry.TranslationTargetTransform, Is.Not.Null);
            Assert.That(hipsEntry.TranslationTargetMmdBoneIndex, Is.GreaterThanOrEqualTo(0));
            SkinnedMeshRenderer? smr = root.GetComponentInChildren<SkinnedMeshRenderer>(includeInactive: true);
            Assert.That(smr, Is.Not.Null);
            Transform? centerBone = FindBoneByName(smr!.bones, "センター");
            if (centerBone != null)
            {
                Assert.That(hipsEntry.TranslationTargetTransform, Is.SameAs(centerBone),
                    "センター exists and must be the first Hips translation target choice.");
            }
            else
            {
                Assert.That(IsAcceptedHipsTranslationTargetName(hipsEntry.TranslationTargetTransform!.name), Is.True,
                    "Hips translation target must follow the configured move-bone priority or fallback.");
            }
        }

        [Test]
        public void PmxScriptedImporterVersionIsTwentyTwoForPlaybackControllerRetargetMerge()
        {
            object[] attributes = typeof(MmdPmxScriptedImporter).GetCustomAttributes(
                typeof(ScriptedImporterAttribute),
                inherit: false);

            Assert.That(attributes, Has.Length.EqualTo(1));
            var attribute = (ScriptedImporterAttribute)attributes[0];
            Assert.That(attribute.version, Is.EqualTo(22),
                "PMX importer version must force reimport for playback-controller-owned PMX source and humanoid retarget bindings.");
        }

        [Test]
        public void PmxImporterImportScaleZeroDotOneFlowsToAssetInstanceAndScalesMeshBounds()
        {
            CopyFixtureToAssetDatabase("test_1bone_cube.pmx", TempPmxPath);
            var importer = AssetImporter.GetAtPath(TempPmxPath) as MmdPmxScriptedImporter;
            Assert.That(importer, Is.Not.Null);

            var serializedImporter = new SerializedObject(importer!);
            serializedImporter.FindProperty("importScale").floatValue = 0.1f;
            serializedImporter.ApplyModifiedPropertiesWithoutUndo();
            importer!.SaveAndReimport();

            MmdPmxAsset? pmxAsset = AssetDatabase.LoadAssetAtPath<MmdPmxAsset>(TempPmxPath);
            Assert.That(pmxAsset, Is.Not.Null);
            MmdPmxAsset loadedPmxAsset = pmxAsset!;
            Assert.That(loadedPmxAsset.ImportScale, Is.EqualTo(0.1f).Within(0.0001f));

            // Load the model to compute unscaled MMD bounds.
            MmdModelDefinition model = loadedPmxAsset.LoadModel();
            Bounds mmdBounds = CalculateMmdBounds(model);

            // Load into scene through the editor PMX loader.
            MmdUnityModelInstance? loadInstance = null;
            try
            {
                loadInstance = MmdEditorPmxLoader.LoadPmxIntoScene(loadedPmxAsset);
                Assert.That(loadInstance.ImportScale, Is.EqualTo(0.1f).Within(0.0001f));

                // Importer-cached Mesh sub-asset carries the scale in its bounds.
                Mesh? importedMesh = loadedPmxAsset.ImportedMesh;
                Assert.That(importedMesh, Is.Not.Null);
                Mesh mesh = importedMesh!;

                // MMD bounds -> Unity bounds transform:
                //   Unity center = (-MMD_center.x * scale, MMD_center.y * scale, -MMD_center.z * scale)
                //   Unity size   = MMD_size * scale
                float scale = 0.1f;
                Vector3 expectedCenter = new Vector3(
                    -mmdBounds.center.x * scale,
                    mmdBounds.center.y * scale,
                    -mmdBounds.center.z * scale);
                Vector3 expectedSize = mmdBounds.size * scale;

                Bounds importedBounds = mesh.bounds;
                Assert.That(importedBounds.size.x, Is.EqualTo(expectedSize.x).Within(0.001f));
                Assert.That(importedBounds.size.y, Is.EqualTo(expectedSize.y).Within(0.001f));
                Assert.That(importedBounds.size.z, Is.EqualTo(expectedSize.z).Within(0.001f));
                Assert.That(importedBounds.center.x, Is.EqualTo(expectedCenter.x).Within(0.001f));
                Assert.That(importedBounds.center.y, Is.EqualTo(expectedCenter.y).Within(0.001f));
                Assert.That(importedBounds.center.z, Is.EqualTo(expectedCenter.z).Within(0.001f));

                // Scene instance references the same importer-owned mesh sub-asset.
                SkinnedMeshRenderer renderer = loadInstance.SkinnedMeshRenderer!;
                Assert.That(renderer.sharedMesh, Is.SameAs(mesh));
            }
            finally
            {
                if (loadInstance?.Root != null)
                {
                    Object.DestroyImmediate(loadInstance.Root);
                }
            }
        }

        [Test]
        public void ImportedPmxAssetCarriesCachedParseSummaryFromImporter()
        {
            CopyFixtureToAssetDatabase("test_1bone_cube.pmx", TempPmxPath);

            MmdPmxAsset pmxAsset = AssetDatabase.LoadAssetAtPath<MmdPmxAsset>(TempPmxPath);

            Assert.That(pmxAsset.ImportSummaryStatus, Is.EqualTo(MmdPmxImportSummaryStatus.Passed));
            Assert.That(pmxAsset.ModelName, Is.Not.Null);
            Assert.That(pmxAsset.VertexCount, Is.EqualTo(TestOneBoneCubeVertexCount));
            Assert.That(pmxAsset.IndexCount, Is.EqualTo(36));
            Assert.That(pmxAsset.BoneCount, Is.EqualTo(1));
            Assert.That(pmxAsset.MaterialCount, Is.EqualTo(1));
            Assert.That(pmxAsset.MorphCount, Is.GreaterThanOrEqualTo(0));
            Assert.That(pmxAsset.IkCount, Is.GreaterThanOrEqualTo(0));
            Assert.That(pmxAsset.RigidbodyCount, Is.GreaterThanOrEqualTo(0));
            Assert.That(pmxAsset.JointCount, Is.GreaterThanOrEqualTo(0));
        }

        [Test]
        public void PmxImporterGeneratesMeshAndMaterialSubAssets()
        {
            CopyFixtureToAssetDatabase("test_1bone_cube.pmx", TempPmxPath);

            MmdPmxAsset pmxAsset = AssetDatabase.LoadAssetAtPath<MmdPmxAsset>(TempPmxPath);

            // D1 contract evidence via LoadAllAssetsAtPath:
            // - main object under .pmx is the importer-owned GameObject hierarchy root
            // - MmdPmxAsset is a metadata sub-asset (resolves via LoadAssetAtPath<MmdPmxAsset>)
            // - exactly one importer-owned Mesh sub-asset (for this fixture)
            // - zero or more Material sub-assets
            Object[] allSubAssets = AssetDatabase.LoadAllAssetsAtPath(TempPmxPath);
            Assert.That(allSubAssets, Is.Not.Null);
            int pmxAssetCount = 0;
            int gameObjectCount = 0;
            int meshSubCount = 0;
            int materialSubCount = 0;
            int textureSubCount = 0;
            foreach (Object o in allSubAssets)
            {
                if (o is MmdPmxAsset) pmxAssetCount++;
                else if (o is GameObject) gameObjectCount++;
                else if (o is Mesh) meshSubCount++;
                else if (o is Material) materialSubCount++;
                else if (o is Texture2D) textureSubCount++;
            }
            Assert.That(pmxAssetCount, Is.EqualTo(1), "MmdPmxAsset must exist as a sub-asset");
            Assert.That(gameObjectCount, Is.GreaterThanOrEqualTo(1), "at least one GameObject in sub-assets (hierarchy root main object)");
            Assert.That(meshSubCount, Is.EqualTo(1));
            Assert.That(materialSubCount, Is.GreaterThanOrEqualTo(0)); // fixture creates 1
            Assert.That(textureSubCount, Is.EqualTo(0), "no Texture sub-assets under .pmx");

            Assert.That(pmxAsset.ImportedMesh, Is.Not.Null);
            Mesh importedMesh = pmxAsset.ImportedMesh!;
            Assert.That(AssetDatabase.Contains(importedMesh), Is.True);
            Assert.That(AssetDatabase.GetAssetPath(importedMesh), Is.EqualTo(TempPmxPath));
            Assert.That(importedMesh.vertexCount, Is.EqualTo(pmxAsset.VertexCount));
            Assert.That(importedMesh.subMeshCount, Is.GreaterThanOrEqualTo(1));
            Assert.That(pmxAsset.ImportedMaterials, Has.Length.EqualTo(pmxAsset.MaterialCount));
            Assert.That(pmxAsset.ImportedMaterials[0], Is.Not.Null);
            Assert.That(AssetDatabase.Contains(pmxAsset.ImportedMaterials[0]), Is.True);
            Assert.That(AssetDatabase.GetAssetPath(pmxAsset.ImportedMaterials[0]), Is.EqualTo(TempPmxPath));
        }

        [Test]
        public void ImportedPmxAssetCarriesGameObjectHierarchySubAsset()
        {
            CopyFixtureToAssetDatabase("test_1bone_cube.pmx", TempPmxPath);

            MmdPmxAsset pmxAsset = AssetDatabase.LoadAssetAtPath<MmdPmxAsset>(TempPmxPath);
            Object[] allSubAssets = AssetDatabase.LoadAllAssetsAtPath(TempPmxPath);

            // Count all GameObjects in sub-assets (root + Model + bones)
            GameObject? hierarchyRoot = null;
            int gameObjectCount = 0;
            foreach (Object o in allSubAssets)
            {
                if (o is GameObject go)
                {
                    gameObjectCount++;
                    // The top-level root has no parent Transform
                    if (go.transform.parent == null)
                    {
                        hierarchyRoot = go;
                    }
                }
            }

            Assert.That(gameObjectCount, Is.GreaterThanOrEqualTo(1),
                "at least one GameObject hierarchy sub-asset under .pmx");
            Assert.That(hierarchyRoot, Is.Not.Null,
                "top-level root GameObject must exist among sub-assets");
            Assert.That(pmxAsset.ImportedRoot, Is.Not.Null);
            Assert.That(pmxAsset.ImportedRoot, Is.SameAs(hierarchyRoot),
                "pmxAsset.ImportedRoot must reference the top-level hierarchy sub-asset from LoadAll");

            // D1: LoadAssetAtPath<GameObject> returns the hierarchy root main object.
            GameObject? mainGameObject = AssetDatabase.LoadAssetAtPath<GameObject>(TempPmxPath);
            Assert.That(mainGameObject, Is.Not.Null,
                "LoadAssetAtPath<GameObject> must return the imported hierarchy root main object");
            Assert.That(mainGameObject, Is.SameAs(hierarchyRoot),
                "GameObject main object must match the hierarchy root sub-asset");
        }

        [Test]
        public void D1_LoadAssetAtPathGameObjectReturnsMainObjectHierarchyRoot()
        {
            // D1: The .pmx main object is the importer-owned hierarchy root GameObject.
            // AssetDatabase.LoadAssetAtPath<GameObject> must resolve it.
            CopyFixtureToAssetDatabase("test_1bone_cube.pmx", TempPmxPath);

            GameObject? mainGo = AssetDatabase.LoadAssetAtPath<GameObject>(TempPmxPath);
            Assert.That(mainGo, Is.Not.Null,
                "LoadAssetAtPath<GameObject> must return the .pmx hierarchy root");
            Assert.That(mainGo.transform.parent, Is.Null,
                "main GameObject must be a root transform (no parent)");

            // The GameObject main object is the same as pmxAsset.ImportedRoot
            MmdPmxAsset pmxAsset = AssetDatabase.LoadAssetAtPath<MmdPmxAsset>(TempPmxPath);
            Assert.That(pmxAsset, Is.Not.Null,
                "LoadAssetAtPath<MmdPmxAsset> still resolves the metadata sub-asset");
            Assert.That(pmxAsset.ImportedRoot, Is.SameAs(mainGo),
                "pmxAsset.ImportedRoot must be the same as the GameObject main object");
        }

        [Test]
        public void D1_LoadAssetAtPathMmdPmxAssetStillReturnsMetadataSubAsset()
        {
            // D1 backward compatibility: AssetDatabase.LoadAssetAtPath<MmdPmxAsset> continues to work.
            CopyFixtureToAssetDatabase("test_1bone_cube.pmx", TempPmxPath);

            MmdPmxAsset? pmxAsset = AssetDatabase.LoadAssetAtPath<MmdPmxAsset>(TempPmxPath);
            Assert.That(pmxAsset, Is.Not.Null,
                "Legacy LoadAssetAtPath<MmdPmxAsset> must still resolve the metadata sub-asset");
            Assert.That(pmxAsset.VertexCount, Is.GreaterThan(0),
                "retrieved MmdPmxAsset must carry valid parse data");
        }

        [Test]
        public void D1_MetadataImportedRootIsGameObjectMainObject()
        {
            // D1: metadata.ImportedRoot is the GameObject main object.
            CopyFixtureToAssetDatabase("test_1bone_cube.pmx", TempPmxPath);

            MmdPmxAsset pmxAsset = AssetDatabase.LoadAssetAtPath<MmdPmxAsset>(TempPmxPath);
            GameObject? mainGo = AssetDatabase.LoadAssetAtPath<GameObject>(TempPmxPath);

            Assert.That(pmxAsset.ImportedRoot, Is.Not.Null);
            Assert.That(mainGo, Is.Not.Null);
            Assert.That(pmxAsset.ImportedRoot, Is.SameAs(mainGo),
                "metadata.ImportedRoot must be the GameObject main object");
        }

        [Test]
        public void D1_SubAssetCountsRemainStable()
        {
            // D1: Mesh/Material/MmdPmxAsset counts remain stable after main object swap.
            CopyFixtureToAssetDatabase("test_1bone_cube.pmx", TempPmxPath);

            Object[] allSubAssets = AssetDatabase.LoadAllAssetsAtPath(TempPmxPath);
            int pmxAssetCount = 0;
            int gameObjectCount = 0;
            int meshCount = 0;
            int materialCount = 0;
            foreach (Object o in allSubAssets)
            {
                if (o is MmdPmxAsset) pmxAssetCount++;
                else if (o is GameObject) gameObjectCount++;
                else if (o is Mesh) meshCount++;
                else if (o is Material) materialCount++;
            }

            Assert.That(pmxAssetCount, Is.EqualTo(1), "exactly 1 MmdPmxAsset sub-asset");
            Assert.That(gameObjectCount, Is.GreaterThanOrEqualTo(1), "at least 1 GameObject (hierarchy root)");
            Assert.That(meshCount, Is.EqualTo(1), "exactly 1 Mesh sub-asset");
            Assert.That(materialCount, Is.GreaterThanOrEqualTo(1), "at least 1 Material sub-asset");
        }

        [Test]
        public void D1_VmdImportBoundary_PmxImportDoesNotCreateVmdOrTimelineSubAssets()
        {
            // VMD import experience Slice 0: importing a PMX must NOT auto-create VMD,
            // TimelineAsset, MmdVmdTimelineClip, MmdVmdTimelineTrack, or other VMD/Timeline
            // sub-assets under the .pmx import path.
            // Uses type-name checks for MMD Timeline runtime/editor types so this test stays
            // in the importer test assembly while still pinning the sub-asset boundary.
            CopyFixtureToAssetDatabase("test_1bone_cube.pmx", TempPmxPath);
            MmdPmxAsset? pmxAsset = AssetDatabase.LoadAssetAtPath<MmdPmxAsset>(TempPmxPath);
            Assert.That(pmxAsset, Is.Not.Null, "precondition: PMX imports as MmdPmxAsset");

            Object[] allSubAssets = AssetDatabase.LoadAllAssetsAtPath(TempPmxPath);
            Assert.That(allSubAssets, Is.Not.Null.And.Not.Empty, "sub-assets exist under .pmx");

            int vmdAssetCount = 0;
            int timelineAssetCount = 0;
            int vmdTimelineClipCount = 0;
            int vmdTimelineTrackCount = 0;
            int vmdTimelineBehaviourCount = 0;
            int animationClipCount = 0;
            int otherVmdTimelineCount = 0;

            foreach (Object o in allSubAssets)
            {
                if (o is MmdVmdAsset) vmdAssetCount++;
                else if (o is UnityEngine.Timeline.TimelineAsset) timelineAssetCount++;
                else if (o.GetType().Name == "MmdVmdTimelineClip") vmdTimelineClipCount++;
                else if (o.GetType().Name == "MmdVmdTimelineTrack") vmdTimelineTrackCount++;
                else if (o.GetType().Name == "MmdVmdTimelineBehaviour") vmdTimelineBehaviourCount++;
                else if (o is AnimationClip) animationClipCount++;
                else if (o.GetType().Name.StartsWith("MmdVmd", StringComparison.Ordinal) ||
                         o.GetType().Name.StartsWith("Timeline", StringComparison.Ordinal))
                {
                    otherVmdTimelineCount++;
                }
            }

            Assert.That(vmdAssetCount, Is.EqualTo(0),
                "PMX import must not create MmdVmdAsset sub-assets");
            Assert.That(timelineAssetCount, Is.EqualTo(0),
                "PMX import must not create TimelineAsset sub-assets");
            Assert.That(vmdTimelineClipCount, Is.EqualTo(0),
                "PMX import must not create MmdVmdTimelineClip sub-assets");
            Assert.That(vmdTimelineTrackCount, Is.EqualTo(0),
                "PMX import must not create MmdVmdTimelineTrack sub-assets");
            Assert.That(vmdTimelineBehaviourCount, Is.EqualTo(0),
                "PMX import must not create MmdVmdTimelineBehaviour sub-assets");
            Assert.That(otherVmdTimelineCount, Is.EqualTo(0),
                "PMX import must not create other VMD/Timeline sub-assets");
            Assert.That(animationClipCount, Is.EqualTo(0),
                "PMX import must not create AnimationClip sub-assets (VMD import experience D3/Slice 4)");

            // Sanity: normal PMX sub-assets still present
            Assert.That(pmxAsset.ImportedMesh, Is.Not.Null);
            Assert.That(pmxAsset.ImportedMaterials, Is.Not.Null.And.Not.Empty);
            Assert.That(pmxAsset.ImportedRoot, Is.Not.Null);
        }

        [Test]
        public void D1_VmdImportBoundary_VmdImportDoesNotCreateAnimationClip()
        {
            // VMD import experience Slice 4: importing a VMD must NOT auto-create any
            // UnityEngine.AnimationClip sub-assets under the .vmd import path.
            // Humanoid AnimationClip bake is an explicit opt-in (H6 slice 1/2),
            // never automatic at import time.
            CopyFixtureToAssetDatabase("test_1bone_cube_motion.vmd", TempVmdPath);
            MmdVmdAsset? vmdAsset = AssetDatabase.LoadAssetAtPath<MmdVmdAsset>(TempVmdPath);
            Assert.That(vmdAsset, Is.Not.Null, "precondition: VMD imports as MmdVmdAsset");

            Object[] allSubAssets = AssetDatabase.LoadAllAssetsAtPath(TempVmdPath);
            Assert.That(allSubAssets, Is.Not.Null.And.Not.Empty, "sub-assets exist under .vmd");

            int animationClipCount = 0;
            foreach (Object o in allSubAssets)
            {
                if (o is AnimationClip) animationClipCount++;
            }

            Assert.That(animationClipCount, Is.EqualTo(0),
                "VMD import must not create AnimationClip sub-assets (VMD import experience D3/Slice 4)");

            // Sanity: main MmdVmdAsset is present, no spurious extra assets
            Assert.That(vmdAsset.ByteLength, Is.GreaterThan(0));
        }

        [Test]
        public void ImportedPmxHierarchyCarriesSkinnedMeshRendererWithStableAssetReferences()
        {
            CopyFixtureToAssetDatabase("test_1bone_cube.pmx", TempPmxPath);

            MmdPmxAsset? loadedPmxAsset = AssetDatabase.LoadAssetAtPath<MmdPmxAsset>(TempPmxPath);
            Assert.That(loadedPmxAsset, Is.Not.Null);
            MmdPmxAsset pmxAsset = loadedPmxAsset!;
            Assert.That(pmxAsset.ImportedRoot, Is.Not.Null);

            GameObject importedRoot = pmxAsset.ImportedRoot!;
            SkinnedMeshRenderer? nullableSmr = importedRoot.GetComponentInChildren<SkinnedMeshRenderer>(includeInactive: true);
            Assert.That(nullableSmr, Is.Not.Null, "imported hierarchy must contain a SkinnedMeshRenderer");
            SkinnedMeshRenderer smr = nullableSmr!;

            // sharedMesh must match the importer-owned Mesh sub-asset
            Assert.That(smr.sharedMesh, Is.Not.Null);
            Assert.That(smr.sharedMesh, Is.SameAs(pmxAsset.ImportedMesh),
                "renderer sharedMesh must be the importer-owned Mesh sub-asset");

            // sharedMaterials must match the importer-owned Material sub-assets
            Assert.That(smr.sharedMaterials, Is.Not.Null);
            Assert.That(smr.sharedMaterials, Has.Length.EqualTo(pmxAsset.ImportedMaterials.Length));
            for (int i = 0; i < smr.sharedMaterials.Length; i++)
            {
                Assert.That(smr.sharedMaterials[i], Is.SameAs(pmxAsset.ImportedMaterials[i]),
                    $"renderer sharedMaterial[{i}] must be importer-owned Material sub-asset");
            }

            // bones must be stable for the 1bone fixture
            Assert.That(smr.bones, Is.Not.Null);
            Assert.That(smr.bones, Has.Length.EqualTo(pmxAsset.BoneCount),
                "renderer bone count must match PMX bone count");
            Assert.That(smr.bones[0], Is.Not.Null);
            Assert.That(smr.bones[0].name, Is.Not.Null.Or.Empty);

            // rootBone must be set
            Assert.That(smr.rootBone, Is.Not.Null);
            Assert.That(smr.rootBone, Is.SameAs(smr.bones[0]));
        }

        [Test]
        public void ImportedPmxHierarchySurvivesSaveAndReimport()
        {
            CopyFixtureToAssetDatabase("test_1bone_cube.pmx", TempPmxPath);

            // Get initial state
            MmdPmxAsset? loadedPmxAsset = AssetDatabase.LoadAssetAtPath<MmdPmxAsset>(TempPmxPath);
            Assert.That(loadedPmxAsset, Is.Not.Null);
            MmdPmxAsset pmxAsset = loadedPmxAsset!;
            Assert.That(pmxAsset.ImportedRoot, Is.Not.Null);
            SkinnedMeshRenderer? nullableSmrBefore = pmxAsset.ImportedRoot!.GetComponentInChildren<SkinnedMeshRenderer>(includeInactive: true);
            Assert.That(nullableSmrBefore, Is.Not.Null);
            int boneCountBefore = pmxAsset.BoneCount;

            // Trigger reimport
            var importer = AssetImporter.GetAtPath(TempPmxPath) as MmdPmxScriptedImporter;
            Assert.That(importer, Is.Not.Null);
            importer!.SaveAndReimport();

            // Load fresh after reimport
            MmdPmxAsset? loadedPmxAssetAfter = AssetDatabase.LoadAssetAtPath<MmdPmxAsset>(TempPmxPath);
            Assert.That(loadedPmxAssetAfter, Is.Not.Null);
            MmdPmxAsset pmxAssetAfter = loadedPmxAssetAfter!;
            Assert.That(pmxAssetAfter.ImportedRoot, Is.Not.Null,
                "ImportedRoot must survive SaveAndReimport");

            SkinnedMeshRenderer? nullableSmrAfter = pmxAssetAfter.ImportedRoot!.GetComponentInChildren<SkinnedMeshRenderer>(includeInactive: true);
            Assert.That(nullableSmrAfter, Is.Not.Null,
                "SkinnedMeshRenderer must survive SaveAndReimport");
            SkinnedMeshRenderer smrAfter = nullableSmrAfter!;

            // Bone count preserved
            Assert.That(pmxAssetAfter.BoneCount, Is.EqualTo(boneCountBefore),
                "bone count must be stable after reimport");
            Assert.That(smrAfter.bones, Has.Length.EqualTo(boneCountBefore));

            // Mesh reference preserved
            Assert.That(smrAfter.sharedMesh, Is.Not.Null);
            Assert.That(smrAfter.sharedMesh, Is.SameAs(pmxAssetAfter.ImportedMesh),
                "renderer sharedMesh must still reference the importer-owned Mesh sub-asset after reimport");

            // Material references preserved
            Assert.That(smrAfter.sharedMaterials, Is.Not.Null);
            Assert.That(smrAfter.sharedMaterials, Has.Length.EqualTo(pmxAssetAfter.ImportedMaterials.Length));
            for (int i = 0; i < smrAfter.sharedMaterials.Length; i++)
            {
                Assert.That(smrAfter.sharedMaterials[i], Is.SameAs(pmxAssetAfter.ImportedMaterials[i]),
                    $"renderer sharedMaterial[{i}] must still reference importer-owned sub-asset after reimport");
            }

            // rootBone preserved
            Assert.That(smrAfter.rootBone, Is.Not.Null);

            // All assets are still under the same .pmx path
            Assert.That(AssetDatabase.GetAssetPath(pmxAssetAfter.ImportedRoot), Is.EqualTo(TempPmxPath));
            Assert.That(AssetDatabase.GetAssetPath(smrAfter.sharedMesh), Is.EqualTo(TempPmxPath));
        }

        [Test]
        public void LoadPmxAssetIntoSceneUsesImporterGeneratedMeshAndMaterials()
        {
            CopyFixtureToAssetDatabase("test_1bone_cube.pmx", TempPmxPath);
            MmdPmxAsset pmxAsset = AssetDatabase.LoadAssetAtPath<MmdPmxAsset>(TempPmxPath);
            MmdUnityModelInstance? instance = null;
            try
            {
                instance = MmdEditorPmxLoader.LoadPmxIntoScene(pmxAsset);

                Assert.That(instance.SkinnedMeshRenderer, Is.Not.Null);
                Assert.That(instance.SkinnedMeshRenderer!.sharedMesh, Is.SameAs(pmxAsset.ImportedMesh));
                Assert.That(instance.Mesh, Is.SameAs(pmxAsset.ImportedMesh));
                Assert.That(AssetDatabase.Contains(instance.SkinnedMeshRenderer.sharedMesh), Is.True);
                // LoadAll evidence: the referenced mesh/materials are importer sub-assets (not transients).
                Object[] allAtPath = AssetDatabase.LoadAllAssetsAtPath(TempPmxPath);
                bool meshIsImporterSubAsset = false;
                foreach (Object o in allAtPath)
                {
                    if (o is Mesh m && m == instance.SkinnedMeshRenderer.sharedMesh) { meshIsImporterSubAsset = true; break; }
                }
                Assert.That(meshIsImporterSubAsset, Is.True);

                // Scene placement must persist imported PMX material sub-asset references so Play/domain reload
                // cannot turn the renderer slots into Missing references.
                Assert.That(pmxAsset.ImportedMaterials, Is.Not.Null.And.Not.Empty);
                Material importerMat0 = pmxAsset.ImportedMaterials[0];
                Material sceneSharedMat0 = instance.SkinnedMeshRenderer.sharedMaterials[0];
                Assert.That(sceneSharedMat0, Is.SameAs(importerMat0),
                    "scene renderer material must be the importer-owned Material sub-asset for persistence");
                Assert.That(AssetDatabase.Contains(sceneSharedMat0), Is.True,
                    "normal Load PMX Into Scene placement must use AssetDatabase-persisted importer Material sub-assets");
                bool sceneMatIsImporterSubAsset = false;
                foreach (Object o in allAtPath)
                {
                    if (o is Material m && m == sceneSharedMat0) { sceneMatIsImporterSubAsset = true; break; }
                }
                Assert.That(sceneMatIsImporterSubAsset, Is.True,
                    "scene material must be enumerated among the importer sub-assets under the .pmx (LoadAll cross-check)");

                Assert.That(instance.SkinnedMeshRenderer.sharedMaterials, Has.Length.EqualTo(instance.Materials.Length));
                Assert.That(instance.SkinnedMeshRenderer.sharedMaterials[0], Is.SameAs(instance.Materials[0]));
                Assert.That(instance.TextureDiagnostics, Is.Not.Null);

                MmdUnityPlaybackController controller = instance.Root.GetComponent<MmdUnityPlaybackController>();
                Assert.That(controller, Is.Not.Null);
                Assert.That(controller.IsConfigured, Is.False);
                Assert.That(controller.HasModelSource, Is.True);
                Assert.That(controller.ModelSourceId, Is.EqualTo(pmxAsset.SourceId));

                Assert.That(controller.ModelAssetSource, Is.SameAs(pmxAsset));
            }
            finally
            {
                if (instance?.Root != null)
                {
                    Object.DestroyImmediate(instance.Root);
                }
            }
        }

        [Test]
        public void LoadPmxAssetIntoSceneUsesImportedHierarchyWhenImportedRootIsNonNull()
        {
            // Slice B: when pmxAsset.ImportedRoot is available, LoadPmxIntoScene must instantiate
            // the imported hierarchy (not the same object, same hierarchy shape), use the importer's
            // Mesh and Material sub-assets, produce no 'Split Runtime' mesh, and keep
            // Controller model source behavior through MmdEditorPmxLoader.
            CopyFixtureToAssetDatabase("test_1bone_cube.pmx", TempPmxPath);
            MmdPmxAsset? loadedPmxAsset = AssetDatabase.LoadAssetAtPath<MmdPmxAsset>(TempPmxPath);
            Assert.That(loadedPmxAsset, Is.Not.Null);
            MmdPmxAsset pmxAsset = loadedPmxAsset!;
            Assert.That(pmxAsset.ImportedRoot, Is.Not.Null,
                "test fixture must have an ImportedRoot hierarchy sub-asset");

            GameObject originalRoot = pmxAsset.ImportedRoot!;
            MmdUnityModelInstance? instance = null;
            try
            {
                instance = MmdEditorPmxLoader.LoadPmxIntoScene(pmxAsset);
                GameObject root = instance.Root!;

                // Instantiated (not same object).
                Assert.That(root, Is.Not.SameAs(originalRoot),
                    "scene root must be an instantiated copy, not the original imported sub-asset");
                Assert.That(root.name, Is.EqualTo(originalRoot.name),
                    "scene root name must match the imported root name");

                // Same hierarchy shape: same bone count, SMR exists.
                SkinnedMeshRenderer? nullableSmr = instance.SkinnedMeshRenderer;
                Assert.That(nullableSmr, Is.Not.Null,
                    "instantiated hierarchy must contain a SkinnedMeshRenderer");
                SkinnedMeshRenderer smr = nullableSmr!;
                Assert.That(smr, Is.Not.Null,
                    "instantiated hierarchy must contain a SkinnedMeshRenderer");
                Assert.That(smr.bones, Is.Not.Null);
                Assert.That(smr.bones, Has.Length.EqualTo(pmxAsset.BoneCount),
                    "bone count must match the PMX descriptor");

                // Uses pmxAsset.ImportedMesh (not a newly built mesh).
                Assert.That(smr.sharedMesh, Is.SameAs(pmxAsset.ImportedMesh),
                    "scene SkinnedMeshRenderer must reference the importer-owned Mesh sub-asset");
                Mesh sharedMesh = smr.sharedMesh!;
                Assert.That(instance.Mesh, Is.SameAs(pmxAsset.ImportedMesh));

                // No 'Split Runtime' marker in mesh name.
                Assert.That(sharedMesh.name, Does.Not.Contain("Split Runtime"),
                    "imported hierarchy wrapping must not rebuild the mesh");

                // Uses pmxAsset.ImportedMaterials.
                Assert.That(pmxAsset.ImportedMaterials, Is.Not.Null.And.Not.Empty);
                Assert.That(smr.sharedMaterials, Is.Not.Null.And.Not.Empty);
                Assert.That(smr.sharedMaterials[0], Is.SameAs(pmxAsset.ImportedMaterials[0]),
                    "scene renderer material must be the importer-owned Material sub-asset");
                Assert.That(instance.Materials[0], Is.SameAs(pmxAsset.ImportedMaterials[0]));

                // Controller source behavior through MmdEditorPmxLoader.
                MmdUnityPlaybackController? nullableController = root.GetComponent<MmdUnityPlaybackController>();
                Assert.That(nullableController, Is.Not.Null);
                MmdUnityPlaybackController controller = nullableController!;
                Assert.That(controller.IsConfigured, Is.False);
                Assert.That(controller.HasModelSource, Is.True);
                Assert.That(controller.ModelSourceId, Is.EqualTo(pmxAsset.SourceId));

                Assert.That(controller.ModelAssetSource, Is.SameAs(pmxAsset));

                // ImportScale preserved on instance.
                Assert.That(instance.ImportScale, Is.EqualTo(pmxAsset.ImportScale).Within(0.0001f));
            }
            finally
            {
                if (instance?.Root != null)
                {
                    Object.DestroyImmediate(instance.Root);
                }
            }
        }

        [Test]
        public void LoadPmxAssetIntoSceneImportedHierarchyPathDoesNotProduceSplitRuntimeMesh()
        {
            // Focused name check: verify the mesh name is the importer's (not "Split Runtime")
            // and that no BuildMesh/ApplySkinning/BakeVertexMorphBlendShapes runs via this path.
            CopyFixtureToAssetDatabase("test_1bone_cube.pmx", TempPmxPath);
            MmdPmxAsset? loadedPmxAsset = AssetDatabase.LoadAssetAtPath<MmdPmxAsset>(TempPmxPath);
            Assert.That(loadedPmxAsset, Is.Not.Null);
            MmdPmxAsset pmxAsset = loadedPmxAsset!;
            Assert.That(pmxAsset.ImportedRoot, Is.Not.Null);

            MmdUnityModelInstance? instance = null;
            try
            {
                instance = MmdEditorPmxLoader.LoadPmxIntoScene(pmxAsset);
                SkinnedMeshRenderer? nullableSmr = instance.SkinnedMeshRenderer;
                Assert.That(nullableSmr, Is.Not.Null);
                SkinnedMeshRenderer smr = nullableSmr!;
                Mesh sharedMesh = smr.sharedMesh!;
                Mesh importedMesh = pmxAsset.ImportedMesh!;

                // The mesh must be the same object as the importer sub-asset, not a clone.
                Assert.That(sharedMesh, Is.SameAs(importedMesh));
                Assert.That(sharedMesh.name, Is.EqualTo(importedMesh.name));
                Assert.That(sharedMesh.name, Does.Not.Contain("Split Runtime"),
                    "wrapping path must not run BuildMesh which sets 'Split Runtime' name");

                // No blend shapes from BakeVertexMorphBlendShapes on this path.
                // (1-bone cube fixture has no morphs, but the check is structural.)
                Assert.That(sharedMesh.blendShapeCount, Is.EqualTo(importedMesh.blendShapeCount),
                    "blend shape count must match the importer mesh exactly; no rebuild");
            }
            finally
            {
                if (instance?.Root != null)
                {
                    Object.DestroyImmediate(instance.Root);
                }
            }
        }

        [Test]
        public void ImportedPmxMaterialBindsSeparateProjectTextureAssetByRelativePath()
        {
            // Corrected policy test (synthetic model + project texture asset):
            // - Place texture reference and actual imported Texture2D asset under an Assets/ path
            //   so MmdAssetPathUtility.TryResolveProjectRelativeAssetPath succeeds for the .pmx owner.
            // - Use MmdPmxImportAssetCacheBuilder.CreateImportedAssetCache (importer cache path uses
            //   sourcePath:null so no runtime decode) then the importer bind step to attach the
            //   *separate project Texture2D* to the material.
            // - Assert the bound texture is a real AssetDatabase asset (Contains true), path equals
            //   the separate texture asset path (not under .pmx), and OwnedTextures is empty for the
            //   importer cache path. No "decoded sub-asset" expectations.
            MmdUnityModelInstance? instance = null;
            string assetsTempDir = TempDirectory + "/texture_bind_test_" + System.Guid.NewGuid().ToString("N").Substring(0, 8);
            string pmxAssetPath = assetsTempDir + "/model.pmx";
            string textureAssetDir = assetsTempDir + "/textures";
            string textureAssetPath = textureAssetDir + "/diffuse.png";
            string textureProjectRelative = "textures/diffuse.png";

            try
            {
                // Create structure under Assets/ (required for project-relative resolution to return Assets/... path)
                Directory.CreateDirectory(Path.Combine(ProjectRoot, textureAssetDir));

                // Write a real image file and import it so AssetDatabase sees a Texture2D at the path.
                WritePng(Path.Combine(ProjectRoot, textureAssetPath), Color.red);
                AssetDatabase.ImportAsset(textureAssetPath, ImportAssetOptions.ForceUpdate);
                Texture2D projectTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(textureAssetPath);
                Assert.That(projectTexture, Is.Not.Null, "test setup must produce an importable project Texture2D asset");

                // Synthetic model carries the PMX-written relative reference (same as real PMX would).
                MmdModelDefinition model = CreateTexturedTriangleModel(textureProjectRelative);

                // Importer cache path: MmdPmxImportAssetCacheBuilder.CreateImportedAssetCache forces
                // sourcePath:null into factory so no transient decode/ownership for importer materials.
                // Bind step then attaches separate project Texture2D (or leaves unbound).
                instance = MmdPmxImportAssetCacheBuilder.CreateImportedAssetCache(model, importScale: 1.0f);

                // This is the importer's post-cache step for Material sub-asset texture binding.
                // It must resolve via the *asset path* (Assets/...), load the separate Texture2D,
                // and set it on the material.
                MmdPmxProjectTextureBinder.BindProjectTextureAssetsToMaterials(model, pmxAssetPath, instance.Materials);

                // Refresh diagnostics after post-bind texture assignment on the importer cache instance.
                // Addresses stale snapshot (diagnostics built in ctor before BindProject mutates mats).
                instance.RefreshMaterialBindingDiagnostics();

                // The material must now reference the separate project Texture2D.
                Texture? bound = ReadBoundDiffuseTexture(instance.Materials[0]);
                Assert.That(bound, Is.Not.Null);
                Assert.That(AssetDatabase.Contains(bound), Is.True, "bound texture must be a persisted project asset");
                string boundAssetPath = AssetDatabase.GetAssetPath(bound);
                Assert.That(boundAssetPath, Is.EqualTo(textureAssetPath), "must bind the separate Texture2D next to the fake PMX asset path");
                Assert.That(boundAssetPath, Does.Not.Contain(".pmx"), "bound texture must not be a .pmx sub-asset");

                // For MmdPmxImportAssetCacheBuilder in the importer path, OwnedTextures must be empty
                // (no runtime decode because sourcePath:null was used). The bound project asset
                // is the one attached.
                Assert.That(instance.OwnedTextures, Is.Not.Null);
                Assert.That(instance.OwnedTextures, Is.Empty, "MmdPmxImportAssetCacheBuilder for importer must not produce owned runtime textures (sourcePath:null)");

                // No registration as .pmx sub-asset: the bound asset lives at its own path and
                // would not appear among sub-assets enumerated from the .pmx asset path.
                Assert.That(AssetDatabase.GetAssetPath(bound), Is.Not.EqualTo(pmxAssetPath));

                // Diagnostics (refreshed) and material parameters now reflect post-bind importer side effects
                // for runtime parity on relevant properties (bound flags, direct scale). No .pmx Texture2D sub-assets.
                Assert.That(instance.MaterialBindingDiagnostics[0].baseMapBound, Is.True);
                Assert.That(instance.MaterialBindingDiagnostics[0].mainTexBound, Is.True);
                // Parity side effects applied via shared ApplyDiffuseBoundSideEffects.
                Assert.That(ReadMaterialFloat(instance.Materials[0], "_BaseMapBound"), Is.EqualTo(1.0f).Within(0.00001f));
            }
            finally
            {
                // Cleanup the temp texture asset explicitly (TearDown will also nuke TempDirectory).
                if (AssetDatabase.LoadAssetAtPath<Object>(textureAssetPath) != null)
                {
                    AssetDatabase.DeleteAsset(textureAssetPath);
                }
                if (AssetDatabase.LoadAssetAtPath<Object>(textureAssetDir) != null)
                {
                    AssetDatabase.DeleteAsset(textureAssetDir);
                }
                AssetDatabase.DeleteAsset(assetsTempDir);
                AssetDatabase.Refresh();

                DestroyInstance(instance);
            }
        }

        [Test]
        public void ImportedPmxMaterialRecordsMissingProjectTextureSummary()
        {
            MmdUnityModelInstance? instance = null;
            string pmxAssetPath = TempDirectory + "/missing_texture_summary/model.pmx";
            string textureReference = "textures/missing_diffuse.png";

            try
            {
                MmdModelDefinition model = CreateTexturedTriangleModel(textureReference);
                instance = MmdPmxImportAssetCacheBuilder.CreateImportedAssetCache(model, importScale: 1.0f);

                MmdPmxProjectTextureBindingSummary summary =
                    MmdPmxProjectTextureBinder.BindProjectTextureAssetsToMaterials(model, pmxAssetPath, instance.Materials);

                Assert.That(summary.ResolvedReferenceCount, Is.EqualTo(0));
                Assert.That(summary.MissingReferenceCount, Is.EqualTo(1));
                Assert.That(summary.MissingReferenceSample, Is.EqualTo("material 0 diffuse: " + textureReference));

                MmdPmxAsset asset = ScriptableObject.CreateInstance<MmdPmxAsset>();
                try
                {
                    asset.Initialize(
                        new byte[] { 1 },
                        pmxAssetPath,
                        pmxAssetPath,
                        parseSummary: MmdPmxParseSummary.FromModel(model));
                    asset.ApplyProjectTextureBindingSummary(
                        summary.ResolvedReferenceCount,
                        summary.MissingReferenceCount,
                        summary.MissingReferenceSample);

                    Assert.That(asset.ResolvedProjectTextureReferenceCount, Is.EqualTo(0));
                    Assert.That(asset.MissingProjectTextureReferenceCount, Is.EqualTo(1));
                    Assert.That(asset.MissingProjectTextureReferenceSample, Is.EqualTo("material 0 diffuse: " + textureReference));
                }
                finally
                {
                    Object.DestroyImmediate(asset);
                }
            }
            finally
            {
                DestroyInstance(instance);
            }
        }

        [Test]
        public void MissingTextureResolverCopiesExplicitSearchRootMatchToPmxRelativeCandidate()
        {
            MmdUnityModelInstance? instance = null;
            string pmxAssetPath = TempDirectory + "/missing_texture_resolver/model.pmx";
            string textureReference = "textures/missing_diffuse.png";
            string targetTextureAssetPath = TempDirectory + "/missing_texture_resolver/textures/missing_diffuse.png";
            string searchRoot = Path.Combine(ProjectRoot, TempDirectory, "external_search_root");
            string externalTexturePath = Path.Combine(searchRoot, "model_package", "textures", "missing_diffuse.png");

            try
            {
                MmdModelDefinition model = CreateTexturedTriangleModel(textureReference);
                instance = MmdPmxImportAssetCacheBuilder.CreateImportedAssetCache(model, importScale: 1.0f);

                MmdPmxProjectTextureBindingSummary before =
                    MmdPmxProjectTextureBinder.BindProjectTextureAssetsToMaterials(model, pmxAssetPath, instance.Materials);
                Assert.That(before.ResolvedReferenceCount, Is.EqualTo(0));
                Assert.That(before.MissingReferenceCount, Is.EqualTo(1));
                Assert.That(File.Exists(Path.Combine(ProjectRoot, targetTextureAssetPath)), Is.False);

                Directory.CreateDirectory(Path.GetDirectoryName(externalTexturePath)!);
                WritePng(externalTexturePath, Color.green);

                MmdPmxMissingTextureResolveResult result =
                    MmdPmxMissingTextureResolver.ResolveFirstMissingTextureReference(
                        pmxAssetPath,
                        before.MissingReferenceSample,
                        searchRoot,
                        reimportOwner: false);

                Assert.That(result.Success, Is.True, result.Message);
                Assert.That(result.Reference, Is.EqualTo(textureReference));
                Assert.That(result.TargetAssetPath, Is.EqualTo(targetTextureAssetPath));
                Assert.That(File.Exists(Path.Combine(ProjectRoot, targetTextureAssetPath)), Is.True);
                Assert.That(AssetDatabase.LoadAssetAtPath<Texture2D>(targetTextureAssetPath), Is.Not.Null);

                MmdPmxProjectTextureBindingSummary after =
                    MmdPmxProjectTextureBinder.BindProjectTextureAssetsToMaterials(model, pmxAssetPath, instance.Materials);
                Assert.That(after.ResolvedReferenceCount, Is.EqualTo(1));
                Assert.That(after.MissingReferenceCount, Is.EqualTo(0));
            }
            finally
            {
                DestroyInstance(instance);
            }
        }

        [Test]
        public void MissingTextureResolverRejectsTraversalReference()
        {
            MmdPmxMissingTextureResolveResult result =
                MmdPmxMissingTextureResolver.ResolveFirstMissingTextureReference(
                    TempDirectory + "/missing_texture_resolver/model.pmx",
                    "material 0 diffuse: ../outside.png",
                    TempDirectory,
                    reimportOwner: false);

            Assert.That(result.Success, Is.False);
            Assert.That(result.Message, Does.Contain("PMX asset directory"));
        }

        [Test]
        public void MissingTextureResolverRejectsAmbiguousBasenameMatches()
        {
            string searchRoot = Path.Combine(ProjectRoot, TempDirectory, "ambiguous_search_root");
            string first = Path.Combine(searchRoot, "model_a", "textures", "missing_diffuse.png");
            string second = Path.Combine(searchRoot, "model_b", "textures", "missing_diffuse.png");

            Directory.CreateDirectory(Path.GetDirectoryName(first)!);
            Directory.CreateDirectory(Path.GetDirectoryName(second)!);
            WritePng(first, Color.red);
            WritePng(second, Color.blue);

            MmdPmxMissingTextureResolveResult result =
                MmdPmxMissingTextureResolver.ResolveFirstMissingTextureReference(
                    TempDirectory + "/missing_texture_ambiguous/model.pmx",
                    "material 0 diffuse: textures/missing_diffuse.png",
                    searchRoot,
                    reimportOwner: false);

            Assert.That(result.Success, Is.False);
            Assert.That(result.Message, Does.Contain("Multiple matching texture files"));
        }

        [Test]
        public void MissingTextureResolverDoesNotMatchPartialDirectorySuffix()
        {
            string searchRoot = Path.Combine(ProjectRoot, TempDirectory, "partial_suffix_search_root");
            string wrong = Path.Combine(searchRoot, "mytextures", "missing_diffuse.png");

            Directory.CreateDirectory(Path.GetDirectoryName(wrong)!);
            WritePng(wrong, Color.red);

            MmdPmxMissingTextureResolveResult result =
                MmdPmxMissingTextureResolver.ResolveFirstMissingTextureReference(
                    TempDirectory + "/missing_texture_partial_suffix/model.pmx",
                    "material 0 diffuse: textures/missing_diffuse.png",
                    searchRoot,
                    reimportOwner: false);

            Assert.That(result.Success, Is.False);
            Assert.That(result.Message, Does.Contain("relative reference"));
        }

        [Test]
        public void MissingTextureResolverDoesNotReportSuccessWhenCopiedFileIsNotUnityTexture()
        {
            string pmxAssetPath = TempDirectory + "/missing_texture_invalid/model.pmx";
            string targetTextureAssetPath = TempDirectory + "/missing_texture_invalid/textures/missing_diffuse.png";
            string searchRoot = Path.Combine(ProjectRoot, TempDirectory, "invalid_search_root");
            string invalidTexturePath = Path.Combine(searchRoot, "textures", "missing_diffuse.png");

            Directory.CreateDirectory(Path.GetDirectoryName(invalidTexturePath)!);
            File.WriteAllText(invalidTexturePath, "not a png");
            LogAssert.Expect(
                LogType.Error,
                new System.Text.RegularExpressions.Regex(
                    "Could not create asset from .*missing_texture_invalid/textures/missing_diffuse\\.png: File could not be read"));

            MmdPmxMissingTextureResolveResult result =
                MmdPmxMissingTextureResolver.ResolveFirstMissingTextureReference(
                    pmxAssetPath,
                    "material 0 diffuse: textures/missing_diffuse.png",
                    searchRoot,
                    reimportOwner: false);

            Assert.That(result.Success, Is.False);
            Assert.That(result.Message, Does.Contain("Texture2D"));
            Assert.That(AssetDatabase.LoadAssetAtPath<Texture2D>(targetTextureAssetPath), Is.Null);
        }

        [Test]
        public void MissingTextureResolverReplacesExistingNonTextureTarget()
        {
            string pmxAssetPath = TempDirectory + "/missing_texture_replace/model.pmx";
            string targetTextureAssetPath = TempDirectory + "/missing_texture_replace/textures/missing_diffuse.png";
            string targetFullPath = Path.Combine(ProjectRoot, targetTextureAssetPath);
            string searchRoot = Path.Combine(ProjectRoot, TempDirectory, "replacement_search_root");
            string sourceTexturePath = Path.Combine(searchRoot, "textures", "missing_diffuse.png");

            Directory.CreateDirectory(Path.GetDirectoryName(targetFullPath)!);
            File.WriteAllText(targetFullPath, "broken existing texture");
            Directory.CreateDirectory(Path.GetDirectoryName(sourceTexturePath)!);
            WritePng(sourceTexturePath, Color.yellow);

            MmdPmxMissingTextureResolveResult result =
                MmdPmxMissingTextureResolver.ResolveFirstMissingTextureReference(
                    pmxAssetPath,
                    "material 0 diffuse: textures/missing_diffuse.png",
                    searchRoot,
                    reimportOwner: false);

            Assert.That(result.Success, Is.True, result.Message);
            Assert.That(AssetDatabase.LoadAssetAtPath<Texture2D>(targetTextureAssetPath), Is.Not.Null);
        }

        [Test]
        public void MissingTextureResolverRestoresExistingTargetWhenReplacementImportFails()
        {
            string pmxAssetPath = TempDirectory + "/missing_texture_restore/model.pmx";
            string targetTextureAssetPath = TempDirectory + "/missing_texture_restore/textures/missing_diffuse.png";
            string targetFullPath = Path.Combine(ProjectRoot, targetTextureAssetPath);
            string searchRoot = Path.Combine(ProjectRoot, TempDirectory, "restore_search_root");
            string invalidSourcePath = Path.Combine(searchRoot, "textures", "missing_diffuse.png");
            string originalTargetContents = "original invalid target";

            Directory.CreateDirectory(Path.GetDirectoryName(targetFullPath)!);
            File.WriteAllText(targetFullPath, originalTargetContents);
            Directory.CreateDirectory(Path.GetDirectoryName(invalidSourcePath)!);
            File.WriteAllText(invalidSourcePath, "replacement is still not a png");
            LogAssert.Expect(
                LogType.Error,
                new System.Text.RegularExpressions.Regex(
                    "Could not create asset from .*missing_texture_restore/textures/missing_diffuse\\.png: File could not be read"));

            MmdPmxMissingTextureResolveResult result =
                MmdPmxMissingTextureResolver.ResolveFirstMissingTextureReference(
                    pmxAssetPath,
                    "material 0 diffuse: textures/missing_diffuse.png",
                    searchRoot,
                    reimportOwner: false);

            Assert.That(result.Success, Is.False);
            Assert.That(result.Message, Does.Contain("Texture2D"));
            Assert.That(File.ReadAllText(targetFullPath), Is.EqualTo(originalTargetContents));
        }

        [Test]
        public void MissingTextureResolverDoesNotCopyTargetFileOntoItself()
        {
            string pmxAssetPath = TempDirectory + "/missing_texture_same_file/model.pmx";
            string targetTextureAssetPath = TempDirectory + "/missing_texture_same_file/textures/missing_diffuse.png";
            string targetFullPath = Path.Combine(ProjectRoot, targetTextureAssetPath);
            string searchRoot = Path.Combine(ProjectRoot, TempDirectory, "missing_texture_same_file");

            Directory.CreateDirectory(Path.GetDirectoryName(targetFullPath)!);
            File.WriteAllText(targetFullPath, "broken existing texture");

            MmdPmxMissingTextureResolveResult result =
                MmdPmxMissingTextureResolver.ResolveFirstMissingTextureReference(
                    pmxAssetPath,
                    "material 0 diffuse: textures/missing_diffuse.png",
                    searchRoot,
                    reimportOwner: false);

            Assert.That(result.Success, Is.False);
            Assert.That(result.Message, Does.Contain("already at the PMX texture target path"));
        }

        [Test]
        public void PmxTextureCandidatePathResolvesBeforeTextureFileExists()
        {
            string pmxAssetPath = TempDirectory + "/texture_candidate_test/model.pmx";
            string missingTexturePath = TempDirectory + "/texture_candidate_test/textures/diffuse.png";

            Assert.That(File.Exists(Path.Combine(ProjectRoot, missingTexturePath)), Is.False);

            bool existingResolved = MmdAssetPathUtility.TryResolveProjectRelativeAssetPath(
                pmxAssetPath,
                "textures/diffuse.png",
                out string existingOnlyAssetPath);
            bool candidateResolved = MmdAssetPathUtility.TryResolveProjectRelativeAssetPathCandidate(
                pmxAssetPath,
                "textures/diffuse.png",
                out string candidateAssetPath);

            Assert.That(existingResolved, Is.False);
            Assert.That(existingOnlyAssetPath, Is.Empty);
            Assert.That(candidateResolved, Is.True);
            Assert.That(candidateAssetPath, Is.EqualTo(missingTexturePath));
        }

        [Test]
        public void LoadPmxAssetIntoScenePersistsModelSourceAcrossSceneReload()
        {
            CopyFixtureToAssetDatabase("test_1bone_cube.pmx", TempPmxPath);
            MmdPmxAsset? initialPmxAsset = AssetDatabase.LoadAssetAtPath<MmdPmxAsset>(TempPmxPath);
            Assert.That(initialPmxAsset, Is.Not.Null);

            try
            {
                Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                MmdPmxAsset? loadedPmxAsset = AssetDatabase.LoadAssetAtPath<MmdPmxAsset>(TempPmxPath);
                Assert.That(loadedPmxAsset, Is.Not.Null);
                MmdPmxAsset pmxAsset = loadedPmxAsset!;
                string expectedSourceId = pmxAsset.SourceId;
                Material expectedImportedMaterial = pmxAsset.ImportedMaterials[0];
                MmdUnityModelInstance instance = MmdEditorPmxLoader.LoadPmxIntoScene(pmxAsset);
                GameObject root = instance.Root!;
                MmdUnityPlaybackController controller = root.GetComponent<MmdUnityPlaybackController>()!;
                Assert.That(controller.ModelAssetSource, Is.SameAs(pmxAsset));

                Assert.That(EditorSceneManager.SaveScene(scene, TempScenePath), Is.True);
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EditorSceneManager.OpenScene(TempScenePath, OpenSceneMode.Single);

                MmdUnityPlaybackController[] controllers =
                    Object.FindObjectsByType<MmdUnityPlaybackController>();
                Assert.That(controllers, Has.Length.EqualTo(1));
                Assert.That(controllers[0].IsConfigured, Is.False);
                Assert.That(controllers[0].HasModelSource, Is.True);
                Assert.That(controllers[0].ModelSourceId, Is.EqualTo(expectedSourceId));

                Assert.That(controllers[0].ModelAssetSource, Is.Not.Null);
                MmdPmxAsset sourceAsset = controllers[0].ModelAssetSource!;
                Assert.That(sourceAsset.SourceId, Is.EqualTo(expectedSourceId));

                MmdPmxAsset? reloadedPmxAsset = AssetDatabase.LoadAssetAtPath<MmdPmxAsset>(TempPmxPath);
                Assert.That(reloadedPmxAsset, Is.Not.Null);
                MmdPmxAsset reopenedPmxAsset = reloadedPmxAsset!;

                SkinnedMeshRenderer[] renderers =
                    Object.FindObjectsByType<SkinnedMeshRenderer>();
                Assert.That(renderers, Has.Length.GreaterThanOrEqualTo(1));
                SkinnedMeshRenderer smr = renderers[0];

                // Must still reference the importer-owned mesh sub-asset (not a transient clone).
                Assert.That(smr.sharedMesh, Is.Not.Null);
                Mesh sharedMesh = smr.sharedMesh!;
                Assert.That(sharedMesh, Is.SameAs(reopenedPmxAsset.ImportedMesh),
                    "scene SMR must reference the importer-owned Mesh sub-asset after save/reopen");

                // Mesh name must not indicate a Split Runtime rebuild.
                Assert.That(sharedMesh.name, Does.Not.Contain("Split Runtime"),
                    "scene mesh must not carry a Split Runtime name; hierarchy reuse path must be used");

                // Bones must match PMX descriptor.
                Assert.That(smr.bones, Is.Not.Null.And.Not.Empty,
                    "scene SMR must have non-empty bones after save/reopen");
                Assert.That(smr.bones, Has.Length.EqualTo(reopenedPmxAsset.BoneCount),
                    "scene SMR bone count must match pmxAsset.BoneCount after save/reopen");
                Assert.That(smr.bones[0], Is.Not.Null);

                // rootBone must be valid.
                Assert.That(smr.rootBone, Is.Not.Null,
                    "scene SMR must have a valid rootBone after save/reopen");

                // Material references still importer-owned.
                Assert.That(smr.sharedMaterials, Is.Not.Empty);
                foreach (Material material in smr.sharedMaterials)
                {
                    Assert.That(material, Is.Not.Null);
                }
                Assert.That(smr.sharedMaterials[0] == expectedImportedMaterial, Is.True);
            }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
        }

        [Test]
        public void LoadPlaybackPmxVmdAssetIntoSceneUsesImporterMaterialSubAssets()
        {
            // Focused test: PMX+VMD asset editor load path (MmdEditorVerificationFacade + PlaybackLoader menu path)
            // must create scene model using importer Mesh/Material sub-assets, then binding over existing.
            // Verifies no switch to runtime-generated materials (parity with drag / Load PMX + attach motion).
            CopyFixtureToAssetDatabase("test_1bone_cube.pmx", TempPmxPath);
            CopyFixtureToAssetDatabase("test_1bone_cube_motion.vmd", TempVmdPath);
            MmdPmxAsset pmxAsset = AssetDatabase.LoadAssetAtPath<MmdPmxAsset>(TempPmxPath);
            MmdVmdAsset vmdAsset = AssetDatabase.LoadAssetAtPath<MmdVmdAsset>(TempVmdPath);
            Assert.That(pmxAsset, Is.Not.Null);
            Assert.That(vmdAsset, Is.Not.Null);
            Assert.That(pmxAsset.ImportedMaterials, Is.Not.Null.And.Not.Empty);
            Assert.That(pmxAsset.ImportedMesh, Is.Not.Null);

            MmdEditorPlaybackSceneLoadResult? loadResult = null;
            try
            {
                // This is the asset overload used by "Load Selected PMX+VMD Assets Into Scene".
                loadResult = MmdEditorVerificationFacade.LoadPlaybackIntoScene(pmxAsset, vmdAsset, frameRate: 30.0f, initialFrame: 0, playOnStart: false);

                Assert.That(loadResult, Is.Not.Null);
                Assert.That(loadResult!.Instance, Is.Not.Null);
                Assert.That(loadResult.Controller, Is.Not.Null);
                Assert.That(loadResult.Controller.IsConfigured, Is.True);

                // Must use importer sub-assets for materials (not freshly CreateSkinned runtime mats).
                Material[] placedMats = loadResult.Instance.Materials;
                Assert.That(placedMats, Is.Not.Null.And.Not.Empty);
                Assert.That(placedMats[0], Is.SameAs(pmxAsset.ImportedMaterials[0]), "playback asset load must bind over importer Material sub-asset, not runtime-generated material");

                SkinnedMeshRenderer? smr = loadResult.Instance.SkinnedMeshRenderer;
                Assert.That(smr, Is.Not.Null);
                Assert.That(smr!.sharedMaterials[0], Is.SameAs(pmxAsset.ImportedMaterials[0]));

                Assert.That(loadResult.Controller.ModelAssetSource, Is.SameAs(pmxAsset));
                Assert.That(loadResult.Controller.MotionAssetSource, Is.SameAs(vmdAsset));
            }
            finally
            {
                if (loadResult?.Instance?.Root != null)
                {
                    Object.DestroyImmediate(loadResult.Instance.Root);
                }
            }
        }

        [Test]
        public void LoadPmxAssetIntoSceneAppliesImporterMaterialRemaps()
        {
            CopyFixtureToAssetDatabase("test_1bone_cube.pmx", TempPmxPath);
            Material remappedMaterial = CreateTestMaterial("remapped_body");
            AssetDatabase.CreateAsset(remappedMaterial, TempRemapMaterialPath);
            AssetDatabase.ImportAsset(TempRemapMaterialPath, ImportAssetOptions.ForceUpdate);
            Material persistedRemappedMaterial = AssetDatabase.LoadAssetAtPath<Material>(TempRemapMaterialPath);

            var importer = AssetImporter.GetAtPath(TempPmxPath) as MmdPmxScriptedImporter;
            Assert.That(importer, Is.Not.Null);
            Assert.That(persistedRemappedMaterial, Is.Not.Null);
            var serializedImporter = new SerializedObject(importer!);
            SerializedProperty remaps = serializedImporter.FindProperty("materialRemaps");
            remaps.arraySize = 1;
            remaps.GetArrayElementAtIndex(0).objectReferenceValue = persistedRemappedMaterial;
            serializedImporter.ApplyModifiedPropertiesWithoutUndo();
            importer!.SaveAndReimport();

            MmdPmxAsset pmxAsset = AssetDatabase.LoadAssetAtPath<MmdPmxAsset>(TempPmxPath);
            MmdUnityModelInstance? instance = null;
            try
            {
                instance = MmdEditorPmxLoader.LoadPmxIntoScene(pmxAsset);

                Assert.That(pmxAsset.MaterialRemaps, Has.Length.EqualTo(1));
                Assert.That(pmxAsset.MaterialRemaps[0], Is.SameAs(persistedRemappedMaterial));
                Assert.That(instance.Materials[0], Is.SameAs(persistedRemappedMaterial));
                Assert.That(instance.SkinnedMeshRenderer!.sharedMaterials[0], Is.SameAs(persistedRemappedMaterial));
            }
            finally
            {
                DestroyInstance(instance);
            }
        }

        [Test]
        public void ImportedPmxAssetCarriesCachedModelBoundsFromImporter()
        {
            CopyFixtureToAssetDatabase("test_1bone_cube.pmx", TempPmxPath);

            MmdPmxAsset pmxAsset = AssetDatabase.LoadAssetAtPath<MmdPmxAsset>(TempPmxPath);
            MmdModelDefinition model = pmxAsset.LoadModel();
            Bounds expectedBounds = CalculateMmdBounds(model);

            AssertVector3(pmxAsset.BoundsMin, expectedBounds.min);
            AssertVector3(pmxAsset.BoundsMax, expectedBounds.max);
            AssertVector3(pmxAsset.BoundsSize, expectedBounds.size);
        }

        [Test]
        public void ImportedPmxAssetCarriesCachedMaterialReferenceSummaryFromImporter()
        {
            CopyFixtureToAssetDatabase("test_1bone_cube.pmx", TempPmxPath);

            MmdPmxAsset pmxAsset = AssetDatabase.LoadAssetAtPath<MmdPmxAsset>(TempPmxPath);
            MmdModelDefinition model = pmxAsset.LoadModel();

            Assert.That(
                pmxAsset.DiffuseTextureReferenceCount,
                Is.EqualTo(CountMaterials(model, material => !string.IsNullOrWhiteSpace(material.texture))));
            Assert.That(
                pmxAsset.SphereTextureReferenceCount,
                Is.EqualTo(CountMaterials(model, material => !string.IsNullOrWhiteSpace(material.sphereTexture))));
            Assert.That(
                pmxAsset.ToonTextureReferenceCount,
                Is.EqualTo(CountMaterials(model, material => !string.IsNullOrWhiteSpace(material.toonTexture))));
            Assert.That(
                pmxAsset.TransparentMaterialCount,
                Is.EqualTo(CountMaterials(model, material => material.alpha < 1.0f)));
            Assert.That(
                pmxAsset.EdgeMaterialCount,
                Is.EqualTo(CountMaterials(model, material => material.drawEdgeFlag && material.edgeSize > 0.0f)));
            Assert.That(pmxAsset.MaterialSummaries, Has.Length.EqualTo(model.materials.Count));
            Assert.That(pmxAsset.MaterialSummaries[0].index, Is.EqualTo(model.materials[0].index));
            Assert.That(pmxAsset.MaterialSummaries[0].name, Is.EqualTo(model.materials[0].name));
            Assert.That(pmxAsset.MaterialSummaries[0].diffuseTexture, Is.EqualTo(model.materials[0].texture));
            Assert.That(pmxAsset.MaterialSummaries[0].sphereTexture, Is.EqualTo(model.materials[0].sphereTexture));
            Assert.That(pmxAsset.MaterialSummaries[0].toonTexture, Is.EqualTo(model.materials[0].toonTexture));
        }

        [Test]
        public void OutlineReadinessReportsCachedEdgeMaterialReleaseBoundary()
        {
            var pmxAsset = ScriptableObject.CreateInstance<MmdPmxAsset>();
            try
            {
                pmxAsset.Initialize(
                    new byte[] { 1, 2, 3, 4 },
                    "outline-source",
                    "outline-source.pmx",
                    assetShaderPreset: "MmdBasicUrpToon",
                    parseSummary: new MmdPmxParseSummary(
                        "outline-model",
                        vertexCount: 4,
                        indexCount: 6,
                        boneCount: 1,
                        morphCount: 0,
                        materialCount: 3,
                        diffuseTextureReferenceCount: 0,
                        sphereTextureReferenceCount: 0,
                        toonTextureReferenceCount: 0,
                        transparentMaterialCount: 0,
                        edgeMaterialCount: 2,
                        ikCount: 0,
                        rigidbodyCount: 0,
                        jointCount: 0,
                        boundsMin: Vector3.zero,
                        boundsMax: Vector3.one));

                MmdOutlineReadiness readiness = MmdAssetInspectorUtility.GetOutlineReadiness(pmxAsset);

                Assert.That(readiness.OutlineEligibleMaterialCount, Is.EqualTo(2));
                Assert.That(readiness.RuntimePath, Is.EqualTo("MmdOutlineRendererFeature (LightMode=MmdOutline)"));
                Assert.That(readiness.ReleaseMode, Is.EqualTo("Back-face mesh-normal extrusion"));
                Assert.That(readiness.FinalVisualParity, Is.EqualTo(MmdOutlineReadiness.NotClaimed));
            }
            finally
            {
                Object.DestroyImmediate(pmxAsset);
            }
        }

        [Test]
        public void OutlineReadinessDoesNotClaimRuntimeOutlineWhenNoEdgeMaterialsExist()
        {
            var pmxAsset = ScriptableObject.CreateInstance<MmdPmxAsset>();
            try
            {
                pmxAsset.Initialize(
                    new byte[] { 1, 2, 3, 4 },
                    "no-outline-source",
                    "no-outline-source.pmx",
                    parseSummary: new MmdPmxParseSummary(
                        "no-outline-model",
                        vertexCount: 4,
                        indexCount: 6,
                        boneCount: 1,
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
                        boundsMin: Vector3.zero,
                        boundsMax: Vector3.one));

                MmdOutlineReadiness readiness = MmdAssetInspectorUtility.GetOutlineReadiness(pmxAsset);

                Assert.That(readiness.OutlineEligibleMaterialCount, Is.EqualTo(0));
                Assert.That(readiness.RuntimePath, Is.EqualTo("No PMX draw-edge materials"));
                Assert.That(readiness.ReleaseMode, Is.EqualTo("Not needed"));
                Assert.That(readiness.FinalVisualParity, Is.EqualTo(MmdOutlineReadiness.NotClaimed));
            }
            finally
            {
                Object.DestroyImmediate(pmxAsset);
            }
        }

        [Test]
        public void ImportedPmxAssetCarriesCachedPhysicsSummaryFromImporter()
        {
            CopyFixtureToAssetDatabase("test_1bone_cube.pmx", TempPmxPath);

            MmdPmxAsset pmxAsset = AssetDatabase.LoadAssetAtPath<MmdPmxAsset>(TempPmxPath);
            MmdModelDefinition model = pmxAsset.LoadModel();

            // The current repo-safe PMX fixture has no physics descriptors; this pins the importer/cache invariant for that case.
            Assert.That(pmxAsset.RigidbodyCount, Is.EqualTo(model.physics?.rigidbodies?.Count ?? 0));
            Assert.That(pmxAsset.JointCount, Is.EqualTo(model.physics?.joints?.Count ?? 0));
        }

        [Test]
        public void PmxParseSummaryCountsPhysicsDescriptorsFromModel()
        {
            var model = new MmdModelDefinition();
            model.physics.rigidbodies.Add(new MmdRigidbodyDefinition { index = 0, name = "body-a" });
            model.physics.rigidbodies.Add(new MmdRigidbodyDefinition { index = 1, name = "body-b" });
            model.physics.joints.Add(new MmdJointDefinition { index = 0, name = "joint-a" });

            MmdPmxParseSummary summary = MmdPmxParseSummary.FromModel(model);

            Assert.That(summary.RigidbodyCount, Is.EqualTo(2));
            Assert.That(summary.JointCount, Is.EqualTo(1));
        }

        [Test]
        public void PmxParseSummaryCarriesModelCredits()
        {
            var model = new MmdModelDefinition
            {
                name = "credit-model",
                englishName = "credit-model-en",
                comment = "日本語コメント",
                englishComment = "English comment"
            };

            MmdPmxParseSummary summary = MmdPmxParseSummary.FromModel(model);
            MmdPmxAsset pmxAsset = ScriptableObject.CreateInstance<MmdPmxAsset>();
            try
            {
                pmxAsset.Initialize(
                    new byte[] { 1, 2, 3 },
                    "credit.pmx",
                    "External/Model/credit.pmx",
                    parseSummary: summary);

                Assert.That(summary.ModelEnglishName, Is.EqualTo("credit-model-en"));
                Assert.That(summary.ModelComment, Is.EqualTo("日本語コメント"));
                Assert.That(summary.ModelEnglishComment, Is.EqualTo("English comment"));
                Assert.That(pmxAsset.ModelEnglishName, Is.EqualTo("credit-model-en"));
                Assert.That(pmxAsset.ModelComment, Is.EqualTo("日本語コメント"));
                Assert.That(pmxAsset.ModelEnglishComment, Is.EqualTo("English comment"));
            }
            finally
            {
                Object.DestroyImmediate(pmxAsset);
            }
        }

        [Test]
        public void PmxParseSummaryCalculatesBoundsFromFiniteVertexPositions()
        {
            var model = new MmdModelDefinition();
            model.vertices.Add(new MmdVertexDefinition { position = new[] { -1.0f, 2.0f, -3.0f } });
            model.vertices.Add(new MmdVertexDefinition { position = new[] { 4.0f, -5.0f, 6.0f } });
            model.vertices.Add(new MmdVertexDefinition { position = new[] { float.NaN, 1.0f, 2.0f } });

            MmdPmxParseSummary summary = MmdPmxParseSummary.FromModel(model);

            AssertVector3(summary.BoundsMin, new Vector3(-1.0f, -5.0f, -3.0f));
            AssertVector3(summary.BoundsMax, new Vector3(4.0f, 2.0f, 6.0f));
            AssertVector3(summary.BoundsSize, new Vector3(5.0f, 7.0f, 9.0f));
        }

        [Test]
        public void PmxParseSummaryReturnsZeroBoundsWhenAllVertexPositionsAreNonFinite()
        {
            var model = new MmdModelDefinition();
            model.vertices.Add(new MmdVertexDefinition { position = new[] { float.NaN, 1.0f, 2.0f } });
            model.vertices.Add(new MmdVertexDefinition { position = new[] { float.PositiveInfinity, 0.0f, 0.0f } });
            model.vertices.Add(new MmdVertexDefinition { position = Array.Empty<float>() });

            MmdPmxParseSummary summary = MmdPmxParseSummary.FromModel(model);

            Assert.That(summary.VertexCount, Is.EqualTo(3));
            AssertVector3(summary.BoundsMin, Vector3.zero);
            AssertVector3(summary.BoundsMax, Vector3.zero);
            AssertVector3(summary.BoundsSize, Vector3.zero);
        }

        [Test]
        public void AnimationTimelineReadinessReportsBindingTargetStateWithoutLoadingPmx()
        {
            MmdPmxAsset readyAsset = ScriptableObject.CreateInstance<MmdPmxAsset>();
            MmdPmxAsset emptyAsset = ScriptableObject.CreateInstance<MmdPmxAsset>();
            try
            {
                readyAsset.Initialize(new byte[] { 1, 2, 3 }, "ready.pmx", "External/Model/ready.pmx");

                MmdAnimationTimelineReadiness ready = MmdAssetInspectorUtility.GetAnimationTimelineReadiness(readyAsset);
                MmdAnimationTimelineReadiness unavailable = MmdAssetInspectorUtility.GetAnimationTimelineReadiness(emptyAsset);

                Assert.That(ready.TimelineBindingTarget, Is.EqualTo("Ready after Load PMX Into Scene"));
                Assert.That(ready.VmdDropReadiness, Is.EqualTo("Requires generated scene controller"));
                Assert.That(ready.PlaybackSource, Is.EqualTo("Scene component or Timeline clip, not PMX import side effect"));
                Assert.That(unavailable.TimelineBindingTarget, Is.EqualTo("Unavailable"));
                Assert.That(unavailable.VmdDropReadiness, Is.EqualTo("Unavailable"));
            }
            finally
            {
                Object.DestroyImmediate(readyAsset);
                Object.DestroyImmediate(emptyAsset);
            }
        }

        [Test]
        public void ImportedMmdAssetsUseLightweightCustomInspectors()
        {
            CopyFixtureToAssetDatabase("test_1bone_cube.pmx", TempPmxPath);
            CopyFixtureToAssetDatabase("test_1bone_cube_motion.vmd", TempVmdPath);
            MmdPmxAsset pmxAsset = AssetDatabase.LoadAssetAtPath<MmdPmxAsset>(TempPmxPath);
            MmdVmdAsset vmdAsset = AssetDatabase.LoadAssetAtPath<MmdVmdAsset>(TempVmdPath);
            AssetImporter pmxImporter = AssetImporter.GetAtPath(TempPmxPath);
            UnityEditor.Editor? pmxEditor = null;
            UnityEditor.Editor? vmdEditor = null;
            UnityEditor.Editor? pmxImporterEditor = null;

            try
            {
                pmxEditor = UnityEditor.Editor.CreateEditor(pmxAsset);
                vmdEditor = UnityEditor.Editor.CreateEditor(vmdAsset);
                pmxImporterEditor = UnityEditor.Editor.CreateEditor(pmxImporter);

                Assert.That(pmxEditor, Is.TypeOf<MmdPmxAssetEditor>());
                Assert.That(vmdEditor, Is.TypeOf<MmdVmdAssetEditor>());
                Assert.That(pmxImporterEditor, Is.TypeOf<MmdPmxScriptedImporterEditor>());
            }
            finally
            {
                if (pmxEditor != null)
                {
                    Object.DestroyImmediate(pmxEditor);
                }

                if (vmdEditor != null)
                {
                    Object.DestroyImmediate(vmdEditor);
                }

                if (pmxImporterEditor != null)
                {
                    Object.DestroyImmediate(pmxImporterEditor);
                }
            }
        }

        [Test]
        public void EditorFacadeLoadsPlaybackFromImportedAssets()
        {
            CopyFixtureToAssetDatabase("test_1bone_cube.pmx", TempPmxPath);
            CopyFixtureToAssetDatabase("test_1bone_cube_motion.vmd", TempVmdPath);
            MmdPmxAsset pmxAsset = AssetDatabase.LoadAssetAtPath<MmdPmxAsset>(TempPmxPath);
            MmdVmdAsset vmdAsset = AssetDatabase.LoadAssetAtPath<MmdVmdAsset>(TempVmdPath);
            MmdEditorPlaybackSceneLoadResult? result = null;

            try
            {
                result = MmdEditorVerificationFacade.LoadPlaybackIntoScene(
                    pmxAsset,
                    vmdAsset,
                    frameRate: 30.0f,
                    initialFrame: 10);

                Assert.That(result.Controller.IsConfigured, Is.True);
                Assert.That(result.Controller.ModelAssetSource, Is.SameAs(pmxAsset));
                Assert.That(result.Controller.MotionAssetSource, Is.SameAs(vmdAsset));
                Assert.That(result.InitialSnapshot.frame.frame, Is.EqualTo(10));
                Assert.That(result.ModelPath, Does.EndWith("test_1bone_cube.pmx"));
                Assert.That(result.MotionPath, Does.EndWith("test_1bone_cube_motion.vmd"));
                // Editor facade load-at-arbitrary-frame (initialFrame=10) diagnostic path forces physics Off (controller default Live is preserved for normal playback).
                Assert.That(result.Controller.PhysicsMode, Is.EqualTo(MmdPhysicsMode.Off));
            }
            finally
            {
                DestroyInstance(result?.Instance);
            }
        }

        [Test]
        public void EditorPlaybackLoaderLoadsPlaybackFromImportedAssetReferences()
        {
            CopyFixtureToAssetDatabase("test_1bone_cube.pmx", TempPmxPath);
            CopyFixtureToAssetDatabase("test_1bone_cube_motion.vmd", TempVmdPath);
            MmdPmxAsset pmxAsset = AssetDatabase.LoadAssetAtPath<MmdPmxAsset>(TempPmxPath);
            MmdVmdAsset vmdAsset = AssetDatabase.LoadAssetAtPath<MmdVmdAsset>(TempVmdPath);
            MmdEditorPlaybackSceneLoadResult? result = null;

            try
            {
                result = MmdEditorPlaybackLoader.LoadPlaybackIntoScene(
                    pmxAsset,
                    vmdAsset,
                    frameRate: 24.0f,
                    initialFrame: 8);

                Assert.That(result.Controller.IsConfigured, Is.True);
                Assert.That(result.Controller.PlayOnStart, Is.True);
                Assert.That(result.Controller.ModelAssetSource, Is.SameAs(pmxAsset));
                Assert.That(result.Controller.MotionAssetSource, Is.SameAs(vmdAsset));
                Assert.That(result.InitialSnapshot.frame.frame, Is.EqualTo(8));
                Assert.That(result.ModelPath, Does.EndWith("test_1bone_cube.pmx"));
                Assert.That(result.MotionPath, Does.EndWith("test_1bone_cube_motion.vmd"));
                Assert.That(result.Instance.BoneTransforms, Has.Length.GreaterThan(0));
                // Editor loader load-at-arbitrary-frame (initialFrame=8) diagnostic path forces physics Off.
                Assert.That(result.Controller.PhysicsMode, Is.EqualTo(MmdPhysicsMode.Off));
            }
            finally
            {
                DestroyInstance(result?.Instance);
            }
        }

        [Test]
        public void ImportedAssetPlaybackReportsInputValidationStageForMissingAssets()
        {
            CopyFixtureToAssetDatabase("test_1bone_cube.pmx", TempPmxPath);
            CopyFixtureToAssetDatabase("test_1bone_cube_motion.vmd", TempVmdPath);
            MmdPmxAsset pmxAsset = AssetDatabase.LoadAssetAtPath<MmdPmxAsset>(TempPmxPath);
            MmdVmdAsset vmdAsset = AssetDatabase.LoadAssetAtPath<MmdVmdAsset>(TempVmdPath);

            MmdEditorVerificationException missingPmx = Assert.Throws<MmdEditorVerificationException>(
                () => MmdEditorPlaybackLoader.LoadPlaybackIntoScene(null!, vmdAsset));
            MmdEditorVerificationException missingVmd = Assert.Throws<MmdEditorVerificationException>(
                () => MmdEditorPlaybackLoader.LoadPlaybackIntoScene(pmxAsset, null!));

            Assert.That(missingPmx.Stage, Is.EqualTo(MmdEditorVerificationFacade.InputValidationStage));
            Assert.That(missingVmd.Stage, Is.EqualTo(MmdEditorVerificationFacade.InputValidationStage));
        }

        [Test]
        public void ImportedAssetPlaybackReportsParseStagesForEmptyAssets()
        {
            CopyFixtureToAssetDatabase("test_1bone_cube.pmx", TempPmxPath);
            CopyFixtureToAssetDatabase("test_1bone_cube_motion.vmd", TempVmdPath);
            MmdPmxAsset pmxAsset = AssetDatabase.LoadAssetAtPath<MmdPmxAsset>(TempPmxPath);
            MmdVmdAsset vmdAsset = AssetDatabase.LoadAssetAtPath<MmdVmdAsset>(TempVmdPath);
            MmdPmxAsset emptyPmx = ScriptableObject.CreateInstance<MmdPmxAsset>();
            MmdVmdAsset emptyVmd = ScriptableObject.CreateInstance<MmdVmdAsset>();

            try
            {
                MmdEditorVerificationException pmxException = Assert.Throws<MmdEditorVerificationException>(
                    () => MmdEditorPlaybackLoader.LoadPlaybackIntoScene(emptyPmx, vmdAsset));
                MmdEditorVerificationException vmdException = Assert.Throws<MmdEditorVerificationException>(
                    () => MmdEditorPlaybackLoader.LoadPlaybackIntoScene(pmxAsset, emptyVmd));

                Assert.That(pmxException.Stage, Is.EqualTo(MmdEditorVerificationFacade.PmxParseStage));
                Assert.That(vmdException.Stage, Is.EqualTo(MmdEditorVerificationFacade.VmdParseStage));
            }
            finally
            {
                Object.DestroyImmediate(emptyPmx);
                Object.DestroyImmediate(emptyVmd);
            }
        }

        [Test]
        public void SceneDragAndDropAcceptsSinglePmxAsset()
        {
            MmdPmxAsset pmxAsset = ScriptableObject.CreateInstance<MmdPmxAsset>();
            try
            {
                bool accepted = MmdSceneDragAndDrop.TryGetDraggedAssets(
                    new Object[] { pmxAsset },
                    out MmdPmxAsset? draggedPmx,
                    out MmdVmdAsset? draggedVmd);

                Assert.That(accepted, Is.True);
                Assert.That(draggedPmx, Is.SameAs(pmxAsset));
                Assert.That(draggedVmd, Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(pmxAsset);
            }
        }

        [Test]
        public void SceneDragAndDropAcceptsPmxAndVmdAssets()
        {
            MmdPmxAsset pmxAsset = ScriptableObject.CreateInstance<MmdPmxAsset>();
            MmdVmdAsset vmdAsset = ScriptableObject.CreateInstance<MmdVmdAsset>();
            try
            {
                bool accepted = MmdSceneDragAndDrop.TryGetDraggedAssets(
                    new Object[] { pmxAsset, vmdAsset },
                    out MmdPmxAsset? draggedPmx,
                    out MmdVmdAsset? draggedVmd);

                Assert.That(accepted, Is.True);
                Assert.That(draggedPmx, Is.SameAs(pmxAsset));
                Assert.That(draggedVmd, Is.SameAs(vmdAsset));
            }
            finally
            {
                Object.DestroyImmediate(pmxAsset);
                Object.DestroyImmediate(vmdAsset);
            }
        }

        [Test]
        public void SceneDragAndDropRejectsAmbiguousOrUnrelatedAssets()
        {
            MmdPmxAsset firstPmx = ScriptableObject.CreateInstance<MmdPmxAsset>();
            MmdPmxAsset secondPmx = ScriptableObject.CreateInstance<MmdPmxAsset>();
            MmdVmdAsset vmdAsset = ScriptableObject.CreateInstance<MmdVmdAsset>();
            var unrelated = new Texture2D(1, 1);
            try
            {
                Assert.That(
                    MmdSceneDragAndDrop.TryGetDraggedAssets(new Object[] { firstPmx, secondPmx }, out _, out _),
                    Is.False);
                Assert.That(
                    MmdSceneDragAndDrop.TryGetDraggedAssets(new Object[] { firstPmx, unrelated }, out _, out _),
                    Is.False);
                Assert.That(
                    MmdSceneDragAndDrop.TryGetDraggedAssets(new Object[] { vmdAsset }, out _, out _),
                    Is.False);
            }
            finally
            {
                Object.DestroyImmediate(firstPmx);
                Object.DestroyImmediate(secondPmx);
                Object.DestroyImmediate(vmdAsset);
                Object.DestroyImmediate(unrelated);
            }
        }

        [Test]
        public void SceneDragAndDropAcceptsSingleVmdAssetOnlyForExistingModelSource()
        {
            MmdPmxAsset pmxAsset = ScriptableObject.CreateInstance<MmdPmxAsset>();
            MmdVmdAsset firstVmd = ScriptableObject.CreateInstance<MmdVmdAsset>();
            MmdVmdAsset secondVmd = ScriptableObject.CreateInstance<MmdVmdAsset>();
            try
            {
                bool accepted = MmdSceneDragAndDrop.TryGetDraggedVmdAssetForExistingModel(
                    new Object[] { firstVmd },
                    Array.Empty<string>(),
                    out MmdVmdAsset? draggedVmd);

                Assert.That(accepted, Is.True);
                Assert.That(draggedVmd, Is.SameAs(firstVmd));
                Assert.That(
                    MmdSceneDragAndDrop.TryGetDraggedVmdAssetForExistingModel(new Object[] { firstVmd, secondVmd }, Array.Empty<string>(), out _),
                    Is.False);
                Assert.That(
                    MmdSceneDragAndDrop.TryGetDraggedVmdAssetForExistingModel(new Object[] { pmxAsset, firstVmd }, Array.Empty<string>(), out _),
                    Is.False);
                Assert.That(
                    MmdSceneDragAndDrop.TryGetDraggedVmdAssetForExistingModel(new Object[] { firstVmd }, new[] { Path.Combine(Path.GetTempPath(), "External", "Motion", "motion.vmd") }, out _),
                    Is.False);
            }
            finally
            {
                Object.DestroyImmediate(pmxAsset);
                Object.DestroyImmediate(firstVmd);
                Object.DestroyImmediate(secondVmd);
            }
        }

        [Test]
        public void SceneDragAndDropAcceptsSingleRawPmxPath()
        {
            string pmxPath = Path.Combine(RepositoryRoot, "packages", "com.yohawing.mmd-loader", "Tests", "Fixtures", "Assets", "test_1bone_cube.pmx");

            bool accepted = MmdSceneDragAndDrop.TryGetDraggedSources(
                new Object[0],
                new[] { pmxPath },
                out MmdPmxAsset? pmxAsset,
                out MmdVmdAsset? vmdAsset,
                out string? draggedPmxPath,
                out string? draggedVmdPath);

            Assert.That(accepted, Is.True);
            Assert.That(pmxAsset, Is.Null);
            Assert.That(vmdAsset, Is.Null);
            Assert.That(draggedPmxPath, Is.EqualTo(Path.GetFullPath(pmxPath)));
            Assert.That(draggedVmdPath, Is.Null);
        }

        [Test]
        public void SceneDragAndDropFallsBackToRawPmxPathWhenObjectReferenceIsNotMmdAsset()
        {
            string pmxPath = Path.Combine(RepositoryRoot, "packages", "com.yohawing.mmd-loader", "Tests", "Fixtures", "Assets", "test_1bone_cube.pmx");
            var nonMmdReference = new Texture2D(1, 1);

            try
            {
                bool accepted = MmdSceneDragAndDrop.TryGetDraggedSources(
                    new Object[] { nonMmdReference },
                    new[] { pmxPath },
                    out MmdPmxAsset? pmxAsset,
                    out MmdVmdAsset? vmdAsset,
                    out string? draggedPmxPath);

                Assert.That(accepted, Is.True);
                Assert.That(pmxAsset, Is.Null);
                Assert.That(vmdAsset, Is.Null);
                Assert.That(draggedPmxPath, Is.EqualTo(Path.GetFullPath(pmxPath)));
            }
            finally
            {
                Object.DestroyImmediate(nonMmdReference);
            }
        }

        [Test]
        public void SceneDragAndDropAcceptsRawPmxAndRawVmdPaths()
        {
            string pmxPath = Path.Combine(RepositoryRoot, "packages", "com.yohawing.mmd-loader", "Tests", "Fixtures", "Assets", "test_1bone_cube.pmx");
            string vmdPath = Path.Combine(RepositoryRoot, "packages", "com.yohawing.mmd-loader", "Tests", "Fixtures", "Assets", "test_1bone_cube_motion.vmd");

            bool accepted = MmdSceneDragAndDrop.TryGetDraggedSources(
                new Object[0],
                new[] { pmxPath, vmdPath },
                out MmdPmxAsset? pmxAsset,
                out MmdVmdAsset? vmdAsset,
                out string? draggedPmxPath,
                out string? draggedVmdPath);

            Assert.That(accepted, Is.True);
            Assert.That(pmxAsset, Is.Null);
            Assert.That(vmdAsset, Is.Null);
            Assert.That(draggedPmxPath, Is.EqualTo(Path.GetFullPath(pmxPath)));
            Assert.That(draggedVmdPath, Is.EqualTo(Path.GetFullPath(vmdPath)));
        }

        [Test]
        public void SceneDragAndDropRejectsAmbiguousOrUnsupportedRawPaths()
        {
            string pmxPath = Path.Combine(RepositoryRoot, "packages", "com.yohawing.mmd-loader", "Tests", "Fixtures", "Assets", "test_1bone_cube.pmx");
            string vmdPath = Path.Combine(RepositoryRoot, "packages", "com.yohawing.mmd-loader", "Tests", "Fixtures", "Assets", "test_1bone_cube_motion.vmd");
            string unsupportedPath = Path.ChangeExtension(pmxPath, ".txt");

            Assert.That(
                MmdSceneDragAndDrop.TryGetDraggedSources(new Object[0], new[] { pmxPath, pmxPath }, out _, out _, out _),
                Is.False);
            Assert.That(
                MmdSceneDragAndDrop.TryGetDraggedSources(new Object[0], new[] { vmdPath }, out _, out _, out _),
                Is.False);
            Assert.That(
                MmdSceneDragAndDrop.TryGetDraggedSources(new Object[0], new[] { unsupportedPath }, out _, out _, out _),
                Is.False);
            Assert.That(
                MmdSceneDragAndDrop.TryGetDraggedSources(new Object[0], new[] { pmxPath, vmdPath, vmdPath }, out _, out _, out _),
                Is.False);
        }

        [Test]
        public void SceneDragAndDropRejectsRawVmdPathWithoutRawPmxPath()
        {
            string vmdPath = Path.Combine(RepositoryRoot, "packages", "com.yohawing.mmd-loader", "Tests", "Fixtures", "Assets", "test_1bone_cube_motion.vmd");

            bool accepted = MmdSceneDragAndDrop.TryGetDraggedSources(
                new Object[0],
                new[] { vmdPath },
                out MmdPmxAsset? pmxAsset,
                out MmdVmdAsset? vmdAsset,
                out string? draggedPmxPath,
                out string? draggedVmdPath);

            Assert.That(accepted, Is.False);
            Assert.That(pmxAsset, Is.Null);
            Assert.That(vmdAsset, Is.Null);
            Assert.That(draggedPmxPath, Is.Null);
            Assert.That(draggedVmdPath, Is.Null);
        }

        [Test]
        public void SceneDragAndDropRejectsMixedImportedPmxAssetAndRawVmdPath()
        {
            CopyFixtureToAssetDatabase("test_1bone_cube.pmx", TempPmxPath);
            MmdPmxAsset pmxAsset = AssetDatabase.LoadAssetAtPath<MmdPmxAsset>(TempPmxPath);
            string vmdPath = Path.Combine(RepositoryRoot, "packages", "com.yohawing.mmd-loader", "Tests", "Fixtures", "Assets", "test_1bone_cube_motion.vmd");

            bool accepted = MmdSceneDragAndDrop.TryGetDraggedSources(
                new Object[] { pmxAsset },
                new[] { vmdPath },
                out MmdPmxAsset? draggedPmxAsset,
                out MmdVmdAsset? draggedVmdAsset,
                out string? draggedPmxPath,
                out string? draggedVmdPath);

            Assert.That(accepted, Is.False);
            Assert.That(draggedPmxAsset, Is.Null);
            Assert.That(draggedVmdAsset, Is.Null);
            Assert.That(draggedPmxPath, Is.Null);
            Assert.That(draggedVmdPath, Is.Null);
        }

        [Test]
        public void SceneDragAndDropRejectsMixedRawPmxPathAndImportedVmdAsset()
        {
            CopyFixtureToAssetDatabase("test_1bone_cube_motion.vmd", TempVmdPath);
            MmdVmdAsset vmdAsset = AssetDatabase.LoadAssetAtPath<MmdVmdAsset>(TempVmdPath);
            string pmxPath = Path.Combine(RepositoryRoot, "packages", "com.yohawing.mmd-loader", "Tests", "Fixtures", "Assets", "test_1bone_cube.pmx");

            bool accepted = MmdSceneDragAndDrop.TryGetDraggedSources(
                new Object[] { vmdAsset },
                new[] { pmxPath },
                out MmdPmxAsset? draggedPmxAsset,
                out MmdVmdAsset? draggedVmdAsset,
                out string? draggedPmxPath,
                out string? draggedVmdPath);

            Assert.That(accepted, Is.False);
            Assert.That(draggedPmxAsset, Is.Null);
            Assert.That(draggedVmdAsset, Is.Null);
            Assert.That(draggedPmxPath, Is.Null);
            Assert.That(draggedVmdPath, Is.Null);
        }

        [Test]
        public void SceneDragAndDropCalculatesGroundDropPosition()
        {
            var ray = new Ray(new Vector3(2.0f, 4.0f, -3.0f), Vector3.down);

            Vector3 position = MmdSceneDragAndDrop.CalculateGroundDropPosition(ray);

            Assert.That(position.x, Is.EqualTo(2.0f).Within(0.0001f));
            Assert.That(position.y, Is.EqualTo(0.0f).Within(0.0001f));
            Assert.That(position.z, Is.EqualTo(-3.0f).Within(0.0001f));
        }

        [Test]
        public void SceneDragAndDropResolvesHierarchyDropParent()
        {
            var parent = new GameObject("mmd-drop-parent");
            try
            {
#pragma warning disable CS0618 // Legacy InstanceID input remains supported by Hierarchy drop callbacks.
                GameObject? resolved = MmdSceneDragAndDrop.GetHierarchyDropParent(parent.GetInstanceID());
#pragma warning restore CS0618
                GameObject? resolvedByEntityId = MmdSceneDragAndDrop.GetHierarchyDropParent(parent.GetEntityId());

                Assert.That(resolved, Is.SameAs(parent));
                Assert.That(resolvedByEntityId, Is.SameAs(parent));
            }
            finally
            {
                Object.DestroyImmediate(parent);
            }
        }

        [Test]
        public void SceneDragAndDropResolvesHierarchyDropHandlerParent()
        {
            var parent = new GameObject("mmd-drop-handler-parent");
            var sibling = new GameObject("mmd-drop-handler-sibling");
            var forcedParent = new GameObject("mmd-drop-handler-forced-parent");
            sibling.transform.SetParent(parent.transform, worldPositionStays: false);
            try
            {
                GameObject? rootDrop = MmdSceneDragAndDrop.GetHierarchyDropParent(
                    0,
                    HierarchyDropFlags.DropUpon,
                    forcedParent: null);
                GameObject? forcedDrop = MmdSceneDragAndDrop.GetHierarchyDropParent(
                    0,
                    HierarchyDropFlags.None,
                    forcedParent.transform);
#pragma warning disable CS0618 // Legacy InstanceID input remains supported by Hierarchy drop callbacks.
                GameObject? uponDrop = MmdSceneDragAndDrop.GetHierarchyDropParent(
                    parent.GetInstanceID(),
                    HierarchyDropFlags.DropUpon,
                    forcedParent: null);
                GameObject? betweenDrop = MmdSceneDragAndDrop.GetHierarchyDropParent(
                    sibling.GetInstanceID(),
                    HierarchyDropFlags.DropBetween,
                    forcedParent: null);
#pragma warning restore CS0618
                GameObject? entityIdUponDrop = MmdSceneDragAndDrop.GetHierarchyDropParent(
                    parent.GetEntityId(),
                    HierarchyDropFlags.DropUpon,
                    forcedParent: null);
                GameObject? entityIdBetweenDrop = MmdSceneDragAndDrop.GetHierarchyDropParent(
                    sibling.GetEntityId(),
                    HierarchyDropFlags.DropBetween,
                    forcedParent: null);

                Assert.That(rootDrop, Is.Null);
                Assert.That(forcedDrop, Is.SameAs(forcedParent));
                Assert.That(uponDrop, Is.SameAs(parent));
                Assert.That(betweenDrop, Is.SameAs(parent));
                Assert.That(entityIdUponDrop, Is.SameAs(parent));
                Assert.That(entityIdBetweenDrop, Is.SameAs(parent));
            }
            finally
            {
                Object.DestroyImmediate(forcedParent);
                Object.DestroyImmediate(parent);
            }
        }

        [Test]
        public void SceneDragAndDropCalculatesHierarchyDropPosition()
        {
            var parent = new GameObject("mmd-drop-position-parent");
            parent.transform.position = new Vector3(3.0f, 2.0f, -4.0f);
            try
            {
                Vector3 parentPosition = MmdSceneDragAndDrop.CalculateHierarchyDropPosition(parent);
                Vector3 rootPosition = MmdSceneDragAndDrop.CalculateHierarchyDropPosition(parent: null);

                Assert.That(parentPosition.x, Is.EqualTo(3.0f).Within(0.0001f));
                Assert.That(parentPosition.y, Is.EqualTo(2.0f).Within(0.0001f));
                Assert.That(parentPosition.z, Is.EqualTo(-4.0f).Within(0.0001f));
                Assert.That(rootPosition, Is.EqualTo(Vector3.zero));
            }
            finally
            {
                Object.DestroyImmediate(parent);
            }
        }

        [Test]
        public void SceneDragAndDropLoadsPmxUnderHierarchyParent()
        {
            CopyFixtureToAssetDatabase("test_1bone_cube.pmx", TempPmxPath);
            MmdPmxAsset pmxAsset = AssetDatabase.LoadAssetAtPath<MmdPmxAsset>(TempPmxPath);
            var parent = new GameObject("mmd-pmx-drop-parent");
            MmdUnityModelInstance? instance = null;

            try
            {
                instance = MmdSceneDragAndDrop.LoadPmxForDragAndDrop(
                    pmxAsset,
                    new Vector3(5.0f, 0.0f, -2.0f),
                    parent);

                Assert.That(instance.Root.transform.parent, Is.SameAs(parent.transform));
                Assert.That(instance.Root.transform.localPosition, Is.EqualTo(Vector3.zero));
                Assert.That(instance.VertexCount, Is.GreaterThan(0));
                Assert.That(instance.IndexCount, Is.GreaterThan(0));
                Assert.That(instance.BoneTransforms, Has.Length.GreaterThan(0));
                MmdUnityPlaybackController controller = instance.Root.GetComponent<MmdUnityPlaybackController>();
                Assert.That(controller, Is.Not.Null);
                // PMX-only drag stores the model source on the controller.
                Assert.That(controller.HasModelSource, Is.True);
                Assert.That(controller.IsConfigured, Is.False);
                Assert.That(controller.ModelSourceId, Is.EqualTo(TempPmxPath));
                Assert.That(controller.ModelAssetSource, Is.SameAs(pmxAsset));
            }
            finally
            {
                DestroyInstance(instance);
                Object.DestroyImmediate(parent);
            }
        }

        [Test]
        public void SceneDragAndDropLoadsPmxAssetAsImportedPrefabInstance()
        {
            CopyFixtureToAssetDatabase("test_1bone_cube.pmx", TempPmxPath);
            MmdPmxAsset pmxAsset = AssetDatabase.LoadAssetAtPath<MmdPmxAsset>(TempPmxPath);
            Assert.That(pmxAsset.ImportedRoot, Is.Not.Null);
            MmdUnityModelInstance? instance = null;

            try
            {
                instance = MmdSceneDragAndDrop.LoadPmxForDragAndDrop(
                    pmxAsset,
                    new Vector3(1.0f, 0.0f, 2.0f),
                    parent: null);

                Assert.That(instance.Root.transform.parent, Is.Null);
                Assert.That(instance.Root.transform.position, Is.EqualTo(new Vector3(1.0f, 0.0f, 2.0f)));
                Assert.That(PrefabUtility.GetPrefabInstanceStatus(instance.Root), Is.EqualTo(PrefabInstanceStatus.Connected));
                Assert.That(PrefabUtility.GetCorrespondingObjectFromSource(instance.Root), Is.SameAs(pmxAsset.ImportedRoot));
                Assert.That(instance.Mesh, Is.SameAs(pmxAsset.ImportedMesh));
                Assert.That(instance.Materials, Is.Not.Empty);
                Assert.That(instance.Materials[0], Is.SameAs(pmxAsset.ImportedMaterials[0]));

                MmdUnityPlaybackController controller = instance.Root.GetComponent<MmdUnityPlaybackController>();
                Assert.That(controller, Is.Not.Null);
                Assert.That(controller.HasModelSource, Is.True);
                Assert.That(controller.ModelAssetSource, Is.SameAs(pmxAsset));
            }
            finally
            {
                DestroyInstance(instance);
            }
        }

        [Test]
        public void SceneDragAndDropLoadsRawPmxPathUnderHierarchyParent()
        {
            string pmxPath = Path.Combine(RepositoryRoot, "packages", "com.yohawing.mmd-loader", "Tests", "Fixtures", "Assets", "test_1bone_cube.pmx");
            var parent = new GameObject("mmd-raw-pmx-drop-parent");
            MmdUnityModelInstance? instance = null;

            try
            {
                instance = MmdSceneDragAndDrop.LoadPmxPathForDragAndDrop(
                    pmxPath,
                    new Vector3(2.0f, 0.0f, -3.0f),
                    parent);

                Assert.That(instance.Root.transform.parent, Is.SameAs(parent.transform));
                Assert.That(instance.Root.transform.localPosition, Is.EqualTo(Vector3.zero));
                Assert.That(instance.VertexCount, Is.GreaterThan(0));
                Assert.That(instance.IndexCount, Is.GreaterThan(0));
                Assert.That(instance.BoneTransforms, Has.Length.GreaterThan(0));
                MmdUnityPlaybackController controller = instance.Root.GetComponent<MmdUnityPlaybackController>();
                Assert.That(controller, Is.Not.Null);
                // Raw PMX path drag also creates model source provider (via RuntimeImporter).
                Assert.That(controller.HasModelSource, Is.True);
                Assert.That(controller.IsConfigured, Is.False);
                Assert.That(controller.ModelSourceId, Is.EqualTo(Path.GetFullPath(pmxPath)));
                MmdRuntimeImporterComponent importer = controller.GetComponent<MmdRuntimeImporterComponent>();
                Assert.That(importer, Is.Not.Null);
                Assert.That(importer.ModelPath, Is.EqualTo(Path.GetFullPath(pmxPath)));
                Assert.That(importer.MotionPath, Is.Empty);
                Assert.That(importer.LastConfigurationStatus, Is.EqualTo("configured-model-path-assigned"));
            }
            finally
            {
                DestroyInstance(instance);
                Object.DestroyImmediate(parent);
            }
        }

        [Test]
        public void SceneDragAndDropLoadsRawPmxPathAtSceneDropPosition()
        {
            string pmxPath = Path.Combine(RepositoryRoot, "packages", "com.yohawing.mmd-loader", "Tests", "Fixtures", "Assets", "test_1bone_cube.pmx");
            MmdUnityModelInstance? instance = null;

            try
            {
                Vector3 dropPosition = new Vector3(4.0f, 0.0f, -5.0f);
                instance = MmdSceneDragAndDrop.LoadPmxPathForDragAndDrop(
                    pmxPath,
                    dropPosition,
                    parent: null);

                Assert.That(instance.Root.transform.parent, Is.Null);
                Assert.That(instance.Root.transform.position, Is.EqualTo(dropPosition));
                Assert.That(instance.VertexCount, Is.GreaterThan(0));
                MmdUnityPlaybackController controller = instance.Root.GetComponent<MmdUnityPlaybackController>();
                Assert.That(controller, Is.Not.Null);
                Assert.That(controller.ModelSourceId, Is.EqualTo(Path.GetFullPath(pmxPath)));
                MmdRuntimeImporterComponent importer = controller.GetComponent<MmdRuntimeImporterComponent>();
                Assert.That(importer, Is.Not.Null);
                Assert.That(importer.ModelPath, Is.EqualTo(Path.GetFullPath(pmxPath)));
                Assert.That(importer.MotionPath, Is.Empty);
            }
            finally
            {
                DestroyInstance(instance);
            }
        }

        [Test]
        public void SceneDragAndDropLoadsPlaybackUnderHierarchyParent()
        {
            CopyFixtureToAssetDatabase("test_1bone_cube.pmx", TempPmxPath);
            CopyFixtureToAssetDatabase("test_1bone_cube_motion.vmd", TempVmdPath);
            MmdPmxAsset pmxAsset = AssetDatabase.LoadAssetAtPath<MmdPmxAsset>(TempPmxPath);
            MmdVmdAsset vmdAsset = AssetDatabase.LoadAssetAtPath<MmdVmdAsset>(TempVmdPath);
            var parent = new GameObject("mmd-playback-drop-parent");
            MmdEditorPlaybackSceneLoadResult? result = null;

            try
            {
                result = MmdSceneDragAndDrop.LoadPlaybackForDragAndDrop(
                    pmxAsset,
                    vmdAsset,
                    new Vector3(-1.0f, 0.0f, 6.0f),
                    parent);

                Assert.That(result.Instance.Root.transform.parent, Is.SameAs(parent.transform));
                Assert.That(result.Instance.Root.transform.localPosition, Is.EqualTo(Vector3.zero));
                Assert.That(result.Controller.IsConfigured, Is.True);
                Assert.That(result.Controller.ModelAssetSource, Is.SameAs(pmxAsset));
                Assert.That(result.Controller.MotionAssetSource, Is.SameAs(vmdAsset));
                Assert.That(result.Instance.VertexCount, Is.GreaterThan(0));
                Assert.That(result.Motion.maxFrame, Is.GreaterThan(0));
            }
            finally
            {
                DestroyInstance(result?.Instance);
                Object.DestroyImmediate(parent);
            }
        }

        [Test]
        public void SceneDragAndDropAttachesVmdSourceComponentToExistingPmxModel()
        {
            CopyFixtureToAssetDatabase("test_1bone_cube.pmx", TempPmxPath);
            CopyFixtureToAssetDatabase("test_1bone_cube_motion.vmd", TempVmdPath);
            MmdPmxAsset pmxAsset = AssetDatabase.LoadAssetAtPath<MmdPmxAsset>(TempPmxPath);
            MmdVmdAsset vmdAsset = AssetDatabase.LoadAssetAtPath<MmdVmdAsset>(TempVmdPath);
            MmdUnityModelInstance? instance = null;
            MmdVmdAsset? replacementVmd = null;

            try
            {
                instance = MmdSceneDragAndDrop.LoadPmxForDragAndDrop(pmxAsset, Vector3.zero, parent: null);
                MmdUnityPlaybackController controller = instance.Root.GetComponent<MmdUnityPlaybackController>();
                // Precondition after PMX-only drag: controller owns the model source.
                Assert.That(controller.ModelAssetSource, Is.SameAs(pmxAsset));
                Assert.That(controller.HasModelSource, Is.True);
                Assert.That(controller.IsConfigured, Is.False);
                // controller (default Live) is sole source of truth for normal playback
                Renderer[] renderers = instance.Root.GetComponentsInChildren<Renderer>(includeInactive: true);
                GameObject childTarget = instance.Root.transform.childCount > 0
                    ? instance.Root.transform.GetChild(0).gameObject
                    : instance.Root;

                bool attached = MmdSceneDragAndDrop.TryAttachVmdSourceToExistingModel(
                    childTarget,
                    vmdAsset);

                // VMD attach stores motion on the controller.
                Assert.That(attached, Is.True);
                Assert.That(controller.MotionAssetSource, Is.SameAs(vmdAsset));
                Assert.That(controller.IsConfigured, Is.False);
                Assert.That(controller.MotionSourceId, Is.EqualTo(TempVmdPath));

                replacementVmd = ScriptableObject.CreateInstance<MmdVmdAsset>();
                bool updated = MmdSceneDragAndDrop.TryAttachVmdSourceToExistingModel(
                    instance.Root,
                    replacementVmd);

                // Replacement attach updates the controller motion source.
                Assert.That(updated, Is.True);
                Assert.That(controller.MotionAssetSource, Is.SameAs(replacementVmd));

                // Re-attach fixture vmd so that motion-comp provides TempVmdPath identity for the MotionSourceId assert below.
                bool reattachedForConfig = MmdSceneDragAndDrop.TryAttachVmdSourceToExistingModel(instance.Root, vmdAsset);
                Assert.That(reattachedForConfig, Is.True);

                // Controller model+motion source allows ConfigureFrom... and controller configured.
                bool configured = controller.ConfigureFromPlaybackSourceIfAvailable();
                Assert.That(configured, Is.True);
                Assert.That(controller.IsConfigured, Is.True);
                Assert.That(controller.MotionSourceId, Is.EqualTo(TempVmdPath));
                foreach (Renderer renderer in renderers)
                {
                    Assert.That(renderer.enabled, Is.True, renderer.name);
                }
            }
            finally
            {
                if (replacementVmd != null)
                {
                    Object.DestroyImmediate(replacementVmd);
                }

                DestroyInstance(instance);
            }
        }

        [Test]
        public void PmxDragCreatesModelSourceProviderWithoutMotion()
        {
            // Focused test: PMX-only drag/load creates model source provider (not full source).
            // ModelAsset set, MotionAsset null, HasModelSource true, IsConfigured false, ModelSourceId stable.
            CopyFixtureToAssetDatabase("test_1bone_cube.pmx", TempPmxPath);
            MmdPmxAsset pmxAsset = AssetDatabase.LoadAssetAtPath<MmdPmxAsset>(TempPmxPath);
            MmdUnityModelInstance? instance = null;

            try
            {
                instance = MmdSceneDragAndDrop.LoadPmxForDragAndDrop(pmxAsset, Vector3.zero, parent: null);

                MmdUnityPlaybackController controller = instance.Root.GetComponent<MmdUnityPlaybackController>();
                Assert.That(controller, Is.Not.Null);
                Assert.That(controller.HasModelSource, Is.True);
                Assert.That(controller.IsConfigured, Is.False);
                Assert.That(controller.ModelSourceId, Is.EqualTo(TempPmxPath));
                Assert.That(controller.ModelAssetSource, Is.SameAs(pmxAsset));

                // Idempotent re-check of provider id stability (no motion yet).
                string modelIdBefore = controller.ModelSourceId;
                Assert.That(controller.ModelSourceId, Is.EqualTo(modelIdBefore));
            }
            finally
            {
                DestroyInstance(instance);
            }
        }

        [Test]
        public void VmdAttachToModelSourceUpdatesMotionWhilePreservingModelAndSettings()
        {
            // Focused test: VMD attach consumes existing model source provider (no duplicate component),
            // preserves model asset + local playback settings, sets motion asset.
            CopyFixtureToAssetDatabase("test_1bone_cube.pmx", TempPmxPath);
            CopyFixtureToAssetDatabase("test_1bone_cube_motion.vmd", TempVmdPath);
            MmdPmxAsset pmxAsset = AssetDatabase.LoadAssetAtPath<MmdPmxAsset>(TempPmxPath);
            MmdVmdAsset vmdAsset = AssetDatabase.LoadAssetAtPath<MmdVmdAsset>(TempVmdPath);
            MmdUnityModelInstance? instance = null;

            try
            {
                instance = MmdSceneDragAndDrop.LoadPmxForDragAndDrop(pmxAsset, Vector3.zero, parent: null);
                MmdUnityPlaybackController controller = instance.Root.GetComponent<MmdUnityPlaybackController>();
                Assert.That(controller.ModelAssetSource, Is.SameAs(pmxAsset));
                Assert.That(controller.IsConfigured, Is.False);

                // Attach creates/updates the controller motion source.
                bool ok = MmdSceneDragAndDrop.TryAttachVmdSourceToExistingModel(instance.Root, vmdAsset);
                Assert.That(ok, Is.True);
                Assert.That(controller.MotionAssetSource, Is.SameAs(vmdAsset));
                Assert.That(controller.HasModelSource, Is.True);
                Assert.That(controller.IsConfigured, Is.False);
                Assert.That(controller.MotionSourceId, Is.EqualTo(TempVmdPath));
            }
            finally
            {
                DestroyInstance(instance);
            }
        }

        [Test]
        public void SceneDragAndDropLoadsRawPmxAndRawVmdPlaybackUnderHierarchyParent()
        {
            string pmxPath = Path.Combine(RepositoryRoot, "packages", "com.yohawing.mmd-loader", "Tests", "Fixtures", "Assets", "test_1bone_cube.pmx");
            string vmdPath = Path.Combine(RepositoryRoot, "packages", "com.yohawing.mmd-loader", "Tests", "Fixtures", "Assets", "test_1bone_cube_motion.vmd");
            var parent = new GameObject("mmd-raw-playback-drop-parent");
            MmdEditorPlaybackSceneLoadResult? result = null;

            try
            {
                result = MmdSceneDragAndDrop.LoadPlaybackPathForDragAndDrop(
                    pmxPath,
                    vmdPath,
                    new Vector3(7.0f, 0.0f, -8.0f),
                    parent);

                string fullPmxPath = Path.GetFullPath(pmxPath);
                string fullVmdPath = Path.GetFullPath(vmdPath);
                Assert.That(result.Instance.Root.transform.parent, Is.SameAs(parent.transform));
                Assert.That(result.Instance.Root.transform.localPosition, Is.EqualTo(Vector3.zero));
                Assert.That(result.Controller.IsConfigured, Is.True);
                MmdRuntimeImporterComponent importer = result.Controller.GetComponent<MmdRuntimeImporterComponent>();
                Assert.That(importer, Is.Not.Null);
                Assert.That(importer.ModelPath, Is.EqualTo(fullPmxPath));
                Assert.That(importer.MotionPath, Is.EqualTo(fullVmdPath));
                Assert.That(result.Controller.ModelSourceId, Is.EqualTo(fullPmxPath));
                Assert.That(result.Controller.MotionSourceId, Is.EqualTo(fullVmdPath));
                Assert.That(result.ModelPath, Is.EqualTo(fullPmxPath));
                Assert.That(result.MotionPath, Is.EqualTo(fullVmdPath));
                Assert.That(result.InitialSnapshot.frame.frame, Is.EqualTo(0));
                Assert.That(result.Instance.VertexCount, Is.GreaterThan(0));
                Assert.That(result.Motion.maxFrame, Is.GreaterThan(0));
            }
            finally
            {
                DestroyInstance(result?.Instance);
                Object.DestroyImmediate(parent);
            }
        }

        [Test]
        public void HumanoidSetupAssetBuilderCreatesSettingsOnlyAsset()
        {
            CopyFixtureToAssetDatabase("test_1bone_cube.pmx", TempPmxPath);
            MmdPmxAsset pmxAsset = AssetDatabase.LoadAssetAtPath<MmdPmxAsset>(TempPmxPath);

            Assert.That(
                MmdHumanoidSetupAssetBuilder.GetDefaultSetupAssetPath(pmxAsset),
                Is.EqualTo(TempDirectory + "/MmdHumanoidSetup.asset"));

            MmdHumanoidSetupAsset setup = MmdHumanoidSetupAssetBuilder.CreateHumanoidSetupAsset(
                pmxAsset,
                TempHumanoidSetupPath,
                MmdHumanoidSetupPreset.MmdStandard);

            Assert.That(setup, Is.Not.Null);
            Assert.That(AssetDatabase.GetAssetPath(setup), Is.EqualTo(TempHumanoidSetupPath));
            Assert.That(setup.PmxAsset, Is.SameAs(pmxAsset));
            Assert.That(setup.SetupPreset, Is.EqualTo(MmdHumanoidSetupPreset.MmdStandard));
            Assert.That(setup.PmxBoneCount, Is.EqualTo(pmxAsset.BoneCount));
            Assert.That(setup.MappingReadiness, Is.EqualTo(MmdHumanoidSetupAsset.MissingRequiredReadiness));
            Assert.That(setup.RequiredMappedBoneCount, Is.EqualTo(0));
            Assert.That(setup.OptionalMappedBoneCount, Is.EqualTo(0));
            Assert.That(setup.MissingRequiredBoneCount, Is.GreaterThan(0));
            Assert.That(setup.AmbiguousMappingCount, Is.EqualTo(0));
            Assert.That(setup.MappingDiagnostics, Is.Not.Empty);
            Assert.That(setup.NativePlaybackImpact, Is.EqualTo(MmdHumanoidSetupAsset.NoNativePlaybackImpact));
            foreach (System.Reflection.FieldInfo field in typeof(MmdHumanoidSetupAsset).GetFields(
                         System.Reflection.BindingFlags.Instance |
                         System.Reflection.BindingFlags.Public |
                         System.Reflection.BindingFlags.NonPublic))
            {
                Assert.That(field.FieldType, Is.Not.EqualTo(typeof(Avatar)), field.Name);
                Assert.That(field.FieldType, Is.Not.EqualTo(typeof(GameObject)), field.Name);
                Assert.That(field.FieldType, Is.Not.EqualTo(typeof(HumanBodyBones)), field.Name);
            }
        }

        [Test]
        public void HumanoidBoneMappingEvaluatorMapsStandardMmdNamesAndIgnoresHelpers()
        {
            MmdModelDefinition model = CreateHumanoidMappingModel(
                "下半身",
                "上半身",
                "首",
                "頭",
                "左足",
                "左ひざ",
                "左足首",
                "右足",
                "右ひざ",
                "右足首",
                "左腕",
                "左ひじ",
                "左手首",
                "右腕",
                "右ひじ",
                "右手首",
                "上半身2",
                "左肩",
                "右肩",
                "左足ＩＫ",
                "右腕捩",
                "左目操作");

            MmdHumanoidBoneMappingReport report = MmdHumanoidBoneMappingEvaluator.Evaluate(model);

            Assert.That(report.Readiness, Is.EqualTo(MmdHumanoidSetupAsset.ReadyReadiness));
            Assert.That(report.RequiredMappedBoneCount, Is.EqualTo(16));
            Assert.That(report.OptionalMappedBoneCount, Is.EqualTo(3));
            Assert.That(report.MissingRequiredBoneCount, Is.EqualTo(0));
            Assert.That(report.AmbiguousMappingCount, Is.EqualTo(0));
            Assert.That(report.IgnoredHelperBoneCount, Is.EqualTo(3));

            Assert.That(MmdHumanoidBoneMappingEvaluator.TryMapBoneName("左足首", out HumanBodyBones humanBone, out bool required), Is.True);
            Assert.That(humanBone, Is.EqualTo(HumanBodyBones.LeftFoot));
            Assert.That(required, Is.True);
            Assert.That(MmdHumanoidBoneMappingEvaluator.TryMapBoneName("左足ＩＫ", out _, out _), Is.False);
        }

        [Test]
        public void HumanoidBoneMappingEvaluatorPinsShoulderToeEyeOptionalEntries()
        {
            MmdModelDefinition model = CreateHumanoidMappingModel(
                "下半身",
                "上半身",
                "首",
                "頭",
                "左足",
                "左ひざ",
                "左足首",
                "右足",
                "右ひざ",
                "右足首",
                "左腕",
                "左ひじ",
                "左手首",
                "右腕",
                "右ひじ",
                "右手首",
                "上半身2",
                "左肩",
                "右肩",
                "左つま先",
                "右つま先",
                "左目",
                "右目");

            MmdHumanoidBoneMappingReport report = MmdHumanoidBoneMappingEvaluator.Evaluate(model);

            Assert.That(report.Readiness, Is.EqualTo(MmdHumanoidSetupAsset.ReadyReadiness));
            Assert.That(report.OptionalMappedBoneCount, Is.EqualTo(7));

            Assert.That(MmdHumanoidBoneMappingEvaluator.TryMapBoneName("左肩", out HumanBodyBones shoulderLeft, out bool shoulderLeftRequired),
                Is.True);
            Assert.That(shoulderLeft, Is.EqualTo(HumanBodyBones.LeftShoulder));
            Assert.That(shoulderLeftRequired, Is.False);

            Assert.That(MmdHumanoidBoneMappingEvaluator.TryMapBoneName("右肩", out HumanBodyBones shoulderRight, out bool shoulderRightRequired),
                Is.True);
            Assert.That(shoulderRight, Is.EqualTo(HumanBodyBones.RightShoulder));
            Assert.That(shoulderRightRequired, Is.False);

            Assert.That(MmdHumanoidBoneMappingEvaluator.TryMapBoneName("左つま先", out HumanBodyBones toeLeft, out bool toeLeftRequired),
                Is.True);
            Assert.That(toeLeft, Is.EqualTo(HumanBodyBones.LeftToes));
            Assert.That(toeLeftRequired, Is.False);

            Assert.That(MmdHumanoidBoneMappingEvaluator.TryMapBoneName("右つま先", out HumanBodyBones toeRight, out bool toeRightRequired),
                Is.True);
            Assert.That(toeRight, Is.EqualTo(HumanBodyBones.RightToes));
            Assert.That(toeRightRequired, Is.False);

            Assert.That(MmdHumanoidBoneMappingEvaluator.TryMapBoneName("左目", out HumanBodyBones eyeLeft, out bool eyeLeftRequired),
                Is.True);
            Assert.That(eyeLeft, Is.EqualTo(HumanBodyBones.LeftEye));
            Assert.That(eyeLeftRequired, Is.False);

            Assert.That(MmdHumanoidBoneMappingEvaluator.TryMapBoneName("右目", out HumanBodyBones eyeRight, out bool eyeRightRequired),
                Is.True);
            Assert.That(eyeRight, Is.EqualTo(HumanBodyBones.RightEye));
            Assert.That(eyeRightRequired, Is.False);

            bool hasShoulderLeftEntry = false;
            bool hasShoulderRightEntry = false;
            bool hasToeLeftEntry = false;
            bool hasToeRightEntry = false;
            bool hasEyeLeftEntry = false;
            bool hasEyeRightEntry = false;
            bool hasChestEntry = false;

            foreach (MmdSerializableBoneMappingEntry entry in report.MappingEntries)
            {
                if (entry.Category != "Optional")
                {
                    continue;
                }

                Assert.That(entry.Required, Is.False);

                switch (entry.MmdBoneName)
                {
                    case "上半身2":
                        hasChestEntry = true;
                        break;
                    case "左肩":
                        hasShoulderLeftEntry = true;
                        break;
                    case "右肩":
                        hasShoulderRightEntry = true;
                        break;
                    case "左つま先":
                        hasToeLeftEntry = true;
                        break;
                    case "右つま先":
                        hasToeRightEntry = true;
                        break;
                    case "左目":
                        hasEyeLeftEntry = true;
                        break;
                    case "右目":
                        hasEyeRightEntry = true;
                        break;
                }
            }

            Assert.That(hasChestEntry, Is.True);
            Assert.That(hasShoulderLeftEntry, Is.True);
            Assert.That(hasShoulderRightEntry, Is.True);
            Assert.That(hasToeLeftEntry, Is.True);
            Assert.That(hasToeRightEntry, Is.True);
            Assert.That(hasEyeLeftEntry, Is.True);
            Assert.That(hasEyeRightEntry, Is.True);
        }

        [Test]
        public void HumanoidBoneMappingEvaluatorReportsMissingRequiredAndAmbiguousMappings()
        {
            MmdModelDefinition missingModel = CreateHumanoidMappingModel("下半身", "上半身", "頭");
            MmdHumanoidBoneMappingReport missingReport = MmdHumanoidBoneMappingEvaluator.Evaluate(missingModel);

            Assert.That(missingReport.Readiness, Is.EqualTo(MmdHumanoidSetupAsset.MissingRequiredReadiness));
            Assert.That(missingReport.RequiredMappedBoneCount, Is.EqualTo(3));
            Assert.That(missingReport.MissingRequiredBoneCount, Is.EqualTo(13));
            Assert.That(string.Join("\n", missingReport.Diagnostics), Does.Contain("missing-required"));

            MmdModelDefinition ambiguousModel = CreateHumanoidMappingModel(
                "下半身",
                "下半身",
                "上半身",
                "首",
                "頭",
                "左足",
                "左ひざ",
                "左足首",
                "右足",
                "右ひざ",
                "右足首",
                "左腕",
                "左ひじ",
                "左手首",
                "右腕",
                "右ひじ",
                "右手首");
            MmdHumanoidBoneMappingReport ambiguousReport = MmdHumanoidBoneMappingEvaluator.Evaluate(ambiguousModel);

            Assert.That(ambiguousReport.Readiness, Is.EqualTo(MmdHumanoidSetupAsset.AmbiguousReadiness));
            Assert.That(ambiguousReport.AmbiguousMappingCount, Is.EqualTo(1));
            Assert.That(string.Join("\n", ambiguousReport.Diagnostics), Does.Contain("ambiguous: Hips"));
        }

        [Test]
        public void HumanoidSetupAssetBuilderRejectsTraversalPathBeforeCreatingDirectories()
        {
            CopyFixtureToAssetDatabase("test_1bone_cube.pmx", TempPmxPath);
            MmdPmxAsset pmxAsset = AssetDatabase.LoadAssetAtPath<MmdPmxAsset>(TempPmxPath);
            string outsideDirectoryName = "__MmdHumanoidTraversalShouldNotExist_" + System.Guid.NewGuid().ToString("N");
            string traversalAssetPath = "Assets/../" + outsideDirectoryName + "/humanoid.asset";

            Assert.Throws<System.ArgumentException>(
                () => MmdHumanoidSetupAssetBuilder.CreateHumanoidSetupAsset(pmxAsset, traversalAssetPath));
            Assert.That(Directory.Exists(Path.Combine(ProjectRoot, outsideDirectoryName)), Is.False);
        }


        [Test]
        public void HumanoidSetupAssetUsesCustomMetadataInspector()
        {
            MmdHumanoidSetupAsset setup = ScriptableObject.CreateInstance<MmdHumanoidSetupAsset>();
            try
            {
                UnityEditor.Editor editor = UnityEditor.Editor.CreateEditor(setup);

                Assert.That(editor, Is.TypeOf<MmdHumanoidSetupAssetEditor>());
                editor.OnInspectorGUI();
            }
            finally
            {
                Object.DestroyImmediate(setup);
            }
        }

        [Test]
        public void HumanoidBoneMappingEvaluatorReportCarriesMappingEntriesForRequiredOptionalAndFinger()
        {
            // Required: 下半身, 上半身, 首, 頭 (4)
            // Optional: 上半身2, 左肩 (2)
            // Finger:   左親指１, 左親指２, 右親指１, 右人指先 (4)
            MmdModelDefinition model = CreateHumanoidMappingModel(
                "下半身",
                "上半身",
                "首",
                "頭",
                "上半身2",
                "左肩",
                "左親指１",
                "左親指２",
                "右親指１",
                "右人指先");

            MmdHumanoidBoneMappingReport report = MmdHumanoidBoneMappingEvaluator.Evaluate(model);

            MmdSerializableBoneMappingEntry[] entries = report.MappingEntries;
            int requiredCount = 0, optionalCount = 0, fingerCount = 0;
            foreach (MmdSerializableBoneMappingEntry entry in entries)
            {
                Assert.That(entry.MmdBoneName, Is.Not.Empty);
                Assert.That(entry.MmdBoneIndex, Is.GreaterThanOrEqualTo(0));
                switch (entry.Category)
                {
                    case "Required":
                        Assert.That(entry.Required, Is.True);
                        requiredCount++;
                        break;
                    case "Optional":
                        Assert.That(entry.Required, Is.False);
                        optionalCount++;
                        break;
                    case "Finger":
                        Assert.That(entry.Required, Is.False);
                        fingerCount++;
                        break;
                    default:
                        Assert.Fail($"Unexpected category: {entry.Category}");
                        break;
                }
            }

            Assert.That(requiredCount, Is.EqualTo(4), "should have 4 required entries");
            Assert.That(optionalCount, Is.EqualTo(2), "should have 2 optional entries");
            Assert.That(fingerCount, Is.EqualTo(4), "should have 4 finger entries");
            Assert.That(entries.Length, Is.EqualTo(10), "total 10 mapping entries");
        }

        [Test]
        public void HumanoidSetupAssetStoresMappingEntriesAfterInitialize()
        {
            CopyFixtureToAssetDatabase("test_1bone_cube.pmx", TempPmxPath);
            MmdPmxAsset pmxAsset = AssetDatabase.LoadAssetAtPath<MmdPmxAsset>(TempPmxPath);

            MmdHumanoidSetupAsset setup = MmdHumanoidSetupAssetBuilder.CreateHumanoidSetupAsset(
                pmxAsset,
                TempHumanoidSetupPath,
                MmdHumanoidSetupPreset.MmdStandard);

            Assert.That(setup.MappingEntries, Is.Not.Null);
            // test_1bone_cube.pmx has 1 bone -> at most 1 matching entry
            Assert.That(setup.MappingEntries.Count, Is.GreaterThanOrEqualTo(0));
            Assert.That(setup.MappingEntries.Count, Is.LessThanOrEqualTo(1));

            // Verify public read-only exposure via foreach and indexer
            var entries = setup.MappingEntries;
            foreach (MmdSerializableBoneMappingEntry entry in entries)
            {
                Assert.That(entry.HumanBone, Is.Not.EqualTo(HumanBodyBones.LastBone));
                Assert.That(entry.MmdBoneName, Is.Not.Empty);
                Assert.That(entry.Category, Is.Not.Empty);
            }
        }

        private static MmdModelDefinition CreateHumanoidMappingModel(params string[] boneNames)
        {
            var model = new MmdModelDefinition();
            for (int i = 0; i < boneNames.Length; i++)
            {
                model.bones.Add(new MmdBoneDefinition
                {
                    index = i,
                    name = boneNames[i],
                    parentIndex = i - 1,
                });
            }

            return model;
        }

        [Test]
        public void PmxPrefabExporterCreatesPrefabWithPersistentMeshMaterialAndProvenance()
        {
            CopyFixtureToAssetDatabase("test_1bone_cube.pmx", TempPmxPath);
            MmdPmxAsset pmxAsset = AssetDatabase.LoadAssetAtPath<MmdPmxAsset>(TempPmxPath);
            Scene activeScene = SceneManager.GetActiveScene();
            bool wasDirty = activeScene.isDirty;

            MmdPmxPrefabExportResult result = MmdPmxPrefabExporter.CreatePrefab(pmxAsset, TempPrefabPath);

            Assert.That(result.PrefabAssetPath, Is.EqualTo(TempPrefabPath));
            Assert.That(result.OutputDirectory, Is.EqualTo(TempDirectory));
            Assert.That(result.MeshAssetPaths, Has.Length.EqualTo(1));
            Assert.That(result.MaterialAssetPaths, Has.Length.GreaterThan(0));
            Assert.That(result.TextureAssetPaths, Is.Empty);
            Assert.That(result.TextureCopyPolicy, Is.EqualTo(MmdPmxPrefabProvenance.TextureCopyPolicy));
            Assert.That(result.PmxSourceId, Is.EqualTo(TempPmxPath));
            Assert.That(result.VertexCount, Is.GreaterThan(0));
            Assert.That(result.MaterialCount, Is.EqualTo(result.MaterialAssetPaths.Length));
            Assert.That(activeScene.isDirty, Is.EqualTo(wasDirty));

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TempPrefabPath);
            Assert.That(prefab, Is.Not.Null);
            var provenance = prefab.GetComponent<MmdPmxPrefabProvenance>();
            Assert.That(provenance, Is.Not.Null);
            Assert.That(provenance.SourceAsset, Is.SameAs(pmxAsset));
            Assert.That(provenance.SourceId, Is.EqualTo(TempPmxPath));
            Assert.That(provenance.PrefabAssetPath, Is.EqualTo(TempPrefabPath));
            Assert.That(provenance.MeshAssetPaths, Is.EqualTo(result.MeshAssetPaths));
            Assert.That(provenance.MaterialAssetPaths, Is.EqualTo(result.MaterialAssetPaths));
            Assert.That(provenance.TextureAssetPaths, Is.Empty);
            Assert.That(provenance.CurrentExportOperationPolicy, Is.EqualTo(MmdPmxPrefabProvenance.ExportOperationPolicy));

            Mesh meshAsset = AssetDatabase.LoadAssetAtPath<Mesh>(result.MeshAssetPaths[0]);
            Assert.That(meshAsset, Is.Not.Null);
            Assert.That(AssetDatabase.Contains(meshAsset), Is.True);
            foreach (string materialAssetPath in result.MaterialAssetPaths)
            {
                Material materialAsset = AssetDatabase.LoadAssetAtPath<Material>(materialAssetPath);
                Assert.That(materialAsset, Is.Not.Null, materialAssetPath);
                Assert.That(AssetDatabase.Contains(materialAsset), Is.True, materialAssetPath);
            }

            GameObject loadedPrefab = PrefabUtility.LoadPrefabContents(TempPrefabPath);
            try
            {
                MmdUnityPlaybackController controller = loadedPrefab.GetComponent<MmdUnityPlaybackController>();
                Assert.That(controller, Is.Not.Null);
                Assert.That(controller.ModelAssetSource, Is.SameAs(pmxAsset));
                AssertNoMissingScripts(loadedPrefab);

                var renderer = loadedPrefab.GetComponentInChildren<Renderer>(includeInactive: true);
                Assert.That(renderer, Is.Not.Null);
                var skinned = renderer as SkinnedMeshRenderer;
                var meshFilter = renderer.GetComponent<MeshFilter>();
                Mesh? prefabMesh = skinned != null ? skinned.sharedMesh : meshFilter != null ? meshFilter.sharedMesh : null;
                Assert.That(prefabMesh, Is.SameAs(meshAsset));
                Assert.That(renderer.sharedMaterials, Has.Length.EqualTo(result.MaterialAssetPaths.Length));
                foreach (Material material in renderer.sharedMaterials)
                {
                    Assert.That(AssetDatabase.Contains(material), Is.True);
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(loadedPrefab);
            }
        }

        [Test]
        public void PmxPrefabExporterFeedbackPathCreatesPrefabAndSelectsIt()
        {
            CopyFixtureToAssetDatabase("test_1bone_cube.pmx", TempPmxPath);
            MmdPmxAsset pmxAsset = AssetDatabase.LoadAssetAtPath<MmdPmxAsset>(TempPmxPath);

            MmdPmxPrefabExportResult? result = MmdPmxPrefabExporter.CreatePrefabWithFeedback(pmxAsset);

            Assert.That(result, Is.Not.Null);
            Assert.That(result!.PrefabAssetPath, Does.EndWith(".prefab"));
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(result.PrefabAssetPath);
            Assert.That(prefab, Is.Not.Null);
            Assert.That(Selection.activeObject, Is.SameAs(prefab));
        }

        [Test]
        public void PmxPrefabExporterRejectsTraversalPathBeforeCreatingDirectories()
        {
            CopyFixtureToAssetDatabase("test_1bone_cube.pmx", TempPmxPath);
            MmdPmxAsset pmxAsset = AssetDatabase.LoadAssetAtPath<MmdPmxAsset>(TempPmxPath);
            string outsideDirectoryName = "__MmdPrefabTraversalShouldNotExist_" + System.Guid.NewGuid().ToString("N");
            string traversalPrefabPath = "Assets/../" + outsideDirectoryName + "/model.prefab";

            Assert.Throws<System.ArgumentException>(
                () => MmdPmxPrefabExporter.CreatePrefab(pmxAsset, traversalPrefabPath));
            Assert.That(Directory.Exists(Path.Combine(ProjectRoot, outsideDirectoryName)), Is.False);
        }

        private static void CopyFixtureToAssetDatabase(string fixtureName, string destinationAssetPath)
        {
            string source = MmdTestFixtures.FixtureAssetPath(fixtureName);
            Directory.CreateDirectory(Path.Combine(ProjectRoot, TempDirectory));
            File.Copy(source, Path.Combine(ProjectRoot, destinationAssetPath), overwrite: true);
            AssetDatabase.ImportAsset(destinationAssetPath, ImportAssetOptions.ForceUpdate);
        }

        private static void SetPmxImporterAnimationType(string assetPath, MmdPmxAnimationType value)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as MmdPmxScriptedImporter;
            Assert.That(importer, Is.Not.Null);

            var serializedImporter = new SerializedObject(importer!);
            serializedImporter.FindProperty("animationType").enumValueIndex = (int)value;
            serializedImporter.ApplyModifiedPropertiesWithoutUndo();
            importer!.SaveAndReimport();
        }

        private static System.Collections.Generic.List<Avatar> GetAvatarSubAssets(string assetPath)
        {
            var avatarSubAssets = new System.Collections.Generic.List<Avatar>();
            foreach (Object subAsset in AssetDatabase.LoadAllAssetsAtPath(assetPath))
            {
                if (subAsset is Avatar avatar)
                {
                    avatarSubAssets.Add(avatar);
                }
            }

            return avatarSubAssets;
        }

        private static Transform? FindBoneByName(Transform[] bones, string name)
        {
            foreach (Transform bone in bones)
            {
                if (bone != null && string.Equals(bone.name, name, StringComparison.Ordinal))
                {
                    return bone;
                }
            }

            return null;
        }

        private static bool IsAcceptedHipsTranslationTargetName(string name)
        {
            return string.Equals(name, "センター", StringComparison.Ordinal)
                   || string.Equals(name, "グルーブ", StringComparison.Ordinal)
                   || string.Equals(name, "全ての親", StringComparison.Ordinal)
                   || string.Equals(name, "腰", StringComparison.Ordinal)
                   || string.Equals(name, "下半身", StringComparison.Ordinal);
        }

        private static string ProjectRoot => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

        private static string RepositoryRoot => Path.GetFullPath(Path.Combine(ProjectRoot, ".."));

        private static int CountMaterials(MmdModelDefinition model, Func<MmdMaterialDefinition, bool> predicate)
        {
            int count = 0;
            if (model.materials == null)
            {
                return count;
            }

            foreach (MmdMaterialDefinition material in model.materials)
            {
                if (predicate(material))
                {
                    count++;
                }
            }

            return count;
        }

        private static Bounds CalculateMmdBounds(MmdModelDefinition model)
        {
            bool hasVertex = false;
            var bounds = new Bounds(Vector3.zero, Vector3.zero);
            foreach (MmdVertexDefinition vertex in model.vertices)
            {
                if (vertex.position == null || vertex.position.Length < 3)
                {
                    continue;
                }

                var position = new Vector3(vertex.position[0], vertex.position[1], vertex.position[2]);
                if (!float.IsFinite(position.x) || !float.IsFinite(position.y) || !float.IsFinite(position.z))
                {
                    continue;
                }

                if (!hasVertex)
                {
                    bounds = new Bounds(position, Vector3.zero);
                    hasVertex = true;
                }
                else
                {
                    bounds.Encapsulate(position);
                }
            }

            return bounds;
        }

        private static void AssertVector3(Vector3 actual, Vector3 expected)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(0.0001f));
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(0.0001f));
            Assert.That(actual.z, Is.EqualTo(expected.z).Within(0.0001f));
        }

        private static void DestroyInstance(MmdUnityModelInstance? instance)
        {
            if (instance == null)
            {
                return;
            }

            if (instance.Root != null)
            {
                Object.DestroyImmediate(instance.Root);
            }

            if (instance.Mesh != null && !AssetDatabase.Contains(instance.Mesh))
            {
                Object.DestroyImmediate(instance.Mesh);
            }

            foreach (Material material in instance.Materials)
            {
                if (material != null && !AssetDatabase.Contains(material))
                {
                    Object.DestroyImmediate(material);
                }
            }

            foreach (Texture2D texture in instance.OwnedTextures)
            {
                if (texture != null && !AssetDatabase.Contains(texture))
                {
                    Object.DestroyImmediate(texture);
                }
            }
        }

        private static Material CreateTestMaterial(string name)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit")
                ?? Shader.Find("Standard")
                ?? Shader.Find("Sprites/Default");
            Assert.That(shader, Is.Not.Null);
            var material = new Material(shader)
            {
                name = name
            };
            return material;
        }

        private static MmdModelDefinition CreateTexturedTriangleModel(string texture)
        {
            var model = new MmdModelDefinition
            {
                name = "importer-textured-triangle"
            };
            model.bones.Add(new MmdBoneDefinition
            {
                index = 0,
                name = "root",
                parentIndex = -1,
                transformOrder = 0,
                origin = new[] { 0.0f, 0.0f, 0.0f },
                isMovable = true,
                isRotatable = true
            });
            model.vertices.Add(CreateVertex(0, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f));
            model.vertices.Add(CreateVertex(1, 1.0f, 0.0f, 0.0f, 1.0f, 0.0f));
            model.vertices.Add(CreateVertex(2, 0.0f, 1.0f, 0.0f, 0.0f, 1.0f));
            model.indices.AddRange(new[] { 0, 1, 2 });
            model.materials.Add(new MmdMaterialDefinition
            {
                index = 0,
                name = "textured-triangle-material",
                texture = texture,
                vertexCount = 3
            });
            return model;
        }

        private static MmdVertexDefinition CreateVertex(
            int index,
            float x,
            float y,
            float z,
            float u,
            float v)
        {
            return new MmdVertexDefinition
            {
                index = index,
                position = new[] { x, y, z },
                normal = new[] { 0.0f, 0.0f, 1.0f },
                uv = new[] { u, v },
                boneIndices = new[] { 0 },
                boneWeights = new[] { 1.0f }
            };
        }

        private static string CreateTempDirectory()
        {
            string tempRoot = Path.Combine(Path.GetTempPath(), "yohawing-mmd-unity-importer-tests", Path.GetRandomFileName());
            Directory.CreateDirectory(tempRoot);
            return tempRoot;
        }

        private static void WritePng(string path, Color color)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false);
            try
            {
                texture.SetPixels(new[] { color, color, color, color });
                texture.Apply();
                File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }

        private static Texture? ReadBoundDiffuseTexture(Material material)
        {
            if (material.HasProperty("_BaseMap"))
            {
                Texture texture = material.GetTexture("_BaseMap");
                if (texture != null)
                {
                    return texture;
                }
            }

            return material.HasProperty("_MainTex")
                ? material.GetTexture("_MainTex")
                : null;
        }

        private static float ReadMaterialFloat(Material material, string propertyName)
        {
            return material.HasProperty(propertyName)
                ? material.GetFloat(propertyName)
                : -1.0f;
        }

        // Hierarchy Readiness Slice C tests.

        [Test]
        public void ImportedPmxAssetReportsReadyForAllReadinessFields()
        {
            // The existing test_1bone_cube fixture produces an importer-owned hierarchy
            // with SkinnedMeshRenderer + bones. All three readiness categories must be Ready.
            CopyFixtureToAssetDatabase("test_1bone_cube.pmx", TempPmxPath);

            MmdPmxAsset pmxAsset = AssetDatabase.LoadAssetAtPath<MmdPmxAsset>(TempPmxPath);
            Assert.That(pmxAsset, Is.Not.Null);

            Assert.That(pmxAsset.HierarchyReadiness, Is.EqualTo(MmdImportReadiness.Ready),
                "ImportedRoot exists with children -> Ready");
            Assert.That(pmxAsset.RendererReadiness, Is.EqualTo(MmdImportReadiness.Ready),
                "SkinnedMeshRenderer exists with sharedMesh -> Ready");
            Assert.That(pmxAsset.BoneBindingReadiness, Is.EqualTo(MmdImportReadiness.Ready),
                "renderer.bones has no nulls, count matches -> Ready");

            Assert.That(pmxAsset.HierarchyReadinessDiagnostic, Does.Contain("ImportedRoot"),
                "Hierarchy diagnostic should mention ImportedRoot");
            Assert.That(pmxAsset.RendererReadinessDiagnostic, Does.Contain("SkinnedMeshRenderer"),
                "Renderer diagnostic should mention SkinnedMeshRenderer");
            Assert.That(pmxAsset.BoneBindingReadinessDiagnostic, Does.Contain("bone(s)"),
                "Bone binding diagnostic should mention bone count");

            // Verify reimport preserves readiness
            var importer = AssetImporter.GetAtPath(TempPmxPath) as MmdPmxScriptedImporter;
            Assert.That(importer, Is.Not.Null);
            importer!.SaveAndReimport();

            MmdPmxAsset pmxAssetAfter = AssetDatabase.LoadAssetAtPath<MmdPmxAsset>(TempPmxPath);
            Assert.That(pmxAssetAfter.HierarchyReadiness, Is.EqualTo(MmdImportReadiness.Ready));
            Assert.That(pmxAssetAfter.RendererReadiness, Is.EqualTo(MmdImportReadiness.Ready));
            Assert.That(pmxAssetAfter.BoneBindingReadiness, Is.EqualTo(MmdImportReadiness.Ready));
        }

        [Test]
        public void NewUninitializedMmdPmxAssetReportsNotEvaluated()
        {
            // A fresh ScriptableObject (not initialized via Initialize) must report NotEvaluated.
            var asset = ScriptableObject.CreateInstance<MmdPmxAsset>();
            try
            {
                Assert.That(asset.HierarchyReadiness, Is.EqualTo(MmdImportReadiness.NotEvaluated));
                Assert.That(asset.RendererReadiness, Is.EqualTo(MmdImportReadiness.NotEvaluated));
                Assert.That(asset.BoneBindingReadiness, Is.EqualTo(MmdImportReadiness.NotEvaluated));
                Assert.That(asset.HierarchyReadinessDiagnostic, Is.EqualTo(string.Empty));
                Assert.That(asset.RendererReadinessDiagnostic, Is.EqualTo(string.Empty));
                Assert.That(asset.BoneBindingReadinessDiagnostic, Is.EqualTo(string.Empty));
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void ComputeReadinessDetectsNullBonesInRenderer()
        {
            // Simulate a hierarchy with a SkinnedMeshRenderer that has a null bone.
            var root = new GameObject("RootWithNullBone");
            try
            {
                var modelObject = new GameObject("Model");
                modelObject.transform.SetParent(root.transform, worldPositionStays: false);
                SkinnedMeshRenderer smr = modelObject.AddComponent<SkinnedMeshRenderer>();
                var mesh = new Mesh();
                mesh.vertices = new Vector3[3];
                mesh.bindposes = new Matrix4x4[2];
                smr.sharedMesh = mesh;
                // Set bones with one null
                smr.bones = new Transform?[] { new GameObject("BoneA").transform, null };

                MmdPmxAsset.ComputeHierarchyReadiness(
                    root,
                    assetBoneCount: 2,
                    out MmdImportReadiness hierarchy,
                    out MmdImportReadiness renderer,
                    out MmdImportReadiness binding,
                    out string _, out string _, out string bindingDiag);

                Assert.That(binding, Is.EqualTo(MmdImportReadiness.Blocked));
                Assert.That(bindingDiag, Does.Contain("null bone(s)"));

                Object.DestroyImmediate(mesh);
                Object.DestroyImmediate(smr.bones[0].gameObject);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ComputeReadinessDetectsBoneCountMismatch()
        {
            // Simulate a hierarchy where renderer.bones length != asset bone count.
            var root = new GameObject("RootBoneMismatch");
            try
            {
                var modelObject = new GameObject("Model");
                modelObject.transform.SetParent(root.transform, worldPositionStays: false);
                SkinnedMeshRenderer smr = modelObject.AddComponent<SkinnedMeshRenderer>();
                var mesh = new Mesh();
                mesh.vertices = new Vector3[3];
                mesh.bindposes = new Matrix4x4[1]; // 1 bindpose
                smr.sharedMesh = mesh;
                smr.bones = new Transform[] { new GameObject("Bone0").transform };

                MmdPmxAsset.ComputeHierarchyReadiness(
                    root,
                    assetBoneCount: 3, // asset says 3 bones, but only 1 in renderer
                    out MmdImportReadiness hierarchy,
                    out MmdImportReadiness renderer,
                    out MmdImportReadiness binding,
                    out string _, out string _, out string bindingDiag);

                Assert.That(binding, Is.EqualTo(MmdImportReadiness.Blocked));
                Assert.That(bindingDiag, Does.Contain("Bone count mismatch"));

                Object.DestroyImmediate(mesh);
                Object.DestroyImmediate(smr.bones[0].gameObject);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        // Humanoid setup source-slice tests.

        [Test]
        public void ImportedPmxHumanoidSetupUsesImportedHierarchyMappingSource()
        {
            CopyFixtureToAssetDatabase("test_1bone_cube.pmx", TempPmxPath);

            MmdPmxAsset? loadedPmxAsset = AssetDatabase.LoadAssetAtPath<MmdPmxAsset>(TempPmxPath);
            Assert.That(loadedPmxAsset, Is.Not.Null);
            MmdPmxAsset pmxAsset = loadedPmxAsset!;
            Assert.That(pmxAsset.ImportedRoot, Is.Not.Null);

            GameObject importedRoot = pmxAsset.ImportedRoot!;
            SkinnedMeshRenderer? nullableSmr = importedRoot.GetComponentInChildren<SkinnedMeshRenderer>(includeInactive: true);
            Assert.That(nullableSmr, Is.Not.Null);
            SkinnedMeshRenderer smr = nullableSmr!;

            var setup = ScriptableObject.CreateInstance<MmdHumanoidSetupAsset>();
            try
            {
                Assert.That(() => setup.Initialize(pmxAsset), Throws.Nothing);
                Assert.That(setup.MappingInputSource, Is.EqualTo(MmdHumanoidSetupAsset.ImportedHierarchyInputSource));

                foreach (MmdSerializableBoneMappingEntry entry in setup.MappingEntries)
                {
                    Assert.That(entry.MmdBoneIndex, Is.GreaterThanOrEqualTo(0));
                    Assert.That(entry.MmdBoneIndex, Is.LessThan(smr.bones.Length));
                    Assert.That(smr.bones[entry.MmdBoneIndex], Is.Not.Null);
                    Assert.That(smr.bones[entry.MmdBoneIndex].name, Is.EqualTo(entry.MmdBoneName));
                }
            }
            finally
            {
                Object.DestroyImmediate(setup);
            }
        }

        [Test]
        public void HumanoidSetupInitializeWithoutImportedRootReportsHierarchyNotReady()
        {
            var pmxAsset = ScriptableObject.CreateInstance<MmdPmxAsset>();
            var setup = ScriptableObject.CreateInstance<MmdHumanoidSetupAsset>();

            try
            {
                Assert.That(() => setup.Initialize(pmxAsset), Throws.Nothing);

                Assert.That(setup.MappingReadiness, Is.EqualTo(MmdHumanoidSetupAsset.HierarchyNotReadyReadiness));
                Assert.That(string.Join("\n", setup.MappingDiagnostics), Does.Contain("hierarchy-not-ready"));
                Assert.That(setup.MappingInputSource, Is.Not.EqualTo(MmdHumanoidSetupAsset.ImportedHierarchyInputSource));
            }
            finally
            {
                Object.DestroyImmediate(setup);
                Object.DestroyImmediate(pmxAsset);
            }
        }

        [Test]
        public void HumanoidSetupPmxAssetReferenceSurvivesPmxSaveAndReimport()
        {
            // D1 regression pin: when .pmx main object is later exchanged to GameObject,
            // MmdPmxAsset becomes a sub-asset. This test verifies that currently, serialized
            // references from MmdHumanoidSetupAsset to MmdPmxAsset survive SaveAndReimport.
            CopyFixtureToAssetDatabase("test_1bone_cube.pmx", TempPmxPath);

            MmdPmxAsset pmxAsset = AssetDatabase.LoadAssetAtPath<MmdPmxAsset>(TempPmxPath);
            Assert.That(pmxAsset, Is.Not.Null,
                "D1 pin: LoadAssetAtPath<MmdPmxAsset> must resolve the metadata asset");

            MmdHumanoidSetupAsset setup = MmdHumanoidSetupAssetBuilder.CreateHumanoidSetupAsset(
                pmxAsset,
                TempHumanoidSetupPath,
                MmdHumanoidSetupPreset.MmdSemiStandard);

            // Pin: reference is valid immediately after creation
            Assert.That(setup.PmxAsset, Is.Not.Null);
            Assert.That(setup.PmxAsset, Is.SameAs(pmxAsset));

            // Trigger reimport of the .pmx file
            var importer = AssetImporter.GetAtPath(TempPmxPath) as MmdPmxScriptedImporter;
            Assert.That(importer, Is.Not.Null);
            importer!.SaveAndReimport();

            // Reload the humanoid setup asset from disk
            AssetDatabase.ImportAsset(TempHumanoidSetupPath, ImportAssetOptions.ForceUpdate);
            MmdHumanoidSetupAsset setupAfter =
                AssetDatabase.LoadAssetAtPath<MmdHumanoidSetupAsset>(TempHumanoidSetupPath);
            Assert.That(setupAfter, Is.Not.Null,
                "HumanoidSetupAsset must survive PMX SaveAndReimport");

            // Pin: serialized PmxAsset reference must not be Missing
            Assert.That(setupAfter.PmxAsset, Is.Not.Null,
                "HumanoidSetupAsset.PmxAsset serialized reference must resolve after PMX SaveAndReimport " +
                "(D1 compatibility pin — will break if main object change alters MmdPmxAsset fileID)");
            MmdPmxAsset setupPmxAsset = setupAfter.PmxAsset!;

            // Pin: referenced asset's SourceId is stable
            Assert.That(setupPmxAsset.SourceId, Is.EqualTo(pmxAsset.SourceId),
                "PmxAsset.SourceId must be stable across reimport");

            // Pin: setup's PmxAsset matches a fresh LoadAssetAtPath
            MmdPmxAsset reloadedPmx = AssetDatabase.LoadAssetAtPath<MmdPmxAsset>(TempPmxPath);
            Assert.That(reloadedPmx, Is.Not.Null);
            Assert.That(setupAfter.PmxAsset, Is.SameAs(reloadedPmx),
                "HumanoidSetupAsset serialized PmxAsset must match LoadAssetAtPath after reimport");
        }

        [Test]
        public void SceneAndPrefabModelSourceReferencesSurvivePmxSaveAndReimport()
        {
            // D1 regression pin: scene and prefab references are the highest-risk serialized
            // object references when the .pmx main object later changes from MmdPmxAsset to GameObject.
            CopyFixtureToAssetDatabase("test_1bone_cube.pmx", TempPmxPath);

            MmdPmxAsset? initialPmxAsset = AssetDatabase.LoadAssetAtPath<MmdPmxAsset>(TempPmxPath);
            Assert.That(initialPmxAsset, Is.Not.Null);

            try
            {
                Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                MmdPmxAsset? loadedPmxAsset = AssetDatabase.LoadAssetAtPath<MmdPmxAsset>(TempPmxPath);
                Assert.That(loadedPmxAsset, Is.Not.Null);
                MmdPmxAsset pmxAsset = loadedPmxAsset!;
                MmdUnityModelInstance instance = MmdEditorPmxLoader.LoadPmxIntoScene(pmxAsset);

                GameObject root = instance.Root!;
                MmdUnityPlaybackController? nullableSceneController = root.GetComponent<MmdUnityPlaybackController>();
                Assert.That(nullableSceneController, Is.Not.Null);
                MmdUnityPlaybackController sceneController = nullableSceneController!;
                Assert.That(sceneController, Is.Not.Null);
                Assert.That(sceneController.ModelAssetSource, Is.SameAs(pmxAsset));
                AssertNoMissingScripts(root);

                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, TempPrefabPath);
                Assert.That(prefab, Is.Not.Null);
                Assert.That(EditorSceneManager.SaveScene(scene, TempScenePath), Is.True);

                var importer = AssetImporter.GetAtPath(TempPmxPath) as MmdPmxScriptedImporter;
                Assert.That(importer, Is.Not.Null);
                importer!.SaveAndReimport();

                MmdPmxAsset? nullableReloadedPmx = AssetDatabase.LoadAssetAtPath<MmdPmxAsset>(TempPmxPath);
                Assert.That(nullableReloadedPmx, Is.Not.Null,
                    "D1 pin: LoadAssetAtPath<MmdPmxAsset> must still resolve the metadata asset after reimport");
                MmdPmxAsset reloadedPmx = nullableReloadedPmx!;

                GameObject? nullableReloadedPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(TempPrefabPath);
                Assert.That(nullableReloadedPrefab, Is.Not.Null);
                GameObject reloadedPrefab = nullableReloadedPrefab!;
                MmdUnityPlaybackController? nullablePrefabController = reloadedPrefab.GetComponent<MmdUnityPlaybackController>();
                Assert.That(nullablePrefabController, Is.Not.Null);
                MmdUnityPlaybackController prefabController = nullablePrefabController!;
                Assert.That(prefabController.ModelAssetSource, Is.Not.Null,
                    "prefab controller ModelAssetSource must not become Missing after PMX SaveAndReimport");
                Assert.That(prefabController.ModelAssetSource, Is.SameAs(reloadedPmx),
                    "prefab controller ModelAssetSource must resolve to the freshly loaded PMX metadata asset");
                AssertNoMissingScripts(reloadedPrefab);

                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EditorSceneManager.OpenScene(TempScenePath, OpenSceneMode.Single);
                MmdPmxAsset? nullableSceneReloadedPmx = AssetDatabase.LoadAssetAtPath<MmdPmxAsset>(TempPmxPath);
                Assert.That(nullableSceneReloadedPmx, Is.Not.Null);
                MmdPmxAsset sceneReloadedPmx = nullableSceneReloadedPmx!;

                MmdUnityPlaybackController[] sceneControllers =
                    Object.FindObjectsByType<MmdUnityPlaybackController>();
                Assert.That(sceneControllers, Has.Length.EqualTo(1));
                Assert.That(sceneControllers[0].ModelAssetSource, Is.Not.Null,
                    "scene controller ModelAssetSource must not become Missing after PMX SaveAndReimport");
                Assert.That(sceneControllers[0].ModelAssetSource, Is.SameAs(sceneReloadedPmx),
                    "scene controller ModelAssetSource must resolve to the freshly loaded PMX metadata asset");
                foreach (GameObject sceneRoot in sceneControllers[0].gameObject.scene.GetRootGameObjects())
                {
                    AssertNoMissingScripts(sceneRoot);
                }
            }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
        }

        [Test]
        public void D1_Slice2a_TryGetDraggedAssetsAcceptsPmxGameObjectMainObject()
        {
            // D1: TryGetDraggedAssets must accept a .pmx GameObject main object and resolve
            // it to the same MmdPmxAsset as LoadAssetAtPath<MmdPmxAsset>.
            CopyFixtureToAssetDatabase("test_1bone_cube.pmx", TempPmxPath);

            GameObject mainGo = AssetDatabase.LoadAssetAtPath<GameObject>(TempPmxPath);
            Assert.That(mainGo, Is.Not.Null, "precondition: .pmx must have a GameObject main object");

            MmdPmxAsset directPmx = AssetDatabase.LoadAssetAtPath<MmdPmxAsset>(TempPmxPath);
            Assert.That(directPmx, Is.Not.Null, "precondition: .pmx must have an MmdPmxAsset sub-asset");

            bool result = MmdSceneDragAndDrop.TryGetDraggedAssets(
                new Object[] { mainGo },
                out MmdPmxAsset? resolved,
                out MmdVmdAsset? vmdAsset);

            Assert.That(result, Is.True, "TryGetDraggedAssets must accept .pmx GameObject main object");
            Assert.That(resolved, Is.Not.Null, "must resolve MmdPmxAsset");
            Assert.That(resolved, Is.SameAs(directPmx),
                "resolved MmdPmxAsset must be the same as LoadAssetAtPath<MmdPmxAsset>");
            Assert.That(vmdAsset, Is.Null, "no VMD asset should be resolved");
        }

        [Test]
        public void D1_Slice2a_TryGetDraggedAssetsAcceptsPmxGameObjectWithVmdAsset()
        {
            // D1: GameObject main + MmdVmdAsset must be accepted.
            CopyFixtureToAssetDatabase("test_1bone_cube.pmx", TempPmxPath);
            CopyFixtureToAssetDatabase("test_1bone_cube_motion.vmd", TempVmdPath);

            GameObject mainGo = AssetDatabase.LoadAssetAtPath<GameObject>(TempPmxPath);
            Assert.That(mainGo, Is.Not.Null);

            MmdVmdAsset vmdAsset = AssetDatabase.LoadAssetAtPath<MmdVmdAsset>(TempVmdPath);
            Assert.That(vmdAsset, Is.Not.Null);

            MmdPmxAsset directPmx = AssetDatabase.LoadAssetAtPath<MmdPmxAsset>(TempPmxPath);
            Assert.That(directPmx, Is.Not.Null);

            bool result = MmdSceneDragAndDrop.TryGetDraggedAssets(
                new Object[] { mainGo, vmdAsset },
                out MmdPmxAsset? resolved,
                out MmdVmdAsset? resolvedVmd);

            Assert.That(result, Is.True,
                "GameObject main + VMD asset must be accepted");
            Assert.That(resolved, Is.SameAs(directPmx),
                "PMX must resolve to direct MmdPmxAsset");
            Assert.That(resolvedVmd, Is.SameAs(vmdAsset),
                "VMD asset must match the dragged VMD");
        }

        [Test]
        public void D1_Slice2a_TryGetDraggedAssetsRejectsGameObjectAndDirectPmxForSamePmx()
        {
            // D1: GameObject main + direct MmdPmxAsset for the same .pmx is ambiguous and must be rejected.
            CopyFixtureToAssetDatabase("test_1bone_cube.pmx", TempPmxPath);

            GameObject mainGo = AssetDatabase.LoadAssetAtPath<GameObject>(TempPmxPath);
            Assert.That(mainGo, Is.Not.Null);

            MmdPmxAsset directPmx = AssetDatabase.LoadAssetAtPath<MmdPmxAsset>(TempPmxPath);
            Assert.That(directPmx, Is.Not.Null);

            bool result = MmdSceneDragAndDrop.TryGetDraggedAssets(
                new Object[] { mainGo, directPmx },
                out MmdPmxAsset? resolved,
                out MmdVmdAsset? vmdAsset);

            Assert.That(result, Is.False,
                "GameObject main + direct MmdPmxAsset for same PMX must be rejected as ambiguous");
            Assert.That(resolved, Is.Null,
                "PMX must be null on rejection");
            Assert.That(vmdAsset, Is.Null);
        }

        [Test]
        public void D1_Slice2a_TryGetDraggedSourcesWithPmxGameObjectMainResolvesAsAssetDrag()
        {
            // D1: TryGetDraggedSources with objectReferences containing PMX GameObject main
            // and paths containing TempPmxPath/project path must resolve as asset drag,
            // pmxPath remains null, vmdPath remains null.
            CopyFixtureToAssetDatabase("test_1bone_cube.pmx", TempPmxPath);

            GameObject mainGo = AssetDatabase.LoadAssetAtPath<GameObject>(TempPmxPath);
            Assert.That(mainGo, Is.Not.Null, "precondition: .pmx must have a GameObject main object");

            MmdPmxAsset directPmx = AssetDatabase.LoadAssetAtPath<MmdPmxAsset>(TempPmxPath);
            Assert.That(directPmx, Is.Not.Null, "precondition: MmdPmxAsset sub-asset exists");

            bool result = MmdSceneDragAndDrop.TryGetDraggedSources(
                objectReferences: new Object[] { mainGo },
                paths: new string[] { TempPmxPath },
                pmxAsset: out MmdPmxAsset? resolvedPmx,
                vmdAsset: out MmdVmdAsset? resolvedVmd,
                pmxPath: out string? resolvedPmxPath,
                vmdPath: out string? resolvedVmdPath);

            Assert.That(result, Is.True,
                "TryGetDraggedSources must accept .pmx GameObject main as asset drag");
            Assert.That(resolvedPmx, Is.SameAs(directPmx),
                "resolved PMX must match direct MmdPmxAsset");
            Assert.That(resolvedVmd, Is.Null, "no VMD asset");
            Assert.That(resolvedPmxPath, Is.Null,
                "pmxPath must remain null (resolved as asset drag, not raw path)");
            Assert.That(resolvedVmdPath, Is.Null,
                "vmdPath must remain null");
        }

        [Test]
        public void D1_Slice2a_GapG1_ContainsMmdAssetReferenceDirectlyPinsGameObjectMainBehavior()
        {
            // G1: ContainsMmdAssetReference (private static) must recognise .pmx GameObject main object
            // as an MMD asset reference, reject plain-transient GameObjects, and reject null.
            // Uses reflection because the method is private to MmdSceneDragAndDrop.
            var bindingFlags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static;
            var method = typeof(MmdSceneDragAndDrop).GetMethod("ContainsMmdAssetReference", bindingFlags);
            Assert.That(method, Is.Not.Null, "reflection: ContainsMmdAssetReference must exist");

            // (1) .pmx GameObject main object must be recognised.
            CopyFixtureToAssetDatabase("test_1bone_cube.pmx", TempPmxPath);
            GameObject mainGo = AssetDatabase.LoadAssetAtPath<GameObject>(TempPmxPath);
            Assert.That(mainGo, Is.Not.Null, "precondition: .pmx must have a GameObject main object");

            bool resultWithMainGo = (bool)method!.Invoke(null, new object[] { new Object[] { mainGo } });
            Assert.That(resultWithMainGo, Is.True,
                "ContainsMmdAssetReference must return true for .pmx GameObject main object");

            // (2) Plain transient GameObject must be rejected.
            GameObject tempGo = new GameObject("not pmx");
            try
            {
                bool resultWithPlainGo = (bool)method.Invoke(null, new object[] { new Object[] { tempGo } });
                Assert.That(resultWithPlainGo, Is.False,
                    "ContainsMmdAssetReference must return false for plain transient GameObject");
            }
            finally
            {
                Object.DestroyImmediate(tempGo);
            }

            // (3) null must be rejected.
            bool resultWithNull = (bool)method.Invoke(null, new object[] { null! });
            Assert.That(resultWithNull, Is.False,
                "ContainsMmdAssetReference must return false for null");
        }

        private static void AssertNoMissingScripts(GameObject root)
        {
            Assert.That(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(root), Is.EqualTo(0), root.name);
            foreach (Transform child in root.transform)
            {
                AssertNoMissingScripts(child.gameObject);
            }
        }
    }
}
