#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using Mmd;
using Mmd.Parser;
using Mmd.Physics;
using Mmd.Rendering;
using Mmd.UnityIntegration;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Rendering;

namespace Mmd.Samples.RuntimeVerification
{
    public sealed class MmdRuntimeVerificationRunner
    {
        private static readonly MethodInfo? ApplyTimelineLivePhysicsForwardMethod =
            typeof(MmdUnityPlaybackController).GetMethod(
                "ApplyTimelineLivePhysicsForward",
                BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo? PlaybackBindingField =
            typeof(MmdUnityPlaybackController).GetField(
                "binding",
                BindingFlags.Instance | BindingFlags.NonPublic);

        private readonly MmdRuntimeVerificationArguments arguments;

        public MmdRuntimeVerificationRunner(MmdRuntimeVerificationArguments arguments)
        {
            this.arguments = arguments ?? throw new ArgumentNullException(nameof(arguments));
        }

        public MmdRuntimeVerificationReport Report { get; private set; } = new();

        public IEnumerator Run()
        {
            var stopwatch = Stopwatch.StartNew();
            var report = new MmdRuntimeVerificationReport
            {
                drive = arguments.Drive == MmdRuntimeVerificationDrive.Timeline ? "timeline" : "controller",
                fastRuntimeRequested = arguments.FastRuntimeEnabled,
                requestedDurationSeconds = arguments.DurationSeconds,
                requestedFrameRate = arguments.FrameRate,
                startedAtUtc = DateTime.UtcNow.ToString("O")
            };

            if (arguments.HelpRequested)
            {
                report.status = "help";
                report.exitCode = 0;
                report.caseResults = Array.Empty<MmdRuntimeVerificationCaseResult>();
                Finish(report, stopwatch, 0);
                yield break;
            }

            if (arguments.Errors.Count > 0)
            {
                report.status = "invalid-arguments";
                report.exitCode = 2;
                report.caseResults = new[]
                {
                    new MmdRuntimeVerificationCaseResult
                    {
                        name = "arguments",
                        status = "failed",
                        parseStatus = "not-run",
                        playbackStatus = "not-run",
                        exception = string.Join(Environment.NewLine, arguments.Errors)
                    }
                };
                Finish(report, stopwatch, report.exitCode);
                yield break;
            }

            MmdRuntimeVerificationCase[] cases = arguments.CreateCases();
            if (arguments.Errors.Count > 0)
            {
                report.status = "invalid-arguments";
                report.exitCode = 2;
                report.caseResults = new[]
                {
                    new MmdRuntimeVerificationCaseResult
                    {
                        name = "arguments",
                        status = "failed",
                        parseStatus = "not-run",
                        playbackStatus = "not-run",
                        exception = string.Join(Environment.NewLine, arguments.Errors)
                    }
                };
                Finish(report, stopwatch, report.exitCode);
                yield break;
            }

            if (cases.Length == 0)
            {
                report.status = "failed";
                report.exitCode = 1;
                report.caseResults = new[]
                {
                    new MmdRuntimeVerificationCaseResult
                    {
                        name = "inputs",
                        status = "failed",
                        parseStatus = "not-run",
                        playbackStatus = "not-run",
                        exception = "No runtime verification cases were found."
                    }
                };
                Finish(report, stopwatch, report.exitCode);
                yield break;
            }

            var results = new List<MmdRuntimeVerificationCaseResult>(cases.Length);
            bool anyFailure = false;
            foreach (MmdRuntimeVerificationCase verificationCase in cases)
            {
                MmdRuntimeVerificationCaseResult result = CreateInitialResult(verificationCase);
                results.Add(result);
                yield return RunCase(verificationCase, result);
                if (!string.Equals(result.status, "passed", StringComparison.Ordinal) &&
                    !string.Equals(result.status, "skipped", StringComparison.Ordinal))
                {
                    anyFailure = true;
                }

                yield return null;
            }

            report.caseResults = results.ToArray();
            report.status = anyFailure ? "failed" : "passed";
            report.exitCode = anyFailure ? 1 : 0;
            Finish(report, stopwatch, report.exitCode);
        }

        private IEnumerator RunCase(
            MmdRuntimeVerificationCase verificationCase,
            MmdRuntimeVerificationCaseResult result)
        {
            var stopwatch = Stopwatch.StartNew();
            int consoleErrorCount = 0;
            int consoleWarningCount = 0;
            Application.LogCallback onLogMessage = OnLogMessage;
            Application.logMessageReceived += onLogMessage;
            try
            {
                if (!string.IsNullOrWhiteSpace(verificationCase.SkipReason))
                {
                    result.status = "skipped";
                    result.skipReason = verificationCase.SkipReason;
                    result.parseStatus = "skipped";
                    result.playbackStatus = "skipped";
                    result.durationSeconds = (float)stopwatch.Elapsed.TotalSeconds;
                    yield break;
                }

                bool parsed = ParseInputs(verificationCase, result);
                if (!parsed)
                {
                    result.parseStatus = "failed";
                    result.status = "failed";
                    result.playbackStatus = "skipped";
                    result.durationSeconds = (float)stopwatch.Elapsed.TotalSeconds;
                    yield break;
                }

                result.parseStatus = "passed";
                if (verificationCase.ParseOnly)
                {
                    result.status = "passed";
                    result.playbackStatus = "skipped";
                    result.durationSeconds = (float)stopwatch.Elapsed.TotalSeconds;
                    yield break;
                }

                if (arguments.Drive == MmdRuntimeVerificationDrive.Timeline)
                {
                    yield return DriveTimeline(verificationCase, result);
                }
                else
                {
                    yield return DriveController(verificationCase, result);
                }

                result.durationSeconds = (float)stopwatch.Elapsed.TotalSeconds;
            }
            finally
            {
                Application.logMessageReceived -= onLogMessage;
                if (!string.Equals(result.status, "skipped", StringComparison.Ordinal))
                {
                    result.consoleErrorCount = consoleErrorCount;
                    result.consoleWarningCount = consoleWarningCount;
                    if (consoleErrorCount > 0)
                    {
                        result.status = "failed";
                        AppendException(
                            result,
                            "Console reported " + consoleErrorCount + " error(s) during case execution.");
                    }

                    if (string.Equals(result.visualSmoke.smokeStatus, "failed", StringComparison.Ordinal))
                    {
                        result.status = "failed";
                        AppendException(result, "Visual smoke failed:"
                            + (result.visualSmoke.isBlank ? " blank" : "")
                            + (result.visualSmoke.isAllBlack ? " all-black" : "")
                            + (result.visualSmoke.isAllWhite ? " all-white" : ""));
                    }
                }
            }

            void OnLogMessage(string logString, string stackTrace, LogType type)
            {
                switch (type)
                {
                    case LogType.Error:
                    case LogType.Exception:
                    case LogType.Assert:
                        consoleErrorCount++;
                        break;
                    case LogType.Warning:
                        consoleWarningCount++;
                        break;
                }
            }
        }

        private bool ParseInputs(
            MmdRuntimeVerificationCase verificationCase,
            MmdRuntimeVerificationCaseResult result)
        {
            var parser = new NativeMmdParser();
            bool ok = true;
            if (!string.IsNullOrWhiteSpace(verificationCase.PmxPath))
            {
                try
                {
                    byte[] pmxBytes = ReadRequiredFile(verificationCase.PmxPath);
                    MmdModelDefinition model = parser.LoadModel(pmxBytes);
                    MmdModelValidator.ThrowIfInvalid(model);
                    result.model = SummarizeModel(model);
                }
                catch (Exception ex)
                {
                    ok = false;
                    AppendException(result, "PMX parse failed: " + FormatException(ex));
                }
            }

            if (!string.IsNullOrWhiteSpace(verificationCase.VmdPath))
            {
                try
                {
                    byte[] vmdBytes = ReadRequiredFile(verificationCase.VmdPath);
                    MmdMotionDefinition motion = parser.LoadMotion(vmdBytes);
                    MmdMotionValidator.ThrowIfInvalid(motion);
                    result.motion = SummarizeMotion(motion);
                }
                catch (Exception ex)
                {
                    ok = false;
                    AppendException(result, "VMD parse failed: " + FormatException(ex));
                }
            }

            return ok;
        }

        private IEnumerator DriveController(
            MmdRuntimeVerificationCase verificationCase,
            MmdRuntimeVerificationCaseResult result)
        {
            GameObject? holder = null;
            MmdUnityPlaybackController? controller = null;
            IEnumerator? playback = null;
            Exception? failure = null;
            try
            {
                try
                {
                    holder = CreatePlaybackRoot(verificationCase, out controller);
                    if (arguments.SampleFrames.Length == 0)
                    {
                        controller.Play();
                    }

                    playback = SamplePlayback(controller, result, arguments.DurationSeconds);
                }
                catch (Exception ex)
                {
                    failure = ex;
                }

                while (failure == null && playback != null)
                {
                    if (!TryMoveNext(playback, out object? current, out failure))
                    {
                        break;
                    }

                    yield return current;
                }

                if (failure == null && controller != null)
                {
                    try
                    {
                        controller.Pause();
                        result.playback = SummarizePlayback(controller, "controller", arguments.DurationSeconds);
                        result.physics = SummarizePhysics(controller);
                        result.materialShaders = CaptureMaterialShaders(controller);
                        result.visualSmoke = CaptureAndAnalyzeSmoke(verificationCase.Name);
                        ValidateExpectedFeatures(verificationCase, result);
                        result.playbackStatus = "passed";
                        if (!string.Equals(result.status, "failed", StringComparison.Ordinal))
                        {
                            result.status = "passed";
                        }
                    }
                    catch (Exception ex)
                    {
                        failure = ex;
                    }
                }

                if (failure != null)
                {
                    result.playbackStatus = "failed";
                    result.status = "failed";
                    AppendException(result, "Controller drive failed: " + FormatException(failure));
                }
            }
            finally
            {
                if (holder != null)
                {
                    UnityEngine.Object.Destroy(holder);
                }
            }
        }

        private IEnumerator DriveTimeline(
            MmdRuntimeVerificationCase verificationCase,
            MmdRuntimeVerificationCaseResult result)
        {
            GameObject? holder = null;
            MmdRuntimeVerificationTimelineDriver? timelineDriver = null;
            MmdUnityPlaybackController? controller = null;
            IEnumerator? timeline = null;
            float start = 0.0f;
            Exception? failure = null;
            try
            {
                try
                {
                    holder = CreatePlaybackRoot(verificationCase, out controller);
                    PlayableDirector director = holder.AddComponent<PlayableDirector>();
                    timelineDriver = new MmdRuntimeVerificationTimelineDriver();
                    timeline = timelineDriver.Play(
                        director,
                        controller,
                        arguments.DurationSeconds,
                        arguments.FrameRate);
                    start = Time.realtimeSinceStartup;
                }
                catch (Exception ex)
                {
                    failure = ex;
                }

                while (failure == null && timeline != null && controller != null)
                {
                    if (!TryMoveNext(timeline, out object? current, out failure))
                    {
                        break;
                    }

                    AddSample(result, controller, start);
                    yield return current;
                }

                if (failure == null && controller != null)
                {
                    try
                    {
                        result.playback = SummarizePlayback(controller, "timeline", arguments.DurationSeconds);
                        result.physics = SummarizePhysics(controller);
                        result.materialShaders = CaptureMaterialShaders(controller);
                        result.visualSmoke = CaptureAndAnalyzeSmoke(verificationCase.Name);
                        ValidateExpectedFeatures(verificationCase, result);
                        result.playbackStatus = "passed";
                        if (!string.Equals(result.status, "failed", StringComparison.Ordinal))
                        {
                            result.status = "passed";
                        }
                    }
                    catch (Exception ex)
                    {
                        failure = ex;
                    }
                }

                if (failure != null)
                {
                    result.playbackStatus = "failed";
                    result.status = "failed";
                    AppendException(result, "Timeline drive failed: " + FormatException(failure));
                }
            }
            finally
            {
                timelineDriver?.Dispose();
                if (holder != null)
                {
                    UnityEngine.Object.Destroy(holder);
                }
            }
        }

        private GameObject CreatePlaybackRoot(
            MmdRuntimeVerificationCase verificationCase,
            out MmdUnityPlaybackController controller)
        {
            var holder = new GameObject("MMD Runtime Verification Case");
            var importer = holder.AddComponent<MmdRuntimeImporterComponent>();
            controller = holder.AddComponent<MmdUnityPlaybackController>();
            controller.SetPlayOnStart(false);
            controller.SetPhysicsMode(MmdPhysicsMode.Live);
            importer.ConfigurePaths(
                verificationCase.PmxPath,
                verificationCase.VmdPath,
                arguments.FrameRate,
                startFrame: 0,
                shouldPlayOnStart: false);

            controller.ConfigureFromRuntimeImporterPaths(
                verificationCase.PmxPath,
                verificationCase.VmdPath,
                new MmdPlaybackConfig(arguments.FrameRate, 0, playOnStart: false),
                allowRuntimeFallback: true,
                materialPreset: verificationCase.MaterialPreset);

            controller.SetPhysicsMode(MmdPhysicsMode.Live);
            if (!arguments.FastRuntimeEnabled)
            {
                controller.DisableFastRuntime();
            }

            return holder;
        }

        private static bool TryMoveNext(
            IEnumerator enumerator,
            out object? current,
            out Exception? failure)
        {
            current = null;
            failure = null;
            try
            {
                bool hasNext = enumerator.MoveNext();
                if (hasNext)
                {
                    current = enumerator.Current;
                }

                return hasNext;
            }
            catch (Exception ex)
            {
                failure = ex;
                return false;
            }
        }

        private IEnumerator SamplePlayback(
            MmdUnityPlaybackController controller,
            MmdRuntimeVerificationCaseResult result,
            float durationSeconds)
        {
            if (arguments.SampleFrames.Length > 0)
            {
                int sampleIndex = 0;
                int maxFrame = arguments.SampleFrames[arguments.SampleFrames.Length - 1];
                for (int frame = 0; frame <= maxFrame; frame++)
                {
                    ApplyLivePhysicsForwardFrame(controller, frame, arguments.FrameRate);
                    if (sampleIndex < arguments.SampleFrames.Length &&
                        frame == arguments.SampleFrames[sampleIndex])
                    {
                        float sampleTime = frame / arguments.FrameRate;
                        AddSample(result, controller, Time.realtimeSinceStartup - sampleTime, frame);
                        sampleIndex++;
                    }

                    yield return null;
                }

                yield break;
            }

            float start = Time.realtimeSinceStartup;
            AddSample(result, controller, start);
            while (Time.realtimeSinceStartup - start < durationSeconds)
            {
                AddSample(result, controller, start);
                yield return null;
            }

            AddSample(result, controller, start);
        }

        private void AddSample(
            MmdRuntimeVerificationCaseResult result,
            MmdUnityPlaybackController controller,
            float startTime,
            int? frameOverride = null)
        {
            var frames = new List<MmdRuntimeVerificationSampledFrame>(result.sampledFrames);
            frames.Add(new MmdRuntimeVerificationSampledFrame
            {
                timeSeconds = Math.Max(0.0f, Time.realtimeSinceStartup - startTime),
                frame = frameOverride ?? controller.CurrentFrame,
                configured = controller.IsConfigured,
                fastRuntimeEnabled = controller.IsFastRuntimeEnabled,
                physicsDiagnosticsAvailable = controller.LastLivePhysicsDiagnostics != null,
                bones = arguments.DumpBones ? BuildBoneSamples(controller) : null,
                matrixSpace = "mmd-model",
                matrixLayout = "column-major",
                importScale = ResolveImportScale(controller)
            });
            result.sampledFrames = frames.ToArray();
        }

        private static void ApplyLivePhysicsForwardFrame(
            MmdUnityPlaybackController controller,
            int frame,
            float frameRate)
        {
            if (ApplyTimelineLivePhysicsForwardMethod == null)
            {
                controller.ApplyFrame(frame);
                return;
            }

            try
            {
                ApplyTimelineLivePhysicsForwardMethod.Invoke(
                    controller,
                    new object[] { frame / frameRate, frameRate });
            }
            catch (TargetInvocationException ex) when (ex.InnerException != null)
            {
                throw ex.InnerException;
            }
        }

        private static MmdRuntimeVerificationBoneSample[] BuildBoneSamples(
            MmdUnityPlaybackController controller)
        {
            MmdPlaybackSnapshot? snapshot = controller.LastSnapshot;
            if (snapshot?.frame?.bones == null || snapshot.frame.bones.Count == 0)
            {
                return Array.Empty<MmdRuntimeVerificationBoneSample>();
            }

            var bones = new MmdRuntimeVerificationBoneSample[snapshot.frame.bones.Count];
            for (int i = 0; i < snapshot.frame.bones.Count; i++)
            {
                MmdEvaluatedBonePose bone = snapshot.frame.bones[i];
                bones[i] = new MmdRuntimeVerificationBoneSample
                {
                    index = bone.index,
                    name = bone.name ?? string.Empty,
                    worldMatrix = CopyWorldMatrix(bone.worldMatrix)
                };
            }

            return bones;
        }

        private static float[] CopyWorldMatrix(float[]? worldMatrix)
        {
            if (worldMatrix == null || worldMatrix.Length != 16)
            {
                return Array.Empty<float>();
            }

            var copy = new float[16];
            Array.Copy(worldMatrix, copy, copy.Length);
            return copy;
        }

        private static float ResolveImportScale(MmdUnityPlaybackController controller)
        {
            if (PlaybackBindingField?.GetValue(controller) is MmdUnityPlaybackBinding binding)
            {
                return binding.Instance.ImportScale;
            }

            return 1.0f;
        }

        private static MmdRuntimeVerificationCaseResult CreateInitialResult(
            MmdRuntimeVerificationCase verificationCase)
        {
            return new MmdRuntimeVerificationCaseResult
            {
                name = verificationCase.Name,
                pmxPath = verificationCase.PmxPath,
                vmdPath = verificationCase.VmdPath,
                parseOnly = verificationCase.ParseOnly,
                requestedMaterialPreset = verificationCase.MaterialPreset.ToString(),
                expectedFeatures = verificationCase.ExpectedFeatures
            };
        }

        private static string[] CaptureMaterialShaders(MmdUnityPlaybackController controller)
        {
            GameObject? root = controller.ConfiguredInstanceRoot;
            if (root == null)
            {
                return Array.Empty<string>();
            }

            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(includeInactive: true);
            var names = new List<string>();
            for (int i = 0; i < renderers.Length; i++)
            {
                Material[] materials = renderers[i].sharedMaterials;
                for (int j = 0; j < materials.Length; j++)
                {
                    string shaderName = materials[j]?.shader != null ? materials[j].shader.name : string.Empty;
                    if (!string.IsNullOrWhiteSpace(shaderName) && !names.Contains(shaderName))
                    {
                        names.Add(shaderName);
                    }
                }
            }

            return names.ToArray();
        }

        private static void ValidateExpectedFeatures(
            MmdRuntimeVerificationCase verificationCase,
            MmdRuntimeVerificationCaseResult result)
        {
            for (int i = 0; i < verificationCase.ExpectedFeatures.Length; i++)
            {
                string feature = verificationCase.ExpectedFeatures[i];
                if (string.Equals(feature, "urp-lit-preset", StringComparison.Ordinal))
                {
                    bool requested = verificationCase.MaterialPreset == MmdMaterialPreset.UrpLit;
                    bool shaderBound = Contains(result.materialShaders, MmdUrpMaterialBindingDescriptorBuilder.UrpLitShaderName);
                    if (!requested || !shaderBound)
                    {
                        result.status = "failed";
                        AppendException(
                            result,
                            "Expected URP Lit material preset, requested=" + verificationCase.MaterialPreset +
                            ", shaders=[" + string.Join(", ", result.materialShaders) + "].");
                    }
                }
                else if (string.Equals(feature, "timeline-drive", StringComparison.Ordinal))
                {
                    if (!string.Equals(result.playback.driver, "timeline", StringComparison.Ordinal))
                    {
                        result.status = "failed";
                        AppendException(
                            result,
                            "Expected timeline playback driver, actual=" + result.playback.driver + ".");
                    }
                }
                else if (string.Equals(feature, "visual-smoke", StringComparison.Ordinal))
                {
                    if (!string.Equals(result.visualSmoke.smokeStatus, "passed", StringComparison.Ordinal))
                    {
                        result.status = "failed";
                        AppendException(
                            result,
                            "Expected visual smoke to pass, status=" + result.visualSmoke.smokeStatus +
                            ", captured=" + result.visualSmoke.captured +
                            ", foregroundPixels=" + result.visualSmoke.foregroundPixelCount +
                            ", outlinePixels=" + result.visualSmoke.outlinePixelCount + ".");
                    }
                }
                else if (string.Equals(feature, "outline-pixel", StringComparison.Ordinal))
                {
                    if (result.visualSmoke.outlinePixelCount <= 0)
                    {
                        result.status = "failed";
                        AppendException(
                            result,
                            "Expected at least one outline pixel in visual smoke, outlinePixels=" +
                            result.visualSmoke.outlinePixelCount + ".");
                    }
                }
            }
        }

        private static bool Contains(string[] values, string expected)
        {
            for (int i = 0; i < values.Length; i++)
            {
                if (string.Equals(values[i], expected, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static byte[] ReadRequiredFile(string path)
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException("Input file was not found.", path);
            }

            return File.ReadAllBytes(path);
        }

        private static MmdRuntimeVerificationModelSummary SummarizeModel(MmdModelDefinition model)
        {
            return new MmdRuntimeVerificationModelSummary
            {
                parsed = true,
                name = model.name ?? string.Empty,
                vertexCount = model.vertices.Count,
                indexCount = model.indices.Count,
                boneCount = model.bones.Count,
                morphCount = model.morphs.Count,
                materialCount = model.materials.Count,
                ikCount = model.ik.Count,
                rigidbodyCount = model.physics?.rigidbodies.Count ?? 0,
                jointCount = model.physics?.joints.Count ?? 0
            };
        }

        private static MmdRuntimeVerificationMotionSummary SummarizeMotion(MmdMotionDefinition motion)
        {
            return new MmdRuntimeVerificationMotionSummary
            {
                parsed = true,
                targetModelName = motion.targetModelName ?? string.Empty,
                maxFrame = motion.maxFrame,
                boneKeyframeCount = motion.boneKeyframes.Count,
                morphKeyframeCount = motion.morphKeyframes.Count,
                modelKeyframeCount = motion.modelKeyframes.Count,
                cameraKeyframeCount = motion.cameraKeyframeCount,
                lightKeyframeCount = motion.lightKeyframeCount,
                selfShadowKeyframeCount = motion.selfShadowKeyframeCount
            };
        }

        private static MmdRuntimeVerificationPlaybackSummary SummarizePlayback(
            MmdUnityPlaybackController controller,
            string driver,
            float durationSeconds)
        {
            return new MmdRuntimeVerificationPlaybackSummary
            {
                configured = controller.IsConfigured,
                fastRuntimeEnabled = controller.IsFastRuntimeEnabled,
                fastRuntimeReason = controller.LastFastRuntimeReason,
                driver = driver,
                finalFrame = controller.CurrentFrame,
                finalTimeSeconds = durationSeconds,
                controllerSourceId = controller.ModelSourceId,
                motionSourceId = controller.MotionSourceId
            };
        }

        private static MmdRuntimeVerificationPhysicsSummary SummarizePhysics(
            MmdUnityPlaybackController controller)
        {
            MmdLivePhysicsFrameDiagnostics? diagnostics = controller.LastLivePhysicsDiagnostics;
            if (diagnostics == null)
            {
                return new MmdRuntimeVerificationPhysicsSummary { available = false };
            }

            return new MmdRuntimeVerificationPhysicsSummary
            {
                available = true,
                frame = diagnostics.frame,
                deltaTime = diagnostics.deltaTime,
                totalMs = diagnostics.totalMs,
                unsupportedWorldAnchorJointCount = diagnostics.unsupportedWorldAnchorJointCount,
                bodyDiagnosticCount = diagnostics.bodyDiagnostics?.Length ?? 0,
                pinnedBodyCount = diagnostics.pinnedBodies?.pinnedBodyCount ?? 0,
                staticPinnedBodyCount = diagnostics.pinnedBodies?.staticPinnedBodyCount ?? 0,
                dynamicOrientationPinnedBodyCount =
                    diagnostics.pinnedBodies?.dynamicOrientationPinnedBodyCount ?? 0,
                dynamicInitialPinnedBodyCount =
                    diagnostics.pinnedBodies?.dynamicInitialPinnedBodyCount ?? 0,
                maxPinnedBodySyncDistance = diagnostics.pinnedBodies?.maxPinnedBodySyncDistance ?? 0.0f,
                maxPinnedBodyRotationAngle = diagnostics.pinnedBodies?.maxPinnedBodyRotationAngle ?? 0.0f,
                comparisonSpace = diagnostics.comparisonSpace ?? string.Empty
            };
        }

        private static void AppendException(MmdRuntimeVerificationCaseResult result, string message)
        {
            if (string.IsNullOrWhiteSpace(result.exception))
            {
                result.exception = message;
                return;
            }

            result.exception += Environment.NewLine + message;
        }

        private static string FormatException(Exception ex)
        {
            return ex.GetType().Name + ": " + ex.Message;
        }

        private MmdRuntimeVerificationVisualSmoke CaptureAndAnalyzeSmoke(string caseName)
        {
            var smoke = new MmdRuntimeVerificationVisualSmoke();
            GameObject? grid = GameObject.Find("MMD Runtime Verification Grid");
            bool restoreGrid = grid != null && grid.activeSelf;
            if (restoreGrid)
            {
                grid!.SetActive(false);
            }

            Camera? camera = Camera.main;
            if (camera == null)
            {
                camera = UnityEngine.Object.FindAnyObjectByType<Camera>();
            }

            if (camera == null)
            {
                smoke.smokeStatus = "no-camera";
                if (restoreGrid)
                {
                    grid!.SetActive(true);
                }

                return smoke;
            }

            const int width = 640;
            const int height = 480;
            var rt = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
            var texture = new Texture2D(width, height, TextureFormat.RGB24, mipChain: false);
            RenderTexture previousTarget = camera.targetTexture;
            RenderTexture previousActive = RenderTexture.active;
            try
            {
                camera.targetTexture = rt;
                camera.Render();

                RenderTexture.active = rt;
                texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                texture.Apply();

                smoke.captured = true;
                smoke.width = width;
                smoke.height = height;

                if (!string.IsNullOrWhiteSpace(arguments.ScreenshotDir))
                {
                    try
                    {
                        Directory.CreateDirectory(arguments.ScreenshotDir);
                        string safeName = caseName.Replace('/', '_').Replace('\\', '_').Replace(':', '_');
                        string path = Path.Combine(arguments.ScreenshotDir, safeName + ".png");
                        byte[] png = texture.EncodeToPNG();
                        File.WriteAllBytes(path, png);
                        smoke.screenshotPath = path;
                    }
                    catch (Exception ex)
                    {
                        smoke.screenshotPath = "write-failed: " + ex.Message;
                    }
                }

                AnalyzePixels(texture, smoke);
                return smoke;
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                RenderTexture.ReleaseTemporary(rt);
                UnityEngine.Object.Destroy(texture);
                if (restoreGrid)
                {
                    grid!.SetActive(true);
                }
            }
        }

        private static void AnalyzePixels(Texture2D texture, MmdRuntimeVerificationVisualSmoke smoke)
        {
            Color32[] pixels = texture.GetPixels32();
            if (pixels.Length == 0)
            {
                smoke.smokeStatus = "no-pixels";
                return;
            }

            double luminanceSum = 0.0;
            int blackCount = 0;
            int whiteCount = 0;
            int foregroundCount = 0;
            int outlineCount = 0;
            int minX = texture.width;
            int minY = texture.height;
            int maxX = -1;
            int maxY = -1;
            Color32 firstPixel = pixels[0];
            bool allSame = true;
            var uniqueColors = new HashSet<int>();
            const int sampleStride = 4;
            int sampledForUnique = 0;

            for (int i = 0; i < pixels.Length; i++)
            {
                Color32 p = pixels[i];
                float luminance = (0.2126f * p.r + 0.7152f * p.g + 0.0722f * p.b) / 255.0f;
                luminanceSum += luminance;
                int x = i % texture.width;
                int y = i / texture.width;
                int bgDelta =
                    Math.Abs(p.r - firstPixel.r) +
                    Math.Abs(p.g - firstPixel.g) +
                    Math.Abs(p.b - firstPixel.b);
                bool foreground = bgDelta > 24;

                if (p.r < 5 && p.g < 5 && p.b < 5)
                {
                    blackCount++;
                }

                if (p.r > 250 && p.g > 250 && p.b > 250)
                {
                    whiteCount++;
                }

                if (foreground)
                {
                    foregroundCount++;
                    if (x < minX) minX = x;
                    if (y < minY) minY = y;
                    if (x > maxX) maxX = x;
                    if (y > maxY) maxY = y;

                    if (luminance < 0.20f)
                    {
                        outlineCount++;
                    }
                }

                if (allSame && (p.r != firstPixel.r || p.g != firstPixel.g || p.b != firstPixel.b))
                {
                    allSame = false;
                }

                if (i % sampleStride == 0 && sampledForUnique < 10000)
                {
                    uniqueColors.Add((p.r << 16) | (p.g << 8) | p.b);
                    sampledForUnique++;
                }
            }

            float avgLuminance = (float)(luminanceSum / pixels.Length);
            float blackRatio = (float)blackCount / pixels.Length;
            float whiteRatio = (float)whiteCount / pixels.Length;

            smoke.averageLuminance = avgLuminance;
            smoke.uniqueColorCount = uniqueColors.Count;
            smoke.isBlank = allSame;
            smoke.isAllBlack = blackRatio > 0.99f;
            smoke.isAllWhite = whiteRatio > 0.99f;
            smoke.foregroundPixelCount = foregroundCount;
            smoke.foregroundCoverage = (float)foregroundCount / pixels.Length;
            smoke.hasContentBounds = foregroundCount > 0;
            smoke.outlinePixelCount = outlineCount;
            if (smoke.hasContentBounds)
            {
                smoke.contentMinX = minX;
                smoke.contentMinY = minY;
                smoke.contentMaxX = maxX;
                smoke.contentMaxY = maxY;
                smoke.touchesImageEdge =
                    minX <= 0 ||
                    minY <= 0 ||
                    maxX >= texture.width - 1 ||
                    maxY >= texture.height - 1;
            }

            if (smoke.isBlank ||
                smoke.isAllBlack ||
                smoke.isAllWhite ||
                smoke.foregroundPixelCount == 0 ||
                smoke.foregroundCoverage < 0.001f)
            {
                smoke.smokeStatus = "failed";
            }
            else
            {
                smoke.smokeStatus = "passed";
            }
        }

        private void Finish(
            MmdRuntimeVerificationReport report,
            Stopwatch stopwatch,
            int exitCode)
        {
            stopwatch.Stop();
            report.finishedAtUtc = DateTime.UtcNow.ToString("O");
            report.durationSeconds = (float)stopwatch.Elapsed.TotalSeconds;
            report.exitCode = exitCode;
            Report = report;
        }
    }
}
