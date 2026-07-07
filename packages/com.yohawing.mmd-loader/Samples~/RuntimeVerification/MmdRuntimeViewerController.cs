#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using Mmd.Physics;
using Mmd.UnityIntegration;
using UnityEngine;

namespace Mmd.Samples.RuntimeVerification
{
    public sealed class MmdRuntimeViewerController : MonoBehaviour
    {
        public static Func<string, string, string>? BrowseFileOverride;

        private readonly List<string> statusLines = new();
        private MmdRuntimeVerificationArguments? arguments;
        private MmdRuntimeViewerFixtureCase[] cases = Array.Empty<MmdRuntimeViewerFixtureCase>();
        private GameObject? playbackRoot;
        private MmdUnityPlaybackController? playbackController;
        private Vector2 listScroll;
        private int selectedIndex = -1;
        private string pmxInput = string.Empty;
        private string vmdInput = string.Empty;

        public void Initialize(MmdRuntimeVerificationArguments viewerArguments)
        {
            arguments = viewerArguments ?? throw new ArgumentNullException(nameof(viewerArguments));
            if (!string.IsNullOrWhiteSpace(arguments.PmxPath))
            {
                pmxInput = arguments.PmxPath;
            }

            if (!string.IsNullOrWhiteSpace(arguments.VmdPath))
            {
                vmdInput = arguments.VmdPath;
            }

            ReloadCases();
            if (cases.Length > 0)
            {
                SelectCase(0);
            }
            else
            {
                AddStatus("No cases loaded. Enter PMX/VMD paths below and click Load.");
            }
        }

        private void OnDestroy()
        {
            ClearPlayback();
            BulletMmdPhysicsBackend.ResetMaxSubStepEstimateFixedTimeStepSecondsForDiagnostics();
        }

        private void OnGUI()
        {
            const float panelWidth = 360.0f;
            GUILayout.BeginArea(new Rect(12.0f, 12.0f, panelWidth, Screen.height - 24.0f), GUI.skin.box);
            GUILayout.Label("Runtime Viewer");

            if (arguments == null)
            {
                GUILayout.Label("Viewer is not initialized.");
                GUILayout.EndArea();
                return;
            }

            if (arguments.Errors.Count > 0)
            {
                GUILayout.Label(string.Join(Environment.NewLine, arguments.Errors));
            }

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Reload", GUILayout.Width(88.0f)))
            {
                ReloadCases();
            }

            if (GUILayout.Button(playbackController != null && playbackController.IsPlaying ? "Pause" : "Play", GUILayout.Width(88.0f)))
            {
                TogglePlayback();
            }

            if (GUILayout.Button("Stop", GUILayout.Width(88.0f)))
            {
                StopPlayback();
            }

            GUILayout.EndHorizontal();

            if (playbackController != null)
            {
                int maxFrame = playbackController.MotionMaxFrame;
                if (maxFrame > 0)
                {
                    int displayFrame = Mathf.Min(playbackController.CurrentFrame, maxFrame);
                    float newFrame = GUILayout.HorizontalSlider(displayFrame, 0, maxFrame);
                    int seekTarget = Mathf.RoundToInt(newFrame);
                    if (seekTarget != displayFrame)
                    {
                        if (playbackController.IsPlaying)
                        {
                            playbackController.Pause();
                        }

                        playbackController.SeekFrame(seekTarget);
                    }

                    GUILayout.Label("Frame: " + playbackController.CurrentFrame + " / " + maxFrame);
                }
                else
                {
                    GUILayout.Label("Frame: " + playbackController.CurrentFrame);
                }
            }

            GUILayout.Space(6.0f);
            DrawFileInputSection();

            GUILayout.Space(6.0f);
            GUILayout.Label("Cases");
            listScroll = GUILayout.BeginScrollView(listScroll, GUILayout.Height(Mathf.Max(120.0f, Screen.height - 480.0f)));
            for (int i = 0; i < cases.Length; i++)
            {
                string label = i == selectedIndex ? "> " + cases[i].Name : cases[i].Name;
                if (GUILayout.Button(label))
                {
                    SelectCase(i);
                }
            }

            GUILayout.EndScrollView();

            GUILayout.Space(6.0f);
            DrawSelectedCase();

            GUILayout.Space(6.0f);
            GUILayout.Label("Status");
            for (int i = Math.Max(0, statusLines.Count - 6); i < statusLines.Count; i++)
            {
                GUILayout.Label(statusLines[i]);
            }

            GUILayout.EndArea();
        }

