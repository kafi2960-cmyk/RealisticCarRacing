using System;
using UnityEngine;

namespace RealisticCarRacing.Tracks
{
    public enum TrackSurface
    {
        Asphalt,
        Kerb,
        Grass,
        Gravel
    }

    /// <summary>
    /// Which surface lies at a point of the circuit, from the car's position along and across the track: asphalt,
    /// kerbs (corners only), gravel traps (outside of the tightest corners) or grass. Gravel traps are visual only
    /// (no colliders, so the physics stay as they are), which is why sound looks surfaces up here instead of asking
    /// the collider under each wheel. Baked by the circuit scene builder from the same corner ranges as the meshes.
    /// </summary>
    public sealed class TrackSurfaces : MonoBehaviour
    {
        [Serializable]
        public struct Range
        {
            public int first;
            public int count;
            /// <summary>+1: right of the driving direction, -1: left, 0: both sides.</summary>
            public float side;
        }

        [SerializeField] RaceTrack track;
        [SerializeField] Range[] kerbs = Array.Empty<Range>();
        [SerializeField] Range[] gravel = Array.Empty<Range>();
        [SerializeField] float kerbWidth = 1.2f;
        [SerializeField] float gravelInner, gravelWidth;

        public RaceTrack Track => track;

        public void Bake(RaceTrack raceTrack, Range[] kerbRanges, Range[] gravelRanges, float kerbs, float gravelFrom, float gravelSpan)
        {
            track = raceTrack;
            this.kerbs = kerbRanges;
            gravel = gravelRanges;
            kerbWidth = kerbs;
            gravelInner = gravelFrom;
            gravelWidth = gravelSpan;
        }

        /// <param name="hint">Last sample index found for this wheel (speeds up the search); updated.</param>
        public TrackSurface SurfaceAt(Vector3 position, ref int hint)
        {
            hint = track.FindClosestSample(position, hint);
            float offset = track.LateralOffset(position, hint);
            float across = Mathf.Abs(offset), side = Mathf.Sign(offset);
            float roadHalf = track.RoadWidth * 0.5f;
            if (across <= roadHalf)
                return TrackSurface.Asphalt;
            if (across <= roadHalf + kerbWidth && Contains(kerbs, hint, side))
                return TrackSurface.Kerb;
            if (across >= gravelInner && across <= gravelInner + gravelWidth && Contains(gravel, hint, side))
                return TrackSurface.Gravel;
            return TrackSurface.Grass;
        }

        bool Contains(Range[] ranges, int sample, float side)
        {
            int samples = track.SampleCount;
            foreach (var range in ranges)
            {
                if (range.side != 0f && range.side != side)
                    continue;
                int along = ((sample - range.first) % samples + samples) % samples;
                if (along < range.count)
                    return true;
            }
            return false;
        }
    }
}
