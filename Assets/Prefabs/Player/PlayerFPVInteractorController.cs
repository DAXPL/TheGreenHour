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
            RaycastHit hit;
            Transform origin = playerCamera ? playerCamera.transform : this.transform;
            if (Physics.Raycast(origin.position, origin.forward, out hit, actionRange, actionMask))
            {
                if(hit.collider.gameObject.TryGetComponent(out Interactor i))
                {
                    Debug.DrawRay(origin.position, origin.TransformDirection(Vector3.forward) * hit.distance, Color.green, 1.0f);
                    interactor = i;
                    interactor.StartInteraction();
                }
                else
                {
                    Debug.DrawRay(origin.position, origin.TransformDirection(Vector3.forward) * hit.distance, Color.yellow, 1.0f);
                }    
            }
            else
            {
                Debug.DrawRay(origin.position, origin.TransformDirection(Vector3.forward) * actionRange, Color.red, 1.0f);
            }
        }

        private void OnPrimaryActionCanceled(InputAction.CallbackContext context)
        {
            interactor = null;
        }

        private void FixedUpdate()
        {
            if (interactor == null) return;
            RaycastHit hit;
            Transform origin = playerCamera ? playerCamera.transform : this.transform;
            if (!Physics.Raycast(origin.position, origin.forward, out hit, actionRange, actionMask))
            {
                interactor.StopInteraction();
                interactor = null;
                return;
            }
            if (!hit.collider.TryGetComponent(out Interactor i))
            {
                interactor.StopInteraction();
                interactor = null;
                return;
            }
            if (i != interactor) 
            {
                interactor.StopInteraction();
                interactor = null;
                return;
            }
            Debug.DrawRay(origin.position, origin.TransformDirection(Vector3.forward) * actionRange, Color.magenta, 1.0f);
        }
    }
}