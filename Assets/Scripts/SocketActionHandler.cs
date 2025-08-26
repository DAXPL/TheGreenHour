using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
namespace GreenHour.Gameplay
{
    [RequireComponent(typeof(XRSocketInteractor))]
    public class SocketActionHandler : MonoBehaviour
    {
        private XRSocketInteractor socket;

        void Awake()
        {
            socket = GetComponent<XRSocketInteractor>();
        }

        void OnEnable()
        {
            if(socket == null) return;
            socket.selectEntered.AddListener(OnItemInserted);
            socket.selectExited.AddListener(OnItemRemoved);
        }

        void OnDisable()
        {
            if (socket == null) return;
            socket.selectEntered.RemoveListener(OnItemInserted);
            socket.selectExited.RemoveListener(OnItemRemoved);
        }

        private void OnItemInserted(SelectEnterEventArgs args)
        {
            GameObject go = args.interactableObject.transform.gameObject;
            if (go == null) return;
            if(!go.TryGetComponent(out Action a)) return;
            a.enabled = true;
        }

        private void OnItemRemoved(SelectExitEventArgs args)
        {
            GameObject go = args.interactableObject.transform.gameObject;
            if (go == null) return;
            if (!go.TryGetComponent(out Action a)) return;
            a.enabled = false;
        }
    }
}