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

                MmdTestInstanceScope.DestroyImporterCacheInstance(instance);
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
                MmdTestInstanceScope.DestroyImporterCacheInstance(instance);
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
                MmdTestInstanceScope.DestroyImporterCacheInstance(instance);
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

                // The authored Scene instance keeps importer sub-assets while active playback mutates clones.
                Material[] placedMats = loadResult.Instance.Materials;
                Assert.That(placedMats, Is.Not.Null.And.Not.Empty);
                Assert.That(placedMats[0], Is.SameAs(pmxAsset.ImportedMaterials[0]), "playback asset load must bind over importer Material sub-asset, not runtime-generated material");

                SkinnedMeshRenderer? smr = loadResult.Instance.SkinnedMeshRenderer;
                Assert.That(smr, Is.Not.Null);
                Assert.That(smr!.sharedMaterials[0], Is.Not.SameAs(pmxAsset.ImportedMaterials[0]));
                Assert.That(smr.sharedMaterials[0].name, Does.StartWith(pmxAsset.ImportedMaterials[0].name));
                Assert.That(loadResult.Instance.Root.GetComponent<MmdSelfShadowTarget>(), Is.Null,
                    "Stage preset PMX+VMD asset scene load must not register a character self-shadow target.");

                Assert.That(loadResult.Controller.ModelAssetSource, Is.SameAs(pmxAsset));
                Assert.That(loadResult.Controller.MotionAssetSource, Is.SameAs(vmdAsset));

                loadResult.Binding.Dispose();
                Assert.That(smr.sharedMaterials[0], Is.SameAs(pmxAsset.ImportedMaterials[0]));
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
                MmdTestInstanceScope.DestroyImporterCacheInstance(instance);
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
    }
}
