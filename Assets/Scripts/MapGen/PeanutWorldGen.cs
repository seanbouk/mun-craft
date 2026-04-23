using System.Collections.Generic;
using MunCraft.Core;
using UnityEngine;

namespace MunCraft.MapGen
{
    /// <summary>
    /// Map 2: Peanut World. Two spheres connected by a smooth neck,
    /// like a peanut or dividing cell. Uses a smooth-minimum blend
    /// of two signed distance fields to create the bridge.
    /// Single material (Rock), no terrain features.
    /// </summary>
    public static class PeanutWorldGen
    {
        public static MapResult Generate(ChunkManager chunkManager, float blockSize,
                                          float lobeRadius = 15f, float lobeOffset = 18f,
                                          float blendK = 18.2f)
        {
            var filled = new List<BlockAddress>();
            Vector3 center1 = new Vector3(0, 0, -lobeOffset);
            Vector3 center2 = new Vector3(0, 0, lobeOffset);

            int scanRange = Mathf.CeilToInt((lobeRadius + lobeOffset + blendK) / blockSize) + 2;

            for (int parity = 0; parity <= 1; parity++)
            for (int z = -scanRange; z <= scanRange; z++)
            for (int y = -scanRange; y <= scanRange; y++)
            for (int x = -scanRange; x <= scanRange; x++)
            {
                var address = new BlockAddress(parity, x, y, z);
                Vector3 pos = address.ToWorldPosition(blockSize);

                // Signed distance to each lobe (negative = inside)
                float d1 = (pos - center1).magnitude - lobeRadius;
                float d2 = (pos - center2).magnitude - lobeRadius;

                // Polynomial smooth-min: blends the two SDFs with a smooth
                // bridge. k controls neck thickness — larger = thicker bridge.
                float combined = SmoothMin(d1, d2, blendK);

                if (combined <= 0)
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

        /// <summary>
        /// Polynomial smooth-min. Blends two values with a smooth transition
        /// of width k. Returns a value smaller than min(a,b) in the blend zone,
        /// which "inflates" the connection between two shapes.
        /// </summary>
        static float SmoothMin(float a, float b, float k)
        {
            float h = Mathf.Max(k - Mathf.Abs(a - b), 0f) / k;
            return Mathf.Min(a, b) - h * h * k * 0.25f;
        }
    }
}
