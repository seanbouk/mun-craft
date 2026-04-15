using System;
using System.Collections.Generic;
using MunCraft.Core;
using UnityEngine;

namespace MunCraft.InventorySystem
{
    /// <summary>
    /// Counts of each block type the player has collected.
    /// Currently in-memory only — resets each play. Persistence later.
    /// </summary>
    public class Inventory : MonoBehaviour
    {
        readonly Dictionary<BlockType, int> _counts = new();

        public event Action OnChanged;

        public int GetCount(BlockType type)
        {
            return _counts.TryGetValue(type, out var c) ? c : 0;
        }

        public bool Has(BlockType type) => GetCount(type) > 0;

        public void Add(BlockType type, int count = 1)
        {
            if (type == BlockType.Air || count <= 0) return;
            _counts[type] = GetCount(type) + count;
            OnChanged?.Invoke();
        }

        public void Clear()
        {
            _counts.Clear();
            OnChanged?.Invoke();
        }
    }
}
