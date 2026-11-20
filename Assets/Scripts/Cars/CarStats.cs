using UnityEngine;

namespace RealisticCarRacing.Vehicles
{
    /// <summary>Headline figures derived from a <see cref="VehicleSettings"/>, for the car select and garage screens.</summary>
    public readonly struct CarStats
    {
        const float AirDensity = 1.225f;
        const float Gravity = 9.81f;

        public readonly float powerKw;
        public readonly float topSpeedKmh;
        /// <summary>Lateral grip including downforce at 110 km/h, as a friction multiplier.</summary>
        public readonly float grip;
        public readonly float massKg;

        CarStats(float powerKw, float topSpeedKmh, float grip, float massKg)
        {
            this.powerKw = powerKw;
            this.topSpeedKmh = topSpeedKmh;
            this.grip = grip;
            this.massKg = massKg;
        }

        // 0..1 ratings for the UI bars.
        public float PowerRating => Mathf.InverseLerp(80f, 260f, powerKw);
        public float TopSpeedRating => Mathf.InverseLerp(170f, 290f, topSpeedKmh);
        public float GripRating => Mathf.InverseLerp(1.3f, 2.1f, grip);
        /// <summary>Lighter cars rate higher.</summary>
        public float AgilityRating => Mathf.InverseLerp(1700f, 1050f, massKg);

        public static CarStats From(VehicleSettings s)
        {
            float peakPower = 0f;
            for (float rpm = s.idleRpm; rpm <= s.redlineRpm; rpm += 100f)
                peakPower = Mathf.Max(peakPower, s.torqueCurve.Evaluate(rpm) * rpm * 2f * Mathf.PI / 60f);

            // Drag-limited speed: bisection on power = aero drag + rolling resistance.
            float wheelPower = peakPower * s.drivetrainEfficiency;
            float low = 0f, high = 150f;
            for (int i = 0; i < 40; i++)
            {
                float v = (low + high) * 0.5f;
                float required = (0.5f * AirDensity * s.dragCoefficient * s.frontalArea * v * v + s.rollingResistance * s.mass * Gravity) * v;
                if (required > wheelPower) high = v; else low = v;
            }
            // Gearing-limited speed: redline in top gear.
            float topRatio = s.forwardGearRatios[s.ForwardGearCount - 1] * s.finalDriveRatio;
            float gearLimit = s.redlineRpm / topRatio * 2f * Mathf.PI * s.wheelRadius / 60f;
            float topSpeed = Mathf.Min(low, gearLimit) * 3.6f;

            const float referenceSpeed = 110f / 3.6f;
            float downforceShare = s.downforceCoefficient * referenceSpeed * referenceSpeed / (s.mass * Gravity);
            float grip = s.sidewaysFriction.stiffness * (1f + downforceShare);

            return new CarStats(peakPower / 1000f, topSpeed, grip, s.mass);
        }
    }
}
