#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Mmd.Rendering;

namespace Mmd.Samples.RuntimeVerification
{
    public static class MmdRuntimeViewerFixtureManifest
    {
        private const int SupportedSchemaVersion = 1;

        public static MmdRuntimeVerificationCase[] LoadPlaybackCases(
            string manifestPath,
            List<string> errors)
        {
            MmdRuntimeViewerFixtureCase[] viewerCases = LoadViewerCases(
                manifestPath,
                errors,
                includeSkipped: false);
            var playbackCases = new List<MmdRuntimeVerificationCase>(viewerCases.Length);
            for (int i = 0; i < viewerCases.Length; i++)
            {
                playbackCases.Add(new MmdRuntimeVerificationCase(
                    viewerCases[i].Name,
                    viewerCases[i].PmxPath,
                    viewerCases[i].VmdPath,
                    parseOnly: false,
                    skipReason: string.Empty,
                    materialPreset: viewerCases[i].MaterialPreset));
            }

            return playbackCases.ToArray();
        }

        public static MmdRuntimeVerificationCase[] LoadGateCases(
            string manifestPath,
            List<string> errors)
        {
            MmdRuntimeViewerFixtureCase[] viewerCases = LoadViewerCases(
                manifestPath,
                errors,
                includeSkipped: true);
            var gateCases = new List<MmdRuntimeVerificationCase>(viewerCases.Length);
            for (int i = 0; i < viewerCases.Length; i++)
            {
                gateCases.Add(new MmdRuntimeVerificationCase(
                    viewerCases[i].Name,
                    viewerCases[i].PmxPath,
                    viewerCases[i].VmdPath,
                    parseOnly: viewerCases[i].ParseOnly,
                    skipReason: viewerCases[i].SkipReason,
                    materialPreset: viewerCases[i].MaterialPreset,
                    expectedFeatures: viewerCases[i].ExpectedFeatures));
            }

            return gateCases.ToArray();
        }

