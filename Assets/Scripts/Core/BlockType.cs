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
        static readonly Color[] Colors =
        {
            new Color(0, 0, 0, 0),           // Air
            new Color(0.50f, 0.50f, 0.50f),   // Stone
            new Color(0.54f, 0.36f, 0.22f),   // Dirt
            new Color(0.30f, 0.65f, 0.25f),   // Grass
            new Color(0.85f, 0.78f, 0.55f),   // Sand
            new Color(0.70f, 0.45f, 0.35f),   // Iron
            new Color(0.90f, 0.75f, 0.20f),   // Gold
            new Color(0.55f, 0.30f, 0.85f),   // Crystal
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
                case BlockType.Grass:   return 0.4f;
                case BlockType.Dirt:    return 0.6f;
                case BlockType.Sand:    return 0.5f;
                case BlockType.Stone:   return 1.6f;
                case BlockType.Iron:    return 2.5f;
                case BlockType.Gold:    return 3.0f;
                case BlockType.Crystal: return 4.0f;
                default:                return 0f;
            }
        }
    }
}
