using System;
using System.Collections.Generic;
using CI = MunCraft.Crafting.CraftingItem;

namespace MunCraft.Crafting
{
    public enum Machine
    {
        Hands,
        Fire,
        Furnace,
        Forge,
        Lathe,
        MokaPot
    }

    public enum RecipeOutputType
    {
        Material,
        Tool,
        Unlock,
        Partial,
        Achievement, // collectible emoji badge
    }

    public struct Recipe
    {
        public Machine Machine;
        public CraftingItem[] Inputs;
        public RecipeOutputType OutputType;
        public CraftingItem Produces;       // for Material/Tool
        public Machine UnlocksTarget;       // for Unlock/Partial
        public float PartialFraction;       // for Partial
        public string AchievementEmoji;     // for Achievement
        public string AchievementName;      // for Achievement
    }

    public static class RecipeDatabase
    {
        public static readonly Recipe[] AllRecipes;

        static RecipeDatabase()
        {
            var list = new List<Recipe>();

            // ================================================================
            //  CORE RECIPES (materials, tools, unlocks)
            // ================================================================

            // ---- Hands (1 slot) ----
            list.Add(Mat(Machine.Hands, new[] { CI.Copper }, RecipeOutputType.Tool, CI.CopperPick));
            list.Add(Unlock(Machine.Hands, new[] { CI.Plant }, Machine.Fire));

            // ---- Fire (2 slots) ----
            list.Add(Mat(Machine.Fire, new[] { CI.Dirt, CI.Rock }, RecipeOutputType.Material, CI.Clay));
            list.Add(Mat(Machine.Fire, new[] { CI.Copper, CI.Tin }, RecipeOutputType.Tool, CI.BronzePick));
            list.Add(Mat(Machine.Fire, new[] { CI.Rock, CI.Rock }, RecipeOutputType.Material, CI.HotRock));
            list.Add(Mat(Machine.Fire, new[] { CI.Plant, CI.Plant }, RecipeOutputType.Material, CI.Charcoal));
            list.Add(Mat(Machine.Fire, new[] { CI.Charcoal, CI.Cassiterite }, RecipeOutputType.Material, CI.Tin));
            list.Add(Unlock(Machine.Fire, new[] { CI.Clay, CI.Charcoal }, Machine.Furnace));

            // ---- Furnace (2 slots) ----
            list.Add(Mat(Machine.Furnace, new[] { CI.Iron, CI.Charcoal }, RecipeOutputType.Material, CI.Steel));
            list.Add(Mat(Machine.Furnace, new[] { CI.Plant, CI.Iron }, RecipeOutputType.Tool, CI.IronPick));
            list.Add(Mat(Machine.Furnace, new[] { CI.Charcoal, CI.Hematite }, RecipeOutputType.Material, CI.Iron));
            list.Add(Unlock(Machine.Furnace, new[] { CI.Steel, CI.Steel }, Machine.Forge));

            // ---- Forge (3 slots) ----
            list.Add(Mat(Machine.Forge, new[] { CI.Plant, CI.Steel, CI.Steel }, RecipeOutputType.Tool, CI.SteelPick));
            list.Add(Mat(Machine.Forge, new[] { CI.Steel, CI.Steel, CI.Iron }, RecipeOutputType.Material, CI.Bed));
            list.Add(Mat(Machine.Forge, new[] { CI.Steel, CI.Steel, CI.Tin }, RecipeOutputType.Material, CI.Carriage));
            list.Add(Mat(Machine.Forge, new[] { CI.Steel, CI.Steel, CI.Copper }, RecipeOutputType.Material, CI.Headstock));
            list.Add(Unlock(Machine.Forge, new[] { CI.Headstock, CI.Carriage, CI.Bed }, Machine.Lathe));

            // ---- Lathe (3 slots) ----
            list.Add(Mat(Machine.Lathe, new[] { CI.Plant, CI.Steel, CI.Steel }, RecipeOutputType.Tool, CI.DamascusPick));
            list.Add(new Recipe
            {
                Machine = Machine.Lathe,
                Inputs = new[] { CI.Steel, CI.Steel, CI.Steel },
                OutputType = RecipeOutputType.Partial,
                UnlocksTarget = Machine.MokaPot,
                PartialFraction = 0.25f,
            });

            // ---- Moka Pot (3 slots) ----
            list.Add(Mat(Machine.MokaPot, new[] { CI.Plant, CI.Ice, CI.HotRock }, RecipeOutputType.Tool, CI.Coffee));

            // ================================================================
            //  ACHIEVEMENTS (emoji badges)
            // ================================================================

            // ---- Hands (1 slot) — 3 badges ----
            list.Add(Badge(Machine.Hands, new[] { CI.Dirt },      "\U0001F3FA", "Mud Pie"));
            list.Add(Badge(Machine.Hands, new[] { CI.Rock },      "\U0001F5FF", "Stone Face"));
            list.Add(Badge(Machine.Hands, new[] { CI.Ice },       "\u2744\uFE0F", "Snowflake"));

            // ---- Fire (2 slots) — 9 badges ----
            list.Add(Badge(Machine.Fire, new[] { CI.Dirt, CI.Dirt },       "\U0001F9F1", "Bricks"));
            list.Add(Badge(Machine.Fire, new[] { CI.Dirt, CI.Plant },      "\U0001F331", "Seedling"));
            list.Add(Badge(Machine.Fire, new[] { CI.Dirt, CI.Ice },        "\U0001F4A7", "Dewdrop"));
            list.Add(Badge(Machine.Fire, new[] { CI.Plant, CI.Rock },      "\U0001FAB4", "Potted Plant"));
            list.Add(Badge(Machine.Fire, new[] { CI.Plant, CI.Ice },       "\U0001F375", "Tea"));
            list.Add(Badge(Machine.Fire, new[] { CI.Ice, CI.Ice },         "\U0001F9CA", "Ice Cube"));
            list.Add(Badge(Machine.Fire, new[] { CI.Ice, CI.Rock },        "\u26F0\uFE0F", "Mountain"));
            list.Add(Badge(Machine.Fire, new[] { CI.Copper, CI.Rock },     "\U0001F514", "Bell"));
            list.Add(Badge(Machine.Fire, new[] { CI.Hematite, CI.Rock },   "\U0001FAA8", "Boulder"));

            // ---- Furnace (2 slots) — 8 badges ----
            list.Add(Badge(Machine.Furnace, new[] { CI.Dirt, CI.Dirt },    "\U0001F3E0", "Cottage"));
            list.Add(Badge(Machine.Furnace, new[] { CI.Rock, CI.Rock },    "\U0001F48E", "Gem"));
            list.Add(Badge(Machine.Furnace, new[] { CI.Plant, CI.Plant },  "\U0001F4DC", "Scroll"));
            list.Add(Badge(Machine.Furnace, new[] { CI.Ice, CI.Ice },      "\U0001F52E", "Crystal Ball"));
            list.Add(Badge(Machine.Furnace, new[] { CI.Dirt, CI.Rock },    "\U0001FAA3", "Bucket"));
            list.Add(Badge(Machine.Furnace, new[] { CI.Plant, CI.Rock },   "\u26B1\uFE0F", "Urn"));
            list.Add(Badge(Machine.Furnace, new[] { CI.Plant, CI.Ice },    "\U0001F9EA", "Potion"));
            list.Add(Badge(Machine.Furnace, new[] { CI.Dirt, CI.Ice },     "\U0001FAE7", "Bubbles"));

            // ---- Forge (3 slots) — 10 badges ----
            list.Add(Badge(Machine.Forge, new[] { CI.Rock, CI.Rock, CI.Rock },     "\U0001F3DB\uFE0F", "Monument"));
            list.Add(Badge(Machine.Forge, new[] { CI.Plant, CI.Plant, CI.Plant },   "\U0001F333", "Great Tree"));
            list.Add(Badge(Machine.Forge, new[] { CI.Dirt, CI.Dirt, CI.Dirt },       "\U0001F3D7\uFE0F", "Foundation"));
            list.Add(Badge(Machine.Forge, new[] { CI.Ice, CI.Ice, CI.Ice },         "\u2603\uFE0F", "Snowman"));
            list.Add(Badge(Machine.Forge, new[] { CI.Plant, CI.Rock, CI.Dirt },     "\U0001F30D", "World"));
            list.Add(Badge(Machine.Forge, new[] { CI.Plant, CI.Rock, CI.Ice },      "\U0001F3D4\uFE0F", "Summit"));
            list.Add(Badge(Machine.Forge, new[] { CI.Dirt, CI.Rock, CI.Ice },       "\U0001F30B", "Volcano"));
            list.Add(Badge(Machine.Forge, new[] { CI.Plant, CI.Dirt, CI.Ice },      "\U0001F30A", "Tidal"));
            list.Add(Badge(Machine.Forge, new[] { CI.Plant, CI.Plant, CI.Rock },    "\U0001F38B", "Bamboo"));
            list.Add(Badge(Machine.Forge, new[] { CI.Plant, CI.Plant, CI.Dirt },    "\U0001F33B", "Sunflower"));

            // ---- Lathe (3 slots) — 8 badges ----
            list.Add(Badge(Machine.Lathe, new[] { CI.Plant, CI.Plant, CI.Plant },   "\U0001F4D6", "Book"));
            list.Add(Badge(Machine.Lathe, new[] { CI.Rock, CI.Rock, CI.Rock },      "\U0001F3C6", "Trophy"));
            list.Add(Badge(Machine.Lathe, new[] { CI.Dirt, CI.Dirt, CI.Dirt },       "\U0001F3B2", "Dice"));
            list.Add(Badge(Machine.Lathe, new[] { CI.Ice, CI.Ice, CI.Ice },          "\u2B50", "Star"));
            list.Add(Badge(Machine.Lathe, new[] { CI.Plant, CI.Rock, CI.Dirt },      "\U0001F3AF", "Bullseye"));
            list.Add(Badge(Machine.Lathe, new[] { CI.Plant, CI.Rock, CI.Ice },       "\U0001F3AA", "Circus"));
            list.Add(Badge(Machine.Lathe, new[] { CI.Dirt, CI.Rock, CI.Ice },        "\U0001F3B5", "Music Box"));
            list.Add(Badge(Machine.Lathe, new[] { CI.Plant, CI.Dirt, CI.Ice },       "\U0001F9E9", "Puzzle"));

            // ---- Moka Pot (3 slots) — 7 badges ----
            list.Add(Badge(Machine.MokaPot, new[] { CI.Plant, CI.Plant, CI.Plant },  "\U0001F343", "Matcha"));
            list.Add(Badge(Machine.MokaPot, new[] { CI.Rock, CI.Rock, CI.Rock },     "\U0001FA99", "Coin"));
            list.Add(Badge(Machine.MokaPot, new[] { CI.Ice, CI.Ice, CI.Ice },        "\U0001F31F", "Superstar"));
            list.Add(Badge(Machine.MokaPot, new[] { CI.Dirt, CI.Dirt, CI.Dirt },      "\U0001F36B", "Chocolate"));
            list.Add(Badge(Machine.MokaPot, new[] { CI.Plant, CI.Plant, CI.Ice },    "\U0001F379", "Cocktail"));
            list.Add(Badge(Machine.MokaPot, new[] { CI.Plant, CI.Rock, CI.Dirt },     "\U0001F370", "Cake"));
            list.Add(Badge(Machine.MokaPot, new[] { CI.Plant, CI.Rock, CI.Ice },      "\U0001F381", "Gift"));

            AllRecipes = list.ToArray();
        }

