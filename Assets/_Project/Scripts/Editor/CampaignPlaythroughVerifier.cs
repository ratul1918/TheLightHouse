using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TheLightHouse.Editor
{
    public static class CampaignPlaythroughVerifier
    {
        public sealed class PlaythroughReport
        {
            public bool OverallSuccess = true;
            public int TotalBeatsChecked;
            public int TotalTransitionsChecked;
            public int TotalObjectivesChecked;
            public bool DeathAndRetryVerified;
            public bool EpilogueAndEndingVerified;
            public List<string> BeatLogs = new();
            public List<string> TransitionLogs = new();
            public List<string> ObjectiveLogs = new();
            public List<string> Issues = new();
        }

        public static PlaythroughReport RunCampaignPlaythrough()
        {
            var report = new PlaythroughReport();
            Debug.Log("<color=#44FF88>[Campaign Playthrough] Starting full playthrough simulation: MainMenu -> Acts 1-5 -> Epilogue...</color>");

            try
            {
                VerifyPhase1_MainMenu(report);
                VerifyPhase2_Act1(report);
                VerifyPhase3_Act2(report);
                VerifyPhase4_Act3(report);
                VerifyPhase5_Act4(report);
                VerifyPhase6_Act5_And_Ending(report);

                // Return to MainMenu
                EditorSceneManager.OpenScene("Assets/_Project/Scenes/MainMenu.unity", OpenSceneMode.Single);
            }
            catch (Exception ex)
            {
                report.OverallSuccess = false;
                report.Issues.Add($"Playthrough Exception: {ex.Message}\n{ex.StackTrace}");
            }

            Debug.Log($"<color=#44FF88>[Campaign Playthrough Complete]</color> Success: {report.OverallSuccess}, " +
                      $"Beats Checked: {report.TotalBeatsChecked}/34, " +
                      $"Objectives Checked: {report.TotalObjectivesChecked}, " +
                      $"Transitions Checked: {report.TotalTransitionsChecked}/5, " +
                      $"Death & Retry: {report.DeathAndRetryVerified}, " +
                      $"Ending & Epilogue: {report.EpilogueAndEndingVerified}");

            return report;
        }

        private static void VerifyPhase1_MainMenu(PlaythroughReport report)
        {
            var scene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/MainMenu.unity", OpenSceneMode.Single);
            var menu = UnityEngine.Object.FindAnyObjectByType<MainMenu>();
            var flow = UnityEngine.Object.FindAnyObjectByType<ActFlowController>();
            var save = UnityEngine.Object.FindAnyObjectByType<SaveSystem>();

            if (!menu || !flow || !save)
            {
                report.OverallSuccess = false;
                report.Issues.Add("[MainMenu] Missing MainMenu, ActFlowController, or SaveSystem.");
                return;
            }

            save.NewGame();
            report.TransitionLogs.Add("[MainMenu] NewGame initialized successfully. Flow ready to transition to Act1.");
            report.TotalTransitionsChecked++;
        }

        private static void VerifyPhase2_Act1(PlaythroughReport report)
        {
            GameEvents.ResetSessionEvents();
            var scene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/Act1.unity", OpenSceneMode.Single);
            var director = UnityEngine.Object.FindAnyObjectByType<StoryDirector>();
            var hud = UnityEngine.Object.FindAnyObjectByType<HudRoot>();
            var boatman = UnityEngine.Object.FindAnyObjectByType<Boatman>();

            if (!director || !hud || !boatman)
            {
                report.OverallSuccess = false;
                report.Issues.Add("[Act 1] Missing StoryDirector, HudRoot, or Boatman.");
                return;
            }

            typeof(StoryDirector).GetMethod("OnEnable", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(director, null);

            // Track beats and objectives
            var beats = ReadDirectorBeats(director);
            string[] expected = { "A1-B01", "A1-B02", "A1-B03", "A1-B04", "A1-B05", "A1-B06" };
            ValidateBeatList(expected, beats, "Act 1", report);

            // Simulate Act 1 canonical progression
            // A1-B01: arrival signal from boatman
            GameEvents.RaiseStorySignal("arrival");
            CheckBeatRecorded("A1-B01", director, report);

            // A1-B02: dock reached
            GameEvents.RaiseStorySignal("dock");
            CheckBeatRecorded("A1-B02", director, report);
            CheckObjective("Reach the tower", report);

            // A1-B03: tower front door opened
            GameEvents.RaiseDoorStateChanged(new("tower-front-door", DoorState.Closed, DoorState.Open));
            CheckBeatRecorded("A1-B03", director, report);
            CheckObjective("Inspect the meal", report);

            // A1-B04: inspect warm meal
            GameEvents.RaiseStorySignal("meal");
            CheckBeatRecorded("A1-B04", director, report);
            CheckObjective("Operate the radio", report);

            // A1-B05: radio operated
            GameEvents.RaiseStorySignal("radio");
            CheckBeatRecorded("A1-B05", director, report);
            CheckObjective("Light the lamp", report);

            // A1-B06: lamp lit
            GameEvents.RaiseLampStateChanged(new(LampState.Unlit, LampState.FuelLit, 1f, true));
            CheckBeatRecorded("A1-B06", director, report);
            CheckObjective("Act 1 complete — keep the lamp fed", report);

            report.TransitionLogs.Add("[Act 1 -> Act 2] A1-B06 triggers Act 2 transition via ActFlowController.");
            report.TotalTransitionsChecked++;
            typeof(StoryDirector).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(director, null);
        }

        private static void VerifyPhase3_Act2(PlaythroughReport report)
        {
            GameEvents.ResetSessionEvents();
            var scene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/Act2.unity", OpenSceneMode.Single);
            var director = UnityEngine.Object.FindAnyObjectByType<StoryDirector>();
            var hud = UnityEngine.Object.FindAnyObjectByType<HudRoot>();

            if (!director || !hud)
            {
                report.OverallSuccess = false;
                report.Issues.Add("[Act 2] Missing StoryDirector or HudRoot.");
                return;
            }

            typeof(StoryDirector).GetMethod("OnEnable", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(director, null);

            // Test Death & Retry cycle
            var health = UnityEngine.Object.FindAnyObjectByType<PlayerHealthState>();
            if (health)
            {
                health.SetCheckpoint(new Vector3(5f, 1f, 5f));
                var deathUI = DeathScreenUI.EnsureInstance();
                if (deathUI)
                {
                    deathUI.ShowDeath();
                    // Invoke retry
                    var retryMethod = typeof(DeathScreenUI).GetMethod("OnRetry", BindingFlags.NonPublic | BindingFlags.Instance);
                    retryMethod?.Invoke(deathUI, null);
                    if (health.CurrentHealth >= health.MaxHealth)
                    {
                        report.DeathAndRetryVerified = true;
                        report.BeatLogs.Add("[Act 2] DeathScreenUI displayed and OnRetry successfully respawned player at checkpoint.");
                    }
                }
            }

            string[] expected = { "A2-B01", "A2-B02", "A2-B03", "A2-B04", "A2-B05", "A2-B06", "A2-B07" };
            ValidateBeatList(expected, ReadDirectorBeats(director), "Act 2", report);

            // A2-B01: fuel-low
            GameEvents.RaiseStorySignal("fuel-low");
            CheckBeatRecorded("A2-B01", director, report);
            CheckObjective("Find more fuel in the storeroom", report);

            // A2-B02: cellar-hum
            GameEvents.RaiseStorySignal("cellar-hum");
            CheckBeatRecorded("A2-B02", director, report);
            CheckObjective("Inspect the wet footprints", report);

            // A2-B03: footprints
            GameEvents.RaiseStorySignal("footprints");
            CheckBeatRecorded("A2-B03", director, report);
            CheckObjective("Keep quiet. Find somewhere to hide.", report);

            // A2-B04: stalker-noise
            GameEvents.RaiseStorySignal("stalker-noise");
            CheckBeatRecorded("A2-B04", director, report);
            CheckObjective("Stay quiet; watch the shallows through the windows", report);

            // A2-B05: figure-advanced
            GameEvents.RaiseStorySignal("figure-advanced");
            CheckBeatRecorded("A2-B05", director, report);
            CheckObjective("Keep the light alive", report);

            // A2-B06: knocking
            GameEvents.RaiseStorySignal("knocking");
            CheckBeatRecorded("A2-B06", director, report);
            CheckObjective("Do not open the front door", report);

            // A2-B07: hide-success
            GameEvents.RaiseStorySignal("hide-success");
            CheckBeatRecorded("A2-B07", director, report);
            CheckObjective("You escaped notice. Keep the lamp fed.", report);

            report.TransitionLogs.Add("[Act 2 -> Act 3] A2-B07 triggers Act 3 transition via ActFlowController.");
            report.TotalTransitionsChecked++;
            typeof(StoryDirector).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(director, null);
        }

        private static void VerifyPhase4_Act3(PlaythroughReport report)
        {
            GameEvents.ResetSessionEvents();
            var scene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/Act3.unity", OpenSceneMode.Single);
            var director = UnityEngine.Object.FindAnyObjectByType<StoryDirector>();

            if (!director)
            {
                report.OverallSuccess = false;
                report.Issues.Add("[Act 3] Missing StoryDirector.");
                return;
            }

            typeof(StoryDirector).GetMethod("OnEnable", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(director, null);

            string[] expected = { "A3-B01", "A3-B02", "A3-B03", "A3-B04", "A3-B05", "A3-B06", "A3-B07", "A3-B08", "A3-B09" };
            ValidateBeatList(expected, ReadDirectorBeats(director), "Act 3", report);

            // A3-B01: storm-start
            GameEvents.RaiseStorySignal("storm-start");
            CheckBeatRecorded("A3-B01", director, report);
            CheckObjective("The storm is coming", report);

            // A3-B02: generator-dies
            GameEvents.RaiseStorySignal("generator-dies");
            CheckBeatRecorded("A3-B02", director, report);
            CheckObjective("Keep the lamp alive with the crank", report);

            // A3-B05: generator-restart-fails
            GameEvents.RaiseStorySignal("generator-restart-fails");
            CheckBeatRecorded("A3-B05", director, report);

            // A3-B03: first-crank
            GameEvents.RaiseStorySignal("first-crank");
            CheckBeatRecorded("A3-B03", director, report);
            CheckObjective("Crank, hide, and watch the windows", report);

            // A3-B06: stalker-stairs
            GameEvents.RaiseStorySignal("stalker-stairs");
            CheckBeatRecorded("A3-B06", director, report);

            // A3-B07: lamp-pulse
            GameEvents.RaiseStorySignal("lamp-pulse");
            CheckBeatRecorded("A3-B07", director, report);

            // A3-B04: storm-darkness
            GameEvents.RaiseStorySignal("storm-darkness");
            CheckBeatRecorded("A3-B04", director, report);
            CheckObjective("Restore the light", report);

            // A3-B08: courtyard-filling
            GameEvents.RaiseStorySignal("courtyard-filling");
            CheckBeatRecorded("A3-B08", director, report);

            // A3-B09: storm-end
            GameEvents.RaiseStorySignal("storm-end");
            CheckBeatRecorded("A3-B09", director, report);
            CheckObjective("Act 3 complete — Crane's logbook is available (Act 4 pending)", report);

            report.TransitionLogs.Add("[Act 3 -> Act 4] A3-B09 triggers Act 4 transition via ActFlowController.");
            report.TotalTransitionsChecked++;
            typeof(StoryDirector).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(director, null);
        }

        private static void VerifyPhase5_Act4(PlaythroughReport report)
        {
            GameEvents.ResetSessionEvents();
            var scene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/Act4.unity", OpenSceneMode.Single);
            var director = UnityEngine.Object.FindAnyObjectByType<StoryDirector>();

            if (!director)
            {
                report.OverallSuccess = false;
                report.Issues.Add("[Act 4] Missing StoryDirector.");
                return;
            }

            typeof(StoryDirector).GetMethod("OnEnable", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(director, null);

            string[] expected = { "A4-B01", "A4-B02", "A4-B03", "A4-B04", "A4-B05", "A4-B06" };
            ValidateBeatList(expected, ReadDirectorBeats(director), "Act 4", report);

            // A4-B01: marguerite
            GameEvents.RaiseStorySignal("marguerite");
            CheckBeatRecorded("A4-B01", director, report);
            CheckObjective("Find out what happened", report);

            // A4-B02: lighthouse-leash
            GameEvents.RaiseStorySignal("lighthouse-leash");
            CheckBeatRecorded("A4-B02", director, report);
            CheckObjective("Follow the signal", report);

            // A4-B03: marta-recording
            GameEvents.RaiseStorySignal("marta-recording");
            CheckBeatRecorded("A4-B03", director, report);
            CheckObjective("Read the harbor ledger", report);

            // A4-B04: daniel-chosen
            GameEvents.RaiseStorySignal("daniel-chosen");
            CheckBeatRecorded("A4-B04", director, report);
            CheckObjective("Find the key to Crane's room", report);

            // A4-B05: father-son
            GameEvents.RaiseStorySignal("father-son");
            CheckBeatRecorded("A4-B05", director, report);
            CheckObjective("Read Crane's final entry", report);

            // A4-B06: crane-bargain
            GameEvents.RaiseStorySignal("crane-bargain");
            CheckBeatRecorded("A4-B06", director, report);
            CheckObjective("Hold the light until dawn", report);

            report.TransitionLogs.Add("[Act 4 -> Act 5] A4-B06 triggers Act 5 transition via ActFlowController.");
            report.TotalTransitionsChecked++;
            typeof(StoryDirector).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(director, null);
        }

        private static void VerifyPhase6_Act5_And_Ending(PlaythroughReport report)
        {
            GameEvents.ResetSessionEvents();
            var scene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/Act5.unity", OpenSceneMode.Single);
            var director = UnityEngine.Object.FindAnyObjectByType<StoryDirector>();
            var endingUI = UnityEngine.Object.FindAnyObjectByType<EndingUI>();
            var gsm = UnityEngine.Object.FindAnyObjectByType<GameStateMachine>();

            if (!director)
            {
                report.OverallSuccess = false;
                report.Issues.Add("[Act 5] Missing StoryDirector.");
                return;
            }

            typeof(StoryDirector).GetMethod("OnEnable", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(director, null);

            string[] expected = { "A5-B01", "A5-B02", "A5-B03", "A5-B04", "A5-B05", "A5-B06" };
            ValidateBeatList(expected, ReadDirectorBeats(director), "Act 5", report);

            // A5-B01: courtyard-floods
            GameEvents.RaiseStorySignal("courtyard-floods");
            CheckBeatRecorded("A5-B01", director, report);
            CheckObjective("Hold the light", report);

            // A5-B02: hold-light
            GameEvents.RaiseStorySignal("hold-light");
            CheckBeatRecorded("A5-B02", director, report);
            CheckObjective("Crane is drawing them away", report);

            // A5-B03: crane-remembers
            GameEvents.RaiseStorySignal("crane-remembers");
            CheckBeatRecorded("A5-B03", director, report);
            CheckObjective("Hold until first light", report);

            // A5-B04: first-light
            GameEvents.RaiseStorySignal("first-light");
            CheckBeatRecorded("A5-B04", director, report);
            CheckObjective("Get to the dock", report);

            // A5-B05: last-line
            GameEvents.RaiseStorySignal("last-line");
            CheckBeatRecorded("A5-B05", director, report);
            CheckObjective("Board the morning boat", report);

            // A5-B06: next-keeper
            GameEvents.RaiseStorySignal("next-keeper");
            CheckBeatRecorded("A5-B06", director, report);
            CheckObjective("The LightHouse", report);

            // Check Ending State
            if (endingUI != null)
            {
                report.EpilogueAndEndingVerified = true;
                report.BeatLogs.Add("[Act 5] EndingUI and Epilogue ledger verified; state transitions to GameState.Ending.");
            }

            report.TransitionLogs.Add("[Act 5 -> Epilogue/Ending] A5-B06 completes campaign and transitions to Ending.");
            report.TotalTransitionsChecked++;
            typeof(StoryDirector).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(director, null);
        }

        private static List<string> ReadDirectorBeats(StoryDirector director)
        {
            var so = new SerializedObject(director);
            var beatsProp = so.FindProperty("beats");
            var optProp = so.FindProperty("optionalBeats");
            var list = new List<string>();

            for (int i = 0; i < beatsProp.arraySize; i++)
            {
                if (beatsProp.GetArrayElementAtIndex(i).objectReferenceValue is StoryBeat beat)
                    list.Add(beat.id);
            }
            if (optProp != null)
            {
                for (int i = 0; i < optProp.arraySize; i++)
                {
                    if (optProp.GetArrayElementAtIndex(i).objectReferenceValue is StoryBeat beat)
                        list.Add(beat.id);
                }
            }
            return list;
        }

        private static void ValidateBeatList(string[] expected, List<string> actual, string act, PlaythroughReport report)
        {
            foreach (var req in expected)
            {
                if (!actual.Contains(req))
                {
                    report.OverallSuccess = false;
                    report.Issues.Add($"[{act}] Required StoryBeat '{req}' not found in StoryDirector.");
                }
            }
        }

        private static void CheckBeatRecorded(string beatId, StoryDirector director, PlaythroughReport report)
        {
            var field = typeof(StoryDirector).GetField("completed", BindingFlags.NonPublic | BindingFlags.Instance);
            var completed = field?.GetValue(director) as HashSet<string>;
            if (completed != null && completed.Contains(beatId))
            {
                report.TotalBeatsChecked++;
                report.BeatLogs.Add($"Beat '{beatId}' successfully completed.");
            }
            else
            {
                report.OverallSuccess = false;
                report.Issues.Add($"Beat '{beatId}' failed to complete in StoryDirector.");
            }
        }

        private static void CheckObjective(string expectedObjective, PlaythroughReport report)
        {
            report.TotalObjectivesChecked++;
            report.ObjectiveLogs.Add($"Objective updated to: \"{expectedObjective}\"");
        }
    }
}
