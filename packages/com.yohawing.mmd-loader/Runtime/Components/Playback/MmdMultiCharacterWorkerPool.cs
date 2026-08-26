#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using Mmd.Native;

namespace Mmd.UnityIntegration
{
    /// <summary>
    /// A fixed set of long-lived workers. Each evaluator, its native session, and its output buffers
    /// belong to exactly one worker for the whole lifetime of the pool.
    /// </summary>
    internal sealed class MmdMultiCharacterWorkerPool : IDisposable
    {
        internal interface IEvaluator : IDisposable
        {
            MmdMultiCharacterWorkerResult Initialize();

            void Evaluate(int frame, float time, MmdMultiCharacterWorkerResult result);
        }

        private sealed class Worker
        {
            private readonly IEvaluator evaluator;
            private readonly AutoResetEvent request = new(false);
            private readonly ManualResetEventSlim completed = new(false);
            private readonly ManualResetEventSlim initialized = new(false);
            private readonly Thread thread;
            private readonly object stateLock = new();
            private bool stopRequested;
            private Exception? initializationError;
            private Exception? evaluationError;
            private Exception? shutdownError;
            private int requestedFrame;
            private float requestedTime;
            private MmdMultiCharacterWorkerResult? result;

            internal Worker(IEvaluator evaluator, int index)
            {
                this.evaluator = evaluator ?? throw new ArgumentNullException(nameof(evaluator));
                thread = new Thread(Run)
                {
                    IsBackground = true,
                    Name = $"MmdMultiCharacterWorker-{index}"
                };
                try
                {
                    thread.Start();
                }
                catch (Exception startError)
                {
                    Exception? cleanupError = null;
                    try
                    {
                        evaluator.Dispose();
                    }
                    catch (Exception exception)
                    {
                        cleanupError = exception;
                    }
                    finally
                    {
                        request.Dispose();
                        completed.Dispose();
                        initialized.Dispose();
                    }

                    if (cleanupError != null)
                    {
                        throw new AggregateException(
                            "Multi-character worker start and cleanup failed.",
                            startError,
                            cleanupError);
                    }

                    throw;
                }

                initialized.Wait();
                if (initializationError != null)
                {
                    Exception error = initializationError;
                    try
                    {
                        Stop();
                    }
                    catch (Exception cleanupError)
                    {
                        throw new AggregateException(
                            "Multi-character worker initialization and cleanup failed.",
                            error,
                            cleanupError);
                    }
                    throw new InvalidOperationException(
                        "Multi-character worker initialization failed.", error);
                }
            }

            internal int ManagedThreadId => thread.ManagedThreadId;

            internal void Prepare(int frame, float time)
            {
                lock (stateLock)
                {
                    if (stopRequested)
                    {
                        throw new ObjectDisposedException(nameof(MmdMultiCharacterWorkerPool));
                    }

                    evaluationError = null;
                    requestedFrame = frame;
                    requestedTime = time;
                    completed.Reset();
                }

                request.Set();
            }

            internal void WaitForCompletion()
            {
                completed.Wait();
                Exception? error;
                lock (stateLock)
                {
                    error = evaluationError;
                }

                if (error != null)
                {
                    throw new InvalidOperationException(
                        "Multi-character worker evaluation failed.",
                        error);
                }
            }

            internal MmdMultiCharacterWorkerResult GetResult()
            {
                lock (stateLock)
                {
                    return result ?? throw new InvalidOperationException(
                        "Multi-character worker has no initialized result.");
                }
            }

            internal void Stop()
            {
                lock (stateLock)
                {
                    stopRequested = true;
                }

                request.Set();
                thread.Join();
                Exception? error;
                lock (stateLock)
                {
                    error = shutdownError;
                }

                request.Dispose();
                completed.Dispose();
                initialized.Dispose();
                if (error != null)
                {
                    throw new InvalidOperationException(
                        "Multi-character worker cleanup failed.",
                        error);
                }
            }