        static Recipe Mat(Machine m, CraftingItem[] inputs, RecipeOutputType type, CraftingItem produces)
        {
            return new Recipe { Machine = m, Inputs = inputs, OutputType = type, Produces = produces };
        }

        static Recipe Unlock(Machine m, CraftingItem[] inputs, Machine target)
        {
            return new Recipe { Machine = m, Inputs = inputs, OutputType = RecipeOutputType.Unlock, UnlocksTarget = target };
        }

        static Recipe Badge(Machine m, CraftingItem[] inputs, string emoji, string name)
        {
            return new Recipe
            {
                Machine = m, Inputs = inputs,
                OutputType = RecipeOutputType.Achievement,
                AchievementEmoji = emoji, AchievementName = name,
            };
        }

        public static Recipe? FindRecipe(Machine machine, CraftingItem[] slotContents)
        {
            var filled = new List<CraftingItem>();
            for (int i = 0; i < slotContents.Length; i++)
                filled.Add(slotContents[i]);

            for (int r = 0; r < AllRecipes.Length; r++)
            {
                var recipe = AllRecipes[r];
                if (recipe.Machine != machine) continue;
                if (recipe.Inputs.Length != filled.Count) continue;
                if (SameMultiset(recipe.Inputs, filled))
                    return recipe;
            }
            return null;
        }

