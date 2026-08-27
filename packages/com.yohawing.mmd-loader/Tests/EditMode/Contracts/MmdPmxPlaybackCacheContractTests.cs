#nullable enable

using System;
using System.Threading.Tasks;
using Mmd.Parser;
using Mmd.Rendering;
using NUnit.Framework;

namespace Mmd.Tests
{
    public sealed class MmdPmxPlaybackCacheContractTests
    {
        [Test]
        public void FailedModelPreloadCanRetryForSameSource()
        {
            int loadCount = 0;
            var parser = new RetryParser(() =>
            {
                loadCount++;
                if (loadCount == 1)
                {
                    throw new InvalidOperationException("first PMX preload failure");
                }

                return new MmdModelDefinition();
            });
            byte[] source = { 0x01, 0x02, 0x03 };
            var cache = new MmdPmxPlaybackCache(source);

            InvalidOperationException firstFailure = Assert.Throws<InvalidOperationException>(
                () => cache.LoadValidatedModel(parser, out _))!;

            Assert.That(firstFailure.Message, Is.EqualTo("first PMX preload failure"));

            MmdModelDefinition model = cache.LoadValidatedModel(parser, out bool cacheHit);

            Assert.That(loadCount, Is.EqualTo(2));
            Assert.That(cacheHit, Is.False);
            Assert.That(cache.LoadValidatedModel(parser, out cacheHit), Is.SameAs(model));
            Assert.That(cacheHit, Is.True);
        }

        [Test]
        public void DescriptorLoadRetriesAfterSourceReplacementDuringPreload()
        {
            byte[] firstSource = { 0x01 };
            byte[] secondSource = { 0x02 };
            var firstStarted = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var secondStarted = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var firstRelease = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var secondRelease = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            MmdModelDefinition firstModel = CreateModelWithIkCount(1);
            MmdModelDefinition secondModel = CreateModelWithIkCount(2);
            var parser = new SourceBlockingParser(
                firstSource,
                secondSource,
                firstStarted,
                secondStarted,
                firstRelease,
                secondRelease,
                firstModel,
                secondModel);
            var cache = new MmdPmxPlaybackCache(firstSource, () => parser);
            MmdRenderingDescriptor? descriptor = null;
            bool cacheHit = true;
            Task load = Task.Run(() =>
            {
                descriptor = cache.LoadDescriptor(MmdMaterialPreset.MmdToon, out cacheHit);
            });

            try
            {
                Assert.That(firstStarted.Task.Wait(TimeSpan.FromSeconds(5)), Is.True);

                cache.ReplaceSource(secondSource, () => { });
                Assert.That(firstRelease.TrySetResult(true), Is.True);
                Assert.That(secondStarted.Task.Wait(TimeSpan.FromSeconds(5)), Is.True);
                Assert.That(secondRelease.TrySetResult(true), Is.True);
                Assert.That(load.Wait(TimeSpan.FromSeconds(5)), Is.True);
                load.GetAwaiter().GetResult();

                Assert.That(descriptor, Is.Not.Null);
                Assert.That(descriptor!.ikCount, Is.EqualTo(2));
                Assert.That(cacheHit, Is.False);
            }
            finally
            {
                firstRelease.TrySetResult(true);
                secondRelease.TrySetResult(true);
                if (!load.IsCompleted)
                {
                    load.Wait(TimeSpan.FromSeconds(5));
                }
            }
        }

