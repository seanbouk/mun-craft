using System.Collections.Generic;
using MunCraft.Core;
using UnityEngine;

namespace MunCraft.MapGen
{
    /// <summary>
    /// Map 2: Peanut World. Two spheres connected by a smooth neck.
    /// Knobbly terrain. Dirt surface with directional bias, ice caps
    /// on the outer tips of both lobes. No ores.
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

                float d1 = (pos - center1).magnitude - lobeRadius;
                float d2 = (pos - center2).magnitude - lobeRadius;

                float combined = SmoothMin(d1, d2, blendK);

                Vector3 dir = pos.magnitude > 0.01f ? pos.normalized : Vector3.up;
                float hills = (NoiseUtil.FBM(dir.x * 2f, dir.y * 2f, dir.z * 2f, 3) * 2f - 1f) * hillAmp;
                float detail = (NoiseUtil.FBM(dir.x * 8f, dir.y * 8f, dir.z * 8f, 2) * 2f - 1f) * detailAmp;
                float displacement = hills + detail;

                float surfaceDist = combined - displacement;
                if (surfaceDist > 0) continue;

                float depth = -surfaceDist;

                BlockType type = PickBlockType(pos, depth, displacement, center1, center2,
                                               lobeOffset, lobeRadius);

                chunkManager.SetBlockSilent(address, type);
                filled.Add(address);
            }

            chunkManager.MarkAllDirty();

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
            if (depth > 1.5f)
                return BlockType.Rock;

            // Ice on the outer ends of the lobes
            float distFromNeck = Mathf.Abs(pos.z);
            float lobeEnd = lobeOffset + lobeRadius * 0.6f;
            if (distFromNeck > lobeEnd)
                return BlockType.Ice;

            // Dirt surface with directional bias (more dirt toward -Z, some rock on +Z)
            float zMin = -lobeOffset - lobeRadius;
            float zMax = lobeOffset + lobeRadius;
            float dirtGradient = Mathf.InverseLerp(zMax, zMin, pos.z);

            float dirtNoise = NoiseUtil.FBM(pos.x * 2.5f + 300f, pos.y * 2.5f + 300f,
                                             pos.z * 2.5f + 300f, 2);

            float dirtChance = dirtGradient * 0.8f + dirtNoise * 0.4f - 0.2f;

            if (dirtChance > 0.4f)
                return BlockType.Dirt;

            return BlockType.Rock;
        }

        static float SmoothMin(float a, float b, float k)
        {
            float h = Mathf.Max(k - Mathf.Abs(a - b), 0f) / k;
            return Mathf.Min(a, b) - h * h * k * 0.25f;
        }
    }
}
