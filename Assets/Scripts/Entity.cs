using UnityEngine;

namespace GreenHour.Gameplay
{
    [CreateAssetMenu(fileName = "Entity", menuName = "Scriptable Objects/Entity")]
    public class Entity : ScriptableObject
    {
        [SerializeField] public string EntityName;
        [SerializeField] public AudioClip[] recordedSounds;
        [SerializeField] public Sprite[] recordedImages;
        private int presenceLevel = 0;
        private int safetyLevel = 0;
        private const int maxPresence = 10;

        public enum PresenceLevel { None, Low, Medium, High }

        public void IncreaseLevels(int presenceAmount, int safetyLevel)
        {
            presenceLevel += presenceAmount;
            safetyLevel += safetyLevel;
        }

        public bool WantsToEscape()
        {
            return presenceLevel>safetyLevel;
        }

        public PresenceLevel GetPresenceLevel()
        {
            if(presenceLevel == 0)
            {
                return PresenceLevel.None;
            }
            else if (presenceLevel <= (maxPresence*(1/3)))
            {
                return PresenceLevel.Low;
            }
            else if (presenceLevel <= (maxPresence * (2 / 3)))
            {
                return PresenceLevel.Medium;
            }
            else
            {
                return PresenceLevel.High;
            }
        }
    }
}