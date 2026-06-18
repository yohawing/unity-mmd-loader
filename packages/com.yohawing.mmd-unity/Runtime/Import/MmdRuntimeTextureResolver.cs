#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Yohawing.MmdUnity.Rendering;

namespace Yohawing.MmdUnity.UnityIntegration
{
    public sealed class MmdTextureBindingDiagnostics
    {
        private readonly List<string> messages = new();
        private readonly List<MmdTextureReferenceDiagnostic> textureReferences = new();

        public IReadOnlyList<string> Messages => messages;

        public IReadOnlyList<MmdTextureReferenceDiagnostic> TextureReferences => textureReferences;

        public int LoadedDiffuseTextureCount { get; internal set; }

        public int LoadedSphereTextureCount { get; internal set; }

        public int LoadedToonTextureCount { get; internal set; }

        public int MissingTextureReferenceCount { get; internal set; }

        public int UnsupportedTextureReferenceCount { get; internal set; }

        public int SkippedSphereTextureReferenceCount { get; internal set; }

        public int SkippedToonTextureReferenceCount { get; internal set; }

        public int SkippedTextureReferenceCount =>
            MissingTextureReferenceCount
            + UnsupportedTextureReferenceCount
            + SkippedSphereTextureReferenceCount
            + SkippedToonTextureReferenceCount;

        internal void AddMessage(string message)
        {
            if (!string.IsNullOrWhiteSpace(message))
            {
                messages.Add(message);
            }
        }

        internal void AddTextureReference(MmdTextureReferenceDiagnostic diagnostic)
        {
            textureReferences.Add(diagnostic ?? throw new ArgumentNullException(nameof(diagnostic)));
        }
    }

    [Serializable]
    public sealed class MmdTextureReferenceDiagnostic
    {
        public int materialIndex;
        public string usage = string.Empty;
        public string reference = string.Empty;
        public string resolvedPath = string.Empty;
        public string status = string.Empty;
        public string reason = string.Empty;
    }

    public sealed class MmdResolvedTexture
    {
        internal MmdResolvedTexture(
            int materialIndex,
            MmdTextureUsage usage,
            string reference,
            string resolvedPath,
            Texture2D texture)
        {
            MaterialIndex = materialIndex;
            Usage = usage;
            Reference = reference;
            ResolvedPath = resolvedPath;
            Texture = texture;
        }

        public int MaterialIndex { get; }

        public MmdTextureUsage Usage { get; }

        public string Reference { get; }

        public string ResolvedPath { get; }

        public Texture2D Texture { get; }
    }

    public sealed class MmdRuntimeTextureResolution
    {
        internal MmdRuntimeTextureResolution(
            IReadOnlyList<MmdResolvedTexture> diffuseTextures,
            IReadOnlyList<MmdResolvedTexture> sphereTextures,
            IReadOnlyList<MmdResolvedTexture> toonTextures,
            MmdTextureBindingDiagnostics diagnostics)
        {
            DiffuseTextures = diffuseTextures;
            SphereTextures = sphereTextures;
            ToonTextures = toonTextures;
            Diagnostics = diagnostics;
        }

        public IReadOnlyList<MmdResolvedTexture> DiffuseTextures { get; }

        public IReadOnlyList<MmdResolvedTexture> SphereTextures { get; }

        public IReadOnlyList<MmdResolvedTexture> ToonTextures { get; }

        public MmdTextureBindingDiagnostics Diagnostics { get; }
    }

    public enum MmdTextureUsage
    {
        Diffuse,
        Sphere,
        Toon
    }

    public static class MmdRuntimeTextureResolver
    {
        private static readonly HashSet<string> SupportedRuntimeImageExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".png",
            ".jpg",
            ".jpeg",
            ".bmp",
            ".tga",
            ".dds",
            ".spa",
            ".sph"
        };

