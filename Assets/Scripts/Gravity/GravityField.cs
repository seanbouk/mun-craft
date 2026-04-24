using System.Collections.Generic;
using MunCraft.Core;
using UnityEngine;

namespace MunCraft.Gravity
{
    /// <summary>
    /// Singleton that owns the Barnes-Hut octree and provides gravity queries.
    /// Listens to ChunkManager.OnBlockChanged for incremental updates —
    /// each mine is an O(log n) tree update, no full rebuild.
    /// </summary>
    public class GravityField : MonoBehaviour
    {
        [Header("Gravity Settings")]
        [Tooltip("For ~7000 blocks at radius 12, 0.2 gives ~9.8 m/s² at surface")]
        public float GravityConstant = 0.2f;

        [Range(0f, 2f)]
        public float Theta = 0.5f;

        [Tooltip("Softening parameter to prevent singularity at zero distance")]
        public float Softening = 0.5f;

        [Tooltip("Clamp maximum gravity magnitude to prevent extreme forces")]
        public float MaxGravity = 30f;

        ChunkManager _chunkManager;
        GravityOctree _octree;

        public static GravityField Instance { get; private set; }

        void Awake()
        {
            Instance = this;
            _octree = new GravityOctree(Theta, GravityConstant, Softening);
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;

            if (_chunkManager != null)
                _chunkManager.OnBlockChanged -= OnBlockChanged;
        }

        public void Initialize(ChunkManager chunkManager, List<BlockAddress> initialBlocks)
        {
            _chunkManager = chunkManager;
            _chunkManager.OnBlockChanged += OnBlockChanged;

            // Initial bulk build — O(n log n), only happens once at scene load
            var positions = new List<Vector3>(initialBlocks.Count);
            for (int i = 0; i < initialBlocks.Count; i++)
                positions.Add(initialBlocks[i].ToWorldPosition(_chunkManager.BlockSize));

            _octree.Build(positions);
        }

        void OnBlockChanged(BlockAddress address, BlockType newType)
        {
            Vector3 worldPos = address.ToWorldPosition(_chunkManager.BlockSize);
            if (newType == BlockType.Air)
                _octree.RemoveBody(worldPos);
            else
                _octree.AddBody(worldPos);
        }

        void LateUpdate()
        {
            if (_octree == null) return;
            _octree.Theta = Theta;
            _octree.GravityConstant = GravityConstant;
        }

        public Vector3 GetGravityAt(Vector3 position)
        {
            if (_octree == null)
                return Vector3.down * GravityConstant;

            Vector3 gravity = _octree.QueryGravity(position);
            float mag = gravity.magnitude;
            if (mag > MaxGravity)
                gravity = gravity / mag * MaxGravity;

            return gravity;
        }

        public Vector3 GetUpAt(Vector3 position)
        {
            Vector3 gravity = GetGravityAt(position);
            return gravity.sqrMagnitude > 0.001f ? -gravity.normalized : Vector3.up;
        }

        public int BlockCount => _octree != null ? _octree.TotalBodies : 0;
    }
}
