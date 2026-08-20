#nullable enable

using System;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Mmd.Editor;
using Mmd.Mme;
using Mmd.Parser;
using Mmd.Physics;
using Mmd.Rendering;
using Mmd.Rendering.Universal;
using Mmd.UnityIntegration;
using Object = UnityEngine.Object;

namespace Mmd.Tests
{
    public sealed partial class MmdAssetImporterTests
    {
        [Test]
        public void PmxAndVmdImportAsMmdAssetsAndValidateNativeClipHeader()
        {
            CopyFixtureToAssetDatabase("test_1bone_cube.pmx", TempPmxPath);
            CopyFixtureToAssetDatabase("test_1bone_cube_motion.vmd", TempVmdPath);

            MmdPmxAsset pmxAsset = AssetDatabase.LoadAssetAtPath<MmdPmxAsset>(TempPmxPath);
            MmdVmdAsset vmdAsset = AssetDatabase.LoadAssetAtPath<MmdVmdAsset>(TempVmdPath);

            Assert.That(pmxAsset, Is.Not.Null);
            Assert.That(vmdAsset, Is.Not.Null);
            Assert.That(pmxAsset.name, Is.EqualTo(Path.GetFileNameWithoutExtension(TempPmxPath)));
            Assert.That(pmxAsset.SourceId, Is.EqualTo(TempPmxPath));
            Assert.That(vmdAsset.SourceId, Is.EqualTo(TempVmdPath));
            Assert.That(File.Exists(pmxAsset.SourcePath), Is.True);
            Assert.That(File.Exists(vmdAsset.SourcePath), Is.True);
            Assert.That(pmxAsset.ByteLength, Is.GreaterThan(0));
            Assert.That(vmdAsset.ByteLength, Is.GreaterThan(0));

            MmdModelDefinition model = pmxAsset.LoadModel();
            Assert.That(vmdAsset.ImportSummaryStatus, Is.EqualTo(MmdVmdImportSummaryStatus.Passed));
            Assert.That(vmdAsset.StructuralDiagnostics, Is.Empty);
            Assert.That(vmdAsset.TargetModelName, Is.EqualTo("テスト用モデル_arm"));
            Assert.That(vmdAsset.MaxFrame, Is.EqualTo(49));
            Assert.That(vmdAsset.BoneKeyframeCount, Is.EqualTo(6));
            Assert.That(vmdAsset.MorphKeyframeCount, Is.Zero);
            Assert.That(vmdAsset.ModelKeyframeCount, Is.Zero);
            Assert.That(vmdAsset.ConstraintStateCount, Is.Zero);
            Assert.That(vmdAsset.CameraKeyframeCount, Is.Zero);
            Assert.That(vmdAsset.LightKeyframeCount, Is.Zero);
            Assert.That(vmdAsset.SelfShadowKeyframeCount, Is.Zero);

            MmdMotionDefinition motion = vmdAsset.CreateNativeClipMotionHeader();
            Assert.That(model.bones, Has.Count.GreaterThan(0));
            Assert.That(motion.targetModelName, Is.EqualTo(vmdAsset.TargetModelName));
            Assert.That(motion.maxFrame, Is.EqualTo(vmdAsset.MaxFrame));
            Assert.That(motion.boneKeyframes, Is.Empty);
            Assert.That(motion.morphKeyframes, Is.Empty);
            Assert.That(motion.modelKeyframes, Is.Empty);
            Assert.That(motion.cameraKeyframes, Is.Empty);
            Assert.That(motion.lightKeyframes, Is.Empty);
            Assert.That(motion.selfShadowKeyframes, Is.Empty);
            Assert.That(motion.cameraKeyframeCount, Is.EqualTo(vmdAsset.CameraKeyframeCount));
            Assert.That(motion.lightKeyframeCount, Is.EqualTo(vmdAsset.LightKeyframeCount));
            Assert.That(motion.selfShadowKeyframeCount, Is.EqualTo(vmdAsset.SelfShadowKeyframeCount));
            Assert.That(motion.sourceBytes, Is.EqualTo(vmdAsset.GetBytesCopy()));
            MmdMotionDefinition secondMotion = vmdAsset.CreateNativeClipMotionHeader();
            Assert.That(secondMotion.sourceBytes, Is.SameAs(motion.sourceBytes));
            Assert.That(vmdAsset.GetBytesCopy(), Is.Not.SameAs(motion.sourceBytes));
        }

