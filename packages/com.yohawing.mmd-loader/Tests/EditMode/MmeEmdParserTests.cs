#nullable enable

using System.Collections.Generic;
using Mmd.Mme;
using NUnit.Framework;

namespace Mmd.Tests
{
    [TestFixture]
    public sealed class MmeEmdParserTests
    {
        [Test]
        public void ParseMaterialEffectAssignments_ExtractsIndexedFxAssignments()
        {
            string content = @"[Info]
Version = 3

[Effect]
Obj = Base.fx
Obj.show = true
Obj[0] = none
Obj[11] = fx\c.fx
Obj[16] = ""fx\body.fx""
";

            IReadOnlyList<MmeEmdMaterialEffectAssignment> assignments =
                MmeEmdParser.ParseMaterialEffectAssignments(content);

            Assert.That(assignments, Has.Count.EqualTo(2));
            Assert.That(assignments[0].materialIndex, Is.EqualTo(11));
            Assert.That(assignments[0].effectPath, Is.EqualTo(@"fx\c.fx"));
            Assert.That(assignments[1].materialIndex, Is.EqualTo(16));
            Assert.That(assignments[1].effectPath, Is.EqualTo(@"fx\body.fx"));
        }

        [Test]
        public void ParseMaterialEffectAssignments_IgnoresObjOutsideEffectSection()
        {
            string content = @"[Info]
Obj[3] = ignored.fx

[Other]
Obj[4] = ignored-too.fx
";

            IReadOnlyList<MmeEmdMaterialEffectAssignment> assignments =
                MmeEmdParser.ParseMaterialEffectAssignments(content);

            Assert.That(assignments, Is.Empty);
        }

        [Test]
        public void ParseEffectMap_PreservesDefaultAndNoneAssignments()
        {
            string content = @"[Effect]
Obj = Base.fx
Obj[1] = none
Obj[3] = Face.fx
";

            MmeEmdEffectMap map = MmeEmdParser.ParseEffectMap(content);

            Assert.That(map.defaultEffectPath, Is.EqualTo("Base.fx"));
            Assert.That(map.noneMaterialIndices, Is.EqualTo(new[] { 1 }));
            Assert.That(map.materialAssignments, Has.Count.EqualTo(1));
            Assert.That(map.materialAssignments[0].materialIndex, Is.EqualTo(3));
            Assert.That(map.materialAssignments[0].effectPath, Is.EqualTo("Face.fx"));
        }
    }
}
