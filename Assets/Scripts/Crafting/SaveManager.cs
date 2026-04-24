using System;
using System.Collections.Generic;
using MunCraft.InventorySystem;
using UnityEngine;

namespace MunCraft.Crafting
{
    /// <summary>
    /// Serializes Inventory + CraftingState to PlayerPrefs as JSON.
    /// Auto-saves on every state change. Loads on demand.
    /// Works in editor (Windows registry) and WebGL (IndexedDB).
    /// </summary>
    public static class SaveManager
    {
        const string SaveKey = "MunCraft_SaveData";

        [Serializable]
        class SaveData
        {
            public int[] ItemIds;
            public int[] ItemCounts;
            public int[] UnlockedMachines;
            public int[] OwnedTools;
            public string[] EarnedAchievements;
            public int MokaProgress;
        }

        public static void Save(Inventory inventory, CraftingState state)
        {
            var data = new SaveData();

            // Inventory
            var ids = new List<int>();
            var counts = new List<int>();
            foreach (var kvp in inventory.AllItems)
            {
                if (kvp.Value <= 0) continue;
                ids.Add((int)kvp.Key);
                counts.Add(kvp.Value);
            }
            data.ItemIds = ids.ToArray();
            data.ItemCounts = counts.ToArray();

            // Unlocked machines
            var machines = new List<int>();
            foreach (Machine m in Enum.GetValues(typeof(Machine)))
                if (state.IsUnlocked(m)) machines.Add((int)m);
            data.UnlockedMachines = machines.ToArray();

            // Tools
            var tools = new List<int>();
            foreach (CraftingItem item in Enum.GetValues(typeof(CraftingItem)))
                if (item.IsTool() && state.HasTool(item)) tools.Add((int)item);
            data.OwnedTools = tools.ToArray();

            // Achievements
            var achievements = new List<string>();
            foreach (var name in state.EarnedAchievements)
                achievements.Add(name);
            data.EarnedAchievements = achievements.ToArray();

            // Moka progress
            data.MokaProgress = state.MokaProgress;

            string json = JsonUtility.ToJson(data);
            PlayerPrefs.SetString(SaveKey, json);
            PlayerPrefs.Save();
        }

        public static bool Load(Inventory inventory, CraftingState state)
        {
            if (!PlayerPrefs.HasKey(SaveKey)) return false;

            string json = PlayerPrefs.GetString(SaveKey);
            SaveData data;
            try
            {
                data = JsonUtility.FromJson<SaveData>(json);
            }
            catch
            {
                UnityEngine.Debug.LogWarning("[SaveManager] Failed to parse save data, ignoring.");
                return false;
            }

            if (data == null) return false;

            // Restore inventory
            inventory.Clear();
            if (data.ItemIds != null && data.ItemCounts != null)
            {
                int count = Mathf.Min(data.ItemIds.Length, data.ItemCounts.Length);
                for (int i = 0; i < count; i++)
                    inventory.Add((CraftingItem)data.ItemIds[i], data.ItemCounts[i]);
            }

            // Restore crafting state
            state.Reset();
            if (data.UnlockedMachines != null)
                foreach (int m in data.UnlockedMachines)
                    state.UnlockMachine((Machine)m);

            if (data.OwnedTools != null)
                foreach (int t in data.OwnedTools)
                    state.AddTool((CraftingItem)t);

            if (data.EarnedAchievements != null)
                foreach (string name in data.EarnedAchievements)
                    state.EarnAchievement(name);

            state.SetMokaProgress(data.MokaProgress);

            return true;
        }

        public static void DeleteSave()
        {
            PlayerPrefs.DeleteKey(SaveKey);
            PlayerPrefs.Save();
            UnityEngine.Debug.Log("[SaveManager] Save data deleted.");
        }

        public static bool HasSave => PlayerPrefs.HasKey(SaveKey);
    }
}
