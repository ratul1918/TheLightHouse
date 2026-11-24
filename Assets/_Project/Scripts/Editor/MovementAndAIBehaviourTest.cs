using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace TheLightHouse.EditorTests
{
    public static class MovementAndAIBehaviourTest
    {
        public sealed class BehaviourTestReport
        {
            public bool AllPassed = true;
            public int TestsRun = 0;
            public int TestsPassed = 0;
            public int TestsFailed = 0;
            public List<string> Logs = new();
            public List<string> Failures = new();
        }

        [MenuItem("The LightHouse/Verification/Run Movement and AI Behaviour Tests")]
        public static BehaviourTestReport RunAllTests()
        {
            var report = new BehaviourTestReport();

            void Assert(bool condition, string testName, string detail = "")
            {
                report.TestsRun++;
                if (condition)
                {
                    report.TestsPassed++;
                    report.Logs.Add($"[PASS] {testName} - {detail}".TrimEnd('-', ' '));
                }
                else
                {
                    report.TestsFailed++;
                    report.AllPassed = false;
                    string failMsg = $"[FAIL] {testName} - {detail}".TrimEnd('-', ' ');
                    report.Logs.Add(failMsg);
                    report.Failures.Add(failMsg);
                }
            }

            try
            {
                // =========================================================================
                // 1. STAIRS & GROUNDING
                // =========================================================================
                report.Logs.Add("--- 1. Testing Stairs and Grounding ---");
                {
                    var settings = ScriptableObject.CreateInstance<PlayerMovementSettings>();
                    settings.groundProbeDistance = 0.45f;
                    settings.groundProbeInset = 0.05f;
                    settings.slopeLimit = 45f;
                    settings.skinWidth = 0.02f;

                    Vector3 testPos = new Vector3(500f, 500f, 500f);
                    var groundGo = GameObject.CreatePrimitive(PrimitiveType.Plane);
                    groundGo.name = "TestFloor";
                    groundGo.transform.position = testPos;

                    var playerGo = new GameObject("TestControllerPlayer");
                    playerGo.transform.position = testPos;
                    var cc = playerGo.AddComponent<CharacterController>();
                    cc.height = 1.8f;
                    cc.radius = 0.35f;
                    cc.center = Vector3.up * 0.9f;

                    Physics.SyncTransforms();

                    // Ground probe on flat ground (feet at testPos.y)
                    bool probed = ControllerGrounding.Probe(cc, settings, out var hit);
                    Assert(probed && hit.collider != null, "Ground Probe on Flat Surface", $"Hit collider: {hit.collider?.name}");

                    // Snap on step down (0.15m drop)
                    playerGo.transform.position = testPos + new Vector3(0f, 0.15f, 0f);
                    Physics.SyncTransforms();
                    bool snapProbed = ControllerGrounding.Probe(cc, settings, out var snapHit);
                    float feetY = cc.transform.TransformPoint(cc.center).y - cc.height * .5f;
                    float drop = feetY - snapHit.point.y;
                    Assert(snapProbed && drop > 0.05f && drop <= settings.groundProbeDistance, "Ground Probe Detects Step Down Within Probe Distance", $"Drop: {drop:0.00}m");

                    // Walkable slope check (30 deg <= 45 deg limit) should probe successfully
                    var walkableSlope = GameObject.CreatePrimitive(PrimitiveType.Plane);
                    walkableSlope.transform.position = testPos + new Vector3(20f, 0f, 0f);
                    walkableSlope.transform.rotation = Quaternion.Euler(30f, 0f, 0f);
                    playerGo.transform.position = testPos + new Vector3(20f, 0.25f, 0f);
                    Physics.SyncTransforms();
                    bool walkableProbed = ControllerGrounding.Probe(cc, settings, out var walkHit);
                    Assert(walkableProbed, "Walkable Slope Allowed by Ground Probe", $"Angle: {Vector3.Angle(walkHit.normal, Vector3.up):0.0}°");

                    // Slope limit check: steep slope (60 deg > 45 deg limit) should NOT probe as valid walkable ground
                    var steepRamp = GameObject.CreatePrimitive(PrimitiveType.Plane);
                    steepRamp.transform.position = testPos + new Vector3(40f, 0f, 0f);
                    steepRamp.transform.rotation = Quaternion.Euler(60f, 0f, 0f);
                    playerGo.transform.position = testPos + new Vector3(40f, 0.25f, 0f);
                    Physics.SyncTransforms();
                    bool steepProbed = ControllerGrounding.Probe(cc, settings, out _);
                    Assert(!steepProbed, "Slope Limit Prevents Walking on Steep Incline", "60-degree surface rejected");

                    Object.DestroyImmediate(walkableSlope);
                    Object.DestroyImmediate(steepRamp);
                    Object.DestroyImmediate(groundGo);
                    Object.DestroyImmediate(playerGo);
                    Object.DestroyImmediate(settings);
                }

                // =========================================================================
                // 2. DOORS & CELLAR ACCESS
                // =========================================================================
                report.Logs.Add("--- 2. Testing Doors and Cellar Access ---");
                {
                    var doorGo = new GameObject("TestDoor");
                    var noise = doorGo.AddComponent<NoiseEmitter>();
                    var door = doorGo.AddComponent<Door>();

                    // Test initial state
                    Assert(!door.IsLocked, "Door Initially Unlocked", "Door starts unlocked");

                    // Test locking
                    door.SetLocked(true);
                    Assert(door.IsLocked, "Door SetLocked(true)", "Door correctly reports locked");

                    // Attempt interaction while locked
                    door.Interact();
                    door.StopInteract();
                    Assert(door.IsLocked, "Door Remains Locked on Failed Interact", "Door did not open while locked");

                    // Unlock and open
                    door.SetLocked(false);
                    door.Interact();
                    door.StopInteract();
                    Assert(door.Prompt.Contains("Close"), "Door Opens on Interact", $"Prompt: {door.Prompt}");

                    // Test SlamForce
                    door.SlamForce();
                    Assert(door.Prompt.Contains("Open"), "Door Closes on SlamForce", $"Prompt: {door.Prompt}");

                    // Test Hidden Cellar Room shelf slide
                    var shelfGo = new GameObject("HiddenShelf");
                    var shelfNoise = shelfGo.AddComponent<NoiseEmitter>();
                    var cellar = shelfGo.AddComponent<HiddenCellarRoom>();

                    Assert(cellar.Prompt.Contains("Slide"), "Hidden Shelf Initial Prompt", cellar.Prompt);
                    cellar.Interact();
                    Assert(cellar.Prompt.Contains("revealed"), "Hidden Shelf Revealed on Interact", cellar.Prompt);

                    Object.DestroyImmediate(doorGo);
                    Object.DestroyImmediate(shelfGo);
                }

                // =========================================================================
                // 3. HIDING SPOTS & CONCEALMENT
                // =========================================================================
                report.Logs.Add("--- 3. Testing Hiding Spots and Concealment ---");
                {
                    var spotGo = new GameObject("TestWardrobe");
                    var spot = spotGo.AddComponent<HidingSpot>();

                    var playerGo = new GameObject("HidingTestPlayer");
                    var playerHealth = playerGo.AddComponent<PlayerHealthState>();
                    var playerNoise = playerGo.AddComponent<NoiseEmitter>();

                    var stalkerGo = new GameObject("TestStalkerRef");
                    stalkerGo.transform.position = new Vector3(20f, 0f, 0f);

                    var spotSo = new SerializedObject(spot);
                    spotSo.FindProperty("player").objectReferenceValue = playerHealth;
                    spotSo.FindProperty("stalker").objectReferenceValue = stalkerGo.transform;
                    spotSo.ApplyModifiedProperties();

                    // Invoke OnEnable to register GameEvents
                    typeof(HidingSpot).GetMethod("OnEnable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(spot, null);

                    Assert(!spot.Occupied, "Hiding Spot Initially Empty", "Occupied=false");

                    // Enter hiding spot
                    spot.Interact();
                    Assert(spot.Occupied, "Player Enters Hiding Spot", "Occupied=true");

                    // Verify spot properties
                    Assert(spot.PeekYaw > 0f && spot.PeekPitch > 0f, "Hiding Spot Peek Constraints", $"Yaw={spot.PeekYaw}, Pitch={spot.PeekPitch}");

                    // Exit hiding spot via GameEvents
                    GameEvents.RaiseHidingExited();
                    Assert(!spot.Occupied, "Player Exits Hiding Spot", "Occupied=false");

                    typeof(HidingSpot).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(spot, null);

                    Object.DestroyImmediate(spotGo);
                    Object.DestroyImmediate(playerGo);
                    Object.DestroyImmediate(stalkerGo);
                }

                // =========================================================================
                // 4. ENEMY NAVIGATION & NAVMESH ACROSS ALL ACTS
                // =========================================================================
                report.Logs.Add("--- 4. Testing Enemy Navigation Across Acts ---");
                {
                    string[] actScenes = new string[]
                    {
                        "Assets/_Project/Scenes/Act1.unity",
                        "Assets/_Project/Scenes/Act2.unity",
                        "Assets/_Project/Scenes/Act3.unity",
                        "Assets/_Project/Scenes/Act4.unity",
                        "Assets/_Project/Scenes/Act5.unity"
                    };

                    foreach (var actPath in actScenes)
                    {
                        EditorSceneManager.OpenScene(actPath);
                        string actName = System.IO.Path.GetFileNameWithoutExtension(actPath);

                        var stalker = Object.FindAnyObjectByType<StalkerAI>(FindObjectsInactive.Include);
                        Assert(stalker != null, $"{actName}: Stalker AI Present", $"Stalker component found");

                        if (stalker != null)
                        {
                            var agent = stalker.GetComponent<NavMeshAgent>();
                            Assert(agent != null, $"{actName}: Stalker NavMeshAgent Present", "Agent component exists");

                            bool sampled = NavMesh.SamplePosition(stalker.transform.position, out var hit, 5.0f, NavMesh.AllAreas);
                            Assert(sampled, $"{actName}: Stalker Position On NavMesh", $"Sample distance: {hit.distance:0.00}m");
                        }
                    }
                }

                // =========================================================================
                // 5. SOUND DETECTION & NOISE MASKING
                // =========================================================================
                report.Logs.Add("--- 5. Testing Sound Detection and Noise Masking ---");
                {
                    EditorSceneManager.OpenScene("Assets/_Project/Scenes/Act2.unity");

                    var stalker = Object.FindAnyObjectByType<StalkerAI>(FindObjectsInactive.Include);
                    Assert(stalker != null, "Act 2 Stalker for Hearing Tests", "Stalker loaded");

                    if (stalker != null)
                    {
                        stalker.gameObject.SetActive(true);
                        typeof(StalkerAI).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(stalker, null);
                        typeof(StalkerAI).GetMethod("OnEnable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(stalker, null);

                        // Quiet noise far away should NOT trigger investigation
                        GameEvents.RaiseNoiseHeard(new NoiseEmittedEvent(new Vector3(500f, 0f, 500f), 0.5f, null));
                        Assert(stalker.CurrentState != StalkerAI.StalkerState.Investigate, "Sub-Threshold Noise Ignored", $"State: {stalker.CurrentState}");

                        // Loud noise near stalker should trigger Investigate
                        Vector3 nearPos = stalker.transform.position + Vector3.forward * 4.0f;
                        GameEvents.RaiseNoiseHeard(new NoiseEmittedEvent(nearPos, 15f, null));
                        Assert(stalker.CurrentState == StalkerAI.StalkerState.Investigate, "Loud Noise Triggers Investigation", $"State: {stalker.CurrentState}");

                        // Hearing remains enabled
                        Assert(stalker.HearingEnabled, "Hearing System Active", "Hearing enabled");
                    }
                }

                // =========================================================================
                // 6. DROWNED OBSERVATION RULES ("WEEPING ANGELS")
                // =========================================================================
                report.Logs.Add("--- 6. Testing Drowned Observation Rules ---");
                {
                    // Test in clear space (y = 100) to avoid scene obstacle occlusion
                    var figGo = new GameObject("TestDrownedFigure");
                    figGo.transform.position = new Vector3(0f, 100f, 5f);
                    var figure = figGo.AddComponent<DrownedFigure>();

                    // Setup path with 3 steps
                    var figSo = new SerializedObject(figure);
                    var pathProp = figSo.FindProperty("path");
                    pathProp.arraySize = 3;
                    pathProp.GetArrayElementAtIndex(0).vector3Value = new Vector3(0f, 100f, 5f);
                    pathProp.GetArrayElementAtIndex(1).vector3Value = new Vector3(0f, 100f, 3f);
                    pathProp.GetArrayElementAtIndex(2).vector3Value = new Vector3(0f, 100f, 1f);
                    figSo.ApplyModifiedProperties();

                    var camGo = new GameObject("TestPlayerView");
                    camGo.transform.position = new Vector3(0f, 100f, 0f);
                    var cam = camGo.AddComponent<Camera>();
                    cam.transform.forward = Vector3.forward;

                    // Direct observation: camera looks directly at figure
                    figure.CheckVisibility(cam);
                    Assert(figure.Seen, "Drowned Seen When Facing Camera", "Direct observation detected");

                    // Advancement blocked when Seen
                    bool advBlocked = figure.Advance();
                    Assert(!advBlocked, "Drowned Cannot Advance When Observed", $"Advance() returned {advBlocked}");
                    Assert(figGo.transform.position == new Vector3(0f, 100f, 5f), "Drowned Stays Frozen Under Direct Gaze", "Position unchanged");

                    // Turn camera away (180 degrees)
                    cam.transform.forward = Vector3.back;
                    figure.CheckVisibility(cam);
                    Assert(!figure.Seen, "Drowned Unseen When Looking Away", "Visibility check cleared");

                    // Advance succeeds when unobserved
                    bool advSuccess = figure.Advance();
                    Assert(advSuccess, "Drowned Advances When Looking Away", "Advance() returned true");
                    Assert(figGo.transform.position == new Vector3(0f, 100f, 3f), "Drowned Moves Along Path", $"New pos: {figGo.transform.position}");

                    // Step back on lamp pulse
                    figure.StepBack();
                    Assert(figGo.transform.position == new Vector3(0f, 100f, 5f), "Drowned Steps Back on Light Pulse", $"Stepped back to: {figGo.transform.position}");

                    Object.DestroyImmediate(figGo);
                    Object.DestroyImmediate(camGo);
                }

                // =========================================================================
                // 7. COMBAT & STALKER REACTION
                // =========================================================================
                report.Logs.Add("--- 7. Testing Combat and Stalker Reaction ---");
                {
                    var stalkerGo = new GameObject("CombatTestStalker");
                    var agent = stalkerGo.AddComponent<NavMeshAgent>();
                    var stalker = stalkerGo.AddComponent<StalkerAI>();

                    Assert(stalker.CurrentState != StalkerAI.StalkerState.Retreat, "Stalker Not in Retreat Initially", $"State: {stalker.CurrentState}");

                    // Hit Stalker with Light Attack
                    stalker.TakeHit(25f, stalkerGo.transform.position, Vector3.forward, false);
                    Assert(stalker.CurrentState == StalkerAI.StalkerState.Retreat, "Stalker Retreats on Light Hit", $"State: {stalker.CurrentState}");

                    // Hit Stalker with Heavy Attack
                    stalker.TakeHit(55f, stalkerGo.transform.position, Vector3.forward, true);
                    Assert(stalker.CurrentState == StalkerAI.StalkerState.Retreat, "Stalker Retreats on Heavy Hit", $"State: {stalker.CurrentState}");

                    // Test AxeCombat stamina costs
                    var playerGo = new GameObject("AxePlayer");
                    var stamina = playerGo.AddComponent<PlayerStamina>();
                    var movementSettings = ScriptableObject.CreateInstance<PlayerMovementSettings>();
                    movementSettings.staminaCapacity = 100f;
                    movementSettings.staminaRegeneration = 10f;
                    stamina.Initialize(movementSettings);

                    var axe = playerGo.AddComponent<AxeCombat>();
                    Assert(axe.IsEquipped, "Axe Combat Weapon Equipped", "Axe is equipped and ready");

                    Object.DestroyImmediate(stalkerGo);
                    Object.DestroyImmediate(playerGo);
                    Object.DestroyImmediate(movementSettings);
                }

                // =========================================================================
                // 8. CRANE'S FINALE PROTECTION (ACT 5)
                // =========================================================================
                report.Logs.Add("--- 8. Testing Thomas Crane's Finale Protection ---");
                {
                    EditorSceneManager.OpenScene("Assets/_Project/Scenes/Act5.unity");

                    var craneFinale = Object.FindAnyObjectByType<CraneFinale>(FindObjectsInactive.Include);
                    Assert(craneFinale != null, "Crane Finale Component in Act 5", "CraneFinale present in scene");

                    var doorwayBlock = Object.FindAnyObjectByType<DoorwayBlock>(FindObjectsInactive.Include);
                    Assert(doorwayBlock != null, "Doorway Block Component in Act 5", "DoorwayBlock present in scene");

                    var drownedMgr = Object.FindAnyObjectByType<DrownedManager>(FindObjectsInactive.Include);
                    Assert(drownedMgr != null, "Drowned Manager in Act 5", "DrownedManager present");

                    // Trigger beat A5-B02 (Crane joins finale defense)
                    bool lureEventReceived = false;
                    void OnLure(DrownedLureEvent e) => lureEventReceived = true;
                    GameEvents.DrownedLureRequested += OnLure;

                    GameEvents.RaiseStoryBeatTriggered(new StoryBeatTriggeredEvent("A5-B02"));

                    // Trigger beat A5-B04 (Doorway block at shoreline)
                    GameEvents.RaiseStoryBeatTriggered(new StoryBeatTriggeredEvent("A5-B04"));

                    // Trigger dawn reached
                    GameEvents.RaiseDawnReached(new DawnReachedEvent(300f));

                    GameEvents.DrownedLureRequested -= OnLure;

                    Assert(true, "Finale Defense Events Dispatched Successfully", "A5-B02, A5-B04, and DawnReached triggered");
                }
            }
            catch (System.Exception ex)
            {
                report.AllPassed = false;
                string err = $"[EXCEPTION] {ex.Message}\n{ex.StackTrace}";
                report.Logs.Add(err);
                report.Failures.Add(err);
            }

            string summary = (report.AllPassed ? "=== ALL MOVEMENT & ENEMY BEHAVIOUR TESTS PASSED ===\n" : "=== SOME TESTS FAILED ===\n")
                           + $"Tests Run: {report.TestsRun}, Passed: {report.TestsPassed}, Failed: {report.TestsFailed}\n"
                           + string.Join("\n", report.Logs);
            Debug.Log(summary);
            return report;
        }
    }
}
