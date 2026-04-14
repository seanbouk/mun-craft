using System;
using UnityEngine;

namespace MunCraft.Core
{
    /// <summary>
    /// Identifies a single block on the BCC lattice.
    /// Parity 0 = Grid A (integer positions), Parity 1 = Grid B (offset by 0.5 in all axes).
    /// </summary>
    public readonly struct BlockAddress : IEquatable<BlockAddress>
    {
        public readonly byte Parity; // 0 = Grid A, 1 = Grid B
        public readonly int X;
        public readonly int Y;
        public readonly int Z;

        public BlockAddress(byte parity, int x, int y, int z)
        {
            Parity = parity;
            X = x;
            Y = y;
            Z = z;
        }

        public BlockAddress(int parity, int x, int y, int z)
            : this((byte)parity, x, y, z) { }

        public Vector3 ToWorldPosition(float blockSize)
        {
            float offset = Parity * 0.5f * blockSize;
            return new Vector3(
                X * blockSize + offset,
                Y * blockSize + offset,
                Z * blockSize + offset
            );
        }

        /// <summary>
        /// Find the nearest block address to a world position.
        /// Checks both grids and returns whichever is closer.
        /// </summary>
        public static BlockAddress FromWorldPosition(Vector3 worldPos, float blockSize)
        {
            // Grid A candidate: round to nearest integer
            int ax = Mathf.RoundToInt(worldPos.x / blockSize);
            int ay = Mathf.RoundToInt(worldPos.y / blockSize);
            int az = Mathf.RoundToInt(worldPos.z / blockSize);
            var gridA = new BlockAddress(0, ax, ay, az);

            // Grid B candidate: round to nearest integer after removing 0.5 offset
            float halfBlock = 0.5f * blockSize;
            int bx = Mathf.RoundToInt((worldPos.x - halfBlock) / blockSize);
            int by = Mathf.RoundToInt((worldPos.y - halfBlock) / blockSize);
            int bz = Mathf.RoundToInt((worldPos.z - halfBlock) / blockSize);
            var gridB = new BlockAddress(1, bx, by, bz);

            float distA = (gridA.ToWorldPosition(blockSize) - worldPos).sqrMagnitude;
            float distB = (gridB.ToWorldPosition(blockSize) - worldPos).sqrMagnitude;

            return distA <= distB ? gridA : gridB;
        }

        /// <summary>
        /// Returns all 14 neighbors: 6 same-grid (square faces) + 8 cross-grid (hex faces).
        /// </summary>
        public void GetNeighbors(Span<BlockAddress> neighbors)
        {
            if (neighbors.Length < 14)
                throw new ArgumentException("Span must have length >= 14");

            int i = 0;

            // 6 same-grid neighbors (across square faces): ±1 along each axis, same parity
            neighbors[i++] = new BlockAddress(Parity, X + 1, Y, Z);
            neighbors[i++] = new BlockAddress(Parity, X - 1, Y, Z);
            neighbors[i++] = new BlockAddress(Parity, X, Y + 1, Z);
            neighbors[i++] = new BlockAddress(Parity, X, Y - 1, Z);
            neighbors[i++] = new BlockAddress(Parity, X, Y, Z + 1);
            neighbors[i++] = new BlockAddress(Parity, X, Y, Z - 1);

            // 8 cross-grid neighbors (across hex faces): flip parity
            // For Grid A→B: offsets are (0,-1) in each axis
            // For Grid B→A: offsets are (0,+1) in each axis
            byte otherParity = (byte)(1 - Parity);
            int lo = Parity == 0 ? -1 : 0;
            int hi = lo + 1;

            for (int dz = lo; dz <= hi; dz++)
            for (int dy = lo; dy <= hi; dy++)
            for (int dx = lo; dx <= hi; dx++)
            {
                neighbors[i++] = new BlockAddress(otherParity, X + dx, Y + dy, Z + dz);
            }
        }

        /// <summary>
        /// Allocating version for convenience. Prefer the Span version in hot paths.
        /// </summary>
        public BlockAddress[] GetNeighbors()
        {
            var result = new BlockAddress[14];
            GetNeighbors(result);
            return result;
        }

        /// <summary>
        /// Which chunk this block belongs to, given a chunk size.
        /// </summary>
        public Vector3Int GetChunkCoord(int chunkSize)
        {
            return new Vector3Int(
                FloorDiv(X, chunkSize),
                FloorDiv(Y, chunkSize),
                FloorDiv(Z, chunkSize)
            );
        }

        /// <summary>
        /// Local index within a chunk (0..chunkSize-1 per axis).
        /// </summary>
        public (int lx, int ly, int lz) GetLocalIndex(int chunkSize)
        {
            return (
                ((X % chunkSize) + chunkSize) % chunkSize,
                ((Y % chunkSize) + chunkSize) % chunkSize,
                ((Z % chunkSize) + chunkSize) % chunkSize
            );
        }

        static int FloorDiv(int a, int b)
        {
            return a >= 0 ? a / b : (a - b + 1) / b;
        }

        // Equality and hashing
        public bool Equals(BlockAddress other)
            => Parity == other.Parity && X == other.X && Y == other.Y && Z == other.Z;

        public override bool Equals(object obj)
            => obj is BlockAddress other && Equals(other);

        public override int GetHashCode()
            => HashCode.Combine(Parity, X, Y, Z);

        public static bool operator ==(BlockAddress a, BlockAddress b) => a.Equals(b);
        public static bool operator !=(BlockAddress a, BlockAddress b) => !a.Equals(b);

        public override string ToString()
            => $"Block({(Parity == 0 ? "A" : "B")}, {X}, {Y}, {Z})";
    }
}
