using MunCraft.Gravity;
using UnityEngine;

namespace MunCraft.Player
{
    /// <summary>
    /// First-person controller with dynamic gravity.
    /// No Rigidbody — custom velocity integration against the gravity field.
    /// </summary>
    public class PlayerController : MonoBehaviour
    {
        [Header("Movement")]
        public float MoveSpeed = 5f;
        public float JumpForce = 5f;
        public float GroundDrag = 8f;
        public float AirDrag = 0.5f;

        [Header("Gravity")]
        public float GravityMultiplier = 1f;
        public float OrientationSmoothTime = 0.15f;

        Vector3 _velocity;
        Vector3 _currentUp;
        bool _isGrounded;
        PlayerCollision _collision;

        public Vector3 Velocity => _velocity;
        public Vector3 CurrentUp => _currentUp;
        public bool IsGrounded => _isGrounded;
        public Vector3 GravityDirection { get; private set; }

        void Awake()
        {
            _collision = GetComponent<PlayerCollision>();
            _currentUp = Vector3.up;
        }

        void Update()
        {
            // Get gravity at current position
            Vector3 gravity = Vector3.zero;
            if (GravityField.Instance != null)
                gravity = GravityField.Instance.GetGravityAt(transform.position);

            GravityDirection = gravity;

            // Target "up" is opposite gravity
            Vector3 targetUp = gravity.sqrMagnitude > 0.001f ? -gravity.normalized : Vector3.up;

            // Smoothly rotate to align with gravity
            _currentUp = Vector3.Slerp(_currentUp, targetUp,
                1f - Mathf.Exp(-10f / Mathf.Max(OrientationSmoothTime, 0.01f) * Time.deltaTime));
            _currentUp.Normalize();

            // Build orientation: keep looking in current forward direction, but align up with gravity
            Vector3 forward = transform.forward;
            Vector3 right = Vector3.Cross(_currentUp, forward).normalized;
            if (right.sqrMagnitude < 0.001f)
                right = Vector3.Cross(_currentUp, Vector3.forward).normalized;
            forward = Vector3.Cross(right, _currentUp).normalized;

            // Apply movement input
            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");
            Vector3 moveDir = (right * h + forward * v).normalized;

            if (_isGrounded)
            {
                _velocity += moveDir * MoveSpeed * Time.deltaTime * 10f;

                // Ground drag
                Vector3 lateralVel = _velocity - Vector3.Dot(_velocity, _currentUp) * _currentUp;
                Vector3 verticalVel = Vector3.Dot(_velocity, _currentUp) * _currentUp;
                lateralVel *= Mathf.Max(0, 1f - GroundDrag * Time.deltaTime);
                _velocity = lateralVel + verticalVel;

                // Jump
                if (Input.GetButtonDown("Jump"))
                {
                    _velocity += _currentUp * JumpForce;
                    _isGrounded = false;
                }
            }
            else
            {
                // Air control (reduced)
                _velocity += moveDir * MoveSpeed * 0.3f * Time.deltaTime * 10f;
                _velocity *= Mathf.Max(0, 1f - AirDrag * Time.deltaTime);
            }

            // Apply gravity
            _velocity += gravity * GravityMultiplier * Time.deltaTime;

            // Integrate position
            Vector3 newPos = transform.position + _velocity * Time.deltaTime;

            // Collision resolution
            if (_collision != null)
            {
                var result = _collision.ResolveCollision(newPos, _currentUp);
                newPos = result.Position;
                _isGrounded = result.IsGrounded;

                // Zero out velocity into collision surfaces
                if (result.HitSomething)
                {
                    // Project velocity to remove component going into the collision
                    float velAlongNormal = Vector3.Dot(_velocity, result.PushDirection);
                    if (velAlongNormal < 0)
                        _velocity -= result.PushDirection * velAlongNormal;
                }
            }

            transform.position = newPos;

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
        }
    }
}
