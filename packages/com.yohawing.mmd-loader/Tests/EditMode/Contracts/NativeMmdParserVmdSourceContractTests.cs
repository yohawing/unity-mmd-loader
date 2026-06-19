using System;
using NUnit.Framework;
using Mmd.Parser;

namespace Mmd.Tests
{
    [TestFixture]
    public sealed class NativeMmdParserVmdSourceContractTests
    {
        [Test]
        public void BuildMotionDefinitionUsesNonJsonSnapshotWithoutNativeInvocation()
        {
            var snapshot = new NativeMmdParser.VmdMotionSourceSnapshot
            {
                TargetModelName = "target",
                MaxFrame = 42,
                CameraKeyframeCount = 1,
                LightKeyframeCount = 2,
                SelfShadowKeyframeCount = 3,
                BoneFrames = new[]
                {
                    new NativeMmdParser.VmdMotionSourceBoneFrame
                    {
                        BoneName = "center",
                        Frame = 7,
                        Translation = new[] { 1.0f, 2.0f, 3.0f },
                        Rotation = new[] { 0.1f, 0.2f, 0.3f, 0.4f },
                        TranslationXInterpolation = new byte[] { 1, 2, 3, 4 },
                        TranslationYInterpolation = new byte[] { 5, 6, 7, 8 },
                        TranslationZInterpolation = new byte[] { 9, 10, 11, 12 },
                        RotationInterpolation = new byte[] { 13, 14, 15, 16 }
                    }
                },
                MorphFrames = new[]
                {
                    new NativeMmdParser.VmdMotionSourceMorphFrame
                    {
                        MorphName = "smile",
                        Frame = 8,
                        Weight = 0.5f
                    }
                },
                PropertyFrames = new[]
                {
                    new NativeMmdParser.VmdMotionSourcePropertyFrame
                    {
                        Frame = 9,
                        Visible = false,
                        IkStates = new[]
                        {
                            new NativeMmdParser.VmdMotionSourceIkState
                            {
                                BoneName = "leg_ik",
                                Enabled = false
                            }
                        }
                    }
                },
                LightFrames = new[]
                {
                    new NativeMmdParser.VmdMotionSourceLightFrame
                    {
                        Frame = 11,
                        Color = new[] { 0.4f, 0.5f, 0.6f },
                        Direction = new[] { -0.5f, -1f, 0.5f }
                    }
                },
                CameraFrames = new[]
                {
                    new NativeMmdParser.VmdMotionSourceCameraFrame
                    {
                        Frame = 10,
                        Distance = -45.0f,
                        Position = new[] { 1.0f, 10.0f, 0.0f },
                        Rotation = new[] { 0.1f, 0.2f, 0.3f },
                        ViewAngle = 30,
                        Perspective = false,
                        // Short interpolation must be padded to the fixed 24-byte block.
                        Interpolation = new byte[] { 20, 107 }
                    }
                }
            };

            MmdMotionDefinition motion = NativeMmdParser.BuildMotionDefinition(snapshot);

            Assert.That(motion.targetModelName, Is.EqualTo("target"));
            Assert.That(motion.maxFrame, Is.EqualTo(42));
            Assert.That(motion.cameraKeyframeCount, Is.EqualTo(1));
            Assert.That(motion.lightKeyframeCount, Is.EqualTo(2));
            Assert.That(motion.selfShadowKeyframeCount, Is.EqualTo(3));

            Assert.That(motion.boneKeyframes, Has.Count.EqualTo(1));
            Assert.That(motion.boneKeyframes[0].boneName, Is.EqualTo("center"));
            Assert.That(motion.boneKeyframes[0].frame, Is.EqualTo(7));
            CollectionAssert.AreEqual(new[] { 1.0f, 2.0f, 3.0f }, motion.boneKeyframes[0].translation);
            CollectionAssert.AreEqual(new[] { 0.1f, 0.2f, 0.3f, 0.4f }, motion.boneKeyframes[0].rotation);
            CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 4 }, motion.boneKeyframes[0].interpolation.translationX);
            CollectionAssert.AreEqual(new byte[] { 5, 6, 7, 8 }, motion.boneKeyframes[0].interpolation.translationY);
            CollectionAssert.AreEqual(new byte[] { 9, 10, 11, 12 }, motion.boneKeyframes[0].interpolation.translationZ);
            CollectionAssert.AreEqual(new byte[] { 13, 14, 15, 16 }, motion.boneKeyframes[0].interpolation.rotation);
            Assert.That(motion.boneKeyframes[0].physicsEnabled, Is.False);

