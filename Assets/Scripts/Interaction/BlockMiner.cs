using System.Collections.Generic;
using MunCraft.Core;
using MunCraft.Meshing;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MunCraft.Interaction
{
    /// <summary>
    /// Hold-to-mine. Visual feedback while mining: each exposed face of the
    /// targeted block independently toggles between white and its original
    /// colour at a re-roll rate that accelerates as mining progresses.
    /// Mutates the actual chunk mesh's vertex colors — no overlay geometry.
    /// </summary>
    public class BlockMiner : MonoBehaviour
    {
        [Header("Mining")]
        public float MiningRange = 5f;
        public float RayStepSize = 0.1f;

        [Header("Highlight (when targeting, not mining)")]
        public Color HighlightColor = new Color(1f, 1f, 1f, 0.25f);

        [Header("Mining flash")]
        [Tooltip("Re-roll period at progress=0 (seconds)")]
        public float FlashStartPeriod = 0.18f;
        [Tooltip("Re-roll period at progress=1 (seconds)")]
        public float FlashEndPeriod = 0.025f;
        [Tooltip("Probability a face is lit on each re-roll")]
        [Range(0f, 1f)]
        public float LitProbability = 0.5f;
        public Color LitColor = new Color(1f, 1f, 1f, 1f);

        [Header("Jutter (drill shake) — set 0 to disable")]
        public float JutterAmplitude = 0f;
        public float JutterStartPeriod = 0.06f;
        public float JutterEndPeriod = 0.015f;

        ChunkManager _chunkManager;
        Camera _camera;

        BlockAddress? _targetBlock;
        BlockAddress? _miningBlock;
        ChunkRenderer _miningRenderer;
        float _miningProgress;
        float _miningTime;
        float _lastFlashTime;

        // Highlight overlay (only used when targeting, not mining)
        GameObject _highlightObj;

        // Jutter
        Vector3 _jutterOffset;
        float _lastJutterTime;

        public BlockAddress? TargetBlock => _targetBlock;
        public float MiningProgress => _miningProgress;

        public void Initialize(ChunkManager chunkManager, Camera camera)
        {
            _chunkManager = chunkManager;
            _camera = camera;
            CreateHighlight();
        }

        void Update()
        {
            if (_chunkManager == null || _camera == null) return;

            _targetBlock = RaycastBlocks();

            var mouse = Mouse.current;
            bool holding = mouse != null
                           && mouse.leftButton.isPressed
                           && Cursor.lockState == CursorLockMode.Locked;

            // Reset on release or target change
            if (!holding || _targetBlock != _miningBlock)
            {
                StopMining();
            }

            // Start / continue mining
            if (holding && _targetBlock.HasValue)
            {
                if (_miningBlock == null)
                    StartMining(_targetBlock.Value);

                if (_miningTime > 0)
                {
                    _miningProgress += Time.deltaTime / _miningTime;

                    if (_miningProgress >= 1f)
                    {
                        var b = _miningBlock.Value;
                        StopMining(); // restore colors first
                        _chunkManager.SetBlock(b, BlockType.Air); // then destroy
                        return;
                    }

                    UpdateFlash();
                }
            }

            UpdateHighlight();
        }

        void StartMining(BlockAddress addr)
        {
            _miningBlock = addr;
            _miningTime = _chunkManager.GetBlock(addr).GetMiningTime();
            _miningProgress = 0f;
            _lastFlashTime = -999f;

            var chunkCoord = addr.GetChunkCoord(Chunk.Size);
            _miningRenderer = ChunkRendererRegistry.Get(chunkCoord);
        }

        void StopMining()
        {
            if (_miningBlock.HasValue && _miningRenderer != null)
            {
                _miningRenderer.RestoreBlockColors(_miningBlock.Value);
            }
            _miningBlock = null;
            _miningRenderer = null;
            _miningProgress = 0f;
            _miningTime = 0f;
        }

        void UpdateFlash()
        {
            if (_miningRenderer == null || !_miningBlock.HasValue) return;
            if (!_miningRenderer.FaceMap.Blocks.TryGetValue(_miningBlock.Value, out var faces))
                return;

            float period = Mathf.Lerp(FlashStartPeriod, FlashEndPeriod, _miningProgress);
            if (Time.time - _lastFlashTime < period) return;
            _lastFlashTime = Time.time;

            // Random per-face: lit (white) or unlit (restore original)
            for (int f = 0; f < 14; f++)
            {
                if (faces[f].Count == 0) continue; // face not in mesh
                if (Random.value < LitProbability)
                    _miningRenderer.SetFaceColor(_miningBlock.Value, f, LitColor);
                else
                    _miningRenderer.RestoreFaceColor(_miningBlock.Value, f);
            }
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
            hr.material = hMat;
            hr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            hr.receiveShadows = false;
            _highlightObj.SetActive(false);
        }

        void UpdateHighlight()
        {
            // Show the wire-ish highlight when targeting but NOT actively mining
            // (during mining, the face colors do the talking)
            bool show = _targetBlock.HasValue && !_miningBlock.HasValue;

            if (!show)
            {
                _highlightObj.SetActive(false);
                return;
            }

            Vector3 worldPos = _targetBlock.Value.ToWorldPosition(_chunkManager.BlockSize);

            // Jutter (off by default)
            Vector3 offset = Vector3.zero;
            if (JutterAmplitude > 0 && _miningBlock.HasValue)
            {
                float period = Mathf.Lerp(JutterStartPeriod, JutterEndPeriod, _miningProgress);
                if (Time.time - _lastJutterTime >= period)
                {
                    _jutterOffset = Random.insideUnitSphere * JutterAmplitude;
                    _lastJutterTime = Time.time;
                }
                offset = _jutterOffset;
            }

            _highlightObj.transform.position = worldPos + offset;
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
