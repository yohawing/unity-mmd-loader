#nullable enable

using System;
using Mmd.Motion;
using Mmd.Parser;
using Mmd.Pose;
using NUnit.Framework;

namespace Mmd.Tests
{
    public sealed class MmdIkSolverTests
    {
        [Test]
        public void PublicSolveRejectsStructurallyInvalidModel()
        {
            MmdModelDefinition model = CreateModelWithInvalidGeometry();

            Assert.Throws<InvalidOperationException>(() => new MmdIkSolver().Solve(
                model,
                new MmdSampledMotion(),
                new MmdSampledMotion(),
                MmdBoneEvaluationPass.AfterPhysics));
        }

        [Test]
        public void SolveValidatedSkipsRepeatedStructuralValidation()
        {
            MmdModelDefinition model = CreateModelWithInvalidGeometry();

            MmdSampledMotion result = new MmdIkSolver().SolveValidated(
                model,
                new MmdSampledMotion(),
                new MmdSampledMotion(),
                MmdBoneEvaluationPass.AfterPhysics);

            Assert.That(result, Is.Not.Null);
            Assert.That(result.Bones, Is.Empty);
        }

        [Test]
        public void ValidatedTopologyPreservesSparseIkSolveParity()
        {
            MmdModelDefinition model = CreateSparseIkModel();
            var solver = new MmdIkSolver();
            var preAppend = new MmdSampledMotion();
            var appended = new MmdSampledMotion();

            MmdSampledMotion legacy = solver.Solve(
                model,
                preAppend,
                appended,
                MmdBoneEvaluationPass.BeforePhysics);
            MmdTopologyPlan topology = MmdTopologyPlan.CreateFromValidatedModel(model);
            MmdSampledMotion planned = solver.SolveValidated(
                model,
                preAppend,
                appended,
                MmdBoneEvaluationPass.BeforePhysics,
                topology);

            Assert.That(topology.IndexedBones.Length, Is.EqualTo(8));
            Assert.That(topology.IndexedBones[1], Is.Null);
            Assert.That(planned.Bones.Keys, Is.EquivalentTo(legacy.Bones.Keys));
            foreach (string boneName in legacy.Bones.Keys)
            {
                Assert.That(planned.Bones[boneName].Translation, Is.EqualTo(legacy.Bones[boneName].Translation));
                Assert.That(planned.Bones[boneName].Rotation, Is.EqualTo(legacy.Bones[boneName].Rotation));
            }
        }

        [Test]
        public void ValidatedTopologyRejectsSourceMutationInsteadOfUsingStaleParents()
        {
            MmdModelDefinition model = CreateSparseIkModel();
            MmdTopologyPlan topology = MmdTopologyPlan.CreateFromValidatedModel(model);
            model.bones[1].parentIndex = 7;

            InvalidOperationException? error = Assert.Throws<InvalidOperationException>(() =>
                new MmdIkSolver().SolveValidated(
                    model,
                    new MmdSampledMotion(),
                    new MmdSampledMotion(),
                    MmdBoneEvaluationPass.BeforePhysics,
                    topology));

            Assert.That(error!.Message, Does.Contain("topology changed"));
        }

        private static MmdModelDefinition CreateModelWithInvalidGeometry()
        {
            var model = new MmdModelDefinition();
            model.vertices.Add(new MmdVertexDefinition());
            return model;
        }

        private static MmdModelDefinition CreateSparseIkModel()
        {
            var model = new MmdModelDefinition();
            AddBone(model, 0, "root", -1, 0.0f, 0.0f, 0.0f);
            AddBone(model, 2, "link", 0, 0.0f, 1.0f, 0.0f);
            AddBone(model, 5, "effector", 2, 0.0f, 2.0f, 0.0f);
            AddBone(model, 7, "goal", 0, 1.0f, 1.0f, 0.0f);
            model.ik.Add(new MmdIkDefinition
            {
                boneIndex = 7,
                targetBoneIndex = 5,
                iterationCount = 4,
                angleLimit = 0.5f,
                links = { new MmdIkLinkDefinition { boneIndex = 2 } }
            });
            return model;
        }

        private static void AddBone(
            MmdModelDefinition model,
            int index,
            string name,
            int parentIndex,
            float x,
            float y,
            float z)
        {
            model.bones.Add(new MmdBoneDefinition
            {
                index = index,
                name = name,
                parentIndex = parentIndex,
                origin = new[] { x, y, z }
            });
        }
    }
}
