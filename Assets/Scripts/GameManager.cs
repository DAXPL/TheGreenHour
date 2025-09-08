using GreenHour.Enviroment;
using GreenHour.Gameplay.Events;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem.LowLevel;
using static GreenHour.Gameplay.Entity;


namespace GreenHour.Gameplay
{
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }
        [Header("General")]
        [SerializeField] private List<Entity> entities = new List<Entity>();
        [SerializeField] private Entity currentEntity;
        [SerializeField] private List<GameEvent> randomEvents;
        [SerializeField] private GameEvent showResults;
        [SerializeField] private Transform bedPosition;
        private int day = 0;
        private bool usedBedToSleep = true;
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
            int actionsTaken = 0;
            foreach (Action action in actions)
            {
                if(action.enabled == false) continue;
                actionsTaken++;
                if (!action.GetPenalty(currentEntity, out int presencePenalty, out int safetyPenalty)) continue;
                presenceAmount += presencePenalty;
                safetyAmount += safetyPenalty;
                action.LockAction();
            }
            Debug.Log($"Taken actions: {actionsTaken}, presence += {presenceAmount}, safety += {safetyAmount}");
            currentEntity.IncreaseLevels(presenceAmount, safetyAmount);
            currentEntity.TakeActions();
            foreach(GameEvent ev in randomEvents)
            {
                if(Random.Range(0,10)>6.0f) ev.Raise();
            }
            if (currentEntity.WantsToEscape())
            {
                Debug.LogWarning("Entity wants to escape");
            }
            if(showResults) showResults.Raise();
        }

        [ContextMenu("Debug - end day")]
        public void EndDay(bool usedBed)
        {
            usedBedToSleep = usedBed;
            CalculateDayResult();
        }
        public void LoadNextDay()
        {
            Debug.LogWarningFormat($"{usedBedToSleep}{bedPosition != null}");
            if (usedBedToSleep==false && bedPosition!=null)
            {
                CharacterController player = FindAnyObjectByType<CharacterController>();
                Debug.LogWarningFormat($"{player != null}");
                if (player != null) 
                { 
                    player.enabled = false;
                    player.transform.position = bedPosition.position;
                    player.enabled = true;
                }
            }

            DayCycle dayCycle = DayCycle.Instance;
            if (dayCycle == null) return;
            dayCycle.ResetDayCycle(usedBedToSleep ? 0 : 120);
        }

        public string GetSafetyDesc()
        {
            PresenceLevel presenceLevel = currentEntity.GetPresenceLevel();

            switch (presenceLevel)
            {
                case PresenceLevel.Low:
                    return "Cisza staje siê napiêta. Ka¿dy szelest zdaje siê mieæ znaczenie – las nie jest zadowolony z mojej obecnoœci. Jestem za g³oœno, p³oszê byt.";
                case PresenceLevel.Medium:
                    return "Niepokój bytu roœnie. Wydaje siê, ¿e ktoœ lub coœ bacznie przygl¹da siê ka¿demu mojemu ruchowi. Muszê byæ ostro¿ny i nie mogê go sp³oszyæ";
                case PresenceLevel.High:
                    return "Powietrze wci¹¿ spokojne. Coœ nieœmia³o majaczy w oddali, ale wci¹¿ siê mnie obawia";
                default:
                    return "Missing no.";
            }
        }

        public string GetPresenceDesc()
        {
            SafetyLevel safeLevel = currentEntity.GetSafetyLevel();

            switch (safeLevel)
            {
                case SafetyLevel.Low:
                    return "Czujê jej cieñ w powietrzu, daleko, ledwie wyczuwalnie. Jakby œledzi³a ka¿dy mój krok zza mg³y.";
                case SafetyLevel.Medium:
                    return "Powoli zbli¿a siê do mnie. Ka¿dy dŸwiêk w lesie mo¿e byæ wskazówk¹ – chyba idzie mi dobrze.";
                case SafetyLevel.High:
                    return "Ju¿ prawie zdoby³em zaufanie lasu. Ka¿dy oddech to sygna³, ¿e nie jestem sam. Jestem blisko celu.";
                default:
                    return "Missing no.";
            }
        }
    
        public void SetCursor(bool visible)
        {
            Cursor.lockState = visible ? CursorLockMode.Confined : CursorLockMode.Locked;
            Cursor.visible = visible;
        }
    }
}
