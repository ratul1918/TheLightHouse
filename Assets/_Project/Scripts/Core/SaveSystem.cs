using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace TheLightHouse
{
    [Serializable] public sealed class GameOptionsData
    {
        // General / Display
        public bool reducedStorm = true;
        public bool disableBobAndFov;
        public float sensitivity = 1f;
        public float volume = 1f;
        [Range(.7f, 1f)] public float renderScale = 1f;
        public bool telemetryEnabled = true;
        public bool vSync = true;
        public int frameRateLimit = 60;
        public int qualityPreset = 0; // 0: Low/Mac, 1: Medium, 2: High
        public int shadowQuality = 1;
        public int textureQuality = 0;
        public float fov = 65f;
        public float brightness = 1f;

        // Audio Channels
        public float masterVolume = 1f;
        public float musicVolume = 0.8f;
        public float ambienceVolume = 0.9f;
        public float sfxVolume = 1f;
        public float dialogueVolume = 1f;
        public float voiceVolume = 1f;
        public float footstepsVolume = 0.85f;
        public float horrorVolume = 1f;

        // Gameplay
        public bool invertY;
        public float headBobIntensity = 1f;
        public bool cameraBob = true;
        public bool motionSicknessReduction;
        public bool objectiveAssistance = true;
        public int difficulty = 1; // 0: Story, 1: Survival, 2: Nightmare

        // Accessibility
        public bool subtitlesEnabled = true;
        public float subtitleSize = 1f;
        public float subtitleBackgroundOpacity = 0.75f;
        public bool reducedFlashing;
        public bool reducedCameraShake;
        public bool toggleInteraction;

        // Controls
        public string inputBindingOverrides = "";
    }
    [Serializable] public sealed class GameSaveData
    {
        public string act = "Act1";
        public string checkpointId = "start";
        public Vector3 checkpoint;
        public List<string> items = new();
        public List<string> pages = new();
        public List<string> flags = new();
        public GameOptionsData options = new();
        public int version = -1;
        public string updatedAt;
        public int slotIndex = 1;
        public int highestUnlockedAct = 1;
    }

    public sealed class SaveSystem : MonoBehaviour
    {
        [SerializeField] string fileName = "thelighthouse-save.json";
        [SerializeField] int currentSlot = 1;
        GameSaveData data = new();
        HashSet<string> items = new(), pages = new(), flags = new();
        CloudSaveClient cloud;
        CloudSavePayload pendingCloud;
        public GameSaveData Data => data;
        public int CurrentSlot => currentSlot;
        public int HighestUnlockedAct => Mathf.Max(1, data.highestUnlockedAct);

        string GetSlotFileName(int slot) => slot == 1 ? fileName : $"thelighthouse-save-slot{slot}.json";
        public bool HasSave => HasSlotSave(currentSlot) || HasSlotSave(1) || File.Exists(Path.Combine(Application.persistentDataPath, fileName));
        public bool HasSlotSave(int slot)
        {
            string baseFile = GetSlotFileName(slot);
            string primary = Path.Combine(Application.persistentDataPath, baseFile);
            string backup = primary + ".bak";
            string fallback = Path.Combine(Application.persistentDataPath, $"fallback-save-slot{slot}.json");
            return File.Exists(primary) || File.Exists(backup) || File.Exists(fallback);
        }

        void Awake()
        {
            var existing = FindObjectsByType<SaveSystem>();
            if (existing.Length > 1) { Destroy(gameObject); return; }
            DontDestroyOnLoad(gameObject);
            cloud = FindAnyObjectByType<CloudSaveClient>();
        }

        void OnEnable()
        {
            GameEvents.ItemCollected += Item;
            GameEvents.PageRead += Page;
            GameEvents.StoryBeatTriggered += Beat;
            GameEvents.CheckpointReached += Checkpoint;
        }

        void OnDisable()
        {
            GameEvents.ItemCollected -= Item;
            GameEvents.PageRead -= Page;
            GameEvents.StoryBeatTriggered -= Beat;
            GameEvents.CheckpointReached -= Checkpoint;
        }

        void Item(ItemCollectedEvent e) => items.Add(e.Id);
        void Page(PageReadEvent e) => pages.Add(e.Id);
        void Beat(StoryBeatTriggeredEvent e)
        {
            flags.Add(e.Id);
            int unlocked = e.Id switch
            {
                "A1-B06" => 2,
                "A2-B07" => 3,
                "A3-B09" => 4,
                "A4-B06" => 5,
                _ => 1
            };
            if (unlocked > data.highestUnlockedAct)
            {
                data.highestUnlockedAct = unlocked;
                Save();
            }
        }

        void Checkpoint(CheckpointReachedEvent e)
        {
            data.checkpointId = e.Id;
            data.checkpoint = e.Position;
            data.act = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            Save();
        }

        public void SetCurrentSlot(int slot)
        {
            currentSlot = Mathf.Clamp(slot, 1, 3);
            Load();
        }

        public bool Save()
        {
            data.items = new(items);
            data.pages = new(pages);
            data.flags = new(flags);
            data.slotIndex = currentSlot;
            data.version++;
            data.updatedAt = DateTime.UtcNow.ToString("O");

            string json = JsonUtility.ToJson(data, true);
            string primaryPath = Path.Combine(Application.persistentDataPath, GetSlotFileName(currentSlot));
            string tmpPath = primaryPath + ".tmp";
            string bakPath = primaryPath + ".bak";
            string fallbackPath = Path.Combine(Application.persistentDataPath, $"fallback-save-slot{currentSlot}.json");

            bool writeSuccess = false;
            try
            {
                // 1. Write to temporary file first
                File.WriteAllText(tmpPath, json);

                // 2. Backup existing valid primary save file
                if (File.Exists(primaryPath))
                {
                    try
                    {
                        File.Copy(primaryPath, bakPath, true);
                    }
                    catch (Exception bakEx)
                    {
                        Debug.LogWarning($"[SaveSystem] Backup copy failed: {bakEx.Message}");
                    }
                }

                // 3. Atomically overwrite primary save
                if (File.Exists(primaryPath))
                {
                    File.Delete(primaryPath);
                }
                File.Move(tmpPath, primaryPath);
                writeSuccess = true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SaveSystem] Failed writing primary save file: {ex.Message}. Attempting emergency fallback write.");
                try
                {
                    File.WriteAllText(fallbackPath, json);
                    writeSuccess = true;
                    Debug.LogWarning($"[SaveSystem] Emergency fallback save succeeded at: {fallbackPath}");
                }
                catch (Exception fallbackEx)
                {
                    Debug.LogError($"[SaveSystem] Emergency fallback save failed: {fallbackEx.Message}");
                }
            }
            finally
            {
                if (File.Exists(tmpPath))
                {
                    try { File.Delete(tmpPath); } catch {}
                }
            }

            PushCloud();
            return writeSuccess;
        }

        void PushCloud()
        {
            cloud ??= FindAnyObjectByType<CloudSaveClient>();
            if (!cloud) return;
            pendingCloud = new CloudSavePayload
            {
                act = data.act,
                checkpoint = data.checkpointId,
                itemsJson = JsonUtility.ToJson(new StringList { values = data.items }),
                pagesJson = JsonUtility.ToJson(new StringList { values = data.pages }),
                flagsJson = JsonUtility.ToJson(new StringList { values = data.flags }),
                optionsJson = JsonUtility.ToJson(data.options),
                version = data.version,
                updatedAt = data.updatedAt
            };
            cloud.PutSave(pendingCloud, (remote, error) =>
            {
                if (error != null) StartCoroutine(RetryCloud());
                else pendingCloud = null;
            });
        }

        System.Collections.IEnumerator RetryCloud()
        {
            yield return new WaitForSecondsRealtime(5f);
            if (pendingCloud != null)
            {
                cloud?.PutSave(pendingCloud, (remote, error) =>
                {
                    if (error != null) StartCoroutine(RetryCloud());
                    else pendingCloud = null;
                });
            }
        }

        public bool Load()
        {
            string primaryPath = Path.Combine(Application.persistentDataPath, GetSlotFileName(currentSlot));
            string bakPath = primaryPath + ".bak";
            string fallbackPath = Path.Combine(Application.persistentDataPath, $"fallback-save-slot{currentSlot}.json");
            string legacyPath = currentSlot == 1 ? Path.Combine(Application.persistentDataPath, fileName) : null;

            bool loaded = TryLoadFile(primaryPath, out var parsed);

            // Corrupted save recovery from backup
            if (!loaded)
            {
                Debug.LogWarning($"[SaveSystem] Primary save unavailable or corrupted at {primaryPath}. Attempting backup recovery.");
                loaded = TryLoadFile(bakPath, out parsed);
                if (loaded)
                {
                    Debug.LogWarning($"[SaveSystem] Successfully recovered corrupted save from backup: {bakPath}");
                    try { File.Copy(bakPath, primaryPath, true); } catch {}
                }
            }

            // Fallback save recovery
            if (!loaded)
            {
                loaded = TryLoadFile(fallbackPath, out parsed);
                if (loaded)
                {
                    Debug.LogWarning($"[SaveSystem] Successfully recovered save from emergency fallback: {fallbackPath}");
                }
            }

            // Legacy save recovery (Slot 1 only)
            if (!loaded && !string.IsNullOrEmpty(legacyPath) && legacyPath != primaryPath)
            {
                loaded = TryLoadFile(legacyPath, out parsed);
            }

            if (!loaded || parsed == null)
            {
                return false;
            }

            ApplyParsedSave(parsed);
            return true;
        }

        private bool TryLoadFile(string path, out GameSaveData result)
        {
            result = null;
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return false;

            try
            {
                string text = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(text)) return false;
                result = JsonUtility.FromJson<GameSaveData>(text);
                return result != null && !string.IsNullOrEmpty(result.act);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SaveSystem] Exception reading or deserializing {path}: {ex.Message}");
                return false;
            }
        }

        private void ApplyParsedSave(GameSaveData parsed)
        {
            data = parsed;
            items = new(data.items ?? new());
            pages = new(data.pages ?? new());
            flags = new(data.flags ?? new());
        }

        public bool LoadWithCloudSync(Action<bool> onComplete, Func<bool> isCancelled = null)
        {
            bool localOk = Load();
            if (!cloud)
                cloud = FindAnyObjectByType<CloudSaveClient>();

            if (!cloud)
            {
                onComplete?.Invoke(localOk);
                return localOk;
            }

            bool callbackInvoked = false;
            cloud.GetSave((remote, error) =>
            {
                if (callbackInvoked || isCancelled?.Invoke() == true) return;
                callbackInvoked = true;

                if (remote != null)
                {
                    MergeCloud(remote);
                    Save(); // Persist cloud update locally
                    onComplete?.Invoke(true);
                }
                else
                {
                    onComplete?.Invoke(localOk);
                }
            });
            return localOk;
        }

        void MergeCloud(CloudSavePayload remote)
        {
            bool useCloud = false;
            if (DateTime.TryParse(remote.updatedAt, out var cloudTime) && DateTime.TryParse(data.updatedAt, out var localTime))
                useCloud = cloudTime > localTime;
            else
                useCloud = remote.version > data.version;

            Debug.Log("Continue save choice: " + (useCloud ? "cloud" : "local"));
            if (!useCloud) return;

            data.act = remote.act;
            data.checkpointId = remote.checkpoint;
            data.version = remote.version;
            data.updatedAt = remote.updatedAt;
            data.items = JsonUtility.FromJson<StringList>(remote.itemsJson)?.values ?? new List<string>();
            data.pages = JsonUtility.FromJson<StringList>(remote.pagesJson)?.values ?? new List<string>();
            data.flags = JsonUtility.FromJson<StringList>(remote.flagsJson)?.values ?? new List<string>();
            data.options = JsonUtility.FromJson<GameOptionsData>(remote.optionsJson) ?? new GameOptionsData();
            items = new(data.items);
            pages = new(data.pages);
            flags = new(data.flags);
        }

        public void DeleteSlotFiles(int slot)
        {
            string primaryPath = Path.Combine(Application.persistentDataPath, GetSlotFileName(slot));
            string tmpPath = primaryPath + ".tmp";
            string bakPath = primaryPath + ".bak";
            string fallbackPath = Path.Combine(Application.persistentDataPath, $"fallback-save-slot{slot}.json");

            try { if (File.Exists(primaryPath)) File.Delete(primaryPath); } catch {}
            try { if (File.Exists(tmpPath)) File.Delete(tmpPath); } catch {}
            try { if (File.Exists(bakPath)) File.Delete(bakPath); } catch {}
            try { if (File.Exists(fallbackPath)) File.Delete(fallbackPath); } catch {}
        }
        [Serializable] sealed class StringList { public List<string> values; }
        public void NewGame(){var currentOptions=data.options;data=new GameSaveData{options=currentOptions??new GameOptionsData()};items.Clear();pages.Clear();flags.Clear();Save();}
        public void SetOptions(GameOptionsData value){data.options=value??new GameOptionsData();Save();}
        public void RestoreSceneState()
        {
            var inventory=FindAnyObjectByType<Inventory>();inventory?.Restore(items);
            var journal=FindAnyObjectByType<LogbookJournal>();journal?.Restore(pages);
            var health=FindAnyObjectByType<PlayerHealthState>();if(health&&data.checkpoint!=Vector3.zero)health.SetCheckpoint(data.checkpoint);
            var controller=FindAnyObjectByType<FirstPersonController>();controller?.ApplyOptions(data.options.sensitivity,data.options.disableBobAndFov);
            var accessibility=FindAnyObjectByType<AccessibilityOptions>();accessibility?.SetReduced(data.options.reducedStorm);
            AudioListener.volume=data.options.volume;
            if(UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline is UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset pipeline)pipeline.renderScale=Mathf.Clamp(data.options.renderScale,.7f,1f);
        }
    }
}
