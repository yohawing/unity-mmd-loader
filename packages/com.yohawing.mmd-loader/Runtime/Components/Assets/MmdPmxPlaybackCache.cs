#nullable enable

using System;
using System.Threading.Tasks;
using Mmd.Parser;
using Mmd.Rendering;
using Mmd.UnityIntegration;

namespace Mmd
{
    /// <summary>
    /// Owns the non-serialized model and rendering-descriptor state used by synchronous PMX playback.
    /// The asset remains the owner of serialized bytes; this cache is invalidated whenever that source changes.
    /// </summary>
    internal sealed class MmdPmxPlaybackCache
    {
        private readonly object gate = new object();
        private byte[] source;
        private byte[]? modelCacheSource;
        private MmdModelDefinition? modelCache;
        private byte[]? preloadSource;
        private Task<MmdModelDefinition>? preloadTask;
        private MmdMaterialPreset descriptorPreset;
        private MmdRenderingDescriptor? descriptorCache;
        private Task<MmdRenderingDescriptor>? descriptorTask;
        private int modelPreloadGeneration;
        private int descriptorModelPreloadGeneration = -1;

        internal MmdPmxPlaybackCache(byte[] initialSource)
        {
            source = initialSource;
        }

        internal void ReplaceSource(byte[] nextSource, Action assignSerializedSource)
        {
            if (nextSource == null)
            {
                throw new ArgumentNullException(nameof(nextSource));
            }

            if (assignSerializedSource == null)
            {
                throw new ArgumentNullException(nameof(assignSerializedSource));
            }

            lock (gate)
            {
                assignSerializedSource();
                source = nextSource;
                modelCacheSource = null;
                modelCache = null;
                preloadSource = null;
                preloadTask = null;
                descriptorCache = null;
                descriptorTask = null;
                descriptorModelPreloadGeneration = -1;
            }
        }

        internal MmdModelDefinition LoadValidatedModel(
            IMmdParser? parser,
            out bool cacheHit)
        {
            while (true)
            {
                byte[] currentSource;
                Task<MmdModelDefinition>? currentPreload;
                lock (gate)
                {
                    if (source.Length == 0)
                    {
                        throw new InvalidOperationException("PMX asset has no imported bytes.");
                    }

                    if (ReferenceEquals(modelCacheSource, source) && modelCache != null)
                    {
                        cacheHit = true;
                        return modelCache;
                    }

                    currentSource = source;
                    currentPreload = ReferenceEquals(preloadSource, currentSource) &&
                                      preloadTask != null &&
                                      !preloadTask.IsFaulted &&
                                      !preloadTask.IsCanceled
                        ? preloadTask
                        : null;
                    if (currentPreload == null)
                    {
                        parser ??= new NativeMmdParser();
                        currentPreload = StartPreloadLocked(currentSource, parser);
                    }
                }

                MmdModelDefinition model = currentPreload.GetAwaiter().GetResult();
                lock (gate)
                {
                    if (!ReferenceEquals(source, currentSource))
                    {
                        continue;
                    }

                    modelCacheSource = currentSource;
                    modelCache = model;
                    cacheHit = false;
                    return model;
                }
            }
        }

        internal Task BeginPreload(MmdMaterialPreset preset = MmdMaterialPreset.MmdToon)
        {
            lock (gate)
            {
                if (source.Length == 0)
                {
                    return Task.CompletedTask;
                }

                Task<MmdModelDefinition> currentModelTask;
                if (ReferenceEquals(modelCacheSource, source) && modelCache != null)
                {
                    currentModelTask = Task.FromResult(modelCache);
                }
                else
                {
                    currentModelTask = GetOrStartPreloadLocked(source, new NativeMmdParser());
                }

                if (ReferenceEquals(modelCacheSource, source) &&
                    descriptorCache != null &&
                    descriptorPreset == preset)
                {
                    return Task.CompletedTask;
                }

                if (ReferenceEquals(preloadSource, source) &&
                    descriptorTask != null &&
                    !descriptorTask.IsFaulted &&
                    !descriptorTask.IsCanceled &&
                    descriptorModelPreloadGeneration == modelPreloadGeneration &&
                    descriptorPreset == preset)
                {
                    return descriptorTask;
                }

                byte[] currentSource = source;
                descriptorPreset = preset;
                descriptorModelPreloadGeneration = modelPreloadGeneration;
                descriptorTask = currentModelTask.ContinueWith(
                    completed =>
                    {
                        MmdRenderingDescriptor descriptor =
                            MmdUnityModelFactory.BuildRuntimePlaybackRenderingDescriptor(
                                completed.GetAwaiter().GetResult(),
                                preset);
                        lock (gate)
                        {
                            if (ReferenceEquals(source, currentSource) && descriptorPreset == preset)
                            {
                                descriptorCache = descriptor;
                            }
                        }

                        return descriptor;
                    },
                    TaskScheduler.Default);
                return descriptorTask;
            }
        }

        internal MmdRenderingDescriptor LoadDescriptor(
            MmdMaterialPreset preset,
            out bool cacheHit)
        {
            lock (gate)
            {
                if (ReferenceEquals(modelCacheSource, source) &&
                    descriptorCache != null &&
                    descriptorPreset == preset)
                {
                    cacheHit = true;
                    return descriptorCache;
                }
            }

            MmdRenderingDescriptor? completedDescriptor =
                (BeginPreload(preset) as Task<MmdRenderingDescriptor>)
                ?.GetAwaiter().GetResult();
            lock (gate)
            {
                MmdRenderingDescriptor? descriptor = descriptorCache ?? completedDescriptor;
                if (descriptor == null || descriptorPreset != preset)
                {
                    throw new InvalidOperationException("PMX playback descriptor preload did not complete.");
                }

                cacheHit = false;
                return descriptor;
            }
        }

        private Task<MmdModelDefinition> StartPreloadLocked(
            byte[] currentSource,
            IMmdParser parser)
        {
            modelPreloadGeneration++;
            preloadSource = currentSource;
            preloadTask = Task.Run(() =>
            {
                MmdModelDefinition model = parser.LoadModel(currentSource);
                MmdModelValidator.ThrowIfInvalid(model);
                lock (gate)
                {
                    if (ReferenceEquals(source, currentSource))
                    {
                        modelCacheSource = currentSource;
                        modelCache = model;
                    }
                }

                return model;
            });
            return preloadTask;
        }

        private Task<MmdModelDefinition> GetOrStartPreloadLocked(
            byte[] currentSource,
            IMmdParser parser)
        {
            if (ReferenceEquals(preloadSource, currentSource) &&
                preloadTask != null &&
                !preloadTask.IsFaulted &&
                !preloadTask.IsCanceled)
            {
                return preloadTask;
            }

            return StartPreloadLocked(currentSource, parser);
        }
    }
}
