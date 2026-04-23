using System.Collections.Generic;
using MunCraft.Core;
using UnityEngine;

namespace MunCraft.MapGen
{
    /// <summary>
    /// Map 2: Peanut World. Two overlapping spheres forming a peanut/
    /// figure-8 shape — like two planetoids that collided and merged.
    /// Single material (Rock), no terrain features.
    /// </summary>
    public static class PeanutWorldGen
    {
        public static MapResult Generate(ChunkManager chunkManager, float blockSize,
                                          float lobeRadius = 10f, float lobeOffset = 8f)
        {
            var filled = new List<BlockAddress>();
            float rSqr = lobeRadius * lobeRadius;
            Vector3 center1 = new Vector3(0, 0, -lobeOffset);
            Vector3 center2 = new Vector3(0, 0, lobeOffset);

            int scanRange = Mathf.CeilToInt((lobeRadius + lobeOffset) / blockSize) + 2;

            for (int parity = 0; parity <= 1; parity++)
            for (int z = -scanRange; z <= scanRange; z++)
            for (int y = -scanRange; y <= scanRange; y++)
            for (int x = -scanRange; x <= scanRange; x++)
            {
                var address = new BlockAddress(parity, x, y, z);
                Vector3 pos = address.ToWorldPosition(blockSize);

                // Inside either lobe
                bool inLobe1 = (pos - center1).sqrMagnitude <= rSqr;
                bool inLobe2 = (pos - center2).sqrMagnitude <= rSqr;

                if (inLobe1 || inLobe2)
                {
                    chunkManager.SetBlockSilent(address, BlockType.Rock);
                    filled.Add(address);
                }
            }

            chunkManager.MarkAllDirty();

            // Spawn on top of one lobe
            float spawnHeight = lobeRadius + 0.559f * blockSize + 1.4f;
            return new MapResult
            {
                FilledBlocks = filled,
                SpawnPosition = new Vector3(0, spawnHeight, -lobeOffset),
                SpawnUp = Vector3.up,
            };
        }
    }
}
