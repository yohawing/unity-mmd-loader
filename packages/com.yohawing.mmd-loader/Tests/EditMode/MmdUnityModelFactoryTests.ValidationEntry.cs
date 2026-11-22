#nullable enable

using System;
using NUnit.Framework;
using UnityEngine;
using Mmd.Parser;
using Mmd.Rendering;
using Mmd.UnityIntegration;

namespace Mmd.Tests
{
    /// <summary>
    /// Characterization of how an invalid PMX model surfaces through the public descriptor and factory
    /// entry points (exception type, joined diagnostic text and order). Recorded from the implementation
    /// that validated in every rendering sub-builder, so that validating once per entry cannot change
    /// what callers observe.
    /// </summary>
    public sealed partial class MmdUnityModelFactoryTests
    {
        private const string InvalidEntryModelDiagnostics =
            "vertex uv must have 2 values: 0; " +
            "material alpha must be finite and between 0 and 1: 0";

        [Test]
        public void InvalidModelThroughRenderingDescriptorBuilderThrowsJoinedDiagnostics()
        {
            InvalidOperationException defaultPreset = Assert.Throws<InvalidOperationException>(
                () => MmdRenderingDescriptorBuilder.Build(CreateInvalidEntryModel()))!;
            Assert.That(defaultPreset.Message, Is.EqualTo(InvalidEntryModelDiagnostics));

            InvalidOperationException explicitPreset = Assert.Throws<InvalidOperationException>(
                () => MmdRenderingDescriptorBuilder.Build(CreateInvalidEntryModel(), MmdMaterialPreset.MmdToon))!;
            Assert.That(explicitPreset.Message, Is.EqualTo(InvalidEntryModelDiagnostics));
        }

        [Test]
        public void InvalidModelThroughFactoryCreateEntriesThrowsJoinedDiagnostics()
        {
            InvalidOperationException skinned = Assert.Throws<InvalidOperationException>(
                () => MmdUnityModelFactory.CreateSkinnedModel(CreateInvalidEntryModel()))!;
            Assert.That(skinned.Message, Is.EqualTo(InvalidEntryModelDiagnostics));

            InvalidOperationException staticModel = Assert.Throws<InvalidOperationException>(
                () => MmdUnityModelFactory.CreateStaticModel(CreateInvalidEntryModel()))!;
            Assert.That(staticModel.Message, Is.EqualTo(InvalidEntryModelDiagnostics));
        }

        [Test]
        public void InvalidModelThroughImportedHierarchyThrowsJoinedDiagnostics()
        {
            using var sceneScope = new MmdTestInstanceScope(
                MmdUnityModelFactory.CreateSkinnedModel(CreateMinimalTriangleModel(includeTextureReferences: false)));

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => MmdUnityModelFactory.CreateFromImportedHierarchy(
                    sceneScope.Instance.Root,
                    CreateInvalidEntryModel(),
                    sourcePath: null,
                    importScale: 1.0f))!;
            Assert.That(exception.Message, Is.EqualTo(InvalidEntryModelDiagnostics));
        }

        [Test]
        public void InvalidModelRebindWithoutExistingMeshThrowsJoinedDiagnostics()
        {
            using var sceneScope = new MmdTestInstanceScope(
                MmdUnityModelFactory.CreateSkinnedModel(CreateMinimalTriangleModel(includeTextureReferences: false)));
            RequireSkinnedRenderer(sceneScope.Instance).sharedMesh = null;

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => MmdUnityModelFactory.CreateExistingSkinnedModelInstance(
                    sceneScope.Instance.Root,
                    CreateInvalidEntryModel(),
                    sourcePath: null))!;
            Assert.That(exception.Message, Is.EqualTo(InvalidEntryModelDiagnostics));
        }

        [Test]
        public void InvalidModelRebindWithExistingMeshThrowsJoinedDiagnostics()
        {
            // The existing-mesh rebind builds the playback-only descriptor. It validates once and
            // reports the invalid model directly (it used to surface an AggregateException of the
            // four parallel builders' failures).
            using var sceneScope = new MmdTestInstanceScope(
                MmdUnityModelFactory.CreateSkinnedModel(CreateMinimalTriangleModel(includeTextureReferences: false)));

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => MmdUnityModelFactory.CreateExistingSkinnedModelInstance(
                    sceneScope.Instance.Root,
                    CreateInvalidEntryModel(),
                    sourcePath: null))!;
            Assert.That(exception.Message, Is.EqualTo(InvalidEntryModelDiagnostics));
        }

        private static MmdModelDefinition CreateInvalidEntryModel()
        {
            MmdModelDefinition model = CreateMinimalTriangleModel(includeTextureReferences: false);
            model.materials[0].alpha = -1.0f;
            model.vertices[0].uv = new[] { 0.0f };
            return model;
        }
    }
}
