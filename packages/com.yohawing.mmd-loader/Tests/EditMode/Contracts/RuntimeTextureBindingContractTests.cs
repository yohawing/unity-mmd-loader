#nullable enable

using System.IO;
using System.Linq;
using System.Diagnostics;
using NUnit.Framework;
using UnityEngine;
using Mmd.Parser;
using Mmd.Rendering;
using Mmd.UnityIntegration;

namespace Mmd.Tests
{
    [TestFixture]
    public sealed class RuntimeTextureBindingContractTests
    {
        [TestCase(@"textures\diffuse.png")]
        [TestCase("textures/diffuse.png")]
        public void RelativeDiffuseTextureLoadsFromPmxSourceDirectoryAndBindsMaterialSlots(string textureReference)
        {
            MmdUnityModelInstance? instance = null;
            string tempRoot = CreateTempDirectory();
            try
            {
                string pmxPath = Path.Combine(tempRoot, "model.pmx");
                string textureDirectory = Path.Combine(tempRoot, "textures");
                Directory.CreateDirectory(textureDirectory);
                File.WriteAllBytes(pmxPath, new byte[] { 0x50, 0x4d, 0x58 });
                WritePng(Path.Combine(textureDirectory, "diffuse.png"), Color.red);

                MmdModelDefinition model = CreateMinimalTriangleModel();
                model.materials[0].texture = textureReference;

                instance = MmdUnityModelFactory.CreateStaticModel(model, pmxPath);

                Assert.That(instance.SourceContext, Is.Not.Null);
                MmdUnityModelSourceContext sourceContext = instance.SourceContext!;
                Assert.That(sourceContext.SourcePath, Is.EqualTo(Path.GetFullPath(pmxPath)));
                Assert.That(instance.LoadedDiffuseTextureCount, Is.EqualTo(1));
                Assert.That(instance.OwnedTextures, Has.Length.EqualTo(1));
                Assert.That(instance.MissingTextureReferenceCount, Is.EqualTo(0));
                Assert.That(instance.UnsupportedTextureReferenceCount, Is.EqualTo(0));
                Assert.That(ReadBoundDiffuseTexture(instance.Materials[0]), Is.EqualTo(instance.OwnedTextures[0]));
                Assert.That(instance.MaterialBindingDiagnostics[0].baseMapBound, Is.True);
                Assert.That(instance.MaterialBindingDiagnostics[0].mainTexBound, Is.True);
                Assert.That(instance.MaterialBindingDiagnostics[0].baseMapTexture, Is.EqualTo(textureReference));
                MmdTextureReferenceDiagnostic diagnostic = instance.TextureDiagnostics.TextureReferences.Single();
                Assert.That(diagnostic.reference, Is.EqualTo(textureReference));
                Assert.That(diagnostic.resolvedPath, Is.EqualTo("textures/diffuse.png"));
                AssertDiagnosticsDoNotContain(instance.TextureDiagnostics, tempRoot, pmxPath, textureDirectory);
            }
            finally
            {
                DestroyInstance(instance);
                Directory.Delete(tempRoot, recursive: true);
            }
        }

        [TestCase(@"..\outside.png")]
        [TestCase("../outside.png")]
        [TestCase(@"textures/sub/..\..\..\outside.png")]
        public void EscapedRelativeTexturePathIsRejectedWithStructuredDiagnostics(string textureReference)
        {
            string tempRoot = CreateTempDirectory();
            try
            {
                string pmxDirectory = Path.Combine(tempRoot, "model");
                Directory.CreateDirectory(pmxDirectory);
                string pmxPath = Path.Combine(pmxDirectory, "model.pmx");
                File.WriteAllBytes(pmxPath, new byte[] { 0x50, 0x4d, 0x58 });

                MmdModelDefinition model = CreateMinimalTriangleModel();
                model.materials[0].texture = textureReference;
                MmdRenderingDescriptor descriptor = MmdRenderingDescriptorBuilder.Build(model);

                MmdRuntimeTextureResolution resolution = MmdRuntimeTextureResolver.ResolveDiffuseTextures(
                    descriptor,
                    MmdUnityModelSourceContext.FromOptionalPath(pmxPath));

                Assert.That(resolution.DiffuseTextures, Is.Empty);
                Assert.That(resolution.Diagnostics.UnsupportedTextureReferenceCount, Is.EqualTo(1));
                Assert.That(resolution.Diagnostics.TextureReferences, Has.Count.EqualTo(1));
                Assert.That(resolution.Diagnostics.TextureReferences[0].materialIndex, Is.EqualTo(0));
                Assert.That(resolution.Diagnostics.TextureReferences[0].usage, Is.EqualTo("diffuse"));
                Assert.That(resolution.Diagnostics.TextureReferences[0].reference, Is.EqualTo(textureReference));
                Assert.That(resolution.Diagnostics.TextureReferences[0].status, Is.EqualTo("unsupported"));
                Assert.That(resolution.Diagnostics.TextureReferences[0].resolvedPath, Is.Empty);
                Assert.That(resolution.Diagnostics.TextureReferences[0].reason, Does.Contain("escapes the PMX source directory"));
                Assert.That(
                    resolution.Diagnostics.Messages.Any(message => message.Contains("escapes the PMX source directory")),
                    Is.True);
                AssertDiagnosticsDoNotContain(resolution.Diagnostics, tempRoot, pmxPath);
            }
            finally
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }

