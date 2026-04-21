using System.Collections.Generic;
using MunCraft.Core;
using UnityEngine;

namespace MunCraft.Debug
{
    /// <summary>
    /// Generates a roughly spherical planet with terrain features:
    ///   - Hills, valleys, and cliffs via multi-octave 3D noise
    ///   - Worm-like caves with occasional surface entrances
    ///   - Biome regions (grass, dirt, sand, exposed stone) on the surface
    ///   - Thin grass only on top-facing surfaces
    ///   - Geological layers with ore veins at depth
    /// </summary>
    public static class SphereGenerator
    {
        [System.Serializable]
        public struct Settings
        {
            // Terrain shape
            public float HillAmplitude;   // fraction of radius for large features
            public float CliffAmplitude;  // fraction of radius for medium/cliff features
            public float DetailAmplitude; // fraction of radius for small surface detail

            // Caves
            public float CaveFrequency;
            public float CaveWidth;       // threshold — smaller = thinner caves
            public float CaveMinDepth;    // blocks from surface before caves appear freely
            public float CaveCoreRadius;  // fraction of radius below which no caves

            public static Settings Default => new Settings
            {
                HillAmplitude = 0.18f,
                CliffAmplitude = 0.09f,
                DetailAmplitude = 0.03f,
                CaveFrequency = 0.14f,
                CaveWidth = 0.14f,
                CaveMinDepth = 3f,
                CaveCoreRadius = 0.2f,
            };
        }

        public static List<BlockAddress> Generate(ChunkManager chunkManager, int radius, float blockSize)
        {
            return Generate(chunkManager, radius, blockSize, Settings.Default);
        }

        public static List<BlockAddress> Generate(ChunkManager chunkManager, int radius,
                                                   float blockSize, Settings s)
        {
            var filled = new List<BlockAddress>();
            float radiusWorld = radius * blockSize;

            // Scan range must cover the maximum terrain displacement
            float maxDisplacement = (s.HillAmplitude + s.CliffAmplitude + s.DetailAmplitude) * radiusWorld;
            int scanRange = Mathf.CeilToInt((radiusWorld + maxDisplacement) / blockSize) + 2;

            for (int parity = 0; parity <= 1; parity++)
            for (int z = -scanRange; z <= scanRange; z++)
            for (int y = -scanRange; y <= scanRange; y++)
            for (int x = -scanRange; x <= scanRange; x++)
            {
                var address = new BlockAddress(parity, x, y, z);
                Vector3 worldPos = address.ToWorldPosition(blockSize);
                float dist = worldPos.magnitude;

                // Quick reject: way outside possible terrain
                if (dist > radiusWorld + maxDisplacement + blockSize) continue;

                Vector3 dir = dist > 0.01f ? worldPos / dist : Vector3.up;

                // Surface height at this direction
                float surfaceHeight = radiusWorld + TerrainHeight(dir, radiusWorld, s);

                // Above terrain → air
                if (dist > surfaceHeight) continue;

                float depth = surfaceHeight - dist; // 0 at surface, large toward center

                // Cave carving
                if (IsCave(worldPos, depth, radiusWorld, s)) continue;

                // Determine block type
                bool isTopFacing = IsTopFacing(worldPos, dir, dist, radiusWorld, blockSize, s);
                float steepness = SurfaceSteepness(worldPos, dir, radiusWorld, blockSize, s);
                float biome = BiomeNoise(dir);
                float terrainDisp = surfaceHeight - radiusWorld; // positive = hill, negative = valley
                BlockType type = PickBlockType(worldPos, depth, isTopFacing, steepness,
                                               biome, radiusWorld, terrainDisp);

                chunkManager.SetBlockSilent(address, type);
                filled.Add(address);
            }

            chunkManager.MarkAllDirty();
            return filled;
        }

        // ---------------------------------------------------------------
        //  Terrain height
        // ---------------------------------------------------------------

        /// <summary>
        /// Height displacement above (or below) the base radius at a given direction.
        /// Public so spawn logic can query the actual surface height.
        /// </summary>
        public static float TerrainHeight(Vector3 dir, float radius, Settings s)
        {
            float x = dir.x, y = dir.y, z = dir.z;

            // Large rolling hills/valleys
            float large = FBM(x * 2f, y * 2f, z * 2f, 4, 2.0f, 0.5f) * 2f - 1f;

            // Medium features — cliffs and ridges
            // Abs + sqrt sharpens the noise into ridge-like features
            float med = FBM(x * 5f, y * 5f, z * 5f, 3, 2.0f, 0.5f) * 2f - 1f;
            float ridge = Mathf.Sqrt(Mathf.Abs(med)) * Mathf.Sign(med);

            // Small surface detail
            float small = FBM(x * 12f, y * 12f, z * 12f, 2, 2.0f, 0.5f) * 2f - 1f;

            return large * s.HillAmplitude * radius
                 + ridge * s.CliffAmplitude * radius
                 + small * s.DetailAmplitude * radius;
        }

