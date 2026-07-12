#nullable enable

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Mmd.Mme
{
    [Serializable]
    public sealed class MmeEmdMaterialEffectAssignment
    {
        public int materialIndex = -1;
        public string effectPath = string.Empty;
    }

    [Serializable]
    public sealed class MmeEmdEffectMap
    {
        public string defaultEffectPath = string.Empty;
        public List<MmeEmdMaterialEffectAssignment> materialAssignments = new();
        public List<int> noneMaterialIndices = new();
    }

    public static class MmeEmdParser
    {
        private static readonly Regex SectionRegex =
            new(@"^\s*\[([^\]]+)\]\s*$", RegexOptions.Compiled);

        private static readonly Regex DefaultEffectRegex =
            new(@"^\s*Obj\s*=\s*(.*?)\s*$", RegexOptions.Compiled);

        private static readonly Regex MaterialEffectRegex =
            new(@"^\s*Obj\[(\d+)\]\s*=\s*(.*?)\s*$", RegexOptions.Compiled);

        public static IReadOnlyList<MmeEmdMaterialEffectAssignment> ParseMaterialEffectAssignments(string content)
        {
            return ParseEffectMap(content).materialAssignments;
        }

        public static MmeEmdEffectMap ParseEffectMap(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return new MmeEmdEffectMap();
            }

            var map = new MmeEmdEffectMap();
            bool inEffectSection = false;
            string[] lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string line in lines)
            {
                string trimmed = line.Trim();
                if (trimmed.Length == 0 || trimmed.StartsWith(";", StringComparison.Ordinal) ||
                    trimmed.StartsWith("#", StringComparison.Ordinal))
                {
                    continue;
                }

                Match sectionMatch = SectionRegex.Match(trimmed);
                if (sectionMatch.Success)
                {
                    inEffectSection = string.Equals(
                        sectionMatch.Groups[1].Value,
                        "Effect",
                        StringComparison.OrdinalIgnoreCase);
                    continue;
                }

                if (!inEffectSection)
                {
                    continue;
                }

                Match defaultMatch = DefaultEffectRegex.Match(trimmed);
                if (defaultMatch.Success)
                {
                    string defaultEffectPath = NormalizeEffectPath(defaultMatch.Groups[1].Value);
                    if (IsEffectEnabled(defaultEffectPath))
                    {
                        map.defaultEffectPath = defaultEffectPath;
                    }
                    else
                    {
                        map.defaultEffectPath = string.Empty;
                    }

                    continue;
                }

                Match materialMatch = MaterialEffectRegex.Match(trimmed);
                if (!materialMatch.Success ||
                    !int.TryParse(materialMatch.Groups[1].Value, out int materialIndex))
                {
                    continue;
                }

                string effectPath = NormalizeEffectPath(materialMatch.Groups[2].Value);
                if (!IsEffectEnabled(effectPath))
                {
                    map.noneMaterialIndices.Add(materialIndex);
                    continue;
                }

                map.materialAssignments.Add(new MmeEmdMaterialEffectAssignment
                {
                    materialIndex = materialIndex,
                    effectPath = effectPath
                });
            }

            return map;
        }

        private static string NormalizeEffectPath(string value)
        {
            string trimmed = value.Trim();
            if (trimmed.Length >= 2 && trimmed[0] == '"' && trimmed[trimmed.Length - 1] == '"')
            {
                return trimmed.Substring(1, trimmed.Length - 2).Trim();
            }

            return trimmed;
        }

        private static bool IsEffectEnabled(string effectPath)
        {
            return effectPath.Length > 0 &&
                   !string.Equals(effectPath, "none", StringComparison.OrdinalIgnoreCase);
        }
    }
}