            Assert.That(motion.morphKeyframes, Has.Count.EqualTo(1));
            Assert.That(motion.morphKeyframes[0].morphName, Is.EqualTo("smile"));
            Assert.That(motion.morphKeyframes[0].frame, Is.EqualTo(8));
            Assert.That(motion.morphKeyframes[0].weight, Is.EqualTo(0.5f));

            Assert.That(motion.modelKeyframes, Has.Count.EqualTo(1));
            Assert.That(motion.modelKeyframes[0].frame, Is.EqualTo(9));
            Assert.That(motion.modelKeyframes[0].visible, Is.False);
            Assert.That(motion.modelKeyframes[0].constraintStates, Has.Count.EqualTo(1));
            Assert.That(motion.modelKeyframes[0].constraintStates[0].boneName, Is.EqualTo("leg_ik"));
            Assert.That(motion.modelKeyframes[0].constraintStates[0].enabled, Is.False);

            Assert.That(motion.cameraKeyframes, Has.Count.EqualTo(1));
            Assert.That(motion.cameraKeyframes[0].frame, Is.EqualTo(10));
            Assert.That(motion.cameraKeyframes[0].distance, Is.EqualTo(-45.0f));
            CollectionAssert.AreEqual(new[] { 1.0f, 10.0f, 0.0f }, motion.cameraKeyframes[0].position);
            CollectionAssert.AreEqual(new[] { 0.1f, 0.2f, 0.3f }, motion.cameraKeyframes[0].rotation);
            Assert.That(motion.cameraKeyframes[0].viewAngle, Is.EqualTo(30));
            Assert.That(motion.cameraKeyframes[0].perspective, Is.False);
            Assert.That(motion.cameraKeyframes[0].interpolation, Has.Length.EqualTo(24));
            Assert.That(motion.cameraKeyframes[0].interpolation[0], Is.EqualTo(20));
            Assert.That(motion.cameraKeyframes[0].interpolation[1], Is.EqualTo(107));
            Assert.That(motion.cameraKeyframes[0].interpolation[2], Is.EqualTo(0));
            Assert.That(motion.cameraKeyframes[0].interpolation[23], Is.EqualTo(0));

