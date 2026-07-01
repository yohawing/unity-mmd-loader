#nullable enable

using Mmd.Motion;
using Mmd.UnityIntegration;
using NUnit.Framework;
using UnityEngine;

namespace Mmd.Tests
{
    public sealed class MmdSelfShadowBoundsCollectorTests
    {
        [Test]
        public void CollectAggregatesActiveEnabledRendererBoundsUnderRoot()
        {
            var root = new GameObject("mmd-root");
            GameObject? body = null;
            GameObject? hair = null;
            try
            {
                body = CreateCube("body", root.transform, new Vector3(0, 0, 0), new Vector3(2, 2, 2));
                hair = CreateCube("hair", root.transform, new Vector3(4, 0, 0), new Vector3(2, 4, 2));

                MmdSelfShadowBoundsResult result = MmdSelfShadowBoundsCollector.Collect(
                    root,
                    MmdSelfShadowProjectionPolicy.Default);

                Assert.That(result.HasBounds, Is.True);
                Assert.That(result.Source, Is.EqualTo(MmdSelfShadowBoundsSource.RendererCollection));
                Assert.That(result.IncludedRendererCount, Is.EqualTo(2));
                AssertVector(result.Center, new Vector3(2, 0, 0));
                AssertVector(result.Size, new Vector3(6, 4, 2));
            }
            finally
            {
                DestroyNow(hair);
                DestroyNow(body);
                DestroyNow(root);
            }
        }

        [Test]
        public void CollectExcludesDisabledRendererAndInactiveChild()
        {
            var root = new GameObject("mmd-root");
            GameObject? active = null;
            GameObject? disabledRenderer = null;
            GameObject? inactiveChild = null;
            try
            {
                active = CreateCube("active", root.transform, new Vector3(1, 2, 3), new Vector3(2, 4, 6));
                disabledRenderer = CreateCube("disabled-renderer", root.transform, new Vector3(100, 0, 0), Vector3.one);
                disabledRenderer.GetComponent<Renderer>().enabled = false;
                inactiveChild = CreateCube("inactive-child", root.transform, new Vector3(-100, 0, 0), Vector3.one);
                inactiveChild.SetActive(false);

                MmdSelfShadowBoundsResult result = MmdSelfShadowBoundsCollector.Collect(
                    root.transform,
                    MmdSelfShadowProjectionPolicy.Default);

                Assert.That(result.HasBounds, Is.True);
                Assert.That(result.IncludedRendererCount, Is.EqualTo(1));
                AssertVector(result.Center, new Vector3(1, 2, 3));
                AssertVector(result.Size, new Vector3(2, 4, 6));
            }
            finally
            {
                DestroyNow(inactiveChild);
                DestroyNow(disabledRenderer);
                DestroyNow(active);
                DestroyNow(root);
            }
        }

        [Test]
        public void CollectUsesManualOverrideBeforeRendererCollection()
        {
            var root = new GameObject("mmd-root");
            GameObject? rendererObject = null;
            try
            {
                rendererObject = CreateCube("renderer", root.transform, new Vector3(100, 100, 100), Vector3.one);
                var policy = new MmdSelfShadowProjectionPolicy(
                    hasManualBoundsOverride: true,
                    manualBoundsOverride: new MmdSelfShadowProjectionBounds(1, 2, 3, 4, 5, 6));

                MmdSelfShadowBoundsResult result = MmdSelfShadowBoundsCollector.Collect(root, policy);

                Assert.That(result.HasBounds, Is.True);
                Assert.That(result.Source, Is.EqualTo(MmdSelfShadowBoundsSource.ManualOverride));
                Assert.That(result.IncludedRendererCount, Is.EqualTo(0));
                AssertVector(result.Center, new Vector3(1, 2, 3));
                AssertVector(result.Size, new Vector3(4, 5, 6));
            }
            finally
            {
                DestroyNow(rendererObject);
                DestroyNow(root);
            }
        }

        [Test]
        public void CollectWithoutRenderersReturnsNoBounds()
        {
            var root = new GameObject("mmd-root");
            try
            {
                MmdSelfShadowBoundsResult result = MmdSelfShadowBoundsCollector.Collect(
                    root,
                    MmdSelfShadowProjectionPolicy.Default);

                Assert.That(result.HasBounds, Is.False);
                Assert.That(result.Source, Is.EqualTo(MmdSelfShadowBoundsSource.None));
                Assert.That(result.IncludedRendererCount, Is.EqualTo(0));
            }
            finally
            {
                DestroyNow(root);
            }
        }

        [Test]
        public void CollectAppliesPaddingToWorldSize()
        {
            var root = new GameObject("mmd-root");
            GameObject? rendererObject = null;
            try
            {
                rendererObject = CreateCube("renderer", root.transform, new Vector3(0, 0, 0), new Vector3(2, 4, 6));
                var policy = new MmdSelfShadowProjectionPolicy(boundsPadding: 0.5f);

                MmdSelfShadowBoundsResult result = MmdSelfShadowBoundsCollector.Collect(root, policy);

                Assert.That(result.HasBounds, Is.True);
                AssertVector(result.Center, Vector3.zero);
                AssertVector(result.Size, new Vector3(3, 5, 7));
            }
            finally
            {
                DestroyNow(rendererObject);
                DestroyNow(root);
            }
        }

        private static GameObject CreateCube(
            string name,
            Transform parent,
            Vector3 position,
            Vector3 scale)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, worldPositionStays: false);
            go.transform.position = position;
            go.transform.localScale = scale;
            DestroyNow(go.GetComponent<Collider>());
            return go;
        }

        private static void DestroyNow(Object? value)
        {
            if (value != null)
            {
                Object.DestroyImmediate(value);
            }
        }

        private static void AssertVector(Vector3 actual, Vector3 expected)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(0.001f));
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(0.001f));
            Assert.That(actual.z, Is.EqualTo(expected.z).Within(0.001f));
        }
    }
}
