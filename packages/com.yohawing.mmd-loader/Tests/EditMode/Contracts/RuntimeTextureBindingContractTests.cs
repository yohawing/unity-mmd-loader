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
                MmdTestInstanceScope.DestroyInstance(instance);
                Directory.Delete(tempRoot, recursive: true);
            }
        }

        [Test]
        public void RelativeTexturePathUsesOperatingSystemCasePolicy()
        {
            string tempRoot = CreateTempDirectory();
            MmdRuntimeTextureResolution? resolution = null;
            try
            {
                string pmxPath = Path.Combine(tempRoot, "model.pmx");
                string textureDirectory = Path.Combine(tempRoot, "Textures");
                Directory.CreateDirectory(textureDirectory);
                File.WriteAllBytes(pmxPath, new byte[] { 0x50, 0x4d, 0x58 });
                WritePng(Path.Combine(textureDirectory, "Diffuse.PNG"), Color.red);

                MmdModelDefinition model = CreateMinimalTriangleModel();
                model.materials[0].texture = @"textures\diffuse.png";
                resolution = MmdRuntimeTextureResolver.ResolveDiffuseTextures(
                    MmdRenderingDescriptorBuilder.Build(model),
                    MmdUnityModelSourceContext.FromOptionalPath(pmxPath));

                if (Path.DirectorySeparatorChar == '\\')
                {
                    Assert.That(resolution.DiffuseTextures, Has.Count.EqualTo(1));
                    Assert.That(resolution.Diagnostics.MissingTextureReferenceCount, Is.Zero);
                }
                else
                {
                    Assert.That(resolution.DiffuseTextures, Is.Empty);
                    Assert.That(resolution.Diagnostics.MissingTextureReferenceCount, Is.EqualTo(1));
                }
            }
            finally
            {
                if (resolution != null)
                {
                    foreach (Texture2D texture in resolution.DiffuseTextures.Select(item => item.Texture).Distinct())
                    {
                        Object.DestroyImmediate(texture);
                    }
                }
                Directory.Delete(tempRoot, recursive: true);
            }
        }

        [Test]
        public void RepeatedDiffusePathSharesDecodeAndOwnedTexture()
        {
            MmdUnityModelInstance? instance = null;
            string tempRoot = CreateTempDirectory();
            try
            {
                string pmxPath = Path.Combine(tempRoot, "model.pmx");
                File.WriteAllBytes(pmxPath, new byte[] { 0x50, 0x4d, 0x58 });
                WritePng(Path.Combine(tempRoot, "shared.png"), Color.red);

                MmdModelDefinition model = CreateMinimalTriangleModel();
                model.indices.AddRange(new[] { 0, 1, 2 });
                model.materials[0].texture = "shared.png";
                model.materials.Add(new MmdMaterialDefinition
                {
                    index = 1,
                    name = "shared-texture-material",
                    texture = "shared.png",
                    vertexCount = 3
                });

                instance = MmdUnityModelFactory.CreateStaticModel(model, pmxPath);

                Assert.That(instance.LoadedDiffuseTextureCount, Is.EqualTo(2));
                Assert.That(instance.TextureDiagnostics.TextureReferences, Has.Count.EqualTo(2));
                Assert.That(instance.OwnedTextures, Has.Length.EqualTo(1));
                Assert.That(ReadBoundDiffuseTexture(instance.Materials[0]), Is.SameAs(instance.OwnedTextures[0]));
                Assert.That(ReadBoundDiffuseTexture(instance.Materials[1]), Is.SameAs(instance.OwnedTextures[0]));
            }
            finally
            {
                MmdTestInstanceScope.DestroyInstance(instance);
                Directory.Delete(tempRoot, recursive: true);
            }
        }

        [Test]
        public void SamePathWithDifferentUsageKeepsSeparateTextures()
        {
            string tempRoot = CreateTempDirectory();
            MmdRuntimeTextureResolution? resolution = null;
            try
            {
                string pmxPath = Path.Combine(tempRoot, "model.pmx");
                File.WriteAllBytes(pmxPath, new byte[] { 0x50, 0x4d, 0x58 });
                WritePng(Path.Combine(tempRoot, "shared.png"), Color.blue);
                MmdModelDefinition model = CreateMinimalTriangleModel();
                model.materials[0].texture = "shared.png";
                model.materials[0].sphereTexture = "shared.png";

                resolution = MmdRuntimeTextureResolver.ResolveDiffuseTextures(
                    MmdRenderingDescriptorBuilder.Build(model),
                    MmdUnityModelSourceContext.FromOptionalPath(pmxPath));

                Assert.That(resolution.DiffuseTextures, Has.Count.EqualTo(1));
                Assert.That(resolution.SphereTextures, Has.Count.EqualTo(1));
                Assert.That(resolution.DiffuseTextures[0].Texture,
                    Is.Not.SameAs(resolution.SphereTextures[0].Texture));
            }
            finally
            {
                if (resolution != null)
                {
                    foreach (Texture2D texture in resolution.DiffuseTextures.Select(item => item.Texture)
                                 .Concat(resolution.SphereTextures.Select(item => item.Texture)).Distinct())
                    {
                        Object.DestroyImmediate(texture);
                    }
                }
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

        [TestCase(false)]
        [TestCase(true)]
        public void CorruptPngImageDataIsContainedAsTextureDiagnosticWithoutLeakingTextures(bool skinned)
        {
            string tempRoot = CreateTempDirectory();
            MmdUnityModelInstance? instance = null;
            try
            {
                string pmxPath = Path.Combine(tempRoot, "model.pmx");
                File.WriteAllBytes(pmxPath, new byte[] { 0x50, 0x4d, 0x58 });
                WritePng(Path.Combine(tempRoot, "valid.png"), Color.red);
                File.WriteAllBytes(Path.Combine(tempRoot, "corrupt.png"), CreateCorruptIdatPng());
                MmdModelDefinition model = CreateTwoMaterialModel("valid.png", "corrupt.png");
                int textureCountBefore = CountLoadedTexture2D();

                instance = skinned
                    ? MmdUnityModelFactory.CreateSkinnedModel(model, pmxPath)
                    : MmdUnityModelFactory.CreateStaticModel(model, pmxPath);

                Assert.That(instance.LoadedDiffuseTextureCount, Is.EqualTo(1));
                Assert.That(instance.UnsupportedTextureReferenceCount, Is.EqualTo(1));
                MmdTextureReferenceDiagnostic failed = instance.TextureDiagnostics.TextureReferences
                    .Single(diagnostic => diagnostic.materialIndex == 1);
                Assert.That(failed.status, Is.EqualTo("unsupported"));
                Assert.That(failed.reason, Is.EqualTo("decode failed"));
                Assert.That(failed.resolvedPath, Is.EqualTo("corrupt.png"));
                AssertDiagnosticsDoNotContain(instance.TextureDiagnostics, tempRoot, pmxPath);

                MmdTestInstanceScope.DestroyInstance(instance);
                instance = null;
                Assert.That(CountLoadedTexture2D(), Is.EqualTo(textureCountBefore));
            }
            finally
            {
                MmdTestInstanceScope.DestroyInstance(instance);
                Directory.Delete(tempRoot, recursive: true);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void LockedTextureFileIsContainedAsUnreadableDiagnosticWithoutLeakingTextures(bool skinned)
        {
            if (Path.DirectorySeparatorChar != '\\')
            {
                Assert.Ignore("FileShare.None is only enforced as a mandatory lock on Windows.");
            }

            string tempRoot = CreateTempDirectory();
            MmdUnityModelInstance? instance = null;
            try
            {
                string pmxPath = Path.Combine(tempRoot, "model.pmx");
                string lockedPath = Path.Combine(tempRoot, "locked.png");
                File.WriteAllBytes(pmxPath, new byte[] { 0x50, 0x4d, 0x58 });
                WritePng(Path.Combine(tempRoot, "valid.png"), Color.red);
                WritePng(lockedPath, Color.blue);
                MmdModelDefinition model = CreateTwoMaterialModel("valid.png", "locked.png");
                int textureCountBefore = CountLoadedTexture2D();

                using (new FileStream(lockedPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                    instance = skinned
                        ? MmdUnityModelFactory.CreateSkinnedModel(model, pmxPath)
                        : MmdUnityModelFactory.CreateStaticModel(model, pmxPath);
                }

                Assert.That(instance.LoadedDiffuseTextureCount, Is.EqualTo(1));
                Assert.That(instance.UnsupportedTextureReferenceCount, Is.EqualTo(1));
                MmdTextureReferenceDiagnostic failed = instance.TextureDiagnostics.TextureReferences
                    .Single(diagnostic => diagnostic.materialIndex == 1);
                Assert.That(failed.status, Is.EqualTo("unreadable"));
                Assert.That(failed.reason, Does.StartWith("read failed"));
                Assert.That(failed.resolvedPath, Is.EqualTo("locked.png"));
                Assert.That(
                    instance.TextureDiagnostics.Messages.Any(message => message.Contains("could not be read")),
                    Is.True);
                AssertDiagnosticsDoNotContain(instance.TextureDiagnostics, tempRoot, pmxPath, lockedPath);

                MmdTestInstanceScope.DestroyInstance(instance);
                instance = null;
                Assert.That(CountLoadedTexture2D(), Is.EqualTo(textureCountBefore));
            }
            finally
            {
                MmdTestInstanceScope.DestroyInstance(instance);
                Directory.Delete(tempRoot, recursive: true);
            }
        }

        [Test]
        public void ResolverDestroysAlreadyDecodedTexturesWhenAnUncontainedFailureEscapes()
        {
            string tempRoot = CreateTempDirectory();
            try
            {
                string pmxPath = Path.Combine(tempRoot, "model.pmx");
                File.WriteAllBytes(pmxPath, new byte[] { 0x50, 0x4d, 0x58 });
                WritePng(Path.Combine(tempRoot, "valid.png"), Color.red);
                MmdModelDefinition model = CreateMinimalTriangleModel();
                model.materials[0].texture = "valid.png";
                MmdRenderingDescriptor descriptor = MmdRenderingDescriptorBuilder.Build(model);
                descriptor.materials.Add(null!);
                int textureCountBefore = CountLoadedTexture2D();

                Assert.Throws<System.ArgumentException>(() => MmdRuntimeTextureResolver.ResolveDiffuseTextures(
                    descriptor,
                    MmdUnityModelSourceContext.FromOptionalPath(pmxPath)));

                Assert.That(CountLoadedTexture2D(), Is.EqualTo(textureCountBefore));
            }
            finally
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }

        [TestCase("sphere.sph", "png")]
        [TestCase("sphere.spa", "jpg")]
        [TestCase("sphere.bmp", "png")]
        [TestCase("sphere.png", "bmp")]
        public void SphereTextureIsDecodedByContentSignatureRatherThanExtension(string fileName, string contentFormat)
        {
            string tempRoot = CreateTempDirectory();
            MmdRuntimeTextureResolution? resolution = null;
            try
            {
                string pmxPath = Path.Combine(tempRoot, "model.pmx");
                File.WriteAllBytes(pmxPath, new byte[] { 0x50, 0x4d, 0x58 });
                File.WriteAllBytes(Path.Combine(tempRoot, fileName), EncodeSolidTexture(contentFormat, Color.green));
                MmdModelDefinition model = CreateMinimalTriangleModel();
                model.materials[0].sphereTexture = fileName;

                resolution = MmdRuntimeTextureResolver.ResolveDiffuseTextures(
                    MmdRenderingDescriptorBuilder.Build(model),
                    MmdUnityModelSourceContext.FromOptionalPath(pmxPath));

                Assert.That(resolution.SphereTextures, Has.Count.EqualTo(1));
                Assert.That(resolution.Diagnostics.UnsupportedTextureReferenceCount, Is.Zero);
                MmdTextureReferenceDiagnostic diagnostic = resolution.Diagnostics.TextureReferences.Single();
                Assert.That(diagnostic.status, Is.EqualTo("loaded"));
                Assert.That(resolution.SphereTextures[0].Texture.width, Is.EqualTo(2));
            }
            finally
            {
                if (resolution != null)
                {
                    foreach (Texture2D texture in resolution.SphereTextures.Select(item => item.Texture).Distinct())
                    {
                        Object.DestroyImmediate(texture);
                    }
                }

                Directory.Delete(tempRoot, recursive: true);
            }
        }

        [Test]
        public void CaseVariantReferencesShareOneDecodedTexture()
        {
            // The decode cache key is case-insensitive on every platform so case-insensitive
            // volumes (Windows, default macOS) never decode the same file twice.
            string tempRoot = CreateTempDirectory();
            MmdRuntimeTextureResolution? resolution = null;
            try
            {
                string pmxPath = Path.Combine(tempRoot, "model.pmx");
                File.WriteAllBytes(pmxPath, new byte[] { 0x50, 0x4d, 0x58 });
                WritePng(Path.Combine(tempRoot, "shared.png"), Color.red);
                if (!File.Exists(Path.Combine(tempRoot, "SHARED.png")))
                {
                    WritePng(Path.Combine(tempRoot, "SHARED.png"), Color.red);
                }

                MmdModelDefinition model = CreateTwoMaterialModel("shared.png", "SHARED.png");
                resolution = MmdRuntimeTextureResolver.ResolveDiffuseTextures(
                    MmdRenderingDescriptorBuilder.Build(model),
                    MmdUnityModelSourceContext.FromOptionalPath(pmxPath));

                Assert.That(resolution.DiffuseTextures, Has.Count.EqualTo(2));
                Assert.That(resolution.DiffuseTextures[1].Texture, Is.SameAs(resolution.DiffuseTextures[0].Texture));
            }
            finally
            {
                if (resolution != null)
                {
                    foreach (Texture2D texture in resolution.DiffuseTextures.Select(item => item.Texture).Distinct())
                    {
                        Object.DestroyImmediate(texture);
                    }
                }

                Directory.Delete(tempRoot, recursive: true);
            }
        }

        private static int CountLoadedTexture2D()
        {
            return Resources.FindObjectsOfTypeAll<Texture2D>().Length;
        }

        private static MmdModelDefinition CreateTwoMaterialModel(string firstTexture, string secondTexture)
        {
            MmdModelDefinition model = CreateMinimalTriangleModel();
            model.indices.AddRange(new[] { 0, 1, 2 });
            model.materials[0].texture = firstTexture;
            model.materials.Add(new MmdMaterialDefinition
            {
                index = 1,
                name = "second-texture-material",
                texture = secondTexture,
                vertexCount = 3
            });
            return model;
        }

        private static byte[] CreateCorruptIdatPng()
        {
            using var stream = new MemoryStream();
            stream.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, 0, 8);
            WritePngChunk(stream, "IHDR", new byte[] { 0, 0, 0, 1, 0, 0, 0, 1, 8, 6, 0, 0, 0 });
            // zlib header followed by a deflate block with the reserved BTYPE=11, then a dummy Adler-32.
            WritePngChunk(stream, "IDAT", new byte[] { 0x78, 0x01, 0xff, 0xff, 0xff, 0xff, 0, 0, 0, 0 });
            WritePngChunk(stream, "IEND", System.Array.Empty<byte>());
            return stream.ToArray();
        }

        private static void WritePngChunk(Stream stream, string type, byte[] data)
        {
            stream.WriteByte((byte)(data.Length >> 24));
            stream.WriteByte((byte)(data.Length >> 16));
            stream.WriteByte((byte)(data.Length >> 8));
            stream.WriteByte((byte)data.Length);
            byte[] typeBytes = System.Text.Encoding.ASCII.GetBytes(type);
            stream.Write(typeBytes, 0, typeBytes.Length);
            stream.Write(data, 0, data.Length);
            stream.Write(new byte[4], 0, 4);
        }

        private static byte[] EncodeSolidTexture(string format, Color color)
        {
            if (format == "bmp")
            {
                // 2x2 24bpp bottom-up BMP; each row is 6 bytes padded to 8.
                var bytes = new byte[54 + 16];
                bytes[0] = (byte)'B';
                bytes[1] = (byte)'M';
                WriteLittleEndian(bytes, 2, bytes.Length);
                WriteLittleEndian(bytes, 10, 54);
                WriteLittleEndian(bytes, 14, 40);
                WriteLittleEndian(bytes, 18, 2);
                WriteLittleEndian(bytes, 22, 2);
                bytes[26] = 1;
                bytes[28] = 24;
                WriteLittleEndian(bytes, 34, 16);
                Color32 pixel = color;
                for (int row = 0; row < 2; row++)
                {
                    for (int x = 0; x < 2; x++)
                    {
                        int offset = 54 + row * 8 + x * 3;
                        bytes[offset] = pixel.b;
                        bytes[offset + 1] = pixel.g;
                        bytes[offset + 2] = pixel.r;
                    }
                }

                return bytes;
            }

            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false);
            try
            {
                texture.SetPixels(new[] { color, color, color, color });
                texture.Apply();
                return format == "jpg" ? texture.EncodeToJPG() : texture.EncodeToPNG();
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }

        private static void WriteLittleEndian(byte[] bytes, int offset, int value)
        {
            bytes[offset] = (byte)value;
            bytes[offset + 1] = (byte)(value >> 8);
            bytes[offset + 2] = (byte)(value >> 16);
            bytes[offset + 3] = (byte)(value >> 24);
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
            model.vertices.Add(MmdTestFixtures.CreateSyntheticVertex(0, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f));
            model.vertices.Add(MmdTestFixtures.CreateSyntheticVertex(1, 1.0f, 0.0f, 0.0f, 1.0f, 0.0f));
            model.vertices.Add(MmdTestFixtures.CreateSyntheticVertex(2, 0.0f, 1.0f, 0.0f, 0.0f, 1.0f));
            model.indices.AddRange(new[] { 0, 1, 2 });
            model.materials.Add(new MmdMaterialDefinition
            {
                index = 0,
                name = "texture-contract-material",
                vertexCount = 3
            });
            return model;
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
    }
}
