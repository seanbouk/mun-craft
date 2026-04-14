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
            new Color(0, 0, 0, 0),           // Air (transparent, never rendered)
            new Color(0.55f, 0.55f, 0.55f),   // Stone - medium gray
            new Color(0.54f, 0.36f, 0.22f),   // Dirt - brown
            new Color(0.30f, 0.65f, 0.25f),   // Grass - green
            new Color(0.85f, 0.78f, 0.55f),   // Sand - tan
            new Color(0.70f, 0.45f, 0.35f),   // Iron - rusty red-brown
            new Color(0.90f, 0.75f, 0.20f),   // Gold - bright yellow
            new Color(0.55f, 0.30f, 0.85f),   // Crystal - purple
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
    }
}
