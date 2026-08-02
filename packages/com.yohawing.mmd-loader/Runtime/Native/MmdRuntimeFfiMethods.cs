#nullable enable

using System;
using System.Runtime.InteropServices;

namespace Mmd.Native
{
    internal static class MmdRuntimeFfiMethods
    {
        internal const string LibraryName = "mmd_runtime_ffi";
        internal const uint ExpectedAbiVersion = 3;
        internal const uint FeatureSplitPhysicsEvaluation = 1u << 0;
        internal const uint FeaturePhysicsBulletNative = 1u << 1;
        internal const uint FeatureHostPoseNativeMorphs = 1u << 3;
        internal const uint FeatureClipBoneTrackIntrospection = 1u << 5;
        internal const uint FeatureClipMorphTrackIntrospection = 1u << 6;
        internal const uint FeatureClipPropertyTrackIntrospection = 1u << 7;
        internal const uint FeatureVmdTrackKeyframeIntrospection = 1u << 8;
        internal const uint FeatureVmdSharedContext = 1u << 9;
        internal const uint FeatureVmdSharedContextBoneReadback = 1u << 10;
        internal const uint PhysicsModeLive = 2;
        internal const uint PhysicsFrameActionSeed = 0;
        internal const uint PhysicsFrameActionStep = 1;
        internal const uint PhysicsShapeSphere = 0;
        internal const uint PhysicsShapeBox = 1;
        internal const uint PhysicsShapeCapsule = 2;
        internal const uint PhysicsBodyModeStatic = 0;
        internal const uint PhysicsBodyModeDynamic = 1;
        internal const uint PhysicsBodyModeDynamicBone = 2;
        internal const uint PhysicsJointKindGeneric6DofSpring = 0;
        internal const int StatusOk = 0;
        internal const int StatusInvalidInput = 1;
        internal const int StatusUnsupported = 2;
        internal const int StatusBufferTooSmall = 3;
        internal const int StatusError = 4;
        internal const uint ReductionTargetDccCubic = 2;
        internal const uint GenericCurveAbiVersionV1 = 1;
        internal const uint GenericCurveBoneLocal = 0;
        internal const uint GenericCurveMorphWeight = 1;
        internal const uint GenericValueTranslation = 1u << 0;
        internal const uint GenericValueQuaternion = 1u << 1;
        internal const uint GenericValueScalar = 1u << 2;
        internal const uint GenericRotationBasisNone = 0;
        internal const uint GenericRotationBasisRuntimeQuaternion = 1;
        internal const uint ClipBoneTrackIntrospectionAbiVersionV1 = 1;
        internal const uint ClipMorphTrackIntrospectionAbiVersionV1 = 1;
        internal const uint ClipPropertyTrackIntrospectionAbiVersionV1 = 1;
        internal const uint VmdTrackKeyframeIntrospectionAbiVersionV1 = 1;
        internal const uint VmdSharedContextAbiVersionV1 = 1;
        internal const uint VmdSharedContextBoneReadbackAbiVersionV1 = 1;
        internal const uint VmdCurveNone = 0;
        internal const uint VmdCurveCubicBezier = 1;

