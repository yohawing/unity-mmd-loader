#nullable enable

using System;
using System.Threading.Tasks;
using UnityEngine;
using Mmd.Native;

namespace Mmd
{
    /// <summary>
    /// Owns the transient source readback and shared native context used by one VMD asset.
    /// Serialized bytes and the raw-source reference remain owned by <see cref="MmdVmdAsset"/>.
    /// </summary>
    internal sealed class MmdVmdNativeContextCache
    {
        internal readonly struct SourceSnapshot
        {
            internal SourceSnapshot(byte[] bytes, long generation)
            {
                Bytes = bytes ?? throw new ArgumentNullException(nameof(bytes));
                Generation = generation;
            }

            internal byte[] Bytes { get; }

            internal long Generation { get; }
        }

        internal const string StaleSourceSnapshotReason =
            "VMD source snapshot is stale; its native playback context was not started.";

        private readonly object gate = new object();
        private readonly Func<byte[], Task<MmdRuntimeFfiVmdContext>> preloadFactory;
        private long sourceGeneration;
        private MmdRuntimeFfiVmdContext? nativeVmdContext;
        private byte[]? nativeVmdContextSource;
        private long nativeVmdContextGeneration;
        private Task<MmdRuntimeFfiVmdContext>? nativeVmdContextPreloadTask;
        private byte[]? nativeVmdContextPreloadSource;
        private long nativeVmdContextPreloadGeneration;
        private TextAsset? sourceReadbackAsset;
        private byte[]? sourceReadback;

        internal MmdVmdNativeContextCache()
            : this(sourceBytes => Task.Run(() => MmdRuntimeFfiVmdContext.Create(sourceBytes)))
        {
        }

        internal MmdVmdNativeContextCache(
            Func<byte[], Task<MmdRuntimeFfiVmdContext>> preloadFactory)
        {
            this.preloadFactory = preloadFactory ?? throw new ArgumentNullException(nameof(preloadFactory));
        }

        internal SourceSnapshot ReadSourceSnapshot(byte[]? serializedData, TextAsset? rawSource)
        {
            lock (gate)
            {
                if (rawSource == null)
                {
                    if (sourceReadback != null || !ReferenceEquals(sourceReadbackAsset, null))
                    {
                        DisposeNativeContextLocked();
                        sourceGeneration++;
                        sourceReadbackAsset = null;
                        sourceReadback = null;
                    }

                    return new SourceSnapshot(serializedData ?? Array.Empty<byte>(), sourceGeneration);
                }

                if (!ReferenceEquals(sourceReadbackAsset, rawSource) || sourceReadback == null)
                {
                    if ((sourceReadback != null || !ReferenceEquals(sourceReadbackAsset, null)) &&
                        !ReferenceEquals(sourceReadbackAsset, rawSource))
                    {
                        DisposeNativeContextLocked();
                        sourceGeneration++;
                    }

                    sourceReadbackAsset = rawSource;
                    sourceReadback = rawSource.bytes;
                }

                return new SourceSnapshot(sourceReadback!, sourceGeneration);
            }
        }

        internal byte[] ReadSourceBytes(byte[]? serializedData, TextAsset? rawSource)
        {
            return ReadSourceSnapshot(serializedData, rawSource).Bytes;
        }

        internal bool TryGetOrCreateNativeVmdContext(
            SourceSnapshot sourceSnapshot,
            Func<byte[], string>? failureOverrideForTests,
            out MmdRuntimeFfiVmdContext? context,
            out string reason)
        {
            reason = string.Empty;
            Task<MmdRuntimeFfiVmdContext> preloadTask;
            lock (gate)
            {
                if (sourceSnapshot.Generation != sourceGeneration)
                {
                    context = null;
                    reason = StaleSourceSnapshotReason;
                    return false;
                }

                if (nativeVmdContext != null &&
                    ReferenceEquals(nativeVmdContextSource, sourceSnapshot.Bytes) &&
                    nativeVmdContextGeneration == sourceSnapshot.Generation)
                {
                    context = nativeVmdContext;
                    return true;
                }

                preloadTask = GetOrStartPreloadLocked(sourceSnapshot, failureOverrideForTests);
            }

            try
            {
                MmdRuntimeFfiVmdContext created = preloadTask.GetAwaiter().GetResult();
                lock (gate)
                {
                    if (sourceSnapshot.Generation == sourceGeneration &&
                        ReferenceEquals(nativeVmdContextPreloadSource, sourceSnapshot.Bytes) &&
                        nativeVmdContextPreloadGeneration == sourceSnapshot.Generation)
                    {
                        nativeVmdContext = created;
                        nativeVmdContextSource = sourceSnapshot.Bytes;
                        nativeVmdContextGeneration = sourceSnapshot.Generation;
                        context = created;
                        return true;
                    }
                }

                created.Dispose();
                context = null;
                reason = "VMD source changed while its native playback context was being prepared.";
                return false;
            }
            catch (Exception exception) when (
                exception is MmdRuntimeUnsupportedException ||
                exception is MmdRuntimeNativeUnavailableException)
            {
                context = null;
                reason = exception.Message;
                return false;
            }
            catch (NativeVmdContextPreloadException exception)
            {
                context = null;
                reason = exception.Message;
                return false;
            }
        }