        public static MmdRuntimeTextureResolution ResolveDiffuseTextures(
            MmdRenderingDescriptor descriptor,
            MmdUnityModelSourceContext? sourceContext)
        {
            if (descriptor == null)
            {
                throw new ArgumentNullException(nameof(descriptor));
            }

            if (descriptor.materials == null)
            {
                throw new ArgumentException("Rendering descriptor materials are required.", nameof(descriptor));
            }

            var diagnostics = new MmdTextureBindingDiagnostics();
            var diffuseTextures = new List<MmdResolvedTexture>(descriptor.materials.Count);
            var sphereTextures = new List<MmdResolvedTexture>(descriptor.materials.Count);
            var toonTextures = new List<MmdResolvedTexture>(descriptor.materials.Count);

            foreach (MmdMaterialDescriptor material in descriptor.materials)
            {
                if (material == null)
                {
                    throw new ArgumentException("Rendering descriptor materials cannot contain null entries.", nameof(descriptor));
                }

                MmdResolvedTexture? diffuseTexture = TryLoadTexture(
                    material.materialIndex,
                    MmdTextureUsage.Diffuse,
                    material.texture,
                    sourceContext,
                    diagnostics);
                if (diffuseTexture != null)
                {
                    diffuseTextures.Add(diffuseTexture);
                    diagnostics.LoadedDiffuseTextureCount++;
                }

                LoadLaterPhaseTexture(
                    material.materialIndex,
                    MmdTextureUsage.Sphere,
                    material.sphereTexture,
                    sourceContext,
                    sphereTextures,
                    diagnostics);
                int toonCountBeforeLoad = toonTextures.Count;
                LoadLaterPhaseTexture(
                    material.materialIndex,
                    MmdTextureUsage.Toon,
                    material.toonTexture,
                    sourceContext,
                    toonTextures,
                    diagnostics);

                // MMD shared toon (toon01..toon10) materials carry only an index, not a texture
                // path, so the path-based load above never resolves them. Substitute the built-in
                // GoldenOracle ramp so shared-toon materials shade through the toon ramp instead of
                // falling back to flat lighting. This is gated on a real source context: the
                // importer cache path (sourceContext == null) intentionally resolves no runtime
                // textures and binds the shared toon as a persisted sub-asset separately.
                if (sourceContext != null &&
                    toonTextures.Count == toonCountBeforeLoad &&
                    material.toonShared &&
                    MmdSharedToonTextures.IsSharedToonIndex(material.sharedToonIndex))
                {
                    AddSharedToonTexture(material.materialIndex, material.sharedToonIndex, toonTextures, diagnostics);
                }
            }

            return new MmdRuntimeTextureResolution(diffuseTextures, sphereTextures, toonTextures, diagnostics);
        }

        private static void AddSharedToonTexture(
            int materialIndex,
            int sharedToonIndex,
            List<MmdResolvedTexture> destination,
            MmdTextureBindingDiagnostics diagnostics)
        {
            Texture2D? texture = MmdSharedToonTextures.TryCreateSharedToonTexture(sharedToonIndex);
            if (texture == null)
            {
                return;
            }

            texture.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
            string reference = $"toon{sharedToonIndex + 1:00}.bmp";
            destination.Add(new MmdResolvedTexture(materialIndex, MmdTextureUsage.Toon, reference, reference, texture));
            diagnostics.LoadedToonTextureCount++;
            diagnostics.AddTextureReference(CreateReferenceDiagnostic(
                materialIndex,
                MmdTextureUsage.Toon,
                reference,
                resolvedPath: reference,
                status: "loaded",
                reason: "built-in MMD shared toon"));
            diagnostics.AddMessage($"Material {materialIndex} shared toon '{reference}' resolved from the built-in MMD toon ramps.");
        }

        private static void LoadLaterPhaseTexture(
            int materialIndex,
            MmdTextureUsage usage,
            string? textureReference,
            MmdUnityModelSourceContext? sourceContext,
            List<MmdResolvedTexture> destination,
            MmdTextureBindingDiagnostics diagnostics)
        {
            string normalizedReference = NormalizeTextureReference(textureReference);
            if (normalizedReference.Length == 0)
            {
                return;
            }

            if (usage == MmdTextureUsage.Sphere)
            {
                diagnostics.SkippedSphereTextureReferenceCount++;
            }
            else if (usage == MmdTextureUsage.Toon)
            {
                diagnostics.SkippedToonTextureReferenceCount++;
            }

            MmdResolvedTexture? resolvedTexture = TryLoadTexture(
                materialIndex,
                usage,
                normalizedReference,
                sourceContext,
                diagnostics);
            if (resolvedTexture == null)
            {
                return;
            }

            destination.Add(resolvedTexture);
            if (usage == MmdTextureUsage.Sphere)
            {
                diagnostics.LoadedSphereTextureCount++;
                diagnostics.AddMessage($"Material {materialIndex} sphere texture '{normalizedReference}' loaded for diagnostics; sphere shading is skipped in this phase.");
            }
            else if (usage == MmdTextureUsage.Toon)
            {
                diagnostics.LoadedToonTextureCount++;
                diagnostics.AddMessage($"Material {materialIndex} toon texture '{normalizedReference}' loaded for diagnostics; toon shading is skipped in this phase.");
            }
        }

