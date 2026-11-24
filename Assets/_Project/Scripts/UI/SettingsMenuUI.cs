using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace TheLightHouse
{
    public sealed class SettingsMenuUI : MonoBehaviour
    {
        public enum Tab { Display, Audio, Gameplay, Accessibility, Controls }

        [Header("Panels & Tabs")]
        [SerializeField] private GameObject rootPanel;
        [SerializeField] private GameObject displayTab;
        [SerializeField] private GameObject audioTab;
        [SerializeField] private GameObject gameplayTab;
        [SerializeField] private GameObject accessibilityTab;
        [SerializeField] private GameObject controlsTab;

        [Header("Tab Buttons")]
        [SerializeField] private Button displayTabButton;
        [SerializeField] private Button audioTabButton;
        [SerializeField] private Button gameplayTabButton;
        [SerializeField] private Button accessibilityTabButton;
        [SerializeField] private Button controlsTabButton;
        [SerializeField] private Button backButton;
        [SerializeField] private Button resetDefaultsButton;

        [Header("Display Controls")]
        [SerializeField] private Dropdown resolutionDropdown;
        [SerializeField] private Toggle fullscreenToggle;
        [SerializeField] private Toggle vSyncToggle;
        [SerializeField] private Dropdown targetFpsDropdown;
        [SerializeField] private Slider renderScaleSlider;
        [SerializeField] private Text renderScaleValueText;
        [SerializeField] private Dropdown qualityPresetDropdown;
        [SerializeField] private Slider fovSlider;
        [SerializeField] private Text fovValueText;
        [SerializeField] private Slider brightnessSlider;

        [Header("Audio Controls")]
        [SerializeField] private Slider masterVolumeSlider;
        [SerializeField] private Slider musicVolumeSlider;
        [SerializeField] private Slider ambienceVolumeSlider;
        [SerializeField] private Slider sfxVolumeSlider;
        [SerializeField] private Slider dialogueVolumeSlider;
        [SerializeField] private Slider footstepsVolumeSlider;
        [SerializeField] private Slider horrorVolumeSlider;

        [Header("Gameplay Controls")]
        [SerializeField] private Slider sensitivitySlider;
        [SerializeField] private Text sensitivityValueText;
        [SerializeField] private Toggle invertYToggle;
        [SerializeField] private Toggle cameraBobToggle;
        [SerializeField] private Slider headBobIntensitySlider;
        [SerializeField] private Toggle motionSicknessToggle;
        [SerializeField] private Toggle objectiveAssistanceToggle;
        [SerializeField] private Dropdown difficultyDropdown;

        [Header("Accessibility Controls")]
        [SerializeField] private Toggle subtitlesToggle;
        [SerializeField] private Slider subtitleSizeSlider;
        [SerializeField] private Slider subtitleBgOpacitySlider;
        [SerializeField] private Toggle reducedFlashingToggle;
        [SerializeField] private Toggle reducedCameraShakeToggle;
        [SerializeField] private Toggle reducedStormToggle;
        [SerializeField] private Toggle toggleInteractionToggle;

        [Header("Controls Configuration")]
        [SerializeField] private Text keybindingsText;
        [SerializeField] private InputActionAsset inputActions;

        private Resolution[] availableResolutions;
        private Tab currentTab = Tab.Display;
        private InputActionRebindingExtensions.RebindingOperation activeRebindOp;
        private GameObject rebindScrollRoot;
        private readonly List<RebindRowUI> rebindRows = new();

        private sealed class RebindRowUI
        {
            public InputRebindManager.RebindEntry entry;
            public Text keyDisplayText;
            public Button rebindButton;
            public Text rebindButtonText;
            public Button resetButton;
        }

        private void Awake()
        {
            SetupTabButtons();
            PopulateResolutions();
            PopulateTargetFps();
            PopulateQualityPresets();
            PopulateDifficulty();
            SetupListeners();
            if (keybindingsText)
            {
                keybindingsText.text =
                    "MOVEMENT: W, A, S, D\n" +
                    "LOOK: Mouse\n" +
                    "SPRINT: Left Shift\n" +
                    "CROUCH: Left Ctrl / C\n" +
                    "JUMP: Space\n" +
                    "INTERACT: E / Left Mouse Click\n" +
                    "JOURNAL: J\n" +
                    "PAUSE / MENU: Escape\n" +
                    "LEAN LEFT/RIGHT: Q / E (while aiming)\n" +
                    "AXE ATTACK: Left Mouse Button\n" +
                    "AXE BLOCK / SHOVE: Right Mouse Button";
            }
        }

        private void OnDestroy()
        {
            CancelActiveRebind();
        }

        public void Open()
        {
            if (!rootPanel) return;
            rootPanel.SetActive(true);
            RefreshUIFromSettings();
            SelectTab(Tab.Display);
        }

        public void Close()
        {
            CancelActiveRebind();
            if (rootPanel) rootPanel.SetActive(false);
        }

        private void SetupTabButtons()
        {
            if (displayTabButton) displayTabButton.onClick.AddListener(() => SelectTab(Tab.Display));
            if (audioTabButton) audioTabButton.onClick.AddListener(() => SelectTab(Tab.Audio));
            if (gameplayTabButton) gameplayTabButton.onClick.AddListener(() => SelectTab(Tab.Gameplay));
            if (accessibilityTabButton) accessibilityTabButton.onClick.AddListener(() => SelectTab(Tab.Accessibility));
            if (controlsTabButton) controlsTabButton.onClick.AddListener(() => SelectTab(Tab.Controls));
            if (backButton) backButton.onClick.AddListener(Close);
            if (resetDefaultsButton) resetDefaultsButton.onClick.AddListener(OnResetDefaults);
        }

        public void SelectTab(Tab tab)
        {
            CancelActiveRebind();
            currentTab = tab;
            if (displayTab) displayTab.SetActive(tab == Tab.Display);
            if (audioTab) audioTab.SetActive(tab == Tab.Audio);
            if (gameplayTab) gameplayTab.SetActive(tab == Tab.Gameplay);
            if (accessibilityTab) accessibilityTab.SetActive(tab == Tab.Accessibility);
            if (controlsTab)
            {
                controlsTab.SetActive(tab == Tab.Controls);
                if (tab == Tab.Controls)
                {
                    SetupOrRefreshControlsUI();
                }
            }
        }

        private void PopulateResolutions()
        {
            if (!resolutionDropdown) return;
            resolutionDropdown.ClearOptions();
            availableResolutions = Screen.resolutions;
            var options = new List<string>();
            int currentResolutionIndex = 0;

            for (int i = 0; i < availableResolutions.Length; i++)
            {
                var r = availableResolutions[i];
                options.Add($"{r.width} x {r.height} @ {r.refreshRateRatio.value:0}Hz");
                if (r.width == Screen.currentResolution.width && r.height == Screen.currentResolution.height)
                {
                    currentResolutionIndex = i;
                }
            }

            resolutionDropdown.AddOptions(options);
            resolutionDropdown.value = currentResolutionIndex;
            resolutionDropdown.RefreshShownValue();
        }

        private void PopulateTargetFps()
        {
            if (!targetFpsDropdown) return;
            targetFpsDropdown.ClearOptions();
            targetFpsDropdown.AddOptions(new List<string> { "30 FPS", "60 FPS", "120 FPS", "UNCAPPED" });
        }

        private void PopulateQualityPresets()
        {
            if (!qualityPresetDropdown) return;
            qualityPresetDropdown.ClearOptions();
            qualityPresetDropdown.AddOptions(new List<string> { "LOW / MAC", "MEDIUM", "HIGH" });
        }

        private void PopulateDifficulty()
        {
            if (!difficultyDropdown) return;
            difficultyDropdown.ClearOptions();
            difficultyDropdown.AddOptions(new List<string> { "STORY (Forgiving)", "SURVIVAL (Standard)", "NIGHTMARE (Punishing)" });
        }

        private void SetupListeners()
        {
            // Display
            if (resolutionDropdown) resolutionDropdown.onValueChanged.AddListener(OnResolutionChanged);
            if (fullscreenToggle) fullscreenToggle.onValueChanged.AddListener(OnFullscreenChanged);
            if (vSyncToggle) vSyncToggle.onValueChanged.AddListener(val => GetSettings()?.UpdateOptions(o => o.vSync = val));
            if (targetFpsDropdown) targetFpsDropdown.onValueChanged.AddListener(OnFpsChanged);
            if (renderScaleSlider) renderScaleSlider.onValueChanged.AddListener(OnRenderScaleChanged);
            if (qualityPresetDropdown) qualityPresetDropdown.onValueChanged.AddListener(val => GetSettings()?.UpdateOptions(o => o.qualityPreset = val));
            if (fovSlider) fovSlider.onValueChanged.AddListener(OnFovChanged);
            if (brightnessSlider) brightnessSlider.onValueChanged.AddListener(val => GetSettings()?.UpdateOptions(o => o.brightness = val));

            // Audio
            if (masterVolumeSlider) masterVolumeSlider.onValueChanged.AddListener(val => GetSettings()?.UpdateOptions(o => o.volume = o.masterVolume = val));
            if (musicVolumeSlider) musicVolumeSlider.onValueChanged.AddListener(val => GetSettings()?.UpdateOptions(o => o.musicVolume = val));
            if (ambienceVolumeSlider) ambienceVolumeSlider.onValueChanged.AddListener(val => GetSettings()?.UpdateOptions(o => o.ambienceVolume = val));
            if (sfxVolumeSlider) sfxVolumeSlider.onValueChanged.AddListener(val => GetSettings()?.UpdateOptions(o => o.sfxVolume = val));
            if (dialogueVolumeSlider) dialogueVolumeSlider.onValueChanged.AddListener(val => GetSettings()?.UpdateOptions(o => o.dialogueVolume = val));
            if (footstepsVolumeSlider) footstepsVolumeSlider.onValueChanged.AddListener(val => GetSettings()?.UpdateOptions(o => o.footstepsVolume = val));
            if (horrorVolumeSlider) horrorVolumeSlider.onValueChanged.AddListener(val => GetSettings()?.UpdateOptions(o => o.horrorVolume = val));

            // Gameplay
            if (sensitivitySlider) sensitivitySlider.onValueChanged.AddListener(OnSensitivityChanged);
            if (invertYToggle) invertYToggle.onValueChanged.AddListener(val => GetSettings()?.UpdateOptions(o => o.invertY = val));
            if (cameraBobToggle) cameraBobToggle.onValueChanged.AddListener(val => GetSettings()?.UpdateOptions(o => { o.cameraBob = val; o.disableBobAndFov = !val; }));
            if (headBobIntensitySlider) headBobIntensitySlider.onValueChanged.AddListener(val => GetSettings()?.UpdateOptions(o => o.headBobIntensity = val));
            if (motionSicknessToggle) motionSicknessToggle.onValueChanged.AddListener(val => GetSettings()?.UpdateOptions(o => o.motionSicknessReduction = val));
            if (objectiveAssistanceToggle) objectiveAssistanceToggle.onValueChanged.AddListener(val => GetSettings()?.UpdateOptions(o => o.objectiveAssistance = val));
            if (difficultyDropdown) difficultyDropdown.onValueChanged.AddListener(val => GetSettings()?.UpdateOptions(o => o.difficulty = val));

            // Accessibility
            if (subtitlesToggle) subtitlesToggle.onValueChanged.AddListener(val => GetSettings()?.UpdateOptions(o => o.subtitlesEnabled = val));
            if (subtitleSizeSlider) subtitleSizeSlider.onValueChanged.AddListener(val => GetSettings()?.UpdateOptions(o => o.subtitleSize = val));
            if (subtitleBgOpacitySlider) subtitleBgOpacitySlider.onValueChanged.AddListener(val => GetSettings()?.UpdateOptions(o => o.subtitleBackgroundOpacity = val));
            if (reducedFlashingToggle) reducedFlashingToggle.onValueChanged.AddListener(val => GetSettings()?.UpdateOptions(o => o.reducedFlashing = val));
            if (reducedCameraShakeToggle) reducedCameraShakeToggle.onValueChanged.AddListener(val => GetSettings()?.UpdateOptions(o => o.reducedCameraShake = val));
            if (reducedStormToggle) reducedStormToggle.onValueChanged.AddListener(val => GetSettings()?.UpdateOptions(o => o.reducedStorm = val));
            if (toggleInteractionToggle) toggleInteractionToggle.onValueChanged.AddListener(val => GetSettings()?.UpdateOptions(o => o.toggleInteraction = val));
        }

        public void RefreshUIFromSettings()
        {
            var mgr = GetSettings();
            var o = mgr != null ? mgr.CurrentOptions : new GameOptionsData();

            if (fullscreenToggle) fullscreenToggle.isOn = Screen.fullScreen;
            if (vSyncToggle) vSyncToggle.isOn = o.vSync;
            if (renderScaleSlider) { renderScaleSlider.value = o.renderScale; if (renderScaleValueText) renderScaleValueText.text = $"{Mathf.RoundToInt(o.renderScale * 100)}%"; }
            if (qualityPresetDropdown) qualityPresetDropdown.value = Mathf.Clamp(o.qualityPreset, 0, 2);
            if (fovSlider) { fovSlider.value = o.fov; if (fovValueText) fovValueText.text = $"{Mathf.RoundToInt(o.fov)}°"; }
            if (brightnessSlider) brightnessSlider.value = o.brightness;

            if (masterVolumeSlider) masterVolumeSlider.value = o.volume;
            if (musicVolumeSlider) musicVolumeSlider.value = o.musicVolume;
            if (ambienceVolumeSlider) ambienceVolumeSlider.value = o.ambienceVolume;
            if (sfxVolumeSlider) sfxVolumeSlider.value = o.sfxVolume;
            if (dialogueVolumeSlider) dialogueVolumeSlider.value = o.dialogueVolume;
            if (footstepsVolumeSlider) footstepsVolumeSlider.value = o.footstepsVolume;
            if (horrorVolumeSlider) horrorVolumeSlider.value = o.horrorVolume;

            if (sensitivitySlider) { sensitivitySlider.value = o.sensitivity; if (sensitivityValueText) sensitivityValueText.text = $"{o.sensitivity:0.0}x"; }
            if (invertYToggle) invertYToggle.isOn = o.invertY;
            if (cameraBobToggle) cameraBobToggle.isOn = o.cameraBob;
            if (headBobIntensitySlider) headBobIntensitySlider.value = o.headBobIntensity;
            if (motionSicknessToggle) motionSicknessToggle.isOn = o.motionSicknessReduction;
            if (objectiveAssistanceToggle) objectiveAssistanceToggle.isOn = o.objectiveAssistance;
            if (difficultyDropdown) difficultyDropdown.value = Mathf.Clamp(o.difficulty, 0, 2);

            if (subtitlesToggle) subtitlesToggle.isOn = o.subtitlesEnabled;
            if (subtitleSizeSlider) subtitleSizeSlider.value = o.subtitleSize;
            if (subtitleBgOpacitySlider) subtitleBgOpacitySlider.value = o.subtitleBackgroundOpacity;
            if (reducedFlashingToggle) reducedFlashingToggle.isOn = o.reducedFlashing;
            if (reducedCameraShakeToggle) reducedCameraShakeToggle.isOn = o.reducedCameraShake;
            if (reducedStormToggle) reducedStormToggle.isOn = o.reducedStorm;
            if (toggleInteractionToggle) toggleInteractionToggle.isOn = o.toggleInteraction;
        }

        private void OnResolutionChanged(int index)
        {
            if (availableResolutions == null || index < 0 || index >= availableResolutions.Length) return;
            var r = availableResolutions[index];
            Screen.SetResolution(r.width, r.height, Screen.fullScreenMode);
        }

        private void OnFullscreenChanged(bool isFullscreen)
        {
            Screen.fullScreen = isFullscreen;
        }

        private void OnFpsChanged(int index)
        {
            int fps = index switch { 0 => 30, 1 => 60, 2 => 120, _ => -1 };
            GetSettings()?.UpdateOptions(o => o.frameRateLimit = fps);
        }

        private void OnRenderScaleChanged(float val)
        {
            if (renderScaleValueText) renderScaleValueText.text = $"{Mathf.RoundToInt(val * 100)}%";
            GetSettings()?.UpdateOptions(o => o.renderScale = val);
        }

        private void OnFovChanged(float val)
        {
            if (fovValueText) fovValueText.text = $"{Mathf.RoundToInt(val)}°";
            GetSettings()?.UpdateOptions(o => o.fov = val);
        }

        private void OnSensitivityChanged(float val)
        {
            if (sensitivityValueText) sensitivityValueText.text = $"{val:0.0}x";
            GetSettings()?.UpdateOptions(o => o.sensitivity = val);
        }

        private void OnResetDefaults()
        {
            CancelActiveRebind();
            GetSettings()?.ResetToDefaults();
            RefreshUIFromSettings();
            RefreshControlsUI();
        }

        public void CancelActiveRebind()
        {
            if (activeRebindOp != null)
            {
                activeRebindOp.Cancel();
                activeRebindOp.Dispose();
                activeRebindOp = null;
                RefreshControlsUI();
            }
        }

        private void EnsureInputActions()
        {
            if (!inputActions)
            {
                inputActions = Resources.Load<InputActionAsset>("InputSystem_Actions");
#if UNITY_EDITOR
                if (!inputActions)
                {
                    inputActions = UnityEditor.AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/InputSystem_Actions.inputactions");
                }
#endif
            }
        }

        public void SetupOrRefreshControlsUI()
        {
            if (!controlsTab) return;
            EnsureInputActions();
            if (!inputActions) return;

            InputRebindManager.LoadOverrides(inputActions);

            if (rebindScrollRoot == null)
            {
                BuildControlsUI();
            }
            else
            {
                RefreshControlsUI();
            }
        }

        private void BuildControlsUI()
        {
            if (!controlsTab) return;

            // Hide previous static text if present
            if (keybindingsText) keybindingsText.gameObject.SetActive(false);
            for (int i = 0; i < controlsTab.transform.childCount; i++)
            {
                var child = controlsTab.transform.GetChild(i);
                if (child.name.Contains("Text") || child.GetComponent<Text>() != null)
                {
                    child.gameObject.SetActive(false);
                }
            }

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // Main Rebind Panel
            rebindScrollRoot = new GameObject("RebindPanel", typeof(RectTransform));
            rebindScrollRoot.transform.SetParent(controlsTab.transform, false);
            var rootRt = rebindScrollRoot.GetComponent<RectTransform>();
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = new Vector2(10, 10);
            rootRt.offsetMax = new Vector2(-10, -10);

            // Instructions text at top
            var headerGo = new GameObject("InstructionsText", typeof(RectTransform), typeof(Text));
            headerGo.transform.SetParent(rebindScrollRoot.transform, false);
            var headerRt = headerGo.GetComponent<RectTransform>();
            headerRt.anchorMin = new Vector2(0f, 0.90f);
            headerRt.anchorMax = new Vector2(1f, 1f);
            headerRt.offsetMin = Vector2.zero;
            headerRt.offsetMax = Vector2.zero;
            var headerText = headerGo.GetComponent<Text>();
            headerText.font = font;
            headerText.fontSize = 14;
            headerText.alignment = TextAnchor.MiddleCenter;
            headerText.color = new Color(0.85f, 0.85f, 0.85f, 1f);
            headerText.text = "KEY REMAPPING — Click REBIND then press any key. Press ESC to cancel.";

            // Scroll View Root
            var scrollGo = new GameObject("ScrollView", typeof(RectTransform), typeof(ScrollRect), typeof(Image));
            scrollGo.transform.SetParent(rebindScrollRoot.transform, false);
            var scrollRt = scrollGo.GetComponent<RectTransform>();
            scrollRt.anchorMin = new Vector2(0f, 0.12f);
            scrollRt.anchorMax = new Vector2(1f, 0.89f);
            scrollRt.offsetMin = Vector2.zero;
            scrollRt.offsetMax = Vector2.zero;

            var scrollBg = scrollGo.GetComponent<Image>();
            scrollBg.color = new Color(0f, 0f, 0f, 0.25f);

            var scrollRect = scrollGo.GetComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.scrollSensitivity = 25f;

            // Viewport
            var viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
            viewportGo.transform.SetParent(scrollGo.transform, false);
            var viewportRt = viewportGo.GetComponent<RectTransform>();
            viewportRt.anchorMin = Vector2.zero;
            viewportRt.anchorMax = Vector2.one;
            viewportRt.offsetMin = Vector2.zero;
            viewportRt.offsetMax = Vector2.zero;
            scrollRect.viewport = viewportRt;

            // Content
            var contentGo = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            contentGo.transform.SetParent(viewportGo.transform, false);
            var contentRt = contentGo.GetComponent<RectTransform>();
            contentRt.anchorMin = new Vector2(0f, 1f);
            contentRt.anchorMax = new Vector2(1f, 1f);
            contentRt.pivot = new Vector2(0.5f, 1f);
            contentRt.sizeDelta = new Vector2(0f, 300f);

            var vlg = contentGo.GetComponent<VerticalLayoutGroup>();
            vlg.spacing = 6f;
            vlg.padding = new RectOffset(16, 16, 8, 8);
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            var csf = contentGo.GetComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scrollRect.content = contentRt;

            // Reset All Button at bottom
            var resetAllBtnGo = new GameObject("ResetAllButton", typeof(RectTransform), typeof(Image), typeof(Button));
            resetAllBtnGo.transform.SetParent(rebindScrollRoot.transform, false);
            var resetAllRt = resetAllBtnGo.GetComponent<RectTransform>();
            resetAllRt.anchorMin = new Vector2(0.25f, 0.01f);
            resetAllRt.anchorMax = new Vector2(0.75f, 0.09f);
            resetAllRt.offsetMin = Vector2.zero;
            resetAllRt.offsetMax = Vector2.zero;

            var resetAllImg = resetAllBtnGo.GetComponent<Image>();
            resetAllImg.color = new Color(0.25f, 0.15f, 0.15f, 0.9f);
            var resetAllBtn = resetAllBtnGo.GetComponent<Button>();
            resetAllBtn.onClick.AddListener(() =>
            {
                CancelActiveRebind();
                InputRebindManager.ResetAllBindings(inputActions);
                RefreshControlsUI();
            });

            var resetAllTextGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
            resetAllTextGo.transform.SetParent(resetAllBtnGo.transform, false);
            var resetAllTextRt = resetAllTextGo.GetComponent<RectTransform>();
            resetAllTextRt.anchorMin = Vector2.zero;
            resetAllTextRt.anchorMax = Vector2.one;
            resetAllTextRt.offsetMin = Vector2.zero;
            resetAllTextRt.offsetMax = Vector2.zero;
            var resetAllText = resetAllTextGo.GetComponent<Text>();
            resetAllText.font = font;
            resetAllText.fontSize = 14;
            resetAllText.alignment = TextAnchor.MiddleCenter;
            resetAllText.color = new Color(1f, 0.8f, 0.8f, 1f);
            resetAllText.text = "RESET ALL CONTROLS TO DEFAULT";

            // Populate Action Rows
            rebindRows.Clear();
            var entries = InputRebindManager.GetRebindEntries(inputActions);
            foreach (var entry in entries)
            {
                var rowView = CreateRebindRow(contentRt.transform, entry, font);
                rebindRows.Add(rowView);
            }
        }

        private RebindRowUI CreateRebindRow(Transform parent, InputRebindManager.RebindEntry entry, Font font)
        {
            var rowGo = new GameObject($"Row_{entry.actionName}_{entry.bindingIndex}", typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            rowGo.transform.SetParent(parent, false);

            var le = rowGo.GetComponent<LayoutElement>();
            le.minHeight = 36f;
            le.preferredHeight = 36f;

            var img = rowGo.GetComponent<Image>();
            img.color = new Color(0.12f, 0.14f, 0.16f, 0.7f);

            var hlg = rowGo.GetComponent<HorizontalLayoutGroup>();
            hlg.spacing = 10f;
            hlg.padding = new RectOffset(12, 12, 4, 4);
            hlg.childControlWidth = false;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;
            hlg.childAlignment = TextAnchor.MiddleLeft;

            // Action Display Name
            var nameGo = new GameObject("ActionName", typeof(RectTransform), typeof(Text), typeof(LayoutElement));
            nameGo.transform.SetParent(rowGo.transform, false);
            nameGo.GetComponent<LayoutElement>().preferredWidth = 260f;
            var nameText = nameGo.GetComponent<Text>();
            nameText.font = font;
            nameText.fontSize = 15;
            nameText.alignment = TextAnchor.MiddleLeft;
            nameText.color = Color.white;
            nameText.text = entry.displayName;

            // Current Key Display
            var keyGo = new GameObject("KeyDisplay", typeof(RectTransform), typeof(Text), typeof(LayoutElement));
            keyGo.transform.SetParent(rowGo.transform, false);
            keyGo.GetComponent<LayoutElement>().preferredWidth = 140f;
            var keyText = keyGo.GetComponent<Text>();
            keyText.font = font;
            keyText.fontSize = 15;
            keyText.alignment = TextAnchor.MiddleCenter;
            keyText.color = new Color(1f, 0.9f, 0.4f, 1f);
            keyText.text = $"[ {entry.currentDisplay} ]";

            // Rebind Button
            var rebindBtnGo = new GameObject("RebindBtn", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            rebindBtnGo.transform.SetParent(rowGo.transform, false);
            rebindBtnGo.GetComponent<LayoutElement>().preferredWidth = 120f;
            var rebindImg = rebindBtnGo.GetComponent<Image>();
            rebindImg.color = new Color(0.2f, 0.35f, 0.5f, 0.9f);
            var rebindBtn = rebindBtnGo.GetComponent<Button>();

            var rebindTextGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
            rebindTextGo.transform.SetParent(rebindBtnGo.transform, false);
            var rTextRt = rebindTextGo.GetComponent<RectTransform>();
            rTextRt.anchorMin = Vector2.zero;
            rTextRt.anchorMax = Vector2.one;
            rTextRt.offsetMin = Vector2.zero;
            rTextRt.offsetMax = Vector2.zero;
            var rebindBtnText = rebindTextGo.GetComponent<Text>();
            rebindBtnText.font = font;
            rebindBtnText.fontSize = 13;
            rebindBtnText.alignment = TextAnchor.MiddleCenter;
            rebindBtnText.color = Color.white;
            rebindBtnText.text = "REBIND";

            // Reset Button
            var resetBtnGo = new GameObject("ResetBtn", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            resetBtnGo.transform.SetParent(rowGo.transform, false);
            resetBtnGo.GetComponent<LayoutElement>().preferredWidth = 80f;
            var resetImg = resetBtnGo.GetComponent<Image>();
            resetImg.color = new Color(0.3f, 0.2f, 0.2f, 0.8f);
            var resetBtn = resetBtnGo.GetComponent<Button>();

            var resetTextGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
            resetTextGo.transform.SetParent(resetBtnGo.transform, false);
            var resTextRt = resetTextGo.GetComponent<RectTransform>();
            resTextRt.anchorMin = Vector2.zero;
            resTextRt.anchorMax = Vector2.one;
            resTextRt.offsetMin = Vector2.zero;
            resTextRt.offsetMax = Vector2.zero;
            var resetBtnText = resetTextGo.GetComponent<Text>();
            resetBtnText.font = font;
            resetBtnText.fontSize = 13;
            resetBtnText.alignment = TextAnchor.MiddleCenter;
            resetBtnText.color = new Color(0.9f, 0.9f, 0.9f, 1f);
            resetBtnText.text = "RESET";

            var rowUI = new RebindRowUI
            {
                entry = entry,
                keyDisplayText = keyText,
                rebindButton = rebindBtn,
                rebindButtonText = rebindBtnText,
                resetButton = resetBtn
            };

            rebindBtn.onClick.AddListener(() =>
            {
                CancelActiveRebind();
                rebindBtnText.text = "PRESS KEY...";
                rebindBtnText.color = Color.yellow;
                activeRebindOp = InputRebindManager.StartRebind(inputActions, entry.actionName, entry.bindingIndex,
                    onComplete: (display) =>
                    {
                        activeRebindOp = null;
                        rebindBtnText.text = "REBIND";
                        rebindBtnText.color = Color.white;
                        RefreshControlsUI();
                    },
                    onCancel: () =>
                    {
                        activeRebindOp = null;
                        rebindBtnText.text = "REBIND";
                        rebindBtnText.color = Color.white;
                        RefreshControlsUI();
                    });
            });

            resetBtn.onClick.AddListener(() =>
            {
                CancelActiveRebind();
                InputRebindManager.ResetBinding(inputActions, entry.actionName, entry.bindingIndex);
                RefreshControlsUI();
            });

            return rowUI;
        }

        public void RefreshControlsUI()
        {
            EnsureInputActions();
            if (!inputActions) return;

            var entries = InputRebindManager.GetRebindEntries(inputActions);
            for (int i = 0; i < rebindRows.Count && i < entries.Count; i++)
            {
                var row = rebindRows[i];
                var ent = entries[i];
                row.entry = ent;
                if (row.keyDisplayText)
                {
                    row.keyDisplayText.text = $"[ {ent.currentDisplay} ]";
                }
                if (row.rebindButtonText && activeRebindOp == null)
                {
                    row.rebindButtonText.text = "REBIND";
                    row.rebindButtonText.color = Color.white;
                }
            }
        }

        private GameSettingsManager GetSettings()
        {
            var mgr = GameSettingsManager.Instance;
            if (!mgr)
            {
                var go = new GameObject("GameSettingsManager");
                mgr = go.AddComponent<GameSettingsManager>();
            }
            return mgr;
        }
    }
}
