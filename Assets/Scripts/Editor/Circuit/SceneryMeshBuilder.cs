using System.Collections.Generic;
using UnityEngine;

namespace RealisticCarRacing.EditorTools.Circuit
{
    /// <summary>Low-poly scenery meshes: grandstand, trees, distant hills and the ground grid.</summary>
    public static class SceneryMeshBuilder
    {
        /// <summary>
        /// Stepped grandstand in local space: X along the track (centred), Z away from the track, Y up.
        /// Sub-meshes: 0 concrete structure, 1 seating/crowd, 2 roof and columns.
        /// </summary>
        public static Mesh Grandstand(float length, int rows, float rowDepth = 0.9f, float rowRise = 0.5f, float frontHeight = 1.6f)
        {
            var builder = new MeshBuilder(3);
            float half = length * 0.5f;
            float u = length / 4f; // crowd texture tile = 4 m

            float Top(int row) => frontHeight + row * rowRise;
            float Front(int row) => row * rowDepth;

            // Front wall facing the track.
            builder.AddQuad(0, new Vector3(-half, 0f, 0f), new Vector3(half, 0f, 0f), new Vector3(half, frontHeight - rowRise, 0f), new Vector3(-half, frontHeight - rowRise, 0f),
                Vector2.zero, new Vector2(length / 4f, 0f), new Vector2(length / 4f, 0.4f), new Vector2(0f, 0.4f), Vector3.back);

            for (int row = 0; row < rows; row++)
            {
                float z0 = Front(row), z1 = Front(row + 1), top = Top(row), below = top - rowRise;
                // Riser (vertical, seen from the track: shows the spectators) and tread.
                builder.AddQuad(1, new Vector3(-half, below, z0), new Vector3(half, below, z0), new Vector3(half, top, z0), new Vector3(-half, top, z0),
                    new Vector2(0f, 0.3f), new Vector2(u, 0.3f), new Vector2(u, 1f), new Vector2(0f, 1f), Vector3.back);
                builder.AddQuad(1, new Vector3(-half, top, z0), new Vector3(half, top, z0), new Vector3(half, top, z1), new Vector3(-half, top, z1),
                    new Vector2(0f, 0f), new Vector2(u, 0f), new Vector2(u, 0.3f), new Vector2(0f, 0.3f), Vector3.up);

                // End caps.
                foreach (float x in new[] { -half, half })
                {
                    builder.AddQuad(0, new Vector3(x, 0f, z0), new Vector3(x, 0f, z1), new Vector3(x, top, z1), new Vector3(x, top, z0),
                        new Vector2(z0, 0f) / 4f, new Vector2(z1, 0f) / 4f, new Vector2(z1, top) / 4f, new Vector2(z0, top) / 4f, new Vector3(x, 0f, 0f));
                }
            }

            float depth = Front(rows), backTop = Top(rows - 1) + 1.2f;
            foreach (Vector3 facing in new[] { Vector3.forward, Vector3.back })
            {
                builder.AddQuad(0, new Vector3(-half, 0f, depth), new Vector3(half, 0f, depth), new Vector3(half, backTop, depth), new Vector3(-half, backTop, depth),
                    Vector2.zero, new Vector2(length / 4f, 0f), new Vector2(length / 4f, backTop / 4f), new Vector2(0f, backTop / 4f), facing);
            }

            // Cantilevered roof on columns along the back.
            float roofHeight = backTop + 3.5f;
            builder.AddBox(new Vector3(0f, roofHeight, depth * 0.5f - 0.5f), Quaternion.identity, new Vector3(length + 2f, 0.35f, depth + 3f), 2, 2, Vector2.one * 4f, includeBottom: true);
            for (float x = -half; x <= half + 0.01f; x += length / Mathf.Max(1, Mathf.RoundToInt(length / 12f)))
                builder.AddBox(new Vector3(x, 0f, depth + 0.3f), Quaternion.identity, new Vector3(0.4f, roofHeight, 0.4f), 2, 2, Vector2.one * 4f);

            return builder.ToMesh($"Grandstand_{length:0}m");
        }