        internal Task BeginNativePlaybackPreload(
            SourceSnapshot sourceSnapshot,
            Func<byte[], string>? failureOverrideForTests)
        {
            lock (gate)
            {
                if (sourceSnapshot.Generation != sourceGeneration)
                {
                    return CreateStalePreloadTaskLocked();
                }

                if (nativeVmdContext != null &&
                    ReferenceEquals(nativeVmdContextSource, sourceSnapshot.Bytes) &&
                    nativeVmdContextGeneration == sourceSnapshot.Generation)
                {
                    return Task.CompletedTask;
                }

                return GetOrStartPreloadLocked(sourceSnapshot, failureOverrideForTests);
            }
        }

        /// <summary>
        /// Releases the native context and all source readback state.
        /// A cleanup failure intentionally leaves the context references intact so a later
        /// lifecycle call can retry the same native cleanup.
        /// </summary>
        internal void Dispose()
        {
            lock (gate)
            {
                DisposeNativeContextLocked();
                sourceGeneration++;
                sourceReadbackAsset = null;
                sourceReadback = null;
            }
        }

        private Task<MmdRuntimeFfiVmdContext> GetOrStartPreloadLocked(
            SourceSnapshot sourceSnapshot,
            Func<byte[], string>? failureOverrideForTests)
        {
            if (ReferenceEquals(nativeVmdContextPreloadSource, sourceSnapshot.Bytes) &&
                nativeVmdContextPreloadGeneration == sourceSnapshot.Generation &&
                nativeVmdContextPreloadTask != null &&
                !nativeVmdContextPreloadTask.IsFaulted &&
                !nativeVmdContextPreloadTask.IsCanceled)
            {
                return nativeVmdContextPreloadTask;
            }

            nativeVmdContextPreloadSource = sourceSnapshot.Bytes;
            nativeVmdContextPreloadGeneration = sourceSnapshot.Generation;
            Task<MmdRuntimeFfiVmdContext> preloadTask;
            if (failureOverrideForTests != null)
            {
                preloadTask = Task.Run(
                    (Func<MmdRuntimeFfiVmdContext>)(() =>
                    {
                        throw new NativeVmdContextPreloadException(failureOverrideForTests(sourceSnapshot.Bytes));
                    }));
            }
            else
            {
                preloadTask = preloadFactory(sourceSnapshot.Bytes)
                    ?? throw new InvalidOperationException(
                        "VMD preload factory returned a null task.");
            }
            nativeVmdContextPreloadTask = preloadTask;
            ObserveFaultedPreload(preloadTask);
            return preloadTask;
        }

        private Task<MmdRuntimeFfiVmdContext> CreateStalePreloadTaskLocked()
        {
            Task<MmdRuntimeFfiVmdContext> staleTask = Task.FromException<MmdRuntimeFfiVmdContext>(
                new NativeVmdContextPreloadException(StaleSourceSnapshotReason));
            ObserveFaultedPreload(staleTask);
            return staleTask;
        }

        private static void ObserveFaultedPreload(Task<MmdRuntimeFfiVmdContext> preloadTask)
        {
            _ = preloadTask.ContinueWith(
                completed => _ = completed.Exception,
                TaskContinuationOptions.OnlyOnFaulted);
        }

        private void DisposeNativeContextLocked()
        {
            MmdRuntimeFfiVmdContext? context = nativeVmdContext;
            if (context == null && nativeVmdContextPreloadTask != null)
            {
                try
                {
                    context = nativeVmdContextPreloadTask.GetAwaiter().GetResult();
                }
                catch
                {
                    // A failed creation owns no native handle. Its diagnostic was already
                    // observed by the preload continuation or the playback caller.
                    nativeVmdContextPreloadTask = null;
                    nativeVmdContextPreloadSource = null;
                    return;
                }
            }

            // Dispose deliberately happens before clearing the references. If native cleanup
            // is unavailable, the context retains its handle and a later lifecycle call can retry.
            context?.Dispose();
            nativeVmdContext = null;
            nativeVmdContextSource = null;
            nativeVmdContextGeneration = 0;
            nativeVmdContextPreloadTask = null;
            nativeVmdContextPreloadSource = null;
            nativeVmdContextPreloadGeneration = 0;
        }

        private sealed class NativeVmdContextPreloadException : Exception
        {
            internal NativeVmdContextPreloadException(string message)
                : base(message)
            {
            }
        }
    }
}
