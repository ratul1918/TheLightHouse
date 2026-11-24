using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

namespace TheLightHouse.EditorTests
{
    public static class SettingsControlsVerificationTest
    {
        [MenuItem("The LightHouse/Verification/Run Controls and Settings Tests")]
        public static string RunTests()
        {
            var results = new List<string>();
            bool allPassed = true;

            void Assert(bool condition, string testName, string detail = "")
            {
                if (condition)
                {
                    results.Add($"[PASS] {testName} {detail}".Trim());
                }
                else
                {
                    results.Add($"[FAIL] {testName} {detail}".Trim());
                    allPassed = false;
                }
            }

            try
            {
                // Ensure MainMenu scene is loaded
                EditorSceneManager.OpenScene("Assets/_Project/Scenes/MainMenu.unity");

                var inputAsset = Resources.Load<InputActionAsset>("InputSystem_Actions");
                Assert(inputAsset != null, "Load Input Actions Asset", "InputSystem_Actions found in Resources");

                // ----------------------------------------------------
                // 1. Interactive Rebinding & Persistence Test
                // ----------------------------------------------------
                var entries = InputRebindManager.GetRebindEntries(inputAsset);
                Assert(entries.Count >= 10, "Enumerate Rebindable Actions", $"Found {entries.Count} actions");

                var moveUp = entries.Find(e => e.actionName == "Move" && e.bindingPart == "up");
                Assert(moveUp != null, "Find Move Forward Binding", $"Initial display: {moveUp?.currentDisplay}");

                var interact = entries.Find(e => e.actionName == "Interact");
                Assert(interact != null, "Find Interact Binding", $"Initial display: {interact?.currentDisplay}");

                // Apply manual override to Move Forward -> I (<Keyboard>/i)
                InputRebindManager.ApplyManualOverride(inputAsset, "Move", moveUp.bindingIndex, "<Keyboard>/i");
                var updatedEntries = InputRebindManager.GetRebindEntries(inputAsset);
                var remappedMoveUp = updatedEntries.Find(e => e.actionName == "Move" && e.bindingPart == "up");
                Assert(remappedMoveUp != null && remappedMoveUp.currentDisplay == "I", "Remap Move Forward to 'I'", $"New display: {remappedMoveUp?.currentDisplay}");

                // Apply manual override to Interact -> G (<Keyboard>/g)
                InputRebindManager.ApplyManualOverride(inputAsset, "Interact", interact.bindingIndex, "<Keyboard>/g");
                updatedEntries = InputRebindManager.GetRebindEntries(inputAsset);
                var remappedInteract = updatedEntries.Find(e => e.actionName == "Interact");
                Assert(remappedInteract != null && remappedInteract.currentDisplay.Contains("G"), "Remap Interact to 'G'", $"New display: {remappedInteract?.currentDisplay}");

                // Verify persistence to PlayerPrefs and GameOptionsData
                string savedPrefs = PlayerPrefs.GetString(InputRebindManager.PrefsKey, "");
                Assert(!string.IsNullOrEmpty(savedPrefs) && savedPrefs.Contains("<Keyboard>/i"), "Persist Rebindings to PlayerPrefs", "Overrides saved to disk");

                // Test reloading overrides into fresh asset state
                inputAsset.RemoveAllBindingOverrides();
                var resetEntries = InputRebindManager.GetRebindEntries(inputAsset);
                var resetMoveUp = resetEntries.Find(e => e.actionName == "Move" && e.bindingPart == "up");
                Assert(resetMoveUp != null && resetMoveUp.currentDisplay == "W", "Clear Overrides in Memory", "Reset to W");

                InputRebindManager.LoadOverrides(inputAsset);
                var reloadedEntries = InputRebindManager.GetRebindEntries(inputAsset);
                var reloadedMoveUp = reloadedEntries.Find(e => e.actionName == "Move" && e.bindingPart == "up");
                Assert(reloadedMoveUp != null && reloadedMoveUp.currentDisplay == "I", "Load Overrides from Persistence", "Restored to I");

                // Test Reset Single Binding
                InputRebindManager.ResetBinding(inputAsset, "Interact", interact.bindingIndex);
                var singleResetEntries = InputRebindManager.GetRebindEntries(inputAsset);
                var singleResetInteract = singleResetEntries.Find(e => e.actionName == "Interact");
                Assert(singleResetInteract != null && singleResetInteract.currentDisplay.Contains("E"), "Reset Single Binding (Interact)", "Restored to E");

                // Test Reset All Bindings
                InputRebindManager.ResetAllBindings(inputAsset);
                var allResetEntries = InputRebindManager.GetRebindEntries(inputAsset);
                var allResetMoveUp = allResetEntries.Find(e => e.actionName == "Move" && e.bindingPart == "up");
                Assert(allResetMoveUp != null && allResetMoveUp.currentDisplay == "W", "Reset All Controls to Default", "Move Forward restored to W");

                // ----------------------------------------------------
                // 2. Settings Controls Gameplay & Engine State Verification
                // ----------------------------------------------------
                var settingsMgr = GameSettingsManager.Instance;
                if (!settingsMgr)
                {
                    var go = new GameObject("GameSettingsManager");
                    settingsMgr = go.AddComponent<GameSettingsManager>();
                }
                Assert(settingsMgr != null, "GameSettingsManager Instance", "Active and responsive");

                // Audio Settings
                settingsMgr.UpdateOptions(o =>
                {
                    o.volume = 0.65f;
                    o.masterVolume = 0.75f;
                    o.musicVolume = 0.5f;
                    o.sfxVolume = 0.8f;
                    o.dialogueVolume = 0.9f;
                    o.ambienceVolume = 0.4f;
                });
                Assert(Mathf.Approximately(AudioListener.volume, 0.65f), "Master AudioListener Volume", $"Volume={AudioListener.volume}");

                var mixer = Resources.Load<AudioMixer>("GameAudioMixer");
                if (mixer)
                {
                    bool hasMusicParam = mixer.GetFloat("MusicVolume", out float musicDb);
                    Assert(hasMusicParam, "Music AudioMixer Exposed Parameter", "Parameter 'MusicVolume' verified on GameAudioMixer");
                }
                else
                {
                    Assert(false, "AudioMixer Resource Load", "GameAudioMixer.mixer not found in Resources");
                }

                // Display Settings
                settingsMgr.UpdateOptions(o =>
                {
                    o.vSync = true;
                    o.frameRateLimit = 60;
                    o.renderScale = 0.85f;
                    o.fov = 75f;
                    o.brightness = 1.2f;
                });
                Assert(QualitySettings.vSyncCount == 1, "VSync Setting Applied", "vSyncCount=1");
                Assert(Application.targetFrameRate == 60, "Frame Rate Limit Applied", "targetFrameRate=60");

                if (UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
                {
                    Assert(Mathf.Approximately(urp.renderScale, 0.85f), "Render Scale Applied to URP", $"renderScale={urp.renderScale}");
                }

                // Camera FOV
                var dummyCam = new GameObject("TestCam").AddComponent<Camera>();
                dummyCam.tag = "MainCamera";
                settingsMgr.ApplyAll(settingsMgr.CurrentOptions);
                Assert(Mathf.Approximately(dummyCam.fieldOfView, 75f), "Camera Field of View Applied", $"FOV={dummyCam.fieldOfView}");
                Object.DestroyImmediate(dummyCam.gameObject);

                // Player Sensitivity and Invert Y
                var dummyPlayer = new GameObject("TestPlayer");
                var charController = dummyPlayer.AddComponent<CharacterController>();
                var fpc = dummyPlayer.AddComponent<FirstPersonController>();
                var camTransform = new GameObject("CamTransform").transform;
                camTransform.SetParent(dummyPlayer.transform);
                var fpcSerialized = new SerializedObject(fpc);
                fpcSerialized.FindProperty("cameraTransform").objectReferenceValue = camTransform;
                fpcSerialized.FindProperty("inputActions").objectReferenceValue = inputAsset;
                fpcSerialized.ApplyModifiedProperties();

                settingsMgr.UpdateOptions(o =>
                {
                    o.sensitivity = 1.75f;
                    o.invertY = true;
                    o.cameraBob = false;
                });
                settingsMgr.ApplyAll(settingsMgr.CurrentOptions);

                float lookSens = fpc.LookSensitivityMultiplier;
                bool invY = fpc.InvertY;
                Assert(Mathf.Approximately(lookSens, 1.75f), "Mouse Sensitivity Applied to Controller", $"lookSensitivityMultiplier={lookSens}");
                Assert(invY == true, "Invert Y Applied to Controller", $"invertY={invY}");

                Object.DestroyImmediate(dummyPlayer);

                // Accessibility: Reduced Storm
                var dummyAccessGo = new GameObject("AccessibilityObj");
                var accessOptions = dummyAccessGo.AddComponent<AccessibilityOptions>();
                settingsMgr.UpdateOptions(o => o.reducedStorm = true);
                settingsMgr.ApplyAll(settingsMgr.CurrentOptions);
                Assert(accessOptions.Reduced == true, "Reduced Storm Accessibility Applied", $"Reduced={accessOptions.Reduced}");

                settingsMgr.UpdateOptions(o => o.reducedStorm = false);
                settingsMgr.ApplyAll(settingsMgr.CurrentOptions);
                Assert(accessOptions.Reduced == false, "Full Storm Accessibility Applied", $"Reduced={accessOptions.Reduced}");
                Object.DestroyImmediate(dummyAccessGo);

                // ----------------------------------------------------
                // 3. SettingsMenuUI Interactive Controls Tab Verification
                // ----------------------------------------------------
                var settingsMenu = Object.FindAnyObjectByType<SettingsMenuUI>(FindObjectsInactive.Include);
                Assert(settingsMenu != null, "Find SettingsMenuUI in MainMenu Scene", "UI component present");

                if (settingsMenu != null)
                {
                    settingsMenu.Open();
                    settingsMenu.SelectTab(SettingsMenuUI.Tab.Controls);

                    var menuSo = new SerializedObject(settingsMenu);
                    var rebindScrollRootProp = menuSo.FindProperty("rebindScrollRoot");
                    var rootGo = rebindScrollRootProp != null ? rebindScrollRootProp.objectReferenceValue as GameObject : null;
                    if (!rootGo)
                    {
                        var controlsTabGo = menuSo.FindProperty("controlsTab").objectReferenceValue as GameObject;
                        if (controlsTabGo)
                        {
                            var foundRoot = controlsTabGo.transform.Find("RebindPanel");
                            if (foundRoot) rootGo = foundRoot.gameObject;
                        }
                    }

                    Assert(rootGo != null && rootGo.activeInHierarchy, "Dynamic Rebind UI Instantiated in Controls Tab", "RebindPanel generated with uGUI ScrollView");

                    var scrollRect = rootGo?.GetComponentInChildren<UnityEngine.UI.ScrollRect>();
                    Assert(scrollRect != null, "ScrollRect Created for Rebind Actions", "Scrollable container active");

                    var rowCount = rootGo?.GetComponentsInChildren<UnityEngine.UI.Button>().Length ?? 0;
                    Assert(rowCount >= 10, "Rebind Buttons Generated for Controls", $"Found {rowCount} buttons in RebindPanel");

                    settingsMenu.Close();
                }

                // Restore default options
                settingsMgr.ResetToDefaults();
            }
            catch (System.Exception ex)
            {
                results.Add($"[EXCEPTION] {ex.Message}\n{ex.StackTrace}");
                allPassed = false;
            }

            string report = (allPassed ? "=== ALL TESTS PASSED ===\n" : "=== SOME TESTS FAILED ===\n") + string.Join("\n", results);
            Debug.Log(report);
            return report;
        }
    }
}
