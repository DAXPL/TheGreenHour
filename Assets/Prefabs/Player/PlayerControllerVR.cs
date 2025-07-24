using GreenHour.PhysicsSurface;
using System;
using UnityEngine;
using UnityEngine.Audio;
using static GreenHour.PhysicsSurface.SurfaceData;
namespace GreenHour.Player
{
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(AudioSource))]
    public class PlayerControllerVR : MonoBehaviour
    {

        [SerializeField] private float stepDistance = 0.5f;
        [SerializeField] private LayerMask walkingMask;
        [SerializeField] private SurfaceData defaultSurfaceData;
        private Vector3 lastPos;
        private CharacterController characterController;
        private AudioSource audioSource;
        private float stepLen = 0.0f;
        private float airborneTime = 0f;
        private bool wasGrounded = false;
        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
            audioSource = GetComponent<AudioSource>();
        }
        private void Update()
        {
            HandleMovement();
        }

        private void HandleMovement()
        {
            float distance = Vector3.Distance(lastPos, transform.position);
            bool isGrounded = characterController.isGrounded;
            bool isCrouching = characterController.height <=1f;
            
            if (isGrounded == false)
            {
                airborneTime += Time.deltaTime;
            }
            if (wasGrounded == false && isGrounded && airborneTime >= 0.2f)
            {
                Debug.Log("Player landed on the ground.");
                PlaySound(SoundType.Landing, 2f, 0.75f);
                wasGrounded = true;
                airborneTime = 0f;
            }
            if (isGrounded)
            {
                airborneTime = 0.0f;
            }
            stepLen += distance;
            if (stepLen >= stepDistance)
            {
                PlaySound(SoundType.Walk, isCrouching ? 0.25f : 1.0f);
                stepLen = 0.0f;
            }
            wasGrounded = characterController.isGrounded;
            lastPos = transform.position;
        }

        private void PlaySound(SoundType type, float volumeScale = 1.0f, float pitchScale = 1.0f)
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