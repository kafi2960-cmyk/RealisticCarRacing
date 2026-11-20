using System.Collections.Generic;
using RealisticCarRacing.Tracks;
using UnityEngine;

namespace RealisticCarRacing.EditorTools.Circuit
{
    /// <summary>
    /// Builds track geometry by sweeping 2D cross-section profiles along the baked centreline.
    /// Profile points are (lateral, height): lateral is measured outwards from <c>anchor</c> on the chosen side.
    /// </summary>
    public static class TrackMeshBuilder
    {
        public readonly struct Section
        {
            public readonly Vector2[] points;
            public readonly float[] u;

            public Section(Vector2[] points, float[] u)
            {
                this.points = points;
                this.u = u;
            }

            /// <summary>U from cumulative edge length, normalised to 0..1.</summary>
            public static Section WithLengthU(Vector2[] points)
            {
                var u = new float[points.Length];
                for (int i = 1; i < points.Length; i++)
                    u[i] = u[i - 1] + Vector2.Distance(points[i - 1], points[i]);
                for (int i = 0; i < u.Length; i++)
                    u[i] /= u[^1];
                return new Section(points, u);
            }
        }

        /// <summary>Jersey-style concrete wall, track-facing side first. Returns the U where the painted face ends.</summary>
        public static Section BarrierSection(float height, out float paintedFraction)
        {
            var section = Section.WithLengthU(new[]
            {
                new Vector2(0f, -0.05f), new Vector2(0f, 0.1f), new Vector2(0.15f, 0.3f), new Vector2(0.24f, height),
                new Vector2(0.44f, height), new Vector2(0.53f, 0.3f), new Vector2(0.68f, 0.1f), new Vector2(0.68f, -0.05f)
            });
            paintedFraction = section.u[3];
            return section;
        }

        /// <summary>
        /// Asphalt ribbon with short skirts down into the ground on both sides. With the defaults it is the whole closed
        /// loop (used for the collider); with <paramref name="first"/>/<paramref name="count"/> it is one render chunk.
        /// </summary>
        public static Mesh Road(TrackSample[] samples, float length, TrackLayout layout, float vTile, int first = 0, int count = -1, string name = "Road")
        {
            float half = layout.roadWidth * 0.5f;
            var section = new Section(
                new[] { new Vector2(-half, -0.3f), new Vector2(-half, layout.roadHeight), new Vector2(half, layout.roadHeight), new Vector2(half, -0.3f) },
                new[] { 0f, 0f, 1f, 1f });
            var builder = new MeshBuilder();
            bool closed = count < 0;
            Extrude(builder, 0, samples, length, first, closed ? samples.Length : count, closed, section, 0f, 1f, vTile);
            return builder.ToMesh(name);
        }

        public static Mesh Barriers(TrackSample[] samples, float length, TrackLayout layout, Section section, float vTile, int first = 0, int count = -1, string name = "Barriers")
        {
            var builder = new MeshBuilder();
            bool closed = count < 0;
            foreach (float side in new[] { -1f, 1f })
                Extrude(builder, 0, samples, length, first, closed ? samples.Length : count, closed, section, layout.BarrierOffset, side, vTile);
            return builder.ToMesh(name);
        }

        /// <summary>
        /// Debris fences standing on both barriers: a 2.4 m sheet along the barrier crest (texture U up the fence,
        /// V along it in <paramref name="vTile"/> metre panels, one post per panel).
        /// </summary>
        public static Mesh Fences(TrackSample[] samples, float length, TrackLayout layout, float vTile, int first = 0, int count = -1, string name = "Fences")
        {
            const float crest = 0.34f, height = 2.4f; // middle of the barrier's flat top (see BarrierSection)
            var section = Section.WithLengthU(new[] { new Vector2(crest, layout.barrierHeight - 0.02f), new Vector2(crest, layout.barrierHeight + height) });
            var builder = new MeshBuilder();
            bool closed = count < 0;
            foreach (float side in new[] { -1f, 1f })
                Extrude(builder, 0, samples, length, first, closed ? samples.Length : count, closed, section, layout.BarrierOffset, side, vTile);
            return builder.ToMesh(name);
        }

        /// <summary>One mesh per kerbed corner (render chunks; <see cref="Kerbs"/> is the combined collider).</summary>
        public static List<Mesh> KerbPieces(TrackSample[] samples, float length, TrackLayout layout)
        {
            var pieces = new List<Mesh>();
            var section = KerbSection(layout);
            int index = 0;
            foreach (var (first, count, _) in CornerRanges(samples, 1f / layout.kerbMaxRadius, padding: 8))
            {
                var builder = new MeshBuilder();
                foreach (float side in new[] { -1f, 1f })
                    Extrude(builder, 0, samples, length, first, count, false, section, layout.roadWidth * 0.5f, side, 2f,
                        taperPivot: new Vector2(0f, layout.roadHeight), taperSamples: 5);
                pieces.Add(builder.ToMesh($"Kerbs_{index++}"));
            }
            return pieces;
        }

