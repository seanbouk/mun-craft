using MunCraft.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MunCraft.Player
{
    /// <summary>
    /// First-person camera. Mouse X rotates the player's body around the up axis
    /// (so movement direction follows where you look). Mouse Y is camera-only pitch.
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
        bool _cursorLocked;

        void Start()
        {
            _controller = GetComponentInParent<PlayerController>();
            LockCursor();
        }

        void LateUpdate()
        {
            if (_controller == null || GameState.MenuOpen) return;

            var kb = Keyboard.current;
            var mouse = Mouse.current;

            if (kb != null && kb.escapeKey.wasPressedThisFrame)
            {
                if (_cursorLocked) UnlockCursor();
                else LockCursor();
            }

            if (mouse != null && mouse.leftButton.wasPressedThisFrame && !_cursorLocked)
            {
                LockCursor();
            }

            Vector3 up = _controller.CurrentUp;
            float mouseX = 0, mouseY = 0;

            if (_cursorLocked && mouse != null)
            {
                Vector2 delta = mouse.delta.ReadValue();
                mouseX = delta.x * MouseSensitivity;
                mouseY = delta.y * MouseSensitivity;
            }

            // Mouse X rotates the player body around the up axis
            // This makes WASD movement follow the look direction
            if (Mathf.Abs(mouseX) > 0.0001f)
            {
                _controller.transform.rotation = Quaternion.AngleAxis(mouseX, up) * _controller.transform.rotation;
            }

            // Mouse Y is camera-only pitch
            _pitch -= mouseY;
            _pitch = Mathf.Clamp(_pitch, PitchMin, PitchMax);

            // Position camera at eye height
            transform.position = _controller.transform.position + up * EyeHeight;

            // Camera direction = body forward + pitch rotation
            Vector3 bodyForward = _controller.transform.forward;
            Vector3 right = Vector3.Cross(up, bodyForward).normalized;
            if (right.sqrMagnitude < 0.001f)
                right = Vector3.Cross(up, Vector3.forward).normalized;
            bodyForward = Vector3.Cross(right, up).normalized;

            Quaternion pitchRot = Quaternion.AngleAxis(_pitch, right);
            Vector3 lookDir = pitchRot * bodyForward;

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
