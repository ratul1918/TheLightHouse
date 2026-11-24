using UnityEngine;

namespace TheLightHouse
{
    [RequireComponent(typeof(NoiseEmitter))]
    public sealed class CrankStation : MonoBehaviour, IInteractable
    {
        [SerializeField] private string prompt = "Crank lamp (hold)";
        [SerializeField] private LampController lamp;
        [SerializeField] private NoiseEmitter noiseEmitter;
        [SerializeField, Min(0f)] private float fuelPerSecond = 8f;
        [SerializeField, Min(0f)] private float noiseLoudness = 20f;
        [SerializeField, Min(0.1f)] private float noiseInterval = 1f;

        private float nextNoiseTime;
        private float nextEffortAudioTime;
        private bool isCranking;
        [SerializeField] private StormSettings stormSettings;
        [SerializeField] private PlayerStamina stamina;
        [SerializeField] private Transform handle;
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip effortClip;
        private float speed;
        private float recoveryUntil;
        public float Speed=>speed;
        private void Update()
        {
            if(!lamp||!lamp.ChargeMode||!stormSettings)return;
            if(!isCranking)speed=Mathf.MoveTowards(speed,0,stormSettings.crankDeceleration*Time.deltaTime);
            if(handle)handle.Rotate(Vector3.forward,speed*stormSettings.handleDegreesPerSecond*Time.deltaTime);
            if(isCranking)lamp.AddCharge(speed*stormSettings.chargeGain*Time.deltaTime);
            if(speed>0&&Time.time>=nextNoiseTime)
            {
                noiseEmitter.Emit(stormSettings.crankMaxNoise*stormSettings.crankNoiseCurve.Evaluate(speed));
                nextNoiseTime=Time.time+stormSettings.noiseInterval;
                if (speed > 0.25f && Time.time >= nextEffortAudioTime && effortClip)
                {
                    nextEffortAudioTime = Time.time + effortClip.length + 0.6f;
                    if (audioSource)
                        audioSource.PlayOneShot(effortClip, 0.75f);
                    GameEvents.RequestDialogue(new DialogueLine("Daniel", "(strained physical exertion grunt)"));
                }
            }
        }

        public string Prompt => prompt;
        public bool RequiresHold => true;

        private void Awake()
        {
            if (!noiseEmitter)
                noiseEmitter = GetComponent<NoiseEmitter>();
            if (!audioSource && !TryGetComponent(out audioSource))
                audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.spatialBlend = 0.5f;
            audioSource.playOnAwake = false;
            if (!effortClip)
            {
                effortClip = Resources.Load<AudioClip>("Audio/REACT_DAN_CRANK_EFFORT")
                          ?? Resources.Load<AudioClip>("REACT_DAN_CRANK_EFFORT");
#if UNITY_EDITOR
                if (!effortClip)
                    effortClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Audio/Voice/DanielHale/REACT_DAN_CRANK_EFFORT.wav");
#endif
            }
        }

        private void OnDisable()
        {
            StopInteract();
        }

        public void Interact()
        {
            if (lamp == null)
            {
                return;
            }
            if(lamp.ChargeMode&&stormSettings)
            {
                if(!isCranking)GameEvents.RaiseCrankActiveChanged(true);
                isCranking=true;
                if(stamina&&stamina.Current<=0)recoveryUntil=Time.time+stormSettings.exhaustionRecovery;
                float cap=Time.time<recoveryUntil?stormSettings.exhaustedSpeedCap:1f;
                speed=Mathf.MoveTowards(speed,cap,stormSettings.crankAcceleration*Time.deltaTime);
                if(stamina&&Time.time>=recoveryUntil)stamina.Spend(stormSettings.staminaDrain*Time.deltaTime,stormSettings.exhaustionRecovery);
                if(speed>0f)GameEvents.RaiseStorySignal("first-crank");
                return;
            }

            if (!isCranking)
            {
                isCranking = true;
                nextNoiseTime = Time.time;
                lamp.SetCranking(true);
            }

            lamp.AddFuel(fuelPerSecond * Time.deltaTime);

            if (Time.time >= nextNoiseTime)
            {
                noiseEmitter.Emit(noiseLoudness);
                nextNoiseTime = Time.time + noiseInterval;
            }
        }

        public void StopInteract()
        {
            if (!isCranking)
            {
                return;
            }

            isCranking = false;
            GameEvents.RaiseCrankActiveChanged(false);
            if(lamp&&lamp.ChargeMode)return;
            lamp?.SetCranking(false);
        }
    }
}
