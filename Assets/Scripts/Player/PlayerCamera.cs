using UnityEngine;
using UnityEngine.InputSystem;

namespace MunCraft.Player
{
    /// <summary>
    /// First-person camera that aligns with the player's gravity-defined up vector.
    /// Pitch (look up/down) is clamped. Yaw (look left/right) rotates around the up axis.
    /// Uses the new Input System for mouse input.
    /// </summary>
    public class PlayerCamera : MonoBehaviour
    {
        [Header("Mouse Look")]
        public float MouseSensitivity = 0.2f;
        public float PitchMin = -80f;
        public float PitchMax = 80f;

        [Header("Position")]
        public float EyeHeight = 0.7f;

        PlayerController _controller;
        float _pitch;
        float _yaw;
        bool _cursorLocked;

        void Start()
        {
            _controller = GetComponentInParent<PlayerController>();
            LockCursor();
        }

        void LateUpdate()
        {
            if (_controller == null) return;

            var kb = Keyboard.current;
            var mouse = Mouse.current;

            // Toggle cursor lock
            if (kb != null && kb.escapeKey.wasPressedThisFrame)
            {
                if (_cursorLocked) UnlockCursor();
                else LockCursor();
            }

            if (mouse != null && mouse.leftButton.wasPressedThisFrame && !_cursorLocked)
            {
                LockCursor();
            }

            // Mouse input
            if (_cursorLocked && mouse != null)
            {
                Vector2 delta = mouse.delta.ReadValue();
                _yaw += delta.x * MouseSensitivity;
                _pitch -= delta.y * MouseSensitivity;
                _pitch = Mathf.Clamp(_pitch, PitchMin, PitchMax);
            }

            // Position camera at eye height
            Vector3 up = _controller.CurrentUp;
            transform.position = _controller.transform.position + up * EyeHeight;

            // Build camera rotation
            Vector3 forward = _controller.transform.forward;
            Vector3 right = Vector3.Cross(up, forward).normalized;
            if (right.sqrMagnitude < 0.001f)
                right = Vector3.Cross(up, Vector3.forward).normalized;
            forward = Vector3.Cross(right, up).normalized;

            // Apply yaw
            Quaternion yawRot = Quaternion.AngleAxis(_yaw, up);
            forward = yawRot * forward;
            right = yawRot * right;

            // Apply pitch
            Quaternion pitchRot = Quaternion.AngleAxis(_pitch, right);
            Vector3 lookDir = pitchRot * forward;

            transform.rotation = Quaternion.LookRotation(lookDir, up);
        }

        void LockCursor()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            _cursorLocked = true;
        }

        void UnlockCursor()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            _cursorLocked = false;
        }

        public bool IsCursorLocked => _cursorLocked;
    }
}
