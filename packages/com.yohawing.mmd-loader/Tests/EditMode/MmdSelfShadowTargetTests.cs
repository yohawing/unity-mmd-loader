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
            var propertyBlock = new MaterialPropertyBlock();
            try
            {
                MmdSelfShadowTarget target = root.AddComponent<MmdSelfShadowTarget>();
                cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.transform.SetParent(root.transform, worldPositionStays: false);
                cube.transform.position = new Vector3(1, 2, 3);
                cube.transform.localScale = new Vector3(2, 4, 6);
                Object.DestroyImmediate(cube.GetComponent<Collider>());

                MmdSelfShadowBoundsResult bounds = target.CollectBounds();
                target.RefreshReceiverGate();
                MmdSelfShadowProjectionState projectionState =
                    target.ProjectionPolicy.Evaluate(new MmdSelfShadowState(1, 0.5f));
                Renderer renderer = cube.GetComponent<Renderer>();
                renderer.GetPropertyBlock(propertyBlock);

                Assert.That(target.SelfShadowEnabled, Is.True);
                Assert.That(target.ProjectionPolicy.BoundsPadding, Is.EqualTo(MmdSelfShadowProjectionPolicy.Default.BoundsPadding));
                Assert.That(bounds.HasBounds, Is.True);
                Assert.That(bounds.Source, Is.EqualTo(MmdSelfShadowBoundsSource.RendererCollection));
                Assert.That(bounds.Center.x, Is.EqualTo(1.0f).Within(0.001f));
                Assert.That(bounds.Center.y, Is.EqualTo(2.0f).Within(0.001f));
                Assert.That(bounds.Center.z, Is.EqualTo(3.0f).Within(0.001f));
                Assert.That(projectionState.Active, Is.True);
                Assert.That(propertyBlock.GetFloat(MmdSelfShadowTarget.MmdSelfShadowReceiveId), Is.EqualTo(1.0f));

                target.SelfShadowEnabled = false;
                renderer.GetPropertyBlock(propertyBlock);
                Assert.That(propertyBlock.GetFloat(MmdSelfShadowTarget.MmdSelfShadowReceiveId), Is.EqualTo(0.0f));
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
            var propertyBlock = new MaterialPropertyBlock();
            try
            {
                oldCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                newCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                oldCube.transform.SetParent(oldRoot.transform, worldPositionStays: false);
                newCube.transform.SetParent(newRoot.transform, worldPositionStays: false);
                Object.DestroyImmediate(oldCube.GetComponent<Collider>());
                Object.DestroyImmediate(newCube.GetComponent<Collider>());

                MmdSelfShadowTarget target = targetGo.AddComponent<MmdSelfShadowTarget>();
                target.BoundsRoot = oldRoot.transform;

                Renderer oldRenderer = oldCube.GetComponent<Renderer>();
                Renderer newRenderer = newCube.GetComponent<Renderer>();
                oldRenderer.GetPropertyBlock(propertyBlock);
                Assert.That(propertyBlock.GetFloat(MmdSelfShadowTarget.MmdSelfShadowReceiveId), Is.EqualTo(1.0f));
                newRenderer.GetPropertyBlock(propertyBlock);
                Assert.That(propertyBlock.GetFloat(MmdSelfShadowTarget.MmdSelfShadowReceiveId), Is.EqualTo(0.0f));

                target.BoundsRoot = newRoot.transform;

                oldRenderer.GetPropertyBlock(propertyBlock);
                Assert.That(propertyBlock.GetFloat(MmdSelfShadowTarget.MmdSelfShadowReceiveId), Is.EqualTo(0.0f));
                newRenderer.GetPropertyBlock(propertyBlock);
                Assert.That(propertyBlock.GetFloat(MmdSelfShadowTarget.MmdSelfShadowReceiveId), Is.EqualTo(1.0f));
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
            var propertyBlock = new MaterialPropertyBlock();
            try
            {
                MmdSelfShadowTarget target = root.AddComponent<MmdSelfShadowTarget>();
                cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.transform.SetParent(root.transform, worldPositionStays: false);
                Object.DestroyImmediate(cube.GetComponent<Collider>());
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
                renderer.GetPropertyBlock(propertyBlock);
                Assert.That(propertyBlock.GetFloat(MmdSelfShadowTarget.MmdSelfShadowReceiveId), Is.EqualTo(0.0f));

                environment.ApplySelfShadowState(new MmdSelfShadowState(0, 0.4f));
                Assert.That(target.TryGetActiveProjectionState(out MmdSelfShadowProjectionState inactiveState), Is.False);
                Assert.That(inactiveState.Active, Is.False);
                target.RefreshReceiverGate();
                renderer.GetPropertyBlock(propertyBlock);
                Assert.That(propertyBlock.GetFloat(MmdSelfShadowTarget.MmdSelfShadowReceiveId), Is.EqualTo(0.0f));

                environment.ApplySelfShadowState(new MmdSelfShadowState(2, 0.4f));
                Assert.That(target.TryGetActiveProjectionState(out MmdSelfShadowProjectionState activeState), Is.True);
                Assert.That(activeState.Mode, Is.EqualTo(2));
                Assert.That(activeState.FarDistance, Is.EqualTo(4.0f).Within(0.001f));
                Assert.That(activeState.BoundsPadding, Is.EqualTo(0.25f).Within(0.001f));
                target.RefreshReceiverGate();
                renderer.GetPropertyBlock(propertyBlock);
                Assert.That(propertyBlock.GetFloat(MmdSelfShadowTarget.MmdSelfShadowReceiveId), Is.EqualTo(1.0f));

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
                renderer.GetPropertyBlock(propertyBlock);
                Assert.That(propertyBlock.GetFloat(MmdSelfShadowTarget.MmdSelfShadowReceiveId), Is.EqualTo(0.0f));
            }
            finally
            {
                Object.DestroyImmediate(cube);
                Object.DestroyImmediate(environmentGo);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void SelfShadowRendererAndShaderUseDedicatedGlobals()
        {
            string runtimeRoot = Path.Combine(MmdTestFixtures.PackageRoot, "Runtime");
            string feature = File.ReadAllText(Path.Combine(runtimeRoot, "Rendering", "Universal", "MmdSelfShadowRendererFeature.cs"));
            string pass = File.ReadAllText(Path.Combine(runtimeRoot, "Rendering", "Universal", "MmdSelfShadowRenderPass.cs"));
            string shader = File.ReadAllText(Path.Combine(runtimeRoot, "Shaders", "MmdBasicUrpToon.shader"));

            Assert.That(feature, Does.Contain("MmdSelfShadowRenderPass"));
            Assert.That(feature, Does.Not.Contain("QualitySettings"));
            Assert.That(feature, Does.Not.Contain("RenderSettings"));
            Assert.That(feature, Does.Contain("DisableAllReceiverGates"));
            Assert.That(feature, Does.Contain("Dispose(bool disposing)"));
            Assert.That(pass, Does.Contain("_MmdSelfShadowMap"));
            Assert.That(pass, Does.Contain("_MmdSelfShadowWorldToShadow"));
            Assert.That(pass, Does.Contain("_MmdSelfShadowParams"));
            Assert.That(pass, Does.Contain("SystemInfo.usesReversedZBuffer"));
            Assert.That(pass, Does.Contain("ClearDepth"));
            Assert.That(pass, Does.Contain("TryGetActiveProjectionState"));
            Assert.That(pass, Does.Not.Contain("MaxRenderers"));
            Assert.That(pass, Does.Contain("_LightDirection"));
            Assert.That(pass, Does.Contain("_LightPosition"));
            Assert.That(pass, Does.Contain("_ShadowBias"));
            Assert.That(pass, Does.Contain("DisableShaderKeyword(CastingPunctualLightShadowKeyword)"));
            Assert.That(pass, Does.Contain("FindPass(\"ShadowCaster\")"));
            Assert.That(pass, Does.Contain("MmdSelfShadowTarget.CollectActiveTargets"));
            Assert.That(shader, Does.Contain("TEXTURE2D(_MmdSelfShadowMap)"));
            Assert.That(shader, Does.Contain("_MmdSelfShadowReceive"));
            Assert.That(shader, Does.Contain("_MmdReceiveShadows <= 0.5h"));
            Assert.That(shader, Does.Contain("SampleMmdSelfShadow"));
            Assert.That(shader, Does.Contain("UNITY_REVERSED_Z"));
            Assert.That(shader, Does.Contain("shadowAttenuation = min(shadowAttenuation, mmdSelfShadowAttenuation);"));
        }
    }
}
