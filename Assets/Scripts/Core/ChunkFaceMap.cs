using System.Collections.Generic;

namespace MunCraft.Core
{
    public struct FaceVertexRange
    {
        public int Start;
        public int Count;
    }

    /// <summary>
    /// Per-block, per-face vertex ranges into a chunk's mesh.
    /// Built by ChunkMesher; consumed by anything that wants to mutate
    /// the appearance of specific faces (mining highlights, etc).
    /// </summary>
    public class ChunkFaceMap
    {
        // For each block, a 14-element array. Faces not emitted have Count==0.
        public readonly Dictionary<BlockAddress, FaceVertexRange[]> Blocks = new();

        public void RecordFace(BlockAddress address, int faceIdx, int vertStart, int vertCount)
        {
            if (!Blocks.TryGetValue(address, out var faces))
            {
                faces = new FaceVertexRange[14];
                Blocks[address] = faces;
            }
            faces[faceIdx] = new FaceVertexRange { Start = vertStart, Count = vertCount };
        }
    }
}
