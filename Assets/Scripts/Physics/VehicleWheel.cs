using System;
using UnityEngine;

namespace RealisticCarRacing.Vehicles
{
    public enum Axle
    {
        Front,
        Rear
    }

    /// <summary>
    /// One corner of the car: the physics WheelCollider plus the visual wheel it drives.
    /// The visual's axes must match the collider's (X = axle, Z = forward); for imported meshes
    /// with a different orientation, parent the mesh under an empty that does.
    /// </summary>
    [Serializable]
    public sealed class VehicleWheel
    {
        public WheelCollider wheelCollider;
        [Tooltip("Visual wheel root. Its position and rotation (spin + steering) are copied from the collider every frame.")]
        public Transform visual;
        public Axle axle;
        public bool isLeft;

        public bool IsFront => axle == Axle.Front;

        public void SyncVisual()
        {
            if (visual == null)
                return;
            wheelCollider.GetWorldPose(out Vector3 position, out Quaternion rotation);
            visual.SetPositionAndRotation(position, rotation);
        }

        /// <summary>Suspension extension: 0 = fully compressed, 1 = fully extended.</summary>
        /// <returns>False (with travel 1) when the wheel is in the air.</returns>
        public bool TryGetSuspensionTravel(out float travel)
        {
            if (!wheelCollider.GetGroundHit(out WheelHit hit))
            {
                travel = 1f;
                return false;
            }

            Vector3 local = wheelCollider.transform.InverseTransformPoint(hit.point) - wheelCollider.center;
            travel = (-local.y - wheelCollider.radius) / wheelCollider.suspensionDistance;
            return true;
        }
    }
}
