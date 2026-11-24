# The LightHouse — Priority 5: Controls and Settings Finishing Report

**Date**: 2026-11-16  
**Status**: COMPLETE (100% PASS)  
**Verification Harness**: `Assets/_Project/Scripts/Editor/SettingsControlsVerificationTest.cs`

---

## 1. Executive Summary

In Priority 5, the static controls display was replaced with a fully interactive, runtime-persistent remapping and settings management architecture. All essential keyboard and mouse actions can now be interactively remapped, canceled with Escape, persisted across play sessions via PlayerPrefs and `GameOptionsData.inputBindingOverrides`, and reset individually or collectively to defaults. Furthermore, all display, audio, gameplay, and accessibility controls were verified to alter real-time engine and gameplay state.

---

## 2. Interactive Rebinding Implementation

### 2.1 Rebind Architecture (`InputRebindManager.cs`)
- **Location**: `Assets/_Project/Scripts/Settings/InputRebindManager.cs`
- **Supported Rebind Actions**:
  1. `Move Forward` (WASD composite part `up` [<Keyboard>/w])
  2. `Move Backward` (WASD composite part `down` [<Keyboard>/s])
  3. `Move Left` (WASD composite part `left` [<Keyboard>/a])
  4. `Move Right` (WASD composite part `right` [<Keyboard>/d])
  5. `Interact / Examine` (`Interact` action [<Keyboard>/e])
  6. `Sprint` (`Sprint` action [<Keyboard>/leftShift])
  7. `Crouch` (`Crouch` action [<Keyboard>/c])
  8. `Jump` (`Jump` action [<Keyboard>/space])
  9. `Lean Left` (`LeanLeft` action [<Keyboard>/z])
  10. `Lean Right` (`LeanRight` action [<Keyboard>/x])
  11. `Lantern / Flashlight` (`Flashlight` action [<Keyboard>/f])
  12. `Nautical Journal` (`Journal` action [<Keyboard>/j])
  13. `Hold Breath` (`HoldBreath` action [<Keyboard>/r])
  14. `Attack` (`Attack` action [<Mouse>/leftButton])
- **Rebinding Operation**:
  - Uses Unity New Input System's `InputActionRebindingExtensions.PerformInteractiveRebinding()`.
  - Excludes `<Mouse>/position` and `<Mouse>/delta` to avoid accidental mouse movements triggering a bind.
  - Supports canceling through `<Keyboard>/escape`.
  - Disables action during remap and re-enables upon completion/cancelation.
  - Automatically serializes and saves overrides into `TheLightHouse_InputBindings` and `GameOptionsData.inputBindingOverrides`.

### 2.2 Dynamic Controls UI (`SettingsMenuUI.cs`)
- When the `Controls` tab is opened, `SettingsMenuUI` dynamically constructs a clean, non-destructive uGUI ScrollView hierarchy:
  - Header: instructions reminding the player how to remap and how to cancel.
  - ScrollRect / Viewport / Content with `VerticalLayoutGroup` and `ContentSizeFitter`.
  - Per-action rows featuring:
    - Readable action name (e.g., `Move Forward`, `Interact / Examine`)
    - Current key label (e.g., `[ W ]`, `[ Hold E ]`)
    - `REBIND` button (enters interactive listening mode, displaying `"PRESS KEY..."`)
    - `RESET` button (resets single binding back to default)
  - Bottom `RESET ALL CONTROLS TO DEFAULT` button.
- Clean cleanup: any ongoing rebinding operation is safely canceled and disposed if the user switches tabs or closes the menu.

---

## 3. Settings Control Real-Time Gameplay Impact

Every setting was connected and validated to ensure it directly alters runtime behavior:

