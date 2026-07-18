#nullable enable

using System;
using System.Collections.Generic;
using Mmd.Rendering;
using UnityEngine;

namespace Mmd.Samples.UnityToonShader
{
    /// <summary>
    /// Self-contained, generated PMX-material canary for the optional UTS adapter sample.
    /// The scene deliberately uses no licensed asset: two representative slots (body and hair)
    /// are rendered as primitive geometry so the Legacy/UTS comparison is reproducible after
    /// importing the package sample and pressing Play.
    /// </summary>
    public sealed class UnityToonShaderAdapterDemo : MonoBehaviour
    {
        [SerializeField] private Shader? utsShaderOverride;
        [SerializeField] private bool rebuildOnEnable = true;

        private readonly List<UnityEngine.Object> generatedObjects = new();
        private Material[] sourceMaterials = Array.Empty<Material>();
        private Material[] convertedMaterials = Array.Empty<Material>();
        private List<UnityToonShaderDiagnostic> diagnostics = new();
        private bool converted;
        private bool convertedMaterialsOwned;
        private bool ambientStateCaptured;
        private UnityEngine.Rendering.AmbientMode previousAmbientMode;
        private Color previousAmbientLight;
        private string status = "Starting adapter demo...";

        private void OnEnable()
        {
            if (rebuildOnEnable)
            {
                RebuildDemo();
            }
        }

        private void OnDisable()
        {
            DestroyDemo();
        }

        [ContextMenu("Rebuild UTS comparison")]
        public void RebuildDemo()
        {
            // ContextMenu is also available in Edit Mode. Keep all generated objects and
            // runtime materials transient so they cannot be saved into the sample scene.
            if (!Application.isPlaying)
                return;

            DestroyDemo();
            diagnostics = new List<UnityToonShaderDiagnostic>();

            ConfigureLighting();
            sourceMaterials = CreateRepresentativeMaterials();
            IReadOnlyList<MmdMaterialDescriptor> descriptors = CreateRepresentativeDescriptors();
            Shader? utsShader = utsShaderOverride != null
                ? utsShaderOverride
                : Shader.Find(UnityToonShaderAdapter.ExpectedShaderName);

            converted = UnityToonShaderAdapter.TryConvertMaterialsWithShader(
                descriptors,
                sourceMaterials,
                utsShader,
                containsMaterialMorphs: false,
                out convertedMaterials,
                diagnostics);
            convertedMaterialsOwned = converted && !ReferenceEquals(convertedMaterials, sourceMaterials);

            CreateComparisonGeometry("Legacy MMD Toon", new Vector3(-1.6f, 0.0f, 0.0f), sourceMaterials);
            CreateComparisonGeometry("UTS converted", new Vector3(1.6f, 0.0f, 0.0f), convertedMaterials);
            CreateDeterministicCamera();

            status = converted
                ? "UTS schema accepted; converted materials are shown on the right."
                : "UTS unavailable/incompatible; the right side intentionally falls back to the original MMD Toon materials.";
        }

        private void ConfigureLighting()
        {
            if (!ambientStateCaptured)
            {
                previousAmbientMode = RenderSettings.ambientMode;
                previousAmbientLight = RenderSettings.ambientLight;
                ambientStateCaptured = true;
            }

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.16f, 0.16f, 0.16f, 1.0f);

            var lightObject = new GameObject("Deterministic Key Light");
            lightObject.transform.SetParent(transform, worldPositionStays: false);
            lightObject.transform.rotation = Quaternion.Euler(35.0f, -35.0f, 0.0f);
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = Color.white;
            light.intensity = 1.25f;
            light.shadows = LightShadows.None;
            generatedObjects.Add(lightObject);
        }

        private Material[] CreateRepresentativeMaterials()
        {
            Shader? shader = Shader.Find("MMD Basic URP Toon") ?? Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                status = "No compatible source shader was found.";
                return Array.Empty<Material>();
            }

