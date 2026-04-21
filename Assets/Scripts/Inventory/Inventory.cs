using System;
using System.Collections.Generic;
using MunCraft.Core;
using MunCraft.Crafting;
using UnityEngine;

namespace MunCraft.InventorySystem
{
    /// <summary>
    /// Unified inventory: tracks counts of CraftingItems (which cover both
    /// raw materials mined from the world and crafted items). When a block
    /// is mined, BlockMiner converts the BlockType to a CraftingItem before
    /// adding.
    /// </summary>
    public class Inventory : MonoBehaviour
    {
        readonly Dictionary<CraftingItem, int> _counts = new();

        public event Action OnChanged;

        public int GetCount(CraftingItem item)
        {
            return _counts.TryGetValue(item, out var c) ? c : 0;
        }

        public bool Has(CraftingItem item) => GetCount(item) > 0;

        public void Add(CraftingItem item, int count = 1)
        {
            if (count <= 0) return;
            _counts[item] = GetCount(item) + count;
            OnChanged?.Invoke();
        }

        public bool Remove(CraftingItem item, int count = 1)
        {
            int current = GetCount(item);
            if (current < count) return false;
            _counts[item] = current - count;
            if (_counts[item] <= 0) _counts.Remove(item);
            OnChanged?.Invoke();
            return true;
        }

        /// <summary>
        /// Convenience: add a mined block by its BlockType.
        /// </summary>
        public void AddBlock(BlockType blockType)
        {
            if (blockType == BlockType.Air) return;
            Add(blockType.ToCraftingItem());
        }

        /// <summary>
        /// Convenience: check a BlockType via its CraftingItem mapping.
        /// </summary>
        public bool HasBlock(BlockType blockType)
        {
            return Has(blockType.ToCraftingItem());
        }

        /// <summary>
        /// Get count for a BlockType via its CraftingItem mapping.
        /// </summary>
        public int GetBlockCount(BlockType blockType)
        {
            return GetCount(blockType.ToCraftingItem());
        }

        /// <summary>
        /// Iterate all items with count > 0.
        /// </summary>
        public IEnumerable<KeyValuePair<CraftingItem, int>> AllItems => _counts;

        public void Clear()
        {
            _counts.Clear();
            OnChanged?.Invoke();
        }
    }
}
