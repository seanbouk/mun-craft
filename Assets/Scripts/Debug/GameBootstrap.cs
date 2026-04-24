using System.Collections.Generic;
using MunCraft.Core;
using MunCraft.Crafting;
using MunCraft.Gravity;
using MunCraft.Interaction;
using MunCraft.InventorySystem;
using MunCraft.MapGen;
using MunCraft.Meshing;
using MunCraft.Player;
using MunCraft.UI;
using UnityEngine;

namespace MunCraft.Debug
{
    /// <summary>
    /// Wires up the game world. Start() just creates the flow manager;
    /// actual world generation is deferred to LoadMap() which the flow
    /// manager calls when a map is selected.
    /// </summary>
    public class GameBootstrap : MonoBehaviour
    {
        [Header("World")]
        public int SphereRadius = 12;
        public float BlockSize = 1.0f;

        [Header("Gravity")]
        public float GravityConstant = 0.2f;
        public float BarnesHutTheta = 0.5f;

        [Header("Player")]
        public float PlayerHeight = 1.8f;
        public float PlayerRadius = 0.3f;
        public float MoveSpeed = 5f;
        public float JumpForce = 5f;

        [Header("Streaming")]
        public float RenderDistance = 30f;

        ChunkManager _chunkManager;
        GravityField _gravityField;
        Inventory _inventory;
        ChunkStreamingManager _streamingManager;
        GameObject _playerObj;
        GameObject _chunksRoot;
        Material _blockMaterial;
        GameObject _menuObj;
        GameObject _debugObj;
        GameObject _inventoryObj;

        int _currentMapId;
        MapResult _spawnResult;

        // Reused buffer for OnBlockChanged
        readonly HashSet<Vector3Int> _dirtyChunkScratch = new();

        public ChunkManager ChunkManager => _chunkManager;
        public static GameBootstrap Instance { get; private set; }

        void Awake() { Instance = this; }

        void Start()
        {
            // Create shared material (needed before any map load)
            var shader = Shader.Find("MunCraft/FlatBlock");
            if (shader == null)
            {
                UnityEngine.Debug.LogError("FlatBlock shader not found! Using fallback.");
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            }
            _blockMaterial = new Material(shader);

            // Persistent inventory + crafting state (survives map changes)
            _inventoryObj = new GameObject("Inventory");
            _inventory = _inventoryObj.AddComponent<Inventory>();
            _inventoryObj.AddComponent<InventoryUI>().Inventory = _inventory;
            _inventoryObj.AddComponent<CraftingState>();
            _inventoryObj.SetActive(false);

            // Persistent side menus (survives map changes)
            _menuObj = new GameObject("SideMenus");
            _menuObj.AddComponent<SideMenuManager>();
            _menuObj.AddComponent<MachinesMenuUI>().Inventory = _inventory;
            _menuObj.AddComponent<GameMenuUI>();
            _menuObj.SetActive(false);

            // Create the flow manager — it shows the title screen
            // and calls LoadMap() when the player picks a level
            var flowObj = new GameObject("GameFlow");
            flowObj.AddComponent<GameFlowManager>();
        }

        void Update()
        {
            if (_playerObj != null)
                Shader.SetGlobalVector("_MunPlayerPos", _playerObj.transform.position);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (_chunkManager != null)
                _chunkManager.OnBlockChanged -= OnBlockChanged;
        }

        // ==============================================================
        //  LoadMap / UnloadMap — called by GameFlowManager
        // ==============================================================