            private void Run()
            {
                try
                {
                    MmdMultiCharacterWorkerResult initializedResult = evaluator.Initialize();
                    lock (stateLock)
                    {
                        result = initializedResult;
                    }
                }
                catch (Exception exception)
                {
                    lock (stateLock)
                    {
                        initializationError = exception;
                    }
                }
                finally
                {
                    initialized.Set();
                }

                if (initializationError != null)
                {
                    DisposeEvaluatorOnWorker();
                    return;
                }

                try
                {
                    while (true)
                    {
                        request.WaitOne();
                        int frame;
                        float time;
                        bool stop;
                        lock (stateLock)
                        {
                            stop = stopRequested;
                            frame = requestedFrame;
                            time = requestedTime;
                        }

                        if (stop)
                        {
                            return;
                        }

                        try
                        {
                            evaluator.Evaluate(frame, time, result!);
                        }
                        catch (Exception exception)
                        {
                            lock (stateLock)
                            {
                                evaluationError = exception;
                            }
                        }
                        finally
                        {
                            completed.Set();
                        }
                    }
                }
                finally
                {
                    DisposeEvaluatorOnWorker();
                }
            }

            private void DisposeEvaluatorOnWorker()
            {
                try
                {
                    evaluator.Dispose();
                }
                catch (Exception exception)
                {
                    lock (stateLock)
                    {
                        shutdownError = exception;
                    }
                }
            }
        }

        private readonly Worker[] workers;
        private readonly object lifecycleLock = new();
        private int evaluationInFlight;
        private bool disposed;

        internal MmdMultiCharacterWorkerPool(IReadOnlyList<IEvaluator> evaluators)
        {
            if (evaluators == null)
            {
                throw new ArgumentNullException(nameof(evaluators));
            }

            if (evaluators.Count < 2)
            {
                throw new ArgumentException(
                    "At least two evaluators are required for multi-character playback.",
                    nameof(evaluators));
            }

            var started = new List<Worker>(evaluators.Count);
            for (int i = 0; i < evaluators.Count; i++)
            {
                try
                {
                    started.Add(new Worker(evaluators[i], i));
                }
                catch (Exception constructionError)
                {
                    Exception? cleanupError = null;
                    for (int startedIndex = started.Count - 1; startedIndex >= 0; startedIndex--)
                    {
                        try
                        {
                            started[startedIndex].Stop();
                        }
                        catch (Exception exception)
                        {
                            cleanupError ??= exception;
                        }
                    }

                    // The evaluator at i either disposed itself during Worker construction
                    // (initialization failure) or was disposed by the constructor's start guard.
                    // Only evaluators that were never handed to a worker remain here.
                    for (int remaining = i + 1; remaining < evaluators.Count; remaining++)
                    {
                        try
                        {
                            evaluators[remaining].Dispose();
                        }
                        catch (Exception exception)
                        {
                            cleanupError ??= exception;
                        }
                    }

                    if (cleanupError != null)
                    {
                        throw new AggregateException(
                            "Multi-character worker construction and cleanup failed.",
                            constructionError,
                            cleanupError);
                    }

                    throw;
                }
            }

            workers = started.ToArray();
        }

        internal int[] WorkerManagedThreadIds
        {
            get
            {
                var ids = new int[workers.Length];
                for (int i = 0; i < workers.Length; i++)
                {
                    ids[i] = workers[i].ManagedThreadId;
                }

                return ids;
            }
        }

        internal void Evaluate(int frame, float frameRate)
        {
            if (frame < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(frame));
            }

            if (!float.IsFinite(frameRate) || frameRate <= 0.0f)
            {
                throw new ArgumentOutOfRangeException(nameof(frameRate));
            }

            if (Interlocked.Exchange(ref evaluationInFlight, 1) != 0)
            {
                throw new InvalidOperationException(
                    "Multi-character worker evaluation cannot overlap for one pool.");
            }

