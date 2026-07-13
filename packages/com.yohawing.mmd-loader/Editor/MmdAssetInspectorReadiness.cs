#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Mmd.Parser;
using Mmd.Rendering.Universal;
using Mmd.UnityIntegration;

namespace Mmd.Editor
{
    internal readonly struct MmdAnimationTimelineReadiness
    {
        public MmdAnimationTimelineReadiness(
            string timelineBindingTarget,
            string vmdDropReadiness,
            string playbackSource)
        {
            TimelineBindingTarget = timelineBindingTarget;
            VmdDropReadiness = vmdDropReadiness;
            PlaybackSource = playbackSource;
        }

        public string TimelineBindingTarget { get; }

        public string VmdDropReadiness { get; }

        public string PlaybackSource { get; }
    }

    internal readonly struct MmdOutlineReadiness
    {
        public const string NotClaimed = "Not claimed";

        public MmdOutlineReadiness(
            int outlineEligibleMaterialCount,
            string runtimePath,
            string releaseMode,
            string finalVisualParity)
        {
            OutlineEligibleMaterialCount = System.Math.Max(0, outlineEligibleMaterialCount);
            RuntimePath = string.IsNullOrWhiteSpace(runtimePath) ? "Unknown" : runtimePath;
            ReleaseMode = string.IsNullOrWhiteSpace(releaseMode) ? "Unknown" : releaseMode;
            FinalVisualParity = string.IsNullOrWhiteSpace(finalVisualParity) ? NotClaimed : finalVisualParity;
        }

        public int OutlineEligibleMaterialCount { get; }

        public string RuntimePath { get; }

        public string ReleaseMode { get; }

        public string FinalVisualParity { get; }
    }

    internal readonly struct MmdSelfShadowRendererSetupReadiness
    {
        public static readonly MmdSelfShadowRendererSetupReadiness NoUrpAsset =
            new MmdSelfShadowRendererSetupReadiness(false, 0, 0, 0);

        public MmdSelfShadowRendererSetupReadiness(
            bool hasUrpAsset,
            int rendererDataCount,
            int featureCount,
            int enabledFeatureCount,
            int activeRendererDataIndex = -1,
            int activeFeatureCount = -1,
            int activeEnabledFeatureCount = -1)
        {
            HasUrpAsset = hasUrpAsset;
            RendererDataCount = Math.Max(0, rendererDataCount);
            FeatureCount = Math.Max(0, featureCount);
            EnabledFeatureCount = Math.Max(0, enabledFeatureCount);
            ActiveRendererDataIndex = activeRendererDataIndex;
            ActiveFeatureCount = Math.Max(0, activeFeatureCount < 0 ? FeatureCount : activeFeatureCount);
            ActiveEnabledFeatureCount = Math.Max(0,
                activeEnabledFeatureCount < 0 ? EnabledFeatureCount : activeEnabledFeatureCount);
        }

        public bool HasUrpAsset { get; }

        public int RendererDataCount { get; }

        public int FeatureCount { get; }

        public int EnabledFeatureCount { get; }

        public int ActiveRendererDataIndex { get; }

        public int ActiveFeatureCount { get; }

        public int ActiveEnabledFeatureCount { get; }

        public bool FeaturePresentOnAnyRendererData => FeatureCount > 0;

        public bool FeatureEnabledOnAnyRendererData => EnabledFeatureCount > 0;

        public bool FeaturePresentOnActiveRendererData => ActiveFeatureCount > 0;

        public bool FeatureEnabledOnActiveRendererData => ActiveEnabledFeatureCount > 0;
    }

    internal readonly struct MmdScaleAwarePhysicsReadiness
    {
        public const string MmdSpaceReadback = "mmd-space-scale-aware";
        public const string ScaleAwareHandoffReady = "scale-aware-runtime-handoff-ready";
        public const string ScaleAwareSmokeCovered = "scale-aware-live-physics-smoke-covered";

        public MmdScaleAwarePhysicsReadiness(
            float importScale,
            bool hasPhysicsDescriptors,
            string gravityPolicy,
            string backendReadbackSpace,
            string scaleAwareHandoffReadiness,
            string requiredSmoke)
        {
            ImportScale = importScale;
            HasPhysicsDescriptors = hasPhysicsDescriptors;
            GravityPolicy = gravityPolicy ?? string.Empty;
            BackendReadbackSpace = backendReadbackSpace ?? string.Empty;
            ScaleAwareHandoffReadiness = scaleAwareHandoffReadiness ?? string.Empty;
            RequiredSmoke = requiredSmoke ?? string.Empty;
        }

        public float ImportScale { get; }

        public bool HasPhysicsDescriptors { get; }

        public string GravityPolicy { get; }

        public string BackendReadbackSpace { get; }

        public string ScaleAwareHandoffReadiness { get; }

        public string RequiredSmoke { get; }
    }

