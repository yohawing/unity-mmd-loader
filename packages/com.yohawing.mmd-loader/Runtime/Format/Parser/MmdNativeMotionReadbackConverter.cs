#nullable enable

using System;
using System.Collections.Generic;
using System.Text;
using Mmd.Native;

namespace Mmd.Parser
{
    /// <summary>
    /// Converts native authored-track readback into the existing managed motion definition.
    /// The raw model-less path is the normal VMD parser boundary; the model-resolved overload
    /// remains an opt-in diagnostic bridge.
    /// </summary>
    internal static class MmdNativeMotionReadbackConverter
    {
        private static readonly Encoding? Cp932Encoding = TryGetCp932Encoding();

        internal static MmdMotionDefinition Build(
            MmdModelDefinition model,
            MmdVmdParseSummary summary,
            MmdRuntimeFfiMethods.VmdBoneKeyframe[] boneKeys,
            MmdRuntimeFfiMethods.MorphTrackDescriptor[] morphDescriptors,
            MmdRuntimeFfiMethods.MorphTrackKey[][] morphKeys,
            MmdRuntimeFfiMethods.VmdCameraKeyframe[] cameraKeys,
            MmdRuntimeFfiMethods.VmdLightKeyframe[] lightKeys,
            MmdRuntimeFfiMethods.VmdSelfShadowKeyframe[] selfShadowKeys,
            MmdRuntimeFfiMethods.VmdPropertyKeyframe[] propertyKeys,
            MmdRuntimeFfiMethods.VmdPropertyIkEntry[] propertyIkEntries,
            byte[] sourceBytes)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            if (boneKeys == null ||
                morphDescriptors == null || morphKeys == null ||
                cameraKeys == null || lightKeys == null || selfShadowKeys == null ||
                propertyKeys == null || propertyIkEntries == null)
            {
                throw new ArgumentNullException("Native authored readback arrays are required.");
            }

            if (sourceBytes == null || sourceBytes.Length == 0)
            {
                throw new ArgumentException("VMD source bytes are required.", nameof(sourceBytes));
            }

            if (morphDescriptors.Length != morphKeys.Length)
            {
                throw new InvalidOperationException(
                    "Native authored morph descriptor/key track counts do not match.");
            }

            Dictionary<int, string> boneNames = BuildNameMap(model.bones, bone => bone.index, bone => bone.name, "bone");
            Dictionary<int, string> morphNames = BuildNameMap(model.morphs, morph => morph.index, morph => morph.name, "morph");
            MmdMotionDefinition motion = CreateMotion(summary, sourceBytes);

