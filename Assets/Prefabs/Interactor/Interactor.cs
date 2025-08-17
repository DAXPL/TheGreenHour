using UnityEngine;
using UnityEngine.Events;
namespace GreenHour.Interactions
{
    public class Interactor : MonoBehaviour
    {
        [SerializeField] private float activationTime = 2.0f;
        private float activationTimer = 0.0f;
        private bool isInInteraction = false;
        [Header("Events")]
        [SerializeField] private UnityEvent OnStartInteraction;
        [SerializeField] private UnityEvent OnStopInteraction;
        [SerializeField] private UnityEvent OnInteractionSuccess;
        public void StartInteraction()
        {
            if(isInInteraction == true) return;
            isInInteraction = true;
            activationTimer = 0;
            OnStartInteraction.Invoke();
            Debug.Log($"Started interacton {this.name}");
        }

        public void StopInteraction()
        {
            if(isInInteraction == false) return;
            isInInteraction = false;
            activationTimer = 0;
            OnStopInteraction.Invoke();
            Debug.Log($"Ended interacton {this.name}");
        }

        private void InteractionSucceed()
        {
            StopInteraction();
            OnInteractionSuccess.Invoke();
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
