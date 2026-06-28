#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace Mmd.Samples.RuntimeVerification
{
    public static class MmdRuntimeViewerFixtureManifest
    {
        private const int SupportedSchemaVersion = 1;

        public static MmdRuntimeVerificationCase[] LoadPlaybackCases(
            string manifestPath,
            List<string> errors)
        {
            if (string.IsNullOrWhiteSpace(manifestPath))
            {
                return Array.Empty<MmdRuntimeVerificationCase>();
            }

            string fullManifestPath = Path.GetFullPath(manifestPath);
            if (!File.Exists(fullManifestPath))
            {
                errors.Add("Fixture manifest does not exist: " + fullManifestPath);
                return Array.Empty<MmdRuntimeVerificationCase>();
            }

            Dictionary<string, object?> manifest;
            try
            {
                manifest = Json.ParseObject(File.ReadAllText(fullManifestPath));
            }
            catch (Exception ex)
            {
                errors.Add("Fixture manifest JSON parse failed: " + ex.Message);
                return Array.Empty<MmdRuntimeVerificationCase>();
            }

            int schemaVersion = GetInt(manifest, "schemaVersion", defaultValue: 0);
            if (schemaVersion != SupportedSchemaVersion)
            {
                errors.Add("Unsupported fixture manifest schemaVersion: " + schemaVersion);
                return Array.Empty<MmdRuntimeVerificationCase>();
            }

            Dictionary<string, object?>? paths = GetObject(manifest, "paths");
            Dictionary<string, object?>? releaseSmoke = GetObject(paths, "releaseSmoke");
            Dictionary<string, object?>? byExtension = GetObject(releaseSmoke, "byExtension");
            Dictionary<string, object?>? playbackSmoke = GetObject(paths, "playbackSmoke");
            List<object?>? cases = GetArray(playbackSmoke, "cases");
            if (cases == null || cases.Count == 0)
            {
                errors.Add("Fixture manifest has no paths.playbackSmoke.cases entries: " + fullManifestPath);
                return Array.Empty<MmdRuntimeVerificationCase>();
            }

            string manifestDirectory = Path.GetDirectoryName(fullManifestPath) ?? Directory.GetCurrentDirectory();
            string baseDirectory = ResolveBaseDirectory(
                manifestDirectory,
                GetString(manifest, "basePath"));

            var resolvedCases = new List<MmdRuntimeVerificationCase>(cases.Count);
            for (int i = 0; i < cases.Count; i++)
            {
                if (cases[i] is not Dictionary<string, object?> fixtureCase)
                {
                    errors.Add("Fixture playbackSmoke case at index " + i + " is not an object.");
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(GetString(fixtureCase, "skipReason")))
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

                resolvedCases.Add(new MmdRuntimeVerificationCase(
                    caseName,
                    pmxPath,
                    vmdPath,
                    parseOnly: false));
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
}
