#nullable enable

namespace Mmd
{
    internal static class MmdPmxModelPresetPolicy
    {
        public const string Character = "Character";

        public static bool AllowsAutomaticSelfShadowTarget(string? modelPreset)
        {
            return string.Equals(modelPreset, Character, System.StringComparison.Ordinal);
        }
    }
}
