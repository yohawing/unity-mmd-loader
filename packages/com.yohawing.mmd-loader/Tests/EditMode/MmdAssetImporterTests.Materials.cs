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
            Assert.That(importedRoot.GetComponent<MmdSelfShadowTarget>(), Is.Null,
                "auto Stage PMX assets must not carry a self-shadow target because they would inflate the character AABB.");
        }
        [Test]
        public void ImportedCharacterPmxHierarchyCarriesSelfShadowTarget()
        {
            CopyFixtureToAssetDatabase("test_semi_basic_bone.pmx", TempHumanoidPmxPath);

            MmdPmxAsset pmxAsset = AssetDatabase.LoadAssetAtPath<MmdPmxAsset>(TempHumanoidPmxPath);
            Assert.That(pmxAsset, Is.Not.Null);
            Assert.That(pmxAsset.ModelPreset, Is.EqualTo(nameof(MmdPmxModelPreset.Character)));
            Assert.That(pmxAsset.ImportedRoot, Is.Not.Null);

            MmdSelfShadowTarget target = pmxAsset.ImportedRoot!.GetComponent<MmdSelfShadowTarget>();
            Assert.That(target, Is.Not.Null);
            Assert.That(target.BoundsRoot.GetComponentInChildren<SkinnedMeshRenderer>(includeInactive: true), Is.Not.Null);
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
                Assert.That(root.GetComponent<MmdSelfShadowTarget>(), Is.Null,
                    "Stage preset PMX scene instances must not be registered as character self-shadow targets.");
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
        public void ImportedHierarchyWrapperDropsStaleSelfShadowTargetWhenExcluded()
        {
            CopyFixtureToAssetDatabase("test_1bone_cube.pmx", TempPmxPath);
            MmdPmxAsset pmxAsset = AssetDatabase.LoadAssetAtPath<MmdPmxAsset>(TempPmxPath);
            Assert.That(pmxAsset, Is.Not.Null);
            Assert.That(pmxAsset.ModelPreset, Is.EqualTo(nameof(MmdPmxModelPreset.Stage)));
            Assert.That(pmxAsset.ImportedRoot, Is.Not.Null);

            GameObject sceneRoot = Object.Instantiate(pmxAsset.ImportedRoot!);
            MmdUnityModelInstance? instance = null;
            try
            {
                MmdSelfShadowTarget staleTarget = sceneRoot.AddComponent<MmdSelfShadowTarget>();
                staleTarget.BoundsRoot = sceneRoot.transform;

                instance = MmdUnityModelFactory.CreateFromInstantiatedImportedHierarchy(
                    sceneRoot,
                    pmxAsset.LoadModel(),
                    sourcePath: null,
                    pmxAsset.ImportScale,
                    includeSelfShadowTarget: false);

                Assert.That(instance.Root, Is.SameAs(sceneRoot));
                Assert.That(sceneRoot.GetComponent<MmdSelfShadowTarget>(), Is.Null,
                    "Stage/Custom imported hierarchy wrappers must remove legacy self-shadow target components.");
            }
            finally
            {
                if (instance?.Root != null)
                {
                    Object.DestroyImmediate(instance.Root);
                }
                else if (sceneRoot != null)
                {
                    Object.DestroyImmediate(sceneRoot);
                }
            }
        }
        [Test]
        public void ImportedHierarchyWrapperAppliesMaterialOverrideToDescriptor()
        {
            CopyFixtureToAssetDatabase("test_1bone_cube.pmx", TempPmxPath);
            MmdPmxAsset pmxAsset = AssetDatabase.LoadAssetAtPath<MmdPmxAsset>(TempPmxPath);
            Assert.That(pmxAsset, Is.Not.Null);
            Assert.That(pmxAsset.ImportedRoot, Is.Not.Null);

            GameObject sceneRoot = Object.Instantiate(pmxAsset.ImportedRoot!);
            MmdMaterialOverrideAsset? overrideAsset = null;
            MmdUnityModelInstance? instance = null;
            try
            {
                overrideAsset = ScriptableObject.CreateInstance<MmdMaterialOverrideAsset>();
                overrideAsset.entries = new[]
                {
                    new MmdMaterialOverrideEntry
                    {
                        materialIndex = 0,
                        hasAlpha = true,
                        alpha = 0.45f,
                        hasSurfaceMode = true,
                        surfaceMode = MmdMaterialOverrideSurfaceMode.AlphaBlend
                    }
                };

                instance = MmdUnityModelFactory.CreateFromInstantiatedImportedHierarchy(
                    sceneRoot,
                    pmxAsset.LoadModel(),
                    sourcePath: null,
                    pmxAsset.ImportScale,
                    includeSelfShadowTarget: true,
                    materialOverride: overrideAsset);

                Assert.That(instance.RenderingDescriptor.materials[0].alpha, Is.EqualTo(0.45f).Within(0.00001f));
                Assert.That(instance.RenderingDescriptor.urpMaterialBindings[0].alpha, Is.EqualTo(0.45f).Within(0.00001f));
                Assert.That(instance.RenderingDescriptor.urpMaterialBindings[0].isTransparent, Is.True);
                Assert.That(instance.RenderingDescriptor.urpMaterialBindings[0].transparencyMode, Is.EqualTo("alphaBlend"));
                Assert.That(ReadMaterialFloat(instance.Materials[0], MmdMaterialPropertyNames.Alpha), Is.EqualTo(0.45f).Within(0.00001f));
                Assert.That(instance.Materials[0].renderQueue, Is.EqualTo((int)UnityEngine.Rendering.RenderQueue.Transparent));
            }
            finally
            {
                Object.DestroyImmediate(overrideAsset);
                if (instance?.Root != null)
                {
                    Object.DestroyImmediate(instance.Root);
                }
                else if (sceneRoot != null)
                {
                    Object.DestroyImmediate(sceneRoot);
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
    }
}
