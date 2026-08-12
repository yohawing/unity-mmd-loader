#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Mmd;
using Mmd.Parser;

namespace Mmd.UnityIntegration
{
    internal static class MmdPmxRuntimeParseCache
    {
        private const int Capacity = 4;
        private static readonly object Gate = new object();
        private static readonly Dictionary<string, Entry> Entries =
            new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
        private static readonly LinkedList<string> Recency = new LinkedList<string>();

        internal readonly struct Result
        {
            internal Result(
                byte[] bytes,
                MmdModelDefinition model,
                bool cacheHit,
                double sourceAcquireMs,
                double parseMs)
            {
                Bytes = bytes;
                Model = model;
                CacheHit = cacheHit;
                SourceAcquireMs = sourceAcquireMs;
                ParseMs = parseMs;
            }

            internal byte[] Bytes { get; }
            internal MmdModelDefinition Model { get; }
            internal bool CacheHit { get; }
            internal double SourceAcquireMs { get; }
            internal double ParseMs { get; }
        }

        private sealed class Entry
        {
            internal byte[] Bytes = Array.Empty<byte>();
            internal MmdModelDefinition Model = null!;
        }

        internal static Result Load(string path)
        {
            long acquireStart = Stopwatch.GetTimestamp();
            byte[] bytes = File.ReadAllBytes(path);
            double sourceAcquireMs = ElapsedMilliseconds(acquireStart);
            lock (Gate)
            {
                if (Entries.TryGetValue(path, out Entry? cached) &&
                    bytes.AsSpan().SequenceEqual(cached.Bytes))
                {
                    Touch(path);
                    return new Result(cached.Bytes, cached.Model, true, sourceAcquireMs, 0.0);
                }
            }

            long parseStart = Stopwatch.GetTimestamp();
            MmdModelDefinition model = new NativeMmdParser().LoadModel(bytes);
            MmdModelValidator.ThrowIfInvalid(model);
            double parseMs = ElapsedMilliseconds(parseStart);

            lock (Gate)
            {
                Entries[path] = new Entry
                {
                    Bytes = bytes,
                    Model = model
                };
                Touch(path);
                while (Entries.Count > Capacity && Recency.First != null)
                {
                    string oldest = Recency.First.Value;
                    Recency.RemoveFirst();
                    Entries.Remove(oldest);
                }
            }

            return new Result(bytes, model, false, sourceAcquireMs, parseMs);
        }

        private static void Touch(string path)
        {
            LinkedListNode<string>? node = Recency.First;
            while (node != null)
            {
                LinkedListNode<string>? next = node.Next;
                if (StringComparer.OrdinalIgnoreCase.Equals(node.Value, path))
                {
                    Recency.Remove(node);
                }

                node = next;
            }
            Recency.AddLast(path);
        }

        private static double ElapsedMilliseconds(long startTimestamp)
        {
            return (Stopwatch.GetTimestamp() - startTimestamp) * 1000.0 / Stopwatch.Frequency;
        }
    }
}