        public static MmdRuntimeViewerFixtureCase[] LoadViewerCases(
            string manifestPath,
            List<string> errors,
            bool includeSkipped = true)
        {
            if (string.IsNullOrWhiteSpace(manifestPath))
            {
                return Array.Empty<MmdRuntimeViewerFixtureCase>();
            }

            string fullManifestPath = Path.GetFullPath(manifestPath);
            if (!File.Exists(fullManifestPath))
            {
                errors.Add("Fixture manifest does not exist: " + fullManifestPath);
                return Array.Empty<MmdRuntimeViewerFixtureCase>();
            }

            Dictionary<string, object?> manifest;
            try
            {
                manifest = Json.ParseObject(File.ReadAllText(fullManifestPath));
            }
            catch (Exception ex)
            {
                errors.Add("Fixture manifest JSON parse failed: " + ex.Message);
                return Array.Empty<MmdRuntimeViewerFixtureCase>();
            }

            int schemaVersion = GetInt(manifest, "schemaVersion", defaultValue: 0);
            if (schemaVersion != SupportedSchemaVersion)
            {
                errors.Add("Unsupported fixture manifest schemaVersion: " + schemaVersion);
                return Array.Empty<MmdRuntimeViewerFixtureCase>();
            }

            Dictionary<string, object?>? paths = GetObject(manifest, "paths");
            Dictionary<string, object?>? releaseSmoke = GetObject(paths, "releaseSmoke");
            Dictionary<string, object?>? byExtension = GetObject(releaseSmoke, "byExtension");
            Dictionary<string, object?>? playbackSmoke = GetObject(paths, "playbackSmoke");
            List<object?>? cases = GetArray(playbackSmoke, "cases");
            if (cases == null || cases.Count == 0)
            {
                errors.Add("Fixture manifest has no paths.playbackSmoke.cases entries: " + fullManifestPath);
                return Array.Empty<MmdRuntimeViewerFixtureCase>();
            }

            string manifestDirectory = Path.GetDirectoryName(fullManifestPath) ?? Directory.GetCurrentDirectory();
            string baseDirectory = ResolveBaseDirectory(
                manifestDirectory,
                GetString(manifest, "basePath"));

            var resolvedCases = new List<MmdRuntimeViewerFixtureCase>(cases.Count);
            for (int i = 0; i < cases.Count; i++)
            {
                if (cases[i] is not Dictionary<string, object?> fixtureCase)
                {
                    errors.Add("Fixture playbackSmoke case at index " + i + " is not an object.");
                    continue;
                }

                string skipReason = GetString(fixtureCase, "skipReason");
                if (!includeSkipped && !string.IsNullOrWhiteSpace(skipReason))
                {
                    continue;
                }

                string caseName = GetString(fixtureCase, "name");
                if (string.IsNullOrWhiteSpace(caseName))
                {
                    caseName = "playbackSmoke[" + i + "]";
                }

                Dictionary<string, object?>? model = GetObject(fixtureCase, "model");
                Dictionary<string, object?>? motion = GetObject(fixtureCase, "motion");
                Dictionary<string, object?>? camera = GetObject(fixtureCase, "camera");
                Dictionary<string, object?>? audio = GetObject(fixtureCase, "audio");
                Dictionary<string, object?>? background = GetObject(fixtureCase, "background");
                string modelExtension = GetString(model, "extension");
                string modelKey = GetString(model, "key");
                string motionKey = GetString(motion, "key");

                if (!string.Equals(modelExtension, "pmx", StringComparison.Ordinal))
                {
                    continue;
                }

                string pmxPath = ResolveRequiredReference(
                    byExtension,
                    baseDirectory,
                    caseName,
                    "pmx",
                    modelKey,
                    errors);
                string vmdPath = ResolveRequiredReference(
                    byExtension,
                    baseDirectory,
                    caseName,
                    "vmd",
                    motionKey,
                    errors);
                if (string.IsNullOrWhiteSpace(pmxPath) || string.IsNullOrWhiteSpace(vmdPath))
                {
                    continue;
                }

                bool caseParseOnly = GetBool(fixtureCase, "parseOnly", false);
                MmdMaterialPreset materialPreset = ResolveMaterialPreset(
                    GetString(fixtureCase, "materialPreset"),
                    caseName,
                    errors);
                string[] expectedFeatures = GetStringArray(fixtureCase, "expectedFeatures");

                resolvedCases.Add(new MmdRuntimeViewerFixtureCase(
                    caseName,
                    pmxPath,
                    vmdPath,
                    ResolveOptionalReference(byExtension, baseDirectory, camera, "cameraVmd"),
                    ResolveOptionalReference(byExtension, baseDirectory, audio, string.Empty),
                    ResolveOptionalReference(byExtension, baseDirectory, background, string.Empty),
                    ResolveAudioOffsetFrame(fixtureCase, audio),
                    skipReason,
                    parseOnly: caseParseOnly,
                    materialPreset: materialPreset,
                    expectedFeatures: expectedFeatures));
            }

            return resolvedCases.ToArray();
        }

        private static string ResolveBaseDirectory(string manifestDirectory, string basePath)
        {
            string path = string.IsNullOrWhiteSpace(basePath) ? "." : basePath;
            return Path.IsPathRooted(path)
                ? Path.GetFullPath(path)
                : Path.GetFullPath(Path.Combine(manifestDirectory, path));
        }

        private static string ResolveRequiredReference(
            Dictionary<string, object?>? byExtension,
            string baseDirectory,
            string caseName,
            string extension,
            string key,
            List<string> errors)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                errors.Add("Fixture case '" + caseName + "' has an incomplete " + extension + " reference.");
                return string.Empty;
            }

