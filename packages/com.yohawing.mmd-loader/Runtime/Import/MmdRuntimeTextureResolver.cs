#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Security;
using UnityEngine;
using Mmd.Rendering;

namespace Mmd.UnityIntegration
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
            string diagnosticReference = GetDiagnosticReference(textureReference);
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
                    diagnosticReference,
                    resolvedPath: string.Empty,
                    status: "unsupported",
                    reason: reason));
                diagnostics.AddMessage($"Material {materialIndex} {usageLabel} texture '{textureReference}' skipped because {reason}.");
                return null;
            }

            if (!TryResolveLocalTexturePath(
                    sourceContext,
                    textureReference,
                    out string resolvedPath,
                    out string diagnosticPath,
                    out string failure))
            {
                diagnostics.UnsupportedTextureReferenceCount++;
                diagnostics.AddTextureReference(CreateReferenceDiagnostic(
                    materialIndex,
                    usage,
                    diagnosticReference,
                    resolvedPath: string.Empty,
                    status: "unsupported",
                    reason: failure));
                diagnostics.AddMessage($"Material {materialIndex} {usageLabel} texture '{diagnosticReference}' skipped: {failure}");
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
                    diagnosticReference,
                    diagnosticPath,
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
                    diagnosticReference,
                    diagnosticPath,
                    status: "missing",
                    reason: "file not found"));
                diagnostics.AddMessage($"Material {materialIndex} {usageLabel} texture '{diagnosticPath}' was not found.");
                return null;
            }

            Texture2D? texture;
            try
            {
                byte[] bytes = MmdTextureDecodeBudget.Default.ReadFileBytes(resolvedPath);
                texture = LoadTextureBytes(bytes, extension, Path.GetFileNameWithoutExtension(resolvedPath));
            }
            catch (Exception ex) when (ex is ArgumentException || ex is NotSupportedException)
            {
                diagnostics.UnsupportedTextureReferenceCount++;
                diagnostics.AddTextureReference(CreateReferenceDiagnostic(
                    materialIndex,
                    usage,
                    diagnosticReference,
                    diagnosticPath,
                    status: "unsupported",
                    reason: "decode failed"));
                diagnostics.AddMessage($"Material {materialIndex} {usageLabel} texture '{diagnosticPath}' could not be decoded at runtime.");
                return null;
            }

            if (texture == null)
            {
                diagnostics.UnsupportedTextureReferenceCount++;
                diagnostics.AddTextureReference(CreateReferenceDiagnostic(
                    materialIndex,
                    usage,
                    diagnosticReference,
                    diagnosticPath,
                    status: "unsupported",
                    reason: "decode returned null"));
                diagnostics.AddMessage($"Material {materialIndex} {usageLabel} texture '{diagnosticPath}' could not be decoded at runtime.");
                return null;
            }

            texture.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
            diagnostics.AddTextureReference(CreateReferenceDiagnostic(
                materialIndex,
                usage,
                diagnosticReference,
                diagnosticPath,
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

            if (string.Equals(extension, ".jpg", StringComparison.OrdinalIgnoreCase)
                || string.Equals(extension, ".jpeg", StringComparison.OrdinalIgnoreCase))
            {
                MmdJpegHeaderValidator.Validate(bytes, MmdTextureDecodeBudget.Default);
                var jpegTexture = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: true)
                {
                    name = textureName
                };
                if (jpegTexture.LoadImage(bytes, markNonReadable: false))
                {
                    return jpegTexture;
                }

                UnityEngine.Object.DestroyImmediate(jpegTexture);
                return null;
            }

            throw new NotSupportedException($"Texture extension '{extension}' is not supported for runtime decode.");
        }

        private static bool IsBmp(byte[] bytes)
        {
            return bytes.Length >= 2 && bytes[0] == (byte)'B' && bytes[1] == (byte)'M';
        }

        private static string NormalizeTextureReference(string? textureReference)
        {
            return string.IsNullOrWhiteSpace(textureReference) ? string.Empty : textureReference.Trim();
        }

        private static string GetDiagnosticReference(string textureReference)
        {
            return IsAbsoluteOrUriReference(textureReference)
                ? "<redacted-path>"
                : EscapeDiagnosticText(textureReference);
        }

        private static string EscapeDiagnosticText(string value)
        {
            const int maxLength = 256;
            var characters = new List<char>(Math.Min(value.Length, maxLength));
            foreach (char character in value)
            {
                if (characters.Count >= maxLength)
                {
                    break;
                }

                characters.Add(char.IsControl(character) ? '?' : character);
            }

            return new string(characters.ToArray());
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
            out string diagnosticPath,
            out string failure)
        {
            resolvedPath = string.Empty;
            diagnosticPath = string.Empty;
            failure = string.Empty;

            if (IsAbsoluteOrUriReference(textureReference))
            {
                failure = "absolute, UNC, device, and URI texture references require an explicitly allowed external root";
                return false;
            }

            if (HasInvalidRelativePathSyntax(textureReference))
            {
                failure = "texture reference is not a valid relative path";
                return false;
            }

            try
            {
                string platformReference = textureReference
                    .Replace('\\', Path.DirectorySeparatorChar)
                    .Replace('/', Path.DirectorySeparatorChar);
                resolvedPath = Path.GetFullPath(Path.Combine(sourceContext.SourceDirectory, platformReference));
            }
            catch (Exception ex) when (ex is ArgumentException
                || ex is NotSupportedException
                || ex is PathTooLongException
                || ex is SecurityException)
            {
                resolvedPath = string.Empty;
                failure = "texture reference is not a valid relative path";
                return false;
            }

            if (!IsUnderDirectory(resolvedPath, sourceContext.SourceDirectory))
            {
                resolvedPath = string.Empty;
                failure = "relative texture path escapes the PMX source directory";
                return false;
            }

            diagnosticPath = Path.GetRelativePath(sourceContext.SourceDirectory, resolvedPath).Replace('\\', '/');
            if (ContainsReparsePoint(sourceContext.SourceDirectory, resolvedPath))
            {
                resolvedPath = string.Empty;
                diagnosticPath = string.Empty;
                failure = "texture reference crosses a symbolic link or junction outside the trusted path boundary";
                return false;
            }

            return true;
        }

        private static bool IsAbsoluteOrUriReference(string textureReference)
        {
            if (Path.IsPathRooted(textureReference)
                || textureReference.StartsWith("\\", StringComparison.Ordinal)
                || textureReference.StartsWith("/", StringComparison.Ordinal)
                || textureReference.StartsWith("\\\\", StringComparison.Ordinal)
                || textureReference.StartsWith("//", StringComparison.Ordinal)
                || (textureReference.Length >= 2
                    && char.IsLetter(textureReference[0])
                    && textureReference[1] == ':'))
            {
                return true;
            }

            return Uri.TryCreate(textureReference, UriKind.Absolute, out _);
        }

        private static bool HasInvalidRelativePathSyntax(string textureReference)
        {
            if (textureReference.IndexOf(':') >= 0)
            {
                return true;
            }

            string[] segments = textureReference.Split('\\', '/');
            foreach (string segment in segments)
            {
                if (segment == "." || segment == "..")
                {
                    continue;
                }

                if (segment.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
                    || segment.Length > 0 && (segment[^1] == ' ' || segment[^1] == '.'))
                {
                    return true;
                }

                string baseName = segment.Split('.')[0];
                if (IsReservedDosDeviceName(baseName))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsReservedDosDeviceName(string value)
        {
            if (value.Equals("CON", StringComparison.OrdinalIgnoreCase)
                || value.Equals("PRN", StringComparison.OrdinalIgnoreCase)
                || value.Equals("AUX", StringComparison.OrdinalIgnoreCase)
                || value.Equals("NUL", StringComparison.OrdinalIgnoreCase)
                || value.Equals("CLOCK$", StringComparison.OrdinalIgnoreCase)
                || value.Equals("CONIN$", StringComparison.OrdinalIgnoreCase)
                || value.Equals("CONOUT$", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return value.Length == 4
                && (value.StartsWith("COM", StringComparison.OrdinalIgnoreCase)
                    || value.StartsWith("LPT", StringComparison.OrdinalIgnoreCase))
                && value[3] >= '1'
                && value[3] <= '9';
        }

        private static bool ContainsReparsePoint(string rootDirectory, string path)
        {
            string relativePath = Path.GetRelativePath(rootDirectory, path);
            string current = rootDirectory;
            foreach (string component in relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
            {
                if (component.Length == 0 || component == ".")
                {
                    continue;
                }

                current = Path.Combine(current, component);
                if (!File.Exists(current) && !Directory.Exists(current))
                {
                    break;
                }

                try
                {
                    if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    {
                        return true;
                    }
                }
                catch (Exception ex) when (ex is IOException
                    || ex is UnauthorizedAccessException
                    || ex is SecurityException)
                {
                    return true;
                }
            }

            return false;
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

            StringComparison comparison = Path.DirectorySeparatorChar == '\\'
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
            return fullPath.StartsWith(fullDirectory, comparison);
        }
    }
}
