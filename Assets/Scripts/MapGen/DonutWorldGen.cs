using System.Collections.Generic;
using MunCraft.Core;
using UnityEngine;

namespace MunCraft.MapGen
{
    /// <summary>
    /// Map 1: Donut World. A torus (ring) shape.
    /// Mostly dirt. Copper "icing" on top dripping down the edges.
    /// Grass sprinkles on the copper. Pink sky.
    /// </summary>
    public static class DonutWorldGen
    {
        public static MapResult Generate(ChunkManager chunkManager, float blockSize,
                                          float majorRadius = 14.4f, float tubeRadius = 7f)
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

                // Torus SDF: distance from the tube surface
                float distXZ = Mathf.Sqrt(pos.x * pos.x + pos.z * pos.z);
                float dx = distXZ - majorRadius;
                float torusDistSqr = dx * dx + pos.y * pos.y;

                if (torusDistSqr > tubeSqr) continue;

                float torusDist = Mathf.Sqrt(torusDistSqr);

                // Tube angle: PI/2 = top, -PI/2 = bottom, 0 = outer edge
                float tubeAngle = Mathf.Atan2(pos.y, dx);
                float topness = Mathf.Sin(tubeAngle);

                // Icing extends the torus surface outward on top — blocks that are
                // OUTSIDE the base torus but within the icing shell are added as copper.
                float depth = tubeRadius - torusDist; // negative = outside base torus

                float icingBase = 0.3f;
                float drip = NoiseUtil.FBM(pos.x * 0.8f + 50f, pos.y * 0.8f + 50f,
                                            pos.z * 0.8f + 50f, 2) * 0.5f;
                float icingThreshold = icingBase - drip;
                bool inIcingZone = topness > icingThreshold;

                // Icing shell: up to 1.5 blocks above the base surface
                float icingHeight = 1.5f;
                bool inIcingShell = inIcingZone && depth > -icingHeight && depth < 1.5f;

                if (depth < 0 && !inIcingShell) continue; // outside both base and icing

                BlockType type;
                if (inIcingShell && depth <= 0)
                {
                    // Built-up icing layer (above the base torus surface)
                    float sprinkle = NoiseUtil.QuickHash(pos * 2.3f);
                    type = sprinkle > 0.94f ? BlockType.Plant : BlockType.Copper;
                }
                else if (inIcingZone && depth < 1.0f && depth >= 0)
                {
                    // Painted icing on the base surface (thin coat)
                    float sprinkle = NoiseUtil.QuickHash(pos * 2.3f);
                    type = sprinkle > 0.94f ? BlockType.Plant : BlockType.Copper;
                }
                else
                {
                    type = BlockType.Dirt;
                }

                chunkManager.SetBlockSilent(address, type);
                filled.Add(address);
            }

            chunkManager.MarkAllDirty();

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