        [Test]
        public void AbsoluteTexturePathIsRejectedAndRedactedEvenWhenFileExists()
        {
            string tempRoot = CreateTempDirectory();
            try
            {
                string pmxDirectory = Path.Combine(tempRoot, "model");
                Directory.CreateDirectory(pmxDirectory);
                string pmxPath = Path.Combine(pmxDirectory, "model.pmx");
                string outsidePath = Path.Combine(tempRoot, "outside.png");
                File.WriteAllBytes(pmxPath, new byte[] { 0x50, 0x4d, 0x58 });
                WritePng(outsidePath, Color.blue);

                MmdRuntimeTextureResolution resolution = ResolveSingleTexture(pmxPath, outsidePath);

                AssertRejectedAndRedacted(resolution.Diagnostics, tempRoot, pmxPath, outsidePath);
            }
            finally
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }

        [TestCase(@"\\server\share\texture.png")]
        [TestCase(@"\private\texture.png")]
        [TestCase(@"\\?\C:\private\texture.png")]
        [TestCase(@"\\.\C:\private\texture.png")]
        [TestCase(@"\??\C:\private\texture.png")]
        [TestCase("file:///C:/private/texture.png")]
        [TestCase("https://example.invalid/texture.png")]
        [TestCase("data:image/png;base64,AAAA")]
        [TestCase(@"C:private\texture.png")]
        public void RootedDeviceAndUriTextureReferencesAreRejectedBeforeResolution(string textureReference)
        {
            string tempRoot = CreateTempDirectory();
            try
            {
                string pmxPath = Path.Combine(tempRoot, "model.pmx");
                File.WriteAllBytes(pmxPath, new byte[] { 0x50, 0x4d, 0x58 });

                MmdRuntimeTextureResolution resolution = ResolveSingleTexture(pmxPath, textureReference);

                MmdTextureReferenceDiagnostic diagnostic = resolution.Diagnostics.TextureReferences.Single();
                Assert.That(resolution.DiffuseTextures, Is.Empty);
                Assert.That(diagnostic.reference, Is.EqualTo("<redacted-path>"));
                Assert.That(diagnostic.resolvedPath, Is.Empty);
                Assert.That(diagnostic.status, Is.EqualTo("unsupported"));
                Assert.That(diagnostic.reason, Does.Contain("explicitly allowed external root"));
                AssertDiagnosticsDoNotContain(resolution.Diagnostics, tempRoot, pmxPath, textureReference);
            }
            finally
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }

        [Test]
        public void JunctionInsidePmxDirectoryCannotReadOutsideTrustedRoot()
        {
            if (Path.DirectorySeparatorChar != '\\')
            {
                Assert.Ignore("Windows junction contract.");
            }

            string tempRoot = CreateTempDirectory();
            string junctionPath = Path.Combine(tempRoot, "model", "linked");
            try
            {
                string pmxDirectory = Path.Combine(tempRoot, "model");
                string outsideDirectory = Path.Combine(tempRoot, "outside");
                Directory.CreateDirectory(pmxDirectory);
                Directory.CreateDirectory(outsideDirectory);
                string pmxPath = Path.Combine(pmxDirectory, "model.pmx");
                string outsidePath = Path.Combine(outsideDirectory, "secret.png");
                File.WriteAllBytes(pmxPath, new byte[] { 0x50, 0x4d, 0x58 });
                WritePng(outsidePath, Color.green);

                using (Process? process = Process.Start(new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/d /c mklink /J \"{junctionPath}\" \"{outsideDirectory}\"",
                    CreateNoWindow = true,
                    UseShellExecute = false
                }))
                {
                    Assert.That(process, Is.Not.Null, "Could not start junction creation.");
                    Assert.That(process!.WaitForExit(10000), Is.True, "Junction creation timed out.");
                    Assert.That(process.ExitCode, Is.EqualTo(0), "Junction creation failed.");
                }

                MmdRuntimeTextureResolution resolution = ResolveSingleTexture(pmxPath, @"linked\secret.png");
                MmdTextureReferenceDiagnostic diagnostic = resolution.Diagnostics.TextureReferences.Single();

                Assert.That(resolution.DiffuseTextures, Is.Empty);
                Assert.That(diagnostic.status, Is.EqualTo("unsupported"));
                Assert.That(diagnostic.resolvedPath, Is.Empty);
                Assert.That(diagnostic.reason, Does.Contain("symbolic link or junction"));
                AssertDiagnosticsDoNotContain(resolution.Diagnostics, tempRoot, pmxPath, outsidePath);
            }
            finally
            {
                if (Directory.Exists(junctionPath))
                {
                    Directory.Delete(junctionPath, recursive: false);
                }

                Directory.Delete(tempRoot, recursive: true);
            }
        }

