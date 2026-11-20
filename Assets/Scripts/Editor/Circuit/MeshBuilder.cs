using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace RealisticCarRacing.EditorTools.Circuit
{
    /// <summary>Accumulates vertices and per-sub-mesh triangles, then produces a Mesh with normals and tangents.</summary>
    public sealed class MeshBuilder
    {
        readonly List<Vector3> vertices = new List<Vector3>();
        readonly List<Vector2> uvs = new List<Vector2>();
        readonly List<int>[] triangles;

        public MeshBuilder(int subMeshCount = 1)
        {
            triangles = new List<int>[subMeshCount];
            for (int i = 0; i < subMeshCount; i++)
                triangles[i] = new List<int>();
        }

        public int VertexCount => vertices.Count;

        public int AddVertex(Vector3 position, Vector2 uv)
        {
            vertices.Add(position);
            uvs.Add(uv);
            return vertices.Count - 1;
        }

        public void AddTriangle(int subMesh, int a, int b, int c)
        {
            triangles[subMesh].Add(a);
            triangles[subMesh].Add(b);
            triangles[subMesh].Add(c);
        }

        /// <summary>Triangle flipped if needed so it faces <paramref name="facing"/>.</summary>
        public void AddTriangle(int subMesh, int a, int b, int c, Vector3 facing)
        {
            Vector3 normal = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]);
            if (Vector3.Dot(normal, facing) < 0f)
                AddTriangle(subMesh, a, c, b);
            else
                AddTriangle(subMesh, a, b, c);
        }

        /// <summary>Quad a-b-c-d (in order around the edge); flipped if needed so it faces <paramref name="facing"/>.</summary>
        public void AddQuad(int subMesh, int a, int b, int c, int d, Vector3 facing)
        {
            Vector3 normal = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]);
            if (Vector3.Dot(normal, facing) < 0f)
            {
                AddTriangle(subMesh, a, c, b);
                AddTriangle(subMesh, a, d, c);
            }
            else
            {
                AddTriangle(subMesh, a, b, c);
                AddTriangle(subMesh, a, c, d);
            }
        }

        /// <summary>Single quad with its own four vertices.</summary>
        public void AddQuad(int subMesh, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector2 uvA, Vector2 uvB, Vector2 uvC, Vector2 uvD, Vector3 facing)
        {
            AddQuad(subMesh, AddVertex(a, uvA), AddVertex(b, uvB), AddVertex(c, uvC), AddVertex(d, uvD), facing);
        }

        /// <summary>
        /// Axis-aligned (in local space) box standing on <paramref name="baseCenter"/>. Walls are UV-mapped in metres
        /// divided by the tile size so façade textures keep their real-world scale; the roof goes to its own sub-mesh.
        /// </summary>
        /// <param name="includeBottom">Add the underside (for roofs, canopies and beams seen from below); it uses the wall sub-mesh.</param>
        public void AddBox(Vector3 baseCenter, Quaternion rotation, Vector3 size, int wallSubMesh, int roofSubMesh,
            Vector2 wallTileSize, float roofTileSize = 10f, bool includeBottom = false)
        {
            Vector3 half = size * 0.5f;
            Vector3 P(float x, float y, float z) => baseCenter + rotation * new Vector3(x, y, z);

            // Four walls: corners listed counter-clockwise seen from above.
            var corners = new[] { new Vector2(-half.x, -half.z), new Vector2(half.x, -half.z), new Vector2(half.x, half.z), new Vector2(-half.x, half.z) };
            for (int i = 0; i < 4; i++)
            {
                Vector2 c0 = corners[i], c1 = corners[(i + 1) % 4];
                float width = Vector2.Distance(c0, c1);
                Vector2 mid = (c0 + c1) * 0.5f;
                Vector3 outward = rotation * new Vector3(mid.x, 0f, mid.y);
                float u1 = width / wallTileSize.x, v1 = size.y / wallTileSize.y;
                AddQuad(wallSubMesh,
                    P(c0.x, 0f, c0.y), P(c1.x, 0f, c1.y), P(c1.x, size.y, c1.y), P(c0.x, size.y, c0.y),
                    new Vector2(0f, 0f), new Vector2(u1, 0f), new Vector2(u1, v1), new Vector2(0f, v1), outward);
            }

            Vector3 r0 = P(-half.x, size.y, -half.z), r1 = P(half.x, size.y, -half.z), r2 = P(half.x, size.y, half.z), r3 = P(-half.x, size.y, half.z);
            AddQuad(roofSubMesh, r0, r1, r2, r3,
                new Vector2(r0.x, r0.z) / roofTileSize, new Vector2(r1.x, r1.z) / roofTileSize,
                new Vector2(r2.x, r2.z) / roofTileSize, new Vector2(r3.x, r3.z) / roofTileSize, Vector3.up);

            if (includeBottom)
            {
                Vector3 b0 = P(-half.x, 0f, -half.z), b1 = P(half.x, 0f, -half.z), b2 = P(half.x, 0f, half.z), b3 = P(-half.x, 0f, half.z);
                AddQuad(wallSubMesh, b0, b1, b2, b3,
                    new Vector2(b0.x, b0.z) / roofTileSize, new Vector2(b1.x, b1.z) / roofTileSize,
                    new Vector2(b2.x, b2.z) / roofTileSize, new Vector2(b3.x, b3.z) / roofTileSize, rotation * Vector3.down);
            }
        }

        public Mesh ToMesh(string name)
        {
            var mesh = new Mesh { name = name };
            if (vertices.Count > 65000)
                mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.subMeshCount = triangles.Length;
            for (int i = 0; i < triangles.Length; i++)
                mesh.SetTriangles(triangles[i], i);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            mesh.Optimize();
            return mesh;
        }
    }
}
