using UnityEngine;

namespace MunCraft.Core
{
    public enum BlockType : byte
    {
        Air = 0,
        Rock = 1,
        Dirt = 2,
        Plant = 3,
        Ice = 4,
        Hematite = 5,
        Copper = 6,
        Cassiterite = 7
    }

    public static class BlockTypeExtensions
    {
        static readonly Color[] Colors =
        {
            new Color(0, 0, 0, 0),           // Air
            new Color(0.50f, 0.50f, 0.50f),   // Rock — grey
            new Color(0.54f, 0.36f, 0.22f),   // Dirt — brown
            new Color(0.30f, 0.65f, 0.25f),   // Plant — green
            new Color(0.78f, 0.88f, 0.95f),   // Ice — pale blue-white
            new Color(0.70f, 0.45f, 0.35f),   // Hematite — rusty red-brown
            new Color(0.80f, 0.55f, 0.25f),   // Copper — orange-copper
            new Color(0.45f, 0.40f, 0.38f),   // Cassiterite — dark grey-brown
        };

        public static Color GetColor(this BlockType type)
        {
            int index = (int)type;
            return index < Colors.Length ? Colors[index] : Color.magenta;
        }

        public static bool IsSolid(this BlockType type)
        {
            return type != BlockType.Air;
        }

        public static float GetMiningTime(this BlockType type)
        {
            switch (type)
            {
                case BlockType.Plant:       return 0.4f;
                case BlockType.Dirt:        return 0.6f;
                case BlockType.Ice:         return 0.5f;
                case BlockType.Rock:        return 1.6f;
                case BlockType.Copper:      return 2.0f;
                case BlockType.Hematite:    return 2.5f;
                case BlockType.Cassiterite: return 3.0f;
                default:                    return 0f;
            }
        }
    }
}
