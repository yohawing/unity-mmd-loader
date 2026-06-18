using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Yohawing.MmdUnity.Parser
{
    internal static class MmdParserFfiMethods
    {
        internal const string LibraryName = "mmd_runtime_ffi";
        internal const string ByteBufferFreeEntryPoint = "mmd_runtime_byte_buffer_free";

        // --- Non-JSON summary entrypoints ---
        // These consts and private DllImport decls surface the mmd-anim FFI
        // summary symbols (MmdRuntimePmxSummary / MmdRuntimeVmdSummary) used by
        // NativeMmdParser's active Load* path.
        // See docs/TODO.md (P0 JSON bridge TODO) and ARCHITECTURE.md Native Binary Layout.
        internal const string PmxSummaryCreateEntryPoint = "mmd_runtime_pmx_summary_create_from_bytes";
        internal const string PmxSummaryFreeEntryPoint = "mmd_runtime_pmx_summary_free";
        internal const string PmxSummaryVersionEntryPoint = "mmd_runtime_pmx_summary_version";
        internal const string PmxSummaryVertexCountEntryPoint = "mmd_runtime_pmx_summary_vertex_count";
        internal const string PmxSummaryFaceCountEntryPoint = "mmd_runtime_pmx_summary_face_count";
        internal const string PmxSummaryMaterialCountEntryPoint = "mmd_runtime_pmx_summary_material_count";
        internal const string PmxSummaryBoneCountEntryPoint = "mmd_runtime_pmx_summary_bone_count";
        internal const string PmxSummaryMorphCountEntryPoint = "mmd_runtime_pmx_summary_morph_count";
        internal const string PmxSummaryDisplayFrameCountEntryPoint = "mmd_runtime_pmx_summary_display_frame_count";
        internal const string PmxSummaryRigidbodyCountEntryPoint = "mmd_runtime_pmx_summary_rigidbody_count";
        internal const string PmxSummaryJointCountEntryPoint = "mmd_runtime_pmx_summary_joint_count";
        internal const string PmxSummarySoftBodyCountEntryPoint = "mmd_runtime_pmx_summary_soft_body_count";
        internal const string PmxSummaryAdditionalUvCountEntryPoint = "mmd_runtime_pmx_summary_additional_uv_count";
        internal const string PmxSummaryModelNameEntryPoint = "mmd_runtime_pmx_summary_model_name";
        internal const string PmxSummaryModelNameEnglishEntryPoint = "mmd_runtime_pmx_summary_model_name_english";

        // --- PMX core getter entrypoints (geometry / materials / bones+IK; pinned for post-JSON migration; private DllImport only, never invoked) ---
        // Geometry
        internal const string PmxSummaryIndexCountEntryPoint = "mmd_runtime_pmx_summary_index_count";
        internal const string PmxSummaryIndexEntryPoint = "mmd_runtime_pmx_summary_index";
        internal const string PmxSummaryVertexPositionEntryPoint = "mmd_runtime_pmx_summary_vertex_position";
        internal const string PmxSummaryVertexNormalEntryPoint = "mmd_runtime_pmx_summary_vertex_normal";
        internal const string PmxSummaryVertexUvEntryPoint = "mmd_runtime_pmx_summary_vertex_uv";
        internal const string PmxSummaryVertexSkinBoneIndexEntryPoint = "mmd_runtime_pmx_summary_vertex_skin_bone_index";
        internal const string PmxSummaryVertexSkinWeightEntryPoint = "mmd_runtime_pmx_summary_vertex_skin_weight";
        internal const string PmxSummaryVertexSkinningKindEntryPoint = "mmd_runtime_pmx_summary_vertex_skinning_kind";
        internal const string PmxSummaryVertexSdefEnabledEntryPoint = "mmd_runtime_pmx_summary_vertex_sdef_enabled";
        internal const string PmxSummaryVertexSdefCEntryPoint = "mmd_runtime_pmx_summary_vertex_sdef_c";
        // Materials
        internal const string PmxSummaryMaterialNameEntryPoint = "mmd_runtime_pmx_summary_material_name";
        internal const string PmxSummaryMaterialTexturePathEntryPoint = "mmd_runtime_pmx_summary_material_texture_path";
        internal const string PmxSummaryMaterialSphereTexturePathEntryPoint = "mmd_runtime_pmx_summary_material_sphere_texture_path";
        internal const string PmxSummaryMaterialToonTexturePathEntryPoint = "mmd_runtime_pmx_summary_material_toon_texture_path";
        internal const string PmxSummaryMaterialSphereModeEntryPoint = "mmd_runtime_pmx_summary_material_sphere_mode";
        internal const string PmxSummaryMaterialSharedToonIndexEntryPoint = "mmd_runtime_pmx_summary_material_shared_toon_index";
        internal const string PmxSummaryMaterialDiffuseEntryPoint = "mmd_runtime_pmx_summary_material_diffuse";
        internal const string PmxSummaryMaterialAmbientEntryPoint = "mmd_runtime_pmx_summary_material_ambient";
        internal const string PmxSummaryMaterialEdgeColorEntryPoint = "mmd_runtime_pmx_summary_material_edge_color";
        internal const string PmxSummaryMaterialEdgeSizeEntryPoint = "mmd_runtime_pmx_summary_material_edge_size";
        internal const string PmxSummaryMaterialFaceCountEntryPoint = "mmd_runtime_pmx_summary_material_face_count";
        internal const string PmxSummaryMaterialDoubleSidedEntryPoint = "mmd_runtime_pmx_summary_material_double_sided";
        internal const string PmxSummaryMaterialEdgeFlagEntryPoint = "mmd_runtime_pmx_summary_material_edge_flag";
        // Bones / IK
        internal const string PmxSummaryBoneNameEntryPoint = "mmd_runtime_pmx_summary_bone_name";
        internal const string PmxSummaryBoneParentIndexEntryPoint = "mmd_runtime_pmx_summary_bone_parent_index";
        internal const string PmxSummaryBoneLayerEntryPoint = "mmd_runtime_pmx_summary_bone_layer";
        internal const string PmxSummaryBonePositionEntryPoint = "mmd_runtime_pmx_summary_bone_position";
        internal const string PmxSummaryBoneRotatableEntryPoint = "mmd_runtime_pmx_summary_bone_rotatable";
        internal const string PmxSummaryBoneTranslatableEntryPoint = "mmd_runtime_pmx_summary_bone_translatable";
        internal const string PmxSummaryBoneAppendRotateEntryPoint = "mmd_runtime_pmx_summary_bone_append_rotate";
        internal const string PmxSummaryBoneAppendTranslateEntryPoint = "mmd_runtime_pmx_summary_bone_append_translate";
        internal const string PmxSummaryBoneAppendLocalEntryPoint = "mmd_runtime_pmx_summary_bone_append_local";
        internal const string PmxSummaryBoneAppendParentIndexEntryPoint = "mmd_runtime_pmx_summary_bone_append_parent_index";
        internal const string PmxSummaryBoneAppendWeightEntryPoint = "mmd_runtime_pmx_summary_bone_append_weight";
        internal const string PmxSummaryBoneFixedAxisPresentEntryPoint = "mmd_runtime_pmx_summary_bone_fixed_axis_present";
        internal const string PmxSummaryBoneFixedAxisEntryPoint = "mmd_runtime_pmx_summary_bone_fixed_axis";
        internal const string PmxSummaryBoneLocalAxisPresentEntryPoint = "mmd_runtime_pmx_summary_bone_local_axis_present";
        internal const string PmxSummaryBoneLocalAxisXEntryPoint = "mmd_runtime_pmx_summary_bone_local_axis_x";
        internal const string PmxSummaryBoneLocalAxisZEntryPoint = "mmd_runtime_pmx_summary_bone_local_axis_z";
        internal const string PmxSummaryBoneExternalParentPresentEntryPoint = "mmd_runtime_pmx_summary_bone_external_parent_present";
        internal const string PmxSummaryBoneExternalParentKeyEntryPoint = "mmd_runtime_pmx_summary_bone_external_parent_key";
        internal const string PmxSummaryBoneIkPresentEntryPoint = "mmd_runtime_pmx_summary_bone_ik_present";
        internal const string PmxSummaryBoneIkTargetIndexEntryPoint = "mmd_runtime_pmx_summary_bone_ik_target_index";
        internal const string PmxSummaryBoneIkLoopCountEntryPoint = "mmd_runtime_pmx_summary_bone_ik_loop_count";
        internal const string PmxSummaryBoneIkLimitAngleEntryPoint = "mmd_runtime_pmx_summary_bone_ik_limit_angle";
        internal const string PmxSummaryBoneIkLinkCountEntryPoint = "mmd_runtime_pmx_summary_bone_ik_link_count";
        internal const string PmxSummaryBoneIkLinkBoneIndexEntryPoint = "mmd_runtime_pmx_summary_bone_ik_link_bone_index";
        internal const string PmxSummaryBoneIkLinkLimitPresentEntryPoint = "mmd_runtime_pmx_summary_bone_ik_link_limit_present";
        internal const string PmxSummaryBoneIkLinkLimitLowerEntryPoint = "mmd_runtime_pmx_summary_bone_ik_link_limit_lower";
        internal const string PmxSummaryBoneIkLinkLimitUpperEntryPoint = "mmd_runtime_pmx_summary_bone_ik_link_limit_upper";

        // --- PMX morph getter entrypoints (header/counts + offset families; pinned for post-JSON migration; private DllImport only, never invoked) ---
        // Header / counts
        internal const string PmxSummaryMorphNameEntryPoint = "mmd_runtime_pmx_summary_morph_name";
        internal const string PmxSummaryMorphEnglishNameEntryPoint = "mmd_runtime_pmx_summary_morph_english_name";
        internal const string PmxSummaryMorphKindEntryPoint = "mmd_runtime_pmx_summary_morph_kind";
        internal const string PmxSummaryMorphPanelEntryPoint = "mmd_runtime_pmx_summary_morph_panel";
        internal const string PmxSummaryMorphVertexOffsetCountEntryPoint = "mmd_runtime_pmx_summary_morph_vertex_offset_count";
        internal const string PmxSummaryMorphGroupOffsetCountEntryPoint = "mmd_runtime_pmx_summary_morph_group_offset_count";
        internal const string PmxSummaryMorphBoneOffsetCountEntryPoint = "mmd_runtime_pmx_summary_morph_bone_offset_count";
        internal const string PmxSummaryMorphUvOffsetCountEntryPoint = "mmd_runtime_pmx_summary_morph_uv_offset_count";
        internal const string PmxSummaryMorphAdditionalUvOffsetCountEntryPoint = "mmd_runtime_pmx_summary_morph_additional_uv_offset_count";
        internal const string PmxSummaryMorphMaterialOffsetCountEntryPoint = "mmd_runtime_pmx_summary_morph_material_offset_count";
        internal const string PmxSummaryMorphFlipOffsetCountEntryPoint = "mmd_runtime_pmx_summary_morph_flip_offset_count";
        internal const string PmxSummaryMorphImpulseOffsetCountEntryPoint = "mmd_runtime_pmx_summary_morph_impulse_offset_count";
        // Vertex
        internal const string PmxSummaryMorphVertexOffsetVertexIndexEntryPoint = "mmd_runtime_pmx_summary_morph_vertex_offset_vertex_index";
        internal const string PmxSummaryMorphVertexOffsetPositionEntryPoint = "mmd_runtime_pmx_summary_morph_vertex_offset_position";
        // Group / flip
        internal const string PmxSummaryMorphGroupOffsetMorphIndexEntryPoint = "mmd_runtime_pmx_summary_morph_group_offset_morph_index";
        internal const string PmxSummaryMorphGroupOffsetWeightEntryPoint = "mmd_runtime_pmx_summary_morph_group_offset_weight";
        internal const string PmxSummaryMorphFlipOffsetMorphIndexEntryPoint = "mmd_runtime_pmx_summary_morph_flip_offset_morph_index";
        internal const string PmxSummaryMorphFlipOffsetWeightEntryPoint = "mmd_runtime_pmx_summary_morph_flip_offset_weight";
        // Bone
        internal const string PmxSummaryMorphBoneOffsetBoneIndexEntryPoint = "mmd_runtime_pmx_summary_morph_bone_offset_bone_index";
        internal const string PmxSummaryMorphBoneOffsetTranslationEntryPoint = "mmd_runtime_pmx_summary_morph_bone_offset_translation";
        internal const string PmxSummaryMorphBoneOffsetRotationEntryPoint = "mmd_runtime_pmx_summary_morph_bone_offset_rotation";
        // UV / additional UV
        internal const string PmxSummaryMorphUvOffsetVertexIndexEntryPoint = "mmd_runtime_pmx_summary_morph_uv_offset_vertex_index";
        internal const string PmxSummaryMorphUvOffsetValueEntryPoint = "mmd_runtime_pmx_summary_morph_uv_offset_value";
        internal const string PmxSummaryMorphAdditionalUvOffsetVertexIndexEntryPoint = "mmd_runtime_pmx_summary_morph_additional_uv_offset_vertex_index";
        internal const string PmxSummaryMorphAdditionalUvOffsetUvIndexEntryPoint = "mmd_runtime_pmx_summary_morph_additional_uv_offset_uv_index";
        internal const string PmxSummaryMorphAdditionalUvOffsetValueEntryPoint = "mmd_runtime_pmx_summary_morph_additional_uv_offset_value";
        // Material
        internal const string PmxSummaryMorphMaterialOffsetMaterialIndexEntryPoint = "mmd_runtime_pmx_summary_morph_material_offset_material_index";
        internal const string PmxSummaryMorphMaterialOffsetOperationEntryPoint = "mmd_runtime_pmx_summary_morph_material_offset_operation";
        internal const string PmxSummaryMorphMaterialOffsetDiffuseEntryPoint = "mmd_runtime_pmx_summary_morph_material_offset_diffuse";
        internal const string PmxSummaryMorphMaterialOffsetSpecularEntryPoint = "mmd_runtime_pmx_summary_morph_material_offset_specular";
        internal const string PmxSummaryMorphMaterialOffsetSpecularPowerEntryPoint = "mmd_runtime_pmx_summary_morph_material_offset_specular_power";
        internal const string PmxSummaryMorphMaterialOffsetAmbientEntryPoint = "mmd_runtime_pmx_summary_morph_material_offset_ambient";
        internal const string PmxSummaryMorphMaterialOffsetEdgeColorEntryPoint = "mmd_runtime_pmx_summary_morph_material_offset_edge_color";
        internal const string PmxSummaryMorphMaterialOffsetEdgeSizeEntryPoint = "mmd_runtime_pmx_summary_morph_material_offset_edge_size";
        internal const string PmxSummaryMorphMaterialOffsetTextureFactorEntryPoint = "mmd_runtime_pmx_summary_morph_material_offset_texture_factor";
        internal const string PmxSummaryMorphMaterialOffsetSphereTextureFactorEntryPoint = "mmd_runtime_pmx_summary_morph_material_offset_sphere_texture_factor";
        internal const string PmxSummaryMorphMaterialOffsetToonTextureFactorEntryPoint = "mmd_runtime_pmx_summary_morph_material_offset_toon_texture_factor";
        // Impulse
        internal const string PmxSummaryMorphImpulseOffsetRigidbodyIndexEntryPoint = "mmd_runtime_pmx_summary_morph_impulse_offset_rigidbody_index";
        internal const string PmxSummaryMorphImpulseOffsetLocalEntryPoint = "mmd_runtime_pmx_summary_morph_impulse_offset_local";
        internal const string PmxSummaryMorphImpulseOffsetVelocityEntryPoint = "mmd_runtime_pmx_summary_morph_impulse_offset_velocity";
        internal const string PmxSummaryMorphImpulseOffsetTorqueEntryPoint = "mmd_runtime_pmx_summary_morph_impulse_offset_torque";

        // --- PMX display frame getter entrypoints (pinned for post-JSON migration; private DllImport only, never invoked) ---
        internal const string PmxSummaryDisplayFrameNameEntryPoint = "mmd_runtime_pmx_summary_display_frame_name";
        internal const string PmxSummaryDisplayFrameEnglishNameEntryPoint = "mmd_runtime_pmx_summary_display_frame_english_name";
        internal const string PmxSummaryDisplayFrameSpecialEntryPoint = "mmd_runtime_pmx_summary_display_frame_special";
        internal const string PmxSummaryDisplayFrameItemCountEntryPoint = "mmd_runtime_pmx_summary_display_frame_item_count";
        internal const string PmxSummaryDisplayFrameItemKindEntryPoint = "mmd_runtime_pmx_summary_display_frame_item_kind";
        internal const string PmxSummaryDisplayFrameItemIndexEntryPoint = "mmd_runtime_pmx_summary_display_frame_item_index";

        // --- PMX physics getter entrypoints (rigidbody/joint; pinned for post-JSON migration; private DllImport only, never invoked) ---
        // Rigidbodies
        internal const string PmxSummaryRigidbodyNameEntryPoint = "mmd_runtime_pmx_summary_rigidbody_name";
        internal const string PmxSummaryRigidbodyEnglishNameEntryPoint = "mmd_runtime_pmx_summary_rigidbody_english_name";
        internal const string PmxSummaryRigidbodyBoneIndexEntryPoint = "mmd_runtime_pmx_summary_rigidbody_bone_index";
        internal const string PmxSummaryRigidbodyGroupEntryPoint = "mmd_runtime_pmx_summary_rigidbody_group";
        internal const string PmxSummaryRigidbodyMaskEntryPoint = "mmd_runtime_pmx_summary_rigidbody_mask";
        internal const string PmxSummaryRigidbodyShapeEntryPoint = "mmd_runtime_pmx_summary_rigidbody_shape";
        internal const string PmxSummaryRigidbodySizeEntryPoint = "mmd_runtime_pmx_summary_rigidbody_size";
        internal const string PmxSummaryRigidbodyPositionEntryPoint = "mmd_runtime_pmx_summary_rigidbody_position";
        internal const string PmxSummaryRigidbodyRotationEntryPoint = "mmd_runtime_pmx_summary_rigidbody_rotation";
        internal const string PmxSummaryRigidbodyMassEntryPoint = "mmd_runtime_pmx_summary_rigidbody_mass";
        internal const string PmxSummaryRigidbodyLinearDampingEntryPoint = "mmd_runtime_pmx_summary_rigidbody_linear_damping";
        internal const string PmxSummaryRigidbodyAngularDampingEntryPoint = "mmd_runtime_pmx_summary_rigidbody_angular_damping";
        internal const string PmxSummaryRigidbodyRestitutionEntryPoint = "mmd_runtime_pmx_summary_rigidbody_restitution";
        internal const string PmxSummaryRigidbodyFrictionEntryPoint = "mmd_runtime_pmx_summary_rigidbody_friction";
        internal const string PmxSummaryRigidbodyModeEntryPoint = "mmd_runtime_pmx_summary_rigidbody_mode";
        // Joints
        internal const string PmxSummaryJointNameEntryPoint = "mmd_runtime_pmx_summary_joint_name";
        internal const string PmxSummaryJointEnglishNameEntryPoint = "mmd_runtime_pmx_summary_joint_english_name";
        internal const string PmxSummaryJointKindEntryPoint = "mmd_runtime_pmx_summary_joint_kind";
        internal const string PmxSummaryJointRigidbodyAIndexEntryPoint = "mmd_runtime_pmx_summary_joint_rigidbody_a_index";
        internal const string PmxSummaryJointRigidbodyBIndexEntryPoint = "mmd_runtime_pmx_summary_joint_rigidbody_b_index";
        internal const string PmxSummaryJointPositionEntryPoint = "mmd_runtime_pmx_summary_joint_position";
        internal const string PmxSummaryJointRotationEntryPoint = "mmd_runtime_pmx_summary_joint_rotation";
        internal const string PmxSummaryJointTranslationLowerLimitEntryPoint = "mmd_runtime_pmx_summary_joint_translation_lower_limit";
        internal const string PmxSummaryJointTranslationUpperLimitEntryPoint = "mmd_runtime_pmx_summary_joint_translation_upper_limit";
        internal const string PmxSummaryJointRotationLowerLimitEntryPoint = "mmd_runtime_pmx_summary_joint_rotation_lower_limit";
        internal const string PmxSummaryJointRotationUpperLimitEntryPoint = "mmd_runtime_pmx_summary_joint_rotation_upper_limit";
        internal const string PmxSummaryJointSpringTranslationFactorEntryPoint = "mmd_runtime_pmx_summary_joint_spring_translation_factor";
        internal const string PmxSummaryJointSpringRotationFactorEntryPoint = "mmd_runtime_pmx_summary_joint_spring_rotation_factor";

        internal const string VmdSummaryCreateEntryPoint = "mmd_runtime_vmd_summary_create_from_bytes";
        internal const string VmdSummaryFreeEntryPoint = "mmd_runtime_vmd_summary_free";
        internal const string VmdSummaryMaxFrameEntryPoint = "mmd_runtime_vmd_summary_max_frame";
        internal const string VmdSummaryBoneKeyframeCountEntryPoint = "mmd_runtime_vmd_summary_bone_keyframe_count";
        internal const string VmdSummaryMorphKeyframeCountEntryPoint = "mmd_runtime_vmd_summary_morph_keyframe_count";
        internal const string VmdSummaryPropertyKeyframeCountEntryPoint = "mmd_runtime_vmd_summary_property_keyframe_count";
        internal const string VmdSummaryCameraKeyframeCountEntryPoint = "mmd_runtime_vmd_summary_camera_keyframe_count";
        internal const string VmdSummaryLightKeyframeCountEntryPoint = "mmd_runtime_vmd_summary_light_keyframe_count";
        internal const string VmdSummarySelfShadowKeyframeCountEntryPoint = "mmd_runtime_vmd_summary_self_shadow_keyframe_count";
        internal const string VmdSummaryModelNameEntryPoint = "mmd_runtime_vmd_summary_model_name";

        // --- VMD per-frame model-motion getter entrypoints (pinned for post-JSON migration; private DllImport only, never invoked) ---
        internal const string VmdSummaryBoneFrameNameEntryPoint = "mmd_runtime_vmd_summary_bone_frame_name";
        internal const string VmdSummaryBoneFrameFrameEntryPoint = "mmd_runtime_vmd_summary_bone_frame_frame";
        internal const string VmdSummaryBoneFrameTranslationXEntryPoint = "mmd_runtime_vmd_summary_bone_frame_translation_x";
        internal const string VmdSummaryBoneFrameTranslationYEntryPoint = "mmd_runtime_vmd_summary_bone_frame_translation_y";
        internal const string VmdSummaryBoneFrameTranslationZEntryPoint = "mmd_runtime_vmd_summary_bone_frame_translation_z";
        internal const string VmdSummaryBoneFrameRotationXEntryPoint = "mmd_runtime_vmd_summary_bone_frame_rotation_x";
        internal const string VmdSummaryBoneFrameRotationYEntryPoint = "mmd_runtime_vmd_summary_bone_frame_rotation_y";
        internal const string VmdSummaryBoneFrameRotationZEntryPoint = "mmd_runtime_vmd_summary_bone_frame_rotation_z";
        internal const string VmdSummaryBoneFrameRotationWEntryPoint = "mmd_runtime_vmd_summary_bone_frame_rotation_w";
        internal const string VmdSummaryBoneFrameInterpolationByteEntryPoint = "mmd_runtime_vmd_summary_bone_frame_interpolation_byte";
        internal const string VmdSummaryMorphFrameNameEntryPoint = "mmd_runtime_vmd_summary_morph_frame_name";
        internal const string VmdSummaryMorphFrameFrameEntryPoint = "mmd_runtime_vmd_summary_morph_frame_frame";
        internal const string VmdSummaryMorphFrameWeightEntryPoint = "mmd_runtime_vmd_summary_morph_frame_weight";
        internal const string VmdSummaryPropertyFrameFrameEntryPoint = "mmd_runtime_vmd_summary_property_frame_frame";
        internal const string VmdSummaryPropertyFrameVisibleEntryPoint = "mmd_runtime_vmd_summary_property_frame_visible";
        internal const string VmdSummaryPropertyFrameIkStateCountEntryPoint = "mmd_runtime_vmd_summary_property_frame_ik_state_count";
        internal const string VmdSummaryPropertyFrameIkStateNameEntryPoint = "mmd_runtime_vmd_summary_property_frame_ik_state_name";
        internal const string VmdSummaryPropertyFrameIkStateEnabledEntryPoint = "mmd_runtime_vmd_summary_property_frame_ik_state_enabled";

        // --- VMD per-frame scene-track getter entrypoints. Camera and light getters are now invoked (camera / light frames flow into the neutral IR). Self-shadow keyframes remain count-only for now. ---
        internal const string VmdSummaryCameraFrameFrameEntryPoint = "mmd_runtime_vmd_summary_camera_frame_frame";
        internal const string VmdSummaryCameraFrameDistanceEntryPoint = "mmd_runtime_vmd_summary_camera_frame_distance";
        internal const string VmdSummaryCameraFramePositionXEntryPoint = "mmd_runtime_vmd_summary_camera_frame_position_x";
        internal const string VmdSummaryCameraFramePositionYEntryPoint = "mmd_runtime_vmd_summary_camera_frame_position_y";
        internal const string VmdSummaryCameraFramePositionZEntryPoint = "mmd_runtime_vmd_summary_camera_frame_position_z";
        internal const string VmdSummaryCameraFrameRotationXEntryPoint = "mmd_runtime_vmd_summary_camera_frame_rotation_x";
        internal const string VmdSummaryCameraFrameRotationYEntryPoint = "mmd_runtime_vmd_summary_camera_frame_rotation_y";
        internal const string VmdSummaryCameraFrameRotationZEntryPoint = "mmd_runtime_vmd_summary_camera_frame_rotation_z";
        internal const string VmdSummaryCameraFrameInterpolationByteEntryPoint = "mmd_runtime_vmd_summary_camera_frame_interpolation_byte";
        internal const string VmdSummaryCameraFrameFovEntryPoint = "mmd_runtime_vmd_summary_camera_frame_fov";
        internal const string VmdSummaryCameraFramePerspectiveEntryPoint = "mmd_runtime_vmd_summary_camera_frame_perspective";
        internal const string VmdSummaryLightFrameFrameEntryPoint = "mmd_runtime_vmd_summary_light_frame_frame";
        internal const string VmdSummaryLightFrameColorXEntryPoint = "mmd_runtime_vmd_summary_light_frame_color_x";
        internal const string VmdSummaryLightFrameColorYEntryPoint = "mmd_runtime_vmd_summary_light_frame_color_y";
        internal const string VmdSummaryLightFrameColorZEntryPoint = "mmd_runtime_vmd_summary_light_frame_color_z";
        internal const string VmdSummaryLightFrameDirectionXEntryPoint = "mmd_runtime_vmd_summary_light_frame_direction_x";
        internal const string VmdSummaryLightFrameDirectionYEntryPoint = "mmd_runtime_vmd_summary_light_frame_direction_y";
        internal const string VmdSummaryLightFrameDirectionZEntryPoint = "mmd_runtime_vmd_summary_light_frame_direction_z";
        internal const string VmdSummarySelfShadowFrameFrameEntryPoint = "mmd_runtime_vmd_summary_self_shadow_frame_frame";
        internal const string VmdSummarySelfShadowFrameModeEntryPoint = "mmd_runtime_vmd_summary_self_shadow_frame_mode";
        internal const string VmdSummarySelfShadowFrameDistanceEntryPoint = "mmd_runtime_vmd_summary_self_shadow_frame_distance";

        [StructLayout(LayoutKind.Sequential)]
        private readonly struct ByteBuffer
        {
            public readonly IntPtr Data;
            public readonly IntPtr Length;
        }

        [DllImport(LibraryName, EntryPoint = ByteBufferFreeEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern void ByteBufferFree(ByteBuffer buffer);

        internal interface IVmdSummaryAccessor
        {
            string TargetModelName { get; }
            uint MaxFrame { get; }
            int BoneFrameCount { get; }
            int MorphFrameCount { get; }
            int PropertyFrameCount { get; }
            int CameraFrameCount { get; }
            int LightFrameCount { get; }
            int SelfShadowFrameCount { get; }

            string GetBoneFrameName(int frameIndex);
            uint GetBoneFrameFrame(int frameIndex);
            float GetBoneFrameTranslation(int frameIndex, int component);
            float GetBoneFrameRotation(int frameIndex, int component);
            byte GetBoneFrameInterpolationByte(int frameIndex, int offset);

            string GetMorphFrameName(int frameIndex);
            uint GetMorphFrameFrame(int frameIndex);
            float GetMorphFrameWeight(int frameIndex);

            uint GetPropertyFrameFrame(int frameIndex);
            bool GetPropertyFrameVisible(int frameIndex);
            int GetPropertyFrameIkStateCount(int frameIndex);
            string GetPropertyFrameIkStateName(int frameIndex, int ikStateIndex);
            bool GetPropertyFrameIkStateEnabled(int frameIndex, int ikStateIndex);

            uint GetCameraFrameFrame(int frameIndex);
            float GetCameraFrameDistance(int frameIndex);
            float GetCameraFramePosition(int frameIndex, int component);
            float GetCameraFrameRotation(int frameIndex, int component);
            byte GetCameraFrameInterpolationByte(int frameIndex, int offset);
            uint GetCameraFrameFov(int frameIndex);
            bool GetCameraFramePerspective(int frameIndex);

            uint GetLightFrameFrame(int frameIndex);
            float GetLightFrameColor(int frameIndex, int component);
            float GetLightFrameDirection(int frameIndex, int component);
        }

        internal interface IPmxSummaryAccessor
        {
            string ModelName { get; }
            int VertexCount { get; }
            int IndexCount { get; }
            int MaterialCount { get; }
            int BoneCount { get; }
            int MorphCount { get; }
            int RigidbodyCount { get; }
            int JointCount { get; }

            uint GetIndex(int index);
            float GetVertexPosition(int vertexIndex, int component);
            float GetVertexNormal(int vertexIndex, int component);
            float GetVertexUv(int vertexIndex, int component);
            int GetVertexSkinBoneIndex(int vertexIndex, int subIndex);
            float GetVertexSkinWeight(int vertexIndex, int subIndex);
            string GetVertexSkinningKind(int vertexIndex);
            bool GetVertexSdefEnabled(int vertexIndex);
            float GetVertexSdef(int vertexIndex, int which, int component);

            string GetMaterialName(int materialIndex);
            string GetMaterialTexturePath(int materialIndex);
            string GetMaterialSphereTexturePath(int materialIndex);
            string GetMaterialToonTexturePath(int materialIndex);
            string GetMaterialSphereMode(int materialIndex);
            int GetMaterialSharedToonIndex(int materialIndex);
            float GetMaterialDiffuse(int materialIndex, int component);
            float GetMaterialAmbient(int materialIndex, int component);
            float GetMaterialEdgeColor(int materialIndex, int component);
            float GetMaterialEdgeSize(int materialIndex);
            int GetMaterialFaceCount(int materialIndex);
            bool GetMaterialDoubleSided(int materialIndex);
            bool GetMaterialEdgeFlag(int materialIndex);

            string GetBoneName(int boneIndex);
            int GetBoneParentIndex(int boneIndex);
            int GetBoneLayer(int boneIndex);
            float GetBonePosition(int boneIndex, int component);
            bool GetBoneRotatable(int boneIndex);
            bool GetBoneTranslatable(int boneIndex);
            bool GetBoneAppendRotate(int boneIndex);
            bool GetBoneAppendTranslate(int boneIndex);
            bool GetBoneAppendLocal(int boneIndex);
            int GetBoneAppendParentIndex(int boneIndex);
            float GetBoneAppendWeight(int boneIndex);
            bool GetBoneFixedAxisPresent(int boneIndex);
            float GetBoneFixedAxis(int boneIndex, int component);
            bool GetBoneLocalAxisPresent(int boneIndex);
            float GetBoneLocalAxisX(int boneIndex, int component);
            float GetBoneLocalAxisZ(int boneIndex, int component);
            bool GetBoneExternalParentPresent(int boneIndex);
            int GetBoneExternalParentKey(int boneIndex);
            bool GetBoneIkPresent(int boneIndex);
            int GetBoneIkTargetIndex(int boneIndex);
            int GetBoneIkLoopCount(int boneIndex);
            float GetBoneIkLimitAngle(int boneIndex);
            int GetBoneIkLinkCount(int boneIndex);
            int GetBoneIkLinkBoneIndex(int boneIndex, int linkIndex);
            bool GetBoneIkLinkLimitPresent(int boneIndex, int linkIndex);
            float GetBoneIkLinkLimitLower(int boneIndex, int linkIndex, int component);
            float GetBoneIkLinkLimitUpper(int boneIndex, int linkIndex, int component);

            string GetMorphName(int morphIndex);
            string GetMorphKind(int morphIndex);
            string GetMorphPanel(int morphIndex);
            int GetMorphVertexOffsetCount(int morphIndex);
            int GetMorphGroupOffsetCount(int morphIndex);
            int GetMorphBoneOffsetCount(int morphIndex);
            int GetMorphUvOffsetCount(int morphIndex);
            int GetMorphAdditionalUvOffsetCount(int morphIndex);
            int GetMorphMaterialOffsetCount(int morphIndex);
            int GetMorphFlipOffsetCount(int morphIndex);
            int GetMorphImpulseOffsetCount(int morphIndex);
            uint GetMorphVertexOffsetVertexIndex(int morphIndex, int offsetIndex);
            float GetMorphVertexOffsetPosition(int morphIndex, int offsetIndex, int component);
            int GetMorphGroupOffsetMorphIndex(int morphIndex, int offsetIndex);
            float GetMorphGroupOffsetWeight(int morphIndex, int offsetIndex);
            int GetMorphBoneOffsetBoneIndex(int morphIndex, int offsetIndex);
            float GetMorphBoneOffsetTranslation(int morphIndex, int offsetIndex, int component);
            float GetMorphBoneOffsetRotation(int morphIndex, int offsetIndex, int component);
            uint GetMorphUvOffsetVertexIndex(int morphIndex, int offsetIndex);
            float GetMorphUvOffsetValue(int morphIndex, int offsetIndex, int component);
            uint GetMorphAdditionalUvOffsetVertexIndex(int morphIndex, int offsetIndex);
            byte GetMorphAdditionalUvOffsetUvIndex(int morphIndex, int offsetIndex);
            float GetMorphAdditionalUvOffsetValue(int morphIndex, int offsetIndex, int component);
            int GetMorphMaterialOffsetMaterialIndex(int morphIndex, int offsetIndex);
            string GetMorphMaterialOffsetOperation(int morphIndex, int offsetIndex);
            float GetMorphMaterialOffsetDiffuse(int morphIndex, int offsetIndex, int component);
            float GetMorphMaterialOffsetSpecular(int morphIndex, int offsetIndex, int component);
            float GetMorphMaterialOffsetSpecularPower(int morphIndex, int offsetIndex);
            float GetMorphMaterialOffsetAmbient(int morphIndex, int offsetIndex, int component);
            float GetMorphMaterialOffsetEdgeColor(int morphIndex, int offsetIndex, int component);
            float GetMorphMaterialOffsetEdgeSize(int morphIndex, int offsetIndex);
            float GetMorphMaterialOffsetTextureFactor(int morphIndex, int offsetIndex, int component);
            float GetMorphMaterialOffsetSphereTextureFactor(int morphIndex, int offsetIndex, int component);
            float GetMorphMaterialOffsetToonTextureFactor(int morphIndex, int offsetIndex, int component);
            int GetMorphFlipOffsetMorphIndex(int morphIndex, int offsetIndex);
            float GetMorphFlipOffsetWeight(int morphIndex, int offsetIndex);
            int GetMorphImpulseOffsetRigidbodyIndex(int morphIndex, int offsetIndex);
            bool GetMorphImpulseOffsetLocal(int morphIndex, int offsetIndex);
            float GetMorphImpulseOffsetVelocity(int morphIndex, int offsetIndex, int component);
            float GetMorphImpulseOffsetTorque(int morphIndex, int offsetIndex, int component);

            string GetRigidbodyName(int rigidbodyIndex);
            int GetRigidbodyBoneIndex(int rigidbodyIndex);
            int GetRigidbodyGroup(int rigidbodyIndex);
            int GetRigidbodyMask(int rigidbodyIndex);
            string GetRigidbodyShape(int rigidbodyIndex);
            float GetRigidbodySize(int rigidbodyIndex, int component);
            float GetRigidbodyPosition(int rigidbodyIndex, int component);
            float GetRigidbodyRotation(int rigidbodyIndex, int component);
            float GetRigidbodyMass(int rigidbodyIndex);
            float GetRigidbodyLinearDamping(int rigidbodyIndex);
            float GetRigidbodyAngularDamping(int rigidbodyIndex);
            float GetRigidbodyRestitution(int rigidbodyIndex);
            float GetRigidbodyFriction(int rigidbodyIndex);
            string GetRigidbodyMode(int rigidbodyIndex);

            string GetJointName(int jointIndex);
            int GetJointRigidbodyAIndex(int jointIndex);
            int GetJointRigidbodyBIndex(int jointIndex);
            float GetJointPosition(int jointIndex, int component);
            float GetJointRotation(int jointIndex, int component);
            float GetJointTranslationLowerLimit(int jointIndex, int component);
            float GetJointTranslationUpperLimit(int jointIndex, int component);
            float GetJointRotationLowerLimit(int jointIndex, int component);
            float GetJointRotationUpperLimit(int jointIndex, int component);
            float GetJointSpringTranslationFactor(int jointIndex, int component);
            float GetJointSpringRotationFactor(int jointIndex, int component);
        }

        internal sealed class VmdSummaryHandle : IVmdSummaryAccessor, IDisposable
        {
            private IntPtr summary;

            internal VmdSummaryHandle(byte[] data)
            {
                if (data == null || data.Length == 0)
                {
                    throw new ArgumentException("VMD bytes are required.", nameof(data));
                }

                summary = VmdSummaryCreateFromBytes(data, new IntPtr(data.Length));
                if (summary == IntPtr.Zero)
                {
                    throw new InvalidOperationException("mmd-runtime VMD summary parser returned a null handle.");
                }
            }

            public string TargetModelName => ReadString(VmdSummaryModelName(summary), "VMD summary model name");
            public uint MaxFrame => VmdSummaryMaxFrame(summary);
            public int BoneFrameCount => CheckedIntPtrToInt(VmdSummaryBoneKeyframeCount(summary), "VMD bone keyframe count");
            public int MorphFrameCount => CheckedIntPtrToInt(VmdSummaryMorphKeyframeCount(summary), "VMD morph keyframe count");
            public int PropertyFrameCount => CheckedIntPtrToInt(VmdSummaryPropertyKeyframeCount(summary), "VMD property keyframe count");
            public int CameraFrameCount => CheckedIntPtrToInt(VmdSummaryCameraKeyframeCount(summary), "VMD camera keyframe count");
            public int LightFrameCount => CheckedIntPtrToInt(VmdSummaryLightKeyframeCount(summary), "VMD light keyframe count");
            public int SelfShadowFrameCount => CheckedIntPtrToInt(VmdSummarySelfShadowKeyframeCount(summary), "VMD self-shadow keyframe count");

            public string GetBoneFrameName(int frameIndex) => ReadString(VmdSummaryBoneFrameName(summary, Index(frameIndex, nameof(frameIndex))), "VMD bone frame name");
            public uint GetBoneFrameFrame(int frameIndex) => VmdSummaryBoneFrameFrame(summary, Index(frameIndex, nameof(frameIndex)));

            public float GetBoneFrameTranslation(int frameIndex, int component)
            {
                IntPtr index = Index(frameIndex, nameof(frameIndex));
                return component switch
                {
                    0 => VmdSummaryBoneFrameTranslationX(summary, index),
                    1 => VmdSummaryBoneFrameTranslationY(summary, index),
                    2 => VmdSummaryBoneFrameTranslationZ(summary, index),
                    _ => 0.0f
                };
            }

            public float GetBoneFrameRotation(int frameIndex, int component)
            {
                IntPtr index = Index(frameIndex, nameof(frameIndex));
                return component switch
                {
                    0 => VmdSummaryBoneFrameRotationX(summary, index),
                    1 => VmdSummaryBoneFrameRotationY(summary, index),
                    2 => VmdSummaryBoneFrameRotationZ(summary, index),
                    3 => VmdSummaryBoneFrameRotationW(summary, index),
                    _ => 0.0f
                };
            }

            public byte GetBoneFrameInterpolationByte(int frameIndex, int offset)
            {
                return VmdSummaryBoneFrameInterpolationByte(summary, Index(frameIndex, nameof(frameIndex)), Index(offset, nameof(offset)));
            }

            public string GetMorphFrameName(int frameIndex) => ReadString(VmdSummaryMorphFrameName(summary, Index(frameIndex, nameof(frameIndex))), "VMD morph frame name");
            public uint GetMorphFrameFrame(int frameIndex) => VmdSummaryMorphFrameFrame(summary, Index(frameIndex, nameof(frameIndex)));
            public float GetMorphFrameWeight(int frameIndex) => VmdSummaryMorphFrameWeight(summary, Index(frameIndex, nameof(frameIndex)));

            public uint GetPropertyFrameFrame(int frameIndex) => VmdSummaryPropertyFrameFrame(summary, Index(frameIndex, nameof(frameIndex)));
            public bool GetPropertyFrameVisible(int frameIndex) => VmdSummaryPropertyFrameVisible(summary, Index(frameIndex, nameof(frameIndex)));
            public int GetPropertyFrameIkStateCount(int frameIndex) => CheckedIntPtrToInt(VmdSummaryPropertyFrameIkStateCount(summary, Index(frameIndex, nameof(frameIndex))), "VMD property IK state count");
            public string GetPropertyFrameIkStateName(int frameIndex, int ikStateIndex) => ReadString(VmdSummaryPropertyFrameIkStateName(summary, Index(frameIndex, nameof(frameIndex)), Index(ikStateIndex, nameof(ikStateIndex))), "VMD property IK state name");
            public bool GetPropertyFrameIkStateEnabled(int frameIndex, int ikStateIndex) => VmdSummaryPropertyFrameIkStateEnabled(summary, Index(frameIndex, nameof(frameIndex)), Index(ikStateIndex, nameof(ikStateIndex)));

            public uint GetCameraFrameFrame(int frameIndex) => VmdSummaryCameraFrameFrame(summary, Index(frameIndex, nameof(frameIndex)));
            public float GetCameraFrameDistance(int frameIndex) => VmdSummaryCameraFrameDistance(summary, Index(frameIndex, nameof(frameIndex)));

            public float GetCameraFramePosition(int frameIndex, int component)
            {
                IntPtr index = Index(frameIndex, nameof(frameIndex));
                return component switch
                {
                    0 => VmdSummaryCameraFramePositionX(summary, index),
                    1 => VmdSummaryCameraFramePositionY(summary, index),
                    2 => VmdSummaryCameraFramePositionZ(summary, index),
                    _ => 0.0f
                };
            }

            public float GetCameraFrameRotation(int frameIndex, int component)
            {
                IntPtr index = Index(frameIndex, nameof(frameIndex));
                return component switch
                {
                    0 => VmdSummaryCameraFrameRotationX(summary, index),
                    1 => VmdSummaryCameraFrameRotationY(summary, index),
                    2 => VmdSummaryCameraFrameRotationZ(summary, index),
                    _ => 0.0f
                };
            }

            public byte GetCameraFrameInterpolationByte(int frameIndex, int offset) => VmdSummaryCameraFrameInterpolationByte(summary, Index(frameIndex, nameof(frameIndex)), Index(offset, nameof(offset)));
            public uint GetCameraFrameFov(int frameIndex) => VmdSummaryCameraFrameFov(summary, Index(frameIndex, nameof(frameIndex)));
            public bool GetCameraFramePerspective(int frameIndex) => VmdSummaryCameraFramePerspective(summary, Index(frameIndex, nameof(frameIndex)));

            public uint GetLightFrameFrame(int frameIndex) => VmdSummaryLightFrameFrame(summary, Index(frameIndex, nameof(frameIndex)));

            public float GetLightFrameColor(int frameIndex, int component)
            {
                IntPtr index = Index(frameIndex, nameof(frameIndex));
                return component switch
                {
                    0 => VmdSummaryLightFrameColorX(summary, index),
                    1 => VmdSummaryLightFrameColorY(summary, index),
                    2 => VmdSummaryLightFrameColorZ(summary, index),
                    _ => 0.0f
                };
            }

            public float GetLightFrameDirection(int frameIndex, int component)
            {
                IntPtr index = Index(frameIndex, nameof(frameIndex));
                return component switch
                {
                    0 => VmdSummaryLightFrameDirectionX(summary, index),
                    1 => VmdSummaryLightFrameDirectionY(summary, index),
                    2 => VmdSummaryLightFrameDirectionZ(summary, index),
                    _ => 0.0f
                };
            }

            public void Dispose()
            {
                if (summary != IntPtr.Zero)
                {
                    VmdSummaryFree(summary);
                    summary = IntPtr.Zero;
                }
            }
        }

        internal static VmdSummaryHandle CreateVmdSummary(byte[] data)
        {
            return new VmdSummaryHandle(data);
        }

        internal sealed class PmxSummaryHandle : IPmxSummaryAccessor, IDisposable
        {
            private IntPtr summary;

            internal PmxSummaryHandle(byte[] data)
            {
                if (data == null || data.Length == 0)
                {
                    throw new ArgumentException("PMX bytes are required.", nameof(data));
                }

                summary = PmxSummaryCreateFromBytes(data, new IntPtr(data.Length));
                if (summary == IntPtr.Zero)
                {
                    throw new InvalidOperationException("mmd-runtime PMX summary parser returned a null handle.");
                }
            }

            public string ModelName => ReadString(PmxSummaryModelName(summary), "PMX summary model name");
            public int VertexCount => CheckedIntPtrToInt(PmxSummaryVertexCount(summary), "PMX vertex count");
            public int IndexCount => CheckedIntPtrToInt(PmxSummaryIndexCount(summary), "PMX index count");
            public int MaterialCount => CheckedIntPtrToInt(PmxSummaryMaterialCount(summary), "PMX material count");
            public int BoneCount => CheckedIntPtrToInt(PmxSummaryBoneCount(summary), "PMX bone count");
            public int MorphCount => CheckedIntPtrToInt(PmxSummaryMorphCount(summary), "PMX morph count");
            public int RigidbodyCount => CheckedIntPtrToInt(PmxSummaryRigidbodyCount(summary), "PMX rigidbody count");
            public int JointCount => CheckedIntPtrToInt(PmxSummaryJointCount(summary), "PMX joint count");

            public uint GetIndex(int index) => PmxSummaryIndex(summary, Index(index, nameof(index)));
            public float GetVertexPosition(int vertexIndex, int component) => PmxSummaryVertexPosition(summary, Index(vertexIndex, nameof(vertexIndex)), Index(component, nameof(component)));
            public float GetVertexNormal(int vertexIndex, int component) => PmxSummaryVertexNormal(summary, Index(vertexIndex, nameof(vertexIndex)), Index(component, nameof(component)));
            public float GetVertexUv(int vertexIndex, int component) => PmxSummaryVertexUv(summary, Index(vertexIndex, nameof(vertexIndex)), Index(component, nameof(component)));
            public int GetVertexSkinBoneIndex(int vertexIndex, int subIndex) => PmxSummaryVertexSkinBoneIndex(summary, Index(vertexIndex, nameof(vertexIndex)), Index(subIndex, nameof(subIndex)));
            public float GetVertexSkinWeight(int vertexIndex, int subIndex) => PmxSummaryVertexSkinWeight(summary, Index(vertexIndex, nameof(vertexIndex)), Index(subIndex, nameof(subIndex)));
            public string GetVertexSkinningKind(int vertexIndex) => ReadString(PmxSummaryVertexSkinningKind(summary, Index(vertexIndex, nameof(vertexIndex))), "PMX vertex skinning kind");
            public bool GetVertexSdefEnabled(int vertexIndex) => PmxSummaryVertexSdefEnabled(summary, Index(vertexIndex, nameof(vertexIndex)));
            public float GetVertexSdef(int vertexIndex, int which, int component) => PmxSummaryVertexSdefC(summary, Index(vertexIndex, nameof(vertexIndex)), Index(which, nameof(which)), Index(component, nameof(component)));

            public string GetMaterialName(int materialIndex) => ReadString(PmxSummaryMaterialName(summary, Index(materialIndex, nameof(materialIndex))), "PMX material name");
            public string GetMaterialTexturePath(int materialIndex) => ReadString(PmxSummaryMaterialTexturePath(summary, Index(materialIndex, nameof(materialIndex))), "PMX material texture path");
            public string GetMaterialSphereTexturePath(int materialIndex) => ReadString(PmxSummaryMaterialSphereTexturePath(summary, Index(materialIndex, nameof(materialIndex))), "PMX material sphere texture path");
            public string GetMaterialToonTexturePath(int materialIndex) => ReadString(PmxSummaryMaterialToonTexturePath(summary, Index(materialIndex, nameof(materialIndex))), "PMX material toon texture path");
            public string GetMaterialSphereMode(int materialIndex) => ReadString(PmxSummaryMaterialSphereMode(summary, Index(materialIndex, nameof(materialIndex))), "PMX material sphere mode");
            public int GetMaterialSharedToonIndex(int materialIndex) => PmxSummaryMaterialSharedToonIndex(summary, Index(materialIndex, nameof(materialIndex)));
            public float GetMaterialDiffuse(int materialIndex, int component) => PmxSummaryMaterialDiffuse(summary, Index(materialIndex, nameof(materialIndex)), Index(component, nameof(component)));
            public float GetMaterialAmbient(int materialIndex, int component) => PmxSummaryMaterialAmbient(summary, Index(materialIndex, nameof(materialIndex)), Index(component, nameof(component)));
            public float GetMaterialEdgeColor(int materialIndex, int component) => PmxSummaryMaterialEdgeColor(summary, Index(materialIndex, nameof(materialIndex)), Index(component, nameof(component)));
            public float GetMaterialEdgeSize(int materialIndex) => PmxSummaryMaterialEdgeSize(summary, Index(materialIndex, nameof(materialIndex)));
            public int GetMaterialFaceCount(int materialIndex) => PmxSummaryMaterialFaceCount(summary, Index(materialIndex, nameof(materialIndex)));
            public bool GetMaterialDoubleSided(int materialIndex) => PmxSummaryMaterialDoubleSided(summary, Index(materialIndex, nameof(materialIndex)));
            public bool GetMaterialEdgeFlag(int materialIndex) => PmxSummaryMaterialEdgeFlag(summary, Index(materialIndex, nameof(materialIndex)));

            public string GetBoneName(int boneIndex) => ReadString(PmxSummaryBoneName(summary, Index(boneIndex, nameof(boneIndex))), "PMX bone name");
            public int GetBoneParentIndex(int boneIndex) => PmxSummaryBoneParentIndex(summary, Index(boneIndex, nameof(boneIndex)));
            public int GetBoneLayer(int boneIndex) => PmxSummaryBoneLayer(summary, Index(boneIndex, nameof(boneIndex)));
            public float GetBonePosition(int boneIndex, int component) => PmxSummaryBonePosition(summary, Index(boneIndex, nameof(boneIndex)), Index(component, nameof(component)));
            public bool GetBoneRotatable(int boneIndex) => PmxSummaryBoneRotatable(summary, Index(boneIndex, nameof(boneIndex)));
            public bool GetBoneTranslatable(int boneIndex) => PmxSummaryBoneTranslatable(summary, Index(boneIndex, nameof(boneIndex)));
            public bool GetBoneAppendRotate(int boneIndex) => PmxSummaryBoneAppendRotate(summary, Index(boneIndex, nameof(boneIndex)));
            public bool GetBoneAppendTranslate(int boneIndex) => PmxSummaryBoneAppendTranslate(summary, Index(boneIndex, nameof(boneIndex)));
            public bool GetBoneAppendLocal(int boneIndex) => PmxSummaryBoneAppendLocal(summary, Index(boneIndex, nameof(boneIndex)));
            public int GetBoneAppendParentIndex(int boneIndex) => PmxSummaryBoneAppendParentIndex(summary, Index(boneIndex, nameof(boneIndex)));
            public float GetBoneAppendWeight(int boneIndex) => PmxSummaryBoneAppendWeight(summary, Index(boneIndex, nameof(boneIndex)));
            public bool GetBoneFixedAxisPresent(int boneIndex) => PmxSummaryBoneFixedAxisPresent(summary, Index(boneIndex, nameof(boneIndex)));
            public float GetBoneFixedAxis(int boneIndex, int component) => PmxSummaryBoneFixedAxis(summary, Index(boneIndex, nameof(boneIndex)), Index(component, nameof(component)));
            public bool GetBoneLocalAxisPresent(int boneIndex) => PmxSummaryBoneLocalAxisPresent(summary, Index(boneIndex, nameof(boneIndex)));
            public float GetBoneLocalAxisX(int boneIndex, int component) => PmxSummaryBoneLocalAxisX(summary, Index(boneIndex, nameof(boneIndex)), Index(component, nameof(component)));
            public float GetBoneLocalAxisZ(int boneIndex, int component) => PmxSummaryBoneLocalAxisZ(summary, Index(boneIndex, nameof(boneIndex)), Index(component, nameof(component)));
            public bool GetBoneExternalParentPresent(int boneIndex) => PmxSummaryBoneExternalParentPresent(summary, Index(boneIndex, nameof(boneIndex)));
            public int GetBoneExternalParentKey(int boneIndex) => PmxSummaryBoneExternalParentKey(summary, Index(boneIndex, nameof(boneIndex)));
            public bool GetBoneIkPresent(int boneIndex) => PmxSummaryBoneIkPresent(summary, Index(boneIndex, nameof(boneIndex)));
            public int GetBoneIkTargetIndex(int boneIndex) => PmxSummaryBoneIkTargetIndex(summary, Index(boneIndex, nameof(boneIndex)));
            public int GetBoneIkLoopCount(int boneIndex) => PmxSummaryBoneIkLoopCount(summary, Index(boneIndex, nameof(boneIndex)));
            public float GetBoneIkLimitAngle(int boneIndex) => PmxSummaryBoneIkLimitAngle(summary, Index(boneIndex, nameof(boneIndex)));
            public int GetBoneIkLinkCount(int boneIndex) => CheckedIntPtrToInt(PmxSummaryBoneIkLinkCount(summary, Index(boneIndex, nameof(boneIndex))), "PMX IK link count");
            public int GetBoneIkLinkBoneIndex(int boneIndex, int linkIndex) => PmxSummaryBoneIkLinkBoneIndex(summary, Index(boneIndex, nameof(boneIndex)), Index(linkIndex, nameof(linkIndex)));
            public bool GetBoneIkLinkLimitPresent(int boneIndex, int linkIndex) => PmxSummaryBoneIkLinkLimitPresent(summary, Index(boneIndex, nameof(boneIndex)), Index(linkIndex, nameof(linkIndex)));
            public float GetBoneIkLinkLimitLower(int boneIndex, int linkIndex, int component) => PmxSummaryBoneIkLinkLimitLower(summary, Index(boneIndex, nameof(boneIndex)), Index(linkIndex, nameof(linkIndex)), Index(component, nameof(component)));
            public float GetBoneIkLinkLimitUpper(int boneIndex, int linkIndex, int component) => PmxSummaryBoneIkLinkLimitUpper(summary, Index(boneIndex, nameof(boneIndex)), Index(linkIndex, nameof(linkIndex)), Index(component, nameof(component)));

            public string GetMorphName(int morphIndex) => ReadString(PmxSummaryMorphName(summary, Index(morphIndex, nameof(morphIndex))), "PMX morph name");
            public string GetMorphKind(int morphIndex) => ReadString(PmxSummaryMorphKind(summary, Index(morphIndex, nameof(morphIndex))), "PMX morph kind");
            public string GetMorphPanel(int morphIndex) => ReadString(PmxSummaryMorphPanel(summary, Index(morphIndex, nameof(morphIndex))), "PMX morph panel");
            public int GetMorphVertexOffsetCount(int morphIndex) => CheckedIntPtrToInt(PmxSummaryMorphVertexOffsetCount(summary, Index(morphIndex, nameof(morphIndex))), "PMX vertex morph offset count");
            public int GetMorphGroupOffsetCount(int morphIndex) => CheckedIntPtrToInt(PmxSummaryMorphGroupOffsetCount(summary, Index(morphIndex, nameof(morphIndex))), "PMX group morph offset count");
            public int GetMorphBoneOffsetCount(int morphIndex) => CheckedIntPtrToInt(PmxSummaryMorphBoneOffsetCount(summary, Index(morphIndex, nameof(morphIndex))), "PMX bone morph offset count");
            public int GetMorphUvOffsetCount(int morphIndex) => CheckedIntPtrToInt(PmxSummaryMorphUvOffsetCount(summary, Index(morphIndex, nameof(morphIndex))), "PMX UV morph offset count");
            public int GetMorphAdditionalUvOffsetCount(int morphIndex) => CheckedIntPtrToInt(PmxSummaryMorphAdditionalUvOffsetCount(summary, Index(morphIndex, nameof(morphIndex))), "PMX additional UV morph offset count");
            public int GetMorphMaterialOffsetCount(int morphIndex) => CheckedIntPtrToInt(PmxSummaryMorphMaterialOffsetCount(summary, Index(morphIndex, nameof(morphIndex))), "PMX material morph offset count");
            public int GetMorphFlipOffsetCount(int morphIndex) => CheckedIntPtrToInt(PmxSummaryMorphFlipOffsetCount(summary, Index(morphIndex, nameof(morphIndex))), "PMX flip morph offset count");
            public int GetMorphImpulseOffsetCount(int morphIndex) => CheckedIntPtrToInt(PmxSummaryMorphImpulseOffsetCount(summary, Index(morphIndex, nameof(morphIndex))), "PMX impulse morph offset count");
            public uint GetMorphVertexOffsetVertexIndex(int morphIndex, int offsetIndex) => PmxSummaryMorphVertexOffsetVertexIndex(summary, Index(morphIndex, nameof(morphIndex)), Index(offsetIndex, nameof(offsetIndex)));
            public float GetMorphVertexOffsetPosition(int morphIndex, int offsetIndex, int component) => PmxSummaryMorphVertexOffsetPosition(summary, Index(morphIndex, nameof(morphIndex)), Index(offsetIndex, nameof(offsetIndex)), Index(component, nameof(component)));
            public int GetMorphGroupOffsetMorphIndex(int morphIndex, int offsetIndex) => PmxSummaryMorphGroupOffsetMorphIndex(summary, Index(morphIndex, nameof(morphIndex)), Index(offsetIndex, nameof(offsetIndex)));
            public float GetMorphGroupOffsetWeight(int morphIndex, int offsetIndex) => PmxSummaryMorphGroupOffsetWeight(summary, Index(morphIndex, nameof(morphIndex)), Index(offsetIndex, nameof(offsetIndex)));
            public int GetMorphBoneOffsetBoneIndex(int morphIndex, int offsetIndex) => PmxSummaryMorphBoneOffsetBoneIndex(summary, Index(morphIndex, nameof(morphIndex)), Index(offsetIndex, nameof(offsetIndex)));
            public float GetMorphBoneOffsetTranslation(int morphIndex, int offsetIndex, int component) => PmxSummaryMorphBoneOffsetTranslation(summary, Index(morphIndex, nameof(morphIndex)), Index(offsetIndex, nameof(offsetIndex)), Index(component, nameof(component)));
            public float GetMorphBoneOffsetRotation(int morphIndex, int offsetIndex, int component) => PmxSummaryMorphBoneOffsetRotation(summary, Index(morphIndex, nameof(morphIndex)), Index(offsetIndex, nameof(offsetIndex)), Index(component, nameof(component)));
            public uint GetMorphUvOffsetVertexIndex(int morphIndex, int offsetIndex) => PmxSummaryMorphUvOffsetVertexIndex(summary, Index(morphIndex, nameof(morphIndex)), Index(offsetIndex, nameof(offsetIndex)));
            public float GetMorphUvOffsetValue(int morphIndex, int offsetIndex, int component) => PmxSummaryMorphUvOffsetValue(summary, Index(morphIndex, nameof(morphIndex)), Index(offsetIndex, nameof(offsetIndex)), Index(component, nameof(component)));
            public uint GetMorphAdditionalUvOffsetVertexIndex(int morphIndex, int offsetIndex) => PmxSummaryMorphAdditionalUvOffsetVertexIndex(summary, Index(morphIndex, nameof(morphIndex)), Index(offsetIndex, nameof(offsetIndex)));
            public byte GetMorphAdditionalUvOffsetUvIndex(int morphIndex, int offsetIndex) => PmxSummaryMorphAdditionalUvOffsetUvIndex(summary, Index(morphIndex, nameof(morphIndex)), Index(offsetIndex, nameof(offsetIndex)));
            public float GetMorphAdditionalUvOffsetValue(int morphIndex, int offsetIndex, int component) => PmxSummaryMorphAdditionalUvOffsetValue(summary, Index(morphIndex, nameof(morphIndex)), Index(offsetIndex, nameof(offsetIndex)), Index(component, nameof(component)));
            public int GetMorphMaterialOffsetMaterialIndex(int morphIndex, int offsetIndex) => PmxSummaryMorphMaterialOffsetMaterialIndex(summary, Index(morphIndex, nameof(morphIndex)), Index(offsetIndex, nameof(offsetIndex)));
            public string GetMorphMaterialOffsetOperation(int morphIndex, int offsetIndex) => ReadString(PmxSummaryMorphMaterialOffsetOperation(summary, Index(morphIndex, nameof(morphIndex)), Index(offsetIndex, nameof(offsetIndex))), "PMX material morph operation");
            public float GetMorphMaterialOffsetDiffuse(int morphIndex, int offsetIndex, int component) => PmxSummaryMorphMaterialOffsetDiffuse(summary, Index(morphIndex, nameof(morphIndex)), Index(offsetIndex, nameof(offsetIndex)), Index(component, nameof(component)));
            public float GetMorphMaterialOffsetSpecular(int morphIndex, int offsetIndex, int component) => PmxSummaryMorphMaterialOffsetSpecular(summary, Index(morphIndex, nameof(morphIndex)), Index(offsetIndex, nameof(offsetIndex)), Index(component, nameof(component)));
            public float GetMorphMaterialOffsetSpecularPower(int morphIndex, int offsetIndex) => PmxSummaryMorphMaterialOffsetSpecularPower(summary, Index(morphIndex, nameof(morphIndex)), Index(offsetIndex, nameof(offsetIndex)));
            public float GetMorphMaterialOffsetAmbient(int morphIndex, int offsetIndex, int component) => PmxSummaryMorphMaterialOffsetAmbient(summary, Index(morphIndex, nameof(morphIndex)), Index(offsetIndex, nameof(offsetIndex)), Index(component, nameof(component)));
            public float GetMorphMaterialOffsetEdgeColor(int morphIndex, int offsetIndex, int component) => PmxSummaryMorphMaterialOffsetEdgeColor(summary, Index(morphIndex, nameof(morphIndex)), Index(offsetIndex, nameof(offsetIndex)), Index(component, nameof(component)));
            public float GetMorphMaterialOffsetEdgeSize(int morphIndex, int offsetIndex) => PmxSummaryMorphMaterialOffsetEdgeSize(summary, Index(morphIndex, nameof(morphIndex)), Index(offsetIndex, nameof(offsetIndex)));
            public float GetMorphMaterialOffsetTextureFactor(int morphIndex, int offsetIndex, int component) => PmxSummaryMorphMaterialOffsetTextureFactor(summary, Index(morphIndex, nameof(morphIndex)), Index(offsetIndex, nameof(offsetIndex)), Index(component, nameof(component)));
            public float GetMorphMaterialOffsetSphereTextureFactor(int morphIndex, int offsetIndex, int component) => PmxSummaryMorphMaterialOffsetSphereTextureFactor(summary, Index(morphIndex, nameof(morphIndex)), Index(offsetIndex, nameof(offsetIndex)), Index(component, nameof(component)));
            public float GetMorphMaterialOffsetToonTextureFactor(int morphIndex, int offsetIndex, int component) => PmxSummaryMorphMaterialOffsetToonTextureFactor(summary, Index(morphIndex, nameof(morphIndex)), Index(offsetIndex, nameof(offsetIndex)), Index(component, nameof(component)));
            public int GetMorphFlipOffsetMorphIndex(int morphIndex, int offsetIndex) => PmxSummaryMorphFlipOffsetMorphIndex(summary, Index(morphIndex, nameof(morphIndex)), Index(offsetIndex, nameof(offsetIndex)));
            public float GetMorphFlipOffsetWeight(int morphIndex, int offsetIndex) => PmxSummaryMorphFlipOffsetWeight(summary, Index(morphIndex, nameof(morphIndex)), Index(offsetIndex, nameof(offsetIndex)));
            public int GetMorphImpulseOffsetRigidbodyIndex(int morphIndex, int offsetIndex) => PmxSummaryMorphImpulseOffsetRigidbodyIndex(summary, Index(morphIndex, nameof(morphIndex)), Index(offsetIndex, nameof(offsetIndex)));
            public bool GetMorphImpulseOffsetLocal(int morphIndex, int offsetIndex) => PmxSummaryMorphImpulseOffsetLocal(summary, Index(morphIndex, nameof(morphIndex)), Index(offsetIndex, nameof(offsetIndex)));
            public float GetMorphImpulseOffsetVelocity(int morphIndex, int offsetIndex, int component) => PmxSummaryMorphImpulseOffsetVelocity(summary, Index(morphIndex, nameof(morphIndex)), Index(offsetIndex, nameof(offsetIndex)), Index(component, nameof(component)));
            public float GetMorphImpulseOffsetTorque(int morphIndex, int offsetIndex, int component) => PmxSummaryMorphImpulseOffsetTorque(summary, Index(morphIndex, nameof(morphIndex)), Index(offsetIndex, nameof(offsetIndex)), Index(component, nameof(component)));

            public string GetRigidbodyName(int rigidbodyIndex) => ReadString(PmxSummaryRigidbodyName(summary, Index(rigidbodyIndex, nameof(rigidbodyIndex))), "PMX rigidbody name");
            public int GetRigidbodyBoneIndex(int rigidbodyIndex) => PmxSummaryRigidbodyBoneIndex(summary, Index(rigidbodyIndex, nameof(rigidbodyIndex)));
            public int GetRigidbodyGroup(int rigidbodyIndex) => PmxSummaryRigidbodyGroup(summary, Index(rigidbodyIndex, nameof(rigidbodyIndex)));
            public int GetRigidbodyMask(int rigidbodyIndex) => PmxSummaryRigidbodyMask(summary, Index(rigidbodyIndex, nameof(rigidbodyIndex)));
            public string GetRigidbodyShape(int rigidbodyIndex) => ReadString(PmxSummaryRigidbodyShape(summary, Index(rigidbodyIndex, nameof(rigidbodyIndex))), "PMX rigidbody shape");
            public float GetRigidbodySize(int rigidbodyIndex, int component) => PmxSummaryRigidbodySize(summary, Index(rigidbodyIndex, nameof(rigidbodyIndex)), Index(component, nameof(component)));
            public float GetRigidbodyPosition(int rigidbodyIndex, int component) => PmxSummaryRigidbodyPosition(summary, Index(rigidbodyIndex, nameof(rigidbodyIndex)), Index(component, nameof(component)));
            public float GetRigidbodyRotation(int rigidbodyIndex, int component) => PmxSummaryRigidbodyRotation(summary, Index(rigidbodyIndex, nameof(rigidbodyIndex)), Index(component, nameof(component)));
            public float GetRigidbodyMass(int rigidbodyIndex) => PmxSummaryRigidbodyMass(summary, Index(rigidbodyIndex, nameof(rigidbodyIndex)));
            public float GetRigidbodyLinearDamping(int rigidbodyIndex) => PmxSummaryRigidbodyLinearDamping(summary, Index(rigidbodyIndex, nameof(rigidbodyIndex)));
            public float GetRigidbodyAngularDamping(int rigidbodyIndex) => PmxSummaryRigidbodyAngularDamping(summary, Index(rigidbodyIndex, nameof(rigidbodyIndex)));
            public float GetRigidbodyRestitution(int rigidbodyIndex) => PmxSummaryRigidbodyRestitution(summary, Index(rigidbodyIndex, nameof(rigidbodyIndex)));
            public float GetRigidbodyFriction(int rigidbodyIndex) => PmxSummaryRigidbodyFriction(summary, Index(rigidbodyIndex, nameof(rigidbodyIndex)));
            public string GetRigidbodyMode(int rigidbodyIndex) => ReadString(PmxSummaryRigidbodyMode(summary, Index(rigidbodyIndex, nameof(rigidbodyIndex))), "PMX rigidbody mode");

            public string GetJointName(int jointIndex) => ReadString(PmxSummaryJointName(summary, Index(jointIndex, nameof(jointIndex))), "PMX joint name");
            public int GetJointRigidbodyAIndex(int jointIndex) => PmxSummaryJointRigidbodyAIndex(summary, Index(jointIndex, nameof(jointIndex)));
            public int GetJointRigidbodyBIndex(int jointIndex) => PmxSummaryJointRigidbodyBIndex(summary, Index(jointIndex, nameof(jointIndex)));
            public float GetJointPosition(int jointIndex, int component) => PmxSummaryJointPosition(summary, Index(jointIndex, nameof(jointIndex)), Index(component, nameof(component)));
            public float GetJointRotation(int jointIndex, int component) => PmxSummaryJointRotation(summary, Index(jointIndex, nameof(jointIndex)), Index(component, nameof(component)));
            public float GetJointTranslationLowerLimit(int jointIndex, int component) => PmxSummaryJointTranslationLowerLimit(summary, Index(jointIndex, nameof(jointIndex)), Index(component, nameof(component)));
            public float GetJointTranslationUpperLimit(int jointIndex, int component) => PmxSummaryJointTranslationUpperLimit(summary, Index(jointIndex, nameof(jointIndex)), Index(component, nameof(component)));
            public float GetJointRotationLowerLimit(int jointIndex, int component) => PmxSummaryJointRotationLowerLimit(summary, Index(jointIndex, nameof(jointIndex)), Index(component, nameof(component)));
            public float GetJointRotationUpperLimit(int jointIndex, int component) => PmxSummaryJointRotationUpperLimit(summary, Index(jointIndex, nameof(jointIndex)), Index(component, nameof(component)));
            public float GetJointSpringTranslationFactor(int jointIndex, int component) => PmxSummaryJointSpringTranslationFactor(summary, Index(jointIndex, nameof(jointIndex)), Index(component, nameof(component)));
            public float GetJointSpringRotationFactor(int jointIndex, int component) => PmxSummaryJointSpringRotationFactor(summary, Index(jointIndex, nameof(jointIndex)), Index(component, nameof(component)));

            public void Dispose()
            {
                if (summary != IntPtr.Zero)
                {
                    PmxSummaryFree(summary);
                    summary = IntPtr.Zero;
                }
            }
        }

        internal static PmxSummaryHandle CreatePmxSummary(byte[] data)
        {
            return new PmxSummaryHandle(data);
        }

        // Private DllImport declarations for the summary symbols.
        // These pin the entrypoints on the contract surface and back the active
        // NativeMmdParser summary wrapper flow. Signatures use IntPtr for
        // opaque handles and count returns (per guidance) and the existing internal
        // ByteBuffer for name accessors.
        [DllImport(LibraryName, EntryPoint = PmxSummaryCreateEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr PmxSummaryCreateFromBytes(byte[] data, IntPtr len);

        [DllImport(LibraryName, EntryPoint = PmxSummaryFreeEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern void PmxSummaryFree(IntPtr summary);

        [DllImport(LibraryName, EntryPoint = PmxSummaryVersionEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern float PmxSummaryVersion(IntPtr summary);

        [DllImport(LibraryName, EntryPoint = PmxSummaryVertexCountEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr PmxSummaryVertexCount(IntPtr summary);

        [DllImport(LibraryName, EntryPoint = PmxSummaryFaceCountEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr PmxSummaryFaceCount(IntPtr summary);

        [DllImport(LibraryName, EntryPoint = PmxSummaryMaterialCountEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr PmxSummaryMaterialCount(IntPtr summary);

        [DllImport(LibraryName, EntryPoint = PmxSummaryBoneCountEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr PmxSummaryBoneCount(IntPtr summary);

        [DllImport(LibraryName, EntryPoint = PmxSummaryMorphCountEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr PmxSummaryMorphCount(IntPtr summary);

        [DllImport(LibraryName, EntryPoint = PmxSummaryDisplayFrameCountEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr PmxSummaryDisplayFrameCount(IntPtr summary);

        [DllImport(LibraryName, EntryPoint = PmxSummaryRigidbodyCountEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr PmxSummaryRigidbodyCount(IntPtr summary);

        [DllImport(LibraryName, EntryPoint = PmxSummaryJointCountEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr PmxSummaryJointCount(IntPtr summary);

        [DllImport(LibraryName, EntryPoint = PmxSummarySoftBodyCountEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr PmxSummarySoftBodyCount(IntPtr summary);

        [DllImport(LibraryName, EntryPoint = PmxSummaryAdditionalUvCountEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr PmxSummaryAdditionalUvCount(IntPtr summary);

        [DllImport(LibraryName, EntryPoint = PmxSummaryModelNameEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern ByteBuffer PmxSummaryModelName(IntPtr summary);

        [DllImport(LibraryName, EntryPoint = PmxSummaryModelNameEnglishEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern ByteBuffer PmxSummaryModelNameEnglish(IntPtr summary);

        [DllImport(LibraryName, EntryPoint = VmdSummaryCreateEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr VmdSummaryCreateFromBytes(byte[] data, IntPtr len);

        [DllImport(LibraryName, EntryPoint = VmdSummaryFreeEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern void VmdSummaryFree(IntPtr summary);

        [DllImport(LibraryName, EntryPoint = VmdSummaryMaxFrameEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern uint VmdSummaryMaxFrame(IntPtr summary);

        [DllImport(LibraryName, EntryPoint = VmdSummaryBoneKeyframeCountEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr VmdSummaryBoneKeyframeCount(IntPtr summary);

        [DllImport(LibraryName, EntryPoint = VmdSummaryMorphKeyframeCountEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr VmdSummaryMorphKeyframeCount(IntPtr summary);

        [DllImport(LibraryName, EntryPoint = VmdSummaryPropertyKeyframeCountEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr VmdSummaryPropertyKeyframeCount(IntPtr summary);

        [DllImport(LibraryName, EntryPoint = VmdSummaryCameraKeyframeCountEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr VmdSummaryCameraKeyframeCount(IntPtr summary);

        [DllImport(LibraryName, EntryPoint = VmdSummaryLightKeyframeCountEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr VmdSummaryLightKeyframeCount(IntPtr summary);

        [DllImport(LibraryName, EntryPoint = VmdSummarySelfShadowKeyframeCountEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr VmdSummarySelfShadowKeyframeCount(IntPtr summary);

        [DllImport(LibraryName, EntryPoint = VmdSummaryModelNameEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern ByteBuffer VmdSummaryModelName(IntPtr summary);

        // Private DllImport for VMD per-frame getters (model motion). Signatures follow
        // native ABI conventions (IntPtr for handles/indices/counts, uint for frames, float/bool/byte scalars,
        // ByteBuffer for UTF-8 name accessors). Wrapped by VmdSummaryHandle.
        [DllImport(LibraryName, EntryPoint = VmdSummaryBoneFrameNameEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern ByteBuffer VmdSummaryBoneFrameName(IntPtr summary, IntPtr frameIndex);

        [DllImport(LibraryName, EntryPoint = VmdSummaryBoneFrameFrameEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern uint VmdSummaryBoneFrameFrame(IntPtr summary, IntPtr frameIndex);

        [DllImport(LibraryName, EntryPoint = VmdSummaryBoneFrameTranslationXEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern float VmdSummaryBoneFrameTranslationX(IntPtr summary, IntPtr frameIndex);

        [DllImport(LibraryName, EntryPoint = VmdSummaryBoneFrameTranslationYEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern float VmdSummaryBoneFrameTranslationY(IntPtr summary, IntPtr frameIndex);

        [DllImport(LibraryName, EntryPoint = VmdSummaryBoneFrameTranslationZEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern float VmdSummaryBoneFrameTranslationZ(IntPtr summary, IntPtr frameIndex);

        [DllImport(LibraryName, EntryPoint = VmdSummaryBoneFrameRotationXEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern float VmdSummaryBoneFrameRotationX(IntPtr summary, IntPtr frameIndex);

        [DllImport(LibraryName, EntryPoint = VmdSummaryBoneFrameRotationYEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern float VmdSummaryBoneFrameRotationY(IntPtr summary, IntPtr frameIndex);

        [DllImport(LibraryName, EntryPoint = VmdSummaryBoneFrameRotationZEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern float VmdSummaryBoneFrameRotationZ(IntPtr summary, IntPtr frameIndex);

        [DllImport(LibraryName, EntryPoint = VmdSummaryBoneFrameRotationWEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern float VmdSummaryBoneFrameRotationW(IntPtr summary, IntPtr frameIndex);

        [DllImport(LibraryName, EntryPoint = VmdSummaryBoneFrameInterpolationByteEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern byte VmdSummaryBoneFrameInterpolationByte(IntPtr summary, IntPtr frameIndex, IntPtr interpolationOffset);

        [DllImport(LibraryName, EntryPoint = VmdSummaryMorphFrameNameEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern ByteBuffer VmdSummaryMorphFrameName(IntPtr summary, IntPtr frameIndex);

        [DllImport(LibraryName, EntryPoint = VmdSummaryMorphFrameFrameEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern uint VmdSummaryMorphFrameFrame(IntPtr summary, IntPtr frameIndex);

        [DllImport(LibraryName, EntryPoint = VmdSummaryMorphFrameWeightEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern float VmdSummaryMorphFrameWeight(IntPtr summary, IntPtr frameIndex);

        [DllImport(LibraryName, EntryPoint = VmdSummaryPropertyFrameFrameEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern uint VmdSummaryPropertyFrameFrame(IntPtr summary, IntPtr frameIndex);

        [DllImport(LibraryName, EntryPoint = VmdSummaryPropertyFrameVisibleEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool VmdSummaryPropertyFrameVisible(IntPtr summary, IntPtr frameIndex);

        [DllImport(LibraryName, EntryPoint = VmdSummaryPropertyFrameIkStateCountEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr VmdSummaryPropertyFrameIkStateCount(IntPtr summary, IntPtr frameIndex);

        [DllImport(LibraryName, EntryPoint = VmdSummaryPropertyFrameIkStateNameEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern ByteBuffer VmdSummaryPropertyFrameIkStateName(IntPtr summary, IntPtr frameIndex, IntPtr ikStateIndex);

        [DllImport(LibraryName, EntryPoint = VmdSummaryPropertyFrameIkStateEnabledEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool VmdSummaryPropertyFrameIkStateEnabled(IntPtr summary, IntPtr frameIndex, IntPtr ikStateIndex);

        // Private DllImport declarations for VMD scene-track getters.
        // Camera and light getters are now consumed by VmdSummaryHandle.
        // Self-shadow keyframes remain count-only for a later slice.
        [DllImport(LibraryName, EntryPoint = VmdSummaryCameraFrameFrameEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern uint VmdSummaryCameraFrameFrame(IntPtr summary, IntPtr frameIndex);

        [DllImport(LibraryName, EntryPoint = VmdSummaryCameraFrameDistanceEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern float VmdSummaryCameraFrameDistance(IntPtr summary, IntPtr frameIndex);

        [DllImport(LibraryName, EntryPoint = VmdSummaryCameraFramePositionXEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern float VmdSummaryCameraFramePositionX(IntPtr summary, IntPtr frameIndex);

        [DllImport(LibraryName, EntryPoint = VmdSummaryCameraFramePositionYEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern float VmdSummaryCameraFramePositionY(IntPtr summary, IntPtr frameIndex);

        [DllImport(LibraryName, EntryPoint = VmdSummaryCameraFramePositionZEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern float VmdSummaryCameraFramePositionZ(IntPtr summary, IntPtr frameIndex);

        [DllImport(LibraryName, EntryPoint = VmdSummaryCameraFrameRotationXEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern float VmdSummaryCameraFrameRotationX(IntPtr summary, IntPtr frameIndex);

        [DllImport(LibraryName, EntryPoint = VmdSummaryCameraFrameRotationYEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern float VmdSummaryCameraFrameRotationY(IntPtr summary, IntPtr frameIndex);

        [DllImport(LibraryName, EntryPoint = VmdSummaryCameraFrameRotationZEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern float VmdSummaryCameraFrameRotationZ(IntPtr summary, IntPtr frameIndex);

        [DllImport(LibraryName, EntryPoint = VmdSummaryCameraFrameInterpolationByteEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern byte VmdSummaryCameraFrameInterpolationByte(IntPtr summary, IntPtr frameIndex, IntPtr interpolationOffset);

        [DllImport(LibraryName, EntryPoint = VmdSummaryCameraFrameFovEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern uint VmdSummaryCameraFrameFov(IntPtr summary, IntPtr frameIndex);

        [DllImport(LibraryName, EntryPoint = VmdSummaryCameraFramePerspectiveEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool VmdSummaryCameraFramePerspective(IntPtr summary, IntPtr frameIndex);

        [DllImport(LibraryName, EntryPoint = VmdSummaryLightFrameFrameEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern uint VmdSummaryLightFrameFrame(IntPtr summary, IntPtr frameIndex);

        [DllImport(LibraryName, EntryPoint = VmdSummaryLightFrameColorXEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern float VmdSummaryLightFrameColorX(IntPtr summary, IntPtr frameIndex);

        [DllImport(LibraryName, EntryPoint = VmdSummaryLightFrameColorYEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern float VmdSummaryLightFrameColorY(IntPtr summary, IntPtr frameIndex);

        [DllImport(LibraryName, EntryPoint = VmdSummaryLightFrameColorZEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern float VmdSummaryLightFrameColorZ(IntPtr summary, IntPtr frameIndex);

        [DllImport(LibraryName, EntryPoint = VmdSummaryLightFrameDirectionXEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern float VmdSummaryLightFrameDirectionX(IntPtr summary, IntPtr frameIndex);

        [DllImport(LibraryName, EntryPoint = VmdSummaryLightFrameDirectionYEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern float VmdSummaryLightFrameDirectionY(IntPtr summary, IntPtr frameIndex);

        [DllImport(LibraryName, EntryPoint = VmdSummaryLightFrameDirectionZEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern float VmdSummaryLightFrameDirectionZ(IntPtr summary, IntPtr frameIndex);

        [DllImport(LibraryName, EntryPoint = VmdSummarySelfShadowFrameFrameEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern uint VmdSummarySelfShadowFrameFrame(IntPtr summary, IntPtr frameIndex);

        [DllImport(LibraryName, EntryPoint = VmdSummarySelfShadowFrameModeEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern byte VmdSummarySelfShadowFrameMode(IntPtr summary, IntPtr frameIndex);

        [DllImport(LibraryName, EntryPoint = VmdSummarySelfShadowFrameDistanceEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern float VmdSummarySelfShadowFrameDistance(IntPtr summary, IntPtr frameIndex);

        // Private DllImport declarations for PMX core getters (geometry/materials/bones+IK).
        // These match native MmdRuntimePmxSummary accessors added in mmd-anim and
        // are wrapped by PmxSummaryHandle for the active parser path.
        // IntPtr for opaque summary + usize (counts/indices), uint for u32 returns, int for i32 returns (parent/target may be -1),
        // float for components, [return: MarshalAs(UnmanagedType.I1)] bool for flags/presents/double-sided, ByteBuffer for name/path strings.
        [DllImport(LibraryName, EntryPoint = PmxSummaryIndexCountEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr PmxSummaryIndexCount(IntPtr summary);

        [DllImport(LibraryName, EntryPoint = PmxSummaryIndexEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern uint PmxSummaryIndex(IntPtr summary, IntPtr index);

        [DllImport(LibraryName, EntryPoint = PmxSummaryVertexPositionEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern float PmxSummaryVertexPosition(IntPtr summary, IntPtr vertexIndex, IntPtr component);

        [DllImport(LibraryName, EntryPoint = PmxSummaryVertexNormalEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern float PmxSummaryVertexNormal(IntPtr summary, IntPtr vertexIndex, IntPtr component);

        [DllImport(LibraryName, EntryPoint = PmxSummaryVertexUvEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern float PmxSummaryVertexUv(IntPtr summary, IntPtr vertexIndex, IntPtr component);

        [DllImport(LibraryName, EntryPoint = PmxSummaryVertexSkinBoneIndexEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern int PmxSummaryVertexSkinBoneIndex(IntPtr summary, IntPtr vertexIndex, IntPtr subIndex);

        [DllImport(LibraryName, EntryPoint = PmxSummaryVertexSkinWeightEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern float PmxSummaryVertexSkinWeight(IntPtr summary, IntPtr vertexIndex, IntPtr subIndex);

        [DllImport(LibraryName, EntryPoint = PmxSummaryVertexSkinningKindEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern ByteBuffer PmxSummaryVertexSkinningKind(IntPtr summary, IntPtr vertexIndex);

        [DllImport(LibraryName, EntryPoint = PmxSummaryVertexSdefEnabledEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool PmxSummaryVertexSdefEnabled(IntPtr summary, IntPtr vertexIndex);

        [DllImport(LibraryName, EntryPoint = PmxSummaryVertexSdefCEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern float PmxSummaryVertexSdefC(IntPtr summary, IntPtr vertexIndex, IntPtr which, IntPtr component);

        [DllImport(LibraryName, EntryPoint = PmxSummaryMaterialNameEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern ByteBuffer PmxSummaryMaterialName(IntPtr summary, IntPtr materialIndex);

        [DllImport(LibraryName, EntryPoint = PmxSummaryMaterialTexturePathEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern ByteBuffer PmxSummaryMaterialTexturePath(IntPtr summary, IntPtr materialIndex);

        [DllImport(LibraryName, EntryPoint = PmxSummaryMaterialSphereTexturePathEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern ByteBuffer PmxSummaryMaterialSphereTexturePath(IntPtr summary, IntPtr materialIndex);

        [DllImport(LibraryName, EntryPoint = PmxSummaryMaterialToonTexturePathEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern ByteBuffer PmxSummaryMaterialToonTexturePath(IntPtr summary, IntPtr materialIndex);

        [DllImport(LibraryName, EntryPoint = PmxSummaryMaterialSphereModeEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern ByteBuffer PmxSummaryMaterialSphereMode(IntPtr summary, IntPtr materialIndex);

        [DllImport(LibraryName, EntryPoint = PmxSummaryMaterialSharedToonIndexEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern int PmxSummaryMaterialSharedToonIndex(IntPtr summary, IntPtr materialIndex);

        [DllImport(LibraryName, EntryPoint = PmxSummaryMaterialDiffuseEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern float PmxSummaryMaterialDiffuse(IntPtr summary, IntPtr materialIndex, IntPtr component);

        [DllImport(LibraryName, EntryPoint = PmxSummaryMaterialAmbientEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern float PmxSummaryMaterialAmbient(IntPtr summary, IntPtr materialIndex, IntPtr component);

        [DllImport(LibraryName, EntryPoint = PmxSummaryMaterialEdgeColorEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern float PmxSummaryMaterialEdgeColor(IntPtr summary, IntPtr materialIndex, IntPtr component);

        [DllImport(LibraryName, EntryPoint = PmxSummaryMaterialEdgeSizeEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern float PmxSummaryMaterialEdgeSize(IntPtr summary, IntPtr materialIndex);

        [DllImport(LibraryName, EntryPoint = PmxSummaryMaterialFaceCountEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern int PmxSummaryMaterialFaceCount(IntPtr summary, IntPtr materialIndex);

        [DllImport(LibraryName, EntryPoint = PmxSummaryMaterialDoubleSidedEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool PmxSummaryMaterialDoubleSided(IntPtr summary, IntPtr materialIndex);

        [DllImport(LibraryName, EntryPoint = PmxSummaryMaterialEdgeFlagEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool PmxSummaryMaterialEdgeFlag(IntPtr summary, IntPtr materialIndex);

        [DllImport(LibraryName, EntryPoint = PmxSummaryBoneNameEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern ByteBuffer PmxSummaryBoneName(IntPtr summary, IntPtr boneIndex);

        [DllImport(LibraryName, EntryPoint = PmxSummaryBoneParentIndexEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern int PmxSummaryBoneParentIndex(IntPtr summary, IntPtr boneIndex);

        [DllImport(LibraryName, EntryPoint = PmxSummaryBoneLayerEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern int PmxSummaryBoneLayer(IntPtr summary, IntPtr boneIndex);

        [DllImport(LibraryName, EntryPoint = PmxSummaryBonePositionEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern float PmxSummaryBonePosition(IntPtr summary, IntPtr boneIndex, IntPtr component);

        [DllImport(LibraryName, EntryPoint = PmxSummaryBoneRotatableEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool PmxSummaryBoneRotatable(IntPtr summary, IntPtr boneIndex);

        [DllImport(LibraryName, EntryPoint = PmxSummaryBoneTranslatableEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool PmxSummaryBoneTranslatable(IntPtr summary, IntPtr boneIndex);

        [DllImport(LibraryName, EntryPoint = PmxSummaryBoneAppendRotateEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool PmxSummaryBoneAppendRotate(IntPtr summary, IntPtr boneIndex);

        [DllImport(LibraryName, EntryPoint = PmxSummaryBoneAppendTranslateEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool PmxSummaryBoneAppendTranslate(IntPtr summary, IntPtr boneIndex);

        [DllImport(LibraryName, EntryPoint = PmxSummaryBoneAppendLocalEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool PmxSummaryBoneAppendLocal(IntPtr summary, IntPtr boneIndex);

        [DllImport(LibraryName, EntryPoint = PmxSummaryBoneAppendParentIndexEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern int PmxSummaryBoneAppendParentIndex(IntPtr summary, IntPtr boneIndex);

        [DllImport(LibraryName, EntryPoint = PmxSummaryBoneAppendWeightEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern float PmxSummaryBoneAppendWeight(IntPtr summary, IntPtr boneIndex);

        [DllImport(LibraryName, EntryPoint = PmxSummaryBoneFixedAxisPresentEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool PmxSummaryBoneFixedAxisPresent(IntPtr summary, IntPtr boneIndex);

        [DllImport(LibraryName, EntryPoint = PmxSummaryBoneFixedAxisEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern float PmxSummaryBoneFixedAxis(IntPtr summary, IntPtr boneIndex, IntPtr component);

        [DllImport(LibraryName, EntryPoint = PmxSummaryBoneLocalAxisPresentEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool PmxSummaryBoneLocalAxisPresent(IntPtr summary, IntPtr boneIndex);

        [DllImport(LibraryName, EntryPoint = PmxSummaryBoneLocalAxisXEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern float PmxSummaryBoneLocalAxisX(IntPtr summary, IntPtr boneIndex, IntPtr component);

        [DllImport(LibraryName, EntryPoint = PmxSummaryBoneLocalAxisZEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern float PmxSummaryBoneLocalAxisZ(IntPtr summary, IntPtr boneIndex, IntPtr component);

        [DllImport(LibraryName, EntryPoint = PmxSummaryBoneExternalParentPresentEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool PmxSummaryBoneExternalParentPresent(IntPtr summary, IntPtr boneIndex);

        [DllImport(LibraryName, EntryPoint = PmxSummaryBoneExternalParentKeyEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern int PmxSummaryBoneExternalParentKey(IntPtr summary, IntPtr boneIndex);

        [DllImport(LibraryName, EntryPoint = PmxSummaryBoneIkPresentEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool PmxSummaryBoneIkPresent(IntPtr summary, IntPtr boneIndex);

        [DllImport(LibraryName, EntryPoint = PmxSummaryBoneIkTargetIndexEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern int PmxSummaryBoneIkTargetIndex(IntPtr summary, IntPtr boneIndex);

        [DllImport(LibraryName, EntryPoint = PmxSummaryBoneIkLoopCountEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern int PmxSummaryBoneIkLoopCount(IntPtr summary, IntPtr boneIndex);

        [DllImport(LibraryName, EntryPoint = PmxSummaryBoneIkLimitAngleEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern float PmxSummaryBoneIkLimitAngle(IntPtr summary, IntPtr boneIndex);

        [DllImport(LibraryName, EntryPoint = PmxSummaryBoneIkLinkCountEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr PmxSummaryBoneIkLinkCount(IntPtr summary, IntPtr boneIndex);

        [DllImport(LibraryName, EntryPoint = PmxSummaryBoneIkLinkBoneIndexEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern int PmxSummaryBoneIkLinkBoneIndex(IntPtr summary, IntPtr boneIndex, IntPtr linkIndex);

        [DllImport(LibraryName, EntryPoint = PmxSummaryBoneIkLinkLimitPresentEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool PmxSummaryBoneIkLinkLimitPresent(IntPtr summary, IntPtr boneIndex, IntPtr linkIndex);

        [DllImport(LibraryName, EntryPoint = PmxSummaryBoneIkLinkLimitLowerEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern float PmxSummaryBoneIkLinkLimitLower(IntPtr summary, IntPtr boneIndex, IntPtr linkIndex, IntPtr component);

        [DllImport(LibraryName, EntryPoint = PmxSummaryBoneIkLinkLimitUpperEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern float PmxSummaryBoneIkLinkLimitUpper(IntPtr summary, IntPtr boneIndex, IntPtr linkIndex, IntPtr component);

        // Private DllImport declarations for PMX morph getters (header/counts + per-offset families).
        // Wrapped by PmxSummaryHandle for the active parser path.
        // Opaque: IntPtr summary; usize idx: IntPtr; u32: uint; u8: byte; f32: float; bool: I1 marshal; names/ops: ByteBuffer.
        // Header / counts / name / kind
        [DllImport(LibraryName, EntryPoint = PmxSummaryMorphNameEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern ByteBuffer PmxSummaryMorphName(IntPtr summary, IntPtr morphIndex);

        [DllImport(LibraryName, EntryPoint = PmxSummaryMorphEnglishNameEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern ByteBuffer PmxSummaryMorphEnglishName(IntPtr summary, IntPtr morphIndex);

        [DllImport(LibraryName, EntryPoint = PmxSummaryMorphKindEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern ByteBuffer PmxSummaryMorphKind(IntPtr summary, IntPtr morphIndex);

        [DllImport(LibraryName, EntryPoint = PmxSummaryMorphPanelEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern ByteBuffer PmxSummaryMorphPanel(IntPtr summary, IntPtr morphIndex);

        [DllImport(LibraryName, EntryPoint = PmxSummaryMorphVertexOffsetCountEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr PmxSummaryMorphVertexOffsetCount(IntPtr summary, IntPtr morphIndex);

        [DllImport(LibraryName, EntryPoint = PmxSummaryMorphGroupOffsetCountEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr PmxSummaryMorphGroupOffsetCount(IntPtr summary, IntPtr morphIndex);

        [DllImport(LibraryName, EntryPoint = PmxSummaryMorphBoneOffsetCountEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr PmxSummaryMorphBoneOffsetCount(IntPtr summary, IntPtr morphIndex);

        [DllImport(LibraryName, EntryPoint = PmxSummaryMorphUvOffsetCountEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr PmxSummaryMorphUvOffsetCount(IntPtr summary, IntPtr morphIndex);

        [DllImport(LibraryName, EntryPoint = PmxSummaryMorphAdditionalUvOffsetCountEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr PmxSummaryMorphAdditionalUvOffsetCount(IntPtr summary, IntPtr morphIndex);

        [DllImport(LibraryName, EntryPoint = PmxSummaryMorphMaterialOffsetCountEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr PmxSummaryMorphMaterialOffsetCount(IntPtr summary, IntPtr morphIndex);

        [DllImport(LibraryName, EntryPoint = PmxSummaryMorphFlipOffsetCountEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr PmxSummaryMorphFlipOffsetCount(IntPtr summary, IntPtr morphIndex);

        [DllImport(LibraryName, EntryPoint = PmxSummaryMorphImpulseOffsetCountEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr PmxSummaryMorphImpulseOffsetCount(IntPtr summary, IntPtr morphIndex);

        // Vertex offsets
        [DllImport(LibraryName, EntryPoint = PmxSummaryMorphVertexOffsetVertexIndexEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern uint PmxSummaryMorphVertexOffsetVertexIndex(IntPtr summary, IntPtr morphIndex, IntPtr offsetIndex);

        [DllImport(LibraryName, EntryPoint = PmxSummaryMorphVertexOffsetPositionEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern float PmxSummaryMorphVertexOffsetPosition(IntPtr summary, IntPtr morphIndex, IntPtr offsetIndex, IntPtr component);

        // Group / flip offsets
        [DllImport(LibraryName, EntryPoint = PmxSummaryMorphGroupOffsetMorphIndexEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern int PmxSummaryMorphGroupOffsetMorphIndex(IntPtr summary, IntPtr morphIndex, IntPtr offsetIndex);

        [DllImport(LibraryName, EntryPoint = PmxSummaryMorphGroupOffsetWeightEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern float PmxSummaryMorphGroupOffsetWeight(IntPtr summary, IntPtr morphIndex, IntPtr offsetIndex);

        [DllImport(LibraryName, EntryPoint = PmxSummaryMorphFlipOffsetMorphIndexEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern int PmxSummaryMorphFlipOffsetMorphIndex(IntPtr summary, IntPtr morphIndex, IntPtr offsetIndex);

        [DllImport(LibraryName, EntryPoint = PmxSummaryMorphFlipOffsetWeightEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern float PmxSummaryMorphFlipOffsetWeight(IntPtr summary, IntPtr morphIndex, IntPtr offsetIndex);

        // Bone offsets
        [DllImport(LibraryName, EntryPoint = PmxSummaryMorphBoneOffsetBoneIndexEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern int PmxSummaryMorphBoneOffsetBoneIndex(IntPtr summary, IntPtr morphIndex, IntPtr offsetIndex);

        [DllImport(LibraryName, EntryPoint = PmxSummaryMorphBoneOffsetTranslationEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern float PmxSummaryMorphBoneOffsetTranslation(IntPtr summary, IntPtr morphIndex, IntPtr offsetIndex, IntPtr component);

        [DllImport(LibraryName, EntryPoint = PmxSummaryMorphBoneOffsetRotationEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern float PmxSummaryMorphBoneOffsetRotation(IntPtr summary, IntPtr morphIndex, IntPtr offsetIndex, IntPtr component);

        // UV offsets
        [DllImport(LibraryName, EntryPoint = PmxSummaryMorphUvOffsetVertexIndexEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern uint PmxSummaryMorphUvOffsetVertexIndex(IntPtr summary, IntPtr morphIndex, IntPtr offsetIndex);

        [DllImport(LibraryName, EntryPoint = PmxSummaryMorphUvOffsetValueEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern float PmxSummaryMorphUvOffsetValue(IntPtr summary, IntPtr morphIndex, IntPtr offsetIndex, IntPtr component);

        // Additional UV offsets
        [DllImport(LibraryName, EntryPoint = PmxSummaryMorphAdditionalUvOffsetVertexIndexEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern uint PmxSummaryMorphAdditionalUvOffsetVertexIndex(IntPtr summary, IntPtr morphIndex, IntPtr offsetIndex);

        [DllImport(LibraryName, EntryPoint = PmxSummaryMorphAdditionalUvOffsetUvIndexEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern byte PmxSummaryMorphAdditionalUvOffsetUvIndex(IntPtr summary, IntPtr morphIndex, IntPtr offsetIndex);

        [DllImport(LibraryName, EntryPoint = PmxSummaryMorphAdditionalUvOffsetValueEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern float PmxSummaryMorphAdditionalUvOffsetValue(IntPtr summary, IntPtr morphIndex, IntPtr offsetIndex, IntPtr component);

        // Material offsets
        [DllImport(LibraryName, EntryPoint = PmxSummaryMorphMaterialOffsetMaterialIndexEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern int PmxSummaryMorphMaterialOffsetMaterialIndex(IntPtr summary, IntPtr morphIndex, IntPtr offsetIndex);

        [DllImport(LibraryName, EntryPoint = PmxSummaryMorphMaterialOffsetOperationEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern ByteBuffer PmxSummaryMorphMaterialOffsetOperation(IntPtr summary, IntPtr morphIndex, IntPtr offsetIndex);

        [DllImport(LibraryName, EntryPoint = PmxSummaryMorphMaterialOffsetDiffuseEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern float PmxSummaryMorphMaterialOffsetDiffuse(IntPtr summary, IntPtr morphIndex, IntPtr offsetIndex, IntPtr component);

        [DllImport(LibraryName, EntryPoint = PmxSummaryMorphMaterialOffsetSpecularEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern float PmxSummaryMorphMaterialOffsetSpecular(IntPtr summary, IntPtr morphIndex, IntPtr offsetIndex, IntPtr component);

        [DllImport(LibraryName, EntryPoint = PmxSummaryMorphMaterialOffsetSpecularPowerEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern float PmxSummaryMorphMaterialOffsetSpecularPower(IntPtr summary, IntPtr morphIndex, IntPtr offsetIndex);

        [DllImport(LibraryName, EntryPoint = PmxSummaryMorphMaterialOffsetAmbientEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern float PmxSummaryMorphMaterialOffsetAmbient(IntPtr summary, IntPtr morphIndex, IntPtr offsetIndex, IntPtr component);

        [DllImport(LibraryName, EntryPoint = PmxSummaryMorphMaterialOffsetEdgeColorEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern float PmxSummaryMorphMaterialOffsetEdgeColor(IntPtr summary, IntPtr morphIndex, IntPtr offsetIndex, IntPtr component);

        [DllImport(LibraryName, EntryPoint = PmxSummaryMorphMaterialOffsetEdgeSizeEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern float PmxSummaryMorphMaterialOffsetEdgeSize(IntPtr summary, IntPtr morphIndex, IntPtr offsetIndex);

        [DllImport(LibraryName, EntryPoint = PmxSummaryMorphMaterialOffsetTextureFactorEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern float PmxSummaryMorphMaterialOffsetTextureFactor(IntPtr summary, IntPtr morphIndex, IntPtr offsetIndex, IntPtr component);

        [DllImport(LibraryName, EntryPoint = PmxSummaryMorphMaterialOffsetSphereTextureFactorEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern float PmxSummaryMorphMaterialOffsetSphereTextureFactor(IntPtr summary, IntPtr morphIndex, IntPtr offsetIndex, IntPtr component);

        [DllImport(LibraryName, EntryPoint = PmxSummaryMorphMaterialOffsetToonTextureFactorEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern float PmxSummaryMorphMaterialOffsetToonTextureFactor(IntPtr summary, IntPtr morphIndex, IntPtr offsetIndex, IntPtr component);

        // Impulse offsets
        [DllImport(LibraryName, EntryPoint = PmxSummaryMorphImpulseOffsetRigidbodyIndexEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern int PmxSummaryMorphImpulseOffsetRigidbodyIndex(IntPtr summary, IntPtr morphIndex, IntPtr offsetIndex);

        [DllImport(LibraryName, EntryPoint = PmxSummaryMorphImpulseOffsetLocalEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool PmxSummaryMorphImpulseOffsetLocal(IntPtr summary, IntPtr morphIndex, IntPtr offsetIndex);

        [DllImport(LibraryName, EntryPoint = PmxSummaryMorphImpulseOffsetVelocityEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern float PmxSummaryMorphImpulseOffsetVelocity(IntPtr summary, IntPtr morphIndex, IntPtr offsetIndex, IntPtr component);

        [DllImport(LibraryName, EntryPoint = PmxSummaryMorphImpulseOffsetTorqueEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern float PmxSummaryMorphImpulseOffsetTorque(IntPtr summary, IntPtr morphIndex, IntPtr offsetIndex, IntPtr component);

        // Private DllImport declarations for PMX display frame getters.
        // Pinned for future neutral IR expansion.
        [DllImport(LibraryName, EntryPoint = PmxSummaryDisplayFrameNameEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern ByteBuffer PmxSummaryDisplayFrameName(IntPtr summary, IntPtr displayFrameIndex);

        [DllImport(LibraryName, EntryPoint = PmxSummaryDisplayFrameEnglishNameEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern ByteBuffer PmxSummaryDisplayFrameEnglishName(IntPtr summary, IntPtr displayFrameIndex);

        [DllImport(LibraryName, EntryPoint = PmxSummaryDisplayFrameSpecialEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool PmxSummaryDisplayFrameSpecial(IntPtr summary, IntPtr displayFrameIndex);

        [DllImport(LibraryName, EntryPoint = PmxSummaryDisplayFrameItemCountEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr PmxSummaryDisplayFrameItemCount(IntPtr summary, IntPtr displayFrameIndex);

        [DllImport(LibraryName, EntryPoint = PmxSummaryDisplayFrameItemKindEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern ByteBuffer PmxSummaryDisplayFrameItemKind(IntPtr summary, IntPtr displayFrameIndex, IntPtr itemIndex);

        [DllImport(LibraryName, EntryPoint = PmxSummaryDisplayFrameItemIndexEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern int PmxSummaryDisplayFrameItemIndex(IntPtr summary, IntPtr displayFrameIndex, IntPtr itemIndex);

        // Private DllImport declarations for PMX physics getters (rigidbody/joint).
        // Wrapped by PmxSummaryHandle for the active parser path.
        [DllImport(LibraryName, EntryPoint = PmxSummaryRigidbodyNameEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern ByteBuffer PmxSummaryRigidbodyName(IntPtr summary, IntPtr rigidbodyIndex);

        [DllImport(LibraryName, EntryPoint = PmxSummaryRigidbodyEnglishNameEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern ByteBuffer PmxSummaryRigidbodyEnglishName(IntPtr summary, IntPtr rigidbodyIndex);

        [DllImport(LibraryName, EntryPoint = PmxSummaryRigidbodyBoneIndexEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern int PmxSummaryRigidbodyBoneIndex(IntPtr summary, IntPtr rigidbodyIndex);

        [DllImport(LibraryName, EntryPoint = PmxSummaryRigidbodyGroupEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern byte PmxSummaryRigidbodyGroup(IntPtr summary, IntPtr rigidbodyIndex);

        [DllImport(LibraryName, EntryPoint = PmxSummaryRigidbodyMaskEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern ushort PmxSummaryRigidbodyMask(IntPtr summary, IntPtr rigidbodyIndex);

        [DllImport(LibraryName, EntryPoint = PmxSummaryRigidbodyShapeEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern ByteBuffer PmxSummaryRigidbodyShape(IntPtr summary, IntPtr rigidbodyIndex);

        [DllImport(LibraryName, EntryPoint = PmxSummaryRigidbodySizeEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern float PmxSummaryRigidbodySize(IntPtr summary, IntPtr rigidbodyIndex, IntPtr component);

        [DllImport(LibraryName, EntryPoint = PmxSummaryRigidbodyPositionEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern float PmxSummaryRigidbodyPosition(IntPtr summary, IntPtr rigidbodyIndex, IntPtr component);

        [DllImport(LibraryName, EntryPoint = PmxSummaryRigidbodyRotationEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern float PmxSummaryRigidbodyRotation(IntPtr summary, IntPtr rigidbodyIndex, IntPtr component);

        [DllImport(LibraryName, EntryPoint = PmxSummaryRigidbodyMassEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern float PmxSummaryRigidbodyMass(IntPtr summary, IntPtr rigidbodyIndex);

        [DllImport(LibraryName, EntryPoint = PmxSummaryRigidbodyLinearDampingEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern float PmxSummaryRigidbodyLinearDamping(IntPtr summary, IntPtr rigidbodyIndex);

        [DllImport(LibraryName, EntryPoint = PmxSummaryRigidbodyAngularDampingEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern float PmxSummaryRigidbodyAngularDamping(IntPtr summary, IntPtr rigidbodyIndex);

        [DllImport(LibraryName, EntryPoint = PmxSummaryRigidbodyRestitutionEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern float PmxSummaryRigidbodyRestitution(IntPtr summary, IntPtr rigidbodyIndex);

        [DllImport(LibraryName, EntryPoint = PmxSummaryRigidbodyFrictionEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern float PmxSummaryRigidbodyFriction(IntPtr summary, IntPtr rigidbodyIndex);

        [DllImport(LibraryName, EntryPoint = PmxSummaryRigidbodyModeEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern ByteBuffer PmxSummaryRigidbodyMode(IntPtr summary, IntPtr rigidbodyIndex);

        [DllImport(LibraryName, EntryPoint = PmxSummaryJointNameEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern ByteBuffer PmxSummaryJointName(IntPtr summary, IntPtr jointIndex);

        [DllImport(LibraryName, EntryPoint = PmxSummaryJointEnglishNameEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern ByteBuffer PmxSummaryJointEnglishName(IntPtr summary, IntPtr jointIndex);

        [DllImport(LibraryName, EntryPoint = PmxSummaryJointKindEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern ByteBuffer PmxSummaryJointKind(IntPtr summary, IntPtr jointIndex);

        [DllImport(LibraryName, EntryPoint = PmxSummaryJointRigidbodyAIndexEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern int PmxSummaryJointRigidbodyAIndex(IntPtr summary, IntPtr jointIndex);

        [DllImport(LibraryName, EntryPoint = PmxSummaryJointRigidbodyBIndexEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern int PmxSummaryJointRigidbodyBIndex(IntPtr summary, IntPtr jointIndex);

        [DllImport(LibraryName, EntryPoint = PmxSummaryJointPositionEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern float PmxSummaryJointPosition(IntPtr summary, IntPtr jointIndex, IntPtr component);

        [DllImport(LibraryName, EntryPoint = PmxSummaryJointRotationEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern float PmxSummaryJointRotation(IntPtr summary, IntPtr jointIndex, IntPtr component);

        [DllImport(LibraryName, EntryPoint = PmxSummaryJointTranslationLowerLimitEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern float PmxSummaryJointTranslationLowerLimit(IntPtr summary, IntPtr jointIndex, IntPtr component);

        [DllImport(LibraryName, EntryPoint = PmxSummaryJointTranslationUpperLimitEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern float PmxSummaryJointTranslationUpperLimit(IntPtr summary, IntPtr jointIndex, IntPtr component);

        [DllImport(LibraryName, EntryPoint = PmxSummaryJointRotationLowerLimitEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern float PmxSummaryJointRotationLowerLimit(IntPtr summary, IntPtr jointIndex, IntPtr component);

        [DllImport(LibraryName, EntryPoint = PmxSummaryJointRotationUpperLimitEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern float PmxSummaryJointRotationUpperLimit(IntPtr summary, IntPtr jointIndex, IntPtr component);

        [DllImport(LibraryName, EntryPoint = PmxSummaryJointSpringTranslationFactorEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern float PmxSummaryJointSpringTranslationFactor(IntPtr summary, IntPtr jointIndex, IntPtr component);

        [DllImport(LibraryName, EntryPoint = PmxSummaryJointSpringRotationFactorEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern float PmxSummaryJointSpringRotationFactor(IntPtr summary, IntPtr jointIndex, IntPtr component);

        private static string ReadString(ByteBuffer buffer, string label)
        {
            try
            {
                int byteLength = CheckedIntPtrToInt(buffer.Length, label + " byte length");
                if (buffer.Data == IntPtr.Zero || byteLength == 0)
                {
                    return string.Empty;
                }

                byte[] bytes = new byte[byteLength];
                Marshal.Copy(buffer.Data, bytes, 0, byteLength);
                return Encoding.UTF8.GetString(bytes, 0, bytes.Length);
            }
            finally
            {
                ByteBufferFree(buffer);
            }
        }

        private static IntPtr Index(int value, string label)
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(label, value, "Native summary indices must be non-negative.");
            }

            return new IntPtr(value);
        }

        private static int CheckedIntPtrToInt(IntPtr value, string label)
        {
            long raw = value.ToInt64();
            if (raw < 0 || raw > int.MaxValue)
            {
                throw new InvalidOperationException($"mmd-runtime {label} is out of range: {raw}");
            }

            return (int)raw;
        }
    }
}
