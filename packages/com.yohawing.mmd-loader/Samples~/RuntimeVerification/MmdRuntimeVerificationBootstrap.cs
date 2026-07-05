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
                MmdRuntimeVerificationArguments.Parse(Environment.GetCommandLineArgs());
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
