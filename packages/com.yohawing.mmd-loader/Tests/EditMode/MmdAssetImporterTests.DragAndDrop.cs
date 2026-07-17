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
        public void ImportedMmdAssetsUseLightweightCustomInspectors()
        {
            CopyFixtureToAssetDatabase("test_1bone_cube.pmx", TempPmxPath);
            CopyFixtureToAssetDatabase("test_1bone_cube_motion.vmd", TempVmdPath);
            MmdPmxAsset pmxAsset = AssetDatabase.LoadAssetAtPath<MmdPmxAsset>(TempPmxPath);
            MmdVmdAsset vmdAsset = AssetDatabase.LoadAssetAtPath<MmdVmdAsset>(TempVmdPath);
            AssetImporter pmxImporter = AssetImporter.GetAtPath(TempPmxPath);
            AssetImporter vmdImporter = AssetImporter.GetAtPath(TempVmdPath);
            UnityEditor.Editor? pmxEditor = null;
            UnityEditor.Editor? vmdEditor = null;
            UnityEditor.Editor? pmxImporterEditor = null;
            UnityEditor.Editor? vmdImporterEditor = null;

            try
            {
                pmxEditor = UnityEditor.Editor.CreateEditor(pmxAsset);
                vmdEditor = UnityEditor.Editor.CreateEditor(vmdAsset);
                pmxImporterEditor = UnityEditor.Editor.CreateEditor(pmxImporter);
                vmdImporterEditor = UnityEditor.Editor.CreateEditor(vmdImporter);

                Assert.That(pmxEditor, Is.TypeOf<MmdPmxAssetEditor>());
                Assert.That(vmdEditor, Is.TypeOf<MmdVmdAssetEditor>());
                Assert.That(pmxImporterEditor, Is.TypeOf<MmdPmxScriptedImporterEditor>());
                Assert.That(vmdImporterEditor, Is.TypeOf<MmdVmdScriptedImporterEditor>());
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

                if (vmdImporterEditor != null)
                {
                    Object.DestroyImmediate(vmdImporterEditor);
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
                MmdTestInstanceScope.DestroyImporterCacheInstance(result?.Instance);
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
                MmdTestInstanceScope.DestroyImporterCacheInstance(result?.Instance);
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
    }
}
