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
                Assert.That(target.TryGetActiveProjectionState(out MmdSelfShadowProjectionState noEnvironmentState), Is.False);
                Assert.That(noEnvironmentState.Active, Is.False);
                AssertReceiverGate(renderer, expectedSelfShadowReceive: 0.0f);

                target.SelfShadowEnabled = false;
                AssertReceiverGate(renderer, expectedSelfShadowReceive: 0.0f);
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
            MmdSceneEnvironmentBinding? environment = null;
            GameObject? oldCube = null;
            GameObject? newCube = null;
            try
            {
                environment = CreateRecordedEnvironment("environment");
                oldCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                newCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Object.DestroyImmediate(oldCube.GetComponent<Collider>());
                Object.DestroyImmediate(newCube.GetComponent<Collider>());
                oldCube.transform.SetParent(oldRoot.transform, worldPositionStays: false);
                newCube.transform.SetParent(newRoot.transform, worldPositionStays: false);

                MmdSelfShadowTarget target = targetGo.AddComponent<MmdSelfShadowTarget>();
                target.SceneEnvironment = environment;
                target.BoundsRoot = oldRoot.transform;

                Renderer oldRenderer = oldCube.GetComponent<Renderer>();
                Renderer newRenderer = newCube.GetComponent<Renderer>();
                AssertReceiverGate(oldRenderer, expectedSelfShadowReceive: 1.0f);

                target.BoundsRoot = newRoot.transform;

                AssertReceiverGate(oldRenderer, expectedSelfShadowReceive: 0.0f);
                AssertReceiverGate(newRenderer, expectedSelfShadowReceive: 1.0f);
            }
            finally
            {
                Object.DestroyImmediate(newCube);
                Object.DestroyImmediate(oldCube);
                Object.DestroyImmediate(newRoot);
                Object.DestroyImmediate(oldRoot);
                if (environment != null)
                {
                    Object.DestroyImmediate(environment.gameObject);
                }
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
                AssertReceiverGate(renderer, expectedSelfShadowReceive: 0.0f);

                environment.ApplySelfShadowState(new MmdSelfShadowState(0, 0.4f));
                Assert.That(target.TryGetActiveProjectionState(out MmdSelfShadowProjectionState inactiveState), Is.False);
                Assert.That(inactiveState.Active, Is.False);
                target.RefreshReceiverGate();
                AssertReceiverGate(renderer, expectedSelfShadowReceive: 0.0f);

                environment.ApplySelfShadowState(new MmdSelfShadowState(2, 0.4f));
                Assert.That(target.TryGetActiveProjectionState(out MmdSelfShadowProjectionState activeState), Is.True);
                Assert.That(activeState.Mode, Is.EqualTo(2));
                Assert.That(activeState.FarDistance, Is.EqualTo(4.0f).Within(0.001f));
                Assert.That(activeState.BoundsPadding, Is.EqualTo(0.25f).Within(0.001f));
                target.RefreshReceiverGate();
                AssertReceiverGate(renderer, expectedSelfShadowReceive: 1.0f);

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
                AssertReceiverGate(renderer, expectedSelfShadowReceive: 0.0f);
            }
            finally
            {
                Object.DestroyImmediate(cube);
                Object.DestroyImmediate(environmentGo);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void UnboundTargetUsesOnlySingleRecordedSceneEnvironment()
        {
            var root = new GameObject("mmd-root");
            MmdSceneEnvironmentBinding? environment = null;
            try
            {
                MmdSelfShadowTarget target = root.AddComponent<MmdSelfShadowTarget>();
                environment = CreateRecordedEnvironment("environment", mode: 2, distance: 0.4f);

                Assert.That(target.SceneEnvironment, Is.Null);
                Assert.That(target.TryGetActiveProjectionState(out MmdSelfShadowProjectionState state), Is.True);
                Assert.That(state.Mode, Is.EqualTo(2));
                Assert.That(state.FarDistance, Is.EqualTo(40.0f).Within(0.001f));
            }
            finally
            {
                if (environment != null)
                {
                    Object.DestroyImmediate(environment.gameObject);
                }
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void UnboundTargetDoesNotUseAmbiguousSceneEnvironments()
        {
            var root = new GameObject("mmd-root");
            MmdSceneEnvironmentBinding? firstEnvironment = null;
            MmdSceneEnvironmentBinding? secondEnvironment = null;
            try
            {
                MmdSelfShadowTarget target = root.AddComponent<MmdSelfShadowTarget>();
                firstEnvironment = CreateRecordedEnvironment("environment-a", mode: 1, distance: 0.4f);
                secondEnvironment = CreateRecordedEnvironment("environment-b", mode: 2, distance: 0.8f);

                Assert.That(target.SceneEnvironment, Is.Null);
                Assert.That(target.TryGetActiveProjectionState(out MmdSelfShadowProjectionState state), Is.False);
                Assert.That(state.Active, Is.False);
            }
            finally
            {
                if (secondEnvironment != null)
                {
                    Object.DestroyImmediate(secondEnvironment.gameObject);
                }
                if (firstEnvironment != null)
                {
                    Object.DestroyImmediate(firstEnvironment.gameObject);
                }
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void SelfShadowRenderPassUsesSceneEnvironmentLightDirectionWhenAvailable()
        {
            var root = new GameObject("mmd-root");
            var environmentGo = new GameObject("environment");
            GameObject? cube = null;
            Material? material = null;
            try
            {
                MmdSelfShadowTarget target = root.AddComponent<MmdSelfShadowTarget>();
                cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Object.DestroyImmediate(cube.GetComponent<Collider>());
                cube.transform.SetParent(root.transform, worldPositionStays: false);

                Shader shader = Shader.Find("MMD Basic URP Toon");
                Assert.That(shader, Is.Not.Null);
                material = new Material(shader);
                cube.GetComponent<Renderer>().sharedMaterial = material;

                MmdSceneEnvironmentBinding environment = environmentGo.AddComponent<MmdSceneEnvironmentBinding>();
                environment.ApplyLightState(new MmdLightState(
                    new[] { 1.0f, 1.0f, 1.0f },
                    new[] { -0.25f, -1.0f, 0.75f }));
                environment.ApplySelfShadowState(new MmdSelfShadowState(1, 0.5f));
                target.SceneEnvironment = environment;

                System.Type passType = System.Type.GetType(
                    "Mmd.Rendering.Universal.MmdSelfShadowRenderPass, Mmd.Rendering.Universal",
                    throwOnError: true)!;
                object pass = System.Activator.CreateInstance(passType)!;
                System.Reflection.MethodInfo setup = passType.GetMethod(
                    "Setup",
                    new[] { typeof(int), typeof(Vector3), typeof(float) })!;
                Assert.That((bool)setup.Invoke(pass, new object[] { 512, new Vector3(0.35f, -1.0f, 0.35f), 0.0025f }),
                    Is.True);

                Vector3 expectedDirection = MmdCoordinateSpace.MmdToUnityPosition(
                    new Vector3(-0.25f, -1.0f, 0.75f)).normalized;
                Vector4 actualDirection = GetPassLightDirection(passType, pass);

                Assert.That(Vector3.Dot(new Vector3(actualDirection.x, actualDirection.y, actualDirection.z), expectedDirection),
                    Is.EqualTo(1.0f).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(material);
                Object.DestroyImmediate(cube);
                Object.DestroyImmediate(environmentGo);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void SelfShadowRenderPassPrefersExplicitSelfShadowLightDirection()
        {
            var root = new GameObject("mmd-root");
            var environmentGo = new GameObject("environment");
            var selfShadowLightGo = new GameObject("self-shadow-light");
            var targetLightGo = new GameObject("target-light");
            GameObject? cube = null;
            Material? material = null;
            try
            {
                MmdSelfShadowTarget target = root.AddComponent<MmdSelfShadowTarget>();
                cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Object.DestroyImmediate(cube.GetComponent<Collider>());
                cube.transform.SetParent(root.transform, worldPositionStays: false);

                Shader shader = Shader.Find("MMD Basic URP Toon");
                Assert.That(shader, Is.Not.Null);
                material = new Material(shader);
                cube.GetComponent<Renderer>().sharedMaterial = material;

                Light selfShadowLight = selfShadowLightGo.AddComponent<Light>();
                selfShadowLight.type = LightType.Directional;
                selfShadowLight.transform.rotation = Quaternion.LookRotation(Vector3.right);
                Light targetLight = targetLightGo.AddComponent<Light>();
                targetLight.type = LightType.Directional;
                targetLight.transform.rotation = Quaternion.LookRotation(Vector3.left);

                MmdSceneEnvironmentBinding environment = environmentGo.AddComponent<MmdSceneEnvironmentBinding>();
                environment.TargetLight = targetLight;
                environment.SelfShadowDirectionLight = selfShadowLight;
                environment.ApplySelfShadowState(new MmdSelfShadowState(1, 0.5f));
                target.SceneEnvironment = environment;

                System.Type passType = System.Type.GetType(
                    "Mmd.Rendering.Universal.MmdSelfShadowRenderPass, Mmd.Rendering.Universal",
                    throwOnError: true)!;
                object pass = System.Activator.CreateInstance(passType)!;
                System.Reflection.MethodInfo setup = passType.GetMethod(
                    "Setup",
                    new[] { typeof(int), typeof(Vector3), typeof(float) })!;
                Assert.That((bool)setup.Invoke(pass, new object[] { 512, Vector3.down, 0.0025f }),
                    Is.True);

                Vector4 actualDirection = GetPassLightDirection(passType, pass);

                Assert.That(Vector3.Dot(new Vector3(actualDirection.x, actualDirection.y, actualDirection.z), Vector3.right),
                    Is.EqualTo(1.0f).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(material);
                Object.DestroyImmediate(cube);
                Object.DestroyImmediate(targetLightGo);
                Object.DestroyImmediate(selfShadowLightGo);
                Object.DestroyImmediate(environmentGo);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void SelfShadowRenderPassUsesRenderSettingsSunBeforeSerializedFallbackDirection()
        {
            Light? previousSun = RenderSettings.sun;
            var root = new GameObject("mmd-root");
            var sunGo = new GameObject("scene-sun");
            MmdSceneEnvironmentBinding? environment = null;
            GameObject? cube = null;
            Material? material = null;
            try
            {
                environment = CreateRecordedEnvironment("environment");
                MmdSelfShadowTarget target = root.AddComponent<MmdSelfShadowTarget>();
                cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Object.DestroyImmediate(cube.GetComponent<Collider>());
                cube.transform.SetParent(root.transform, worldPositionStays: false);

                Shader shader = Shader.Find("MMD Basic URP Toon");
                Assert.That(shader, Is.Not.Null);
                material = new Material(shader);
                cube.GetComponent<Renderer>().sharedMaterial = material;

                Light sun = sunGo.AddComponent<Light>();
                sun.type = LightType.Directional;
                sun.transform.rotation = Quaternion.LookRotation(Vector3.left);
                RenderSettings.sun = sun;

                System.Type passType = System.Type.GetType(
                    "Mmd.Rendering.Universal.MmdSelfShadowRenderPass, Mmd.Rendering.Universal",
                    throwOnError: true)!;
                object pass = System.Activator.CreateInstance(passType)!;
                System.Reflection.MethodInfo setup = passType.GetMethod(
                    "Setup",
                    new[] { typeof(int), typeof(Vector3), typeof(float) })!;
                Assert.That((bool)setup.Invoke(pass, new object[] { 512, Vector3.right, 0.0025f }),
                    Is.True);

                Vector4 actualDirection = GetPassLightDirection(passType, pass);

                Assert.That(Vector3.Dot(new Vector3(actualDirection.x, actualDirection.y, actualDirection.z), Vector3.left),
                    Is.EqualTo(1.0f).Within(0.001f));
            }
            finally
            {
                RenderSettings.sun = previousSun;
                Object.DestroyImmediate(material);
                Object.DestroyImmediate(cube);
                Object.DestroyImmediate(sunGo);
                if (environment != null)
                {
                    Object.DestroyImmediate(environment.gameObject);
                }
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void SelfShadowRenderPassUsesUnityMinusZViewConventionForShadowDirection()
        {
            Light? previousSun = RenderSettings.sun;
            var root = new GameObject("mmd-root");
            var unusableSun = new GameObject("unusable-scene-sun");
            MmdSceneEnvironmentBinding? environment = null;
            GameObject? cube = null;
            Material? material = null;
            List<MmdSelfShadowTarget>? activeTargets = null;
            List<MmdSelfShadowTarget>? previousActiveTargets = null;
            try
            {
                Light blocker = unusableSun.AddComponent<Light>();
                blocker.type = LightType.Point;
                unusableSun.SetActive(false);
                RenderSettings.sun = blocker;
                environment = CreateRecordedEnvironment("environment");
                MmdSelfShadowTarget target = root.AddComponent<MmdSelfShadowTarget>();
                target.SceneEnvironment = environment;
                activeTargets = GetActiveTargetRegistry();
                previousActiveTargets = new List<MmdSelfShadowTarget>(activeTargets);
                activeTargets.Clear();
                activeTargets.Add(target);
                cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Object.DestroyImmediate(cube.GetComponent<Collider>());
                cube.transform.SetParent(root.transform, worldPositionStays: false);

                Shader shader = Shader.Find("MMD Basic URP Toon");
                Assert.That(shader, Is.Not.Null);
                material = new Material(shader);
                cube.GetComponent<Renderer>().sharedMaterial = material;

                System.Type passType = System.Type.GetType(
                    "Mmd.Rendering.Universal.MmdSelfShadowRenderPass, Mmd.Rendering.Universal",
                    throwOnError: true)!;
                object pass = System.Activator.CreateInstance(passType)!;
                System.Reflection.MethodInfo setup = passType.GetMethod(
                    "Setup",
                    new[] { typeof(int), typeof(Vector3), typeof(float) })!;
                Assert.That((bool)setup.Invoke(pass, new object[] { 512, Vector3.forward, 0.0025f }),
                    Is.True);

                Matrix4x4 view = GetPassMatrix(passType, pass, "viewMatrix");
                Vector3 viewForward = view.MultiplyVector(Vector3.forward).normalized;
                Assert.That(viewForward.x, Is.EqualTo(0.0f).Within(0.001f));
                Assert.That(viewForward.y, Is.EqualTo(0.0f).Within(0.001f));
                Assert.That(viewForward.z, Is.EqualTo(-1.0f).Within(0.001f));

                Matrix4x4 worldToShadow = GetPassMatrix(passType, pass, "worldToShadow");
                Vector3 nearLightFace = worldToShadow.MultiplyPoint(Vector3.back * 0.5f);
                Vector3 farLightFace = worldToShadow.MultiplyPoint(Vector3.forward * 0.5f);

                Assert.That(nearLightFace.z, Is.InRange(-0.001f, 1.001f));
                Assert.That(farLightFace.z, Is.InRange(-0.001f, 1.001f));
                if (SystemInfo.usesReversedZBuffer)
                {
                    Assert.That(nearLightFace.z, Is.GreaterThan(farLightFace.z));
                }
                else
                {
                    Assert.That(nearLightFace.z, Is.LessThan(farLightFace.z));
                }
            }
            finally
            {
                RenderSettings.sun = previousSun;
                if (activeTargets != null && previousActiveTargets != null)
                {
                    activeTargets.Clear();
                    for (int i = 0; i < previousActiveTargets.Count; i++)
                    {
                        if (previousActiveTargets[i] != null)
                        {
                            activeTargets.Add(previousActiveTargets[i]);
                        }
                    }
                }

                Object.DestroyImmediate(material);
                Object.DestroyImmediate(cube);
                if (environment != null)
                {
                    Object.DestroyImmediate(environment.gameObject);
                }
                Object.DestroyImmediate(unusableSun);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void SelfShadowRenderPassFitsDepthRangeToBoundsAndNormalizesWorldBias()
        {
            var root = new GameObject("mmd-root");
            var environmentGo = new GameObject("environment");
            GameObject? cube = null;
            Material? material = null;
            try
            {
                MmdSelfShadowTarget target = root.AddComponent<MmdSelfShadowTarget>();
                cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Object.DestroyImmediate(cube.GetComponent<Collider>());
                cube.transform.SetParent(root.transform, worldPositionStays: false);

                Shader shader = Shader.Find("MMD Basic URP Toon");
                Assert.That(shader, Is.Not.Null);
                material = new Material(shader);
                cube.GetComponent<Renderer>().sharedMaterial = material;

                MmdSceneEnvironmentBinding environment = environmentGo.AddComponent<MmdSceneEnvironmentBinding>();
                environment.SelfShadowProjectionPolicy = new MmdSelfShadowProjectionPolicy(
                    distanceScale: 100.0f,
                    minFarDistance: 100.0f,
                    maxFarDistance: 100.0f);
                environment.ApplySelfShadowState(new MmdSelfShadowState(1, 1.0f));
                target.SceneEnvironment = environment;
                Assert.That(target.TryGetActiveProjectionState(out MmdSelfShadowProjectionState state), Is.True);
                Assert.That(state.FarDistance, Is.EqualTo(100.0f).Within(0.001f));

                System.Type passType = System.Type.GetType(
                    "Mmd.Rendering.Universal.MmdSelfShadowRenderPass, Mmd.Rendering.Universal",
                    throwOnError: true)!;
                object pass = System.Activator.CreateInstance(passType)!;
                System.Reflection.MethodInfo setup = passType.GetMethod(
                    "Setup",
                    new[] { typeof(int), typeof(Vector3), typeof(float) })!;
                const float worldBias = 0.01f;
                Assert.That((bool)setup.Invoke(pass, new object[] { 512, Vector3.forward, worldBias }),
                    Is.True);

                Matrix4x4 projection = GetPassMatrix(passType, pass, "projectionMatrix");
                float depthRange = Mathf.Abs(2.0f / projection.m22);
                Vector4 shadowParams = GetPassVector(passType, pass, "shadowParams");

                Assert.That(depthRange, Is.GreaterThan(0.1f));
                Assert.That(depthRange, Is.LessThan(5.0f));
                Assert.That(shadowParams.y, Is.EqualTo(worldBias / depthRange).Within(0.0001f));
            }
            finally
            {
                Object.DestroyImmediate(material);
                Object.DestroyImmediate(cube);
                Object.DestroyImmediate(environmentGo);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void SelfShadowRenderPassUsesRenderSettingsSunOverCustomFallbackDirection()
        {
            var root = new GameObject("mmd-root");
            var lightGo = new GameObject("sun");
            MmdSceneEnvironmentBinding? environment = null;
            GameObject? cube = null;
            Material? material = null;
            Light? previousSun = RenderSettings.sun;
            try
            {
                environment = CreateRecordedEnvironment("environment");
                MmdSelfShadowTarget target = root.AddComponent<MmdSelfShadowTarget>();
                Assert.That(target.SceneEnvironment, Is.Null);

                cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Object.DestroyImmediate(cube.GetComponent<Collider>());
                cube.transform.SetParent(root.transform, worldPositionStays: false);

                Shader shader = Shader.Find("MMD Basic URP Toon");
                Assert.That(shader, Is.Not.Null);
                material = new Material(shader);
                cube.GetComponent<Renderer>().sharedMaterial = material;

                Light sun = lightGo.AddComponent<Light>();
                sun.type = LightType.Directional;
                sun.transform.rotation = Quaternion.LookRotation(Vector3.right);
                RenderSettings.sun = sun;

                System.Type passType = System.Type.GetType(
                    "Mmd.Rendering.Universal.MmdSelfShadowRenderPass, Mmd.Rendering.Universal",
                    throwOnError: true)!;
                object pass = System.Activator.CreateInstance(passType)!;
                System.Reflection.MethodInfo setup = passType.GetMethod(
                    "Setup",
                    new[] { typeof(int), typeof(Vector3), typeof(float) })!;
                Assert.That((bool)setup.Invoke(pass, new object[] { 512, Vector3.left, 0.0025f }),
                    Is.True);

                Vector4 actualDirection = GetPassLightDirection(passType, pass);

                Assert.That(Vector3.Dot(new Vector3(actualDirection.x, actualDirection.y, actualDirection.z), Vector3.right),
                    Is.EqualTo(1.0f).Within(0.001f));
            }
            finally
            {
                RenderSettings.sun = previousSun;
                Object.DestroyImmediate(material);
                Object.DestroyImmediate(cube);
                Object.DestroyImmediate(lightGo);
                if (environment != null)
                {
                    Object.DestroyImmediate(environment.gameObject);
                }
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void TargetClearsReceiverGateWhenDisabled()
        {
            var root = new GameObject("mmd-root");
            MmdSceneEnvironmentBinding? environment = null;
            GameObject? cube = null;
            try
            {
                environment = CreateRecordedEnvironment("environment");
                cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Object.DestroyImmediate(cube.GetComponent<Collider>());
                cube.transform.SetParent(root.transform, worldPositionStays: false);
                Renderer renderer = cube.GetComponent<Renderer>();

                MmdSelfShadowTarget target = root.AddComponent<MmdSelfShadowTarget>();
                target.SceneEnvironment = environment;
                target.RefreshReceiverGate();
                AssertReceiverGate(renderer, expectedSelfShadowReceive: 1.0f);

                target.enabled = false;

                AssertReceiverGate(renderer, expectedSelfShadowReceive: 0.0f);
            }
            finally
            {
                Object.DestroyImmediate(cube);
                if (environment != null)
                {
                    Object.DestroyImmediate(environment.gameObject);
                }
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void DisableAllReceiverGatesClearsSelfShadowSuppression()
        {
            var root = new GameObject("mmd-root");
            MmdSceneEnvironmentBinding? environment = null;
            GameObject? cube = null;
            try
            {
                environment = CreateRecordedEnvironment("environment");
                cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Object.DestroyImmediate(cube.GetComponent<Collider>());
                cube.transform.SetParent(root.transform, worldPositionStays: false);
                Renderer renderer = cube.GetComponent<Renderer>();

                MmdSelfShadowTarget target = root.AddComponent<MmdSelfShadowTarget>();
                target.SceneEnvironment = environment;
                target.RefreshReceiverGate();
                AssertReceiverGate(renderer, expectedSelfShadowReceive: 1.0f);

                MmdSelfShadowTarget.DisableAllReceiverGates();

                AssertReceiverGate(renderer, expectedSelfShadowReceive: 0.0f);
            }
            finally
            {
                Object.DestroyImmediate(cube);
                if (environment != null)
                {
                    Object.DestroyImmediate(environment.gameObject);
                }
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void DisableAllReceiverGatesPreservesUnrelatedRendererPropertyBlock()
        {
            var root = new GameObject("mmd-root");
            MmdSceneEnvironmentBinding? environment = null;
            GameObject? cube = null;
            try
            {
                environment = CreateRecordedEnvironment("environment");
                cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Object.DestroyImmediate(cube.GetComponent<Collider>());
                cube.transform.SetParent(root.transform, worldPositionStays: false);
                Renderer renderer = cube.GetComponent<Renderer>();
                var propertyBlock = new MaterialPropertyBlock();
                propertyBlock.SetFloat(Shader.PropertyToID("_UnrelatedRendererValue"), 0.25f);
                renderer.SetPropertyBlock(propertyBlock);

                MmdSelfShadowTarget target = root.AddComponent<MmdSelfShadowTarget>();
                target.SceneEnvironment = environment;
                target.RefreshReceiverGate();
                AssertReceiverGate(renderer, expectedSelfShadowReceive: 1.0f);

                MmdSelfShadowTarget.DisableAllReceiverGates();

                AssertReceiverGate(renderer, expectedSelfShadowReceive: 0.0f);
                AssertPropertyBlockFloat(renderer, Shader.PropertyToID("_UnrelatedRendererValue"), 0.25f);
            }
            finally
            {
                Object.DestroyImmediate(cube);
                if (environment != null)
                {
                    Object.DestroyImmediate(environment.gameObject);
                }
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void TargetDoesNotEnableSelfShadowReceiveBeforeRendererFeatureEnablesReceiverGate()
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

                AssertReceiverGate(renderer, expectedSelfShadowReceive: 0.0f);
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

                Assert.That((float)shadowDepthBiasProperty.GetValue(feature), Is.EqualTo(0.02f).Within(0.000001f));

                shadowDepthBiasProperty.SetValue(feature, 0.025f);
                Assert.That((float)shadowDepthBiasProperty.GetValue(feature), Is.EqualTo(0.025f).Within(0.000001f));

                shadowDepthBiasProperty.SetValue(feature, -1.0f);
                Assert.That((float)shadowDepthBiasProperty.GetValue(feature), Is.EqualTo(0.0f));

                shadowDepthBiasProperty.SetValue(feature, float.NaN);
                Assert.That((float)shadowDepthBiasProperty.GetValue(feature), Is.EqualTo(0.0f));

                shadowDepthBiasProperty.SetValue(feature, float.PositiveInfinity);
                Assert.That((float)shadowDepthBiasProperty.GetValue(feature), Is.EqualTo(0.0f));

                shadowDepthBiasProperty.SetValue(feature, 1.0f);
                Assert.That((float)shadowDepthBiasProperty.GetValue(feature), Is.EqualTo(0.5f));
            }
            finally
            {
                if (feature != null)
                {
                    Object.DestroyImmediate(feature);
                }
            }
        }

        private static MmdSceneEnvironmentBinding CreateRecordedEnvironment(
            string name,
            int mode = 1,
            float distance = 0.5f)
        {
            var go = new GameObject(name);
            MmdSceneEnvironmentBinding environment = go.AddComponent<MmdSceneEnvironmentBinding>();
            environment.ApplySelfShadowState(new MmdSelfShadowState((byte)mode, distance));
            return environment;
        }

        private static List<MmdSelfShadowTarget> GetActiveTargetRegistry()
        {
            System.Reflection.FieldInfo field = typeof(MmdSelfShadowTarget).GetField(
                "ActiveTargets",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
            return (List<MmdSelfShadowTarget>)field.GetValue(null)!;
        }

        private static void AssertReceiverGate(
            Renderer renderer,
            float expectedSelfShadowReceive)
        {
            var propertyBlock = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(propertyBlock);
            Assert.That(propertyBlock.GetFloat(MmdSelfShadowTarget.MmdSelfShadowReceiveId), Is.EqualTo(expectedSelfShadowReceive));
        }

        private static void AssertPropertyBlockFloat(Renderer renderer, int propertyId, float expected)
        {
            var propertyBlock = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(propertyBlock);
            Assert.That(propertyBlock.GetFloat(propertyId), Is.EqualTo(expected));
        }

        private static Vector4 GetPassLightDirection(System.Type passType, object pass)
        {
            System.Reflection.FieldInfo field = passType.GetField(
                "lightDirection",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
            return (Vector4)field.GetValue(pass);
        }

        private static Matrix4x4 GetPassMatrix(System.Type passType, object pass, string fieldName)
        {
            System.Reflection.FieldInfo field = passType.GetField(
                fieldName,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
            return (Matrix4x4)field.GetValue(pass);
        }

        private static Vector4 GetPassVector(System.Type passType, object pass, string fieldName)
        {
            System.Reflection.FieldInfo field = passType.GetField(
                fieldName,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
            return (Vector4)field.GetValue(pass);
        }

        [Test]
        public void SelfShadowRendererAndShaderUseDedicatedGlobals()
        {
            string runtimeRoot = Path.Combine(MmdTestFixtures.PackageRoot, "Runtime");
            string target = File.ReadAllText(Path.Combine(runtimeRoot, "Components", "Scene", "MmdSelfShadowTarget.cs"));
            string feature = File.ReadAllText(Path.Combine(runtimeRoot, "Rendering", "Universal", "MmdSelfShadowRendererFeature.cs"));
            string pass = File.ReadAllText(Path.Combine(runtimeRoot, "Rendering", "Universal", "MmdSelfShadowRenderPass.cs"));
            string shader = File.ReadAllText(Path.Combine(runtimeRoot, "Shaders", "MmdBasicUrpToon.shader"));

            Assert.That(target, Does.Contain("[AddComponentMenu(\"\")]"));
            Assert.That(target, Does.Contain("EnsureHiddenTarget"));
            Assert.That(target, Does.Contain("HideFlags.HideInInspector"));
            Assert.That(target, Does.Contain("SetFloat(MmdSelfShadowReceiveId, selfShadowValue)"));
            Assert.That(target, Does.Contain("MmdSceneEnvironmentBinding? resolvedEnvironment = null;"));
            Assert.That(target, Does.Contain("return resolvedEnvironment;"));
            Assert.That(target, Does.Not.Contain("MmdSuppressStandardShadowsId"));
            Assert.That(target, Does.Not.Contain("MmdReceiveShadowsId"));
            Assert.That(target, Does.Not.Contain("SetFloat(MmdSuppressStandardShadowsId"));
            Assert.That(target, Does.Not.Contain("SetFloat(MmdReceiveShadowsId"));
            Assert.That(target, Does.Not.Contain("StandardReceiveRestoreValues"));
            Assert.That(feature, Does.Contain("MmdSelfShadowRenderPass"));
            Assert.That(feature, Does.Not.Contain("QualitySettings"));
            Assert.That(feature, Does.Contain("SetReceiverGateAvailableForRendering(true)"));
            Assert.That(feature, Does.Contain("SetReceiverGateAvailableForRendering(false)"));
            Assert.That(feature, Does.Contain("DisableAllReceiverGates"));
            Assert.That(feature, Does.Contain("Dispose(bool disposing)"));
            Assert.That(feature, Does.Contain("shadowDepthBias = DefaultShadowDepthBias"));
            Assert.That(feature, Does.Contain("World-space MMD self-shadow depth bias in meters"));
            Assert.That(feature, Does.Not.Contain("debugDepthPreview"));
            Assert.That(feature, Does.Not.Contain("DebugDepthPreview"));
            Assert.That(feature, Does.Not.Contain("DebugDepthPreviewContrast"));
            Assert.That(feature, Does.Not.Contain("debugVisibilityPreview"));
            Assert.That(feature, Does.Not.Contain("DebugVisibilityPreview"));
            Assert.That(feature, Does.Not.Contain("Hidden/MMD/SelfShadowDepthDebug"));
            Assert.That(feature, Does.Not.Contain("CoreUtils.CreateEngineMaterial"));
            Assert.That(feature, Does.Not.Contain("debugDepthPreviewMaterial"));
            Assert.That(feature, Does.Contain("DefaultShadowDirection"));
            Assert.That(feature, Does.Contain("Fallback self-shadow direction"));
            Assert.That(feature, Does.Contain("public float ShadowDepthBias"));
            Assert.That(pass, Does.Contain("_MmdSelfShadowMap"));
            Assert.That(pass, Does.Not.Contain("_MmdSelfShadowDebugDepth"));
            Assert.That(pass, Does.Contain("_MmdSelfShadowWorldToShadow"));
            Assert.That(pass, Does.Contain("_MmdSelfShadowParams"));
            Assert.That(pass, Does.Contain("\"MMD Self Shadow Pass\""));
            Assert.That(pass, Does.Contain("name = \"MMD Self Shadow Map\""));
            Assert.That(pass, Does.Contain("colorFormat = GraphicsFormat.R32_SFloat"));
            Assert.That(pass, Does.Not.Contain("isShadowMap = true"));
            Assert.That(pass, Does.Contain("name = \"MMD Self Shadow Depth Test\""));
            Assert.That(pass, Does.Not.Contain("MMD Self Shadow Debug Depth Preview"));
            Assert.That(pass, Does.Not.Contain("MMD Self Shadow Debug Depth"));
            Assert.That(pass, Does.Not.Contain("SetGlobalTextureAfterPass(debugDepth"));
            Assert.That(pass, Does.Not.Contain("DrawProcedural(Matrix4x4.identity"));
            Assert.That(pass, Does.Contain("ResolveShadowDirection"));
            Assert.That(pass, Does.Contain("TryGetSelfShadowLightDirection"));
            Assert.That(pass, Does.Contain("RenderSettings.sun"));
            Assert.That(pass, Does.Not.Contain("FindObjectsByType<Light>"));
            Assert.That(pass, Does.Contain("Setup(int requestedMapSize, Vector3 requestedShadowDirection)"));
            Assert.That(pass, Does.Contain("MmdSelfShadowRendererFeature.DefaultShadowDepthBias"));
            Assert.That(pass, Does.Contain("Setup(int requestedMapSize, Vector3 requestedShadowDirection, float requestedShadowDepthBias)"));
            Assert.That(pass, Does.Not.Contain("bool debugDepthPreview"));
            Assert.That(pass, Does.Not.Contain("bool debugVisibilityPreview"));
            Assert.That(pass, Does.Not.Contain("debugVisibilityPreview"));
            Assert.That(pass, Does.Not.Contain("debugDepthPreview"));
            Assert.That(pass, Does.Contain("NormalizeShadowDepthBias(shadowDepthBias, far - near)"));
            Assert.That(pass, Does.Contain("parameters = new Vector4(1.0f, normalizedShadowDepthBias, 0.0f, 0.0f)"));
            Assert.That(pass, Does.Not.Contain("1.0f / Mathf.Max(1, shadowMapSize)"));
            Assert.That(pass, Does.Not.Contain("0.0025f"));
            Assert.That(pass, Does.Contain("SystemInfo.usesReversedZBuffer"));
            Assert.That(pass, Does.Contain("ClearDepth"));
            Assert.That(pass, Does.Contain("passData.ClearDepth = 1.0f;"));
            Assert.That(pass, Does.Contain("passData.ClearColor = SystemInfo.usesReversedZBuffer ? Color.black : Color.white;"));
            Assert.That(pass, Does.Not.Contain("passData.ClearDepth = SystemInfo.usesReversedZBuffer ? 0.0f : 1.0f;"));
            Assert.That(pass, Does.Contain("TryGetActiveProjectionState"));
            Assert.That(pass, Does.Not.Contain("MaxRenderers"));
            Assert.That(pass, Does.Contain("_LightDirection"));
            Assert.That(pass, Does.Contain("_LightPosition"));
            Assert.That(pass, Does.Contain("_ShadowBias"));
            Assert.That(pass, Does.Contain("DisableShaderKeyword(CastingPunctualLightShadowKeyword)"));
            Assert.That(pass, Does.Contain("AllowPassCulling(false)"));
            Assert.That(pass, Does.Contain("SetRenderAttachment(shadowMap, 0, AccessFlags.Write)"));
            Assert.That(pass, Does.Contain("SetRenderAttachmentDepth(shadowDepth, AccessFlags.ReadWrite)"));
            Assert.That(pass, Does.Contain("FindPass(\"MmdSelfShadowCaster\")"));
            Assert.That(pass, Does.Not.Contain("FindPass(\"ShadowCaster\")"));
            Assert.That(pass, Does.Contain("MmdSelfShadowTarget.CollectActiveTargets"));
            Assert.That(shader, Does.Contain("[PerRendererData] [HideInInspector] _MmdSelfShadowReceive"));
            Assert.That(shader, Does.Not.Contain("_MmdReceiveShadows"));
            Assert.That(shader, Does.Not.Contain("_MmdSuppressStandardShadows"));
            Assert.That(shader, Does.Contain("#pragma multi_compile_instancing"));
            Assert.That(shader, Does.Contain("UNITY_INSTANCING_BUFFER_START(MmdPerRenderer)"));
            Assert.That(shader, Does.Contain("UNITY_DEFINE_INSTANCED_PROP(float, _MmdSelfShadowReceive)"));
            Assert.That(shader, Does.Contain("UNITY_ACCESS_INSTANCED_PROP(MmdPerRenderer, _MmdSelfShadowReceive)"));
            Assert.That(shader, Does.Not.Contain("_MAIN_LIGHT_SHADOWS"));
            Assert.That(shader, Does.Not.Contain("_SHADOWS_SOFT"));
            Assert.That(shader, Does.Contain("URP standard shadows are only for casting MMD characters onto the scene/environment."));
            Assert.That(shader, Does.Contain("ForwardLit intentionally does not sample URP main-light shadow attenuation."));
            Assert.That(shader, Does.Not.Contain("effectiveReceiveShadows"));
            Assert.That(shader, Does.Not.Contain("mainLight.shadowAttenuation"));
            Assert.That(shader, Does.Contain("TEXTURE2D(_MmdSelfShadowMap)"));
            Assert.That(shader, Does.Contain("SAMPLER(sampler_MmdSelfShadowMap)"));
            Assert.That(shader, Does.Contain("half sampledDepth = SAMPLE_TEXTURE2D(_MmdSelfShadowMap, sampler_MmdSelfShadowMap, shadowCoord.xy).r;"));
            Assert.That(shader, Does.Contain("return ComputeMmdSelfShadowVisibility(shadowCoord.z, sampledDepth);"));
            Assert.That(shader, Does.Contain("return 1.0h - saturate(occluderDepthDelta * 1500.0h - 0.3h);"));
            Assert.That(shader, Does.Not.Contain("LOAD_TEXTURE2D(_MmdSelfShadowMap"));
            Assert.That(shader, Does.Not.Contain("_MmdSelfShadowReceive <= 0.5h || _MmdReceiveShadows"));
            Assert.That(shader, Does.Contain("SampleMmdSelfShadow"));
            Assert.That(shader, Does.Contain("UNITY_REVERSED_Z"));
            Assert.That(shader, Does.Contain("ComputeMmdSelfShadowVisibility"));
            Assert.That(shader, Does.Contain("Name \"MmdSelfShadowCaster\""));
            Assert.That(shader, Does.Contain("ColorMask R"));
            Assert.That(shader, Does.Contain("float shadowDepth = input.shadowCoord.z / max(input.shadowCoord.w, 1e-5);"));
            Assert.That(shader, Does.Contain("half selfShadowVisibility = SampleMmdSelfShadow(input.positionWS, selfShadowReceive);"));
            Assert.That(shader, Does.Not.Contain("if (_MmdSelfShadowParams.w > 0.5)"));
            Assert.That(shader, Does.Not.Contain("return half4(selfShadowVisibility.xxx, 1.0h);"));
            Assert.That(shader, Does.Contain("half lightVisibility = saturate(dot(normalWS, lightDirection) * 3.0h);"));
            Assert.That(shader, Does.Contain("half toonVisibility = min(selfShadowVisibility, lightVisibility);"));
            Assert.That(shader, Does.Contain("half3 fallbackToonColor = half3(1.0h, 1.0h, 1.0h);"));
            Assert.That(shader, Does.Contain("half3 mappedToonColor = SAMPLE_TEXTURE2D(_ToonMap, sampler_ToonMap, float2(0.5, 0.22)).rgb;"));
            Assert.That(shader, Does.Contain("half3 mmdToonLight = lerp(toonColor, half3(1.0h, 1.0h, 1.0h), toonVisibility);"));
            Assert.That(shader, Does.Not.Contain("lerp(0.55h.xxx, 1.0h.xxx, selfShadowVisibility)"));
            Assert.That(shader, Does.Not.Contain("three-mmd-loader"));
        }
    }
}
