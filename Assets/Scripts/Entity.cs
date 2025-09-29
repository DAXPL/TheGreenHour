using System.Collections.Generic;
using UnityEngine;
using GreenHour.Gameplay.Events;
namespace GreenHour.Gameplay
{
    [CreateAssetMenu(fileName = "Entity", menuName = "Scriptable Objects/Entity")]
    public class Entity : ScriptableObject
    {
        [SerializeField] public string EntityName;
        [SerializeField] public AudioClip[] recordedSounds;
        [SerializeField] public Sprite[] recordedImages;

        [Header("Events")]
        [SerializeField] private List<GameEvent> lowPresenceEvents;
        [SerializeField] private List<GameEvent> mediumPresenceEvents;
        [SerializeField] private List<GameEvent> highPresenceEvents;

        private int presenceLevel = 0;
        private int safetyLevel = 0;
        private const int maxPresence = 10;

        public enum PresenceLevel { None, Low, Medium, High }
        public enum SafetyLevel { Low, Medium, High }

        public void IncreaseLevels(int presenceAmount, int safetyAmount)
        {
            presenceLevel += presenceAmount;
            safetyLevel += safetyAmount;
        }

        public bool WantsToEscape()
        {
            return (presenceLevel>safetyLevel) && (GetPresenceLevel()>PresenceLevel.Medium);
        }

        public PresenceLevel GetPresenceLevel()
        {
            if (presenceLevel == 0)
            {
                return PresenceLevel.None;
            }
            else if (presenceLevel <= (int)(maxPresence * (1f / 3f)))
            {
                return PresenceLevel.Low;
            }
            else if (presenceLevel <= (int)(maxPresence * (2f / 3f)))
            {
                return PresenceLevel.Medium;
            }
            else
            {
                return PresenceLevel.High;
            }
        }

        public SafetyLevel GetSafetyLevel()
        {
            int diff = safetyLevel - presenceLevel;

            if (diff >= 5) // sporo bezpieczniej ni¿ obecnoœæ
            {
                return SafetyLevel.High;
            }
            else if (diff >= 0) // w miarê wyrównane
            {
                return SafetyLevel.Medium;
            }
            else // presence wiêksze ni¿ safety
            {
                return SafetyLevel.Low;
            }
        }

        public void TakeActions()
        {
            PresenceLevel presenceLevel = GetPresenceLevel();

            switch (presenceLevel) 
            {
                case PresenceLevel.Low:
                    for(int i = 0;i< lowPresenceEvents.Count; i++)
                        lowPresenceEvents[i].Raise();
                    break;
                case PresenceLevel.Medium:
                    for (int i = 0; i < mediumPresenceEvents.Count; i++)
                        mediumPresenceEvents[i].Raise();
                    break;
                case PresenceLevel.High:
                    for (int i = 0; i < highPresenceEvents.Count; i++)
                        highPresenceEvents[i].Raise();
                    break;
                default:
                    Debug.Log("Entity did nothing!");
                    break;
            }
        }
        
        public Sprite GetRecordedImage(int id)
        {
            if(recordedImages.Length <= 0) return null;
            if (id >= recordedImages.Length) id = recordedImages.Length;
            if (id < 0) id = 0;
            return recordedImages[id];
        }
    
    }
}