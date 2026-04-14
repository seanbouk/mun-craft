using MunCraft.Core;
using UnityEngine;

namespace MunCraft.Player
{
    /// <summary>
    /// Custom capsule-vs-sphere collision against the block lattice.
    /// No Unity physics colliders on blocks — we query the lattice directly.
    /// Uses multiple resolution passes for stability with overlapping collision spheres.
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

        [Tooltip("Extra push distance to prevent floating-point jitter")]
        public float SkinWidth = 0.02f;

        [Tooltip("Number of collision resolution passes per frame")]
        public int Iterations = 4;

        [Tooltip("How far below feet to probe for ground (in block units)")]
        public float GroundProbeDistance = 0.7f;

        [Header("Debug")]
        public bool ShowDebug;

        ChunkManager _chunkManager;

        // Debug stats (public so DebugUI can read them)
        [System.NonSerialized] public int LastBlocksChecked;
        [System.NonSerialized] public int LastCollisionsFound;
        [System.NonSerialized] public float LastDeepestPenetration;
        [System.NonSerialized] public Vector3 LastPushDir;

        public struct CollisionResult
        {
            public Vector3 Position;
            public bool IsGrounded;
            public bool HitSomething;
            public Vector3 PushDirection;
        }

        public void Initialize(ChunkManager chunkManager)
        {
            _chunkManager = chunkManager;
        }

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
            float combinedRadius = Radius + blockRadius;
            float halfHeight = Height * 0.5f;
            float innerHalfHeight = halfHeight - Radius;
            float groundDotThreshold = Mathf.Cos(MaxGroundAngle * Mathf.Deg2Rad);

            int totalChecked = 0;
            int totalCollisions = 0;
            float deepest = 0f;

            // Multiple passes for stability
            for (int iter = 0; iter < Iterations; iter++)
            {
                Vector3 capsuleTop = result.Position + up * innerHalfHeight;
                Vector3 capsuleBottom = result.Position - up * innerHalfHeight;

                // Track the deepest penetration this pass
                float deepestPenetration = 0f;
                Vector3 deepestPushDir = Vector3.zero;
                bool hitThisPass = false;

                for (int parity = 0; parity <= 1; parity++)
                {
                    float offset = parity * 0.5f * blockSize;
                    int cx = Mathf.RoundToInt((result.Position.x - offset) / blockSize);
                    int cy = Mathf.RoundToInt((result.Position.y - offset) / blockSize);
                    int cz = Mathf.RoundToInt((result.Position.z - offset) / blockSize);

                    for (int dz = -SearchRadius; dz <= SearchRadius; dz++)
                    for (int dy = -SearchRadius; dy <= SearchRadius; dy++)
                    for (int dx = -SearchRadius; dx <= SearchRadius; dx++)
                    {
                        var addr = new BlockAddress(parity, cx + dx, cy + dy, cz + dz);
                        if (!_chunkManager.IsSolid(addr)) continue;

                        totalChecked++;
                        Vector3 blockPos = addr.ToWorldPosition(blockSize);
                        Vector3 closest = ClosestPointOnSegment(capsuleTop, capsuleBottom, blockPos);
                        Vector3 diff = closest - blockPos;
                        float dist = diff.magnitude;

                        if (ShowDebug && iter == 0)
                        {
                            Color c = dist < combinedRadius ? Color.red : Color.yellow;
                            DrawDebugSphere(blockPos, blockRadius, c);
                        }

                        if (dist < combinedRadius && dist > 0.0001f)
                        {
                            float penetration = combinedRadius - dist + SkinWidth;
                            Vector3 pushDir = diff / dist;

                            totalCollisions++;

                            if (penetration > deepestPenetration)
                            {
                                deepestPenetration = penetration;
                                deepestPushDir = pushDir;
                            }

                            hitThisPass = true;

                            float groundDot = Vector3.Dot(pushDir, up);
                            if (groundDot > groundDotThreshold)
                                result.IsGrounded = true;
                        }
                    }
                }

                if (hitThisPass)
                {
                    if (deepestPenetration > deepest)
                        deepest = deepestPenetration;

                    result.Position += deepestPushDir * deepestPenetration;
                    result.HitSomething = true;
                    result.PushDirection += deepestPushDir;

                    if (ShowDebug)
                    {
                        UnityEngine.Debug.DrawRay(result.Position, deepestPushDir * deepestPenetration * 5f,
                            Color.magenta, 0f);
                    }
                }
                else
                {
                    break;
                }
            }

            if (result.HitSomething)
                result.PushDirection.Normalize();

            // Ground probe: check if there's a solid block near the player's feet
            // This is independent of collision penetration — solves the grounded flicker
            if (!result.IsGrounded)
            {
                Vector3 feetPos = result.Position - up * (Height * 0.5f);
                for (float d = 0; d <= GroundProbeDistance; d += 0.05f)
                {
                    Vector3 probePos = feetPos - up * d;
                    var probeAddr = Core.BlockAddress.FromWorldPosition(probePos, blockSize);
                    if (_chunkManager.IsSolid(probeAddr))
                    {
                        result.IsGrounded = true;
                        break;
                    }
                }
            }

            // Draw capsule in debug
            if (ShowDebug)
            {
                Vector3 top = result.Position + up * innerHalfHeight;
                Vector3 bottom = result.Position - up * innerHalfHeight;
                UnityEngine.Debug.DrawLine(top, bottom, result.IsGrounded ? Color.green : Color.cyan, 0f);
                DrawDebugSphere(top, Radius, result.IsGrounded ? Color.green : Color.cyan);
                DrawDebugSphere(bottom, Radius, result.IsGrounded ? Color.green : Color.cyan);
            }

            LastBlocksChecked = totalChecked;
            LastCollisionsFound = totalCollisions;
            LastDeepestPenetration = deepest;
            LastPushDir = result.HitSomething ? result.PushDirection : Vector3.zero;

            // Console logging — only when ShowDebug is on
            if (ShowDebug)
            {
                UnityEngine.Debug.Log(
                    $"[Collision] pos={result.Position:F2} checked={totalChecked} " +
                    $"collisions={totalCollisions} deepest={deepest:F3} " +
                    $"grounded={result.IsGrounded} push={LastPushDir:F2} " +
                    $"distFromOrigin={result.Position.magnitude:F2} " +
                    $"combinedR={combinedRadius:F3} blockR={blockRadius:F3}");
            }

            return result;
        }

        static void DrawDebugSphere(Vector3 center, float radius, Color color)
        {
            // Draw a simple wireframe cross to represent the sphere
            UnityEngine.Debug.DrawLine(center + Vector3.right * radius, center - Vector3.right * radius, color, 0f);
            UnityEngine.Debug.DrawLine(center + Vector3.up * radius, center - Vector3.up * radius, color, 0f);
            UnityEngine.Debug.DrawLine(center + Vector3.forward * radius, center - Vector3.forward * radius, color, 0f);
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
