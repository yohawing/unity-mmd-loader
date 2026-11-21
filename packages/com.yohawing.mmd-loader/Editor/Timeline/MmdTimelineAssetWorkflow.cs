#nullable enable

using System;
using Mmd;

namespace Mmd.Editor.Timeline
{
    internal static class MmdTimelineAssetWorkflow
    {
        /// <summary>
        /// Clip duration in seconds from a (cached) max frame, so callers that already have the
        /// import-time summary value can avoid re-parsing the VMD just to size a clip.
        /// </summary>
        public static double CalculateClipDurationSeconds(int maxFrame, float frameRate)
        {
            MmdPlaybackTime.ValidateFrameRate(frameRate);
            return (Math.Max(0, maxFrame) + 1) / (double)frameRate;
        }
    }
}