            Assert.That(motion.lightKeyframes, Has.Count.EqualTo(1));
            Assert.That(motion.lightKeyframes[0].frame, Is.EqualTo(11));
            CollectionAssert.AreEqual(new[] { 0.4f, 0.5f, 0.6f }, motion.lightKeyframes[0].color);
            CollectionAssert.AreEqual(new[] { -0.5f, -1f, 0.5f }, motion.lightKeyframes[0].direction);
        }

        [Test]
        public void BuildMotionDefinitionClampsSummaryCountsAndPadsShortInterpolation()
        {
            var snapshot = new NativeMmdParser.VmdMotionSourceSnapshot
            {
                TargetModelName = null,
                MaxFrame = 0,
                CameraKeyframeCount = uint.MaxValue,
                LightKeyframeCount = uint.MaxValue,
                SelfShadowKeyframeCount = uint.MaxValue,
                BoneFrames = new[]
                {
                    new NativeMmdParser.VmdMotionSourceBoneFrame
                    {
                        TranslationXInterpolation = new byte[] { 7 }
                    }
                },
                CameraFrames = new[]
                {
                    new NativeMmdParser.VmdMotionSourceCameraFrame
                    {
                        // 26 bytes: must be truncated to the fixed 24-byte block.
                        Interpolation = OneToN(26)
                    }
                }
            };

            MmdMotionDefinition motion = NativeMmdParser.BuildMotionDefinition(snapshot);

            Assert.That(motion.targetModelName, Is.Empty);
            // cameraKeyframeCount reflects the summary-reported count (independent of the loaded
            // frame list, mirroring the light / self-shadow count-only surface).
            Assert.That(motion.cameraKeyframeCount, Is.EqualTo(int.MaxValue));
            Assert.That(motion.lightKeyframeCount, Is.EqualTo(int.MaxValue));
            Assert.That(motion.selfShadowKeyframeCount, Is.EqualTo(int.MaxValue));
            CollectionAssert.AreEqual(new byte[] { 7, 0, 0, 0 }, motion.boneKeyframes[0].interpolation.translationX);
            CollectionAssert.AreEqual(new byte[] { 0, 0, 0, 0 }, motion.boneKeyframes[0].interpolation.rotation);

            // Over-long camera interpolation is truncated to the fixed 24-byte block.
            Assert.That(motion.cameraKeyframes, Has.Count.EqualTo(1));
            Assert.That(motion.cameraKeyframes[0].interpolation, Has.Length.EqualTo(24));
            Assert.That(motion.cameraKeyframes[0].interpolation[0], Is.EqualTo(1));
            Assert.That(motion.cameraKeyframes[0].interpolation[23], Is.EqualTo(24));
        }

        private static byte[] OneToN(int count)
        {
            var result = new byte[count];
            for (int i = 0; i < count; i++)
            {
                result[i] = (byte)(i + 1);
            }

            return result;
        }

        [Test]
        public void LoadMotionUsesVmdJsonParserWithoutSummaryAccessor()
        {
            byte[] observedBytes = null;
            var parser = new NativeMmdParser(
                bytes =>
                {
                    observedBytes = bytes;
                    return @"{
                        ""metadata"": {
                            ""modelName"": ""json-target"",
                            ""maxFrame"": 24,
                            ""counts"": {
                                ""bones"": 1,
                                ""morphs"": 1,
                                ""cameras"": 1,
                                ""lights"": 1,
                                ""selfShadows"": 1,
                                ""properties"": 1
                            }
                        },
                        ""boneFrames"": [{
                            ""boneName"": ""json-center"",
                            ""frame"": 12,
                            ""translation"": [1.0, 2.0, 3.0],
                            ""rotation"": [0.1, 0.2, 0.3, 0.4],
                            ""interpolation"": [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16]
                        }],
                        ""morphFrames"": [{
                            ""morphName"": ""json-smile"",
                            ""frame"": 13,
                            ""weight"": 0.75
                        }],
                        ""propertyFrames"": [{
                            ""frame"": 14,
                            ""visible"": false,
                            ""ikStates"": [{
                                ""boneName"": ""json-leg-ik"",
                                ""enabled"": false
                            }]
                        }],
                        ""cameraFrames"": [{
                            ""frame"": 20,
                            ""distance"": -45.0,
                            ""position"": [1.0, 2.0, 3.0],
                            ""rotation"": [0.1, 0.2, 0.3],
                            ""interpolation"": [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23],
                            ""fov"": 30,
                            ""perspective"": true
                        }],
                        ""lightFrames"": [{
                            ""frame"": 40,
                            ""color"": [0.1, 0.2, 0.3],
                            ""direction"": [0.4, 0.5, 0.6]
                        }],
                        ""selfShadowFrames"": [{
                            ""frame"": 50,
                            ""mode"": 1,
                            ""distance"": 0.5
                        }]
                    }";
                });

            MmdMotionDefinition motion = parser.LoadMotion(new byte[] { 4, 5, 6 });

            CollectionAssert.AreEqual(new byte[] { 4, 5, 6 }, observedBytes);
            Assert.That(motion.targetModelName, Is.EqualTo("json-target"));
            Assert.That(motion.boneKeyframes, Has.Count.EqualTo(1));
            Assert.That(motion.boneKeyframes[0].boneName, Is.EqualTo("json-center"));
            CollectionAssert.AreEqual(new byte[] { 1, 5, 9, 13 }, motion.boneKeyframes[0].interpolation.translationX);
            CollectionAssert.AreEqual(new byte[] { 4, 8, 12, 16 }, motion.boneKeyframes[0].interpolation.rotation);
            Assert.That(motion.morphKeyframes[0].morphName, Is.EqualTo("json-smile"));
            Assert.That(motion.modelKeyframes[0].constraintStates[0].boneName, Is.EqualTo("json-leg-ik"));
            Assert.That(motion.cameraKeyframes, Has.Count.EqualTo(1));
            Assert.That(motion.cameraKeyframes[0].viewAngle, Is.EqualTo(30));
            Assert.That(motion.cameraKeyframes[0].interpolation[23], Is.EqualTo(23));
            Assert.That(motion.lightKeyframes, Has.Count.EqualTo(1));
            CollectionAssert.AreEqual(new[] { 0.4f, 0.5f, 0.6f }, motion.lightKeyframes[0].direction);
            Assert.That(motion.selfShadowKeyframeCount, Is.EqualTo(1));
        }

    }
}
