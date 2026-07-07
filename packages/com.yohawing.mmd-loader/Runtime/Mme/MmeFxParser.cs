#nullable enable

using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Mmd.Mme
{
    public static class MmeFxParser
    {
        private static readonly Regex DefineStringRegex =
            new(@"^\s*#define\s+(\w+)\s+""([^""]+)""", RegexOptions.Compiled);

        private static readonly Regex DefineOnlyRegex =
            new(@"^\s*#define\s+(\w+)\s*$", RegexOptions.Compiled);

        private static readonly Regex DefineIntRegex =
            new(@"^\s*#define\s+(\w+)\s+(\d+)", RegexOptions.Compiled);

        private static readonly Regex FloatVarRegex =
            new(@"^\s*(?:static\s+)?(?:const\s+)?float\s+(\w+)\s*=\s*([\d.eE+\-]+)\s*;",
                RegexOptions.Compiled);

        private static readonly Regex IncludeRegex =
            new(@"^\s*#include\s+""([^""]+)""", RegexOptions.Compiled);

        public static MmeFxEffectDescriptor? TryParse(string content, string sourcePath)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return null;
            }

            var descriptor = new MmeFxEffectDescriptor
            {
                sourcePath = sourcePath ?? string.Empty
            };

            string[] lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (string line in lines)
            {
                string trimmed = line.TrimStart();
                if (trimmed.Length == 0 || trimmed.StartsWith("/*") || trimmed.StartsWith("*") ||
                    trimmed.StartsWith("//"))
                {
                    continue;
                }

                if (trimmed.StartsWith("#define"))
                {
                    ParseDefine(trimmed, descriptor);
                }
                else if (trimmed.StartsWith("float") || trimmed.StartsWith("static") ||
                         trimmed.StartsWith("const"))
                {
                    ParseFloatVariable(trimmed, descriptor);
                }
                else if (trimmed.StartsWith("#include"))
                {
                    ParseInclude(trimmed, descriptor);
                }
            }

            if (string.IsNullOrEmpty(descriptor.effectType))
            {
                return null;
            }

            return descriptor;
        }

        private static void ParseDefine(string line, MmeFxEffectDescriptor descriptor)
        {
            Match stringMatch = DefineStringRegex.Match(line);
            if (stringMatch.Success)
            {
                string key = stringMatch.Groups[1].Value;
                string value = stringMatch.Groups[2].Value;

                switch (key)
                {
                    case "TEXTURE_NORMALMAP":
                        descriptor.normalMapTexture = value;
                        break;
                    case "TEXTURE_THRESHOLD":
                        descriptor.thresholdTexture = value;
                        break;
                    case "ALBEDO_MAP_FILE":
                        descriptor.albedoMapTexture = value;
                        break;
                    case "NORMAL_MAP_FILE":
                        descriptor.normalMapFile = value;
                        break;
                    case "SMOOTHNESS_MAP_FILE":
                        descriptor.smoothnessMapFile = value;
                        break;
                    case "METALNESS_MAP_FILE":
                        descriptor.metalnessMapFile = value;
                        break;
                }

                return;
            }

            Match intMatch = DefineIntRegex.Match(line);
            if (intMatch.Success)
            {
                string key = intMatch.Groups[1].Value;
                string value = intMatch.Groups[2].Value;

                if (key == "MAX_ANISOTROPY" &&
                    int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int aniso))
                {
                    descriptor.maxAnisotropy = aniso;
                }

                return;
            }

            Match flagMatch = DefineOnlyRegex.Match(line);
            if (flagMatch.Success)
            {
                string key = flagMatch.Groups[1].Value;
                switch (key)
                {
                    case "USE_NORMALMAP":
                        descriptor.useNormalMap = true;
                        break;
                    case "USE_MATERIAL_TEXTURE":
                        descriptor.useMaterialTexture = true;
                        break;
                    case "USE_MATERIAL_SPECULAR":
                        descriptor.useMaterialSpecular = true;
                        break;
                    case "USE_MATERIAL_SPHERE":
                        descriptor.useMaterialSphere = true;
                        break;
                    case "USE_SELFSHADOW_MODE":
                        descriptor.useSelfShadow = true;
                        break;
                    case "USE_SOFT_SHADOW":
                        descriptor.useSoftShadow = true;
                        break;
                }
            }
        }

        private static void ParseFloatVariable(string line, MmeFxEffectDescriptor descriptor)
        {
            Match match = FloatVarRegex.Match(line);
            if (!match.Success)
            {
                return;
            }

            string name = match.Groups[1].Value;
            if (!float.TryParse(match.Groups[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture,
                    out float value))
            {
                return;
            }

            switch (name)
            {
                case "NormalMapResolution":
                    descriptor.normalMapResolution = value;
                    break;
                case "SoftShadowParam":
                    descriptor.softShadowParam = value;
                    break;
                case "SelfShadowPower":
                    descriptor.selfShadowPower = value;
                    break;
                case "smoothness":
                    descriptor.smoothness = value;
                    break;
                case "metalness":
                    descriptor.metalness = value;
                    break;
            }
        }

        private static void ParseInclude(string line, MmeFxEffectDescriptor descriptor)
        {
            Match match = IncludeRegex.Match(line);
            if (!match.Success)
            {
                return;
            }

            string includePath = match.Groups[1].Value;

            if (includePath.IndexOf("AlternativeFull", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                descriptor.effectType = "AlternativeFull";
            }
            else if (includePath.IndexOf("material_common", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     includePath.IndexOf("ray-mmd", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     includePath.IndexOf("ray.x", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                descriptor.effectType = "ray-mmd";
            }
            else
            {
                descriptor.effectType = "unknown";
            }
        }
    }
}
