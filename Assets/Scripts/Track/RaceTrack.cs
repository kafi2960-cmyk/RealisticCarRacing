using UnityEngine;

namespace RealisticCarRacing.Tracks
{
    /// <summary>
    /// Scene-side description of a circuit: the baked centreline used by AI, lap timing, spawning and tests.
    /// Samples are in world space and the loop starts on the start/finish line.
    /// </summary>
    public sealed class RaceTrack : MonoBehaviour
    {
        const float GridSlotSpacing = 8f;
        const float FirstGridSlotDistance = 10f;

        [SerializeField] TrackLayout layout;
        [SerializeField] TrackSample[] samples;
        [SerializeField] float length;

        public TrackLayout Layout => layout;
        public int SampleCount => samples.Length;
        public float Length => length;
        public float RoadWidth => layout.roadWidth;
        public float RoadSurfaceHeight => layout.roadHeight;

        public void Bake(TrackLayout trackLayout, TrackSample[] bakedSamples)
        {
            layout = trackLayout;
            samples = bakedSamples;
            float spacing = bakedSamples.Length > 1 ? bakedSamples[1].distance - bakedSamples[0].distance : 0f;
            length = bakedSamples[^1].distance + spacing;
        }

        /// <summary>Sample by index; wraps around the loop in both directions.</summary>
        public TrackSample GetSample(int index)
        {
            int count = samples.Length;
            return samples[((index % count) + count) % count];
        }

        /// <param name="hint">Previous result; when given, only nearby samples are searched (cheap per-frame tracking).</param>
        public int FindClosestSample(Vector3 position, int hint = -1, int searchRadius = 40)
        {
            int count = samples.Length;
            int first = hint < 0 ? 0 : hint - searchRadius;
            int last = hint < 0 ? count - 1 : hint + searchRadius;
            int best = 0;
            float bestDistance = float.MaxValue;
            for (int i = first; i <= last; i++)
            {
                int index = ((i % count) + count) % count;
                float distance = (samples[index].position - position).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = index;
                }
            }
            return best;
        }

        /// <summary>Signed distance from the centreline, positive to the right of the driving direction.</summary>
        public float LateralOffset(Vector3 position, int sampleIndex)
        {
            var sample = GetSample(sampleIndex);
            return Vector3.Dot(position - sample.position, sample.right);
        }

        /// <summary>Staggered two-column starting grid behind the start/finish line; slot 0 is pole position.</summary>
        public Pose GetGridPose(int slot)
        {
            float distanceBack = FirstGridSlotDistance + slot * GridSlotSpacing;
            float spacing = samples[1].distance - samples[0].distance;
            var sample = GetSample(-Mathf.RoundToInt(distanceBack / spacing));
            float side = slot % 2 == 0 ? -1f : 1f;
            Vector3 position = sample.position + sample.right * (side * RoadWidth * 0.22f) + Vector3.up * (RoadSurfaceHeight + 0.3f);
            return new Pose(position, Quaternion.LookRotation(sample.forward, Vector3.up));
        }

        void OnDrawGizmosSelected()
        {
            if (samples == null || samples.Length < 2)
                return;
            Gizmos.color = Color.cyan;
            for (int i = 0; i < samples.Length; i++)
                Gizmos.DrawLine(samples[i].position, samples[(i + 1) % samples.Length].position);
        }
    }
}