            int bodyBoneKeyCount = 0;
            for (int keyIndex = 0; keyIndex < boneKeys.Length; keyIndex++)
            {
                MmdRuntimeFfiMethods.VmdBoneKeyframe key = boneKeys[keyIndex];
                int boneIndex = CheckedUIntToInt(key.boneIndex, "native bone key index");
                string boneName = ResolveName(boneNames, boneIndex, "bone", keyIndex);
                byte[] rawInterpolation = CopyRequiredBytes(
                    key.interpolation,
                    64,
                    "native raw bone interpolation",
                    0,
                    keyIndex);
                byte[] translationX = DecodeBoneInterpolation(rawInterpolation, 0);
                byte[] translationY = DecodeBoneInterpolation(rawInterpolation, 1);
                byte[] translationZ = DecodeBoneInterpolation(rawInterpolation, 2);
                byte[] rotation = DecodeBoneInterpolation(rawInterpolation, 3);

                bodyBoneKeyCount++;
                motion.boneKeyframes.Add(new MmdBoneKeyframeDefinition
                {
                    boneName = boneName,
                    frame = CheckedFrame(key.frame, summary.MaxFrame, "native bone key", 0, keyIndex),
                    translation = CopyRequired(key.positionXyz, 3, "native bone translation", 0, keyIndex),
                    rotation = CopyRequired(key.rotationXyzw, 4, "native bone rotation", 0, keyIndex),
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

            AddPropertyKeys(motion, summary, propertyKeys, propertyIkEntries);
            AddSceneKeys(motion, summary, cameraKeys, lightKeys, selfShadowKeys);
            MmdMotionValidator.ThrowIfInvalid(motion);
            return motion;
        }

        internal static MmdMotionDefinition BuildRaw(
            MmdVmdParseSummary summary,
            MmdRuntimeFfiMethods.VmdRawBoneKeyframe[] boneKeys,
            MmdRuntimeFfiMethods.VmdRawMorphKeyframe[] morphKeys,
            MmdRuntimeFfiMethods.VmdCameraKeyframe[] cameraKeys,
            MmdRuntimeFfiMethods.VmdLightKeyframe[] lightKeys,
            MmdRuntimeFfiMethods.VmdSelfShadowKeyframe[] selfShadowKeys,
            MmdRuntimeFfiMethods.VmdPropertyKeyframe[] propertyKeys,
            MmdRuntimeFfiMethods.VmdPropertyIkEntry[] propertyIkEntries,
            byte[] sourceBytes)
        {
            if (boneKeys == null || morphKeys == null || cameraKeys == null ||
                lightKeys == null || selfShadowKeys == null || propertyKeys == null ||
                propertyIkEntries == null)
            {
                throw new ArgumentNullException("Native raw VMD readback arrays are required.");
            }

            MmdMotionDefinition motion = CreateMotion(summary, sourceBytes);
            for (int keyIndex = 0; keyIndex < boneKeys.Length; keyIndex++)
            {
                MmdRuntimeFfiMethods.VmdRawBoneKeyframe key = boneKeys[keyIndex];
                byte[] rawInterpolation = CopyRequiredBytes(
                    key.interpolation,
                    64,
                    "native raw bone interpolation",
                    0,
                    keyIndex);
                motion.boneKeyframes.Add(new MmdBoneKeyframeDefinition
                {
                    boneName = DecodeRawVmdName(key.boneNameBytes, "bone", keyIndex),
                    frame = CheckedFrame(key.frame, summary.MaxFrame, "native raw bone key", 0, keyIndex),
                    translation = CopyRequired(key.positionXyz, 3, "native raw bone translation", 0, keyIndex),
                    rotation = CopyRequired(key.rotationXyzw, 4, "native raw bone rotation", 0, keyIndex),
                    interpolation = new MmdBoneInterpolationDefinition
                    {
                        translationX = DecodeBoneInterpolation(rawInterpolation, 0),
                        translationY = DecodeBoneInterpolation(rawInterpolation, 1),
                        translationZ = DecodeBoneInterpolation(rawInterpolation, 2),
                        rotation = DecodeBoneInterpolation(rawInterpolation, 3)
                    },
                    physicsEnabled = false,
                    rawInterpolation = rawInterpolation
                });
            }

            for (int keyIndex = 0; keyIndex < morphKeys.Length; keyIndex++)
            {
                MmdRuntimeFfiMethods.VmdRawMorphKeyframe key = morphKeys[keyIndex];
                motion.morphKeyframes.Add(new MmdMorphKeyframeDefinition
                {
                    morphName = DecodeRawVmdName(key.morphNameBytes, "morph", keyIndex),
                    frame = CheckedFrame(key.frame, summary.MaxFrame, "native raw morph key", 0, keyIndex),
                    weight = RequireFinite(key.weight, "native raw morph weight", 0, keyIndex)
                });
            }

            AddPropertyKeys(motion, summary, propertyKeys, propertyIkEntries);
            AddSceneKeys(motion, summary, cameraKeys, lightKeys, selfShadowKeys);
            MmdMotionValidator.ThrowIfInvalid(motion);
            return motion;
        }

        private static MmdMotionDefinition CreateMotion(
            MmdVmdParseSummary summary,
            byte[] sourceBytes)
        {
            if (sourceBytes == null || sourceBytes.Length == 0)
            {
                throw new ArgumentException("VMD source bytes are required.", nameof(sourceBytes));
            }

            return new MmdMotionDefinition
            {
                targetModelName = summary.TargetModelName ?? string.Empty,
                maxFrame = summary.MaxFrame,
                cameraKeyframeCount = summary.CameraKeyframeCount,
                lightKeyframeCount = summary.LightKeyframeCount,
                selfShadowKeyframeCount = summary.SelfShadowKeyframeCount,
                sourceBytes = (byte[])sourceBytes.Clone()
            };
        }

        private static void AddPropertyKeys(
            MmdMotionDefinition motion,
            MmdVmdParseSummary summary,
            MmdRuntimeFfiMethods.VmdPropertyKeyframe[] propertyKeys,
            MmdRuntimeFfiMethods.VmdPropertyIkEntry[] propertyIkEntries)
        {
            if (propertyKeys.Length != summary.ModelKeyframeCount)
            {
                throw new InvalidOperationException(
                    "Native authored property readback count " + propertyKeys.Length +
                    " does not match the VMD summary count " + summary.ModelKeyframeCount + ".");
            }

            int propertyEntryCount = 0;
            for (int keyIndex = 0; keyIndex < propertyKeys.Length; keyIndex++)
            {
                MmdRuntimeFfiMethods.VmdPropertyKeyframe key = propertyKeys[keyIndex];
                if (key.visible > 1)
                {
                    throw new InvalidOperationException(
                        "Native property visibility is not an ABI-safe boolean: key " + keyIndex + ".");
                }

                int entryOffset = CheckedIntPtrToInt(
                    key.ikEntryOffset,
                    "native property IK entry offset",
                    keyIndex);
                int entryCount = CheckedIntPtrToInt(
                    key.ikEntryCount,
                    "native property IK entry count",
                    keyIndex);
                if (entryOffset < 0 || entryCount < 0 || entryOffset > propertyIkEntries.Length - entryCount)
                {
                    throw new InvalidOperationException(
                        "Native property IK entry range is outside the shared context buffer: key " +
                        keyIndex + ".");
                }

                propertyEntryCount = checked(propertyEntryCount + entryCount);
                var modelKey = new MmdModelKeyframeDefinition
                {
                    frame = CheckedFrame(key.frame, summary.MaxFrame, "native property key", 0, keyIndex),
                    visible = key.visible != 0
                };
                for (int entryIndex = 0; entryIndex < entryCount; entryIndex++)
                {
                    MmdRuntimeFfiMethods.VmdPropertyIkEntry entry =
                        propertyIkEntries[entryOffset + entryIndex];
                    if (entry.enabled > 1)
                    {
                        throw new InvalidOperationException(
                            "Native property IK enabled value is not an ABI-safe boolean: key " +
                            keyIndex + ", entry " + entryIndex + ".");
                    }

                    modelKey.constraintStates.Add(new MmdModelConstraintStateDefinition
                    {
                        boneName = DecodePropertyIkName(entry.nameBytes, keyIndex, entryIndex),
                        enabled = entry.enabled != 0
                    });
                }

                motion.modelKeyframes.Add(modelKey);
            }

            if (propertyEntryCount != summary.ConstraintStateCount ||
                propertyEntryCount != propertyIkEntries.Length)
            {
                throw new InvalidOperationException(
                    "Native authored property IK entry count " + propertyEntryCount +
                    " does not match the VMD summary/native counts " +
                    summary.ConstraintStateCount + "/" + propertyIkEntries.Length + ".");
            }
        }

        private static void AddSceneKeys(
            MmdMotionDefinition motion,
            MmdVmdParseSummary summary,
            MmdRuntimeFfiMethods.VmdCameraKeyframe[] cameraKeys,
            MmdRuntimeFfiMethods.VmdLightKeyframe[] lightKeys,
            MmdRuntimeFfiMethods.VmdSelfShadowKeyframe[] selfShadowKeys)
        {
            ValidateSceneTrackCount(cameraKeys.Length, summary.CameraKeyframeCount, "camera");
            ValidateSceneTrackCount(lightKeys.Length, summary.LightKeyframeCount, "light");
            ValidateSceneTrackCount(
                selfShadowKeys.Length,
                summary.SelfShadowKeyframeCount,
                "self-shadow");

            AddCameraKeys(motion, cameraKeys, summary.MaxFrame);
            AddLightKeys(motion, lightKeys, summary.MaxFrame);
            AddSelfShadowKeys(motion, selfShadowKeys, summary.MaxFrame);
        }

        private static string DecodeRawVmdName(byte[]? values, string channel, int keyIndex)
        {
            if (values == null || values.Length != 15)
            {
                throw new InvalidOperationException(
                    "Native raw VMD " + channel + " name must contain exactly 15 bytes: key " +
                    keyIndex + ".");
            }

            int length = 0;
            while (length < values.Length && values[length] != 0)
            {
                length++;
            }

            Encoding encoding = Cp932Encoding ?? throw new MmdRuntimeUnsupportedException(
                "CP932/Shift-JIS decoding is unavailable for native raw VMD " + channel +
                " names.");
            string name = encoding.GetString(values, 0, length);

            name = name.TrimEnd(' ', '\0');
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new InvalidOperationException(
                    "Native raw VMD " + channel + " name is empty: key " + keyIndex + ".");
            }

            return name;
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
                throw new MmdRuntimeUnsupportedException(
                    "Native " + label + " track index " + index + " is not present in MmdModelDefinition: track " +
                    trackIndex + ".");
            }

            return name;
        }

