using UnityEngine;

namespace RealisticCarRacing.Vehicles
{
    /// <summary>
    /// Headline figures derived from a <see cref="VehicleSettings"/>, for the garage: power, torque, top speed,
    /// 0-100 km/h and the 0..1 ratings behind the stat bars. Calculated, not hand-entered, so they follow the tuning.
    /// </summary>
    public readonly struct CarStats
    {
        const float AirDensity = 1.225f;
        const float Gravity = 9.81f;
        const float KwToHp = 1.341f;

        public readonly float powerKw;
        public readonly float powerRpm;
        public readonly float torqueNm;
        public readonly float torqueRpm;
        public readonly float topSpeedKmh;
        /// <summary>Standing start to 100 km/h at full throttle, automatic gearbox.</summary>
        public readonly float zeroTo100Seconds;
        /// <summary>Lateral grip including downforce at 110 km/h, as a friction multiplier.</summary>
        public readonly float grip;
        public readonly float massKg;
        public readonly DriveType driveType;
        public readonly int gears;

        CarStats(float powerKw, float powerRpm, float torqueNm, float torqueRpm, float topSpeedKmh, float zeroTo100Seconds, float grip,
            float massKg, DriveType driveType, int gears)
        {
            this.powerKw = powerKw;
            this.powerRpm = powerRpm;
            this.torqueNm = torqueNm;
            this.torqueRpm = torqueRpm;
            this.topSpeedKmh = topSpeedKmh;
            this.zeroTo100Seconds = zeroTo100Seconds;
            this.grip = grip;
            this.massKg = massKg;
            this.driveType = driveType;
            this.gears = gears;
        }

        public float PowerHp => powerKw * KwToHp;
        /// <summary>Horsepower per tonne.</summary>
        public float PowerToWeight => PowerHp / (massKg / 1000f);
        public string DriveLabel => driveType switch
        {
            DriveType.FrontWheelDrive => "Front-wheel drive",
            DriveType.AllWheelDrive => "All-wheel drive",
            _ => "Rear-wheel drive"
        };

        // 0..1 ratings for the UI bars.
        public float PowerRating => Mathf.InverseLerp(80f, 260f, powerKw);
        public float TopSpeedRating => Mathf.InverseLerp(170f, 290f, topSpeedKmh);
        public float GripRating => Mathf.InverseLerp(1.3f, 2.1f, grip);
        /// <summary>Lighter cars rate higher.</summary>
        public float AgilityRating => Mathf.InverseLerp(1700f, 1050f, massKg);

        public static CarStats From(VehicleSettings s)
        {
            float peakPower = 0f, powerRpm = 0f, peakTorque = 0f, torqueRpm = 0f;
            for (float rpm = s.idleRpm; rpm <= s.redlineRpm; rpm += 100f)
            {
                float torque = s.torqueCurve.Evaluate(rpm);
                float power = torque * rpm * 2f * Mathf.PI / 60f;
                if (power > peakPower) { peakPower = power; powerRpm = rpm; }
                if (torque > peakTorque) { peakTorque = torque; torqueRpm = rpm; }
            }

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

            return new CarStats(peakPower / 1000f, powerRpm, peakTorque, torqueRpm, topSpeed, ZeroTo100(s), grip, s.mass, s.driveType,
                s.ForwardGearCount);
        }

        /// <summary>
        /// Straight-line launch simulated the way <see cref="VehicleController"/> drives: clutch slipping at the
        /// launch RPM, torque through the gears, the automatic gearbox's shift rules with the drive cut while
        /// shifting, traction limited by the driven tyres' load, and aero drag plus rolling resistance.
        /// <para>
        /// Three tyre effects were measured on the game's WheelColliders (traces in <c>CarSpecsTests</c>) and are
        /// modelled as observed: driven tyres slip in proportion to their load (so the wheels, and the gearbox
        /// reading them, run ahead of the road and shift up early); from a standstill they spin until the first
        /// gear change, which can make the gearbox shift up and back down; and under acceleration front-wheel-drive
        /// cars lose much of their front load, rear-wheel-drive cars gain little. <c>CarSpecsTests</c> checks the
        /// result against timed runs for every car and setup.
        /// </para>
        /// </summary>
        public static float ZeroTo100(VehicleSettings s)
        {
            // The cars' layout (TestVehicleFactory): 47 % of the weight on the front axle at rest.
            const float target = 100f / 3.6f, dt = 0.005f, frontStatic = 0.472f;
            // Measured load moved per g of acceleration (share of the car's weight): large off the front driven
            // axle, small onto the rear one (the suspension's reaction to the drive force differs).
            const float frontTransferPerG = 0.42f, rearTransferPerG = 0.05f;
            // Measured: slip at a given share of peak grip is about 0.6x the straight line to the curve's extremum.
            const float slipScale = 0.6f;
            float radius = s.wheelRadius;
            float effectiveMass = s.mass + 4f * 0.5f * s.wheelMass; // + spinning wheels (discs: I = m r² / 2)
            float mu = s.forwardFriction.extremumValue * s.forwardFriction.stiffness;
            float v = 0f, t = 0f, accel = 0f, slip = 0f, shiftTimer = 0f, sinceShift = s.minTimeBetweenShifts, spinTime = 0f;
            bool spinning = true; // decided on the first step
            int gear = 1;
            while (v < target && t < 30f)
            {
                float ratio = s.forwardGearRatios[gear - 1] * s.finalDriveRatio;
                float drivetrainRpm = v * (1f + slip) / (2f * Mathf.PI * radius) * 60f * ratio;
                if (spinning)
                    drivetrainRpm = Mathf.Max(drivetrainRpm, Mathf.Lerp(s.launchRpm, s.redlineRpm, spinTime / 0.4f)); // spinning wheels flare

                // Same rules as VehicleTransmission.UpdateAutomatic.
                if (shiftTimer <= 0f && sinceShift >= s.minTimeBetweenShifts)
                {
                    int next = drivetrainRpm > s.upshiftRpm && gear < s.ForwardGearCount ? gear + 1
                        : drivetrainRpm < s.downshiftRpm && gear > 1 ? gear - 1 : gear;
                    if (next != gear)
                    {
                        gear = next;
                        shiftTimer = s.shiftDuration;
                        sinceShift = 0f;
                        spinning = false; // the drive is cut while shifting: the tyres catch up with the road
                        continue;
                    }
                }

                float rpm = Mathf.Clamp(Mathf.Max(drivetrainRpm, s.launchRpm), s.idleRpm, s.redlineRpm);
                float drive = shiftTimer > 0f ? 0f : s.torqueCurve.Evaluate(rpm) * ratio * s.drivetrainEfficiency / radius;
                float g = accel / Gravity;
                float drivenShare = s.driveType switch
                {
                    DriveType.AllWheelDrive => 1f,
                    DriveType.FrontWheelDrive => frontStatic - frontTransferPerG * g,
                    _ => 1f - frontStatic + rearTransferPerG * g
                };
                float grip = mu * drivenShare * s.mass * Gravity;
                float slidingGrip = grip * s.forwardFriction.asymptoteValue / s.forwardFriction.extremumValue;
                if (t == 0f)
                    spinning = drive > slidingGrip; // a tyre at rest has no grip reserve
                spinTime = spinning ? spinTime + dt : 0f;
                if (spinning)
                {
                    slip = s.forwardFriction.asymptoteSlip;
                    drive = slidingGrip;
                }
                else
                {
                    drive = Mathf.Min(drive, grip); // over the limit the tyre runs at its peak
                    slip = slipScale * s.forwardFriction.extremumSlip * drive / grip;
                }
                float resistance = 0.5f * AirDensity * s.dragCoefficient * s.frontalArea * v * v + s.rollingResistance * s.mass * Gravity;
                accel = Mathf.Max(0f, (drive - resistance) / effectiveMass);
                v += accel * dt;
                t += dt;
                shiftTimer -= dt;
                sinceShift += dt;
            }
            return t;
        }
    }
}