        [Test]
        public void MissingRelativeTextureUsesOnlyRootRelativeDiagnostics()
        {
            string tempRoot = CreateTempDirectory();
            try
            {
                string pmxPath = Path.Combine(tempRoot, "model.pmx");
                File.WriteAllBytes(pmxPath, new byte[] { 0x50, 0x4d, 0x58 });

                MmdRuntimeTextureResolution resolution = ResolveSingleTexture(pmxPath, @"textures\missing.png");
                MmdTextureReferenceDiagnostic diagnostic = resolution.Diagnostics.TextureReferences.Single();

                Assert.That(diagnostic.reference, Is.EqualTo(@"textures\missing.png"));
                Assert.That(diagnostic.resolvedPath, Is.EqualTo("textures/missing.png"));
                Assert.That(diagnostic.status, Is.EqualTo("missing"));
                Assert.That(diagnostic.reason, Is.EqualTo("file not found"));
                AssertDiagnosticsDoNotContain(resolution.Diagnostics, tempRoot, pmxPath);
            }
            finally
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }

        private static MmdRuntimeTextureResolution ResolveSingleTexture(string pmxPath, string textureReference)
        {
            MmdModelDefinition model = CreateMinimalTriangleModel();
            model.materials[0].texture = textureReference;
            return MmdRuntimeTextureResolver.ResolveDiffuseTextures(
                MmdRenderingDescriptorBuilder.Build(model),
                MmdUnityModelSourceContext.FromOptionalPath(pmxPath));
        }

        private static void AssertRejectedAndRedacted(
            MmdTextureBindingDiagnostics diagnostics,
            params string[] secrets)
        {
            Assert.That(diagnostics.UnsupportedTextureReferenceCount, Is.EqualTo(1));
            MmdTextureReferenceDiagnostic diagnostic = diagnostics.TextureReferences.Single();
            Assert.That(diagnostic.reference, Is.EqualTo("<redacted-path>"));
            Assert.That(diagnostic.resolvedPath, Is.Empty);
            Assert.That(diagnostic.status, Is.EqualTo("unsupported"));
            Assert.That(diagnostic.reason, Does.Contain("explicitly allowed external root"));
            AssertDiagnosticsDoNotContain(diagnostics, secrets);
        }

        private static void AssertDiagnosticsDoNotContain(
            MmdTextureBindingDiagnostics diagnostics,
            params string[] secrets)
        {
            string[] diagnosticText = diagnostics.Messages
                .Concat(diagnostics.TextureReferences.SelectMany(diagnostic => new[]
                {
                    diagnostic.reference,
                    diagnostic.resolvedPath,
                    diagnostic.reason
                }))
                .ToArray();
            foreach (string secret in secrets.Where(secret => !string.IsNullOrEmpty(secret)))
            {
                foreach (string text in diagnosticText)
                {
                    Assert.That(text, Does.Not.Contain(secret).IgnoreCase);
                }
            }
        }

        private static MmdModelDefinition CreateMinimalTriangleModel()
        {
            var model = new MmdModelDefinition
            {
                name = "minimal-texture-contract-triangle"
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
                name = "texture-contract-material",
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
            string path = Path.Combine(Path.GetTempPath(), "mmd-unity-texture-contract-" + Path.GetRandomFileName());
            Directory.CreateDirectory(path);
            return path;
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

            return material.HasProperty("_MainTex") ? material.GetTexture("_MainTex") : null;
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

            if (instance.Mesh != null)
            {
                Object.DestroyImmediate(instance.Mesh);
            }

            foreach (Material material in instance.Materials.Where(material => material != null).Distinct())
            {
                Object.DestroyImmediate(material);
            }

            foreach (Texture2D texture in instance.OwnedTextures.Where(texture => texture != null).Distinct())
            {
                Object.DestroyImmediate(texture);
            }
        }
    }
}
