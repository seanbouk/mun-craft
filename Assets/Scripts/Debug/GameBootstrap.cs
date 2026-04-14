using System.Collections.Generic;
using MunCraft.Core;
using MunCraft.Gravity;
using MunCraft.Interaction;
using MunCraft.Meshing;
using MunCraft.Player;
using UnityEngine;

namespace MunCraft.Debug
{
    /// <summary>
    /// Wires up the entire scene on Start: generates the sphere, creates chunk renderers,
    /// initializes gravity, spawns the player.
    /// Attach to a root GameObject in the scene.
    /// </summary>
    public class GameBootstrap : MonoBehaviour
    {
        [Header("World")]
        public int SphereRadius = 12;
        public float BlockSize = 1.0f;

        [Header("Gravity")]
        public float GravityConstant = 9.81f;
        public float BarnesHutTheta = 0.5f;

        [Header("Player")]
        public float PlayerHeight = 1.8f;
        public float PlayerRadius = 0.3f;
        public float MoveSpeed = 5f;
        public float JumpForce = 5f;

        ChunkManager _chunkManager;
        GravityField _gravityField;
        GameObject _playerObj;
        GameObject _chunksRoot;
        Material _blockMaterial;

        public ChunkManager ChunkManager => _chunkManager;

        void Start()
        {
            // Create shared material
            var shader = Shader.Find("MunCraft/FlatBlock");
            if (shader == null)
            {
                UnityEngine.Debug.LogError("FlatBlock shader not found! Using fallback.");
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            }
            _blockMaterial = new Material(shader);

            // Create ChunkManager
            var chunkManagerObj = new GameObject("ChunkManager");
            _chunkManager = chunkManagerObj.AddComponent<ChunkManager>();
            _chunkManager.BlockSize = BlockSize;

            // Create GravityField
            var gravityObj = new GameObject("GravityField");
            _gravityField = gravityObj.AddComponent<GravityField>();
            _gravityField.GravityConstant = GravityConstant;
            _gravityField.Theta = BarnesHutTheta;

            // Chunks root
            _chunksRoot = new GameObject("Chunks");

            // Generate sphere
            var filledBlocks = SphereGenerator.Generate(_chunkManager, SphereRadius, BlockSize);

            // Initialize gravity
            _gravityField.Initialize(_chunkManager, filledBlocks);

            // Create chunk renderers
            CreateChunkRenderers();

            // Listen for block changes to handle remeshing of neighbor chunks
            _chunkManager.OnBlockChanged += OnBlockChanged;

            // Spawn player
            SpawnPlayer();

            // Set up debug UI
            SetupDebugUI();
        }

        void CreateChunkRenderers()
        {
            foreach (var kvp in _chunkManager.Chunks)
            {
                CreateChunkRenderer(kvp.Key, kvp.Value);
            }
        }

        void CreateChunkRenderer(Vector3Int coord, Chunk chunk)
        {
            if (chunk.IsEmpty()) return;

            var obj = new GameObject($"Chunk({coord.x},{coord.y},{coord.z})");
            obj.transform.parent = _chunksRoot.transform;
            obj.AddComponent<MeshFilter>();
            obj.AddComponent<MeshRenderer>();
            var renderer = obj.AddComponent<ChunkRenderer>();
            renderer.Initialize(chunk, _chunkManager, _blockMaterial);
        }

        void SpawnPlayer()
        {
            _playerObj = new GameObject("Player");

            // Position on top of the sphere (surface is at ~radius + circumradius)
            float surfaceHeight = SphereRadius * BlockSize + 0.559f * BlockSize;
            Vector3 spawnPos = Vector3.up * (surfaceHeight + PlayerHeight);
            _playerObj.transform.position = spawnPos;

            // Player controller
            var controller = _playerObj.AddComponent<PlayerController>();
            controller.MoveSpeed = MoveSpeed;
            controller.JumpForce = JumpForce;

            // Player collision
            var collision = _playerObj.AddComponent<PlayerCollision>();
            collision.Height = PlayerHeight;
            collision.Radius = PlayerRadius;
            collision.Initialize(_chunkManager);

            // Camera
            var cameraObj = new GameObject("PlayerCamera");
            cameraObj.transform.parent = _playerObj.transform;
            var cam = cameraObj.AddComponent<Camera>();
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 200f;
            cam.fieldOfView = 75f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.05f, 0.05f, 0.1f);
            cameraObj.AddComponent<PlayerCamera>();

            // Remove the default camera
            var defaultCam = Camera.main;
            if (defaultCam != null && defaultCam.gameObject != cameraObj)
                Destroy(defaultCam.gameObject);

            // Block miner
            var miner = _playerObj.AddComponent<BlockMiner>();
            miner.Initialize(_chunkManager, cam);

            // Teleport with correct orientation
            controller.Teleport(spawnPos, Vector3.up);
        }

        void SetupDebugUI()
        {
            var debugObj = new GameObject("DebugUI");
            var debugUI = debugObj.AddComponent<DebugUI>();
            debugUI.Bootstrap = this;
            debugUI.GravityFieldRef = _gravityField;
            debugUI.Player = _playerObj.GetComponent<PlayerController>();
        }

        void OnBlockChanged(BlockAddress address, BlockType newType)
        {
            // Mark the chunk and its neighbors as dirty
            // (because removing a block can expose faces in adjacent chunks)
            var chunkCoord = address.GetChunkCoord(Chunk.Size);

            for (int dz = -1; dz <= 1; dz++)
            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                var neighborCoord = chunkCoord + new Vector3Int(dx, dy, dz);
                var chunk = _chunkManager.GetChunk(neighborCoord);
                if (chunk != null)
                    chunk.IsDirty = true;
            }
        }

        public void ResetScene()
        {
            RegenerateSphere(SphereRadius);
        }

        public void RegenerateSphere(int radius)
        {
            SphereRadius = radius;

            // Clear existing world
            _chunkManager.Clear();

            // Destroy chunk renderers
            if (_chunksRoot != null)
                Destroy(_chunksRoot);
            _chunksRoot = new GameObject("Chunks");

            // Regenerate
            var filledBlocks = SphereGenerator.Generate(_chunkManager, SphereRadius, BlockSize);
            _gravityField.Initialize(_chunkManager, filledBlocks);
            CreateChunkRenderers();

            // Reset player
            float surfaceHeight = SphereRadius * BlockSize + 0.559f * BlockSize;
            Vector3 spawnPos = Vector3.up * (surfaceHeight + PlayerHeight);
            var controller = _playerObj.GetComponent<PlayerController>();
            controller.Teleport(spawnPos, Vector3.up);
        }

        void OnDestroy()
        {
            if (_chunkManager != null)
                _chunkManager.OnBlockChanged -= OnBlockChanged;
        }
    }
}
