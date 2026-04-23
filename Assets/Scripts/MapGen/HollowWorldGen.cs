using System.Collections.Generic;
using MunCraft.Core;
using UnityEngine;

namespace MunCraft.MapGen
{
    /// <summary>
    /// Map 3: Worlds World. Four small spheres positioned at the vertices
    /// of a regular tetrahedron. Close enough to jump between them —
    /// gravity shifts as you move between worlds.
    /// Single material (Rock), no terrain features.
    /// </summary>
    public static class WorldsWorldGen
    {
        public static MapResult Generate(ChunkManager chunkManager, float blockSize,
                                          float sphereRadius = 8f, float tetEdge = 22f)
        {
            var filled = new List<BlockAddress>();
            float rSqr = sphereRadius * sphereRadius;

            // Regular tetrahedron vertices, centered at origin.
            // Edge length = tetEdge. The vertices of a regular tetrahedron
            // with edge a, centered at origin:
            float a = tetEdge;
            Vector3[] centers =
            {
                new Vector3(1, 1, 1).normalized * (a * Mathf.Sqrt(3f / 8f)),
                new Vector3(1, -1, -1).normalized * (a * Mathf.Sqrt(3f / 8f)),
                new Vector3(-1, 1, -1).normalized * (a * Mathf.Sqrt(3f / 8f)),
                new Vector3(-1, -1, 1).normalized * (a * Mathf.Sqrt(3f / 8f)),
            };

            // Scan range must cover all 4 spheres
            float maxExtent = a * Mathf.Sqrt(3f / 8f) + sphereRadius;
            int scanRange = Mathf.CeilToInt(maxExtent / blockSize) + 2;

            for (int parity = 0; parity <= 1; parity++)
            for (int z = -scanRange; z <= scanRange; z++)
            for (int y = -scanRange; y <= scanRange; y++)
            for (int x = -scanRange; x <= scanRange; x++)
            {
                var address = new BlockAddress(parity, x, y, z);
                Vector3 pos = address.ToWorldPosition(blockSize);

                // Inside any of the 4 spheres
                bool inside = false;
                for (int s = 0; s < 4; s++)
                {
                    if ((pos - centers[s]).sqrMagnitude <= rSqr)
                    {
                        inside = true;
                        break;
                    }
                }

                if (inside)
                {
                    chunkManager.SetBlockSilent(address, BlockType.Rock);
                    filled.Add(address);
                }
            }

            chunkManager.MarkAllDirty();

            // Spawn on top of the first sphere (the one at (1,1,1) direction)
            Vector3 spawnCenter = centers[0];
            Vector3 spawnUp = spawnCenter.normalized;
            float spawnHeight = sphereRadius + 0.559f * blockSize + 1.4f;
            Vector3 spawnPos = spawnCenter + spawnUp * spawnHeight;

            return new MapResult
            {
                FilledBlocks = filled,
                SpawnPosition = spawnPos,
                SpawnUp = spawnUp,
            };
        }
    }
}
