#nullable enable
#pragma warning disable CS0649

using System;

namespace Mmd.Parser
{
    public sealed partial class NativeMmdParser
    {
        internal static MmdMotionDefinition BuildMotionDefinition(VmdMotionSourceSnapshot? source)
        {
            source ??= new VmdMotionSourceSnapshot();
            var motion = new MmdMotionDefinition
            {
                targetModelName = source.TargetModelName ?? string.Empty,
                maxFrame = CheckedUIntToInt(source.MaxFrame, "VMD maxFrame")
            };

            motion.cameraKeyframeCount = UIntCountToInt(source.CameraKeyframeCount);
            motion.lightKeyframeCount = UIntCountToInt(source.LightKeyframeCount);
            motion.selfShadowKeyframeCount = UIntCountToInt(source.SelfShadowKeyframeCount);

            foreach (VmdMotionSourceBoneFrame frame in source.BoneFrames ?? Array.Empty<VmdMotionSourceBoneFrame>())
            {
                motion.boneKeyframes.Add(new MmdBoneKeyframeDefinition
                {
                    boneName = frame?.BoneName ?? string.Empty,
                    frame = CheckedUIntToInt(frame?.Frame ?? 0u, "VMD bone frame"),
                    translation = CopyVec3(frame?.Translation),
                    rotation = CopyVec4(frame?.Rotation),
                    interpolation = new MmdBoneInterpolationDefinition
                    {
                        translationX = CopyInterpolation(frame?.TranslationXInterpolation),
                        translationY = CopyInterpolation(frame?.TranslationYInterpolation),
                        translationZ = CopyInterpolation(frame?.TranslationZInterpolation),
                        rotation = CopyInterpolation(frame?.RotationInterpolation)
                    },
                    physicsEnabled = false
                });
            }

            foreach (VmdMotionSourceMorphFrame frame in source.MorphFrames ?? Array.Empty<VmdMotionSourceMorphFrame>())
            {
                motion.morphKeyframes.Add(new MmdMorphKeyframeDefinition
                {
                    morphName = frame?.MorphName ?? string.Empty,
                    frame = CheckedUIntToInt(frame?.Frame ?? 0u, "VMD morph frame"),
                    weight = frame?.Weight ?? 0.0f
                });
            }

            foreach (VmdMotionSourcePropertyFrame frame in source.PropertyFrames ?? Array.Empty<VmdMotionSourcePropertyFrame>())
            {
                var keyframe = new MmdModelKeyframeDefinition
                {
                    frame = CheckedUIntToInt(frame?.Frame ?? 0u, "VMD property frame"),
                    visible = frame?.Visible ?? true
                };
                foreach (VmdMotionSourceIkState state in frame?.IkStates ?? Array.Empty<VmdMotionSourceIkState>())
                {
                    keyframe.constraintStates.Add(new MmdModelConstraintStateDefinition
                    {
                        boneName = state?.BoneName ?? string.Empty,
                        enabled = state?.Enabled ?? true
                    });
                }

                motion.modelKeyframes.Add(keyframe);
            }

            foreach (VmdMotionSourceCameraFrame frame in source.CameraFrames ?? Array.Empty<VmdMotionSourceCameraFrame>())
            {
                motion.cameraKeyframes.Add(new MmdCameraKeyframeDefinition
                {
                    frame = CheckedUIntToInt(frame?.Frame ?? 0u, "VMD camera frame"),
                    distance = frame?.Distance ?? 0.0f,
                    position = CopyVec3(frame?.Position),
                    rotation = CopyVec3(frame?.Rotation),
                    viewAngle = CheckedUIntToInt(frame?.ViewAngle ?? 0u, "VMD camera view angle"),
                    perspective = frame?.Perspective ?? true,
                    interpolation = CopyCameraInterpolation(frame?.Interpolation)
                });
            }

            foreach (VmdMotionSourceLightFrame frame in source.LightFrames ?? Array.Empty<VmdMotionSourceLightFrame>())
            {
                motion.lightKeyframes.Add(new MmdLightKeyframeDefinition
                {
                    frame = CheckedUIntToInt(frame?.Frame ?? 0u, "VMD light frame"),
                    color = CopyVec3(frame?.Color),
                    direction = CopyVec3(frame?.Direction),
                });
            }

            foreach (VmdMotionSourceSelfShadowFrame frame in source.SelfShadowFrames ?? Array.Empty<VmdMotionSourceSelfShadowFrame>())
            {
                motion.selfShadowKeyframes.Add(new MmdSelfShadowKeyframeDefinition
                {
                    frame = CheckedUIntToInt(frame?.Frame ?? 0u, "VMD self-shadow frame"),
                    mode = frame?.Mode ?? 0,
                    distance = frame?.Distance ?? 0.0f
                });
            }

            return motion;
        }

