using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using static GreenHour.Gameplay.Entity;

namespace GreenHour.Gameplay
{
    public class Action : MonoBehaviour
    {
        [SerializeField] protected List<ActionResult> results = new List<ActionResult> ();
        [SerializeField] protected bool singleUse = true;
        [SerializeField] protected bool isPassive = false;
        [SerializeField] protected PresenceLevel minimalPresenceLevel = PresenceLevel.None;
        public UnityEvent SetupAction;
        public UnityEvent OnActionTaken;
        public UnityEvent OnActionResulted;
        protected bool actionAvailable = true;
        protected bool actionTaken = false;
        public void Awake()
        {
            actionAvailable = true;
            actionTaken = false;
        }
        public virtual void Start()
        {
            SetupAction.Invoke();
        }
        public void TakeAction()
        {
            if (actionAvailable == false) return;
            actionTaken = true;
            actionAvailable = false;
            OnActionTaken.Invoke();
        }

        public virtual bool GetPenalty(Entity entity, out int presencePenalty, out int safetyPenalty) 
        {
            presencePenalty = 0; 
            safetyPenalty=0;
            if (!isPassive)
            {
                if (actionTaken == false) return false; 
            }
            else
            {
                if (actionAvailable == false) return false; 
            }

            foreach (var result in results) 
            { 
                if(result == null) continue;
                if (result.entity.name == entity.name)
                {
                    presencePenalty = result.presencePenalty;
                    safetyPenalty = result.safetyPenalty;
                    result.action.Invoke();
                    return true;
                }
            }
            return isPassive;
        }
        public void LockAction()
        {
            if (singleUse == false)
            {
                actionAvailable = true;
            }
            actionTaken = false;
            OnActionResulted.Invoke();
        }

    }
    [System.Serializable]
    public class ActionResult
    {
        public Entity entity;
        public int presencePenalty;
        public int safetyPenalty;
        public UnityEvent action;
    }
}