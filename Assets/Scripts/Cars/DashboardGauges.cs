using UnityEngine;

namespace RealisticCarRacing.Vehicles
{
    /// <summary>Rev counter and speedometer needles in the cockpit. Needles turn about their local Z axis.</summary>
    public sealed class DashboardGauges : MonoBehaviour
    {
        [SerializeField] VehicleController vehicle;
        [SerializeField] Transform tachometerNeedle;
        [SerializeField] Transform speedometerNeedle;
        [SerializeField] float maxSpeedKmh = 320f;
        [Tooltip("Dial sweep in degrees, centred on 12 o'clock.")]
        [SerializeField] float sweep = 270f;

        public void Configure(VehicleController controller, Transform tachometer, Transform speedometer)
        {
            vehicle = controller;
            tachometerNeedle = tachometer;
            speedometerNeedle = speedometer;
        }

        /// <summary>
        /// Needle angle about local Z for a 0..1 reading. Positive angles turn +Y towards -X, which the driver
        /// (looking along +Z) sees as anticlockwise: 0 = bottom left, 1 = bottom right.
        /// </summary>
        public float NeedleAngle(float reading) => sweep * 0.5f - Mathf.Clamp01(reading) * sweep;

        Renderer needleRenderer;

        void LateUpdate()
        {
            if (vehicle == null || !vehicle.isActiveAndEnabled || tachometerNeedle == null)
                return;
            // The cockpit is only drawn for the in-car camera; other cars' gauges needn't move.
            if (needleRenderer == null)
                needleRenderer = tachometerNeedle.GetComponentInChildren<Renderer>();
            if (needleRenderer != null && !needleRenderer.enabled)
                return;
            float maxRpm = vehicle.Settings.redlineRpm * 1.15f;
            if (tachometerNeedle != null)
                tachometerNeedle.localRotation = Quaternion.Euler(0f, 0f, NeedleAngle(vehicle.EngineRpm / maxRpm));
            if (speedometerNeedle != null)
                speedometerNeedle.localRotation = Quaternion.Euler(0f, 0f, NeedleAngle(vehicle.SpeedKmh / maxSpeedKmh));
        }
    }
}
