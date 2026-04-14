using UnityEngine;

namespace MunCraft.Meshing
{
    /// <summary>
    /// Defines the geometry of a unit truncated octahedron centered at the origin.
    /// The shape has 24 vertices, 8 hexagonal faces, and 6 square faces (14 total).
    ///
    /// For a BCC lattice with blockSize=1, the Voronoi cell (truncated octahedron) has
    /// vertices at all permutations of (0, ±0.25, ±0.5) — scaled from the standard form.
    /// </summary>
    public static class TruncOctGeometry
    {
        // The 24 vertices of a truncated octahedron, Voronoi cell of BCC lattice with unit spacing.
        // These are all permutations of (0, ±1/4, ±1/2).
        public static readonly Vector3[] Vertices = GenerateVertices();

        // 14 faces: indices into the Vertices array.
        // Faces[0..5] = 6 square faces (4 vertices each)
        // Faces[6..13] = 8 hexagonal faces (6 vertices each)
        public static readonly int[][] Faces = GenerateFaces();

        // Normal direction for each face (outward).
        public static readonly Vector3[] FaceNormals = GenerateFaceNormals();

        // Which neighbor direction each face corresponds to.
        // Faces 0-5 (squares): same-grid neighbors along ±X, ±Y, ±Z
        // Faces 6-13 (hexagons): cross-grid neighbors
        public static readonly int[] FaceToNeighborIndex = GenerateFaceNeighborMap();

        static Vector3[] GenerateVertices()
        {
            // All permutations of (0, ±0.25, ±0.5)
            // That's 3 choices for which axis is 0 × 2 signs for 0.25 × 2 signs for 0.5 × 2 arrangements = 24
            var verts = new Vector3[24];
            int i = 0;

            float a = 0.25f;
            float b = 0.5f;

            // For each axis being zero
            // Axis X = 0: permutations of (0, ±a, ±b) and (0, ±b, ±a)
            for (int sy = -1; sy <= 1; sy += 2)
            for (int sz = -1; sz <= 1; sz += 2)
            {
                verts[i++] = new Vector3(0, sy * a, sz * b);
                verts[i++] = new Vector3(0, sy * b, sz * a);
            }

            // Axis Y = 0: permutations of (±a, 0, ±b) and (±b, 0, ±a)
            for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2)
            {
                verts[i++] = new Vector3(sx * a, 0, sz * b);
                verts[i++] = new Vector3(sx * b, 0, sz * a);
            }

            // Axis Z = 0: permutations of (±a, ±b, 0) and (±b, ±a, 0)
            for (int sx = -1; sx <= 1; sx += 2)
            for (int sy = -1; sy <= 1; sy += 2)
            {
                verts[i++] = new Vector3(sx * a, sy * b, 0);
                verts[i++] = new Vector3(sx * b, sy * a, 0);
            }

            return verts;
        }

        static int[][] GenerateFaces()
        {
            // Build faces by grouping vertices that share a face.
            // Square faces: perpendicular to each axis (6 faces, 4 verts each)
            // Hex faces: perpendicular to (±1,±1,±1) directions (8 faces, 6 verts each)

            var faces = new int[14][];
            var verts = Vertices;

            // Square faces: the 4 vertices closest to each axis direction
            // +X face: vertices with largest X component
            // These are the vertices where the axis component = ±0.5 and the face normal axis = ±0.5
            faces[0] = FindSquareFace(verts, Vector3.right);     // +X
            faces[1] = FindSquareFace(verts, Vector3.left);      // -X
            faces[2] = FindSquareFace(verts, Vector3.up);        // +Y
            faces[3] = FindSquareFace(verts, Vector3.down);      // -Y
            faces[4] = FindSquareFace(verts, Vector3.forward);   // +Z
            faces[5] = FindSquareFace(verts, Vector3.back);      // -Z

            // Hex faces: 8 faces in the (±1,±1,±1) directions
            // Loop order MUST match BlockAddress.GetNeighbors: dz outer, dy middle, dx inner
            int fi = 6;
            for (int sz = -1; sz <= 1; sz += 2)
            for (int sy = -1; sy <= 1; sy += 2)
            for (int sx = -1; sx <= 1; sx += 2)
            {
                Vector3 dir = new Vector3(sx, sy, sz).normalized;
                faces[fi++] = FindHexFace(verts, dir);
            }

            return faces;
        }

