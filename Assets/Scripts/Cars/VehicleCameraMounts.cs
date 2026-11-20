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
        [Tooltip("Hidden while the camera is in the cockpit (e.g. the outer glass, which would sit right in front of the lens).")]
        [SerializeField] Renderer[] exteriorOnly = System.Array.Empty<Renderer>();

        Renderer[] interiorRenderers;

        public Transform Hood => hood;
        public Transform Cockpit => cockpit;
        public bool InteriorVisible { get; private set; } = true;

        public void Configure(Transform hoodMount, Transform cockpitMount, Transform interiorRoot = null, Renderer[] exteriorOnlyRenderers = null)
        {
            hood = hoodMount;
            cockpit = cockpitMount;
            interior = interiorRoot;
            exteriorOnly = exteriorOnlyRenderers ?? System.Array.Empty<Renderer>();
            interiorRenderers = null;
            SetInteriorVisible(false);
        }

        /// <summary>Shows or hides the cabin interior. Scripts on it (e.g. the steering wheel) keep running.</summary>
        public void SetInteriorVisible(bool visible)
        {
            foreach (var renderer in exteriorOnly)
            {
                if (renderer != null)
                    renderer.enabled = !visible;
            }
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