        public void LoadMap(int mapId)
        {
            // ChunkManager
            var chunkManagerObj = new GameObject("ChunkManager");
            _chunkManager = chunkManagerObj.AddComponent<ChunkManager>();
            _chunkManager.BlockSize = BlockSize;

            // GravityField
            var gravityObj = new GameObject("GravityField");
            _gravityField = gravityObj.AddComponent<GravityField>();
            _gravityField.GravityConstant = GravityConstant;
            _gravityField.Theta = BarnesHutTheta;

            // Chunks root
            _chunksRoot = new GameObject("Chunks");

            // Generate world based on map selection
            _currentMapId = mapId;
            var result = GenerateMap(mapId);
            _spawnResult = result;
            _gravityField.Initialize(_chunkManager, result.FilledBlocks);

            // Block change listener
            _chunkManager.OnBlockChanged += OnBlockChanged;

            // Show the persistent inventory/crafting UI
            _inventoryObj.SetActive(true);

            // Player
            SpawnPlayer();

            // Streaming
            var streamObj = new GameObject("ChunkStreaming");
            _streamingManager = streamObj.AddComponent<ChunkStreamingManager>();
            _streamingManager.RenderDistance = RenderDistance;
            _streamingManager.Initialize(_chunkManager, _playerObj.transform,
                                         _blockMaterial, _chunksRoot);
            _streamingManager.ForceLoadNearby();

            // Debug UI
            SetupDebugUI();

            // Show persistent menus
            _menuObj.SetActive(true);
            // Ensure any open panels are closed from previous session
            var sideMgr = _menuObj.GetComponent<SideMenuManager>();
            if (sideMgr != null) sideMgr.CloseAll();

            // Lock cursor for gameplay
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        public void UnloadMap()
        {
            if (_chunkManager != null)
                _chunkManager.OnBlockChanged -= OnBlockChanged;

            // Hide persistent UI, destroy map-specific objects
            if (_menuObj != null) _menuObj.SetActive(false);
            SafeDestroy(_debugObj);
            SafeDestroy(_playerObj);
            SafeDestroy(_chunksRoot);
            if (_streamingManager != null) SafeDestroy(_streamingManager.gameObject);
            if (_chunkManager != null) SafeDestroy(_chunkManager.gameObject);
            if (_gravityField != null) SafeDestroy(_gravityField.gameObject);

            // Hide inventory UI (but keep the data)
            if (_inventoryObj != null) _inventoryObj.SetActive(false);

            _debugObj = null;
            _playerObj = null;
            _chunksRoot = null;
            _streamingManager = null;
            _chunkManager = null;
            _gravityField = null;
            _inventory = null;

            ChunkRendererRegistry.Clear();
            GameState.MenuOpen = false;
            RenderSettings.skybox = null;
        }

        static void SafeDestroy(GameObject obj)
        {
            if (obj != null) Destroy(obj);
        }

        // ==============================================================
        //  Internal helpers (same as before)
        // ==============================================================

        MapResult GenerateMap(int mapId)
        {
            switch (mapId)
            {
                case 0: return RoundWorldGen.Generate(_chunkManager, BlockSize, SphereRadius);
                case 1: return DonutWorldGen.Generate(_chunkManager, BlockSize);
                case 2: return PeanutWorldGen.Generate(_chunkManager, BlockSize);
                case 3: return WorldsWorldGen.Generate(_chunkManager, BlockSize);
                default: return RoundWorldGen.Generate(_chunkManager, BlockSize, SphereRadius);
            }
        }

        void SpawnPlayer()
        {
            _playerObj = new GameObject("Player");

            Vector3 spawnPos = _spawnResult.SpawnPosition;
            _playerObj.transform.position = spawnPos;

            var collision = _playerObj.AddComponent<PlayerCollision>();
            collision.Height = PlayerHeight;
            collision.Radius = PlayerRadius;
            collision.Initialize(_chunkManager);

            var controller = _playerObj.AddComponent<PlayerController>();
            controller.MoveSpeed = MoveSpeed;
            controller.JumpForce = JumpForce;

            var cameraObj = new GameObject("PlayerCamera");
            cameraObj.transform.parent = _playerObj.transform;
            var cam = cameraObj.AddComponent<Camera>();
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 200f;
            cam.fieldOfView = 75f;
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.backgroundColor = new Color(0.01f, 0.01f, 0.06f);
            cameraObj.AddComponent<PlayerCamera>();

            var skyShader = Shader.Find("MunCraft/Sky");
            if (skyShader != null)
            {
                var skyMat = new Material(skyShader);
                ApplySkyColors(skyMat, _currentMapId);
                RenderSettings.skybox = skyMat;
            }

            // Remove the default camera if one exists
            var defaultCam = Camera.main;
            if (defaultCam != null && defaultCam.gameObject != cameraObj)
                Destroy(defaultCam.gameObject);

            var miner = _playerObj.AddComponent<BlockMiner>();
            miner.Initialize(_chunkManager, cam, _inventory);

            controller.Teleport(spawnPos, _spawnResult.SpawnUp);
        }

        static void ApplySkyColors(Material skyMat, int mapId)
        {
            Color bottom, top;
            switch (mapId)
            {
                case 0: // Round World — azure/deep blue (shader defaults)
                    bottom = new Color(0.35f, 0.55f, 0.75f);
                    top = new Color(0.01f, 0.01f, 0.06f);
                    break;
                case 1: // Donut World — pink, pale bottom to bright top
                    bottom = new Color(0.90f, 0.80f, 0.82f);
                    top = new Color(0.85f, 0.25f, 0.45f);
                    break;
                case 2: // Peanut World — dark purple
                    bottom = new Color(0.15f, 0.08f, 0.25f);
                    top = new Color(0.03f, 0.01f, 0.06f);
                    break;
                case 3: // Worlds World — mustard bottom, black top
                    bottom = new Color(0.50f, 0.42f, 0.15f);
                    top = new Color(0f, 0f, 0f);
                    break;
                default:
                    return;
            }
            skyMat.SetColor("_AzureColor", bottom);
            skyMat.SetColor("_DeepBlueColor", top);
        }

        void SetupDebugUI()
        {
            _debugObj = new GameObject("DebugUI");
            var debugUI = _debugObj.AddComponent<DebugUI>();
            debugUI.Bootstrap = this;
            debugUI.GravityFieldRef = _gravityField;
            debugUI.Player = _playerObj.GetComponent<PlayerController>();
            debugUI.StreamingManager = _streamingManager;
        }

        void OnBlockChanged(BlockAddress address, BlockType newType)
        {
            _dirtyChunkScratch.Clear();
            _dirtyChunkScratch.Add(address.GetChunkCoord(Chunk.Size));

            System.Span<BlockAddress> neighbors = stackalloc BlockAddress[14];
            address.GetNeighbors(neighbors);
            for (int i = 0; i < 14; i++)
                _dirtyChunkScratch.Add(neighbors[i].GetChunkCoord(Chunk.Size));

            foreach (var coord in _dirtyChunkScratch)
            {
                var c = _chunkManager.GetChunk(coord);
                if (c != null) c.IsDirty = true;
            }
        }

        public void ResetScene() => RegenerateSphere(SphereRadius);

        public void RegenerateSphere(int radius)
        {
            SphereRadius = radius;
            _chunkManager.Clear();

            if (_chunksRoot != null) Destroy(_chunksRoot);
            _chunksRoot = new GameObject("Chunks");

            var result = GenerateMap(_currentMapId);
            _spawnResult = result;
            _gravityField.Initialize(_chunkManager, result.FilledBlocks);

            var controller = _playerObj.GetComponent<PlayerController>();
            controller.Teleport(result.SpawnPosition, result.SpawnUp);

            if (_streamingManager != null)
            {
                _streamingManager.Initialize(_chunkManager, _playerObj.transform,
                                             _blockMaterial, _chunksRoot);
                _streamingManager.ForceLoadNearby();
            }
        }
    }
}