        // ---------------------------------------------------------------
        //  Caves (worm-style: intersection of two noise channels)
        // ---------------------------------------------------------------

        static bool IsCave(Vector3 worldPos, float depth, float radius, Settings s)
        {
            // No caves in the very center (keep a solid core)
            float dist = worldPos.magnitude;
            if (dist < radius * s.CaveCoreRadius) return false;

            float freq = s.CaveFrequency;
            float n1 = FBM(worldPos.x * freq + 100f, worldPos.y * freq,
                           worldPos.z * freq, 2, 2f, 0.5f) * 2f - 1f;
            float n2 = FBM(worldPos.x * freq, worldPos.y * freq + 100f,
                           worldPos.z * freq + 100f, 2, 2f, 0.5f) * 2f - 1f;

            float width = s.CaveWidth;

            // Near the surface: narrow the threshold so cave openings are rare
            if (depth < s.CaveMinDepth)
            {
                float t = depth / s.CaveMinDepth; // 0 at surface, 1 at minDepth
                width *= Mathf.Lerp(0.25f, 1f, t);
            }

            return Mathf.Abs(n1) < width && Mathf.Abs(n2) < width;
        }

        // ---------------------------------------------------------------
        //  "Top-facing" check — is the block above (radially) air?
        // ---------------------------------------------------------------

        static bool IsTopFacing(Vector3 worldPos, Vector3 dir, float dist,
                                 float radius, float blockSize, Settings s)
        {
            // Check if a position one block further from center would be above terrain
            float aboveDist = dist + blockSize;
            Vector3 aboveDir = (worldPos + dir * blockSize).normalized;
            float aboveSurface = radius + TerrainHeight(aboveDir, radius, s);
            return aboveDist > aboveSurface;
        }

        // ---------------------------------------------------------------
        //  Surface steepness — 0 = flat (top-facing), 1 = vertical cliff
        //  Estimated by checking terrain height at nearby directions
        // ---------------------------------------------------------------

        static float SurfaceSteepness(Vector3 worldPos, Vector3 dir, float radius,
                                       float blockSize, Settings s)
        {
            // Sample terrain height at 4 nearby directions to estimate slope
            float h0 = TerrainHeight(dir, radius, s);
            float step = blockSize / radius; // angular step on the unit sphere

            // Perturb direction in two orthogonal directions
            Vector3 tangent = Vector3.Cross(dir, Vector3.up);
            if (tangent.sqrMagnitude < 0.01f) tangent = Vector3.Cross(dir, Vector3.right);
            tangent.Normalize();
            Vector3 bitangent = Vector3.Cross(dir, tangent).normalized;

            float h1 = TerrainHeight((dir + tangent * step).normalized, radius, s);
            float h2 = TerrainHeight((dir - tangent * step).normalized, radius, s);
            float h3 = TerrainHeight((dir + bitangent * step).normalized, radius, s);
            float h4 = TerrainHeight((dir - bitangent * step).normalized, radius, s);

            float slope = Mathf.Max(Mathf.Abs(h1 - h2), Mathf.Abs(h3 - h4)) / (2f * blockSize);
            return Mathf.Clamp01(slope); // 0 = flat, 1+ = cliff
        }

        // ---------------------------------------------------------------
        //  Biome noise (low-frequency, sampled on the sphere surface)
        // ---------------------------------------------------------------

        static float BiomeNoise(Vector3 dir)
        {
            return FBM(dir.x * 1.8f + 50f, dir.y * 1.8f + 50f, dir.z * 1.8f + 50f,
                       2, 2f, 0.5f);
        }

        // ---------------------------------------------------------------
        //  Block type assignment
        // ---------------------------------------------------------------

