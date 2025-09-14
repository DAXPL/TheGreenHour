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
                    string[] descsL =
                    {
                        "The entity hasn’t revealed itself yet. Maybe it’s still hiding from me.",
                        "Nothing unusual happened… but I can’t shake the feeling I’m being observed.",
                        "The forest stays quiet. If it’s here, it’s keeping its distance.",
                        "Still hiding, huh? Classic cryptid move. I’ll smoke you out tomorrow."
                    };
                    return descsL[Random.Range(0,descsL.Length)];
                case PresenceLevel.Medium:
                    string[] descsM =
                    {
                        "Something is near. I’m sure of it.",
                        "I’m not crazy. The signs are here. Just wait till I post this!",
                        "No reveal yet… but tomorrow’s the day, I can feel it!",
                        "Not a coincidence anymore. Every rustle screams proof!"
                    };
                    return descsM[Random.Range(0, descsM.Length)];
                case PresenceLevel.High:
                    string[] descsH =
                    {
                        "No doubt now — the entity is here, moving in the dark.",
                        "Every shadow, every sound screams its presence.",
                        "If I make it out, this will be the proof of a lifetime!"
                    };
                    return descsH[Random.Range(0, descsH.Length)];
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
                    string[] descsL =
                    {
                        "Damn, I pushed too hard… it almost bolted! Need to chill, or I’ll lose it all.",
                        "I’m scaring it off… no, no, no! If it runs, I lose everything!",
                        "That was too much. One more slip and it’s gone for good.",
                        "Ugh, I acting like a tourist, not a researcher. Rookie mistake."
                    };
                    return descsL[Random.Range(0, descsL.Length)];
                case SafetyLevel.Medium:
                    string[] descsM =
                    {
                        "It knows I’m here, but it hasn’t left. This is the sweet spot.",
                        "The creature is wary… but curious. We’re dancing on a knife’s edge.",
                        "Every move matters now. One wrong sound and it’s gone, one right gesture and it stays.",
                        "It’s keeping its distance, but it hasn’t vanished. That’s hope."
                    };
                    return descsM[Random.Range(0, descsM.Length)];
                case SafetyLevel.High:
                    string[] descsH =
                    {
                        "I’ve earned its trust. For once, the legend doesn’t run — it stays.",
                        "The creature feels safe with me. This is history in the making!",
                        "Incredible. It stayed — I think it actually trusts me!",
                        "For a second, it felt like we shared the same silence. Like it wanted me here"
                    };
                    return descsH[Random.Range(0, descsH.Length)];
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
