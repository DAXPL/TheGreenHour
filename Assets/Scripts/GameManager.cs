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
                    return "Cisza staje si� napi�ta. Ka�dy szelest zdaje si� mie� znaczenie � las nie jest zadowolony z mojej obecno�ci. Jestem za g�o�no, p�osz� byt.";
                case PresenceLevel.Medium:
                    return "Niepok�j bytu ro�nie. Wydaje si�, �e kto� lub co� bacznie przygl�da si� ka�demu mojemu ruchowi. Musz� by� ostro�ny i nie mog� go sp�oszy�";
                case PresenceLevel.High:
                    return "Powietrze wci�� spokojne. Co� nie�mia�o majaczy w oddali, ale wci�� si� mnie obawia";
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
                    return "Czuj� jej cie� w powietrzu, daleko, ledwie wyczuwalnie. Jakby �ledzi�a ka�dy m�j krok zza mg�y.";
                case SafetyLevel.Medium:
                    return "Powoli zbli�a si� do mnie. Ka�dy d�wi�k w lesie mo�e by� wskaz�wk� � chyba idzie mi dobrze.";
                case SafetyLevel.High:
                    return "Ju� prawie zdoby�em zaufanie lasu. Ka�dy oddech to sygna�, �e nie jestem sam. Jestem blisko celu.";
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
