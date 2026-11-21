#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Mmd.Tests
{
    /// <summary>
    /// Serializes every editor curve of a baked clip with exact float bits so characterization
    /// tests can pin binding order, key times, values and tangents without a tolerance.
    /// </summary>
    internal static class MmdClipCurveDump
    {
        internal static List<string> FormatResult(AnimationClip? clip, IReadOnlyList<string> diagnostics)
        {
            var lines = new List<string>();
            foreach (string diagnostic in diagnostics)
            {
                lines.Add("diag: " + diagnostic);
            }

            if (clip == null)
            {
                lines.Add("clip: null");
                return lines;
            }

            lines.Add("clip.name: " + clip.name);
            lines.Add("clip.frameRate: " + Bits(clip.frameRate));
            lines.Add("clip.length: " + Bits(clip.length));
            lines.AddRange(Format(clip));
            return lines;
        }

        internal static void AssertMatchesGolden(IReadOnlyList<string> actual, string goldenPath)
        {
            Assert.That(goldenPath, Does.Exist, "golden file is missing");
            string[] expected = File.ReadAllLines(goldenPath, Encoding.UTF8);
            int common = Math.Min(expected.Length, actual.Count);
            for (int index = 0; index < common; index++)
            {
                if (!string.Equals(expected[index], actual[index], StringComparison.Ordinal))
                {
                    Assert.Fail(
                        "clip dump differs from " + Path.GetFileName(goldenPath) + " at line " + (index + 1)
                        + "\nexpected: " + expected[index]
                        + "\nactual:   " + actual[index]);
                }
            }

            Assert.That(actual.Count, Is.EqualTo(expected.Length), "clip dump line count");
        }

        internal static List<string> Format(AnimationClip clip)
        {
            var lines = new List<string>();
            foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(clip))
            {
                AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, binding)!;
                var line = new StringBuilder();
                line.Append(binding.path).Append('|')
                    .Append(binding.type.Name).Append('|')
                    .Append(binding.propertyName).Append('|')
                    .Append(curve.keys.Length.ToString(CultureInfo.InvariantCulture));
                foreach (Keyframe key in curve.keys)
                {
                    line.Append(' ')
                        .Append(Bits(key.time)).Append(':')
                        .Append(Bits(key.value)).Append(':')
                        .Append(Bits(key.inTangent)).Append(':')
                        .Append(Bits(key.outTangent));
                }

                lines.Add(line.ToString());
            }

            return lines;
        }

        internal static string Bits(float value)
        {
            return BitConverter.SingleToInt32Bits(value).ToString("x8", CultureInfo.InvariantCulture);
        }
    }
}
