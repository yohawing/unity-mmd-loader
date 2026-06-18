#nullable enable

using System;

namespace Yohawing.MmdUnity.UnityIntegration
{
    [Serializable]
    public sealed class MmdShaderBindingDiagnostics
    {
        public string requestedShaderName = string.Empty;
        public string resolvedShaderName = string.Empty;
        public string fallbackShaderName = string.Empty;
        public string fallbackReason = string.Empty;
        public bool shaderFallbackUsed;
        public string[] fallbackCandidates = Array.Empty<string>();
    }
}
