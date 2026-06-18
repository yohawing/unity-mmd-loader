using System;
using UnityEngine;

namespace Yohawing.MmdUnity.Tracing
{
    public static class MmdActualTraceDumper
    {
        public const string FinalWorldUpdateCheckpoint = MmdTraceCheckpoints.FinalWorldUpdate;

        public static MmdTrace CreateTrace(string model, string motion, string space = "mmd")
        {
            if (string.IsNullOrWhiteSpace(model))
            {
                throw new ArgumentException("Model identifier is required.", nameof(model));
            }

            if (string.IsNullOrWhiteSpace(motion))
            {
                throw new ArgumentException("Motion identifier is required.", nameof(motion));
            }

            return new MmdTrace
            {
                schemaVersion = 1,
                model = model,
                motion = motion,
                space = string.IsNullOrWhiteSpace(space) ? "mmd" : space
            };
        }

        public static string ToJson(MmdTrace trace, bool prettyPrint = true)
        {
            if (trace == null)
            {
                throw new ArgumentNullException(nameof(trace));
            }

            return JsonUtility.ToJson(trace, prettyPrint);
        }
    }
}
