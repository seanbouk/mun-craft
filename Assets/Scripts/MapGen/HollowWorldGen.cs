using System.Collections.Generic;
using MunCraft.Core;
using UnityEngine;

namespace MunCraft.MapGen
{
    /// <summary>
    /// Map 3: Hollow World. A large sphere with a hollow interior.
    /// The player starts inside. Wall is ~4 blocks thick.
    /// Single material (Rock), no terrain features.
    ///
    /// Note: the shell theorem means gravity inside a uniform hollow sphere
    /// is near-zero. With discrete blocks and Barnes-Hut approximation,
    /// the player will experience small noisy forces and drift toward
    /// the nearest wall. This is intentional — interesting low-gravity
    /// gameplay.
    /// </summary>
    public static class HollowWorldGen
    {
        public static MapResult Generate(ChunkManager chunkManager, float blockSize,
                                          float outerRadius = 20f, float wallThickness = 4f)
        {
            var filled = new List<BlockAddress>();
            float innerRadius = outerRadius - wallThickness;
            float outerSqr = outerRadius * outerRadius;
            float innerSqr = innerRadius * innerRadius;

            int scanRange = Mathf.CeilToInt(outerRadius / blockSize) + 2;

            for (int parity = 0; parity <= 1; parity++)
            for (int z = -scanRange; z <= scanRange; z++)
            for (int y = -scanRange; y <= scanRange; y++)
            for (int x = -scanRange; x <= scanRange; x++)
            {
                var address = new BlockAddress(parity, x, y, z);
                Vector3 pos = address.ToWorldPosition(blockSize);
                float distSqr = pos.sqrMagnitude;

                // Inside the wall (between inner and outer radius)
                if (distSqr >= innerSqr && distSqr <= outerSqr)
                {
                    chunkManager.SetBlockSilent(address, BlockType.Rock);
                    filled.Add(address);
                }
            }

            chunkManager.MarkAllDirty();

            // Spawn inside, near the inner wall (top), looking inward
            float spawnDist = innerRadius - 1.4f - 0.559f * blockSize;
            return new MapResult
            {
                FilledBlocks = filled,
                SpawnPosition = Vector3.up * spawnDist,
                // "Up" points away from the wall (toward center) so the player
                // faces inward. Gravity will be near-zero so this is mostly cosmetic.
                SpawnUp = -Vector3.up,
            };
        }
    }
}
