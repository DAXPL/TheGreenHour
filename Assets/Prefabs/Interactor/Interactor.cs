using GreenHour.Enviroment;
using UnityEngine;
using UnityEngine.Events;
namespace GreenHour.Interactions
{
    public class Interactor : MonoBehaviour
    {
        [SerializeField] private float activationTime = 2.0f;
        [SerializeField] private int activationPenalty = 0;
        private float activationTimer = 0.0f;
        private bool isInInteraction = false;
        private bool toggled;
        [Header("Events")]
        [SerializeField] private UnityEvent OnStartInteraction;
        [SerializeField] private UnityEvent OnStopInteraction;
        [SerializeField] private UnityEvent OnInteractionSuccess;
        [SerializeField] private UnityEvent<bool> OnToggle;
        public void StartInteraction()
        {
            if(isInInteraction == true) return;
            isInInteraction = true;
            activationTimer = 0;
            OnStartInteraction.Invoke();
        }

        public void StopInteraction()
        {
            if(isInInteraction == false) return;
            isInInteraction = false;
            activationTimer = 0;
            OnStopInteraction.Invoke();
        }

        private void InteractionSucceed()
        {
            StopInteraction();
            OnInteractionSuccess.Invoke();
            toggled = !toggled;
            OnToggle.Invoke(toggled);
            if(activationPenalty>0 && DayCycle.Instance != null) DayCycle.Instance.SetTimePenalty(activationPenalty);
        }
        public void ActivateAction()
        {
            OnInteractionSuccess.Invoke();
            toggled = !toggled;
            OnToggle.Invoke(toggled);
        }

        private void Update()
        {
            if (!isInInteraction) return;
            activationTimer += Time.deltaTime;
            if (activationTimer >= activationTime) InteractionSucceed();
        }
        public float InteractionProgress()
        {
            if(!isInInteraction) return 0.0f;
            return activationTimer / activationTime;
        }
    }
}
