using GreenHour.Interactions;
using GreenHour.Interactions.Items;
using GreenHour.PhysicsSurface;
using System;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.UI;
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
        [SerializeField] private XRDirectInteractor itemGrabOrigin;
        private XRGrabInteractable grabbedInteractable;
        private Item grabbedItem;
        [Header("UI")]
        [SerializeField] private GameObject interactionUI;
        [SerializeField] private Image progressImage;

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
                PrimaryGrab(xrGrab);
            }
        }
        private void OnSecondaryAction(InputAction.CallbackContext context)
        {
            if (grabbedItem != null)
            {
                ItemRelease();
                return;
            }

            if (grabbedInteractable != null)
            {
                PrimaryRelease();
                return;
            }

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
            Interactor i = GetInteractor(out Item item);
            if (item != null && grabbedItem == null) 
            {
                ItemGrab(item);
            }
            else if (i != null)
            {
                interactor = i;
                interactor.StartInteraction();
                return;
            }
            else if (grabbedItem != null)
            {
                grabbedItem.OnUse();
            }
        }
        private void OnPrimaryActionCanceled(InputAction.CallbackContext context)
        {
            PrimaryRelease();
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
            Interactor i = GetInteractor(out Item item);
            interactionAnimator?.SetBool("isActive", (i != null || item != null));

            if (interactor == null)
            {
                if (progressImage) progressImage.fillAmount = 0;
                return;
            }

            if (i == null || i != interactor)
            {
                interactor.StopInteraction();
                interactor = null;
                if (progressImage) progressImage.fillAmount = 0;
                return;
            }
            if(progressImage)progressImage.fillAmount = interactor.InteractionProgress();

        }

        private Interactor GetInteractor(out Item item)
        {
            item = null;
            RaycastHit hit;
            Transform origin = playerCamera ? playerCamera.transform : this.transform;
            if (!Physics.Raycast(origin.position, origin.forward, out hit, actionRange, actionMask))
            {
                Debug.DrawRay(origin.position, origin.TransformDirection(Vector3.forward) * actionRange, Color.red, 1.0f);
                return null; 
            }
            if(hit.collider.TryGetComponent(out Item hitItem)) item = hitItem;
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
        
        public void PrimaryGrab(XRGrabInteractable interactable)
        {
            if (grabbedInteractable != null) return;
            grabbedInteractable = interactable;
            grabOrigin.StartManualInteraction((IXRSelectInteractable)interactable);
        }

        public void PrimaryRelease()
        {
            if (grabbedInteractable == null) return;

            grabOrigin.EndManualInteraction();
            grabbedInteractable = null;
        }
        
        public void ItemGrab(Item item)
        {
            if(item == null) return;
            if (grabbedItem != null) return;
            grabbedItem = item;

            if(itemGrabOrigin) itemGrabOrigin.StartManualInteraction((IXRSelectInteractable)item.gameObject.GetComponent(typeof(IXRSelectInteractable)));
        }
        public void ItemRelease()
        {
            if (grabbedItem == null) return;
            if(itemGrabOrigin)itemGrabOrigin.EndManualInteraction();
            Rigidbody rb = grabbedItem.GetComponent<Rigidbody>();
            grabbedItem = null;
            if (rb != null)
            {
                rb.AddForce(playerCamera.transform.forward * pushForce);
            }
        }
    }
}