using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using TheLightHouse.Voice;

namespace TheLightHouse.Editor
{
    public static class AudioSystemValidationTest
    {
        public sealed class AudioTestReport
        {
            public bool AllPassed = true;
            public bool DatabaseComplete;
            public bool DrownedLureVerified;
            public bool CraneFootstepsVerified;
            public bool CrankEffortVerified;
            public bool LullabyAndFarewellVerified;
            public bool ResourcesLoadingVerified;
            public System.Collections.Generic.List<string> Logs = new();
            public System.Collections.Generic.List<string> Errors = new();
        }

        public static AudioTestReport RunAudioTests()
        {
            var report = new AudioTestReport();

            try
            {
                // 1. Verify 60/60 Voice Database Lines
                var db = AssetDatabase.LoadAssetAtPath<VoiceDatabase>(VoiceDatabaseBuilder.DatabasePath);
                if (db != null)
                {
                    int total = db.AllLines.Count(l => l != null);
                    int withClip = db.AllLines.Count(l => l != null && l.Clip != null);
                    if (total == 60 && withClip == 60)
                    {
                        report.DatabaseComplete = true;
                        report.Logs.Add($"VoiceDatabase verified: 60/60 voice lines have valid AudioClips assigned.");
                    }
                    else
                    {
                        report.Errors.Add($"VoiceDatabase incomplete: {withClip}/{total} clips assigned.");
                    }
                }
                else
                {
                    report.Errors.Add("VoiceDatabase asset not found.");
                }

                // 2. Verify Drowned's Sung Lure
                var lureClip = Resources.Load<AudioClip>("Audio/DROWNED_LURE_SUNG")
                            ?? AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Audio/Voice/TheDrowned/DROWNED_LURE_SUNG.wav");
                if (lureClip != null && lureClip.length >= 4.0f)
                {
                    report.DrownedLureVerified = true;
                    report.Logs.Add($"Drowned sung lure verified: {lureClip.name} ({lureClip.length:F2}s, {lureClip.frequency}Hz).");
                }
                else
                {
                    report.Errors.Add("DROWNED_LURE_SUNG audio clip missing or invalid length.");
                }

                // 3. Verify Crane's Dragging Footsteps
                var dragClip = Resources.Load<AudioClip>("Audio/CRANE_DRAG_STEP")
                            ?? AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Audio/Voice/ThomasCrane/CRANE_DRAG_STEP.wav");
                if (dragClip != null && dragClip.length > 0.5f)
                {
                    report.CraneFootstepsVerified = true;
                    report.Logs.Add($"Crane dragging footstep verified: {dragClip.name} ({dragClip.length:F2}s).");
                }
                else
                {
                    report.Errors.Add("CRANE_DRAG_STEP audio clip missing or invalid.");
                }

                // 4. Verify Daniel Crank Effort
                var crankClip = Resources.Load<AudioClip>("Audio/REACT_DAN_CRANK_EFFORT")
                             ?? AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Audio/Voice/DanielHale/REACT_DAN_CRANK_EFFORT.wav");
                if (crankClip != null && crankClip.length > 0.5f)
                {
                    report.CrankEffortVerified = true;
                    report.Logs.Add($"Daniel crank effort verified: {crankClip.name} ({crankClip.length:F2}s).");
                }
                else
                {
                    report.Errors.Add("REACT_DAN_CRANK_EFFORT audio clip missing or invalid.");
                }

                // 5. Verify Lullaby and Shoreline Farewell
                var farewellClip = Resources.Load<AudioClip>("Audio/Voice_Crane_ShorelineFarewell")
                                ?? Resources.Load<AudioClip>("Audio/CRANE_LULLABY_FINAL")
                                ?? AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Audio/Voice_Crane_ShorelineFarewell.wav");
                var shantyClip = Resources.Load<AudioClip>("Audio/Voice_Crane_CellarShanty")
                              ?? Resources.Load<AudioClip>("Audio/CRANE_HUM_01")
                              ?? AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Audio/Voice_Crane_CellarShanty.wav");
                if (farewellClip != null && shantyClip != null)
                {
                    report.LullabyAndFarewellVerified = true;
                    report.Logs.Add($"Crane lullaby and farewell verified: Farewell={farewellClip.name} ({farewellClip.length:F2}s), Shanty={shantyClip.name} ({shantyClip.length:F2}s).");
                }
                else
                {
                    report.Errors.Add("Crane lullaby/farewell audio clips missing.");
                }

                // 6. Verify Standalone Resources Loading
                string[] requiredResources =
                {
                    "Audio/DROWNED_LURE_SUNG",
                    "Audio/CRANE_DRAG_STEP",
                    "Audio/REACT_DAN_CRANK_EFFORT",
                    "Audio/Voice_Crane_ShorelineFarewell",
                    "Audio/Voice_Crane_CellarShanty",
                    "Audio/Voice_Marta_RadioGreeting",
                    "Audio/Voice_Marta_HarborBoardLedger"
                };
                bool allResOk = true;
                foreach (var req in requiredResources)
                {
                    var c = Resources.Load<AudioClip>(req);
                    if (c == null)
                    {
                        allResOk = false;
                        report.Errors.Add($"Resources audio clip '{req}' failed to load.");
                    }
                }
                if (allResOk)
                {
                    report.ResourcesLoadingVerified = true;
                    report.Logs.Add("All critical standalone audio clips verified loadable via Resources.Load<AudioClip>.");
                }
            }
            catch (Exception ex)
            {
                report.AllPassed = false;
                report.Errors.Add($"Audio Test Exception: {ex.Message}\n{ex.StackTrace}");
            }

            report.AllPassed = report.DatabaseComplete &&
                               report.DrownedLureVerified &&
                               report.CraneFootstepsVerified &&
                               report.CrankEffortVerified &&
                               report.LullabyAndFarewellVerified &&
                               report.ResourcesLoadingVerified;

            Debug.Log($"<color=#44FF88>[Audio System Test Complete]</color> Success: {report.AllPassed}\n" +
                      $"Database 60/60: {report.DatabaseComplete}\n" +
                      $"Drowned Lure: {report.DrownedLureVerified}\n" +
                      $"Crane Drag Footsteps: {report.CraneFootstepsVerified}\n" +
                      $"Daniel Crank Effort: {report.CrankEffortVerified}\n" +
                      $"Lullaby & Farewell: {report.LullabyAndFarewellVerified}\n" +
                      $"Resources Loading: {report.ResourcesLoadingVerified}");

            return report;
        }
    }
}
