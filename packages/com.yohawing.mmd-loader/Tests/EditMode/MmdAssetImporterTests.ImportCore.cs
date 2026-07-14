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

            Assert.That(pmxAsset.ModelPreset, Is.EqualTo(nameof(MmdPmxModelPreset.Stage)));
            Assert.That(pmxAsset.MeshGenerationMode, Is.EqualTo(nameof(MmdPmxMeshGenerationMode.SingleMesh)));
            Assert.That(pmxAsset.MaterialTexturePolicy, Is.EqualTo(nameof(MmdPmxMaterialTexturePolicy.ResolveReferencesOnly)));
            Assert.That(pmxAsset.AnimationType, Is.EqualTo(nameof(MmdPmxAnimationType.Generic)));
            Assert.That(pmxAsset.ShaderPreset, Is.EqualTo(nameof(MmdPmxShaderPreset.MmdBasicUrpToon)));
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
            serializedImporter.FindProperty("meshGenerationMode").enumValueIndex = (int)MmdPmxMeshGenerationMode.SplitByMaterial;
            serializedImporter.FindProperty("materialTexturePolicy").enumValueIndex = (int)MmdPmxMaterialTexturePolicy.ResolveReferencesOnly;
            serializedImporter.FindProperty("animationType").enumValueIndex = (int)MmdPmxAnimationType.Humanoid;
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

            Assert.That(pmxAsset.ShaderPreset, Is.EqualTo(nameof(MmdPmxShaderPreset.UrpLit)));
            Assert.That(pmxAsset.ImportedMaterials, Is.Not.Null.And.Not.Empty);
            Assert.That(pmxAsset.ImportedMaterials[0].shader, Is.Not.Null);
            Assert.That(pmxAsset.ImportedMaterials[0].shader.name,
                Is.EqualTo(MmdUrpMaterialBindingDescriptorBuilder.UrpLitShaderName));
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

            Assert.That(pmxAsset.ShaderPreset, Is.EqualTo(nameof(MmdPmxShaderPreset.UrpLit)));
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
            Assert.That(pmxAsset.ModelPreset, Is.EqualTo(nameof(MmdPmxModelPreset.Character)));
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
        public void PmxScriptedImporterVersionIsTwentySixForOutlineMaterialPolicy()
        {
            object[] attributes = typeof(MmdPmxScriptedImporter).GetCustomAttributes(
                typeof(ScriptedImporterAttribute),
                inherit: false);

            Assert.That(attributes, Has.Length.EqualTo(1));
            var attribute = (ScriptedImporterAttribute)attributes[0];
            Assert.That(attribute.version, Is.EqualTo(26),
                "PMX importer version must force reimport for outline material policy changes.");
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
            Assert.That(pmxAsset.ImportedMaterials[0].shader, Is.Not.Null);
            Assert.That(pmxAsset.ImportedMaterials[0].shader.name,
                Is.EqualTo(MmdUrpMaterialBindingDescriptorBuilder.DefaultShaderName));
        }
    }
}
