using GreenHour.Interactions;
using GreenHour.PhysicsSurface;
using System;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
namespace GreenHour.Player
{
    public class PlayerFPVInteractorController : MonoBehaviour
    {
        [SerializeField] private Camera playerCamera;
        private Interactor interactor;
        [Header("Key bindings")]
        [SerializeField] private InputActionReference primaryActionReference;
        [SerializeField] private InputActionReference secondaryActionReference;
        [SerializeField] private InputActionReference interactionActionReference;
        [SerializeField] private InputActionReference menuActionReference;
        [Header("Interaction settings")]
        [SerializeField] private float actionRange = 5.0f;
        [SerializeField] private LayerMask actionMask;
        [SerializeField] private float pushForce = 100.0f;
        [SerializeField] private SurfaceData defaultSurfaceData;
        [Header("Interface")]
        [SerializeField] private Animator interactionAnimator;
        [Header("Grabbing")]
        [SerializeField] private XRDirectInteractor grabOrigin;
        private XRGrabInteractable grabbedInteractable;
        [Header("UI")]
        [SerializeField] private GameObject interactionUI;

        private void OnEnable()
        {
            if (primaryActionReference)
            {
                primaryActionReference.action.started += OnPrimaryAction;
                primaryActionReference.action.canceled += OnPrimaryActionCanceled;
            }
            if (secondaryActionReference)
            {
                secondaryActionReference.action.started += OnSecondaryAction;
            }
            if (interactionActionReference)
            {
                interactionActionReference.action.started += OnInteractionAction;
                interactionActionReference.action.canceled += OnInteractionActionCanceled;
            }
            if(menuActionReference)
            {
                menuActionReference.action.started += OnMenuAction;
            }
        }

        private void OnMenuAction(InputAction.CallbackContext context)
        {
            if(interactionUI) interactionUI.SetActive(!interactionUI.activeSelf);
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
            GameObject go = GetGameobject(out Vector3 hitpoint);
            if (go != null && go.TryGetComponent(out XRGrabInteractable xrGrab))
            {
                Grab(xrGrab);
            }  
        }
        private void OnSecondaryAction(InputAction.CallbackContext context)
        {
            GameObject go = GetGameobject(out Vector3 hitpoint);
            if(go == null)  return;
            if (!go.TryGetComponent(out Rigidbody rb)) return;
            if (rb.isKinematic) return;

            //Adding force
            Vector3 angle = (hitpoint - playerCamera.transform.position).normalized;
            rb.AddForceAtPosition(angle * pushForce, hitpoint);

            //Sound effect based on surface
            if(go.TryGetComponent(out Surface surface))
            {
                surface.PlayHitSound(angle.magnitude, hitpoint);
            }
            else if(defaultSurfaceData)
            {
                defaultSurfaceData.PlayHitSound(angle.magnitude, hitpoint);
            }
                
        }
        private void OnInteractionAction(InputAction.CallbackContext context)
        {
            Interactor i = GetInteractor();
            if (i != null)
            {
                interactor = i;
                interactor.StartInteraction();
                return;
            }
        }
        private void OnPrimaryActionCanceled(InputAction.CallbackContext context)
        {
            Release();
        }
        private void OnInteractionActionCanceled(InputAction.CallbackContext context)
        {
            if (interactor != null)
            {
                interactor.StopInteraction();
            }
            interactor = null;
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
        
        private GameObject GetGameobject(out Vector3 hitPoint)
        {
            RaycastHit hit;
            hitPoint = Vector3.zero;
            Transform origin = playerCamera ? playerCamera.transform : this.transform;
            if (!Physics.Raycast(origin.position, origin.forward, out hit, actionRange, actionMask))
            {
                Debug.DrawRay(origin.position, origin.TransformDirection(Vector3.forward) * actionRange, Color.red, 1.0f);
                return null;
            }
            else
            {
                Debug.DrawRay(origin.position, origin.TransformDirection(Vector3.forward) * hit.distance, Color.green, 1.0f);
                hitPoint = hit.point;
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