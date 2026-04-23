using System.Collections.Generic;
using MunCraft.Core;
using UnityEngine;

namespace MunCraft.MapGen
{
    /// <summary>
    /// Map 3: Worlds World. Four small spheres at tetrahedron vertices.
    /// Each world is a different surface material with a metal core
    /// (half by volume = ~79% of radius).
    ///
    ///   World 0 (top-front):  Plant surface, Hematite core
    ///   World 1 (top-back):   Dirt surface, Copper core
    ///   World 2 (bottom-left): Ice surface, Ice core (solid ice)
    ///   World 3 (bottom-right): Rock surface, Cassiterite core
    /// </summary>
    public static class WorldsWorldGen
    {
        static readonly BlockType[] SurfaceMaterials =
            { BlockType.Plant, BlockType.Dirt, BlockType.Ice, BlockType.Rock };
        static readonly BlockType[] CoreMaterials =
            { BlockType.Hematite, BlockType.Copper, BlockType.Ice, BlockType.Cassiterite };

        public static MapResult Generate(ChunkManager chunkManager, float blockSize,
                                          float sphereRadius = 8f, float tetEdge = 22f)
        {
            var filled = new List<BlockAddress>();
            float rSqr = sphereRadius * sphereRadius;

            // Core radius: half by volume → r_core = r * (0.5)^(1/3) ≈ r * 0.794
            float coreRadius = sphereRadius * 0.794f;
            float coreSqr = coreRadius * coreRadius;

            // Regular tetrahedron vertices, centered at origin
            float a = tetEdge;
            float s = a * Mathf.Sqrt(3f / 8f);
            Vector3[] centers =
            {
                new Vector3( 1,  1,  1).normalized * s,
                new Vector3( 1, -1, -1).normalized * s,
                new Vector3(-1,  1, -1).normalized * s,
                new Vector3(-1, -1,  1).normalized * s,
            };

            float maxExtent = s + sphereRadius;
            int scanRange = Mathf.CeilToInt(maxExtent / blockSize) + 2;

            for (int parity = 0; parity <= 1; parity++)
            for (int z = -scanRange; z <= scanRange; z++)
            for (int y = -scanRange; y <= scanRange; y++)
            for (int x = -scanRange; x <= scanRange; x++)
            {
                var address = new BlockAddress(parity, x, y, z);
                Vector3 pos = address.ToWorldPosition(blockSize);

                // Check each sphere
                for (int w = 0; w < 4; w++)
                {
                    float distSqr = (pos - centers[w]).sqrMagnitude;
                    if (distSqr > rSqr) continue;

                    // Ice world (w=2) is hollow — only the shell, no core
                    if (w == 2 && distSqr < coreSqr)
                        break;

                    // Core or surface?
                    BlockType type = distSqr <= coreSqr
                        ? CoreMaterials[w]
                        : SurfaceMaterials[w];

                    chunkManager.SetBlockSilent(address, type);
                    filled.Add(address);
                    break;
                }
            }

            chunkManager.MarkAllDirty();

            // Spawn on top of the first sphere
            Vector3 spawnCenter = centers[0];
            Vector3 spawnUp = spawnCenter.normalized;
            float spawnHeight = sphereRadius + 0.559f * blockSize + 1.4f;

            return new MapResult
            {
                FilledBlocks = filled,
                SpawnPosition = spawnCenter + spawnUp * spawnHeight,
                SpawnUp = spawnUp,
            };
        }
    }
}
