using System.Collections.Generic;
using MunCraft.Core;
using UnityEngine;

namespace MunCraft.MapGen
{
    /// <summary>
    /// Map 2: Peanut World. Two spheres connected by a smooth neck.
    /// ~25% as knobbly as Round World. No dirt. Grass is directional —
    /// mostly on the outside of one orb and near the neck on the other.
    /// Ice on the highest parts. No ores.
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

            // Terrain noise amplitude: ~25% of Round World
            // Round World uses ~0.18 * radius for hills ≈ 2.7 for r=15
            // We use ~0.045 * lobeRadius ≈ 0.675
            float hillAmp = 0.135f * lobeRadius;
            float detailAmp = 0.045f * lobeRadius;

            int scanRange = Mathf.CeilToInt((lobeRadius + lobeOffset + blendK + hillAmp + 2) / blockSize) + 2;

            for (int parity = 0; parity <= 1; parity++)
            for (int z = -scanRange; z <= scanRange; z++)
            for (int y = -scanRange; y <= scanRange; y++)
            for (int x = -scanRange; x <= scanRange; x++)
            {
                var address = new BlockAddress(parity, x, y, z);
                Vector3 pos = address.ToWorldPosition(blockSize);

                // Signed distance to each lobe
                float d1 = (pos - center1).magnitude - lobeRadius;
                float d2 = (pos - center2).magnitude - lobeRadius;

                // Smooth-min blend
                float combined = SmoothMin(d1, d2, blendK);

                // Terrain noise displacement (displaces the surface outward)
                Vector3 dir = pos.magnitude > 0.01f ? pos.normalized : Vector3.up;
                float hills = (NoiseUtil.FBM(dir.x * 2f, dir.y * 2f, dir.z * 2f, 3) * 2f - 1f) * hillAmp;
                float detail = (NoiseUtil.FBM(dir.x * 8f, dir.y * 8f, dir.z * 8f, 2) * 2f - 1f) * detailAmp;
                float displacement = hills + detail;

                float surfaceDist = combined - displacement;
                if (surfaceDist > 0) continue;

                // Depth from surface (0 = at surface, positive = deeper)
                float depth = -surfaceDist;

                // --- Material selection ---
                BlockType type = PickBlockType(pos, depth, displacement, center1, center2,
                                               lobeOffset, lobeRadius);

                chunkManager.SetBlockSilent(address, type);
                filled.Add(address);
            }

            chunkManager.MarkAllDirty();

            // Spawn on top of one lobe
            float spawnHeight = lobeRadius + hillAmp + 0.559f * blockSize + 1.4f;
            return new MapResult
            {
                FilledBlocks = filled,
                SpawnPosition = new Vector3(0, spawnHeight, -lobeOffset),
                SpawnUp = Vector3.up,
            };
        }

        static BlockType PickBlockType(Vector3 pos, float depth, float displacement,
                                        Vector3 center1, Vector3 center2,
                                        float lobeOffset, float lobeRadius)
        {
            // Only surface blocks get non-Rock materials (top ~1.5 blocks)
            if (depth > 1.5f)
                return BlockType.Rock;

            // Ice on the outer ends of the lobes (far from the neck)
            float distFromNeck = Mathf.Abs(pos.z);
            float lobeEnd = lobeOffset + lobeRadius * 0.6f;
            if (distFromNeck > lobeEnd)
                return BlockType.Ice;

            // Grass direction: biased toward -Z overall.
            // On lobe1 (z ~ -lobeOffset): grass on the outside (more negative z)
            // On lobe2 (z ~ +lobeOffset): grass near the neck (less positive z)
            // Result: grass covers the -Z hemisphere of lobe1 and the neck-side of lobe2

            // Normalize z position to a 0-1 "grass likelihood"
            float zMin = -lobeOffset - lobeRadius;
            float zMax = lobeOffset + lobeRadius;
            float grassGradient = Mathf.InverseLerp(zMax, zMin, pos.z); // 0 at +Z far side, 1 at -Z far side

            // Add some noise so the boundary isn't a hard line
            float grassNoise = NoiseUtil.FBM(pos.x * 2.5f + 300f, pos.y * 2.5f + 300f,
                                              pos.z * 2.5f + 300f, 2);

            float grassChance = grassGradient * 0.8f + grassNoise * 0.4f - 0.2f;

            if (grassChance > 0.4f)
                return BlockType.Plant;

            return BlockType.Rock;
        }

        static float SmoothMin(float a, float b, float k)
        {
            float h = Mathf.Max(k - Mathf.Abs(a - b), 0f) / k;
            return Mathf.Min(a, b) - h * h * k * 0.25f;
        }
    }
}
