using System;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.InputSystem;
using GreenHour.PhysicsSurface;
using static GreenHour.PhysicsSurface.Surface;
using static GreenHour.PhysicsSurface.SurfaceData;

namespace GreenHour.Player
{
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(AudioSource))]
    public class PlayerControllerFPV : MonoBehaviour
    {
        [SerializeField] private Camera playerCamera;

        [Header("Key Bindings")]
        [SerializeField] private InputActionReference moveReference;
        [SerializeField] private InputActionReference lookReference;
        [SerializeField] private InputActionReference crouchReference;

        [Header("Settings")]
        [SerializeField] private SurfaceData defaultSurfaceData;
        [SerializeField] private LayerMask walkingMask;
        [Space]
        [SerializeField] private float moveSpeed = 5f;
        [SerializeField] private float stepDistance = 0.5f;
        [SerializeField] private float lookSensitivity = 1f;
        [SerializeField] private float crouchHeight = 1f;
        [SerializeField] private float standingHeight = 2f;
        [SerializeField] private float crouchSpeed = 2.5f;
        
        private AudioSource audioSource;

        private CharacterController characterController;
        private Vector3 moveDirection;
        private Vector2 lookInput;

        private float gravity = -9.81f;
        private float verticalVelocity;
        private float pitch = 0f;

        private bool isCrouching = false;
        private bool wasGrounded = false;

        private float stepLen = 0.0f;
        private float airborneTime = 0f;

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
            audioSource = GetComponent<AudioSource>();
        }

        private void OnEnable()
        {
            if (moveReference)
            {
                moveReference.action.performed += OnMove;
                moveReference.action.canceled += OnMove;
            }
            if (lookReference)
            {
                lookReference.action.performed += OnLook;
                lookReference.action.canceled += OnLook;
            }
            if (crouchReference)
            {
                crouchReference.action.performed += OnCrouch;
            }
        }

        private void OnDisable()
        {
            if (moveReference)
            {
                moveReference.action.performed -= OnMove;
                moveReference.action.canceled -= OnMove;
            }
            if (lookReference)
            {
                lookReference.action.performed -= OnLook;
                lookReference.action.canceled -= OnLook;
            }
            if (crouchReference)
            {
                crouchReference.action.performed -= OnCrouch;
            }
        }

        private void Update()
        {
            HandleMovement();
            HandleLook();
            HandleCrouchTransition();
        }

        private void OnMove(InputAction.CallbackContext context)
        {
            Vector2 input = context.ReadValue<Vector2>();
            moveDirection = new Vector3(input.x, 0, input.y);
        }

        private void OnLook(InputAction.CallbackContext context)
        {
            lookInput = context.ReadValue<Vector2>();
        }

        private void OnCrouch(InputAction.CallbackContext context)
        {
            isCrouching = !isCrouching;
        }

        private void HandleMovement()
        {
            Vector3 move = transform.right * moveDirection.x + transform.forward * moveDirection.z;

            bool isGrounded = characterController.isGrounded;
            float currentSpeed = isCrouching ? crouchSpeed : moveSpeed;
            move *= currentSpeed;

            if(isGrounded == false)
            {
                airborneTime += Time.deltaTime;
            }
            if (wasGrounded == false && isGrounded && airborneTime>=0.2f)
            {
                Debug.Log("Player landed on the ground.");
                PlaySound(SoundType.Landing, 2f, 0.75f);
                wasGrounded = true;
                airborneTime = 0f;
            }

            if (isGrounded)
            {
                airborneTime = 0.0f;
                if (verticalVelocity < 0)verticalVelocity = -2f;
                
            }

            stepLen += move.magnitude * Time.deltaTime;
            if (stepLen >= stepDistance)
            {
                PlaySound(SoundType.Walk, isCrouching?0.25f:1.0f);
                stepLen = 0.0f;
            }

            verticalVelocity += gravity * Time.deltaTime;
            move.y = verticalVelocity;
            wasGrounded = characterController.isGrounded;
            characterController.Move(move * Time.deltaTime);
        }

        private void HandleLook()
        {
            if (playerCamera == null) return;

            float mouseX = lookInput.x * lookSensitivity;
            float mouseY = lookInput.y * lookSensitivity;

            pitch -= mouseY;
            pitch = Mathf.Clamp(pitch, -90f, 90f);

            playerCamera.transform.localRotation = Quaternion.Euler(pitch, 0, 0);
            transform.Rotate(Vector3.up * mouseX);
        }

        private void HandleCrouchTransition()
        {
            float targetHeight = isCrouching ? crouchHeight : standingHeight;

            if (!isCrouching)
            {
                Vector3 top = transform.position + Vector3.up * (characterController.height / 2);
                if (Physics.SphereCast(top, characterController.radius * 0.9f, Vector3.up, out RaycastHit hit, standingHeight - crouchHeight))
                {
                    targetHeight = crouchHeight;
                }
            }

            characterController.height = targetHeight;
            characterController.center = new Vector3(0, targetHeight / 2, 0);
            if (playerCamera) playerCamera.transform.localPosition = new Vector3(0, targetHeight * 0.75f, 0);
        }

        
        private void PlaySound(SoundType type, float volumeScale=1.0f, float pitchScale = 1.0f)
        {
            if (audioSource == null) return;
            Vector3 rayOrigin = transform.position + (Vector3.up * 0.1f);
            if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 0.2f, walkingMask))
            {
                Surface groundSurface = hit.collider.GetComponent<Surface>();
                AudioClip clip = groundSurface ? groundSurface.GetSound(type) : (defaultSurfaceData ? defaultSurfaceData.GetSound(type) : null);
                if (clip == null) return;
                audioSource.pitch = UnityEngine.Random.Range(0.9f, 1.1f) * pitchScale;//Sound fatigue
                float volume = audioSource.volume;
                audioSource.PlayOneShot(clip, volumeScale);
            }
        }
    }
}