using System;
using System.Collections.Generic;

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
        Material,  // produces a CraftingItem, added to inventory
        Tool,      // produces a tool (pick or coffee), permanently unlocked
        Unlock,    // unlocks the next machine
        Partial,   // contributes a fraction toward unlocking a machine (moka pot)
    }

    public struct Recipe
    {
        public Machine Machine;
        public CraftingItem[] Inputs;    // unordered multiset
        public RecipeOutputType OutputType;
        public CraftingItem Produces;    // for Material/Tool
        public Machine UnlocksTarget;    // for Unlock/Partial
        public float PartialFraction;    // for Partial (0.25 = 25%)
    }

    /// <summary>
    /// Static recipe list. All recipes from the user's design — nothing extra.
    /// </summary>
    public static class RecipeDatabase
    {
        public static readonly Recipe[] AllRecipes;

        static RecipeDatabase()
        {
            var list = new List<Recipe>();

            // ---- Hands (1 slot) ----
            list.Add(Mat(Machine.Hands, new[] { CraftingItem.Copper }, RecipeOutputType.Tool, CraftingItem.CopperPick));
            list.Add(Unlock(Machine.Hands, new[] { CraftingItem.Plant }, Machine.Fire));

            // ---- Fire (2 slots) ----
            list.Add(Mat(Machine.Fire, new[] { CraftingItem.Dirt, CraftingItem.Rock }, RecipeOutputType.Material, CraftingItem.Clay));
            list.Add(Mat(Machine.Fire, new[] { CraftingItem.Copper, CraftingItem.Tin }, RecipeOutputType.Tool, CraftingItem.BronzePick));
            list.Add(Mat(Machine.Fire, new[] { CraftingItem.Rock, CraftingItem.Rock }, RecipeOutputType.Material, CraftingItem.HotRock));
            list.Add(Mat(Machine.Fire, new[] { CraftingItem.Plant, CraftingItem.Plant }, RecipeOutputType.Material, CraftingItem.Charcoal));
            list.Add(Mat(Machine.Fire, new[] { CraftingItem.Charcoal, CraftingItem.Cassiterite }, RecipeOutputType.Material, CraftingItem.Tin));
            list.Add(Unlock(Machine.Fire, new[] { CraftingItem.Clay, CraftingItem.Charcoal }, Machine.Furnace));

            // ---- Furnace (2 slots) ----
            list.Add(Mat(Machine.Furnace, new[] { CraftingItem.Iron, CraftingItem.Charcoal }, RecipeOutputType.Material, CraftingItem.Steel));
            list.Add(Mat(Machine.Furnace, new[] { CraftingItem.Plant, CraftingItem.Iron }, RecipeOutputType.Tool, CraftingItem.IronPick));
            list.Add(Mat(Machine.Furnace, new[] { CraftingItem.Charcoal, CraftingItem.Hematite }, RecipeOutputType.Material, CraftingItem.Iron));
            list.Add(Unlock(Machine.Furnace, new[] { CraftingItem.Steel, CraftingItem.Steel }, Machine.Forge));

            // ---- Forge (3 slots) ----
            list.Add(Mat(Machine.Forge, new[] { CraftingItem.Plant, CraftingItem.Steel, CraftingItem.Steel }, RecipeOutputType.Tool, CraftingItem.SteelPick));
            list.Add(Mat(Machine.Forge, new[] { CraftingItem.Steel, CraftingItem.Steel, CraftingItem.Iron }, RecipeOutputType.Material, CraftingItem.Bed));
            list.Add(Mat(Machine.Forge, new[] { CraftingItem.Steel, CraftingItem.Steel, CraftingItem.Tin }, RecipeOutputType.Material, CraftingItem.Carriage));
            list.Add(Mat(Machine.Forge, new[] { CraftingItem.Steel, CraftingItem.Steel, CraftingItem.Copper }, RecipeOutputType.Material, CraftingItem.Headstock));
            list.Add(Unlock(Machine.Forge, new[] { CraftingItem.Headstock, CraftingItem.Carriage, CraftingItem.Bed }, Machine.Lathe));

            // ---- Lathe (3 slots) ----
            list.Add(Mat(Machine.Lathe, new[] { CraftingItem.Plant, CraftingItem.Steel, CraftingItem.Steel }, RecipeOutputType.Tool, CraftingItem.DamascusPick));
            list.Add(new Recipe
            {
                Machine = Machine.Lathe,
                Inputs = new[] { CraftingItem.Steel, CraftingItem.Steel, CraftingItem.Steel },
                OutputType = RecipeOutputType.Partial,
                UnlocksTarget = Machine.MokaPot,
                PartialFraction = 0.25f,
            });

            // ---- Moka Pot (3 slots) ----
            list.Add(Mat(Machine.MokaPot, new[] { CraftingItem.Plant, CraftingItem.Ice, CraftingItem.HotRock }, RecipeOutputType.Tool, CraftingItem.Coffee));

            AllRecipes = list.ToArray();
        }

        static Recipe Mat(Machine m, CraftingItem[] inputs, RecipeOutputType type, CraftingItem produces)
        {
            return new Recipe
            {
                Machine = m,
                Inputs = inputs,
                OutputType = type,
                Produces = produces,
            };
        }

        static Recipe Unlock(Machine m, CraftingItem[] inputs, Machine target)
        {
            return new Recipe
            {
                Machine = m,
                Inputs = inputs,
                OutputType = RecipeOutputType.Unlock,
                UnlocksTarget = target,
            };
        }

        /// <summary>
        /// Find a matching recipe for the given machine and slot contents.
        /// Inputs are compared as unordered multisets.
        /// Returns null if no match.
        /// </summary>
        public static Recipe? FindRecipe(Machine machine, CraftingItem[] slotContents)
        {
            // Filter out "empty" entries (default enum value)
            var filled = new List<CraftingItem>();
            for (int i = 0; i < slotContents.Length; i++)
            {
                // CraftingItem.Plant == 0, so we can't use default check.
                // Instead: all slots should be filled for a recipe to match.
                filled.Add(slotContents[i]);
            }

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
            // Sort copies and compare
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

        /// <summary>
        /// How many input slots does this machine have?
        /// </summary>
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

        /// <summary>
        /// Display name for a machine.
        /// </summary>
        public static string DisplayName(Machine machine)
        {
            switch (machine)
            {
                case Machine.MokaPot: return "Moka Pot";
                default: return machine.ToString();
            }
        }
    }
}