        [Test]
        public void ImportedVmdUsesNativeRawSourceSubassetAndSurvivesReimport()
        {
            CopyFixtureToAssetDatabase("test_1bone_cube_motion.vmd", TempVmdPath);

            byte[] sourceBytes = File.ReadAllBytes(Path.Combine(ProjectRoot, TempVmdPath));
            MmdVmdAsset vmdAsset = AssetDatabase.LoadAssetAtPath<MmdVmdAsset>(TempVmdPath);
            Assert.That(vmdAsset, Is.Not.Null);

            Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(TempVmdPath);
            TextAsset? rawSource = null;
            foreach (Object subAsset in subAssets)
            {
                if (subAsset is TextAsset textAsset && textAsset.name == "VMDSource")
                {
                    rawSource = textAsset;
                    break;
                }
            }

            Assert.That(rawSource, Is.Not.Null, "VMD import must create one named raw-source TextAsset subasset.");
            Assert.That(rawSource!.dataSize, Is.EqualTo(sourceBytes.LongLength));
            Assert.That(rawSource.bytes, Is.EqualTo(sourceBytes));

            SerializedObject serializedAsset = new SerializedObject(vmdAsset);
            Assert.That(serializedAsset.FindProperty("data")!.arraySize, Is.Zero,
                "New imports must not serialize the raw VMD into the managed byte[] field.");
            Assert.That(
                serializedAsset.FindProperty("rawSource")!.objectReferenceValue,
                Is.SameAs(rawSource),
                "MmdVmdAsset must retain the importer-created raw-source subasset reference.");

            Assert.That(vmdAsset.ByteLength, Is.EqualTo(sourceBytes.Length));
            Assert.That(vmdAsset.GetBytesCopy(), Is.EqualTo(sourceBytes));
            Assert.That(vmdAsset.CreateNativeClipMotionHeader().sourceBytes, Is.EqualTo(sourceBytes));

            AssetDatabase.ImportAsset(TempVmdPath, ImportAssetOptions.ForceUpdate);
            MmdVmdAsset reloaded = AssetDatabase.LoadAssetAtPath<MmdVmdAsset>(TempVmdPath);
            Assert.That(reloaded, Is.Not.Null);
            Assert.That(reloaded.SourceId, Is.EqualTo(TempVmdPath));
            Assert.That(reloaded.ByteLength, Is.EqualTo(sourceBytes.Length));
            Assert.That(reloaded.GetBytesCopy(), Is.EqualTo(sourceBytes));
            Assert.That(reloaded.ImportSummaryStatus, Is.EqualTo(MmdVmdImportSummaryStatus.Passed));
            Assert.That(reloaded.StructuralDiagnostics, Is.Empty);
            Assert.That(reloaded.CreateNativeClipMotionHeader().sourceBytes, Is.EqualTo(sourceBytes));

            Object[] reloadedSubAssets = AssetDatabase.LoadAllAssetsAtPath(TempVmdPath);
            Assert.That(
                Array.FindAll(reloadedSubAssets, value => value is TextAsset textAsset && textAsset.name == "VMDSource"),
                Has.Length.EqualTo(1));
        }

        [Test]
        public void ImportedVmdSummaryCacheInvalidatesWhenSourceBytesChange()
        {
            const string cachePath = TempDirectory + "/summary-cache.vmd";
            byte[] firstBytes = MmdTestFixtures.CreateDenseVmdBytes("cache", "root", 2, 1);
            byte[] secondBytes = MmdTestFixtures.CreateDenseVmdBytes("cache", "root", 2, 16);
            Directory.CreateDirectory(Path.Combine(ProjectRoot, TempDirectory));
            string absolutePath = Path.Combine(ProjectRoot, cachePath);

            File.WriteAllBytes(absolutePath, firstBytes);
            DateTime firstLastWriteTimeUtc = File.GetLastWriteTimeUtc(absolutePath);
            AssetDatabase.ImportAsset(cachePath, ImportAssetOptions.ForceUpdate);
            MmdVmdAsset firstAsset = AssetDatabase.LoadAssetAtPath<MmdVmdAsset>(cachePath);
            Assert.That(firstAsset, Is.Not.Null);
            Assert.That(firstAsset.BoneKeyframeCount, Is.EqualTo(2));
            Assert.That(firstAsset.MaxFrame, Is.Zero);

            File.WriteAllBytes(absolutePath, secondBytes);
            File.SetLastWriteTimeUtc(absolutePath, firstLastWriteTimeUtc);
            AssetDatabase.ImportAsset(cachePath, ImportAssetOptions.ForceUpdate);
            MmdVmdAsset secondAsset = AssetDatabase.LoadAssetAtPath<MmdVmdAsset>(cachePath);
            Assert.That(secondAsset, Is.Not.Null);
            Assert.That(secondAsset.BoneKeyframeCount, Is.EqualTo(2));
            Assert.That(secondAsset.MaxFrame, Is.EqualTo(1));
            Assert.That(secondAsset.GetBytesCopy(), Is.EqualTo(secondBytes));
        }

        [Test]
        public void ImportedVmdReusesRawSourceOnUnchangedReimportAndReplacesItWhenContentChanges()
        {
            const string rawSourcePath = TempDirectory + "/raw-source-identity.vmd";
            byte[] firstBytes = MmdTestFixtures.CreateDenseVmdBytes("raw-source", "root", 2, 1);
            byte[] secondBytes = MmdTestFixtures.CreateDenseVmdBytes("raw-source", "root", 2, 16);
            Assert.That(secondBytes.Length, Is.EqualTo(firstBytes.Length),
                "The replacement fixture must preserve byte length to cover the mtime/length cache candidate.");

            Directory.CreateDirectory(Path.Combine(ProjectRoot, TempDirectory));
            string absolutePath = Path.Combine(ProjectRoot, rawSourcePath);
            File.WriteAllBytes(absolutePath, firstBytes);
            DateTime firstLastWriteTimeUtc = File.GetLastWriteTimeUtc(absolutePath);
            AssetDatabase.ImportAsset(rawSourcePath, ImportAssetOptions.ForceUpdate);

            TextAsset firstRawSource = FindImportedVmdRawSource(rawSourcePath);
            Assert.That(firstRawSource.bytes, Is.EqualTo(firstBytes));
            int firstRawSourceInstanceId = firstRawSource.GetInstanceID();

            AssetDatabase.ImportAsset(rawSourcePath, ImportAssetOptions.ForceUpdate);
            TextAsset unchangedRawSource = FindImportedVmdRawSource(rawSourcePath);
            Assert.That(unchangedRawSource.GetInstanceID(), Is.EqualTo(firstRawSourceInstanceId),
                "An unchanged reimport must reuse the existing VMDSource object.");

            File.WriteAllBytes(absolutePath, secondBytes);
            File.SetLastWriteTimeUtc(absolutePath, firstLastWriteTimeUtc);
            AssetDatabase.ImportAsset(rawSourcePath, ImportAssetOptions.ForceUpdate);

            TextAsset changedRawSource = FindImportedVmdRawSource(rawSourcePath);
            Assert.That(changedRawSource.bytes, Is.EqualTo(secondBytes));

            MmdVmdAsset changedAsset = AssetDatabase.LoadAssetAtPath<MmdVmdAsset>(rawSourcePath);
            Assert.That(changedAsset, Is.Not.Null);
            Assert.That(changedAsset.MaxFrame, Is.EqualTo(1));
            Assert.That(changedAsset.GetBytesCopy(), Is.EqualTo(secondBytes));
        }

