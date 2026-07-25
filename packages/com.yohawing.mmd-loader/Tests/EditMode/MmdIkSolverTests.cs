#nullable enable

using System;
using Mmd.Motion;
using Mmd.Parser;
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

        private static MmdModelDefinition CreateModelWithInvalidGeometry()
        {
            var model = new MmdModelDefinition();
            model.vertices.Add(new MmdVertexDefinition());
            return model;
        }
    }
}
