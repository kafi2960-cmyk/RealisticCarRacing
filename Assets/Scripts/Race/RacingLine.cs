using RealisticCarRacing.Tracks;
using UnityEngine;

namespace RealisticCarRacing.Racing
{
    /// <summary>
    /// A classic outside–inside–outside racing line as a lateral offset from the centreline per sample.
    /// Before a corner the line moves to the outside, at the apex to the inside; offsets are smoothed and
    /// clamped to <see cref="MaxOffset"/>.
    /// </summary>
    public sealed class RacingLine
    {
        readonly float[] offsets;

        public RacingLine(RaceTrack track, float maxOffset)
        {
            MaxOffset = maxOffset;
            int n = track.SampleCount;
            float spacing = track.Length / n;
            var curvature = new float[n];
            for (int i = 0; i < n; i++)
                curvature[i] = track.GetSample(i).curvature;

            int near = Mathf.RoundToInt(20f / spacing);
            int farStart = Mathf.RoundToInt(30f / spacing), farEnd = Mathf.RoundToInt(100f / spacing);
            var raw = new float[n];
            for (int i = 0; i < n; i++)
            {
                float here = Average(curvature, i - near, i + near);
                float ahead = Average(curvature, i + farStart, i + farEnd);
                // Positive curvature = left turn. At the apex (here) go left/inside; approaching (ahead) stay right/outside.
                float bias = here - 0.8f * ahead;
                raw[i] = -maxOffset * (float)System.Math.Tanh(bias * 60f);
            }

            offsets = new float[n];
            int smoothing = Mathf.RoundToInt(24f / spacing);
            for (int i = 0; i < n; i++)
                offsets[i] = Mathf.Clamp(Average(raw, i - smoothing, i + smoothing), -maxOffset, maxOffset);
        }

        public float MaxOffset { get; }

        /// <summary>Metres from the centreline (positive = right) at a sample; wraps around the lap.</summary>
        public float OffsetAt(int sample)
        {
            int n = offsets.Length;
            return offsets[((sample % n) + n) % n];
        }

        static float Average(float[] values, int first, int last)
        {
            int n = values.Length;
            float sum = 0f;
            for (int i = first; i <= last; i++)
                sum += values[((i % n) + n) % n];
            return sum / (last - first + 1);
        }
    }
}
