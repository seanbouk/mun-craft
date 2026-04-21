using MunCraft.Core;
using UnityEngine;

namespace MunCraft.Crafting
{
    /// <summary>
    /// Every item that can exist in the crafting inventory.
    /// Raw materials map 1:1 to BlockType. Crafted materials, tools,
    /// and machine parts are additional entries.
    /// </summary>
    public enum CraftingItem
    {
        // --- Raw materials (mined from the world) ---
        Plant = 0,
        Dirt,
        Rock,
        Ice,
        Copper,
        Hematite,
        Cassiterite,

        // --- Crafted materials ---
        Clay,
        HotRock,
        Charcoal,
        Tin,
        Iron,
        Steel,
        Bed,       // lathe part
        Carriage,  // lathe part
        Headstock, // lathe part

        // --- Tools ---
        CopperPick,
        BronzePick,
        IronPick,
        SteelPick,
        DamascusPick,
        Coffee,
    }

    public static class CraftingItemExtensions
    {
        /// <summary>
        /// Convert a BlockType to its CraftingItem equivalent (for inventory).
        /// </summary>
        public static CraftingItem ToCraftingItem(this BlockType blockType)
        {
            switch (blockType)
            {
                case BlockType.Plant:       return CraftingItem.Plant;
                case BlockType.Dirt:        return CraftingItem.Dirt;
                case BlockType.Rock:        return CraftingItem.Rock;
                case BlockType.Ice:         return CraftingItem.Ice;
                case BlockType.Copper:      return CraftingItem.Copper;
                case BlockType.Hematite:    return CraftingItem.Hematite;
                case BlockType.Cassiterite: return CraftingItem.Cassiterite;
                default:                    return CraftingItem.Rock;
            }
        }

        public static bool IsRawMaterial(this CraftingItem item)
        {
            return item <= CraftingItem.Cassiterite;
        }

        public static bool IsTool(this CraftingItem item)
        {
            return item >= CraftingItem.CopperPick;
        }

        public static bool IsPick(this CraftingItem item)
        {
            return item >= CraftingItem.CopperPick && item <= CraftingItem.DamascusPick;
        }

        /// <summary>
        /// Display name: inserts spaces before capitals (e.g. "HotRock" → "Hot Rock").
        /// </summary>
        public static string DisplayName(this CraftingItem item)
        {
            string name = item.ToString();
            var sb = new System.Text.StringBuilder(name.Length + 4);
            for (int i = 0; i < name.Length; i++)
            {
                if (i > 0 && char.IsUpper(name[i]))
                    sb.Append(' ');
                sb.Append(name[i]);
            }
            return sb.ToString();
        }

        /// <summary>
        /// Colour for UI display. Raw materials use their BlockType colour;
        /// crafted items get custom colours.
        /// </summary>
        public static Color GetColor(this CraftingItem item)
        {
            switch (item)
            {
                // Raw — delegate to BlockType colours
                case CraftingItem.Plant:       return BlockType.Plant.GetColor();
                case CraftingItem.Dirt:        return BlockType.Dirt.GetColor();
                case CraftingItem.Rock:        return BlockType.Rock.GetColor();
                case CraftingItem.Ice:         return BlockType.Ice.GetColor();
                case CraftingItem.Copper:      return BlockType.Copper.GetColor();
                case CraftingItem.Hematite:    return BlockType.Hematite.GetColor();
                case CraftingItem.Cassiterite: return BlockType.Cassiterite.GetColor();

                // Crafted materials
                case CraftingItem.Clay:      return new Color(0.72f, 0.55f, 0.40f);
                case CraftingItem.HotRock:   return new Color(0.85f, 0.35f, 0.15f);
                case CraftingItem.Charcoal:  return new Color(0.20f, 0.20f, 0.22f);
                case CraftingItem.Tin:       return new Color(0.75f, 0.75f, 0.78f);
                case CraftingItem.Iron:      return new Color(0.60f, 0.60f, 0.62f);
                case CraftingItem.Steel:     return new Color(0.70f, 0.72f, 0.75f);
                case CraftingItem.Bed:       return new Color(0.55f, 0.58f, 0.62f);
                case CraftingItem.Carriage:  return new Color(0.58f, 0.60f, 0.65f);
                case CraftingItem.Headstock: return new Color(0.62f, 0.65f, 0.70f);

                // Tools
                case CraftingItem.CopperPick:   return new Color(0.80f, 0.55f, 0.25f);
                case CraftingItem.BronzePick:   return new Color(0.75f, 0.60f, 0.30f);
                case CraftingItem.IronPick:     return new Color(0.60f, 0.60f, 0.62f);
                case CraftingItem.SteelPick:    return new Color(0.70f, 0.72f, 0.75f);
                case CraftingItem.DamascusPick: return new Color(0.55f, 0.65f, 0.70f);
                case CraftingItem.Coffee:       return new Color(0.40f, 0.25f, 0.15f);

                default: return Color.magenta;
            }
        }
    }
}