        private static MmdResolvedTexture? TryLoadTexture(
            int materialIndex,
            MmdTextureUsage usage,
            string? rawTextureReference,
            MmdUnityModelSourceContext? sourceContext,
            MmdTextureBindingDiagnostics diagnostics)
        {
            string textureReference = NormalizeTextureReference(rawTextureReference);
            if (textureReference.Length == 0)
            {
                return null;
            }

            string usageLabel = GetUsageLabel(usage);
            if (sourceContext == null)
            {
                diagnostics.UnsupportedTextureReferenceCount++;
                string reason = "no PMX source path is available";
                diagnostics.AddTextureReference(CreateReferenceDiagnostic(
                    materialIndex,
                    usage,
                    textureReference,
                    resolvedPath: string.Empty,
                    status: "unsupported",
                    reason: reason));
                diagnostics.AddMessage($"Material {materialIndex} {usageLabel} texture '{textureReference}' skipped because {reason}.");
                return null;
            }

            if (!TryResolveLocalTexturePath(sourceContext, textureReference, out string resolvedPath, out string failure))
            {
                diagnostics.UnsupportedTextureReferenceCount++;
                diagnostics.AddTextureReference(CreateReferenceDiagnostic(
                    materialIndex,
                    usage,
                    textureReference,
                    resolvedPath,
                    status: "unsupported",
                    reason: failure));
                diagnostics.AddMessage($"Material {materialIndex} {usageLabel} texture '{textureReference}' skipped: {failure}");
                return null;
            }

            string extension = Path.GetExtension(resolvedPath);
            if (!SupportedRuntimeImageExtensions.Contains(extension))
            {
                diagnostics.UnsupportedTextureReferenceCount++;
                string reason = $"unsupported extension '{extension}'";
                diagnostics.AddTextureReference(CreateReferenceDiagnostic(
                    materialIndex,
                    usage,
                    textureReference,
                    resolvedPath,
                    status: "unsupported",
                    reason: reason));
                diagnostics.AddMessage($"Material {materialIndex} {usageLabel} texture '{textureReference}' has {reason}.");
                return null;
            }

            if (!File.Exists(resolvedPath))
            {
                diagnostics.MissingTextureReferenceCount++;
                diagnostics.AddTextureReference(CreateReferenceDiagnostic(
                    materialIndex,
                    usage,
                    textureReference,
                    resolvedPath,
                    status: "missing",
                    reason: "file not found"));
                diagnostics.AddMessage($"Material {materialIndex} {usageLabel} texture file was not found: {resolvedPath}");
                return null;
            }

            byte[] bytes = File.ReadAllBytes(resolvedPath);
            Texture2D? texture;
            try
            {
                texture = LoadTextureBytes(bytes, extension, Path.GetFileNameWithoutExtension(resolvedPath));
            }
            catch (Exception ex) when (ex is ArgumentException || ex is NotSupportedException)
            {
                diagnostics.UnsupportedTextureReferenceCount++;
                diagnostics.AddTextureReference(CreateReferenceDiagnostic(
                    materialIndex,
                    usage,
                    textureReference,
                    resolvedPath,
                    status: "unsupported",
                    reason: "decode failed: " + ex.Message));
                diagnostics.AddMessage($"Material {materialIndex} {usageLabel} texture could not be decoded at runtime: {resolvedPath}; {ex.Message}");
                return null;
            }

            if (texture == null)
            {
                diagnostics.UnsupportedTextureReferenceCount++;
                diagnostics.AddTextureReference(CreateReferenceDiagnostic(
                    materialIndex,
                    usage,
                    textureReference,
                    resolvedPath,
                    status: "unsupported",
                    reason: "decode returned null"));
                diagnostics.AddMessage($"Material {materialIndex} {usageLabel} texture could not be decoded at runtime: {resolvedPath}");
                return null;
            }

            texture.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
            diagnostics.AddTextureReference(CreateReferenceDiagnostic(
                materialIndex,
                usage,
                textureReference,
                resolvedPath,
                status: "loaded",
                reason: string.Empty));
            return new MmdResolvedTexture(materialIndex, usage, textureReference, resolvedPath, texture);
        }