        [Test]
        public void AdvisoryDescriptorPreloadFailurePropagatesAndRetriesForSameSource()
        {
            int loadCount = 0;
            var failure = new InvalidOperationException("advisory PMX descriptor preload failure");
            var parser = new RetryParser(() =>
            {
                loadCount++;
                if (loadCount <= 2)
                {
                    throw failure;
                }

                return CreateModelWithIkCount(1);
            });
            byte[] source = { 0x01, 0x02, 0x03 };
            var observed = new TaskCompletionSource<TaskStatus>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var cache = new MmdPmxPlaybackCache(
                source,
                () => parser,
                status => observed.TrySetResult(status));

            Task advisoryPreload = cache.BeginPreload();

            Assert.That(
                observed.Task.Wait(TimeSpan.FromSeconds(5)),
                Is.True);
            Assert.That(advisoryPreload.IsFaulted, Is.True);
            Assert.That(observed.Task.GetAwaiter().GetResult(), Is.EqualTo(TaskStatus.Faulted));

            InvalidOperationException propagatedFailure = Assert.Throws<InvalidOperationException>(
                () => cache.LoadDescriptor(MmdMaterialPreset.MmdToon, out _))!;

            Assert.That(propagatedFailure, Is.SameAs(failure));

            MmdRenderingDescriptor descriptor =
                cache.LoadDescriptor(MmdMaterialPreset.MmdToon, out bool cacheHit);

            Assert.That(descriptor, Is.Not.Null);
            Assert.That(cacheHit, Is.False);
            Assert.That(loadCount, Is.EqualTo(3));
        }

        private static MmdModelDefinition CreateModelWithIkCount(int ikCount)
        {
            var model = new MmdModelDefinition();
            model.bones.Add(new MmdBoneDefinition
            {
                index = 0,
                name = "root",
                origin = new[] { 0.0f, 0.0f, 0.0f }
            });
            for (int i = 0; i < ikCount; i++)
            {
                model.ik.Add(new MmdIkDefinition
                {
                    boneIndex = 0,
                    targetBoneIndex = 0,
                    iterationCount = 1,
                    links = new System.Collections.Generic.List<MmdIkLinkDefinition>()
                });
            }

            return model;
        }

        private sealed class SourceBlockingParser : IMmdParser
        {
            private readonly byte[] firstSource;
            private readonly byte[] secondSource;
            private readonly TaskCompletionSource<bool> firstStarted;
            private readonly TaskCompletionSource<bool> secondStarted;
            private readonly TaskCompletionSource<bool> firstRelease;
            private readonly TaskCompletionSource<bool> secondRelease;
            private readonly MmdModelDefinition firstModel;
            private readonly MmdModelDefinition secondModel;

            internal SourceBlockingParser(
                byte[] firstSource,
                byte[] secondSource,
                TaskCompletionSource<bool> firstStarted,
                TaskCompletionSource<bool> secondStarted,
                TaskCompletionSource<bool> firstRelease,
                TaskCompletionSource<bool> secondRelease,
                MmdModelDefinition firstModel,
                MmdModelDefinition secondModel)
            {
                this.firstSource = firstSource;
                this.secondSource = secondSource;
                this.firstStarted = firstStarted;
                this.secondStarted = secondStarted;
                this.firstRelease = firstRelease;
                this.secondRelease = secondRelease;
                this.firstModel = firstModel;
                this.secondModel = secondModel;
            }

            public MmdModelDefinition LoadModel(ReadOnlySpan<byte> data)
            {
                if (data[0] == firstSource[0])
                {
                    firstStarted.TrySetResult(true);
                    firstRelease.Task.GetAwaiter().GetResult();
                    return firstModel;
                }

                if (data[0] == secondSource[0])
                {
                    secondStarted.TrySetResult(true);
                    secondRelease.Task.GetAwaiter().GetResult();
                    return secondModel;
                }

                throw new InvalidOperationException("Unexpected PMX test source.");
            }

            public MmdMotionDefinition LoadMotion(ReadOnlySpan<byte> data)
            {
                throw new NotSupportedException();
            }
        }

        private sealed class RetryParser : IMmdParser
        {
            private readonly Func<MmdModelDefinition> loadModel;

            internal RetryParser(Func<MmdModelDefinition> loadModel)
            {
                this.loadModel = loadModel;
            }

            public MmdModelDefinition LoadModel(ReadOnlySpan<byte> data)
            {
                return loadModel();
            }

            public MmdMotionDefinition LoadMotion(ReadOnlySpan<byte> data)
            {
                throw new NotSupportedException();
            }
        }
    }
}
