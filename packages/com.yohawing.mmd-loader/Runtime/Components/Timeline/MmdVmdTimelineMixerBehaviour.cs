#nullable enable

using System;
using UnityEngine.Playables;

namespace Mmd.Timeline
{
    /// <summary>
    /// Deterministic single-winner arbitration for overlapping VMD body-motion clips.
    /// This is not weighted pose blending: exactly one positive-weight input wins, and that
    /// winner's full pose is applied once per graph evaluation.
    /// </summary>
    [Serializable]
    public sealed class MmdVmdTimelineMixerBehaviour : PlayableBehaviour
    {
        public override void PrepareFrame(Playable playable, FrameData info)
        {
            // PrepareFrame runs top-down before child ProcessFrame, so track-managed clips can
            // no-op their direct apply path and leave evaluation to this mixer only.
            int inputCount = playable.GetInputCount();
            for (int i = 0; i < inputCount; i++)
            {
                Playable input = playable.GetInput(i);
                if (!TryGetBehaviour(input, out MmdVmdTimelineBehaviour? behaviour) || behaviour == null)
                {
                    continue;
                }

                behaviour.IsTrackManaged = true;
            }
        }

        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            if (!TrySelectWinner(playable, out int winnerIndex, out Playable winnerPlayable, out MmdVmdTimelineBehaviour? winner))
            {
                return;
            }

            if (winner == null)
            {
                return;
            }

            winner.ApplyTimelineEvaluation(winnerPlayable, playerData);
        }

        /// <summary>
        /// Greatest positive input weight wins. Equal weights resolve to the later input index.
        /// Zero-weight inputs are ignored; an all-zero frame has no winner.
        /// </summary>
        internal static bool TrySelectWinner(
            Playable playable,
            out int winnerIndex,
            out Playable winnerPlayable,
            out MmdVmdTimelineBehaviour? winner)
        {
            winnerIndex = -1;
            winnerPlayable = default;
            winner = null;

            int inputCount = playable.GetInputCount();
            float bestWeight = 0.0f;

            for (int i = 0; i < inputCount; i++)
            {
                float weight = playable.GetInputWeight(i);
                if (weight <= 0.0f)
                {
                    continue;
                }

                // Use >= so equal weights keep advancing to the later input index.
                if (winnerIndex >= 0 && weight < bestWeight)
                {
                    continue;
                }

                Playable input = playable.GetInput(i);
                if (!TryGetBehaviour(input, out MmdVmdTimelineBehaviour? behaviour) || behaviour == null)
                {
                    continue;
                }

                bestWeight = weight;
                winnerIndex = i;
                winnerPlayable = input;
                winner = behaviour;
            }

            return winnerIndex >= 0;
        }

        private static bool TryGetBehaviour(Playable input, out MmdVmdTimelineBehaviour? behaviour)
        {
            behaviour = null;
            if (!input.IsValid() || input.GetPlayableType() != typeof(MmdVmdTimelineBehaviour))
            {
                return false;
            }

            var scriptPlayable = (ScriptPlayable<MmdVmdTimelineBehaviour>)input;
            behaviour = scriptPlayable.GetBehaviour();
            return behaviour != null;
        }
    }
}
