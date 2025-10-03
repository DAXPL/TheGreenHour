using GreenHour.Gameplay.Events;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Content.Interaction;

namespace GreenHour.Interactions
{
    [RequireComponent(typeof(Interactor))]
    public class Breakers : MonoBehaviour
    {
        public static bool breakersOn = true;

        [SerializeField] private LeverRule[] levers;
        [SerializeField] private UnityEvent onBroke;
        [SerializeField] private UnityEvent onFix;

        private void Start()
        {
            foreach (LeverRule rule in levers)
            {
                rule.lever.onLeverActivate.AddListener(() => TurnLever(rule.lever));
            }
            Break();
        }

        public void TrySwitchOn()
        {
            foreach (LeverRule rule in levers)
            {
                if (rule.lever.value == false) return;
            }
            Debug.Log("Breaker enabled!");
            breakersOn = true;
            onFix.Invoke();
        }

        public void TurnLever(XRLever lever)
        {
            for (int i = 0; i < levers.Length; i++) 
            {
                if(levers[i].lever == lever)
                {
                    levers[i].ExecuteRule();
                    break;
                }
            }
        }

        [ContextMenu("Break")]
        public void Break()
        {
            breakersOn = false;
            onBroke.Invoke();
            foreach (LeverRule rule in levers)
            {
                rule.lever.value = false;
            }
        }

        [System.Serializable]
        private class LeverRule
        {
            public XRLever lever;
            public XRLever[] leversToToggle;

            public void ExecuteRule()
            {
                foreach(XRLever l in leversToToggle)
                {
                    l.value = false;
                }
            }
        }
    }
}