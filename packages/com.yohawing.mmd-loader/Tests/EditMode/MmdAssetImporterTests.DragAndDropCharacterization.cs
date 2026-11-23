#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Mmd.Editor;
using Mmd.Parser;
using Mmd.Rendering;
using Mmd.UnityIntegration;
using Object = UnityEngine.Object;

namespace Mmd.Tests
{
    // Characterization of the PMX asset scene drag-and-drop instance. Pins everything the dropped
    // instance exposes: the importer-owned mesh and materials, bones and bind pose, physics bodies,
    // diagnostics, material rendering targets, and the rendering-descriptor metadata consumed after
    // the drop. The descriptor metadata and material binding diagnostics are compared against the
    // full split descriptor (Build + SplitBySubmesh + override), which is what the drop path has
    // always produced for these fields.
    public sealed partial class MmdAssetImporterTests
    {
        [TestCase("test_1bone_cube.pmx", null)]
        [TestCase("test_vertex_morph.pmx", null)]
        [TestCase("test_hair_physics.pmx", null)]
        [TestCase("GeneratedPmx/mmd-material-morph-alpha-opaque-depth.pmx", null)]
        [TestCase("GeneratedPmx/mmd-texture-alpha-used-uv-cutout.pmx", "GeneratedPmx/texture-alpha-cutout.png")]
        public void SceneDragAndDropPmxAssetInstanceCharacterization(string pmxFixture, string? textureFixture)
        {
            MmdPmxAsset pmxAsset = ImportCharacterizationFixture(pmxFixture, textureFixture);
            MmdModelDefinition model = pmxAsset.LoadModel();
            MmdUnityModelInstance? instance = null;
            try
            {
                instance = MmdSceneDragAndDrop.LoadPmxForDragAndDrop(pmxAsset, new Vector3(1.0f, 2.0f, 3.0f), parent: null);

                AssertDragAndDropInstanceCharacterization(pmxAsset, model, instance, expectMaterialClones: false);
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
        public void SceneDragAndDropPmxAssetInstanceCharacterizationWithMaterialOverride()
        {
            MmdTestFixtures.CopyFixtureToAssetDatabase("test_1bone_cube.pmx", TempPmxPath);
            MmdMaterialOverrideAsset overrideAsset = ScriptableObject.CreateInstance<MmdMaterialOverrideAsset>();
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
            AssetDatabase.CreateAsset(overrideAsset, TempMaterialOverridePath);
            AssetDatabase.ImportAsset(TempMaterialOverridePath, ImportAssetOptions.ForceUpdate);
            MmdMaterialOverrideAsset persistedOverride =
                AssetDatabase.LoadAssetAtPath<MmdMaterialOverrideAsset>(TempMaterialOverridePath);
            Assert.That(persistedOverride, Is.Not.Null);

            var importer = AssetImporter.GetAtPath(TempPmxPath) as MmdPmxScriptedImporter;
            Assert.That(importer, Is.Not.Null);
            var serializedImporter = new SerializedObject(importer!);
            serializedImporter.FindProperty("materialOverrideAsset").objectReferenceValue = persistedOverride;
            serializedImporter.ApplyModifiedPropertiesWithoutUndo();
            importer!.SaveAndReimport();

            MmdPmxAsset pmxAsset = AssetDatabase.LoadAssetAtPath<MmdPmxAsset>(TempPmxPath);
            Assert.That(pmxAsset.MaterialOverrideAsset, Is.SameAs(persistedOverride));
            MmdModelDefinition model = pmxAsset.LoadModel();
            MmdUnityModelInstance? instance = null;
            try
            {
                instance = MmdSceneDragAndDrop.LoadPmxForDragAndDrop(pmxAsset, Vector3.zero, parent: null);

                AssertDragAndDropInstanceCharacterization(pmxAsset, model, instance, expectMaterialClones: true);
                Assert.That(instance.RenderingDescriptor.materials[0].alpha, Is.EqualTo(0.45f).Within(0.00001f));
                Assert.That(instance.RenderingDescriptor.urpMaterialBindings[0].transparencyMode, Is.EqualTo("alphaBlend"));
                Assert.That(instance.MaterialBindingDiagnostics[0].transparencyMode, Is.EqualTo("alphaBlend"));
                Assert.That(instance.Materials[0].renderQueue, Is.EqualTo((int)UnityEngine.Rendering.RenderQueue.Transparent));
            }
            finally
            {
                if (instance != null)
                {
                    // The override clones are owned by the dropped instance; the imported
                    // material sub-assets are persistent and must not be destroyed.
                    foreach (Material material in instance.Materials)
                    {
                        if (material != null && !EditorUtility.IsPersistent(material))
                        {
                            Object.DestroyImmediate(material);
                        }
                    }
                }

                if (instance?.Root != null)
                {
                    Object.DestroyImmediate(instance.Root);
                }
            }
        }

        [Test]
        public void ImportedHierarchyPublicPathsReturnFullRenderingDescriptor()
        {
            MmdPmxAsset pmxAsset = ImportCharacterizationFixture("test_vertex_morph.pmx", textureFixture: null);
            MmdModelDefinition model = pmxAsset.LoadModel();
            Assert.That(model.bones, Is.Not.Empty);
            MmdRenderingDescriptor reference = MmdRenderingMeshSplitter.SplitBySubmesh(
                MmdRenderingDescriptorBuilder.Build(model, MmdMaterialPreset.MmdToon)).rendering;
            Assert.That(reference.vertexMorphs.Any(morph => morph.offsets.Count > 0), Is.True,
                "the fixture must carry vertex-morph offsets for this check to mean anything");

            MmdUnityModelInstance? factoryInstance = null;
            MmdUnityModelInstance? facadeInstance = null;
            MmdUnityModelInstance? droppedInstance = null;
            try
            {
                factoryInstance = MmdUnityModelFactory.CreateFromImportedHierarchy(
                    pmxAsset.ImportedRoot!,
                    model,
                    sourcePath: null,
                    pmxAsset.ImportScale);
                AssertFullRenderingDescriptor(factoryInstance.RenderingDescriptor, reference, "CreateFromImportedHierarchy");

                facadeInstance = MmdEditorVerificationFacade.LoadPmxIntoScene(pmxAsset).Instance;
                AssertFullRenderingDescriptor(facadeInstance.RenderingDescriptor, reference, "LoadPmxIntoScene(MmdPmxAsset)");

                // Scene drag-and-drop only uses the dropped root, so it skips the geometry.
                droppedInstance = MmdSceneDragAndDrop.LoadPmxForDragAndDrop(pmxAsset, Vector3.zero, parent: null);
                Assert.That(droppedInstance.RenderingDescriptor.vertices, Is.Empty);
                Assert.That(droppedInstance.RenderingDescriptor.indices, Is.Empty);
                Assert.That(droppedInstance.RenderingDescriptor.skinning, Is.Empty);
            }
            finally
            {
                foreach (MmdUnityModelInstance? instance in new[] { factoryInstance, facadeInstance, droppedInstance })
                {
                    if (instance?.Root != null)
                    {
                        Object.DestroyImmediate(instance.Root);
                    }
                }
            }
        }

        private static void AssertFullRenderingDescriptor(
            MmdRenderingDescriptor actual,
            MmdRenderingDescriptor reference,
            string path)
        {
            Assert.That(actual.vertices, Is.Not.Empty, path);
            Assert.That(actual.vertices.Count, Is.EqualTo(reference.vertices.Count), path);
            Assert.That(actual.indices, Is.EqualTo(reference.indices), path);
            Assert.That(actual.skinning, Is.Not.Empty, path);
            Assert.That(actual.skinning.Count, Is.EqualTo(reference.skinning.Count), path);
            Assert.That(
                actual.vertexMorphs.Select(morph => morph.offsets.Count),
                Is.EqualTo(reference.vertexMorphs.Select(morph => morph.offsets.Count)),
                path);
            Assert.That(JsonUtility.ToJson(actual.textureOrientation), Is.EqualTo(JsonUtility.ToJson(reference.textureOrientation)), path);
        }

        private MmdPmxAsset ImportCharacterizationFixture(string pmxFixture, string? textureFixture)
        {
            Directory.CreateDirectory(Path.Combine(MmdTestFixtures.ProjectRoot, TempDirectory));
            if (textureFixture != null)
            {
                string textureAssetPath = TempDirectory + "/" + Path.GetFileName(textureFixture);
                File.Copy(
                    MmdTestFixtures.FixtureAssetPath(textureFixture),
                    Path.Combine(MmdTestFixtures.ProjectRoot, textureAssetPath),
                    overwrite: true);
                AssetDatabase.ImportAsset(textureAssetPath, ImportAssetOptions.ForceUpdate);
            }

            string pmxAssetPath = TempDirectory + "/" + Path.GetFileName(pmxFixture);
            MmdTestFixtures.CopyFixtureToAssetDatabase(pmxFixture, pmxAssetPath);
            MmdPmxAsset pmxAsset = AssetDatabase.LoadAssetAtPath<MmdPmxAsset>(pmxAssetPath);
            Assert.That(pmxAsset, Is.Not.Null);
            Assert.That(pmxAsset.ImportedRoot, Is.Not.Null, "the drop path under test instantiates the imported prefab");
            return pmxAsset;
        }

        private static void AssertDragAndDropInstanceCharacterization(
            MmdPmxAsset pmxAsset,
            MmdModelDefinition model,
            MmdUnityModelInstance instance,
            bool expectMaterialClones)
        {
            GameObject root = instance.Root;
            GameObject importedRoot = pmxAsset.ImportedRoot!;

            // Scene placement: a prefab instance of the imported root with a configured controller.
            Assert.That(PrefabUtility.GetCorrespondingObjectFromSource(root), Is.SameAs(importedRoot));
            MmdUnityPlaybackController? controller = root.GetComponent<MmdUnityPlaybackController>();
            Assert.That(controller, Is.Not.Null);
            Assert.That(controller!.ModelAssetSource, Is.SameAs(pmxAsset));
            foreach (Transform transform in root.GetComponentsInChildren<Transform>(includeInactive: true))
            {
                Assert.That(transform.gameObject.hideFlags, Is.EqualTo(HideFlags.None), transform.name);
            }

            // Mesh: the importer-owned sub-asset, never rebuilt.
            Renderer renderer = model.bones.Count > 0
                ? instance.SkinnedMeshRenderer!
                : instance.MeshRenderer!;
            Assert.That(renderer, Is.Not.Null);
            Renderer importedRenderer = (Renderer)PrefabUtility.GetCorrespondingObjectFromSource(renderer);
            Assert.That(importedRenderer, Is.Not.Null);
            Mesh importedMesh = model.bones.Count > 0
                ? ((SkinnedMeshRenderer)importedRenderer).sharedMesh
                : importedRenderer.GetComponent<MeshFilter>().sharedMesh;
            Mesh currentMesh = model.bones.Count > 0
                ? ((SkinnedMeshRenderer)renderer).sharedMesh
                : renderer.GetComponent<MeshFilter>().sharedMesh;
            Assert.That(instance.Mesh, Is.SameAs(importedMesh));
            Assert.That(currentMesh, Is.SameAs(importedMesh));
            Assert.That(AssetDatabase.GetAssetPath(instance.Mesh), Is.EqualTo(AssetDatabase.GetAssetPath(pmxAsset)));
            Assert.That(instance.Mesh.name, Does.Not.Contain("Split Runtime"));
            Assert.That(instance.VertexCount, Is.EqualTo(importedMesh.vertexCount));
            Assert.That(instance.SubmeshCount, Is.EqualTo(importedMesh.subMeshCount));
            Assert.That(instance.SubmeshCount, Is.EqualTo(model.materials.Count));
            Assert.That(instance.IndexCount, Is.EqualTo(model.indices.Count));

            // Materials: the imported materials, or per-instance clones when an override is set.
            Material[] importedMaterials = importedRenderer.sharedMaterials;
            Assert.That(instance.Materials, Is.EqualTo(renderer.sharedMaterials));
            Assert.That(instance.Materials.Length, Is.EqualTo(importedMaterials.Length));
            for (int i = 0; i < importedMaterials.Length; i++)
            {
                if (expectMaterialClones)
                {
                    Assert.That(instance.Materials[i], Is.Not.SameAs(importedMaterials[i]), $"material {i}");
                    Assert.That(instance.Materials[i].name, Is.EqualTo(importedMaterials[i].name), $"material {i}");
                    Assert.That(instance.Materials[i].shader, Is.SameAs(importedMaterials[i].shader), $"material {i}");
                }
                else
                {
                    Assert.That(instance.Materials[i], Is.SameAs(importedMaterials[i]), $"material {i}");
                }
            }

            // Bones: the renderer's bones, reset to the imported bind pose.
            if (model.bones.Count > 0)
            {
                var skinned = (SkinnedMeshRenderer)renderer;
                Assert.That(instance.BoneTransforms, Is.EqualTo(skinned.bones));
                Assert.That(instance.BoneTransforms.Length, Is.EqualTo(model.bones.Count));
                Assert.That(skinned.rootBone, Is.SameAs(instance.BoneTransforms[0]));
                for (int i = 0; i < instance.BoneTransforms.Length; i++)
                {
                    Transform bone = instance.BoneTransforms[i];
                    Transform importedBone = (Transform)PrefabUtility.GetCorrespondingObjectFromSource(bone);
                    Assert.That(importedBone, Is.Not.Null, bone.name);
                    AssertVector3Near(bone.localPosition, importedBone.localPosition, bone.name);
                    AssertQuaternionNear(bone.localRotation, importedBone.localRotation, bone.name);
                    AssertVector3Near(instance.BindLocalPositions[i], bone.localPosition, bone.name);
                    AssertQuaternionNear(instance.BindLocalRotations[i], bone.localRotation, bone.name);
                }
            }
            else
            {
                Assert.That(instance.BoneTransforms, Is.Empty);
            }

            MmdUnityPhysicsBody[] physicsBodies = root.GetComponentsInChildren<MmdUnityPhysicsBody>(includeInactive: true);
            Assert.That(instance.PhysicsBodies, Is.EqualTo(model.bones.Count > 0 ? physicsBodies : Array.Empty<MmdUnityPhysicsBody>()));
            Assert.That(
                physicsBodies.Length,
                Is.EqualTo(importedRoot.GetComponentsInChildren<MmdUnityPhysicsBody>(includeInactive: true).Length));

            // Source, scale and diagnostics that do not depend on the rendering descriptor.
            Assert.That(instance.ImportScale, Is.EqualTo(MmdCoordinateSpace.NormalizeImportScale(pmxAsset.ImportScale)));
            string? expectedSourcePath = string.IsNullOrWhiteSpace(pmxAsset.SourcePath) ? null : pmxAsset.SourcePath;
            Assert.That(instance.SourceContext?.SourcePath, Is.EqualTo(MmdUnityModelSourceContext.FromOptionalPath(expectedSourcePath)?.SourcePath));
            Assert.That(instance.OwnedTextures, Is.Empty);
            Assert.That(instance.LoadedDiffuseTextureCount, Is.Zero);
            Assert.That(instance.MissingTextureReferenceCount, Is.Zero);
            Assert.That(instance.ShaderDiagnostics.resolvedShaderName, Is.EqualTo(renderer.sharedMaterial.shader.name));
            Assert.That(
                instance.BlendShapeIndexMap.OrderBy(pair => pair.Value).Select(pair => pair.Key),
                Is.EqualTo(Enumerable.Range(0, importedMesh.blendShapeCount).Select(importedMesh.GetBlendShapeName)));

            // Rendering-descriptor metadata and descriptor-derived diagnostics: identical to the
            // full split descriptor with the material override applied.
            MmdRenderingDescriptor reference = MmdRenderingMeshSplitter.SplitBySubmesh(
                MmdRenderingDescriptorBuilder.Build(model, MmdMaterialPreset.MmdToon)).rendering;
            MmdMaterialOverrideApplier.ApplyToRenderingDescriptor(pmxAsset.MaterialOverrideAsset, reference);
            AssertRenderingDescriptorMetadataEqual(instance.RenderingDescriptor, reference);

            var expected = new MmdUnityModelInstance(
                root,
                instance.Mesh,
                instance.Materials,
                reference,
                instance.BoneTransforms,
                instance.PhysicsBodies,
                instance.MeshRenderer,
                instance.SkinnedMeshRenderer,
                instance.SourceContext,
                Array.Empty<Texture2D>(),
                new MmdTextureBindingDiagnostics(),
                instance.ShaderDiagnostics,
                instance.ImportScale,
                pmxAsset.ResolveMaterialRenderingTargets(instance.Materials.Length));
            Assert.That(instance.MaterialRenderingTargets, Is.EqualTo(expected.MaterialRenderingTargets));
            Assert.That(
                instance.MaterialBindingDiagnostics.Select(diagnostic => JsonUtility.ToJson(diagnostic)),
                Is.EqualTo(expected.MaterialBindingDiagnostics.Select(diagnostic => JsonUtility.ToJson(diagnostic))));
            Assert.That(instance.MaterialBindingDiagnostics.Length, Is.EqualTo(model.materials.Count));
            Assert.That(
                instance.VertexMorphBlendShapes.Select(DescribeBlendShapeBinding),
                Is.EqualTo(expected.VertexMorphBlendShapes.Select(DescribeBlendShapeBinding)));
            foreach (MmdUnityVertexMorphBlendShapeBinding binding in instance.VertexMorphBlendShapes)
            {
                Assert.That(importedMesh.GetBlendShapeName(binding.BlendShapeIndex), Is.EqualTo(binding.BlendShapeName));
            }

            Assert.DoesNotThrow(() => MmdUnityFrameApplier.ValidateSupportedMorphPlayback(instance));
        }

        private static void AssertRenderingDescriptorMetadataEqual(
            MmdRenderingDescriptor actual,
            MmdRenderingDescriptor expected)
        {
            Assert.That(ToJsonLines(actual.materials), Is.EqualTo(ToJsonLines(expected.materials)), "materials");
            Assert.That(ToJsonLines(actual.urpMaterialBindings), Is.EqualTo(ToJsonLines(expected.urpMaterialBindings)), "urpMaterialBindings");
            Assert.That(
                actual.submeshes.Select(submesh => (submesh.submeshIndex, submesh.materialIndex, submesh.indexStart, submesh.indexCount)),
                Is.EqualTo(expected.submeshes.Select(submesh => (submesh.submeshIndex, submesh.materialIndex, submesh.indexStart, submesh.indexCount))),
                "submeshes");
            Assert.That(ToJsonLines(actual.groupMorphs), Is.EqualTo(ToJsonLines(expected.groupMorphs)), "groupMorphs");
            Assert.That(ToJsonLines(actual.materialMorphs), Is.EqualTo(ToJsonLines(expected.materialMorphs)), "materialMorphs");
            Assert.That(ToJsonLines(actual.flipMorphs), Is.EqualTo(ToJsonLines(expected.flipMorphs)), "flipMorphs");
            Assert.That(
                actual.vertexMorphs.Select(morph => (morph.morphIndex, morph.morphName)),
                Is.EqualTo(expected.vertexMorphs.Select(morph => (morph.morphIndex, morph.morphName))),
                "vertexMorphs");
            Assert.That(
                actual.uvMorphs.Select(morph => (morph.morphIndex, morph.morphName, morph.morphType, morph.uvOffsetCount)),
                Is.EqualTo(expected.uvMorphs.Select(morph => (morph.morphIndex, morph.morphName, morph.morphType, morph.uvOffsetCount))),
                "uvMorphs");
            Assert.That(actual.ikCount, Is.EqualTo(expected.ikCount), "ikCount");
        }

        private static IEnumerable<string> ToJsonLines<T>(IEnumerable<T> items)
        {
            return items.Select(item => JsonUtility.ToJson(item)).ToArray();
        }

        private static string DescribeBlendShapeBinding(MmdUnityVertexMorphBlendShapeBinding binding)
        {
            return $"{binding.MorphIndex}|{binding.MorphName}|{binding.BlendShapeName}|{binding.BlendShapeIndex}";
        }

        private static void AssertVector3Near(Vector3 actual, Vector3 expected, string message)
        {
            Assert.That(Vector3.Distance(actual, expected), Is.LessThan(0.00001f), message);
        }

        private static void AssertQuaternionNear(Quaternion actual, Quaternion expected, string message)
        {
            Assert.That(Mathf.Abs(Quaternion.Dot(actual, expected)), Is.GreaterThan(0.99999f), message);
        }
    }
}
