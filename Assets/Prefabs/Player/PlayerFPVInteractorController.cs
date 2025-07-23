using GreenHour.Interactions;
using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GreenHour.Player
{
    public class PlayerFPVInteractorController : MonoBehaviour
    {
        [SerializeField] private Camera playerCamera;
        private Interactor interactor;
        [Header("Key bindings")]
        [SerializeField] private InputActionReference primaryActionReference;
        [SerializeField] private InputActionReference secondaryActionReference;
        [Header("Interaction settings")]
        [SerializeField] private float actionRange = 5.0f;
        [SerializeField] private LayerMask actionMask;

        private void OnEnable()
        {
            if (primaryActionReference)
            {
                primaryActionReference.action.performed += OnPrimaryAction;
                primaryActionReference.action.canceled += OnPrimaryActionCanceled;
            }
        }

        private void OnDisable()
        {
            if (primaryActionReference)
            {
                primaryActionReference.action.performed -= OnPrimaryAction;
                primaryActionReference.action.canceled -= OnPrimaryActionCanceled;
            }
        }
        
        private void OnPrimaryAction(InputAction.CallbackContext context)
        {
            Interactor i = GetInteractor();
            if(i == null) return;
            interactor = i;
            interactor.StartInteraction();
        }

        private void OnPrimaryActionCanceled(InputAction.CallbackContext context)
        {
            if(interactor != null)
            {
                interactor.StopInteraction();
            }
            interactor = null;
        }

        private void Update()
        {
            if (interactor == null) return;
            Interactor i = GetInteractor();

            if (i == null || i != interactor)
            {
                interactor.StopInteraction();
                interactor = null;
                return;
            }
        }

        private Interactor GetInteractor()
        {
            RaycastHit hit;
            Transform origin = playerCamera ? playerCamera.transform : this.transform;
            if (!Physics.Raycast(origin.position, origin.forward, out hit, actionRange, actionMask))
            {
                Debug.DrawRay(origin.position, origin.TransformDirection(Vector3.forward) * actionRange, Color.red, 1.0f);
                return null; 
            }
            if (!hit.collider.TryGetComponent(out Interactor i))
            {
                Debug.DrawRay(origin.position, origin.TransformDirection(Vector3.forward) * hit.distance, Color.yellow, 1.0f);
                return null;
            }
            Debug.DrawRay(origin.position, origin.TransformDirection(Vector3.forward) * hit.distance, Color.green, 1.0f);
            return i;
        }
    }
}