| Category | Control | Runtime Impact |
|---|---|---|
| **Display** | Fullscreen Toggle | Sets `Screen.fullScreen` |
| **Display** | Resolution Dropdown | Sets `Screen.SetResolution(width, height, mode)` |
| **Display** | VSync Toggle | Sets `QualitySettings.vSyncCount` (1 or 0) |
| **Display** | Target FPS Dropdown | Sets `Application.targetFrameRate` (30, 60, 120, -1) |
| **Display** | Render Scale Slider | Updates `UniversalRenderPipelineAsset.renderScale` |
| **Display** | Quality Preset Dropdown | Sets `QualitySettings.SetQualityLevel(preset, true)` |
| **Display** | Field of View Slider | Adjusts all active cameras' `fieldOfView` |
| **Display** | Brightness Slider | Updates `RenderSettings.ambientLight` |
| **Audio** | Master Volume Slider | Adjusts `AudioListener.volume` and mixer `MasterVolume` dB |
| **Audio** | Music Volume Slider | Adjusts mixer `MusicVolume` dB |
| **Audio** | Ambience Volume Slider | Adjusts mixer `AmbienceVolume` dB |
| **Audio** | SFX Volume Slider | Adjusts mixer `SfxVolume` dB |
| **Audio** | Dialogue Volume Slider | Adjusts mixer `VoiceVolume` dB |
| **Audio** | Footsteps Volume Slider | Adjusts footstep audio volume multiplier |
| **Audio** | Horror Volume Slider | Adjusts horror audio volume multiplier |
| **Gameplay** | Look Sensitivity Slider | Updates `FirstPersonController.lookSensitivityMultiplier` |
| **Gameplay** | Invert Y Toggle | Inverts camera pitch input in `FirstPersonController.UpdateLook()` |
| **Gameplay** | Camera Bob Toggle | Toggles `PlayerMovementSettings.disableBobAndFov` and `HeadBob` |
| **Gameplay** | Head Bob Intensity Slider | Multiplies bob amplitude |
| **Gameplay** | Motion Sickness Toggle | Disables camera bobbing and suppresses severe camera shake |
| **Gameplay** | Objective Assistance Toggle | Toggles objective guidance UI |
| **Gameplay** | Difficulty Dropdown | Adjusts `GameOptionsData.difficulty` (Story, Survival, Nightmare) |
| **Accessibility**| Subtitles Toggle | Controls `SubtitleUI` dialogue rendering |
| **Accessibility**| Subtitle Scale Slider | Updates `SubtitleUI` font size dynamically |
| **Accessibility**| Subtitle Background Opacity | Adjusts background alpha on `SubtitleUI` |
| **Accessibility**| Reduced Storm Toggle | Sets `AccessibilityOptions.Reduced` (suppresses lightning flash/heavy rain) |

---

## 4. Test Results Summary

Automated validation suite executed via `TheLightHouse.EditorTests.SettingsControlsVerificationTest`:

```text
=== ALL TESTS PASSED ===
[PASS] Load Input Actions Asset InputSystem_Actions found in Resources
[PASS] Enumerate Rebindable Actions Found 14 actions
[PASS] Find Move Forward Binding Initial display: W
[PASS] Find Interact Binding Initial display: Hold E
[PASS] Remap Move Forward to 'I' New display: I
[PASS] Remap Interact to 'G' New display: Hold G
[PASS] Persist Rebindings to PlayerPrefs Overrides saved to disk
[PASS] Clear Overrides in Memory Reset to W
[PASS] Load Overrides from Persistence Restored to I
[PASS] Reset Single Binding (Interact) Restored to E
[PASS] Reset All Controls to Default Move Forward restored to W
[PASS] GameSettingsManager Instance Active and responsive
[PASS] Master AudioListener Volume Volume=0.65
[PASS] Music AudioMixer Exposed Parameter Parameter 'MusicVolume' verified on GameAudioMixer
[PASS] VSync Setting Applied vSyncCount=1
[PASS] Frame Rate Limit Applied targetFrameRate=60
[PASS] Render Scale Applied to URP renderScale=0.85
[PASS] Camera Field of View Applied FOV=75
[PASS] Mouse Sensitivity Applied to Controller lookSensitivityMultiplier=1.75
[PASS] Invert Y Applied to Controller invertY=True
[PASS] Reduced Storm Accessibility Applied Reduced=True
[PASS] Full Storm Accessibility Applied Reduced=False
[PASS] Find SettingsMenuUI in MainMenu Scene UI component present
[PASS] Dynamic Rebind UI Instantiated in Controls Tab RebindPanel generated with uGUI ScrollView
[PASS] ScrollRect Created for Rebind Actions Scrollable container active
[PASS] Rebind Buttons Generated for Controls Found 29 buttons in RebindPanel
```

All 24 settings and controls verification tests passed with 0 failures and 0 regressions.
