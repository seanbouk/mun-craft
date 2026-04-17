using System.Collections.Generic;
using UnityEngine;

namespace MunCraft.Meshing
{
    /// <summary>
    /// Pragmatic static registry so other systems (e.g. BlockMiner) can find
    /// a ChunkRenderer by chunk coord without creating a Core→Meshing
    /// dependency cycle.
    /// </summary>
    public static class ChunkRendererRegistry
    {
        static readonly Dictionary<Vector3Int, ChunkRenderer> _renderers = new();

        public static void Register(Vector3Int coord, ChunkRenderer r) => _renderers[coord] = r;
        public static void Unregister(Vector3Int coord) => _renderers.Remove(coord);

        public static ChunkRenderer Get(Vector3Int coord)
        {
            _renderers.TryGetValue(coord, out var r);
            return r;
        }

        public static void Clear() => _renderers.Clear();

        public static int Count => _renderers.Count;

        public static IEnumerable<ChunkRenderer> All => _renderers.Values;
    }
}
