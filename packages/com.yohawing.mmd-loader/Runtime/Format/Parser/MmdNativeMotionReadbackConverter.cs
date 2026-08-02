#nullable enable

using System;
using System.Collections.Generic;
using Mmd.Native;

namespace Mmd.Parser
{
    /// <summary>
    /// Converts the intentionally name-less native authored-track readback into the
    /// existing managed motion definition. This is an opt-in migration boundary; it
    /// does not participate in the normal VMD parser path.
    /// </summary>
    internal static class MmdNativeMotionReadbackConverter
    {
        private const byte DefaultCurveX1 = 20;
        private const byte DefaultCurveY1 = 20;
        private const byte DefaultCurveX2 = 107;
        private const byte DefaultCurveY2 = 107;

        internal static MmdMotionDefinition Build(
            MmdModelDefinition model,
            MmdVmdParseSummary summary,
            MmdRuntimeFfiMethods.BoneTrackDescriptor[] boneDescriptors,
            MmdRuntimeFfiMethods.BoneTrackKey[][] boneKeys,
            MmdRuntimeFfiMethods.MorphTrackDescriptor[] morphDescriptors,
            MmdRuntimeFfiMethods.MorphTrackKey[][] morphKeys,
            MmdRuntimeFfiMethods.VmdCameraKeyframe[] cameraKeys,
            MmdRuntimeFfiMethods.VmdLightKeyframe[] lightKeys,
            MmdRuntimeFfiMethods.VmdSelfShadowKeyframe[] selfShadowKeys,
            byte[] sourceBytes)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            if (boneDescriptors == null || boneKeys == null ||
                morphDescriptors == null || morphKeys == null ||
                cameraKeys == null || lightKeys == null || selfShadowKeys == null)
            {
                throw new ArgumentNullException("Native authored readback arrays are required.");
            }

            if (sourceBytes == null || sourceBytes.Length == 0)
            {
                throw new ArgumentException("VMD source bytes are required.", nameof(sourceBytes));
            }

            if (boneDescriptors.Length != boneKeys.Length)
            {
                throw new InvalidOperationException(
                    "Native authored bone descriptor/key track counts do not match.");
            }
            if (morphDescriptors.Length != morphKeys.Length)
            {
                throw new InvalidOperationException(
                    "Native authored morph descriptor/key track counts do not match.");
            }

            Dictionary<int, string> boneNames = BuildNameMap(model.bones, bone => bone.index, bone => bone.name, "bone");
            Dictionary<int, string> morphNames = BuildNameMap(model.morphs, morph => morph.index, morph => morph.name, "morph");
            var motion = new MmdMotionDefinition
            {
                targetModelName = summary.TargetModelName ?? string.Empty,
                maxFrame = summary.MaxFrame,
                cameraKeyframeCount = summary.CameraKeyframeCount,
                lightKeyframeCount = summary.LightKeyframeCount,
                selfShadowKeyframeCount = summary.SelfShadowKeyframeCount,
                sourceBytes = (byte[])sourceBytes.Clone()
            };

