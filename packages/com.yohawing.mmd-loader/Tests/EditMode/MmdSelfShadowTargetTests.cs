#nullable enable

using System.Collections.Generic;
using System.IO;
using Mmd.Motion;
using Mmd.UnityIntegration;
using NUnit.Framework;
using UnityEngine;

namespace Mmd.Tests
{
    public sealed class MmdSelfShadowTargetTests
    {
        [SetUp]
        public void SetUp()
        {
            MmdSelfShadowTarget.SetReceiverGateAvailableForRendering(true);
        }

        [TearDown]
        public void TearDown()
        {
            MmdSelfShadowTarget.SetReceiverGateAvailableForRendering(false);
        }

        [Test]
        public void TargetRegistersOnlyEnabledActiveSelfShadowTargetsForRendererFeature()
        {
            var root = new GameObject("mmd-root");
            var disabled = new GameObject("mmd-disabled");
            var inactive = new GameObject("mmd-inactive");
            var buffer = new List<MmdSelfShadowTarget>();
            try
            {
                MmdSelfShadowTarget rootTarget = root.AddComponent<MmdSelfShadowTarget>();
                MmdSelfShadowTarget disabledTarget = disabled.AddComponent<MmdSelfShadowTarget>();
                MmdSelfShadowTarget inactiveTarget = inactive.AddComponent<MmdSelfShadowTarget>();
                disabledTarget.SelfShadowEnabled = false;
                inactive.SetActive(false);

                MmdSelfShadowTarget.CollectActiveTargets(buffer);

                Assert.That(buffer, Is.EquivalentTo(new[] { rootTarget }));
            }
            finally
            {
                Object.DestroyImmediate(inactive);
                Object.DestroyImmediate(disabled);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void TargetUsesDefaultProjectionPolicyAndBoundsCollector()
        {
            var root = new GameObject("mmd-root");
            GameObject? cube = null;
            try
            {
                MmdSelfShadowTarget target = root.AddComponent<MmdSelfShadowTarget>();
                cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Object.DestroyImmediate(cube.GetComponent<Collider>());
                cube.transform.SetParent(root.transform, worldPositionStays: false);
                cube.transform.position = new Vector3(1, 2, 3);
                cube.transform.localScale = new Vector3(2, 4, 6);

                MmdSelfShadowBoundsResult bounds = target.CollectBounds();
                target.RefreshReceiverGate();
                MmdSelfShadowProjectionState projectionState =
                    target.ProjectionPolicy.Evaluate(new MmdSelfShadowState(1, 0.5f));
                Renderer renderer = cube.GetComponent<Renderer>();

                Assert.That(target.SelfShadowEnabled, Is.True);
                Assert.That(target.ProjectionPolicy.BoundsPadding, Is.EqualTo(MmdSelfShadowProjectionPolicy.Default.BoundsPadding));
                Assert.That(bounds.HasBounds, Is.True);
                Assert.That(bounds.Source, Is.EqualTo(MmdSelfShadowBoundsSource.RendererCollection));
                Assert.That(bounds.Center.x, Is.EqualTo(1.0f).Within(0.001f));
                Assert.That(bounds.Center.y, Is.EqualTo(2.0f).Within(0.001f));
                Assert.That(bounds.Center.z, Is.EqualTo(3.0f).Within(0.001f));
                Assert.That(projectionState.Active, Is.True);
                AssertReceiverGate(renderer, expectedSelfShadowReceive: 1.0f, expectedSuppressStandard: 1.0f);

                target.SelfShadowEnabled = false;
                AssertReceiverGate(renderer, expectedSelfShadowReceive: 0.0f, expectedSuppressStandard: 0.0f);
            }
            finally
            {
                Object.DestroyImmediate(cube);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void TargetProjectionPolicySupportsManualBoundsOverride()
        {
            var root = new GameObject("mmd-root");
            try
            {
                MmdSelfShadowTarget target = root.AddComponent<MmdSelfShadowTarget>();
                target.ProjectionPolicy = new MmdSelfShadowProjectionPolicy(
                    boundsPadding: 0.5f,
                    hasManualBoundsOverride: true,
                    manualBoundsOverride: new MmdSelfShadowProjectionBounds(1, 2, 3, 4, 5, 6));

                MmdSelfShadowBoundsResult bounds = target.CollectBounds();

                Assert.That(bounds.HasBounds, Is.True);
                Assert.That(bounds.Source, Is.EqualTo(MmdSelfShadowBoundsSource.ManualOverride));
                Assert.That(bounds.Center.x, Is.EqualTo(1.0f).Within(0.001f));
                Assert.That(bounds.Center.y, Is.EqualTo(2.0f).Within(0.001f));
                Assert.That(bounds.Center.z, Is.EqualTo(3.0f).Within(0.001f));
                Assert.That(bounds.Size.x, Is.EqualTo(5.0f).Within(0.001f));
                Assert.That(bounds.Size.y, Is.EqualTo(6.0f).Within(0.001f));
                Assert.That(bounds.Size.z, Is.EqualTo(7.0f).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void BoundsRootChangeMovesReceiverGate()
        {
            var targetGo = new GameObject("mmd-target");
            var oldRoot = new GameObject("old-root");
            var newRoot = new GameObject("new-root");
            GameObject? oldCube = null;
            GameObject? newCube = null;
            try
            {
                oldCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                newCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Object.DestroyImmediate(oldCube.GetComponent<Collider>());
                Object.DestroyImmediate(newCube.GetComponent<Collider>());
                oldCube.transform.SetParent(oldRoot.transform, worldPositionStays: false);
                newCube.transform.SetParent(newRoot.transform, worldPositionStays: false);

                MmdSelfShadowTarget target = targetGo.AddComponent<MmdSelfShadowTarget>();
                target.BoundsRoot = oldRoot.transform;

                Renderer oldRenderer = oldCube.GetComponent<Renderer>();
                Renderer newRenderer = newCube.GetComponent<Renderer>();
                AssertReceiverGate(oldRenderer, expectedSelfShadowReceive: 1.0f, expectedSuppressStandard: 1.0f);

                target.BoundsRoot = newRoot.transform;

                AssertReceiverGate(oldRenderer, expectedSelfShadowReceive: 0.0f, expectedSuppressStandard: 0.0f);
                AssertReceiverGate(newRenderer, expectedSelfShadowReceive: 1.0f, expectedSuppressStandard: 1.0f);
            }
            finally
            {
                Object.DestroyImmediate(newCube);
                Object.DestroyImmediate(oldCube);
                Object.DestroyImmediate(newRoot);
                Object.DestroyImmediate(oldRoot);
                Object.DestroyImmediate(targetGo);
            }
        }

        [Test]
        public void TargetUsesSceneEnvironmentSelfShadowStateWhenBound()
        {
            var root = new GameObject("mmd-root");
            var environmentGo = new GameObject("environment");
            GameObject? cube = null;
            try
            {
                MmdSelfShadowTarget target = root.AddComponent<MmdSelfShadowTarget>();
                cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Object.DestroyImmediate(cube.GetComponent<Collider>());
                cube.transform.SetParent(root.transform, worldPositionStays: false);
                Renderer renderer = cube.GetComponent<Renderer>();

                MmdSceneEnvironmentBinding environment = environmentGo.AddComponent<MmdSceneEnvironmentBinding>();
                environment.SelfShadowProjectionPolicy = new MmdSelfShadowProjectionPolicy(
                    distanceScale: 10.0f,
                    minFarDistance: 1.0f,
                    maxFarDistance: 50.0f,
                    boundsPadding: 0.25f);
                target.SceneEnvironment = environment;

                Assert.That(target.TryGetActiveProjectionState(out MmdSelfShadowProjectionState notAppliedState), Is.False);
                Assert.That(notAppliedState.Active, Is.False);
                target.RefreshReceiverGate();
                AssertReceiverGate(renderer, expectedSelfShadowReceive: 0.0f, expectedSuppressStandard: 0.0f);

                environment.ApplySelfShadowState(new MmdSelfShadowState(0, 0.4f));
                Assert.That(target.TryGetActiveProjectionState(out MmdSelfShadowProjectionState inactiveState), Is.False);
                Assert.That(inactiveState.Active, Is.False);
                target.RefreshReceiverGate();
                AssertReceiverGate(renderer, expectedSelfShadowReceive: 0.0f, expectedSuppressStandard: 0.0f);

                environment.ApplySelfShadowState(new MmdSelfShadowState(2, 0.4f));
                Assert.That(target.TryGetActiveProjectionState(out MmdSelfShadowProjectionState activeState), Is.True);
                Assert.That(activeState.Mode, Is.EqualTo(2));
                Assert.That(activeState.FarDistance, Is.EqualTo(4.0f).Within(0.001f));
                Assert.That(activeState.BoundsPadding, Is.EqualTo(0.25f).Within(0.001f));
                target.RefreshReceiverGate();
                AssertReceiverGate(renderer, expectedSelfShadowReceive: 1.0f, expectedSuppressStandard: 1.0f);

                environment.SelfShadowProjectionPolicy = new MmdSelfShadowProjectionPolicy(
                    distanceScale: 20.0f,
                    minFarDistance: 1.0f,
                    maxFarDistance: 50.0f,
                    boundsPadding: 0.5f);
                Assert.That(target.TryGetActiveProjectionState(out MmdSelfShadowProjectionState updatedState), Is.True);
                Assert.That(updatedState.FarDistance, Is.EqualTo(8.0f).Within(0.001f));
                Assert.That(updatedState.BoundsPadding, Is.EqualTo(0.5f).Within(0.001f));

                environment.SelfShadowEnabled = false;
                Assert.That(target.TryGetActiveProjectionState(out MmdSelfShadowProjectionState disabledState), Is.False);
                Assert.That(disabledState.Active, Is.False);
                target.RefreshReceiverGate();
                AssertReceiverGate(renderer, expectedSelfShadowReceive: 0.0f, expectedSuppressStandard: 0.0f);
            }
            finally
            {
                Object.DestroyImmediate(cube);
                Object.DestroyImmediate(environmentGo);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void TargetClearsReceiverGateWhenDisabled()
        {
            var root = new GameObject("mmd-root");
            GameObject? cube = null;
            try
            {
                cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Object.DestroyImmediate(cube.GetComponent<Collider>());
                cube.transform.SetParent(root.transform, worldPositionStays: false);
                Renderer renderer = cube.GetComponent<Renderer>();

                MmdSelfShadowTarget target = root.AddComponent<MmdSelfShadowTarget>();
                target.RefreshReceiverGate();
                AssertReceiverGate(renderer, expectedSelfShadowReceive: 1.0f, expectedSuppressStandard: 1.0f);

                target.enabled = false;

                AssertReceiverGate(renderer, expectedSelfShadowReceive: 0.0f, expectedSuppressStandard: 0.0f);
            }
            finally
            {
                Object.DestroyImmediate(cube);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void DisableAllReceiverGatesClearsSelfShadowSuppression()
        {
            var root = new GameObject("mmd-root");
            GameObject? cube = null;
            try
            {
                cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Object.DestroyImmediate(cube.GetComponent<Collider>());
                cube.transform.SetParent(root.transform, worldPositionStays: false);
                Renderer renderer = cube.GetComponent<Renderer>();

                MmdSelfShadowTarget target = root.AddComponent<MmdSelfShadowTarget>();
                target.RefreshReceiverGate();
                AssertReceiverGate(renderer, expectedSelfShadowReceive: 1.0f, expectedSuppressStandard: 1.0f);

                MmdSelfShadowTarget.DisableAllReceiverGates();

                AssertReceiverGate(renderer, expectedSelfShadowReceive: 0.0f, expectedSuppressStandard: 0.0f);
            }
            finally
            {
                Object.DestroyImmediate(cube);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void DisableAllReceiverGatesPreservesMaterialStandardReceiveOptOut()
        {
            var root = new GameObject("mmd-root");
            GameObject? cube = null;
            Material? material = null;
            try
            {
                cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Object.DestroyImmediate(cube.GetComponent<Collider>());
                cube.transform.SetParent(root.transform, worldPositionStays: false);
                Renderer renderer = cube.GetComponent<Renderer>();
                material = new Material(Shader.Find("MMD Basic URP Toon"));
                material.SetFloat(MmdSelfShadowTarget.MmdReceiveShadowsId, 0.0f);
                renderer.sharedMaterial = material;

                MmdSelfShadowTarget target = root.AddComponent<MmdSelfShadowTarget>();
                target.RefreshReceiverGate();
                AssertReceiverGate(renderer, expectedSelfShadowReceive: 1.0f, expectedSuppressStandard: 1.0f);

                MmdSelfShadowTarget.DisableAllReceiverGates();

                AssertReceiverGate(renderer, expectedSelfShadowReceive: 0.0f, expectedSuppressStandard: 0.0f);
                Assert.That(material.GetFloat(MmdSelfShadowTarget.MmdReceiveShadowsId), Is.EqualTo(0.0f));
            }
            finally
            {
                Object.DestroyImmediate(material);
                Object.DestroyImmediate(cube);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void TargetPreservesRendererStandardReceiveOptOutAcrossToggleAndDisableAll()
        {
            var root = new GameObject("mmd-root");
            GameObject? cube = null;
            try
            {
                cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Object.DestroyImmediate(cube.GetComponent<Collider>());
                cube.transform.SetParent(root.transform, worldPositionStays: false);
                Renderer renderer = cube.GetComponent<Renderer>();
                var propertyBlock = new MaterialPropertyBlock();
                propertyBlock.SetFloat(MmdSelfShadowTarget.MmdReceiveShadowsId, 0.0f);
                renderer.SetPropertyBlock(propertyBlock);

                MmdSelfShadowTarget target = root.AddComponent<MmdSelfShadowTarget>();
                target.RefreshReceiverGate();
                AssertReceiverGate(renderer, expectedSelfShadowReceive: 1.0f, expectedSuppressStandard: 1.0f);
                AssertPropertyBlockFloat(renderer, MmdSelfShadowTarget.MmdReceiveShadowsId, 0.0f);

                target.SelfShadowEnabled = false;
                AssertReceiverGate(renderer, expectedSelfShadowReceive: 0.0f, expectedSuppressStandard: 0.0f);
                AssertPropertyBlockFloat(renderer, MmdSelfShadowTarget.MmdReceiveShadowsId, 0.0f);

                target.SelfShadowEnabled = true;
                AssertReceiverGate(renderer, expectedSelfShadowReceive: 1.0f, expectedSuppressStandard: 1.0f);
                AssertPropertyBlockFloat(renderer, MmdSelfShadowTarget.MmdReceiveShadowsId, 0.0f);

                MmdSelfShadowTarget.DisableAllReceiverGates();

                AssertReceiverGate(renderer, expectedSelfShadowReceive: 0.0f, expectedSuppressStandard: 0.0f);
                AssertPropertyBlockFloat(renderer, MmdSelfShadowTarget.MmdReceiveShadowsId, 0.0f);
            }
            finally
            {
                Object.DestroyImmediate(cube);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void DisableAllReceiverGatesPreservesUnrelatedRendererPropertyBlock()
        {
            var root = new GameObject("mmd-root");
            GameObject? cube = null;
            try
            {
                cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Object.DestroyImmediate(cube.GetComponent<Collider>());
                cube.transform.SetParent(root.transform, worldPositionStays: false);
                Renderer renderer = cube.GetComponent<Renderer>();
                var propertyBlock = new MaterialPropertyBlock();
                propertyBlock.SetFloat(Shader.PropertyToID("_UnrelatedRendererValue"), 0.25f);
                renderer.SetPropertyBlock(propertyBlock);

                MmdSelfShadowTarget target = root.AddComponent<MmdSelfShadowTarget>();
                target.RefreshReceiverGate();
                AssertReceiverGate(renderer, expectedSelfShadowReceive: 1.0f, expectedSuppressStandard: 1.0f);

                MmdSelfShadowTarget.DisableAllReceiverGates();

                AssertReceiverGate(renderer, expectedSelfShadowReceive: 0.0f, expectedSuppressStandard: 0.0f);
                AssertPropertyBlockFloat(renderer, Shader.PropertyToID("_UnrelatedRendererValue"), 0.25f);
            }
            finally
            {
                Object.DestroyImmediate(cube);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void TargetDoesNotSuppressStandardReceiveBeforeRendererFeatureEnablesReceiverGate()
        {
            MmdSelfShadowTarget.SetReceiverGateAvailableForRendering(false);
            var root = new GameObject("mmd-root");
            GameObject? cube = null;
            try
            {
                cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Object.DestroyImmediate(cube.GetComponent<Collider>());
                cube.transform.SetParent(root.transform, worldPositionStays: false);
                Renderer renderer = cube.GetComponent<Renderer>();

                MmdSelfShadowTarget target = root.AddComponent<MmdSelfShadowTarget>();
                target.RefreshReceiverGate();

                AssertReceiverGate(renderer, expectedSelfShadowReceive: 0.0f, expectedSuppressStandard: 0.0f);
            }
            finally
            {
                Object.DestroyImmediate(cube);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void SelfShadowRendererFeatureClampsShadowDepthBias()
        {
            Object? feature = null;
            try
            {
                System.Type featureType = System.Type.GetType(
                    "Mmd.Rendering.Universal.MmdSelfShadowRendererFeature, Mmd.Rendering.Universal",
                    throwOnError: true)!;
                System.Reflection.PropertyInfo shadowDepthBiasProperty =
                    featureType.GetProperty("ShadowDepthBias")!;
                feature = ScriptableObject.CreateInstance(featureType);

                Assert.That((float)shadowDepthBiasProperty.GetValue(feature), Is.EqualTo(0.0025f).Within(0.000001f));

                shadowDepthBiasProperty.SetValue(feature, 0.025f);
                Assert.That((float)shadowDepthBiasProperty.GetValue(feature), Is.EqualTo(0.025f).Within(0.000001f));

                shadowDepthBiasProperty.SetValue(feature, -1.0f);
                Assert.That((float)shadowDepthBiasProperty.GetValue(feature), Is.EqualTo(0.0f));

                shadowDepthBiasProperty.SetValue(feature, float.NaN);
                Assert.That((float)shadowDepthBiasProperty.GetValue(feature), Is.EqualTo(0.0f));

                shadowDepthBiasProperty.SetValue(feature, float.PositiveInfinity);
                Assert.That((float)shadowDepthBiasProperty.GetValue(feature), Is.EqualTo(0.0f));

                shadowDepthBiasProperty.SetValue(feature, 1.0f);
                Assert.That((float)shadowDepthBiasProperty.GetValue(feature), Is.EqualTo(0.1f));
            }
            finally
            {
                if (feature != null)
                {
                    Object.DestroyImmediate(feature);
                }
            }
        }

        private static void AssertReceiverGate(
            Renderer renderer,
            float expectedSelfShadowReceive,
            float expectedSuppressStandard)
        {
            var propertyBlock = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(propertyBlock);
            Assert.That(propertyBlock.GetFloat(MmdSelfShadowTarget.MmdSelfShadowReceiveId), Is.EqualTo(expectedSelfShadowReceive));
            Assert.That(propertyBlock.GetFloat(MmdSelfShadowTarget.MmdSuppressStandardShadowsId), Is.EqualTo(expectedSuppressStandard));
        }

        private static void AssertPropertyBlockFloat(Renderer renderer, int propertyId, float expected)
        {
            var propertyBlock = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(propertyBlock);
            Assert.That(propertyBlock.GetFloat(propertyId), Is.EqualTo(expected));
        }

        [Test]
        public void SelfShadowRendererAndShaderUseDedicatedGlobals()
        {
            string runtimeRoot = Path.Combine(MmdTestFixtures.PackageRoot, "Runtime");
            string target = File.ReadAllText(Path.Combine(runtimeRoot, "Components", "Scene", "MmdSelfShadowTarget.cs"));
            string feature = File.ReadAllText(Path.Combine(runtimeRoot, "Rendering", "Universal", "MmdSelfShadowRendererFeature.cs"));
            string pass = File.ReadAllText(Path.Combine(runtimeRoot, "Rendering", "Universal", "MmdSelfShadowRenderPass.cs"));
            string shader = File.ReadAllText(Path.Combine(runtimeRoot, "Shaders", "MmdBasicUrpToon.shader"));

            Assert.That(target, Does.Contain("MmdSuppressStandardShadowsId"));
            Assert.That(target, Does.Contain("SetFloat(MmdSelfShadowReceiveId, selfShadowValue)"));
            Assert.That(target, Does.Contain("SetFloat(MmdSuppressStandardShadowsId, suppressStandardValue)"));
            Assert.That(target, Does.Not.Contain("SetFloat(MmdReceiveShadowsId"));
            Assert.That(target, Does.Not.Contain("StandardReceiveRestoreValues"));
            Assert.That(feature, Does.Contain("MmdSelfShadowRenderPass"));
            Assert.That(feature, Does.Not.Contain("QualitySettings"));
            Assert.That(feature, Does.Not.Contain("RenderSettings"));
            Assert.That(feature, Does.Contain("SetReceiverGateAvailableForRendering(true)"));
            Assert.That(feature, Does.Contain("SetReceiverGateAvailableForRendering(false)"));
            Assert.That(feature, Does.Contain("DisableAllReceiverGates"));
            Assert.That(feature, Does.Contain("Dispose(bool disposing)"));
            Assert.That(feature, Does.Contain("shadowDepthBias = DefaultShadowDepthBias"));
            Assert.That(feature, Does.Contain("public float ShadowDepthBias"));
            Assert.That(feature, Does.Contain("if (pass.Setup(shadowMapSize, shadowDirection, ShadowDepthBias))"));
            Assert.That(pass, Does.Contain("_MmdSelfShadowMap"));
            Assert.That(pass, Does.Contain("_MmdSelfShadowWorldToShadow"));
            Assert.That(pass, Does.Contain("_MmdSelfShadowParams"));
            Assert.That(pass, Does.Contain("Setup(int requestedMapSize, Vector3 requestedShadowDirection)"));
            Assert.That(pass, Does.Contain("MmdSelfShadowRendererFeature.DefaultShadowDepthBias"));
            Assert.That(pass, Does.Contain("Setup(int requestedMapSize, Vector3 requestedShadowDirection, float requestedShadowDepthBias)"));
            Assert.That(pass, Does.Contain("parameters = new Vector4(1.0f, shadowDepthBias, 1.0f / Mathf.Max(1, targets.Count), 0.0f)"));
            Assert.That(pass, Does.Not.Contain("0.0025f"));
            Assert.That(pass, Does.Contain("SystemInfo.usesReversedZBuffer"));
            Assert.That(pass, Does.Contain("ClearDepth"));
            Assert.That(pass, Does.Contain("TryGetActiveProjectionState"));
            Assert.That(pass, Does.Not.Contain("MaxRenderers"));
            Assert.That(pass, Does.Contain("_LightDirection"));
            Assert.That(pass, Does.Contain("_LightPosition"));
            Assert.That(pass, Does.Contain("_ShadowBias"));
            Assert.That(pass, Does.Contain("DisableShaderKeyword(CastingPunctualLightShadowKeyword)"));
            Assert.That(pass, Does.Contain("AllowPassCulling(false)"));
            Assert.That(pass, Does.Contain("FindPass(\"ShadowCaster\")"));
            Assert.That(pass, Does.Contain("MmdSelfShadowTarget.CollectActiveTargets"));
            Assert.That(shader, Does.Contain("[PerRendererData] _MmdReceiveShadows"));
            Assert.That(shader, Does.Contain("[PerRendererData] [HideInInspector] _MmdSelfShadowReceive"));
            Assert.That(shader, Does.Contain("[PerRendererData] [HideInInspector] _MmdSuppressStandardShadows"));
            Assert.That(shader, Does.Contain("#pragma multi_compile_instancing"));
            Assert.That(shader, Does.Contain("UNITY_INSTANCING_BUFFER_START(MmdPerRenderer)"));
            Assert.That(shader, Does.Contain("UNITY_DEFINE_INSTANCED_PROP(float, _MmdReceiveShadows)"));
            Assert.That(shader, Does.Contain("UNITY_DEFINE_INSTANCED_PROP(float, _MmdSelfShadowReceive)"));
            Assert.That(shader, Does.Contain("UNITY_DEFINE_INSTANCED_PROP(float, _MmdSuppressStandardShadows)"));
            Assert.That(shader, Does.Contain("UNITY_ACCESS_INSTANCED_PROP(MmdPerRenderer, _MmdSelfShadowReceive)"));
            Assert.That(shader, Does.Not.Contain("UNITY_ACCESS_INSTANCED_PROP(MmdPerRenderer, _MmdReceiveShadows)"));
            Assert.That(shader, Does.Not.Contain("UNITY_ACCESS_INSTANCED_PROP(MmdPerRenderer, _MmdSuppressStandardShadows)"));
            Assert.That(shader, Does.Not.Contain("effectiveReceiveShadows"));
            Assert.That(shader, Does.Not.Contain("mainLight.shadowAttenuation"));
            Assert.That(shader, Does.Contain("TEXTURE2D(_MmdSelfShadowMap)"));
            Assert.That(shader, Does.Contain("SAMPLER(sampler_MmdSelfShadowMap)"));
            Assert.That(shader, Does.Contain("SAMPLE_TEXTURE2D(_MmdSelfShadowMap"));
            Assert.That(shader, Does.Not.Contain("_MmdSelfShadowReceive <= 0.5h || _MmdReceiveShadows"));
            Assert.That(shader, Does.Contain("SampleMmdSelfShadow"));
            Assert.That(shader, Does.Contain("UNITY_REVERSED_Z"));
            Assert.That(shader, Does.Contain("half shadowAttenuation = SampleMmdSelfShadow(input.positionWS, selfShadowReceive);"));
        }
    }
}
