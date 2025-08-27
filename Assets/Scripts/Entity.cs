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
    }
}