        static bool SameMultiset(CraftingItem[] a, List<CraftingItem> b)
        {
            if (a.Length != b.Count) return false;
            var sa = new CraftingItem[a.Length];
            Array.Copy(a, sa, a.Length);
            var sb = new CraftingItem[b.Count];
            b.CopyTo(sb);
            Array.Sort(sa);
            Array.Sort(sb);
            for (int i = 0; i < sa.Length; i++)
                if (sa[i] != sb[i]) return false;
            return true;
        }

        public static int SlotCount(Machine machine)
        {
            switch (machine)
            {
                case Machine.Hands: return 1;
                case Machine.Fire: return 2;
                case Machine.Furnace: return 2;
                case Machine.Forge: return 3;
                case Machine.Lathe: return 3;
                case Machine.MokaPot: return 3;
                default: return 0;
            }
        }

        public static string DisplayName(Machine machine)
        {
            switch (machine)
            {
                case Machine.MokaPot: return "Moka Pot";
                default: return machine.ToString();
            }
        }

        /// <summary>
        /// Count achievement recipes for a given machine.
        /// </summary>
        public static int AchievementTotal(Machine machine)
        {
            int count = 0;
            for (int i = 0; i < AllRecipes.Length; i++)
                if (AllRecipes[i].Machine == machine && AllRecipes[i].OutputType == RecipeOutputType.Achievement)
                    count++;
            return count;
        }
    }
}
