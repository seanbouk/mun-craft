using System.Collections.Generic;
using MunCraft.Core;
using UnityEngine;

namespace MunCraft.Debug
{
    /// <summary>
    /// Generates a solid sphere of blocks on the BCC lattice.
    /// Fills all lattice positions within the given radius from the center.
    /// </summary>
    public static class SphereGenerator
    {
        /// <summary>
        /// Generate a sphere of blocks centered at the lattice origin.
        /// Returns the list of addresses that were filled (useful for gravity tree building).
        /// </summary>
        public static List<BlockAddress> Generate(ChunkManager chunkManager, int radius, float blockSize)
        {
            var filled = new List<BlockAddress>();
            float radiusWorld = radius * blockSize;
            float radiusSqr = radiusWorld * radiusWorld;
            Vector3 center = Vector3.zero;

            // Scan both grids within a bounding box
            int scanRange = radius + 1;

            for (int parity = 0; parity <= 1; parity++)
            {
                for (int z = -scanRange; z <= scanRange; z++)
                for (int y = -scanRange; y <= scanRange; y++)
                for (int x = -scanRange; x <= scanRange; x++)
                {
                    var address = new BlockAddress(parity, x, y, z);
                    Vector3 worldPos = address.ToWorldPosition(blockSize);

                    if ((worldPos - center).sqrMagnitude <= radiusSqr)
                    {
                        BlockType type = PickBlockType(worldPos, radiusWorld);
                        chunkManager.SetBlockSilent(address, type);
                        filled.Add(address);
                    }
                }
            }

            chunkManager.MarkAllDirty();
            return filled;
        }

        /// <summary>
        /// Pick a block type based on depth from surface (like geological layers).
        /// </summary>
        static BlockType PickBlockType(Vector3 worldPos, float radius)
        {
            float distFromCenter = worldPos.magnitude;
            float depth = radius - distFromCenter; // 0 at surface, radius at center
            float normalizedDepth = depth / radius;

            // Surface: grass
            if (normalizedDepth < 0.05f)
                return BlockType.Grass;

            // Shallow: dirt
            if (normalizedDepth < 0.15f)
                return BlockType.Dirt;

            // Some sand pockets (using noise-like pattern from position)
            float noise = Mathf.Abs(Mathf.Sin(worldPos.x * 3.7f + worldPos.y * 5.3f + worldPos.z * 7.1f));
            if (normalizedDepth < 0.25f && noise > 0.8f)
                return BlockType.Sand;

            // Deep: mostly stone with ore veins
            if (noise > 0.95f)
                return BlockType.Gold;
            if (noise > 0.85f)
                return BlockType.Iron;
            if (noise > 0.75f && normalizedDepth > 0.5f)
                return BlockType.Crystal;

            return BlockType.Stone;
        }
    }
}
