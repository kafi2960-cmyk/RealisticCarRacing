using UnityEngine;

namespace RealisticCarRacing.Vehicles
{
    /// <summary>
    /// A selectable car: its physics tuning, garage paint options and (placeholder) model materials.
    /// Create via Assets &gt; Create &gt; RealisticCarRacing &gt; Car Definition and list it in the GameCatalog.
    /// </summary>
    [CreateAssetMenu(fileName = "Car", menuName = "RealisticCarRacing/Car Definition")]
    public sealed class CarDefinition : ScriptableObject
    {
        [Tooltip("Stable identifier used in save data. Don't change it after release.")]
        public string id = "car";
        public string displayName = "Car";
        [TextArea] public string description;
        public VehicleSettings settings;
        [Tooltip("Paint colours offered in the garage; the first is the default.")]
        public Color[] paints = { new Color(0.75f, 0.1f, 0.08f) };

        [Header("Placeholder model materials")]
        [Tooltip("Body material; a copy is tinted with the chosen paint.")]
        public Material bodyMaterial;
        public Material cabinMaterial;
        public Material tyreMaterial;
        public Material markerMaterial;

        public Color Paint(int index) => paints.Length == 0 ? Color.white : paints[Mathf.Abs(index) % paints.Length];
    }
}
