using System.Collections.Generic;
using RealisticCarRacing.Controls;
using RealisticCarRacing.Racing;
using RealisticCarRacing.Tracks;
using RealisticCarRacing.Vehicles;
using UnityEngine;

namespace RealisticCarRacing.AI
{
    /// <summary>How hard an AI driver pushes.</summary>
    public struct AIProfile
    {
        public float corneringGrip;
        public float maxSpeed;
        public float brakingDeceleration;

        /// <param name="variation">-1..1: spreads drivers of the same difficulty slightly apart.</param>
        public static AIProfile For(AIDifficulty difficulty, float variation)
        {
            var profile = difficulty switch
            {
                AIDifficulty.Easy => new AIProfile { corneringGrip = 0.52f, maxSpeed = 32f, brakingDeceleration = 4f },
                AIDifficulty.Hard => new AIProfile { corneringGrip = 0.8f, maxSpeed = 60f, brakingDeceleration = 5.5f },
                _ => new AIProfile { corneringGrip = 0.66f, maxSpeed = 42f, brakingDeceleration = 4.8f }
            };
            float scale = 1f + 0.03f * Mathf.Clamp(variation, -1f, 1f);
            profile.corneringGrip *= scale;
            profile.maxSpeed *= scale;
            return profile;
        }
    }

    /// <summary>
    /// Race opponent: follows the <see cref="RacingLine"/> with the <see cref="TrackFollowerDriver"/>'s steering and
    /// speed planning, and reacts to traffic — moving over to pass slower cars ahead, holding back when there is
    /// no room, and keeping clear of cars alongside. Drives through <see cref="IVehicleInputSource"/>, so it has
    /// exactly the same physics as the player.
    /// </summary>
    public sealed class RaceAIDriver : IVehicleInputSource
    {
        const float CarClearance = 2.6f;      // centre-to-centre lateral space needed to pass
        const float LookAheadMetres = 35f;
        const float AlongsideMetres = 5f;
        const float LineChangeRate = 3f;       // metres per second of lateral target movement
        const float RoadEdgeMargin = 1.3f;

        readonly TrackFollowerDriver follower;
        readonly VehicleController vehicle;
        readonly RaceTrack track;
        readonly RacingLine line;
        readonly IReadOnlyList<Racer> field;
        float offset;
        bool initialised;

        public RaceAIDriver(VehicleController vehicle, RaceTrack track, RacingLine line, IReadOnlyList<Racer> field, AIProfile profile)
        {
            this.vehicle = vehicle;
            this.track = track;
            this.line = line;
            this.field = field;
            follower = new TrackFollowerDriver(vehicle, track)
            {
                CorneringGrip = profile.corneringGrip,
                MaxSpeed = profile.maxSpeed,
                BrakingDeceleration = profile.brakingDeceleration
            };
        }

        /// <summary>The racer this driver controls (set once the racer exists).</summary>
        public Racer Self { get; set; }
        /// <summary>Lateral offset currently being driven, metres (positive = right).</summary>
        public float CurrentOffset => offset;
        /// <summary>Reduced after the chequered flag.</summary>
        public float MaxSpeed { get => follower.MaxSpeed; set => follower.MaxSpeed = value; }

        public VehicleInputState ReadInput()
        {
            int sample = Self.Progress.Sample;
            float spacing = track.Length / track.SampleCount;
            Vector3 position = vehicle.transform.position;
            float speed = vehicle.Body.linearVelocity.magnitude;
            float myLateral = track.LateralOffset(position, sample);
            if (!initialised)
            {
                offset = myLateral;
                initialised = true;
            }

            float limit = track.RoadWidth * 0.5f - RoadEdgeMargin;
            float target = line.OffsetAt(sample + Mathf.RoundToInt(15f / spacing));
            float speedCap = float.MaxValue;

            foreach (var other in field)
            {
                if (other == Self || other.Vehicle == null)
                    continue;
                float half = track.Length * 0.5f;
                float gap = Mathf.Repeat(other.Progress.Distance - Self.Progress.Distance + half, track.Length) - half;
                if (gap < -AlongsideMetres || gap > LookAheadMetres)
                    continue;

                float otherLateral = track.LateralOffset(other.Vehicle.transform.position, other.Progress.Sample);
                float otherSpeed = other.Vehicle.Body.linearVelocity.magnitude;

                if (gap <= AlongsideMetres)
                {
                    // Side by side: hold a car's width of space.
                    if (Mathf.Abs(otherLateral - myLateral) < CarClearance)
                        target = otherLateral + (myLateral >= otherLateral ? CarClearance : -CarClearance);
                    continue;
                }

                // Ahead and in our path: pick the side with room, or follow if there is none.
                if (Mathf.Abs(otherLateral - target) >= CarClearance || otherSpeed > speed + 1f)
                    continue;
                float left = otherLateral - CarClearance, right = otherLateral + CarClearance;
                bool leftFits = left > -limit, rightFits = right < limit;
                if (leftFits || rightFits)
                    target = leftFits && (!rightFits || Mathf.Abs(left - myLateral) <= Mathf.Abs(right - myLateral)) ? left : right;
                bool blocked = !leftFits && !rightFits || Mathf.Abs(otherLateral - myLateral) < CarClearance * 0.8f;
                if (blocked && gap < 15f)
                    speedCap = Mathf.Min(speedCap, otherSpeed + (gap - 8f) * 0.3f);
            }

            target = Mathf.Clamp(target, -limit, limit);
            offset = Mathf.MoveTowards(offset, target, LineChangeRate * Time.fixedDeltaTime);
            follower.LateralOffset = offset;

            var input = follower.ReadInput();
            if (speed > speedCap)
            {
                input.Throttle = 0f;
                input.Brake = Mathf.Max(input.Brake, Mathf.Clamp01((speed - speedCap) * 0.3f));
            }
            return input;
        }
    }
}
