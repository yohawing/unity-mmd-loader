#nullable enable

using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Mmd.Samples.RuntimeVerification.Editor
{
    public static class MmdRuntimeVerificationBuildCommand
    {
        private const string UrpLitShaderName = "Universal Render Pipeline/Lit";
        private const string UrpLitAnchorMaterialPath = "Assets/RuntimeVerification/Resources/RuntimeVerificationUrpLitAnchor.mat";

        public static void BuildFromCommandLine()
        {
            BuildArguments arguments = BuildArguments.Parse(Environment.GetCommandLineArgs());
            string? outputDirectory = Path.GetDirectoryName(arguments.OutputPath);
            if (!string.IsNullOrWhiteSpace(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            var options = new BuildPlayerOptions
            {
                scenes = new[] { arguments.ScenePath },
                locationPathName = arguments.OutputPath,
                target = arguments.BuildTarget,
                options = arguments.Development ? BuildOptions.Development : BuildOptions.None
            };

            EnsureRuntimeShaderAnchorAsset();

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;
            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log(
                    "Runtime verification player build succeeded: " +
                    arguments.OutputPath +
                    " size=" +
                    summary.totalSize);
                EditorApplication.Exit(0);
                return;
            }

            Debug.LogError(
                "Runtime verification player build failed: " +
                summary.result +
                " errors=" +
                summary.totalErrors);
            EditorApplication.Exit(1);
        }

        private static void EnsureRuntimeShaderAnchorAsset()
        {
            Shader urpLitShader = Shader.Find(UrpLitShaderName);
            if (urpLitShader == null)
            {
                Debug.LogWarning("Runtime verification build could not resolve " + UrpLitShaderName + ".");
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(UrpLitAnchorMaterialPath) ?? "Assets/RuntimeVerification/Resources");
            Material material = AssetDatabase.LoadAssetAtPath<Material>(UrpLitAnchorMaterialPath);
            if (material == null)
            {
                material = new Material(urpLitShader)
                {
                    name = "RuntimeVerificationUrpLitAnchor"
                };
                AssetDatabase.CreateAsset(material, UrpLitAnchorMaterialPath);
                Debug.Log("Runtime verification build created shader anchor: " + UrpLitAnchorMaterialPath);
            }
            else if (material.shader != urpLitShader)
            {
                material.shader = urpLitShader;
                EditorUtility.SetDirty(material);
                Debug.Log("Runtime verification build updated shader anchor: " + UrpLitAnchorMaterialPath);
            }

            AssetDatabase.SaveAssets();
        }

        private sealed class BuildArguments
        {
            public string ScenePath = "Assets/RuntimeVerification/RuntimeVerification.unity";
            public string OutputPath = "artifacts/runtime-verification/MmdRuntimeVerification.exe";
            public bool Development;
            public BuildTarget BuildTarget = BuildTarget.StandaloneWindows64;

            public static BuildArguments Parse(string[] commandLineArgs)
            {
                var parsed = new BuildArguments();
                string[] args = commandLineArgs ?? Array.Empty<string>();
                for (int i = 0; i < args.Length; i++)
                {
                    string arg = args[i] ?? string.Empty;
                    string name = arg;
                    string? value = null;
                    int equals = arg.IndexOf('=');
                    if (equals >= 0)
                    {
                        name = arg.Substring(0, equals);
                        value = arg.Substring(equals + 1);
                    }

                    bool needsValue = name is "--scene-path" or "--output" or "--build-target";
                    if (needsValue && value == null)
                    {
                        if (i + 1 >= args.Length)
                        {
                            throw new ArgumentException("Missing value for " + name + ".");
                        }

                        value = args[++i];
                    }

                    switch (name)
                    {
                        case "--scene-path":
                            parsed.ScenePath = value ?? parsed.ScenePath;
                            break;
                        case "--output":
                            parsed.OutputPath = value ?? parsed.OutputPath;
                            break;
                        case "--development":
                            parsed.Development = true;
                            break;
                        case "--build-target":
                            parsed.BuildTarget = ParseBuildTarget(value ?? string.Empty);
                            break;
                    }
                }

                if (string.IsNullOrWhiteSpace(parsed.ScenePath))
                {
                    throw new ArgumentException("--scene-path is required.");
                }

                if (string.IsNullOrWhiteSpace(parsed.OutputPath))
                {
                    throw new ArgumentException("--output is required.");
                }

                return parsed;
            }

            private static BuildTarget ParseBuildTarget(string value)
            {
                if (Enum.TryParse(value, ignoreCase: true, out BuildTarget target))
                {
                    return target;
                }

                throw new ArgumentException("Invalid --build-target value: " + value);
            }
        }
    }
}
