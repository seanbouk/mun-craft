using System;
using System.Collections.Generic;
using UnityEngine;

namespace MunCraft.Crafting
{
    /// <summary>
    /// Runtime crafting state: machine unlocks, slot contents, moka pot
    /// progress, owned tools. Resets each play (no persistence yet).
    /// </summary>
    public class CraftingState : MonoBehaviour
    {
        // Which machines are unlocked (Hands always starts unlocked)
        readonly HashSet<Machine> _unlocked = new() { Machine.Hands };

        // Current slot contents per machine (null = empty slot)
        readonly Dictionary<Machine, CraftingItem?[]> _slots = new();

        // Moka pot partial progress (0 to 4, unlocks at 4)
        int _mokaProgress;

        // Tools owned (permanently unlocked picks + coffee)
        readonly HashSet<CraftingItem> _tools = new();

        // Crafted material inventory (separate from mined raw materials)
        readonly Dictionary<CraftingItem, int> _craftedCounts = new();

        public event Action OnChanged;

        public static CraftingState Instance { get; private set; }

        void Awake()
        {
            Instance = this;
            // Initialize slot arrays for all machines
            foreach (Machine m in Enum.GetValues(typeof(Machine)))
            {
                _slots[m] = new CraftingItem?[RecipeDatabase.SlotCount(m)];
            }
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ---- Machine locks ----

        public bool IsUnlocked(Machine machine) => _unlocked.Contains(machine);

        public void UnlockMachine(Machine machine)
        {
            _unlocked.Add(machine);
            OnChanged?.Invoke();
        }

        // ---- Slots ----

        public CraftingItem?[] GetSlots(Machine machine) => _slots[machine];

        public void SetSlot(Machine machine, int index, CraftingItem? item)
        {
            _slots[machine][index] = item;
            OnChanged?.Invoke();
        }

        public void ClearSlots(Machine machine)
        {
            var slots = _slots[machine];
            for (int i = 0; i < slots.Length; i++) slots[i] = null;
            OnChanged?.Invoke();
        }

        /// <summary>
        /// Are all slots filled (non-null)?
        /// </summary>
        public bool AllSlotsFilled(Machine machine)
        {
            var slots = _slots[machine];
            for (int i = 0; i < slots.Length; i++)
                if (slots[i] == null) return false;
            return true;
        }

        /// <summary>
        /// Get filled slot contents as an array (for recipe matching).
        /// Only returns non-null items.
        /// </summary>
        public CraftingItem[] GetFilledSlots(Machine machine)
        {
            var slots = _slots[machine];
            var result = new List<CraftingItem>();
            for (int i = 0; i < slots.Length; i++)
                if (slots[i].HasValue) result.Add(slots[i].Value);
            return result.ToArray();
        }

        // ---- Tools ----

        public bool HasTool(CraftingItem tool) => _tools.Contains(tool);

        public void AddTool(CraftingItem tool)
        {
            _tools.Add(tool);
            OnChanged?.Invoke();
        }

        /// <summary>
        /// Count of unlocked picks (for mining speed bonus).
        /// </summary>
        public int PickCount
        {
            get
            {
                int count = 0;
                if (_tools.Contains(CraftingItem.CopperPick)) count++;
                if (_tools.Contains(CraftingItem.BronzePick)) count++;
                if (_tools.Contains(CraftingItem.IronPick)) count++;
                if (_tools.Contains(CraftingItem.SteelPick)) count++;
                if (_tools.Contains(CraftingItem.DamascusPick)) count++;
                return count;
            }
        }

        public bool HasCoffee => _tools.Contains(CraftingItem.Coffee);

        // ---- Moka pot progress ----

        public int MokaProgress => _mokaProgress;

        public void AddMokaProgress()
        {
            _mokaProgress++;
            if (_mokaProgress >= 4)
                UnlockMachine(Machine.MokaPot);
            OnChanged?.Invoke();
        }

        // ---- Achievements ----

        readonly HashSet<string> _achievements = new();

        public bool HasAchievement(string name) => _achievements.Contains(name);

        public void EarnAchievement(string name)
        {
            _achievements.Add(name);
            OnChanged?.Invoke();
        }

        public int GetAchievementCount(Machine machine)
        {
            int count = 0;
            for (int i = 0; i < RecipeDatabase.AllRecipes.Length; i++)
            {
                var r = RecipeDatabase.AllRecipes[i];
                if (r.Machine == machine && r.OutputType == RecipeOutputType.Achievement
                    && _achievements.Contains(r.AchievementName))
                    count++;
            }
            return count;
        }

        public int TotalAchievements => _achievements.Count;

        /// <summary>
        /// All earned achievement names, for display.
        /// </summary>
        public IEnumerable<string> EarnedAchievements => _achievements;

        // ---- Crafted material counts ----

        public int GetCraftedCount(CraftingItem item)
        {
            return _craftedCounts.TryGetValue(item, out var c) ? c : 0;
        }

        public void AddCrafted(CraftingItem item, int count = 1)
        {
            _craftedCounts[item] = GetCraftedCount(item) + count;
            OnChanged?.Invoke();
        }

        // ---- Reset ----

        public void Reset()
        {
            _unlocked.Clear();
            _unlocked.Add(Machine.Hands);
            foreach (var slots in _slots.Values)
                for (int i = 0; i < slots.Length; i++) slots[i] = null;
            _mokaProgress = 0;
            _tools.Clear();
            _craftedCounts.Clear();
            _achievements.Clear();
            OnChanged?.Invoke();
        }
    }
}
