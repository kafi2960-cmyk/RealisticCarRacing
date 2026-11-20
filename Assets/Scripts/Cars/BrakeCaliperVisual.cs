using UnityEngine;

namespace RealisticCarRacing.Vehicles
{
    /// <summary>
    /// Keeps a brake caliper on its wheel: it moves with the suspension and turns with the steering, but does not
    /// spin with the wheel. Runs after <see cref="VehicleController"/> has placed the wheel visuals.
    /// </summary>
    public sealed class BrakeCaliperVisual : MonoBehaviour
    {
        [SerializeField] WheelCollider wheelCollider;
        [SerializeField] Transform wheelVisual;

        public void Configure(WheelCollider collider, Transform visual)
        {
            wheelCollider = collider;
            wheelVisual = visual;
        }

        void LateUpdate()
        {
            if (wheelCollider == null || wheelVisual == null)
                return;
            transform.SetPositionAndRotation(wheelVisual.position,
                wheelCollider.transform.rotation * Quaternion.Euler(0f, wheelCollider.steerAngle, 0f));
        }
    }
}
