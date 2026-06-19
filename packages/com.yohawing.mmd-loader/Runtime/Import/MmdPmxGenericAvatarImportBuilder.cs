#nullable enable

using System;
using UnityEngine;

namespace Mmd.UnityIntegration
{
    /// <summary>
    /// Editor-independent helper for Generic Avatar construction during PMX import.
    /// The importer owns the animation type decision, sub-asset registration, and Animator assignment.
    /// </summary>
    public static class MmdPmxGenericAvatarImportBuilder
    {
        public readonly struct MmdPmxGenericAvatarImportResult
        {
            public Avatar? Avatar { get; }
            public string Readiness { get; }
            public string Diagnostic { get; }

            public MmdPmxGenericAvatarImportResult(Avatar? avatar, string readiness, string diagnostic)
            {
                Avatar = avatar;
                Readiness = readiness ?? string.Empty;
                Diagnostic = diagnostic ?? string.Empty;
            }
        }

        public static MmdPmxGenericAvatarImportResult TryBuildGenericAvatar(
            GameObject root,
            string modelName,
            bool shouldBuildGeneric,
            string animationTypeLabel,
            string rootMotionTransformName)
        {
            if (!shouldBuildGeneric)
            {
                return new MmdPmxGenericAvatarImportResult(
                    null,
                    "NotRequested",
                    "generic-avatar: animation type is " + animationTypeLabel);
            }

            if (root == null)
            {
                return new MmdPmxGenericAvatarImportResult(
                    null,
                    "HierarchyNotReady",
                    "generic-avatar: imported root is null");
            }

            string normalizedRootMotionTransformName = rootMotionTransformName ?? string.Empty;
            Avatar? avatar = null;
            try
            {
                avatar = AvatarBuilder.BuildGenericAvatar(root, normalizedRootMotionTransformName);
            }
            catch (Exception ex)
            {
                return new MmdPmxGenericAvatarImportResult(
                    null,
                    "AvatarInvalid",
                    "generic-avatar: AvatarBuilder.BuildGenericAvatar failed: " + ex.Message);
            }

            if (avatar == null || !avatar.isValid)
            {
                if (avatar != null)
                {
                    UnityEngine.Object.DestroyImmediate(avatar);
                }

                return new MmdPmxGenericAvatarImportResult(
                    null,
                    "AvatarInvalid",
                    "generic-avatar: AvatarBuilder.BuildGenericAvatar returned invalid avatar; rootMotionTransformName='" +
                    normalizedRootMotionTransformName + "'");
            }

            avatar.hideFlags = HideFlags.None;
            avatar.name = string.IsNullOrWhiteSpace(modelName)
                ? "Generic Avatar"
                : modelName + " Generic Avatar";

            return new MmdPmxGenericAvatarImportResult(
                avatar,
                "Ready",
                "generic-avatar: ready; rootMotionTransformName='" + normalizedRootMotionTransformName + "'");
        }
    }
}