            int bodyBoneKeyCount = 0;
            for (int trackIndex = 0; trackIndex < boneDescriptors.Length; trackIndex++)
            {
                MmdRuntimeFfiMethods.BoneTrackDescriptor descriptor = boneDescriptors[trackIndex];
                int boneIndex = CheckedUIntToInt(descriptor.boneIndex, "native bone track index");
                string boneName = ResolveName(boneNames, boneIndex, "bone", trackIndex);
                MmdRuntimeFfiMethods.BoneTrackKey[] keys = boneKeys[trackIndex] ??
                    throw new InvalidOperationException("Native bone track keys are null: " + trackIndex + ".");

                bodyBoneKeyCount = checked(bodyBoneKeyCount + keys.Length);
                for (int keyIndex = 0; keyIndex < keys.Length; keyIndex++)
                {
                    MmdRuntimeFfiMethods.BoneTrackKey key = keys[keyIndex];
                    int keyBoneIndex = CheckedUIntToInt(key.boneIndex, "native bone key index");
                    if (keyBoneIndex != boneIndex)
                    {
                        throw new InvalidOperationException(
                            "Native bone key index does not match its track descriptor: track " +
                            trackIndex + ", key " + keyIndex + ".");
                    }

                    byte[] translationX = CurveToInterpolation(key.translationX, "translationX", trackIndex, keyIndex);
                    byte[] translationY = CurveToInterpolation(key.translationY, "translationY", trackIndex, keyIndex);
                    byte[] translationZ = CurveToInterpolation(key.translationZ, "translationZ", trackIndex, keyIndex);
                    byte[] rotation = CurveToInterpolation(key.rotation, "rotation", trackIndex, keyIndex);
                    byte[] rawInterpolation = BuildRawInterpolation(translationX, translationY, translationZ, rotation);

                    motion.boneKeyframes.Add(new MmdBoneKeyframeDefinition
                    {
                        boneName = boneName,
                        frame = CheckedFrame(key.frame, summary.MaxFrame, "native bone key", trackIndex, keyIndex),
                        translation = CopyRequired(key.positionXyz, 3, "native bone translation", trackIndex, keyIndex),
                        rotation = CopyRequired(key.rotationXyzw, 4, "native bone rotation", trackIndex, keyIndex),
                        interpolation = new MmdBoneInterpolationDefinition
                        {
                            translationX = translationX,
                            translationY = translationY,
                            translationZ = translationZ,
                            rotation = rotation
                        },
                        physicsEnabled = false,
                        rawInterpolation = rawInterpolation
                    });
                }
            }

            if (bodyBoneKeyCount != summary.BoneKeyframeCount)
            {
                throw new InvalidOperationException(
                    "Native authored bone readback count " + bodyBoneKeyCount +
                    " does not match the VMD summary count " + summary.BoneKeyframeCount + ".");
            }
            int bodyMorphKeyCount = 0;
            for (int trackIndex = 0; trackIndex < morphDescriptors.Length; trackIndex++)
            {
                MmdRuntimeFfiMethods.MorphTrackDescriptor descriptor = morphDescriptors[trackIndex];
                int morphIndex = CheckedUIntToInt(descriptor.morphIndex, "native morph track index");
                string morphName = ResolveName(morphNames, morphIndex, "morph", trackIndex);
                MmdRuntimeFfiMethods.MorphTrackKey[] keys = morphKeys[trackIndex] ??
                    throw new InvalidOperationException("Native morph track keys are null: " + trackIndex + ".");

                bodyMorphKeyCount = checked(bodyMorphKeyCount + keys.Length);
                for (int keyIndex = 0; keyIndex < keys.Length; keyIndex++)
                {
                    MmdRuntimeFfiMethods.MorphTrackKey key = keys[keyIndex];
                    int keyMorphIndex = CheckedUIntToInt(key.morphIndex, "native morph key index");
                    if (keyMorphIndex != morphIndex)
                    {
                        throw new InvalidOperationException(
                            "Native morph key index does not match its track descriptor: track " +
                            trackIndex + ", key " + keyIndex + ".");
                    }

                    RequireFinite(key.weight, "native morph weight", trackIndex, keyIndex);
                    motion.morphKeyframes.Add(new MmdMorphKeyframeDefinition
                    {
                        morphName = morphName,
                        frame = CheckedFrame(key.frame, summary.MaxFrame, "native morph key", trackIndex, keyIndex),
                        weight = key.weight
                    });
                }
            }

            if (bodyMorphKeyCount != summary.MorphKeyframeCount)
            {
                throw new InvalidOperationException(
                    "Native authored morph readback count " + bodyMorphKeyCount +
                    " does not match the VMD summary count " + summary.MorphKeyframeCount + ".");
            }

            AddCameraKeys(motion, cameraKeys, summary.MaxFrame);
            AddLightKeys(motion, lightKeys, summary.MaxFrame);
            AddSelfShadowKeys(motion, selfShadowKeys, summary.MaxFrame);
            MmdMotionValidator.ThrowIfInvalid(motion);
            return motion;
        }

