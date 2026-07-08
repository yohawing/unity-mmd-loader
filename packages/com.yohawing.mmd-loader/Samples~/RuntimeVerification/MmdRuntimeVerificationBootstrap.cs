#nullable enable

using System;
using System.Collections;
using System.IO;
using UnityEngine;
using Mmd.Physics;

namespace Mmd.Samples.RuntimeVerification
{
    public sealed class MmdRuntimeVerificationBootstrap : MonoBehaviour
    {
        private const string ViewRootName = "MMD Runtime Verification View";
        private const string GridObjectName = "MMD Runtime Verification Grid";

        private IEnumerator Start()
        {
            ConfigureSceneView();

            MmdRuntimeVerificationArguments arguments =
                MmdRuntimeVerificationArguments.Parse(GetCommandLineArgs());
            if (arguments.PhysicsMaxSubStepFixedStepSeconds > 0.0f)
            {
                BulletMmdPhysicsBackend.SetMaxSubStepEstimateFixedTimeStepSecondsForDiagnostics(
                    arguments.PhysicsMaxSubStepFixedStepSeconds);
            }
            else
            {
                BulletMmdPhysicsBackend.ResetMaxSubStepEstimateFixedTimeStepSecondsForDiagnostics();
            }

            if (arguments.ViewerMode)
            {
                MmdRuntimeViewerController viewer =
                    gameObject.GetComponent<MmdRuntimeViewerController>() ??
                    gameObject.AddComponent<MmdRuntimeViewerController>();
                viewer.Initialize(arguments);
                yield break;
            }

            try
            {
                var runner = new MmdRuntimeVerificationRunner(arguments);
                yield return runner.Run();
                runner.Report.physicsMaxSubStepFixedStepSeconds =
                    BulletMmdPhysicsBackend.MaxSubStepEstimateFixedTimeStepSecondsForDiagnostics;

                string json = JsonUtility.ToJson(runner.Report, prettyPrint: true);
                if (!string.IsNullOrWhiteSpace(arguments.OutputPath))
                {
                    string? directory = Path.GetDirectoryName(arguments.OutputPath);
                    if (!string.IsNullOrWhiteSpace(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    File.WriteAllText(arguments.OutputPath, json);
                }

                Console.WriteLine(json);
                Debug.Log(json);
                Application.Quit(runner.Report.exitCode);
            }
            finally
            {
                BulletMmdPhysicsBackend.ResetMaxSubStepEstimateFixedTimeStepSecondsForDiagnostics();
            }
        }

        private static string[] GetCommandLineArgs()
        {
            string[] args = Environment.GetCommandLineArgs();
#if UNITY_EDITOR
            if (ShouldUseEditorViewerMode(args))
            {
                var editorArgs = new string[args.Length + 1];
                Array.Copy(args, editorArgs, args.Length);
                editorArgs[args.Length] = "--viewer";
                return editorArgs;
            }
#endif
            return args;
        }

#if UNITY_EDITOR
        private static bool ShouldUseEditorViewerMode(string[] args)
        {
            if (!Application.isEditor || Application.isBatchMode)
            {
                return false;
            }

            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i] ?? string.Empty;
                int equals = arg.IndexOf('=');
                string name = equals >= 0 ? arg.Substring(0, equals) : arg;
                switch (name)
                {
                    case "--viewer":
                    case "--pmx":
                    case "--vmd":
                    case "--dir":
                    case "--fixture-manifest":
                    case "--screenshot-dir":
                    case "--material-preset":
                    case "--out":
                    case "--duration":
                    case "--frame-rate":
                    case "--sample-frames":
                    case "--physics-max-substep-fixed-step":
                    case "--dump-bones":
                    case "--drive":
                    case "--fast-runtime":
                    case "--help":
                    case "-h":
                        return false;
                }
            }

            return true;
        }
#endif

        private static void ConfigureSceneView()
        {
            Camera camera = Camera.main ?? FindAnyObjectByType<Camera>();
            if (camera != null)
            {
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.white;
            }

            if (GameObject.Find(GridObjectName) != null)
            {
                return;
            }

            var root = new GameObject(ViewRootName);
            var grid = new GameObject(GridObjectName);
            grid.transform.SetParent(root.transform, worldPositionStays: false);

            Mesh mesh = BuildGridMesh(lineCountPerAxis: 25, spacing: 0.25f);
            var filter = grid.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;

            var renderer = grid.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = CreateGridMaterial();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private static Mesh BuildGridMesh(int lineCountPerAxis, float spacing)
        {
            int halfLineCount = lineCountPerAxis / 2;
            int vertexCount = (lineCountPerAxis + 1) * 4;
            var vertices = new Vector3[vertexCount];
            var indices = new int[vertexCount];
            int cursor = 0;
            float extent = halfLineCount * spacing;

            for (int i = -halfLineCount; i <= halfLineCount; i++)
            {
                float offset = i * spacing;
                vertices[cursor] = new Vector3(-extent, 0.0f, offset);
                indices[cursor] = cursor;
                cursor++;
                vertices[cursor] = new Vector3(extent, 0.0f, offset);
                indices[cursor] = cursor;
                cursor++;
                vertices[cursor] = new Vector3(offset, 0.0f, -extent);
                indices[cursor] = cursor;
                cursor++;
                vertices[cursor] = new Vector3(offset, 0.0f, extent);
                indices[cursor] = cursor;
                cursor++;
            }

            var mesh = new Mesh
            {
                name = "Runtime Verification Grid Mesh",
                hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild
            };
            mesh.SetVertices(vertices);
            mesh.SetIndices(indices, MeshTopology.Lines, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Material CreateGridMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Unlit/Color")
                ?? Shader.Find("Sprites/Default");
            var material = new Material(shader)
            {
                name = "Runtime Verification Grid Material",
                color = new Color(0.72f, 0.76f, 0.82f, 1.0f),
                hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild
            };
            return material;
        }
    }
}