        private void ReloadCases()
        {
            ClearPlayback();
            selectedIndex = -1;
            statusLines.Clear();

            if (arguments == null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(arguments.FixtureManifestPath))
            {
                cases = MmdRuntimeViewerFixtureManifest.LoadViewerCases(
                    arguments.FixtureManifestPath,
                    arguments.Errors,
                    includeSkipped: true);
            }
            else if (string.IsNullOrWhiteSpace(arguments.PmxPath) &&
                string.IsNullOrWhiteSpace(arguments.VmdPath) &&
                string.IsNullOrWhiteSpace(arguments.DirectoryPath))
            {
                cases = Array.Empty<MmdRuntimeViewerFixtureCase>();
            }
            else
            {
                MmdRuntimeVerificationCase[] verificationCases = arguments.CreateCases();
                cases = new MmdRuntimeViewerFixtureCase[verificationCases.Length];
                for (int i = 0; i < verificationCases.Length; i++)
                {
                    cases[i] = new MmdRuntimeViewerFixtureCase(
                        verificationCases[i].Name,
                        verificationCases[i].PmxPath,
                        verificationCases[i].VmdPath,
                        string.Empty,
                        string.Empty,
                        string.Empty,
                        0.0f,
                        verificationCases[i].ParseOnly ? "parse-only" : string.Empty);
                }
            }

            AddStatus("Loaded " + cases.Length + " case(s).");
        }

        private void SelectCase(int index)
        {
            if (arguments == null || index < 0 || index >= cases.Length)
            {
                return;
            }

            selectedIndex = index;
            MmdRuntimeViewerFixtureCase selected = cases[index];
            if (!string.IsNullOrWhiteSpace(selected.SkipReason))
            {
                ClearPlayback();
                AddStatus("Skipped: " + selected.SkipReason);
                return;
            }

            try
            {
                StartPlayback(selected.PmxPath, selected.VmdPath, selected.Name);
                AddStatus("Playing: " + selected.Name);
            }
            catch (Exception ex)
            {
                ClearPlayback();
                AddStatus("Load failed: " + ex.GetType().Name + ": " + ex.Message);
            }
        }

        private void DrawFileInputSection()
        {
            GUILayout.Label("Load Files");

            GUILayout.BeginHorizontal();
            GUILayout.Label("PMX", GUILayout.Width(36.0f));
            pmxInput = GUILayout.TextField(pmxInput);
#if UNITY_EDITOR
            if (BrowseFileOverride != null &&
                GUILayout.Button("Browse...", GUILayout.Width(72.0f)))
            {
                string result = BrowseFileOverride("Select PMX file", "pmx");
                if (!string.IsNullOrWhiteSpace(result))
                {
                    pmxInput = result;
                }
            }
#endif
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("VMD", GUILayout.Width(36.0f));
            vmdInput = GUILayout.TextField(vmdInput);
#if UNITY_EDITOR
            if (BrowseFileOverride != null &&
                GUILayout.Button("Browse...", GUILayout.Width(72.0f)))
            {
                string result = BrowseFileOverride("Select VMD file", "vmd");
                if (!string.IsNullOrWhiteSpace(result))
                {
                    vmdInput = result;
                }
            }
#endif
            GUILayout.EndHorizontal();

            if (GUILayout.Button("Load", GUILayout.Width(88.0f)))
            {
                LoadFromInput();
            }
        }

        private void LoadFromInput()
        {
            if (arguments == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(pmxInput))
            {
                AddStatus("PMX path is required.");
                return;
            }

            string pmxPath = Path.GetFullPath(pmxInput.Trim());
            if (!File.Exists(pmxPath))
            {
                AddStatus("PMX file not found: " + pmxPath);
                return;
            }

            string vmdPath = string.Empty;
            if (!string.IsNullOrWhiteSpace(vmdInput))
            {
                vmdPath = Path.GetFullPath(vmdInput.Trim());
                if (!File.Exists(vmdPath))
                {
                    AddStatus("VMD file not found: " + vmdPath);
                    return;
                }
            }

            string displayName = BuildDisplayName(pmxPath, vmdPath);
            try
            {
                selectedIndex = -1;
                StartPlayback(pmxPath, vmdPath, displayName);
                AddStatus("Playing: " + displayName);
            }
            catch (Exception ex)
            {
                ClearPlayback();
                AddStatus("Load failed: " + ex.GetType().Name + ": " + ex.Message);
            }
        }