        private static Dictionary<int, string> BuildNameMap<T>(
            IReadOnlyList<T> definitions,
            Func<T, int> getIndex,
            Func<T, string> getName,
            string label)
            where T : class
        {
            if (definitions == null)
            {
                throw new InvalidOperationException("MmdModelDefinition " + label + " list is null.");
            }

            var names = new Dictionary<int, string>();
            for (int i = 0; i < definitions.Count; i++)
            {
                T definition = definitions[i];
                if (definition == null)
                {
                    throw new InvalidOperationException("MmdModelDefinition " + label + " is null: " + i + ".");
                }

                int index = getIndex(definition);
                string name = getName(definition);

                if (index < 0 || string.IsNullOrWhiteSpace(name) || !names.TryAdd(index, name))
                {
                    throw new InvalidOperationException(
                        "MmdModelDefinition " + label + " index/name map is invalid: " + i + ".");
                }
            }

            return names;
        }

        private static string ResolveName(Dictionary<int, string> names, int index, string label, int trackIndex)
        {
            if (!names.TryGetValue(index, out string? name))
            {
                throw new InvalidOperationException(
                    "Native " + label + " track index " + index + " is not present in MmdModelDefinition: track " +
                    trackIndex + ".");
            }

            return name;
        }

        private static byte[] CurveToInterpolation(
            MmdRuntimeFfiMethods.BoneTrackCurve curve,
            string channel,
            int trackIndex,
            int keyIndex)
        {
            if (curve.kind == MmdRuntimeFfiMethods.VmdCurveNone)
            {
                return new[] { DefaultCurveX1, DefaultCurveY1, DefaultCurveX2, DefaultCurveY2 };
            }

            if (curve.kind != MmdRuntimeFfiMethods.VmdCurveCubicBezier)
            {
                throw new InvalidOperationException(
                    "Native bone interpolation kind is unsupported: " + channel + ", track " +
                    trackIndex + ", key " + keyIndex + ".");
            }

            return new[]
            {
                CurvePointToByte(curve.x1, channel, trackIndex, keyIndex),
                CurvePointToByte(curve.y1, channel, trackIndex, keyIndex),
                CurvePointToByte(curve.x2, channel, trackIndex, keyIndex),
                CurvePointToByte(curve.y2, channel, trackIndex, keyIndex)
            };
        }

        private static byte CurvePointToByte(float point, string channel, int trackIndex, int keyIndex)
        {
            if (!float.IsFinite(point) || point < 0.0f || point > 1.0f)
            {
                throw new InvalidOperationException(
                    "Native bone interpolation point is outside [0,1]: " + channel + ", track " +
                    trackIndex + ", key " + keyIndex + ".");
            }

            return (byte)Math.Clamp((int)MathF.Round(point * 127.0f), 0, 127);
        }

        private static byte[] BuildRawInterpolation(
            byte[] translationX,
            byte[] translationY,
            byte[] translationZ,
            byte[] rotation)
        {
            var raw = new byte[64];
            CopyInterpolationChannel(raw, 0, translationX);
            CopyInterpolationChannel(raw, 1, translationY);
            CopyInterpolationChannel(raw, 2, translationZ);
            CopyInterpolationChannel(raw, 3, rotation);
            return raw;
        }

        private static void CopyInterpolationChannel(byte[] destination, int channel, byte[] source)
        {
            for (int i = 0; i < 4; i++)
            {
                destination[channel + i * 4] = source[i];
            }
        }