        internal static VmdMotionSourceSnapshot CreateMotionSnapshot(VmdParsedAnimationJson? parsed)
        {
            if (parsed == null)
            {
                return new VmdMotionSourceSnapshot();
            }

            VmdParsedMetadataJson metadata = parsed.metadata ?? new VmdParsedMetadataJson();
            VmdParsedBoneFrameJson[] boneFrames = parsed.boneFrames ?? Array.Empty<VmdParsedBoneFrameJson>();
            VmdParsedMorphFrameJson[] morphFrames = parsed.morphFrames ?? Array.Empty<VmdParsedMorphFrameJson>();
            VmdParsedPropertyFrameJson[] propertyFrames = parsed.propertyFrames ?? Array.Empty<VmdParsedPropertyFrameJson>();
            VmdParsedCameraFrameJson[] cameraFrames = parsed.cameraFrames ?? Array.Empty<VmdParsedCameraFrameJson>();
            VmdParsedLightFrameJson[] lightFrames = parsed.lightFrames ?? Array.Empty<VmdParsedLightFrameJson>();
            VmdParsedSelfShadowFrameJson[] selfShadowFrames = parsed.selfShadowFrames ?? Array.Empty<VmdParsedSelfShadowFrameJson>();

            var source = new VmdMotionSourceSnapshot
            {
                TargetModelName = metadata.modelName ?? string.Empty,
                MaxFrame = metadata.maxFrame,
                CameraKeyframeCount = CountToUInt(cameraFrames.Length),
                LightKeyframeCount = CountToUInt(lightFrames.Length),
                SelfShadowKeyframeCount = CountToUInt(selfShadowFrames.Length),
                BoneFrames = new VmdMotionSourceBoneFrame[boneFrames.Length],
                MorphFrames = new VmdMotionSourceMorphFrame[morphFrames.Length],
                PropertyFrames = new VmdMotionSourcePropertyFrame[propertyFrames.Length],
                CameraFrames = new VmdMotionSourceCameraFrame[cameraFrames.Length],
                LightFrames = new VmdMotionSourceLightFrame[lightFrames.Length],
                SelfShadowFrames = new VmdMotionSourceSelfShadowFrame[selfShadowFrames.Length]
            };

            for (int i = 0; i < source.BoneFrames.Length; i++)
            {
                VmdParsedBoneFrameJson frame = boneFrames[i] ?? new VmdParsedBoneFrameJson();
                source.BoneFrames[i] = new VmdMotionSourceBoneFrame
                {
                    BoneName = frame.boneName ?? string.Empty,
                    Frame = frame.frame,
                    Translation = CopyVec3(frame.translation),
                    Rotation = CopyVec4(frame.rotation),
                    TranslationXInterpolation = VmdJsonBoneInterpolation(frame.interpolation, 0),
                    TranslationYInterpolation = VmdJsonBoneInterpolation(frame.interpolation, 1),
                    TranslationZInterpolation = VmdJsonBoneInterpolation(frame.interpolation, 2),
                    RotationInterpolation = VmdJsonBoneInterpolation(frame.interpolation, 3)
                };
            }

            for (int i = 0; i < source.MorphFrames.Length; i++)
            {
                VmdParsedMorphFrameJson frame = morphFrames[i] ?? new VmdParsedMorphFrameJson();
                source.MorphFrames[i] = new VmdMotionSourceMorphFrame
                {
                    MorphName = frame.morphName ?? string.Empty,
                    Frame = frame.frame,
                    Weight = frame.weight
                };
            }

            for (int i = 0; i < source.PropertyFrames.Length; i++)
            {
                VmdParsedPropertyFrameJson frame = propertyFrames[i] ?? new VmdParsedPropertyFrameJson();
                VmdParsedIkStateJson[] ikStates = frame.ikStates ?? Array.Empty<VmdParsedIkStateJson>();
                var propertyFrame = new VmdMotionSourcePropertyFrame
                {
                    Frame = frame.frame,
                    Visible = frame.visible,
                    IkStates = new VmdMotionSourceIkState[ikStates.Length]
                };

                for (int j = 0; j < propertyFrame.IkStates.Length; j++)
                {
                    VmdParsedIkStateJson state = ikStates[j] ?? new VmdParsedIkStateJson();
                    propertyFrame.IkStates[j] = new VmdMotionSourceIkState
                    {
                        BoneName = state.boneName ?? string.Empty,
                        Enabled = state.enabled
                    };
                }

                source.PropertyFrames[i] = propertyFrame;
            }

            for (int i = 0; i < source.CameraFrames.Length; i++)
            {
                VmdParsedCameraFrameJson frame = cameraFrames[i] ?? new VmdParsedCameraFrameJson();
                source.CameraFrames[i] = new VmdMotionSourceCameraFrame
                {
                    Frame = frame.frame,
                    Distance = frame.distance,
                    Position = CopyVec3(frame.position),
                    Rotation = CopyVec3(frame.rotation),
                    ViewAngle = frame.ViewAngle,
                    Perspective = frame.perspective,
                    Interpolation = CopyByteArray(frame.interpolation)
                };
            }

            for (int i = 0; i < source.LightFrames.Length; i++)
            {
                VmdParsedLightFrameJson frame = lightFrames[i] ?? new VmdParsedLightFrameJson();
                source.LightFrames[i] = new VmdMotionSourceLightFrame
                {
                    Frame = frame.frame,
                    Color = CopyVec3(frame.color),
                    Direction = CopyVec3(frame.direction)
                };
            }

            for (int i = 0; i < source.SelfShadowFrames.Length; i++)
            {
                VmdParsedSelfShadowFrameJson frame = selfShadowFrames[i] ?? new VmdParsedSelfShadowFrameJson();
                source.SelfShadowFrames[i] = new VmdMotionSourceSelfShadowFrame
                {
                    Frame = frame.frame,
                    Mode = frame.mode,
                    Distance = frame.distance
                };
            }

            return source;
        }

