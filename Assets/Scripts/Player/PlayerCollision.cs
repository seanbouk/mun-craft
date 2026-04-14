using MunCraft.Core;
using UnityEngine;

namespace MunCraft.Player
{
    /// <summary>
    /// Custom capsule-vs-sphere collision against the block lattice.
    /// No Unity physics colliders on blocks — we query the lattice directly.
    /// </summary>
    public class PlayerCollision : MonoBehaviour
    {
        [Header("Capsule")]
        public float Height = 1.8f;
        public float Radius = 0.3f;

        [Header("Collision")]
        [Tooltip("Circumscribed sphere radius of a truncated octahedron (blockSize=1)")]
        public float BlockCollisionRadius = 0.559f;

        [Tooltip("How many lattice cells to search in each direction")]
        public int SearchRadius = 2;

        [Tooltip("Maximum slope angle (degrees) that counts as ground")]
        public float MaxGroundAngle = 60f;

        ChunkManager _chunkManager;

        public struct CollisionResult
        {
            public Vector3 Position;
            public bool IsGrounded;
            public bool HitSomething;
            public Vector3 PushDirection; // aggregate push normal
        }

        public void Initialize(ChunkManager chunkManager)
        {
            _chunkManager = chunkManager;
        }

        /// <summary>
        /// Test the player capsule at the given position against nearby blocks.
        /// Returns the resolved position and grounding state.
        /// </summary>
        public CollisionResult ResolveCollision(Vector3 position, Vector3 up)
        {
            var result = new CollisionResult
            {
                Position = position,
                IsGrounded = false,
                HitSomething = false,
                PushDirection = Vector3.zero
            };

            if (_chunkManager == null) return result;

            float blockSize = _chunkManager.BlockSize;
            float blockRadius = BlockCollisionRadius * blockSize;

            // Capsule is defined by two sphere centers (top and bottom of the inner line segment)
            float halfHeight = Height * 0.5f;
            float innerHalfHeight = halfHeight - Radius;
            Vector3 capsuleTop = position + up * innerHalfHeight;
            Vector3 capsuleBottom = position - up * innerHalfHeight;

            // Find nearby blocks
            // Check a region around the player position
            int scan = SearchRadius;
            float groundDotThreshold = Mathf.Cos(MaxGroundAngle * Mathf.Deg2Rad);

            for (int parity = 0; parity <= 1; parity++)
            {
                // Find approximate lattice coordinates for the player
                Vector3 searchCenter = position;
                float offset = parity * 0.5f * blockSize;
                int cx = Mathf.RoundToInt((searchCenter.x - offset) / blockSize);
                int cy = Mathf.RoundToInt((searchCenter.y - offset) / blockSize);
                int cz = Mathf.RoundToInt((searchCenter.z - offset) / blockSize);

                for (int dz = -scan; dz <= scan; dz++)
                for (int dy = -scan; dy <= scan; dy++)
                for (int dx = -scan; dx <= scan; dx++)
                {
                    var addr = new BlockAddress(parity, cx + dx, cy + dy, cz + dz);
                    if (!_chunkManager.IsSolid(addr)) continue;

                    Vector3 blockPos = addr.ToWorldPosition(blockSize);

                    // Capsule vs sphere collision
                    // Find closest point on capsule line segment to sphere center
                    Vector3 closest = ClosestPointOnSegment(capsuleTop, capsuleBottom, blockPos);
                    Vector3 diff = closest - blockPos;
                    float dist = diff.magnitude;
                    float combinedRadius = Radius + blockRadius;

                    if (dist < combinedRadius && dist > 0.0001f)
                    {
                        // Penetration — push out
                        float penetration = combinedRadius - dist;
                        Vector3 pushDir = diff.normalized;

                        result.Position += pushDir * penetration;
                        result.HitSomething = true;
                        result.PushDirection += pushDir;

                        // Update capsule endpoints for subsequent tests
                        capsuleTop = result.Position + up * innerHalfHeight;
                        capsuleBottom = result.Position - up * innerHalfHeight;

                        // Check if this counts as ground
                        float groundDot = Vector3.Dot(pushDir, up);
                        if (groundDot > groundDotThreshold)
                            result.IsGrounded = true;
                    }
                }
            }

            if (result.HitSomething)
                result.PushDirection.Normalize();

            return result;
        }

        static Vector3 ClosestPointOnSegment(Vector3 a, Vector3 b, Vector3 point)
        {
            Vector3 ab = b - a;
            float t = Vector3.Dot(point - a, ab) / ab.sqrMagnitude;
            t = Mathf.Clamp01(t);
            return a + ab * t;
        }
    }
}
