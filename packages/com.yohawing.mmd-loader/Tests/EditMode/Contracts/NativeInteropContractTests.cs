using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Mmd.Native;
using Mmd.Parser;

namespace Mmd.Tests
{
    [TestFixture]
    public sealed class NativeInteropContractTests
    {
        [Test]
        public void ParserFfiPinsSummaryEntrypointsForCurrentBehavior()
        {
            Assert.That(MmdParserFfiMethods.LibraryName, Is.EqualTo("mmd_runtime_ffi"));
            Assert.That(MmdParserFfiMethods.ByteBufferFreeEntryPoint, Is.EqualTo("mmd_runtime_byte_buffer_free"));

            // Summary entrypoints are the active NativeMmdParser surface.
            // These asserts are static contract checks only; no native functions
            // are invoked here and the test must remain green even against an
            // old/locked package DLL.
            // PMX summary
            Assert.That(MmdParserFfiMethods.PmxSummaryCreateEntryPoint, Is.EqualTo("mmd_runtime_pmx_summary_create_from_bytes"));
            Assert.That(MmdParserFfiMethods.PmxSummaryFreeEntryPoint, Is.EqualTo("mmd_runtime_pmx_summary_free"));
            Assert.That(MmdParserFfiMethods.PmxSummaryVersionEntryPoint, Is.EqualTo("mmd_runtime_pmx_summary_version"));
            Assert.That(MmdParserFfiMethods.PmxSummaryVertexCountEntryPoint, Is.EqualTo("mmd_runtime_pmx_summary_vertex_count"));
            Assert.That(MmdParserFfiMethods.PmxSummaryFaceCountEntryPoint, Is.EqualTo("mmd_runtime_pmx_summary_face_count"));
            Assert.That(MmdParserFfiMethods.PmxSummaryMaterialCountEntryPoint, Is.EqualTo("mmd_runtime_pmx_summary_material_count"));
            Assert.That(MmdParserFfiMethods.PmxSummaryBoneCountEntryPoint, Is.EqualTo("mmd_runtime_pmx_summary_bone_count"));
            Assert.That(MmdParserFfiMethods.PmxSummaryMorphCountEntryPoint, Is.EqualTo("mmd_runtime_pmx_summary_morph_count"));
            Assert.That(MmdParserFfiMethods.PmxSummaryDisplayFrameCountEntryPoint, Is.EqualTo("mmd_runtime_pmx_summary_display_frame_count"));
            Assert.That(MmdParserFfiMethods.PmxSummaryRigidbodyCountEntryPoint, Is.EqualTo("mmd_runtime_pmx_summary_rigidbody_count"));
            Assert.That(MmdParserFfiMethods.PmxSummaryJointCountEntryPoint, Is.EqualTo("mmd_runtime_pmx_summary_joint_count"));
            Assert.That(MmdParserFfiMethods.PmxSummarySoftBodyCountEntryPoint, Is.EqualTo("mmd_runtime_pmx_summary_soft_body_count"));
            Assert.That(MmdParserFfiMethods.PmxSummaryAdditionalUvCountEntryPoint, Is.EqualTo("mmd_runtime_pmx_summary_additional_uv_count"));
            Assert.That(MmdParserFfiMethods.PmxSummaryModelNameEntryPoint, Is.EqualTo("mmd_runtime_pmx_summary_model_name"));
            Assert.That(MmdParserFfiMethods.PmxSummaryModelNameEnglishEntryPoint, Is.EqualTo("mmd_runtime_pmx_summary_model_name_english"));
            // VMD summary
            Assert.That(MmdParserFfiMethods.VmdSummaryCreateEntryPoint, Is.EqualTo("mmd_runtime_vmd_summary_create_from_bytes"));
            Assert.That(MmdParserFfiMethods.VmdSummaryFreeEntryPoint, Is.EqualTo("mmd_runtime_vmd_summary_free"));
            Assert.That(MmdParserFfiMethods.VmdSummaryMaxFrameEntryPoint, Is.EqualTo("mmd_runtime_vmd_summary_max_frame"));
            Assert.That(MmdParserFfiMethods.VmdSummaryBoneKeyframeCountEntryPoint, Is.EqualTo("mmd_runtime_vmd_summary_bone_keyframe_count"));
            Assert.That(MmdParserFfiMethods.VmdSummaryMorphKeyframeCountEntryPoint, Is.EqualTo("mmd_runtime_vmd_summary_morph_keyframe_count"));
            Assert.That(MmdParserFfiMethods.VmdSummaryPropertyKeyframeCountEntryPoint, Is.EqualTo("mmd_runtime_vmd_summary_property_keyframe_count"));
            Assert.That(MmdParserFfiMethods.VmdSummaryCameraKeyframeCountEntryPoint, Is.EqualTo("mmd_runtime_vmd_summary_camera_keyframe_count"));
            Assert.That(MmdParserFfiMethods.VmdSummaryLightKeyframeCountEntryPoint, Is.EqualTo("mmd_runtime_vmd_summary_light_keyframe_count"));
            Assert.That(MmdParserFfiMethods.VmdSummarySelfShadowKeyframeCountEntryPoint, Is.EqualTo("mmd_runtime_vmd_summary_self_shadow_keyframe_count"));
            Assert.That(MmdParserFfiMethods.VmdSummaryModelNameEntryPoint, Is.EqualTo("mmd_runtime_vmd_summary_model_name"));

            // VMD per-frame model-motion getters.
            // Static asserts only; these symbols are not invoked by this test.
            Assert.That(MmdParserFfiMethods.VmdSummaryBoneFrameNameEntryPoint, Is.EqualTo("mmd_runtime_vmd_summary_bone_frame_name"));
            Assert.That(MmdParserFfiMethods.VmdSummaryBoneFrameFrameEntryPoint, Is.EqualTo("mmd_runtime_vmd_summary_bone_frame_frame"));
            Assert.That(MmdParserFfiMethods.VmdSummaryBoneFrameTranslationXEntryPoint, Is.EqualTo("mmd_runtime_vmd_summary_bone_frame_translation_x"));
            Assert.That(MmdParserFfiMethods.VmdSummaryBoneFrameTranslationYEntryPoint, Is.EqualTo("mmd_runtime_vmd_summary_bone_frame_translation_y"));
            Assert.That(MmdParserFfiMethods.VmdSummaryBoneFrameTranslationZEntryPoint, Is.EqualTo("mmd_runtime_vmd_summary_bone_frame_translation_z"));
            Assert.That(MmdParserFfiMethods.VmdSummaryBoneFrameRotationXEntryPoint, Is.EqualTo("mmd_runtime_vmd_summary_bone_frame_rotation_x"));
            Assert.That(MmdParserFfiMethods.VmdSummaryBoneFrameRotationYEntryPoint, Is.EqualTo("mmd_runtime_vmd_summary_bone_frame_rotation_y"));
            Assert.That(MmdParserFfiMethods.VmdSummaryBoneFrameRotationZEntryPoint, Is.EqualTo("mmd_runtime_vmd_summary_bone_frame_rotation_z"));
            Assert.That(MmdParserFfiMethods.VmdSummaryBoneFrameRotationWEntryPoint, Is.EqualTo("mmd_runtime_vmd_summary_bone_frame_rotation_w"));
            Assert.That(MmdParserFfiMethods.VmdSummaryBoneFrameInterpolationByteEntryPoint, Is.EqualTo("mmd_runtime_vmd_summary_bone_frame_interpolation_byte"));
            Assert.That(MmdParserFfiMethods.VmdSummaryMorphFrameNameEntryPoint, Is.EqualTo("mmd_runtime_vmd_summary_morph_frame_name"));
            Assert.That(MmdParserFfiMethods.VmdSummaryMorphFrameFrameEntryPoint, Is.EqualTo("mmd_runtime_vmd_summary_morph_frame_frame"));
            Assert.That(MmdParserFfiMethods.VmdSummaryMorphFrameWeightEntryPoint, Is.EqualTo("mmd_runtime_vmd_summary_morph_frame_weight"));
            Assert.That(MmdParserFfiMethods.VmdSummaryPropertyFrameFrameEntryPoint, Is.EqualTo("mmd_runtime_vmd_summary_property_frame_frame"));
            Assert.That(MmdParserFfiMethods.VmdSummaryPropertyFrameVisibleEntryPoint, Is.EqualTo("mmd_runtime_vmd_summary_property_frame_visible"));
            Assert.That(MmdParserFfiMethods.VmdSummaryPropertyFrameIkStateCountEntryPoint, Is.EqualTo("mmd_runtime_vmd_summary_property_frame_ik_state_count"));
            Assert.That(MmdParserFfiMethods.VmdSummaryPropertyFrameIkStateNameEntryPoint, Is.EqualTo("mmd_runtime_vmd_summary_property_frame_ik_state_name"));
            Assert.That(MmdParserFfiMethods.VmdSummaryPropertyFrameIkStateEnabledEntryPoint, Is.EqualTo("mmd_runtime_vmd_summary_property_frame_ik_state_enabled"));

            // VMD per-frame scene-track getters (camera/light/self-shadow; pinned on contract surface only).
            Assert.That(MmdParserFfiMethods.VmdSummaryCameraFrameFrameEntryPoint, Is.EqualTo("mmd_runtime_vmd_summary_camera_frame_frame"));
            Assert.That(MmdParserFfiMethods.VmdSummaryCameraFrameDistanceEntryPoint, Is.EqualTo("mmd_runtime_vmd_summary_camera_frame_distance"));
            Assert.That(MmdParserFfiMethods.VmdSummaryCameraFramePositionXEntryPoint, Is.EqualTo("mmd_runtime_vmd_summary_camera_frame_position_x"));
            Assert.That(MmdParserFfiMethods.VmdSummaryCameraFramePositionYEntryPoint, Is.EqualTo("mmd_runtime_vmd_summary_camera_frame_position_y"));
            Assert.That(MmdParserFfiMethods.VmdSummaryCameraFramePositionZEntryPoint, Is.EqualTo("mmd_runtime_vmd_summary_camera_frame_position_z"));
            Assert.That(MmdParserFfiMethods.VmdSummaryCameraFrameRotationXEntryPoint, Is.EqualTo("mmd_runtime_vmd_summary_camera_frame_rotation_x"));
            Assert.That(MmdParserFfiMethods.VmdSummaryCameraFrameRotationYEntryPoint, Is.EqualTo("mmd_runtime_vmd_summary_camera_frame_rotation_y"));
            Assert.That(MmdParserFfiMethods.VmdSummaryCameraFrameRotationZEntryPoint, Is.EqualTo("mmd_runtime_vmd_summary_camera_frame_rotation_z"));
            Assert.That(MmdParserFfiMethods.VmdSummaryCameraFrameInterpolationByteEntryPoint, Is.EqualTo("mmd_runtime_vmd_summary_camera_frame_interpolation_byte"));
            Assert.That(MmdParserFfiMethods.VmdSummaryCameraFrameFovEntryPoint, Is.EqualTo("mmd_runtime_vmd_summary_camera_frame_fov"));
            Assert.That(MmdParserFfiMethods.VmdSummaryCameraFramePerspectiveEntryPoint, Is.EqualTo("mmd_runtime_vmd_summary_camera_frame_perspective"));
            Assert.That(MmdParserFfiMethods.VmdSummaryLightFrameFrameEntryPoint, Is.EqualTo("mmd_runtime_vmd_summary_light_frame_frame"));
            Assert.That(MmdParserFfiMethods.VmdSummaryLightFrameColorXEntryPoint, Is.EqualTo("mmd_runtime_vmd_summary_light_frame_color_x"));
            Assert.That(MmdParserFfiMethods.VmdSummaryLightFrameColorYEntryPoint, Is.EqualTo("mmd_runtime_vmd_summary_light_frame_color_y"));
            Assert.That(MmdParserFfiMethods.VmdSummaryLightFrameColorZEntryPoint, Is.EqualTo("mmd_runtime_vmd_summary_light_frame_color_z"));
            Assert.That(MmdParserFfiMethods.VmdSummaryLightFrameDirectionXEntryPoint, Is.EqualTo("mmd_runtime_vmd_summary_light_frame_direction_x"));
            Assert.That(MmdParserFfiMethods.VmdSummaryLightFrameDirectionYEntryPoint, Is.EqualTo("mmd_runtime_vmd_summary_light_frame_direction_y"));
            Assert.That(MmdParserFfiMethods.VmdSummaryLightFrameDirectionZEntryPoint, Is.EqualTo("mmd_runtime_vmd_summary_light_frame_direction_z"));
            Assert.That(MmdParserFfiMethods.VmdSummarySelfShadowFrameFrameEntryPoint, Is.EqualTo("mmd_runtime_vmd_summary_self_shadow_frame_frame"));
            Assert.That(MmdParserFfiMethods.VmdSummarySelfShadowFrameModeEntryPoint, Is.EqualTo("mmd_runtime_vmd_summary_self_shadow_frame_mode"));
            Assert.That(MmdParserFfiMethods.VmdSummarySelfShadowFrameDistanceEntryPoint, Is.EqualTo("mmd_runtime_vmd_summary_self_shadow_frame_distance"));

            // PMX core getter entrypoints (geometry 9 + materials 13 + bones/IK 27).
            // Static asserts only (method must remain static-only and never call native).
            // These must pass even against current/locked package DLL before native update + parser migration.
            // Geometry
            Assert.That(MmdParserFfiMethods.PmxSummaryIndexCountEntryPoint, Is.EqualTo("mmd_runtime_pmx_summary_index_count"));
            Assert.That(MmdParserFfiMethods.PmxSummaryIndexEntryPoint, Is.EqualTo("mmd_runtime_pmx_summary_index"));
            Assert.That(MmdParserFfiMethods.PmxSummaryVertexPositionEntryPoint, Is.EqualTo("mmd_runtime_pmx_summary_vertex_position"));
            Assert.That(MmdParserFfiMethods.PmxSummaryVertexNormalEntryPoint, Is.EqualTo("mmd_runtime_pmx_summary_vertex_normal"));
            Assert.That(MmdParserFfiMethods.PmxSummaryVertexUvEntryPoint, Is.EqualTo("mmd_runtime_pmx_summary_vertex_uv"));
            Assert.That(MmdParserFfiMethods.PmxSummaryVertexSkinBoneIndexEntryPoint, Is.EqualTo("mmd_runtime_pmx_summary_vertex_skin_bone_index"));
            Assert.That(MmdParserFfiMethods.PmxSummaryVertexSkinWeightEntryPoint, Is.EqualTo("mmd_runtime_pmx_summary_vertex_skin_weight"));
            Assert.That(MmdParserFfiMethods.PmxSummaryVertexSkinningKindEntryPoint, Is.EqualTo("mmd_runtime_pmx_summary_vertex_skinning_kind"));
            Assert.That(MmdParserFfiMethods.PmxSummaryVertexSdefEnabledEntryPoint, Is.EqualTo("mmd_runtime_pmx_summary_vertex_sdef_enabled"));
            Assert.That(MmdParserFfiMethods.PmxSummaryVertexSdefCEntryPoint, Is.EqualTo("mmd_runtime_pmx_summary_vertex_sdef_c"));
            // Materials
            Assert.That(MmdParserFfiMethods.PmxSummaryMaterialNameEntryPoint, Is.EqualTo("mmd_runtime_pmx_summary_material_name"));
            Assert.That(MmdParserFfiMethods.PmxSummaryMaterialTexturePathEntryPoint, Is.EqualTo("mmd_runtime_pmx_summary_material_texture_path"));
            Assert.That(MmdParserFfiMethods.PmxSummaryMaterialSphereTexturePathEntryPoint, Is.EqualTo("mmd_runtime_pmx_summary_material_sphere_texture_path"));
            Assert.That(MmdParserFfiMethods.PmxSummaryMaterialToonTexturePathEntryPoint, Is.EqualTo("mmd_runtime_pmx_summary_material_toon_texture_path"));
            Assert.That(MmdParserFfiMethods.PmxSummaryMaterialSphereModeEntryPoint, Is.EqualTo("mmd_runtime_pmx_summary_material_sphere_mode"));
            Assert.That(MmdParserFfiMethods.PmxSummaryMaterialSharedToonIndexEntryPoint, Is.EqualTo("mmd_runtime_pmx_summary_material_shared_toon_index"));
            Assert.That(MmdParserFfiMethods.PmxSummaryMaterialDiffuseEntryPoint, Is.EqualTo("mmd_runtime_pmx_summary_material_diffuse"));
            Assert.That(MmdParserFfiMethods.PmxSummaryMaterialAmbientEntryPoint, Is.EqualTo("mmd_runtime_pmx_summary_material_ambient"));
            Assert.That(MmdParserFfiMethods.PmxSummaryMaterialEdgeColorEntryPoint, Is.EqualTo("mmd_runtime_pmx_summary_material_edge_color"));
            Assert.That(MmdParserFfiMethods.PmxSummaryMaterialEdgeSizeEntryPoint, Is.EqualTo("mmd_runtime_pmx_summary_material_edge_size"));
            Assert.That(MmdParserFfiMethods.PmxSummaryMaterialFaceCountEntryPoint, Is.EqualTo("mmd_runtime_pmx_summary_material_face_count"));
            Assert.That(MmdParserFfiMethods.PmxSummaryMaterialDoubleSidedEntryPoint, Is.EqualTo("mmd_runtime_pmx_summary_material_double_sided"));
            Assert.That(MmdParserFfiMethods.PmxSummaryMaterialEdgeFlagEntryPoint, Is.EqualTo("mmd_runtime_pmx_summary_material_edge_flag"));
            // Bones / IK
            Assert.That(MmdParserFfiMethods.PmxSummaryBoneNameEntryPoint, Is.EqualTo("mmd_runtime_pmx_summary_bone_name"));
            Assert.That(MmdParserFfiMethods.PmxSummaryBoneParentIndexEntryPoint, Is.EqualTo("mmd_runtime_pmx_summary_bone_parent_index"));
            Assert.That(MmdParserFfiMethods.PmxSummaryBoneLayerEntryPoint, Is.EqualTo("mmd_runtime_pmx_summary_bone_layer"));
            Assert.That(MmdParserFfiMethods.PmxSummaryBonePositionEntryPoint, Is.EqualTo("mmd_runtime_pmx_summary_bone_position"));
            Assert.That(MmdParserFfiMethods.PmxSummaryBoneRotatableEntryPoint, Is.EqualTo("mmd_runtime_pmx_summary_bone_rotatable"));
            Assert.That(MmdParserFfiMethods.PmxSummaryBoneTranslatableEntryPoint, Is.EqualTo("mmd_runtime_pmx_summary_bone_translatable"));
            Assert.That(MmdParserFfiMethods.PmxSummaryBoneAppendRotateEntryPoint, Is.EqualTo("mmd_runtime_pmx_summary_bone_append_rotate"));
            Assert.That(MmdParserFfiMethods.PmxSummaryBoneAppendTranslateEntryPoint, Is.EqualTo("mmd_runtime_pmx_summary_bone_append_translate"));
            Assert.That(MmdParserFfiMethods.PmxSummaryBoneAppendLocalEntryPoint, Is.EqualTo("mmd_runtime_pmx_summary_bone_append_local"));
            Assert.That(MmdParserFfiMethods.PmxSummaryBoneAppendParentIndexEntryPoint, Is.EqualTo("mmd_runtime_pmx_summary_bone_append_parent_index"));
            Assert.That(MmdParserFfiMethods.PmxSummaryBoneAppendWeightEntryPoint, Is.EqualTo("mmd_runtime_pmx_summary_bone_append_weight"));
            Assert.That(MmdParserFfiMethods.PmxSummaryBoneFixedAxisPresentEntryPoint, Is.EqualTo("mmd_runtime_pmx_summary_bone_fixed_axis_present"));
            Assert.That(MmdParserFfiMethods.PmxSummaryBoneFixedAxisEntryPoint, Is.EqualTo("mmd_runtime_pmx_summary_bone_fixed_axis"));
            Assert.That(MmdParserFfiMethods.PmxSummaryBoneLocalAxisPresentEntryPoint, Is.EqualTo("mmd_runtime_pmx_summary_bone_local_axis_present"));
            Assert.That(MmdParserFfiMethods.PmxSummaryBoneLocalAxisXEntryPoint, Is.EqualTo("mmd_runtime_pmx_summary_bone_local_axis_x"));
            Assert.That(MmdParserFfiMethods.PmxSummaryBoneLocalAxisZEntryPoint, Is.EqualTo("mmd_runtime_pmx_summary_bone_local_axis_z"));
            Assert.That(MmdParserFfiMethods.PmxSummaryBoneExternalParentPresentEntryPoint, Is.EqualTo("mmd_runtime_pmx_summary_bone_external_parent_present"));
            Assert.That(MmdParserFfiMethods.PmxSummaryBoneExternalParentKeyEntryPoint, Is.EqualTo("mmd_runtime_pmx_summary_bone_external_parent_key"));
            Assert.That(MmdParserFfiMethods.PmxSummaryBoneIkPresentEntryPoint, Is.EqualTo("mmd_runtime_pmx_summary_bone_ik_present"));
            Assert.That(MmdParserFfiMethods.PmxSummaryBoneIkTargetIndexEntryPoint, Is.EqualTo("mmd_runtime_pmx_summary_bone_ik_target_index"));
            Assert.That(MmdParserFfiMethods.PmxSummaryBoneIkLoopCountEntryPoint, Is.EqualTo("mmd_runtime_pmx_summary_bone_ik_loop_count"));
            Assert.That(MmdParserFfiMethods.PmxSummaryBoneIkLimitAngleEntryPoint, Is.EqualTo("mmd_runtime_pmx_summary_bone_ik_limit_angle"));
            Assert.That(MmdParserFfiMethods.PmxSummaryBoneIkLinkCountEntryPoint, Is.EqualTo("mmd_runtime_pmx_summary_bone_ik_link_count"));
            Assert.That(MmdParserFfiMethods.PmxSummaryBoneIkLinkBoneIndexEntryPoint, Is.EqualTo("mmd_runtime_pmx_summary_bone_ik_link_bone_index"));
            Assert.That(MmdParserFfiMethods.PmxSummaryBoneIkLinkLimitPresentEntryPoint, Is.EqualTo("mmd_runtime_pmx_summary_bone_ik_link_limit_present"));
            Assert.That(MmdParserFfiMethods.PmxSummaryBoneIkLinkLimitLowerEntryPoint, Is.EqualTo("mmd_runtime_pmx_summary_bone_ik_link_limit_lower"));
            Assert.That(MmdParserFfiMethods.PmxSummaryBoneIkLinkLimitUpperEntryPoint, Is.EqualTo("mmd_runtime_pmx_summary_bone_ik_link_limit_upper"));

            // PMX morph getter entrypoints (header/counts + offset families).
            // Static asserts only; no native calls (must pass against old/locked DLL).
            // Header / counts
            Assert.That(MmdParserFfiMethods.PmxSummaryMorphNameEntryPoint, Is.EqualTo("mmd_runtime_pmx_summary_morph_name"));
            Assert.That(MmdParserFfiMethods.PmxSummaryMorphEnglishNameEntryPoint, Is.EqualTo("mmd_runtime_pmx_summary_morph_english_name"));
            Assert.That(MmdParserFfiMethods.PmxSummaryMorphKindEntryPoint, Is.EqualTo("mmd_runtime_pmx_summary_morph_kind"));
            Assert.That(MmdParserFfiMethods.PmxSummaryMorphPanelEntryPoint, Is.EqualTo("mmd_runtime_pmx_summary_morph_panel"));
            Assert.That(MmdParserFfiMethods.PmxSummaryMorphVertexOffsetCountEntryPoint, Is.EqualTo("mmd_runtime_pmx_summary_morph_vertex_offset_count"));
            Assert.That(MmdParserFfiMethods.PmxSummaryMorphGroupOffsetCountEntryPoint, Is.EqualTo("mmd_runtime_pmx_summary_morph_group_offset_count"));
            Assert.That(MmdParserFfiMethods.PmxSummaryMorphBoneOffsetCountEntryPoint, Is.EqualTo("mmd_runtime_pmx_summary_morph_bone_offset_count"));
            Assert.That(MmdParserFfiMethods.PmxSummaryMorphUvOffsetCountEntryPoint, Is.EqualTo("mmd_runtime_pmx_summary_morph_uv_offset_count"));
            Assert.That(MmdParserFfiMethods.PmxSummaryMorphAdditionalUvOffsetCountEntryPoint, Is.EqualTo("mmd_runtime_pmx_summary_morph_additional_uv_offset_count"));
            Assert.That(MmdParserFfiMethods.PmxSummaryMorphMaterialOffsetCountEntryPoint, Is.EqualTo("mmd_runtime_pmx_summary_morph_material_offset_count"));
            Assert.That(MmdParserFfiMethods.PmxSummaryMorphFlipOffsetCountEntryPoint, Is.EqualTo("mmd_runtime_pmx_summary_morph_flip_offset_count"));
            Assert.That(MmdParserFfiMethods.PmxSummaryMorphImpulseOffsetCountEntryPoint, Is.EqualTo("mmd_runtime_pmx_summary_morph_impulse_offset_count"));
            // Vertex
            Assert.That(MmdParserFfiMethods.PmxSummaryMorphVertexOffsetVertexIndexEntryPoint, Is.EqualTo("mmd_runtime_pmx_summary_morph_vertex_offset_vertex_index"));
            Assert.That(MmdParserFfiMethods.PmxSummaryMorphVertexOffsetPositionEntryPoint, Is.EqualTo("mmd_runtime_pmx_summary_morph_vertex_offset_position"));
            // Group / flip
            Assert.That(MmdParserFfiMethods.PmxSummaryMorphGroupOffsetMorphIndexEntryPoint, Is.EqualTo("mmd_runtime_pmx_summary_morph_group_offset_morph_index"));
            Assert.That(MmdParserFfiMethods.PmxSummaryMorphGroupOffsetWeightEntryPoint, Is.EqualTo("mmd_runtime_pmx_summary_morph_group_offset_weight"));
            Assert.That(MmdParserFfiMethods.PmxSummaryMorphFlipOffsetMorphIndexEntryPoint, Is.EqualTo("mmd_runtime_pmx_summary_morph_flip_offset_morph_index"));
            Assert.That(MmdParserFfiMethods.PmxSummaryMorphFlipOffsetWeightEntryPoint, Is.EqualTo("mmd_runtime_pmx_summary_morph_flip_offset_weight"));
            // Bone
            Assert.That(MmdParserFfiMethods.PmxSummaryMorphBoneOffsetBoneIndexEntryPoint, Is.EqualTo("mmd_runtime_pmx_summary_morph_bone_offset_bone_index"));
            Assert.That(MmdParserFfiMethods.PmxSummaryMorphBoneOffsetTranslationEntryPoint, Is.EqualTo("mmd_runtime_pmx_summary_morph_bone_offset_translation"));
            Assert.That(MmdParserFfiMethods.PmxSummaryMorphBoneOffsetRotationEntryPoint, Is.EqualTo("mmd_runtime_pmx_summary_morph_bone_offset_rotation"));
            // UV / additional UV
            Assert.That(MmdParserFfiMethods.PmxSummaryMorphUvOffsetVertexIndexEntryPoint, Is.EqualTo("mmd_runtime_pmx_summary_morph_uv_offset_vertex_index"));
            Assert.That(MmdParserFfiMethods.PmxSummaryMorphUvOffsetValueEntryPoint, Is.EqualTo("mmd_runtime_pmx_summary_morph_uv_offset_value"));
            Assert.That(MmdParserFfiMethods.PmxSummaryMorphAdditionalUvOffsetVertexIndexEntryPoint, Is.EqualTo("mmd_runtime_pmx_summary_morph_additional_uv_offset_vertex_index"));
            Assert.That(MmdParserFfiMethods.PmxSummaryMorphAdditionalUvOffsetUvIndexEntryPoint, Is.EqualTo("mmd_runtime_pmx_summary_morph_additional_uv_offset_uv_index"));
            Assert.That(MmdParserFfiMethods.PmxSummaryMorphAdditionalUvOffsetValueEntryPoint, Is.EqualTo("mmd_runtime_pmx_summary_morph_additional_uv_offset_value"));
            // Material
            Assert.That(MmdParserFfiMethods.PmxSummaryMorphMaterialOffsetMaterialIndexEntryPoint, Is.EqualTo("mmd_runtime_pmx_summary_morph_material_offset_material_index"));
            Assert.That(MmdParserFfiMethods.PmxSummaryMorphMaterialOffsetOperationEntryPoint, Is.EqualTo("mmd_runtime_pmx_summary_morph_material_offset_operation"));
            Assert.That(MmdParserFfiMethods.PmxSummaryMorphMaterialOffsetDiffuseEntryPoint, Is.EqualTo("mmd_runtime_pmx_summary_morph_material_offset_diffuse"));
            Assert.That(MmdParserFfiMethods.PmxSummaryMorphMaterialOffsetSpecularEntryPoint, Is.EqualTo("mmd_runtime_pmx_summary_morph_material_offset_specular"));
            Assert.That(MmdParserFfiMethods.PmxSummaryMorphMaterialOffsetSpecularPowerEntryPoint, Is.EqualTo("mmd_runtime_pmx_summary_morph_material_offset_specular_power"));
            Assert.That(MmdParserFfiMethods.PmxSummaryMorphMaterialOffsetAmbientEntryPoint, Is.EqualTo("mmd_runtime_pmx_summary_morph_material_offset_ambient"));
            Assert.That(MmdParserFfiMethods.PmxSummaryMorphMaterialOffsetEdgeColorEntryPoint, Is.EqualTo("mmd_runtime_pmx_summary_morph_material_offset_edge_color"));
            Assert.That(MmdParserFfiMethods.PmxSummaryMorphMaterialOffsetEdgeSizeEntryPoint, Is.EqualTo("mmd_runtime_pmx_summary_morph_material_offset_edge_size"));
            Assert.That(MmdParserFfiMethods.PmxSummaryMorphMaterialOffsetTextureFactorEntryPoint, Is.EqualTo("mmd_runtime_pmx_summary_morph_material_offset_texture_factor"));
            Assert.That(MmdParserFfiMethods.PmxSummaryMorphMaterialOffsetSphereTextureFactorEntryPoint, Is.EqualTo("mmd_runtime_pmx_summary_morph_material_offset_sphere_texture_factor"));
            Assert.That(MmdParserFfiMethods.PmxSummaryMorphMaterialOffsetToonTextureFactorEntryPoint, Is.EqualTo("mmd_runtime_pmx_summary_morph_material_offset_toon_texture_factor"));
            // Impulse
            Assert.That(MmdParserFfiMethods.PmxSummaryMorphImpulseOffsetRigidbodyIndexEntryPoint, Is.EqualTo("mmd_runtime_pmx_summary_morph_impulse_offset_rigidbody_index"));
            Assert.That(MmdParserFfiMethods.PmxSummaryMorphImpulseOffsetLocalEntryPoint, Is.EqualTo("mmd_runtime_pmx_summary_morph_impulse_offset_local"));
            Assert.That(MmdParserFfiMethods.PmxSummaryMorphImpulseOffsetVelocityEntryPoint, Is.EqualTo("mmd_runtime_pmx_summary_morph_impulse_offset_velocity"));
            Assert.That(MmdParserFfiMethods.PmxSummaryMorphImpulseOffsetTorqueEntryPoint, Is.EqualTo("mmd_runtime_pmx_summary_morph_impulse_offset_torque"));

            // PMX display frame getter entrypoints (pinned on C# contract surface for post-JSON migration).
            Assert.That(MmdParserFfiMethods.PmxSummaryDisplayFrameNameEntryPoint, Is.EqualTo("mmd_runtime_pmx_summary_display_frame_name"));
            Assert.That(MmdParserFfiMethods.PmxSummaryDisplayFrameEnglishNameEntryPoint, Is.EqualTo("mmd_runtime_pmx_summary_display_frame_english_name"));
            Assert.That(MmdParserFfiMethods.PmxSummaryDisplayFrameSpecialEntryPoint, Is.EqualTo("mmd_runtime_pmx_summary_display_frame_special"));
            Assert.That(MmdParserFfiMethods.PmxSummaryDisplayFrameItemCountEntryPoint, Is.EqualTo("mmd_runtime_pmx_summary_display_frame_item_count"));
            Assert.That(MmdParserFfiMethods.PmxSummaryDisplayFrameItemKindEntryPoint, Is.EqualTo("mmd_runtime_pmx_summary_display_frame_item_kind"));
            Assert.That(MmdParserFfiMethods.PmxSummaryDisplayFrameItemIndexEntryPoint, Is.EqualTo("mmd_runtime_pmx_summary_display_frame_item_index"));

            // PMX physics getter entrypoints (rigidbody/joint only; soft body detail getters are deferred).
            Assert.That(MmdParserFfiMethods.PmxSummaryRigidbodyNameEntryPoint, Is.EqualTo("mmd_runtime_pmx_summary_rigidbody_name"));
            Assert.That(MmdParserFfiMethods.PmxSummaryRigidbodyEnglishNameEntryPoint, Is.EqualTo("mmd_runtime_pmx_summary_rigidbody_english_name"));
            Assert.That(MmdParserFfiMethods.PmxSummaryRigidbodyBoneIndexEntryPoint, Is.EqualTo("mmd_runtime_pmx_summary_rigidbody_bone_index"));
            Assert.That(MmdParserFfiMethods.PmxSummaryRigidbodyGroupEntryPoint, Is.EqualTo("mmd_runtime_pmx_summary_rigidbody_group"));
            Assert.That(MmdParserFfiMethods.PmxSummaryRigidbodyMaskEntryPoint, Is.EqualTo("mmd_runtime_pmx_summary_rigidbody_mask"));
            Assert.That(MmdParserFfiMethods.PmxSummaryRigidbodyShapeEntryPoint, Is.EqualTo("mmd_runtime_pmx_summary_rigidbody_shape"));
            Assert.That(MmdParserFfiMethods.PmxSummaryRigidbodySizeEntryPoint, Is.EqualTo("mmd_runtime_pmx_summary_rigidbody_size"));
            Assert.That(MmdParserFfiMethods.PmxSummaryRigidbodyPositionEntryPoint, Is.EqualTo("mmd_runtime_pmx_summary_rigidbody_position"));
            Assert.That(MmdParserFfiMethods.PmxSummaryRigidbodyRotationEntryPoint, Is.EqualTo("mmd_runtime_pmx_summary_rigidbody_rotation"));
            Assert.That(MmdParserFfiMethods.PmxSummaryRigidbodyMassEntryPoint, Is.EqualTo("mmd_runtime_pmx_summary_rigidbody_mass"));
            Assert.That(MmdParserFfiMethods.PmxSummaryRigidbodyLinearDampingEntryPoint, Is.EqualTo("mmd_runtime_pmx_summary_rigidbody_linear_damping"));
            Assert.That(MmdParserFfiMethods.PmxSummaryRigidbodyAngularDampingEntryPoint, Is.EqualTo("mmd_runtime_pmx_summary_rigidbody_angular_damping"));
            Assert.That(MmdParserFfiMethods.PmxSummaryRigidbodyRestitutionEntryPoint, Is.EqualTo("mmd_runtime_pmx_summary_rigidbody_restitution"));
            Assert.That(MmdParserFfiMethods.PmxSummaryRigidbodyFrictionEntryPoint, Is.EqualTo("mmd_runtime_pmx_summary_rigidbody_friction"));
            Assert.That(MmdParserFfiMethods.PmxSummaryRigidbodyModeEntryPoint, Is.EqualTo("mmd_runtime_pmx_summary_rigidbody_mode"));
            Assert.That(MmdParserFfiMethods.PmxSummaryJointNameEntryPoint, Is.EqualTo("mmd_runtime_pmx_summary_joint_name"));
            Assert.That(MmdParserFfiMethods.PmxSummaryJointEnglishNameEntryPoint, Is.EqualTo("mmd_runtime_pmx_summary_joint_english_name"));
            Assert.That(MmdParserFfiMethods.PmxSummaryJointKindEntryPoint, Is.EqualTo("mmd_runtime_pmx_summary_joint_kind"));
            Assert.That(MmdParserFfiMethods.PmxSummaryJointRigidbodyAIndexEntryPoint, Is.EqualTo("mmd_runtime_pmx_summary_joint_rigidbody_a_index"));
            Assert.That(MmdParserFfiMethods.PmxSummaryJointRigidbodyBIndexEntryPoint, Is.EqualTo("mmd_runtime_pmx_summary_joint_rigidbody_b_index"));
            Assert.That(MmdParserFfiMethods.PmxSummaryJointPositionEntryPoint, Is.EqualTo("mmd_runtime_pmx_summary_joint_position"));
            Assert.That(MmdParserFfiMethods.PmxSummaryJointRotationEntryPoint, Is.EqualTo("mmd_runtime_pmx_summary_joint_rotation"));
            Assert.That(MmdParserFfiMethods.PmxSummaryJointTranslationLowerLimitEntryPoint, Is.EqualTo("mmd_runtime_pmx_summary_joint_translation_lower_limit"));
            Assert.That(MmdParserFfiMethods.PmxSummaryJointTranslationUpperLimitEntryPoint, Is.EqualTo("mmd_runtime_pmx_summary_joint_translation_upper_limit"));
            Assert.That(MmdParserFfiMethods.PmxSummaryJointRotationLowerLimitEntryPoint, Is.EqualTo("mmd_runtime_pmx_summary_joint_rotation_lower_limit"));
            Assert.That(MmdParserFfiMethods.PmxSummaryJointRotationUpperLimitEntryPoint, Is.EqualTo("mmd_runtime_pmx_summary_joint_rotation_upper_limit"));
            Assert.That(MmdParserFfiMethods.PmxSummaryJointSpringTranslationFactorEntryPoint, Is.EqualTo("mmd_runtime_pmx_summary_joint_spring_translation_factor"));
            Assert.That(MmdParserFfiMethods.PmxSummaryJointSpringRotationFactorEntryPoint, Is.EqualTo("mmd_runtime_pmx_summary_joint_spring_rotation_factor"));

            AssertPrivateFfiSignature("VmdSummaryCameraFrameFrame", typeof(uint), typeof(IntPtr), typeof(IntPtr));
            AssertPrivateFfiSignature("VmdSummaryCameraFrameInterpolationByte", typeof(byte), typeof(IntPtr), typeof(IntPtr), typeof(IntPtr));
            AssertPrivateFfiSignature("VmdSummaryCameraFramePerspective", typeof(bool), typeof(IntPtr), typeof(IntPtr));
            AssertPrivateFfiSignature("VmdSummaryLightFrameColorX", typeof(float), typeof(IntPtr), typeof(IntPtr));
            AssertPrivateFfiSignature("VmdSummarySelfShadowFrameMode", typeof(byte), typeof(IntPtr), typeof(IntPtr));

            AssertPrivateFfiSignature("PmxSummaryIndex", typeof(uint), typeof(IntPtr), typeof(IntPtr));
            AssertPrivateFfiSignatureReturnName("PmxSummaryVertexSkinningKind", "ByteBuffer", typeof(IntPtr), typeof(IntPtr));
            AssertPrivateFfiSignature("PmxSummaryVertexSdefC", typeof(float), typeof(IntPtr), typeof(IntPtr), typeof(IntPtr), typeof(IntPtr));
            AssertPrivateFfiSignatureReturnName("PmxSummaryMaterialSphereMode", "ByteBuffer", typeof(IntPtr), typeof(IntPtr));
            AssertPrivateFfiSignature("PmxSummaryMaterialFaceCount", typeof(int), typeof(IntPtr), typeof(IntPtr));
            AssertPrivateFfiSignature("PmxSummaryBoneIkLoopCount", typeof(int), typeof(IntPtr), typeof(IntPtr));

            // Representative private signature reflection checks for PMX morph getters (no native invocation).
            AssertPrivateFfiSignatureReturnName("PmxSummaryMorphName", "ByteBuffer", typeof(IntPtr), typeof(IntPtr));
            AssertPrivateFfiSignatureReturnName("PmxSummaryMorphKind", "ByteBuffer", typeof(IntPtr), typeof(IntPtr));
            AssertPrivateFfiSignatureReturnName("PmxSummaryMorphPanel", "ByteBuffer", typeof(IntPtr), typeof(IntPtr));
            AssertPrivateFfiSignature("PmxSummaryMorphVertexOffsetVertexIndex", typeof(uint), typeof(IntPtr), typeof(IntPtr), typeof(IntPtr));
            AssertPrivateFfiSignature("PmxSummaryMorphAdditionalUvOffsetUvIndex", typeof(byte), typeof(IntPtr), typeof(IntPtr), typeof(IntPtr));
            AssertPrivateFfiSignatureReturnName("PmxSummaryMorphMaterialOffsetOperation", "ByteBuffer", typeof(IntPtr), typeof(IntPtr), typeof(IntPtr));
            AssertPrivateFfiSignature("PmxSummaryMorphImpulseOffsetLocal", typeof(bool), typeof(IntPtr), typeof(IntPtr), typeof(IntPtr));
            AssertPrivateFfiSignature("PmxSummaryMorphMaterialOffsetDiffuse", typeof(float), typeof(IntPtr), typeof(IntPtr), typeof(IntPtr), typeof(IntPtr));
            AssertPrivateFfiSignatureReturnName("PmxSummaryDisplayFrameName", "ByteBuffer", typeof(IntPtr), typeof(IntPtr));
            AssertPrivateFfiSignature("PmxSummaryDisplayFrameSpecial", typeof(bool), typeof(IntPtr), typeof(IntPtr));
            AssertPrivateFfiSignature("PmxSummaryDisplayFrameItemCount", typeof(IntPtr), typeof(IntPtr), typeof(IntPtr));
            AssertPrivateFfiSignatureReturnName("PmxSummaryDisplayFrameItemKind", "ByteBuffer", typeof(IntPtr), typeof(IntPtr), typeof(IntPtr));
            AssertPrivateFfiSignature("PmxSummaryDisplayFrameItemIndex", typeof(int), typeof(IntPtr), typeof(IntPtr), typeof(IntPtr));
            AssertPrivateFfiSignatureReturnName("PmxSummaryRigidbodyName", "ByteBuffer", typeof(IntPtr), typeof(IntPtr));
            AssertPrivateFfiSignature("PmxSummaryRigidbodyGroup", typeof(byte), typeof(IntPtr), typeof(IntPtr));
            AssertPrivateFfiSignature("PmxSummaryRigidbodyMask", typeof(ushort), typeof(IntPtr), typeof(IntPtr));
            AssertPrivateFfiSignatureReturnName("PmxSummaryJointKind", "ByteBuffer", typeof(IntPtr), typeof(IntPtr));
            AssertPrivateFfiSignature("PmxSummaryJointRigidbodyAIndex", typeof(int), typeof(IntPtr), typeof(IntPtr));
            AssertPrivateFfiSignature("PmxSummaryJointPosition", typeof(float), typeof(IntPtr), typeof(IntPtr), typeof(IntPtr));
        }