        private void StartPlayback(string pmxPath, string vmdPath, string displayName)
        {
            if (arguments == null)
            {
                return;
            }

            ClearPlayback();
            var holder = new GameObject("MMD Runtime Viewer Case: " + displayName);
            playbackRoot = holder;
            var importer = holder.AddComponent<MmdRuntimeImporterComponent>();
            playbackController = holder.AddComponent<MmdUnityPlaybackController>();
            playbackController.SetPlayOnStart(false);
            playbackController.SetPhysicsMode(MmdPhysicsMode.Live);
            importer.ConfigurePaths(
                pmxPath,
                vmdPath,
                arguments.FrameRate,
                startFrame: 0,
                shouldPlayOnStart: false);
            playbackController.ConfigureFromRuntimeImporterPaths(
                pmxPath,
                vmdPath,
                new MmdPlaybackConfig(arguments.FrameRate, 0, playOnStart: false),
                allowRuntimeFallback: true);
            playbackController.SetPhysicsMode(MmdPhysicsMode.Live);
            if (!arguments.FastRuntimeEnabled)
            {
                playbackController.DisableFastRuntime();
            }

            playbackController.Play();
        }

        private static string BuildDisplayName(string pmxPath, string vmdPath)
        {
            string pmxName = Path.GetFileNameWithoutExtension(pmxPath);
            if (string.IsNullOrWhiteSpace(vmdPath))
            {
                return pmxName;
            }

            return pmxName + "__" + Path.GetFileNameWithoutExtension(vmdPath);
        }

        private void TogglePlayback()
        {
            if (playbackController == null)
            {
                if (selectedIndex >= 0)
                {
                    SelectCase(selectedIndex);
                }

                return;
            }

            if (playbackController.IsPlaying)
            {
                playbackController.Pause();
                AddStatus("Paused.");
            }
            else
            {
                playbackController.Play();
                AddStatus("Playing.");
            }
        }

        private void StopPlayback()
        {
            ClearPlayback();
            AddStatus("Stopped.");
        }

        private void ClearPlayback()
        {
            playbackController = null;
            if (playbackRoot != null)
            {
                Destroy(playbackRoot);
                playbackRoot = null;
            }
        }

        private void DrawSelectedCase()
        {
            if (selectedIndex < 0 || selectedIndex >= cases.Length)
            {
                if (playbackController != null)
                {
                    GUILayout.Label("Loaded from file input.");
                    GUILayout.Label("Frame: " + playbackController.CurrentFrame);
                    GUILayout.Label("Fast runtime: " + playbackController.IsFastRuntimeEnabled);
                }
                else
                {
                    GUILayout.Label("No case selected.");
                }

                return;
            }

            MmdRuntimeViewerFixtureCase selected = cases[selectedIndex];
            GUILayout.Label("Selected: " + selected.Name);
            DrawPath("PMX", selected.PmxPath);
            DrawPath("VMD", selected.VmdPath);
            DrawPath("Camera", selected.CameraPath);
            DrawPath("Audio", selected.AudioPath);
            DrawPath("Background", selected.BackgroundPath);
            if (Math.Abs(selected.AudioOffsetFrame) > float.Epsilon)
            {
                GUILayout.Label("Audio offset frame: " + selected.AudioOffsetFrame.ToString("0.###"));
            }

            if (playbackController != null)
            {
                GUILayout.Label("Frame: " + playbackController.CurrentFrame);
                GUILayout.Label("Fast runtime: " + playbackController.IsFastRuntimeEnabled);
            }
        }

        private static void DrawPath(string label, string path)
        {
            if (!string.IsNullOrWhiteSpace(path))
            {
                GUILayout.Label(label + ": " + Path.GetFileName(path));
            }
        }

        private void AddStatus(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                return;
            }

            statusLines.Add(line);
            if (statusLines.Count > 32)
            {
                statusLines.RemoveAt(0);
            }

            Debug.Log("[RuntimeViewer] " + line);
        }
    }
}