    internal readonly struct MmdVmdMotionSummary
    {
        public MmdVmdMotionSummary(
            string targetModelName,
            int maxFrame,
            int boneKeyframeCount,
            int morphKeyframeCount,
            int modelKeyframeCount,
            int constraintStateCount,
            int cameraKeyframeCount = 0,
            int lightKeyframeCount = 0,
            int selfShadowKeyframeCount = 0)
        {
            TargetModelName = targetModelName ?? string.Empty;
            MaxFrame = maxFrame;
            BoneKeyframeCount = boneKeyframeCount;
            MorphKeyframeCount = morphKeyframeCount;
            ModelKeyframeCount = modelKeyframeCount;
            ConstraintStateCount = constraintStateCount;
            CameraKeyframeCount = Math.Max(0, cameraKeyframeCount);
            LightKeyframeCount = Math.Max(0, lightKeyframeCount);
            SelfShadowKeyframeCount = Math.Max(0, selfShadowKeyframeCount);
        }

        public string TargetModelName { get; }

        public int MaxFrame { get; }

        public int BoneKeyframeCount { get; }

        public int MorphKeyframeCount { get; }

        public int ModelKeyframeCount { get; }

        public int ConstraintStateCount { get; }

        public int CameraKeyframeCount { get; }

        public int LightKeyframeCount { get; }

        public int SelfShadowKeyframeCount { get; }
    }

    /// <summary>
    /// Small immutable diagnostic shape for VMD Timeline readiness preview.
    /// Duration (MaxFrame), camera/light scene-motion presence, and self-shadow scene-motion diagnostics
    /// are derived exclusively from MmdVmdAsset cached import summary.
    /// No LoadMotion or parse occurs through this helper (see GetVmdTimelineReadiness).
    /// </summary>
    internal readonly struct MmdVmdTimelineReadiness
    {
        public MmdVmdTimelineReadiness(
            int maxFrame,
            int cameraKeyframeCount,
            int lightKeyframeCount,
            int selfShadowKeyframeCount,
            string clipDurationSource,
            string sceneMotionStatus,
            string selfShadowSceneMotionStatus,
            string clipCreationRequirement)
        {
            MaxFrame = Math.Max(0, maxFrame);
            CameraKeyframeCount = Math.Max(0, cameraKeyframeCount);
            LightKeyframeCount = Math.Max(0, lightKeyframeCount);
            SelfShadowKeyframeCount = Math.Max(0, selfShadowKeyframeCount);
            HasSceneMotion = (CameraKeyframeCount > 0) || (LightKeyframeCount > 0);
            HasSelfShadowSceneMotion = SelfShadowKeyframeCount > 0;
            ClipDurationSource = string.IsNullOrWhiteSpace(clipDurationSource) ? "Unavailable" : clipDurationSource;
            SceneMotionStatus = string.IsNullOrWhiteSpace(sceneMotionStatus) ? "None" : sceneMotionStatus;
            SelfShadowSceneMotionStatus = string.IsNullOrWhiteSpace(selfShadowSceneMotionStatus)
                ? "Self-shadow scene motion: none"
                : selfShadowSceneMotionStatus;
            ClipCreationRequirement = string.IsNullOrWhiteSpace(clipCreationRequirement)
                ? "PMX model source and Timeline are required for VMD Clip creation."
                : clipCreationRequirement;
        }

        public int MaxFrame { get; }

        public int CameraKeyframeCount { get; }

        public int LightKeyframeCount { get; }

        public int SelfShadowKeyframeCount { get; }

        public bool HasSceneMotion { get; }

        public bool HasSelfShadowSceneMotion { get; }

        public string ClipDurationSource { get; }

        public string SceneMotionStatus { get; }

        public string SelfShadowSceneMotionStatus { get; }

        public string ClipCreationRequirement { get; }
    }
}
