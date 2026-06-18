#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Mmd.Rendering;

namespace Mmd
{
    [Serializable]
    public sealed class MmdPlaybackSnapshotSummary
    {
        public string model = string.Empty;
        public string motion = string.Empty;
        public int frame;
        public int boneCount;
        public int morphCount;
        public int vertexCount;
        public int indexCount;
        public int skinningCount;
        public int materialCount;
        public int submeshCount;
        public int urpMaterialBindingCount;
        public int vertexMorphCount;
        public int ikCount;
    }

    [Serializable]
    public sealed class MmdPlaybackSnapshotSequenceSummary
    {
        public string model = string.Empty;
        public string motion = string.Empty;
        public int frameCount;
        public int firstFrame;
        public int lastFrame;
        public int boneCount;
        public int morphCount;
        public int vertexCount;
        public int indexCount;
        public int skinningCount;
        public int materialCount;
        public int submeshCount;
        public int urpMaterialBindingCount;
        public int vertexMorphCount;
        public int ikCount;
    }

    public static class MmdPlaybackSnapshotDiagnostics
    {
        public static MmdPlaybackSnapshotSummary Summarize(MmdPlaybackSnapshot snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            MmdEvaluatedFrame frame = snapshot.frame ?? throw new InvalidOperationException("Playback snapshot frame is required.");
            MmdRenderingDescriptor rendering = snapshot.rendering ?? throw new InvalidOperationException("Playback snapshot rendering is required.");
            return new MmdPlaybackSnapshotSummary
            {
                model = snapshot.model ?? string.Empty,
                motion = snapshot.motion ?? string.Empty,
                frame = frame.frame,
                boneCount = CountRequired(frame.bones, "frame.bones"),
                morphCount = CountRequired(frame.morphs, "frame.morphs"),
                vertexCount = CountRequired(rendering.vertices, "rendering.vertices"),
                indexCount = CountRequired(rendering.indices, "rendering.indices"),
                skinningCount = CountRequired(rendering.skinning, "rendering.skinning"),
                materialCount = CountRequired(rendering.materials, "rendering.materials"),
                submeshCount = CountRequired(rendering.submeshes, "rendering.submeshes"),
                urpMaterialBindingCount = CountRequired(rendering.urpMaterialBindings, "rendering.urpMaterialBindings"),
                vertexMorphCount = CountRequired(rendering.vertexMorphs, "rendering.vertexMorphs"),
                ikCount = rendering.ikCount
            };
        }

        public static MmdPlaybackSnapshotSequenceSummary SummarizeSequence(IReadOnlyList<MmdPlaybackSnapshot> snapshots)
        {
            if (snapshots == null)
            {
                throw new ArgumentNullException(nameof(snapshots));
            }

            if (snapshots.Count == 0)
            {
                throw new ArgumentException("At least one snapshot is required.", nameof(snapshots));
            }

            for (int i = 0; i < snapshots.Count; i++)
            {
                if (snapshots[i] == null)
                {
                    throw new InvalidOperationException($"Playback snapshot sequence item is required: {i}");
                }
            }

            List<MmdPlaybackSnapshot> orderedSnapshots = snapshots
                .OrderBy(snapshot => (snapshot.frame ?? throw new InvalidOperationException("Playback snapshot frame is required.")).frame)
                .ToList();
            MmdPlaybackSnapshot first = orderedSnapshots[0];
            MmdPlaybackSnapshot last = orderedSnapshots[orderedSnapshots.Count - 1];
            MmdPlaybackSnapshotSummary firstSummary = Summarize(first);
            for (int i = 1; i < orderedSnapshots.Count; i++)
            {
                MmdPlaybackSnapshotSummary summary = Summarize(orderedSnapshots[i]);
                EnsureSequenceString(firstSummary.model, summary.model, nameof(MmdPlaybackSnapshotSummary.model));
                EnsureSequenceString(firstSummary.motion, summary.motion, nameof(MmdPlaybackSnapshotSummary.motion));
                EnsureSequenceCount(firstSummary.boneCount, summary.boneCount, nameof(MmdPlaybackSnapshotSummary.boneCount));
                EnsureSequenceCount(firstSummary.morphCount, summary.morphCount, nameof(MmdPlaybackSnapshotSummary.morphCount));
                EnsureSequenceCount(firstSummary.vertexCount, summary.vertexCount, nameof(MmdPlaybackSnapshotSummary.vertexCount));
                EnsureSequenceCount(firstSummary.indexCount, summary.indexCount, nameof(MmdPlaybackSnapshotSummary.indexCount));
                EnsureSequenceCount(firstSummary.skinningCount, summary.skinningCount, nameof(MmdPlaybackSnapshotSummary.skinningCount));
                EnsureSequenceCount(firstSummary.materialCount, summary.materialCount, nameof(MmdPlaybackSnapshotSummary.materialCount));
                EnsureSequenceCount(firstSummary.submeshCount, summary.submeshCount, nameof(MmdPlaybackSnapshotSummary.submeshCount));
                EnsureSequenceCount(firstSummary.urpMaterialBindingCount, summary.urpMaterialBindingCount, nameof(MmdPlaybackSnapshotSummary.urpMaterialBindingCount));
                EnsureSequenceCount(firstSummary.vertexMorphCount, summary.vertexMorphCount, nameof(MmdPlaybackSnapshotSummary.vertexMorphCount));
                EnsureSequenceCount(firstSummary.ikCount, summary.ikCount, nameof(MmdPlaybackSnapshotSummary.ikCount));
            }

            return new MmdPlaybackSnapshotSequenceSummary
            {
                model = firstSummary.model,
                motion = firstSummary.motion,
                frameCount = snapshots.Count,
                firstFrame = first.frame.frame,
                lastFrame = last.frame.frame,
                boneCount = firstSummary.boneCount,
                morphCount = firstSummary.morphCount,
                vertexCount = firstSummary.vertexCount,
                indexCount = firstSummary.indexCount,
                skinningCount = firstSummary.skinningCount,
                materialCount = firstSummary.materialCount,
                submeshCount = firstSummary.submeshCount,
                urpMaterialBindingCount = firstSummary.urpMaterialBindingCount,
                vertexMorphCount = firstSummary.vertexMorphCount,
                ikCount = firstSummary.ikCount
            };
        }

        private static void EnsureSequenceCount(int expected, int actual, string field)
        {
            if (expected != actual)
            {
                throw new InvalidOperationException($"Playback snapshot sequence {field} changed: {expected} != {actual}");
            }
        }

        private static void EnsureSequenceString(string expected, string actual, string field)
        {
            if (!string.Equals(expected, actual, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Playback snapshot sequence {field} changed: {expected} != {actual}");
            }
        }

        private static int CountRequired<T>(IReadOnlyCollection<T>? collection, string field)
        {
            if (collection == null)
            {
                throw new InvalidOperationException($"Playback snapshot {field} is required.");
            }

            return collection.Count;
        }
    }
}
