using UnityEngine;
namespace GreenHour.Gameplay
{
    public class DirectionalMicrophone : Action
    {
        private AudioSource audioSource;
        [SerializeField] private AudioClip norecording;
        private AudioClip recording;
        public override void Start()
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource) audioSource.clip = norecording;
            this.enabled = false;
        }
        public override bool GetPenalty(Entity entity, out int presencePenalty, out int safetyPenalty)
        {
            presencePenalty = 0;
            safetyPenalty = 0;
            if (actionAvailable == false) return false;
            if (actionTaken == false) return false;
            if (this.enabled == false) return false;
            if (entity == null) return false;
            if (entity.GetPresenceLevel() < minimalPresenceLevel) return false;

            if (entity.recordedSounds.Length>0) recording = entity.recordedSounds[Random.Range(0, entity.recordedSounds.Length)];
            if(audioSource) audioSource.clip = recording;
            return true;
        }
    }
}