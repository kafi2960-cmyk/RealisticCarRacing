using System;
using System.Collections.Generic;
using UnityEngine;

namespace RealisticCarRacing.Tracks
{
    /// <summary>One evenly spaced point on a track centreline, with its driving frame.</summary>
    [Serializable]
    public struct TrackSample
    {
        public Vector3 position;
        /// <summary>Driving direction (horizontal).</summary>
        public Vector3 forward;
        /// <summary>Horizontal unit vector to the driver's right.</summary>
        public Vector3 right;
        /// <summary>Distance from the start of the loop in metres.</summary>
        public float distance;
        /// <summary>Signed curvature in 1/m, smoothed. Positive for left-hand turns.</summary>
        public float curvature;
    }

    /// <summary>
    /// Closed centripetal Catmull-Rom spline through control points, resampled at equal arc length.
    /// Centripetal parameterisation avoids cusps and self-intersections between unevenly spaced points.
    /// </summary>
    public static class TrackSpline
    {
        const int SubdivisionsPerSegment = 200;
        const int CurvatureSmoothingRadius = 3;

        /// <param name="controlPoints">Closed loop in driving order; the last point connects back to the first.</param>
        /// <param name="spacing">Target distance between samples in metres.</param>
        /// <param name="startControlPoint">Control point that becomes distance 0 (the start/finish line).</param>
        public static TrackSample[] Sample(IReadOnlyList<Vector3> controlPoints, float spacing, int startControlPoint = 0)
        {
            int count = controlPoints.Count;
            if (count < 4)
                throw new ArgumentException("A closed track needs at least four control points.", nameof(controlPoints));

            var dense = new List<Vector3>(count * SubdivisionsPerSegment);
            for (int i = 0; i < count; i++)
            {
                int segment = (startControlPoint + i) % count;
                Vector3 p0 = controlPoints[(segment - 1 + count) % count];
                Vector3 p1 = controlPoints[segment];
                Vector3 p2 = controlPoints[(segment + 1) % count];
                Vector3 p3 = controlPoints[(segment + 2) % count];
                for (int step = 0; step < SubdivisionsPerSegment; step++)
                    dense.Add(CentripetalCatmullRom(p0, p1, p2, p3, step / (float)SubdivisionsPerSegment));
            }

            var cumulative = new float[dense.Count + 1];
            for (int i = 1; i <= dense.Count; i++)
                cumulative[i] = cumulative[i - 1] + Vector3.Distance(dense[i - 1], dense[i % dense.Count]);
            float length = cumulative[dense.Count];

            int sampleCount = Mathf.Max(8, Mathf.RoundToInt(length / spacing));
            float actualSpacing = length / sampleCount;
            var samples = new TrackSample[sampleCount];
            int cursor = 0;
            for (int i = 0; i < sampleCount; i++)
            {
                float distance = i * actualSpacing;
                while (cumulative[cursor + 1] < distance)
                    cursor++;
                float t = Mathf.InverseLerp(cumulative[cursor], cumulative[cursor + 1], distance);
                samples[i].position = Vector3.Lerp(dense[cursor], dense[(cursor + 1) % dense.Count], t);
                samples[i].distance = distance;
            }

            var rawCurvature = new float[sampleCount];
            for (int i = 0; i < sampleCount; i++)
            {
                Vector3 previous = samples[(i - 1 + sampleCount) % sampleCount].position;
                Vector3 next = samples[(i + 1) % sampleCount].position;
                Vector3 forward = Vector3.ProjectOnPlane(next - previous, Vector3.up).normalized;
                samples[i].forward = forward;
                samples[i].right = Vector3.Cross(Vector3.up, forward);

                Vector3 inbound = Vector3.ProjectOnPlane(samples[i].position - previous, Vector3.up);
                Vector3 outbound = Vector3.ProjectOnPlane(next - samples[i].position, Vector3.up);
                // SignedAngle about +Y is clockwise-positive seen from above, i.e. right turns; negate so left is positive.
                rawCurvature[i] = -Vector3.SignedAngle(inbound, outbound, Vector3.up) * Mathf.Deg2Rad / actualSpacing;
            }

            for (int i = 0; i < sampleCount; i++)
            {
                float sum = 0f;
                for (int k = -CurvatureSmoothingRadius; k <= CurvatureSmoothingRadius; k++)
                    sum += rawCurvature[(i + k + sampleCount) % sampleCount];
                samples[i].curvature = sum / (2 * CurvatureSmoothingRadius + 1);
            }

            return samples;
        }

        static Vector3 CentripetalCatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float u)
        {
            float t0 = 0f;
            float t1 = t0 + Knot(p0, p1);
            float t2 = t1 + Knot(p1, p2);
            float t3 = t2 + Knot(p2, p3);
            float t = Mathf.Lerp(t1, t2, u);

            Vector3 a1 = Remap(p0, p1, t0, t1, t);
            Vector3 a2 = Remap(p1, p2, t1, t2, t);
            Vector3 a3 = Remap(p2, p3, t2, t3, t);
            Vector3 b1 = Remap(a1, a2, t0, t2, t);
            Vector3 b2 = Remap(a2, a3, t1, t3, t);
            return Remap(b1, b2, t1, t2, t);
        }

        static float Knot(Vector3 a, Vector3 b) => Mathf.Sqrt(Mathf.Max(Vector3.Distance(a, b), 1e-4f));

        static Vector3 Remap(Vector3 a, Vector3 b, float ta, float tb, float t) =>
            (tb - t) / (tb - ta) * a + (t - ta) / (tb - ta) * b;
    }
}
