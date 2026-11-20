using UnityEngine;

namespace RealisticCarRacing.Vehicles
{
    /// <summary>
    /// Engine model. With the clutch engaged, RPM follows the driven wheels through the gearbox;
    /// below the launch RPM the clutch is treated as slipping so the car can pull away.
    /// Torque comes from the settings' torque curve, with a rev limiter and engine braking.
    /// </summary>
    public sealed class VehicleEngine
    {
        // Torque is cut this close to the redline.
        const float RevLimiterBand = 50f;

        readonly VehicleSettings settings;

        public float Rpm { get; private set; }
        public bool IsRevLimited { get; private set; }

        public VehicleEngine(VehicleSettings settings)
        {
            this.settings = settings;
            Rpm = settings.idleRpm;
        }

        /// <param name="throttle">Accelerator, 0..1.</param>
        /// <param name="drivetrainRpm">Engine speed implied by the driven wheels in the current gear.</param>
        /// <param name="clutchEngaged">False in neutral and while a shift is in progress.</param>
        /// <param name="shifting">True while the gearbox is changing gear.</param>
        /// <returns>Crankshaft torque in N·m; negative while engine braking; zero with the clutch open.</returns>
        public float Update(float throttle, float drivetrainRpm, bool clutchEngaged, bool shifting, float deltaTime)
        {
            float idle = settings.idleRpm;
            float redline = settings.redlineRpm;

            float targetRpm;
            if (shifting)
                targetRpm = drivetrainRpm; // rev-match onto the incoming gear
            else if (!clutchEngaged)
                targetRpm = Mathf.Lerp(idle, redline, throttle); // free revving in neutral
            else
                targetRpm = Mathf.Max(drivetrainRpm, Mathf.Lerp(idle, settings.launchRpm, throttle));

            targetRpm = Mathf.Clamp(targetRpm, idle, redline);
            Rpm = Mathf.Lerp(Rpm, targetRpm, 1f - Mathf.Exp(-settings.rpmResponse * deltaTime));
            IsRevLimited = Rpm >= redline - RevLimiterBand;

            if (!clutchEngaged)
                return 0f;

            float driveTorque = IsRevLimited ? 0f : settings.torqueCurve.Evaluate(Rpm) * throttle;
            float engineBraking = settings.engineBrakeTorque * Mathf.InverseLerp(idle, redline, Rpm) * (1f - throttle);
            return driveTorque - engineBraking;
        }
    }
}
