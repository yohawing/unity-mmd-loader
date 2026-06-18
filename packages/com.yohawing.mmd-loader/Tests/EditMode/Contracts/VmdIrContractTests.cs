using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Mmd.Parser;

namespace Mmd.Tests
{
    [TestFixture]
    public sealed class VmdIrContractTests
    {
        public static IEnumerable<TestCaseData> MotionFixtures()
        {
            foreach (string baseName in MmdTestFixtures.MotionFixtureBaseNames())
            {
                yield return new TestCaseData(baseName).SetName("VMD IR fixture " + baseName);
            }
        }

        [TestCaseSource(nameof(MotionFixtures))]
        public void MotionFixtureMatchesGoldenVmdIr(string baseName)
        {
            MmdTestFixtures.GenerateMotionGoldenIfMissing(baseName);

            MmdMotionDefinition actual = MmdTestFixtures.ParseMotionFile(MmdTestFixtures.MotionFixtureFileName(baseName));
            MmdMotionDefinition golden = MmdTestFixtures.LoadMotionGolden(baseName);

            Assert.That(MmdMotionValidator.ValidateStructuralMotion(actual), Is.Empty);
            AssertMotionKeyframesAreSortedByTarget(actual);
            Assert.That(actual.boneKeyframes, Has.Count.EqualTo(golden.boneKeyframes.Count));
            Assert.That(actual.morphKeyframes, Has.Count.EqualTo(golden.morphKeyframes.Count));
            Assert.That(actual.modelKeyframes, Has.Count.EqualTo(golden.modelKeyframes.Count));
            Assert.That(actual.maxFrame, Is.EqualTo(golden.maxFrame));
            Assert.That(actual.targetModelName, Is.EqualTo(golden.targetModelName));
        }

        [Test]
        public void StructuralValidatorRejectsInvalidVmdIr()
        {
            var motion = new MmdMotionDefinition { maxFrame = -1 };
            motion.boneKeyframes.Add(new MmdBoneKeyframeDefinition
            {
                boneName = "",
                frame = -1,
                translation = new[] { 0.0f },
                rotation = new[] { 0.0f },
                interpolation = new MmdBoneInterpolationDefinition
                {
                    translationX = new byte[] { 0, 0, 0 },
                    translationY = null,
                    translationZ = Array.Empty<byte>(),
                    rotation = new byte[] { 0, 0, 0, 0, 0 }
                }
            });
            motion.morphKeyframes.Add(new MmdMorphKeyframeDefinition { morphName = "", frame = -1, weight = float.NaN });
            motion.modelKeyframes.Add(new MmdModelKeyframeDefinition
            {
                frame = -1,
                constraintStates =
                {
                    new MmdModelConstraintStateDefinition { boneName = "   " }
                }
            });
            motion.modelKeyframes.Add(new MmdModelKeyframeDefinition { frame = 0, constraintStates = null });

            IReadOnlyList<string> errors = MmdMotionValidator.ValidateStructuralMotion(motion);

            AssertHasErrorContaining(errors, "maxFrame");
            AssertHasErrorContaining(errors, "bone keyframe name");
            AssertHasErrorContaining(errors, "bone keyframe frame");
            AssertHasErrorContaining(errors, "translation must have 3 values");
            AssertHasErrorContaining(errors, "rotation must have 4 values");
            AssertHasErrorContaining(errors, "interpolation translationX");
            AssertHasErrorContaining(errors, "interpolation translationY");
            AssertHasErrorContaining(errors, "interpolation translationZ");
            AssertHasErrorContaining(errors, "interpolation rotation");
            AssertHasErrorContaining(errors, "morph keyframe name");
            AssertHasErrorContaining(errors, "morph keyframe frame");
            AssertHasErrorContaining(errors, "morph keyframe weight");
            AssertHasErrorContaining(errors, "model keyframe frame");
            AssertHasErrorContaining(errors, "constraint state boneName");
            AssertHasErrorContaining(errors, "constraintStates");
        }

        [Test]
        public void StructuralValidatorRejectsNullVmdIrCollections()
        {
            var motion = new MmdMotionDefinition
            {
                boneKeyframes = null,
                morphKeyframes = null,
                modelKeyframes = null
            };

            IReadOnlyList<string> errors = MmdMotionValidator.ValidateStructuralMotion(motion);

            AssertHasErrorContaining(errors, "boneKeyframes");
            AssertHasErrorContaining(errors, "morphKeyframes");
            AssertHasErrorContaining(errors, "modelKeyframes");
        }

        private static void AssertMotionKeyframesAreSortedByTarget(MmdMotionDefinition motion)
        {
            foreach (IGrouping<string, MmdBoneKeyframeDefinition> group in motion.boneKeyframes.GroupBy(keyframe => keyframe.boneName))
            {
                AssertFramesAreSorted(group.Select(keyframe => keyframe.frame), "bone keyframes: " + group.Key);
            }

            foreach (IGrouping<string, MmdMorphKeyframeDefinition> group in motion.morphKeyframes.GroupBy(keyframe => keyframe.morphName))
            {
                AssertFramesAreSorted(group.Select(keyframe => keyframe.frame), "morph keyframes: " + group.Key);
            }

            AssertFramesAreSorted(motion.modelKeyframes.Select(keyframe => keyframe.frame), "model keyframes");
        }

        private static void AssertFramesAreSorted(IEnumerable<int> frames, string context)
        {
            int previous = int.MinValue;
            foreach (int frame in frames)
            {
                Assert.That(frame, Is.GreaterThanOrEqualTo(previous), context);
                previous = frame;
            }
        }

        private static void AssertHasErrorContaining(IReadOnlyList<string> errors, string expected)
        {
            Assert.That(errors.Any(error => error.Contains(expected, StringComparison.Ordinal)), Is.True, expected);
        }
    }
}
