using System;
using System.Collections.Generic;
using MunCraft.Core;
using UnityEngine;

namespace MunCraft.Meshing
{
    /// <summary>
    /// Generates a single Mesh for a chunk by emitting only exposed faces
    /// of truncated octahedron blocks. Also produces a face map so callers
    /// can find/edit specific block faces in the resulting mesh.
    /// </summary>
    public static class ChunkMesher
    {
        public static Mesh BuildMesh(Chunk chunk, ChunkManager chunkManager,
                                     out ChunkFaceMap faceMap, out Color[] originalColors)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            var colors = new List<Color>();
            var normals = new List<Vector3>();
            var blockCenters = new List<Vector3>(); // UV0 — block center for per-block fog/vignette

            faceMap = new ChunkFaceMap();
            Span<BlockAddress> neighbors = stackalloc BlockAddress[14];

            for (int parity = 0; parity <= 1; parity++)
            {
                for (int lz = 0; lz < Chunk.Size; lz++)
                for (int ly = 0; ly < Chunk.Size; ly++)
                for (int lx = 0; lx < Chunk.Size; lx++)
                {
                    BlockType type = chunk.GetBlock((byte)parity, lx, ly, lz);
                    if (type == BlockType.Air) continue;

                    int gx = chunk.Coord.x * Chunk.Size + lx;
                    int gy = chunk.Coord.y * Chunk.Size + ly;
                    int gz = chunk.Coord.z * Chunk.Size + lz;
                    var address = new BlockAddress(parity, gx, gy, gz);

                    Vector3 blockWorldPos = address.ToWorldPosition(chunkManager.BlockSize);
                    Color blockColor = type.GetColor();

                    address.GetNeighbors(neighbors);

                    for (int faceIdx = 0; faceIdx < 14; faceIdx++)
                    {
                        int neighborIdx = TruncOctGeometry.FaceToNeighborIndex[faceIdx];
                        BlockAddress neighborAddr = neighbors[neighborIdx];

                        if (chunkManager.GetBlock(neighborAddr).IsSolid())
                            continue;

                        int vertStart = vertices.Count;
                        EmitFace(faceIdx, blockWorldPos, blockColor, chunkManager.BlockSize,
                                 vertices, triangles, colors, normals, blockCenters);
                        int vertCount = vertices.Count - vertStart;
                        faceMap.RecordFace(address, faceIdx, vertStart, vertCount);
                    }
                }
            }

            if (vertices.Count == 0)
            {
                originalColors = System.Array.Empty<Color>();
                return null;
            }

            var mesh = new Mesh();
            if (vertices.Count > 65535)
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.SetColors(colors);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, blockCenters); // block center for per-block fog/vignette in shader

            originalColors = colors.ToArray();
            return mesh;
        }

        static void EmitFace(int faceIdx, Vector3 blockWorldPos, Color color, float blockSize,
            List<Vector3> vertices, List<int> triangles, List<Color> colors,
            List<Vector3> normals, List<Vector3> blockCenters)
        {
            int[] faceVertIndices = TruncOctGeometry.Faces[faceIdx];
            Vector3 faceNormal = TruncOctGeometry.FaceNormals[faceIdx];

            int baseVertex = vertices.Count;

            for (int i = 0; i < faceVertIndices.Length; i++)
            {
                Vector3 localVert = TruncOctGeometry.Vertices[faceVertIndices[i]];
                vertices.Add(blockWorldPos + localVert * blockSize);
                colors.Add(color);
                normals.Add(faceNormal);
                blockCenters.Add(blockWorldPos);
            }

            int vertCount = faceVertIndices.Length;
            for (int i = 0; i < vertCount - 2; i++)
            {
                triangles.Add(baseVertex);
                triangles.Add(baseVertex + i + 1);
                triangles.Add(baseVertex + i + 2);
            }
        }
    }
}
