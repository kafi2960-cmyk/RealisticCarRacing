using UnityEngine;

namespace RealisticCarRacing.Vehicles
{
    /// <summary>Turns the in-cabin steering wheel (about its local Z axis) to match the front wheels.</summary>
    public sealed class SteeringWheelVisual : MonoBehaviour
    {
        [SerializeField] VehicleController vehicle;
        [Tooltip("Steering wheel degrees per degree of road-wheel angle.")]
        [SerializeField] float steeringRatio = 13f;
        [SerializeField] float maxRotation = 450f;

        Quaternion restRotation;

        public void Configure(VehicleController controller) => vehicle = controller;

        void Awake() => restRotation = transform.localRotation;

        void LateUpdate()
        {
            if (vehicle == null)
                return;
            float angle = Mathf.Clamp(vehicle.SteerAngle * steeringRatio, -maxRotation, maxRotation);
            // Steering right turns the rim clockwise as seen by the driver (looking along +Z).
            transform.localRotation = restRotation * Quaternion.AngleAxis(-angle, Vector3.forward);
        }
    }
}
