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
        private const string TempMaterialOverridePath = TempDirectory + "/material_override.asset";
        private const string TempNormalMapPath = TempDirectory + "/normal_map.png";
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
            model.vertices.Add(MmdTestFixtures.CreateSyntheticVertex(0, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f));
            model.vertices.Add(MmdTestFixtures.CreateSyntheticVertex(1, 1.0f, 0.0f, 0.0f, 1.0f, 0.0f));
            model.vertices.Add(MmdTestFixtures.CreateSyntheticVertex(2, 0.0f, 1.0f, 0.0f, 0.0f, 1.0f));
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





        // Humanoid setup source-slice tests.










        private static void AssertUpperArmBindPointsHorizontally(
            System.Collections.Generic.IReadOnlyList<MmdHumanoidRetargetBinding> entries,
            HumanBodyBones upperArmBone,
            HumanBodyBones lowerArmBone,
            Vector3 expectedDirection)
        {
            MmdHumanoidRetargetBinding? upperArm = null;
            MmdHumanoidRetargetBinding? lowerArm = null;
            foreach (MmdHumanoidRetargetBinding entry in entries)
            {
                if (entry.HumanBone == upperArmBone)
                {
                    upperArm = entry;
                }
                else if (entry.HumanBone == lowerArmBone)
                {
                    lowerArm = entry;
                }
            }

            Assert.That(upperArm, Is.Not.Null, upperArmBone + " binding");
            Assert.That(lowerArm, Is.Not.Null, lowerArmBone + " binding");
            Assert.That(upperArm!.NativeTransform, Is.Not.Null);
            Assert.That(lowerArm!.NativeTransform, Is.Not.Null);

            Transform upperTransform = upperArm.NativeTransform!;
            Transform lowerTransform = lowerArm.NativeTransform!;
            Quaternion originalRotation = upperTransform.localRotation;
            try
            {
                upperTransform.localRotation = upperArm.NativeBindLocalRotation;
                Vector3 direction = (lowerTransform.position - upperTransform.position).normalized;
                Assert.That(Vector3.Dot(direction, expectedDirection), Is.GreaterThan(0.999f),
                    upperArmBone + " native retarget bind must use the same geometric T-pose baseline as the proxy Avatar.");
            }
            finally
            {
                upperTransform.localRotation = originalRotation;
            }
        }

        private static void AssertNoMissingScripts(GameObject root)
        {
            Assert.That(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(root), Is.EqualTo(0), root.name);
            foreach (Transform child in root.transform)
            {
                AssertNoMissingScripts(child.gameObject);
            }
        }

        private static void AddSelfShadowFeature(
            UniversalRendererData rendererData,
            MmdSelfShadowRendererFeature feature)
        {
            var rendererDataSo = new SerializedObject(rendererData);
            var features = rendererDataSo.FindProperty("m_RendererFeatures");
            features.arraySize = 1;
            features.GetArrayElementAtIndex(0).objectReferenceValue = feature;
            rendererDataSo.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetRendererDataList(
            UniversalRenderPipelineAsset pipeline,
            int defaultRendererIndex,
            params UniversalRendererData[] rendererData)
        {
            var pipelineSo = new SerializedObject(pipeline);
            var rendererDataList = pipelineSo.FindProperty("m_RendererDataList");
            rendererDataList.arraySize = rendererData.Length;
            for (int i = 0; i < rendererData.Length; i++)
            {
                rendererDataList.GetArrayElementAtIndex(i).objectReferenceValue = rendererData[i];
            }

            var defaultRendererIndexProperty = pipelineSo.FindProperty("m_DefaultRendererIndex");
            defaultRendererIndexProperty.intValue = defaultRendererIndex;
            pipelineSo.ApplyModifiedPropertiesWithoutUndo();
        }

        private sealed class SelfShadowRendererSetupFixture : IDisposable
        {
            private SelfShadowRendererSetupFixture(
                UniversalRenderPipelineAsset pipeline,
                UniversalRendererData rendererData,
                MmdSelfShadowRendererFeature? feature)
            {
                Pipeline = pipeline;
                RendererData = rendererData;
                Feature = feature;
            }

            public UniversalRenderPipelineAsset Pipeline { get; }

            private UniversalRendererData RendererData { get; }

            private MmdSelfShadowRendererFeature? Feature { get; }

            public static SelfShadowRendererSetupFixture Create(bool includeFeature, bool featureEnabled = true)
            {
                var pipeline = ScriptableObject.CreateInstance<UniversalRenderPipelineAsset>();
                var rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
                MmdSelfShadowRendererFeature? feature = null;

                if (includeFeature)
                {
                    feature = ScriptableObject.CreateInstance<MmdSelfShadowRendererFeature>();
                    feature.SetActive(featureEnabled);
                    AddSelfShadowFeature(rendererData, feature);
                }

                SetRendererDataList(pipeline, 0, rendererData);

                return new SelfShadowRendererSetupFixture(pipeline, rendererData, feature);
            }

            public void Dispose()
            {
                if (Feature != null)
                {
                    Object.DestroyImmediate(Feature);
                }

                Object.DestroyImmediate(RendererData);
                Object.DestroyImmediate(Pipeline);
            }
        }
    }
}