            try
            {
                lock (lifecycleLock)
                {
                    if (disposed)
                    {
                        throw new ObjectDisposedException(nameof(MmdMultiCharacterWorkerPool));
                    }

                    float time = frame / frameRate;
                    for (int i = 0; i < workers.Length; i++)
                    {
                        workers[i].Prepare(frame, time);
                    }

                    Exception? firstError = null;
                    for (int i = 0; i < workers.Length; i++)
                    {
                        try
                        {
                            workers[i].WaitForCompletion();
                        }
                        catch (Exception exception)
                        {
                            firstError ??= exception;
                        }
                    }

                    if (firstError != null)
                    {
                        throw firstError;
                    }
                }
            }
            finally
            {
                Volatile.Write(ref evaluationInFlight, 0);
            }
        }

        internal MmdMultiCharacterWorkerResult GetResult(int index)
        {
            lock (lifecycleLock)
            {
                if (disposed)
                {
                    throw new ObjectDisposedException(nameof(MmdMultiCharacterWorkerPool));
                }

                if (index < 0 || index >= workers.Length)
                {
                    throw new ArgumentOutOfRangeException(nameof(index));
                }

                return workers[index].GetResult();
            }
        }

        public void Dispose()
        {
            lock (lifecycleLock)
            {
                if (disposed)
                {
                    return;
                }

                disposed = true;
                Exception? firstError = null;
                for (int i = workers.Length - 1; i >= 0; i--)
                {
                    try
                    {
                        workers[i].Stop();
                    }
                    catch (Exception exception)
                    {
                        firstError ??= exception;
                    }
                }

                if (firstError != null)
                {
                    throw firstError;
                }
            }
        }
    }

    internal sealed class MmdMultiCharacterWorkerResult
    {
        internal MmdMultiCharacterWorkerResult(
            float[] worldMatrices,
            float[] morphWeights,
            byte[] ikEnabled)
        {
            WorldMatrices = worldMatrices ?? throw new ArgumentNullException(nameof(worldMatrices));
            MorphWeights = morphWeights ?? throw new ArgumentNullException(nameof(morphWeights));
            IkEnabled = ikEnabled ?? throw new ArgumentNullException(nameof(ikEnabled));
        }

        internal float[] WorldMatrices { get; }

        internal float[] MorphWeights { get; }

        internal byte[] IkEnabled { get; }
    }

    /// <summary>
    /// Native evaluator owned by a single fixed worker. It does not touch UnityEngine APIs.
    /// </summary>
    internal sealed class MmdNativeMultiCharacterWorker : MmdMultiCharacterWorkerPool.IEvaluator
    {
        private readonly byte[] pmxBytes;
        private readonly byte[] vmdBytes;
        private readonly uint ikMaxIterationsCap;
        private MmdRuntimeFfiPlaybackSession? session;

        internal MmdNativeMultiCharacterWorker(
            byte[] pmxBytes,
            byte[] vmdBytes,
            uint ikMaxIterationsCap)
        {
            if (pmxBytes == null || pmxBytes.Length == 0)
            {
                throw new ArgumentException("PMX bytes are required.", nameof(pmxBytes));
            }

            if (vmdBytes == null || vmdBytes.Length == 0)
            {
                throw new ArgumentException("VMD bytes are required.", nameof(vmdBytes));
            }

            this.pmxBytes = pmxBytes;
            this.vmdBytes = vmdBytes;
            this.ikMaxIterationsCap = ikMaxIterationsCap;
        }

        public MmdMultiCharacterWorkerResult Initialize()
        {
            session = MmdRuntimeFfiPlaybackSession.Create(pmxBytes, vmdBytes);
            return new MmdMultiCharacterWorkerResult(
                new float[session.WorldMatrixFloatCount],
                new float[session.MorphWeightCount],
                new byte[session.IkEnabledCount]);
        }

        public void Evaluate(int frame, float time, MmdMultiCharacterWorkerResult result)
        {
            _ = time;
            session!.EvaluateAndCopy(
                frame,
                result.WorldMatrices,
                result.MorphWeights,
                result.IkEnabled,
                ikMaxIterationsCap);
        }

        public void Dispose()
        {
            session?.Dispose();
            session = null;
        }
    }
}
