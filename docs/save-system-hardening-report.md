# Save and Load Hardening Report

## Overview
Comprehensive hardening pass addressing corrupted saves, failed file writes, cloud-save timing races during Continue, multi-slot isolation, checkpoint persistence, and offline fallback.

---

## 1. Corrupted Saves & Failed Write Recovery
* **Atomic Writes**: Implemented in [`SaveSystem.cs`](file:///Users/rafiurrahman/The%20LightHouse/Assets/_Project/Scripts/Core/SaveSystem.cs): saves are written to a `.tmp` file first, flushed, and then moved to replace the primary target file, preventing partial or interrupted writes.
* **Automatic Backup Rotation**: Each valid save creates/updates a `.bak` copy (`thelighthouse-save-slot{slot}.json.bak`) before overwriting.
* **Corruption Detection & Recovery**:
  * If the primary slot file is corrupted (invalid JSON syntax, zero-byte file, truncated disk write, or deserialize exception), `SaveSystem.Load()` catches the error and falls back to `.bak`.
  * On valid backup recovery, the main file is restored from the backup automatically.
* **Emergency Fallback Storage**: If writing to the primary path encounters an exception (e.g., file lock, permissions, disk full), save data is saved to `fallback-save-slot{slot}.json`. If both primary and backup are lost, this fallback is recovered automatically.

---

## 2. Asynchronous Cloud-Save Timing Race Resolution
* **The Root Cause**: Previously, `ActFlowController.Continue()` called `saves.Load()` synchronously and immediately called `BeginTransition(saves.Data.act)`. `saves.Load()` initiated an asynchronous HTTP GET request to `/save` in the background. Because `BeginTransition` ran before the cloud GET request returned, the game loaded the old local act before `MergeCloud` could determine whether a newer cloud save existed.
* **The Fix**:
  * Implemented `SaveSystem.LoadWithCloudSync(Action<bool> onComplete)`.
  * Updated `ActFlowController.Continue()` to run `ContinueRoutine()`: displays loading screen, initiates `LoadWithCloudSync`, and waits for the cloud response (guarded by a 2.0s timeout for offline/high-latency networks).
  * `MergeCloud` evaluates timestamps/versions and updates save state **prior** to `BeginTransition(saves.Data.act)`.

---

## 3. Test Suite Verification Matrix ([`SaveSystemHardeningTest.cs`](file:///Users/rafiurrahman/The%20LightHouse/Assets/_Project/Scripts/Editor/SaveSystemHardeningTest.cs))
All 9 test conditions passed:

| Test Case | Condition Tested | Result |
|---|---|---|
| **Slot 1 Isolation** | Write, read, checkpoint, and reload in Slot 1 | **PASS** |
| **Slot 2 Isolation** | Write, read, checkpoint, and reload in Slot 2 | **PASS** |
| **Slot 3 Isolation** | Write, read, checkpoint, and reload in Slot 3 | **PASS** |
| **Corrupted Save Recovery** | Corrupted primary JSON recovers from `.bak` | **PASS** |
| **Fallback Recovery** | Primary & backup both corrupted; emergency fallback recovered | **PASS** |
| **Failed Write Handling** | Primary write exceptions handled safely without crash or data loss | **PASS** |
| **Checkpoint Persistence** | `CheckpointReachedEvent` captured, serialized, and restored | **PASS** |
| **Restart Scene State** | Scene restart restores options, inventory, audio, and checkpoint | **PASS** |
| **Cloud Race Resolution** | Cloud save with higher version/newer timestamp selected before scene load | **PASS** |
| **Offline Resilience** | Immediate, graceful fallback to local save when offline or unauthenticated | **PASS** |

---

## 4. Standalone Player Build
* Rebuilt and validated standalone macOS build at [`The LightHouse.app`](file:///Users/rafiurrahman/The%20LightHouse/Builds/macOS-Verification/The%20LightHouse.app).
