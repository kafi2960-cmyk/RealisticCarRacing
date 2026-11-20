using UnityEngine;

namespace RealisticCarRacing.Vehicles
{
    /// <summary>
    /// Gearbox with reverse, neutral and N forward gears. In manual mode gears change only on request;
    /// in automatic mode <see cref="UpdateAutomatic"/> picks the forward gear from drivetrain RPM.
    /// Choosing between drive and reverse is left to the caller, which knows what the pedals mean.
    /// </summary>
    public sealed class VehicleTransmission
    {
        public const int Reverse = -1;
        public const int Neutral = 0;

        readonly VehicleSettings settings;
        float shiftTimer;
        float timeSinceShift = float.MaxValue;

        public VehicleTransmission(VehicleSettings settings)
        {
            this.settings = settings;
            Mode = settings.defaultTransmission;
        }

        /// <summary>-1 reverse, 0 neutral, 1..<see cref="ForwardGearCount"/> forward.</summary>
        public int Gear { get; private set; } = 1;

        public TransmissionMode Mode { get; set; }
        public bool IsShifting => shiftTimer > 0f;
        public int ForwardGearCount => settings.ForwardGearCount;
        public string GearLabel => Gear == Reverse ? "R" : Gear == Neutral ? "N" : ForwardLabels[Mathf.Min(Gear, ForwardLabels.Length - 1)];

        // Shared strings so reading the gear every frame (HUD) never allocates.
        static readonly string[] ForwardLabels = { "N", "1", "2", "3", "4", "5", "6", "7", "8", "9", "10" };

        /// <summary>Engine-to-wheel ratio including the final drive. Negative in reverse, zero in neutral.</summary>
        public float OverallRatio => GearRatio(Gear) * settings.finalDriveRatio;

        public float GearRatio(int gear)
        {
            if (gear > 0)
                return settings.forwardGearRatios[gear - 1];
            return gear == Reverse ? -settings.reverseGearRatio : 0f;
        }

        public void Tick(float deltaTime)
        {
            if (shiftTimer > 0f)
                shiftTimer -= deltaTime;
            timeSinceShift += deltaTime;
        }

        public bool ShiftUp(float forwardSpeed) => TryShiftTo(Gear + 1, forwardSpeed);

        public bool ShiftDown(float forwardSpeed) => TryShiftTo(Gear - 1, forwardSpeed);

        /// <param name="forwardSpeed">Car speed along its forward axis in m/s (negative when rolling backwards).</param>
        /// <returns>True if the shift started.</returns>
        public bool TryShiftTo(int gear, float forwardSpeed)
        {
            if (gear == Gear || gear < Reverse || gear > ForwardGearCount)
                return false;

            // Selecting a direction against the car's motion would wreck a real gearbox,
            // so it is only allowed close to a standstill.
            if (gear == Reverse && forwardSpeed > settings.directionChangeSpeed)
                return false;
            if (gear >= 1 && forwardSpeed < -settings.directionChangeSpeed)
                return false;

            Gear = gear;
            shiftTimer = gear == Neutral ? 0f : settings.shiftDuration;
            timeSinceShift = 0f;
            return true;
        }

        /// <param name="drivetrainRpm">Engine speed implied by the driven wheels in the current gear.</param>
        public void UpdateAutomatic(float drivetrainRpm)
        {
            if (Mode != TransmissionMode.Automatic || IsShifting || Gear < 1)
                return;
            if (timeSinceShift < settings.minTimeBetweenShifts)
                return;

            if (drivetrainRpm > settings.upshiftRpm && Gear < ForwardGearCount)
                TryShiftTo(Gear + 1, 0f);
            else if (drivetrainRpm < settings.downshiftRpm && Gear > 1)
                TryShiftTo(Gear - 1, 0f);
        }
    }
}
