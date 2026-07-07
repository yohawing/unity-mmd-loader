#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Mmd.Motion;
using Mmd.Parser;
using Mmd.Physics;
using Mmd.Rendering;
using Mmd.UnityIntegration;
using UnityEngine;
using UnityEngine.Networking;

namespace Mmd.Samples.RuntimeVerification
{
    [System.Serializable]
    internal sealed class MmdRecentEntry
    {
        public string name = string.Empty;
        public string pmxPath = string.Empty;
        public string vmdPath = string.Empty;
        public string cameraPath = string.Empty;
        public string audioPath = string.Empty;
        public string backgroundPath = string.Empty;
        public string materialPreset = string.Empty;
        public float audioOffsetFrame;
        public long timestamp;
    }

    [System.Serializable]
    internal sealed class MmdRecentEntryList
    {
        public List<MmdRecentEntry> entries = new();
    }

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
        private float orbitYaw = 180.0f;
        private float orbitPitch = 15.0f;
        private float orbitDistance = 10.0f;
        private Vector3 orbitTarget = new Vector3(0.0f, 1.0f, 0.0f);
        private bool orbitDragging;
        private bool panDragging;
        private Vector2 lastMousePosition;
        private NativeVmdCameraTrackSampler? cameraSampler;
        private bool cameraVmdActive;
        private AudioSource? audioSource;
        private float audioOffsetFrame;
        private GameObject? backgroundRoot;
        private const int MaxRecentEntries = 16;
        private const string RecentsFileName = "mmd-viewer-recents.json";
        private MmdRecentEntryList recentEntries = new();
        private Vector2 recentsScroll;

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
            LoadRecents();
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

        private void Update()
        {
            if (cameraVmdActive && cameraSampler != null && playbackController != null)
            {
                if (cameraSampler.TrySample(playbackController.CurrentFrame, out MmdCameraState cameraState))
                {
                    MmdUnityCameraPose pose = MmdCameraStateToUnity.Convert(cameraState, importScale: 0.1f);
                    Camera main = Camera.main;
                    if (main != null)
                    {
                        main.transform.position = pose.Position;
                        main.transform.rotation = pose.Rotation;
                        main.fieldOfView = pose.FieldOfView;
                    }
                }
            }

            if (audioSource != null && audioSource.clip != null && playbackController != null)
            {
                float frameRate = arguments?.FrameRate ?? 30.0f;
                float audioTime = (playbackController.CurrentFrame - audioOffsetFrame) / frameRate;
                if (playbackController.IsPlaying && audioTime >= 0.0f)
                {
                    if (!audioSource.isPlaying)
                    {
                        audioSource.time = audioTime;
                        audioSource.Play();
                    }
                }
                else
                {
                    if (audioSource.isPlaying)
                    {
                        audioSource.Pause();
                    }
                }
            }

            if (!cameraVmdActive)
            {
                Quaternion rotation = Quaternion.Euler(orbitPitch, orbitYaw, 0.0f);
                Vector3 offset = rotation * new Vector3(0.0f, 0.0f, -orbitDistance);
                Camera main = Camera.main;
                if (main != null)
                {
                    main.transform.position = orbitTarget + offset;
                    main.transform.LookAt(orbitTarget);
                }

                if (Input.mousePosition.x < 384.0f)
                {
                    orbitDragging = false;
                    panDragging = false;
                    return;
                }

                Vector2 mousePosition = Input.mousePosition;

                if (Input.GetMouseButtonDown(1))
                {
                    orbitDragging = true;
                    lastMousePosition = mousePosition;
                }

                if (Input.GetMouseButtonUp(1))
                {
                    orbitDragging = false;
                }

                if (orbitDragging && Input.GetMouseButton(1))
                {
                    Vector2 delta = mousePosition - lastMousePosition;
                    orbitYaw += delta.x * 0.3f;
                    orbitPitch -= delta.y * 0.3f;
                    orbitPitch = Mathf.Clamp(orbitPitch, -89.0f, 89.0f);
                    lastMousePosition = mousePosition;
                }

                if (Input.GetMouseButtonDown(2))
                {
                    panDragging = true;
                    lastMousePosition = mousePosition;
                }

                if (Input.GetMouseButtonUp(2))
                {
                    panDragging = false;
                }

                if (panDragging && Input.GetMouseButton(2))
                {
                    Vector2 delta = mousePosition - lastMousePosition;
                    float panScale = orbitDistance * 0.002f;
                    Vector3 right = rotation * Vector3.right;
                    Vector3 up = rotation * Vector3.up;
                    orbitTarget -= right * delta.x * panScale;
                    orbitTarget += up * delta.y * panScale;
                    lastMousePosition = mousePosition;
                }

                float scrollDelta = Input.mouseScrollDelta.y;
                if (Mathf.Abs(scrollDelta) > 0.01f)
                {
                    orbitDistance *= 1.0f - scrollDelta * 0.1f;
                    orbitDistance = Mathf.Clamp(orbitDistance, 0.1f, 100.0f);
                }
            }
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

            if (cameraSampler != null)
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button(cameraVmdActive ? "Camera: VMD" : "Camera: Free", GUILayout.Width(120.0f)))
                {
                    cameraVmdActive = !cameraVmdActive;
                    if (!cameraVmdActive)
                    {
                        AutoCenterCamera();
                    }
                }

