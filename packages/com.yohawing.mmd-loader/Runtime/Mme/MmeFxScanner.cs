#nullable enable

using System;
using System.Collections.Generic;
using System.IO;

namespace Mmd.Mme
{
    public static class MmeFxScanner
    {
        public static IReadOnlyList<MmeFxEffectDescriptor> ScanFromModelPath(string pmxPath)
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

            ScanDirectory(directory, results);

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
                    content = File.ReadAllText(fxFile);
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
    }
}
