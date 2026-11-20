using RealisticCarRacing.Controls;
using RealisticCarRacing.Tracks;
using RealisticCarRacing.Vehicles;
using UnityEngine;

namespace RealisticCarRacing.AI
{
    /// <summary>
    /// Basic autonomous driver: pure-pursuit steering along the track centreline (optionally offset
    /// sideways) and a speed plan that brakes early enough for the tightest corner ahead.
    /// It drives through the same <see cref="IVehicleInputSource"/> interface as the player, so it
    /// is subject to the same physics. Used by the circuit tests; the starting point for race AI.
    /// </summary>
    public sealed class TrackFollowerDriver : IVehicleInputSource
    {
        const float Gravity = 9.81f;

        readonly VehicleController vehicle;
        readonly RaceTrack track;
        int closestSample = -1;

        public TrackFollowerDriver(VehicleController vehicle, RaceTrack track)
        {
            this.vehicle = vehicle;
            this.track = track;
        }

        /// <summary>Sideways offset from the centreline to follow, metres (positive = right).</summary>
        public float LateralOffset { get; set; }
        /// <summary>Usable lateral grip in g. Lower is more cautious.</summary>
        public float CorneringGrip { get; set; } = 0.8f;
        /// <summary>Planned braking deceleration in m/s².</summary>
        public float BrakingDeceleration { get; set; } = 5f;
        public float MaxSpeed { get; set; } = 60f;

        public int ClosestSample => closestSample;

        public VehicleInputState ReadInput()
        {
            Vector3 position = vehicle.transform.position;
            closestSample = track.FindClosestSample(position, closestSample);
            float speed = Mathf.Max(0f, vehicle.ForwardSpeed);
            float spacing = track.Length / track.SampleCount;

            float lookAhead = Mathf.Clamp(speed * 0.75f, 8f, 30f);
            var target = track.GetSample(closestSample + Mathf.CeilToInt(lookAhead / spacing));
            Vector3 targetPoint = target.position + target.right * LateralOffset;

            // Pure pursuit: the arc through the target point gives the curvature to steer for.
            Vector3 local = vehicle.transform.InverseTransformPoint(targetPoint);
            float curvature = 2f * local.x / Mathf.Max(local.sqrMagnitude, 1f);
            float steerAngle = Mathf.Atan(curvature * vehicle.Wheelbase) * Mathf.Rad2Deg;
            float steer = Mathf.Clamp(steerAngle / vehicle.GetSteerLimit(vehicle.ForwardSpeed), -1f, 1f);

            float targetSpeed = PlanSpeed(spacing);
            float speedError = targetSpeed - speed;
            // Traction management: ease off as the body slip angle grows, as a driver would.
            Vector3 velocity = Vector3.ProjectOnPlane(vehicle.Body.linearVelocity, Vector3.up);
            float slipAngle = speed > 3f ? Mathf.Abs(Vector3.SignedAngle(vehicle.transform.forward, velocity, Vector3.up)) : 0f;
            float traction = Mathf.Clamp01(1f - (slipAngle - 2f) / 5f);
            return new VehicleInputState
            {
                Steer = steer,
                Throttle = Mathf.Clamp01(speedError * 0.4f) * traction,
                Brake = Mathf.Clamp01(-speedError * 0.3f)
            };
        }

        /// <summary>Highest speed now that still allows slowing to each upcoming corner's limit in time.</summary>
        float PlanSpeed(float spacing)
        {
            float target = MaxSpeed;
            int horizon = Mathf.CeilToInt(250f / spacing);
            for (int i = 0; i < horizon; i++)
            {
                // Curvature of the offset line actually driven: tighter on the inside of a corner, wider outside.
                float centreCurvature = track.GetSample(closestSample + i).curvature;
                float curvature = Mathf.Abs(centreCurvature / Mathf.Max(0.2f, 1f + centreCurvature * LateralOffset));
                if (curvature < 1e-4f)
                    continue;
                float cornerSpeed = Mathf.Sqrt(CorneringGrip * Gravity / curvature);
                float distance = i * spacing;
                target = Mathf.Min(target, Mathf.Sqrt(cornerSpeed * cornerSpeed + 2f * BrakingDeceleration * distance));
            }
            return target;
        }
    }
}