        static Section KerbSection(TrackLayout layout)
        {
            float w = layout.kerbWidth, h = layout.roadHeight;
            return new Section(
                new[] { new Vector2(0f, h), new Vector2(w * 0.55f, h + 0.035f), new Vector2(w, h + 0.005f), new Vector2(w, -0.1f) },
                new[] { 0f, 0.55f, 1f, 1f });
        }

        /// <summary>Raised red/white kerbs on both sides of every corner tighter than <see cref="TrackLayout.kerbMaxRadius"/>.</summary>
        public static Mesh Kerbs(TrackSample[] samples, float length, TrackLayout layout, out int kerbCount)
        {
            float h = layout.roadHeight;
            var section = KerbSection(layout);
            var builder = new MeshBuilder();
            var ranges = CornerRanges(samples, 1f / layout.kerbMaxRadius, padding: 8);
            foreach (var (first, count, _) in ranges)
            foreach (float side in new[] { -1f, 1f })
                Extrude(builder, 0, samples, length, first, count, false, section, layout.roadWidth * 0.5f, side, 2f,
                    taperPivot: new Vector2(0f, h), taperSamples: 5);
            kerbCount = ranges.Count;
            return builder.ToMesh("Kerbs");
        }

        /// <summary>Gravel traps on the outside of the tightest corners, one mesh per trap (visual only).</summary>
        public static List<Mesh> GravelTraps(TrackSample[] samples, float length, TrackLayout layout)
        {
            float inner = layout.roadWidth * 0.5f + layout.kerbWidth + 0.4f;
            float width = layout.runoffWidth - layout.kerbWidth - 1.4f;
            var section = new Section(new[] { new Vector2(0f, 0.012f), new Vector2(width, 0.012f) }, new[] { 0f, width / 4f });
            var traps = new List<Mesh>();
            foreach (var (first, count, turnSign) in CornerRanges(samples, 1f / layout.gravelMaxRadius, padding: 15))
            {
                var builder = new MeshBuilder();
                // A left turn (positive curvature) runs wide to the right.
                Extrude(builder, 0, samples, length, first, count, false, section, inner, turnSign > 0f ? 1f : -1f, 4f,
                    taperPivot: new Vector2(0f, 0.012f), taperSamples: 10);
                traps.Add(builder.ToMesh($"GravelTrap_{traps.Count}"));
            }
            return traps;
        }

        /// <summary>Start/finish line (sub-mesh 1, checker) and starting-grid slot markings (sub-mesh 0, white paint).</summary>
        public static Mesh Markings(RaceTrack track, TrackLayout layout, int gridSlots)
        {
            var builder = new MeshBuilder(2);
            float y = layout.roadHeight + 0.008f;
            var start = track.GetSample(0);
            AddFlatQuad(builder, 1, start.position, start.forward, start.right, layout.roadWidth * 0.5f, 0.75f, y);

            for (int slot = 0; slot < gridSlots; slot++)
            {
                Pose pose = track.GetGridPose(slot);
                Vector3 forward = pose.forward, right = pose.right;
                Vector3 front = new Vector3(pose.position.x, 0f, pose.position.z) + forward * 2.6f;
                AddFlatQuad(builder, 0, front, forward, right, 1.3f, 0.08f, y);
                AddFlatQuad(builder, 0, front - forward * 0.6f + right * 1.22f, forward, right, 0.08f, 0.6f, y);
                AddFlatQuad(builder, 0, front - forward * 0.6f - right * 1.22f, forward, right, 0.08f, 0.6f, y);
            }
            return builder.ToMesh("TrackMarkings");
        }

        /// <summary>Gantry over the start line (sub-mesh 0 metal, 1 checker panels), spanning barrier to barrier.</summary>
        public static Mesh StartGantry(RaceTrack track, TrackLayout layout)
        {
            var builder = new MeshBuilder(2);
            var start = track.GetSample(0);
            var rotation = Quaternion.LookRotation(start.forward, Vector3.up);
            float span = layout.BarrierOffset + 1.5f;
            foreach (float side in new[] { -1f, 1f })
                builder.AddBox(start.position + start.right * (side * span), rotation, new Vector3(0.6f, 7.6f, 0.6f), 0, 0, Vector2.one * 2f);
            builder.AddBox(start.position + Vector3.up * 6.4f, rotation, new Vector3(span * 2f + 0.6f, 1.2f, 0.8f), 0, 0, Vector2.one * 2f, includeBottom: true);
            foreach (float face in new[] { -1f, 1f })
                builder.AddBox(start.position + Vector3.up * 6.5f + start.forward * (face * 0.42f), rotation, new Vector3(12f, 1f, 0.04f), 1, 1, Vector2.one);
            return builder.ToMesh("StartGantry");
        }

