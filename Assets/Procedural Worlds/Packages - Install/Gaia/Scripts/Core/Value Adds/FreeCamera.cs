﻿using UnityEngine;
#if GAIA_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Gaia
{
    /// <summary>
    /// This camera script has been adapted from the camera provided by Unity with the black smith environment pack
    /// </summary>

    public class FreeCamera : MonoBehaviour
    {
        public bool enableInputCapture = true;
        public bool lockAndHideCursor = false;
        public bool holdRightMouseCapture = true;

        public float lookSpeed = 5f;
        public float moveSpeed = 5f;
        public float sprintSpeed = 50f;
        public bool m_useScrollSpeedIncrease = true;
        public float m_speedIncreaseValue = 100f;
        public float m_cameraRoll = 0f;

        private bool m_inputCaptured;
        private float m_yaw;
        private float m_pitch;
        private const string m_scrollWheelKey = "Mouse ScrollWheel";
        private const string m_mouseXKey = "Mouse X";
        private const string m_mouseYKey = "Mouse Y";
        private const string m_verticalKey = "Vertical";
        private const string m_horizontalKey = "Horizontal";

        private void Awake()
        {
            enabled = enableInputCapture;
        }

        private void OnEnable()
        {
            if (enableInputCapture && !holdRightMouseCapture)
                CaptureInput();
        }

        private void OnDisable()
        {
            ReleaseInput();
        }

        private void OnValidate()
        {
            if (Application.isPlaying)
                enabled = enableInputCapture;
        }

        private void CaptureInput()
        {
            if (lockAndHideCursor)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            m_inputCaptured = true;

            m_yaw = transform.eulerAngles.y;
            m_pitch = transform.eulerAngles.x;
        }

        private void ReleaseInput()
        {
            if (lockAndHideCursor)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            m_inputCaptured = false;
        }

        private void OnApplicationFocus(bool focus)
        {
            if (m_inputCaptured && !focus)
                ReleaseInput();
        }

        private void Update()
        {
#if GAIA_INPUT_SYSTEM
            if (!enableInputCapture)
            {
                return;
            }

            //Make sure we have a mouse
            if (Mouse.current == null)
            {
                return;
            }

            if (!m_inputCaptured)
            {
                if (!holdRightMouseCapture && Mouse.current.leftButton.wasPressedThisFrame)
                    CaptureInput();
                else if (holdRightMouseCapture && Mouse.current.rightButton.wasPressedThisFrame)
                    CaptureInput();
            }

            if (!m_inputCaptured)
                return;

            if (m_useScrollSpeedIncrease)
            {
                sprintSpeed += (Mouse.current.scroll.ReadValue().y / 500f * m_speedIncreaseValue);
                if (sprintSpeed < moveSpeed)
                {
                    sprintSpeed = moveSpeed;
                }
            }

            if (!holdRightMouseCapture && Keyboard.current[Key.Escape].wasPressedThisFrame)
                ReleaseInput();
            else if (holdRightMouseCapture && Mouse.current.rightButton.wasReleasedThisFrame)
                ReleaseInput();

            float rotStrafe = Mouse.current.delta.x.value / 20f;
            float rotFwd = Mouse.current.delta.y.value / 10f;

            m_yaw = (m_yaw + lookSpeed * rotStrafe) % 360f;
            m_pitch = (m_pitch - lookSpeed * rotFwd) % 360f;
            transform.rotation = Quaternion.AngleAxis(m_yaw, Vector3.up) * Quaternion.AngleAxis(m_pitch, Vector3.right);
            transform.eulerAngles = new Vector3(transform.eulerAngles.x, transform.eulerAngles.y, m_cameraRoll);

            float speed = Time.deltaTime * (Keyboard.current[Key.LeftShift].value > 0 ? sprintSpeed : moveSpeed);
            float forward = speed * Keyboard.current[Key.W].value + speed * -Keyboard.current[Key.S].value;
            float right = speed * Keyboard.current[Key.D].value + speed * -Keyboard.current[Key.A].value;
            float up = speed * Keyboard.current[Key.E].value + speed * -Keyboard.current[Key.Q].value;
            transform.position += transform.forward * forward + transform.right * right + Vector3.up * up;
#endif
        }

        public void RefreshCameraRoll(float roll)
        {
            m_cameraRoll = roll;
            transform.eulerAngles = new Vector3(transform.eulerAngles.x, transform.eulerAngles.y, m_cameraRoll);
        }
    }
}