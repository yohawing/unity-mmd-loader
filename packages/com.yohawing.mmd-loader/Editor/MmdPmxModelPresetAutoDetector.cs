#nullable enable

using Mmd.Parser;

namespace Mmd.Editor
{
    internal static class MmdPmxModelPresetAutoDetector
    {
        public static MmdPmxModelPreset Detect(MmdModelDefinition model)
        {
            if (model == null || model.bones == null || model.bones.Count == 0)
            {
                return MmdPmxModelPreset.Stage;
            }

            MmdHumanoidBoneMappingReport report = MmdHumanoidBoneMappingEvaluator.Evaluate(model);
            return string.Equals(
                report.Readiness,
                MmdHumanoidMappingReadiness.Ready,
                System.StringComparison.Ordinal)
                ? MmdPmxModelPreset.Character
                : MmdPmxModelPreset.Stage;
        }

        public static bool IsCharacter(MmdPmxModelPreset preset)
        {
            return preset == MmdPmxModelPreset.Character;
        }
    }
}