        static void AddFlatQuad(MeshBuilder builder, int subMesh, Vector3 center, Vector3 forward, Vector3 right, float halfWidth, float halfLength, float y)
        {
            center.y = y;
            Vector3 a = center - right * halfWidth - forward * halfLength;
            Vector3 b = center + right * halfWidth - forward * halfLength;
            Vector3 c = center + right * halfWidth + forward * halfLength;
            Vector3 d = center - right * halfWidth + forward * halfLength;
            // UVs in metres so a 1 m checker tile gives 0.5 m squares.
            builder.AddQuad(subMesh, a, b, c, d,
                new Vector2(0f, 0f), new Vector2(halfWidth * 2f, 0f), new Vector2(halfWidth * 2f, halfLength * 2f), new Vector2(0f, halfLength * 2f),
                Vector3.up);
        }

        /// <summary>Index ranges (first, count, sign of turn) where |curvature| exceeds the threshold, padded and merged.</summary>
        public static List<(int first, int count, float turnSign)> CornerRanges(TrackSample[] samples, float minCurvature, int padding)
        {
            int n = samples.Length;
            var corner = new bool[n];
            for (int i = 0; i < n; i++)
            {
                if (Mathf.Abs(samples[i].curvature) < minCurvature)
                    continue;
                for (int k = -padding; k <= padding; k++)
                    corner[((i + k) % n + n) % n] = true;
            }

            // Start scanning just after a gap so a corner spanning the loop seam stays in one piece.
            int origin = System.Array.IndexOf(corner, false);
            var ranges = new List<(int, int, float)>();
            if (origin < 0)
                return ranges;
            for (int i = 0; i < n; i++)
            {
                int index = (origin + i) % n;
                if (!corner[index])
                    continue;
                int first = index, count = 0;
                float turn = 0f;
                while (i < n && corner[(origin + i) % n])
                {
                    turn += samples[(origin + i) % n].curvature;
                    count++;
                    i++;
                }
                ranges.Add((first, count, Mathf.Sign(turn)));
            }
            return ranges;
        }

        /// <summary>
        /// Sweeps <paramref name="section"/> along samples [first, first+count) (indices wrap). Each profile edge gets its own
        /// vertices so creases stay sharp. Quads are oriented away from the section's centroid (or up, for flat strips).
        /// </summary>
        public static void Extrude(MeshBuilder builder, int subMesh, TrackSample[] samples, float length, int first, int count,
            bool closed, Section section, float anchor, float side, float vTile, Vector2 taperPivot = default, int taperSamples = 0)
        {
            int n = samples.Length;
            float spacing = length / n;
            int rings = closed ? count + 1 : count;
            var points = section.points;

            Vector2 centroid = Vector2.zero;
            foreach (var p in points)
                centroid += p;
            centroid /= points.Length;

            for (int edge = 0; edge < points.Length - 1; edge++)
            {
                int previousA = -1, previousB = -1;
                for (int ring = 0; ring < rings; ring++)
                {
                    var sample = samples[(first + ring) % n];
                    float scale = 1f;
                    if (taperSamples > 0)
                        scale = Mathf.SmoothStep(0f, 1f, Mathf.Min(ring, rings - 1 - ring) / (float)taperSamples);

                    Vector2 a = Taper(points[edge], taperPivot, scale);
                    Vector2 b = Taper(points[edge + 1], taperPivot, scale);
                    float v = (samples[first].distance + ring * spacing) / vTile;
                    int indexA = builder.AddVertex(Place(sample, a, anchor, side), new Vector2(section.u[edge], v));
                    int indexB = builder.AddVertex(Place(sample, b, anchor, side), new Vector2(section.u[edge + 1], v));

                    if (ring > 0)
                    {
                        Vector3 facing = Place(sample, (a + b) * 0.5f, anchor, side) - Place(sample, Taper(centroid, taperPivot, scale), anchor, side);
                        if (facing.sqrMagnitude < 1e-8f)
                            facing = Vector3.up;
                        builder.AddQuad(subMesh, previousA, previousB, indexB, indexA, facing);
                    }
                    previousA = indexA;
                    previousB = indexB;
                }
            }
        }

        static Vector2 Taper(Vector2 point, Vector2 pivot, float scale) => pivot + (point - pivot) * scale;

        static Vector3 Place(TrackSample sample, Vector2 point, float anchor, float side) =>
            sample.position + sample.right * (side * (anchor + point.x)) + Vector3.up * point.y;
    }
}