                GUILayout.EndHorizontal();
            }
            else
            {
                GUILayout.Label("Camera: Free");
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

            if (recentEntries.entries.Count > 0)
            {
                GUILayout.Space(6.0f);
                GUILayout.BeginHorizontal();
                GUILayout.Label("Recents");
                if (GUILayout.Button("Clear", GUILayout.Width(50.0f)))
                {
                    recentEntries.entries.Clear();
                    SaveRecents();
                }

                GUILayout.EndHorizontal();
                recentsScroll = GUILayout.BeginScrollView(recentsScroll, GUILayout.Height(Mathf.Min(120.0f, recentEntries.entries.Count * 22.0f)));
                for (int i = 0; i < recentEntries.entries.Count; i++)
                {
                    MmdRecentEntry entry = recentEntries.entries[i];
                    if (!File.Exists(entry.pmxPath))
                    {
                        GUI.enabled = false;
                    }

                    if (GUILayout.Button(entry.name))
                    {
                        try
                        {
                            selectedIndex = -1;
                            StartPlayback(entry.pmxPath, entry.vmdPath, entry.name,
                                entry.cameraPath,
                                entry.audioPath,
                                entry.backgroundPath,
                                entry.audioOffsetFrame,
                                ResolveRecentMaterialPreset(entry));
                            AddStatus("Playing (recent): " + entry.name);
                        }
                        catch (Exception ex)
                        {
                            ClearPlayback();
                            AddStatus("Load failed: " + ex.GetType().Name + ": " + ex.Message);
                        }
                    }

                    GUI.enabled = true;
                }

                GUILayout.EndScrollView();
            }

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
                        verificationCases[i].ParseOnly ? "parse-only" : string.Empty,
                        materialPreset: verificationCases[i].MaterialPreset);
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
                StartPlayback(
                    selected.PmxPath,
                    selected.VmdPath,
                    selected.Name,
                    selected.CameraPath,
                    selected.AudioPath,
                    selected.BackgroundPath,
                    selected.AudioOffsetFrame,
                    selected.MaterialPreset);
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

        private void StartPlayback(
            string pmxPath,
            string vmdPath,
            string displayName,
            string cameraPath = "",
            string audioPath = "",
            string backgroundPath = "",
            float audioOffset = 0.0f,
            MmdMaterialPreset materialPreset = MmdMaterialPreset.MmdToon)
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
                allowRuntimeFallback: true,
                materialPreset: materialPreset);
            playbackController.SetPhysicsMode(MmdPhysicsMode.Live);
            if (!arguments.FastRuntimeEnabled)
            {
                playbackController.DisableFastRuntime();
            }

            playbackController.Play();
            AutoCenterCamera();

            cameraSampler?.Dispose();
            cameraSampler = null;
            cameraVmdActive = false;
            if (!string.IsNullOrWhiteSpace(cameraPath) && File.Exists(cameraPath))
            {
                byte[] cameraVmdBytes = File.ReadAllBytes(cameraPath);
                if (NativeVmdCameraTrackSampler.TryCreate(cameraVmdBytes, out NativeVmdCameraTrackSampler? sampler))
                {
                    cameraSampler = sampler;
                    cameraVmdActive = true;
                    AddStatus("Camera VMD loaded.");
                }
            }

            if (audioSource != null)
            {
                Destroy(audioSource);
                audioSource = null;
            }

            audioOffsetFrame = audioOffset;
            if (!string.IsNullOrWhiteSpace(audioPath) && File.Exists(audioPath))
            {
                StartCoroutine(LoadAudioClip(audioPath));
            }

            if (backgroundRoot != null)
            {
                Destroy(backgroundRoot);
                backgroundRoot = null;
            }

            if (!string.IsNullOrWhiteSpace(backgroundPath) && File.Exists(backgroundPath))
            {
                LoadBackground(backgroundPath);
            }

            AddRecent(displayName, pmxPath, vmdPath, cameraPath, audioPath, backgroundPath, audioOffset, materialPreset);
        }

        private void AutoCenterCamera()
        {
            if (playbackRoot == null)
            {
                return;
            }

            Renderer[] renderers = playbackRoot.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                return;
            }

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            orbitTarget = bounds.center;
            orbitDistance = bounds.size.magnitude * 1.5f;
            orbitPitch = 15.0f;
            orbitYaw = 180.0f;
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
            cameraSampler?.Dispose();
            cameraSampler = null;
            cameraVmdActive = false;
            if (audioSource != null)
            {
                audioSource.Stop();
                audioSource = null;
            }

            audioOffsetFrame = 0.0f;
            playbackController = null;
            if (backgroundRoot != null)
            {
                Destroy(backgroundRoot);
                backgroundRoot = null;
            }

            if (playbackRoot != null)
            {
                Destroy(playbackRoot);
                playbackRoot = null;
            }
        }

        private IEnumerator LoadAudioClip(string path)
        {
            GameObject expectedRoot = playbackRoot;
            string uri = "file:///" + path.Replace("\\", "/");
            using UnityWebRequest request = UnityWebRequestMultimedia.GetAudioClip(uri, AudioType.UNKNOWN);
            yield return request.SendWebRequest();
            if (request.result != UnityWebRequest.Result.Success)
            {
                AddStatus("Audio load failed: " + request.error);
                yield break;
            }

            if (playbackRoot != expectedRoot || playbackRoot == null)
            {
                yield break;
            }

            AudioClip clip = DownloadHandlerAudioClip.GetContent(request);
            audioSource = playbackRoot.AddComponent<AudioSource>();
            audioSource.clip = clip;
            audioSource.playOnAwake = false;
            AddStatus("Audio loaded: " + Path.GetFileName(path));
        }

        private void LoadBackground(string path)
        {
            try
            {
                string extension = Path.GetExtension(path).ToLowerInvariant();
                if (extension == ".pmx")
                {
                    byte[] pmxBytes = File.ReadAllBytes(path);
                    var parser = new NativeMmdParser();
                    MmdModelDefinition model = parser.LoadModel(pmxBytes);
                    backgroundRoot = new GameObject("MMD Background: " + Path.GetFileName(path));
                    MmdUnityModelInstance instance = MmdUnityModelFactory.CreateSkinnedModel(model, path, importScale: 0.1f);
                    instance.Root.transform.SetParent(backgroundRoot.transform, worldPositionStays: false);
                    AddStatus("Background loaded: " + Path.GetFileName(path));
                }
            }
            catch (Exception ex)
            {
                AddStatus("Background load failed: " + ex.GetType().Name + ": " + ex.Message);
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
            GUILayout.Label("Material preset: " + selected.MaterialPreset);
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

        private string GetRecentsFilePath()
        {
            return Path.Combine(Application.persistentDataPath, RecentsFileName);
        }

        private void LoadRecents()
        {
            string path = GetRecentsFilePath();
            if (!File.Exists(path))
            {
                recentEntries = new MmdRecentEntryList();
                return;
            }

            try
            {
                string json = File.ReadAllText(path);
                recentEntries = JsonUtility.FromJson<MmdRecentEntryList>(json) ?? new MmdRecentEntryList();
            }
            catch (Exception)
            {
                recentEntries = new MmdRecentEntryList();
            }
        }

        private void SaveRecents()
        {
            try
            {
                string json = JsonUtility.ToJson(recentEntries, prettyPrint: true);
                string path = GetRecentsFilePath();
                string? directory = Path.GetDirectoryName(path);
                if (directory != null && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(path, json);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[RuntimeViewer] Failed to save recents: " + ex.Message);
            }
        }

        private static MmdMaterialPreset ResolveRecentMaterialPreset(MmdRecentEntry entry)
        {
            return MmdRuntimeVerificationArguments.TryParseMaterialPreset(entry.materialPreset, out MmdMaterialPreset preset)
                ? preset
                : MmdMaterialPreset.MmdToon;
        }

        private void AddRecent(string name, string pmxPath, string vmdPath,
            string cameraPath = "",
            string audioPath = "",
            string backgroundPath = "",
            float audioOffset = 0.0f,
            MmdMaterialPreset materialPreset = MmdMaterialPreset.MmdToon)
        {
            recentEntries.entries.RemoveAll(e =>
                string.Equals(e.pmxPath, pmxPath, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(e.vmdPath, vmdPath, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(e.cameraPath, cameraPath, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(e.audioPath, audioPath, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(e.backgroundPath, backgroundPath, StringComparison.OrdinalIgnoreCase));

            var entry = new MmdRecentEntry
            {
                name = name,
                pmxPath = pmxPath,
                vmdPath = vmdPath,
                cameraPath = cameraPath,
                audioPath = audioPath,
                backgroundPath = backgroundPath,
                materialPreset = materialPreset.ToString(),
                audioOffsetFrame = audioOffset,
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };

            recentEntries.entries.Insert(0, entry);

            if (recentEntries.entries.Count > MaxRecentEntries)
            {
                recentEntries.entries.RemoveRange(MaxRecentEntries, recentEntries.entries.Count - MaxRecentEntries);
            }

            SaveRecents();
        }
    }
}
