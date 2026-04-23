using MunCraft.Core;
using MunCraft.Debug;
using UnityEngine;

namespace MunCraft.MapGen
{
    /// <summary>
    /// Map 0: Round World. Wraps the existing SphereGenerator exactly —
    /// full terrain, biomes, caves, multiple materials.
    /// </summary>
    public static class RoundWorldGen
    {
        public static MapResult Generate(ChunkManager chunkManager, float blockSize, int radius)
        {
            var filled = SphereGenerator.Generate(chunkManager, radius, blockSize);

            float terrainDisp = SphereGenerator.TerrainHeight(
                Vector3.up, radius * blockSize, SphereGenerator.Settings.Default);
            float surfaceH = radius * blockSize + terrainDisp + 0.559f * blockSize;

            return new MapResult
            {
                FilledBlocks = filled,
                SpawnPosition = Vector3.up * (surfaceH + 1.4f),
                SpawnUp = Vector3.up,
            };
        }
    }
}
