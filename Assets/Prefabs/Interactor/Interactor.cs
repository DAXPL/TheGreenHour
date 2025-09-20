using GreenHour.Enviroment;
using GreenHour.Interactions.Items;
using UnityEngine;
using UnityEngine.Events;
namespace GreenHour.Interactions
{
    public class Interactor : MonoBehaviour
    {
        [SerializeField] private ItemData activationItem;
        [SerializeField] private string description;
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
        private ItemData itemInUse;
        public void StartInteraction(Item holdedItem = null)
        {
            if(isInInteraction == true) return;
            if (holdedItem != null) itemInUse = holdedItem.GetData(); 
            isInInteraction = true;
            activationTimer = 0;
            OnStartInteraction.Invoke();
        }

        public void StopInteraction()
        {
            if(isInInteraction == false) return;
            itemInUse = null;
            isInInteraction = false;
            activationTimer = 0;
            OnStopInteraction.Invoke();
        }

        private void InteractionSucceed()
        {
            ItemData usedItem = itemInUse;
            StopInteraction();
            if (activationItem != null && activationItem != usedItem)
                return;

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

        public float GetPenalty()
        {
            return activationPenalty;
        }

        public string GetDescription()
        {
            return description;
        }

        public ItemData GetNeededItemData()
        {
            return activationItem;
        }
    }
}
