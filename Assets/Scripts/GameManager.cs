using GreenHour.Enviroment;
using System.Collections.Generic;
using UnityEngine;


namespace GreenHour.Gameplay
{
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }
        [Header("General")]
        [SerializeField] private List<Entity> entities = new List<Entity>();
        [SerializeField] private Entity currentEntity;
        private int day = 0;
        private void Awake()
        {
            if(Instance == null)
            {
                Instance = this;
            }
            else
            {
                Debug.LogWarning("Multiple GameManagers!");
                Destroy(gameObject);
            }
        }
        private void Start() 
        {
            if(entities.Count <= 0)
            {
                Debug.LogError("No entities!");
                return;
            }
            currentEntity = Instantiate(entities[Random.Range(0,entities.Count)]);
        }

        public void CalculateDayResult()
        {
            if (currentEntity == null)
            {
                Debug.LogError("There is no current entity!");
                return;
            }

            day++;
            int presenceAmount = 1; //Because everyday increasing
            int safetyAmount = 0;
            Action[] actions = FindObjectsByType<Action>(FindObjectsSortMode.None);
            foreach (Action action in actions)
            {
                if(!action.GetPenalty(currentEntity, out int presencePenalty, out int safetyPenalty)) continue;
                presenceAmount += presencePenalty;
                safetyAmount += safetyPenalty;
                action.LockAction();
            }

            currentEntity.IncreaseLevels(presenceAmount, safetyAmount);

            if (currentEntity.WantsToEscape())
            {
                Debug.LogWarning("Entity wants to escape");
            }
        }

        [ContextMenu("Debug - end day")]
        public void EndDay()
        {
            CalculateDayResult();
            DayCycle dayCycle = DayCycle.Instance;
            if (dayCycle == null) return;
            dayCycle.ResetDayCycle();
        }
    }
}

