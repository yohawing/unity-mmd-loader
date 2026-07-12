#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Mmd.Motion;
using Mmd.Parser;
using Mmd.Physics;
using Mmd.Rendering;
using Mmd.UnityIntegration;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UIElements;

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
        private const string ViewerCaseRootPrefix = "MMD Runtime Viewer Case: ";

        private readonly List<string> statusLines = new();
        private MmdRuntimeVerificationArguments? arguments;
        private MmdRuntimeViewerFixtureCase[] cases = Array.Empty<MmdRuntimeViewerFixtureCase>();
        private GameObject? playbackRoot;
        private MmdUnityPlaybackController? playbackController;
        private readonly List<GameObject> playbackRoots = new();
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
        private bool isLoading;
        private string loadingLabel = string.Empty;
        private UIDocument? uiDocument;
        private PanelSettings? ownedPanelSettings;
        private VisualElement? uiRoot;
        private Label? errorLabel;
        private Label? loadingStatusLabel;
        private Button? playButton;
        private Label? frameLabel;
        private Slider? frameSlider;
        private Button? cameraButton;
        private TextField? pmxField;
        private TextField? vmdField;
        private Button? loadButton;
        private Button? closePanelButton;
        private Button? openPanelButton;
        private VisualElement? casesList;
        private VisualElement? recentsSection;
        private VisualElement? recentsList;
        private Label? selectedDetailsLabel;
        private Label? statusLabel;
        private bool runtimeUiReady;
        private bool suppressFrameSliderChange;
        private bool panelCollapsed;
        private Transform? editorPreviewOwnershipRoot;

        public void ConfigureEditorPreviewOwnership(Transform ownershipRoot)
        {
            editorPreviewOwnershipRoot = ownershipRoot != null
                ? ownershipRoot
                : throw new ArgumentNullException(nameof(ownershipRoot));
            ApplyEditorPreviewOwnership(ownershipRoot.gameObject);
        }

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
            BuildRuntimeUi();
            if (cases.Length > 0)
            {
                SelectCase(0);
            }
            else
            {
                AddStatus("No cases loaded. Enter PMX/VMD paths below and click Load.");
            }

            RefreshRuntimeUi();
        }

        private void OnDestroy()
        {
            ClearPlayback();
            if (ownedPanelSettings != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(ownedPanelSettings);
                }
                else
                {
                    DestroyImmediate(ownedPanelSettings);
                }

                ownedPanelSettings = null;
            }

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

                if (!TryReadMouse(out ViewerMouseState mouse))
                {
                    orbitDragging = false;
                    panDragging = false;
                    RefreshRuntimeUi();
                    return;
                }

                if (mouse.position.x < 384.0f)
                {
                    orbitDragging = false;
                    panDragging = false;
                    RefreshRuntimeUi();
                    return;
                }

                Vector2 mousePosition = mouse.position;

                if (mouse.rightDown)
                {
                    orbitDragging = true;
                    lastMousePosition = mousePosition;
                }

                if (mouse.rightUp)
                {
                    orbitDragging = false;
                }

                if (orbitDragging && mouse.rightHeld)
                {
                    Vector2 delta = mousePosition - lastMousePosition;
                    orbitYaw += delta.x * 0.3f;
                    orbitPitch -= delta.y * 0.3f;
                    orbitPitch = Mathf.Clamp(orbitPitch, -89.0f, 89.0f);
                    lastMousePosition = mousePosition;
                }

                if (mouse.middleDown)
                {
                    panDragging = true;
                    lastMousePosition = mousePosition;
                }

                if (mouse.middleUp)
                {
                    panDragging = false;
                }

                if (panDragging && mouse.middleHeld)
                {
                    Vector2 delta = mousePosition - lastMousePosition;
                    float panScale = orbitDistance * 0.002f;
                    Vector3 right = rotation * Vector3.right;
                    Vector3 up = rotation * Vector3.up;
                    orbitTarget -= right * delta.x * panScale;
                    orbitTarget += up * delta.y * panScale;
                    lastMousePosition = mousePosition;
                }

                float scrollDelta = mouse.scrollY;
                if (Mathf.Abs(scrollDelta) > 0.01f)
                {
                    orbitDistance *= 1.0f - scrollDelta * 0.1f;
                    orbitDistance = Mathf.Clamp(orbitDistance, 0.1f, 100.0f);
                }
            }

            RefreshRuntimeUi();
        }

        private void BuildRuntimeUi()
        {
            try
            {
                uiDocument = gameObject.GetComponent<UIDocument>() ?? gameObject.AddComponent<UIDocument>();
                if (uiDocument.panelSettings == null)
                {
                    PanelSettings panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
                    panelSettings.name = "MMD Runtime Viewer Panel Settings";
                    panelSettings.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
                    panelSettings.themeStyleSheet = Resources.Load<ThemeStyleSheet>("MmdRuntimeViewerTheme");
                    uiDocument.panelSettings = panelSettings;
                    ownedPanelSettings = panelSettings;
                }

                uiRoot = uiDocument.rootVisualElement;
                uiRoot.Clear();

                VisualTreeAsset? tree = Resources.Load<VisualTreeAsset>("MmdRuntimeViewer");
                if (tree != null)
                {
                    tree.CloneTree(uiRoot);
                }
                else
                {
                    BuildRuntimeUiFallback(uiRoot);
                }

                StyleSheet? styleSheet = Resources.Load<StyleSheet>("MmdRuntimeViewer");
                if (styleSheet != null)
                {
                    uiRoot.styleSheets.Add(styleSheet);
                }

                BindRuntimeUi(uiRoot);
                ApplyRuntimeUiDefaultStyles(uiRoot);
                runtimeUiReady = true;
                RefreshCasesUi();
                RefreshRecentsUi();
            }
            catch (Exception ex)
            {
                runtimeUiReady = false;
                Debug.LogWarning("[RuntimeViewer] UI Toolkit setup failed; falling back to IMGUI: " + ex.Message);
            }
        }

        private void BuildRuntimeUiFallback(VisualElement root)
        {
            var panel = new VisualElement { name = "viewer-panel" };
            panel.AddToClassList("viewer-panel");
            root.Add(panel);

            root.Add(new Button { name = "open-panel-button", text = "Viewer" });
            var titleRow = new VisualElement { name = "title-row" };
            titleRow.AddToClassList("row");
            titleRow.AddToClassList("split-row");
            titleRow.Add(new Label("Runtime Viewer") { name = "viewer-title" });
            titleRow.Add(new Button { name = "close-panel-button", text = "X" });
            panel.Add(titleRow);
            panel.Add(new Label { name = "error-label" });
            panel.Add(new Label { name = "loading-label" });

            var transport = new VisualElement { name = "transport-row" };
            transport.AddToClassList("row");
            transport.Add(new Button { name = "reload-button", text = "Reload" });
            transport.Add(new Button { name = "play-button", text = "Play" });
            transport.Add(new Button { name = "stop-button", text = "Stop" });
            panel.Add(transport);

            panel.Add(new Slider { name = "frame-slider", lowValue = 0.0f, highValue = 1.0f });
            panel.Add(new Label { name = "frame-label" });
            panel.Add(new Button { name = "camera-button", text = "Camera: Free" });

            panel.Add(new Label("Load Files"));
            panel.Add(new TextField("PMX") { name = "pmx-field" });
            panel.Add(new TextField("VMD") { name = "vmd-field" });
            panel.Add(new Button { name = "load-button", text = "Load" });

            panel.Add(new Label("Cases"));
            panel.Add(new ScrollView { name = "cases-list" });
            var recents = new VisualElement { name = "recents-section" };
            recents.Add(new Label("Recents"));
            recents.Add(new Button { name = "clear-recents-button", text = "Clear" });
            recents.Add(new ScrollView { name = "recents-list" });
            panel.Add(recents);

            panel.Add(new Label { name = "selected-details" });
            panel.Add(new Label("Status"));
            panel.Add(new Label { name = "status-label" });
        }

        private void BindRuntimeUi(VisualElement root)
        {
            errorLabel = root.Q<Label>("error-label");
            loadingStatusLabel = root.Q<Label>("loading-label");
            playButton = root.Q<Button>("play-button");
            frameLabel = root.Q<Label>("frame-label");
            frameSlider = root.Q<Slider>("frame-slider");
            cameraButton = root.Q<Button>("camera-button");
            pmxField = root.Q<TextField>("pmx-field");
            vmdField = root.Q<TextField>("vmd-field");
            loadButton = root.Q<Button>("load-button");
            closePanelButton = root.Q<Button>("close-panel-button");
            openPanelButton = root.Q<Button>("open-panel-button");
            casesList = root.Q<VisualElement>("cases-list");
            recentsSection = root.Q<VisualElement>("recents-section");
            recentsList = root.Q<VisualElement>("recents-list");
            selectedDetailsLabel = root.Q<Label>("selected-details");
            statusLabel = root.Q<Label>("status-label");

            root.Q<Button>("reload-button")?.RegisterCallback<ClickEvent>(_ => ReloadCases());
            playButton?.RegisterCallback<ClickEvent>(_ => TogglePlayback());
            root.Q<Button>("stop-button")?.RegisterCallback<ClickEvent>(_ => StopPlayback());
            cameraButton?.RegisterCallback<ClickEvent>(_ => ToggleCameraMode());
            loadButton?.RegisterCallback<ClickEvent>(_ => LoadFromInput());
            closePanelButton?.RegisterCallback<ClickEvent>(_ => SetPanelCollapsed(true));
            openPanelButton?.RegisterCallback<ClickEvent>(_ => SetPanelCollapsed(false));
            root.Q<Button>("clear-recents-button")?.RegisterCallback<ClickEvent>(_ => ClearRecents());
#if UNITY_EDITOR
            root.Q<Button>("pmx-browse-button")?.RegisterCallback<ClickEvent>(_ => BrowseIntoField(pmxField, "Select PMX file", "pmx"));
            root.Q<Button>("vmd-browse-button")?.RegisterCallback<ClickEvent>(_ => BrowseIntoField(vmdField, "Select VMD file", "vmd"));
#endif

            pmxField?.RegisterValueChangedCallback(evt => pmxInput = evt.newValue ?? string.Empty);
            vmdField?.RegisterValueChangedCallback(evt => vmdInput = evt.newValue ?? string.Empty);
            frameSlider?.RegisterValueChangedCallback(OnFrameSliderChanged);
        }

        private static void ApplyRuntimeUiDefaultStyles(VisualElement root)
        {
            Font? runtimeFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ??
                Resources.GetBuiltinResource<Font>("Arial.ttf");
            VisualElement? panel = root.Q<VisualElement>("viewer-panel");
            if (panel != null)
            {
                panel.style.position = Position.Absolute;
                panel.style.left = 12.0f;
                panel.style.top = 12.0f;
                panel.style.width = 360.0f;
                panel.style.height = Length.Percent(96.0f);
                panel.style.paddingLeft = 10.0f;
                panel.style.paddingRight = 10.0f;
                panel.style.paddingTop = 10.0f;
                panel.style.paddingBottom = 10.0f;
                panel.style.backgroundColor = new Color(0.095f, 0.105f, 0.12f, 0.92f);
            }

            foreach (Label label in root.Query<Label>().ToList())
            {
                label.style.color = new Color(0.90f, 0.92f, 0.94f, 1.0f);
                label.style.fontSize = 13.0f;
                label.style.whiteSpace = WhiteSpace.Normal;
                label.style.unityTextAlign = TextAnchor.MiddleLeft;
                label.style.minHeight = 18.0f;
                if (runtimeFont != null)
                {
                    label.style.unityFont = runtimeFont;
                }
            }

            foreach (Button button in root.Query<Button>().ToList())
            {
                button.style.minHeight = 24.0f;
                button.style.marginBottom = 3.0f;
                button.style.color = new Color(0.96f, 0.97f, 0.98f, 1.0f);
                button.style.fontSize = 13.0f;
                button.style.backgroundColor = new Color(0.21f, 0.24f, 0.28f, 1.0f);
                if (runtimeFont != null)
                {
                    button.style.unityFont = runtimeFont;
                }
            }

            Button? closeButton = root.Q<Button>("close-panel-button");
            if (closeButton != null)
            {
                closeButton.style.width = 28.0f;
            }

            Button? openButton = root.Q<Button>("open-panel-button");
            if (openButton != null)
            {
                openButton.style.position = Position.Absolute;
                openButton.style.left = 12.0f;
                openButton.style.top = 12.0f;
                openButton.style.width = 74.0f;
            }

            foreach (TextField field in root.Query<TextField>().ToList())
            {
                field.style.minHeight = 24.0f;
                field.style.color = Color.white;
                field.style.fontSize = 13.0f;
                if (runtimeFont != null)
                {
                    field.style.unityFont = runtimeFont;
                }
            }

            foreach (ScrollView scrollView in root.Query<ScrollView>().ToList())
            {
                scrollView.style.minHeight = 80.0f;
            }
        }

        private void RefreshRuntimeUi()
        {
            if (!runtimeUiReady)
            {
                return;
            }

            RefreshRuntimePanelBounds();
            RefreshPanelVisibility();

            if (errorLabel != null)
            {
                string errors = arguments == null
                    ? "Viewer is not initialized."
                    : string.Join(Environment.NewLine, arguments.Errors);
                errorLabel.text = errors;
                errorLabel.style.display = string.IsNullOrWhiteSpace(errors) ? DisplayStyle.None : DisplayStyle.Flex;
            }

            if (loadingStatusLabel != null)
            {
                loadingStatusLabel.text = string.IsNullOrWhiteSpace(loadingLabel) ? "Loading..." : loadingLabel;
                loadingStatusLabel.style.display = isLoading ? DisplayStyle.Flex : DisplayStyle.None;
            }

            if (pmxField != null && pmxField.value != pmxInput)
            {
                pmxField.SetValueWithoutNotify(pmxInput);
            }

            if (vmdField != null && vmdField.value != vmdInput)
            {
                vmdField.SetValueWithoutNotify(vmdInput);
            }

            if (loadButton != null)
            {
                loadButton.SetEnabled(!isLoading);
            }

            if (playButton != null)
            {
                playButton.text = playbackController != null && playbackController.IsPlaying ? "Pause" : "Play";
            }

            RefreshFrameUi();
            RefreshCameraUi();
            RefreshSelectedDetailsUi();
            RefreshStatusUi();
        }

        private void RefreshRuntimePanelBounds()
        {
            VisualElement? panel = uiRoot?.Q<VisualElement>("viewer-panel");
            if (panel == null)
            {
                return;
            }

            panel.style.height = Mathf.Max(560.0f, Screen.height - 24.0f);
            panel.style.maxHeight = Mathf.Max(560.0f, Screen.height - 24.0f);
            panel.style.backgroundColor = new Color(0.095f, 0.105f, 0.12f, 0.92f);
        }

        private void RefreshPanelVisibility()
        {
            VisualElement? panel = uiRoot?.Q<VisualElement>("viewer-panel");
            if (panel != null)
            {
                panel.style.display = panelCollapsed ? DisplayStyle.None : DisplayStyle.Flex;
            }

            if (openPanelButton != null)
            {
                openPanelButton.style.display = panelCollapsed ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        private void SetPanelCollapsed(bool collapsed)
        {
            panelCollapsed = collapsed;
            RefreshRuntimeUi();
        }

        private void RefreshFrameUi()
        {
            if (frameSlider == null || frameLabel == null)
            {
                return;
            }

            if (playbackController == null)
            {
                frameSlider.style.display = DisplayStyle.None;
                frameLabel.text = string.Empty;
                return;
            }

            frameSlider.style.display = DisplayStyle.Flex;
            int maxFrame = playbackController.MotionMaxFrame;
            int currentFrame = playbackController.CurrentFrame;
            frameSlider.lowValue = 0.0f;
            frameSlider.highValue = Mathf.Max(1, maxFrame);
            suppressFrameSliderChange = true;
            frameSlider.SetValueWithoutNotify(Mathf.Clamp(currentFrame, 0, Mathf.Max(1, maxFrame)));
            suppressFrameSliderChange = false;
            frameLabel.text = maxFrame > 0
                ? "Frame: " + currentFrame + " / " + maxFrame
                : "Frame: " + currentFrame;
        }

        private void RefreshCameraUi()
        {
            if (cameraButton == null)
            {
                return;
            }

            cameraButton.text = cameraSampler != null
                ? (cameraVmdActive ? "Camera: VMD" : "Camera: Free")
                : "Camera: Free";
            cameraButton.SetEnabled(cameraSampler != null);
        }

        private void RefreshCasesUi()
        {
            if (casesList == null)
            {
                return;
            }

            casesList.Clear();
            for (int i = 0; i < cases.Length; i++)
            {
                int caseIndex = i;
                var button = new Button(() => SelectCase(caseIndex))
                {
                    text = i == selectedIndex ? "> " + cases[i].Name : cases[i].Name
                };
                button.AddToClassList("list-button");
                button.SetEnabled(!isLoading);
                casesList.Add(button);
            }
        }

        private void RefreshRecentsUi()
        {
            if (recentsSection == null || recentsList == null)
            {
                return;
            }

            recentsSection.style.display = recentEntries.entries.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            recentsList.Clear();
            for (int i = 0; i < recentEntries.entries.Count; i++)
            {
                MmdRecentEntry entry = recentEntries.entries[i];
                var button = new Button(() => LoadRecent(entry))
                {
                    text = entry.name
                };
                button.AddToClassList("list-button");
                button.SetEnabled(!isLoading && File.Exists(entry.pmxPath));
                recentsList.Add(button);
            }
        }

        private void RefreshSelectedDetailsUi()
        {
            if (selectedDetailsLabel == null)
            {
                return;
            }

            selectedDetailsLabel.text = BuildSelectedDetailsText();
        }

        private string BuildSelectedDetailsText()
        {
            if (selectedIndex < 0 || selectedIndex >= cases.Length)
            {
                if (playbackController != null)
                {
                    return "Loaded from file input." + Environment.NewLine +
                        "Frame: " + playbackController.CurrentFrame + Environment.NewLine +
                        "Fast runtime: " + playbackController.IsFastRuntimeEnabled;
                }

                return "No case selected.";
            }

            MmdRuntimeViewerFixtureCase selected = cases[selectedIndex];
            var lines = new List<string>
            {
                "Selected: " + selected.Name,
                BuildPathLine("PMX", selected.PmxPath),
                BuildPathLine("VMD", selected.VmdPath),
                BuildPathLine("Camera", selected.CameraPath),
                BuildPathLine("Audio", selected.AudioPath),
                BuildPathLine("Background", selected.BackgroundPath),
                "Material preset: " + selected.MaterialPreset
            };

            if (Math.Abs(selected.AudioOffsetFrame) > float.Epsilon)
            {
                lines.Add("Audio offset frame: " + selected.AudioOffsetFrame.ToString("0.###"));
            }

            if (playbackController != null)
            {
                lines.Add("Frame: " + playbackController.CurrentFrame);
                lines.Add("Fast runtime: " + playbackController.IsFastRuntimeEnabled);
            }

            lines.RemoveAll(string.IsNullOrWhiteSpace);
            return string.Join(Environment.NewLine, lines);
        }

        private void RefreshStatusUi()
        {
            if (statusLabel == null)
            {
                return;
            }

            int start = Math.Max(0, statusLines.Count - 6);
            statusLabel.text = string.Join(Environment.NewLine, statusLines.GetRange(start, statusLines.Count - start));
        }

        private void OnFrameSliderChanged(ChangeEvent<float> evt)
        {
            if (suppressFrameSliderChange || playbackController == null)
            {
                return;
            }

            int seekTarget = Mathf.RoundToInt(evt.newValue);
            if (seekTarget == playbackController.CurrentFrame)
            {
                return;
            }

            if (playbackController.IsPlaying)
            {
                playbackController.Pause();
            }

            playbackController.SeekFrame(seekTarget);
            RefreshRuntimeUi();
        }

        private void ToggleCameraMode()
        {
            if (cameraSampler == null)
            {
                return;
            }

            cameraVmdActive = !cameraVmdActive;
            if (!cameraVmdActive)
            {
                AutoCenterCamera();
            }

            RefreshRuntimeUi();
        }

        private void LoadRecent(MmdRecentEntry entry)
        {
            selectedIndex = -1;
            QueuePlayback(
                entry.pmxPath,
                entry.vmdPath,
                entry.name,
                "Playing (recent): " + entry.name,
                entry.cameraPath,
                entry.audioPath,
                entry.backgroundPath,
                entry.audioOffsetFrame,
                ResolveRecentMaterialPreset(entry));
        }

        private void ClearRecents()
        {
            recentEntries.entries.Clear();
            SaveRecents();
            RefreshRecentsUi();
        }

        private static string BuildPathLine(string label, string path)
        {
            return string.IsNullOrWhiteSpace(path) ? string.Empty : label + ": " + Path.GetFileName(path);
        }

#if UNITY_EDITOR
        private static void BrowseIntoField(TextField? field, string title, string extension)
        {
            if (field == null || BrowseFileOverride == null)
            {
                return;
            }

            string result = BrowseFileOverride(title, extension);
            if (!string.IsNullOrWhiteSpace(result))
            {
                field.value = result;
            }
        }
#endif

        private void OnGUI()
        {
            if (runtimeUiReady)
            {
                return;
            }

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

            if (isLoading)
            {
                GUILayout.Label(string.IsNullOrWhiteSpace(loadingLabel) ? "Loading..." : loadingLabel);
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
                    bool previousEnabled = GUI.enabled;
                    if (!File.Exists(entry.pmxPath) || isLoading)
                    {
                        GUI.enabled = false;
                    }

                    if (GUILayout.Button(entry.name))
                    {
                        selectedIndex = -1;
                        QueuePlayback(
                            entry.pmxPath,
                            entry.vmdPath,
                            entry.name,
                            "Playing (recent): " + entry.name,
                            entry.cameraPath,
                            entry.audioPath,
                            entry.backgroundPath,
                            entry.audioOffsetFrame,
                            ResolveRecentMaterialPreset(entry));
                    }

                    GUI.enabled = previousEnabled;
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
            if (isLoading)
            {
                AddStatus("Load already in progress.");
                return;
            }

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
            RefreshCasesUi();
            RefreshRecentsUi();
        }

        private void SelectCase(int index)
        {
            if (isLoading)
            {
                AddStatus("Load already in progress.");
                return;
            }

            if (arguments == null || index < 0 || index >= cases.Length)
            {
                return;
            }

            selectedIndex = index;
            RefreshCasesUi();
            MmdRuntimeViewerFixtureCase selected = cases[index];
            if (!string.IsNullOrWhiteSpace(selected.SkipReason))
            {
                ClearPlayback();
                AddStatus("Skipped: " + selected.SkipReason);
                return;
            }

            try
            {
                QueuePlayback(
                    selected.PmxPath,
                    selected.VmdPath,
                    selected.Name,
                    "Playing: " + selected.Name,
                    selected.CameraPath,
                    selected.AudioPath,
                    selected.BackgroundPath,
                    selected.AudioOffsetFrame,
                    selected.MaterialPreset);
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

            bool previousEnabled = GUI.enabled;
            GUI.enabled = previousEnabled && !isLoading;
            if (GUILayout.Button("Load", GUILayout.Width(88.0f)))
            {
                LoadFromInput();
            }
            GUI.enabled = previousEnabled;
        }

        private void LoadFromInput()
        {
            if (isLoading)
            {
                AddStatus("Load already in progress.");
                return;
            }

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
            selectedIndex = -1;
            QueuePlayback(pmxPath, vmdPath, displayName, "Playing: " + displayName);
        }

        private void QueuePlayback(
            string pmxPath,
            string vmdPath,
            string displayName,
            string successStatus,
            string cameraPath = "",
            string audioPath = "",
            string backgroundPath = "",
            float audioOffset = 0.0f,
            MmdMaterialPreset materialPreset = MmdMaterialPreset.MmdToon)
        {
            if (isLoading)
            {
                AddStatus("Load already in progress.");
                return;
            }

            isLoading = true;
            loadingLabel = "Loading: " + displayName;
            AddStatus(loadingLabel);
            if (Application.isPlaying)
            {
                StartCoroutine(LoadPlaybackAfterUiFrame(
                    pmxPath,
                    vmdPath,
                    displayName,
                    successStatus,
                    cameraPath,
                    audioPath,
                    backgroundPath,
                    audioOffset,
                    materialPreset));
            }
            else
            {
                CompletePlaybackLoad(
                    pmxPath,
                    vmdPath,
                    displayName,
                    successStatus,
                    cameraPath,
                    audioPath,
                    backgroundPath,
                    audioOffset,
                    materialPreset);
            }
        }

        private IEnumerator LoadPlaybackAfterUiFrame(
            string pmxPath,
            string vmdPath,
            string displayName,
            string successStatus,
            string cameraPath,
            string audioPath,
            string backgroundPath,
            float audioOffset,
            MmdMaterialPreset materialPreset)
        {
            yield return null;

            CompletePlaybackLoad(
                pmxPath,
                vmdPath,
                displayName,
                successStatus,
                cameraPath,
                audioPath,
                backgroundPath,
                audioOffset,
                materialPreset);
        }

        private void CompletePlaybackLoad(
            string pmxPath,
            string vmdPath,
            string displayName,
            string successStatus,
            string cameraPath,
            string audioPath,
            string backgroundPath,
            float audioOffset,
            MmdMaterialPreset materialPreset)
        {
            try
            {
                StartPlayback(
                    pmxPath,
                    vmdPath,
                    displayName,
                    cameraPath,
                    audioPath,
                    backgroundPath,
                    audioOffset,
                    materialPreset);
                AddStatus(successStatus);
            }
            catch (Exception ex)
            {
                ClearPlayback();
                AddStatus("Load failed: " + ex.GetType().Name + ": " + ex.Message);
            }
            finally
            {
                isLoading = false;
                loadingLabel = string.Empty;
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
            var holder = new GameObject(ViewerCaseRootPrefix + displayName);
            ApplyEditorPreviewOwnership(holder);
            playbackRoot = holder;
            playbackRoots.Add(holder);
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
            AdoptConfiguredInstanceRoot(holder, playbackController.ConfiguredInstanceRoot);
            ApplyEditorPreviewOwnership(holder);
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
                if (Application.isPlaying)
                {
                    StartCoroutine(LoadAudioClip(audioPath));
                }
                else
                {
                    AddStatus("Audio preview requires Play Mode.");
                }
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
            RefreshRuntimeUi();
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
                DestroyViewerObject(backgroundRoot);
                backgroundRoot = null;
            }

            if (playbackRoot != null)
            {
                DestroyViewerObject(playbackRoot);
                playbackRoot = null;
            }

            for (int i = playbackRoots.Count - 1; i >= 0; i--)
            {
                GameObject root = playbackRoots[i];
                if (root != null)
                {
                    DestroyViewerObject(root);
                }
            }

            playbackRoots.Clear();
            DestroyOrphanedViewerRoots();
        }

        private IEnumerator LoadAudioClip(string path)
        {
            GameObject? expectedRoot = playbackRoot;
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
                    ApplyEditorPreviewOwnership(backgroundRoot);
                    playbackRoots.Add(backgroundRoot);
                    MmdUnityModelInstance instance = MmdUnityModelFactory.CreateSkinnedModel(model, path, importScale: 0.1f);
                    instance.Root.transform.SetParent(backgroundRoot.transform, worldPositionStays: false);
                    ApplyEditorPreviewOwnership(backgroundRoot);
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
            RefreshStatusUi();
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
            RefreshRecentsUi();
        }

        private static void AdoptConfiguredInstanceRoot(GameObject holder, GameObject? configuredRoot)
        {
            if (configuredRoot == null || configuredRoot == holder)
            {
                return;
            }

            configuredRoot.transform.SetParent(holder.transform, worldPositionStays: true);
        }

        private void ApplyEditorPreviewOwnership(GameObject root)
        {
            if (editorPreviewOwnershipRoot == null || root == null)
            {
                return;
            }

            if (root.transform != editorPreviewOwnershipRoot && root.transform.parent != editorPreviewOwnershipRoot)
            {
                root.transform.SetParent(editorPreviewOwnershipRoot, worldPositionStays: true);
            }

            Transform[] hierarchy = root.GetComponentsInChildren<Transform>(includeInactive: true);
            foreach (Transform item in hierarchy)
            {
                item.gameObject.hideFlags = HideFlags.HideAndDontSave;
                Component[] components = item.GetComponents<Component>();
                foreach (Component component in components)
                {
                    if (component != null)
                    {
                        component.hideFlags = HideFlags.HideAndDontSave;
                    }
                }
            }
        }

        private static void DestroyOrphanedViewerRoots()
        {
            GameObject[] roots = gameObjectSceneRoots();
            for (int i = 0; i < roots.Length; i++)
            {
                GameObject root = roots[i];
                if (root != null && IsViewerOwnedRootName(root.name))
                {
                    DestroyViewerObject(root);
                }
            }
        }

        private static GameObject[] gameObjectSceneRoots()
        {
            var roots = new List<GameObject>();
            GameObject[] allObjects = FindObjectsByType<GameObject>(FindObjectsInactive.Include);
            for (int i = 0; i < allObjects.Length; i++)
            {
                GameObject candidate = allObjects[i];
                if (candidate != null && candidate.transform.parent == null && candidate.scene.IsValid())
                {
                    roots.Add(candidate);
                }
            }

            return roots.ToArray();
        }

        private static bool IsViewerOwnedRootName(string objectName)
        {
            if (string.IsNullOrWhiteSpace(objectName))
            {
                return false;
            }

            return objectName.StartsWith(ViewerCaseRootPrefix, StringComparison.Ordinal) ||
                objectName.StartsWith("MMD Background: ", StringComparison.Ordinal);
        }

        private static void DestroyViewerObject(GameObject target)
        {
            if (target == null)
            {
                return;
            }

            target.SetActive(false);
            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }

        private readonly struct ViewerMouseState
        {
            public ViewerMouseState(
                Vector2 position,
                bool rightDown,
                bool rightHeld,
                bool rightUp,
                bool middleDown,
                bool middleHeld,
                bool middleUp,
                float scrollY)
            {
                this.position = position;
                this.rightDown = rightDown;
                this.rightHeld = rightHeld;
                this.rightUp = rightUp;
                this.middleDown = middleDown;
                this.middleHeld = middleHeld;
                this.middleUp = middleUp;
                this.scrollY = scrollY;
            }

            public readonly Vector2 position;
            public readonly bool rightDown;
            public readonly bool rightHeld;
            public readonly bool rightUp;
            public readonly bool middleDown;
            public readonly bool middleHeld;
            public readonly bool middleUp;
            public readonly float scrollY;
        }

        private static bool TryReadMouse(out ViewerMouseState state)
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            try
            {
                state = new ViewerMouseState(
                    Input.mousePosition,
                    Input.GetMouseButtonDown(1),
                    Input.GetMouseButton(1),
                    Input.GetMouseButtonUp(1),
                    Input.GetMouseButtonDown(2),
                    Input.GetMouseButton(2),
                    Input.GetMouseButtonUp(2),
                    Input.mouseScrollDelta.y);
                return true;
            }
            catch (InvalidOperationException)
            {
            }
#endif

            return TryReadInputSystemMouse(out state);
        }

        private static bool TryReadInputSystemMouse(out ViewerMouseState state)
        {
            state = default;
            Type? mouseType = Type.GetType("UnityEngine.InputSystem.Mouse, Unity.InputSystem");
            object? mouse = mouseType?.GetProperty("current", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
            if (mouse == null)
            {
                return false;
            }

            if (!TryReadVector2Control(mouse, "position", out Vector2 position))
            {
                return false;
            }

            TryReadVector2Control(mouse, "scroll", out Vector2 scroll);
            state = new ViewerMouseState(
                position,
                ReadButtonProperty(mouse, "rightButton", "wasPressedThisFrame"),
                ReadButtonProperty(mouse, "rightButton", "isPressed"),
                ReadButtonProperty(mouse, "rightButton", "wasReleasedThisFrame"),
                ReadButtonProperty(mouse, "middleButton", "wasPressedThisFrame"),
                ReadButtonProperty(mouse, "middleButton", "isPressed"),
                ReadButtonProperty(mouse, "middleButton", "wasReleasedThisFrame"),
                scroll.y);
            return true;
        }

        private static bool TryReadVector2Control(object owner, string propertyName, out Vector2 value)
        {
            value = default;
            object? control = owner.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance)?.GetValue(owner);
            object? result = control?.GetType().GetMethod("ReadValue", Type.EmptyTypes)?.Invoke(control, null);
            if (result is Vector2 vector)
            {
                value = vector;
                return true;
            }

            return false;
        }

        private static bool ReadButtonProperty(object owner, string buttonPropertyName, string propertyName)
        {
            object? control = owner.GetType().GetProperty(buttonPropertyName, BindingFlags.Public | BindingFlags.Instance)?.GetValue(owner);
            object? result = control?.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance)?.GetValue(control);
            return result is bool pressed && pressed;
        }
    }
}