        static BlockType PickBlockType(Vector3 worldPos, float depth,
                                        bool isTopFacing, float steepness,
                                        float biome, float radius, float terrainDisp)
        {
            float n = QuickHash(worldPos);

            // --- Surface (top-facing, thin layer) ---
            if (isTopFacing && depth < 1.5f)
            {
                // Ice on mountain tops (high terrain displacement)
                float peakThreshold = radius * 0.10f; // top 10% of height range
                if (terrainDisp > peakThreshold)
                    return BlockType.Ice;

                // Rare copper nuggets on surface near dirt/rock boundaries
                if (n > 0.97f && steepness > 0.15f && steepness < 0.6f)
                    return BlockType.Copper;

                // Flat → plant, steep → rock, in between → dirt (with randomness)
                float plantChance = Mathf.Clamp01(1f - steepness * 2.5f); // 1 at flat, 0 at steep
                float rockChance = Mathf.Clamp01(steepness * 2f - 0.4f);  // 0 at flat, 1 at steep
                // remainder is dirt

                float roll = QuickHash(worldPos * 1.7f);
                if (roll < plantChance * 0.8f) return BlockType.Plant;
                if (roll > 1f - rockChance * 0.7f) return BlockType.Rock;
                return BlockType.Dirt;
            }

            // --- Near surface (cliff face / just below surface) ---
            if (depth < 4f)
            {
                // Ice near peaks even on cliff faces
                if (terrainDisp > radius * 0.10f && n > 0.5f)
                    return BlockType.Ice;

                // Steep = mostly rock, moderate = mix, gentle = dirt
                if (steepness > 0.5f) return BlockType.Rock;
                if (steepness > 0.25f)
                    return n > 0.4f ? BlockType.Rock : BlockType.Dirt;
                return BlockType.Dirt;
            }

            // --- Mid depth (mostly rock, occasional dirt + ores) ---
            if (depth < 10f)
            {
                if (n > 0.93f) return BlockType.Hematite;
                if (n > 0.85f) return BlockType.Dirt;
                return BlockType.Rock;
            }

            // --- Deep (rock + ore veins) ---
            {
                float deepN = QuickHash(worldPos * 3f);
                if (deepN > 0.97f) return BlockType.Copper;
                if (deepN > 0.93f) return BlockType.Hematite;
                if (deepN > 0.88f && depth > 15f) return BlockType.Cassiterite;
                return BlockType.Rock;
            }
        }

        // ---------------------------------------------------------------
        //  Noise utilities
        // ---------------------------------------------------------------

        /// <summary>
        /// Smooth 3D value noise (trilinear interpolation of hashed grid values).
        /// </summary>
        static float ValueNoise3D(float x, float y, float z)
        {
            int ix = Mathf.FloorToInt(x);
            int iy = Mathf.FloorToInt(y);
            int iz = Mathf.FloorToInt(z);
            float fx = x - ix; float fy = y - iy; float fz = z - iz;

            // Smooth hermite interpolation
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            fz = fz * fz * (3f - 2f * fz);

            float c000 = HashGrid(ix, iy, iz);
            float c100 = HashGrid(ix + 1, iy, iz);
            float c010 = HashGrid(ix, iy + 1, iz);
            float c110 = HashGrid(ix + 1, iy + 1, iz);
            float c001 = HashGrid(ix, iy, iz + 1);
            float c101 = HashGrid(ix + 1, iy, iz + 1);
            float c011 = HashGrid(ix, iy + 1, iz + 1);
            float c111 = HashGrid(ix + 1, iy + 1, iz + 1);

            float x00 = Mathf.Lerp(c000, c100, fx);
            float x10 = Mathf.Lerp(c010, c110, fx);
            float x01 = Mathf.Lerp(c001, c101, fx);
            float x11 = Mathf.Lerp(c011, c111, fx);
            float xy0 = Mathf.Lerp(x00, x10, fy);
            float xy1 = Mathf.Lerp(x01, x11, fy);
            return Mathf.Lerp(xy0, xy1, fz);
        }

        /// <summary>
        /// Fractal Brownian Motion — stacked octaves of value noise.
        /// Returns 0..1.
        /// </summary>
        static float FBM(float x, float y, float z, int octaves, float lacunarity, float persistence)
        {
            float value = 0f, amplitude = 1f, maxAmp = 0f;
            for (int i = 0; i < octaves; i++)
            {
                value += amplitude * ValueNoise3D(x, y, z);
                maxAmp += amplitude;
                amplitude *= persistence;
                x *= lacunarity; y *= lacunarity; z *= lacunarity;
            }
            return value / maxAmp;
        }

        /// <summary>
        /// Integer hash → float in [0, 1). Used for grid-point values.
        /// </summary>
        static float HashGrid(int x, int y, int z)
        {
            int h = x * 374761393 + y * 668265263 + z;
            h = (h ^ (h >> 13)) * 1274126177;
            h = h ^ (h >> 16);
            return (h & 0x7fffffff) / (float)0x7fffffff;
        }

        /// <summary>
        /// Quick per-position hash for ore/type variation. Returns 0..1.
        /// </summary>
        static float QuickHash(Vector3 pos)
        {
            return Mathf.Abs(Mathf.Sin(
                pos.x * 12.9898f + pos.y * 78.233f + pos.z * 37.719f
            ) * 43758.5453f) % 1f;
        }
    }
}
