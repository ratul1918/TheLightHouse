using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace TheLightHouse.Editor
{
    public static class SaveSystemHardeningTest
    {
        public sealed class SaveTestReport
        {
            public bool AllPassed = true;
            public bool Slot1Passed;
            public bool Slot2Passed;
            public bool Slot3Passed;
            public bool CorruptedSaveRecoveryPassed;
            public bool FallbackRecoveryPassed;
            public bool FailedWriteRecoveryPassed;
            public bool CheckpointSaveRestorePassed;
            public bool RestartSceneStatePassed;
            public bool OfflineCloudSyncPassed;
            public bool CloudTimingRaceResolved;
            public System.Collections.Generic.List<string> Logs = new();
            public System.Collections.Generic.List<string> Errors = new();
        }

        public static SaveTestReport RunAllTests()
        {
            var report = new SaveTestReport();
            var go = new GameObject("SaveSystem_Test", typeof(SaveSystem));
            var saveSystem = go.GetComponent<SaveSystem>();
            typeof(SaveSystem).GetMethod("OnEnable", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(saveSystem, null);

            try
            {
                TestSlots(saveSystem, report);
                TestCorruptedSaveRecovery(saveSystem, report);
                TestFallbackRecovery(saveSystem, report);
                TestCheckpointSaveRestore(saveSystem, report);
                TestRestartSceneState(saveSystem, report);
                TestOfflineCloudSync(saveSystem, report);
                TestCloudTimingRaceResolved(saveSystem, report);
            }
            catch (Exception ex)
            {
                report.AllPassed = false;
                report.Errors.Add($"Unhandled Exception: {ex.Message}\n{ex.StackTrace}");
            }
            finally
            {
                typeof(SaveSystem).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(saveSystem, null);
                UnityEngine.Object.DestroyImmediate(go);
            }

            report.AllPassed = report.Slot1Passed &&
                               report.Slot2Passed &&
                               report.Slot3Passed &&
                               report.CorruptedSaveRecoveryPassed &&
                               report.FallbackRecoveryPassed &&
                               report.CheckpointSaveRestorePassed &&
                               report.RestartSceneStatePassed &&
                               report.OfflineCloudSyncPassed &&
                               report.CloudTimingRaceResolved;

            Debug.Log($"<color=#44FF88>[SaveSystem Hardening Test Complete]</color> Success: {report.AllPassed}\n" +
                      $"Slots (1,2,3): {report.Slot1Passed}, {report.Slot2Passed}, {report.Slot3Passed}\n" +
                      $"Corrupted Recovery: {report.CorruptedSaveRecoveryPassed}\n" +
                      $"Fallback Recovery: {report.FallbackRecoveryPassed}\n" +
                      $"Checkpoint & Restart: {report.CheckpointSaveRestorePassed}, {report.RestartSceneStatePassed}\n" +
                      $"Offline & Cloud Race: {report.OfflineCloudSyncPassed}, {report.CloudTimingRaceResolved}");

            return report;
        }

        private static void TestSlots(SaveSystem save, SaveTestReport report)
        {
            for (int slot = 1; slot <= 3; slot++)
            {
                save.SetCurrentSlot(slot);
                save.NewGame();
                save.Data.act = $"Act{slot}";
                save.Data.checkpointId = $"checkpoint-slot-{slot}";
                save.Save();

                if (!save.HasSlotSave(slot))
                {
                    report.Errors.Add($"Slot {slot} HasSlotSave returned false after Save()");
                    continue;
                }

                // Verify reloading
                save.SetCurrentSlot(slot);
                if (save.Data.act == $"Act{slot}" && save.Data.checkpointId == $"checkpoint-slot-{slot}")
                {
                    if (slot == 1) report.Slot1Passed = true;
                    if (slot == 2) report.Slot2Passed = true;
                    if (slot == 3) report.Slot3Passed = true;
                    report.Logs.Add($"Slot {slot} verified: Act={save.Data.act}, Checkpoint={save.Data.checkpointId}");
                }
                else
                {
                    report.Errors.Add($"Slot {slot} reload mismatch: Act={save.Data.act}");
                }
            }
        }

        private static void TestCorruptedSaveRecovery(SaveSystem save, SaveTestReport report)
        {
            save.SetCurrentSlot(2);
            save.Data.act = "Act2";
            save.Data.checkpointId = "valid-before-corruption";
            save.Save();
            // Save second time to rotate "valid-before-corruption" into .bak
            save.Save();

            string primaryPath = Path.Combine(Application.persistentDataPath, "thelighthouse-save-slot2.json");

            // Intentionally corrupt primary save with broken/truncated JSON
            File.WriteAllText(primaryPath, "{\"act\": \"Act2\", \"checkpointId\": \"BROKEN_JSON_GARBAGE{{{{!!");

            // Attempt to load
            bool loaded = save.Load();

            if (loaded && save.Data.checkpointId == "valid-before-corruption")
            {
                report.CorruptedSaveRecoveryPassed = true;
                report.Logs.Add("Corrupted primary save automatically detected and recovered from valid .bak file.");
            }
            else
            {
                report.Errors.Add($"Corrupted save recovery failed. Loaded: {loaded}, Checkpoint: {save.Data.checkpointId}");
            }
        }

        private static void TestFallbackRecovery(SaveSystem save, SaveTestReport report)
        {
            int slot = 3;
            save.SetCurrentSlot(slot);
            string primaryPath = Path.Combine(Application.persistentDataPath, $"thelighthouse-save-slot{slot}.json");
            string bakPath = primaryPath + ".bak";
            string fallbackPath = Path.Combine(Application.persistentDataPath, $"fallback-save-slot{slot}.json");

            // Write emergency fallback file
            var emergencySave = new GameSaveData { act = "Act3", checkpointId = "emergency-checkpoint" };
            File.WriteAllText(fallbackPath, JsonUtility.ToJson(emergencySave, true));

            // Corrupt or delete primary and backup
            File.WriteAllText(primaryPath, "CORRUPTED");
            if (File.Exists(bakPath)) File.WriteAllText(bakPath, "CORRUPTED_BAK");

            bool loaded = save.Load();
            if (loaded && save.Data.checkpointId == "emergency-checkpoint")
            {
                report.FallbackRecoveryPassed = true;
                report.Logs.Add("Emergency fallback save successfully recovered when both primary and backup were corrupted.");
            }
            else
            {
                report.Errors.Add($"Emergency fallback recovery failed. Loaded: {loaded}, Checkpoint: {save.Data.checkpointId}");
            }
        }

        private static void TestCheckpointSaveRestore(SaveSystem save, SaveTestReport report)
        {
            save.SetCurrentSlot(1);
            Vector3 testPos = new Vector3(14.5f, 3.2f, -8.7f);
            GameEvents.RaiseCheckpointReached(new CheckpointReachedEvent("storm-stairs-landing", testPos));

            if (save.Data.checkpointId == "storm-stairs-landing" && Vector3.Distance(save.Data.checkpoint, testPos) < 0.01f)
            {
                report.CheckpointSaveRestorePassed = true;
                report.Logs.Add($"Checkpoint event correctly recorded: {save.Data.checkpointId} at {save.Data.checkpoint}");
            }
            else
            {
                report.Errors.Add($"Checkpoint mismatch: {save.Data.checkpointId} vs storm-stairs-landing");
            }
        }

        private static void TestRestartSceneState(SaveSystem save, SaveTestReport report)
        {
            save.SetCurrentSlot(1);
            save.Data.options.sensitivity = 1.35f;
            save.Data.options.volume = 0.75f;
            save.Save();

            save.RestoreSceneState();
            if (Mathf.Approximately(AudioListener.volume, 0.75f))
            {
                report.RestartSceneStatePassed = true;
                report.Logs.Add("RestoreSceneState applied AudioListener.volume and options properly.");
            }
            else
            {
                report.Errors.Add($"AudioListener.volume mismatch: {AudioListener.volume}");
            }
        }

        private static void TestOfflineCloudSync(SaveSystem save, SaveTestReport report)
        {
            // Test LoadWithCloudSync without active cloud connection
            bool called = false;
            bool successResult = false;
            save.LoadWithCloudSync(success =>
            {
                called = true;
                successResult = success;
            });

            if (called && successResult)
            {
                report.OfflineCloudSyncPassed = true;
                report.Logs.Add("LoadWithCloudSync completed immediately in offline mode using local recovery.");
            }
            else
            {
                report.Errors.Add($"Offline cloud sync failed: called={called}, success={successResult}");
            }
        }

        private static void TestCloudTimingRaceResolved(SaveSystem save, SaveTestReport report)
        {
            // Simulate newer cloud save arriving
            save.SetCurrentSlot(1);
            save.Data.act = "Act1";
            save.Data.version = 1;
            save.Data.updatedAt = DateTime.UtcNow.AddHours(-2).ToString("O");
            save.Save();

            var remoteCloudPayload = new CloudSavePayload
            {
                act = "Act4",
                checkpoint = "cloud-cellar-reveal",
                version = 10,
                updatedAt = DateTime.UtcNow.ToString("O"),
                itemsJson = "{\"values\":[\"CraneRoomKey\"]}",
                pagesJson = "{\"values\":[\"marguerite\"]}",
                flagsJson = "{\"values\":[\"A4-B01\"]}",
                optionsJson = "{}"
            };

            // Call MergeCloud
            var mergeMethod = typeof(SaveSystem).GetMethod("MergeCloud", BindingFlags.NonPublic | BindingFlags.Instance);
            mergeMethod?.Invoke(save, new object[] { remoteCloudPayload });

            if (save.Data.act == "Act4" && save.Data.checkpointId == "cloud-cellar-reveal" && save.Data.items.Contains("CraneRoomKey"))
            {
                report.CloudTimingRaceResolved = true;
                report.Logs.Add("Cloud merge correctly prioritizes newer cloud save (Act4) over older local save (Act1).");
            }
            else
            {
                report.Errors.Add($"Cloud merge selection failed: Act={save.Data.act}");
            }
        }
    }
}
