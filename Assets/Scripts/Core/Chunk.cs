using UnityEngine;

namespace MunCraft.Core
{
    /// <summary>
    /// Stores block data for a fixed region of the BCC lattice.
    /// Two flat byte arrays — one for each grid parity.
    /// </summary>
    public class Chunk
    {
        public const int Size = 8; // blocks per axis per grid

        public readonly Vector3Int Coord; // chunk coordinate in chunk-space
        public readonly byte[] GridA; // Size^3 blocks
        public readonly byte[] GridB; // Size^3 blocks

        public bool IsDirty { get; set; }

        public Chunk(Vector3Int coord)
        {
            Coord = coord;
            GridA = new byte[Size * Size * Size];
            GridB = new byte[Size * Size * Size];
            IsDirty = true;
        }

        public BlockType GetBlock(byte parity, int lx, int ly, int lz)
        {
            byte[] grid = parity == 0 ? GridA : GridB;
            return (BlockType)grid[FlatIndex(lx, ly, lz)];
        }

        public void SetBlock(byte parity, int lx, int ly, int lz, BlockType type)
        {
            byte[] grid = parity == 0 ? GridA : GridB;
            grid[FlatIndex(lx, ly, lz)] = (byte)type;
            IsDirty = true;
        }

        static int FlatIndex(int x, int y, int z)
        {
            return x + Size * (y + Size * z);
        }

        /// <summary>
        /// Returns true if every block in both grids is Air.
        /// </summary>
        public bool IsEmpty()
        {
            for (int i = 0; i < GridA.Length; i++)
                if (GridA[i] != 0) return false;
            for (int i = 0; i < GridB.Length; i++)
                if (GridB[i] != 0) return false;
            return true;
        }
    }
}
