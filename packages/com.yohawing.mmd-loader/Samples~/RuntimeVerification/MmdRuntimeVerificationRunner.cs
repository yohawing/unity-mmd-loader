#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Mmd;
using Mmd.Parser;
using Mmd.Physics;
using Mmd.UnityIntegration;
using UnityEngine;
using UnityEngine.Playables;

namespace Mmd.Samples.RuntimeVerification
{
    public sealed class MmdRuntimeVerificationRunner
    {
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
                if (!string.Equals(result.status, "passed", StringComparison.Ordinal))
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
            bool parsed = ParseInputs(verificationCase, result);
            if (!parsed)
            {
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
            try
            {
                holder = CreatePlaybackRoot(verificationCase, out MmdUnityPlaybackController controller);
                controller.Play();
                yield return SamplePlayback(controller, result, arguments.DurationSeconds);
                controller.Pause();
                result.playback = SummarizePlayback(controller, "controller", arguments.DurationSeconds);
                result.physics = SummarizePhysics(controller);
                result.playbackStatus = "passed";
                result.status = "passed";
            }
            catch (Exception ex)
            {
                result.playbackStatus = "failed";
                result.status = "failed";
                AppendException(result, "Controller drive failed: " + FormatException(ex));
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
            try
            {
                holder = CreatePlaybackRoot(verificationCase, out MmdUnityPlaybackController controller);
                PlayableDirector director = holder.AddComponent<PlayableDirector>();
                timelineDriver = new MmdRuntimeVerificationTimelineDriver();
                IEnumerator timeline = timelineDriver.Play(
                    director,
                    controller,
                    arguments.DurationSeconds,
                    arguments.FrameRate);
                float start = Time.realtimeSinceStartup;
                while (timeline.MoveNext())
                {
                    AddSample(result, controller, start);
                    yield return timeline.Current;
                }

                result.playback = SummarizePlayback(controller, "timeline", arguments.DurationSeconds);
                result.physics = SummarizePhysics(controller);
                result.playbackStatus = "passed";
                result.status = "passed";
            }
            catch (Exception ex)
            {
                result.playbackStatus = "failed";
                result.status = "failed";
                AppendException(result, "Timeline drive failed: " + FormatException(ex));
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
                allowRuntimeFallback: true);

            controller.SetPhysicsMode(MmdPhysicsMode.Live);
            if (!arguments.FastRuntimeEnabled)
            {
                controller.DisableFastRuntime();
            }

            return holder;
        }

        private IEnumerator SamplePlayback(
            MmdUnityPlaybackController controller,
            MmdRuntimeVerificationCaseResult result,
            float durationSeconds)
        {
            float start = Time.realtimeSinceStartup;
            AddSample(result, controller, start);
            while (Time.realtimeSinceStartup - start < durationSeconds)
            {
                AddSample(result, controller, start);
                yield return null;
            }

            AddSample(result, controller, start);
        }

        private static void AddSample(
            MmdRuntimeVerificationCaseResult result,
            MmdUnityPlaybackController controller,
            float startTime)
        {
            if (result.sampledFrames.Length >= 8)
            {
                return;
            }

            var frames = new List<MmdRuntimeVerificationSampledFrame>(result.sampledFrames);
            frames.Add(new MmdRuntimeVerificationSampledFrame
            {
                timeSeconds = Math.Max(0.0f, Time.realtimeSinceStartup - startTime),
                frame = controller.CurrentFrame,
                configured = controller.IsConfigured,
                fastRuntimeEnabled = controller.IsFastRuntimeEnabled,
                physicsDiagnosticsAvailable = controller.LastLivePhysicsDiagnostics != null
            });
            result.sampledFrames = frames.ToArray();
        }

        private static MmdRuntimeVerificationCaseResult CreateInitialResult(
            MmdRuntimeVerificationCase verificationCase)
        {
            return new MmdRuntimeVerificationCaseResult
            {
                name = verificationCase.Name,
                pmxPath = verificationCase.PmxPath,
                vmdPath = verificationCase.VmdPath,
                parseOnly = verificationCase.ParseOnly
            };
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
