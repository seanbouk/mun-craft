using MunCraft.Core;
using UnityEngine;

namespace MunCraft.Meshing
{
    /// <summary>
    /// MonoBehaviour that lives on each chunk's GameObject.
    /// Holds MeshFilter + MeshRenderer and remeshes when the chunk is dirty.
    /// </summary>
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public class ChunkRenderer : MonoBehaviour
    {
        Chunk _chunk;
        ChunkManager _chunkManager;
        MeshFilter _meshFilter;
        MeshRenderer _meshRenderer;

        public void Initialize(Chunk chunk, ChunkManager chunkManager, Material material)
        {
            _chunk = chunk;
            _chunkManager = chunkManager;

            _meshFilter = GetComponent<MeshFilter>();
            _meshRenderer = GetComponent<MeshRenderer>();
            _meshRenderer.material = material;
            _meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _meshRenderer.receiveShadows = false;

            Remesh();
        }

        void LateUpdate()
        {
            if (_chunk != null && _chunk.IsDirty)
            {
                Remesh();
                _chunk.IsDirty = false;
            }
        }

        public void Remesh()
        {
            if (_chunk == null || _chunkManager == null)
                return;

            // Destroy old mesh
            if (_meshFilter.sharedMesh != null)
            {
                Destroy(_meshFilter.sharedMesh);
            }

            Mesh mesh = ChunkMesher.BuildMesh(_chunk, _chunkManager);
            _meshFilter.sharedMesh = mesh;
        }

        void OnDestroy()
        {
            if (_meshFilter != null && _meshFilter.sharedMesh != null)
                Destroy(_meshFilter.sharedMesh);
        }
    }
}
