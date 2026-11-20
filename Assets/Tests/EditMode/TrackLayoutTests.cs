using NUnit.Framework;
using RealisticCarRacing.Tracks;
using UnityEngine;

namespace RealisticCarRacing.Tests
{
    /// <summary>Geometry checks on the default circuit layout, so edits that would break the generated track fail fast.</summary>
    public class TrackLayoutTests
    {
        TrackLayout layout;
        TrackSample[] samples;
        float spacing;

        [SetUp]
        public void SetUp()
        {
            layout = ScriptableObject.CreateInstance<TrackLayout>();
            samples = layout.Sample();
            spacing = samples[1].distance - samples[0].distance;
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(layout);

        [Test]
        public void LapLength_IsRealistic()
        {
            float length = samples.Length * spacing;
            Assert.That(length, Is.InRange(1800f, 3000f));
            Assert.That(spacing, Is.EqualTo(layout.sampleSpacing).Within(0.05f));
        }

        [Test]
        public void Samples_AreEvenlySpacedAndClosed()
        {
            for (int i = 0; i < samples.Length; i++)
            {
                float gap = Vector3.Distance(samples[i].position, samples[(i + 1) % samples.Length].position);
                Assert.That(gap, Is.EqualTo(spacing).Within(0.1f), $"gap after sample {i}");
            }
        }

        [Test]
        public void Loop_TurnsOnceAnticlockwise()
        {
            float turning = 0f;
            foreach (var sample in samples)
                turning += sample.curvature * spacing;
            Assert.That(turning, Is.EqualTo(2f * Mathf.PI).Within(0.05f));
        }

        [Test]
        public void Corners_AreWiderThanTheBarrierOffset()
        {
            float tightest = float.MaxValue;
            foreach (var sample in samples)
                tightest = Mathf.Min(tightest, 1f / Mathf.Max(Mathf.Abs(sample.curvature), 1e-5f));
            Assert.That(tightest, Is.GreaterThan(layout.BarrierOffset + 10f), "inner barrier would fold over itself");
        }

        [Test]
        public void SeparateSections_DoNotOverlap()
        {
            int window = Mathf.CeilToInt(150f / spacing);
            float minimum = 2f * layout.BarrierOffset + 20f;
            for (int i = 0; i < samples.Length; i += 2)
            for (int k = i + window; k < samples.Length; k += 2)
            {
                if (samples.Length - (k - i) < window)
                    continue;
                float distance = Vector3.Distance(samples[i].position, samples[k].position);
                Assert.That(distance, Is.GreaterThan(minimum), $"sections at {samples[i].distance:0} m and {samples[k].distance:0} m are too close");
            }
        }

        [Test]
        public void StartLine_IsOnTheMainStraight()
        {
            Assert.That(samples[0].position, Is.EqualTo(layout.controlPoints[layout.startControlPoint]).Using(Vector3EqualityComparer(0.01f)));
            for (int i = -40; i <= 40; i++)
                Assert.That(Mathf.Abs(samples[(i + samples.Length) % samples.Length].curvature), Is.LessThan(1f / 300f));
        }

        static System.Collections.Generic.IEqualityComparer<Vector3> Vector3EqualityComparer(float tolerance) =>
            new Vector3Comparer(tolerance);

        sealed class Vector3Comparer : System.Collections.Generic.IEqualityComparer<Vector3>
        {
            readonly float tolerance;
            public Vector3Comparer(float tolerance) => this.tolerance = tolerance;
            public bool Equals(Vector3 a, Vector3 b) => Vector3.Distance(a, b) <= tolerance;
            public int GetHashCode(Vector3 v) => 0;
        }
    }
}
