using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
namespace GreenHour.Interactions.Items
{
    [RequireComponent(typeof(XRGrabInteractable))]
    public class Item : MonoBehaviour
    {
        [SerializeField] private UnityEvent OnUsed;
        [SerializeField] private UnityEvent<bool> OnToggled;
        bool toggled = false;
        private void Awake()
        {
            XRGrabInteractable xrint = GetComponent<XRGrabInteractable>();
            if(xrint == null)
            {
                Debug.LogWarning($"No XRGrabInteractable in {this.gameObject.name}!");
                return;
            }
            xrint.activated.AddListener(OnUse);
        }

        public void OnUse(ActivateEventArgs args)
        {
            OnUse();
        }
        public void OnUse()
        {
            OnUsed.Invoke();
            toggled = !toggled;
            OnToggled.Invoke(toggled);
        }
    }
}