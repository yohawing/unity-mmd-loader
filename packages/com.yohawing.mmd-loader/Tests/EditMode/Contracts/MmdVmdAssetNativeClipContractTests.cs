#nullable enable

using System;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using Mmd.Native;
using Mmd.Parser;

namespace Mmd.Tests
{
    [TestFixture]
    public sealed class MmdVmdAssetNativeClipContractTests
    {
        [Test]
        public void NativeSummaryMapsKeyCountsIntoTheExistingPublicDto()
        {
            byte[] sourceBytes = MmdTestFixtures.ReadFixtureAssetBytes("test_1bone_cube_motion.vmd");
            MmdVmdParseSummary summary = MmdVmdNativeSummaryAdapter.Read(sourceBytes);
            MmdMotionDefinition parsed = new NativeMmdParser().LoadMotion(sourceBytes);
            int constraintStateCount = 0;
            foreach (MmdModelKeyframeDefinition keyframe in parsed.modelKeyframes)
            {
                constraintStateCount += keyframe.constraintStates.Count;
            }

            Assert.That(summary.TargetModelName, Is.EqualTo(parsed.targetModelName));
            Assert.That(summary.MaxFrame, Is.EqualTo(parsed.maxFrame));
            Assert.That(summary.BoneKeyframeCount, Is.EqualTo(parsed.boneKeyframes.Count));
            Assert.That(summary.MorphKeyframeCount, Is.EqualTo(parsed.morphKeyframes.Count));
            Assert.That(summary.ModelKeyframeCount, Is.EqualTo(parsed.modelKeyframes.Count));
            Assert.That(summary.ConstraintStateCount, Is.EqualTo(constraintStateCount));
            Assert.That(summary.CameraKeyframeCount, Is.EqualTo(parsed.cameraKeyframes.Count));
            Assert.That(summary.LightKeyframeCount, Is.EqualTo(parsed.lightKeyframes.Count));
            Assert.That(summary.SelfShadowKeyframeCount, Is.EqualTo(parsed.selfShadowKeyframes.Count));
        }

        [Test]
        public void NativeSummaryDecodesCp932ModelNameWithNulPadding()
        {
            MmdVmdParseSummary summary = MmdVmdNativeSummaryAdapter.Read(
                MmdTestFixtures.BuildSceneTrackVmdBytes("モデル名"));

            Assert.That(summary.TargetModelName, Is.EqualTo("モデル名"));
        }

