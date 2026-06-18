#nullable enable

using UnityEngine;
using Yohawing.MmdUnity;

namespace Yohawing.MmdUnity.UnityIntegration
{
    /// <summary>
    /// Editor-independent helper for the Humanoid Avatar sub-asset build step during PMX import.
    /// Owns only the readiness/diagnostic selection for non-Humanoid, the proxy rig creation,
    /// Avatar build, diagnostics combination, naming/hideFlags adjustment, and proxy root cleanup.
    /// </summary>
    /// <remarks>
    /// This type lives in Runtime/Import under Yohawing.MmdUnity.UnityIntegration for API compat
    /// with prior import builders. The importer retains ownership of enum decisions and sub-asset
    /// registration.
    /// </remarks>
    public static class MmdPmxHumanoidAvatarImportBuilder
    {
        /// <summary>
        /// Result of the (optional) Humanoid Avatar import build.
        /// Avatar is non-null only on successful Ready case (with hideFlags=None and name applied).
        /// </summary>
        public readonly struct MmdPmxHumanoidAvatarImportResult
        {
            public Avatar? Avatar { get; }
            public string Readiness { get; }
            public string Diagnostic { get; }

            public MmdPmxHumanoidAvatarImportResult(Avatar? avatar, string readiness, string diagnostic)
            {
                Avatar = avatar;
                Readiness = readiness ?? string.Empty;
                Diagnostic = diagnostic ?? string.Empty;
            }
        }

        /// <summary>
        /// Performs the Humanoid Avatar build decision and execution using the provided
        /// MmdPmxAsset (post-initialize) and model name for naming.
        /// Accepts bool + string label so the runtime helper does not depend on importer setting enums.
        /// </summary>
        public static MmdPmxHumanoidAvatarImportResult TryBuildHumanoidAvatar(
            MmdPmxAsset asset,
            string modelName,
            bool shouldBuildHumanoid,
            string animationTypeLabel)
        {
            if (!shouldBuildHumanoid)
            {
                return new MmdPmxHumanoidAvatarImportResult(
                    null,
                    "NotRequested",
                    "humanoid-avatar: animation type is " + animationTypeLabel);
            }

            MmdHumanoidProxyRigResult proxyRig = MmdHumanoidProxyRigFactory.CreateProxyRig(asset);
            string readiness = proxyRig.Readiness;
            string diagnostic = string.Join("; ", proxyRig.Diagnostics);

            if (proxyRig.ProxyRoot == null)
            {
                return new MmdPmxHumanoidAvatarImportResult(null, readiness, diagnostic);
            }

            try
            {
                MmdHumanoidAvatarBuildResult avatarResult = MmdHumanoidProxyRigFactory.BuildAvatar(proxyRig);
                diagnostic = CombineDiagnostics(diagnostic, avatarResult.Diagnostics);

                if (!avatarResult.IsValidHumanAvatar || avatarResult.Avatar == null)
                {
                    readiness = string.Equals(proxyRig.Readiness, MmdHumanoidSetupAsset.ReadyReadiness, System.StringComparison.Ordinal)
                        ? "AvatarInvalid"
                        : proxyRig.Readiness;
                    return new MmdPmxHumanoidAvatarImportResult(null, readiness, diagnostic);
                }

                Avatar avatar = avatarResult.Avatar;
                avatar.hideFlags = HideFlags.None;
                avatar.name = string.IsNullOrWhiteSpace(modelName)
                    ? "Avatar"
                    : modelName + " Avatar";
                readiness = MmdHumanoidSetupAsset.ReadyReadiness;
                return new MmdPmxHumanoidAvatarImportResult(avatar, readiness, diagnostic);
            }
            finally
            {
                Object.DestroyImmediate(proxyRig.ProxyRoot);
            }
        }

        private static string CombineDiagnostics(string first, System.Collections.Generic.IReadOnlyList<string> second)
        {
            string joinedSecond = second != null ? string.Join("; ", second) : string.Empty;
            if (string.IsNullOrWhiteSpace(first))
            {
                return joinedSecond;
            }

            if (string.IsNullOrWhiteSpace(joinedSecond))
            {
                return first;
            }

            return first + "; " + joinedSecond;
        }
    }
}
