using UnityEngine;
namespace TheLightHouse
{
    [RequireComponent(typeof(AudioSource),typeof(NoiseEmitter))]
    public sealed class SingingLure : MonoBehaviour
    {
        [SerializeField] Transform player;
        [SerializeField] Transform[] lureTargets;
        [SerializeField] DrownedManager figures;
        [SerializeField] AudioClip singing;
        [SerializeField] float interval=45;
        [SerializeField] float responseWindow=8;
        [SerializeField] float approachDistance=1.5f;
        [SerializeField] float alertLoudness=18;
        float nextSinging;float until;float initialDistance;bool active;
        void Awake()
        {
            var source = GetComponent<AudioSource>();
            if (source)
            {
                source.spatialBlend = 1f;
                source.minDistance = 3f;
                source.maxDistance = 35f;
                source.rolloffMode = AudioRolloffMode.Logarithmic;
                source.playOnAwake = false;
            }
            if (!singing)
            {
                singing = Resources.Load<AudioClip>("Audio/DROWNED_LURE_SUNG")
                       ?? Resources.Load<AudioClip>("DROWNED_LURE_SUNG");
#if UNITY_EDITOR
                if (!singing)
                    singing = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Audio/Voice/TheDrowned/DROWNED_LURE_SUNG.wav");
#endif
            }
        }
        void OnEnable()=>GameEvents.StoryBeatTriggered+=Beat;
        void OnDisable()=>GameEvents.StoryBeatTriggered-=Beat;
        void Beat(StoryBeatTriggeredEvent e){if(e.Id=="A2-B04"){active=true;nextSinging=Time.time+interval;}}
        float NearestDistance(){float best=float.MaxValue;foreach(var target in lureTargets)best=Mathf.Min(best,Vector3.Distance(player.position,target.position));return best;}
        void Update()
        {
            if(!active)return;
            if(Time.time>=nextSinging)
            {
                nextSinging=Time.time+interval;until=Time.time+responseWindow;initialDistance=NearestDistance();
                if(singing)GetComponent<AudioSource>().PlayOneShot(singing);GameEvents.RequestDialogue(new("","(singing from the shore)"));
            }
            if(Time.time<until&&initialDistance-NearestDistance()>=approachDistance)
            {until=0;figures.LureNearest(player.position);GetComponent<NoiseEmitter>().Emit(alertLoudness);}
        }
    }
}
