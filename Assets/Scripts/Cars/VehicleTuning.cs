using UnityEngine;

namespace RealisticCarRacing.Vehicles
{
    public enum TuningPreset
    {
        Balanced,
        Grip,
        TopSpeed
    }

    /// <summary>Garage setups: each produces a modified copy of a car's settings, leaving the asset untouched.</summary>
    public static class VehicleTuning
    {
        public static string Title(TuningPreset preset) => preset switch
        {
            TuningPreset.Grip => "HIGH GRIP",
            TuningPreset.TopSpeed => "TOP SPEED",
            _ => "BALANCED"
        };

        public static string Describe(TuningPreset preset) => preset switch
        {
            TuningPreset.Grip => "More wing and stickier tyres, shorter gearing. Faster through corners, lower top speed.",
            TuningPreset.TopSpeed => "Less wing and longer gearing. Higher top speed, less grip in corners.",
            _ => "Factory setup: a compromise between cornering and straight-line speed."
        };

        /// <summary>A new settings instance with the preset applied; the caller owns (and should destroy) it.</summary>
        public static VehicleSettings CreateTuned(VehicleSettings source, TuningPreset preset)
        {
            var tuned = Object.Instantiate(source);
            tuned.name = $"{source.name} ({preset})";
            switch (preset)
            {
                case TuningPreset.Grip:
                    tuned.downforceCoefficient *= 1.6f;
                    tuned.dragCoefficient *= 1.06f;
                    tuned.sidewaysFriction.stiffness *= 1.08f;
                    tuned.forwardFriction.stiffness *= 1.05f;
                    tuned.finalDriveRatio *= 1.06f;
                    break;
                case TuningPreset.TopSpeed:
                    tuned.downforceCoefficient *= 0.5f;
                    tuned.dragCoefficient *= 0.93f;
                    tuned.sidewaysFriction.stiffness *= 0.97f;
                    tuned.finalDriveRatio *= 0.92f;
                    break;
            }
            return tuned;
        }
    }
}