        [Test]
        public void FailedImportSummaryPreservesBytesButRejectsNativeClipHeader()
        {
            MmdVmdAsset asset = ScriptableObject.CreateInstance<MmdVmdAsset>();
            byte[] sourceBytes = { 0x56, 0x4D, 0x44, 0x00 };

            try
            {
                asset.Initialize(
                    sourceBytes,
                    "invalid.vmd",
                    "Assets/invalid.vmd",
                    new MmdVmdParseSummary("invalid", 0, 0, 0, 0, 0),
                    new[] { "Failed to parse VMD during import: truncated record" });

                Assert.That(asset.ByteLength, Is.EqualTo(sourceBytes.Length));
                Assert.That(asset.GetBytesCopy(), Is.EqualTo(sourceBytes));
                Assert.That(asset.ImportSummaryStatus, Is.EqualTo(MmdVmdImportSummaryStatus.Failed));

                InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                    () => asset.CreateNativeClipMotionHeader())!;

                Assert.That(exception.Message, Does.Contain("Failed to parse VMD during import"));
                Assert.That(exception.Message, Does.Contain("truncated record"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void UnparsedAssetBuildsNativeClipHeaderFromNativeSummary()
        {
            byte[] sourceBytes = MmdTestFixtures.ReadFixtureAssetBytes("test_1bone_cube_motion.vmd");
            MmdVmdAsset asset = ScriptableObject.CreateInstance<MmdVmdAsset>();

            try
            {
                asset.Initialize(sourceBytes, "test_1bone_cube_motion.vmd", "Assets/test_1bone_cube_motion.vmd");

                Assert.That(asset.ImportSummaryStatus, Is.EqualTo(MmdVmdImportSummaryStatus.NotParsed));
                MmdMotionDefinition header = asset.CreateNativeClipMotionHeader();
                MmdMotionDefinition parsed = new NativeMmdParser().LoadMotion(sourceBytes);

                Assert.That(header.maxFrame, Is.EqualTo(parsed.maxFrame));
                Assert.That(header.boneKeyframes, Is.Empty);
                Assert.That(header.morphKeyframes, Is.Empty);
                Assert.That(header.sourceBytes, Is.EqualTo(sourceBytes));
                Assert.That(header.sourceBytes, Is.Not.SameAs(sourceBytes));
                Assert.That(asset.GetBytesCopy(), Is.Not.SameAs(header.sourceBytes));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void StaticNativeClipHeaderClonesLegacySourceBytes()
        {
            byte[] sourceBytes = MmdTestFixtures.ReadFixtureAssetBytes("test_1bone_cube_motion.vmd");
            MmdMotionDefinition header = MmdVmdAsset.CreateNativeClipMotionHeader(
                sourceBytes,
                new MmdVmdParseSummary("test", 49, 6, 0, 0, 0));

            Assert.That(header.sourceBytes, Is.Not.SameAs(sourceBytes));
            Assert.That(header.sourceBytes, Is.EqualTo(sourceBytes));
        }

        [Test]
        public void PassedSummaryWithStructuralDiagnosticsRejectsNativeClipHeader()
        {
            MmdVmdAsset asset = ScriptableObject.CreateInstance<MmdVmdAsset>();
            try
            {
                asset.Initialize(
                    MmdTestFixtures.ReadFixtureAssetBytes("test_1bone_cube_motion.vmd"),
                    "diagnostic.vmd",
                    "Assets/diagnostic.vmd",
                    new MmdVmdParseSummary("model", 1, 1, 0, 0, 0),
                    new[] { "structural: invalid interpolation" });

                Assert.That(asset.ImportSummaryStatus, Is.EqualTo(MmdVmdImportSummaryStatus.Passed));
                InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                    () => asset.CreateNativeClipMotionHeader())!;

                Assert.That(exception.Message, Does.Contain("invalid interpolation"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void NativeVmdContextIsReusedForTheSameAssetSourceIdentity()
        {
            byte[] sourceBytes = MmdTestFixtures.ReadFixtureAssetBytes("test_1bone_cube_motion.vmd");
            MmdVmdAsset asset = ScriptableObject.CreateInstance<MmdVmdAsset>();

            try
            {
                asset.Initialize(
                    sourceBytes,
                    "test_1bone_cube_motion.vmd",
                    "Assets/test_1bone_cube_motion.vmd",
                    new MmdVmdParseSummary("test", 49, 6, 0, 0, 0));

                if (!asset.TryGetOrCreateNativeVmdContext(out var first, out string firstReason))
                {
                    Assert.Ignore("Shared VMD context is unavailable: " + firstReason);
                }

                Assert.That(first, Is.Not.Null);
                Assert.That(
                    asset.TryGetOrCreateNativeVmdContext(out var second, out string secondReason),
                    Is.True,
                    secondReason);
                Assert.That(second, Is.SameAs(first));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void AssetRetainsNativeVmdContextWhenCleanupMustBeRetried()
        {
            int freeCount = 0;
            bool failFirstFree = true;
            var context = new MmdRuntimeFfiVmdContext(
                new IntPtr(1),
                _ =>
                {
                    freeCount++;
                    if (failFirstFree)
                    {
                        failFirstFree = false;
                        throw new InvalidOperationException("transient native cleanup failure");
                    }
                });
            MmdVmdAsset asset = ScriptableObject.CreateInstance<MmdVmdAsset>();
            MmdVmdNativeContextCache cache = new MmdVmdNativeContextCache();
            FieldInfo cacheField = typeof(MmdVmdAsset).GetField(
                "nativeVmdContextCache",
                BindingFlags.Instance | BindingFlags.NonPublic)!;
            cacheField.SetValue(asset, cache);
            FieldInfo contextField = typeof(MmdVmdNativeContextCache).GetField(
                "nativeVmdContext",
                BindingFlags.Instance | BindingFlags.NonPublic)!;
            MethodInfo dispose = typeof(MmdVmdAsset).GetMethod(
                "DisposeNativeVmdContext",
                BindingFlags.Instance | BindingFlags.NonPublic)!;
            try
            {
                contextField.SetValue(cache, context);

                TargetInvocationException firstFailure = Assert.Throws<TargetInvocationException>(
                    () => dispose.Invoke(asset, Array.Empty<object>()))!;
                Assert.That(firstFailure.InnerException, Is.TypeOf<InvalidOperationException>());
                Assert.That(contextField.GetValue(cache), Is.SameAs(context));

                dispose.Invoke(asset, Array.Empty<object>());

                Assert.That(freeCount, Is.EqualTo(2));
                Assert.That(contextField.GetValue(cache), Is.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void FailedNativeVmdPreloadCanRetryForTheSameSource()
        {
            byte[] sourceBytes = MmdTestFixtures.ReadFixtureAssetBytes("test_1bone_cube_motion.vmd");
            int attempts = 0;
            MmdVmdAsset asset = ScriptableObject.CreateInstance<MmdVmdAsset>();

            try
            {
                asset.Initialize(
                    sourceBytes,
                    "test_1bone_cube_motion.vmd",
                    "Assets/test_1bone_cube_motion.vmd",
                    new MmdVmdParseSummary("test", 49, 6, 0, 0, 0));
                MmdVmdAsset.NativeVmdContextFailureReasonOverrideForTests = _ =>
                {
                    attempts++;
                    return "forced VMD preload failure " + attempts;
                };

                Task firstTask = asset.BeginNativePlaybackPreload();
                Exception firstFailure = Assert.Catch<Exception>(
                    () => firstTask.GetAwaiter().GetResult())!;
                Assert.That(firstFailure.Message, Does.Contain("forced VMD preload failure 1"));

                Task retryTask = asset.BeginNativePlaybackPreload();
                Exception retryFailure = Assert.Catch<Exception>(
                    () => retryTask.GetAwaiter().GetResult())!;
                Assert.That(retryTask, Is.Not.SameAs(firstTask));
                Assert.That(retryFailure.Message, Does.Contain("forced VMD preload failure 2"));
                Assert.That(attempts, Is.EqualTo(2));
            }
            finally
            {
                MmdVmdAsset.NativeVmdContextFailureReasonOverrideForTests = null;
                UnityEngine.Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void NativeVmdPreloadSharesInFlightFactoryForTheSameSource()
        {
            byte[] sourceBytes = { 0x56, 0x4D, 0x44, 0x00 };
            int attempts = 0;
            int freeCount = 0;
            var pending = new TaskCompletionSource<MmdRuntimeFfiVmdContext>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var context = new MmdRuntimeFfiVmdContext(
                new IntPtr(1),
                _ => freeCount++);
            var cache = new MmdVmdNativeContextCache(
                _ =>
                {
                    attempts++;
                    return pending.Task;
                });
            MmdVmdNativeContextCache.SourceSnapshot sourceSnapshot =
                cache.ReadSourceSnapshot(sourceBytes, rawSource: null);

            try
            {
                Task first = cache.BeginNativePlaybackPreload(sourceSnapshot, failureOverrideForTests: null);
                Task second = cache.BeginNativePlaybackPreload(sourceSnapshot, failureOverrideForTests: null);

                Assert.That(second, Is.SameAs(first));
                Assert.That(attempts, Is.EqualTo(1));
                Assert.That(pending.TrySetResult(context), Is.True);
                Assert.DoesNotThrow(() => first.GetAwaiter().GetResult());
            }
            finally
            {
                pending.TrySetCanceled();
                cache.Dispose();
            }

            Assert.That(freeCount, Is.EqualTo(1));
        }

        [Test]
        public void CanceledNativeVmdPreloadStartsANewFactoryAttemptForTheSameSource()
        {
            byte[] sourceBytes = { 0x56, 0x4D, 0x44, 0x00 };
            int attempts = 0;
            int freeCount = 0;
            var firstPending = new TaskCompletionSource<MmdRuntimeFfiVmdContext>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var retryPending = new TaskCompletionSource<MmdRuntimeFfiVmdContext>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var context = new MmdRuntimeFfiVmdContext(
                new IntPtr(1),
                _ => freeCount++);
            var cache = new MmdVmdNativeContextCache(
                _ =>
                {
                    attempts++;
                    return attempts == 1 ? firstPending.Task : retryPending.Task;
                });
            MmdVmdNativeContextCache.SourceSnapshot sourceSnapshot =
                cache.ReadSourceSnapshot(sourceBytes, rawSource: null);

            try
            {
                Task first = cache.BeginNativePlaybackPreload(sourceSnapshot, failureOverrideForTests: null);
                Assert.That(attempts, Is.EqualTo(1));
                Assert.That(firstPending.TrySetCanceled(), Is.True);
                Assert.Catch<OperationCanceledException>(
                    () => first.GetAwaiter().GetResult());

                Task retry = cache.BeginNativePlaybackPreload(sourceSnapshot, failureOverrideForTests: null);

                Assert.That(retry, Is.Not.SameAs(first));
                Assert.That(attempts, Is.EqualTo(2));
                Assert.That(retryPending.TrySetResult(context), Is.True);
                Assert.DoesNotThrow(() => retry.GetAwaiter().GetResult());
            }
            finally
            {
                firstPending.TrySetCanceled();
                retryPending.TrySetCanceled();
                cache.Dispose();
            }

            Assert.That(freeCount, Is.EqualTo(1));
        }

        [Test]
        public void StaleVmdSourceSnapshotIsRejectedWithoutStartingFactory()
        {
            byte[] sourceBytes = { 0x56, 0x4D, 0x44, 0x00 };
            int attempts = 0;
            var cache = new MmdVmdNativeContextCache(
                _ =>
                {
                    attempts++;
                    throw new AssertionException("A stale source snapshot must not start the factory.");
                });

            try
            {
                MmdVmdNativeContextCache.SourceSnapshot snapshot =
                    cache.ReadSourceSnapshot(sourceBytes, rawSource: null);
                cache.Dispose();

                Assert.That(
                    cache.TryGetOrCreateNativeVmdContext(
                        snapshot,
                        failureOverrideForTests: null,
                        out MmdRuntimeFfiVmdContext? context,
                        out string reason),
                    Is.False);
                Assert.That(context, Is.Null);
                Assert.That(reason, Is.EqualTo(MmdVmdNativeContextCache.StaleSourceSnapshotReason));

                Task staleTask = cache.BeginNativePlaybackPreload(
                    snapshot,
                    failureOverrideForTests: null);

                Assert.That(staleTask.IsFaulted, Is.True);
                Exception staleFailure = Assert.Catch<Exception>(
                    () => staleTask.GetAwaiter().GetResult())!;
                Assert.That(staleFailure.Message, Is.EqualTo(MmdVmdNativeContextCache.StaleSourceSnapshotReason));
                Assert.That(attempts, Is.EqualTo(0));
            }
            finally
            {
                cache.Dispose();
            }
        }

        [Test]
        public void RawVmdSourceReplacementAdvancesSnapshotGeneration()
        {
            var cache = new MmdVmdNativeContextCache();
            TextAsset? firstRawSource = null;
            TextAsset? secondRawSource = null;
            try
            {
                firstRawSource = new TextAsset("raw-source-a");
                secondRawSource = new TextAsset("raw-source-b");

                MmdVmdNativeContextCache.SourceSnapshot first =
                    cache.ReadSourceSnapshot(Array.Empty<byte>(), firstRawSource);
                MmdVmdNativeContextCache.SourceSnapshot second =
                    cache.ReadSourceSnapshot(Array.Empty<byte>(), secondRawSource);

                Assert.That(second.Generation, Is.GreaterThan(first.Generation));
            }
            finally
            {
                cache.Dispose();
                if (firstRawSource != null)
                {
                    UnityEngine.Object.DestroyImmediate(firstRawSource);
                }

                if (secondRawSource != null)
                {
                    UnityEngine.Object.DestroyImmediate(secondRawSource);
                }
            }
        }

        [Test]
        public void CompletedVmdPreloadIsDisposedOnceWhenRawSourceIsReplaced()
        {
            int freeCount = 0;
            var context = new MmdRuntimeFfiVmdContext(
                new IntPtr(1),
                _ => freeCount++);
            var cache = new MmdVmdNativeContextCache(
                _ => Task.FromResult(context));
            TextAsset? firstRawSource = null;
            TextAsset? secondRawSource = null;

            try
            {
                firstRawSource = new TextAsset("raw-source-a");
                secondRawSource = new TextAsset("raw-source-b");
                MmdVmdNativeContextCache.SourceSnapshot first =
                    cache.ReadSourceSnapshot(Array.Empty<byte>(), firstRawSource);

                Task preload = cache.BeginNativePlaybackPreload(
                    first,
                    failureOverrideForTests: null);
                Assert.DoesNotThrow(() => preload.GetAwaiter().GetResult());

                MmdVmdNativeContextCache.SourceSnapshot second =
                    cache.ReadSourceSnapshot(Array.Empty<byte>(), secondRawSource);

                Assert.That(second.Generation, Is.GreaterThan(first.Generation));
                Assert.That(freeCount, Is.EqualTo(1));
            }
            finally
            {
                cache.Dispose();
                if (firstRawSource != null)
                {
                    UnityEngine.Object.DestroyImmediate(firstRawSource);
                }

                if (secondRawSource != null)
                {
                    UnityEngine.Object.DestroyImmediate(secondRawSource);
                }
            }

            Assert.That(freeCount, Is.EqualTo(1));
        }

        [Test]
        public void RawSourceToSerializedBytesTransitionDisposesContextOnceAndReturnsSerializedSource()
        {
            int freeCount = 0;
            var context = new MmdRuntimeFfiVmdContext(
                new IntPtr(1),
                _ => freeCount++);
            var cache = new MmdVmdNativeContextCache();
            TextAsset? rawSource = null;
            byte[] serializedBytes = { 0x56, 0x4D, 0x44, 0x00 };
            FieldInfo contextField = typeof(MmdVmdNativeContextCache).GetField(
                "nativeVmdContext",
                BindingFlags.Instance | BindingFlags.NonPublic)!;
            FieldInfo contextSourceField = typeof(MmdVmdNativeContextCache).GetField(
                "nativeVmdContextSource",
                BindingFlags.Instance | BindingFlags.NonPublic)!;
            FieldInfo readbackAssetField = typeof(MmdVmdNativeContextCache).GetField(
                "sourceReadbackAsset",
                BindingFlags.Instance | BindingFlags.NonPublic)!;
            FieldInfo readbackField = typeof(MmdVmdNativeContextCache).GetField(
                "sourceReadback",
                BindingFlags.Instance | BindingFlags.NonPublic)!;

            try
            {
                rawSource = new TextAsset("raw-source");
                byte[] rawBytes = cache.ReadSourceBytes(serializedBytes, rawSource);
                contextField.SetValue(cache, context);
                contextSourceField.SetValue(cache, rawBytes);
                UnityEngine.Object.DestroyImmediate(rawSource);
                rawSource = null;

                byte[] result = cache.ReadSourceBytes(serializedBytes, rawSource: null);

                Assert.That(result, Is.SameAs(serializedBytes));
                Assert.That(freeCount, Is.EqualTo(1));
                Assert.That(readbackAssetField.GetValue(cache), Is.Null);
                Assert.That(readbackField.GetValue(cache), Is.Null);

                cache.Dispose();
                Assert.That(freeCount, Is.EqualTo(1));
            }
            finally
            {
                cache.Dispose();
                if (rawSource != null)
                {
                    UnityEngine.Object.DestroyImmediate(rawSource);
                }
            }
        }
    }
}