        [StructLayout(LayoutKind.Sequential)]
        internal struct PhysicsStepStats
        {
            internal float inputDtSeconds;
            internal float clampedDtSeconds;
            internal uint substeps;
            internal float accumulatorSeconds;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct PhysicsWorldStepReport
        {
            internal PhysicsStepStats tick;
            internal IntPtr kinematicRigidbodiesFed;
            internal IntPtr bonesWrittenBack;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct ReductionTolerances
        {
            internal const float UnityPositionTolerance = 0.01f;
            internal const float HighPrecisionUnityPositionTolerance = 0.001f;
            internal const float RotationToleranceRadians = 0.005f;
            internal const float MorphWeightTolerance = 0.0001f;

            internal float localPosition;
            internal float localRotationRadians;
            internal float worldPosition;
            internal float worldRotationRadians;
            internal float morphWeight;

            internal static ReductionTolerances Default => new ReductionTolerances
            {
                localPosition = 1.0e-4f,
                localRotationRadians = 1.0e-4f,
                worldPosition = 1.0e-4f,
                worldRotationRadians = 1.0e-4f,
                morphWeight = 1.0e-4f
            };

            internal static ReductionTolerances ForUnityAnimationClip(
                float importScale,
                bool highPrecision = false)
            {
                if (!float.IsFinite(importScale) || importScale <= 0.0f)
                {
                    throw new ArgumentOutOfRangeException(nameof(importScale));
                }

                float unityPositionTolerance = highPrecision
                    ? HighPrecisionUnityPositionTolerance
                    : UnityPositionTolerance;
                float sourcePositionTolerance = unityPositionTolerance / importScale;
                return new ReductionTolerances
                {
                    localPosition = sourcePositionTolerance,
                    localRotationRadians = RotationToleranceRadians,
                    worldPosition = sourcePositionTolerance,
                    worldRotationRadians = RotationToleranceRadians,
                    morphWeight = MorphWeightTolerance
                };
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct GenericCurveInfo
        {
            internal uint structSize;
            internal uint abiVersion;
            internal uint reductionTarget;
            internal uint coordinateSystem;
            internal uint lengthUnit;
            internal uint angleUnit;
            internal uint timeUnit;
            internal uint tangentUnit;
            internal ulong modelIdentity;
            internal float startFrame;
            internal float frameStep;
            internal IntPtr frameCount;
            internal IntPtr boneCount;
            internal IntPtr morphCount;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct PhysicsHostPoseView
        {
            internal IntPtr localPositionOffsetsXyz;
            internal IntPtr localRotationXyzw;
            internal IntPtr localScalesXyz;
            internal IntPtr boneCount;
            internal IntPtr morphWeights;
            internal IntPtr morphCount;
            internal IntPtr ikEnabled;
            internal IntPtr ikCount;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct PhysicsRigidbodyDescriptor
        {
            internal uint shape;
            internal float shapeSizeX;
            internal float shapeSizeY;
            internal float shapeSizeZ;
            internal float positionX;
            internal float positionY;
            internal float positionZ;
            internal float rotationX;
            internal float rotationY;
            internal float rotationZ;
            internal float mass;
            internal float linearDamping;
            internal float angularDamping;
            internal float friction;
            internal float restitution;
            internal ushort collisionGroup;
            internal ushort collisionMask;
            internal int boneIndex;
            internal uint mode;
            internal float bodyFromBonePositionX;
            internal float bodyFromBonePositionY;
            internal float bodyFromBonePositionZ;
            internal float bodyFromBoneRotationX;
            internal float bodyFromBoneRotationY;
            internal float bodyFromBoneRotationZ;
            internal float bodyFromBoneRotationW;
            internal float boneFromBodyPositionX;
            internal float boneFromBodyPositionY;
            internal float boneFromBodyPositionZ;
            internal float boneFromBodyRotationX;
            internal float boneFromBodyRotationY;
            internal float boneFromBodyRotationZ;
            internal float boneFromBodyRotationW;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct PhysicsJointDescriptor
        {
            internal uint kind;
            internal IntPtr rigidbodyA;
            internal IntPtr rigidbodyB;
            internal float positionX;
            internal float positionY;
            internal float positionZ;
            internal float rotationX;
            internal float rotationY;
            internal float rotationZ;
            internal float translationLowerX;
            internal float translationLowerY;
            internal float translationLowerZ;
            internal float translationUpperX;
            internal float translationUpperY;
            internal float translationUpperZ;
            internal float rotationLowerX;
            internal float rotationLowerY;
            internal float rotationLowerZ;
            internal float rotationUpperX;
            internal float rotationUpperY;
            internal float rotationUpperZ;
            internal float springTranslationX;
            internal float springTranslationY;
            internal float springTranslationZ;
            internal float springRotationX;
            internal float springRotationY;
            internal float springRotationZ;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct GenericCurveDescriptor
        {
            internal uint structSize;
            internal uint abiVersion;
            internal uint kind;
            internal uint targetIndex;
            internal int parentIndex;
            internal uint valueFlags;
            internal uint interpolation;
            internal uint rotationBasis;
            internal IntPtr keyCount;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct GenericCurveKey
        {
            internal IntPtr sampleIndex;
            internal float frame;
            internal float translationX;
            internal float translationY;
            internal float translationZ;
            internal float rotationX;
            internal float rotationY;
            internal float rotationZ;
            internal float rotationW;
            internal float scalar;
            internal float segmentPrevOutTranslationX;
            internal float segmentPrevOutTranslationY;
            internal float segmentPrevOutTranslationZ;
            internal float segmentCurrentInTranslationX;
            internal float segmentCurrentInTranslationY;
            internal float segmentCurrentInTranslationZ;
            internal float segmentFromPreviousStartEulerX;
            internal float segmentFromPreviousStartEulerY;
            internal float segmentFromPreviousStartEulerZ;
            internal float segmentFromPreviousEndEulerX;
            internal float segmentFromPreviousEndEulerY;
            internal float segmentFromPreviousEndEulerZ;
            internal float segmentPrevOutRotationX;
            internal float segmentPrevOutRotationY;
            internal float segmentPrevOutRotationZ;
            internal float segmentCurrentInRotationX;
            internal float segmentCurrentInRotationY;
            internal float segmentCurrentInRotationZ;
            internal float segmentPrevOutScalar;
            internal float segmentCurrentInScalar;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct VmdCurve
        {
            internal uint kind;
            internal float x1;
            internal float y1;
            internal float x2;
            internal float y2;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct VmdCameraKeyframe
        {
            internal uint frame;
            internal float distance;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3, ArraySubType = UnmanagedType.R4)]
            internal float[] positionXyz;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3, ArraySubType = UnmanagedType.R4)]
            internal float[] rotationXyz;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 24, ArraySubType = UnmanagedType.U1)]
            internal byte[] interpolation;
            internal uint fov;
            internal byte perspective;
            internal VmdCurve positionX;
            internal VmdCurve positionY;
            internal VmdCurve positionZ;
            internal VmdCurve rotation;
            internal VmdCurve distanceCurve;
            internal VmdCurve fovCurve;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct VmdLightKeyframe
        {
            internal uint frame;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3, ArraySubType = UnmanagedType.R4)]
            internal float[] color;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3, ArraySubType = UnmanagedType.R4)]
            internal float[] direction;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct VmdSelfShadowKeyframe
        {
            internal uint frame;
            internal byte mode;
            internal float distance;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct VmdBoneKeyframe
        {
            internal uint boneIndex;
            internal uint frame;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3, ArraySubType = UnmanagedType.R4)]
            internal float[] positionXyz;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4, ArraySubType = UnmanagedType.R4)]
            internal float[] rotationXyzw;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64, ArraySubType = UnmanagedType.U1)]
            internal byte[] interpolation;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct VmdPropertyKeyframe
        {
            internal uint frame;
            internal byte visible;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3, ArraySubType = UnmanagedType.U1)]
            internal byte[] reserved;
            internal IntPtr ikEntryOffset;
            internal IntPtr ikEntryCount;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct VmdPropertyIkEntry
        {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 20, ArraySubType = UnmanagedType.U1)]
            internal byte[] nameBytes;
            internal byte enabled;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3, ArraySubType = UnmanagedType.U1)]
            internal byte[] reserved;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct BoneTrackCurve
        {
            internal uint kind;
            internal float x1;
            internal float y1;
            internal float x2;
            internal float y2;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct BoneTrackDescriptor
        {
            internal uint boneIndex;
            internal IntPtr keyCount;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct BoneTrackKey
        {
            internal uint boneIndex;
            internal uint frame;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3, ArraySubType = UnmanagedType.R4)]
            internal float[] positionXyz;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4, ArraySubType = UnmanagedType.R4)]
            internal float[] rotationXyzw;
            internal BoneTrackCurve translationX;
            internal BoneTrackCurve translationY;
            internal BoneTrackCurve translationZ;
            internal BoneTrackCurve rotation;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct MorphTrackDescriptor
        {
            internal uint morphIndex;
            internal IntPtr keyCount;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct MorphTrackKey
        {
            internal uint morphIndex;
            internal uint frame;
            internal float weight;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct PropertyTrackDescriptor
        {
            internal IntPtr keyCount;
            internal IntPtr ikEnabledCount;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct PropertyTrackKey
        {
            internal uint frame;
            internal IntPtr ikEnabledOffset;
            internal IntPtr ikEnabledCount;
        }

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_abi_version", CallingConvention = CallingConvention.Cdecl)]
        internal static extern uint AbiVersion();

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_feature_flags", CallingConvention = CallingConvention.Cdecl)]
        internal static extern uint FeatureFlags();

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_last_error_message", CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr LastErrorMessage();

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_model_create_from_pmx_bytes", CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr ModelCreateFromPmxBytes(byte[] data, IntPtr len);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_model_bone_count", CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr ModelBoneCount(IntPtr model);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_model_morph_count", CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr ModelMorphCount(IntPtr model);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_model_ik_count", CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr ModelIkCount(IntPtr model);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_model_free", CallingConvention = CallingConvention.Cdecl)]
        internal static extern void ModelFree(IntPtr model);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_clip_create_from_vmd_bytes_for_model", CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr ClipCreateFromVmdBytesForModel(IntPtr model, byte[] data, IntPtr len);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_vmd_context_create_from_vmd_bytes", CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr VmdContextCreateFromVmdBytes(byte[] data, IntPtr len);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_vmd_context_free", CallingConvention = CallingConvention.Cdecl)]
        internal static extern void VmdContextFree(IntPtr context);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_vmd_context_camera_frame_count", CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr VmdContextCameraFrameCount(IntPtr context);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_vmd_context_copy_camera_keyframes", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int VmdContextCopyCameraKeyframes(
            IntPtr context,
            IntPtr outKeys,
            IntPtr outKeyCapacity,
            out IntPtr outWritten);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_vmd_context_light_frame_count", CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr VmdContextLightFrameCount(IntPtr context);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_vmd_context_copy_light_keyframes", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int VmdContextCopyLightKeyframes(
            IntPtr context,
            IntPtr outKeys,
            IntPtr outKeyCapacity,
            out IntPtr outWritten);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_vmd_context_self_shadow_frame_count", CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr VmdContextSelfShadowFrameCount(IntPtr context);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_vmd_context_copy_self_shadow_keyframes", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int VmdContextCopySelfShadowKeyframes(
            IntPtr context,
            IntPtr outKeys,
            IntPtr outKeyCapacity,
            out IntPtr outWritten);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_vmd_context_property_frame_count", CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr VmdContextPropertyFrameCount(IntPtr context);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_vmd_context_property_ik_entry_count", CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr VmdContextPropertyIkEntryCount(IntPtr context);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_vmd_context_copy_property_keyframes", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int VmdContextCopyPropertyKeyframes(
            IntPtr context,
            IntPtr outKeys,
            IntPtr outKeyCapacity,
            out IntPtr outWritten);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_vmd_context_copy_property_ik_entries", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int VmdContextCopyPropertyIkEntries(
            IntPtr context,
            IntPtr outEntries,
            IntPtr outEntryCapacity,
            out IntPtr outWritten);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_clip_create_from_vmd_context_for_model", CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr ClipCreateFromVmdContextForModel(IntPtr model, IntPtr context);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_vmd_context_bone_keyframe_count_for_model", CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr VmdContextBoneKeyframeCountForModel(IntPtr model, IntPtr context);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_vmd_context_copy_bone_keyframes_for_model", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int VmdContextCopyBoneKeyframesForModel(
            IntPtr model,
            IntPtr context,
            IntPtr outKeys,
            IntPtr outKeyCapacity,
            out IntPtr outWritten,
            out IntPtr outSkipped);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_clip_bone_track_count", CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr ClipBoneTrackCount(IntPtr clip);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_clip_bone_track_descriptor", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ClipBoneTrackDescriptor(
            IntPtr clip,
            IntPtr trackIndex,
            ref BoneTrackDescriptor outDescriptor);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_clip_bone_track_key_count", CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr ClipBoneTrackKeyCount(IntPtr clip, IntPtr trackIndex);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_clip_copy_bone_track_keys", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ClipCopyBoneTrackKeys(
            IntPtr clip,
            IntPtr trackIndex,
            IntPtr outKeys,
            IntPtr outKeyCapacity,
            out IntPtr outWritten);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_clip_morph_track_count", CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr ClipMorphTrackCount(IntPtr clip);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_clip_morph_track_descriptor", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ClipMorphTrackDescriptor(
            IntPtr clip,
            IntPtr trackIndex,
            ref MorphTrackDescriptor outDescriptor);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_clip_morph_track_key_count", CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr ClipMorphTrackKeyCount(IntPtr clip, IntPtr trackIndex);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_clip_copy_morph_track_keys", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ClipCopyMorphTrackKeys(
            IntPtr clip,
            IntPtr trackIndex,
            IntPtr outKeys,
            IntPtr outKeyCapacity,
            out IntPtr outWritten);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_clip_property_track_count", CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr ClipPropertyTrackCount(IntPtr clip);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_clip_property_track_descriptor", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ClipPropertyTrackDescriptor(
            IntPtr clip,
            ref PropertyTrackDescriptor outDescriptor);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_clip_property_track_key_count", CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr ClipPropertyTrackKeyCount(IntPtr clip);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_clip_property_track_ik_enabled_count", CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr ClipPropertyTrackIkEnabledCount(IntPtr clip);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_clip_copy_property_track_keys", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ClipCopyPropertyTrackKeys(
            IntPtr clip,
            IntPtr outKeys,
            IntPtr outKeyCapacity,
            out IntPtr outWritten);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_clip_copy_property_track_ik_enabled", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ClipCopyPropertyTrackIkEnabled(
            IntPtr clip,
            IntPtr outStates,
            IntPtr outStateCapacity,
            out IntPtr outWritten);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_vmd_camera_track_create_from_vmd_bytes", CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr VmdCameraTrackCreateFromVmdBytes(byte[] data, IntPtr len);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_vmd_camera_track_frame_count", CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr VmdCameraTrackFrameCount(IntPtr track);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_vmd_camera_track_copy_keyframes", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int VmdCameraTrackCopyKeyframes(
            IntPtr track,
            IntPtr outKeys,
            IntPtr outKeyCapacity,
            out IntPtr outWritten);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_vmd_camera_track_sample", CallingConvention = CallingConvention.Cdecl)]
        internal static extern byte VmdCameraTrackSample(IntPtr track, float frame, [Out] float[] outF32, IntPtr outF32Len);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_vmd_light_track_sample", CallingConvention = CallingConvention.Cdecl)]
        internal static extern byte VmdLightTrackSample(IntPtr track, float frame, [Out] float[] outF32, IntPtr outF32Len);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_vmd_light_track_create_from_vmd_bytes", CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr VmdLightTrackCreateFromVmdBytes(byte[] data, IntPtr len);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_vmd_light_track_frame_count", CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr VmdLightTrackFrameCount(IntPtr track);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_vmd_light_track_copy_keyframes", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int VmdLightTrackCopyKeyframes(
            IntPtr track,
            IntPtr outKeys,
            IntPtr outKeyCapacity,
            out IntPtr outWritten);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_vmd_light_track_free", CallingConvention = CallingConvention.Cdecl)]
        internal static extern void VmdLightTrackFree(IntPtr track);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_vmd_self_shadow_track_sample", CallingConvention = CallingConvention.Cdecl)]
        internal static extern byte VmdSelfShadowTrackSample(IntPtr track, float frame, [Out] float[] outF32, IntPtr outF32Len);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_vmd_self_shadow_track_create_from_vmd_bytes", CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr VmdSelfShadowTrackCreateFromVmdBytes(byte[] data, IntPtr len);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_vmd_self_shadow_track_frame_count", CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr VmdSelfShadowTrackFrameCount(IntPtr track);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_vmd_self_shadow_track_copy_keyframes", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int VmdSelfShadowTrackCopyKeyframes(
            IntPtr track,
            IntPtr outKeys,
            IntPtr outKeyCapacity,
            out IntPtr outWritten);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_vmd_self_shadow_track_free", CallingConvention = CallingConvention.Cdecl)]
        internal static extern void VmdSelfShadowTrackFree(IntPtr track);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_vmd_camera_track_free", CallingConvention = CallingConvention.Cdecl)]
        internal static extern void VmdCameraTrackFree(IntPtr track);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_clip_free", CallingConvention = CallingConvention.Cdecl)]
        internal static extern void ClipFree(IntPtr clip);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_instance_create_for_model", CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr InstanceCreateForModel(IntPtr model);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_instance_free", CallingConvention = CallingConvention.Cdecl)]
        internal static extern void InstanceFree(IntPtr instance);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_instance_set_physics_mode", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int InstanceSetPhysicsMode(IntPtr instance, uint mode);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_physics_world_create_from_pmx_bytes", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PhysicsWorldCreateFromPmxBytes(byte[] data, IntPtr len, out IntPtr world);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_physics_world_create", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PhysicsWorldCreate(
            [In] PhysicsRigidbodyDescriptor[] rigidbodies,
            IntPtr rigidbodyCount,
            [In] PhysicsJointDescriptor[] joints,
            IntPtr jointCount,
            out IntPtr world);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_physics_world_free", CallingConvention = CallingConvention.Cdecl)]
        internal static extern void PhysicsWorldFree(IntPtr world);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_physics_world_reset", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PhysicsWorldReset(IntPtr world, IntPtr instance, out IntPtr seededRigidbodyCount);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_evaluate_host_frame", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int EvaluateHostFrame(
            IntPtr instance,
            IntPtr world,
            ref PhysicsHostPoseView pose,
            uint action,
            float deltaTime,
            float ikTolerance,
            uint ikMaxIterationsCap,
            out PhysicsWorldStepReport outReport);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_physics_world_rigidbody_count", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PhysicsWorldRigidbodyCount(IntPtr world, out IntPtr rigidbodyCount);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_physics_world_copy_rigidbody_states", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PhysicsWorldCopyRigidbodyStates(IntPtr world, [Out] float[] outTransformsF32, IntPtr outTransformsF32Len);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_instance_evaluate_clip_frame", CallingConvention = CallingConvention.Cdecl)]
        internal static extern byte InstanceEvaluateClipFrame(IntPtr instance, IntPtr clip, float frame);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_instance_world_matrix_f32_len", CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr InstanceWorldMatrixF32Len(IntPtr instance);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_instance_copy_world_matrices", CallingConvention = CallingConvention.Cdecl)]
        internal static extern byte InstanceCopyWorldMatrices(IntPtr instance, [Out] float[] outF32, IntPtr outF32Len);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_instance_morph_weight_len", CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr InstanceMorphWeightLen(IntPtr instance);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_instance_copy_morph_weights", CallingConvention = CallingConvention.Cdecl)]
        internal static extern byte InstanceCopyMorphWeights(IntPtr instance, [Out] float[] outF32, IntPtr outF32Len);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_instance_clip_frame_batch_world_matrix_f32_len", CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr InstanceClipFrameBatchWorldMatrixF32Len(IntPtr instance, IntPtr frameCount);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_instance_clip_frame_batch_morph_weight_f32_len", CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr InstanceClipFrameBatchMorphWeightF32Len(IntPtr instance, IntPtr frameCount);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_instance_evaluate_clip_frame_batch", CallingConvention = CallingConvention.Cdecl)]
        internal static extern byte InstanceEvaluateClipFrameBatch(
            IntPtr instance,
            IntPtr clip,
            float startFrame,
            float frameStep,
            IntPtr frameCount,
            uint workerCount,
            [Out] float[] outWorldMatricesF32,
            IntPtr outWorldMatricesF32Len,
            [Out] float[] outMorphWeightsF32,
            IntPtr outMorphWeightsF32Len);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_reduced_pose_create_from_dense", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ReducedPoseCreateFromDense(
            IntPtr model,
            ulong modelIdentity,
            float[] worldMatricesF32,
            IntPtr worldMatricesF32Len,
            float[] morphWeightsF32,
            IntPtr morphWeightsF32Len,
            IntPtr frameCount,
            float startFrame,
            float frameStep,
            uint target,
            ReductionTolerances tolerances,
            out IntPtr reducedPose);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_reduced_pose_free", CallingConvention = CallingConvention.Cdecl)]
        internal static extern void ReducedPoseFree(IntPtr reducedPose);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_reduced_pose_generic_curve_info", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ReducedPoseGenericCurveInfo(
            IntPtr reducedPose,
            ref GenericCurveInfo info);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_reduced_pose_generic_curve_count", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ReducedPoseGenericCurveCount(
            IntPtr reducedPose,
            out IntPtr curveCount);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_reduced_pose_generic_curve_descriptor", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ReducedPoseGenericCurveDescriptor(
            IntPtr reducedPose,
            IntPtr curveIndex,
            ref GenericCurveDescriptor descriptor);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_reduced_pose_generic_curve_keys", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ReducedPoseGenericCurveKeys(
            IntPtr reducedPose,
            IntPtr curveIndex,
            [Out] GenericCurveKey[]? keys,
            IntPtr keyCapacity,
            IntPtr keyStrideBytes,
            out IntPtr requiredCount);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_instance_ik_enabled_len", CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr InstanceIkEnabledLen(IntPtr instance);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_instance_copy_ik_enabled", CallingConvention = CallingConvention.Cdecl)]
        internal static extern byte InstanceCopyIkEnabled(IntPtr instance, [Out] byte[] outU8, IntPtr outU8Len);

        internal static uint ValidateAbiVersion()
        {
            uint abiVersion = AbiVersion();
            if (abiVersion != ExpectedAbiVersion)
            {
                throw new InvalidOperationException(
                    $"mmd-runtime ABI version {abiVersion} is not supported. Expected {ExpectedAbiVersion}.");
            }

            return abiVersion;
        }

        internal static uint ValidateVmdSharedContextCapability()
        {
            uint abiVersion = AbiVersion();
            if (abiVersion != ExpectedAbiVersion)
            {
                throw new MmdRuntimeUnsupportedException(
                    $"mmd-runtime ABI version {abiVersion} is not supported for the shared VMD context. " +
                    $"Expected {ExpectedAbiVersion}.");
            }

            uint featureFlags = FeatureFlags();
            if ((featureFlags & FeatureVmdSharedContext) == 0)
            {
                throw new MmdRuntimeUnsupportedException(
                    "mmd-runtime does not provide the shared VMD context feature " +
                    $"(required feature bit 9, flags=0x{featureFlags:X8}).");
            }

            return VmdSharedContextAbiVersionV1;
        }

        internal static uint ValidateVmdSharedContextBoneReadbackCapability()
        {
            ValidateVmdSharedContextCapability();
            uint featureFlags = FeatureFlags();
            if ((featureFlags & FeatureVmdSharedContextBoneReadback) == 0)
            {
                throw new MmdRuntimeUnsupportedException(
                    "mmd-runtime does not provide shared VMD raw bone readback " +
                    $"(required feature bit 10, flags=0x{featureFlags:X8}).");
            }

            return VmdSharedContextBoneReadbackAbiVersionV1;
        }

    }
    internal sealed partial class MmdRuntimeFfiPlaybackSession : IDisposable
    {
        internal const long MaxReductionInputBytes = 256L * 1024L * 1024L;

        private IntPtr model;
        private IntPtr clip;
        private IntPtr instance;
        private bool disposed;

        private MmdRuntimeFfiPlaybackSession(IntPtr model, IntPtr clip, IntPtr instance)
        {
            this.model = model;
            this.clip = clip;
            this.instance = instance;
            AbiVersion = MmdRuntimeFfiMethods.ExpectedAbiVersion;
            BoneCount = MmdFfiMarshal.CheckedIntPtrToInt(MmdRuntimeFfiMethods.ModelBoneCount(model), "bone count");
            MorphCount = MmdFfiMarshal.CheckedIntPtrToInt(MmdRuntimeFfiMethods.ModelMorphCount(model), "morph count");
            IkCount = MmdFfiMarshal.CheckedIntPtrToInt(MmdRuntimeFfiMethods.ModelIkCount(model), "IK count");
            WorldMatrixFloatCount = MmdFfiMarshal.CheckedIntPtrToInt(MmdRuntimeFfiMethods.InstanceWorldMatrixF32Len(instance), "world matrix float count");
            MorphWeightCount = MmdFfiMarshal.CheckedIntPtrToInt(MmdRuntimeFfiMethods.InstanceMorphWeightLen(instance), "morph weight count");
            IkEnabledCount = MmdFfiMarshal.CheckedIntPtrToInt(MmdRuntimeFfiMethods.InstanceIkEnabledLen(instance), "IK enabled count");
        }

        public uint AbiVersion { get; }
        public int BoneCount { get; }
        public int MorphCount { get; }
        public int IkCount { get; }
        public int WorldMatrixFloatCount { get; }
        public int MorphWeightCount { get; }
        public int IkEnabledCount { get; }

        internal IntPtr GetNativeModelHandle()
        {
            ThrowIfDisposed();
            return model;
        }

        public static MmdRuntimeFfiPlaybackSession Create(
            byte[] pmxBytes,
            byte[] vmdBytes,
            bool abiAlreadyValidated = false)
        {
            if (pmxBytes == null || pmxBytes.Length == 0)
            {
                throw new ArgumentException("PMX bytes are required.", nameof(pmxBytes));
            }

            if (vmdBytes == null || vmdBytes.Length == 0)
            {
                throw new ArgumentException("VMD bytes are required.", nameof(vmdBytes));
            }

            if (!abiAlreadyValidated)
            {
                MmdRuntimeFfiMethods.ValidateAbiVersion();
            }

            return CreateFromModelClipFactory(
                pmxBytes,
                model => MmdRuntimeFfiMethods.ClipCreateFromVmdBytesForModel(
                    model,
                    vmdBytes,
                    new IntPtr(vmdBytes.Length)),
                "mmd-runtime VMD import returned a null clip",
                unavailableOperation: null);
        }

        public static MmdRuntimeFfiPlaybackSession CreateFromVmdContext(
            byte[] pmxBytes,
            MmdRuntimeFfiVmdContext context,
            bool abiAlreadyValidated = false)
        {
            if (pmxBytes == null || pmxBytes.Length == 0)
            {
                throw new ArgumentException("PMX bytes are required.", nameof(pmxBytes));
            }

            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (!abiAlreadyValidated)
            {
                try
                {
                    MmdRuntimeFfiMethods.ValidateVmdSharedContextCapability();
                }
                catch (DllNotFoundException exception)
                {
                    throw MmdRuntimeNativeBoundary.Unavailable(
                        "shared VMD context-backed playback session", exception);
                }
                catch (EntryPointNotFoundException exception)
                {
                    throw MmdRuntimeNativeBoundary.Unavailable(
                        "shared VMD context-backed playback session", exception);
                }
                catch (BadImageFormatException exception)
                {
                    throw MmdRuntimeNativeBoundary.Unavailable(
                        "shared VMD context-backed playback session", exception);
                }
            }

            IntPtr contextHandle = context.GetNativeHandle();
            return CreateFromModelClipFactory(
                pmxBytes,
                model => MmdRuntimeFfiMethods.ClipCreateFromVmdContextForModel(model, contextHandle),
                "mmd-runtime VMD context import returned a null clip",
                "shared VMD context-backed playback session");
        }

        private static MmdRuntimeFfiPlaybackSession CreateFromModelClipFactory(
            byte[] pmxBytes,
            Func<IntPtr, IntPtr> createClip,
            string clipFailureMessage,
            string? unavailableOperation)
        {
            IntPtr model = IntPtr.Zero;
            IntPtr clip = IntPtr.Zero;
            IntPtr instance = IntPtr.Zero;
            try
            {
                model = MmdRuntimeFfiMethods.ModelCreateFromPmxBytes(
                    pmxBytes,
                    new IntPtr(pmxBytes.Length));
                if (model == IntPtr.Zero)
                {
                    throw new InvalidOperationException(
                        "mmd-runtime PMX import returned a null model: " + MmdRuntimeFfiMarshal.LastErrorMessage());
                }

                clip = createClip(model);
                if (clip == IntPtr.Zero)
                {
                    throw new InvalidOperationException(clipFailureMessage + ": " +
                        MmdRuntimeFfiMarshal.LastErrorMessage());
                }

                instance = MmdRuntimeFfiMethods.InstanceCreateForModel(model);
                if (instance == IntPtr.Zero)
                {
                    throw new InvalidOperationException(
                        "mmd-runtime instance creation returned null: " + MmdRuntimeFfiMarshal.LastErrorMessage());
                }

                MmdRuntimeFfiPlaybackSession session = new MmdRuntimeFfiPlaybackSession(model, clip, instance);
                model = IntPtr.Zero;
                clip = IntPtr.Zero;
                instance = IntPtr.Zero;
                return session;
            }
            catch (DllNotFoundException exception)
            {
                if (unavailableOperation == null)
                {
                    throw;
                }

                throw MmdRuntimeNativeBoundary.Unavailable(unavailableOperation, exception);
            }
            catch (EntryPointNotFoundException exception)
            {
                if (unavailableOperation == null)
                {
                    throw;
                }

                throw MmdRuntimeNativeBoundary.Unavailable(unavailableOperation, exception);
            }
            catch (BadImageFormatException exception)
            {
                if (unavailableOperation == null)
                {
                    throw;
                }

                throw MmdRuntimeNativeBoundary.Unavailable(unavailableOperation, exception);
            }
            finally
            {
                if (instance != IntPtr.Zero)
                {
                    MmdRuntimeFfiMethods.InstanceFree(instance);
                }

                if (clip != IntPtr.Zero)
                {
                    MmdRuntimeFfiMethods.ClipFree(clip);
                }

                if (model != IntPtr.Zero)
                {
                    MmdRuntimeFfiMethods.ModelFree(model);
                }
            }
        }

        public void EvaluateAndCopy(float frame, float[] worldMatrices, float[] morphWeights, byte[] ikEnabled)
        {
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(MmdRuntimeFfiPlaybackSession));
            }

            byte evaluated = MmdRuntimeFfiMethods.InstanceEvaluateClipFrame(instance, clip, frame);
            if (evaluated == 0)
            {
                throw new InvalidOperationException("mmd-runtime clip frame evaluation returned false.");
            }

            if (worldMatrices.Length > 0 &&
                MmdRuntimeFfiMethods.InstanceCopyWorldMatrices(instance, worldMatrices, new IntPtr(worldMatrices.Length)) == 0)
            {
                throw new InvalidOperationException("mmd-runtime world matrix copy returned false.");
            }

            if (morphWeights.Length > 0 &&
                MmdRuntimeFfiMethods.InstanceCopyMorphWeights(instance, morphWeights, new IntPtr(morphWeights.Length)) == 0)
            {
                throw new InvalidOperationException("mmd-runtime morph weight copy returned false.");
            }

            if (ikEnabled.Length > 0 &&
                MmdRuntimeFfiMethods.InstanceCopyIkEnabled(instance, ikEnabled, new IntPtr(ikEnabled.Length)) == 0)
            {
                throw new InvalidOperationException("mmd-runtime IK enabled copy returned false.");
            }
        }

        public void EvaluateBatch(
            float startFrame,
            float frameStep,
            int frameCount,
            uint workerCount,
            float[] worldMatrices,
            float[] morphWeights)
        {
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(MmdRuntimeFfiPlaybackSession));
            }

            if (!float.IsFinite(startFrame) || !float.IsFinite(frameStep))
            {
                throw new ArgumentOutOfRangeException(nameof(startFrame), "Batch frame inputs must be finite.");
            }

            if (frameCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(frameCount));
            }

            if (worldMatrices == null)
            {
                throw new ArgumentNullException(nameof(worldMatrices));
            }

            if (morphWeights == null)
            {
                throw new ArgumentNullException(nameof(morphWeights));
            }

            int requiredWorldCount = MmdFfiMarshal.CheckedIntPtrToInt(
                MmdRuntimeFfiMethods.InstanceClipFrameBatchWorldMatrixF32Len(instance, new IntPtr(frameCount)),
                "batch world matrix float count");
            int requiredMorphCount = MmdFfiMarshal.CheckedIntPtrToInt(
                MmdRuntimeFfiMethods.InstanceClipFrameBatchMorphWeightF32Len(instance, new IntPtr(frameCount)),
                "batch morph weight float count");
            if (worldMatrices.Length < requiredWorldCount)
            {
                throw new ArgumentException(
                    $"Batch world matrix buffer requires {requiredWorldCount} floats.", nameof(worldMatrices));
            }

            if (morphWeights.Length < requiredMorphCount)
            {
                throw new ArgumentException(
                    $"Batch morph weight buffer requires {requiredMorphCount} floats.", nameof(morphWeights));
            }

            if (MmdRuntimeFfiMethods.InstanceEvaluateClipFrameBatch(
                    instance,
                    clip,
                    startFrame,
                    frameStep,
                    new IntPtr(frameCount),
                    workerCount,
                    worldMatrices,
                    new IntPtr(worldMatrices.Length),
                    morphWeights,
                    new IntPtr(morphWeights.Length)) == 0)
            {
                throw new InvalidOperationException("mmd-runtime batch clip frame evaluation returned false.");
            }
        }

        internal MmdRuntimeReducedPose ReduceBatch(
            float batchStartFrame,
            int frameCount,
            uint workerCount,
            MmdRuntimeFfiMethods.ReductionTolerances tolerances)
        {
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(MmdRuntimeFfiPlaybackSession));
            }

            if (!float.IsFinite(batchStartFrame) || frameCount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(frameCount));
            }

            ThrowIfReductionInputTooLarge(WorldMatrixFloatCount, MorphWeightCount, frameCount);
            var worldMatrices = new float[checked(WorldMatrixFloatCount * frameCount)];
            var morphWeights = new float[checked(MorphWeightCount * frameCount)];
            EvaluateBatch(batchStartFrame, 1.0f, frameCount, workerCount, worldMatrices, morphWeights);

            IntPtr reducedPose = IntPtr.Zero;
            int status = MmdRuntimeFfiMethods.ReducedPoseCreateFromDense(
                model,
                0,
                worldMatrices,
                new IntPtr(worldMatrices.Length),
                morphWeights,
                new IntPtr(morphWeights.Length),
                new IntPtr(frameCount),
                0.0f,
                1.0f,
                MmdRuntimeFfiMethods.ReductionTargetDccCubic,
                tolerances,
                out reducedPose);
            if (status != MmdRuntimeFfiMethods.StatusOk || reducedPose == IntPtr.Zero)
            {
                if (reducedPose != IntPtr.Zero)
                {
                    MmdRuntimeFfiMethods.ReducedPoseFree(reducedPose);
                }

                throw new InvalidOperationException(
                    "mmd-runtime reduced pose creation failed with status " + status + ": "
                    + MmdRuntimeFfiMarshal.LastErrorMessage());
            }

            return new MmdRuntimeReducedPose(reducedPose);
        }

        internal static void ThrowIfReductionInputTooLarge(
            int worldMatrixFloatCount,
            int morphWeightCount,
            int frameCount)
        {
            if (worldMatrixFloatCount < 0 || morphWeightCount < 0 || frameCount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(frameCount));
            }

            long inputBytes = checked(
                ((long)worldMatrixFloatCount + morphWeightCount) * frameCount * sizeof(float));
            if (inputBytes > MaxReductionInputBytes)
            {
                throw new MmdRuntimeReductionInputTooLargeException(
                    "sparse reduction requires " + inputBytes
                    + " bytes of dense native input, exceeding the "
                    + MaxReductionInputBytes + " byte safety limit");
            }
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            Exception? firstCleanupException = null;
            try
            {
                IntPtr handle = instance;
                instance = IntPtr.Zero;
                if (handle != IntPtr.Zero)
                {
                    MmdRuntimeFfiMethods.InstanceFree(handle);
                }
            }
            catch (Exception exception)
            {
                firstCleanupException ??= exception;
            }

            try
            {
                IntPtr handle = clip;
                clip = IntPtr.Zero;
                if (handle != IntPtr.Zero)
                {
                    MmdRuntimeFfiMethods.ClipFree(handle);
                }
            }
            catch (Exception exception)
            {
                firstCleanupException ??= exception;
            }

            try
            {
                IntPtr handle = model;
                model = IntPtr.Zero;
                if (handle != IntPtr.Zero)
                {
                    MmdRuntimeFfiMethods.ModelFree(handle);
                }
            }
            catch (Exception exception)
            {
                firstCleanupException ??= exception;
            }

            if (firstCleanupException != null)
            {
                throw firstCleanupException;
            }
        }
    }

    internal sealed class MmdRuntimeReducedPose : IDisposable
    {
        private IntPtr handle;

        internal MmdRuntimeReducedPose(IntPtr handle)
        {
            this.handle = handle != IntPtr.Zero
                ? handle
                : throw new ArgumentException("Reduced pose handle is required.", nameof(handle));
        }

        internal MmdRuntimeFfiMethods.GenericCurveInfo GetGenericCurveInfo()
        {
            ThrowIfDisposed();
            var info = new MmdRuntimeFfiMethods.GenericCurveInfo
            {
                structSize = checked((uint)Marshal.SizeOf<MmdRuntimeFfiMethods.GenericCurveInfo>())
            };
            int status = MmdRuntimeFfiMethods.ReducedPoseGenericCurveInfo(handle, ref info);
            ThrowForStatus(status, "generic curve info");
            return info;
        }

        internal int GetGenericCurveCount()
        {
            ThrowIfDisposed();
            int status = MmdRuntimeFfiMethods.ReducedPoseGenericCurveCount(handle, out IntPtr count);
            ThrowForStatus(status, "generic curve count");
            return MmdFfiMarshal.CheckedIntPtrToInt(count, "reduced pose curve count");
        }

        internal MmdRuntimeFfiMethods.GenericCurveDescriptor GetGenericCurveDescriptor(int curveIndex)
        {
            ThrowIfDisposed();
            var descriptor = new MmdRuntimeFfiMethods.GenericCurveDescriptor
            {
                structSize = checked((uint)Marshal.SizeOf<MmdRuntimeFfiMethods.GenericCurveDescriptor>())
            };
            int status = MmdRuntimeFfiMethods.ReducedPoseGenericCurveDescriptor(
                handle,
                new IntPtr(curveIndex),
                ref descriptor);
            ThrowForStatus(status, "generic curve descriptor");
            return descriptor;
        }

        internal MmdRuntimeFfiMethods.GenericCurveKey[] GetGenericCurveKeys(int curveIndex)
        {
            ThrowIfDisposed();
            IntPtr keyStrideBytes = new IntPtr(Marshal.SizeOf<MmdRuntimeFfiMethods.GenericCurveKey>());
            int status = MmdRuntimeFfiMethods.ReducedPoseGenericCurveKeys(
                handle,
                new IntPtr(curveIndex),
                null,
                IntPtr.Zero,
                keyStrideBytes,
                out IntPtr requiredCount);
            if (status != MmdRuntimeFfiMethods.StatusBufferTooSmall)
            {
                ThrowForStatus(status, "generic curve key count");
            }

            MmdRuntimeFfiMethods.GenericCurveKey[] keys = AllocateGenericCurveKeyBuffer(requiredCount);
            if (keys.Length == 0)
            {
                return keys;
            }

            status = MmdRuntimeFfiMethods.ReducedPoseGenericCurveKeys(
                handle,
                new IntPtr(curveIndex),
                keys,
                new IntPtr(keys.Length),
                keyStrideBytes,
                out IntPtr copiedCount);
            ThrowForStatus(status, "generic curve keys");
            if (copiedCount != requiredCount)
            {
                throw new InvalidOperationException("mmd-runtime reduced pose curve key count changed during enumeration.");
            }

            return keys;
        }

        internal static MmdRuntimeFfiMethods.GenericCurveKey[] AllocateGenericCurveKeyBuffer(
            IntPtr requiredCount)
        {
            int keyCount = MmdFfiMarshal.CheckedIntPtrToInt(
                requiredCount, "reduced pose curve key count");
            return keyCount == 0
                ? Array.Empty<MmdRuntimeFfiMethods.GenericCurveKey>()
                : new MmdRuntimeFfiMethods.GenericCurveKey[keyCount];
        }

        public void Dispose()
        {
            if (handle == IntPtr.Zero)
            {
                return;
            }

            MmdRuntimeFfiMethods.ReducedPoseFree(handle);
            handle = IntPtr.Zero;
        }

        private void ThrowIfDisposed()
        {
            if (handle == IntPtr.Zero)
            {
                throw new ObjectDisposedException(nameof(MmdRuntimeReducedPose));
            }
        }

        private static void ThrowForStatus(int status, string operation)
        {
            if (status == MmdRuntimeFfiMethods.StatusOk)
            {
                return;
            }

            string message = "mmd-runtime reduced pose " + operation + " failed with status " + status + ": "
                             + MmdRuntimeFfiMarshal.LastErrorMessage();
            if (status == MmdRuntimeFfiMethods.StatusUnsupported)
            {
                throw new MmdRuntimeUnsupportedException(message);
            }

            throw new InvalidOperationException(message);
        }
    }

    internal sealed class MmdRuntimeUnsupportedException : Exception
    {
        internal MmdRuntimeUnsupportedException(string message) : base(message)
        {
        }
    }

    internal sealed class MmdRuntimeReductionInputTooLargeException : Exception
    {
        internal MmdRuntimeReductionInputTooLargeException(string message) : base(message)
        {
        }
    }

    internal static class MmdRuntimeFfiMarshal
    {
        internal static string LastErrorMessage()
        {
            IntPtr message = MmdRuntimeFfiMethods.LastErrorMessage();
            return message == IntPtr.Zero ? "no native diagnostic" : Marshal.PtrToStringAnsi(message) ?? "no native diagnostic";
        }
    }

}

