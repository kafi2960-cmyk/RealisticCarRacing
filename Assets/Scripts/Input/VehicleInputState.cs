using UnityEngine;

namespace RealisticCarRacing.Controls
{
    /// <summary>
    /// Driver commands for one physics step. Produced by any <see cref="IVehicleInputSource"/>
    /// (player devices, AI, network, replays) and consumed by the vehicle physics, which never
    /// reads input devices itself.
    /// </summary>
    public struct VehicleInputState
    {
        /// <summary>Accelerator pedal, 0..1.</summary>
        public float Throttle;

        /// <summary>Brake pedal, 0..1. With the automatic gearbox, holding it at a standstill selects reverse.</summary>
        public float Brake;

        /// <summary>Steering, -1 (full left) .. 1 (full right).</summary>
        public float Steer;

        public bool Handbrake;

        // One-shot requests: true only in the read that follows the button press.
        public bool ShiftUp;
        public bool ShiftDown;
        public bool ToggleTransmission;
        public bool ResetVehicle;

        public VehicleInputState Clamped()
        {
            var clamped = this;
            clamped.Throttle = Mathf.Clamp01(Throttle);
            clamped.Brake = Mathf.Clamp01(Brake);
            clamped.Steer = Mathf.Clamp(Steer, -1f, 1f);
            return clamped;
        }
    }

    public interface IVehicleInputSource
    {
        /// <summary>
        /// Returns the current driver commands. Called once per physics step; one-shot requests
        /// (shifts, toggles, reset) are cleared by the read.
        /// </summary>
        VehicleInputState ReadInput();
    }
}
