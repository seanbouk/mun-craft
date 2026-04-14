using UnityEngine;

namespace MunCraft.Core
{
    public enum BlockType : byte
    {
        Air = 0,
        Stone = 1,
        Dirt = 2,
        Grass = 3,
        Sand = 4,
        Iron = 5,
        Gold = 6,
        Crystal = 7
    }

    public static class BlockTypeExtensions
    {
        // Three shades per block type: base, lighter, darker
        static readonly Color[][] ColorVariants =
        {
            // Air
            new[] { new Color(0, 0, 0, 0), new Color(0, 0, 0, 0), new Color(0, 0, 0, 0) },
            // Stone
            new[] { new Color(0.50f, 0.50f, 0.50f), new Color(0.58f, 0.58f, 0.58f), new Color(0.42f, 0.43f, 0.44f) },
            // Dirt
            new[] { new Color(0.54f, 0.36f, 0.22f), new Color(0.62f, 0.42f, 0.27f), new Color(0.45f, 0.30f, 0.18f) },
            // Grass
            new[] { new Color(0.30f, 0.65f, 0.25f), new Color(0.36f, 0.72f, 0.30f), new Color(0.24f, 0.55f, 0.20f) },
            // Sand
            new[] { new Color(0.85f, 0.78f, 0.55f), new Color(0.90f, 0.84f, 0.62f), new Color(0.78f, 0.72f, 0.48f) },
            // Iron
            new[] { new Color(0.70f, 0.45f, 0.35f), new Color(0.78f, 0.52f, 0.40f), new Color(0.60f, 0.38f, 0.28f) },
            // Gold
            new[] { new Color(0.90f, 0.75f, 0.20f), new Color(0.95f, 0.82f, 0.28f), new Color(0.82f, 0.67f, 0.14f) },
            // Crystal
            new[] { new Color(0.55f, 0.30f, 0.85f), new Color(0.64f, 0.38f, 0.92f), new Color(0.46f, 0.22f, 0.75f) },
        };

        /// <summary>
        /// Get a colour for this block type, with one of three random shades
        /// determined by the block's address (so the same block always gets the same shade).
        /// </summary>
        public static Color GetColor(this BlockType type, BlockAddress address)
        {
            int index = (int)type;
            if (index >= ColorVariants.Length) return Color.magenta;

            // Simple hash from block address to pick shade 0, 1, or 2
            int hash = address.X * 73856093 ^ address.Y * 19349669 ^ address.Z * 83492791 ^ address.Parity * 39916801;
            int shade = ((hash & 0x7FFFFFFF) % 3);
            return ColorVariants[index][shade];
        }

        /// <summary>
        /// Get the base colour (no variant). Used for highlights etc.
        /// </summary>
        public static Color GetColor(this BlockType type)
        {
            int index = (int)type;
            if (index >= ColorVariants.Length) return Color.magenta;
            return ColorVariants[index][0];
        }

        public static bool IsSolid(this BlockType type)
        {
            return type != BlockType.Air;
        }
    }
}
