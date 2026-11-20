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
        [Tooltip("Names shown for the paints, in the same order.")]
        public string[] paintNames = System.Array.Empty<string>();

        [Header("Placeholder model materials")]
        [Tooltip("Body material; a copy is tinted with the chosen paint.")]
        public Material bodyMaterial;
        public Material cabinMaterial;
        public Material tyreMaterial;
        public Material markerMaterial;
        [Tooltip("Wheels, brakes, glass, lamps and cabin details. Empty = all-primitive placeholder.")]
        public CarVisualKit visualKit;

        public Color Paint(int index) => paints.Length == 0 ? Color.white : paints[Mathf.Abs(index) % paints.Length];

        public string PaintName(int index)
        {
            if (paints.Length == 0)
                return string.Empty;
            index = Mathf.Abs(index) % paints.Length;
            return index < paintNames.Length && !string.IsNullOrEmpty(paintNames[index]) ? paintNames[index] : $"Paint {index + 1}";
        }

        public int WheelDesignCount => visualKit != null ? visualKit.WheelDesignCount : 1;
    }
}
