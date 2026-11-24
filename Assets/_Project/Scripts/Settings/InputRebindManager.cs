using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TheLightHouse
{
    public static class InputRebindManager
    {
        public const string PrefsKey = "TheLightHouse_InputBindings";

        public sealed class RebindEntry
        {
            public string actionName;
            public string displayName;
            public string bindingPart;
            public int bindingIndex;
            public string currentDisplay;
            public string effectivePath;
        }

        public static event Action OnBindingsChanged;

        public static List<RebindEntry> GetRebindEntries(InputActionAsset asset)
        {
            var list = new List<RebindEntry>();
            if (!asset) return list;

            var playerMap = asset.FindActionMap("Player");
            if (playerMap == null) return list;

            // Define rebindable actions and their composite parts/bindings
            AddCompositePart(list, playerMap, "Move", "up", "Move Forward", 2);
            AddCompositePart(list, playerMap, "Move", "down", "Move Backward", 4);
            AddCompositePart(list, playerMap, "Move", "left", "Move Left", 6);
            AddCompositePart(list, playerMap, "Move", "right", "Move Right", 8);

            AddActionBinding(list, playerMap, "Interact", "Interact / Examine", 0);
            AddActionBinding(list, playerMap, "Sprint", "Sprint", 0);
            AddActionBinding(list, playerMap, "Crouch", "Crouch", 1);
            AddActionBinding(list, playerMap, "Jump", "Jump", 0);
            AddActionBinding(list, playerMap, "LeanLeft", "Lean Left", 0);
            AddActionBinding(list, playerMap, "LeanRight", "Lean Right", 0);
            AddActionBinding(list, playerMap, "Flashlight", "Lantern / Flashlight", 0);
            AddActionBinding(list, playerMap, "Journal", "Nautical Journal", 0);
            AddActionBinding(list, playerMap, "HoldBreath", "Hold Breath", 0);
            AddActionBinding(list, playerMap, "Attack", "Attack", 1);

            return list;
        }

        private static void AddCompositePart(List<RebindEntry> list, InputActionMap map, string actionName, string partName, string displayName, int fallbackIndex)
        {
            var action = map.FindAction(actionName);
            if (action == null) return;

            int targetIndex = fallbackIndex;
            for (int i = 0; i < action.bindings.Count; i++)
            {
                var b = action.bindings[i];
                if (b.isPartOfComposite && string.Equals(b.name, partName, StringComparison.OrdinalIgnoreCase))
                {
                    targetIndex = i;
                    break;
                }
            }

            if (targetIndex >= 0 && targetIndex < action.bindings.Count)
            {
                list.Add(new RebindEntry
                {
                    actionName = actionName,
                    displayName = displayName,
                    bindingPart = partName,
                    bindingIndex = targetIndex,
                    currentDisplay = action.GetBindingDisplayString(targetIndex),
                    effectivePath = action.bindings[targetIndex].effectivePath
                });
            }
        }

        private static void AddActionBinding(List<RebindEntry> list, InputActionMap map, string actionName, string displayName, int bindingIndex)
        {
            var action = map.FindAction(actionName);
            if (action == null || bindingIndex >= action.bindings.Count) return;

            list.Add(new RebindEntry
            {
                actionName = actionName,
                displayName = displayName,
                bindingPart = null,
                bindingIndex = bindingIndex,
                currentDisplay = action.GetBindingDisplayString(bindingIndex),
                effectivePath = action.bindings[bindingIndex].effectivePath
            });
        }

        public static InputActionRebindingExtensions.RebindingOperation StartRebind(
            InputActionAsset asset,
            string actionName,
            int bindingIndex,
            Action<string> onComplete,
            Action onCancel)
        {
            if (!asset)
            {
                onCancel?.Invoke();
                return null;
            }

            var playerMap = asset.FindActionMap("Player");
            var action = playerMap?.FindAction(actionName);
            if (action == null || bindingIndex < 0 || bindingIndex >= action.bindings.Count)
            {
                onCancel?.Invoke();
                return null;
            }

            bool wasEnabled = action.enabled;
            if (wasEnabled) action.Disable();

            var operation = action.PerformInteractiveRebinding(bindingIndex)
                .WithControlsExcluding("Mouse/position")
                .WithControlsExcluding("Mouse/delta")
                .WithCancelingThrough("<Keyboard>/escape")
                .OnMatchWaitForAnother(0.1f)
                .OnComplete(op =>
                {
                    string newDisplay = action.GetBindingDisplayString(bindingIndex);
                    op.Dispose();
                    if (wasEnabled) action.Enable();
                    SaveOverrides(asset);
                    OnBindingsChanged?.Invoke();
                    onComplete?.Invoke(newDisplay);
                })
                .OnCancel(op =>
                {
                    op.Dispose();
                    if (wasEnabled) action.Enable();
                    onCancel?.Invoke();
                });

            operation.Start();
            return operation;
        }

        public static void ApplyManualOverride(InputActionAsset asset, string actionName, int bindingIndex, string overridePath)
        {
            if (!asset) return;
            var playerMap = asset.FindActionMap("Player");
            var action = playerMap?.FindAction(actionName);
            if (action == null || bindingIndex < 0 || bindingIndex >= action.bindings.Count) return;

            action.ApplyBindingOverride(bindingIndex, overridePath);
            SaveOverrides(asset);
            OnBindingsChanged?.Invoke();
        }

        public static void ResetBinding(InputActionAsset asset, string actionName, int bindingIndex)
        {
            if (!asset) return;
            var playerMap = asset.FindActionMap("Player");
            var action = playerMap?.FindAction(actionName);
            if (action == null || bindingIndex < 0 || bindingIndex >= action.bindings.Count) return;

            action.RemoveBindingOverride(bindingIndex);
            SaveOverrides(asset);
            OnBindingsChanged?.Invoke();
        }

        public static void ResetAllBindings(InputActionAsset asset)
        {
            if (!asset) return;
            asset.RemoveAllBindingOverrides();
            PlayerPrefs.DeleteKey(PrefsKey);
            PlayerPrefs.Save();

            var settingsMgr = GameSettingsManager.Instance;
            if (settingsMgr != null)
            {
                settingsMgr.UpdateOptions(o => o.inputBindingOverrides = string.Empty);
            }

            OnBindingsChanged?.Invoke();
        }

        public static void SaveOverrides(InputActionAsset asset)
        {
            if (!asset) return;
            string json = asset.SaveBindingOverridesAsJson();
            PlayerPrefs.SetString(PrefsKey, json);
            PlayerPrefs.Save();

            var settingsMgr = GameSettingsManager.Instance;
            if (settingsMgr != null)
            {
                settingsMgr.UpdateOptions(o => o.inputBindingOverrides = json);
            }
        }

        public static void LoadOverrides(InputActionAsset asset)
        {
            if (!asset) return;

            string json = string.Empty;
            var settingsMgr = GameSettingsManager.Instance;
            if (settingsMgr != null && !string.IsNullOrEmpty(settingsMgr.CurrentOptions.inputBindingOverrides))
            {
                json = settingsMgr.CurrentOptions.inputBindingOverrides;
            }
            else if (PlayerPrefs.HasKey(PrefsKey))
            {
                json = PlayerPrefs.GetString(PrefsKey);
            }

            if (!string.IsNullOrEmpty(json))
            {
                asset.LoadBindingOverridesFromJson(json);
            }
        }
    }
}
