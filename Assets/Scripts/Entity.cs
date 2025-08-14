using UnityEngine;

namespace GreenHour.Gameplay
{
    [CreateAssetMenu(fileName = "Entity", menuName = "Scriptable Objects/Entity")]
    public class Entity : ScriptableObject
    {
        [SerializeField] public string EntityName;

        int presenceLevel = 0;
        int safetyLevel = 0;

        public void IncreaseLevels(int presenceAmount, int safetyLevel)
        {
            presenceLevel += presenceAmount;
            safetyLevel += safetyLevel;
        }

        public bool WantsToEscape()
        {
            return presenceLevel>safetyLevel;
        }
    }
}