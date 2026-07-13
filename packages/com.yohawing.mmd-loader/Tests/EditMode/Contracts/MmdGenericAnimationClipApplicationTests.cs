#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mmd.Editor;
using Mmd.Physics;
using Mmd.UnityIntegration;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace Mmd.Tests
{
    public sealed class MmdGenericAnimationClipApplicationTests
    {
        private const string CubePmx = "Packages/com.yohawing.mmd-loader/Tests/Fixtures/Assets/test_1bone_cube.pmx";
        private const string CubeVmd = "Packages/com.yohawing.mmd-loader/Tests/Fixtures/Assets/test_1bone_cube_motion.vmd";
        private const string MorphPmx = "Packages/com.yohawing.mmd-loader/Tests/Fixtures/Assets/test_vertex_morph.pmx";
        private const string MorphVmd = "Packages/com.yohawing.mmd-loader/Tests/Fixtures/Assets/test_vertex_morph_motion.vmd";
        private const float FrameRate = 30.0f;
        private const float PositionTolerance = 0.0001f;
        private const float RotationTolerance = 0.01f;
        private const float MorphTolerance = 0.001f;

        [TestCase(CubePmx, CubeVmd, 9)]
        [TestCase(MorphPmx, MorphVmd, 10)]
        public void InMemoryClipDrivesFreshHierarchyThroughAnimatorPlayable(
            string pmxPath, string vmdPath, int endFrame)
        {
            CreateAssets(pmxPath, vmdPath, out MmdPmxAsset pmx, out MmdVmdAsset vmd);
            MmdUnityPlaybackBinding? native = null;
            MmdUnityPlaybackBinding? animated = null;
            AnimationClip? clip = null;
            PlayableGraph graph = default;
            try
            {
                clip = MmdGenericAnimationClipWriter.CreateInMemoryClip(pmx, vmd, FrameRate, 0, endFrame).Clip;
                Assert.That(clip, Is.Not.Null);
                native = MmdUnityPlaybackBinding.CreateSkinned(pmx, vmd);
                animated = MmdUnityPlaybackBinding.CreateSkinned(pmx, vmd);
                native.SetPhysicsMode(MmdPhysicsMode.Off);
                animated.SetPhysicsMode(MmdPhysicsMode.Off);

                Animator animator = animated.Instance.Root.AddComponent<Animator>();
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                Assert.That(animator.avatar, Is.Null, "Generic clip application must not require a Humanoid Avatar.");
                graph = PlayableGraph.Create("MmdGenericAnimationClipApplicationTests.Animator");
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                AnimationClipPlayable playable = AnimationClipPlayable.Create(graph, clip!);
                AnimationPlayableOutput output = AnimationPlayableOutput.Create(graph, "Generic Clip", animator);
                output.SetSourcePlayable(playable);
                graph.Play();

                foreach (int frame in FixedFrames(endFrame))
                {
                    native.ApplyFrame(frame, FrameRate);
                    playable.SetTime(frame / FrameRate);
                    graph.Evaluate(0.0f);
                    AssertParity(native.Instance, animated.Instance, frame, pmxPath == MorphPmx);
                }
            }
            finally
            {
                if (graph.IsValid()) graph.Destroy();
                DestroyBinding(native);
                DestroyBinding(animated);
                if (clip != null) UnityEngine.Object.DestroyImmediate(clip);
                DestroyAssets(pmx, vmd);
            }
        }

        [TestCase(CubePmx, CubeVmd, 9)]
        [TestCase(MorphPmx, MorphVmd, 10)]
        public void ReimportedClipDrivesFreshHierarchyThroughStandardTimelineAnimationTrack(
            string pmxPath, string vmdPath, int endFrame)
        {
            string directory = "Assets/MmdGenericClipApplication_" + Guid.NewGuid().ToString("N");
            string clipPath = directory + "/clip.anim";
            CreateAssets(pmxPath, vmdPath, out MmdPmxAsset pmx, out MmdVmdAsset vmd);
            MmdUnityPlaybackBinding? native = null;
            MmdUnityPlaybackBinding? animated = null;
            TimelineAsset? timeline = null;
            GameObject? directorObject = null;
            try
            {
                MmdGenericAnimationClipWriterResult written = MmdGenericAnimationClipWriter.CreateAnimationClipAsset(
                    pmx, vmd, FrameRate, 0, endFrame, clipPath);
                Assert.That(written.Clip, Is.Not.Null, string.Join("\n", written.Diagnostics));
                AssetDatabase.ImportAsset(clipPath, ImportAssetOptions.ForceUpdate);
                AnimationClip? reimported = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
                Assert.That(reimported, Is.Not.Null);

                native = MmdUnityPlaybackBinding.CreateSkinned(pmx, vmd);
                animated = MmdUnityPlaybackBinding.CreateSkinned(pmx, vmd);
                native.SetPhysicsMode(MmdPhysicsMode.Off);
                animated.SetPhysicsMode(MmdPhysicsMode.Off);
                Animator animator = animated.Instance.Root.AddComponent<Animator>();
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                Assert.That(animator.avatar, Is.Null, "Generic Timeline application must not require a Humanoid Avatar.");

                timeline = ScriptableObject.CreateInstance<TimelineAsset>();
                AnimationTrack track = timeline.CreateTrack<AnimationTrack>(null, "Generic Animation");
                TimelineClip timelineClip = track.CreateClip(reimported!);
                timelineClip.start = 0.0;
                timelineClip.duration = (endFrame + 1) / FrameRate;

                directorObject = new GameObject("generic-clip-timeline-director");
                PlayableDirector director = directorObject.AddComponent<PlayableDirector>();
                director.playOnAwake = false;
                director.timeUpdateMode = DirectorUpdateMode.Manual;
                director.playableAsset = timeline;
                director.SetGenericBinding(track, animator);
                director.RebuildGraph();

                foreach (int frame in FixedFrames(endFrame))
                {
                    native.ApplyFrame(frame, FrameRate);
                    director.time = frame / FrameRate;
                    director.Evaluate();
                    AssertParity(native.Instance, animated.Instance, frame, pmxPath == MorphPmx);
                }
            }
            finally
            {
                if (directorObject != null) UnityEngine.Object.DestroyImmediate(directorObject);
                if (timeline != null) UnityEngine.Object.DestroyImmediate(timeline);
                DestroyBinding(native);
                DestroyBinding(animated);
                AssetDatabase.DeleteAsset(directory);
                DestroyAssets(pmx, vmd);
            }
        }

        private static IEnumerable<int> FixedFrames(int endFrame)
        {
            yield return 0;
            yield return endFrame / 2;
            yield return endFrame;
        }

        private static void AssertParity(
            MmdUnityModelInstance expected,
            MmdUnityModelInstance actual,
            int frame,
            bool requireMorph)
        {
            Dictionary<string, Transform> expectedBones = MapBones(expected);
            Dictionary<string, Transform> actualBones = MapBones(actual);
            Assert.That(actualBones.Keys, Is.EquivalentTo(expectedBones.Keys));
            foreach ((string path, Transform expectedBone) in expectedBones)
            {
                Transform actualBone = actualBones[path];
                Assert.That(Vector3.Distance(actualBone.localPosition, expectedBone.localPosition),
                    Is.LessThanOrEqualTo(PositionTolerance), "frame " + frame + " local position: " + path);
                Assert.That(Vector3.Distance(actualBone.position, expectedBone.position),
                    Is.LessThanOrEqualTo(PositionTolerance), "frame " + frame + " world position: " + path);
                Assert.That(Quaternion.Angle(actualBone.localRotation, expectedBone.localRotation),
                    Is.LessThanOrEqualTo(RotationTolerance), "frame " + frame + " local rotation: " + path);
                Assert.That(Quaternion.Angle(actualBone.rotation, expectedBone.rotation),
                    Is.LessThanOrEqualTo(RotationTolerance), "frame " + frame + " world rotation: " + path);
            }

            Assert.That(actual.VertexMorphBlendShapes.Count, Is.EqualTo(expected.VertexMorphBlendShapes.Count));
            Assert.That(expected.SkinnedMeshRenderer, Is.Not.Null);
            Assert.That(actual.SkinnedMeshRenderer, Is.Not.Null);
            if (requireMorph) Assert.That(expected.VertexMorphBlendShapes, Is.Not.Empty);
            foreach (MmdUnityVertexMorphBlendShapeBinding morph in expected.VertexMorphBlendShapes)
            {
                float expectedWeight = expected.SkinnedMeshRenderer!.GetBlendShapeWeight(morph.BlendShapeIndex);
                float actualWeight = actual.SkinnedMeshRenderer!.GetBlendShapeWeight(morph.BlendShapeIndex);
                Assert.That(Mathf.Abs(actualWeight - expectedWeight), Is.LessThanOrEqualTo(MorphTolerance),
                    "frame " + frame + " BlendShape: " + morph.BlendShapeName);
            }
        }

        private static Dictionary<string, Transform> MapBones(MmdUnityModelInstance instance)
        {
            return instance.BoneTransforms.ToDictionary(
                bone => AnimationUtility.CalculateTransformPath(bone, instance.Root.transform),
                bone => bone,
                StringComparer.Ordinal);
        }

        private static void CreateAssets(string pmxPath, string vmdPath, out MmdPmxAsset pmx, out MmdVmdAsset vmd)
        {
            pmx = ScriptableObject.CreateInstance<MmdPmxAsset>();
            pmx.Initialize(File.ReadAllBytes(pmxPath), Path.GetFileName(pmxPath), pmxPath, 1.0f);
            vmd = ScriptableObject.CreateInstance<MmdVmdAsset>();
            vmd.Initialize(File.ReadAllBytes(vmdPath), Path.GetFileName(vmdPath), vmdPath);
        }

        private static void DestroyBinding(MmdUnityPlaybackBinding? binding)
        {
            if (binding == null) return;
            MmdUnityModelInstance instance = binding.Instance;
            binding.Dispose();
            if (instance.Root != null) UnityEngine.Object.DestroyImmediate(instance.Root);
            if (instance.Mesh != null) UnityEngine.Object.DestroyImmediate(instance.Mesh);
            foreach (Material material in instance.Materials.Distinct())
                if (material != null) UnityEngine.Object.DestroyImmediate(material);
            foreach (Texture2D texture in instance.OwnedTextures.Distinct())
                if (texture != null) UnityEngine.Object.DestroyImmediate(texture);
        }

        private static void DestroyAssets(MmdPmxAsset pmx, MmdVmdAsset vmd)
        {
            UnityEngine.Object.DestroyImmediate(pmx);
            UnityEngine.Object.DestroyImmediate(vmd);
        }
    }
}
