#nullable enable

using System;
using Mmd.Parser;
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
