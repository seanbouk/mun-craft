using MunCraft.Core;
using UnityEngine;

namespace MunCraft.Interaction
{
    /// <summary>
    /// Handles block mining: raycast from camera to find target block,
    /// click to destroy.
    /// </summary>
    public class BlockMiner : MonoBehaviour
    {
        [Header("Mining")]
        public float MiningRange = 5f;
        public float RayStepSize = 0.1f;

        [Header("Highlight")]
        public Color HighlightColor = new Color(1f, 1f, 1f, 0.3f);

        ChunkManager _chunkManager;
        Camera _camera;
        BlockAddress? _targetBlock;
        GameObject _highlightObj;
        MeshFilter _highlightMeshFilter;
        MeshRenderer _highlightMeshRenderer;

        public BlockAddress? TargetBlock => _targetBlock;

        public void Initialize(ChunkManager chunkManager, Camera camera)
        {
            _chunkManager = chunkManager;
            _camera = camera;
            CreateHighlightObject();
        }

        void Update()
        {
            if (_chunkManager == null || _camera == null) return;

            // Raycast to find target block
            _targetBlock = RaycastBlocks();

            UpdateHighlight();

            // Mine on click
            if (Input.GetMouseButtonDown(0) && _targetBlock.HasValue)
            {
                if (Cursor.lockState == CursorLockMode.Locked)
                {
                    _chunkManager.SetBlock(_targetBlock.Value, BlockType.Air);
                }
            }
        }

        /// <summary>
        /// Step along a ray from camera center, find the first solid block hit.
        /// </summary>
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

        void CreateHighlightObject()
        {
            _highlightObj = new GameObject("BlockHighlight");
            _highlightMeshFilter = _highlightObj.AddComponent<MeshFilter>();
            _highlightMeshRenderer = _highlightObj.AddComponent<MeshRenderer>();

            // Use a simple wireframe-ish material
            var mat = new Material(Shader.Find("MunCraft/FlatBlock"));
            mat.color = HighlightColor;
            _highlightMeshRenderer.material = mat;
            _highlightMeshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _highlightMeshRenderer.receiveShadows = false;
            _highlightObj.SetActive(false);
        }

        void UpdateHighlight()
        {
            if (!_targetBlock.HasValue)
            {
                _highlightObj.SetActive(false);
                return;
            }

            Vector3 worldPos = _targetBlock.Value.ToWorldPosition(_chunkManager.BlockSize);
            _highlightObj.transform.position = worldPos;

            // Build a slightly scaled-up truncated octahedron mesh for the highlight
            if (_highlightMeshFilter.sharedMesh == null)
            {
                _highlightMeshFilter.sharedMesh = BuildHighlightMesh(_chunkManager.BlockSize);
            }

            _highlightObj.SetActive(true);
        }

        static Mesh BuildHighlightMesh(float blockSize)
        {
            // Build a full truncated octahedron mesh (all 14 faces) slightly scaled up
            float scale = blockSize * 1.02f; // 2% larger than the block

            var vertices = new System.Collections.Generic.List<Vector3>();
            var triangles = new System.Collections.Generic.List<int>();
            var colors = new System.Collections.Generic.List<Color>();

            Color highlightCol = new Color(1f, 1f, 1f, 0.5f);

            for (int faceIdx = 0; faceIdx < 14; faceIdx++)
            {
                int[] faceVerts = MunCraft.Meshing.TruncOctGeometry.Faces[faceIdx];
                int baseVert = vertices.Count;

                for (int i = 0; i < faceVerts.Length; i++)
                {
                    vertices.Add(MunCraft.Meshing.TruncOctGeometry.Vertices[faceVerts[i]] * scale);
                    colors.Add(highlightCol);
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
            if (_highlightObj != null)
                Destroy(_highlightObj);
        }
    }
}