        /// <summary>Broad-leaf tree: sub-mesh 0 trunk, 1 canopy (three lumpy spheres).</summary>
        public static Mesh DeciduousTree()
        {
            var builder = new MeshBuilder(2);
            AddCylinder(builder, 0, Vector3.zero, 0.28f, 0.18f, 4.2f, 6);
            var random = new System.Random(7);
            AddBlob(builder, 1, new Vector3(0f, 5.4f, 0f), 2.7f, random);
            AddBlob(builder, 1, new Vector3(1.1f, 4.6f, 0.5f), 2.1f, random);
            AddBlob(builder, 1, new Vector3(-0.9f, 4.9f, -0.7f), 2.2f, random);
            return builder.ToMesh("Tree_Deciduous");
        }

        /// <summary>Conifer: sub-mesh 0 trunk, 1 stacked cones.</summary>
        public static Mesh PineTree()
        {
            var builder = new MeshBuilder(2);
            AddCylinder(builder, 0, Vector3.zero, 0.22f, 0.12f, 3f, 6);
            float[] radii = { 2.6f, 2.1f, 1.6f, 1.0f };
            for (int i = 0; i < radii.Length; i++)
                AddCone(builder, 1, new Vector3(0f, 1.6f + i * 1.55f, 0f), radii[i], 2.8f, 9);
            return builder.ToMesh("Tree_Pine");
        }

        /// <summary>Ring of hills on the horizon so the circuit doesn't end at a flat edge.</summary>
        public static Mesh HillRing(Vector3 center, float innerRadius, float outerRadius, int segments = 160, int rings = 8)
        {
            var builder = new MeshBuilder();
            var grid = new int[rings + 1, segments + 1];
            for (int r = 0; r <= rings; r++)
            {
                float t = r / (float)rings;
                float radius = Mathf.Lerp(innerRadius, outerRadius, t);
                for (int s = 0; s <= segments; s++)
                {
                    float angle = s / (float)segments * Mathf.PI * 2f;
                    // Periodic in angle so the ring closes without a seam.
                    float ridge = Mathf.PerlinNoise(Mathf.Cos(angle) * 2.2f + 10f, Mathf.Sin(angle) * 2.2f + 10f);
                    float detail = Mathf.PerlinNoise(Mathf.Cos(angle) * 9f + 30f + t * 3f, Mathf.Sin(angle) * 9f + 30f);
                    float height = Mathf.SmoothStep(0f, 1f, Mathf.Min(t * 2.5f, 1f)) * (90f + ridge * 320f + detail * 60f);
                    var position = center + new Vector3(Mathf.Cos(angle) * radius, height - 2f, Mathf.Sin(angle) * radius);
                    grid[r, s] = builder.AddVertex(position, new Vector2(position.x, position.z) / 60f);
                }
            }
            for (int r = 0; r < rings; r++)
            for (int s = 0; s < segments; s++)
                builder.AddQuad(0, grid[r, s], grid[r, s + 1], grid[r + 1, s + 1], grid[r + 1, s], Vector3.up);
            return builder.ToMesh("HillRing");
        }

        /// <summary>Flat ground grid; subdivided so per-vertex fog stays smooth. UVs are in metres.</summary>
        public static Mesh GroundGrid(Vector3 center, float size, int cells)
        {
            var builder = new MeshBuilder();
            var grid = new int[cells + 1, cells + 1];
            float step = size / cells;
            for (int z = 0; z <= cells; z++)
            for (int x = 0; x <= cells; x++)
            {
                var position = center + new Vector3(-size * 0.5f + x * step, 0f, -size * 0.5f + z * step);
                grid[z, x] = builder.AddVertex(position, new Vector2(position.x, position.z));
            }
            for (int z = 0; z < cells; z++)
            for (int x = 0; x < cells; x++)
                builder.AddQuad(0, grid[z, x], grid[z, x + 1], grid[z + 1, x + 1], grid[z + 1, x], Vector3.up);
            return builder.ToMesh("Ground");
        }

        static void AddCylinder(MeshBuilder builder, int subMesh, Vector3 basePoint, float bottomRadius, float topRadius, float height, int sides)
        {
            int previousBottom = -1, previousTop = -1;
            for (int i = 0; i <= sides; i++)
            {
                float angle = i / (float)sides * Mathf.PI * 2f;
                var direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                int bottom = builder.AddVertex(basePoint + direction * bottomRadius, new Vector2(i / (float)sides, 0f));
                int top = builder.AddVertex(basePoint + direction * topRadius + Vector3.up * height, new Vector2(i / (float)sides, height / 2f));
                if (i > 0)
                {
                    float midAngle = (i - 0.5f) / sides * Mathf.PI * 2f;
                    builder.AddQuad(subMesh, previousBottom, bottom, top, previousTop, new Vector3(Mathf.Cos(midAngle), 0f, Mathf.Sin(midAngle)));
                }
                previousBottom = bottom;
                previousTop = top;
            }
        }

