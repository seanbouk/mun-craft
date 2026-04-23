using System.Collections.Generic;
using MunCraft.Core;
using UnityEngine;

namespace MunCraft.MapGen
{
    /// <summary>
    /// Map 1: Donut World. A torus (ring) shape.
    /// Single material (Rock), no terrain features.
    /// </summary>
    public static class DonutWorldGen
    {
        public static MapResult Generate(ChunkManager chunkManager, float blockSize,
                                          float majorRadius = 12f, float tubeRadius = 5f)
        {
            var filled = new List<BlockAddress>();
            float tubeSqr = tubeRadius * tubeRadius;
            int scanRange = Mathf.CeilToInt((majorRadius + tubeRadius) / blockSize) + 2;

            for (int parity = 0; parity <= 1; parity++)
            for (int z = -scanRange; z <= scanRange; z++)
            for (int y = -scanRange; y <= scanRange; y++)
            for (int x = -scanRange; x <= scanRange; x++)
            {
                var address = new BlockAddress(parity, x, y, z);
                Vector3 pos = address.ToWorldPosition(blockSize);

                // Torus equation: (sqrt(x² + z²) - R)² + y² <= r²
                float distXZ = Mathf.Sqrt(pos.x * pos.x + pos.z * pos.z);
                float d = (distXZ - majorRadius);
                float torusDist = d * d + pos.y * pos.y;

                if (torusDist <= tubeSqr)
                {
                    chunkManager.SetBlockSilent(address, BlockType.Rock);
                    filled.Add(address);
                }
            }

            chunkManager.MarkAllDirty();

            // Spawn on top of the tube at (R, tubeR, 0)
            float spawnHeight = tubeRadius + 0.559f * blockSize + 1.4f;
            return new MapResult
            {
                FilledBlocks = filled,
                SpawnPosition = new Vector3(majorRadius, spawnHeight, 0),
                SpawnUp = Vector3.up,
            };
        }
    }
}
