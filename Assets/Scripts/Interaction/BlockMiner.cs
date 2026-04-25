using System.Collections.Generic;
using MunCraft.Core;
using MunCraft.Crafting;
using MunCraft.InventorySystem;
using MunCraft.Meshing;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MunCraft.Interaction
{
    /// <summary>
    /// Click-to-mine. While mining, the whole brick flashes between white and
    /// its original colour a fixed number of times spread over the mining
    /// duration. First flash starts on mouse-down; the brick is white at the
    /// moment of destruction.
    /// </summary>
    public class BlockMiner : MonoBehaviour
    {
        [Header("Mining")]
        public float MiningRange = 5f;
        public float RayStepSize = 0.1f;

        [Header("Highlight (when targeting, not mining)")]
        public Color HighlightColor = new Color(1f, 1f, 1f, 0.25f);

        [Header("Mining flash")]
        [Tooltip("Number of white flashes spread over the mining duration")]
        public int FlashCount = 12;
        public Color FlashColor = Color.white;

        ChunkManager _chunkManager;
        Camera _camera;
        Inventory _inventory;

        BlockAddress? _targetBlock;
        BlockAddress? _miningBlock;
        ChunkRenderer _miningRenderer;
        float _miningProgress;
        float _miningTime;
        int _lastFlashSegment;

        AudioClip[] _hitClips;
        AudioSource[] _hitPool;
        int _hitPoolHead;
        const int HitPoolSize = 16;

        // Highlight overlay (only used when targeting, not mining)
        GameObject _highlightObj;

        public BlockAddress? TargetBlock => _targetBlock;
        public float MiningProgress => _miningProgress;

        public void Initialize(ChunkManager chunkManager, Camera camera, Inventory inventory = null)
        {
            _chunkManager = chunkManager;
            _camera = camera;
            _inventory = inventory;
            CreateHighlight();
            SetupHitAudio();
        }

        void SetupHitAudio()
        {
            _hitClips = Resources.LoadAll<AudioClip>("Game/Hits");
            _hitPool = new AudioSource[HitPoolSize];
            for (int i = 0; i < HitPoolSize; i++)
            {
                var src = gameObject.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.spatialBlend = 0f;
                _hitPool[i] = src;
            }
        }

        void Update()
        {
            if (_chunkManager == null || _camera == null || GameState.MenuOpen) return;

            _targetBlock = RaycastBlocks();

            var mouse = Mouse.current;
            bool cursorReady = Cursor.lockState == CursorLockMode.Locked;
            bool holding = mouse != null && mouse.leftButton.isPressed && cursorReady;
            bool justClicked = mouse != null && mouse.leftButton.wasPressedThisFrame && cursorReady;

            // Stop mining on release or if the look target drifts
            if (_miningBlock.HasValue && (!holding || _targetBlock != _miningBlock))
                StopMining();

            // Coffee = continuous mining (hold to auto-start on next block)
            // Without coffee: require a fresh click per block
            bool hasCoffee = CraftingState.Instance != null && CraftingState.Instance.HasCoffee;
            bool canStart = hasCoffee ? holding : justClicked;

            if (canStart && _targetBlock.HasValue && _miningBlock == null)
                StartMining(_targetBlock.Value);

            // Continue mining
            if (_miningBlock.HasValue && _miningTime > 0)
            {
                _miningProgress += Time.deltaTime / _miningTime;

                if (_miningProgress >= 1f)
                {
                    var b = _miningBlock.Value;
                    var minedType = _chunkManager.GetBlock(b);
                    PlayCollectionHit(minedType);
                    StopMining(); // restore colors first
                    _chunkManager.SetBlock(b, BlockType.Air); // then destroy
                    _inventory?.AddBlock(minedType);
                    return;
                }

                int segment = Mathf.Clamp(Mathf.FloorToInt(_miningProgress * FlashCount), 0, FlashCount - 1);
                if (segment != _lastFlashSegment)
                {
                    PlayHit(_chunkManager.GetBlock(_miningBlock.Value), false);
                    _lastFlashSegment = segment;
                }

                ApplyFlashState();
            }

            UpdateHighlight();
        }

        void StartMining(BlockAddress addr)
        {
            _miningBlock = addr;
            float baseTime = _chunkManager.GetBlock(addr).GetMiningTime();
            // Each pick gives 50% cumulative speed boost
            int picks = CraftingState.Instance != null ? CraftingState.Instance.PickCount : 0;
            _miningTime = baseTime / (1f + 0.5f * picks);
            _miningProgress = 0f;
            _lastFlashSegment = -1;

            var chunkCoord = addr.GetChunkCoord(Chunk.Size);
            _miningRenderer = ChunkRendererRegistry.Get(chunkCoord);
        }

        void StopMining()
        {
            if (_miningBlock.HasValue && _miningRenderer != null)
                _miningRenderer.RestoreBlockColors(_miningBlock.Value);
            _miningBlock = null;
            _miningRenderer = null;
            _miningProgress = 0f;
            _miningTime = 0f;
            _lastFlashSegment = -1;
        }

        // Map mining time to "hardness" 0..1, then to pitch and volume so weaker
        // materials sound duller and quieter, harder ones brighter and fuller.
        static (float pitch, float volume) GetHitParams(BlockType type)
        {
            float t = type.GetMiningTime();
            float hardness = Mathf.InverseLerp(0.4f, 3.0f, t);
            float pitch = Mathf.Lerp(0.75f, 1.05f, hardness);
            float volume = Mathf.Lerp(0.45f, 0.9f, hardness);
            return (pitch, volume);
        }

        void PlayHit(BlockType type, bool collection)
        {
            if (_hitClips == null || _hitClips.Length == 0 || _hitPool == null) return;
            var (pitch, volume) = GetHitParams(type);
            if (collection) volume = Mathf.Min(volume * 1.6f, 1f);

            var src = _hitPool[_hitPoolHead];
            _hitPoolHead = (_hitPoolHead + 1) % _hitPool.Length;
            src.clip = _hitClips[Random.Range(0, _hitClips.Length)];
            src.pitch = pitch;
            src.volume = volume;
            src.Play();
        }

        void PlayCollectionHit(BlockType type)
        {
            PlayHit(type, true);
            StartCoroutine(EchoCollection(type));
        }

        System.Collections.IEnumerator EchoCollection(BlockType type)
        {
            yield return new WaitForSeconds(0.08f);
            if (_hitClips == null || _hitClips.Length == 0 || _hitPool == null) yield break;
            var (pitch, volume) = GetHitParams(type);
            volume = Mathf.Min(volume * 0.8f, 1f);

            var src = _hitPool[_hitPoolHead];
            _hitPoolHead = (_hitPoolHead + 1) % _hitPool.Length;
            src.clip = _hitClips[Random.Range(0, _hitClips.Length)];
            src.pitch = pitch * 0.92f; // a touch lower for the tail
            src.volume = volume;
            src.Play();
        }

        /// <summary>
        /// Whole-brick flash. Divides the mining duration into FlashCount equal
        /// segments. In each segment the brick is white for the first half and
        /// original for the second half — except the final segment, which stays
        /// white throughout so the brick is white at the moment of destruction.
        /// </summary>
        void ApplyFlashState()
        {
            if (_miningRenderer == null || !_miningBlock.HasValue || FlashCount < 1) return;

            float fracIntoMine = _miningProgress * FlashCount;
            int segment = Mathf.Clamp(Mathf.FloorToInt(fracIntoMine), 0, FlashCount - 1);
            float withinSegment = fracIntoMine - segment;

            bool white = segment >= FlashCount - 1 || withinSegment < 0.5f;

            if (white)
                _miningRenderer.SetBlockColor(_miningBlock.Value, FlashColor);
            else
                _miningRenderer.RestoreBlockColors(_miningBlock.Value);
        }

        BlockAddress? RaycastBlocks()
        {
            Ray ray = new Ray(_camera.transform.position, _camera.transform.forward);
            float distance = 0;
            while (distance < MiningRange)
            {
                Vector3 point = ray.GetPoint(distance);
                var address = BlockAddress.FromWorldPosition(point, _chunkManager.BlockSize);
                if (_chunkManager.IsSolid(address))
                    return address;
                distance += RayStepSize;
            }
            return null;
        }

        void CreateHighlight()
        {
            _highlightObj = new GameObject("BlockHighlight");
            var hf = _highlightObj.AddComponent<MeshFilter>();
            var hr = _highlightObj.AddComponent<MeshRenderer>();
            hf.sharedMesh = BuildSimpleBrick(_chunkManager.BlockSize, 1.02f);
            var hMat = new Material(Shader.Find("MunCraft/FlatBlock"));
            hMat.color = HighlightColor;
            hMat.SetFloat("_FogStrength", 0f);
            hMat.SetFloat("_VignetteStrength", 0f);
            hr.material = hMat;
            hr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            hr.receiveShadows = false;
            _highlightObj.SetActive(false);
        }

        void UpdateHighlight()
        {
            // Only show the white outline when targeting but not actively mining.
            // While mining, the brick's own face colors do the talking.
            bool show = _targetBlock.HasValue && !_miningBlock.HasValue;
            if (!show)
            {
                _highlightObj.SetActive(false);
                return;
            }

            Vector3 worldPos = _targetBlock.Value.ToWorldPosition(_chunkManager.BlockSize);
            _highlightObj.transform.position = worldPos;
            _highlightObj.SetActive(true);
        }

        static Mesh BuildSimpleBrick(float blockSize, float scale)
        {
            float s = blockSize * scale;
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            var colors = new List<Color>();

            for (int faceIdx = 0; faceIdx < 14; faceIdx++)
            {
                int[] faceVerts = TruncOctGeometry.Faces[faceIdx];
                int baseVert = vertices.Count;
                for (int i = 0; i < faceVerts.Length; i++)
                {
                    vertices.Add(TruncOctGeometry.Vertices[faceVerts[i]] * s);
                    colors.Add(Color.white);
                }
                for (int i = 0; i < faceVerts.Length - 2; i++)
                {
                    triangles.Add(baseVert);
                    triangles.Add(baseVert + i + 1);
                    triangles.Add(baseVert + i + 2);
                }
            }

            var mesh = new Mesh();
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.SetColors(colors);
            mesh.RecalculateNormals();
            return mesh;
        }

        void OnDestroy()
        {
            if (_miningBlock.HasValue && _miningRenderer != null)
                _miningRenderer.RestoreBlockColors(_miningBlock.Value);
            if (_highlightObj != null) Destroy(_highlightObj);
        }
    }
}