            Dictionary<string, object?>? map = GetObject(byExtension, extension);
            if (map == null)
            {
                errors.Add("Fixture manifest is missing paths.releaseSmoke.byExtension." + extension + ".");
                return string.Empty;
            }

            string relativePath = GetString(map, key);
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                errors.Add("Fixture case '" + caseName + "' references missing fixture key: " + extension + "." + key);
                return string.Empty;
            }

            return Path.IsPathRooted(relativePath)
                ? Path.GetFullPath(relativePath)
                : Path.GetFullPath(Path.Combine(baseDirectory, relativePath));
        }

        private static string ResolveOptionalReference(
            Dictionary<string, object?>? byExtension,
            string baseDirectory,
            Dictionary<string, object?>? reference,
            string defaultExtension)
        {
            string extension = GetString(reference, "extension");
            if (string.IsNullOrWhiteSpace(extension))
            {
                extension = defaultExtension;
            }

            string key = GetString(reference, "key");
            if (string.IsNullOrWhiteSpace(extension) || string.IsNullOrWhiteSpace(key))
            {
                return string.Empty;
            }

            Dictionary<string, object?>? map = GetObject(byExtension, extension);
            string relativePath = GetString(map, key);
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                return string.Empty;
            }

            return Path.IsPathRooted(relativePath)
                ? Path.GetFullPath(relativePath)
                : Path.GetFullPath(Path.Combine(baseDirectory, relativePath));
        }

        private static Dictionary<string, object?>? GetObject(
            Dictionary<string, object?>? value,
            string key)
        {
            if (value == null || !value.TryGetValue(key, out object? child))
            {
                return null;
            }

            return child as Dictionary<string, object?>;
        }

        private static List<object?>? GetArray(
            Dictionary<string, object?>? value,
            string key)
        {
            if (value == null || !value.TryGetValue(key, out object? child))
            {
                return null;
            }

            return child as List<object?>;
        }

        private static string GetString(Dictionary<string, object?>? value, string key)
        {
            if (value == null || !value.TryGetValue(key, out object? child))
            {
                return string.Empty;
            }

            return child as string ?? string.Empty;
        }

        private static int GetInt(
            Dictionary<string, object?> value,
            string key,
            int defaultValue)
        {
            if (!value.TryGetValue(key, out object? child))
            {
                return defaultValue;
            }

            if (child is double number)
            {
                return (int)number;
            }

            return defaultValue;
        }

        private static float ResolveAudioOffsetFrame(
            Dictionary<string, object?> fixtureCase,
            Dictionary<string, object?>? audio)
        {
            float caseOffset = GetFloat(fixtureCase, "audioOffsetFrame", float.NaN);
            if (!float.IsNaN(caseOffset))
            {
                return caseOffset;
            }

            return GetFloat(audio, "offsetFrame", 0.0f);
        }

        private static bool GetBool(
            Dictionary<string, object?>? value,
            string key,
            bool defaultValue)
        {
            if (value == null || !value.TryGetValue(key, out object? child))
            {
                return defaultValue;
            }

            if (child is bool b)
            {
                return b;
            }

            return defaultValue;
        }

        private static string[] GetStringArray(
            Dictionary<string, object?>? value,
            string key)
        {
            if (value == null || !value.TryGetValue(key, out object? child))
            {
                return Array.Empty<string>();
            }

            if (child is not List<object?> list || list.Count == 0)
            {
                return Array.Empty<string>();
            }

            var result = new List<string>(list.Count);
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] is string s && !string.IsNullOrWhiteSpace(s))
                {
                    result.Add(s);
                }
            }

            return result.ToArray();
        }

        private static MmdMaterialPreset ResolveMaterialPreset(
            string value,
            string caseName,
            List<string> errors)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return MmdMaterialPreset.MmdToon;
            }

            if (MmdRuntimeVerificationArguments.TryParseMaterialPreset(value, out MmdMaterialPreset preset))
            {
                return preset;
            }

            errors.Add("Fixture case '" + caseName + "' has invalid materialPreset: " + value + ".");
            return MmdMaterialPreset.MmdToon;
        }

        private static float GetFloat(
            Dictionary<string, object?>? value,
            string key,
            float defaultValue)
        {
            if (value == null || !value.TryGetValue(key, out object? child))
            {
                return defaultValue;
            }

            if (child is double number)
            {
                return (float)number;
            }

            return defaultValue;
        }

        private static class Json
        {
            public static Dictionary<string, object?> ParseObject(string json)
            {
                var parser = new Parser(json);
                object? value = parser.ParseValue();
                parser.SkipWhitespace();
                if (!parser.IsEnd)
                {
                    throw new FormatException("Unexpected trailing JSON at index " + parser.Position + ".");
                }

                return value as Dictionary<string, object?>
                    ?? throw new FormatException("Top-level JSON value must be an object.");
            }

            private sealed class Parser
            {
                private readonly string json;
                private int index;

                public Parser(string json)
                {
                    this.json = json ?? string.Empty;
                }

                public int Position => index;
                public bool IsEnd => index >= json.Length;

                public object? ParseValue()
                {
                    SkipWhitespace();
                    if (IsEnd)
                    {
                        throw new FormatException("Unexpected end of JSON.");
                    }

                    char c = json[index];
                    if (c == '{')
                    {
                        return ParseObjectValue();
                    }

                    if (c == '[')
                    {
                        return ParseArray();
                    }

                    if (c == '"')
                    {
                        return ParseString();
                    }

                    if (c == '-' || char.IsDigit(c))
                    {
                        return ParseNumber();
                    }

                    if (TryReadLiteral("true"))
                    {
                        return true;
                    }

                    if (TryReadLiteral("false"))
                    {
                        return false;
                    }

                    if (TryReadLiteral("null"))
                    {
                        return null;
                    }

                    throw new FormatException("Unexpected JSON token at index " + index + ".");
                }

                public void SkipWhitespace()
                {
                    while (!IsEnd && char.IsWhiteSpace(json[index]))
                    {
                        index++;
                    }
                }

                private Dictionary<string, object?> ParseObjectValue()
                {
                    Expect('{');
                    var result = new Dictionary<string, object?>(StringComparer.Ordinal);
                    SkipWhitespace();
                    if (TryConsume('}'))
                    {
                        return result;
                    }

                    while (true)
                    {
                        SkipWhitespace();
                        string key = ParseString();
                        SkipWhitespace();
                        Expect(':');
                        result[key] = ParseValue();
                        SkipWhitespace();
                        if (TryConsume('}'))
                        {
                            return result;
                        }

                        Expect(',');
                    }
                }

                private List<object?> ParseArray()
                {
                    Expect('[');
                    var result = new List<object?>();
                    SkipWhitespace();
                    if (TryConsume(']'))
                    {
                        return result;
                    }

                    while (true)
                    {
                        result.Add(ParseValue());
                        SkipWhitespace();
                        if (TryConsume(']'))
                        {
                            return result;
                        }

                        Expect(',');
                    }
                }

                private string ParseString()
                {
                    Expect('"');
                    var result = new System.Text.StringBuilder();
                    while (!IsEnd)
                    {
                        char c = json[index++];
                        if (c == '"')
                        {
                            return result.ToString();
                        }

                        if (c != '\\')
                        {
                            result.Append(c);
                            continue;
                        }

                        if (IsEnd)
                        {
                            throw new FormatException("Unexpected end of JSON escape sequence.");
                        }

                        char escaped = json[index++];
                        switch (escaped)
                        {
                            case '"':
                            case '\\':
                            case '/':
                                result.Append(escaped);
                                break;
                            case 'b':
                                result.Append('\b');
                                break;
                            case 'f':
                                result.Append('\f');
                                break;
                            case 'n':
                                result.Append('\n');
                                break;
                            case 'r':
                                result.Append('\r');
                                break;
                            case 't':
                                result.Append('\t');
                                break;
                            case 'u':
                                result.Append(ParseUnicodeEscape());
                                break;
                            default:
                                throw new FormatException("Unsupported JSON escape at index " + (index - 1) + ".");
                        }
                    }

                    throw new FormatException("Unterminated JSON string.");
                }

                private char ParseUnicodeEscape()
                {
                    if (index + 4 > json.Length)
                    {
                        throw new FormatException("Incomplete JSON unicode escape.");
                    }

                    string hex = json.Substring(index, 4);
                    index += 4;
                    if (!ushort.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ushort value))
                    {
                        throw new FormatException("Invalid JSON unicode escape: " + hex);
                    }

                    return (char)value;
                }

                private double ParseNumber()
                {
                    int start = index;
                    if (!IsEnd && json[index] == '-')
                    {
                        index++;
                    }

                    while (!IsEnd && char.IsDigit(json[index]))
                    {
                        index++;
                    }

                    if (!IsEnd && json[index] == '.')
                    {
                        index++;
                        while (!IsEnd && char.IsDigit(json[index]))
                        {
                            index++;
                        }
                    }

                    if (!IsEnd && (json[index] == 'e' || json[index] == 'E'))
                    {
                        index++;
                        if (!IsEnd && (json[index] == '+' || json[index] == '-'))
                        {
                            index++;
                        }

                        while (!IsEnd && char.IsDigit(json[index]))
                        {
                            index++;
                        }
                    }

                    string text = json.Substring(start, index - start);
                    if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
                    {
                        throw new FormatException("Invalid JSON number: " + text);
                    }

                    return value;
                }

                private bool TryReadLiteral(string literal)
                {
                    if (index + literal.Length > json.Length)
                    {
                        return false;
                    }

                    if (string.CompareOrdinal(json, index, literal, 0, literal.Length) != 0)
                    {
                        return false;
                    }

                    index += literal.Length;
                    return true;
                }

                private bool TryConsume(char expected)
                {
                    if (!IsEnd && json[index] == expected)
                    {
                        index++;
                        return true;
                    }

                    return false;
                }

                private void Expect(char expected)
                {
                    if (IsEnd || json[index] != expected)
                    {
                        throw new FormatException("Expected '" + expected + "' at index " + index + ".");
                    }

                    index++;
                }
            }
        }
    }

    public readonly struct MmdRuntimeViewerFixtureCase
    {
        public MmdRuntimeViewerFixtureCase(
            string name,
            string pmxPath,
            string vmdPath,
            string cameraPath,
            string audioPath,
            string backgroundPath,
            float audioOffsetFrame,
            string skipReason,
            bool parseOnly = false,
            MmdMaterialPreset materialPreset = MmdMaterialPreset.MmdToon,
            string[]? expectedFeatures = null)
        {
            Name = name ?? string.Empty;
            PmxPath = pmxPath ?? string.Empty;
            VmdPath = vmdPath ?? string.Empty;
            CameraPath = cameraPath ?? string.Empty;
            AudioPath = audioPath ?? string.Empty;
            BackgroundPath = backgroundPath ?? string.Empty;
            AudioOffsetFrame = audioOffsetFrame;
            SkipReason = skipReason ?? string.Empty;
            ParseOnly = parseOnly;
            MaterialPreset = materialPreset;
            ExpectedFeatures = expectedFeatures ?? Array.Empty<string>();
        }

        public string Name { get; }
        public string PmxPath { get; }
        public string VmdPath { get; }
        public string CameraPath { get; }
        public string AudioPath { get; }
        public string BackgroundPath { get; }
        public float AudioOffsetFrame { get; }
        public string SkipReason { get; }
        public bool ParseOnly { get; }
        public MmdMaterialPreset MaterialPreset { get; }
        public string[] ExpectedFeatures { get; }
    }
}
