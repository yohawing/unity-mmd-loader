#nullable enable

using System;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Yohawing.MmdUnity.Editor
{
    public static class MmdEditorBatchmodeDiagnosticsCommand
    {
        public static string GetRequiredArg(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                {
                    return args[i + 1];
                }
            }

            throw new ArgumentException("Missing command line argument: " + name);
        }

        public static string? TryGetArg(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                {
                    return args[i + 1];
                }
            }

            return null;
        }

        public static int TryParseInt(string name, int defaultValue)
        {
            string? value = TryGetArg(name);
            return string.IsNullOrWhiteSpace(value)
                ? defaultValue
                : int.Parse(value, CultureInfo.InvariantCulture);
        }

        public static float TryParseFloat(string name, float defaultValue)
        {
            string? value = TryGetArg(name);
            return string.IsNullOrWhiteSpace(value)
                ? defaultValue
                : float.Parse(value, CultureInfo.InvariantCulture);
        }

        public static void WriteJsonOutput<T>(string outputPath, T value)
        {
            if (string.IsNullOrWhiteSpace(outputPath))
            {
                throw new ArgumentException("Output path is required.", nameof(outputPath));
            }

            string fullOutputPath = Path.GetFullPath(outputPath);
            string? outputDirectory = Path.GetDirectoryName(fullOutputPath);
            if (!string.IsNullOrEmpty(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            File.WriteAllText(fullOutputPath, JsonUtility.ToJson(value, prettyPrint: true));
        }

        public static void RunCommand(Func<string> action, string failurePrefix)
        {
            try
            {
                string successMessage = action();
                Debug.Log(successMessage);
                EditorApplication.Exit(0);
            }
            catch (Exception ex)
            {
                Debug.LogError(failurePrefix + Environment.NewLine + ex);
                EditorApplication.Exit(1);
            }
        }
    }
}
