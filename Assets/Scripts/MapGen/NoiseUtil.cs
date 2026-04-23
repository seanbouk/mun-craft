using UnityEngine;

namespace MunCraft.MapGen
{
    /// <summary>
    /// Shared 3D value noise utilities for map generators.
    /// Same algorithms as SphereGenerator (which keeps its own private copy
    /// so Round World is untouched).
    /// </summary>
    public static class NoiseUtil
    {
        public static float ValueNoise3D(float x, float y, float z)
        {
            int ix = Mathf.FloorToInt(x);
            int iy = Mathf.FloorToInt(y);
            int iz = Mathf.FloorToInt(z);
            float fx = x - ix; float fy = y - iy; float fz = z - iz;

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

        public static float FBM(float x, float y, float z, int octaves,
                                  float lacunarity = 2f, float persistence = 0.5f)
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

        public static float HashGrid(int x, int y, int z)
        {
            int h = x * 374761393 + y * 668265263 + z;
            h = (h ^ (h >> 13)) * 1274126177;
            h = h ^ (h >> 16);
            return (h & 0x7fffffff) / (float)0x7fffffff;
        }

        public static float QuickHash(Vector3 pos)
        {
            return Mathf.Abs(Mathf.Sin(
                pos.x * 12.9898f + pos.y * 78.233f + pos.z * 37.719f
            ) * 43758.5453f) % 1f;
        }
    }
}