        private static MmdTextureReferenceDiagnostic CreateReferenceDiagnostic(
            int materialIndex,
            MmdTextureUsage usage,
            string reference,
            string resolvedPath,
            string status,
            string reason)
        {
            return new MmdTextureReferenceDiagnostic
            {
                materialIndex = materialIndex,
                usage = GetUsageLabel(usage),
                reference = reference,
                resolvedPath = resolvedPath,
                status = status,
                reason = reason
            };
        }

        internal static Texture2D? DecodeTextureBytes(byte[] bytes, string extension, string textureName)
        {
            if (bytes == null || bytes.Length == 0) return null;
            try { return LoadTextureBytes(bytes, extension, textureName); }
            catch (System.Exception ex) when (ex is System.ArgumentException || ex is System.NotSupportedException) { return null; }
        }

        private static Texture2D? LoadTextureBytes(byte[] bytes, string extension, string textureName)
        {
            if (IsBmp(bytes))
            {
                return MmdBmpDecoder.Decode(bytes, textureName);
            }

            if (string.Equals(extension, ".tga", StringComparison.OrdinalIgnoreCase))
            {
                return MmdTgaDecoder.Decode(bytes, textureName);
            }

            if (string.Equals(extension, ".png", StringComparison.OrdinalIgnoreCase))
            {
                return MmdPngDecoder.Decode(bytes, textureName);
            }

            if (string.Equals(extension, ".dds", StringComparison.OrdinalIgnoreCase))
            {
                return MmdDdsDecoder.Decode(bytes, textureName);
            }

            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: true)
            {
                name = textureName
            };

            if (texture.LoadImage(bytes, markNonReadable: false))
            {
                return texture;
            }

            UnityEngine.Object.DestroyImmediate(texture);
            return null;
        }

        private static bool IsBmp(byte[] bytes)
        {
            return bytes.Length >= 2 && bytes[0] == (byte)'B' && bytes[1] == (byte)'M';
        }

        private static string NormalizeTextureReference(string? textureReference)
        {
            return string.IsNullOrWhiteSpace(textureReference) ? string.Empty : textureReference.Trim();
        }

        private static string GetUsageLabel(MmdTextureUsage usage)
        {
            return usage switch
            {
                MmdTextureUsage.Diffuse => "diffuse",
                MmdTextureUsage.Sphere => "sphere",
                MmdTextureUsage.Toon => "toon",
                _ => "unknown"
            };
        }

        private static bool TryResolveLocalTexturePath(
            MmdUnityModelSourceContext sourceContext,
            string textureReference,
            out string resolvedPath,
            out string failure)
        {
            resolvedPath = string.Empty;
            failure = string.Empty;

            if (Uri.TryCreate(textureReference, UriKind.Absolute, out Uri uri) && !uri.IsFile)
            {
                failure = "only local file texture references are supported";
                return false;
            }

            string candidate = Path.IsPathRooted(textureReference)
                ? textureReference
                : Path.Combine(sourceContext.SourceDirectory, textureReference);
            resolvedPath = Path.GetFullPath(candidate);

            if (!Path.IsPathRooted(textureReference)
                && textureReference.Contains("..")
                && !IsUnderDirectory(resolvedPath, sourceContext.SourceDirectory))
            {
                failure = "relative texture path escapes the PMX source directory";
                return false;
            }

            return true;
        }

        private static bool IsUnderDirectory(string path, string directory)
        {
            string fullPath = Path.GetFullPath(path);
            string fullDirectory = Path.GetFullPath(directory);
            if (!fullDirectory.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
                && !fullDirectory.EndsWith(Path.AltDirectorySeparatorChar.ToString(), StringComparison.Ordinal))
            {
                fullDirectory += Path.DirectorySeparatorChar;
            }

            return fullPath.StartsWith(fullDirectory, StringComparison.OrdinalIgnoreCase);
        }
    }
}
