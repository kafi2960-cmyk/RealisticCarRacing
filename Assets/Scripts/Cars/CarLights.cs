using UnityEngine;

namespace RealisticCarRacing.Vehicles
{
    /// <summary>
    /// Lamp states from the driving state: brake lights while the brake pedal is pressed, reverse lights in reverse
    /// gear. Lamps switch by swapping shared materials (no per-car material copies, SRP Batcher friendly). Headlight
    /// beams are real spot lights; they are only used on cars that are given beams (the player's) and on quality
    /// levels with per-pixel lights, because every real-time light costs on mobile GPUs.
    /// </summary>
    public sealed class CarLights : MonoBehaviour
    {
        const float BrakeThreshold = 0.05f;

        [SerializeField] VehicleController vehicle;
        [SerializeField] Renderer[] brakeLamps = System.Array.Empty<Renderer>();
        [SerializeField] Renderer[] reverseLamps = System.Array.Empty<Renderer>();
        [SerializeField] Material tailLight, brakeLight, reverseOff, reverseOn;
        [SerializeField] Light[] beams = System.Array.Empty<Light>();
        [Tooltip("Lowest quality level index that shows the headlight beams.")]
        [SerializeField] int beamsFromQualityLevel = 1;

        bool initialized;

        public bool BrakeLightsOn { get; private set; }
        public bool ReverseLightsOn { get; private set; }
        public bool BeamsOn { get; private set; }
        public System.Collections.Generic.IReadOnlyList<Renderer> BrakeLamps => brakeLamps;
        public System.Collections.Generic.IReadOnlyList<Renderer> ReverseLamps => reverseLamps;

        public void Configure(VehicleController controller, Renderer[] brakeLampRenderers, Renderer[] reverseLampRenderers,
            Material tail, Material brake, Material reverseOffMaterial, Material reverseOnMaterial, Light[] headlightBeams)
        {
            vehicle = controller;
            brakeLamps = brakeLampRenderers;
            reverseLamps = reverseLampRenderers;
            tailLight = tail;
            brakeLight = brake;
            reverseOff = reverseOffMaterial;
            reverseOn = reverseOnMaterial;
            beams = headlightBeams ?? System.Array.Empty<Light>();
        }

        void OnEnable()
        {
            QualitySettings.activeQualityLevelChanged += OnQualityLevelChanged;
            UpdateBeams();
            Apply(force: true);
        }

        void OnDisable() => QualitySettings.activeQualityLevelChanged -= OnQualityLevelChanged;

        void OnQualityLevelChanged(int previous, int current) => UpdateBeams();

        void UpdateBeams()
        {
            BeamsOn = beams.Length > 0 && QualitySettings.GetQualityLevel() >= beamsFromQualityLevel;
            foreach (var beam in beams)
            {
                if (beam != null)
                    beam.enabled = BeamsOn;
            }
        }

        void Update() => Apply(force: false);

        void Apply(bool force)
        {
            bool braking = vehicle != null && vehicle.isActiveAndEnabled && vehicle.BrakeInput > BrakeThreshold;
            bool reversing = vehicle != null && vehicle.isActiveAndEnabled && vehicle.Gear == VehicleTransmission.Reverse;
            if (force || !initialized || braking != BrakeLightsOn)
                SetMaterial(brakeLamps, braking ? brakeLight : tailLight);
            if (force || !initialized || reversing != ReverseLightsOn)
                SetMaterial(reverseLamps, reversing ? reverseOn : reverseOff);
            BrakeLightsOn = braking;
            ReverseLightsOn = reversing;
            initialized = true;
        }

        static void SetMaterial(Renderer[] renderers, Material material)
        {
            if (material == null)
                return;
            foreach (var renderer in renderers)
            {
                if (renderer != null)
                    renderer.sharedMaterial = material;
            }
        }
    }
}