        private static void AddCameraKeys(
            MmdMotionDefinition motion,
            MmdRuntimeFfiMethods.VmdCameraKeyframe[] keys,
            int maxFrame)
        {
            for (int i = 0; i < keys.Length; i++)
            {
                MmdRuntimeFfiMethods.VmdCameraKeyframe key = keys[i];
                motion.cameraKeyframes.Add(new MmdCameraKeyframeDefinition
                {
                    frame = CheckedFrame(key.frame, maxFrame, "native camera key", 0, i),
                    distance = RequireFinite(key.distance, "native camera distance", 0, i),
                    position = CopyRequired(key.positionXyz, 3, "native camera position", 0, i),
                    rotation = CopyRequired(key.rotationXyz, 3, "native camera rotation", 0, i),
                    viewAngle = CheckedUIntToInt(key.fov, "native camera view angle"),
                    perspective = key.perspective != 0,
                    interpolation = CopyRequiredBytes(key.interpolation, 24, "native camera interpolation", 0, i)
                });
            }
        }

        private static void AddLightKeys(
            MmdMotionDefinition motion,
            MmdRuntimeFfiMethods.VmdLightKeyframe[] keys,
            int maxFrame)
        {
            for (int i = 0; i < keys.Length; i++)
            {
                MmdRuntimeFfiMethods.VmdLightKeyframe key = keys[i];
                motion.lightKeyframes.Add(new MmdLightKeyframeDefinition
                {
                    frame = CheckedFrame(key.frame, maxFrame, "native light key", 0, i),
                    color = CopyRequired(key.color, 3, "native light color", 0, i),
                    direction = CopyRequired(key.direction, 3, "native light direction", 0, i)
                });
            }
        }

        private static void AddSelfShadowKeys(
            MmdMotionDefinition motion,
            MmdRuntimeFfiMethods.VmdSelfShadowKeyframe[] keys,
            int maxFrame)
        {
            for (int i = 0; i < keys.Length; i++)
            {
                MmdRuntimeFfiMethods.VmdSelfShadowKeyframe key = keys[i];
                motion.selfShadowKeyframes.Add(new MmdSelfShadowKeyframeDefinition
                {
                    frame = CheckedFrame(key.frame, maxFrame, "native self-shadow key", 0, i),
                    mode = key.mode,
                    distance = RequireFinite(key.distance, "native self-shadow distance", 0, i)
                });
            }
        }

        private static float[] CopyRequired(float[]? values, int expectedLength, string label, int trackIndex, int keyIndex)
        {
            if (values == null || values.Length != expectedLength)
            {
                throw new InvalidOperationException(
                    label + " must have " + expectedLength + " values: track " + trackIndex + ", key " + keyIndex + ".");
            }

            var result = (float[])values.Clone();
            RequireFinite(result, label, trackIndex, keyIndex);
            return result;
        }

        private static byte[] CopyRequiredBytes(byte[]? values, int expectedLength, string label, int trackIndex, int keyIndex)
        {
            if (values == null || values.Length != expectedLength)
            {
                throw new InvalidOperationException(
                    label + " must have " + expectedLength + " values: track " + trackIndex + ", key " + keyIndex + ".");
            }

            return (byte[])values.Clone();
        }

        private static float RequireFinite(float value, string label, int trackIndex, int keyIndex)
        {
            if (!float.IsFinite(value))
            {
                throw new InvalidOperationException(
                    label + " must be finite: track " + trackIndex + ", key " + keyIndex + ".");
            }

            return value;
        }

        private static void RequireFinite(float[] values, string label, int trackIndex, int keyIndex)
        {
            for (int i = 0; i < values.Length; i++)
            {
                RequireFinite(values[i], label, trackIndex, keyIndex);
            }
        }

        private static int CheckedFrame(uint value, int maxFrame, string label, int trackIndex, int keyIndex)
        {
            int frame = CheckedUIntToInt(value, label + " frame");
            if (frame < 0 || frame > maxFrame)
            {
                throw new InvalidOperationException(
                    label + " frame is outside the VMD summary range: track " + trackIndex + ", key " + keyIndex + ".");
            }

            return frame;
        }

        private static int CheckedUIntToInt(uint value, string label)
        {
            if (value > int.MaxValue)
            {
                throw new InvalidOperationException(label + " is out of range: " + value + ".");
            }

            return (int)value;
        }

    }
}