            var body = new Material(shader) { name = "Generated PMX Body (Legacy)" };
            var hair = new Material(shader) { name = "Generated PMX Hair (Legacy)" };
            ConfigureSourceMaterial(body, new Color(0.78f, 0.43f, 0.32f, 1.0f));
            ConfigureSourceMaterial(hair, new Color(0.18f, 0.30f, 0.72f, 1.0f));
            return new[] { body, hair };
        }

        private static void ConfigureSourceMaterial(Material material, Color color)
        {
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color"))
                material.SetColor("_Color", color);
            if (material.HasProperty("_DiagnosticColor"))
                material.SetColor("_DiagnosticColor", Color.white);
            if (material.HasProperty("_OutlineColor"))
                material.SetColor("_OutlineColor", Color.black);
            if (material.HasProperty("_OutlineWidth"))
                material.SetFloat("_OutlineWidth", 1.0f);
            if (material.HasProperty("_OutlineVisible"))
                material.SetFloat("_OutlineVisible", 1.0f);
            material.enableInstancing = true;
        }

        private static IReadOnlyList<MmdMaterialDescriptor> CreateRepresentativeDescriptors()
        {
            return new[]
            {
                new MmdMaterialDescriptor
                {
                    materialIndex = 0,
                    name = "PMX Body",
                    alpha = 1.0f,
                    diffuseColor = new[] { 0.78f, 0.43f, 0.32f },
                    edgeColor = new[] { 0.0f, 0.0f, 0.0f, 1.0f },
                    edgeSize = 1.0f,
                    drawEdgeFlag = true,
                    cullingPolicy = "backface-culling"
                },
                new MmdMaterialDescriptor
                {
                    materialIndex = 1,
                    name = "PMX Hair",
                    alpha = 1.0f,
                    diffuseColor = new[] { 0.18f, 0.30f, 0.72f },
                    edgeColor = new[] { 0.0f, 0.0f, 0.0f, 1.0f },
                    edgeSize = 1.0f,
                    drawEdgeFlag = true,
                    cullingPolicy = "backface-culling"
                }
            };
        }

        private void CreateComparisonGeometry(string label, Vector3 offset, Material[] materials)
        {
            var group = new GameObject(label);
            group.transform.SetParent(transform, worldPositionStays: false);
            group.transform.localPosition = offset;
            generatedObjects.Add(group);

            CreatePrimitive(group.transform, PrimitiveType.Sphere, "Body", new Vector3(0.0f, 1.0f, 0.0f),
                new Vector3(1.0f, 1.15f, 1.0f), materials.Length > 0 ? materials[0] : null);
            CreatePrimitive(group.transform, PrimitiveType.Cube, "Hair", new Vector3(0.0f, 2.0f, 0.0f),
                new Vector3(0.8f, 0.45f, 0.8f), materials.Length > 1 ? materials[1] : null);
        }

        private void CreatePrimitive(
            Transform parent,
            PrimitiveType type,
            string name,
            Vector3 position,
            Vector3 scale,
            Material? material)
        {
            GameObject primitive = GameObject.CreatePrimitive(type);
            primitive.name = name;
            primitive.transform.SetParent(parent, worldPositionStays: false);
            primitive.transform.localPosition = position;
            primitive.transform.localScale = scale;
            Collider? collider = primitive.GetComponent<Collider>();
            if (collider != null)
                DestroyGenerated(collider);
            if (material != null)
                primitive.GetComponent<Renderer>().sharedMaterial = material;
            generatedObjects.Add(primitive);
        }

        private void CreateDeterministicCamera()
        {
            var cameraObject = new GameObject("Deterministic Demo Camera");
            cameraObject.transform.SetParent(transform, worldPositionStays: false);
            cameraObject.transform.position = new Vector3(0.0f, 1.55f, 7.2f);
            cameraObject.transform.LookAt(new Vector3(0.0f, 1.15f, 0.0f), Vector3.up);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 32.0f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 30.0f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.055f, 0.065f, 0.09f, 1.0f);
            camera.depth = -10.0f;
            generatedObjects.Add(cameraObject);
        }

        private void OnGUI()
        {
            GUI.Label(new Rect(20.0f, 18.0f, 740.0f, 28.0f), "Unity Toon Shader Adapter — generated PMX material canary");
            GUI.Label(new Rect(20.0f, 46.0f, 740.0f, 24.0f), "LEFT: Legacy MMD Toon    RIGHT: UTS conversion / fail-closed fallback");
            GUI.Label(new Rect(20.0f, 72.0f, 900.0f, 24.0f), status);
            float y = 100.0f;
            foreach (UnityToonShaderDiagnostic diagnostic in diagnostics)
            {
                GUI.Label(new Rect(20.0f, y, 1100.0f, 22.0f), diagnostic.ToString());
                y += 20.0f;
            }
        }

        private void DestroyDemo()
        {
            foreach (UnityEngine.Object generated in generatedObjects)
            {
                if (generated != null)
                    DestroyGenerated(generated);
            }
            generatedObjects.Clear();

            if (convertedMaterialsOwned)
                UnityToonShaderAdapter.DestroyMaterials(convertedMaterials);
            convertedMaterials = Array.Empty<Material>();
            convertedMaterialsOwned = false;

            foreach (Material material in sourceMaterials)
            {
                if (material != null)
                    DestroyGenerated(material);
            }
            sourceMaterials = Array.Empty<Material>();

            if (ambientStateCaptured)
            {
                RenderSettings.ambientMode = previousAmbientMode;
                RenderSettings.ambientLight = previousAmbientLight;
                ambientStateCaptured = false;
            }
        }

        private static void DestroyGenerated(UnityEngine.Object target)
        {
#if UNITY_EDITOR
            // RebuildDemo can be invoked repeatedly from the ContextMenu while playing. Immediate
            // cleanup keeps the side-by-side canary from showing one-frame duplicate geometry.
            UnityEngine.Object.DestroyImmediate(target);
#else
            UnityEngine.Object.Destroy(target);
#endif
        }
    }
}
