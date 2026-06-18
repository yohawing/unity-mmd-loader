#nullable enable

using NUnit.Framework;
using Mmd.Motion;
using Mmd.Parser;

namespace Mmd.Tests
{
    public sealed class MmdBoneMorphEvaluatorCoWTests
    {
        [Test]
        public void NoBoneMorphModelReturnsSameSampledMotionReference()
        {
            MmdModelDefinition model = CreateTwoBoneModel();
            model.morphs.Add(new MmdMorphDefinition
            {
                index = 0,
                name = "vertex-only",
                type = "vertex",
                panel = "other"
            });
            MmdSampledMotion sampled = CreateSampledMotion();
            sampled.Morphs["vertex-only"] = 1.0f;

            MmdSampledMotion result = MmdBoneMorphEvaluator.ApplyBoneMorphs(model, sampled);

            Assert.That(ReferenceEquals(result, sampled), Is.True);
        }

        [Test]
        public void ZeroBoneMorphWeightReturnsSameSampledMotionReference()
        {
            MmdModelDefinition model = CreateTwoBoneModel();
            AddBoneMorph(model, index: 0, name: "bone-offset", boneIndex: 0, translationX: 1.0f);
            MmdSampledMotion sampled = CreateSampledMotion();
            sampled.Morphs["bone-offset"] = 0.0f;

            MmdSampledMotion result = MmdBoneMorphEvaluator.ApplyBoneMorphs(model, sampled);

            Assert.That(ReferenceEquals(result, sampled), Is.True);
        }

        [Test]
        public void DirectBoneMorphUsesCopyOnWriteForAffectedBoneOnly()
        {
            MmdModelDefinition model = CreateTwoBoneModel();
            AddBoneMorph(model, index: 0, name: "bone-offset", boneIndex: 0, translationX: 2.0f);
            MmdSampledMotion sampled = CreateSampledMotion();
            float[] originalRootTranslation = sampled.Bones["root"].Translation;
            float[] originalChildTranslation = sampled.Bones["child"].Translation;
            sampled.Morphs["bone-offset"] = 0.5f;

            MmdSampledMotion result = MmdBoneMorphEvaluator.ApplyBoneMorphs(model, sampled);

            Assert.That(ReferenceEquals(result, sampled), Is.False);
            Assert.That(sampled.Bones["root"].Translation, Is.SameAs(originalRootTranslation));
            Assert.That(sampled.Bones["root"].Translation[0], Is.EqualTo(1.0f));
            Assert.That(result.Bones["root"].Translation, Is.Not.SameAs(originalRootTranslation));
            Assert.That(result.Bones["root"].Translation[0], Is.EqualTo(2.0f).Within(0.0001f));
            Assert.That(result.Bones["child"].Translation, Is.SameAs(originalChildTranslation));
        }

        [Test]
        public void DuplicateBoneMorphNamesAccumulateOffsets()
        {
            MmdModelDefinition model = CreateTwoBoneModel();
            AddBoneMorph(model, index: 0, name: "duplicate", boneIndex: 0, translationX: 1.0f);
            AddBoneMorph(model, index: 1, name: "duplicate", boneIndex: 0, translationX: 3.0f);
            MmdSampledMotion sampled = CreateSampledMotion();
            sampled.Morphs["duplicate"] = 0.5f;

            MmdSampledMotion result = MmdBoneMorphEvaluator.ApplyBoneMorphs(model, sampled);

            Assert.That(result.Bones["root"].Translation[0], Is.EqualTo(3.0f).Within(0.0001f));
        }

        [Test]
        public void CompositeGroupAndFlipBoneMorphsStillResolve()
        {
            MmdModelDefinition model = CreateTwoBoneModel();
            AddBoneMorph(model, index: 0, name: "bone-offset", boneIndex: 0, translationX: 2.0f);
            model.morphs.Add(new MmdMorphDefinition
            {
                index = 1,
                name = "group-to-bone",
                type = "group",
                panel = "other",
                groupOffsets =
                {
                    new MmdGroupMorphOffsetDefinition { morphIndex = 0, weight = 1.0f }
                }
            });
            model.morphs.Add(new MmdMorphDefinition
            {
                index = 2,
                name = "flip-to-bone",
                type = "flip",
                panel = "other",
                flipOffsets =
                {
                    new MmdFlipMorphOffsetDefinition { morphIndex = 0, weight = 0.5f }
                }
            });
            model.morphs.Add(new MmdMorphDefinition
            {
                index = 3,
                name = "group-to-flip-to-bone",
                type = "group",
                panel = "other",
                groupOffsets =
                {
                    new MmdGroupMorphOffsetDefinition { morphIndex = 2, weight = 1.0f }
                }
            });

            AssertResolvedRootX(model, "group-to-bone", 1.0f, expectedX: 3.0f);
            AssertResolvedRootX(model, "flip-to-bone", 1.0f, expectedX: 2.0f);
            AssertResolvedRootX(model, "group-to-flip-to-bone", 1.0f, expectedX: 2.0f);
        }

        private static void AssertResolvedRootX(
            MmdModelDefinition model,
            string morphName,
            float weight,
            float expectedX)
        {
            MmdSampledMotion sampled = CreateSampledMotion();
            sampled.Morphs[morphName] = weight;

            MmdSampledMotion result = MmdBoneMorphEvaluator.ApplyBoneMorphs(model, sampled);

            Assert.That(result.Bones["root"].Translation[0], Is.EqualTo(expectedX).Within(0.0001f));
        }

        private static MmdModelDefinition CreateTwoBoneModel()
        {
            var model = new MmdModelDefinition { name = "bone-morph-cow-test" };
            model.bones.Add(new MmdBoneDefinition
            {
                index = 0,
                name = "root",
                parentIndex = -1,
                origin = new[] { 0.0f, 0.0f, 0.0f }
            });
            model.bones.Add(new MmdBoneDefinition
            {
                index = 1,
                name = "child",
                parentIndex = 0,
                origin = new[] { 0.0f, 1.0f, 0.0f }
            });
            return model;
        }

        private static MmdSampledMotion CreateSampledMotion()
        {
            var sampled = new MmdSampledMotion();
            sampled.Bones["root"] = new MmdBonePoseSample(
                new[] { 1.0f, 0.0f, 0.0f },
                new[] { 0.0f, 0.0f, 0.0f, 1.0f });
            sampled.Bones["child"] = new MmdBonePoseSample(
                new[] { 0.0f, 1.0f, 0.0f },
                new[] { 0.0f, 0.0f, 0.0f, 1.0f });
            sampled.IkStates["root"] = true;
            return sampled;
        }

        private static void AddBoneMorph(
            MmdModelDefinition model,
            int index,
            string name,
            int boneIndex,
            float translationX)
        {
            model.morphs.Add(new MmdMorphDefinition
            {
                index = index,
                name = name,
                type = "bone",
                panel = "other",
                boneOffsets =
                {
                    new MmdBoneMorphOffsetDefinition
                    {
                        boneIndex = boneIndex,
                        translation = new[] { translationX, 0.0f, 0.0f },
                        orientation = new[] { 0.0f, 0.0f, 0.0f, 1.0f }
                    }
                }
            });
        }
    }
}
