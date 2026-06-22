#nullable enable

using NUnit.Framework;
using UnityEngine;
using Mmd.Editor;

namespace Mmd.Tests
{
    [TestFixture]
    public sealed class PmxImportHierarchyReadinessContractTests
    {
        [Test]
        public void StaticModelWithMeshRendererReportsReadyRendererAndNotEvaluatedBoneBinding()
        {
            var root = new GameObject("StaticRoot");
            Mesh? mesh = null;
            try
            {
                var modelObject = new GameObject("Model");
                modelObject.transform.SetParent(root.transform, worldPositionStays: false);
                mesh = new Mesh
                {
                    name = "Static Mesh",
                    vertices = new Vector3[3]
                };
                MeshFilter meshFilter = modelObject.AddComponent<MeshFilter>();
                meshFilter.sharedMesh = mesh;
                modelObject.AddComponent<MeshRenderer>();

                MmdPmxAsset.ComputeHierarchyReadiness(
                    root,
                    assetBoneCount: 0,
                    out MmdImportReadiness hierarchyReadiness,
                    out MmdImportReadiness rendererReadiness,
                    out MmdImportReadiness boneBindingReadiness,
                    out string hierarchyDiagnostic,
                    out string rendererDiagnostic,
                    out string boneBindingDiagnostic);

                Assert.That(hierarchyReadiness, Is.EqualTo(MmdImportReadiness.Ready));
                Assert.That(rendererReadiness, Is.EqualTo(MmdImportReadiness.Ready));
                Assert.That(boneBindingReadiness, Is.EqualTo(MmdImportReadiness.NotEvaluated));
                Assert.That(hierarchyDiagnostic, Does.Contain("ImportedRoot exists"));
                Assert.That(rendererDiagnostic, Does.Contain("MeshRenderer"));
                Assert.That(boneBindingDiagnostic, Does.Contain("Static renderer path"));
            }
            finally
            {
                if (mesh != null)
                {
                    Object.DestroyImmediate(mesh);
                }

                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void StaticModelWithoutMeshRendererReportsBlockedRendererAndNotEvaluatedBoneBinding()
        {
            var root = new GameObject("StaticRootWithoutRenderer");
            try
            {
                MmdPmxAsset.ComputeHierarchyReadiness(
                    root,
                    assetBoneCount: 0,
                    out MmdImportReadiness hierarchyReadiness,
                    out MmdImportReadiness rendererReadiness,
                    out MmdImportReadiness boneBindingReadiness,
                    out string _,
                    out string rendererDiagnostic,
                    out string boneBindingDiagnostic);

                Assert.That(hierarchyReadiness, Is.EqualTo(MmdImportReadiness.Ready));
                Assert.That(rendererReadiness, Is.EqualTo(MmdImportReadiness.Blocked));
                Assert.That(boneBindingReadiness, Is.EqualTo(MmdImportReadiness.NotEvaluated));
                Assert.That(rendererDiagnostic, Does.Contain("No MeshRenderer"));
                Assert.That(boneBindingDiagnostic, Does.Contain("Static renderer path"));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void NullImportedRootReportsBlockedReadinessForSkinnedModel()
        {
            MmdPmxAsset.ComputeHierarchyReadiness(
                null,
                assetBoneCount: 5,
                out MmdImportReadiness hierarchyReadiness,
                out MmdImportReadiness rendererReadiness,
                out MmdImportReadiness boneBindingReadiness,
                out string hierarchyDiagnostic,
                out string rendererDiagnostic,
                out string boneBindingDiagnostic);

            Assert.That(hierarchyReadiness, Is.EqualTo(MmdImportReadiness.Blocked));
            Assert.That(rendererReadiness, Is.EqualTo(MmdImportReadiness.Blocked));
            Assert.That(boneBindingReadiness, Is.EqualTo(MmdImportReadiness.Blocked));
            Assert.That(hierarchyDiagnostic, Does.Contain("null"));
            Assert.That(rendererDiagnostic, Does.Contain("No SkinnedMeshRenderer"));
            Assert.That(boneBindingDiagnostic, Does.Contain("No SkinnedMeshRenderer"));
        }
    }
}