        private static byte[] DecodeBoneInterpolation(byte[] rawInterpolation, int channel)
        {
            return new[]
            {
                rawInterpolation[channel],
                rawInterpolation[4 + channel],
                rawInterpolation[8 + channel],
                rawInterpolation[12 + channel]
            };
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

        private static int CheckedIntPtrToInt(IntPtr value, string label, int keyIndex)
        {
            long raw = value.ToInt64();
            if (raw < 0 || raw > int.MaxValue)
            {
                throw new InvalidOperationException(
                    label + " is out of range: key " + keyIndex + ", value " + raw + ".");
            }

            return (int)raw;
        }

        private static void ValidateSceneTrackCount(int actual, int expected, string label)
        {
            if (actual != expected)
            {
                throw new InvalidOperationException(
                    "Native authored " + label + " readback count " + actual +
                    " does not match the VMD summary count " + expected + ".");
            }
        }

        private static string DecodePropertyIkName(byte[]? values, int keyIndex, int entryIndex)
        {
            if (values == null || values.Length != 20)
            {
                throw new InvalidOperationException(
                    "Native property IK name must contain exactly 20 bytes: key " + keyIndex +
                    ", entry " + entryIndex + ".");
            }

            int length = 0;
            while (length < values.Length && values[length] != 0)
            {
                length++;
            }

            Encoding encoding = Cp932Encoding ?? throw new MmdRuntimeUnsupportedException(
                "CP932/Shift-JIS decoding is unavailable for native property IK names.");
            string name = encoding.GetString(values, 0, length);

            name = name.TrimEnd(' ', '\0');
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new InvalidOperationException(
                    "Native property IK name is empty: key " + keyIndex + ", entry " + entryIndex + ".");
            }

            return name;
        }

        private static Encoding? TryGetCp932Encoding()
        {
            try
            {
                return Encoding.GetEncoding(932);
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

    }
}
