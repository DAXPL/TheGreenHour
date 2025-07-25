using GreenHour.Interactions;
using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
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
        [Header("Interface")]
        [SerializeField] private Animator interactionAnimator;
        [Header("Grabbing")]
        [SerializeField] private XRDirectInteractor grabOrigin;
        private XRGrabInteractable grabbedInteractable;
        private FixedJoint grabJoint;


        private void OnEnable()
        {
            if (primaryActionReference)
            {
                primaryActionReference.action.started += OnPrimaryAction;
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
            if(i != null)
            {
                interactor = i;
                interactor.StartInteraction();
                return;
            }

            GameObject go = GetGameobject();
            if (go != null && go.TryGetComponent(out XRGrabInteractable xrGrab))
            {
                Grab(xrGrab);
            }
            
        }

        private void OnPrimaryActionCanceled(InputAction.CallbackContext context)
        {
            if(interactor != null)
            {
                interactor.StopInteraction();
            }
            interactor = null;
            Release();
        }

        private void Update()
        {
            Interactor i = GetInteractor();
            interactionAnimator?.SetBool("isActive", i != null);
            if (interactor == null) return;

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
        
        private GameObject GetGameobject()
        {
            RaycastHit hit;
            Transform origin = playerCamera ? playerCamera.transform : this.transform;
            if (!Physics.Raycast(origin.position, origin.forward, out hit, actionRange, actionMask))
            {
                Debug.DrawRay(origin.position, origin.TransformDirection(Vector3.forward) * actionRange, Color.red, 1.0f);
                return null;
            }
            else
            {
                Debug.DrawRay(origin.position, origin.TransformDirection(Vector3.forward) * hit.distance, Color.green, 1.0f);
                return hit.collider.gameObject;
            }
        }
        
        public void Grab(XRGrabInteractable interactable)
        {
            if (grabbedInteractable != null) return;
            grabbedInteractable = interactable;
            grabOrigin.StartManualInteraction((IXRSelectInteractable)interactable);
        }
        public void Release()
        {
            if (grabbedInteractable == null) return;

            grabOrigin.EndManualInteraction();
            grabbedInteractable = null;
        }
    }
}