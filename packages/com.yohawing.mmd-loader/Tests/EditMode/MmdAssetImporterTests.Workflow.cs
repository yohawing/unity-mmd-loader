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
    }
}
