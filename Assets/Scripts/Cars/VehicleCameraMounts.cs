using UnityEngine;

namespace RealisticCarRacing.Vehicles
{
    /// <summary>
    /// Where the hood and cockpit cameras sit on this car. Each car model places its own mount transforms
    /// (forward = viewing direction); the camera rig falls back to default offsets if they are missing.
    /// </summary>
    public sealed class VehicleCameraMounts : MonoBehaviour
    {
        [SerializeField] Transform hood;
        [Tooltip("Driver's eye position.")]
        [SerializeField] Transform cockpit;
        [Tooltip("Cabin interior; only rendered while a camera is in the cockpit.")]
        [SerializeField] Transform interior;

        Renderer[] interiorRenderers;

        public Transform Hood => hood;
        public Transform Cockpit => cockpit;
        public bool InteriorVisible { get; private set; } = true;

        public void Configure(Transform hoodMount, Transform cockpitMount, Transform interiorRoot = null)
        {
            hood = hoodMount;
            cockpit = cockpitMount;
            interior = interiorRoot;
            interiorRenderers = null;
            SetInteriorVisible(false);
        }

        /// <summary>Shows or hides the cabin interior. Scripts on it (e.g. the steering wheel) keep running.</summary>
        public void SetInteriorVisible(bool visible)
        {
            if (interior == null)
                return;
            interiorRenderers ??= interior.GetComponentsInChildren<Renderer>(true);
            foreach (var renderer in interiorRenderers)
            {
                if (renderer != null)
                    renderer.enabled = visible;
            }
            InteriorVisible = visible;
        }
    }
}
