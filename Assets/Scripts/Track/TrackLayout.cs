using UnityEngine;

namespace RealisticCarRacing.Tracks
{
    /// <summary>
    /// Design data for one circuit: centreline control points and cross-section dimensions.
    /// Edit it, then run RealisticCarRacing &gt; Create Circuit Scene to regenerate the geometry.
    /// Keep corner radii above the barrier offset (half road width + run-off) or the inner barrier folds over itself.
    /// </summary>
    [CreateAssetMenu(fileName = "TrackLayout", menuName = "RealisticCarRacing/Track Layout")]
    public sealed class TrackLayout : ScriptableObject
    {
        [Tooltip("Closed loop of centreline points in driving order (metres, Y = height).")]
        public Vector3[] controlPoints =
        {
            new Vector3(-250f, 0f, 0f), new Vector3(0f, 0f, 0f), new Vector3(250f, 0f, 0f),
            new Vector3(330f, 0f, 40f), new Vector3(350f, 0f, 130f), new Vector3(300f, 0f, 200f),
            new Vector3(200f, 0f, 210f), new Vector3(140f, 0f, 250f), new Vector3(120f, 0f, 320f),
            new Vector3(170f, 0f, 390f), new Vector3(160f, 0f, 460f), new Vector3(80f, 0f, 480f),
            new Vector3(20f, 0f, 430f), new Vector3(-40f, 0f, 350f), new Vector3(-150f, 0f, 330f),
            new Vector3(-260f, 0f, 380f), new Vector3(-350f, 0f, 330f), new Vector3(-375f, 0f, 225f),
            new Vector3(-325f, 0f, 155f), new Vector3(-345f, 0f, 80f), new Vector3(-310f, 0f, 10f)
        };

        [Tooltip("Control point on the start/finish line.")]
        public int startControlPoint = 1;

        [Header("Cross-section (metres)")]
        public float roadWidth = 12f;
        [Tooltip("Height of the asphalt above the surrounding ground.")]
        public float roadHeight = 0.03f;
        [Tooltip("Grass run-off between the road edge and the barrier.")]
        public float runoffWidth = 9f;
        public float kerbWidth = 1.2f;
        public float barrierHeight = 1.05f;

        [Header("Detail")]
        [Tooltip("Spacing of the baked centreline samples.")]
        public float sampleSpacing = 2f;
        [Tooltip("Corners tighter than this radius get kerbs.")]
        public float kerbMaxRadius = 140f;
        [Tooltip("Corners tighter than this radius get a gravel trap on the outside.")]
        public float gravelMaxRadius = 70f;

        public float BarrierOffset => roadWidth * 0.5f + runoffWidth;

        public TrackSample[] Sample() => TrackSpline.Sample(controlPoints, sampleSpacing, startControlPoint);
    }
}
