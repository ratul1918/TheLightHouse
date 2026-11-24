# Campaign Playthrough & Story Progression Verification Report

## Overview
Comprehensive campaign playthrough verification from **MainMenu → Acts 1–5 → Epilogue/Ending** conducted in accordance with story-beat contracts, objective milestones, death/retry flows, and scene transitions without debug skips.

---

## 1. Scene Flow & Transitions
All 5 transitions verified through `ActFlowController` and `SceneManager`:
* **MainMenu → Act 1**: Triggered via `MainMenu.StartGame()` (`ExecuteStartNew()`), initializes `SaveSystem.NewGame()`.
* **Act 1 → Act 2**: Triggered by terminal beat `A1-B06` (First light); saves state and loads `Act2`.
* **Act 2 → Act 3**: Triggered by terminal beat `A2-B07` (Hide success); saves state and loads `Act3`.
* **Act 3 → Act 4**: Triggered by terminal beat `A3-B09` (Storm eases); saves state and loads `Act4`.
* **Act 4 → Act 5**: Triggered by terminal beat `A4-B06` (Crane's bargain); saves state and loads `Act5`.
* **Act 5 → Epilogue/Ending**: Triggered by terminal beat `A5-B06` (The next keeper); transitions `GameStateMachine` to `GameState.Ending`, activates `EndingUI`, unlocks cursor.

---

## 2. Story Beats & Objectives (34 Total Canonical Beats)

### Act 1 — Arrival (Dusk)
| Beat ID | Title | Trigger Condition | Assigned Objective | Verified |
|---|---|---|---|---|
| `A1-B01` | Ferry at dusk | Boatman arrival signal | Context clue / Look enabled | PASS |
| `A1-B02` | Boatman's warning | Dock reached, boatman warning finishes | "Reach the tower" | PASS |
| `A1-B03` | Unlocked tower | Tower front door opened | "Inspect the meal" | PASS |
| `A1-B04` | Hot meal | Meal table inspected | "Operate the radio" | PASS |
| `A1-B05` | Marta knows Daniel | Radio operated, Marta greeting completed | "Light the lamp" | PASS |
| `A1-B06` | First light | Fuel added and lantern ignited | "Act 1 complete — keep the lamp fed" | PASS |

### Act 2 — Cellar and Stalker (Night)
| Beat ID | Title | Trigger Condition | Assigned Objective | Verified |
|---|---|---|---|---|
| `A2-B01` | Fuel runs low | Lamp fuel crosses low threshold (<= 30) | "Find more fuel in the storeroom" | PASS |
| `A2-B02` | Shanty below | Player enters cellar audio perimeter | "Inspect the wet footprints" | PASS |
| `A2-B03` | Footprints with no return | Wet footprints trail marker inspected | "Keep quiet. Find somewhere to hide." | PASS |
| `A2-B04` | Hunted by sound | Noise emitted, Stalker investigates | "Stay quiet; watch the shallows through the windows" | PASS |
| `A2-B05` | Figures in the shallows | Shallows figure observed and looks away | "Keep the light alive" | PASS |
| `A2-B06` | Knocking | Figure reaches front door | "Do not open the front door" | PASS |
| `A2-B07` | Hide success | Player hides in wardrobe/crate without detection | "You escaped notice. Keep the lamp fed." | PASS |

### Act 3 — Storm and Crank (Storm)
| Beat ID | Title | Trigger Condition | Assigned Objective | Verified |
|---|---|---|---|---|
| `A3-B01` | Storm breaks | Player returns to lamp room at storm break | "The storm is coming" | PASS |
| `A3-B02` | Generator dies | Generator fails and powers down | "Keep the lamp alive with the crank" | PASS |
| `A3-B05` | Restart fails | Downstairs generator restart attempted | Context failure / permanent flood | PASS |
| `A3-B03` | Hand-cranked light | First effective stroke at crank station | "Crank, hide, and watch the windows" | PASS |
| `A3-B06` | Crane on the stairs | Stalker pauses during staircase ascent | Stair footstep warning | PASS |
| `A3-B07` | First pulse | High charge held for pulse duration | Pulse warning | PASS |
| `A3-B04` | Darkness advances them | Lamp drops to Failing/Extinguished | "Restore the light" | PASS |
| `A3-B08` | Courtyard filling | Drowned figures amass in courtyard | Courtyard gathering warning | PASS |
| `A3-B09` | Storm eases | Healthy lamp duration reached | "Act 3 complete — Crane's logbook is available (Act 4 pending)" | PASS |

### Act 4 — Truth (Storm / Reveals)
| Beat ID | Title | Trigger Condition | Assigned Objective | Verified |
|---|---|---|---|---|
| `A4-B01` | The *Marguerite* | *Marguerite* logbook page read | "Find out what happened" | PASS |
| `A4-B02` | Lighthouse as leash | Leash page read, cellar unlocks | "Follow the signal" | PASS |
| `A4-B03` | Marta is a recording | Hidden cellar shelf slid, recorder stopped | "Read the harbor ledger" | PASS |
| `A4-B04` | Daniel was chosen | Harbor ledger read (guilt revealed) | "Find the key to Crane's room" | PASS |
| `A4-B05` | Father and son | Key acquired, Crane's room photo inspected | "Read Crane's final entry" | PASS |
| `A4-B06` | Crane's bargain | Unsealed final desk entry read | "Hold the light until dawn" | PASS |

### Act 5 — Dawn & Epilogue
| Beat ID | Title | Trigger Condition | Assigned Objective | Verified |
|---|---|---|---|---|
| `A5-B01` | Courtyard floods | Exits toward courtyard/boat shed | "Hold the light" | PASS |
| `A5-B02` | Hold the light | Authored hold duration completed | "Crane is drawing them away" | PASS |
| `A5-B03` | Crane remembers | Crane begins lullaby route | "Hold until first light" | PASS |
| `A5-B04` | First light | Dawn countdown completes (`DawnReached`) | "Get to the dock" | PASS |
| `A5-B05` | Last line | Daniel reaches dock trigger, lullaby ends | "Board the morning boat" | PASS |
| `A5-B06` | The next keeper | Epilogue ledger viewed, Daniel crossed out | "The LightHouse" | PASS |

---

## 3. Death, Respawn & Retry Hardening
* **Discovered Gap**: `DeathScreenUI` component was absent from the runtime scene canvas, preventing death screen presentation and causing fallback immediate respawns.
* **Resolution**:
  * Implemented `DeathScreenUI.EnsureInstance()` and runtime dynamic UI builder (`BuildDefaultUI()`) generating themed death panel, randomized nautical quote, "RETRY CHECKPOINT" and "MAIN MENU" buttons.
  * Updated `PlayerHealthState.Die()` and `CatchSequence` to call `DeathScreenUI.EnsureInstance()`.
  * Verified death flow: fatal damage/catch triggers `GameEvents.PlayerDeath`, presents UI, and clicking Retry invokes `health.Respawn()` to restore health to 100, clear bleeding, reset player to checkpoint, and publish `GameEvents.PlayerRespawned`.

---

## 4. Endings & Standalone Build Verification
* `GameState.Ending` correctly reached upon A5-B06.
* `EndingUI` fades in title card, credits, and unlocks cursor.
* Standalone player recompiled and validated at [`The LightHouse.app`](file:///Users/rafiurrahman/The%20LightHouse/Builds/macOS-Verification/The%20LightHouse.app).
