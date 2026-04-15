using MunCraft.Core;
using UnityEngine;

namespace MunCraft.Meshing
{
    /// <summary>
    /// Owns one chunk's mesh. Provides per-block-face color editing so other
    /// systems can re-color individual faces without rebuilding the mesh.
    /// </summary>
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public class ChunkRenderer : MonoBehaviour
    {
        Chunk _chunk;
        ChunkManager _chunkManager;
        MeshFilter _meshFilter;
        MeshRenderer _meshRenderer;
        Mesh _mesh;
        ChunkFaceMap _faceMap;
        Color[] _baseColors;
        Color[] _workingColors;
        bool _colorsDirty;

        public ChunkFaceMap FaceMap => _faceMap;

        public void Initialize(Chunk chunk, ChunkManager chunkManager, Material material)
        {
            _chunk = chunk;
            _chunkManager = chunkManager;

            _meshFilter = GetComponent<MeshFilter>();
            _meshRenderer = GetComponent<MeshRenderer>();
            _meshRenderer.material = material;
            _meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _meshRenderer.receiveShadows = false;

            ChunkRendererRegistry.Register(_chunk.Coord, this);
            Remesh();
        }

        void LateUpdate()
        {
            if (_chunk != null && _chunk.IsDirty)
            {
                Remesh();
                _chunk.IsDirty = false;
            }

            if (_colorsDirty && _mesh != null)
            {
                _mesh.SetColors(_workingColors);
                _colorsDirty = false;
            }
        }

        public void Remesh()
        {
            if (_chunk == null || _chunkManager == null) return;

            if (_mesh != null) Destroy(_mesh);

            _mesh = ChunkMesher.BuildMesh(_chunk, _chunkManager, out _faceMap, out _baseColors);
            _meshFilter.sharedMesh = _mesh;

            if (_baseColors != null && _baseColors.Length > 0)
            {
                _workingColors = (Color[])_baseColors.Clone();
            }
            else
            {
                _workingColors = null;
            }
            _colorsDirty = false;
        }

        /// <summary>
        /// Override the colors of a specific face on a block.
        /// No-op if the block isn't in this chunk's face map.
        /// </summary>
        public void SetFaceColor(BlockAddress addr, int faceIdx, Color color)
        {
            if (_faceMap == null || _workingColors == null) return;
            if (!_faceMap.Blocks.TryGetValue(addr, out var faces)) return;
            var range = faces[faceIdx];
            if (range.Count == 0) return;
            for (int i = 0; i < range.Count; i++)
                _workingColors[range.Start + i] = color;
            _colorsDirty = true;
        }

        /// <summary>
        /// Restore the original color for a specific face.
        /// </summary>
        public void RestoreFaceColor(BlockAddress addr, int faceIdx)
        {
            if (_faceMap == null || _workingColors == null) return;
            if (!_faceMap.Blocks.TryGetValue(addr, out var faces)) return;
            var range = faces[faceIdx];
            if (range.Count == 0) return;
            for (int i = 0; i < range.Count; i++)
                _workingColors[range.Start + i] = _baseColors[range.Start + i];
            _colorsDirty = true;
        }

        /// <summary>
        /// Restore all of a block's faces to their original colors.
        /// </summary>
        public void RestoreBlockColors(BlockAddress addr)
        {
            if (_faceMap == null || _workingColors == null) return;
            if (!_faceMap.Blocks.TryGetValue(addr, out var faces)) return;
            for (int f = 0; f < 14; f++)
            {
                var range = faces[f];
                if (range.Count == 0) continue;
                for (int i = 0; i < range.Count; i++)
                    _workingColors[range.Start + i] = _baseColors[range.Start + i];
            }
            _colorsDirty = true;
        }

        void OnDestroy()
        {
            if (_chunk != null)
                ChunkRendererRegistry.Unregister(_chunk.Coord);
            if (_mesh != null)
                Destroy(_mesh);
        }
    }
}