        [Test]
        public void ImportedInvalidVmdPreservesRawBytesAndRejectsNativeClipHeader()
        {
            byte[] invalidBytes = { 0x56, 0x4D, 0x44, 0x00 };
            Directory.CreateDirectory(Path.Combine(ProjectRoot, TempDirectory));
            File.WriteAllBytes(Path.Combine(ProjectRoot, TempVmdPath), invalidBytes);
            AssetDatabase.ImportAsset(TempVmdPath, ImportAssetOptions.ForceUpdate);

            MmdVmdAsset vmdAsset = AssetDatabase.LoadAssetAtPath<MmdVmdAsset>(TempVmdPath);
            Assert.That(vmdAsset, Is.Not.Null);
            Assert.That(vmdAsset.ByteLength, Is.EqualTo(invalidBytes.Length));
            Assert.That(vmdAsset.GetBytesCopy(), Is.EqualTo(invalidBytes));
            Assert.That(vmdAsset.ImportSummaryStatus, Is.EqualTo(MmdVmdImportSummaryStatus.Failed));
            Assert.That(vmdAsset.StructuralDiagnostics.Count, Is.EqualTo(1));
            Assert.That(vmdAsset.StructuralDiagnostics[0], Does.Contain("Failed to parse VMD"));

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => vmdAsset.CreateNativeClipMotionHeader())!;
            Assert.That(exception.Message, Does.Contain("Failed to parse VMD"));

            TextAsset? rawSource = null;
            foreach (Object subAsset in AssetDatabase.LoadAllAssetsAtPath(TempVmdPath))
            {
                if (subAsset is TextAsset textAsset && textAsset.name == "VMDSource")
                {
                    rawSource = textAsset;
                    break;
                }
            }

            Assert.That(rawSource, Is.Not.Null);
            Assert.That(rawSource!.bytes, Is.EqualTo(invalidBytes));
        }

        [Test]
        public void VmdScriptedImporterVersionIsTwoForRawSourceMigration()
        {
            object[] attributes = typeof(MmdVmdScriptedImporter).GetCustomAttributes(
                typeof(ScriptedImporterAttribute),
                inherit: false);

            Assert.That(attributes, Has.Length.EqualTo(1));
            var attribute = (ScriptedImporterAttribute)attributes[0];
            Assert.That(attribute.version, Is.EqualTo(2));
        }

        private static TextAsset FindImportedVmdRawSource(string assetPath)
        {
            foreach (Object subAsset in AssetDatabase.LoadAllAssetsAtPath(assetPath))
            {
                if (subAsset is TextAsset textAsset && textAsset.name == "VMDSource")
                {
                    return textAsset;
                }
            }

            Assert.Fail("VMD import must create a named raw-source TextAsset subasset.");
            return null!;
        }

