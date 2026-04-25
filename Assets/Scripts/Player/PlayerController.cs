using MunCraft.Core;
using MunCraft.Gravity;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MunCraft.Player
{
    /// <summary>
    /// First-person controller with dynamic gravity.
    /// No Rigidbody — custom velocity integration against the gravity field.
    /// Uses substeps to prevent tunneling through thin surfaces.
    /// </summary>
    public class PlayerController : MonoBehaviour
    {
        [Header("Movement")]
        public float MoveSpeed = 5f;
        public float JumpForce = 5f;
        public float GroundDrag = 8f;
        public float AirDrag = 0.5f;
        public float MaxVelocity = 20f;

        [Header("Gravity")]
        public float GravityMultiplier = 1f;
        public float OrientationSmoothTime = 0.15f;

        [Header("Grounding")]
        [Tooltip("Small downward force applied when grounded to keep player on surface")]
        public float GroundSnapForce = 2f;

        [Header("Physics")]
        [Tooltip("Max distance per substep — smaller = more accurate but slower")]
        public float MaxStepDistance = 0.3f;

        [Header("Grounding")]
        [Tooltip("Seconds to stay 'grounded' after losing ground contact")]
        public float CoyoteTime = 0.25f;

        Vector3 _velocity;
        Vector3 _currentUp;
        bool _isGrounded;
        float _groundedTimer;
        PlayerCollision _collision;
        bool _loggedStartup;

        // Landing-event state. Decoupled from coyote time so brief contact
        // loss while running over bumpy terrain doesn't fire a landing sound.
        const float LandingGracePeriod = 0.4f;
        float _airborneTimer;        // seconds since last real ground contact
        bool _flaggedAirborne;       // crossed the grace period (or jumped)

        // Debug state — read by DebugUI
        [System.NonSerialized] public float _lastH, _lastV, _lastMouseX, _lastMouseY;
        [System.NonSerialized] public bool _lastAnyKey, _lastFocused;

        public Vector3 Velocity => _velocity;
        public Vector3 CurrentUp => _currentUp;
        public bool IsGrounded => _isGrounded;
        public Vector3 GravityDirection { get; private set; }

        public event System.Action OnJumped;
        public event System.Action OnLanded;

        void Awake()
        {
            _currentUp = Vector3.up;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0 || GameState.MenuOpen) return;

            // Lazy-find collision component (avoids Awake ordering issues)
            if (_collision == null)
                _collision = GetComponent<PlayerCollision>();

            if (!_loggedStartup)
            {
                _loggedStartup = true;
                UnityEngine.Debug.Log($"[PlayerController] Started. Collision={(_collision != null ? "FOUND" : "NULL")}");
            }

            // Get gravity at current position
            Vector3 gravity = Vector3.zero;
            if (GravityField.Instance != null)
                gravity = GravityField.Instance.GetGravityAt(transform.position);

            GravityDirection = gravity;

            // Target "up" is opposite gravity
            Vector3 targetUp = gravity.sqrMagnitude > 0.001f ? -gravity.normalized : Vector3.up;

            // Smoothly rotate to align with gravity
            _currentUp = Vector3.Slerp(_currentUp, targetUp,
                1f - Mathf.Exp(-10f / Mathf.Max(OrientationSmoothTime, 0.01f) * dt));
            _currentUp.Normalize();

            // Build orientation: keep looking in current forward direction, but align up with gravity
            Vector3 forward = transform.forward;
            Vector3 right = Vector3.Cross(_currentUp, forward).normalized;
            if (right.sqrMagnitude < 0.001f)
                right = Vector3.Cross(_currentUp, Vector3.forward).normalized;
            forward = Vector3.Cross(right, _currentUp).normalized;

            // Apply movement input (using new Input System directly)
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            float h = 0, v = 0, mouseX = 0, mouseY = 0;
            bool jumpPressed = false;
            if (kb != null)
            {
                if (kb.dKey.isPressed) h += 1f;
                if (kb.aKey.isPressed) h -= 1f;
                if (kb.wKey.isPressed) v += 1f;
                if (kb.sKey.isPressed) v -= 1f;
                jumpPressed = kb.spaceKey.wasPressedThisFrame;
            }
            if (mouse != null)
            {
                Vector2 mouseDelta = mouse.delta.ReadValue();
                mouseX = mouseDelta.x * 0.1f;
                mouseY = mouseDelta.y * 0.1f;
            }
            Vector3 moveDir = (right * h + forward * v);
            if (moveDir.sqrMagnitude > 1f) moveDir.Normalize();

            // Expose input state for on-screen debug
            _lastH = h; _lastV = v; _lastMouseX = mouseX; _lastMouseY = mouseY;
            _lastAnyKey = kb != null && kb.anyKey.isPressed; _lastFocused = Application.isFocused;

            if (_isGrounded)
            {
                // Cancel any velocity into the ground
                float velIntoGround = Vector3.Dot(_velocity, -_currentUp);
                if (velIntoGround > 0)
                    _velocity += _currentUp * velIntoGround;

                // Movement on surface
                _velocity += moveDir * MoveSpeed * dt * 10f;

                // Ground drag
                Vector3 lateralVel = _velocity - Vector3.Dot(_velocity, _currentUp) * _currentUp;
                Vector3 verticalVel = Vector3.Dot(_velocity, _currentUp) * _currentUp;
                lateralVel *= Mathf.Max(0, 1f - GroundDrag * dt);
                _velocity = lateralVel + verticalVel;

                // Small snap force to keep player pressed against surface
                // (collision will resolve this, but it keeps ground contact stable)
                _velocity -= _currentUp * GroundSnapForce * dt;

                // Jump
                if (jumpPressed)
                {
                    _velocity += _currentUp * JumpForce;
                    _isGrounded = false;
                    _groundedTimer = 0; // prevent coyote-time double jump
                    _flaggedAirborne = true; // explicit jump always counts as airborne
                    _airborneTimer = LandingGracePeriod;
                    OnJumped?.Invoke();
                }

                // Do NOT apply gravity when grounded — collision handles it
            }
            else
            {
                // Apply gravity only when airborne
                _velocity += gravity * GravityMultiplier * dt;

                // Air control (reduced)
                _velocity += moveDir * MoveSpeed * 0.3f * dt * 10f;
                _velocity *= Mathf.Max(0, 1f - AirDrag * dt);
            }

            // Clamp velocity
            if (_velocity.sqrMagnitude > MaxVelocity * MaxVelocity)
                _velocity = _velocity.normalized * MaxVelocity;

            // Capture incoming velocity for the landing-impact check below
            // (substep collision resolution may zero it out by the end of the frame).
            Vector3 incomingVelocity = _velocity;

            // Substep integration to prevent tunneling
            Vector3 totalMove = _velocity * dt;
            float totalDist = totalMove.magnitude;
            int steps = Mathf.Max(1, Mathf.CeilToInt(totalDist / MaxStepDistance));
            Vector3 stepMove = totalMove / steps;

            Vector3 pos = transform.position;
            bool grounded = false;

            for (int i = 0; i < steps; i++)
            {
                Vector3 newPos = pos + stepMove;

                if (_collision != null)
                {
                    var result = _collision.ResolveCollision(newPos, _currentUp);
                    newPos = result.Position;

                    if (result.IsGrounded)
                        grounded = true;

                    if (result.HitSomething)
                    {
                        // Zero out velocity into collision surface
                        float velAlongNormal = Vector3.Dot(_velocity, result.PushDirection);
                        if (velAlongNormal < 0)
                            _velocity -= result.PushDirection * velAlongNormal;

                        // Recalculate remaining step moves with corrected velocity
                        int remaining = steps - i - 1;
                        if (remaining > 0)
                            stepMove = _velocity * dt / steps;
                    }
                }

                pos = newPos;
            }

            // Coyote time: stay grounded briefly after losing contact
            if (grounded)
            {
                _groundedTimer = CoyoteTime;
                _isGrounded = true;
            }
            else
            {
                _groundedTimer -= dt;
                _isGrounded = _groundedTimer > 0;
            }

            // Landing event: only fire if we actually spent a meaningful amount
            // of time airborne (or explicitly jumped). Brief contact loss while
            // running on uneven terrain doesn't count.
            if (grounded)
            {
                if (_flaggedAirborne)
                {
                    float downVel = Vector3.Dot(incomingVelocity, -_currentUp);
                    if (downVel > 1f) OnLanded?.Invoke();
                    _flaggedAirborne = false;
                }
                _airborneTimer = 0f;
            }
            else
            {
                _airborneTimer += dt;
                if (_airborneTimer >= LandingGracePeriod)
                    _flaggedAirborne = true;
            }

            transform.position = pos;

            // Apply orientation
            Quaternion targetRot = Quaternion.LookRotation(forward, _currentUp);
            transform.rotation = targetRot;
        }

        /// <summary>
        /// Teleport the player to a position and reset velocity.
        /// </summary>
        public void Teleport(Vector3 position, Vector3 up)
        {
            transform.position = position;
            _velocity = Vector3.zero;
            _currentUp = up;
            _isGrounded = false;
            _groundedTimer = 0;
            _airborneTimer = 0f;
            _flaggedAirborne = false;
        }
    }
}
