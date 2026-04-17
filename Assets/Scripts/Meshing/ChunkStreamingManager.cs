using System.Collections.Generic;
using MunCraft.Core;
using UnityEngine;

namespace MunCraft.Meshing
{
    /// <summary>
    /// Creates/destroys chunk renderers based on distance from the player.
    /// Block data and gravity stay loaded for the whole world — only the
    /// mesh renderers are streamed, so gravity never pops when a chunk
    /// appears or disappears.
    /// </summary>
    public class ChunkStreamingManager : MonoBehaviour
    {
        [Header("Streaming")]
        [Tooltip("Chunks within this distance (world units) get renderers")]
        public float RenderDistance = 30f;

        [Tooltip("Multiplier on RenderDistance for unloading (hysteresis to prevent thrashing)")]
        public float UnloadFactor = 1.3f;

        [Tooltip("Max renderer creates per frame (spread the cost)")]
        public int MaxCreatesPerFrame = 4;

        [Tooltip("Max renderer destroys per frame")]
        public int MaxDestroysPerFrame = 4;

        [Tooltip("How often to re-evaluate streaming (seconds)")]
        public float UpdateInterval = 0.25f;

        ChunkManager _chunkManager;
        Transform _playerTransform;
        Material _blockMaterial;
        GameObject _chunksRoot;

        float _lastUpdateTime;

        // Track which chunk coords have active renderers (separate from the registry
        // so we can iterate our own set without allocating)
        readonly HashSet<Vector3Int> _activeCoords = new();

        // Scratch lists reused each update to avoid allocation
        readonly List<Vector3Int> _toCreate = new();
        readonly List<Vector3Int> _toDestroy = new();

        public void Initialize(ChunkManager chunkManager, Transform playerTransform,
                               Material blockMaterial, GameObject chunksRoot)
        {
            _chunkManager = chunkManager;
            _playerTransform = playerTransform;
            _blockMaterial = blockMaterial;
            _chunksRoot = chunksRoot;
        }

        void Update()
        {
            if (_chunkManager == null || _playerTransform == null) return;

            // Throttle — no need to re-evaluate every frame
            if (Time.time - _lastUpdateTime < UpdateInterval) return;
            _lastUpdateTime = Time.time;

            Vector3 playerPos = _playerTransform.position;
            float loadDistSqr = RenderDistance * RenderDistance;
            float unloadDistSqr = (RenderDistance * UnloadFactor) * (RenderDistance * UnloadFactor);
            float chunkWorldSize = Chunk.Size * _chunkManager.BlockSize;

            _toCreate.Clear();
            _toDestroy.Clear();

            // Check all chunks in the world — create renderers for nearby ones
            foreach (var kvp in _chunkManager.Chunks)
            {
                Vector3Int coord = kvp.Key;
                Chunk chunk = kvp.Value;

                if (chunk.IsEmpty()) continue;

                Vector3 chunkCenter = ChunkCenter(coord, chunkWorldSize);
                float distSqr = (chunkCenter - playerPos).sqrMagnitude;

                bool hasRenderer = _activeCoords.Contains(coord);

                if (!hasRenderer && distSqr <= loadDistSqr)
                {
                    _toCreate.Add(coord);
                }
                else if (hasRenderer && distSqr > unloadDistSqr)
                {
                    _toDestroy.Add(coord);
                }
            }

            // Sort creates by distance (nearest first)
            _toCreate.Sort((a, b) =>
            {
                float da = (ChunkCenter(a, chunkWorldSize) - playerPos).sqrMagnitude;
                float db = (ChunkCenter(b, chunkWorldSize) - playerPos).sqrMagnitude;
                return da.CompareTo(db);
            });

            // Apply creates (throttled)
            int creates = Mathf.Min(_toCreate.Count, MaxCreatesPerFrame);
            for (int i = 0; i < creates; i++)
            {
                CreateRenderer(_toCreate[i]);
            }

            // Apply destroys (throttled)
            int destroys = Mathf.Min(_toDestroy.Count, MaxDestroysPerFrame);
            for (int i = 0; i < destroys; i++)
            {
                DestroyRenderer(_toDestroy[i]);
            }
        }

        void CreateRenderer(Vector3Int coord)
        {
            var chunk = _chunkManager.GetChunk(coord);
            if (chunk == null || chunk.IsEmpty()) return;

            // Guard: already exists (race between throttled frames)
            if (_activeCoords.Contains(coord)) return;

            var obj = new GameObject($"Chunk({coord.x},{coord.y},{coord.z})");
            obj.transform.parent = _chunksRoot.transform;
            obj.AddComponent<MeshFilter>();
            obj.AddComponent<MeshRenderer>();
            var renderer = obj.AddComponent<ChunkRenderer>();
            renderer.Initialize(chunk, _chunkManager, _blockMaterial);

            _activeCoords.Add(coord);
        }

        void DestroyRenderer(Vector3Int coord)
        {
            var renderer = ChunkRendererRegistry.Get(coord);
            if (renderer != null)
            {
                Destroy(renderer.gameObject);
            }
            _activeCoords.Remove(coord);
        }

        static Vector3 ChunkCenter(Vector3Int coord, float chunkWorldSize)
        {
            return new Vector3(
                coord.x * chunkWorldSize + chunkWorldSize * 0.5f,
                coord.y * chunkWorldSize + chunkWorldSize * 0.5f,
                coord.z * chunkWorldSize + chunkWorldSize * 0.5f
            );
        }

        /// <summary>
        /// Force-load all renderers within render distance right now (no throttle).
        /// Called at startup so the player doesn't see an empty world while
        /// the streaming manager trickles in chunks.
        /// </summary>
        public void ForceLoadNearby()
        {
            if (_chunkManager == null || _playerTransform == null) return;

            Vector3 playerPos = _playerTransform.position;
            float loadDistSqr = RenderDistance * RenderDistance;
            float chunkWorldSize = Chunk.Size * _chunkManager.BlockSize;

            foreach (var kvp in _chunkManager.Chunks)
            {
                if (kvp.Value.IsEmpty()) continue;

                Vector3 chunkCenter = ChunkCenter(kvp.Key, chunkWorldSize);
                if ((chunkCenter - playerPos).sqrMagnitude <= loadDistSqr)
                {
                    CreateRenderer(kvp.Key);
                }
            }
        }
    }
}