        private static byte[] VmdJsonBoneInterpolation(int[] values, int component)
        {
            return new[]
            {
                JsonByteAt(values, component),
                JsonByteAt(values, component + 4),
                JsonByteAt(values, component + 8),
                JsonByteAt(values, component + 12)
            };
        }

        private static byte JsonByteAt(int[]? values, int index)
        {
            if (values == null || index < 0 || index >= values.Length)
            {
                return 0;
            }

            int value = values[index];
            return value < 0 ? (byte)0 : value > byte.MaxValue ? byte.MaxValue : (byte)value;
        }

        private static byte[] CopyByteArray(int[]? values)
        {
            if (values == null || values.Length == 0)
            {
                return Array.Empty<byte>();
            }

            var result = new byte[values.Length];
            for (int i = 0; i < values.Length; i++)
            {
                result[i] = JsonByteAt(values, i);
            }

            return result;
        }

        private static uint CountToUInt(int value)
        {
            return value < 0 ? 0u : (uint)value;
        }

        private static byte[] CopyInterpolation(byte[]? values)
        {
            return new[]
            {
                values != null && values.Length > 0 ? values[0] : (byte)0,
                values != null && values.Length > 1 ? values[1] : (byte)0,
                values != null && values.Length > 2 ? values[2] : (byte)0,
                values != null && values.Length > 3 ? values[3] : (byte)0
            };
        }

        // MMD camera interpolation is a flat 24-byte block (6 curves x 4 control points:
        // X, Y, Z, rotation, distance, view-angle). Pad/truncate defensively to a fixed 24.
        private static byte[] CopyCameraInterpolation(byte[]? values)
        {
            var result = new byte[24];
            if (values != null)
            {
                int count = Math.Min(values.Length, result.Length);
                for (int i = 0; i < count; i++)
                {
                    result[i] = values[i];
                }
            }

            return result;
        }
    }
}