        static void AddCone(MeshBuilder builder, int subMesh, Vector3 basePoint, float radius, float height, int sides)
        {
            var random = new System.Random(sides * 13 + (int)(radius * 100));
            // Open underneath: the foliage material is double-sided.
            int apex = builder.AddVertex(basePoint + Vector3.up * height, new Vector2(0.5f, 1f));
            var rim = new List<int>();
            for (int i = 0; i <= sides; i++)
            {
                float angle = i % sides / (float)sides * Mathf.PI * 2f;
                float jitter = 1f + ((float)random.NextDouble() - 0.5f) * 0.25f;
                var direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                rim.Add(builder.AddVertex(basePoint + direction * radius * jitter - Vector3.up * 0.2f * jitter, new Vector2(i / (float)sides * 3f, 0f)));
            }
            for (int i = 0; i < sides; i++)
            {
                float midAngle = (i + 0.5f) / sides * Mathf.PI * 2f;
                var outward = new Vector3(Mathf.Cos(midAngle), 0.6f, Mathf.Sin(midAngle));
                builder.AddTriangle(subMesh, rim[i], rim[i + 1], apex, outward);
            }
        }

        /// <summary>Subdivided icosahedron with noise-displaced vertices, for organic canopies.</summary>
        static void AddBlob(MeshBuilder builder, int subMesh, Vector3 center, float radius, System.Random random)
        {
            var (vertices, faces) = Icosphere();
            var indices = new int[vertices.Count];
            for (int i = 0; i < vertices.Count; i++)
            {
                Vector3 direction = vertices[i];
                float bump = 1f + ((float)random.NextDouble() - 0.5f) * 0.35f;
                Vector3 position = center + Vector3.Scale(direction, new Vector3(1f, 0.8f, 1f)) * radius * bump;
                var uv = new Vector2(Mathf.Atan2(direction.z, direction.x) / Mathf.PI + 1f, direction.y + 1f) * 1.5f;
                indices[i] = builder.AddVertex(position, uv);
            }
            foreach (var (a, b, c) in faces)
            {
                Vector3 outward = vertices[a] + vertices[b] + vertices[c];
                builder.AddTriangle(subMesh, indices[a], indices[b], indices[c], outward);
            }
        }

        static (List<Vector3> vertices, List<(int, int, int)> faces) Icosphere()
        {
            float t = (1f + Mathf.Sqrt(5f)) / 2f;
            var vertices = new List<Vector3>
            {
                new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1)
            };
            for (int i = 0; i < vertices.Count; i++)
                vertices[i] = vertices[i].normalized;
            var faces = new List<(int, int, int)>
            {
                (0, 11, 5), (0, 5, 1), (0, 1, 7), (0, 7, 10), (0, 10, 11), (1, 5, 9), (5, 11, 4), (11, 10, 2), (10, 7, 6), (7, 1, 8),
                (3, 9, 4), (3, 4, 2), (3, 2, 6), (3, 6, 8), (3, 8, 9), (4, 9, 5), (2, 4, 11), (6, 2, 10), (8, 6, 7), (9, 8, 1)
            };

            // One subdivision: 80 faces, 42 vertices.
            var midpoints = new Dictionary<long, int>();
            int Midpoint(int a, int b)
            {
                long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                if (midpoints.TryGetValue(key, out int index))
                    return index;
                vertices.Add(((vertices[a] + vertices[b]) * 0.5f).normalized);
                midpoints[key] = vertices.Count - 1;
                return vertices.Count - 1;
            }
            var subdivided = new List<(int, int, int)>();
            foreach (var (a, b, c) in faces)
            {
                int ab = Midpoint(a, b), bc = Midpoint(b, c), ca = Midpoint(c, a);
                subdivided.Add((a, ab, ca));
                subdivided.Add((b, bc, ab));
                subdivided.Add((c, ca, bc));
                subdivided.Add((ab, bc, ca));
            }
            return (vertices, subdivided);
        }
    }
}
