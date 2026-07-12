#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Mmd.Mme
{
    public static class MmeFxScanner
    {
        private const string DotFxExtension = ".fx";

        public static IReadOnlyList<MmeFxEffectDescriptor> ScanFromModelPath(string pmxPath)
        {
            return ScanFromModelPathCore(pmxPath, materialCount: 0, scanEmd: false);
        }

        public static IReadOnlyList<MmeFxEffectDescriptor> ScanFromModelPath(string pmxPath, int materialCount)
        {
            return ScanFromModelPathCore(pmxPath, materialCount, scanEmd: true);
        }

        private static IReadOnlyList<MmeFxEffectDescriptor> ScanFromModelPathCore(
            string pmxPath,
            int materialCount,
            bool scanEmd)
        {
            if (string.IsNullOrWhiteSpace(pmxPath))
            {
                return Array.Empty<MmeFxEffectDescriptor>();
            }

            string? directory = Path.GetDirectoryName(pmxPath);
            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
            {
                return Array.Empty<MmeFxEffectDescriptor>();
            }

            var results = new List<MmeFxEffectDescriptor>();
            string modelBaseName = Path.GetFileNameWithoutExtension(pmxPath);
            bool hasModelEmd = scanEmd && HasModelEmd(directory, modelBaseName);

            if (hasModelEmd)
            {
                ScanEmdFile(directory, modelBaseName, materialCount, results);
            }
            else
            {
                ScanDirectory(directory, results);
            }

            string[] subdirs;
            try
            {
                subdirs = Directory.GetDirectories(directory);
            }
            catch (Exception)
            {
                return results;
            }

            foreach (string subdir in subdirs)
            {
                if (hasModelEmd)
                {
                    continue;
                }

                ScanDirectory(subdir, results);
            }

            return results;
        }

        private static void ScanDirectory(string directory, List<MmeFxEffectDescriptor> results)
        {
            string[] fxFiles;
            try
            {
                fxFiles = Directory.GetFiles(directory, "*.fx", SearchOption.TopDirectoryOnly);
            }
            catch (Exception)
            {
                return;
            }

            foreach (string fxFile in fxFiles)
            {
                string content;
                try
                {
                    content = ReadTextBestEffort(fxFile);
                }
                catch (Exception)
                {
                    continue;
                }

                MmeFxEffectDescriptor? descriptor = MmeFxParser.TryParse(content, fxFile);
                if (descriptor != null)
                {
                    results.Add(descriptor);
                }
            }
        }

        private static bool HasModelEmd(string directory, string modelBaseName)
        {
            return !string.IsNullOrWhiteSpace(modelBaseName) &&
                   File.Exists(Path.Combine(directory, modelBaseName + ".emd"));
        }

        private static void ScanEmdFile(
            string directory,
            string modelBaseName,
            int materialCount,
            List<MmeFxEffectDescriptor> results)
        {
            if (string.IsNullOrWhiteSpace(modelBaseName))
            {
                return;
            }

            string emdFile = Path.Combine(directory, modelBaseName + ".emd");
            if (!File.Exists(emdFile))
            {
                return;
            }

            string content;
            try
            {
                content = ReadTextBestEffort(emdFile);
            }
            catch (Exception)
            {
                return;
            }

            MmeEmdEffectMap effectMap = MmeEmdParser.ParseEffectMap(content);
            List<MmeEmdMaterialEffectAssignment> assignments = BuildEffectiveAssignments(effectMap, materialCount);
            for (int i = 0; i < assignments.Count; i++)
            {
                MmeEmdMaterialEffectAssignment assignment = assignments[i];
                if (!TryResolveEffectPath(directory, assignment.effectPath, out string fxFile))
                {
                    continue;
                }

                MmeFxEffectDescriptor? descriptor = TryParseFxFile(fxFile);
                if (descriptor == null)
                {
                    continue;
                }

                descriptor.materialIndex = assignment.materialIndex;
                results.Add(descriptor);
            }
        }

        private static List<MmeEmdMaterialEffectAssignment> BuildEffectiveAssignments(
            MmeEmdEffectMap effectMap,
            int materialCount)
        {
            var assignments = new List<MmeEmdMaterialEffectAssignment>(effectMap.materialAssignments);
            if (string.IsNullOrWhiteSpace(effectMap.defaultEffectPath))
            {
                return assignments;
            }

            var explicitIndices = new HashSet<int>();
            for (int i = 0; i < effectMap.materialAssignments.Count; i++)
            {
                int materialIndex = effectMap.materialAssignments[i].materialIndex;
                explicitIndices.Add(materialIndex);
            }

            for (int i = 0; i < effectMap.noneMaterialIndices.Count; i++)
            {
                int materialIndex = effectMap.noneMaterialIndices[i];
                explicitIndices.Add(materialIndex);
            }

            for (int materialIndex = 0; materialIndex < materialCount; materialIndex++)
            {
                if (explicitIndices.Contains(materialIndex))
                {
                    continue;
                }

                assignments.Add(new MmeEmdMaterialEffectAssignment
                {
                    materialIndex = materialIndex,
                    effectPath = effectMap.defaultEffectPath
                });
            }

            return assignments;
        }

        private static MmeFxEffectDescriptor? TryParseFxFile(string fxFile)
        {
            string content;
            try
            {
                content = ReadTextBestEffort(fxFile);
            }
            catch (Exception)
            {
                return null;
            }

            return MmeFxParser.TryParse(content, fxFile);
        }

        private static string ReadTextBestEffort(string path)
        {
            byte[] bytes = File.ReadAllBytes(path);
            try
            {
                return TrimByteOrderMark(new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true)
                    .GetString(bytes));
            }
            catch (DecoderFallbackException)
            {
                return TrimByteOrderMark(Encoding.GetEncoding(932).GetString(bytes));
            }
        }

        private static string TrimByteOrderMark(string text)
        {
            return text.Length > 0 && text[0] == '\uFEFF' ? text.Substring(1) : text;
        }

        private static bool TryResolveEffectPath(string directory, string effectPath, out string fxFile)
        {
            fxFile = string.Empty;
            if (string.IsNullOrWhiteSpace(effectPath) || Path.IsPathRooted(effectPath) ||
                !string.Equals(Path.GetExtension(effectPath), DotFxExtension, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string root = Path.GetFullPath(directory);
            string relativePath = effectPath
                .Replace('\\', Path.DirectorySeparatorChar)
                .Replace('/', Path.DirectorySeparatorChar);
            string candidate = Path.GetFullPath(Path.Combine(root, relativePath));
            if (!IsUnderDirectory(root, candidate) || !File.Exists(candidate))
            {
                return false;
            }

            fxFile = candidate;
            return true;
        }

        private static bool IsUnderDirectory(string root, string candidate)
        {
            string normalizedRoot = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
                                    Path.DirectorySeparatorChar;
            string normalizedCandidate = candidate.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return normalizedCandidate.StartsWith(normalizedRoot, StringComparison.Ordinal);
        }
    }
}
