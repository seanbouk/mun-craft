using System.Collections.Generic;
using UnityEngine;

namespace MunCraft.Gravity
{
    /// <summary>
    /// Barnes-Hut octree for efficient N-body gravity computation.
    /// Each node stores center of mass and total mass.
    /// Query returns the gravity vector at any point in O(log n).
    /// </summary>
    public class GravityOctree
    {
        const int MaxDepth = 20;
        const int MaxBodiesPerLeaf = 8;

        class Node
        {
            public Bounds Bounds;
            public Vector3 CenterOfMass;
            public float TotalMass;
            public Node[] Children; // null for leaf
            public List<Vector3> Bodies; // only on leaf nodes
            public bool IsLeaf => Children == null;
        }

        Node _root;
        float _theta;
        float _gravityConstant;
        float _softening; // prevents singularity at zero distance

        public GravityOctree(float theta = 0.5f, float gravityConstant = 9.81f, float softening = 0.5f)
        {
            _theta = theta;
            _gravityConstant = gravityConstant;
            _softening = softening;
        }

        public float Theta
        {
            get => _theta;
            set => _theta = Mathf.Clamp(value, 0f, 2f);
        }

        public float GravityConstant
        {
            get => _gravityConstant;
            set => _gravityConstant = value;
        }

        /// <summary>
        /// Build the octree from a list of block world positions.
        /// Each block has mass 1.
        /// </summary>
        public void Build(List<Vector3> blockPositions)
        {
            if (blockPositions == null || blockPositions.Count == 0)
            {
                _root = null;
                return;
            }

            // Compute bounding box
            Vector3 min = blockPositions[0];
            Vector3 max = blockPositions[0];
            for (int i = 1; i < blockPositions.Count; i++)
            {
                min = Vector3.Min(min, blockPositions[i]);
                max = Vector3.Max(max, blockPositions[i]);
            }

            // Make it a cube (octree needs uniform subdivision)
            Vector3 size = max - min;
            float maxSize = Mathf.Max(size.x, Mathf.Max(size.y, size.z)) + 1f;
            Vector3 center = (min + max) * 0.5f;
            var bounds = new Bounds(center, Vector3.one * maxSize);

            _root = new Node
            {
                Bounds = bounds,
                Bodies = new List<Vector3>(blockPositions)
            };

            Subdivide(_root, 0);
            ComputeMass(_root);
        }

        void Subdivide(Node node, int depth)
        {
            if (node.Bodies.Count <= MaxBodiesPerLeaf || depth >= MaxDepth)
                return;

            node.Children = new Node[8];
            Vector3 center = node.Bounds.center;
            Vector3 halfSize = node.Bounds.size * 0.25f;

            for (int i = 0; i < 8; i++)
            {
                Vector3 offset = new Vector3(
                    (i & 1) == 0 ? -halfSize.x : halfSize.x,
                    (i & 2) == 0 ? -halfSize.y : halfSize.y,
                    (i & 4) == 0 ? -halfSize.z : halfSize.z
                );
                node.Children[i] = new Node
                {
                    Bounds = new Bounds(center + offset, node.Bounds.size * 0.5f),
                    Bodies = new List<Vector3>()
                };
            }

            // Distribute bodies to children
            for (int b = 0; b < node.Bodies.Count; b++)
            {
                Vector3 pos = node.Bodies[b];
                int octant = 0;
                if (pos.x >= center.x) octant |= 1;
                if (pos.y >= center.y) octant |= 2;
                if (pos.z >= center.z) octant |= 4;
                node.Children[octant].Bodies.Add(pos);
            }

            node.Bodies = null; // internal nodes don't keep bodies

            // Recurse
            for (int i = 0; i < 8; i++)
            {
                if (node.Children[i].Bodies.Count > 0)
                    Subdivide(node.Children[i], depth + 1);
            }
        }

        void ComputeMass(Node node)
        {
            if (node.IsLeaf)
            {
                if (node.Bodies == null || node.Bodies.Count == 0)
                {
                    node.TotalMass = 0;
                    node.CenterOfMass = node.Bounds.center;
                    return;
                }

                Vector3 com = Vector3.zero;
                for (int i = 0; i < node.Bodies.Count; i++)
                    com += node.Bodies[i];
                com /= node.Bodies.Count;

                node.CenterOfMass = com;
                node.TotalMass = node.Bodies.Count;
                return;
            }

            float totalMass = 0;
            Vector3 weightedPos = Vector3.zero;

            for (int i = 0; i < 8; i++)
            {
                ComputeMass(node.Children[i]);
                float m = node.Children[i].TotalMass;
                totalMass += m;
                weightedPos += node.Children[i].CenterOfMass * m;
            }

            node.TotalMass = totalMass;
            node.CenterOfMass = totalMass > 0 ? weightedPos / totalMass : node.Bounds.center;
        }

        /// <summary>
        /// Query the gravitational acceleration at a world position.
        /// Returns a Vector3 pointing toward the center of mass (the "down" direction).
        /// </summary>
        public Vector3 QueryGravity(Vector3 position)
        {
            if (_root == null || _root.TotalMass == 0)
                return Vector3.zero;

            Vector3 accel = Vector3.zero;
            AccumulateGravity(_root, position, ref accel);
            return accel;
        }

        void AccumulateGravity(Node node, Vector3 position, ref Vector3 accel)
        {
            if (node.TotalMass == 0)
                return;

            Vector3 diff = node.CenterOfMass - position;
            float distSqr = diff.sqrMagnitude + _softening * _softening;

            if (node.IsLeaf)
            {
                // For leaf nodes with few bodies, compute directly
                if (node.Bodies != null)
                {
                    for (int i = 0; i < node.Bodies.Count; i++)
                    {
                        Vector3 d = node.Bodies[i] - position;
                        float dSqr = d.sqrMagnitude + _softening * _softening;
                        float dist = Mathf.Sqrt(dSqr);
                        // F = G * m / r^2, direction = d/|d|
                        // acceleration = G * m * d / |d|^3
                        accel += _gravityConstant * d / (dist * dSqr);
                    }
                }
                return;
            }

            // Barnes-Hut criterion: if node is "far enough", use aggregate
            float nodeSize = node.Bounds.size.x;
            float nodeDist = Mathf.Sqrt(distSqr);

            if (nodeSize / nodeDist < _theta)
            {
                // Use aggregate mass
                accel += _gravityConstant * node.TotalMass * diff / (nodeDist * distSqr);
                return;
            }

            // Otherwise recurse into children
            for (int i = 0; i < 8; i++)
                AccumulateGravity(node.Children[i], position, ref accel);
        }
    }
}
