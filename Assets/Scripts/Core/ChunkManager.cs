using System;
using System.Collections.Generic;
using UnityEngine;

namespace MunCraft.Core
{
    /// <summary>
    /// Single source of truth for all block data.
    /// Handles chunk creation, lookup, and cross-chunk neighbor queries.
    /// </summary>
    public class ChunkManager : MonoBehaviour
    {
        [Header("Settings")]
        public float BlockSize = 1.0f;

        public event Action<BlockAddress, BlockType> OnBlockChanged;

        readonly Dictionary<Vector3Int, Chunk> _chunks = new();

        public IReadOnlyDictionary<Vector3Int, Chunk> Chunks => _chunks;

        public Chunk GetOrCreateChunk(Vector3Int coord)
        {
            if (!_chunks.TryGetValue(coord, out var chunk))
            {
                chunk = new Chunk(coord);
                _chunks[coord] = chunk;
            }
            return chunk;
        }

        public Chunk GetChunk(Vector3Int coord)
        {
            _chunks.TryGetValue(coord, out var chunk);
            return chunk;
        }

        public BlockType GetBlock(BlockAddress address)
        {
            var chunkCoord = address.GetChunkCoord(Chunk.Size);
            var chunk = GetChunk(chunkCoord);
            if (chunk == null) return BlockType.Air;

            var (lx, ly, lz) = address.GetLocalIndex(Chunk.Size);
            return chunk.GetBlock(address.Parity, lx, ly, lz);
        }

        public void SetBlock(BlockAddress address, BlockType type)
        {
            var chunkCoord = address.GetChunkCoord(Chunk.Size);
            var chunk = GetOrCreateChunk(chunkCoord);

            var (lx, ly, lz) = address.GetLocalIndex(Chunk.Size);
            chunk.SetBlock(address.Parity, lx, ly, lz, type);

            OnBlockChanged?.Invoke(address, type);
        }

        /// <summary>
        /// Set a block without firing the event. Used during bulk generation.
        /// Call NotifyAllDirty() after bulk operations.
        /// </summary>
        public void SetBlockSilent(BlockAddress address, BlockType type)
        {
            var chunkCoord = address.GetChunkCoord(Chunk.Size);
            var chunk = GetOrCreateChunk(chunkCoord);

            var (lx, ly, lz) = address.GetLocalIndex(Chunk.Size);
            chunk.SetBlock(address.Parity, lx, ly, lz, type);
        }

        /// <summary>
        /// Marks all chunks as dirty. Call after bulk generation.
        /// </summary>
        public void MarkAllDirty()
        {
            foreach (var chunk in _chunks.Values)
                chunk.IsDirty = true;
        }

        /// <summary>
        /// Clear all block data.
        /// </summary>
        public void Clear()
        {
            _chunks.Clear();
        }

        /// <summary>
        /// Check if a block is solid (non-air).
        /// </summary>
        public bool IsSolid(BlockAddress address)
        {
            return GetBlock(address).IsSolid();
        }

        /// <summary>
        /// Get the world position of a block.
        /// </summary>
        public Vector3 BlockToWorld(BlockAddress address)
        {
            return address.ToWorldPosition(BlockSize);
        }

        /// <summary>
        /// Find the nearest block to a world position.
        /// </summary>
        public BlockAddress WorldToBlock(Vector3 worldPos)
        {
            return BlockAddress.FromWorldPosition(worldPos, BlockSize);
        }

        /// <summary>
        /// Count total non-air blocks across all chunks.
        /// </summary>
        public int CountSolidBlocks()
        {
            int count = 0;
            foreach (var chunk in _chunks.Values)
            {
                for (int i = 0; i < chunk.GridA.Length; i++)
                    if (chunk.GridA[i] != 0) count++;
                for (int i = 0; i < chunk.GridB.Length; i++)
                    if (chunk.GridB[i] != 0) count++;
            }
            return count;
        }
    }
}