        [Test]
        public void ImportedPmxAssetCarriesImportScaleSummaryFromImporter()
        {
            CopyFixtureToAssetDatabase("test_1bone_cube.pmx", TempPmxPath);

            MmdPmxAsset pmxAsset = AssetDatabase.LoadAssetAtPath<MmdPmxAsset>(TempPmxPath);

            Assert.That(pmxAsset.ImportScale, Is.EqualTo(0.1f).Within(0.0001f));
        }
        [Test]
        public void ImportedPmxAssetCarriesImportUiSettingsSummaryFromImporter()
        {
            CopyFixtureToAssetDatabase("test_1bone_cube.pmx", TempPmxPath);

            MmdPmxAsset pmxAsset = AssetDatabase.LoadAssetAtPath<MmdPmxAsset>(TempPmxPath);

            Assert.That(pmxAsset.ModelPreset, Is.EqualTo(nameof(MmdPmxModelPreset.Stage)));
            Assert.That(pmxAsset.AnimationType, Is.EqualTo(nameof(MmdPmxAnimationType.Generic)));
            Assert.That(pmxAsset.ShaderPreset, Is.EqualTo("MMD Basic Toon"));
            Assert.That(pmxAsset.ImportedAvatar, Is.Null);
            Assert.That(pmxAsset.HumanoidAvatarReadiness, Is.EqualTo("NotRequested"));
        }
        [Test]
        public void InitialPmxImportAutoAssignsCharacterPresetFromStandardBones()
        {
            CopyFixtureToAssetDatabase("test_semi_basic_bone.pmx", TempHumanoidPmxPath);

            MmdPmxAsset pmxAsset = AssetDatabase.LoadAssetAtPath<MmdPmxAsset>(TempHumanoidPmxPath);

            Assert.That(pmxAsset, Is.Not.Null);
            Assert.That(pmxAsset.ModelPreset, Is.EqualTo(nameof(MmdPmxModelPreset.Character)));
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
            Assert.That(animator.applyRootMotion, Is.True,
                "Imported animation roots must enable root motion by default for ordinary Unity clips.");
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
            serializedImporter.FindProperty("animationType").enumValueIndex = (int)MmdPmxAnimationType.Humanoid;
            serializedImporter.FindProperty("shaderPreset").enumValueIndex = (int)MmdPmxShaderPreset.MmdBasicUrpToon;
            serializedImporter.ApplyModifiedPropertiesWithoutUndo();
            importer!.SaveAndReimport();

            MmdPmxAsset pmxAsset = AssetDatabase.LoadAssetAtPath<MmdPmxAsset>(TempPmxPath);

            Assert.That(pmxAsset.ImportScale, Is.EqualTo(2.5f).Within(0.0001f));
            Assert.That(pmxAsset.ModelPreset, Is.EqualTo(nameof(MmdPmxModelPreset.Stage)));
            Assert.That(pmxAsset.AnimationType, Is.EqualTo(nameof(MmdPmxAnimationType.Humanoid)));
            Assert.That(pmxAsset.ShaderPreset, Is.EqualTo("MMD Basic Toon"));
            Assert.That(pmxAsset.HumanoidAvatarReadiness, Is.Not.EqualTo("NotRequested"));
            Assert.That(pmxAsset.ImportSummaryStatus, Is.EqualTo(MmdPmxImportSummaryStatus.Passed));
            Assert.That(pmxAsset.VertexCount, Is.EqualTo(TestOneBoneCubeVertexCount));
            Assert.That(pmxAsset.MaterialCount, Is.EqualTo(1));

            Assert.That(pmxAsset.RigidbodyCount, Is.EqualTo(0));
            Assert.That(pmxAsset.JointCount, Is.EqualTo(0));

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
        public void PmxImporterUrpLitShaderPresetGeneratesUrpLitMaterials()
        {
            CopyFixtureToAssetDatabase("test_1bone_cube.pmx", TempPmxPath);
            var importer = AssetImporter.GetAtPath(TempPmxPath) as MmdPmxScriptedImporter;
            Assert.That(importer, Is.Not.Null);

            var serializedImporter = new SerializedObject(importer!);
            serializedImporter.FindProperty("shaderPreset").enumValueIndex = (int)MmdPmxShaderPreset.UrpLit;
            serializedImporter.ApplyModifiedPropertiesWithoutUndo();
            importer!.SaveAndReimport();

            MmdPmxAsset pmxAsset = AssetDatabase.LoadAssetAtPath<MmdPmxAsset>(TempPmxPath);

            Assert.That(pmxAsset.ShaderPreset, Is.EqualTo("URP Lit"));
            Assert.That(pmxAsset.ImportedMaterials, Is.Not.Null.And.Not.Empty);
            Assert.That(pmxAsset.ImportedMaterials[0].shader, Is.Not.Null);
            Assert.That(pmxAsset.ImportedMaterials[0].shader.name,
                Is.EqualTo(MmdUrpMaterialBindingDescriptorBuilder.UrpLitShaderName));
        }
        [Test]
        public void PmxImporterMmdToonLitShaderPresetGeneratesOptInToonLitMaterials()
        {
            CopyFixtureToAssetDatabase("test_1bone_cube.pmx", TempPmxPath);
            var importer = AssetImporter.GetAtPath(TempPmxPath) as MmdPmxScriptedImporter;
            Assert.That(importer, Is.Not.Null);

            var serializedImporter = new SerializedObject(importer!);
            serializedImporter.FindProperty("shaderPreset").enumValueIndex = (int)MmdPmxShaderPreset.MmdToonLit;
            serializedImporter.ApplyModifiedPropertiesWithoutUndo();
            importer!.SaveAndReimport();

            MmdPmxAsset pmxAsset = AssetDatabase.LoadAssetAtPath<MmdPmxAsset>(TempPmxPath);

            Assert.That(pmxAsset.ShaderPreset, Is.EqualTo("MMD URP Toon"));
            Assert.That(pmxAsset.ImportedMaterials, Is.Not.Null.And.Not.Empty);
            Assert.That(pmxAsset.ImportedMaterials[0].shader, Is.Not.Null);
            Assert.That(pmxAsset.ImportedMaterials[0].shader.name,
                Is.EqualTo(MmdUrpMaterialBindingDescriptorBuilder.MmdToonLitShaderName));
        }

        [Test]
        public void PmxImporterCustomProfileUsesProfileShaderAndPreservesPresetSummary()
        {
            CopyFixtureToAssetDatabase("test_1bone_cube.pmx", TempPmxPath);
            var profile = ScriptableObject.CreateInstance<MmdMaterialProfileAsset>();
            profile.shader = Shader.Find(MmdUrpMaterialBindingDescriptorBuilder.UrpLitShaderName);
            profile.textureTargets = new MmdMaterialProfileTextureTargets
            {
                diffuseTextureProperties = new[] { "_BaseMap" }
            };
            profile.renderingTargets = new MmdMaterialProfileRenderingTargets
            {
                colorProperty = string.Empty,
                ambientColorProperty = string.Empty,
                alphaProperty = string.Empty,
                alphaClipThresholdProperty = "_Cutoff",
                outlineColorProperty = string.Empty,
                outlineWidthProperty = string.Empty,
                outlineVisibleProperty = string.Empty,
                outlineScreenSpaceWeightProperty = string.Empty,
                outlineZTestProperty = string.Empty,
                unsupportedFeatures = new[] { "sphere-texture", "toon-texture", "outline" },
                requiredKeywords = new[] { "_SURFACE_TYPE_TRANSPARENT" }
            };
            AssetDatabase.Refresh();
            AssetDatabase.CreateAsset(profile, TempMaterialProfilePath);
            AssetDatabase.SaveAssets();

            var importer = AssetImporter.GetAtPath(TempPmxPath) as MmdPmxScriptedImporter;
            Assert.That(importer, Is.Not.Null);
            var serializedImporter = new SerializedObject(importer!);
            serializedImporter.FindProperty("shaderPreset").enumValueIndex = (int)MmdPmxShaderPreset.CustomProfile;
            serializedImporter.FindProperty("materialProfileAsset").objectReferenceValue = profile;
            serializedImporter.ApplyModifiedPropertiesWithoutUndo();
            importer!.SaveAndReimport();

            MmdPmxAsset pmxAsset = AssetDatabase.LoadAssetAtPath<MmdPmxAsset>(TempPmxPath);

            Assert.That(pmxAsset.ShaderPreset, Is.EqualTo("Custom Profile"));
            Assert.That(pmxAsset.ImportedMaterials, Is.Not.Null.And.Not.Empty);
            Assert.That(pmxAsset.ImportedMaterials[0].shader, Is.Not.Null);
            Assert.That(pmxAsset.ImportedMaterials[0].shader.name,
                Is.EqualTo(MmdUrpMaterialBindingDescriptorBuilder.UrpLitShaderName));
            Assert.That(pmxAsset.ImportedMaterials[0].IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT"), Is.True);
            Assert.That(AssetDatabase.GetDependencies(TempPmxPath), Does.Contain(TempMaterialProfilePath));
        }

        [Test]
        public void PmxImporterCustomProfileUsesUtsShaderAndDeclaredMaterialStates()
        {
            Shader? utsShader = Shader.Find("Toon/Toon");
            if (utsShader == null)
            {
                Assert.Ignore("Optional Unity Toon Shader is not installed.");
            }

            CopyFixtureToAssetDatabase("test_1bone_cube.pmx", TempPmxPath);
            CreateUtsProfileAsset(utsShader!);
            ConfigureCustomProfileImporter(TempPmxPath);

            MmdPmxAsset pmxAsset = AssetDatabase.LoadAssetAtPath<MmdPmxAsset>(TempPmxPath);
            Material material = pmxAsset.ImportedMaterials[0];
            Assert.That(material.shader, Is.SameAs(utsShader));
            Assert.That(material.IsKeywordEnabled("_OUTLINE_NML"), Is.True);
            Assert.That(material.GetShaderPassEnabled("SRPDefaultUnlit"), Is.True);
            Assert.That(material.HasProperty("_BaseColor"), Is.True);
            Assert.That(material.HasProperty("_CullMode"), Is.True);
            Assert.That(material.HasProperty("_Clipping_Level"), Is.True);
            Assert.That(material.HasProperty("_Outline_Width"), Is.True);
            Assert.That(material.HasProperty("_MatCap_Sampler"), Is.True);
            Assert.That(pmxAsset.ShaderPreset, Is.EqualTo("Custom Profile"));
            Assert.That(AssetDatabase.GetDependencies(TempPmxPath), Does.Contain(TempMaterialProfilePath));
        }
        [Test]
        public void PmxImporterMmdUrpToonReimportPreservesBoundDiffuseTexture()
        {
            const string pmxPath = TempDirectory + "/mmd-texture-alpha-used-uv-cutout.pmx";
            const string texturePath = TempDirectory + "/texture-alpha-cutout.png";
            Directory.CreateDirectory(Path.Combine(ProjectRoot, TempDirectory));
            File.Copy(
                MmdTestFixtures.FixtureAssetPath("GeneratedPmx/mmd-texture-alpha-used-uv-cutout.pmx"),
                Path.Combine(ProjectRoot, pmxPath),
                overwrite: true);
            File.Copy(
                MmdTestFixtures.FixtureAssetPath("GeneratedPmx/texture-alpha-cutout.png"),
                Path.Combine(ProjectRoot, texturePath),
                overwrite: true);
            AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(pmxPath, ImportAssetOptions.ForceUpdate);

            var importer = AssetImporter.GetAtPath(pmxPath) as MmdPmxScriptedImporter;
            Assert.That(importer, Is.Not.Null);
            var serializedImporter = new SerializedObject(importer!);
            serializedImporter.FindProperty("shaderPreset").enumValueIndex = (int)MmdPmxShaderPreset.MmdToonLit;
            serializedImporter.ApplyModifiedPropertiesWithoutUndo();
            importer!.SaveAndReimport();

            AssertMmdUrpToonDiffuseTextureIsBound(pmxPath, texturePath);
            AssetDatabase.ImportAsset(pmxPath, ImportAssetOptions.ForceUpdate);
            AssertMmdUrpToonDiffuseTextureIsBound(pmxPath, texturePath);
        }
        [Test]
        public void PmxImporterAppliesPersistentMaterialOverrideAssetAfterTextureBinding()
        {
            CopyFixtureToAssetDatabase("test_1bone_cube.pmx", TempPmxPath);
            string normalMapPath = Path.Combine(ProjectRoot, TempNormalMapPath);
            WritePng(normalMapPath, new Color(0.2f, 0.3f, 0.4f, 1.0f));
            AssetDatabase.ImportAsset(TempNormalMapPath, ImportAssetOptions.ForceUpdate);
            Texture2D? normalMap = AssetDatabase.LoadAssetAtPath<Texture2D>(TempNormalMapPath);
            Assert.That(normalMap, Is.Not.Null);

            MmdMaterialOverrideAsset overrideAsset = ScriptableObject.CreateInstance<MmdMaterialOverrideAsset>();
            overrideAsset.entries = new[]
            {
                new MmdMaterialOverrideEntry
                {
                    materialIndex = 0,
                    hasMetallic = true,
                    metallic = 0.72f,
                    hasSmoothness = true,
                    smoothness = 0.18f,
                    hasNormalMap = true,
                    normalMap = normalMap,
                    hasNormalScale = true,
                    normalScale = 0.33f
                }
            };
            AssetDatabase.CreateAsset(overrideAsset, TempMaterialOverridePath);
            AssetDatabase.ImportAsset(TempMaterialOverridePath, ImportAssetOptions.ForceUpdate);
            MmdMaterialOverrideAsset persistedOverride =
                AssetDatabase.LoadAssetAtPath<MmdMaterialOverrideAsset>(TempMaterialOverridePath);

            var importer = AssetImporter.GetAtPath(TempPmxPath) as MmdPmxScriptedImporter;
            Assert.That(importer, Is.Not.Null);
            Assert.That(persistedOverride, Is.Not.Null);

            var serializedImporter = new SerializedObject(importer!);
            serializedImporter.FindProperty("shaderPreset").enumValueIndex = (int)MmdPmxShaderPreset.UrpLit;
            serializedImporter.FindProperty("materialOverrideAsset").objectReferenceValue = persistedOverride;
            serializedImporter.ApplyModifiedPropertiesWithoutUndo();
            importer!.SaveAndReimport();

            MmdPmxAsset pmxAsset = AssetDatabase.LoadAssetAtPath<MmdPmxAsset>(TempPmxPath);

            Assert.That(pmxAsset.ShaderPreset, Is.EqualTo("URP Lit"));
            Assert.That(pmxAsset.ImportedMaterials, Is.Not.Null.And.Not.Empty);
            Material importedMaterial = pmxAsset.ImportedMaterials[0];
            Assert.That(importedMaterial.shader, Is.Not.Null);
            Assert.That(importedMaterial.shader.name,
                Is.EqualTo(MmdUrpMaterialBindingDescriptorBuilder.UrpLitShaderName));
            Assert.That(importedMaterial.HasProperty(MmdMaterialPropertyNames.Metallic), Is.True);
            Assert.That(importedMaterial.HasProperty(MmdMaterialPropertyNames.Smoothness), Is.True);
            Assert.That(importedMaterial.HasProperty(MmdMaterialPropertyNames.BumpMap), Is.True);
            Assert.That(importedMaterial.HasProperty(MmdMaterialPropertyNames.BumpScale), Is.True);
            Assert.That(importedMaterial.GetFloat(MmdMaterialPropertyNames.Metallic),
                Is.EqualTo(0.72f).Within(0.00001f));
            Assert.That(importedMaterial.GetFloat(MmdMaterialPropertyNames.Smoothness),
                Is.EqualTo(0.18f).Within(0.00001f));
            Assert.That(importedMaterial.GetTexture(MmdMaterialPropertyNames.BumpMap), Is.SameAs(normalMap));
            Assert.That(importedMaterial.IsKeywordEnabled("_NORMALMAP"), Is.True);
            Assert.That(importedMaterial.GetFloat(MmdMaterialPropertyNames.BumpScale),
                Is.EqualTo(0.33f).Within(0.00001f));
        }

        [Test]
        public void PmxImporterAutoDiscoversMmeNormalMapAndExplicitOverrideWins()
        {
            CopyFixtureToAssetDatabase("test_1bone_cube.pmx", TempMmePmxPath);

            string autoFxAssetPath = TempDirectory + "/auto.fx";
            string autoEmdAssetPath = TempDirectory + "/mme_test_1bone_cube.emd";
            string autoNormalMapAssetPath = TempDirectory + "/auto_normal.png";
            string explicitNormalMapAssetPath = TempDirectory + "/explicit_normal.png";

            WritePng(Path.Combine(ProjectRoot, autoNormalMapAssetPath), new Color(0.2f, 0.8f, 0.4f, 1.0f));
            WritePng(Path.Combine(ProjectRoot, explicitNormalMapAssetPath), new Color(0.8f, 0.3f, 0.1f, 1.0f));
            File.WriteAllText(
                Path.Combine(ProjectRoot, autoFxAssetPath),
                "#define USE_NORMALMAP\n#define TEXTURE_NORMALMAP \"auto_normal.png\"\n#include \"AlternativeFull.fxsub\"\n");
            File.WriteAllText(
                Path.Combine(ProjectRoot, autoEmdAssetPath),
                "[Effect]\nObj[0] = auto.fx\n");

            AssetDatabase.Refresh();
            AssetDatabase.ImportAsset(autoNormalMapAssetPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(explicitNormalMapAssetPath, ImportAssetOptions.ForceUpdate);
            Shader.globalRenderPipeline = "UniversalPipeline";
            string absoluteMmePmxPath = Path.Combine(ProjectRoot, TempMmePmxPath);
            var scannedEffects = MmeFxScanner.ScanFromModelPath(absoluteMmePmxPath, materialCount: 1);
            Assert.That(scannedEffects, Has.Count.EqualTo(1),
                $"pmx={absoluteMmePmxPath}; emdExists={File.Exists(Path.ChangeExtension(absoluteMmePmxPath, ".emd"))}");
            Assert.That(scannedEffects[0].useNormalMap, Is.True);
            Assert.That(scannedEffects[0].normalMapTexture, Is.EqualTo("auto_normal.png"));
            Assert.That(
                MmdAssetPathUtility.TryResolveProjectRelativeAssetPath(
                    scannedEffects[0].sourcePath,
                    scannedEffects[0].normalMapTexture!,
                    out string resolvedAutoNormalMapPath),
                Is.True,
                $"source={scannedEffects[0].sourcePath}; reference={scannedEffects[0].normalMapTexture}");
            Assert.That(AssetDatabase.LoadAssetAtPath<Texture2D>(resolvedAutoNormalMapPath), Is.Not.Null,
                $"resolvedPath={resolvedAutoNormalMapPath}");
            AssetDatabase.ImportAsset(TempMmePmxPath, ImportAssetOptions.ForceUpdate);

            MmdPmxScriptedImporter importer = AssetImporter.GetAtPath(TempMmePmxPath) as MmdPmxScriptedImporter;
            Assert.That(importer, Is.Not.Null);
            var serializedImporter = new SerializedObject(importer!);
            serializedImporter.FindProperty("shaderPreset").enumValueIndex = (int)MmdPmxShaderPreset.UrpLit;
            serializedImporter.ApplyModifiedPropertiesWithoutUndo();
            Shader.globalRenderPipeline = "UniversalPipeline";
            importer!.SaveAndReimport();
            Shader.globalRenderPipeline = "UniversalPipeline";

            Texture2D autoNormalMap = AssetDatabase.LoadAssetAtPath<Texture2D>(autoNormalMapAssetPath);
            Assert.That(autoNormalMap, Is.Not.Null);
            MmdPmxAsset autoImportedAsset = AssetDatabase.LoadAssetAtPath<MmdPmxAsset>(TempMmePmxPath);
            Assert.That(autoImportedAsset, Is.Not.Null);
            Assert.That(autoImportedAsset.ShaderPreset, Is.EqualTo("URP Lit"));
            Assert.That(autoImportedAsset.ImportedMaterials, Is.Not.Null.And.Not.Empty);
            Material autoImportedMaterial = autoImportedAsset.ImportedMaterials[0];
            Assert.That(autoImportedMaterial.shader, Is.Not.Null);
            Assert.That(autoImportedMaterial.shader.name,
                Is.EqualTo(MmdUrpMaterialBindingDescriptorBuilder.UrpLitShaderName));
            Assert.That(
                autoImportedMaterial.HasProperty(MmdMaterialPropertyNames.BumpMap),
                Is.True,
                $"pipeline={Shader.globalRenderPipeline}; shader={autoImportedMaterial.shader.name}; supported={autoImportedMaterial.shader.isSupported}; passCount={autoImportedMaterial.passCount}");
            Assert.That(autoImportedMaterial.GetTexture(MmdMaterialPropertyNames.BumpMap), Is.SameAs(autoNormalMap));
            Assert.That(autoImportedMaterial.IsKeywordEnabled("_NORMALMAP"), Is.True);

            Texture2D explicitNormalMap = AssetDatabase.LoadAssetAtPath<Texture2D>(explicitNormalMapAssetPath);
            Assert.That(explicitNormalMap, Is.Not.Null);
            MmdMaterialOverrideAsset overrideAsset = ScriptableObject.CreateInstance<MmdMaterialOverrideAsset>();
            overrideAsset.entries = new[]
            {
                new MmdMaterialOverrideEntry
                {
                    materialIndex = 0,
                    hasNormalMap = true,
                    normalMap = explicitNormalMap
                }
            };
            AssetDatabase.CreateAsset(overrideAsset, TempMmeMaterialOverridePath);
            AssetDatabase.ImportAsset(TempMmeMaterialOverridePath, ImportAssetOptions.ForceUpdate);
            MmdMaterialOverrideAsset persistedOverride =
                AssetDatabase.LoadAssetAtPath<MmdMaterialOverrideAsset>(TempMmeMaterialOverridePath);
            Assert.That(persistedOverride, Is.Not.Null);
            Assert.That(persistedOverride!.entries, Has.Length.EqualTo(1));
            Assert.That(persistedOverride.entries[0].enabled, Is.True);
            Assert.That(persistedOverride.entries[0].normalMap, Is.SameAs(explicitNormalMap));

            importer = AssetImporter.GetAtPath(TempMmePmxPath) as MmdPmxScriptedImporter;
            Assert.That(importer, Is.Not.Null);
            serializedImporter = new SerializedObject(importer!);
            SerializedProperty overrideProperty = serializedImporter.FindProperty("materialOverrideAsset");
            Assert.That(overrideProperty, Is.Not.Null);
            overrideProperty!.objectReferenceValue = persistedOverride;
            serializedImporter.ApplyModifiedPropertiesWithoutUndo();
            importer = AssetImporter.GetAtPath(TempMmePmxPath) as MmdPmxScriptedImporter;
            Assert.That(importer, Is.Not.Null);
            Shader.globalRenderPipeline = "UniversalPipeline";
            importer!.SaveAndReimport();

            MmdPmxAsset explicitImportedAsset = AssetDatabase.LoadAssetAtPath<MmdPmxAsset>(TempMmePmxPath);
            Material explicitImportedMaterial = explicitImportedAsset.ImportedMaterials[0];
            Assert.That(explicitImportedMaterial.GetTexture(MmdMaterialPropertyNames.BumpMap), Is.SameAs(explicitNormalMap));
            Assert.That(explicitImportedMaterial.IsKeywordEnabled("_NORMALMAP"), Is.True);
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
            Assert.That(pmxAsset.ModelPreset, Is.EqualTo(nameof(MmdPmxModelPreset.Character)));
            Assert.That(pmxAsset.AnimationType, Is.EqualTo(nameof(MmdPmxAnimationType.Humanoid)));
            Assert.That(pmxAsset.HumanoidAvatarReadiness, Is.EqualTo(MmdHumanoidMappingReadiness.Ready));
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
            AssertUpperArmBindPointsHorizontally(
                controller.HumanoidRetargetEntries,
                HumanBodyBones.LeftUpperArm,
                HumanBodyBones.LeftLowerArm,
                Vector3.left);
            AssertUpperArmBindPointsHorizontally(
                controller.HumanoidRetargetEntries,
                HumanBodyBones.RightUpperArm,
                HumanBodyBones.RightLowerArm,
                Vector3.right);
            foreach (MmdHumanoidRetargetBinding entry in controller.HumanoidRetargetEntries)
            {
                Assert.That(entry.ProxyTransform, Is.Not.Null, entry.HumanBone + " proxy transform");
                Assert.That(entry.NativeTransform, Is.Not.Null, entry.HumanBone + " native transform");
                Assert.That(entry.MmdBoneIndex, Is.GreaterThanOrEqualTo(0), entry.HumanBone + " MMD bone index");
                Assert.That(Quaternion.Angle(entry.ProxyBindLocalRotation, entry.ProxyTransform!.localRotation),
                    Is.LessThan(0.001f),
                    entry.HumanBone + " proxy bind rotation must be captured after Avatar T-pose.");
                if (entry.HumanBone != HumanBodyBones.LeftUpperArm &&
                    entry.HumanBone != HumanBodyBones.RightUpperArm)
                {
                    Assert.That(Quaternion.Angle(entry.NativeBindLocalRotation, entry.NativeTransform!.localRotation),
                        Is.LessThan(0.001f),
                        entry.HumanBone + " native bind rotation must be captured from the imported hierarchy.");
                }
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
        public void PmxScriptedImporterVersionIsTwentyNineForMaterialReimportMigration()
        {
            object[] attributes = typeof(MmdPmxScriptedImporter).GetCustomAttributes(
                typeof(ScriptedImporterAttribute),
                inherit: false);

            Assert.That(attributes, Has.Length.EqualTo(1));
            var attribute = (ScriptedImporterAttribute)attributes[0];
            Assert.That(attribute.version, Is.EqualTo(29),
                "PMX importer version must force a material rebind after the importer migration.");
        }

        private static void CreateUtsProfileAsset(Shader utsShader)
        {
            var profile = ScriptableObject.CreateInstance<MmdMaterialProfileAsset>();
            profile.shader = utsShader;
            profile.textureTargets = new MmdMaterialProfileTextureTargets
            {
                diffuseTextureProperties = new[] { "_BaseMap", "_MainTex" },
                sphereTextureProperty = "_MatCap_Sampler",
                sphereTextureBoundProperty = "_MatCap",
                sphereModeProperty = "_Is_BlendAddToMatCap"
            };
            profile.renderingTargets = new MmdMaterialProfileRenderingTargets
            {
                baseColorProperty = "_BaseColor",
                colorProperty = "_Color",
                ambientColorProperty = string.Empty,
                alphaProperty = string.Empty,
                alphaClipThresholdProperty = "_Clipping_Level",
                shadowAlphaClipThresholdProperty = string.Empty,
                textureAlphaOutputWeightProperty = string.Empty,
                textureAlphaClipMaskProperty = "_IsBaseMapAlphaAsClippingMask",
                alphaClipModeProperty = "_ClippingMode",
                cullProperty = "_CullMode",
                surfaceProperty = string.Empty,
                blendProperty = string.Empty,
                sourceBlendProperty = string.Empty,
                destinationBlendProperty = string.Empty,
                zWriteProperty = "_ZWrite",
                outlineColorProperty = "_Outline_Color",
                outlineWidthProperty = "_Outline_Width",
                outlineVisibleProperty = "_OUTLINE",
                outlineScreenSpaceWeightProperty = string.Empty,
                outlineZTestProperty = string.Empty,
                requiredKeywords = new[]
                {
                    "_OUTLINE_NML",
                    "_MatCap",
                    "_IS_CLIPPING_OFF",
                    "_IS_CLIPPING_TRANSMODE",
                    "_IS_TRANSCLIPPING_ON",
                    "_IS_OUTLINE_CLIPPING_NO"
                },
                requiredPasses = new[] { "SRPDefaultUnlit" },
                unsupportedFeatures = new[] { "toon-texture", "self-shadow", "material-morph" },
                supportsMaterialMorphs = false
            };
            AssetDatabase.CreateAsset(profile, TempMaterialProfilePath);
            AssetDatabase.SaveAssets();
        }

        private static void AssertMmdUrpToonDiffuseTextureIsBound(string pmxPath, string texturePath)
        {
            MmdPmxAsset pmxAsset = AssetDatabase.LoadAssetAtPath<MmdPmxAsset>(pmxPath);
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            Assert.That(pmxAsset, Is.Not.Null);
            Assert.That(texture, Is.Not.Null);
            Assert.That(pmxAsset.ImportedMaterials, Is.Not.Null.And.Not.Empty);
            Material material = pmxAsset.ImportedMaterials[0];
            Assert.That(material.shader.name, Is.EqualTo(MmdUrpMaterialBindingDescriptorBuilder.MmdToonLitShaderName));
            Assert.That(material.GetTexture("_BaseMap"), Is.SameAs(texture));
            Assert.That(material.GetFloat("_BaseMapBound"), Is.EqualTo(1.0f).Within(0.00001f));
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
    }
}