        static int[] FindSquareFace(Vector3[] verts, Vector3 normal)
        {
            // Square face vertices are the 4 closest to the face direction
            // They have the max dot product with the normal
            var indices = new System.Collections.Generic.List<int>();
            float maxDot = float.MinValue;

            // Find the maximum dot product
            for (int i = 0; i < verts.Length; i++)
            {
                float d = Vector3.Dot(verts[i], normal);
                if (d > maxDot) maxDot = d;
            }

            // Collect vertices at that max (with tolerance)
            for (int i = 0; i < verts.Length; i++)
            {
                float d = Vector3.Dot(verts[i], normal);
                if (d > maxDot - 0.001f)
                    indices.Add(i);
            }

            // Sort vertices in winding order around the face center
            SortFaceVertices(verts, indices, normal);
            return indices.ToArray();
        }

        static int[] FindHexFace(Vector3[] verts, Vector3 normal)
        {
            // Hex face vertices: 6 vertices closest to the diagonal direction
            var candidates = new System.Collections.Generic.List<(int index, float dot)>();

            for (int i = 0; i < verts.Length; i++)
            {
                float d = Vector3.Dot(verts[i], normal);
                candidates.Add((i, d));
            }

            candidates.Sort((a, b) => b.dot.CompareTo(a.dot));

            var indices = new System.Collections.Generic.List<int>();
            for (int i = 0; i < 6; i++)
                indices.Add(candidates[i].index);

            SortFaceVertices(verts, indices, normal);
            return indices.ToArray();
        }

        static void SortFaceVertices(Vector3[] verts, System.Collections.Generic.List<int> indices, Vector3 normal)
        {
            // Sort vertices in counter-clockwise order when viewed from outside
            Vector3 center = Vector3.zero;
            for (int i = 0; i < indices.Count; i++)
                center += verts[indices[i]];
            center /= indices.Count;

            // Build a local coordinate frame on the face
            Vector3 up = Vector3.Cross(normal, Vector3.right);
            if (up.sqrMagnitude < 0.01f)
                up = Vector3.Cross(normal, Vector3.up);
            up.Normalize();
            Vector3 right = Vector3.Cross(up, normal).normalized;

            indices.Sort((a, b) =>
            {
                Vector3 da = verts[a] - center;
                Vector3 db = verts[b] - center;
                float angleA = Mathf.Atan2(Vector3.Dot(da, up), Vector3.Dot(da, right));
                float angleB = Mathf.Atan2(Vector3.Dot(db, up), Vector3.Dot(db, right));
                return angleA.CompareTo(angleB);
            });
        }

        static Vector3[] GenerateFaceNormals()
        {
            var normals = new Vector3[14];

            // Square faces: axis-aligned
            normals[0] = Vector3.right;
            normals[1] = Vector3.left;
            normals[2] = Vector3.up;
            normals[3] = Vector3.down;
            normals[4] = Vector3.forward;
            normals[5] = Vector3.back;

            // Hex faces: diagonal directions
            // Loop order MUST match BlockAddress.GetNeighbors: dz outer, dy middle, dx inner
            int i = 6;
            for (int sz = -1; sz <= 1; sz += 2)
            for (int sy = -1; sy <= 1; sy += 2)
            for (int sx = -1; sx <= 1; sx += 2)
            {
                normals[i++] = new Vector3(sx, sy, sz).normalized;
            }

            return normals;
        }

        static int[] GenerateFaceNeighborMap()
        {
            // Maps face index to neighbor index in BlockAddress.GetNeighbors() output.
            // GetNeighbors returns: [0-5] = same-grid ±X,±Y,±Z, [6-13] = cross-grid 8 combos
            // Face indices: [0-5] = square faces ±X,±Y,±Z, [6-13] = hex faces

            // Squares map directly: face 0 (+X) → neighbor 0 (+X), etc.
            // Hex faces: face 6+ maps to neighbor 6+ (same ordering: nested sz,sy,sx loops)
            var map = new int[14];
            for (int i = 0; i < 14; i++)
                map[i] = i;
            return map;
        }

        /// <summary>
        /// Triangulate a face (works for quads and hexagons).
        /// Returns triangle indices as a fan from vertex 0.
        /// </summary>
        public static int[] TriangulateFace(int vertexCount)
        {
            int triCount = vertexCount - 2;
            int[] tris = new int[triCount * 3];
            for (int i = 0; i < triCount; i++)
            {
                tris[i * 3] = 0;
                tris[i * 3 + 1] = i + 1;
                tris[i * 3 + 2] = i + 2;
            }
            return tris;
        }
    }
}
