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
        private readonly object gate = new object();
        private MmdRuntimeFfiVmdContext? nativeVmdContext;
        private byte[]? nativeVmdContextSource;
        private Task<MmdRuntimeFfiVmdContext>? nativeVmdContextPreloadTask;
        private byte[]? nativeVmdContextPreloadSource;
        private TextAsset? sourceReadbackAsset;
        private byte[]? sourceReadback;

        internal byte[] ReadSourceBytes(byte[]? serializedData, TextAsset? rawSource)
        {
            lock (gate)
            {
                if (rawSource == null)
                {
                    if (sourceReadback != null || !ReferenceEquals(sourceReadbackAsset, null))
                    {
                        DisposeNativeContextLocked();
                        sourceReadbackAsset = null;
                        sourceReadback = null;
                    }

                    return serializedData ?? Array.Empty<byte>();
                }

                if (!ReferenceEquals(sourceReadbackAsset, rawSource) || sourceReadback == null)
                {
                    if ((sourceReadback != null || !ReferenceEquals(sourceReadbackAsset, null)) &&
                        !ReferenceEquals(sourceReadbackAsset, rawSource))
                    {
                        DisposeNativeContextLocked();
                    }

                    sourceReadbackAsset = rawSource;
                    sourceReadback = rawSource.bytes;
                }

                return sourceReadback!;
            }
        }

        internal bool TryGetOrCreateNativeVmdContext(
            byte[] sourceBytes,
            Func<byte[], string>? failureOverrideForTests,
            out MmdRuntimeFfiVmdContext? context,
            out string reason)
        {
            reason = string.Empty;
            Task<MmdRuntimeFfiVmdContext> preloadTask;
            lock (gate)
            {
                if (nativeVmdContext != null && ReferenceEquals(nativeVmdContextSource, sourceBytes))
                {
                    context = nativeVmdContext;
                    return true;
                }

                preloadTask = GetOrStartPreloadLocked(sourceBytes, failureOverrideForTests);
            }

            try
            {
                MmdRuntimeFfiVmdContext created = preloadTask.GetAwaiter().GetResult();
                lock (gate)
                {
                    if (ReferenceEquals(nativeVmdContextPreloadSource, sourceBytes))
                    {
                        nativeVmdContext = created;
                        nativeVmdContextSource = sourceBytes;
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
            byte[] sourceBytes,
            Func<byte[], string>? failureOverrideForTests)
        {
            lock (gate)
            {
                if (nativeVmdContext != null && ReferenceEquals(nativeVmdContextSource, sourceBytes))
                {
                    return Task.CompletedTask;
                }

                return GetOrStartPreloadLocked(sourceBytes, failureOverrideForTests);
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
                sourceReadbackAsset = null;
                sourceReadback = null;
            }
        }

        private Task<MmdRuntimeFfiVmdContext> GetOrStartPreloadLocked(
            byte[] sourceBytes,
            Func<byte[], string>? failureOverrideForTests)
        {
            if (ReferenceEquals(nativeVmdContextPreloadSource, sourceBytes) &&
                nativeVmdContextPreloadTask != null &&
                !nativeVmdContextPreloadTask.IsFaulted &&
                !nativeVmdContextPreloadTask.IsCanceled)
            {
                return nativeVmdContextPreloadTask;
            }

            nativeVmdContextPreloadSource = sourceBytes;
            nativeVmdContextPreloadTask = Task.Run(() =>
            {
                if (failureOverrideForTests != null)
                {
                    throw new NativeVmdContextPreloadException(failureOverrideForTests(sourceBytes));
                }

                return MmdRuntimeFfiVmdContext.Create(sourceBytes);
            });
            _ = nativeVmdContextPreloadTask.ContinueWith(
                completed => _ = completed.Exception,
                TaskContinuationOptions.OnlyOnFaulted);
            return nativeVmdContextPreloadTask;
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
            nativeVmdContextPreloadTask = null;
            nativeVmdContextPreloadSource = null;
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