        [Test]
        public void FastRuntimeAndPhysicsWrapperNamesAreSeparate()
        {
            Assert.That(MmdRuntimeFfiMethods.LibraryName, Is.EqualTo("mmd_runtime_ffi"));
            Assert.That(MmdRuntimeFfiMethods.ExpectedAbiVersion, Is.EqualTo(1));
            Assert.That(MmdNativePhysicsMethods.LibraryName, Is.EqualTo("mmd_bullet"));
        }

        [Test]
        public void WindowsPluginLayoutContainsRuntimeAndPhysicsButNoLegacyParserDll()
        {
            string pluginRoot = Path.Combine(MmdTestFixtures.PackageRoot, "Runtime", "Plugins", "x86_64");
            string[] dllNames = Directory.GetFiles(pluginRoot, "*.dll")
                .Select(Path.GetFileName)
                .OrderBy(name => name, System.StringComparer.Ordinal)
                .ToArray();

            Assert.That(Path.Combine(pluginRoot, "mmd_runtime_ffi.dll"), Does.Exist);
            Assert.That(Path.Combine(pluginRoot, "mmd_runtime_ffi.dll.meta"), Does.Exist);
            Assert.That(Path.Combine(pluginRoot, "mmd_bullet.dll"), Does.Exist);
            Assert.That(Path.Combine(pluginRoot, "mmd_bullet.dll.meta"), Does.Exist);
            CollectionAssert.AreEqual(
                new[] { "mmd_bullet.dll", "mmd_runtime_ffi.dll" },
                dllNames);
            Assert.That(Path.Combine(pluginRoot, "yohawing_mmd_unity_native.dll"), Does.Not.Exist);
            Assert.That(Path.Combine(pluginRoot, "yohawing_mmd_unity_native.dll.meta"), Does.Not.Exist);
        }

        private static void AssertPrivateFfiSignature(string methodName, Type returnType, params Type[] parameterTypes)
        {
            MethodInfo method = GetPrivateFfiMethod(methodName);
            Assert.That(method.ReturnType, Is.EqualTo(returnType), methodName);
            CollectionAssert.AreEqual(parameterTypes, method.GetParameters().Select(parameter => parameter.ParameterType).ToArray(), methodName);
        }

        private static void AssertPrivateFfiSignatureReturnName(string methodName, string returnTypeName, params Type[] parameterTypes)
        {
            MethodInfo method = GetPrivateFfiMethod(methodName);
            Assert.That(method.ReturnType.Name, Is.EqualTo(returnTypeName), methodName);
            CollectionAssert.AreEqual(parameterTypes, method.GetParameters().Select(parameter => parameter.ParameterType).ToArray(), methodName);
        }

        private static MethodInfo GetPrivateFfiMethod(string methodName)
        {
            MethodInfo method = typeof(MmdParserFfiMethods).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(method, Is.Not.Null, methodName);
            return method;
        }
    }
}
